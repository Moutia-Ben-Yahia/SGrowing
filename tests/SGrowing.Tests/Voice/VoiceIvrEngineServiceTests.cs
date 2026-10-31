using FluentAssertions;
using SGrowing.Api.Modules.Voice.IVR;
using Xunit;

namespace SGrowing.Tests.Voice;

public class VoiceIvrEngineServiceTests
{
    private readonly VoiceIvrEngineService _service = new();
    private readonly Guid _tenantId = Guid.NewGuid();

    private IvrFlow CreateSampleFlow(Guid? offHoursFallbackId = null)
    {
        var startNodeId = Guid.NewGuid();
        var salesNodeId = Guid.NewGuid();
        var supportNodeId = Guid.NewGuid();
        var hangupNodeId = Guid.NewGuid();
        var offHoursNodeId = offHoursFallbackId ?? Guid.NewGuid();

        var nodes = new List<IvrNode>
        {
            new(
                startNodeId,
                "MainMenu",
                IvrNodeType.CollectDtmfDigits,
                "Press 1 for Sales, Press 2 for Support.",
                null,
                new List<string> { "1", "2" },
                MaxDigits: 1,
                TimeoutSeconds: 5,
                MaxRetries: 2,
                Transitions: new Dictionary<string, Guid>
                {
                    { "1", salesNodeId },
                    { "2", supportNodeId }
                },
                DefaultNextNodeId: hangupNodeId
            ),
            new(
                salesNodeId,
                "SalesQueue",
                IvrNodeType.RouteToQueue,
                "Transferring you to Sales team.",
                null,
                null,
                TargetQueue: "SalesQueue"
            ),
            new(
                supportNodeId,
                "SupportQueue",
                IvrNodeType.RouteToQueue,
                "Connecting to Support specialist.",
                null,
                null,
                TargetQueue: "Tier1Support"
            ),
            new(
                hangupNodeId,
                "HangupNode",
                IvrNodeType.Hangup,
                "Thank you for calling. Goodbye.",
                null,
                null
            ),
            new(
                offHoursNodeId,
                "OffHoursVoicemail",
                IvrNodeType.VoicemailDrop,
                "We are currently closed. Please leave a message after the tone.",
                null,
                null
            )
        };

        return new IvrFlow(
            Guid.NewGuid(),
            _tenantId,
            "Main Enterprise Inbound Flow",
            "Customer routing tree",
            startNodeId,
            nodes,
            new IvrOperatingSchedule(9, 17, new List<DayOfWeek> { DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday, DayOfWeek.Friday }),
            offHoursNodeId,
            true,
            DateTime.UtcNow
        );
    }

    [Fact]
    public async Task SaveFlowAsync_ShouldValidateAndPersistFlow()
    {
        var flow = CreateSampleFlow();
        var saved = await _service.SaveFlowAsync(flow);

        saved.Should().NotBeNull();
        saved.FlowId.Should().Be(flow.FlowId);
    }

    [Fact]
    public async Task SaveFlowAsync_ShouldThrow_WhenStartNodeDoesNotExist()
    {
        var invalidFlow = new IvrFlow(
            Guid.NewGuid(),
            _tenantId,
            "Bad Flow",
            "Missing start node",
            Guid.NewGuid(), // Non-existent
            new List<IvrNode>
            {
                new(Guid.NewGuid(), "Node1", IvrNodeType.Hangup, null, null, null)
            },
            new IvrOperatingSchedule(),
            null,
            true,
            DateTime.UtcNow
        );

        var act = () => _service.SaveFlowAsync(invalidFlow);
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*StartNodeId*was not found*");
    }

    [Fact]
    public async Task InitiateSessionAsync_ShouldStartAtMainMenu_WhenDuringBusinessHours()
    {
        var flow = CreateSampleFlow();
        await _service.SaveFlowAsync(flow);

        // Monday at 11:00 AM UTC
        var mondayWorkHours = new DateTime(2026, 11, 2, 11, 0, 0, DateTimeKind.Utc);
        var session = await _service.InitiateSessionAsync(_tenantId, flow.FlowId, "+15551234567", mondayWorkHours);

        session.Status.Should().Be(IvrSessionStatus.AwaitingInput);
        session.CurrentNodeId.Should().Be(flow.StartNodeId);
        session.CurrentNodeName.Should().Be("MainMenu");
    }

    [Fact]
    public async Task InitiateSessionAsync_ShouldRouteToOffHours_WhenOutsideBusinessHours()
    {
        var flow = CreateSampleFlow();
        await _service.SaveFlowAsync(flow);

        // Sunday at 2:00 AM UTC
        var sundayNight = new DateTime(2026, 11, 1, 2, 0, 0, DateTimeKind.Utc);
        var session = await _service.InitiateSessionAsync(_tenantId, flow.FlowId, "+15551234567", sundayNight);

        session.Status.Should().Be(IvrSessionStatus.VoicemailRecording);
        session.CurrentNodeName.Should().Be("OffHoursVoicemail");
    }

    [Fact]
    public async Task ProcessInputAsync_ShouldTransitionToSalesQueue_WhenCallerPresses1()
    {
        var flow = CreateSampleFlow();
        await _service.SaveFlowAsync(flow);

        var monday = new DateTime(2026, 11, 2, 11, 0, 0, DateTimeKind.Utc);
        var session = await _service.InitiateSessionAsync(_tenantId, flow.FlowId, "+15551234567", monday);

        var inputReq = new SubmitSessionInputRequest(session.SessionId, DtmfInput: "1");
        var updated = await _service.ProcessInputAsync(inputReq);

        updated.Status.Should().Be(IvrSessionStatus.RoutingToAgent);
        updated.CurrentNodeName.Should().Be("SalesQueue");
        updated.RoutedQueue.Should().Be("SalesQueue");
        updated.AuditTrail.Should().HaveCount(2);
    }

    [Fact]
    public async Task ProcessInputAsync_ShouldRetryAndEventuallyDisconnect_WhenInvalidInputRepeated()
    {
        var flow = CreateSampleFlow();
        await _service.SaveFlowAsync(flow);

        var monday = new DateTime(2026, 11, 2, 11, 0, 0, DateTimeKind.Utc);
        var session = await _service.InitiateSessionAsync(_tenantId, flow.FlowId, "+15551234567", monday);

        // Attempt 1: Invalid digit "9"
        var update1 = await _service.ProcessInputAsync(new SubmitSessionInputRequest(session.SessionId, DtmfInput: "9"));
        update1.CurrentRetries.Should().Be(1);
        update1.Status.Should().Be(IvrSessionStatus.AwaitingInput);

        // Attempt 2: Invalid digit "9" again (reaches MaxRetries: 2, routes to DefaultNextNodeId -> HangupNode)
        var update2 = await _service.ProcessInputAsync(new SubmitSessionInputRequest(session.SessionId, DtmfInput: "9"));
        update2.Status.Should().Be(IvrSessionStatus.Completed);
        update2.CurrentNodeName.Should().Be("HangupNode");
    }
}
