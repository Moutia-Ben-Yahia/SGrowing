using Microsoft.EntityFrameworkCore;
using SGrowing.Api.Data;
using SGrowing.Api.Modules.Commerce;
using SGrowing.Core.Entities;
using Xunit;

namespace SGrowing.Tests.Commerce;

public class CrossBorderTradeTests
{
    private SGrowingDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<SGrowingDbContext>()
            .UseInMemoryDatabase(databaseName: $"CrossBorderTradeDb_{Guid.NewGuid()}")
            .Options;
        return new SGrowingDbContext(options);
    }

    [Fact]
    public async Task CrossBorder_Trade_Fx_And_Customs_Lifecycle_Should_Succeed()
    {
        var db = CreateDbContext();
        var service = new CrossBorderTradeService(db);
        var tenantId = Guid.NewGuid();

        // 1. Configure Maghreb & GCC Countries
        await service.ConfigureCountryAsync(tenantId, CountryTradeConfig.Create(tenantId, "TN", "Tunisia", "TND", "+216", 8, 19.0m, 8.0m, true));
        await service.ConfigureCountryAsync(tenantId, CountryTradeConfig.Create(tenantId, "DZ", "Algeria", "DZD", "+213", 9, 19.0m, 600.0m, true));
        await service.ConfigureCountryAsync(tenantId, CountryTradeConfig.Create(tenantId, "SA", "Saudi Arabia", "SAR", "+966", 9, 15.0m, 25.0m, true));

        var countries = await service.ListSupportedCountriesAsync(tenantId);
        Assert.Equal(3, countries.Count);

        // 2. Set FX rates
        await service.SetFxRateAsync(tenantId, "TND", "USD", 0.32m);
        await service.SetFxRateAsync(tenantId, "EUR", "TND", 3.35m);

        // Direct conversion: 100 TND -> USD = 32 USD
        var usdAmount = await service.ConvertCurrencyAsync(tenantId, 100.0m, "TND", "USD");
        Assert.Equal(32.0m, usdAmount);

        // Inverse conversion: 335 TND -> EUR = 100 EUR
        var eurAmount = await service.ConvertCurrencyAsync(tenantId, 335.0m, "TND", "EUR");
        Assert.Equal(100.0m, eurAmount);

        // 3. Test Regional Phone Normalization (Handling local trunk zeros and formatting to E.164)
        var algeriaPhone = service.NormalizePhoneNumber("0550123456", "DZ");
        Assert.Equal("+213550123456", algeriaPhone);

        var saudiPhone = service.NormalizePhoneNumber("0501234567", "SA");
        Assert.Equal("+966501234567", saudiPhone);

        var tunisiaPhone = service.NormalizePhoneNumber("98123456", "TN");
        Assert.Equal("+21698123456", tunisiaPhone);

        // 4. Generate Export Customs Commercial Invoice
        var order = CodOrder.Create(
            tenantId,
            "ORD-EXP-001",
            "Karim Meziani",
            "0550123456",
            "Hydra, Alger",
            "Alger",
            "Hydra",
            250.0m);
        db.CodOrders.Add(order);
        await db.SaveChangesAsync();

        var invoiceItems = new List<CustomsInvoiceItem>
        {
            new CustomsInvoiceItem
            {
                HsTariffCode = "6109.10", // Cotton apparel
                Description = "Premium Cotton Polo",
                Quantity = 2,
                DeclaredUnitValue = 35.0m,
                DeclaredTotalValue = 70.0m,
                CountryOfOrigin = "TN"
            }
        };

        var customsInvoice = await service.GenerateCustomsCommercialInvoiceAsync(
            tenantId,
            order.Id,
            "DZ",
            invoiceItems,
            "EUR");

        Assert.NotNull(customsInvoice);
        Assert.StartsWith("EXP-", customsInvoice.InvoiceNumber);
        Assert.Equal("+213550123456", customsInvoice.ConsigneePhoneE164);
        Assert.Equal(70.0m, customsInvoice.TotalDeclaredCustomsValue);
        Assert.Equal("DAP", customsInvoice.Incoterm);
        Assert.Equal("TN", customsInvoice.ExporterCountry);
        Assert.Equal("DZ", customsInvoice.DestinationCountry);
    }
}
