using System.Reflection;
using System.Text.RegularExpressions;
using Genelife.Api.Commands;

namespace Genelife.UnitTests.Docs;

/// <summary>
/// Fails when a roadmap or architecture status page drifts from the code. These
/// are deliberately narrow: each assertion names a path that the 2026-11 V5
/// cleanup changed so a future revert or rename breaks the test instead of
/// sending the next engineer to a code path that no longer exists.
/// </summary>
[Trait("Category", "Documentation")]
public class DocsConsistencyTests
{
    private static string RepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "GeneLife.sln")))
            directory = directory.Parent;

        return directory?.FullName ?? throw new InvalidOperationException("Repository root not found.");
    }

    private static string Read(string relativePath) =>
        File.ReadAllText(Path.Combine(RepositoryRoot(), relativePath));

    [Fact]
    public void No_feature_flag_or_conditional_architecture_path_remains()
    {
        Assert.DoesNotContain("FeatureFlags", Read("Genelife.API/appsettings.json"));
        Assert.DoesNotContain("FeatureFlags", Read("Genelife.API/Program.cs"));
        Assert.DoesNotContain("useWolverineEndpoints", Read("Genelife.API/Program.cs"));
        Assert.DoesNotContain("useMartenReads", Read("Genelife.API/Program.cs"));
    }

    [Fact]
    public void Legacy_endpoint_and_saga_files_are_gone()
    {
        var removed = new[]
        {
            "Genelife.API/Endpoints/AuthEndpoints.cs",
            "Genelife.API/Endpoints/SimulationEndpoints.cs",
            "Genelife.API/Endpoints/CompanyEndpoints.cs",
            "Genelife.API/Endpoints/WorldDataEndpoints.cs",
            "Genelife.API/Sagas/HiringSaga.cs",
            "Genelife.API/Sagas/HiringState.cs"
        };

        foreach (var path in removed)
            Assert.False(File.Exists(Path.Combine(RepositoryRoot(), path)), $"{path} should be removed");
    }

    [Fact]
    public void Moved_routes_are_declared_on_wolverine_endpoints()
    {
        var endpoints = string.Concat(Directory
            .GetFiles(Path.Combine(RepositoryRoot(), "Genelife.API/Endpoints/Wolverine"), "*.cs")
            .Select(File.ReadAllText));

        foreach (var route in new[]
                 {
                     "/api/auth/token",
                     "/api/auth/local-admin",
                     "/api/simulation/start",
                     "/api/simulation/stop",
                     "/api/simulation/status",
                     "/api/simulation/metrics",
                     "/api/simulation/sims",
                     "/api/simulation/save",
                     "/api/simulation/restore",
                     "/api/data",
                     "/api/map/create",
                     "/api/map/buildings/{networkId}/relocate"
                 })
            Assert.Contains(route, endpoints);
    }

    [Fact]
    public void V2_status_documents_relocation_and_default_animation()
    {
        var status = Read("docs/plans/roadmap-v2/STATUS.md");

        Assert.Contains("RelocateBuildingCommand", status);
        Assert.Contains("default-on", status);
        Assert.DoesNotContain("does not yet support relocation", status);
        Assert.DoesNotContain("`CommuteMode.Animate` is opt-in", status);
    }

    [Fact]
    public void Completed_roadmap_status_documents_exist()
    {
        foreach (var doc in new[] { "docs/plans/roadmap-v2/STATUS.md", "docs/plans/roadmap-v3/STATUS.md", "docs/plans/roadmap-v4/STATUS.md" })
            Assert.True(File.Exists(Path.Combine(RepositoryRoot(), doc)), doc);
    }
}

[Trait("Category", "Documentation")]
public class CommandCoverageTests
{
    private static string EngineSource() =>
        File.ReadAllText(Path.Combine(DocsConsistencyTestsRepositoryRoot(), "Genelife.Application/Services/SimulationEngine.cs"));

    [Fact]
    public void Every_admin_command_has_an_apply_case()
    {
        var source = EngineSource();
        var cases = Regex.Matches(source, @"case\s+([A-Za-z0-9_]+Command)\s+[a-zA-Z_]")
            .Select(match => match.Groups[1].Value)
            .ToHashSet(StringComparer.Ordinal);

        var commands = typeof(AdminCommand).Assembly.GetTypes()
            .Where(type => type.IsSubclassOf(typeof(AdminCommand)) && !type.IsAbstract)
            .Select(type => type.Name)
            .OrderBy(name => name)
            .ToArray();

        var missing = commands.Where(command => !cases.Contains(command)).ToArray();
        Assert.Empty(missing);
    }

    private static string DocsConsistencyTestsRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "GeneLife.sln")))
            directory = directory.Parent;
        return directory?.FullName ?? throw new InvalidOperationException("Repository root not found.");
    }
}
