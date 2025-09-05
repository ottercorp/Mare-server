using MareSynchronosShared.Metrics;
using MareSynchronosStaticFilesServer.Services;
using Microsoft.AspNetCore.Mvc;

namespace MareSynchronosStaticFilesServer.Utils;

public class RequestFileStreamResult : FileStreamResult
{
    private readonly Guid _requestId;
    private readonly string _userid;
    private readonly RequestQueueService _requestQueueService;
    private readonly MareMetrics _mareMetrics;

    public RequestFileStreamResult(Guid requestId, string userId,  RequestQueueService requestQueueService, MareMetrics mareMetrics,
        Stream fileStream, string contentType) : base(fileStream, contentType)
    {
        _requestId = requestId;
        _requestQueueService = requestQueueService;
        _mareMetrics = mareMetrics;
        _userid = userId;
        _mareMetrics.IncGauge(MetricsAPI.GaugeCurrentDownloads);
    }

    public override void ExecuteResult(ActionContext context)
    {
        var response = context.HttpContext.Response;
        try
        {
            base.ExecuteResult(context);
        }
        catch
        {
            throw;
        }
        finally
        {
            _requestQueueService.FinishRequest(_requestId, _userid);

            _mareMetrics.DecGauge(MetricsAPI.GaugeCurrentDownloads);
            FileStream?.Dispose();
        }
    }

    public override async Task ExecuteResultAsync(ActionContext context)
    {
        var response = context.HttpContext.Response;
        try
        {
            await base.ExecuteResultAsync(context).ConfigureAwait(false);
        }
        catch
        {
            throw;
        }
        finally
        {
            _requestQueueService.FinishRequest(_requestId, _userid);
            _mareMetrics.DecGauge(MetricsAPI.GaugeCurrentDownloads);
            FileStream?.Dispose();
        }
    }
}