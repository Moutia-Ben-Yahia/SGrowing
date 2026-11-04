using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using SGrowing.Api.Data;
using SGrowing.Api.Modules.Voice;
using SGrowing.Core.Entities;
using Xunit;

namespace SGrowing.Tests.Voice;

public class VoiceCallAmdServiceTests
{
    [Fact]
    public async Task ClassifyAudioStreamAmdAsync_DurationUnderOrEqual2400_ClassifiesAsHumanPickup()
    {
        // Arrange
        var options = new DbContextOptionsBuilder<SGrowingDbContext>()
            .UseInMemoryDatabase(databaseName: $"AmdDb_{Guid.NewGuid()}")
            .Options;

        var tenantId = Guid.NewGuid();
        using var db = new SGrowingDbContext(options);
        var service = new VoiceCallAmdService(db);

        // Act (duration 1800ms)
        var result = await service.ClassifyAudioStreamAmdAsync(
            tenantId,
            "+12025550198",
            audioDurationMs: 1800
        );

        // Assert
        result.Should().NotBeNull();
        result.DestinationPhone.Should().Be("+12025550198");
        result.Classification.Should().Be("HumanPickup");
        result.Confidence.Should().Be(0.94m);
        result.DurationMs.Should().Be(1800);
        result.VoicemailDropped.Should().BeFalse();
        result.AudioDropFileUrl.Should().BeEmpty();
        result.ActionTaken.Should().Contain("Human voice detected");

        var recordInDb = await db.VoiceCallAmdRecords.FindAsync(result.RecordId);
        recordInDb.Should().NotBeNull();
        recordInDb!.TenantId.Should().Be(tenantId);
        recordInDb.DetectedClassification.Should().Be("HumanPickup");
    }

    [Fact]
    public async Task ClassifyAudioStreamAmdAsync_DurationAbove2400_ClassifiesAsAnsweringMachineBeep_WithDefaultVoicemail()
    {
        // Arrange
        var options = new DbContextOptionsBuilder<SGrowingDbContext>()
            .UseInMemoryDatabase(databaseName: $"AmdDb_{Guid.NewGuid()}")
            .Options;

        var tenantId = Guid.NewGuid();
        using var db = new SGrowingDbContext(options);
        var service = new VoiceCallAmdService(db);

        // Act (duration 3200ms with no custom template)
        var result = await service.ClassifyAudioStreamAmdAsync(
            tenantId,
            "+12025550144",
            audioDurationMs: 3200
        );

        // Assert
        result.Should().NotBeNull();
        result.Classification.Should().Be("AnsweringMachineBeep");
        result.VoicemailDropped.Should().BeTrue();
        result.AudioDropFileUrl.Should().Be("https://storage.sgrowing.io/audio/voicemail-quick-offer.wav");
        result.ActionTaken.Should().Contain("Acoustic beep detected");
        result.ActionTaken.Should().Contain("call line released");
    }

    [Fact]
    public async Task ClassifyAudioStreamAmdAsync_AnsweringMachine_UsesTenantVoicemailDropTemplate()
    {
        // Arrange
        var options = new DbContextOptionsBuilder<SGrowingDbContext>()
            .UseInMemoryDatabase(databaseName: $"AmdDb_{Guid.NewGuid()}")
            .Options;

        var tenantId = Guid.NewGuid();
        using var db = new SGrowingDbContext(options);
        var service = new VoiceCallAmdService(db);

        // Create tenant voicemail template
        await service.ConfigureVoicemailTemplateAsync(
            tenantId,
            "VIP Account Drop",
            "https://cdn.sgrowing.io/custom-voicemail-vip.mp3",
            "Hey, missed you on the phone today!",
            useTts: false
        );

        // Act
        var result = await service.ClassifyAudioStreamAmdAsync(
            tenantId,
            "+12025550999",
            audioDurationMs: 3500
        );

        // Assert
        result.Should().NotBeNull();
        result.Classification.Should().Be("AnsweringMachineBeep");
        result.VoicemailDropped.Should().BeTrue();
        result.AudioDropFileUrl.Should().Be("https://cdn.sgrowing.io/custom-voicemail-vip.mp3");
    }

    [Fact]
    public async Task ClassifyAudioStreamAmdAsync_ForcedClassification_OverridesAcousticDuration()
    {
        // Arrange
        var options = new DbContextOptionsBuilder<SGrowingDbContext>()
            .UseInMemoryDatabase(databaseName: $"AmdDb_{Guid.NewGuid()}")
            .Options;

        var tenantId = Guid.NewGuid();
        using var db = new SGrowingDbContext(options);
        var service = new VoiceCallAmdService(db);

        // Act: duration is 4000ms (normally answering machine), but forced to HumanPickup
        var result = await service.ClassifyAudioStreamAmdAsync(
            tenantId,
            "+12025550777",
            audioDurationMs: 4000,
            forcedClassification: "HumanPickup"
        );

        // Assert
        result.Classification.Should().Be("HumanPickup");
        result.VoicemailDropped.Should().BeFalse();
        result.ActionTaken.Should().Contain("Human voice detected");
    }

    [Fact]
    public async Task ConfigureVoicemailTemplateAsync_PersistsTemplateCorrectly()
    {
        // Arrange
        var options = new DbContextOptionsBuilder<SGrowingDbContext>()
            .UseInMemoryDatabase(databaseName: $"AmdDb_{Guid.NewGuid()}")
            .Options;

        var tenantId = Guid.NewGuid();
        using var db = new SGrowingDbContext(options);
        var service = new VoiceCallAmdService(db);

        // Act
        var template = await service.ConfigureVoicemailTemplateAsync(
            tenantId,
            "Black Friday Followup",
            "https://cdn.sgrowing.io/bf-voicemail.mp3",
            "Special Black Friday offer waiting for you.",
            useTts: true
        );

        // Assert
        template.Should().NotBeNull();
        template.TenantId.Should().Be(tenantId);
        template.TemplateName.Should().Be("Black Friday Followup");
        template.AudioFileUrl.Should().Be("https://cdn.sgrowing.io/bf-voicemail.mp3");
        template.DynamicTextPrompt.Should().Be("Special Black Friday offer waiting for you.");
        template.UseTtsSynthesis.Should().BeTrue();

        var inDb = await db.VoicemailDropTemplates.FindAsync(template.Id);
        inDb.Should().NotBeNull();
    }

    [Fact]
    public async Task GetAmdRecordsAsync_ReturnsRecordsForTenant_OrderedAndLimited()
    {
        // Arrange
        var options = new DbContextOptionsBuilder<SGrowingDbContext>()
            .UseInMemoryDatabase(databaseName: $"AmdDb_{Guid.NewGuid()}")
            .Options;

        var tenantId = Guid.NewGuid();
        var otherTenantId = Guid.NewGuid();

        using var db = new SGrowingDbContext(options);
        var service = new VoiceCallAmdService(db);

        // Seed 3 records for tenant and 1 for other tenant
        await service.ClassifyAudioStreamAmdAsync(tenantId, "+1001", 1000);
        await service.ClassifyAudioStreamAmdAsync(tenantId, "+1002", 3000);
        await service.ClassifyAudioStreamAmdAsync(tenantId, "+1003", 1500);
        await service.ClassifyAudioStreamAmdAsync(otherTenantId, "+9999", 2000);

        // Act
        var records = await service.GetAmdRecordsAsync(tenantId, limit: 2);

        // Assert
        records.Should().HaveCount(2);
        records.Should().OnlyContain(r => r.TenantId == tenantId);
    }

    [Fact]
    public async Task ClassifyAudioStreamAmdAsync_ForcedAnsweringMachine_InjectsVoicemailEvenIfShortDuration()
    {
        var options = new DbContextOptionsBuilder<SGrowingDbContext>()
            .UseInMemoryDatabase(databaseName: $"AmdDb_{Guid.NewGuid()}")
            .Options;

        var tenantId = Guid.NewGuid();
        using var db = new SGrowingDbContext(options);
        var service = new VoiceCallAmdService(db);

        // 1000ms duration (normally HumanPickup), forced to AnsweringMachineBeep
        var result = await service.ClassifyAudioStreamAmdAsync(
            tenantId,
            "+12025550222",
            audioDurationMs: 1000,
            forcedClassification: "AnsweringMachineBeep"
        );

        result.Should().NotBeNull();
        result.Classification.Should().Be("AnsweringMachineBeep");
        result.VoicemailDropped.Should().BeTrue();
        result.AudioDropFileUrl.Should().NotBeEmpty();
    }

    [Fact]
    public async Task ClassifyAudioStreamAmdAsync_WithCallSessionId_PersistsCallSessionIdInRecord()
    {
        var options = new DbContextOptionsBuilder<SGrowingDbContext>()
            .UseInMemoryDatabase(databaseName: $"AmdDb_{Guid.NewGuid()}")
            .Options;

        var tenantId = Guid.NewGuid();
        var callSessionId = Guid.NewGuid();
        using var db = new SGrowingDbContext(options);
        var service = new VoiceCallAmdService(db);

        var result = await service.ClassifyAudioStreamAmdAsync(
            tenantId,
            "+12025550333",
            audioDurationMs: 1500,
            callSessionId: callSessionId
        );

        var inDb = await db.VoiceCallAmdRecords.FindAsync(result.RecordId);
        inDb.Should().NotBeNull();
        inDb!.CallSessionId.Should().Be(callSessionId);
    }
}
