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

    [Fact]
    public void RejectQuote_SetsStatusToRejectedAndRecordsApproverReason()
    {
        var command = new RejectQuoteCommand(
            QuoteId: "QUOTE-2026-882",
            ApproverId: "dir-202",
            ApproverName: "Sarah Director",
            Reason: "Discount too deep without executive sponsorship"
        );

        var result = _engine.RejectQuote(command);

        result.Should().NotBeNull();
        result.Status.Should().Be(QuoteApprovalStatus.Rejected);
        result.ApprovedBy.Should().Contain("Sarah Director");
        result.ApprovedBy.Should().Contain("Discount too deep without executive sponsorship");
        result.SlaMinutesRemaining.Should().Be(0);
    }

    [Fact]
    public void SubmitQuote_MidLevelDiscount_RoutesToSalesManagerTier()
    {
        var request = new QuoteApprovalRequest(
            QuoteId: "QUOTE-MID-15",
            AccountName: "MidMarket Co",
            TotalListPrice: 60000m,
            DiscountPercentage: 15.0m,
            PaymentTerms: "Net 30",
            RequesterId: "rep-005",
            RequesterName: "Daniel Rep",
            Items: new() { new QuoteItem("SKU-PRO", "Growth OS Pro", 1, 60000m, 60000m) },
            Justification: "Standard mid-market competitive concession"
        );

        var result = _engine.SubmitQuote(request);

        result.Should().NotBeNull();
        result.RequiredTier.Should().Be(ApprovalTier.SalesManager);
        result.Status.Should().Be(QuoteApprovalStatus.PendingApproval);
        result.SlaMinutesRemaining.Should().Be(240);
        result.AuditToken.Should().BeNull();
    }

    [Fact]
    public void ApproveQuote_NonExistentQuote_ThrowsKeyNotFoundException()
    {
        var command = new ApproveQuoteCommand(
            QuoteId: "QUOTE-DOES-NOT-EXIST",
            ApproverId: "mgr-001",
            ApproverName: "Manager",
            Comments: "Approve"
        );

        var act = () => _engine.ApproveQuote(command);

        act.Should().Throw<KeyNotFoundException>()
            .WithMessage("*QUOTE-DOES-NOT-EXIST*");
    }
}
