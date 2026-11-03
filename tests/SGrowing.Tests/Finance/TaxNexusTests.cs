using System;
using System.Collections.Generic;
using FluentAssertions;
using SGrowing.Api.Modules.Finance.TaxNexus;
using Xunit;

namespace SGrowing.Tests.Finance;

public class TaxNexusTests
{
    private readonly TaxNexusEngine _engine = new();

    [Fact]
    public void GetJurisdictions_ReturnsSeededJurisdictions()
    {
        var jurisdictions = _engine.GetJurisdictions();

        jurisdictions.Should().NotBeNull();
        jurisdictions.Should().HaveCount(5);
        jurisdictions.Should().Contain(j => j.JurisdictionCode == "US-CA" && j.State == NexusState.ApproachingNexusLimit);
        jurisdictions.Should().Contain(j => j.JurisdictionCode == "US-NY" && j.State == NexusState.NexusTriggeredRegistrationRequired);
    }

    [Fact]
    public void UpdateRegistration_UpdatesStateAndReturnsConfirmation()
    {
        var jurisdiction = "US-NY";
        var request = new UpdateNexusRegistrationRequest(jurisdiction, NexusState.RegisteredAndCollecting);

        var result = _engine.UpdateRegistration(request);

        result.Should().NotBeNull();
        result.JurisdictionCode.Should().Be(jurisdiction);
        result.State.Should().Be(NexusState.RegisteredAndCollecting);

        var updated = _engine.GetJurisdictions().Find(j => j.JurisdictionCode == jurisdiction)!;
        updated.State.Should().Be(NexusState.RegisteredAndCollecting);
    }

    [Fact]
    public void UpdateRegistration_ThrowsWhenJurisdictionNotFound()
    {
        var action = () => _engine.UpdateRegistration(new UpdateNexusRegistrationRequest("INVALID-JUR", NexusState.RegisteredAndCollecting));

        action.Should().Throw<KeyNotFoundException>()
            .WithMessage("*not found*");
    }
}
