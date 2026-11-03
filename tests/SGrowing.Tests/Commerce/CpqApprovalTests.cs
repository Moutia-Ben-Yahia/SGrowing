using System;
using System.Collections.Generic;
using FluentAssertions;
using SGrowing.Api.Modules.Commerce.CpqApproval;
using Xunit;

namespace SGrowing.Tests.Commerce;

public class CpqApprovalTests
{
    private readonly CpqApprovalEngine _engine = new();

    [Fact]
    public void GetOverview_ReturnsSeededQuotesAndMetrics()
    {
        var overview = _engine.GetOverview();

        overview.Should().NotBeNull();
        overview.PendingApprovalsCount.Should().BeGreaterThanOrEqualTo(3);
        overview.TotalPendingValue.Should().BeGreaterThan(0);
        overview.Quotes.Should().NotBeEmpty();
    }

    [Fact]
    public void SubmitQuote_AutoApprovesLowDiscount()
    {
        var request = new QuoteApprovalRequest(
            QuoteId: "QUOTE-TEST-001",
            AccountName: "CloudScale Inc",
            TotalListPrice: 50000m,
            DiscountPercentage: 8.5m,
            PaymentTerms: "Net 30",
            RequesterId: "rep-001",
            RequesterName: "Alice Rep",
            Items: new() { new QuoteItem("SKU-CORE", "Growth OS Core", 1, 50000m, 50000m) },
            Justification: "Standard rep discount"
        );

        var result = _engine.SubmitQuote(request);

        result.Should().NotBeNull();
        result.RequiredTier.Should().Be(ApprovalTier.RepAutonomous);
        result.Status.Should().Be(QuoteApprovalStatus.Approved);
        result.AuditToken.Should().StartWith("CPQ-AUDIT-");
    }

    [Fact]
    public void SubmitQuote_RoutesHighDiscountToBoardGate()
    {
        var request = new QuoteApprovalRequest(
            QuoteId: "QUOTE-TEST-002",
            AccountName: "Mega Corp Global",
            TotalListPrice: 500000m,
            DiscountPercentage: 40.0m,
            PaymentTerms: "Net 90",
            RequesterId: "rep-002",
            RequesterName: "Bob Rep",
            Items: new() { new QuoteItem("SKU-ENT", "Growth OS Enterprise", 1, 500000m, 500000m) },
            Justification: "Board gate requirement"
        );

        var result = _engine.SubmitQuote(request);

        result.Should().NotBeNull();
        result.RequiredTier.Should().Be(ApprovalTier.CroBoardGate);
        result.Status.Should().Be(QuoteApprovalStatus.PendingApproval);
        result.AuditToken.Should().BeNull();
    }

    [Fact]
    public void ApproveQuote_GeneratesAuditTokenAndUpdatesStatus()
    {
        var command = new ApproveQuoteCommand(
            QuoteId: "QUOTE-2026-881",
            ApproverId: "mgr-101",
            ApproverName: "Victoria Vance (VP Sales)",
            Comments: "Approved given 3-year upfront commitment"
        );

        var result = _engine.ApproveQuote(command);

        result.Should().NotBeNull();
        result.Status.Should().Be(QuoteApprovalStatus.Approved);
        result.ApprovedBy.Should().Be("Victoria Vance (VP Sales)");
        result.AuditToken.Should().StartWith("CPQ-AUDIT-");
    }

    [Fact]
    public void EscalateQuote_AdvancesTierAndResetsSla()
    {
        var result = _engine.EscalateQuote("QUOTE-2026-881", "SLA expiring, customer decision meeting today");

        result.Should().NotBeNull();
        result.Status.Should().Be(QuoteApprovalStatus.Escalated);
        result.RequiredTier.Should().Be(ApprovalTier.SalesDirector);
        result.SlaMinutesRemaining.Should().Be(180);
    }
}
