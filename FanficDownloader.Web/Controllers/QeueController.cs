using Microsoft.AspNetCore.Mvc;
using FanficDownloader.Web.Services;

namespace FanficDownloader.Web.Controllers;

[ApiController]
[Route("queue")]
public class QueueController : ControllerBase
{
    private readonly DownloadQueueService _queue;

    public QueueController(DownloadQueueService queue)
    {
        _queue = queue;
    }

    [HttpGet("position")]
    public IActionResult GetPosition()
    {
        var current = _queue.GetQueueLength();
        return Ok(new { position = current });
    }
}