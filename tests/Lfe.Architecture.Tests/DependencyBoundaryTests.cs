using System.Xml.Linq;

namespace Lfe.Architecture.Tests;

public sealed class DependencyBoundaryTests
{
    [Fact]
    public void SourceProjectReferencesFollowRevision2DependencyDirection()
    {
        var violations = ProjectReferenceScanner.LoadFromSourceTree(RepositoryPaths.SourceDirectory)
            .ValidateDependencyDirection()
            .ToArray();

        Assert.True(violations.Length == 0, string.Join(Environment.NewLine, violations));
    }

    [Fact]
    public void HostNeutralCoreRuntimeProjectsDoNotReferenceCodexAdapters()
    {
        var violations = ProjectReferenceScanner.LoadFromSourceTree(RepositoryPaths.SourceDirectory)
            .ValidateNoHostNeutralCodexReferences()
            .ToArray();

        Assert.True(violations.Length == 0, string.Join(Environment.NewLine, violations));
    }

    [Fact]
    public void HostNeutralSdkSourceDoesNotOwnCodexReferenceHostExtension()
    {
        var sourceFiles = Directory.EnumerateFiles(RepositoryPaths.SourceDirectory, "*.cs", SearchOption.AllDirectories)
            .Where(path => !ProjectLayers.CodexOwnedProjects.Contains(RepositoryPaths.ProjectNameFromPath(path)))
            .ToArray();

        var violations = sourceFiles
            .Where(path => File.ReadAllText(path).Contains("UseCodexReferenceHost", StringComparison.Ordinal))
            .Select(path => $"UseCodexReferenceHost must stay in a Codex adapter/preset package, but was found in {Path.GetRelativePath(RepositoryPaths.LfeDirectory, path)}.")
            .ToArray();

        Assert.True(violations.Length == 0, string.Join(Environment.NewLine, violations));
    }

    [Fact]
    public void BoundaryAssertionReportsSyntheticForbiddenReference()
    {
        const string xml = """
            <Project Sdk="Microsoft.NET.Sdk">
              <ItemGroup>
                <ProjectReference Include="..\Lfe.CodexAdapter\Lfe.CodexAdapter.csproj" />
              </ItemGroup>
            </Project>
            """;

        var project = ProjectReferenceScanner.LoadFromXml("Lfe.HashLine", xml);
        var violation = Assert.Single(new[] { project }.ValidateDependencyDirection());

        Assert.Contains("Lfe.HashLine", violation, StringComparison.Ordinal);
        Assert.Contains("PureCapability", violation, StringComparison.Ordinal);
        Assert.Contains("Lfe.CodexAdapter", violation, StringComparison.Ordinal);
        Assert.Contains("HostAdapter", violation, StringComparison.Ordinal);
    }
}

internal static class ProjectReferenceScanner
{
    public static IReadOnlyList<ProjectReferenceGraphNode> LoadFromSourceTree(string sourceDirectory)
    {
        return Directory.EnumerateFiles(sourceDirectory, "*.csproj", SearchOption.AllDirectories)
            .Select(LoadFromFile)
            .OrderBy(project => project.Name, StringComparer.Ordinal)
            .ToArray();
    }

    public static ProjectReferenceGraphNode LoadFromFile(string projectFile)
    {
        var document = XDocument.Load(projectFile);
        var projectDirectory = Path.GetDirectoryName(projectFile) ?? throw new InvalidOperationException($"Project path has no directory: {projectFile}");
        var references = ReadProjectReferenceNames(document, projectDirectory).ToArray();

        return new ProjectReferenceGraphNode(Path.GetFileNameWithoutExtension(projectFile), references);
    }

    public static ProjectReferenceGraphNode LoadFromXml(string projectName, string xml)
    {
        var document = XDocument.Parse(xml);
        return new ProjectReferenceGraphNode(projectName, ReadProjectReferenceNames(document, Environment.CurrentDirectory).ToArray());
    }

    private static IEnumerable<string> ReadProjectReferenceNames(XDocument document, string projectDirectory)
    {
        return document.Descendants()
            .Where(element => element.Name.LocalName == "ProjectReference")
            .Select(element => element.Attribute("Include")?.Value)
            .Where(include => !string.IsNullOrWhiteSpace(include))
            .Select(include => ResolveProjectName(projectDirectory, include!));
    }

    private static string ResolveProjectName(string projectDirectory, string include)
    {
        var normalized = include.Replace('\\', Path.DirectorySeparatorChar).Replace('/', Path.DirectorySeparatorChar);
        var projectPath = Path.GetFullPath(Path.Combine(projectDirectory, normalized));
        return Path.GetFileNameWithoutExtension(projectPath);
    }
}

internal static class DependencyBoundaryAssertions
{
    public static IEnumerable<string> ValidateDependencyDirection(this IEnumerable<ProjectReferenceGraphNode> projects)
    {
        foreach (var project in projects)
        {
            var sourceLayer = ProjectLayers.LayerFor(project.Name);

            foreach (var referencedProject in project.ProjectReferences)
            {
                var targetLayer = ProjectLayers.LayerFor(referencedProject);
                if (sourceLayer > targetLayer || IsAllowedSameLayerReference(sourceLayer, targetLayer))
                {
                    continue;
                }

                yield return $"Forbidden Revision 2 dependency: {project.Name} ({sourceLayer}) references {referencedProject} ({targetLayer}). References must flow inward only: Distribution/HostAdapter -> Composition -> Runtime -> PureCapability -> Contracts.";
            }
        }
    }

    public static IEnumerable<string> ValidateNoHostNeutralCodexReferences(this IEnumerable<ProjectReferenceGraphNode> projects)
    {
        foreach (var project in projects.Where(project => ProjectLayers.IsHostNeutral(project.Name)))
        {
            foreach (var referencedProject in project.ProjectReferences.Where(ProjectLayers.CodexOwnedProjects.Contains))
            {
                yield return $"Forbidden Codex edge dependency: host-neutral project {project.Name} references {referencedProject}. Codex references belong only in adapter, preset, or distribution projects.";
            }
        }
    }

    private static bool IsAllowedSameLayerReference(ProjectLayer sourceLayer, ProjectLayer targetLayer)
    {
        return sourceLayer == targetLayer && sourceLayer is not ProjectLayer.Contracts;
    }
}

internal sealed record ProjectReferenceGraphNode(string Name, IReadOnlyList<string> ProjectReferences);

internal enum ProjectLayer
{
    Contracts = 0,
    PureCapability = 1,
    Runtime = 2,
    Composition = 3,
    HostAdapter = 4,
    Distribution = 5,
}

internal static class ProjectLayers
{
    public static readonly ISet<string> CodexOwnedProjects = new HashSet<string>(StringComparer.Ordinal)
    {
        "Lfe.CodexAdapter",
        "Lfe.CodexMcpBridge",
        "Lfe.CodexAdapter.Demo",
    };

    private static readonly IReadOnlyDictionary<string, ProjectLayer> Layers = new Dictionary<string, ProjectLayer>(StringComparer.Ordinal)
    {
        ["Lfe.UlwHostContract"] = ProjectLayer.Contracts,
        ["Lfe.Protocol"] = ProjectLayer.Contracts,

        ["Lfe.AgentsMd"] = ProjectLayer.PureCapability,
        ["Lfe.AstGrep"] = ProjectLayer.PureCapability,
        ["Lfe.BackgroundAgent"] = ProjectLayer.PureCapability,
        ["Lfe.BoulderState"] = ProjectLayer.PureCapability,
        ["Lfe.CommandExecutor"] = ProjectLayer.PureCapability,
        ["Lfe.CommentChecker"] = ProjectLayer.PureCapability,
        ["Lfe.GitWorktree"] = ProjectLayer.PureCapability,
        ["Lfe.HashLine"] = ProjectLayer.PureCapability,
        ["Lfe.ModelCore"] = ProjectLayer.PureCapability,
        ["Lfe.RulesEngine"] = ProjectLayer.PureCapability,
        ["Lfe.SearchTools"] = ProjectLayer.PureCapability,
        ["Lfe.SessionManager"] = ProjectLayer.PureCapability,
        ["Lfe.SkillMcp"] = ProjectLayer.PureCapability,
        ["Lfe.SkillsCore"] = ProjectLayer.PureCapability,
        ["Lfe.SlashCommand"] = ProjectLayer.PureCapability,
        ["Lfe.TeamModeCore"] = ProjectLayer.PureCapability,
        ["Lfe.Tmux"] = ProjectLayer.PureCapability,
        ["Lfe.TmuxSubagent"] = ProjectLayer.PureCapability,
        ["Lfe.UlwIntent"] = ProjectLayer.PureCapability,
        ["Lfe.Utils"] = ProjectLayer.PureCapability,

        ["Lfe.Hooks"] = ProjectLayer.Runtime,
        ["Lfe.UlwKernel"] = ProjectLayer.Runtime,
        ["Lfe.UlwLoopState"] = ProjectLayer.Runtime,

        ["Lfe.AgentOs"] = ProjectLayer.Composition,
        ["Lfe.AgentOs.OmoPreset"] = ProjectLayer.Composition,

        ["Lfe.AstGrepMcp"] = ProjectLayer.HostAdapter,
        ["Lfe.CodexAdapter"] = ProjectLayer.HostAdapter,
        ["Lfe.CodexMcpBridge"] = ProjectLayer.HostAdapter,
        ["Lfe.LspTools"] = ProjectLayer.HostAdapter,

        ["Lfe.CodexAdapter.Demo"] = ProjectLayer.Distribution,
        ["Lfe.Sidecar"] = ProjectLayer.Distribution,
        ["Lfe.StandaloneRuntime"] = ProjectLayer.Distribution,
    };

    public static ProjectLayer LayerFor(string projectName)
    {
        if (Layers.TryGetValue(projectName, out var layer))
        {
            return layer;
        }

        throw new InvalidOperationException($"Project {projectName} has no architecture layer classification. Add it to Lfe.Architecture.Tests before referencing it.");
    }

    public static bool IsHostNeutral(string projectName)
    {
        var layer = LayerFor(projectName);
        return layer is ProjectLayer.Contracts or ProjectLayer.PureCapability or ProjectLayer.Runtime or ProjectLayer.Composition;
    }
}

internal static class RepositoryPaths
{
    public static string LfeDirectory => FindLfeDirectory();

    public static string SourceDirectory => Path.Combine(LfeDirectory, "src");

    public static string ProjectNameFromPath(string path)
    {
        var relative = Path.GetRelativePath(SourceDirectory, path);
        return relative.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)[0];
    }

    private static string FindLfeDirectory()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null)
        {
            var candidate = Path.Combine(current.FullName, "lfe");
            if (Directory.Exists(Path.Combine(candidate, "src")))
            {
                return candidate;
            }

            if (Directory.Exists(Path.Combine(current.FullName, "src")) && File.Exists(Path.Combine(current.FullName, "Lfe.sln")))
            {
                return current.FullName;
            }

            current = current.Parent;
        }

        throw new DirectoryNotFoundException($"Could not locate lfe/src from {AppContext.BaseDirectory}.");
    }
}
