using MareSynchronos.API.Routes;
using MareSynchronosStaticFilesServer.Services;
using MareSynchronosStaticFilesServer.Utils;
using Microsoft.AspNetCore.Mvc;

namespace MareSynchronosStaticFilesServer.Controllers;

[Route(MareFiles.Cache)]
public class CacheController : ControllerBase
{
    private readonly RequestFileStreamResultFactory _requestFileStreamResultFactory;
    private readonly CachedFileProvider _cachedFileProvider;
    private readonly RequestQueueService _requestQueue;
    private readonly FileStatisticsService _fileStatisticsService;

    public CacheController(ILogger<CacheController> logger, RequestFileStreamResultFactory requestFileStreamResultFactory,
        CachedFileProvider cachedFileProvider, RequestQueueService requestQueue, FileStatisticsService fileStatisticsService) : base(logger)
    {
        _requestFileStreamResultFactory = requestFileStreamResultFactory;
        _cachedFileProvider = cachedFileProvider;
        _requestQueue = requestQueue;
        _fileStatisticsService = fileStatisticsService;
    }

    [HttpGet(MareFiles.Cache_Get)]
    public async Task<IActionResult> GetFiles(Guid requestId)
    {
        _logger.LogDebug($"GetFile:{MareUser}:{requestId}");

        if (!_requestQueue.IsActiveProcessing(requestId, MareUser, out var request)) return BadRequest();

        _requestQueue.ActivateRequest(requestId, MareUser);

        Response.ContentType = "application/octet-stream";
        Response.Headers.CacheControl = "public, max-age=604800";

        long requestSize = 0;
        List<BlockFileDataSubstream> substreams = new();

        foreach (var fileHash in request.FileIds)
        {
            var fs = await _cachedFileProvider.DownloadAndGetLocalFileInfo(fileHash).ConfigureAwait(false);
            if (fs == null) continue;

            substreams.Add(new(fs));

            requestSize += fs.Length;
        }

        _fileStatisticsService.LogRequest(requestSize, MareUser);

        return _requestFileStreamResultFactory.Create(requestId, MareUser, new BlockFileDataStream(substreams));
    }

    [HttpGet(MareFiles.Cache_Get_Single)]
    public async Task<IActionResult> GetSingle(string hash)
    {
        string customRequestIdHeader = Request.Headers["X-Request-ID"];

        if (string.IsNullOrEmpty(customRequestIdHeader) || !Guid.TryParse(customRequestIdHeader, out var requestId))
        {
            return BadRequest("Request ID is missing or invalid.");
        }

        _logger.LogDebug("GetFileSingle:{user}:{requestId}:{hash}", MareUser, requestId, hash);

        // if (!_requestQueue.IsActiveProcessing(requestId, MareUser, out var request)) return BadRequest();
        //
        // _requestQueue.ActivateRequest(requestId, MareUser);

        Response.ContentType = "application/octet-stream";
        Response.Headers.Append("X-Request-ID", requestId.ToString());

        long requestSize = 0;

        List<BlockFileDataSubstream> substreams = new();
        var fs = await _cachedFileProvider.DownloadAndGetLocalFileInfo(hash).ConfigureAwait(false);
        if (fs == null) return NotFound();
        substreams.Add(new(fs));
        requestSize += fs.Length;
        if (requestSize > 0)
        {
            Response.Headers.CacheControl = "public, max-age=604800";
        }
        else
        {
            Response.Headers.CacheControl = "private, no-cache";
        }
        _fileStatisticsService.LogRequest(requestSize, MareUser);
        return _requestFileStreamResultFactory.Create(requestId, MareUser, new BlockFileDataStream(substreams));

    }

}