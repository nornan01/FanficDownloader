using FanficDownloader.Application.Services;
using FanficDownloader.Web.Dtos;
using Microsoft.AspNetCore.Mvc;
using FanficDownloader.Core.Models;
using FanficDownloader.Application.Models;


namespace FanficDownloader.Web.Controllers;

[ApiController]
[Route("download")]
public class DownloadController : ControllerBase
{
    private readonly DownloadQueueService _queue;
    private readonly FanficDownloadService _downloadService;

    public DownloadController(DownloadQueueService queue, FanficDownloadService downloadService)
    {
        _queue = queue;
        _downloadService = downloadService;
    }

    [HttpPost("txt")]
    public async Task<IActionResult> DownloadTxt([FromForm] DownloadRequest request,
                                             CancellationToken ct)
    {
        
        var tcs = new TaskCompletionSource<DownloadFileResult>();
        var position = _queue.GetQueueLength() + 1;
        Response.Headers["X-Queue-Position"] = position.ToString();

        await _queue.Enqueue(async () =>
        {
            try
            {
                var file = await _downloadService.BuildTxtAsync(request.Url, ct);
                tcs.SetResult(file);
            }
            catch (Exception ex)
            {
                tcs.SetException(ex);
            }
        });

        var result = await tcs.Task;

        return File(result.Bytes, result.ContentType, result.FileName);
    }


    [HttpPost("epub")]
    public async Task<IActionResult> DownloadEpub([FromForm] DownloadRequest request,
                                              CancellationToken ct)
    {
        
        var tcs = new TaskCompletionSource<DownloadFileResult>();

        var position = _queue.GetQueueLength() + 1;
        Response.Headers["X-Queue-Position"] = position.ToString();

        await _queue.Enqueue(async () =>
        {
            try
            {
                var file = await _downloadService.BuildEpubAsync(request.Url, ct);
                tcs.SetResult(file);
            }
            catch (Exception ex)
            {
                tcs.SetException(ex);
            }
        });

        var result = await tcs.Task;

        return File(result.Bytes, result.ContentType, result.FileName);
    }


    [HttpPost("info")]
    public async Task<IActionResult> GetInfo([FromForm] DownloadRequest request,
                                         CancellationToken ct)
    {
        try
        {
            var fanfic = await _downloadService.GetInfoAsync(request.Url, ct);
            return Ok(fanfic);
        }
        catch (NotSupportedException)
        {
            return BadRequest("Source not supported");
        }
    }

}
