namespace Notification.Domain.Tests;

public sealed class DomainAssemblyTests
{
    [Fact]
    public void DomainAssemblyDoesNotReferenceInfrastructureFrameworks()
    {
        var references = typeof(Notification.Domain.DomainAssemblyMarker).Assembly
            .GetReferencedAssemblies()
            .Select(reference => reference.Name)
            .Where(name => name is not null)
            .ToArray();

        Assert.DoesNotContain("Microsoft.AspNetCore", references);
        Assert.DoesNotContain("Microsoft.EntityFrameworkCore", references);
        Assert.DoesNotContain("StackExchange.Redis", references);
    }
}
