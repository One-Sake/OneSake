using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using OneSake.Game.Replay;
using OneSake.Persistence.Entities;
using OneSake.Server.Controllers;
using OneSake.Server.Match;
using OneSake.Server.Services;

namespace OneSake.Tests.Replays;

public class BugReportsControllerTests
{
    private sealed class ListStore : IBugReportStore
    {
        public Task<int> SaveAsync(BugReport report, MatchRecording? recording, CancellationToken cancellationToken = default) => Task.FromResult(1);

        public Task<IReadOnlyList<BugReportListItem>> ListAsync(int take, CancellationToken cancellationToken = default)
        {
            return Task.FromResult<IReadOnlyList<BugReportListItem>>(
                [new BugReportListItem(1, "EFFECT", "OP01-016", "text", "ROOM1", 3, true, DateTime.UtcNow)]);
        }
    }

    private static BugReportsController Create(string? adminToken, string? sentToken)
    {
        var store = new ListStore();
        var settings = new Dictionary<string, string?> { ["BugReports:AdminToken"] = adminToken };
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(settings).Build();
        var service = new BugReportService(new MatchRoomManager(), store, NullLogger<BugReportService>.Instance);
        var context = new DefaultHttpContext();
        if (sentToken is not null)
        {
            context.Request.Headers["X-Admin-Token"] = sentToken;
        }

        return new BugReportsController(service, store, configuration) { ControllerContext = new ControllerContext { HttpContext = context } };
    }

    [Fact]
    public async Task List_IsOff_WithoutAConfiguredToken()
    {
        Assert.IsType<NotFoundResult>(await Create(null, "anything").List(CancellationToken.None));
    }

    [Fact]
    public async Task List_NeedsTheRightToken()
    {
        Assert.IsType<UnauthorizedResult>(await Create("secret-token", null).List(CancellationToken.None));
        Assert.IsType<UnauthorizedResult>(await Create("secret-token", "wrong").List(CancellationToken.None));

        var ok = Assert.IsType<OkObjectResult>(await Create("secret-token", "secret-token").List(CancellationToken.None));
        var item = Assert.Single(Assert.IsAssignableFrom<IReadOnlyList<BugReportListItem>>(ok.Value));
        Assert.Equal("OP01-016", item.CardId);
    }

    [Fact]
    public async Task Create_ReturnsTheNumber_OrABadRequest()
    {
        var controller = Create(null, null);

        var ok = Assert.IsType<OkObjectResult>(await controller.Create(new BugReportRequest { RoomCode = "ROOM1", Description = "x", Category = "OTHER" }, CancellationToken.None));
        Assert.Contains("id", System.Text.Json.JsonSerializer.Serialize(ok.Value));

        Assert.IsType<BadRequestObjectResult>(await controller.Create(new BugReportRequest { RoomCode = "ROOM1", Description = "", Category = "OTHER" }, CancellationToken.None));
    }
}
