using System.Collections.Concurrent;
using System.Net;

namespace SGrowing.Api.Modules.CustomerData.Firmographics;

public interface IFirmographicsEngine
{
    DeanonymizeIpResult DeanonymizeIp(DeanonymizeIpRequest request);
    FirmographicCompanyProfile? GetCompany(string tenantId, string domain);
    void RegisterTenantIcpRules(string tenantId, List<IcpScoringRule> rules);
    void RegisterCompanyProfile(string tenantId, FirmographicCompanyProfile profile, List<string>? ipRanges = null);
}

public class FirmographicsEngine : IFirmographicsEngine
{
    private record IpBlockMapping(string CidrOrIp, FirmographicCompanyProfile Profile);

    private readonly ConcurrentDictionary<string, FirmographicCompanyProfile> _globalCompanies = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentBag<IpBlockMapping> _globalIpMappings = new();

    private readonly ConcurrentDictionary<string, FirmographicCompanyProfile> _tenantCompanies = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, List<IpBlockMapping>> _tenantIpMappings = new();
    private readonly ConcurrentDictionary<string, List<IcpScoringRule>> _tenantRules = new();

    public FirmographicsEngine()
    {
        SeedEnterpriseProfiles();
    }

    private void SeedEnterpriseProfiles()
    {
        var acme = new FirmographicCompanyProfile(
            Domain: "acmecorp.com",
            CompanyName: "Acme Corp",
            Industry: "Technology",
            EmployeeCount: 2500,
            RevenueRange: "$100M - $250M",
            HeadquartersCity: "San Francisco",
            HeadquartersCountry: "United States",
            TechStack: new List<string> { "Kubernetes", "AWS", "Snowflake", "Salesforce", "React" }
        );

        var techScale = new FirmographicCompanyProfile(
            Domain: "techscale.io",
            CompanyName: "TechScale",
            Industry: "Technology",
            EmployeeCount: 180,
            RevenueRange: "$20M - $50M",
            HeadquartersCity: "Austin",
            HeadquartersCountry: "United States",
            TechStack: new List<string> { "PostgreSQL", "Docker", "Node.js", "HubSpot", "Tailwind" }
        );

        var globalLogistics = new FirmographicCompanyProfile(
            Domain: "globallogistics.com",
            CompanyName: "GlobalLogistics",
            Industry: "Logistics",
            EmployeeCount: 12000,
            RevenueRange: "$1B+",
            HeadquartersCity: "Rotterdam",
            HeadquartersCountry: "Netherlands",
            TechStack: new List<string> { "SAP", "Azure", "Oracle", "Kafka", "Angular" }
        );

        var megacorp = new FirmographicCompanyProfile(
            Domain: "megacorp.com",
            CompanyName: "Megacorp",
            Industry: "Finance",
            EmployeeCount: 45000,
            RevenueRange: "$5B+",
            HeadquartersCity: "New York",
            HeadquartersCountry: "United States",
            TechStack: new List<string> { "AWS", "C# / .NET", "Snowflake", "Databricks", "Workday" }
        );

        RegisterGlobalCompany(acme, "acme.corp", "198.51.100.0/24", "198.51.100.25", "64.233.160.0/24", "64.233.160.1");
        RegisterGlobalCompany(techScale, "techscale.com", "203.0.113.0/24", "203.0.113.45", "203.0.113.10");
        RegisterGlobalCompany(globalLogistics, null, "192.0.2.0/24", "192.0.2.88", "192.0.2.1");
        RegisterGlobalCompany(megacorp, null, "12.145.20.0/24", "12.145.20.50", "12.145.20.1");
    }

    private void RegisterGlobalCompany(FirmographicCompanyProfile profile, string? aliasDomain, params string[] ipCidrs)
    {
        _globalCompanies[profile.Domain] = profile;
        if (!string.IsNullOrWhiteSpace(aliasDomain))
        {
            _globalCompanies[aliasDomain] = profile;
        }

        foreach (var cidr in ipCidrs)
        {
            _globalIpMappings.Add(new IpBlockMapping(cidr, profile));
        }
    }

    public void RegisterCompanyProfile(string tenantId, FirmographicCompanyProfile profile, List<string>? ipRanges = null)
    {
        var cleanDomain = NormalizeDomain(profile.Domain);
        var tenantKey = BuildTenantDomainKey(tenantId, cleanDomain);
        _tenantCompanies[tenantKey] = profile;

        if (ipRanges != null && ipRanges.Count > 0)
        {
            var list = _tenantIpMappings.GetOrAdd(tenantId, _ => new List<IpBlockMapping>());
            lock (list)
            {
                foreach (var ipRange in ipRanges)
                {
                    list.Add(new IpBlockMapping(ipRange, profile));
                }
            }
        }
    }

    public void RegisterTenantIcpRules(string tenantId, List<IcpScoringRule> rules)
    {
        _tenantRules[tenantId] = rules ?? new List<IcpScoringRule>();
    }

    public FirmographicCompanyProfile? GetCompany(string tenantId, string domain)
    {
        if (string.IsNullOrWhiteSpace(domain))
            return null;

        var cleanDomain = NormalizeDomain(domain);

        // 1. Check tenant specific store
        var tenantKey = BuildTenantDomainKey(tenantId, cleanDomain);
        if (_tenantCompanies.TryGetValue(tenantKey, out var tenantProfile))
        {
            return tenantProfile;
        }

        // 2. Check global company directory
        if (_globalCompanies.TryGetValue(cleanDomain, out var globalProfile))
        {
            return globalProfile;
        }

        return null;
    }

    public DeanonymizeIpResult DeanonymizeIp(DeanonymizeIpRequest request)
    {
        var now = DateTime.UtcNow;

        if (string.IsNullOrWhiteSpace(request.IpAddress) || !IPAddress.TryParse(request.IpAddress.Trim(), out var parsedIp))
        {
            return new DeanonymizeIpResult(
                MatchFound: false,
                IpAddress: request.IpAddress ?? string.Empty,
                Company: null,
                IcpFit: null,
                TriggeredSalesAlert: false,
                AlertDetails: "Invalid or missing IP address format.",
                ProcessedAtUtc: now
            );
        }

        // 1. Resolve Company from IP
        var matchedCompany = ResolveCompanyFromIp(request.TenantId, parsedIp);
        if (matchedCompany == null)
        {
            return new DeanonymizeIpResult(
                MatchFound: false,
                IpAddress: request.IpAddress,
                Company: null,
                IcpFit: null,
                TriggeredSalesAlert: false,
                AlertDetails: "No enterprise reverse IP match found (Consumer ISP or unmapped IP block).",
                ProcessedAtUtc: now
            );
        }

        // Cache the matched company in the tenant's company directory
        var tenantKey = BuildTenantDomainKey(request.TenantId, NormalizeDomain(matchedCompany.Domain));
        _tenantCompanies[tenantKey] = matchedCompany;

        // 2. Evaluate ICP Fit
        IcpEvaluationResult icpFit;
        if (_tenantRules.TryGetValue(request.TenantId, out var customRules) && customRules.Count > 0)
        {
            icpFit = EvaluateCustomIcpRules(matchedCompany, customRules, request.VisitedPage);
        }
        else
        {
            icpFit = EvaluateDefaultIcp(matchedCompany, request.VisitedPage);
        }

        // 3. High-Intent Sales Alert Trigger:
        // If FitTier == "Tier_A" and VisitedPage is /pricing, /demo, or /checkout, triggers TriggeredSalesAlert = true
        bool isHighIntentPage = IsHighIntentPage(request.VisitedPage);
        bool triggeredSalesAlert = icpFit.FitTier == "Tier_A" && isHighIntentPage;

        string alertDetails;
        if (triggeredSalesAlert)
        {
            alertDetails = $"[HIGH INTENT SALES ALERT] Enterprise Tier_A account {matchedCompany.CompanyName} ({matchedCompany.Domain}, {matchedCompany.EmployeeCount} employees) is actively viewing '{request.VisitedPage}'. Immediate priority sales outreach triggered.";
        }
        else if (icpFit.FitTier == "Tier_A")
        {
            alertDetails = $"Enterprise Tier_A target {matchedCompany.CompanyName} active on page '{request.VisitedPage ?? "unknown"}'. Standard nurturing assigned.";
        }
        else
        {
            alertDetails = $"Visitor mapped to {matchedCompany.CompanyName} ({matchedCompany.Industry}, {matchedCompany.EmployeeCount} employees) with fit tier {icpFit.FitTier} (Score: {icpFit.CompositeScore}).";
        }

        return new DeanonymizeIpResult(
            MatchFound: true,
            IpAddress: request.IpAddress,
            Company: matchedCompany,
            IcpFit: icpFit,
            TriggeredSalesAlert: triggeredSalesAlert,
            AlertDetails: alertDetails,
            ProcessedAtUtc: now
        );
    }

    private FirmographicCompanyProfile? ResolveCompanyFromIp(string tenantId, IPAddress ip)
    {
        // Check tenant custom mappings first
        if (_tenantIpMappings.TryGetValue(tenantId, out var tenantMappings))
        {
            lock (tenantMappings)
            {
                foreach (var mapping in tenantMappings)
                {
                    if (IsIpInCidr(ip, mapping.CidrOrIp))
                        return mapping.Profile;
                }
            }
        }

        // Check global seeded mappings
        foreach (var mapping in _globalIpMappings)
        {
            if (IsIpInCidr(ip, mapping.CidrOrIp))
                return mapping.Profile;
        }

        return null;
    }

    private IcpEvaluationResult EvaluateDefaultIcp(FirmographicCompanyProfile company, string? visitedPage)
    {
        int score = 0;
        var reasons = new List<string>();

        // 1. Industry Scoring (Target: Tech, Logistics, Finance, Healthcare, Manufacturing)
        var highValueIndustries = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "Technology", "Logistics", "Finance", "Healthcare", "Manufacturing", "Enterprise Software"
        };

        if (highValueIndustries.Contains(company.Industry))
        {
            score += 50;
            reasons.Add($"High-priority target industry ({company.Industry}: +50 pts)");
        }
        else
        {
            score += 20;
            reasons.Add($"Secondary industry ({company.Industry}: +20 pts)");
        }

        // 2. Employee Count Scoring
        if (company.EmployeeCount >= 500)
        {
            score += 40;
            reasons.Add($"Enterprise scale ({company.EmployeeCount} employees: +40 pts)");
        }
        else if (company.EmployeeCount >= 100)
        {
            score += 25;
            reasons.Add($"Mid-market scale ({company.EmployeeCount} employees: +25 pts)");
        }
        else if (company.EmployeeCount >= 20)
        {
            score += 15;
            reasons.Add($"Growth scale ({company.EmployeeCount} employees: +15 pts)");
        }
        else
        {
            score += 5;
            reasons.Add($"Early scale ({company.EmployeeCount} employees: +5 pts)");
        }

        // 3. Revenue Scale Bonus
        if (company.RevenueRange.Contains("$1B", StringComparison.OrdinalIgnoreCase) ||
            company.RevenueRange.Contains("$5B", StringComparison.OrdinalIgnoreCase) ||
            company.RevenueRange.Contains("$100M", StringComparison.OrdinalIgnoreCase))
        {
            score += 10;
            reasons.Add($"Enterprise revenue tier ({company.RevenueRange}: +10 pts)");
        }

        int compositeScore = Math.Clamp(score, 0, 100);
        string fitTier = compositeScore switch
        {
            >= 80 => "Tier_A",
            >= 60 => "Tier_B",
            >= 40 => "Tier_C",
            _ => "Unqualified"
        };

        bool isHighIntent = fitTier == "Tier_A" && IsHighIntentPage(visitedPage);
        string rationale = string.Join("; ", reasons);

        return new IcpEvaluationResult(fitTier, compositeScore, isHighIntent, rationale);
    }

    private IcpEvaluationResult EvaluateCustomIcpRules(FirmographicCompanyProfile company, List<IcpScoringRule> rules, string? visitedPage)
    {
        if (rules.Count == 0)
        {
            return EvaluateDefaultIcp(company, visitedPage);
        }

        decimal totalScore = 0;
        var rationales = new List<string>();

        // Calculate sum of weights to determine if cumulative component rules or distinct persona segments
        decimal weightSum = rules.Sum(r => r.Weight <= 1.0m ? r.Weight * 100 : r.Weight);
        bool isCumulative = weightSum <= 100.0m;

        if (isCumulative)
        {
            foreach (var rule in rules)
            {
                decimal effectiveWeight = rule.Weight <= 1.0m ? rule.Weight * 100 : rule.Weight;
                bool hasIndustry = !string.IsNullOrWhiteSpace(rule.TargetIndustry) && rule.TargetIndustry != "*";
                bool industryMatches = !hasIndustry ||
                                      company.Industry.Equals(rule.TargetIndustry, StringComparison.OrdinalIgnoreCase) ||
                                      company.Industry.Contains(rule.TargetIndustry, StringComparison.OrdinalIgnoreCase);

                bool hasEmployeeConstraint = rule.MinEmployees > 0 || rule.MaxEmployees > 0;
                bool employeeMatches = !hasEmployeeConstraint ||
                                       (company.EmployeeCount >= rule.MinEmployees &&
                                        (rule.MaxEmployees <= 0 || company.EmployeeCount <= rule.MaxEmployees));

                if (industryMatches && employeeMatches)
                {
                    totalScore += effectiveWeight;
                    rationales.Add($"Matched rule ({rule.TargetIndustry} [{rule.MinEmployees}-{rule.MaxEmployees}]): +{effectiveWeight:F0} pts");
                }
                else if (industryMatches && hasEmployeeConstraint)
                {
                    decimal partial = effectiveWeight * 0.5m;
                    totalScore += partial;
                    rationales.Add($"Industry match for {rule.TargetIndustry} (outside employee range): +{partial:F0} pts");
                }
                else if (employeeMatches && hasIndustry)
                {
                    decimal partial = effectiveWeight * 0.3m;
                    totalScore += partial;
                    rationales.Add($"Size match [{rule.MinEmployees}-{rule.MaxEmployees}] (industry mismatch): +{partial:F0} pts");
                }
            }
        }
        else
        {
            decimal bestScore = 0;
            string bestRationale = "No custom ICP persona matched.";

            foreach (var rule in rules)
            {
                decimal effectiveWeight = rule.Weight <= 1.0m ? rule.Weight * 100 : rule.Weight;
                bool hasIndustry = !string.IsNullOrWhiteSpace(rule.TargetIndustry) && rule.TargetIndustry != "*";
                bool industryMatches = !hasIndustry ||
                                      company.Industry.Equals(rule.TargetIndustry, StringComparison.OrdinalIgnoreCase) ||
                                      company.Industry.Contains(rule.TargetIndustry, StringComparison.OrdinalIgnoreCase);

                bool hasEmployeeConstraint = rule.MinEmployees > 0 || rule.MaxEmployees > 0;
                bool employeeMatches = !hasEmployeeConstraint ||
                                       (company.EmployeeCount >= rule.MinEmployees &&
                                        (rule.MaxEmployees <= 0 || company.EmployeeCount <= rule.MaxEmployees));

                decimal score = 0;
                string rationale;

                if (industryMatches && employeeMatches)
                {
                    score = effectiveWeight;
                    rationale = $"Full ICP match for persona {rule.TargetIndustry} [{rule.MinEmployees}-{rule.MaxEmployees}]: {score:F0} pts";
                }
                else if (industryMatches)
                {
                    score = effectiveWeight * 0.5m;
                    rationale = $"Industry match for persona {rule.TargetIndustry}: {score:F0} pts";
                }
                else if (employeeMatches)
                {
                    score = effectiveWeight * 0.3m;
                    rationale = $"Size match for persona [{rule.MinEmployees}-{rule.MaxEmployees}]: {score:F0} pts";
                }
                else
                {
                    rationale = "Persona did not match.";
                }

                if (score > bestScore)
                {
                    bestScore = score;
                    bestRationale = rationale;
                }
            }

            totalScore = bestScore;
            rationales.Add(bestRationale);
        }

        int compositeScore = Math.Clamp((int)Math.Round(totalScore), 0, 100);
        string fitTier = compositeScore switch
        {
            >= 80 => "Tier_A",
            >= 60 => "Tier_B",
            >= 40 => "Tier_C",
            _ => "Unqualified"
        };

        bool isHighIntent = fitTier == "Tier_A" && IsHighIntentPage(visitedPage);
        string combinedRationale = rationales.Count > 0 ? string.Join("; ", rationales) : "No ICP rules matched.";

        return new IcpEvaluationResult(fitTier, compositeScore, isHighIntent, combinedRationale);
    }

    private static bool IsHighIntentPage(string? visitedPage)
    {
        if (string.IsNullOrWhiteSpace(visitedPage))
            return false;

        var clean = visitedPage.Trim().ToLowerInvariant();
        return clean.StartsWith("/pricing") ||
               clean.StartsWith("/demo") ||
               clean.StartsWith("/checkout") ||
               clean.Equals("pricing") ||
               clean.Equals("demo") ||
               clean.Equals("checkout");
    }

    private static bool IsIpInCidr(IPAddress address, string cidrOrIp)
    {
        if (string.IsNullOrWhiteSpace(cidrOrIp))
            return false;

        var parts = cidrOrIp.Split('/');
        if (parts.Length != 2)
        {
            return IPAddress.TryParse(cidrOrIp, out var singleIp) && singleIp.Equals(address);
        }

        if (!IPAddress.TryParse(parts[0], out var baseIp) || !int.TryParse(parts[1], out var prefixLength))
            return false;

        var baseBytes = baseIp.GetAddressBytes();
        var addressBytes = address.GetAddressBytes();

        if (baseBytes.Length != addressBytes.Length)
            return false;

        int fullBytes = prefixLength / 8;
        int remainingBits = prefixLength % 8;

        for (int i = 0; i < fullBytes; i++)
        {
            if (baseBytes[i] != addressBytes[i])
                return false;
        }

        if (remainingBits > 0 && fullBytes < baseBytes.Length)
        {
            byte mask = (byte)(0xFF << (8 - remainingBits));
            if ((baseBytes[fullBytes] & mask) != (addressBytes[fullBytes] & mask))
                return false;
        }

        return true;
    }

    private static string NormalizeDomain(string domain)
    {
        var clean = domain.Trim().ToLowerInvariant()
            .Replace("https://", "")
            .Replace("http://", "")
            .TrimEnd('/');

        if (clean.StartsWith("www."))
            clean = clean[4..];

        return clean;
    }

    private static string BuildTenantDomainKey(string tenantId, string domain)
    {
        return $"{tenantId.Trim().ToLowerInvariant()}:{domain}";
    }
}
