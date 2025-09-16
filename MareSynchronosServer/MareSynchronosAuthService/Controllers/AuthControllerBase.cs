using MareSynchronosAuthService.Authentication;
using MareSynchronosAuthService.Services;
using MareSynchronosShared.Data;
using MareSynchronosShared.Models;
using MareSynchronosShared.Services;
using MareSynchronosShared.Utils;
using MareSynchronosShared.Utils.Configuration;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using StackExchange.Redis;
using System.Globalization;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace MareSynchronosAuthService.Controllers;

public abstract class AuthControllerBase : Controller
{
    protected readonly ILogger Logger;
    protected readonly IHttpContextAccessor HttpAccessor;
    protected readonly IConfigurationService<AuthServiceConfiguration> Configuration;
    protected readonly IDbContextFactory<MareDbContext> MareDbContextFactory;
    protected readonly SecretKeyAuthenticatorService SecretKeyAuthenticatorService;
    private readonly IDatabase _redis;
    private readonly GeoIPService _geoIPProvider;

    protected AuthControllerBase(ILogger logger,
    IHttpContextAccessor accessor, IDbContextFactory<MareDbContext> mareDbContextFactory,
    SecretKeyAuthenticatorService secretKeyAuthenticatorService,
    IConfigurationService<AuthServiceConfiguration> configuration,
    IDatabase redisDb, GeoIPService geoIPProvider)
    {
        Logger = logger;
        HttpAccessor = accessor;
        _redis = redisDb;
        _geoIPProvider = geoIPProvider;
        MareDbContextFactory = mareDbContextFactory;
        SecretKeyAuthenticatorService = secretKeyAuthenticatorService;
        Configuration = configuration;
    }

    protected async Task<IActionResult> GenericAuthResponse(MareDbContext dbContext, string charaIdent, SecretKeyAuthReply authResult, string nameWithWorld = "", string? machineId = null, string? aidHash = null)
    {
        if (aidHash is null)
        {
            Logger.LogWarning("Authenticate:AidHash:{id}:{ident}", authResult.Uid, aidHash);
            return Unauthorized("令牌状态错误, 请联系管理员.");
        }

        if (await IsIdentBanned(dbContext, aidHash))
        {
            Logger.LogWarning("Authenticate:IDENTBAN:{id}:{ident}", authResult.Uid, charaIdent);
            return Unauthorized("你的FF账号被禁止使用本服务.");
        }

        if (machineId is not null && await IsIdentBanned(dbContext, machineId))
        {
            Logger.LogWarning("Authenticate:MachineBAN:{id}:{ident}", authResult.Uid, machineId);
            return Unauthorized("你被禁止使用本服务.");
        }

        if (!authResult.Success && !authResult.TempBan)
        {
            Logger.LogWarning("Authenticate:INVALID:{id}:{ident}", authResult?.Uid ?? "NOUID", charaIdent);
            return Unauthorized("密钥无效. 请确认你的Mare账户存在或尝试重新关联DC账户.");
        }
        if (!authResult.Success && authResult.TempBan)
        {
            Logger.LogWarning("Authenticate:TEMPBAN:{id}:{ident}", authResult.Uid ?? "NOUID", charaIdent);
            return Unauthorized("失败次数过多, 你已被暂时封禁. 请检查你的密钥设置并在5分钟后重试.");
        }

        if (authResult.Permaban || authResult.MarkedForBan)
        {
            await EnsureBan(authResult.Uid!, authResult.PrimaryUid, aidHash);

            if (!string.IsNullOrEmpty(machineId))
            {
                await EnsureBan(authResult.Uid!, authResult.PrimaryUid, machineId, true);
            }

            Logger.LogWarning("Authenticate:UIDBAN:{id}:{ident}", authResult.Uid, charaIdent);
            return Unauthorized("你的Mare账号已被封禁.");
        }

        var existingIdent = await _redis.StringGetAsync("UID:" + authResult.Uid);
        if (!string.IsNullOrEmpty(existingIdent))
        {
            Logger.LogWarning("Authenticate:DUPLICATE:{id}:{ident}", authResult.Uid, charaIdent);
            return Unauthorized("该Mare账号已经登录. 将在60秒后尝试重新登录. 如果依旧无法登录, 请重启游戏.");
        }

        Logger.LogInformation("Authenticate:SUCCESS:{id}:{ident}", authResult.Uid, charaIdent);
        return await CreateJwtFromId(authResult.Uid!, charaIdent, authResult.Alias ?? string.Empty, nameWithWorld, aidHash);
    }

    protected JwtSecurityToken CreateJwt(IEnumerable<Claim> authClaims)
    {
        var authSigningKey = new SymmetricSecurityKey(Encoding.ASCII.GetBytes(Configuration.GetValue<string>(nameof(MareConfigurationBase.Jwt))));

        var token = new SecurityTokenDescriptor()
        {
            Subject = new ClaimsIdentity(authClaims),
            SigningCredentials = new SigningCredentials(authSigningKey, SecurityAlgorithms.HmacSha256Signature),
            Expires = new(long.Parse(authClaims.First(f => string.Equals(f.Type, MareClaimTypes.Expires, StringComparison.Ordinal)).Value!, CultureInfo.InvariantCulture), DateTimeKind.Utc),
        };

        var handler = new JwtSecurityTokenHandler();
        return handler.CreateJwtSecurityToken(token);
    }

    protected async Task<IActionResult> CreateJwtFromId(string uid, string charaIdent, string alias, string nameWithWorld, string aidHash)
    {
        var token = CreateJwt(new List<Claim>()
        {
            new Claim(MareClaimTypes.Uid, uid),
            new Claim(MareClaimTypes.CharaIdent, charaIdent),
            new Claim(MareClaimTypes.Alias, alias),
            new Claim(MareClaimTypes.Expires, DateTime.UtcNow.AddHours(6).Ticks.ToString(CultureInfo.InvariantCulture)),
            new Claim(MareClaimTypes.Continent, await _geoIPProvider.GetCountryFromIP(HttpAccessor)),
            new Claim(MareClaimTypes.NameWithWorld, nameWithWorld),
            new Claim(MareClaimTypes.AidHash, aidHash)
        });

        return Content(token.RawData);
    }

    protected async Task EnsureBan(string uid, string? primaryUid, string charaIdent, bool isMid = false)
    {
        using var dbContext = await MareDbContextFactory.CreateDbContextAsync();
        if (!dbContext.BannedUsers.Any(c => c.CharacterIdentification == charaIdent))
        {
            if (!isMid)
            {
                dbContext.BannedUsers.Add(new Banned()
                {
                    CharacterIdentification = charaIdent,
                    Reason = "自动封禁 (" + uid + ")",
                });
            }
            else
            {
                dbContext.BannedUsers.Add(new Banned()
                {
                    CharacterIdentification = charaIdent,
                    Reason = "MID封禁 (" + uid + ")",
                });
            }

        }

        var uidToLookFor = primaryUid ?? uid;

        var primaryUserAuth = await dbContext.Auth.FirstAsync(f => f.UserUID == uidToLookFor);
        primaryUserAuth.MarkForBan = false;
        primaryUserAuth.IsBanned = true;

        var lodestone = await dbContext.LodeStoneAuth.Include(a => a.User).FirstOrDefaultAsync(c => c.User.UID == uidToLookFor);

        if (lodestone != null)
        {
            if (!dbContext.BannedRegistrations.Any(c => c.DiscordIdOrLodestoneAuth == lodestone.HashedLodestoneId))
            {
                dbContext.BannedRegistrations.Add(new BannedRegistrations()
                {
                    DiscordIdOrLodestoneAuth = lodestone.HashedLodestoneId,
                });
            }
            if (!dbContext.BannedRegistrations.Any(c => c.DiscordIdOrLodestoneAuth == lodestone.DiscordId.ToString()))
            {
                dbContext.BannedRegistrations.Add(new BannedRegistrations()
                {
                    DiscordIdOrLodestoneAuth = lodestone.DiscordId.ToString(),
                });
            }
        }
        
        //添加所有主UID下的CharaId
        if (primaryUserAuth.CharaIds is not null)
        {
            foreach (var id in primaryUserAuth.CharaIds)
            {
                if (!dbContext.BannedUsers.Any(c => c.CharacterIdentification == id))
                {
                    dbContext.BannedUsers.Add(new Banned()
                    {
                        CharacterIdentification = id,
                        Reason = "角色封禁 (" + uid + ")",
                    });
                }
            }
        }
        
        await dbContext.SaveChangesAsync();
    }

    protected async Task<bool> IsIdentBanned(MareDbContext dbContext, string charaIdent)
    {
        return await dbContext.BannedUsers.AsNoTracking().AnyAsync(u => u.CharacterIdentification == charaIdent).ConfigureAwait(false);
    }
}