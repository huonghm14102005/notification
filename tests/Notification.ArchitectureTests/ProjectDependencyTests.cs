using System.Xml.Linq;

namespace Notification.ArchitectureTests;

public sealed class ProjectDependencyTests
{
    private static readonly string RepositoryRoot = FindRepositoryRoot();

    [Fact]
    public void DomainHasNoProjectReferences() =>
        Assert.Empty(ProjectReferences("src/Notification.Domain/Notification.Domain.csproj"));

    [Fact]
    public void ApplicationOnlyReferencesDomain() =>
        Assert.Equal(["Notification.Domain"], ProjectReferences("src/Notification.Application/Notification.Application.csproj"));

    [Theory]
    [InlineData("src/Notification.Api")]
    [InlineData("src/Notification.Worker")]
    public void EntryPointsDoNotContainPersistenceCode(string projectDirectory)
    {
        var files = Directory.GetFiles(Path.Combine(RepositoryRoot, projectDirectory), "*.cs", SearchOption.AllDirectories);
        foreach (var file in files)
        {
            var source = File.ReadAllText(file);
            Assert.DoesNotContain("DbContext", source, StringComparison.Ordinal);
            Assert.DoesNotContain("Npgsql", source, StringComparison.Ordinal);
        }
    }

    private static string[] ProjectReferences(string relativeProject)
    {
        var document = XDocument.Load(Path.Combine(RepositoryRoot, relativeProject));
        return document.Descendants("ProjectReference")
            .Select(reference => Path.GetFileNameWithoutExtension(reference.Attribute("Include")!.Value))
            .Order(StringComparer.Ordinal)
            .ToArray();
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Notification.slnx")))
        {
            directory = directory.Parent;
        }
        return directory?.FullName ?? throw new InvalidOperationException("Repository root not found.");
    }
}
