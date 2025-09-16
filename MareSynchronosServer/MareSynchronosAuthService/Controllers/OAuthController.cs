using MareSynchronos.API.Routes;
using MareSynchronosAuthService.Services;
using MareSynchronosShared;
using MareSynchronosShared.Data;
using MareSynchronosShared.Services;
using MareSynchronosShared.Utils;
using MareSynchronosShared.Utils.Configuration;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using StackExchange.Redis;
using System.Collections.Concurrent;
using System.Globalization;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text.Json;
using System.Web;
using MareSynchronosShared.Models;

namespace MareSynchronosAuthService.Controllers;

[Route(MareAuth.OAuth)]
public class OAuthController : AuthControllerBase
{
    private const string _discordOAuthCall = "discordCall";
    private const string _discordOAuthCallback = "discordCallback";
    private static readonly ConcurrentDictionary<string, string> _cookieOAuthResponse = [];

    public OAuthController(ILogger<OAuthController> logger,
    IHttpContextAccessor accessor, IDbContextFactory<MareDbContext> mareDbContext,
    SecretKeyAuthenticatorService secretKeyAuthenticatorService,
    IConfigurationService<AuthServiceConfiguration> configuration,
    IDatabase redisDb, GeoIPService geoIPProvider)
        : base(logger, accessor, mareDbContext, secretKeyAuthenticatorService,
            configuration, redisDb, geoIPProvider)
    {
    }

    [AllowAnonymous]
    [HttpPost("afdCall")]
    public async Task<IActionResult> AFDWebSocket([FromBody] AFDApi.ApiResponse response)
    {
        using var dbContext = await MareDbContextFactory.CreateDbContextAsync();
        var order = response.Data.Order;
        ulong discordId = 0;
        Logger.LogInformation($"[Support] Get an order {JsonSerializer.Serialize(order)}");
        if (string.IsNullOrEmpty(order.CustomOrderId) || !ulong.TryParse(order.CustomOrderId, out discordId))
        {
            Logger.LogWarning($"[Support] Get an order without custom order ID. OutTradeNo: {order.OutTradeNo}");
            return new JsonResult(new { ec = 200, em = "" });
        }

        var user = await dbContext.Supports.FirstOrDefaultAsync(x => x.DiscordId == discordId).ConfigureAwait(false);
        var auth = await dbContext.LodeStoneAuth.AsNoTracking().Include(x => x.User).FirstOrDefaultAsync(x => x.DiscordId == discordId).ConfigureAwait(false);
        if (user is null)
        {
            user = new Support
            {
                DiscordId = discordId,
                ExpiresAt = DateTime.UtcNow,
                LastOrder = string.Empty,
                UserId = order.UserId,
                UserUID = auth?.User?.UID ?? string.Empty,
            };
            dbContext.Supports.Add(user);
            Logger.LogInformation($"[Support] New Supporter {discordId} - {auth?.User?.UID}");
        }

        try
        {
            if (user.LastOrder == order.OutTradeNo)
            {
                throw new Exception(
                    $"Discord user {user.DiscordId} issued an order has same outTradeNo {order.OutTradeNo}");
            }
            user.LastOrder = order.OutTradeNo;

            if (user.UserId != order.UserId)
            {
                Logger.LogWarning(
                    $"[Support] Update discord user {user.DiscordId}: Updating user ID '{order.UserId}'. OutTradeNo: {order.OutTradeNo}");
                user.UserId = order.UserId;
            }

            if (user.ExpiresAt < DateTime.UtcNow)
            {
                user.ExpiresAt = DateTime.UtcNow;
            }
            user.ExpiresAt = user.ExpiresAt!.Value.AddDays(order.Month * 31);

        }
        catch (Exception ex)
        {
            Logger.LogError($"[Support] Error: " + ex, ex.Message);
        }
        await dbContext.SaveChangesAsync();
        return new JsonResult(new { ec = 200, em = "" });
    }

    [AllowAnonymous]
    [HttpGet(_discordOAuthCall)]
    public IActionResult DiscordOAuthSetCookieAndRedirect([FromQuery] string sessionId)
    {
        var discordOAuthUri = Configuration.GetValueOrDefault<Uri?>(nameof(AuthServiceConfiguration.PublicOAuthBaseUri), null);
        var discordClientSecret = Configuration.GetValueOrDefault<string?>(nameof(AuthServiceConfiguration.DiscordOAuthClientSecret), null);
        var discordClientId = Configuration.GetValueOrDefault<string?>(nameof(AuthServiceConfiguration.DiscordOAuthClientId), null);
        if (discordClientSecret == null || discordClientId == null || discordOAuthUri == null)
            return BadRequest("服务器不支持Oauth2验证");

        Logger.LogDebug("Starting OAuth Process for {session}", sessionId);

        var cookieOptions = new CookieOptions
        {
            HttpOnly = true,
            Secure = true,
            Expires = DateTime.UtcNow.AddMinutes(30)
        };
        Response.Cookies.Append("DiscordOAuthSessionCookie", sessionId, cookieOptions);

        var parameters = new Dictionary<string, string>
        {
            { "client_id", discordClientId },
            { "response_type", "code" },
            { "redirect_uri", new Uri(discordOAuthUri, _discordOAuthCallback).ToString() },
            { "scope", "identify"},
        };
        using var content = new FormUrlEncodedContent(parameters);
        UriBuilder builder = new UriBuilder("https://discord.com/oauth2/authorize");
        var query = HttpUtility.ParseQueryString(builder.Query);
        foreach (var param in parameters)
        {
            query[param.Key] = param.Value;
        }
        builder.Query = query.ToString();

        return Redirect(builder.ToString());
    }

    [AllowAnonymous]
    [HttpGet(_discordOAuthCallback)]
    public async Task<IActionResult> DiscordOAuthCallback([FromQuery] string code)
    {
        var reqId = Request.Cookies["DiscordOAuthSessionCookie"];

        var discordOAuthUri = Configuration.GetValueOrDefault<Uri?>(nameof(AuthServiceConfiguration.PublicOAuthBaseUri), null);
        var discordClientSecret = Configuration.GetValueOrDefault<string?>(nameof(AuthServiceConfiguration.DiscordOAuthClientSecret), null);
        var discordClientId = Configuration.GetValueOrDefault<string?>(nameof(AuthServiceConfiguration.DiscordOAuthClientId), null);
        if (discordClientSecret == null || discordClientId == null || discordOAuthUri == null)
            return BadRequest("服务器不支持Oauth2验证");
        if (string.IsNullOrEmpty(reqId)) return BadRequest("未找到Cookie");
        if (string.IsNullOrEmpty(code)) return BadRequest("未找到OAuth2码");

        Logger.LogDebug("Discord OAuth Callback for {session}", reqId);

        var query = HttpUtility.ParseQueryString(discordOAuthUri.Query);
        using var client = new HttpClient();
        var parameters = new Dictionary<string, string>
        {
            { "client_id", discordClientId },
            { "client_secret", discordClientSecret },
            { "grant_type", "authorization_code" },
            { "code", code },
            { "redirect_uri", new Uri(discordOAuthUri, _discordOAuthCallback).ToString() }
        };

        using var content = new FormUrlEncodedContent(parameters);
        using var response = await client.PostAsync("https://discord.com/api/oauth2/token", content);
        using var responseBody = await response.Content.ReadAsStreamAsync().ConfigureAwait(false);

        if (!response.IsSuccessStatusCode)
        {
            Logger.LogDebug("Failed to get Discord token for {session}", reqId);
            return BadRequest("获取Discord token失败");
        }

        using var tokenJson = await JsonDocument.ParseAsync(responseBody).ConfigureAwait(false);
        var token = tokenJson.RootElement.GetProperty("access_token").GetString();

        using var httpClient = new HttpClient();
        httpClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
        using var meResponse = await httpClient.GetAsync("https://discord.com/api/users/@me");
        using var meBody = await meResponse.Content.ReadAsStreamAsync().ConfigureAwait(false);

        if (!meResponse.IsSuccessStatusCode)
        {
            Logger.LogDebug("Failed to get Discord user info for {session}", reqId);
            return BadRequest("获取Discord用户信息失败");
        }

        ulong discordUserId = 0;
        string discordUserName = string.Empty;
        try
        {
            using var jsonResponse = await JsonDocument.ParseAsync(meBody).ConfigureAwait(false);
            discordUserId = ulong.Parse(jsonResponse.RootElement.GetProperty("id").GetString()!);
            discordUserName = jsonResponse.RootElement.GetProperty("username").GetString()!;
        }
        catch (Exception ex)
        {
            Logger.LogDebug(ex, "Failed to parse Discord user info for {session}", reqId);
            return BadRequest("尝试从 @me 获得Token失败");
        }

        if (discordUserId == 0)
            return BadRequest("获取 Discord ID 失败");

        using var dbContext = await MareDbContextFactory.CreateDbContextAsync();

        var mareUser = await dbContext.LodeStoneAuth.Include(u => u.User).SingleOrDefaultAsync(u => u.DiscordId == discordUserId);
        if (mareUser == default)
        {
            Logger.LogDebug("未找到对应的Mare用户 {session}, DiscordId: {id}", reqId, discordUserId);

            return BadRequest("未找到与本Discord账号对应的Mare账号.");
        }

        JwtSecurityToken? jwt = null;
        try
        {
            jwt = CreateJwt([
                new Claim(MareClaimTypes.Uid, mareUser.User!.UID),
                new Claim(MareClaimTypes.Expires, DateTime.UtcNow.AddDays(14).Ticks.ToString(CultureInfo.InvariantCulture)),
                new Claim(MareClaimTypes.DiscordId, discordUserId.ToString()),
                new Claim(MareClaimTypes.DiscordUser, discordUserName),
                new Claim(MareClaimTypes.OAuthLoginToken, true.ToString())
            ]);
        }
        catch (Exception ex)
        {
            Logger.LogWarning(ex, "Failed to create the OAuth2 token for session {session} and Discord user {user}", reqId, discordUserId);
            return BadRequest("创建 OAuth2 令牌失败. 请联系管理员.");
        }

        _cookieOAuthResponse[reqId] = jwt.RawData;
        _ = Task.Run(async () =>
        {
            bool isRemoved = false;
            for (int i = 0; i < 30; i++)
            {
                await Task.Delay(TimeSpan.FromSeconds(3)).ConfigureAwait(false);
                if (!_cookieOAuthResponse.ContainsKey(reqId))
                {
                    isRemoved = true;
                    break;
                }
            }
            if (!isRemoved)
                _cookieOAuthResponse.TryRemove(reqId, out _);
        });

        Logger.LogDebug("Setting JWT response for {session}, process complete", reqId);
        return Ok("OAuth2 token已生成. 插件将自动获取. 你可以关闭本标签页了.");
    }

    [Authorize(Policy = "OAuthToken")]
    [HttpPost(MareAuth.OAuth_GetUIDsBasedOnSecretKeys)]
    public async Task<Dictionary<string, string>> GetUIDsBasedOnSecretKeys([FromBody] List<string> secretKeys)
    {
        if (!secretKeys.Any())
            return [];

        using var dbContext = await MareDbContextFactory.CreateDbContextAsync();

        Dictionary<string, string> secretKeysToUIDDict = secretKeys.Distinct().ToDictionary(k => k, _ => string.Empty, StringComparer.Ordinal);
        foreach (var key in secretKeys)
        {
            var shaKey = StringUtils.Sha256String(key);
            var associatedAuth = await dbContext.Auth.AsNoTracking().SingleOrDefaultAsync(a => a.HashedKey == shaKey);
            if (associatedAuth != null)
            {
                secretKeysToUIDDict[key] = associatedAuth.UserUID;
            }
        }

        return secretKeysToUIDDict;
    }

    [Authorize(Policy = "OAuthToken")]
    [HttpPost(MareAuth.OAuth_RenewOAuthToken)]
    public IActionResult RenewOAuthToken()
    {
        var claims = HttpContext.User.Claims.Where(c => c.Type != MareClaimTypes.Expires).ToList();
        claims.Add(new Claim(MareClaimTypes.Expires, DateTime.UtcNow.AddDays(14).Ticks.ToString(CultureInfo.InvariantCulture)));
        return Content(CreateJwt(claims).RawData);
    }

    [AllowAnonymous]
    [HttpGet(MareAuth.OAuth_GetDiscordOAuthToken)]
    public async Task<IActionResult> GetDiscordOAuthToken([FromQuery] string sessionId)
    {
        Logger.LogDebug("Starting to wait for GetDiscordOAuthToken for {session}", sessionId);
        using CancellationTokenSource cts = new();
        cts.CancelAfter(TimeSpan.FromSeconds(55));
        try
        {
            while (!_cookieOAuthResponse.ContainsKey(sessionId) && !cts.Token.IsCancellationRequested)
            {
                await Task.Delay(TimeSpan.FromSeconds(1), cts.Token);
            }
        }
        catch
        {
            Logger.LogDebug("Timeout elapsed for {session}", sessionId);
            return BadRequest("等待 Discord OAuth2 响应超时");
        }
        if (cts.IsCancellationRequested)
        {
            Logger.LogDebug("Timeout elapsed for {session}", sessionId);
            return BadRequest("未收到 Discord OAuth2 响应");
        }
        _cookieOAuthResponse.TryRemove(sessionId, out var token);
        if (token == null)
        {
            Logger.LogDebug("No token found for {session}", sessionId);
            return BadRequest("OAuth 连接未建立");
        }
        Logger.LogDebug("Returning JWT for {session}, process complete", sessionId);
        return Content(token);
    }

    [AllowAnonymous]
    [HttpGet(MareAuth.OAuth_GetDiscordOAuthEndpoint)]
    public Uri? GetDiscordOAuthEndpoint()
    {
        var discordOAuthUri = Configuration.GetValueOrDefault<Uri?>(nameof(AuthServiceConfiguration.PublicOAuthBaseUri), null);
        var discordClientSecret = Configuration.GetValueOrDefault<string?>(nameof(AuthServiceConfiguration.DiscordOAuthClientSecret), null);
        var discordClientId = Configuration.GetValueOrDefault<string?>(nameof(AuthServiceConfiguration.DiscordOAuthClientId), null);
        if (discordClientSecret == null || discordClientId == null || discordOAuthUri == null)
            return null;
        return new Uri(discordOAuthUri, _discordOAuthCall);
    }

    [Authorize(Policy = "OAuthToken")]
    [HttpGet(MareAuth.OAuth_GetUIDs)]
    public async Task<Dictionary<string, string>> GetAvailableUIDs()
    {
        string primaryUid = HttpContext.User.Claims.Single(c => string.Equals(c.Type, MareClaimTypes.Uid, StringComparison.Ordinal))!.Value;
        using var dbContext = await MareDbContextFactory.CreateDbContextAsync();

        var mareUser = await dbContext.Auth.AsNoTracking().Include(u => u.User).FirstOrDefaultAsync(f => f.UserUID == primaryUid).ConfigureAwait(false);
        if (mareUser == null || mareUser.User == null) return [];
        var uid = mareUser.User.UID;
        var allUids = await dbContext.Auth.AsNoTracking().Include(u => u.User).Where(a => a.UserUID == uid || a.PrimaryUserUID == uid).ToListAsync().ConfigureAwait(false);
        var result = allUids.OrderBy(u => u.UserUID == uid ? 0 : 1).ThenBy(u => u.UserUID).Select(u => (u.UserUID, u.User.Alias)).ToDictionary();
        return result;
    }

    [Authorize(Policy = "OAuthToken")]
    [HttpPost(MareAuth.OAuth_CreateOAuth)]
    public async Task<IActionResult> CreateTokenWithOAuth(string uid, string charaIdent, string nameWithWorld, string? machineId = null, string? aidHash = null)
    {
        using var dbContext = await MareDbContextFactory.CreateDbContextAsync();

        return await AuthenticateOAuthInternal(dbContext, uid, charaIdent,nameWithWorld, machineId, aidHash);
    }

    private async Task<IActionResult> AuthenticateOAuthInternal(MareDbContext dbContext, string requestedUid, string charaIdent, string nameWithWorld, string? machineId = null,  string? aidHash = null)
    {
        try
        {
            string primaryUid = HttpContext.User.Claims.Single(c => string.Equals(c.Type, MareClaimTypes.Uid, StringComparison.Ordinal))!.Value;
            
            if (primaryUid != requestedUid &&
                !await dbContext.Auth.AsNoTracking().AnyAsync(a => a.UserUID == requestedUid && a.PrimaryUserUID == primaryUid))
            {
                return BadRequest("UID不属于当前Discord账户, 请点击 '从服务器更新UID' 并重新分配");
            }
            
            if (string.IsNullOrEmpty(requestedUid)) return BadRequest("无 UID");
            if (string.IsNullOrEmpty(charaIdent)) return BadRequest("无 CharaIdent");
            if (string.IsNullOrEmpty(nameWithWorld)) return BadRequest("无效的角色名");
            if (string.IsNullOrEmpty(aidHash)) return BadRequest("无效的用户");

            var ip = HttpAccessor.GetIpAddress();

            var authResult = await SecretKeyAuthenticatorService.AuthorizeOauthAsync(ip, primaryUid, requestedUid);

            return await GenericAuthResponse(dbContext, charaIdent, authResult, nameWithWorld, machineId, aidHash);
        }
        catch (Exception ex)
        {
            Logger.LogWarning(ex, "Authenticate:UNKNOWN");
            return Unauthorized("认证时出现了未知服务器错误");
        }
    }
}