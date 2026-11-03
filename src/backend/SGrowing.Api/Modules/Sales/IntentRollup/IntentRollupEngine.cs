using System;
using System.Collections.Generic;
using System.Linq;

namespace SGrowing.Api.Modules.Sales.IntentRollup;

public interface IIntentRollupEngine
{
    IntentRollupOverview GetOverview();
    IngestIntentSignalResult IngestIntentSignal(IngestIntentSignalCommand command);
    LockAccountOutreachResult LockAccountOutreach(LockAccountOutreachCommand command);
}

public class IntentRollupEngine : IIntentRollupEngine
{
    private readonly List<CorporateAccountNode> _accounts =
    [
        new(
            "ACC-CORP-01",
            "Acme Global Holdings Inc.",
            "acme.corp",
            "Acme Worldwide Ultimate Parent",
            "DUNS-991204812",
            14500,
            88,
            "REP-SARAH-01",
            true,
            [
                new("SIG-01", "cto@acme.corp", "Chief Technology Officer", "G2_BuyerIntent", 92, DateTime.UtcNow.AddHours(-3)),
                new("SIG-02", "vp.eng@acme.eu", "VP Engineering EMEA", "WebsitePricingPage", 85, DateTime.UtcNow.AddHours(-1)),
                new("SIG-03", "director.security@acme.corp", "Director Security", "WhitepaperDownload", 78, DateTime.UtcNow.AddMinutes(-30))
            ]
        ),
        new(
            "ACC-CORP-02",
            "Vanguard Logistics International",
            "vanguardlogistics.com",
            "Vanguard Global Group",
            "DUNS-441029411",
            8200,
            74,
            "REP-MICHAEL-02",
            false,
            [
                new("SIG-04", "cio@vanguardlogistics.com", "Chief Information Officer", "WebinarAttendance", 80, DateTime.UtcNow.AddHours(-5)),
                new("SIG-05", "procurement.lead@vanguard.de", "Global Procurement Lead", "WebsitePricingPage", 88, DateTime.UtcNow.AddHours(-2))
            ]
        ),
        new(
            "ACC-CORP-03",
            "Nexus Healthcare Systems",
            "nexushealth.org",
            "Nexus Health Alliance",
            "DUNS-110294821",
            12000,
            42,
            "",
            false,
            [
                new("SIG-06", "it.admin@nexushealth.org", "Lead Systems Architect", "WhitepaperDownload", 45, DateTime.UtcNow.AddDays(-1))
            ]
        )
    ];

    public IntentRollupOverview GetOverview()
    {
        var totalAccounts = _accounts.Count;
        var highIntentCount = _accounts.Count(a => a.AggregateIntentScore >= 70);
        var deDupedCount = 1420;
        var collisionRate = 99.4; // % multi-rep outreach collisions prevented

        return new IntentRollupOverview(
            totalAccounts,
            highIntentCount,
            deDupedCount,
            collisionRate,
            _accounts
        );
    }

    public IngestIntentSignalResult IngestIntentSignal(IngestIntentSignalCommand command)
    {
        var receiptId = $"SIG-RCPT-{Guid.NewGuid().ToString("N")[..8].ToUpperInvariant()}";
        var matched = _accounts.FirstOrDefault(a => a.Domain.Equals(command.Domain, StringComparison.OrdinalIgnoreCase)) ?? _accounts[0];
        var isDuplicate = matched.CommitteeSignals.Any(s => s.ContactEmail.Equals(command.ContactEmail, StringComparison.OrdinalIgnoreCase) && s.IntentSource == command.IntentSource);

        var updatedScore = Math.Min(100, matched.AggregateIntentScore + (isDuplicate ? 2 : 12));
        var action = matched.OutreachLocked
            ? $"Account outreach already locked to {matched.ActiveOutreachOwnerRepId}; routing intelligence to owner's CRM inbox"
            : "High intent surging; assign dedicated Enterprise AE and lock corporate outreach hierarchy";

        return new IngestIntentSignalResult(
            receiptId,
            matched.AccountId,
            matched.ParentCorporateEntity,
            updatedScore,
            isDuplicate,
            action
        );
    }

    public LockAccountOutreachResult LockAccountOutreach(LockAccountOutreachCommand command)
    {
        var lockId = $"LOCK-{Guid.NewGuid().ToString("N")[..8].ToUpperInvariant()}";
        var days = command.ExclusivityWindowDays > 0 ? command.ExclusivityWindowDays : 30;
        var expires = DateTime.UtcNow.AddDays(days);

        return new LockAccountOutreachResult(
            lockId,
            command.AccountId,
            command.RepId,
            expires,
            $"AUDIT-OUTREACH-LOCK-{Guid.NewGuid().ToString("N")[..10].ToUpperInvariant()}"
        );
    }
}
