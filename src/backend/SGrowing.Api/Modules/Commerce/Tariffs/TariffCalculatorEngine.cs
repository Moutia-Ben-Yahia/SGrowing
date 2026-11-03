using System;
using System.Collections.Concurrent;
using System.Collections.Generic;

namespace SGrowing.Api.Modules.Commerce.Tariffs;

public interface ITariffCalculatorEngine
{
    HsCodeClassification ClassifyProduct(string productDescription);
    LandedCostBreakdown CalculateLandedCost(CalculateLandedCostRequest request);
    decimal GetCountryVatRate(string countryCode);
}

public class TariffCalculatorEngine : ITariffCalculatorEngine
{
    private readonly ConcurrentDictionary<string, decimal> _vatRates = new(StringComparer.OrdinalIgnoreCase)
    {
        ["DE"] = 19.0m, // Germany MwSt
        ["FR"] = 20.0m, // France TVA
        ["GB"] = 20.0m, // United Kingdom VAT
        ["US"] = 7.5m,  // United States average state sales tax
        ["JP"] = 10.0m, // Japan Consumption Tax
        ["AU"] = 10.0m  // Australia GST
    };

    private readonly ConcurrentDictionary<string, decimal> _dutyRates = new(StringComparer.OrdinalIgnoreCase)
    {
        ["8471.50"] = 0.0m,  // Processing units / servers (ITA zero duty)
        ["8517.62"] = 0.0m,  // Machines for transmission/reception of data (ITA zero duty)
        ["8542.31"] = 0.0m,  // Electronic integrated circuits
        ["8504.40"] = 3.2m,  // Static converters / power supplies
        ["9031.80"] = 2.5m   // Measuring / telemetry appliances
    };

    public HsCodeClassification ClassifyProduct(string productDescription)
    {
        var text = productDescription.ToLowerInvariant();

        if (text.Contains("server") || text.Contains("compute") || text.Contains("hardware appliance"))
        {
            return new HsCodeClassification("8471.50", "Processing units other than those of subheading 8471.41 or 8471.49", "Chapter 84 - Nuclear reactors, boilers, machinery", 96.5);
        }

        if (text.Contains("router") || text.Contains("telecom") || text.Contains("gateway") || text.Contains("switch"))
        {
            return new HsCodeClassification("8517.62", "Machines for the reception, conversion and transmission or regeneration of voice, images or other data", "Chapter 85 - Electrical machinery and equipment", 94.0);
        }

        if (text.Contains("power") || text.Contains("supply") || text.Contains("converter"))
        {
            return new HsCodeClassification("8504.40", "Static converters, power supply units for automatic data-processing machines", "Chapter 85 - Electrical machinery and equipment", 91.0);
        }

        // Default high-tech data telemetry appliance
        return new HsCodeClassification("9031.80", "Other optical instruments and appliances for inspecting semiconductor wafers", "Chapter 90 - Optical, photographic, measuring instruments", 88.0);
    }

    public LandedCostBreakdown CalculateLandedCost(CalculateLandedCostRequest request)
    {
        var hs = ClassifyProduct(request.ProductDescription);
        var country = request.DestinationCountry.ToUpperInvariant();

        var vatRate = GetCountryVatRate(country);
        _dutyRates.TryGetValue(hs.HsCode, out var dutyRate);

        decimal insurance = Math.Round(request.ProductPrice * 0.01m, 2); // 1% CIF insurance
        decimal cifValue = request.ProductPrice + request.ShippingCost + insurance;

        decimal dutyAmount = Math.Round(cifValue * (dutyRate / 100m), 2);
        decimal taxableBase = cifValue + dutyAmount;
        decimal vatAmount = Math.Round(taxableBase * (vatRate / 100m), 2);

        decimal totalLandedCost = Math.Round(cifValue + dutyAmount + vatAmount, 2);

        return new LandedCostBreakdown(
            request.ItemSku,
            hs.HsCode,
            country,
            request.ProductPrice,
            request.ShippingCost,
            insurance,
            dutyRate,
            dutyAmount,
            vatRate,
            vatAmount,
            totalLandedCost,
            Incoterm: "DDP"
        );
    }

    public decimal GetCountryVatRate(string countryCode)
    {
        _vatRates.TryGetValue(countryCode, out var rate);
        return rate > 0m ? rate : 15.0m; // Default global fallback
    }
}
