using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Mvc;
using OneSake.Server.Services;

namespace OneSake.Server.Controllers;

// POST /bugreports - "Fehler melden" from the match.
// GET  /bugreports - list for admins (header X-Admin-Token, only when BugReports:AdminToken is set).
[ApiController]
[Route("bugreports")]
public class BugReportsController : ControllerBase
{
    private readonly BugReportService _service;
    private readonly IBugReportStore _store;
    private readonly IConfiguration _configuration;

    public BugReportsController(BugReportService service, IBugReportStore store, IConfiguration configuration)
    {
        _service = service;
        _store = store;
        _configuration = configuration;
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] BugReportRequest request, CancellationToken cancellationToken)
    {
        var clientKey = Match.GuestIdValidator.Normalize(request.GuestId)
            ?? HttpContext.Connection.RemoteIpAddress?.ToString()
            ?? "unknown";
        var result = await _service.SubmitAsync(request, clientKey, cancellationToken);

        if (result.RateLimited)
        {
            return StatusCode(StatusCodes.Status429TooManyRequests, new { error = result.Error });
        }

        if (result.Id is null)
        {
            return result.Error?.StartsWith("The report could not be saved", StringComparison.Ordinal) == true
                ? StatusCode(StatusCodes.Status503ServiceUnavailable, new { error = result.Error })
                : BadRequest(new { error = result.Error });
        }

        return Ok(new { id = result.Id });
    }

    [HttpGet]
    public async Task<IActionResult> List(CancellationToken cancellationToken)
    {
        var token = _configuration["BugReports:AdminToken"];
        if (string.IsNullOrWhiteSpace(token))
        {
            // Without a configured token the list does not exist.
            return NotFound();
        }

        var sent = Request.Headers["X-Admin-Token"].ToString();
        if (!CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(sent), Encoding.UTF8.GetBytes(token)))
        {
            return Unauthorized();
        }

        try
        {
            return Ok(await _store.ListAsync(200, cancellationToken));
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            HttpContext.RequestServices.GetRequiredService<ILogger<BugReportsController>>()
                .LogWarning(exception, "Could not load bug reports.");
            return StatusCode(StatusCodes.Status503ServiceUnavailable);
        }
    }
}
