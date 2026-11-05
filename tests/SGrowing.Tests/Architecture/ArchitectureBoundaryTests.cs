using System.Reflection;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using SGrowing.Api.Common;
using SGrowing.Api.Data;
using SGrowing.Api.Modules.AI;
using SGrowing.Api.Modules.Auth;
using SGrowing.Api.Modules.Billing;
using SGrowing.Api.Modules.Channels;
using SGrowing.Api.Modules.Commerce;
using SGrowing.Api.Modules.Conversations;
using SGrowing.Api.Modules.CRM;
using SGrowing.Api.Modules.Dev;
using SGrowing.Api.Modules.Dev.Gateway;
using SGrowing.Api.Modules.Diagnostics;
using SGrowing.Api.Modules.Funnels;
using SGrowing.Api.Modules.Journeys;
using SGrowing.Api.Modules.Marketing;
using SGrowing.Api.Modules.Outbound;
using SGrowing.Api.Modules.SEO;
using SGrowing.Api.Modules.Voice;
using SGrowing.Core.Entities;
using Xunit;

namespace SGrowing.Tests.Architecture;

public class ArchitectureBoundaryTests
{
    [Fact]
    public void CoreDomainEntities_DoNotReference_ApiOrWorker()
    {
        var coreAssembly = typeof(BaseEntity).Assembly;

        // 1. Check referenced assemblies
        var referencedAssemblies = coreAssembly.GetReferencedAssemblies();
        referencedAssemblies.Should().NotContain(a =>
            string.Equals(a.Name, "SGrowing.Api", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(a.Name, "SGrowing.Worker", StringComparison.OrdinalIgnoreCase),
            "SGrowing.Core must not reference SGrowing.Api or SGrowing.Worker");

        // 2. Check all types in SGrowing.Core.Entities
        var entityTypes = coreAssembly.GetTypes()
            .Where(t => t.Namespace != null && t.Namespace.StartsWith("SGrowing.Core.Entities", StringComparison.Ordinal))
            .ToList();

        entityTypes.Should().NotBeEmpty();

        foreach (var type in entityTypes)
        {
            var members = type.GetMembers(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static);
            foreach (var m in members)
            {
                Type? memberType = m switch
                {
                    PropertyInfo p => p.PropertyType,
                    FieldInfo f => f.FieldType,
                    MethodInfo method => method.ReturnType,
                    _ => null
                };

                if (memberType != null)
                {
                    var typeToCheck = memberType.IsGenericType ? memberType.GetGenericArguments()[0] : memberType;
                    var ns = typeToCheck.Namespace;
                    if (ns != null)
                    {
                        ns.Should().NotStartWith("SGrowing.Api", $"Entity {type.Name} member {m.Name} references API layer");
                        ns.Should().NotStartWith("SGrowing.Worker", $"Entity {type.Name} member {m.Name} references Worker layer");
                    }
                }
            }
        }
    }

    [Fact]
    public void CoreEntitiesInheritingBaseEntity_HaveValidId()
    {
        var coreAssembly = typeof(BaseEntity).Assembly;
        var entityTypes = coreAssembly.GetTypes()
            .Where(t => typeof(BaseEntity).IsAssignableFrom(t) && t is { IsClass: true, IsAbstract: false })
            .ToList();

        entityTypes.Should().NotBeEmpty();

        foreach (var type in entityTypes)
        {
            var idProp = type.GetProperty("Id");
            idProp.Should().NotBeNull($"Entity {type.Name} must declare or inherit an Id property");
            idProp!.PropertyType.Should().Be(typeof(Guid));

            if (type.GetConstructor(Type.EmptyTypes) is not null)
            {
                var instance = (BaseEntity)Activator.CreateInstance(type)!;
                instance.Id.Should().NotBeEmpty($"Entity {type.Name} must initialize Id to a valid non-empty Guid");
            }
        }
    }

    [Fact]
    public void CoreEndpointClasses_AllImplement_IEndpointModule()
    {
        var endpointTypes = new[]
        {
            typeof(HealthEndpoints),
            typeof(AuthEndpoints),
            typeof(CrmEndpoints),
            typeof(ConversationsEndpoints),
            typeof(OutboundSdrEndpoints),
            typeof(JourneysFeedbackEndpoints),
            typeof(ChannelsMessagingEndpoints),
            typeof(CommerceEndpoints),
            typeof(MarketingEndpoints),
            typeof(VoiceEndpoints),
            typeof(AiGatewayEndpoints),
            typeof(FinanceEndpoints),
            typeof(DeveloperGatewayEndpoints),
            typeof(DevEndpoints),
            typeof(SeoEndpoints),
            typeof(FunnelEndpoints),
            typeof(DevGatewayEndpoints)
        };

        endpointTypes.Should().HaveCount(17);

        foreach (var type in endpointTypes)
        {
            typeof(IEndpointModule).IsAssignableFrom(type)
                .Should().BeTrue($"{type.Name} must implement IEndpointModule");
            type.IsClass.Should().BeTrue();
            type.IsAbstract.Should().BeFalse();
            type.GetConstructor(Type.EmptyTypes).Should().NotBeNull($"{type.Name} must have a public parameterless constructor");
        }
    }

    [Fact]
    public void SGrowingDbContext_CorePartitionedEntityTypes_AreMappedToExplicitSchemas()
    {
        var options = new DbContextOptionsBuilder<SGrowingDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        using var db = new SGrowingDbContext(options);

        Type[] corePartitionedTypes = [
            typeof(Tenant),
            typeof(Workspace),
            typeof(User),
            typeof(Contact),
            typeof(Company),
            typeof(Deal),
            typeof(Product),
            typeof(CodOrder),
            typeof(Conversation),
            typeof(Message),
            typeof(WorkflowGraph),
            typeof(WorkflowExecution),
            typeof(HelpCategory),
            typeof(HelpArticle),
            typeof(SlaPolicy),
            typeof(TenantSubscription),
            typeof(TelemetryEvent),
            typeof(AgentMission),
            typeof(OutboxMessageEntity)
        ];

        foreach (var type in corePartitionedTypes)
        {
            var entityType = db.Model.FindEntityType(type);
            entityType.Should().NotBeNull();
            var schema = entityType!.GetSchema();
            schema.Should().NotBeNullOrWhiteSpace($"Entity {type.Name} must be mapped to an explicit schema");
            schema.Should().NotBe("public");
        }
    }
}
