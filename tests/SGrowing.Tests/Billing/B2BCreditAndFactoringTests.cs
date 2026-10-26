using Microsoft.EntityFrameworkCore;
using SGrowing.Api.Data;
using SGrowing.Api.Modules.Billing;
using Xunit;

namespace SGrowing.Tests.Billing;

public class B2BCreditAndFactoringTests
{
    private readonly SGrowingDbContext _db;
    private readonly B2BCreditAndFactoringService _service;

    public B2BCreditAndFactoringTests()
    {
        var options = new DbContextOptionsBuilder<SGrowingDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        _db = new SGrowingDbContext(options);
        _service = new B2BCreditAndFactoringService(_db);
    }

    [Fact]
    public async Task EstablishCreditAccount_DrawdownAndSettlement_CalculatesBalancesCorrectly()
    {
        var tenantId = Guid.NewGuid();
        var companyId = Guid.NewGuid();

        // 1. Establish Credit Line of 20,000 TND with Net-30
        var account = await _service.EstablishCreditAccountAsync(
            tenantId,
            companyId,
            "Sousse Pharma Distribution",
            creditLimit: 20000m,
            termsDays: 30,
            score: "AAA"
        );

        Assert.NotNull(account);
        Assert.Equal(20000m, account.CreditLimit);
        Assert.Equal(20000m, account.AvailableCredit);

        // 2. Invoice Drawdown for 6,500 TND
        var afterDrawdown = await _service.RecordInvoiceDrawdownAsync(tenantId, companyId, 6500m);
        Assert.Equal(6500m, afterDrawdown.OutstandingBalance);
        Assert.Equal(13500m, afterDrawdown.AvailableCredit);

        // 3. Payment Settlement of 4,000 TND
        var afterSettlement = await _service.RecordPaymentSettlementAsync(tenantId, companyId, 4000m);
        Assert.Equal(2500m, afterSettlement.OutstandingBalance);
        Assert.Equal(17500m, afterSettlement.AvailableCredit);

        // 4. Exceeding Credit limit should throw
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _service.RecordInvoiceDrawdownAsync(tenantId, companyId, 25000m));
    }

    [Fact]
    public async Task RequestCourierFactoring_AndFundAdvance_UnlocksImmediateLiquidity()
    {
        var tenantId = Guid.NewGuid();

        // 1. Merchant has 50,000 TND delivered float trapped with Yalidine
        var factoring = await _service.RequestCourierCashFactoringAsync(
            tenantId,
            "YALIDINE-TN",
            "Yalidine Express",
            inTransitFloat: 25000m,
            deliveredFloat: 50000m,
            discountRate: 2.5m
        );

        Assert.NotNull(factoring);
        Assert.Equal(48750m, factoring.ImmediateCashAvailable); // 50,000 - 2.5% (1,250)
        Assert.Equal("Draft", factoring.Status);

        // 2. Fund advance
        var funded = await _service.FundFactoringAdvanceAsync(tenantId, factoring.Id);
        Assert.Equal("Funded", funded.Status);
        Assert.NotNull(funded.FundedAtUtc);

        var summary = await _service.GetFinancialSummaryAsync(tenantId);
        Assert.Equal(1, summary.FactoringContractsActive);
        Assert.Equal(48750m, summary.TotalImmediateLiquidityUnlocked);
    }
}
