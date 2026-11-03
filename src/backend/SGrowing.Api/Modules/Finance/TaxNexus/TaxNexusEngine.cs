using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;

namespace SGrowing.Api.Modules.Finance.TaxNexus;

public interface ITaxNexusEngine
{
    List<TaxJurisdictionNexus> GetJurisdictions();
    NexusRegistrationResult UpdateRegistration(UpdateNexusRegistrationRequest request);
}

public class TaxNexusEngine : ITaxNexusEngine
{
    private readonly ConcurrentDictionary<string, TaxJurisdictionNexus> _jurisdictions = new(StringComparer.OrdinalIgnoreCase);

    public TaxNexusEngine()
    {
        // Seed economic nexus jurisdictions
        var j1 = new TaxJurisdictionNexus("US-CA", "California, USA", 500000m, 200, 480000m, 185, 96.0, NexusState.ApproachingNexusLimit, 38400m);
        var j2 = new TaxJurisdictionNexus("US-NY", "New York, USA", 500000m, 100, 520000m, 115, 104.0, NexusState.NexusTriggeredRegistrationRequired, 44200m);
        var j3 = new TaxJurisdictionNexus("US-TX", "Texas, USA", 500000m, 200, 310000m, 95, 62.0, NexusState.SafeBelowThreshold, 0m);
        var j4 = new TaxJurisdictionNexus("EU-OSS", "European Union One-Stop-Shop (OSS)", 10000m, 50, 65000m, 320, 650.0, NexusState.RegisteredAndCollecting, 13650m);
        var j5 = new TaxJurisdictionNexus("UK-HMRC", "United Kingdom (HMRC VAT)", 85000m, 100, 78000m, 88, 91.8, NexusState.ApproachingNexusLimit, 15600m);

        _jurisdictions[j1.JurisdictionCode] = j1;
        _jurisdictions[j2.JurisdictionCode] = j2;
        _jurisdictions[j3.JurisdictionCode] = j3;
        _jurisdictions[j4.JurisdictionCode] = j4;
        _jurisdictions[j5.JurisdictionCode] = j5;
    }

    public List<TaxJurisdictionNexus> GetJurisdictions() => _jurisdictions.Values.OrderBy(j => j.JurisdictionCode).ToList();

    public NexusRegistrationResult UpdateRegistration(UpdateNexusRegistrationRequest request)
    {
        if (!_jurisdictions.TryGetValue(request.JurisdictionCode, out var existing))
        {
            throw new KeyNotFoundException($"Jurisdiction {request.JurisdictionCode} not found.");
        }

        var updated = existing with
        {
            State = request.NewState
        };

        _jurisdictions[request.JurisdictionCode] = updated;

        return new NexusRegistrationResult(
            request.JurisdictionCode,
            request.NewState,
            $"Jurisdiction {request.JurisdictionCode} status updated to '{request.NewState}'. Tax collection policy active."
        );
    }
}
