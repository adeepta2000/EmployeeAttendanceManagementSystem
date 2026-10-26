using ArchUnitNET.Domain;
using ArchUnitNET.Fluent;
using ArchUnitNET.Loader;
using ArchUnitNET.xUnitV3;
using FluentAssertions;
using static ArchUnitNET.Fluent.ArchRuleDefinition;
using Assembly = System.Reflection.Assembly;

namespace Attendance.Domain.Tests;

/// <summary>
/// Enforces the dependency rule. Never weaken these rules to get a build green.
/// </summary>
public sealed class ArchitectureTests
{
    private static readonly Assembly DomainAssembly = Assembly.Load("Attendance.Domain");
    private static readonly Assembly ApplicationAssembly = Assembly.Load("Attendance.Application");
    private static readonly Assembly InfrastructureAssembly = Assembly.Load("Attendance.Infrastructure");
    private static readonly Assembly ApiAssembly = Assembly.Load("Attendance.Api");
    private static readonly Assembly SharedAssembly = Assembly.Load("Attendance.Shared");

    private static readonly Architecture Architecture = new ArchLoader()
        .LoadAssemblies(DomainAssembly, ApplicationAssembly, InfrastructureAssembly, ApiAssembly, SharedAssembly)
        .Build();

    private static readonly IObjectProvider<IType> DomainLayer = TypesIn("Domain", DomainAssembly);
    private static readonly IObjectProvider<IType> ApplicationLayer = TypesIn("Application", ApplicationAssembly);
    private static readonly IObjectProvider<IType> InfrastructureLayer = TypesIn("Infrastructure", InfrastructureAssembly);
    private static readonly IObjectProvider<IType> ApiLayer = TypesIn("Api", ApiAssembly);

    [Fact]
    public void Domain_depends_on_no_other_project() =>
        Types().That().Are(DomainLayer)
            .Should().NotDependOnAny(TypesIn(
                "Application, Infrastructure, Api or Shared",
                ApplicationAssembly, InfrastructureAssembly, ApiAssembly, SharedAssembly))
            .Check(Architecture);

    [Fact]
    public void Domain_references_no_packages()
    {
        var references = DomainAssembly.GetReferencedAssemblies().Select(a => a.Name);

        references.Should().OnlyContain(
            name => name == "netstandard" || name!.StartsWith("System", StringComparison.Ordinal),
            "Domain may use only the base class library: no EF Core, no Shared, no third-party packages");
    }

    // Shared ("cross-cutting contracts, result types") is allowed here: Application can reference it.
    [Fact]
    public void Application_depends_only_on_Domain_and_Shared() =>
        Types().That().Are(ApplicationLayer)
            .Should().NotDependOnAny(TypesIn("Infrastructure or Api", InfrastructureAssembly, ApiAssembly))
            .Check(Architecture);

    [Fact]
    public void Infrastructure_does_not_depend_on_Api() =>
        Types().That().Are(InfrastructureLayer)
            .Should().NotDependOnAny(ApiLayer)
            .Check(Architecture);

    // Program.cs is the composition root. The "Program" prefix also covers its compiler-generated closures.
    [Fact]
    public void Api_uses_Infrastructure_only_in_Program() =>
        Types().That().Are(ApiLayer).And().DoNotHaveFullNameStartingWith("Program")
            .Should().NotDependOnAny(InfrastructureLayer)
            .Check(Architecture);

    private static IObjectProvider<IType> TypesIn(string description, Assembly assembly, params Assembly[] moreAssemblies) =>
        Types().That().ResideInAssembly(assembly, moreAssemblies).As(description);
}
