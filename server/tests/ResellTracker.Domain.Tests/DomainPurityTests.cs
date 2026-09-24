using ResellTracker.Domain;

namespace ResellTracker.Domain.Tests;

// The profit math and status rules must stay a pure library: no EF Core, no
// ASP.NET Core. If one of those ever sneaks in as a reference, this fails.
public class DomainPurityTests
{
    [Fact]
    public void Domain_references_only_the_base_class_library()
    {
        var references = typeof(DomainAssembly).Assembly
            .GetReferencedAssemblies()
            .Select(a => a.Name ?? "")
            .ToList();

        Assert.DoesNotContain(references, name =>
            name.StartsWith("Microsoft.EntityFrameworkCore", StringComparison.Ordinal) ||
            name.StartsWith("Microsoft.AspNetCore", StringComparison.Ordinal));
    }
}
