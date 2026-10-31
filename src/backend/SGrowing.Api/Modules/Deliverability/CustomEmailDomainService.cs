using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace SGrowing.Api.Modules.Deliverability;

public enum CustomDomainStatus
{
    PendingDns,
    Verifying,
    Active,
    DnsDegraded,
    FailedVerification
}

public enum DnsRecordType
{
    TXT,
    CNAME,
    MX
}

public enum DnsRecordStatus
{
    Matched,
    Missing,
    Mismatch,
    PropagationPending
}

public enum DmarcPolicy
{
    None,
    Quarantine,
    Reject
}

public enum RegistrarProvider
{
    Cloudflare,
    AwsRoute53,
    GoDaddy,
    Namecheap
}

public record DkimKeyPair(
    string Selector,
    int KeyLengthBits,
    string PublicKeyBase64,
    string PrivateKeyPemOrBase64,
    string KeyFingerprint,
    string TxtRecordValue
);

public record CustomDnsRecord(
    Guid Id,
    DnsRecordType RecordType,
    string Hostname,
    string ExpectedValue,
    string? CurrentValue,
    DnsRecordStatus Status,
    int Ttl = 300,
    int? Priority = null,
    string Purpose = "",
    DateTime? LastCheckedUtc = null,
    string? ErrorDetails = null
);

public record CustomEmailDomain(
    Guid Id,
    Guid TenantId,
    string Domain,
    string ReturnPathPrefix,
    string ReturnPathCnameTarget,
    DmarcPolicy DmarcPolicy,
    string DmarcReportEmail,
    CustomDomainStatus Status,
    int HealthScore,
    DkimKeyPair Dkim,
    List<CustomDnsRecord> Records,
    DateTime CreatedAtUtc,
    DateTime? LastVerifiedAtUtc,
    string? VerificationError = null
);

public record RegisterCustomDomainDto(
    string Domain,
    Guid? TenantId = null,
    string? ReturnPathPrefix = "mail",
    string? DmarcPolicy = "quarantine",
    string? CustomDmarcReportEmail = null
);

public record CustomDomainSummaryDto(
    Guid Id,
    Guid TenantId,
    string Domain,
    string ReturnPathPrefix,
    CustomDomainStatus Status,
    int HealthScore,
    int TotalRecords,
    int MatchedRecords,
    string DkimSelector,
    string DkimFingerprint,
    DateTime CreatedAtUtc,
    DateTime? LastVerifiedAtUtc
);

public record CustomDomainDetailDto(
    Guid Id,
    Guid TenantId,
    string Domain,
    string ReturnPathPrefix,
    string ReturnPathCnameTarget,
    DmarcPolicy DmarcPolicy,
    string DmarcReportEmail,
    CustomDomainStatus Status,
    int HealthScore,
    DkimKeyPair Dkim,
    IReadOnlyList<CustomDnsRecord> Records,
    DateTime CreatedAtUtc,
    DateTime? LastVerifiedAtUtc,
    string? VerificationError
);

public record DomainVerificationResult(
    Guid DomainId,
    string Domain,
    CustomDomainStatus PreviousStatus,
    CustomDomainStatus NewStatus,
    int HealthScore,
    int RecordsChecked,
    int RecordsMatched,
    bool AllMatched,
    long VerificationDurationMs,
    IReadOnlyList<CustomDnsRecord> UpdatedRecords,
    string Summary
);

public record RegistrarExportRecord(
    string Type,
    string Hostname,
    string Value,
    int Ttl,
    int? Priority = null
);

public record RegistrarExportDto(
    string Registrar,
    string Domain,
    string Format,
    string RawOutput,
    string Instructions,
    IReadOnlyList<RegistrarExportRecord> Records
);

public record DeliverabilityDomainMetricsDto(
    int TotalDomains,
    int ActiveDomains,
    int PendingDnsDomains,
    int DegradedDomains,
    int VerifyingDomains,
    double AverageHealthScore,
    int TotalDkimKeysGenerated,
    double FullyAlignedRatePercent
);

public interface ICustomEmailDomainService
{
    Task<CustomEmailDomain> RegisterDomainAsync(Guid tenantId, RegisterCustomDomainDto dto);
    Task<CustomDomainDetailDto?> GetDomainAsync(Guid domainId);
    Task<IReadOnlyList<CustomDomainSummaryDto>> ListDomainsAsync(Guid? tenantId = null);
    Task<DomainVerificationResult> VerifyDomainDnsAsync(Guid domainId, bool? simulateAllPass = null);
    Task<bool> DeleteDomainAsync(Guid domainId);
    RegistrarExportDto ExportRegistrarConfig(Guid domainId, RegistrarProvider registrar);
    RegistrarExportDto ExportRegistrarConfig(Guid domainId, string registrarName);
    Task<DeliverabilityDomainMetricsDto> GetOverviewMetricsAsync(Guid? tenantId = null);
    DkimKeyPair GenerateDkimKeyPair(string domain, string selector = "sgrowing");
}

public class CustomEmailDomainService : ICustomEmailDomainService
{
    public const string DefaultReturnPathTarget = "mail.sgrowing.net";
    public const string DefaultSpfValue = "v=spf1 include:_spf.sgrowing.net ~all";
    public const string DefaultMxTarget = "feedback-smtp.sgrowing.net";
    public const int DefaultMxPriority = 10;
    public const string DefaultDkimSelector = "sgrowing";

    private static readonly ConcurrentDictionary<Guid, CustomEmailDomain> _domainStore = new();
    private static readonly object _initLock = new();
    private static bool _isSeeded = false;

    public CustomEmailDomainService()
    {
        EnsureSeeded();
    }

    public static void ResetStore()
    {
        lock (_initLock)
        {
            _domainStore.Clear();
            _isSeeded = false;
        }
    }

    private void EnsureSeeded()
    {
        if (_isSeeded) return;

        lock (_initLock)
        {
            if (_isSeeded) return;

            // 1. Active: mail.acme-corp.com (100% verified)
            var tenant1 = Guid.Parse("11111111-1111-1111-1111-111111111111");
            var domain1Id = Guid.Parse("a1111111-1111-1111-1111-111111111111");
            var dkim1 = GenerateDkimKeyPair("mail.acme-corp.com", DefaultDkimSelector);
            var records1 = CreateRecords("mail.acme-corp.com", "mail", DefaultReturnPathTarget, dkim1, DmarcPolicy.Quarantine, "dmarc@mail.acme-corp.com");
            // Mark all matched
            for (int i = 0; i < records1.Count; i++)
            {
                records1[i] = records1[i] with
                {
                    Status = DnsRecordStatus.Matched,
                    CurrentValue = records1[i].ExpectedValue,
                    LastCheckedUtc = DateTime.UtcNow.AddMinutes(-15)
                };
            }
            _domainStore[domain1Id] = new CustomEmailDomain(
                Id: domain1Id,
                TenantId: tenant1,
                Domain: "mail.acme-corp.com",
                ReturnPathPrefix: "mail",
                ReturnPathCnameTarget: DefaultReturnPathTarget,
                DmarcPolicy: DmarcPolicy.Quarantine,
                DmarcReportEmail: "dmarc@mail.acme-corp.com",
                Status: CustomDomainStatus.Active,
                HealthScore: 100,
                Dkim: dkim1,
                Records: records1,
                CreatedAtUtc: DateTime.UtcNow.AddDays(-12),
                LastVerifiedAtUtc: DateTime.UtcNow.AddMinutes(-15)
            );

            // 2. PendingDns: billing.fintech.tn (Freshly provisioned)
            var tenant2 = Guid.Parse("22222222-2222-2222-2222-222222222222");
            var domain2Id = Guid.Parse("b2222222-2222-2222-2222-222222222222");
            var dkim2 = GenerateDkimKeyPair("billing.fintech.tn", DefaultDkimSelector);
            var records2 = CreateRecords("billing.fintech.tn", "mail", DefaultReturnPathTarget, dkim2, DmarcPolicy.Quarantine, "dmarc@billing.fintech.tn");
            // Pending DNS
            for (int i = 0; i < records2.Count; i++)
            {
                records2[i] = records2[i] with
                {
                    Status = DnsRecordStatus.PropagationPending,
                    CurrentValue = null,
                    LastCheckedUtc = DateTime.UtcNow.AddHours(-1)
                };
            }
            _domainStore[domain2Id] = new CustomEmailDomain(
                Id: domain2Id,
                TenantId: tenant2,
                Domain: "billing.fintech.tn",
                ReturnPathPrefix: "mail",
                ReturnPathCnameTarget: DefaultReturnPathTarget,
                DmarcPolicy: DmarcPolicy.Quarantine,
                DmarcReportEmail: "dmarc@billing.fintech.tn",
                Status: CustomDomainStatus.PendingDns,
                HealthScore: 0,
                Dkim: dkim2,
                Records: records2,
                CreatedAtUtc: DateTime.UtcNow.AddHours(-2),
                LastVerifiedAtUtc: null
            );

            // 3. DnsDegraded: updates.growthpulse.io (SPF mismatch, DMARC missing)
            var tenant3 = Guid.Parse("33333333-3333-3333-3333-333333333333");
            var domain3Id = Guid.Parse("c3333333-3333-3333-3333-333333333333");
            var dkim3 = GenerateDkimKeyPair("updates.growthpulse.io", DefaultDkimSelector);
            var records3 = CreateRecords("updates.growthpulse.io", "bounces", DefaultReturnPathTarget, dkim3, DmarcPolicy.Reject, "dmarc@updates.growthpulse.io");
            // CNAME matched, DKIM matched, SPF mismatch, DMARC missing, MX matched
            for (int i = 0; i < records3.Count; i++)
            {
                if (records3[i].Purpose == "DKIM Public Key" || records3[i].Purpose == "Return-Path CNAME" || records3[i].Purpose == "Inbound Bounce MX")
                {
                    records3[i] = records3[i] with
                    {
                        Status = DnsRecordStatus.Matched,
                        CurrentValue = records3[i].ExpectedValue,
                        LastCheckedUtc = DateTime.UtcNow.AddMinutes(-45)
                    };
                }
                else if (records3[i].Purpose == "SPF Validation")
                {
                    records3[i] = records3[i] with
                    {
                        Status = DnsRecordStatus.Mismatch,
                        CurrentValue = "v=spf1 include:_spf.google.com ~all",
                        ErrorDetails = "SPF record missing 'include:_spf.sgrowing.net' alignment mechanism",
                        LastCheckedUtc = DateTime.UtcNow.AddMinutes(-45)
                    };
                }
                else if (records3[i].Purpose == "DMARC Policy")
                {
                    records3[i] = records3[i] with
                    {
                        Status = DnsRecordStatus.Missing,
                        CurrentValue = null,
                        ErrorDetails = "NXDOMAIN: No TXT record published at _dmarc.updates.growthpulse.io",
                        LastCheckedUtc = DateTime.UtcNow.AddMinutes(-45)
                    };
                }
            }
            _domainStore[domain3Id] = new CustomEmailDomain(
                Id: domain3Id,
                TenantId: tenant3,
                Domain: "updates.growthpulse.io",
                ReturnPathPrefix: "bounces",
                ReturnPathCnameTarget: DefaultReturnPathTarget,
                DmarcPolicy: DmarcPolicy.Reject,
                DmarcReportEmail: "dmarc@updates.growthpulse.io",
                Status: CustomDomainStatus.DnsDegraded,
                HealthScore: 60,
                Dkim: dkim3,
                Records: records3,
                CreatedAtUtc: DateTime.UtcNow.AddDays(-5),
                LastVerifiedAtUtc: DateTime.UtcNow.AddMinutes(-45),
                VerificationError: "SPF include alignment missing and DMARC record not published."
            );

            // 4. Verifying: outreach.saasaccelerate.com (Verification sweep active)
            var tenant4 = Guid.Parse("44444444-4444-4444-4444-444444444444");
            var domain4Id = Guid.Parse("d4444444-4444-4444-4444-444444444444");
            var dkim4 = GenerateDkimKeyPair("outreach.saasaccelerate.com", DefaultDkimSelector);
            var records4 = CreateRecords("outreach.saasaccelerate.com", "mail", DefaultReturnPathTarget, dkim4, DmarcPolicy.Quarantine, "dmarc@outreach.saasaccelerate.com");
            records4[0] = records4[0] with { Status = DnsRecordStatus.Matched, CurrentValue = records4[0].ExpectedValue, LastCheckedUtc = DateTime.UtcNow };
            records4[1] = records4[1] with { Status = DnsRecordStatus.Matched, CurrentValue = records4[1].ExpectedValue, LastCheckedUtc = DateTime.UtcNow };
            records4[2] = records4[2] with { Status = DnsRecordStatus.PropagationPending, CurrentValue = null, LastCheckedUtc = DateTime.UtcNow };
            records4[3] = records4[3] with { Status = DnsRecordStatus.PropagationPending, CurrentValue = null, LastCheckedUtc = DateTime.UtcNow };
            records4[4] = records4[4] with { Status = DnsRecordStatus.PropagationPending, CurrentValue = null, LastCheckedUtc = DateTime.UtcNow };

            _domainStore[domain4Id] = new CustomEmailDomain(
                Id: domain4Id,
                TenantId: tenant4,
                Domain: "outreach.saasaccelerate.com",
                ReturnPathPrefix: "mail",
                ReturnPathCnameTarget: DefaultReturnPathTarget,
                DmarcPolicy: DmarcPolicy.Quarantine,
                DmarcReportEmail: "dmarc@outreach.saasaccelerate.com",
                Status: CustomDomainStatus.Verifying,
                HealthScore: 40,
                Dkim: dkim4,
                Records: records4,
                CreatedAtUtc: DateTime.UtcNow.AddMinutes(-30),
                LastVerifiedAtUtc: DateTime.UtcNow.AddMinutes(-5)
            );

            _isSeeded = true;
        }
    }

    public DkimKeyPair GenerateDkimKeyPair(string domain, string selector = DefaultDkimSelector)
    {
        using var rsa = RSA.Create(2048);
        byte[] publicKeyDer = rsa.ExportSubjectPublicKeyInfo();
        string publicKeyBase64 = Convert.ToBase64String(publicKeyDer);

        byte[] privateKeyDer = rsa.ExportPkcs8PrivateKey();
        string privateKeyBase64 = Convert.ToBase64String(privateKeyDer);

        // SHA-256 fingerprint formatted as colon-separated hex bytes
        byte[] hash = SHA256.HashData(publicKeyDer);
        string fingerprint = string.Join(":", hash.Take(16).Select(b => b.ToString("X2")));

        string txtValue = $"v=DKIM1; k=rsa; p={publicKeyBase64}";

        return new DkimKeyPair(
            Selector: selector,
            KeyLengthBits: 2048,
            PublicKeyBase64: publicKeyBase64,
            PrivateKeyPemOrBase64: privateKeyBase64,
            KeyFingerprint: fingerprint,
            TxtRecordValue: txtValue
        );
    }

    private static List<CustomDnsRecord> CreateRecords(
        string domain,
        string returnPathPrefix,
        string returnPathTarget,
        DkimKeyPair dkim,
        DmarcPolicy dmarcPolicy,
        string dmarcReportEmail)
    {
        var records = new List<CustomDnsRecord>();

        // 1. Return-Path CNAME
        string cnameHostname = $"{returnPathPrefix}.{domain}";
        records.Add(new CustomDnsRecord(
            Id: Guid.NewGuid(),
            RecordType: DnsRecordType.CNAME,
            Hostname: cnameHostname,
            ExpectedValue: returnPathTarget,
            CurrentValue: null,
            Status: DnsRecordStatus.PropagationPending,
            Ttl: 300,
            Purpose: "Return-Path CNAME"
        ));

        // 2. DKIM Public Key TXT
        string dkimHostname = $"{dkim.Selector}._domainkey.{domain}";
        records.Add(new CustomDnsRecord(
            Id: Guid.NewGuid(),
            RecordType: DnsRecordType.TXT,
            Hostname: dkimHostname,
            ExpectedValue: dkim.TxtRecordValue,
            CurrentValue: null,
            Status: DnsRecordStatus.PropagationPending,
            Ttl: 300,
            Purpose: "DKIM Public Key"
        ));

        // 3. SPF Alignment TXT
        string spfHostname = domain;
        records.Add(new CustomDnsRecord(
            Id: Guid.NewGuid(),
            RecordType: DnsRecordType.TXT,
            Hostname: spfHostname,
            ExpectedValue: DefaultSpfValue,
            CurrentValue: null,
            Status: DnsRecordStatus.PropagationPending,
            Ttl: 300,
            Purpose: "SPF Validation"
        ));

        // 4. DMARC Policy TXT
        string dmarcHostname = $"_dmarc.{domain}";
        string policyStr = dmarcPolicy.ToString().ToLowerInvariant();
        string dmarcExpected = $"v=DMARC1; p={policyStr}; rua=mailto:{dmarcReportEmail}; pct=100";
        records.Add(new CustomDnsRecord(
            Id: Guid.NewGuid(),
            RecordType: DnsRecordType.TXT,
            Hostname: dmarcHostname,
            ExpectedValue: dmarcExpected,
            CurrentValue: null,
            Status: DnsRecordStatus.PropagationPending,
            Ttl: 300,
            Purpose: "DMARC Policy"
        ));

        // 5. Inbound Bounce MX
        string mxHostname = $"{returnPathPrefix}.{domain}";
        records.Add(new CustomDnsRecord(
            Id: Guid.NewGuid(),
            RecordType: DnsRecordType.MX,
            Hostname: mxHostname,
            ExpectedValue: DefaultMxTarget,
            CurrentValue: null,
            Status: DnsRecordStatus.PropagationPending,
            Ttl: 300,
            Priority: DefaultMxPriority,
            Purpose: "Inbound Bounce MX"
        ));

        return records;
    }

    public static string NormalizeDomain(string rawDomain)
    {
        if (string.IsNullOrWhiteSpace(rawDomain))
            throw new ArgumentException("Domain name cannot be null or whitespace.", nameof(rawDomain));

        string cleaned = rawDomain.Trim().ToLowerInvariant();

        // Strip URI protocols if entered
        if (cleaned.StartsWith("http://", StringComparison.OrdinalIgnoreCase))
            cleaned = cleaned[7..];
        else if (cleaned.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            cleaned = cleaned[8..];

        // Strip trailing slash or path components
        int slashIndex = cleaned.IndexOf('/');
        if (slashIndex >= 0)
            cleaned = cleaned[..slashIndex];

        cleaned = cleaned.TrimEnd('.');

        // Validate basic RFC domain structure
        var domainRegex = new Regex(@"^(?:[a-z0-9](?:[a-z0-9-]{0,61}[a-z0-9])?\.)+[a-z]{2,63}$", RegexOptions.IgnoreCase);
        if (!domainRegex.IsMatch(cleaned))
        {
            throw new ArgumentException($"Invalid domain format '{rawDomain}'. Must be a valid FQDN (e.g. mail.example.com).", nameof(rawDomain));
        }

        return cleaned;
    }

    public Task<CustomEmailDomain> RegisterDomainAsync(Guid tenantId, RegisterCustomDomainDto dto)
    {
        string normalizedDomain = NormalizeDomain(dto.Domain);

        // Check if domain already registered
        bool alreadyExists = _domainStore.Values.Any(d =>
            string.Equals(d.Domain, normalizedDomain, StringComparison.OrdinalIgnoreCase));

        if (alreadyExists)
        {
            throw new InvalidOperationException($"Domain '{normalizedDomain}' is already registered in the system.");
        }

        string returnPrefix = string.IsNullOrWhiteSpace(dto.ReturnPathPrefix) ? "mail" : dto.ReturnPathPrefix.Trim().ToLowerInvariant();

        DmarcPolicy parsedDmarcPolicy = DmarcPolicy.Quarantine;
        if (!string.IsNullOrWhiteSpace(dto.DmarcPolicy))
        {
            if (Enum.TryParse<DmarcPolicy>(dto.DmarcPolicy, true, out var pol))
                parsedDmarcPolicy = pol;
        }

        string reportEmail = string.IsNullOrWhiteSpace(dto.CustomDmarcReportEmail)
            ? $"dmarc@{normalizedDomain}"
            : dto.CustomDmarcReportEmail.Trim();

        var dkim = GenerateDkimKeyPair(normalizedDomain, DefaultDkimSelector);
        var records = CreateRecords(normalizedDomain, returnPrefix, DefaultReturnPathTarget, dkim, parsedDmarcPolicy, reportEmail);

        var domainId = Guid.NewGuid();
        var domain = new CustomEmailDomain(
            Id: domainId,
            TenantId: tenantId,
            Domain: normalizedDomain,
            ReturnPathPrefix: returnPrefix,
            ReturnPathCnameTarget: DefaultReturnPathTarget,
            DmarcPolicy: parsedDmarcPolicy,
            DmarcReportEmail: reportEmail,
            Status: CustomDomainStatus.PendingDns,
            HealthScore: 0,
            Dkim: dkim,
            Records: records,
            CreatedAtUtc: DateTime.UtcNow,
            LastVerifiedAtUtc: null
        );

        _domainStore[domainId] = domain;
        return Task.FromResult(domain);
    }

    public Task<CustomDomainDetailDto?> GetDomainAsync(Guid domainId)
    {
        if (_domainStore.TryGetValue(domainId, out var domain))
        {
            var detail = new CustomDomainDetailDto(
                Id: domain.Id,
                TenantId: domain.TenantId,
                Domain: domain.Domain,
                ReturnPathPrefix: domain.ReturnPathPrefix,
                ReturnPathCnameTarget: domain.ReturnPathCnameTarget,
                DmarcPolicy: domain.DmarcPolicy,
                DmarcReportEmail: domain.DmarcReportEmail,
                Status: domain.Status,
                HealthScore: domain.HealthScore,
                Dkim: domain.Dkim,
                Records: domain.Records.AsReadOnly(),
                CreatedAtUtc: domain.CreatedAtUtc,
                LastVerifiedAtUtc: domain.LastVerifiedAtUtc,
                VerificationError: domain.VerificationError
            );
            return Task.FromResult<CustomDomainDetailDto?>(detail);
        }

        return Task.FromResult<CustomDomainDetailDto?>(null);
    }

    public Task<IReadOnlyList<CustomDomainSummaryDto>> ListDomainsAsync(Guid? tenantId = null)
    {
        var query = _domainStore.Values.AsEnumerable();
        if (tenantId.HasValue && tenantId.Value != Guid.Empty)
        {
            query = query.Where(d => d.TenantId == tenantId.Value);
        }

        var summaries = query
            .OrderByDescending(d => d.CreatedAtUtc)
            .Select(d => new CustomDomainSummaryDto(
                Id: d.Id,
                TenantId: d.TenantId,
                Domain: d.Domain,
                ReturnPathPrefix: d.ReturnPathPrefix,
                Status: d.Status,
                HealthScore: d.HealthScore,
                TotalRecords: d.Records.Count,
                MatchedRecords: d.Records.Count(r => r.Status == DnsRecordStatus.Matched),
                DkimSelector: d.Dkim.Selector,
                DkimFingerprint: d.Dkim.KeyFingerprint,
                CreatedAtUtc: d.CreatedAtUtc,
                LastVerifiedAtUtc: d.LastVerifiedAtUtc
            ))
            .ToList();

        return Task.FromResult<IReadOnlyList<CustomDomainSummaryDto>>(summaries.AsReadOnly());
    }

    public Task<DomainVerificationResult> VerifyDomainDnsAsync(Guid domainId, bool? simulateAllPass = null)
    {
        if (!_domainStore.TryGetValue(domainId, out var domain))
        {
            throw new KeyNotFoundException($"Custom email domain with ID '{domainId}' was not found.");
        }

        var sw = System.Diagnostics.Stopwatch.StartNew();
        var prevStatus = domain.Status;

        // Perform DNS lookup verification simulation
        var updatedRecords = new List<CustomDnsRecord>();
        int matchedCount = 0;

        foreach (var rec in domain.Records)
        {
            bool match;
            if (simulateAllPass.HasValue)
            {
                match = simulateAllPass.Value;
            }
            else
            {
                // Default heuristic: If domain is already Active, maintain 100% match.
                // Otherwise simulate high success with slight propagation delay on MX/DMARC if fresh.
                if (domain.Status == CustomDomainStatus.Active)
                {
                    match = true;
                }
                else if (domain.Status == CustomDomainStatus.DnsDegraded)
                {
                    // keep degraded state unless simulateAllPass passed
                    match = rec.Purpose != "SPF Validation" && rec.Purpose != "DMARC Policy";
                }
                else
                {
                    match = true;
                }
            }

            if (match)
            {
                updatedRecords.Add(rec with
                {
                    Status = DnsRecordStatus.Matched,
                    CurrentValue = rec.ExpectedValue,
                    LastCheckedUtc = DateTime.UtcNow,
                    ErrorDetails = null
                });
                matchedCount++;
            }
            else
            {
                // Assign realistic failure/delay
                DnsRecordStatus failureStatus = rec.Purpose switch
                {
                    "SPF Validation" => DnsRecordStatus.Mismatch,
                    "DMARC Policy" => DnsRecordStatus.Missing,
                    _ => DnsRecordStatus.PropagationPending
                };

                string? error = failureStatus switch
                {
                    DnsRecordStatus.Mismatch => "SPF record mismatch: expected SGrowing include directive",
                    DnsRecordStatus.Missing => "NXDOMAIN: DNS record does not exist on authoritative nameservers",
                    _ => "DNS propagation pending or TTL unexpired on nameserver cache"
                };

                updatedRecords.Add(rec with
                {
                    Status = failureStatus,
                    CurrentValue = failureStatus == DnsRecordStatus.Mismatch ? "v=spf1 include:_spf.other.com ~all" : null,
                    LastCheckedUtc = DateTime.UtcNow,
                    ErrorDetails = error
                });
            }
        }

        sw.Stop();

        // Calculate health score & status
        int total = updatedRecords.Count;
        int healthScore = total > 0 ? (int)Math.Round((double)matchedCount / total * 100.0) : 0;

        CustomDomainStatus newStatus;
        string summary;
        string? verificationError = null;

        if (matchedCount == total)
        {
            newStatus = CustomDomainStatus.Active;
            summary = $"All {total} DNS records successfully verified and cryptographically aligned with SGrowing.";
        }
        else
        {
            bool dkimOk = updatedRecords.Any(r => r.Purpose == "DKIM Public Key" && r.Status == DnsRecordStatus.Matched);
            bool cnameOk = updatedRecords.Any(r => r.Purpose == "Return-Path CNAME" && r.Status == DnsRecordStatus.Matched);

            if (dkimOk && cnameOk)
            {
                newStatus = CustomDomainStatus.DnsDegraded;
                summary = $"Core DKIM and Return-Path records verified ({matchedCount}/{total}). Secondary SPF/DMARC records require attention.";
                verificationError = "SPF or DMARC record alignment incomplete.";
            }
            else
            {
                newStatus = CustomDomainStatus.PendingDns;
                summary = $"DNS records not yet propagated ({matchedCount}/{total} verified). Please review registrar configuration.";
                verificationError = "DKIM or Return-Path CNAME records not yet detected.";
            }
        }

        var updatedDomain = domain with
        {
            Status = newStatus,
            HealthScore = healthScore,
            Records = updatedRecords,
            LastVerifiedAtUtc = DateTime.UtcNow,
            VerificationError = verificationError
        };

        _domainStore[domainId] = updatedDomain;

        return Task.FromResult(new DomainVerificationResult(
            DomainId: domainId,
            Domain: domain.Domain,
            PreviousStatus: prevStatus,
            NewStatus: newStatus,
            HealthScore: healthScore,
            RecordsChecked: total,
            RecordsMatched: matchedCount,
            AllMatched: matchedCount == total,
            VerificationDurationMs: sw.ElapsedMilliseconds,
            UpdatedRecords: updatedRecords.AsReadOnly(),
            Summary: summary
        ));
    }

    public Task<bool> DeleteDomainAsync(Guid domainId)
    {
        bool removed = _domainStore.TryRemove(domainId, out _);
        return Task.FromResult(removed);
    }

    public RegistrarExportDto ExportRegistrarConfig(Guid domainId, string registrarName)
    {
        if (string.IsNullOrWhiteSpace(registrarName))
            throw new ArgumentException("Registrar name cannot be null or empty.", nameof(registrarName));

        var clean = registrarName.Trim().ToLowerInvariant();
        RegistrarProvider provider = clean switch
        {
            "cloudflare" => RegistrarProvider.Cloudflare,
            "aws" or "route53" or "awsroute53" => RegistrarProvider.AwsRoute53,
            "godaddy" => RegistrarProvider.GoDaddy,
            "namecheap" => RegistrarProvider.Namecheap,
            _ => throw new ArgumentException($"Unsupported registrar '{registrarName}'. Supported registrars: Cloudflare, AwsRoute53, GoDaddy, Namecheap.")
        };

        return ExportRegistrarConfig(domainId, provider);
    }

    public RegistrarExportDto ExportRegistrarConfig(Guid domainId, RegistrarProvider registrar)
    {
        if (!_domainStore.TryGetValue(domainId, out var domain))
        {
            throw new KeyNotFoundException($"Custom email domain with ID '{domainId}' was not found.");
        }

        var exportRecords = domain.Records.Select(r => new RegistrarExportRecord(
            Type: r.RecordType.ToString(),
            Hostname: r.Hostname,
            Value: r.ExpectedValue,
            Ttl: r.Ttl,
            Priority: r.Priority
        )).ToList();

        string rawOutput;
        string format;
        string instructions;

        var dkim = domain.Dkim;
        string returnPrefix = domain.ReturnPathPrefix;
        string dmarcExpected = domain.Records.FirstOrDefault(r => r.Purpose == "DMARC Policy")?.ExpectedValue
            ?? $"v=DMARC1; p={domain.DmarcPolicy.ToString().ToLowerInvariant()}; rua=mailto:{domain.DmarcReportEmail}; pct=100";

        switch (registrar)
        {
            case RegistrarProvider.Cloudflare:
                format = "ZoneFile";
                instructions = "Log in to Cloudflare Dashboard -> Select Domain -> DNS -> Records -> Import DNS Records (or manually paste the zone file). Ensure proxy status is set to 'DNS Only' (Grey Cloud).";
                var sbCf = new StringBuilder();
                sbCf.AppendLine($"; Cloudflare RFC 1035 BIND Zone File for {domain.Domain}");
                sbCf.AppendLine($"; Generated by SGrowing Whitelabel DNS Provisioner on {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC");
                sbCf.AppendLine($"$ORIGIN {domain.Domain}.");
                sbCf.AppendLine("$TTL 300");
                sbCf.AppendLine();
                sbCf.AppendLine($"; 1. Return-Path CNAME (Must be DNS Only / Grey Cloud)");
                sbCf.AppendLine($"{returnPrefix}    300    IN    CNAME    {DefaultReturnPathTarget}.");
                sbCf.AppendLine();
                sbCf.AppendLine($"; 2. 2048-bit RSA DKIM Key");
                sbCf.AppendLine($"{dkim.Selector}._domainkey    300    IN    TXT    \"{dkim.TxtRecordValue}\"");
                sbCf.AppendLine();
                sbCf.AppendLine($"; 3. SPF Alignment");
                sbCf.AppendLine($"@    300    IN    TXT    \"{DefaultSpfValue}\"");
                sbCf.AppendLine();
                sbCf.AppendLine($"; 4. DMARC Quarantine/Reject Policy");
                sbCf.AppendLine($"_dmarc    300    IN    TXT    \"{dmarcExpected}\"");
                sbCf.AppendLine();
                sbCf.AppendLine($"; 5. Inbound Bounce Routing MX");
                sbCf.AppendLine($"{returnPrefix}    300    IN    MX    {DefaultMxPriority}    {DefaultMxTarget}.");
                rawOutput = sbCf.ToString();
                break;

            case RegistrarProvider.AwsRoute53:
                format = "JsonChangeBatch";
                instructions = "In AWS Route 53, select your Hosted Zone -> Create records -> Switch to JSON / import. Alternatively use AWS CLI: `aws route53 change-resource-record-sets --hosted-zone-id <YOUR_ZONE_ID> --change-batch file://records.json`.";
                
                var changes = new List<object>
                {
                    new
                    {
                        Action = "UPSERT",
                        ResourceRecordSet = new
                        {
                            Name = $"{returnPrefix}.{domain.Domain}.",
                            Type = "CNAME",
                            TTL = 300,
                            ResourceRecords = new[] { new { Value = $"\"{DefaultReturnPathTarget}\"" } }
                        }
                    },
                    new
                    {
                        Action = "UPSERT",
                        ResourceRecordSet = new
                        {
                            Name = $"{dkim.Selector}._domainkey.{domain.Domain}.",
                            Type = "TXT",
                            TTL = 300,
                            ResourceRecords = new[] { new { Value = $"\"{dkim.TxtRecordValue}\"" } }
                        }
                    },
                    new
                    {
                        Action = "UPSERT",
                        ResourceRecordSet = new
                        {
                            Name = $"{domain.Domain}.",
                            Type = "TXT",
                            TTL = 300,
                            ResourceRecords = new[] { new { Value = $"\"{DefaultSpfValue}\"" } }
                        }
                    },
                    new
                    {
                        Action = "UPSERT",
                        ResourceRecordSet = new
                        {
                            Name = $"_dmarc.{domain.Domain}.",
                            Type = "TXT",
                            TTL = 300,
                            ResourceRecords = new[] { new { Value = $"\"{dmarcExpected}\"" } }
                        }
                    },
                    new
                    {
                        Action = "UPSERT",
                        ResourceRecordSet = new
                        {
                            Name = $"{returnPrefix}.{domain.Domain}.",
                            Type = "MX",
                            TTL = 300,
                            ResourceRecords = new[] { new { Value = $"{DefaultMxPriority} \"{DefaultMxTarget}\"" } }
                        }
                    }
                };

                var route53Batch = new
                {
                    Comment = $"SGrowing Custom Email Domain Provisioning for {domain.Domain}",
                    Changes = changes
                };
                rawOutput = JsonSerializer.Serialize(route53Batch, new JsonSerializerOptions { WriteIndented = true });
                break;

            case RegistrarProvider.GoDaddy:
                format = "JsonApi";
                instructions = "Go to GoDaddy Domain Portfolio -> Select Domain -> DNS -> Add New Record or import via GoDaddy DNS REST API: PUT /v1/domains/{domain}/records.";
                var goDaddyRecords = new object[]
                {
                    new { type = "CNAME", name = returnPrefix, data = DefaultReturnPathTarget, ttl = 600 },
                    new { type = "TXT", name = $"{dkim.Selector}._domainkey", data = dkim.TxtRecordValue, ttl = 600 },
                    new { type = "TXT", name = "@", data = DefaultSpfValue, ttl = 600 },
                    new { type = "TXT", name = "_dmarc", data = dmarcExpected, ttl = 600 },
                    new { type = "MX", name = returnPrefix, data = DefaultMxTarget, priority = (int?)DefaultMxPriority, ttl = 600 }
                };
                rawOutput = JsonSerializer.Serialize(goDaddyRecords, new JsonSerializerOptions { WriteIndented = true });
                break;

            case RegistrarProvider.Namecheap:
                format = "Table";
                instructions = "Go to Namecheap Dashboard -> Domain List -> Click 'Manage' next to your domain -> 'Advanced DNS' tab -> 'Add New Record' for each row below.";
                var sbNc = new StringBuilder();
                sbNc.AppendLine($"# Namecheap Advanced DNS Table for {domain.Domain}");
                sbNc.AppendLine($"# Hostnames in Namecheap are relative to the root domain");
                sbNc.AppendLine();
                sbNc.AppendLine(string.Format("{0,-10} | {1,-24} | {2,-45} | {3,-10}", "Type", "Host", "Value / Target", "TTL"));
                sbNc.AppendLine(new string('-', 95));
                sbNc.AppendLine(string.Format("{0,-10} | {1,-24} | {2,-45} | {3,-10}", "CNAME", returnPrefix, DefaultReturnPathTarget, "Automatic"));
                sbNc.AppendLine(string.Format("{0,-10} | {1,-24} | {2,-45} | {3,-10}", "TXT", $"{dkim.Selector}._domainkey", dkim.TxtRecordValue, "Automatic"));
                sbNc.AppendLine(string.Format("{0,-10} | {1,-24} | {2,-45} | {3,-10}", "TXT", "@", DefaultSpfValue, "Automatic"));
                sbNc.AppendLine(string.Format("{0,-10} | {1,-24} | {2,-45} | {3,-10}", "TXT", "_dmarc", dmarcExpected, "Automatic"));
                sbNc.AppendLine(string.Format("{0,-10} | {1,-24} | {2,-45} | {3,-10}", "MX", returnPrefix, $"{DefaultMxTarget} (Pri: {DefaultMxPriority})", "Automatic"));
                rawOutput = sbNc.ToString();
                break;

            default:
                throw new ArgumentOutOfRangeException(nameof(registrar), registrar, "Unsupported registrar provider.");
        }

        return new RegistrarExportDto(
            Registrar: registrar.ToString(),
            Domain: domain.Domain,
            Format: format,
            RawOutput: rawOutput,
            Instructions: instructions,
            Records: exportRecords.AsReadOnly()
        );
    }

    public Task<DeliverabilityDomainMetricsDto> GetOverviewMetricsAsync(Guid? tenantId = null)
    {
        var query = _domainStore.Values.AsEnumerable();
        if (tenantId.HasValue && tenantId.Value != Guid.Empty)
        {
            query = query.Where(d => d.TenantId == tenantId.Value);
        }

        var domains = query.ToList();
        int total = domains.Count;
        int active = domains.Count(d => d.Status == CustomDomainStatus.Active);
        int pending = domains.Count(d => d.Status == CustomDomainStatus.PendingDns);
        int degraded = domains.Count(d => d.Status == CustomDomainStatus.DnsDegraded);
        int verifying = domains.Count(d => d.Status == CustomDomainStatus.Verifying);

        double avgHealth = total > 0 ? Math.Round(domains.Average(d => d.HealthScore), 1) : 0;
        int totalDkim = total; // Each domain has a dedicated 2048-bit key
        double fullyAlignedRate = total > 0 ? Math.Round((double)active / total * 100.0, 1) : 0;

        return Task.FromResult(new DeliverabilityDomainMetricsDto(
            TotalDomains: total,
            ActiveDomains: active,
            PendingDnsDomains: pending,
            DegradedDomains: degraded,
            VerifyingDomains: verifying,
            AverageHealthScore: avgHealth,
            TotalDkimKeysGenerated: totalDkim,
            FullyAlignedRatePercent: fullyAlignedRate
        ));
    }
}
