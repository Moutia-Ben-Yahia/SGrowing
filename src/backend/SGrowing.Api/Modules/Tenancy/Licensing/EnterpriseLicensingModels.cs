namespace SGrowing.Api.Modules.Tenancy.Licensing;

public record EnterpriseLicense(
    string LicenseKey,
    string OrganizationName,
    string Tier, // "Enterprise", "Government", "Scale"
    int MaxSeats,
    int MaxMonthlyEvents,
    List<string> EntitledModules,
    DateTime IssuedAtUtc,
    DateTime ExpiresAtUtc,
    bool IsAirGappedValid,
    string CryptographicSignature
);

public record LicenseValidationResult(
    bool IsValid,
    string StatusMessage,
    EnterpriseLicense? License,
    int DaysRemaining
);

public record ActivateLicenseRequest(
    string LicenseKey,
    string OrganizationName,
    string SignedPayload
);

public record AirGapVerificationRequest(
    string SignedPayload,
    string MachineFingerprint
);
