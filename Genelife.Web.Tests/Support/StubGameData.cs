using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace Genelife.Web.Tests.Support;

/// <summary>
/// Stub transport for the admin pages.
///
/// Every data page injects the same sealed <c>GameDataService</c> over a single
/// <see cref="HttpClient"/>, so a stub <see cref="HttpMessageHandler"/> is the
/// whole seam — no mocking library, and the real serialization path is exercised.
///
/// Auth is handled automatically: a test declares only the data endpoints its
/// page actually reads. Endpoints nobody declared return 404 and are recorded in
/// <see cref="UnmatchedRequests"/>, so a page that quietly grows a new call shows
/// up here instead of silently rendering empty.
/// </summary>
public sealed class StubGameData : HttpMessageHandler
{
    private static readonly JsonSerializerOptions WebJson = new(JsonSerializerDefaults.Web);

    private readonly Dictionary<string, Func<HttpResponseMessage>> routes =
        new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Every request the page made, as "METHOD /path".</summary>
    public List<string> Requests { get; } = new();

    /// <summary>Paths the page requested that no test declared.</summary>
    public List<string> UnmatchedRequests { get; } = new();

    public StubGameData()
    {
        // The service falls back to a player token when local admin is refused;
        // both are answered so tests never have to think about authentication.
        routes["/api/auth/local-admin"] = () => Json(new
        {
            accessToken = "test-admin-token",
            expiresAt = DateTimeOffset.UtcNow.AddHours(1)
        });
        routes["/api/auth/token"] = () => Json(new
        {
            accessToken = "test-player-token",
            expiresAt = DateTimeOffset.UtcNow.AddHours(1)
        });
    }

    /// <summary>Declares a successful GET/POST returning <paramref name="body"/>.</summary>
    public StubGameData Ok(string path, object body)
    {
        routes[Normalize(path)] = () => Json(body);
        return this;
    }

    /// <summary>Declares an endpoint that fails, for exercising error states.</summary>
    public StubGameData Fail(string path, HttpStatusCode status = HttpStatusCode.InternalServerError)
    {
        routes[Normalize(path)] = () => new HttpResponseMessage(status);
        return this;
    }

    /// <summary>Declares a successful call that returns no body.</summary>
    public StubGameData NoContent(string path)
    {
        routes[Normalize(path)] = () => new HttpResponseMessage(HttpStatusCode.NoContent);
        return this;
    }

    public HttpClient CreateClient() => new(this, disposeHandler: false)
    {
        BaseAddress = new Uri("http://localhost/")
    };

    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        var uri = request.RequestUri!;
        var path = uri.AbsolutePath;
        var withQuery = $"{path}{uri.Query}";
        var method = request.Method.Method;
        Requests.Add($"{method} {withQuery}");

        // Prefer an exact path+query match, then fall back to a path-only route
        // so a test can declare one handler for every query a page might send.
        if (routes.TryGetValue(withQuery, out var exact))
            return Task.FromResult(exact());
        if (routes.TryGetValue(path, out var byPath))
            return Task.FromResult(byPath());

        UnmatchedRequests.Add($"{method} {withQuery}");
        return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound));
    }

    private static HttpResponseMessage Json(object body) =>
        new(HttpStatusCode.OK) { Content = JsonContent.Create(body, options: WebJson) };

    private static string Normalize(string path) =>
        path.StartsWith('/') ? path : $"/{path}";
}
