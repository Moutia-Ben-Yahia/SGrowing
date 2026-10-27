using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;

namespace SGrowing.Api.Modules.CRM.ContractSigning;

internal class OtpSession
{
    public string Code { get; set; } = string.Empty;
    public DateTime ExpiresAtUtc { get; set; }
    public int AttemptsLeft { get; set; } = 3;
    public bool IsVerified { get; set; }
}

public class MultiPartySigningService : IMultiPartySigningService
{
    private static readonly ConcurrentDictionary<Guid, MultiPartyContractDto> _contracts = new();
    private static readonly ConcurrentDictionary<string, Guid> _tokenToContractMap = new(StringComparer.Ordinal);
    private static readonly ConcurrentDictionary<string, OtpSession> _otpRegistry = new(StringComparer.Ordinal);

    public Task<MultiPartyContractDto> CreateMultiPartyContractAsync(Guid tenantId, CreateMultiPartyContractRequest req, CancellationToken ct = default)
    {
        var contractId = Guid.NewGuid();
        var signers = new List<ContractSignerDto>();

        foreach (var s in req.Signers.OrderBy(s => s.OrderIndex))
        {
            var token = Guid.NewGuid().ToString("N");
            var signer = new ContractSignerDto(
                SignerId: Guid.NewGuid(),
                OrderIndex: s.OrderIndex,
                FullName: s.FullName,
                Email: s.Email,
                PhoneNumber: s.PhoneNumber,
                Role: s.Role,
                Status: SignerStatus.Pending,
                SigningToken: token,
                RequiresOtp: s.RequiresOtp,
                IsOtpVerified: false,
                SentAtUtc: null,
                ViewedAtUtc: null,
                SignedAtUtc: null,
                SignatureImageBase64: null
            );
            signers.Add(signer);
            _tokenToContractMap[token] = contractId;
        }

        var contract = new MultiPartyContractDto(
            ContractId: contractId,
            TenantId: tenantId,
            Title: req.Title,
            DocumentUrl: req.DocumentUrl,
            WorkflowType: req.WorkflowType,
            OverallStatus: "Draft",
            Signers: signers,
            VisualFields: req.VisualFields,
            CurrentSequentialOrderIndex: 1,
            CreatedAtUtc: DateTime.UtcNow,
            CompletedAtUtc: null,
            SealedPdfUrl: null,
            ChecksumSha256: null
        );

        _contracts[contractId] = contract;
        return Task.FromResult(contract);
    }

    public Task<MultiPartyContractDto> InitiateWorkflowAsync(Guid tenantId, Guid contractId, CancellationToken ct = default)
    {
        if (!_contracts.TryGetValue(contractId, out var contract) || contract.TenantId != tenantId)
        {
            throw new KeyNotFoundException($"Contract {contractId} not found.");
        }

        var updatedSigners = new List<ContractSignerDto>();
        int currentOrder = 1;

        if (contract.WorkflowType == SigningWorkflowType.Sequential)
        {
            // Only signers with minimum OrderIndex get Sent status
            currentOrder = contract.Signers.Min(s => s.OrderIndex);
            foreach (var s in contract.Signers)
            {
                if (s.OrderIndex == currentOrder)
                {
                    updatedSigners.Add(s with { Status = SignerStatus.Sent, SentAtUtc = DateTime.UtcNow });
                }
                else
                {
                    updatedSigners.Add(s);
                }
            }
        }
        else
        {
            // Parallel: all signers get Sent
            foreach (var s in contract.Signers)
            {
                updatedSigners.Add(s with { Status = SignerStatus.Sent, SentAtUtc = DateTime.UtcNow });
            }
        }

        var updatedContract = contract with
        {
            OverallStatus = "InProgress",
            Signers = updatedSigners,
            CurrentSequentialOrderIndex = currentOrder
        };

        _contracts[contractId] = updatedContract;
        return Task.FromResult(updatedContract);
    }

    public Task<OtpVerificationResult> SendSignerOtpAsync(string signingToken, string destination = "email", CancellationToken ct = default)
    {
        if (!_tokenToContractMap.TryGetValue(signingToken, out var contractId) || !_contracts.TryGetValue(contractId, out var contract))
        {
            return Task.FromResult(new OtpVerificationResult(false, "Invalid signing token.", 0, false));
        }

        var signer = contract.Signers.FirstOrDefault(s => s.SigningToken == signingToken);
        if (signer == null)
        {
            return Task.FromResult(new OtpVerificationResult(false, "Signer not found.", 0, false));
        }

        // Generate 6-digit numeric OTP
        var code = Random.Shared.Next(100000, 999999).ToString();
        var session = new OtpSession
        {
            Code = code,
            ExpiresAtUtc = DateTime.UtcNow.AddMinutes(10),
            AttemptsLeft = 3,
            IsVerified = false
        };

        _otpRegistry[signingToken] = session;

        return Task.FromResult(new OtpVerificationResult(
            Success: true,
            Message: $"Verification code sent via {destination} to {signer.Email}.",
            RemainingAttempts: 3,
            IsVerified: false,
            DevTestOtpCode: code
        ));
    }

    public Task<OtpVerificationResult> VerifySignerOtpAsync(string signingToken, string otpCode, CancellationToken ct = default)
    {
        if (!_otpRegistry.TryGetValue(signingToken, out var session))
        {
            return Task.FromResult(new OtpVerificationResult(false, "No active OTP request found. Please request a new code.", 0, false));
        }

        if (DateTime.UtcNow > session.ExpiresAtUtc)
        {
            _otpRegistry.TryRemove(signingToken, out _);
            return Task.FromResult(new OtpVerificationResult(false, "Verification code has expired. Please request a new code.", 0, false));
        }

        if (session.AttemptsLeft <= 0)
        {
            _otpRegistry.TryRemove(signingToken, out _);
            return Task.FromResult(new OtpVerificationResult(false, "Maximum attempts exceeded. Please request a new code.", 0, false));
        }

        if (session.Code != otpCode.Trim())
        {
            session.AttemptsLeft--;
            return Task.FromResult(new OtpVerificationResult(false, "Incorrect verification code.", session.AttemptsLeft, false));
        }

        session.IsVerified = true;

        // Update signer status to OtpVerified in contract
        if (_tokenToContractMap.TryGetValue(signingToken, out var contractId) && _contracts.TryGetValue(contractId, out var contract))
        {
            var updatedSigners = contract.Signers.Select(s => s.SigningToken == signingToken ? s with { IsOtpVerified = true, Status = SignerStatus.OtpVerified } : s).ToList();
            _contracts[contractId] = contract with { Signers = updatedSigners };
        }

        return Task.FromResult(new OtpVerificationResult(true, "Identity verified successfully.", session.AttemptsLeft, true, session.Code));
    }

    public Task<MultiPartyContractDto> SubmitSignerSignatureAsync(string signingToken, SubmitSignatureRequest req, CancellationToken ct = default)
    {
        if (!_tokenToContractMap.TryGetValue(signingToken, out var contractId) || !_contracts.TryGetValue(contractId, out var contract))
        {
            throw new KeyNotFoundException("Invalid signing token.");
        }

        var signer = contract.Signers.FirstOrDefault(s => s.SigningToken == signingToken);
        if (signer == null)
        {
            throw new KeyNotFoundException("Signer not found.");
        }

        if (signer.RequiresOtp && !signer.IsOtpVerified)
        {
            if (_otpRegistry.TryGetValue(signingToken, out var otp) && otp.IsVerified)
            {
                signer = signer with { IsOtpVerified = true };
            }
            else
            {
                throw new InvalidOperationException("Signer identity must be verified via OTP prior to signature submission.");
            }
        }

        // Update signer to Signed
        var signedSigner = signer with
        {
            Status = SignerStatus.Signed,
            SignedAtUtc = DateTime.UtcNow,
            SignatureImageBase64 = req.SignatureImageBase64
        };

        var updatedSigners = contract.Signers.Select(s => s.SigningToken == signingToken ? signedSigner : s).ToList();

        // Update visual fields with values submitted by this signer
        var updatedFields = new List<VisualPdfFieldDto>(contract.VisualFields);
        if (req.CompletedFields != null)
        {
            for (int i = 0; i < updatedFields.Count; i++)
            {
                var submitted = req.CompletedFields.FirstOrDefault(cf => cf.FieldId == updatedFields[i].FieldId);
                if (submitted != null && !string.IsNullOrEmpty(submitted.Value))
                {
                    updatedFields[i] = updatedFields[i] with { Value = submitted.Value };
                }
            }
        }

        // Orchestration progression
        bool allSigned = updatedSigners.All(s => s.Status == SignerStatus.Signed);
        int nextOrderIndex = contract.CurrentSequentialOrderIndex;
        string overallStatus = allSigned ? "Completed" : "InProgress";
        string? sealedUrl = null;
        string? checksumSha256 = null;

        if (allSigned)
        {
            using var sha256 = SHA256.Create();
            var rawBytes = Encoding.UTF8.GetBytes($"{contract.ContractId}:{contract.DocumentUrl}:{DateTime.UtcNow:O}");
            checksumSha256 = Convert.ToHexString(sha256.ComputeHash(rawBytes)).ToLowerInvariant();
            sealedUrl = $"/storage/sealed/{contract.TenantId}/{contract.ContractId}-sealed.pdf";
        }
        else if (contract.WorkflowType == SigningWorkflowType.Sequential)
        {
            // Advance sequential signer index
            var remaining = updatedSigners.Where(s => s.Status == SignerStatus.Pending).OrderBy(s => s.OrderIndex).ToList();
            if (remaining.Count > 0)
            {
                nextOrderIndex = remaining.First().OrderIndex;
                for (int i = 0; i < updatedSigners.Count; i++)
                {
                    if (updatedSigners[i].OrderIndex == nextOrderIndex && updatedSigners[i].Status == SignerStatus.Pending)
                    {
                        updatedSigners[i] = updatedSigners[i] with { Status = SignerStatus.Sent, SentAtUtc = DateTime.UtcNow };
                    }
                }
            }
        }

        var result = contract with
        {
            Signers = updatedSigners,
            VisualFields = updatedFields,
            CurrentSequentialOrderIndex = nextOrderIndex,
            OverallStatus = overallStatus,
            CompletedAtUtc = allSigned ? DateTime.UtcNow : null,
            SealedPdfUrl = sealedUrl,
            ChecksumSha256 = checksumSha256
        };

        _contracts[contractId] = result;
        return Task.FromResult(result);
    }

    public Task<MultiPartyContractDto> GetContractStatusAsync(Guid contractId, CancellationToken ct = default)
    {
        if (!_contracts.TryGetValue(contractId, out var contract))
        {
            throw new KeyNotFoundException($"Contract {contractId} not found.");
        }
        return Task.FromResult(contract);
    }

    public Task<MultiPartyContractDto?> GetContractBySigningTokenAsync(string signingToken, CancellationToken ct = default)
    {
        if (_tokenToContractMap.TryGetValue(signingToken, out var contractId) && _contracts.TryGetValue(contractId, out var contract))
        {
            // Auto mark as viewed
            var updatedSigners = contract.Signers.Select(s =>
                s.SigningToken == signingToken && s.Status == SignerStatus.Sent
                    ? s with { Status = SignerStatus.Viewed, ViewedAtUtc = DateTime.UtcNow }
                    : s
            ).ToList();

            var updatedContract = contract with { Signers = updatedSigners };
            _contracts[contractId] = updatedContract;
            return Task.FromResult<MultiPartyContractDto?>(updatedContract);
        }
        return Task.FromResult<MultiPartyContractDto?>(null);
    }
}
