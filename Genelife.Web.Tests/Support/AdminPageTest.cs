using Bunit;
using Genelife.Web.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Genelife.Web.Tests.Support;

/// <summary>
/// Base for admin page tests. Registers a <see cref="GameDataService"/> backed by
/// <see cref="StubGameData"/> so every page under test reads from declared
/// fixtures instead of a live API.
/// </summary>
public abstract class AdminPageTest : BunitContext
{
    protected StubGameData Stub { get; }

    protected AdminPageTest()
    {
        Stub = new StubGameData();

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ApiBaseUrl"] = "http://localhost/",
                ["AdminPlayerId"] = "test-admin"
            })
            .Build();

        Services.AddSingleton(new GameDataService(Stub.CreateClient(), configuration));
    }

    /// <summary>
    /// Fails when the page requested an endpoint the test never declared. Without
    /// this a missing fixture renders as an unexplained empty state.
    /// </summary>
    protected void AssertNoUndeclaredRequests()
    {
        Assert.True(
            Stub.UnmatchedRequests.Count == 0,
            $"page requested undeclared endpoints: {string.Join(", ", Stub.UnmatchedRequests)}");
    }
}
