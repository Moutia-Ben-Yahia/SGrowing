using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.Json;
using FluentAssertions;
using SGrowing.Api.Common;
using SGrowing.Api.Modules.AI;
using SGrowing.Api.Modules.AI.Governance;
using SGrowing.Api.Modules.AI.Missions;
using SGrowing.Core.Entities;
using Xunit;

namespace SGrowing.Tests.Architecture;

public class ModuleBoundaryArchitectureTests
{
    [Fact]
    public void EndpointModules_ImplementIEndpointModule_AndHavePublicParameterlessConstructor()
    {
        var apiAssembly = typeof(IEndpointModule).Assembly;
        var moduleTypes = apiAssembly.GetTypes()
            .Where(t => typeof(IEndpointModule).IsAssignableFrom(t) && t is { IsClass: true, IsAbstract: false })
            .ToList();

        moduleTypes.Should().NotBeEmpty("SGrowing.Api should declare concrete IEndpointModule implementations for auto-discovery");

        foreach (var type in moduleTypes)
        {
            var parameterlessCtor = type.GetConstructor(Type.EmptyTypes);
            parameterlessCtor.Should().NotBeNull(
                $"Endpoint module '{type.FullName}' implements IEndpointModule and must have a public parameterless constructor for auto-discovery via MapEndpointModules");
            parameterlessCtor!.IsPublic.Should().BeTrue(
                $"Constructor for '{type.FullName}' must be public");

            // Instantiation check (MapEndpointModules activates concrete modules via Activator.CreateInstance)
            var instance = Activator.CreateInstance(type);
            instance.Should().NotBeNull();
            instance.Should().BeAssignableTo<IEndpointModule>();
        }
    }

    [Fact]
    public void EndpointClasses_ResideInDesignatedModulesNamespace()
    {
        var apiAssembly = typeof(IEndpointModule).Assembly;
        var endpointTypes = apiAssembly.GetTypes()
            .Where(t => t.IsClass && !t.IsAbstract && (
                typeof(IEndpointModule).IsAssignableFrom(t) ||
                t.Name.EndsWith("Endpoints", StringComparison.Ordinal) ||
                t.Name.EndsWith("EndpointModule", StringComparison.Ordinal)))
            .ToList();

        endpointTypes.Should().NotBeEmpty();

        foreach (var type in endpointTypes)
        {
            type.Namespace.Should().NotBeNull();
            type.Namespace.Should().StartWith(
                "SGrowing.Api.Modules",
                $"Endpoint class '{type.FullName}' must reside in the designated 'SGrowing.Api.Modules.*' namespace for vertical slice modular isolation");
        }
    }

    [Fact]
    public void CoreEntityClasses_FollowEncapsulationPrinciples_NoPublicMutableStaticFields()
    {
        var coreAssembly = typeof(BaseEntity).Assembly;
        var entityTypes = coreAssembly.GetTypes()
            .Where(t => t.IsClass && !t.IsAbstract &&
                        !t.IsDefined(typeof(CompilerGeneratedAttribute), false) &&
                        !t.Name.Contains('<') &&
                        t.Namespace != null && t.Namespace.StartsWith("SGrowing.Core.Entities", StringComparison.Ordinal))
            .ToList();

        entityTypes.Should().NotBeEmpty();

        foreach (var type in entityTypes)
        {
            // 1. No public mutable static fields
            var mutableStaticFields = type.GetFields(BindingFlags.Public | BindingFlags.Static)
                .Where(f => !f.IsInitOnly && !f.IsLiteral)
                .ToList();

            mutableStaticFields.Should().BeEmpty(
                $"Entity '{type.FullName}' must not declare public mutable static fields to preserve domain encapsulation");

            // 2. Valid constructors (must have at least one constructor)
            var ctors = type.GetConstructors(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            ctors.Should().NotBeEmpty(
                $"Entity '{type.FullName}' must declare or inherit at least one valid instance constructor");
        }
    }

    [Fact]
    public void PublicContractDtoRecords_AreImmutableRecords()
    {
        var apiAssembly = typeof(IEndpointModule).Assembly;
        var coreAssembly = typeof(BaseEntity).Assembly;

        var dtoRecordTypes = apiAssembly.GetTypes().Concat(coreAssembly.GetTypes())
            .Where(t => t.IsClass && !t.IsAbstract && t.Namespace != null &&
                        (t.Namespace.StartsWith("SGrowing.Api.Modules", StringComparison.Ordinal) ||
                         t.Namespace.StartsWith("SGrowing.Core", StringComparison.Ordinal)) &&
                        (t.Name.EndsWith("Dto", StringComparison.Ordinal) ||
                         t.Name.EndsWith("Request", StringComparison.Ordinal) ||
                         t.Name.EndsWith("Response", StringComparison.Ordinal)) &&
                        t.GetMethod("<Clone>$") != null) // Compiler-generated clone method for records
            .ToList();

        dtoRecordTypes.Should().NotBeEmpty();

        foreach (var recordType in dtoRecordTypes)
        {
            // Immutability: public properties should have init-only or get-only accessors
            var publicProps = recordType.GetProperties(BindingFlags.Public | BindingFlags.Instance);
            foreach (var prop in publicProps)
            {
                if (prop.CanWrite && prop.SetMethod != null && prop.SetMethod.IsPublic)
                {
                    var isInitOnly = prop.SetMethod.ReturnParameter
                        .GetRequiredCustomModifiers()
                        .Contains(typeof(IsExternalInit));

                    isInitOnly.Should().BeTrue(
                        $"Public property '{prop.Name}' on record DTO '{recordType.FullName}' must have an init-only setter to ensure immutability");
                }
            }
        }
    }

    [Fact]
    public void PublicContractDtoRecords_SerializeAndDeserializeCorrectly()
    {
        // 1. MissionObjectiveRequest
        var missionReq = new MissionObjectiveRequest(
            Title: "MENA Q4 Growth Sprint",
            StrategicGoal: "Acquire 500 B2B Enterprise Retailers in North Africa",
            BudgetLimit: 1500.50m,
            TargetGeo: "Tunisia, Algeria, Morocco",
            Constraints: ["Max CAC $12.00", "Spintax Compliance Required"]
        );

        var missionJson = JsonSerializer.Serialize(missionReq);
        var missionDeserialized = JsonSerializer.Deserialize<MissionObjectiveRequest>(missionJson);
        missionDeserialized.Should().NotBeNull();
        missionDeserialized.Should().BeEquivalentTo(missionReq);

        // 2. MissionApprovalDecisionRequest
        var approvalDecisionReq = new MissionApprovalDecisionRequest(
            Approved: true,
            ReviewerNotes: "Approved by Head of Growth after budget verification"
        );
        var approvalJson = JsonSerializer.Serialize(approvalDecisionReq);
        var approvalDeserialized = JsonSerializer.Deserialize<MissionApprovalDecisionRequest>(approvalJson);
        approvalDeserialized.Should().NotBeNull();
        approvalDeserialized.Should().BeEquivalentTo(approvalDecisionReq);

        // 3. ContactBriefDto
        var contactBrief = new ContactBriefDto(
            ContactId: Guid.NewGuid(),
            ContactName: "Tarak Ben Ammar",
            CompanyName: "Carthage Retail Group",
            KeyBulletPoints: ["Evaluating ERP sync", "Budget approved for Q4"],
            BuyingIntentLevel: "High",
            LastUpdatedUtc: DateTime.UtcNow
        );
        var contactJson = JsonSerializer.Serialize(contactBrief);
        var contactDeserialized = JsonSerializer.Deserialize<ContactBriefDto>(contactJson);
        contactDeserialized.Should().NotBeNull();
        contactDeserialized!.ContactId.Should().Be(contactBrief.ContactId);
        contactDeserialized.ContactName.Should().Be(contactBrief.ContactName);
        contactDeserialized.BuyingIntentLevel.Should().Be("High");

        // 4. ApprovalDecisionResult
        var decisionResult = new ApprovalDecisionResult(
            RequestId: Guid.NewGuid(),
            RequiresHumanApproval: true,
            RiskLevel: ApprovalRiskLevel.High,
            Status: ToolApprovalStatus.Pending,
            OneTimeApprovalToken: "a1b2c3d4e5f60718293a4b5c6d7e8f90",
            Reason: "Exceeds autonomous limit"
        );
        var decisionJson = JsonSerializer.Serialize(decisionResult);
        var decisionDeserialized = JsonSerializer.Deserialize<ApprovalDecisionResult>(decisionJson);
        decisionDeserialized.Should().NotBeNull();
        decisionDeserialized.Should().BeEquivalentTo(decisionResult);
    }
}
