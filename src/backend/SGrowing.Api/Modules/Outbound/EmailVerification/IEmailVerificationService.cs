namespace SGrowing.Api.Modules.Outbound.EmailVerification;

public enum EmailVerificationStatus
{
    Deliverable,
    Risky,
    Undeliverable,
    Disposable,
    CatchAll
}

public record EmailVerificationResult(
    string Email,
    bool SyntaxValid,
    bool IsDisposable,
    string Domain,
    bool HasMxRecords,
    List<string> MxHosts,
    bool SmtpConnectionPassed,
    bool IsCatchAll,
    int RiskScore, // 0 = Lowest risk (100% deliverable), 100 = Guaranteed bounce
    EmailVerificationStatus Status,
    string Reason
);

public interface IEmailVerificationService
{
    Task<EmailVerificationResult> VerifyEmailAsync(string email, CancellationToken ct = default);
    Task<List<EmailVerificationResult>> BatchVerifyEmailsAsync(IEnumerable<string> emails, CancellationToken ct = default);
}
