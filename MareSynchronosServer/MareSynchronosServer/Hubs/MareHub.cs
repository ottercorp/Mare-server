using MareSynchronos.API.Data;
using MareSynchronos.API.Data.Enum;
using MareSynchronos.API.Dto;
using MareSynchronos.API.SignalR;
using MareSynchronosServer.Services;
using MareSynchronosServer.Utils;
using MareSynchronosShared;
using MareSynchronosShared.Data;
using MareSynchronosShared.Metrics;
using MareSynchronosShared.Models;
using MareSynchronosShared.Services;
using MareSynchronosShared.Utils.Configuration;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using StackExchange.Redis.Extensions.Core.Abstractions;
using System.Collections.Concurrent;
using System.Text.Json;
using MareSynchronos.API.Dto.Group;
using MareSynchronos.API.Dto.User;

namespace MareSynchronosServer.Hubs;

[Authorize(Policy = "Authenticated")]
public partial class MareHub : Hub<IMareHub>, IMareHub
{
    private static readonly ConcurrentDictionary<string, string> _userConnections = new(StringComparer.Ordinal);
    private readonly MareMetrics _mareMetrics;
    private readonly SystemInfoService _systemInfoService;
    private readonly IHttpContextAccessor _contextAccessor;
    private readonly MareHubLogger _logger;
    private readonly string _shardName;
    private readonly int _maxExistingGroupsByUser;
    private readonly int _maxJoinedGroupsByUser;
    private readonly int _maxGroupUserCount;
    private readonly IRedisDatabase _redis;
    private readonly OnlineSyncedPairCacheService _onlineSyncedPairCacheService;
    private readonly MareCensus _mareCensus;
    private readonly GPoseLobbyDistributionService _gPoseLobbyDistributionService;
    private readonly Uri _fileServerAddress;
    private readonly Version _expectedClientVersion;
    private readonly Lazy<MareDbContext> _dbContextLazy;
    private MareDbContext DbContext => _dbContextLazy.Value;
    private readonly int _maxCharaDataByUser;
    private readonly int _maxCharaDataByUserVanity;

    public MareHub(MareMetrics mareMetrics,
        IDbContextFactory<MareDbContext> mareDbContextFactory, ILogger<MareHub> logger, SystemInfoService systemInfoService,
        IConfigurationService<ServerConfiguration> configuration, IHttpContextAccessor contextAccessor,
        IRedisDatabase redisDb, OnlineSyncedPairCacheService onlineSyncedPairCacheService, MareCensus mareCensus,
        GPoseLobbyDistributionService gPoseLobbyDistributionService)
    {
        _mareMetrics = mareMetrics;
        _systemInfoService = systemInfoService;
        _shardName = configuration.GetValue<string>(nameof(ServerConfiguration.ShardName));
        _maxExistingGroupsByUser = configuration.GetValueOrDefault(nameof(ServerConfiguration.MaxExistingGroupsByUser), 3);
        _maxJoinedGroupsByUser = configuration.GetValueOrDefault(nameof(ServerConfiguration.MaxJoinedGroupsByUser), 6);
        _maxGroupUserCount = configuration.GetValueOrDefault(nameof(ServerConfiguration.MaxGroupUserCount), 100);
        _fileServerAddress = configuration.GetValue<Uri>(nameof(ServerConfiguration.CdnFullUrl));
        _expectedClientVersion = configuration.GetValueOrDefault(nameof(ServerConfiguration.ExpectedClientVersion), new Version(0, 0, 0));
        _maxCharaDataByUser = configuration.GetValueOrDefault(nameof(ServerConfiguration.MaxCharaDataByUser), 10);
        _maxCharaDataByUserVanity = configuration.GetValueOrDefault(nameof(ServerConfiguration.MaxCharaDataByUserVanity), 50);
        _contextAccessor = contextAccessor;
        _redis = redisDb;
        _onlineSyncedPairCacheService = onlineSyncedPairCacheService;
        _mareCensus = mareCensus;
        _gPoseLobbyDistributionService = gPoseLobbyDistributionService;
        _logger = new MareHubLogger(this, logger);
        _dbContextLazy = new Lazy<MareDbContext>(() => mareDbContextFactory.CreateDbContext());
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            if (_dbContextLazy.IsValueCreated) DbContext.Dispose();
        }

        base.Dispose(disposing);
    }

    [Authorize(Policy = "Identified")]
    public async Task<ConnectionDto> GetConnectionDto()
    {
        _logger.LogCallInfo();

        _mareMetrics.IncCounter(MetricsAPI.CounterInitializedConnections);

        await Clients.Caller.Client_UpdateSystemInfo(_systemInfoService.SystemInfoDto).ConfigureAwait(false);

        var dbUser = await DbContext.Users.SingleAsync(f => f.UID == UserUID).ConfigureAwait(false);
        dbUser.LastLoggedIn = DateTime.UtcNow;

        await Clients.Caller.Client_ReceiveServerMessage(MessageSeverity.Information, "Welcome to Mare Synchronos \"" + _shardName + "\", Current Online Users: " + _systemInfoService.SystemInfoDto.OnlineUsers).ConfigureAwait(false);

        await Clients.Caller.Client_UpdateSupporterList(_systemInfoService.SupportersDto).ConfigureAwait(false);

        var defaultPermissions = await DbContext.UserDefaultPreferredPermissions.SingleOrDefaultAsync(u => u.UserUID == UserUID).ConfigureAwait(false);
        if (defaultPermissions == null)
        {
            defaultPermissions = new UserDefaultPreferredPermission()
            {
                UserUID = UserUID,
            };

            DbContext.UserDefaultPreferredPermissions.Add(defaultPermissions);
        }

        await DbContext.SaveChangesAsync().ConfigureAwait(false);

        return new ConnectionDto(new UserData(dbUser.UID, string.IsNullOrWhiteSpace(dbUser.Alias) ? null : dbUser.Alias))
        {
            CurrentClientVersion = _expectedClientVersion,
            ServerVersion = IMareHub.ApiVersion,
            IsAdmin = dbUser.IsAdmin,
            IsModerator = dbUser.IsModerator,
            ServerInfo = new ServerInfo()
            {
                MaxGroupsCreatedByUser = _maxExistingGroupsByUser,
                ShardName = _shardName,
                MaxGroupsJoinedByUser = _maxJoinedGroupsByUser,
                MaxGroupUserCount = _maxGroupUserCount,
                FileServerAddress = _fileServerAddress,
                MaxCharaData = _maxCharaDataByUser,
                MaxCharaDataVanity = _maxCharaDataByUserVanity,
            },
            DefaultPreferredPermissions = new DefaultPermissionsDto()
            {
                DisableGroupAnimations = defaultPermissions.DisableGroupAnimations,
                DisableGroupSounds = defaultPermissions.DisableGroupSounds,
                DisableGroupVFX = defaultPermissions.DisableGroupVFX,
                DisableIndividualAnimations = defaultPermissions.DisableIndividualAnimations,
                DisableIndividualSounds = defaultPermissions.DisableIndividualSounds,
                DisableIndividualVFX = defaultPermissions.DisableIndividualVFX,
                IndividualIsSticky = defaultPermissions.IndividualIsSticky,
            },
        };
    }

    [Authorize(Policy = "Authenticated")]
    public async Task<bool> CheckClientHealth()
    {
        await UpdateUserOnRedis().ConfigureAwait(false);

        return false;
    }

    [Authorize(Policy = "Authenticated")]
    public override async Task OnConnectedAsync()
    {
        if (AidHash == "NUK")
        {
            _logger.LogCallWarning(MareHubLogger.Args(_contextAccessor.GetIpAddress(), "NoAidHash", UserUID, Context.ConnectionId));
        }
        if (_userConnections.TryGetValue(UserUID, out var oldId))
        {
            _logger.LogCallWarning(MareHubLogger.Args(_contextAccessor.GetIpAddress(), "UpdatingId", oldId, Context.ConnectionId));
            _userConnections[UserUID] = Context.ConnectionId;
        }
        else
        {
            _mareMetrics.IncGaugeWithLabels(MetricsAPI.GaugeConnections, labels: Continent);

            try
            {
                _logger.LogCallInfo(MareHubLogger.Args(_contextAccessor.GetIpAddress(), Context.ConnectionId, UserCharaIdent));
                await _onlineSyncedPairCacheService.InitPlayer(UserUID).ConfigureAwait(false);
                await UpdateUserOnRedis().ConfigureAwait(false);
                _userConnections[UserUID] = Context.ConnectionId;
            }
            catch
            {
                _userConnections.Remove(UserUID, out _);
            }
        }

        await base.OnConnectedAsync().ConfigureAwait(false);
    }

    [Authorize(Policy = "Authenticated")]
    public override async Task OnDisconnectedAsync(Exception exception)
    {
        if (_userConnections.TryGetValue(UserUID, out var connectionId)
            && string.Equals(connectionId, Context.ConnectionId, StringComparison.Ordinal))
        {
            _mareMetrics.DecGaugeWithLabels(MetricsAPI.GaugeConnections, labels: Continent);

            try
            {
                await GposeLobbyLeave().ConfigureAwait(false);

                await _onlineSyncedPairCacheService.DisposePlayer(UserUID).ConfigureAwait(false);

                _logger.LogCallInfo(MareHubLogger.Args(_contextAccessor.GetIpAddress(), Context.ConnectionId, UserCharaIdent));
                if (exception != null)
                    _logger.LogCallWarning(MareHubLogger.Args(_contextAccessor.GetIpAddress(), Context.ConnectionId, exception.Message, exception.StackTrace));

                await RemoveUserFromRedis().ConfigureAwait(false);

                _mareCensus.ClearStatistics(UserUID);

                await SendOfflineToAllPairedUsers().ConfigureAwait(false);

                DbContext.RemoveRange(DbContext.Files.Where(f => !f.Uploaded && f.UploaderUID == UserUID));
                await DbContext.SaveChangesAsync().ConfigureAwait(false);

            }
            catch { }
            finally
            {
                _userConnections.Remove(UserUID, out _);
            }
        }
        else
        {
            _logger.LogCallWarning(MareHubLogger.Args(_contextAccessor.GetIpAddress(), "ObsoleteId", UserUID, Context.ConnectionId));
        }

        await base.OnDisconnectedAsync(exception).ConfigureAwait(false);
    }

    /// <summary>
    /// Notifiy the recipient pair to apply the spesified Moodles we provide from our own moodles list to their status manager.
    /// <para>
    /// NOTICE:
    /// This CAN check for permission validation, and will verify the moodles info fits
    /// the allowed permissions. Will return false if it does not.
    /// </para>
    /// </summary>
    [Authorize(Policy = "Identified")]
    public async Task<bool> UserApplyMoodlesByStatus(ApplyMoodlesByStatusDto dto)
    {
        // construct a new dto with the client caller as the user.
        var newDto = new ApplyMoodlesByStatusDto(User: new UserData(UserUID), Statuses: dto.Statuses);

        // notify the recipient pair to apply the moodles.
        await Clients.User(dto.User.UID).Client_UserApplyMoodlesByStatus(newDto).ConfigureAwait(false);

        return true;
    }

    [Authorize(Policy = "Identified")]
    public async Task MoodlesShare(MoodlesDto dto)
    {
        _logger.LogCallInfo(MareHubLogger.Args(dto.Action, dto.Statuses));
        try
        {
            if (dto.Action == MoodlesAction.UpLoad)
            {
                var json = JsonSerializer.Deserialize<Moodles>(dto.Statuses);
                json.UserUID = dto.User.UID;
                json.User = await DbContext.Users.FindAsync(dto.User.UID).ConfigureAwait(false);
                var moodles = await DbContext.Moodles.FirstOrDefaultAsync(x => x.GUID == json.GUID).ConfigureAwait(false);
                if (moodles is null)
                {
                    DbContext.Moodles.Add(json);
                }
                else
                {
                    if (moodles.UserUID != dto.User.UID)
                    {
                        throw new Exception(
                            $"User {dto.User.UID} tried to update moodles {moodles.GUID} owned by {moodles.User.UID}");
                    }
                    DbContext.Entry(moodles).CurrentValues.SetValues(json);
                }
                DbContext.SaveChanges();
            }
            else if (dto.Action == MoodlesAction.Remove)
            {
                var json = JsonSerializer.Deserialize<Moodles>(dto.Statuses);
                var moodles = await DbContext.Moodles.FirstOrDefaultAsync(x => x.UserUID == dto.User.UID && x.GUID == json.GUID).ConfigureAwait(false);
                if (moodles is not null)
                {
                    DbContext.Moodles.Remove(moodles);
                }
                DbContext.SaveChanges();
            }
            else if (dto.Action == MoodlesAction.Download)
            {
                var data =await DbContext.Moodles.AsNoTracking().ToListAsync().ConfigureAwait(false);
                var json = JsonSerializer.Serialize(data);
                await Clients.User(dto.User.UID).Client_MoodlesShare(new MoodlesDto(dto.User, MoodlesAction.Download, json)).ConfigureAwait(false);
            }
        }
        catch (Exception e)
        {
            _logger.LogCallWarning(MareHubLogger.Args(_contextAccessor.GetIpAddress(), Context.ConnectionId, dto.User.UID, Context.ConnectionId, e.Message));
        }
    }

    [Authorize(Policy = "Identified")]
    public async Task<bool> UpdatePFinder(PFinderDto pFinderDto)
    {
        _logger.LogCallInfo(MareHubLogger.Args(pFinderDto.Title, pFinderDto.User));
        try
        {
            var pf = await DbContext.PFinder.FirstOrDefaultAsync(x => x.Guid == pFinderDto.Guid).ConfigureAwait(false);
            if (pf == null)
            {
                pf = new PFinder(pFinderDto);
                DbContext.PFinder.Add(pf);
            }
            else
            {
                if (pf.UserId != pFinderDto.User.UID)
                {
                    return false;
                }
                pFinderDto.LastUpdate = DateTimeOffset.UtcNow;
                pf.Update(pFinderDto);
            }

            DbContext.SaveChanges();
            return true;
        }
        catch (Exception e)
        {
            _logger.LogCallWarning(MareHubLogger.Args(_contextAccessor.GetIpAddress(), Context.ConnectionId, pFinderDto.User.UID, Context.ConnectionId, e.Message));
            return false;
        }
    }

    [Authorize(Policy = "Identified")]
    public async Task<List<PFinderDto>> RefreshPFinderList(UserDto userDto)
    {
        _logger.LogCallInfo(MareHubLogger.Args(userDto.User.AliasOrUID));
        try
        {
            List<PFinderDto> result = [];
            var pfs = await DbContext.PFinder.AsNoTracking().Include(x => x.Group).Include(x => x.User)
                .Where(x => x.EndTime > DateTimeOffset.UtcNow).ToListAsync().ConfigureAwait(false);
            var lastPassedPf = await DbContext.PFinder.AsNoTracking().Include(x => x.Group).Include(x => x.User)
                .FirstOrDefaultAsync(x =>
                    x.UserId == userDto.User.UID && DateTimeOffset.MinValue.AddYears(1) < x.EndTime &&
                    x.EndTime < DateTimeOffset.UtcNow && x.GroupId != null).ConfigureAwait(false);
            foreach (var pf in pfs)
            {
                var inGroup = await DbContext.GroupPairs.AnyAsync(x =>x.GroupGID == pf.GroupId && x.GroupUserUID == userDto.User.UID).ConfigureAwait(false);
                if (!pf.Open && !inGroup) continue;
                if (pf.GroupId is null) continue;
                result.Add(pf.ToDto());
            }
            result = result.OrderByDescending(x=> x.LastUpdate).ToList();
            if (lastPassedPf != null)
            {
                result.Add(lastPassedPf.ToDto());
            }

            return result;
        }
        catch (Exception e)
        {
            _logger.LogCallWarning(MareHubLogger.Args(_contextAccessor.GetIpAddress(), Context.ConnectionId, userDto.User.UID, Context.ConnectionId, e.Message));
            return [];
        }
    }
}