using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using MareSynchronos.API.Data;
using MareSynchronos.API.Data.Enum;
using MareSynchronos.API.Data.Extensions;
using MareSynchronos.API.Dto.User;
using MareSynchronosServer.Utils;
using MareSynchronosShared.Metrics;
using MareSynchronosShared.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace MareSynchronosServer.Hubs;

public partial class MareHub
{
    private static readonly string[] AllowedExtensionsForGamePaths = { ".mdl", ".tex", ".mtrl", ".tmb", ".pap", ".avfx", ".atex", ".sklb", ".eid", ".phyb", ".pbd", ".scd", ".skp", ".shpk" };

    [Authorize(Policy = "Identified")]
    public async Task UserAddPair(UserDto dto)
    {
        _logger.LogCallInfo(MareHubLogger.Args(dto));

        // don't allow adding nothing
        var uid = dto.User.UID.Trim();
        if (string.Equals(dto.User.UID, UserUID, StringComparison.Ordinal) || string.IsNullOrWhiteSpace(dto.User.UID)) return;

        // grab other user, check if it exists and if a pair already exists
        var otherUser = await DbContext.Users.SingleOrDefaultAsync(u => u.UID == uid || u.Alias == uid).ConfigureAwait(false);
        if (otherUser == null)
        {
            await Clients.Caller.Client_ReceiveServerMessage(MessageSeverity.Warning, $"无法与 {dto.User.UID} 配对, UID不存在").ConfigureAwait(false);
            return;
        }

        if (string.Equals(otherUser.UID, UserUID, StringComparison.Ordinal))
        {
            await Clients.Caller.Client_ReceiveServerMessage(MessageSeverity.Warning, $"只有神知道你为什么要和自己配对, 住手").ConfigureAwait(false);
            return;
        }

        var existingEntry =
            await DbContext.ClientPairs.AsNoTracking()
                .FirstOrDefaultAsync(p =>
                    p.User.UID == UserUID && p.OtherUserUID == otherUser.UID).ConfigureAwait(false);

        if (existingEntry != null)
        {
            await Clients.Caller.Client_ReceiveServerMessage(MessageSeverity.Warning, $"无法与 {dto.User.UID} 配对, 已配对").ConfigureAwait(false);
            return;
        }

        // grab self create new client pair and save
        var user = await DbContext.Users.SingleAsync(u => u.UID == UserUID).ConfigureAwait(false);

        _logger.LogCallInfo(MareHubLogger.Args(dto, "Success"));

        ClientPair wl = new ClientPair()
        {
            OtherUser = otherUser,
            User = user,
        };
        await DbContext.ClientPairs.AddAsync(wl).ConfigureAwait(false);

        var existingData = await GetPairInfo(UserUID, otherUser.UID).ConfigureAwait(false);

        var permissions = existingData?.OwnPermissions;
        if (permissions == null || !permissions.Sticky)
        {
            var ownDefaultPermissions = await DbContext.UserDefaultPreferredPermissions.AsNoTracking().SingleOrDefaultAsync(f => f.UserUID == UserUID).ConfigureAwait(false);

            permissions = new UserPermissionSet()
            {
                User = user,
                OtherUser = otherUser,
                DisableAnimations = ownDefaultPermissions.DisableIndividualAnimations,
                DisableSounds = ownDefaultPermissions.DisableIndividualSounds,
                DisableVFX = ownDefaultPermissions.DisableIndividualVFX,
                IsPaused = false,
                Sticky = true
            };

            var existingDbPerms = await DbContext.Permissions.SingleOrDefaultAsync(u => u.UserUID == UserUID && u.OtherUserUID == otherUser.UID).ConfigureAwait(false);
            if (existingDbPerms == null)
            {
                await DbContext.Permissions.AddAsync(permissions).ConfigureAwait(false);
            }
            else
            {
                existingDbPerms.DisableAnimations = permissions.DisableAnimations;
                existingDbPerms.DisableSounds = permissions.DisableSounds;
                existingDbPerms.DisableVFX = permissions.DisableVFX;
                existingDbPerms.IsPaused = false;
                existingDbPerms.Sticky = true;

                DbContext.Permissions.Update(existingDbPerms);
            }
        }

        await DbContext.SaveChangesAsync().ConfigureAwait(false);

        // get the opposite entry of the client pair
        var otherEntry = OppositeEntry(otherUser.UID);
        var otherIdent = await GetUserIdent(otherUser.UID).ConfigureAwait(false);

        var otherPermissions = existingData?.OtherPermissions ?? null;

        var ownPerm = permissions.ToUserPermissions(setSticky: true);
        var otherPerm = otherPermissions.ToUserPermissions();

        var userPairResponse = new UserPairDto(otherUser.ToUserData(),
            otherEntry == null ? IndividualPairStatus.OneSided : IndividualPairStatus.Bidirectional,
            ownPerm, otherPerm);

        await Clients.User(user.UID).Client_UserAddClientPair(userPairResponse).ConfigureAwait(false);

        // check if other user is online
        if (otherIdent == null || otherEntry == null) return;

        // send push with update to other user if other user is online
        await Clients.User(otherUser.UID)
            .Client_UserUpdateOtherPairPermissions(new UserPermissionsDto(user.ToUserData(),
            permissions.ToUserPermissions())).ConfigureAwait(false);

        await Clients.User(otherUser.UID)
            .Client_UpdateUserIndividualPairStatusDto(new(user.ToUserData(), IndividualPairStatus.Bidirectional))
            .ConfigureAwait(false);

        if (!ownPerm.IsPaused() && !otherPerm.IsPaused())
        {
            await Clients.User(UserUID).Client_UserSendOnline(new(otherUser.ToUserData(), otherIdent)).ConfigureAwait(false);
            await Clients.User(otherUser.UID).Client_UserSendOnline(new(user.ToUserData(), UserCharaIdent)).ConfigureAwait(false);
        }
    }

    [Authorize(Policy = "Identified")]
    public async Task UserDelete()
    {
        _logger.LogCallInfo();

        var userEntry = await DbContext.Users.SingleAsync(u => u.UID == UserUID).ConfigureAwait(false);
        var secondaryUsers = await DbContext.Auth.Include(u => u.User).Where(u => u.PrimaryUserUID == UserUID).Select(c => c.User).ToListAsync().ConfigureAwait(false);
        foreach (var user in secondaryUsers)
        {
            await DeleteUser(user).ConfigureAwait(false);
        }

        await DeleteUser(userEntry).ConfigureAwait(false);
    }

    [Authorize(Policy = "Identified")]
    public async Task<List<OnlineUserIdentDto>> UserGetOnlinePairs(CensusDataDto? censusData)
    {
        _logger.LogCallInfo();

        var allPairedUsers = await GetAllPairedUnpausedUsers().ConfigureAwait(false);
        var pairs = await GetOnlineUsers(allPairedUsers).ConfigureAwait(false);

        await SendOnlineToAllPairedUsers().ConfigureAwait(false);

        _mareCensus.PublishStatistics(UserUID, censusData);

        return pairs.Select(p => new OnlineUserIdentDto(new UserData(p.Key), p.Value)).ToList();
    }

    [Authorize(Policy = "Identified")]
    public async Task<List<UserFullPairDto>> UserGetPairedClients()
    {
        _logger.LogCallInfo();

        var pairs = await GetAllPairInfo(UserUID).ConfigureAwait(false);
        return pairs.Select(p =>
        {
            return new UserFullPairDto(new UserData(p.Key, p.Value.Alias),
                p.Value.ToIndividualPairStatus(),
                p.Value.GIDs.Where(g => !string.Equals(g, Constants.IndividualKeyword, StringComparison.OrdinalIgnoreCase)).ToList(),
                p.Value.OwnPermissions.ToUserPermissions(setSticky: true),
                p.Value.OtherPermissions.ToUserPermissions());
        }).ToList();
    }

    [Authorize(Policy = "Identified")]
    public async Task<UserProfileDto> UserGetProfile(UserDto user)
    {
        _logger.LogCallInfo(MareHubLogger.Args(user));

        var allUserPairs = await GetAllPairedUnpausedUsers().ConfigureAwait(false);

        if (!allUserPairs.Contains(user.User.UID, StringComparer.Ordinal) && !string.Equals(user.User.UID, UserUID, StringComparison.Ordinal))
        {
            return new UserProfileDto(user.User, false, null, null, "你无法在暂停配对时查看档案.");
        }

        var data = await DbContext.UserProfileData.SingleOrDefaultAsync(u => u.UserUID == user.User.UID).ConfigureAwait(false);
        if (data == null) return new UserProfileDto(user.User, false, null, null, null);

        if (data.FlaggedForReport) return new UserProfileDto(user.User, true, null, null, "该用户已被举报正在等待处理");
        if (data.ProfileDisabled) return new UserProfileDto(user.User, true, null, null, "该档案被禁止访问");

        return new UserProfileDto(user.User, false, data.IsNSFW, data.Base64ProfileImage, data.UserDescription);
    }

    [Authorize(Policy = "Identified")]
    public async Task UserPushData(UserCharaDataMessageDto dto)
    {
        _logger.LogCallInfo(MareHubLogger.Args(dto.CharaData.FileReplacements.Count));

        // check for honorific containing . and /
        try
        {
            var honorificJson = Encoding.Default.GetString(Convert.FromBase64String(dto.CharaData.HonorificData));
            var deserialized = JsonSerializer.Deserialize<JsonElement>(honorificJson);
            if (deserialized.TryGetProperty("Title", out var honorificTitle))
            {
                var title = honorificTitle.GetString().Normalize(NormalizationForm.FormKD);
                if (UrlRegex().IsMatch(title))
                {
                    await Clients.Caller.Client_ReceiveServerMessage(MessageSeverity.Error, "你的数据未能发送: 禁止在 Honorific 中使用网页地址. 请移除后重试.").ConfigureAwait(false);
                    throw new HubException("无效的数据, Honorific 称号: " + title);
                }
            }
        }
        catch (HubException)
        {
            throw;
        }
        catch (Exception)
        {
            // swallow
        }

        bool hadInvalidData = false;
        List<string> invalidGamePaths = new();
        List<string> invalidFileSwapPaths = new();
        foreach (var replacement in dto.CharaData.FileReplacements.SelectMany(p => p.Value))
        {
            var invalidPaths = replacement.GamePaths.Where(p => !GamePathRegex().IsMatch(p)).ToList();
            invalidPaths.AddRange(replacement.GamePaths.Where(p => !AllowedExtensionsForGamePaths.Any(e => p.EndsWith(e, StringComparison.OrdinalIgnoreCase))));
            replacement.GamePaths = replacement.GamePaths.Where(p => !invalidPaths.Contains(p, StringComparer.OrdinalIgnoreCase)).ToArray();
            bool validGamePaths = replacement.GamePaths.Any();
            bool validHash = string.IsNullOrEmpty(replacement.Hash) || HashRegex().IsMatch(replacement.Hash);
            bool validFileSwapPath = string.IsNullOrEmpty(replacement.FileSwapPath) || GamePathRegex().IsMatch(replacement.FileSwapPath);
            if (!validGamePaths || !validHash || !validFileSwapPath)
            {
                _logger.LogCallWarning(MareHubLogger.Args("Invalid Data", "GamePaths", validGamePaths, string.Join(",", invalidPaths), "Hash", validHash, replacement.Hash, "FileSwap", validFileSwapPath, replacement.FileSwapPath));
                hadInvalidData = true;
                if (!validFileSwapPath) invalidFileSwapPaths.Add(replacement.FileSwapPath);
                if (!validGamePaths) invalidGamePaths.AddRange(replacement.GamePaths);
                if (!validHash) invalidFileSwapPaths.Add(replacement.Hash);
            }
        }

        if (hadInvalidData)
        {
            await Clients.Caller.Client_ReceiveServerMessage(MessageSeverity.Error, "你的某些Mod被服务器拒绝. 输入 /xllog 查看更多信息.").ConfigureAwait(false);
            throw new HubException("无效的数据, 请联系Mod作者解决问题"
            + Environment.NewLine
            + string.Join(Environment.NewLine, invalidGamePaths.Select(p => "无效的游戏路径: " + p))
            + Environment.NewLine
            + string.Join(Environment.NewLine, invalidFileSwapPaths.Select(p => "无效的文件路径: " + p)));
        }

        var recipientUids = dto.Recipients.Select(r => r.UID).ToList();
        bool allCached = await _onlineSyncedPairCacheService.AreAllPlayersCached(UserUID,
            recipientUids, Context.ConnectionAborted).ConfigureAwait(false);

        if (!allCached)
        {
            var allPairedUsers = await GetAllPairedUnpausedUsers().ConfigureAwait(false);

            recipientUids = allPairedUsers.Where(f => recipientUids.Contains(f, StringComparer.Ordinal)).ToList();

            await _onlineSyncedPairCacheService.CachePlayers(UserUID, allPairedUsers, Context.ConnectionAborted).ConfigureAwait(false);
        }

        _logger.LogCallInfo(MareHubLogger.Args(recipientUids.Count));

        await Clients.Users(recipientUids).Client_UserReceiveCharacterData(new OnlineUserCharaDataDto(new UserData(UserUID), dto.CharaData)).ConfigureAwait(false);

        _mareCensus.PublishStatistics(UserUID, dto.CensusDataDto);

        _mareMetrics.IncCounter(MetricsAPI.CounterUserPushData);
        _mareMetrics.IncCounter(MetricsAPI.CounterUserPushDataTo, recipientUids.Count);
    }

    [Authorize(Policy = "Identified")]
    public async Task UserRemovePair(UserDto dto)
    {
        _logger.LogCallInfo(MareHubLogger.Args(dto));

        if (string.Equals(dto.User.UID, UserUID, StringComparison.Ordinal)) return;

        // check if client pair even exists
        ClientPair callerPair =
            await DbContext.ClientPairs.SingleOrDefaultAsync(w => w.UserUID == UserUID && w.OtherUserUID == dto.User.UID).ConfigureAwait(false);
        if (callerPair == null) return;

        var pairData = await GetPairInfo(UserUID, dto.User.UID).ConfigureAwait(false);

        // delete from database, send update info to users pair list
        DbContext.ClientPairs.Remove(callerPair);
        await DbContext.SaveChangesAsync().ConfigureAwait(false);

        _logger.LogCallInfo(MareHubLogger.Args(dto, "Success"));

        await Clients.User(UserUID).Client_UserRemoveClientPair(dto).ConfigureAwait(false);

        // check if opposite entry exists
        if (!pairData.IndividuallyPaired) return;

        // check if other user is online, if no then there is no need to do anything further
        var otherIdent = await GetUserIdent(dto.User.UID).ConfigureAwait(false);
        if (otherIdent == null) return;

        // if the other user had paused the user the state will be offline for either, do nothing
        bool callerHadPaused = pairData.OwnPermissions?.IsPaused ?? false;

        // send updated individual pair status
        await Clients.User(dto.User.UID)
            .Client_UpdateUserIndividualPairStatusDto(new(new(UserUID), IndividualPairStatus.OneSided))
            .ConfigureAwait(false);

        UserPermissionSet? otherPermissions = pairData.OtherPermissions;
        bool otherHadPaused = otherPermissions?.IsPaused ?? true;

        // if the either had paused, do nothing
        if (callerHadPaused && otherHadPaused) return;

        var currentPairData = await GetPairInfo(UserUID, dto.User.UID).ConfigureAwait(false);

        // if neither user had paused each other and either is not in an unpaused group with each other, change state to offline
        if (!currentPairData?.IsSynced ?? true)
        {
            await Clients.User(UserUID).Client_UserSendOffline(dto).ConfigureAwait(false);
            await Clients.User(dto.User.UID).Client_UserSendOffline(new(new(UserUID))).ConfigureAwait(false);
        }
    }

    [Authorize(Policy = "Identified")]
    public async Task UserReportProfile(UserProfileReportDto dto)
    {
        _logger.LogCallInfo(MareHubLogger.Args(dto));

        UserProfileDataReport report = await DbContext.UserProfileReports.SingleOrDefaultAsync(u => u.ReportedUserUID == dto.User.UID && u.ReportingUserUID == UserUID).ConfigureAwait(false);
        if (report != null)
        {
            await Clients.Caller.Client_ReceiveServerMessage(MessageSeverity.Error, "你已经举报了该用户, 正在等待处理").ConfigureAwait(false);
            return;
        }

        UserProfileData profile = await DbContext.UserProfileData.SingleOrDefaultAsync(u => u.UserUID == dto.User.UID).ConfigureAwait(false);
        if (profile == null)
        {
            // await Clients.Caller.Client_ReceiveServerMessage(MessageSeverity.Error, "This user has no profile").ConfigureAwait(false);
            // return;
            profile = new UserProfileData(){
                UserUID = dto.User.UID,
                Base64ProfileImage = null,
                UserDescription = null,
                IsNSFW = false
            };
        }

        UserProfileDataReport reportToAdd = new()
        {
            ReportDate = DateTime.UtcNow,
            ReportingUserUID = UserUID,
            ReportReason = dto.ProfileReport,
            ReportedUserUID = dto.User.UID,
        };

        profile.FlaggedForReport = true;

        await DbContext.UserProfileReports.AddAsync(reportToAdd).ConfigureAwait(false);

        await DbContext.SaveChangesAsync().ConfigureAwait(false);

        await Clients.User(dto.User.UID).Client_ReceiveServerMessage(MessageSeverity.Warning, "你已被举报，正在等待处理").ConfigureAwait(false);

        var allPairedUsers = await GetAllPairedUnpausedUsers(dto.User.UID).ConfigureAwait(false);
        var pairs = await GetOnlineUsers(allPairedUsers).ConfigureAwait(false);

        await Clients.Users(pairs.Select(p => p.Key)).Client_UserUpdateProfile(new(dto.User)).ConfigureAwait(false);
        await Clients.Users(dto.User.UID).Client_UserUpdateProfile(new(dto.User)).ConfigureAwait(false);
    }

    [Authorize(Policy = "Identified")]
    public async Task UserSetProfile(UserProfileDto dto)
    {
        _logger.LogCallInfo(MareHubLogger.Args(dto));

        if (!string.Equals(dto.User.UID, UserUID, StringComparison.Ordinal)) throw new HubException("你只能修改你自己的档案");

        var existingData = await DbContext.UserProfileData.SingleOrDefaultAsync(u => u.UserUID == dto.User.UID).ConfigureAwait(false);

        if (existingData?.FlaggedForReport ?? false)
        {
            await Clients.Caller.Client_ReceiveServerMessage(MessageSeverity.Error, "你有待处理的举报").ConfigureAwait(false);
            return;
        }

        if (existingData?.ProfileDisabled ?? false)
        {
            await Clients.Caller.Client_ReceiveServerMessage(MessageSeverity.Error, "你的档案已被禁用，无法编辑").ConfigureAwait(false);
            return;
        }

        if (!string.IsNullOrEmpty(dto.ProfilePictureBase64))
        {
            byte[] imageData = Convert.FromBase64String(dto.ProfilePictureBase64);
            using MemoryStream ms = new(imageData);
            var format = await Image.DetectFormatAsync(ms).ConfigureAwait(false);
            if (!format.FileExtensions.Contains("png", StringComparer.OrdinalIgnoreCase))
            {
                await Clients.Caller.Client_ReceiveServerMessage(MessageSeverity.Error, "图片必须为png格式").ConfigureAwait(false);
                return;
            }
            using var image = Image.Load<Rgba32>(imageData);

            if (image.Width > 256 || image.Height > 256 || (imageData.Length > 250 * 1024))
            {
                await Clients.Caller.Client_ReceiveServerMessage(MessageSeverity.Error, "图片不能大于 256x256 且不能大于 250KB.").ConfigureAwait(false);
                return;
            }
        }

        if (existingData != null)
        {
            if (string.Equals("", dto.ProfilePictureBase64, StringComparison.OrdinalIgnoreCase))
            {
                existingData.Base64ProfileImage = null;
            }
            else if (dto.ProfilePictureBase64 != null)
            {
                existingData.Base64ProfileImage = dto.ProfilePictureBase64;
            }

            if (dto.IsNSFW != null)
            {
                existingData.IsNSFW = dto.IsNSFW.Value;
            }

            if (dto.Description != null)
            {
                existingData.UserDescription = dto.Description;
            }
        }
        else
        {
            UserProfileData userProfileData = new()
            {
                UserUID = dto.User.UID,
                Base64ProfileImage =null,
                UserDescription =null,
                IsNSFW =false
            };

            await DbContext.UserProfileData.AddAsync(userProfileData).ConfigureAwait(false);
        }

        await DbContext.SaveChangesAsync().ConfigureAwait(false);

        var allPairedUsers = await GetAllPairedUnpausedUsers().ConfigureAwait(false);
        var pairs = await GetOnlineUsers(allPairedUsers).ConfigureAwait(false);

        await Clients.Users(pairs.Select(p => p.Key)).Client_UserUpdateProfile(new(dto.User)).ConfigureAwait(false);
        await Clients.Caller.Client_UserUpdateProfile(new(dto.User)).ConfigureAwait(false);
    }

    [Authorize(Policy = "Identified")]
    public async Task UpdateLocation(LocationDto dto, bool offline = false)
    {
        _logger.LogCallInfo(MareHubLogger.Args(dto));

        var permissibleGroupGIDsQuery = DbContext.GroupPairPreferredPermissions.AsNoTracking()
            .Where(gpp => gpp.UserUID == dto.user.UID && !gpp.IsPaused && gpp.ShareLocation == true)
            .Select(gpp => gpp.GroupGID);

        var groupUserUIDsQuery = DbContext.GroupPairs.AsNoTracking()
            .Where(gp => permissibleGroupGIDsQuery.Contains(gp.GroupGID))
            .Select(gp => gp.GroupUserUID)
            .Distinct();

        var directlySharedUserUIDsQuery = DbContext.Permissions.AsNoTracking()
            .Where(p => p.UserUID == dto.user.UID && !p.IsPaused && p.ShareLocation == true)
            .Select(p => p.OtherUserUID);

        var allUsers = directlySharedUserUIDsQuery.ToList()
            .Union(groupUserUIDsQuery.ToList(), StringComparer.Ordinal).Distinct(StringComparer.Ordinal);

        if (offline)
        {
            await _redis.RemoveAsync("Location:" +  dto.user.UID, StackExchange.Redis.CommandFlags.FireAndForget).ConfigureAwait(false);
        }
        else
        {

            _logger.LogCallInfo([$"Adding: {dto}"]);
            var res =  await _redis.AddAsync($"Location:{dto.user.UID}", dto).ConfigureAwait(false);
            if (!res) _logger.LogCallWarning([$"Failed to add: {dto}"]);
        }

        await Clients.Users(allUsers).Client_SendLocationToClient(dto).ConfigureAwait(false);
    }

    [Authorize(Policy = "Identified")]
    public async Task<List<LocationDto>> RequestLocationInfo()
    {
        _logger.LogCallInfo();
        var uids = await GetAllPairedUnpausedUsers().ConfigureAwait(false);
        var data =await _redis.GetAllAsync<LocationDto>(uids.Select(x => $"Location:{x}").ToHashSet(StringComparer.Ordinal))
            .ConfigureAwait(false);
        return data.Select(x => x.Value).ToList();
    }

    [GeneratedRegex(@"^([a-z0-9_ '+&,\.\-\{\}]+\/)+([a-z0-9_ '+&,\.\-\{\}]+\.[a-z]{3,4})$", RegexOptions.IgnoreCase | RegexOptions.Compiled | RegexOptions.ECMAScript)]
    private static partial Regex GamePathRegex();

    [GeneratedRegex(@"^[A-Z0-9]{40}$", RegexOptions.IgnoreCase | RegexOptions.Compiled | RegexOptions.ECMAScript)]
    private static partial Regex HashRegex();

    [GeneratedRegex("^[-a-zA-Z0-9@:%._\\+~#=]{1,256}[\\.,][a-zA-Z0-9()]{1,6}\\b(?:[-a-zA-Z0-9()@:%_\\+.~#?&\\/=]*)$")]
    private static partial Regex UrlRegex();

    private ClientPair OppositeEntry(string otherUID) =>
                                    DbContext.ClientPairs.AsNoTracking().SingleOrDefault(w => w.User.UID == otherUID && w.OtherUser.UID == UserUID);
}