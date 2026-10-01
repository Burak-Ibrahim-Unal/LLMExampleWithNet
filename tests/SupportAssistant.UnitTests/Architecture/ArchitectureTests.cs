using System.Reflection;
using NetArchTest.Rules;
using Shouldly;

namespace SupportAssistant.UnitTests.Architecture;

/// <summary>Layer rules from agent.md, enforced on the compiled assemblies.</summary>
public sealed class ArchitectureTests
{
    private static readonly Assembly SharedKernel = typeof(Shared.Kernel.Abstractions.EntityBase).Assembly;
    private static readonly Assembly SharedApplication = typeof(Shared.Application.Common.ApiResult<>).Assembly;
    private static readonly Assembly KnowledgeDomain = typeof(Knowledge.Domain.Entities.KnowledgeDocument).Assembly;
    private static readonly Assembly KnowledgeApplication = Knowledge.Application.AssemblyReference.Assembly;
    private static readonly Assembly Api = typeof(Program).Assembly;

    private static void ShouldPass(NetArchTest.Rules.TestResult result)
    {
        result.IsSuccessful.ShouldBeTrue($"Violating types: {string.Join(", ", result.FailingTypeNames ?? [])}");
    }

    [Fact]
    public void Shared_kernel_has_no_framework_or_outer_layer_dependencies()
    {
        ShouldPass(Types.InAssembly(SharedKernel).ShouldNot()
            .HaveDependencyOnAny("Microsoft.EntityFrameworkCore", "Microsoft.Extensions", "Shared.Application", "Shared.Infrastructure")
            .GetResult());
    }

    [Fact]
    public void Shared_application_does_not_depend_on_infrastructure()
    {
        ShouldPass(Types.InAssembly(SharedApplication).ShouldNot()
            .HaveDependencyOnAny("Shared.Infrastructure", "Microsoft.EntityFrameworkCore")
            .GetResult());
    }

    [Fact]
    public void Knowledge_domain_depends_only_on_the_kernel()
    {
        ShouldPass(Types.InAssembly(KnowledgeDomain).ShouldNot()
            .HaveDependencyOnAny("Knowledge.Application", "Knowledge.Infrastructure", "Microsoft.EntityFrameworkCore", "MediatR")
            .GetResult());
    }

    [Fact]
    public void Knowledge_application_does_not_know_the_database_or_the_llm_sdks()
    {
        ShouldPass(Types.InAssembly(KnowledgeApplication).ShouldNot()
            .HaveDependencyOnAny("Knowledge.Infrastructure", "Shared.Infrastructure", "Microsoft.EntityFrameworkCore", "OpenAI", "Microsoft.Extensions.AI")
            .GetResult());
    }

    [Fact]
    public void Api_endpoints_go_through_the_service_layer()
    {
        ShouldPass(Types.InAssembly(Api).That().ResideInNamespace("SupportAssistant.API.Endpoints").ShouldNot()
            .HaveDependencyOnAny("Shared.Infrastructure", "Knowledge.Infrastructure", "Knowledge.Domain.Repositories", "Microsoft.EntityFrameworkCore")
            .GetResult());
    }
}
