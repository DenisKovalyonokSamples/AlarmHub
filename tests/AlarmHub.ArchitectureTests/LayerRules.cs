using System.Reflection;
using AlarmHub.Api.Domain.Customers;
using AlarmHub.Processing.Domain;
using NetArchTest.Rules;

namespace AlarmHub.ArchitectureTests;

/// <summary>
/// Architecture rules that keep the service boundaries honest.
/// </summary>
/// <remarks>
/// These fail the build when somebody takes a shortcut (for example referencing EF Core from the
/// contracts assembly). They are cheap to run and catch the most expensive mistakes early.
/// </remarks>
public sealed class LayerRules
{
    private static readonly Assembly ContractsAssembly = typeof(AlarmHub.Contracts.DeviceEvents.DeviceEventReceivedV1).Assembly;
    private static readonly Assembly ApiAssembly = typeof(Customer).Assembly;
    private static readonly Assembly ProcessingAssembly = typeof(Incident).Assembly;

    /// <summary>Contracts are shared by every service, so they must stay free of infrastructure.</summary>
    [Fact]
    public void Contracts_DoNotDependOnInfrastructure()
    {
        TestResult result = Types.InAssembly(ContractsAssembly)
            .ShouldNot()
            .HaveDependencyOnAny("Microsoft.EntityFrameworkCore", "Microsoft.AspNetCore", "Azure.Messaging")
            .GetResult();

        Assert.True(result.IsSuccessful, Describe(result));
    }

    /// <summary>Domain types must not reach into persistence or HTTP.</summary>
    [Fact]
    public void ApiDomain_DoesNotDependOnInfrastructure()
    {
        TestResult result = Types.InAssembly(ApiAssembly)
            .That().ResideInNamespaceStartingWith("AlarmHub.Api.Domain")
            .ShouldNot()
            .HaveDependencyOnAny(
                "Microsoft.EntityFrameworkCore",
                "Microsoft.AspNetCore",
                "AlarmHub.Api.Infrastructure",
                "AlarmHub.Api.Endpoints")
            .GetResult();

        Assert.True(result.IsSuccessful, Describe(result));
    }

    /// <summary>Processing rules must stay testable without a broker or a database.</summary>
    [Fact]
    public void ProcessingDomain_DoesNotDependOnInfrastructure()
    {
        TestResult result = Types.InAssembly(ProcessingAssembly)
            .That().ResideInNamespaceStartingWith("AlarmHub.Processing.Domain")
            .ShouldNot()
            .HaveDependencyOnAny(
                "Microsoft.EntityFrameworkCore",
                "Azure.Messaging",
                "AlarmHub.Processing.Infrastructure",
                "AlarmHub.Processing.Messaging")
            .GetResult();

        Assert.True(result.IsSuccessful, Describe(result));
    }

    /// <summary>Handlers in the API live in endpoints, not in the domain.</summary>
    [Fact]
    public void Endpoints_AreSealed()
    {
        TestResult result = Types.InAssembly(ApiAssembly)
            .That().ResideInNamespaceStartingWith("AlarmHub.Api.Endpoints")
            .And().AreClasses()
            .Should().BeSealed()
            .GetResult();

        Assert.True(result.IsSuccessful, Describe(result));
    }

    private static string Describe(TestResult result) =>
        result.IsSuccessful
            ? "ok"
            : "violating types: " + string.Join(", ", result.FailingTypeNames ?? []);
}
