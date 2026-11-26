using System.Net;
using System.Net.Http.Json;
using Bunit;
using Genelife.Web.Components;
using Genelife.Web.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Genelife.Web.Tests.Components;

/// <summary>
/// Covers <see cref="SimulationControl"/>, the one admin component with no
/// rendered-output coverage. It is also the most stateful: it polls the API on a
/// timer, distinguishes "stopped" from "status unknown", and gates Start/Stop
/// behind an admin token, so a regression here silently misreports whether the
/// simulation is running.
///
/// The service is a concrete sealed class over HttpClient, so the tests drive it
/// with a stub <see cref="HttpMessageHandler"/> rather than a mocking library.
/// </summary>
public class SimulationControlTests : BunitContext
{
    private sealed class StubHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, HttpResponseMessage> respond;

        public StubHandler(Func<HttpRequestMessage, HttpResponseMessage> respond) => this.respond = respond;

        public List<string> Requests { get; } = new();

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            Requests.Add($"{request.Method.Method} {request.RequestUri!.AbsolutePath}");
            return Task.FromResult(respond(request));
        }
    }

    private static HttpResponseMessage Json(object body, HttpStatusCode status = HttpStatusCode.OK) =>
        new(status) { Content = JsonContent.Create(body) };

    private static HttpResponseMessage Token() =>
        Json(new { accessToken = "test-token", expiresAt = DateTimeOffset.UtcNow.AddHours(1) });

    /// <summary>
    /// Registers a <see cref="GameDataService"/> backed by a stub transport.
    /// <paramref name="adminAvailable"/> selects whether the local-admin token
    /// endpoint succeeds, which is what gates the Start/Stop commands.
    /// </summary>
    private StubHandler RegisterGameData(
        bool running,
        bool adminAvailable = true,
        HttpStatusCode statusCode = HttpStatusCode.OK)
    {
        var handler = new StubHandler(request => request.RequestUri!.AbsolutePath switch
        {
            "/api/auth/local-admin" => adminAvailable
                ? Token()
                : new HttpResponseMessage(HttpStatusCode.NotFound),
            "/api/auth/token" => Token(),
            "/api/simulation/status" => statusCode == HttpStatusCode.OK
                ? Json(new { isRunning = running })
                : new HttpResponseMessage(statusCode),
            "/api/simulation/start" => new HttpResponseMessage(HttpStatusCode.OK),
            "/api/simulation/stop" => new HttpResponseMessage(HttpStatusCode.OK),
            _ => new HttpResponseMessage(HttpStatusCode.NotFound)
        });

        var http = new HttpClient(handler) { BaseAddress = new Uri("http://localhost/") };
        Services.AddSingleton(new GameDataService(http, new ConfigurationBuilder().Build()));
        return handler;
    }

    [Fact]
    public void RunningSimulationShowsStopButton()
    {
        RegisterGameData(running: true);

        var cut = Render<SimulationControl>();

        cut.WaitForAssertion(() => Assert.Contains("Simulation running", cut.Markup));
        Assert.Contains("Stop", cut.Markup);
        Assert.DoesNotContain(">Start<", cut.Markup);
    }

    [Fact]
    public void StoppedSimulationShowsStartButton()
    {
        RegisterGameData(running: false);

        var cut = Render<SimulationControl>();

        cut.WaitForAssertion(() => Assert.Contains("Simulation stopped", cut.Markup));
        Assert.Contains("Start", cut.Markup);
        Assert.DoesNotContain(">Stop<", cut.Markup);
    }

    [Fact]
    public void RunningStateUsesTheSuccessBadge()
    {
        RegisterGameData(running: true);

        var cut = Render<SimulationControl>();

        cut.WaitForAssertion(() => Assert.Contains("text-bg-success", cut.Find(".badge").ClassList));
    }

    [Fact]
    public void FailedStatusFetchReportsUnknownRatherThanStopped()
    {
        RegisterGameData(running: false, statusCode: HttpStatusCode.InternalServerError);

        var cut = Render<SimulationControl>();

        // A failed poll must not be reported as "stopped" -- that would tell an
        // operator the simulation is idle when the API is simply unreachable.
        cut.WaitForAssertion(() => Assert.Contains("Simulation status unknown", cut.Markup));
        Assert.DoesNotContain("Simulation stopped", cut.Markup);
        Assert.Contains("simulation-error", cut.Markup);
    }

    [Fact]
    public void StartButtonPostsToTheStartEndpoint()
    {
        var handler = RegisterGameData(running: false);

        var cut = Render<SimulationControl>();
        cut.WaitForAssertion(() => Assert.Contains("Simulation stopped", cut.Markup));

        cut.Find("button.btn-success").Click();

        cut.WaitForAssertion(() => Assert.Contains("POST /api/simulation/start", handler.Requests));
    }

    [Fact]
    public void StopButtonPostsToTheStopEndpoint()
    {
        var handler = RegisterGameData(running: true);

        var cut = Render<SimulationControl>();
        cut.WaitForAssertion(() => Assert.Contains("Simulation running", cut.Markup));

        cut.Find("button.btn-outline-danger").Click();

        cut.WaitForAssertion(() => Assert.Contains("POST /api/simulation/stop", handler.Requests));
    }

    [Fact]
    public void StartWithoutAdminAccessSurfacesTheError()
    {
        RegisterGameData(running: false, adminAvailable: false);

        var cut = Render<SimulationControl>();
        cut.WaitForAssertion(() => Assert.Contains("Simulation stopped", cut.Markup));

        cut.Find("button.btn-success").Click();

        cut.WaitForAssertion(() => Assert.Contains("Local admin access is disabled", cut.Markup));
    }

    [Fact]
    public void DisposingWhilePollingDoesNotThrow()
    {
        RegisterGameData(running: true);

        var cut = Render<SimulationControl>();
        cut.WaitForAssertion(() => Assert.Contains("Simulation running", cut.Markup));

        // The component starts a fire-and-forget poll loop; disposal must cancel
        // it cleanly instead of surfacing an unobserved task exception.
        var exception = Record.Exception(() => DisposeComponentsAsync().GetAwaiter().GetResult());

        Assert.Null(exception);
    }
}
