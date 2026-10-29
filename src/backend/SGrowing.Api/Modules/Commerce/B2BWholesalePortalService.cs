using System.Collections.Concurrent;
using System.Text.Json.Serialization;
using SGrowing.Api.Data;

namespace SGrowing.Api.Modules.Commerce;

#region Enums & Roles

public static class CorporateRoles
{
    public const string ProcurementBuyer = "ProcurementBuyer";
    public const string DepartmentDirector = "DepartmentDirector";
    public const string CFO = "CFO";

    public static readonly string[] All = { ProcurementBuyer, DepartmentDirector, CFO };

    public static bool IsManager(string? role) =>
        role is DepartmentDirector or CFO;
}

public static class WholesaleOrderStatus
{
    public const string Draft = "Draft";
    public const string PendingCorporateApproval = "PendingCorporateApproval";
    public const string Confirmed = "Confirmed";
    public const string Fulfilling = "Fulfilling";
    public const string Delivered = "Delivered";
    public const string Rejected = "Rejected";
    public const string Cancelled = "Cancelled";
}

public static class CreditLedgerEntryType
{
    public const string HoldPlaced = "HoldPlaced";
    public const string HoldReleased = "HoldReleased";
    public const string InvoiceBilled = "InvoiceBilled";
    public const string InvoicePaid = "InvoicePaid";
    public const string CreditLimitAdjustment = "CreditLimitAdjustment";
}

#endregion

#region DTOs & Contracts

public record CorporateAccountDto(
    Guid Id,
    Guid TenantId,
    string CompanyName,
    string TaxId,
    decimal CreditLimit,
    int PaymentTermsDays,
    decimal RequiresApprovalAboveAmount,
    string Currency,
    bool IsActive,
    DateTime CreatedAtUtc,
    List<CorporateBranchDto> Branches,
    List<CorporateOfficerDto> Officers);

public record CorporateBranchDto(
    Guid Id,
    Guid CorporateAccountId,
    string BranchName,
    string BranchCode,
    string ShippingAddress,
    string City,
    string PostalCode,
    string Country,
    string ContactPerson,
    string ContactPhone,
    bool IsActive);

public record CorporateOfficerDto(
    Guid Id,
    Guid CorporateAccountId,
    string Name,
    string Email,
    string Role,
    decimal IndividualSpendingLimit,
    bool IsActive);

public record CreditStatusDto(
    Guid CorporateAccountId,
    string CompanyName,
    decimal CreditLimit,
    decimal UnpaidInvoicesTotal,
    decimal PendingOrderHoldsTotal,
    decimal AvailableCredit,
    decimal CreditUtilizationPercent,
    int PaymentTermsDays,
    string PaymentTermsLabel,
    int UnpaidInvoicesCount,
    int PendingHoldsCount,
    bool IsCreditExhausted,
    DateTime EvaluatedAtUtc);

public record CreditLedgerEntryDto(
    Guid Id,
    Guid CorporateAccountId,
    string EntryType,
    decimal Amount,
    string ReferenceNumber,
    string ReferenceType,
    string Notes,
    DateTime CreatedAtUtc);

public record CorporateInvoiceDto(
    Guid Id,
    Guid CorporateAccountId,
    string InvoiceNumber,
    Guid? PurchaseOrderId,
    string? ExternalPoNumber,
    decimal TotalAmount,
    decimal AmountPaid,
    decimal BalanceDue,
    string Status,
    DateTime IssuedAtUtc,
    DateTime DueDateUtc,
    DateTime? PaidAtUtc);

public record BulkPricingTierDto(
    int MinQuantity,
    int? MaxQuantity,
    decimal DiscountPercent,
    string TierLabel);

public record MatrixCellInputDto(
    string Size,
    string Color,
    int Quantity);

public record MatrixCalculatedItemDto(
    string Sku,
    string Size,
    string Color,
    int Quantity,
    decimal BaseUnitPrice,
    decimal UnitPrice,
    decimal LineTotal);

public record BulkMatrixCalculationRequest(
    string ProductStyle,
    string BaseSku,
    decimal BaseUnitPrice,
    List<string> Colors,
    List<string> Sizes,
    List<MatrixCellInputDto> MatrixQuantities,
    decimal TaxRatePercent = 19.0m,
    decimal ShippingFee = 45.0m);

public record BulkMatrixCalculationResultDto(
    string ProductStyle,
    string BaseSku,
    int TotalUnits,
    decimal BaseUnitPrice,
    decimal DiscountedUnitPrice,
    decimal VolumeDiscountPercent,
    string AppliedTierLabel,
    decimal SubtotalBeforeDiscount,
    decimal DiscountAmount,
    decimal Subtotal,
    decimal TaxRatePercent,
    decimal TaxAmount,
    decimal ShippingFee,
    decimal GrandTotal,
    List<MatrixCalculatedItemDto> MatrixItems,
    List<BulkPricingTierDto> AvailableTiers);

public record SubmitBulkMatrixOrderRequest(
    Guid CorporateAccountId,
    Guid BranchId,
    Guid OfficerId,
    string ExternalPoNumber,
    string ProductStyle,
    string BaseSku,
    decimal BaseUnitPrice,
    List<string> Colors,
    List<string> Sizes,
    List<MatrixCellInputDto> MatrixQuantities,
    decimal TaxRatePercent = 19.0m,
    decimal ShippingFee = 45.0m,
    string? ShippingNotes = null,
    bool AllowCreditOverride = false);

public record WholesaleOrderDto(
    Guid Id,
    Guid TenantId,
    Guid CorporateAccountId,
    string CompanyName,
    Guid BranchId,
    string BranchName,
    Guid SubmittedByOfficerId,
    string SubmitterName,
    string SubmitterRole,
    string OrderNumber,
    string ExternalPoNumber,
    string Status,
    int TotalUnits,
    decimal Subtotal,
    decimal VolumeDiscountPercent,
    decimal DiscountAmount,
    decimal TaxAmount,
    decimal ShippingFee,
    decimal GrandTotal,
    decimal CreditHoldAmount,
    bool RequiresApproval,
    string? ApprovalReason,
    Guid? ApprovedByOfficerId,
    string? ApproverName,
    DateTime? ApprovedAtUtc,
    string? RejectionReason,
    bool WarehouseStockReserved,
    string? ShippingNotes,
    List<MatrixCalculatedItemDto> Items,
    DateTime CreatedAtUtc,
    DateTime UpdatedAtUtc);

public record ApproveOrderRequest(
    Guid ApproverOfficerId,
    string? ApprovalNotes = null);

public record RejectOrderRequest(
    Guid ApproverOfficerId,
    string Reason);

public record CreateCorporateAccountRequest(
    string CompanyName,
    string TaxId,
    decimal CreditLimit,
    int PaymentTermsDays = 30,
    decimal RequiresApprovalAboveAmount = 5000m,
    string Currency = "USD",
    List<CreateBranchRequest>? Branches = null,
    List<CreateOfficerRequest>? Officers = null);

public record CreateBranchRequest(
    string BranchName,
    string BranchCode,
    string ShippingAddress,
    string City,
    string PostalCode,
    string Country,
    string ContactPerson,
    string ContactPhone);

public record CreateOfficerRequest(
    string Name,
    string Email,
    string Role,
    decimal IndividualSpendingLimit);

public record CreateInvoiceRequest(
    Guid? PurchaseOrderId,
    string? ExternalPoNumber,
    decimal Amount,
    int DueInDays = 30);

public record ReorderRequest(
    Guid OfficerId,
    Guid BranchId,
    string? NewExternalPoNumber = null);

#endregion

public interface IB2BWholesalePortalService
{
    // 1. Multi-Tier Account Hierarchy
    Task<List<CorporateAccountDto>> GetAccountsAsync(Guid tenantId);
    Task<CorporateAccountDto?> GetAccountByIdAsync(Guid tenantId, Guid accountId);
    Task<CorporateAccountDto> CreateAccountAsync(Guid tenantId, CreateCorporateAccountRequest request);
    Task<CorporateBranchDto> AddBranchAsync(Guid tenantId, Guid accountId, CreateBranchRequest request);
    Task<CorporateOfficerDto> AddOfficerAsync(Guid tenantId, Guid accountId, CreateOfficerRequest request);

    // 2. Revolving Credit Limit Ledger
    Task<CreditStatusDto> GetCreditStatusAsync(Guid tenantId, Guid accountId);
    Task<List<CreditLedgerEntryDto>> GetCreditLedgerEntriesAsync(Guid tenantId, Guid accountId);
    Task<CorporateInvoiceDto> CreateInvoiceAsync(Guid tenantId, Guid accountId, CreateInvoiceRequest request);
    Task<CorporateInvoiceDto> RecordInvoicePaymentAsync(Guid tenantId, Guid accountId, Guid invoiceId, decimal amountPaid, string? reference = null);

    // 3. Bulk Matrix SKU Calculations
    Task<BulkMatrixCalculationResultDto> CalculateBulkMatrixOrderAsync(Guid tenantId, BulkMatrixCalculationRequest request);

    // 4. Wholesale Purchase Orders & Approval Chains
    Task<WholesaleOrderDto> SubmitBulkMatrixOrderAsync(Guid tenantId, SubmitBulkMatrixOrderRequest request);
    Task<List<WholesaleOrderDto>> GetOrdersAsync(Guid tenantId, Guid? accountId = null, Guid? branchId = null, string? status = null);
    Task<WholesaleOrderDto?> GetOrderByIdAsync(Guid tenantId, Guid orderId);
    Task<WholesaleOrderDto> ApproveOrderAsync(Guid tenantId, Guid orderId, ApproveOrderRequest request);
    Task<WholesaleOrderDto> RejectOrderAsync(Guid tenantId, Guid orderId, RejectOrderRequest request);
    Task<List<WholesaleOrderDto>> GetPendingApprovalsAsync(Guid tenantId, Guid? accountId = null, Guid? officerId = null);
    Task<WholesaleOrderDto> ReorderPreviousOrderAsync(Guid tenantId, Guid orderId, ReorderRequest request);
}

public class B2BWholesalePortalService : IB2BWholesalePortalService
{
    private readonly SGrowingDbContext _db;

    // In-memory thread-safe state store partitioned by Tenant
    private static readonly ConcurrentDictionary<Guid, ConcurrentDictionary<Guid, CorporateAccountRecord>> s_accounts = new();
    private static readonly ConcurrentDictionary<Guid, ConcurrentDictionary<Guid, WholesaleOrderRecord>> s_orders = new();
    private static readonly ConcurrentDictionary<Guid, List<CreditLedgerEntryRecord>> s_ledgerEntries = new();
    private static readonly ConcurrentDictionary<Guid, List<CorporateInvoiceRecord>> s_invoices = new();
    private static readonly object s_initLock = new();
    private static bool s_seeded = false;

    // Standard Wholesale Pricing Tiers
    public static readonly List<BulkPricingTierDto> DefaultPricingTiers = new()
    {
        new(1, 49, 0.0m, "Tier 1: Standard Bulk (1-49 units)"),
        new(50, 199, 10.0m, "Tier 2: Wholesale Silver (50-199 units, 10% OFF)"),
        new(200, 499, 18.0m, "Tier 3: Wholesale Gold (200-499 units, 18% OFF)"),
        new(500, 999, 25.0m, "Tier 4: Enterprise Volume (500-999 units, 25% OFF)"),
        new(1000, null, 32.0m, "Tier 5: Master Distributor (1,000+ units, 32% OFF)")
    };

    public B2BWholesalePortalService(SGrowingDbContext db)
    {
        _db = db;
        EnsureSeeded();
    }

    private void EnsureSeeded()
    {
        if (s_seeded) return;
        lock (s_initLock)
        {
            if (s_seeded) return;

            var defaultTenantId = Guid.Parse("11111111-1111-1111-1111-111111111111");

            // Seed Account 1: Acme Industrial Logistics Corp
            var acmeId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
            var acmeBranch1 = new CorporateBranchRecord(
                Guid.Parse("bbbbbbbb-1111-1111-1111-111111111111"),
                acmeId,
                "Paris Hub & Fulfillment Depot",
                "DEPOT-PAR-01",
                "45 Rue de la Logistique",
                "Paris",
                "75012",
                "France",
                "Jean Dupont",
                "+33 1 42 68 00 11",
                true);

            var acmeBranch2 = new CorporateBranchRecord(
                Guid.Parse("bbbbbbbb-2222-2222-2222-222222222222"),
                acmeId,
                "Lyon Distribution Center",
                "HUB-LYN-02",
                "12 Avenue des Entrepôts",
                "Lyon",
                "69007",
                "France",
                "Claire Moreau",
                "+33 4 78 50 12 34",
                true);

            var acmeBranch3 = new CorporateBranchRecord(
                Guid.Parse("bbbbbbbb-3333-3333-3333-333333333333"),
                acmeId,
                "Marseille Port Terminal",
                "PORT-MRS-03",
                "8 Quai Maritime",
                "Marseille",
                "13002",
                "France",
                "Henri Blanc",
                "+33 4 91 14 25 80",
                true);

            var acmeBuyer = new CorporateOfficerRecord(
                Guid.Parse("cccccccc-1111-1111-1111-111111111111"),
                acmeId,
                "Sarah Jenkins",
                "s.jenkins@acme-industrial.com",
                CorporateRoles.ProcurementBuyer,
                5000.00m,
                true);

            var acmeDirector = new CorporateOfficerRecord(
                Guid.Parse("cccccccc-2222-2222-2222-222222222222"),
                acmeId,
                "Marcus Vance",
                "m.vance@acme-industrial.com",
                CorporateRoles.DepartmentDirector,
                25000.00m,
                true);

            var acmeCfo = new CorporateOfficerRecord(
                Guid.Parse("cccccccc-3333-3333-3333-333333333333"),
                acmeId,
                "Elena Rostova",
                "e.rostova@acme-industrial.com",
                CorporateRoles.CFO,
                150000.00m,
                true);

            var acmeAccount = new CorporateAccountRecord(
                acmeId,
                defaultTenantId,
                "Acme Industrial Logistics Corp",
                "FR-889102934",
                100000.00m, // $100,000 Credit limit
                30,         // Net 30
                7500.00m,   // Approval above $7,500
                "USD",
                true,
                DateTime.UtcNow.AddMonths(-6),
                new List<CorporateBranchRecord> { acmeBranch1, acmeBranch2, acmeBranch3 },
                new List<CorporateOfficerRecord> { acmeBuyer, acmeDirector, acmeCfo });

            var tenantAccounts = s_accounts.GetOrAdd(defaultTenantId, _ => new());
            tenantAccounts[acmeId] = acmeAccount;

            // Seed Account 2: Global Apex Retailers Group
            var apexId = Guid.Parse("aaaaaaaa-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
            var apexBranch1 = new CorporateBranchRecord(
                Guid.NewGuid(),
                apexId,
                "Casablanca Gateway Logistics",
                "CASA-MAIN-01",
                "Boulevard Abdelmoumen",
                "Casablanca",
                "20100",
                "Morocco",
                "Yassine Bennani",
                "+212 522 99 88 77",
                true);

            var apexBranch2 = new CorporateBranchRecord(
                Guid.NewGuid(),
                apexId,
                "Tunis Carthage Hub",
                "TUN-CARTH-01",
                "Zone Industrielle Charguia II",
                "Tunis",
                "2035",
                "Tunisia",
                "Mehdi Zribi",
                "+216 71 80 90 00",
                true);

            var apexBuyer = new CorporateOfficerRecord(
                Guid.NewGuid(),
                apexId,
                "Amine Khelil",
                "a.khelil@globalapex.com",
                CorporateRoles.ProcurementBuyer,
                10000.00m,
                true);

            var apexDirector = new CorporateOfficerRecord(
                Guid.NewGuid(),
                apexId,
                "Nadia Bouzid",
                "n.bouzid@globalapex.com",
                CorporateRoles.DepartmentDirector,
                50000.00m,
                true);

            var apexAccount = new CorporateAccountRecord(
                apexId,
                defaultTenantId,
                "Global Apex Retailers Group",
                "TN-9988776B",
                250000.00m, // $250,000 Credit limit
                60,         // Net 60
                15000.00m,  // Approval above $15,000
                "USD",
                true,
                DateTime.UtcNow.AddMonths(-3),
                new List<CorporateBranchRecord> { apexBranch1, apexBranch2 },
                new List<CorporateOfficerRecord> { apexBuyer, apexDirector });

            tenantAccounts[apexId] = apexAccount;

            // Seed initial Invoices for Acme
            var invoiceList = s_invoices.GetOrAdd(acmeId, _ => new());
            invoiceList.Add(new CorporateInvoiceRecord(
                Guid.NewGuid(),
                acmeId,
                "INV-2026-00412",
                null,
                "PO-ACME-8921",
                21400.00m,
                0m,
                21400.00m,
                "Unpaid",
                DateTime.UtcNow.AddDays(-14),
                DateTime.UtcNow.AddDays(16),
                null));

            // Seed initial Credit Ledger for Acme
            var ledgerList = s_ledgerEntries.GetOrAdd(acmeId, _ => new());
            ledgerList.Add(new CreditLedgerEntryRecord(
                Guid.NewGuid(),
                acmeId,
                CreditLedgerEntryType.CreditLimitAdjustment,
                100000.00m,
                "FACILITY-INITIAL",
                "CreditFacility",
                "Initial revolving credit facility activated with Net-30 payment terms",
                DateTime.UtcNow.AddMonths(-6)));

            ledgerList.Add(new CreditLedgerEntryRecord(
                Guid.NewGuid(),
                acmeId,
                CreditLedgerEntryType.InvoiceBilled,
                21400.00m,
                "INV-2026-00412",
                "Invoice",
                "Billed for previous shipment PO-ACME-8921",
                DateTime.UtcNow.AddDays(-14)));

            // Seed Orders for Acme
            var tenantOrders = s_orders.GetOrAdd(defaultTenantId, _ => new());

            // Order 1: Confirmed previous order (Fulfilling)
            var order1Id = Guid.NewGuid();
            var order1 = new WholesaleOrderRecord(
                order1Id,
                defaultTenantId,
                acmeId,
                "Acme Industrial Logistics Corp",
                acmeBranch1.Id,
                acmeBranch1.BranchName,
                acmeBuyer.Id,
                acmeBuyer.Name,
                acmeBuyer.Role,
                "WPO-2026-0104",
                "PO-ACME-9011",
                WholesaleOrderStatus.Fulfilling,
                240,
                5510.40m,
                18.0m,
                1209.60m,
                1046.98m,
                0m,
                6557.38m,
                6557.38m,
                false,
                null,
                acmeBuyer.Id,
                acmeBuyer.Name,
                DateTime.UtcNow.AddDays(-3),
                null,
                true,
                "Gate 3 Delivery, forklift required.",
                new List<MatrixCalculatedItemDto>
                {
                    new("WWR-POLO-NVY-M", "M", "Navy", 80, 28.0m, 22.96m, 1836.80m),
                    new("WWR-POLO-NVY-L", "L", "Navy", 80, 28.0m, 22.96m, 1836.80m),
                    new("WWR-POLO-BLK-XL", "XL", "Black", 80, 28.0m, 22.96m, 1836.80m)
                },
                DateTime.UtcNow.AddDays(-3),
                DateTime.UtcNow.AddDays(-2));

            tenantOrders[order1Id] = order1;
            ledgerList.Add(new CreditLedgerEntryRecord(
                Guid.NewGuid(),
                acmeId,
                CreditLedgerEntryType.HoldPlaced,
                6557.38m,
                "WPO-2026-0104",
                "PurchaseOrder",
                "Revolving credit hold placed on confirmed wholesale order WPO-2026-0104",
                DateTime.UtcNow.AddDays(-3)));

            // Order 2: Pending Corporate Approval (exceeds Buyer limit of $5,000)
            var order2Id = Guid.NewGuid();
            var order2 = new WholesaleOrderRecord(
                order2Id,
                defaultTenantId,
                acmeId,
                "Acme Industrial Logistics Corp",
                acmeBranch2.Id,
                acmeBranch2.BranchName,
                acmeBuyer.Id,
                acmeBuyer.Name,
                acmeBuyer.Role,
                "WPO-2026-0105",
                "PO-ACME-9045-PEND",
                WholesaleOrderStatus.PendingCorporateApproval,
                550,
                11550.00m,
                25.0m,
                3850.00m,
                2194.50m,
                0m,
                13744.50m,
                13744.50m,
                true,
                "Order total ($13,744.50) exceeds officer limit ($5,000.00) and account threshold ($7,500.00). Requires Department Director or CFO approval.",
                null,
                null,
                null,
                null,
                false, // Stock not yet reserved!
                "Urgent delivery for Lyon expansion depot.",
                new List<MatrixCalculatedItemDto>
                {
                    new("WWR-POLO-NVY-S", "S", "Navy", 50, 28.0m, 21.0m, 1050.00m),
                    new("WWR-POLO-NVY-M", "M", "Navy", 100, 28.0m, 21.0m, 2100.00m),
                    new("WWR-POLO-NVY-L", "L", "Navy", 150, 28.0m, 21.0m, 3150.00m),
                    new("WWR-POLO-CHA-M", "M", "Charcoal", 100, 28.0m, 21.0m, 2100.00m),
                    new("WWR-POLO-CHA-L", "L", "Charcoal", 150, 28.0m, 21.0m, 3150.00m)
                },
                DateTime.UtcNow.AddHours(-5),
                DateTime.UtcNow.AddHours(-5));

            tenantOrders[order2Id] = order2;
            ledgerList.Add(new CreditLedgerEntryRecord(
                Guid.NewGuid(),
                acmeId,
                CreditLedgerEntryType.HoldPlaced,
                13744.50m,
                "WPO-2026-0105",
                "PurchaseOrder",
                "Pre-approval revolving credit hold placed on order WPO-2026-0105",
                DateTime.UtcNow.AddHours(-5)));

            s_seeded = true;
        }
    }

    #region 1. Multi-Tier Account Hierarchy

    public Task<List<CorporateAccountDto>> GetAccountsAsync(Guid tenantId)
    {
        var accounts = s_accounts.GetOrAdd(tenantId, _ => new());
        var list = accounts.Values
            .Select(MapToDto)
            .OrderBy(a => a.CompanyName)
            .ToList();
        return Task.FromResult(list);
    }

    public Task<CorporateAccountDto?> GetAccountByIdAsync(Guid tenantId, Guid accountId)
    {
        var accounts = s_accounts.GetOrAdd(tenantId, _ => new());
        if (accounts.TryGetValue(accountId, out var acc))
        {
            return Task.FromResult<CorporateAccountDto?>(MapToDto(acc));
        }
        return Task.FromResult<CorporateAccountDto?>(null);
    }

    public Task<CorporateAccountDto> CreateAccountAsync(Guid tenantId, CreateCorporateAccountRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.CompanyName))
            throw new ArgumentException("Company name is required.", nameof(request.CompanyName));

        if (string.IsNullOrWhiteSpace(request.TaxId))
            throw new ArgumentException("Tax ID / VAT number is required.", nameof(request.TaxId));

        if (request.CreditLimit <= 0)
            throw new ArgumentException("Credit limit must be greater than zero.", nameof(request.CreditLimit));

        var accountId = Guid.NewGuid();
        var branches = (request.Branches ?? new List<CreateBranchRequest>())
            .Select(b => new CorporateBranchRecord(
                Guid.NewGuid(),
                accountId,
                b.BranchName,
                string.IsNullOrWhiteSpace(b.BranchCode) ? $"BR-{Guid.NewGuid().ToString("N")[..6].ToUpper()}" : b.BranchCode,
                b.ShippingAddress,
                b.City,
                b.PostalCode,
                b.Country,
                b.ContactPerson,
                b.ContactPhone,
                true))
            .ToList();

        var officers = (request.Officers ?? new List<CreateOfficerRequest>())
            .Select(o => new CorporateOfficerRecord(
                Guid.NewGuid(),
                accountId,
                o.Name,
                o.Email,
                string.IsNullOrWhiteSpace(o.Role) ? CorporateRoles.ProcurementBuyer : o.Role,
                o.IndividualSpendingLimit > 0 ? o.IndividualSpendingLimit : 5000m,
                true))
            .ToList();

        var record = new CorporateAccountRecord(
            accountId,
            tenantId,
            request.CompanyName.Trim(),
            request.TaxId.Trim(),
            request.CreditLimit,
            request.PaymentTermsDays <= 0 ? 30 : request.PaymentTermsDays,
            request.RequiresApprovalAboveAmount > 0 ? request.RequiresApprovalAboveAmount : 5000m,
            string.IsNullOrWhiteSpace(request.Currency) ? "USD" : request.Currency.ToUpper(),
            true,
            DateTime.UtcNow,
            branches,
            officers);

        var accounts = s_accounts.GetOrAdd(tenantId, _ => new());
        accounts[accountId] = record;

        // Initialize ledger
        var ledger = s_ledgerEntries.GetOrAdd(accountId, _ => new());
        lock (ledger)
        {
            ledger.Add(new CreditLedgerEntryRecord(
                Guid.NewGuid(),
                accountId,
                CreditLedgerEntryType.CreditLimitAdjustment,
                request.CreditLimit,
                "INITIAL-LIMIT",
                "FacilitySetup",
                $"Initial revolving credit facility activated with Net-{record.PaymentTermsDays} payment terms",
                DateTime.UtcNow));
        }

        return Task.FromResult(MapToDto(record));
    }

    public Task<CorporateBranchDto> AddBranchAsync(Guid tenantId, Guid accountId, CreateBranchRequest request)
    {
        var accounts = s_accounts.GetOrAdd(tenantId, _ => new());
        if (!accounts.TryGetValue(accountId, out var acc))
            throw new KeyNotFoundException($"Corporate account '{accountId}' not found.");

        var branch = new CorporateBranchRecord(
            Guid.NewGuid(),
            accountId,
            request.BranchName.Trim(),
            string.IsNullOrWhiteSpace(request.BranchCode) ? $"BR-{Guid.NewGuid().ToString("N")[..6].ToUpper()}" : request.BranchCode.Trim(),
            request.ShippingAddress,
            request.City,
            request.PostalCode,
            request.Country,
            request.ContactPerson,
            request.ContactPhone,
            true);

        lock (acc.Branches)
        {
            acc.Branches.Add(branch);
        }

        return Task.FromResult(MapBranchToDto(branch));
    }

    public Task<CorporateOfficerDto> AddOfficerAsync(Guid tenantId, Guid accountId, CreateOfficerRequest request)
    {
        var accounts = s_accounts.GetOrAdd(tenantId, _ => new());
        if (!accounts.TryGetValue(accountId, out var acc))
            throw new KeyNotFoundException($"Corporate account '{accountId}' not found.");

        var officer = new CorporateOfficerRecord(
            Guid.NewGuid(),
            accountId,
            request.Name.Trim(),
            request.Email.Trim().ToLowerInvariant(),
            string.IsNullOrWhiteSpace(request.Role) ? CorporateRoles.ProcurementBuyer : request.Role,
            request.IndividualSpendingLimit,
            true);

        lock (acc.Officers)
        {
            acc.Officers.Add(officer);
        }

        return Task.FromResult(MapOfficerToDto(officer));
    }

    #endregion

    #region 2. Revolving Credit Limit Ledger

    public Task<CreditStatusDto> GetCreditStatusAsync(Guid tenantId, Guid accountId)
    {
        var accounts = s_accounts.GetOrAdd(tenantId, _ => new());
        if (!accounts.TryGetValue(accountId, out var acc))
            throw new KeyNotFoundException($"Corporate account '{accountId}' not found.");

        // Formula: AvailableCredit = CreditLimit - UnpaidInvoicesTotal - PendingOrderHoldsTotal
        var invoices = s_invoices.GetOrAdd(accountId, _ => new());
        decimal unpaidInvoicesTotal;
        int unpaidInvoicesCount;
        lock (invoices)
        {
            var activeInvoices = invoices.Where(i => i.Status != "Paid").ToList();
            unpaidInvoicesTotal = activeInvoices.Sum(i => i.BalanceDue);
            unpaidInvoicesCount = activeInvoices.Count;
        }

        var orders = s_orders.GetOrAdd(tenantId, _ => new());
        var activeOrdersWithHold = orders.Values
            .Where(o => o.CorporateAccountId == accountId &&
                        (o.Status == WholesaleOrderStatus.PendingCorporateApproval ||
                         o.Status == WholesaleOrderStatus.Confirmed ||
                         o.Status == WholesaleOrderStatus.Fulfilling))
            .ToList();

        decimal pendingHoldsTotal = activeOrdersWithHold.Sum(o => o.CreditHoldAmount);
        int pendingHoldsCount = activeOrdersWithHold.Count;

        decimal totalCommitted = unpaidInvoicesTotal + pendingHoldsTotal;
        decimal availableCredit = Math.Max(0m, acc.CreditLimit - totalCommitted);
        decimal utilizationPercent = acc.CreditLimit > 0
            ? Math.Min(100.0m, Math.Round((totalCommitted / acc.CreditLimit) * 100.0m, 2))
            : 0m;

        var status = new CreditStatusDto(
            CorporateAccountId: acc.Id,
            CompanyName: acc.CompanyName,
            CreditLimit: acc.CreditLimit,
            UnpaidInvoicesTotal: Math.Round(unpaidInvoicesTotal, 2),
            PendingOrderHoldsTotal: Math.Round(pendingHoldsTotal, 2),
            AvailableCredit: Math.Round(availableCredit, 2),
            CreditUtilizationPercent: utilizationPercent,
            PaymentTermsDays: acc.PaymentTermsDays,
            PaymentTermsLabel: $"Net {acc.PaymentTermsDays}",
            UnpaidInvoicesCount: unpaidInvoicesCount,
            PendingHoldsCount: pendingHoldsCount,
            IsCreditExhausted: availableCredit <= 0.01m,
            EvaluatedAtUtc: DateTime.UtcNow);

        return Task.FromResult(status);
    }

    public Task<List<CreditLedgerEntryDto>> GetCreditLedgerEntriesAsync(Guid tenantId, Guid accountId)
    {
        var ledger = s_ledgerEntries.GetOrAdd(accountId, _ => new());
        lock (ledger)
        {
            var list = ledger
                .OrderByDescending(e => e.CreatedAtUtc)
                .Select(e => new CreditLedgerEntryDto(
                    e.Id,
                    e.CorporateAccountId,
                    e.EntryType,
                    e.Amount,
                    e.ReferenceNumber,
                    e.ReferenceType,
                    e.Notes,
                    e.CreatedAtUtc))
                .ToList();
            return Task.FromResult(list);
        }
    }

    public Task<CorporateInvoiceDto> CreateInvoiceAsync(Guid tenantId, Guid accountId, CreateInvoiceRequest request)
    {
        var accounts = s_accounts.GetOrAdd(tenantId, _ => new());
        if (!accounts.TryGetValue(accountId, out var acc))
            throw new KeyNotFoundException($"Corporate account '{accountId}' not found.");

        if (request.Amount <= 0)
            throw new ArgumentException("Invoice amount must be greater than zero.", nameof(request.Amount));

        var invoiceId = Guid.NewGuid();
        var invoiceNum = $"INV-{DateTime.UtcNow:yyyyMM}-{Guid.NewGuid().ToString("N")[..5].ToUpper()}";
        var dueDays = request.DueInDays > 0 ? request.DueInDays : acc.PaymentTermsDays;

        var invoice = new CorporateInvoiceRecord(
            invoiceId,
            accountId,
            invoiceNum,
            request.PurchaseOrderId,
            request.ExternalPoNumber,
            request.Amount,
            0m,
            request.Amount,
            "Unpaid",
            DateTime.UtcNow,
            DateTime.UtcNow.AddDays(dueDays),
            null);

        var invoices = s_invoices.GetOrAdd(accountId, _ => new());
        lock (invoices)
        {
            invoices.Add(invoice);
        }

        // If this invoice corresponds to an active purchase order, release the temporary order hold
        // so it becomes an invoice debt without double-counting!
        if (request.PurchaseOrderId.HasValue)
        {
            var orders = s_orders.GetOrAdd(tenantId, _ => new());
            if (orders.TryGetValue(request.PurchaseOrderId.Value, out var order))
            {
                order.CreditHoldAmount = 0m; // Converted from order hold to invoice debt
            }
        }

        var ledger = s_ledgerEntries.GetOrAdd(accountId, _ => new());
        lock (ledger)
        {
            ledger.Add(new CreditLedgerEntryRecord(
                Guid.NewGuid(),
                accountId,
                CreditLedgerEntryType.InvoiceBilled,
                request.Amount,
                invoiceNum,
                "Invoice",
                $"Billed invoice {invoiceNum} for Net-{dueDays} days payment terms",
                DateTime.UtcNow));
        }

        return Task.FromResult(MapInvoiceToDto(invoice));
    }

    public Task<CorporateInvoiceDto> RecordInvoicePaymentAsync(
        Guid tenantId,
        Guid accountId,
        Guid invoiceId,
        decimal amountPaid,
        string? reference = null)
    {
        var accounts = s_accounts.GetOrAdd(tenantId, _ => new());
        if (!accounts.TryGetValue(accountId, out _))
            throw new KeyNotFoundException($"Corporate account '{accountId}' not found.");

        if (amountPaid <= 0)
            throw new ArgumentException("Payment amount must be greater than zero.", nameof(amountPaid));

        var invoices = s_invoices.GetOrAdd(accountId, _ => new());
        CorporateInvoiceRecord? invoice;
        lock (invoices)
        {
            invoice = invoices.FirstOrDefault(i => i.Id == invoiceId);
            if (invoice == null)
                throw new KeyNotFoundException($"Invoice '{invoiceId}' not found for corporate account '{accountId}'.");

            invoice.AmountPaid += amountPaid;
            invoice.BalanceDue = Math.Max(0m, invoice.TotalAmount - invoice.AmountPaid);

            if (invoice.BalanceDue <= 0.001m)
            {
                invoice.Status = "Paid";
                invoice.PaidAtUtc = DateTime.UtcNow;
            }
            else
            {
                invoice.Status = "PartiallyPaid";
            }
        }

        // Record credit replenishment in ledger
        var ledger = s_ledgerEntries.GetOrAdd(accountId, _ => new());
        lock (ledger)
        {
            ledger.Add(new CreditLedgerEntryRecord(
                Guid.NewGuid(),
                accountId,
                CreditLedgerEntryType.InvoicePaid,
                amountPaid,
                invoice.InvoiceNumber,
                "Payment",
                $"Payment of {amountPaid:C} received for invoice {invoice.InvoiceNumber}. Available revolving credit replenished.",
                DateTime.UtcNow));
        }

        return Task.FromResult(MapInvoiceToDto(invoice));
    }

    #endregion

    #region 3. Bulk Matrix SKU Calculations

    public Task<BulkMatrixCalculationResultDto> CalculateBulkMatrixOrderAsync(Guid tenantId, BulkMatrixCalculationRequest request)
    {
        if (request.BaseUnitPrice <= 0)
            throw new ArgumentException("Base unit price must be greater than zero.", nameof(request.BaseUnitPrice));

        var colors = request.Colors ?? new List<string>();
        var sizes = request.Sizes ?? new List<string>();
        var matrixInputs = request.MatrixQuantities ?? new List<MatrixCellInputDto>();

        // Build items list
        var calculatedItems = new List<MatrixCalculatedItemDto>();
        int totalUnits = 0;

        foreach (var input in matrixInputs)
        {
            if (input.Quantity <= 0) continue;
            totalUnits += input.Quantity;
        }

        // Determine applicable volume tier
        var activeTier = DefaultPricingTiers
            .FirstOrDefault(t => totalUnits >= t.MinQuantity && (!t.MaxQuantity.HasValue || totalUnits <= t.MaxQuantity.Value))
            ?? DefaultPricingTiers[0];

        decimal discountPercent = activeTier.DiscountPercent;
        decimal discountedUnitPrice = Math.Round(request.BaseUnitPrice * (1.0m - (discountPercent / 100.0m)), 2);

        foreach (var input in matrixInputs)
        {
            if (input.Quantity <= 0) continue;

            string colorCode = input.Color.Length >= 3 ? input.Color[..3].ToUpper() : input.Color.ToUpper();
            string sizeCode = input.Size.ToUpper();
            string sku = $"{request.BaseSku}-{colorCode}-{sizeCode}";

            decimal lineTotal = Math.Round(input.Quantity * discountedUnitPrice, 2);
            calculatedItems.Add(new MatrixCalculatedItemDto(
                Sku: sku,
                Size: input.Size,
                Color: input.Color,
                Quantity: input.Quantity,
                BaseUnitPrice: request.BaseUnitPrice,
                UnitPrice: discountedUnitPrice,
                LineTotal: lineTotal));
        }

        decimal subtotalBeforeDiscount = Math.Round(totalUnits * request.BaseUnitPrice, 2);
        decimal subtotal = Math.Round(totalUnits * discountedUnitPrice, 2);
        decimal discountAmount = Math.Round(subtotalBeforeDiscount - subtotal, 2);

        decimal taxRate = request.TaxRatePercent >= 0 ? request.TaxRatePercent : 19.0m;
        decimal taxAmount = Math.Round(subtotal * (taxRate / 100.0m), 2);
        decimal shippingFee = totalUnits >= 500 ? 0m : request.ShippingFee; // Free freight for 500+ bulk orders
        decimal grandTotal = subtotal + taxAmount + shippingFee;

        var result = new BulkMatrixCalculationResultDto(
            ProductStyle: request.ProductStyle,
            BaseSku: request.BaseSku,
            TotalUnits: totalUnits,
            BaseUnitPrice: request.BaseUnitPrice,
            DiscountedUnitPrice: discountedUnitPrice,
            VolumeDiscountPercent: discountPercent,
            AppliedTierLabel: activeTier.TierLabel,
            SubtotalBeforeDiscount: subtotalBeforeDiscount,
            DiscountAmount: discountAmount,
            Subtotal: subtotal,
            TaxRatePercent: taxRate,
            TaxAmount: taxAmount,
            ShippingFee: shippingFee,
            GrandTotal: grandTotal,
            MatrixItems: calculatedItems,
            AvailableTiers: DefaultPricingTiers);

        return Task.FromResult(result);
    }

    #endregion

    #region 4. Wholesale Purchase Orders & Approval Chains

    public async Task<WholesaleOrderDto> SubmitBulkMatrixOrderAsync(Guid tenantId, SubmitBulkMatrixOrderRequest request)
    {
        var accounts = s_accounts.GetOrAdd(tenantId, _ => new());
        if (!accounts.TryGetValue(request.CorporateAccountId, out var acc))
            throw new KeyNotFoundException($"Corporate account '{request.CorporateAccountId}' not found.");

        var branch = acc.Branches.FirstOrDefault(b => b.Id == request.BranchId);
        if (branch == null)
            throw new ArgumentException($"Branch '{request.BranchId}' does not belong to corporate account '{acc.CompanyName}'.");

        var officer = acc.Officers.FirstOrDefault(o => o.Id == request.OfficerId);
        if (officer == null)
            throw new ArgumentException($"Officer '{request.OfficerId}' is not registered under corporate account '{acc.CompanyName}'.");

        if (!officer.IsActive)
            throw new InvalidOperationException($"Corporate officer '{officer.Name}' is inactive.");

        // 1. Calculate order total & bulk tier pricing
        var calcReq = new BulkMatrixCalculationRequest(
            ProductStyle: request.ProductStyle,
            BaseSku: request.BaseSku,
            BaseUnitPrice: request.BaseUnitPrice,
            Colors: request.Colors,
            Sizes: request.Sizes,
            MatrixQuantities: request.MatrixQuantities,
            TaxRatePercent: request.TaxRatePercent,
            ShippingFee: request.ShippingFee);

        var calc = await CalculateBulkMatrixOrderAsync(tenantId, calcReq);
        if (calc.TotalUnits <= 0)
            throw new ArgumentException("Order matrix must contain at least one unit.");

        // 2. Revolving Credit Check:
        // AvailableCredit = CreditLimit - UnpaidInvoicesTotal - PendingOrderHoldsTotal
        var creditStatus = await GetCreditStatusAsync(tenantId, request.CorporateAccountId);

        if (calc.GrandTotal > creditStatus.AvailableCredit && !request.AllowCreditOverride)
        {
            throw new InvalidOperationException(
                $"Order total of {calc.GrandTotal:C} exceeds available credit limit of {creditStatus.AvailableCredit:C} " +
                $"(Limit: {creditStatus.CreditLimit:C}, Unpaid Invoices: {creditStatus.UnpaidInvoicesTotal:C}, Pending Holds: {creditStatus.PendingOrderHoldsTotal:C}). " +
                "Wholesale order submission blocked.");
        }

        // 3. Approval Routing:
        // If order total exceeds officer's IndividualSpendingLimit or account's RequiresApprovalAboveAmount
        bool exceedsOfficerLimit = calc.GrandTotal > officer.IndividualSpendingLimit;
        bool exceedsAccountThreshold = calc.GrandTotal > acc.RequiresApprovalAboveAmount;
        bool requiresApproval = exceedsOfficerLimit || exceedsAccountThreshold;

        string? approvalReason = null;
        string orderStatus;
        bool stockReserved;

        if (requiresApproval)
        {
            orderStatus = WholesaleOrderStatus.PendingCorporateApproval;
            stockReserved = false; // Warehouse stock reserved ONLY upon corporate approval

            if (exceedsOfficerLimit && exceedsAccountThreshold)
            {
                approvalReason = $"Order total of {calc.GrandTotal:C} exceeds officer limit ({officer.IndividualSpendingLimit:C}) " +
                                 $"and company threshold ({acc.RequiresApprovalAboveAmount:C}). Requires Department Director or CFO approval.";
            }
            else if (exceedsOfficerLimit)
            {
                approvalReason = $"Order total of {calc.GrandTotal:C} exceeds officer individual limit of {officer.IndividualSpendingLimit:C}.";
            }
            else
            {
                approvalReason = $"Order total of {calc.GrandTotal:C} exceeds company threshold of {acc.RequiresApprovalAboveAmount:C}.";
            }
        }
        else
        {
            orderStatus = WholesaleOrderStatus.Confirmed;
            stockReserved = true; // Auto-confirmed within limits -> reserve stock immediately!
        }

        var orderId = Guid.NewGuid();
        var orderNum = $"WPO-{DateTime.UtcNow:yyyyMM}-{Guid.NewGuid().ToString("N")[..5].ToUpper()}";
        var poNum = string.IsNullOrWhiteSpace(request.ExternalPoNumber)
            ? $"PO-{DateTime.UtcNow:yyyyMMdd}-{Guid.NewGuid().ToString("N")[..4].ToUpper()}"
            : request.ExternalPoNumber.Trim();

        var order = new WholesaleOrderRecord(
            Id: orderId,
            TenantId: tenantId,
            CorporateAccountId: acc.Id,
            CompanyName: acc.CompanyName,
            BranchId: branch.Id,
            BranchName: branch.BranchName,
            SubmittedByOfficerId: officer.Id,
            SubmitterName: officer.Name,
            SubmitterRole: officer.Role,
            OrderNumber: orderNum,
            ExternalPoNumber: poNum,
            Status: orderStatus,
            TotalUnits: calc.TotalUnits,
            Subtotal: calc.Subtotal,
            VolumeDiscountPercent: calc.VolumeDiscountPercent,
            DiscountAmount: calc.DiscountAmount,
            TaxAmount: calc.TaxAmount,
            ShippingFee: calc.ShippingFee,
            GrandTotal: calc.GrandTotal,
            CreditHoldAmount: calc.GrandTotal, // Place hold for the entire amount
            RequiresApproval: requiresApproval,
            ApprovalReason: approvalReason,
            ApprovedByOfficerId: requiresApproval ? null : officer.Id,
            ApproverName: requiresApproval ? null : officer.Name,
            ApprovedAtUtc: requiresApproval ? null : DateTime.UtcNow,
            RejectionReason: null,
            WarehouseStockReserved: stockReserved,
            ShippingNotes: request.ShippingNotes,
            Items: calc.MatrixItems,
            CreatedAtUtc: DateTime.UtcNow,
            UpdatedAtUtc: DateTime.UtcNow);

        var orders = s_orders.GetOrAdd(tenantId, _ => new());
        orders[orderId] = order;

        // Record credit hold in ledger
        var ledger = s_ledgerEntries.GetOrAdd(acc.Id, _ => new());
        lock (ledger)
        {
            ledger.Add(new CreditLedgerEntryRecord(
                Guid.NewGuid(),
                acc.Id,
                CreditLedgerEntryType.HoldPlaced,
                calc.GrandTotal,
                orderNum,
                "PurchaseOrder",
                $"Revolving credit hold placed on wholesale order {orderNum} ({orderStatus})",
                DateTime.UtcNow));
        }

        return MapOrderToDto(order);
    }

    public Task<List<WholesaleOrderDto>> GetOrdersAsync(
        Guid tenantId,
        Guid? accountId = null,
        Guid? branchId = null,
        string? status = null)
    {
        var orders = s_orders.GetOrAdd(tenantId, _ => new());
        var query = orders.Values.AsEnumerable();

        if (accountId.HasValue)
            query = query.Where(o => o.CorporateAccountId == accountId.Value);

        if (branchId.HasValue)
            query = query.Where(o => o.BranchId == branchId.Value);

        if (!string.IsNullOrWhiteSpace(status))
            query = query.Where(o => o.Status.Equals(status.Trim(), StringComparison.OrdinalIgnoreCase));

        var list = query
            .OrderByDescending(o => o.CreatedAtUtc)
            .Select(MapOrderToDto)
            .ToList();

        return Task.FromResult(list);
    }

    public Task<WholesaleOrderDto?> GetOrderByIdAsync(Guid tenantId, Guid orderId)
    {
        var orders = s_orders.GetOrAdd(tenantId, _ => new());
        if (orders.TryGetValue(orderId, out var o))
        {
            return Task.FromResult<WholesaleOrderDto?>(MapOrderToDto(o));
        }
        return Task.FromResult<WholesaleOrderDto?>(null);
    }

    public Task<WholesaleOrderDto> ApproveOrderAsync(Guid tenantId, Guid orderId, ApproveOrderRequest request)
    {
        var orders = s_orders.GetOrAdd(tenantId, _ => new());
        if (!orders.TryGetValue(orderId, out var order))
            throw new KeyNotFoundException($"Wholesale order '{orderId}' not found.");

        if (order.Status != WholesaleOrderStatus.PendingCorporateApproval)
        {
            throw new InvalidOperationException($"Order '{order.OrderNumber}' is in '{order.Status}' status and cannot be approved.");
        }

        var accounts = s_accounts.GetOrAdd(tenantId, _ => new());
        if (!accounts.TryGetValue(order.CorporateAccountId, out var acc))
            throw new KeyNotFoundException($"Corporate account for order '{orderId}' not found.");

        var approver = acc.Officers.FirstOrDefault(o => o.Id == request.ApproverOfficerId);
        if (approver == null)
            throw new ArgumentException($"Approving officer '{request.ApproverOfficerId}' is not an officer of '{acc.CompanyName}'.");

        if (!CorporateRoles.IsManager(approver.Role))
        {
            throw new UnauthorizedAccessException(
                $"Officer '{approver.Name}' has role '{approver.Role}' which lacks managerial approval authorization. Must be DepartmentDirector or CFO.");
        }

        // Advance to Confirmed and reserve warehouse stock
        order.Status = WholesaleOrderStatus.Confirmed;
        order.WarehouseStockReserved = true;
        order.ApprovedByOfficerId = approver.Id;
        order.ApproverName = approver.Name;
        order.ApprovedAtUtc = DateTime.UtcNow;
        order.RequiresApproval = false;
        order.UpdatedAtUtc = DateTime.UtcNow;

        return Task.FromResult(MapOrderToDto(order));
    }

    public Task<WholesaleOrderDto> RejectOrderAsync(Guid tenantId, Guid orderId, RejectOrderRequest request)
    {
        var orders = s_orders.GetOrAdd(tenantId, _ => new());
        if (!orders.TryGetValue(orderId, out var order))
            throw new KeyNotFoundException($"Wholesale order '{orderId}' not found.");

        if (order.Status != WholesaleOrderStatus.PendingCorporateApproval)
        {
            throw new InvalidOperationException($"Order '{order.OrderNumber}' is in '{order.Status}' status and cannot be rejected.");
        }

        var accounts = s_accounts.GetOrAdd(tenantId, _ => new());
        if (!accounts.TryGetValue(order.CorporateAccountId, out var acc))
            throw new KeyNotFoundException($"Corporate account for order '{orderId}' not found.");

        var approver = acc.Officers.FirstOrDefault(o => o.Id == request.ApproverOfficerId);
        if (approver == null)
            throw new ArgumentException($"Rejecting officer '{request.ApproverOfficerId}' is not an officer of '{acc.CompanyName}'.");

        if (!CorporateRoles.IsManager(approver.Role))
        {
            throw new UnauthorizedAccessException(
                $"Officer '{approver.Name}' has role '{approver.Role}' which lacks managerial authority.");
        }

        order.Status = WholesaleOrderStatus.Rejected;
        order.RejectionReason = string.IsNullOrWhiteSpace(request.Reason) ? "Rejected by corporate management." : request.Reason;
        order.WarehouseStockReserved = false;
        order.UpdatedAtUtc = DateTime.UtcNow;

        // Release the credit hold!
        decimal releasedAmount = order.CreditHoldAmount;
        order.CreditHoldAmount = 0m;

        var ledger = s_ledgerEntries.GetOrAdd(acc.Id, _ => new());
        lock (ledger)
        {
            ledger.Add(new CreditLedgerEntryRecord(
                Guid.NewGuid(),
                acc.Id,
                CreditLedgerEntryType.HoldReleased,
                releasedAmount,
                order.OrderNumber,
                "PurchaseOrder",
                $"Credit hold released for rejected order {order.OrderNumber}: {order.RejectionReason}",
                DateTime.UtcNow));
        }

        return Task.FromResult(MapOrderToDto(order));
    }

    public Task<List<WholesaleOrderDto>> GetPendingApprovalsAsync(Guid tenantId, Guid? accountId = null, Guid? officerId = null)
    {
        var orders = s_orders.GetOrAdd(tenantId, _ => new());
        var query = orders.Values
            .Where(o => o.Status == WholesaleOrderStatus.PendingCorporateApproval);

        if (accountId.HasValue)
            query = query.Where(o => o.CorporateAccountId == accountId.Value);

        var list = query
            .OrderByDescending(o => o.CreatedAtUtc)
            .Select(MapOrderToDto)
            .ToList();

        return Task.FromResult(list);
    }

    public async Task<WholesaleOrderDto> ReorderPreviousOrderAsync(Guid tenantId, Guid orderId, ReorderRequest request)
    {
        var orders = s_orders.GetOrAdd(tenantId, _ => new());
        if (!orders.TryGetValue(orderId, out var prevOrder))
            throw new KeyNotFoundException($"Previous wholesale order '{orderId}' not found.");

        var accounts = s_accounts.GetOrAdd(tenantId, _ => new());
        if (!accounts.TryGetValue(prevOrder.CorporateAccountId, out var acc))
            throw new KeyNotFoundException($"Corporate account for order '{orderId}' not found.");

        var branch = acc.Branches.FirstOrDefault(b => b.Id == request.BranchId)
                     ?? acc.Branches.FirstOrDefault(b => b.Id == prevOrder.BranchId)
                     ?? acc.Branches.First();

        var officer = acc.Officers.FirstOrDefault(o => o.Id == request.OfficerId)
                      ?? acc.Officers.FirstOrDefault(o => o.Id == prevOrder.SubmittedByOfficerId)
                      ?? acc.Officers.First();

        var matrixQuantities = prevOrder.Items
            .Select(i => new MatrixCellInputDto(i.Size, i.Color, i.Quantity))
            .ToList();

        var colors = prevOrder.Items.Select(i => i.Color).Distinct().ToList();
        var sizes = prevOrder.Items.Select(i => i.Size).Distinct().ToList();

        var submitReq = new SubmitBulkMatrixOrderRequest(
            CorporateAccountId: acc.Id,
            BranchId: branch.Id,
            OfficerId: officer.Id,
            ExternalPoNumber: string.IsNullOrWhiteSpace(request.NewExternalPoNumber)
                ? $"REORDER-{prevOrder.OrderNumber}"
                : request.NewExternalPoNumber,
            ProductStyle: "Wholesale Apparel Reorder",
            BaseSku: prevOrder.Items.FirstOrDefault()?.Sku.Split('-')[0] ?? "WWR-POLO",
            BaseUnitPrice: prevOrder.Items.FirstOrDefault()?.BaseUnitPrice ?? 28.0m,
            Colors: colors,
            Sizes: sizes,
            MatrixQuantities: matrixQuantities,
            TaxRatePercent: 19.0m,
            ShippingFee: prevOrder.ShippingFee,
            ShippingNotes: $"1-Click Reorder from previous PO {prevOrder.OrderNumber} ({prevOrder.ExternalPoNumber})");

        return await SubmitBulkMatrixOrderAsync(tenantId, submitReq);
    }

    #endregion

    #region Mapping Helpers

    private static CorporateAccountDto MapToDto(CorporateAccountRecord r) => new(
        r.Id,
        r.TenantId,
        r.CompanyName,
        r.TaxId,
        r.CreditLimit,
        r.PaymentTermsDays,
        r.RequiresApprovalAboveAmount,
        r.Currency,
        r.IsActive,
        r.CreatedAtUtc,
        r.Branches.Select(MapBranchToDto).ToList(),
        r.Officers.Select(MapOfficerToDto).ToList());

    private static CorporateBranchDto MapBranchToDto(CorporateBranchRecord b) => new(
        b.Id,
        b.CorporateAccountId,
        b.BranchName,
        b.BranchCode,
        b.ShippingAddress,
        b.City,
        b.PostalCode,
        b.Country,
        b.ContactPerson,
        b.ContactPhone,
        b.IsActive);

    private static CorporateOfficerDto MapOfficerToDto(CorporateOfficerRecord o) => new(
        o.Id,
        o.CorporateAccountId,
        o.Name,
        o.Email,
        o.Role,
        o.IndividualSpendingLimit,
        o.IsActive);

    private static WholesaleOrderDto MapOrderToDto(WholesaleOrderRecord o) => new(
        o.Id,
        o.TenantId,
        o.CorporateAccountId,
        o.CompanyName,
        o.BranchId,
        o.BranchName,
        o.SubmittedByOfficerId,
        o.SubmitterName,
        o.SubmitterRole,
        o.OrderNumber,
        o.ExternalPoNumber,
        o.Status,
        o.TotalUnits,
        o.Subtotal,
        o.VolumeDiscountPercent,
        o.DiscountAmount,
        o.TaxAmount,
        o.ShippingFee,
        o.GrandTotal,
        o.CreditHoldAmount,
        o.RequiresApproval,
        o.ApprovalReason,
        o.ApprovedByOfficerId,
        o.ApproverName,
        o.ApprovedAtUtc,
        o.RejectionReason,
        o.WarehouseStockReserved,
        o.ShippingNotes,
        o.Items,
        o.CreatedAtUtc,
        o.UpdatedAtUtc);

    private static CorporateInvoiceDto MapInvoiceToDto(CorporateInvoiceRecord i) => new(
        i.Id,
        i.CorporateAccountId,
        i.InvoiceNumber,
        i.PurchaseOrderId,
        i.ExternalPoNumber,
        i.TotalAmount,
        i.AmountPaid,
        i.BalanceDue,
        i.Status,
        i.IssuedAtUtc,
        i.DueDateUtc,
        i.PaidAtUtc);

    #endregion

    #region Internal Storage Records

    internal class CorporateAccountRecord
    {
        public Guid Id { get; set; }
        public Guid TenantId { get; set; }
        public string CompanyName { get; set; }
        public string TaxId { get; set; }
        public decimal CreditLimit { get; set; }
        public int PaymentTermsDays { get; set; }
        public decimal RequiresApprovalAboveAmount { get; set; }
        public string Currency { get; set; }
        public bool IsActive { get; set; }
        public DateTime CreatedAtUtc { get; set; }
        public List<CorporateBranchRecord> Branches { get; set; }
        public List<CorporateOfficerRecord> Officers { get; set; }

        public CorporateAccountRecord(
            Guid id,
            Guid tenantId,
            string companyName,
            string taxId,
            decimal creditLimit,
            int paymentTermsDays,
            decimal requiresApprovalAboveAmount,
            string currency,
            bool isActive,
            DateTime createdAtUtc,
            List<CorporateBranchRecord> branches,
            List<CorporateOfficerRecord> officers)
        {
            Id = id;
            TenantId = tenantId;
            CompanyName = companyName;
            TaxId = taxId;
            CreditLimit = creditLimit;
            PaymentTermsDays = paymentTermsDays;
            RequiresApprovalAboveAmount = requiresApprovalAboveAmount;
            Currency = currency;
            IsActive = isActive;
            CreatedAtUtc = createdAtUtc;
            Branches = branches;
            Officers = officers;
        }
    }

    internal record CorporateBranchRecord(
        Guid Id,
        Guid CorporateAccountId,
        string BranchName,
        string BranchCode,
        string ShippingAddress,
        string City,
        string PostalCode,
        string Country,
        string ContactPerson,
        string ContactPhone,
        bool IsActive);

    internal record CorporateOfficerRecord(
        Guid Id,
        Guid CorporateAccountId,
        string Name,
        string Email,
        string Role,
        decimal IndividualSpendingLimit,
        bool IsActive);

    internal class WholesaleOrderRecord
    {
        public Guid Id { get; set; }
        public Guid TenantId { get; set; }
        public Guid CorporateAccountId { get; set; }
        public string CompanyName { get; set; }
        public Guid BranchId { get; set; }
        public string BranchName { get; set; }
        public Guid SubmittedByOfficerId { get; set; }
        public string SubmitterName { get; set; }
        public string SubmitterRole { get; set; }
        public string OrderNumber { get; set; }
        public string ExternalPoNumber { get; set; }
        public string Status { get; set; }
        public int TotalUnits { get; set; }
        public decimal Subtotal { get; set; }
        public decimal VolumeDiscountPercent { get; set; }
        public decimal DiscountAmount { get; set; }
        public decimal TaxAmount { get; set; }
        public decimal ShippingFee { get; set; }
        public decimal GrandTotal { get; set; }
        public decimal CreditHoldAmount { get; set; }
        public bool RequiresApproval { get; set; }
        public string? ApprovalReason { get; set; }
        public Guid? ApprovedByOfficerId { get; set; }
        public string? ApproverName { get; set; }
        public DateTime? ApprovedAtUtc { get; set; }
        public string? RejectionReason { get; set; }
        public bool WarehouseStockReserved { get; set; }
        public string? ShippingNotes { get; set; }
        public List<MatrixCalculatedItemDto> Items { get; set; }
        public DateTime CreatedAtUtc { get; set; }
        public DateTime UpdatedAtUtc { get; set; }

        public WholesaleOrderRecord(
            Guid Id,
            Guid TenantId,
            Guid CorporateAccountId,
            string CompanyName,
            Guid BranchId,
            string BranchName,
            Guid SubmittedByOfficerId,
            string SubmitterName,
            string SubmitterRole,
            string OrderNumber,
            string ExternalPoNumber,
            string Status,
            int TotalUnits,
            decimal Subtotal,
            decimal VolumeDiscountPercent,
            decimal DiscountAmount,
            decimal TaxAmount,
            decimal ShippingFee,
            decimal GrandTotal,
            decimal CreditHoldAmount,
            bool RequiresApproval,
            string? ApprovalReason,
            Guid? ApprovedByOfficerId,
            string? ApproverName,
            DateTime? ApprovedAtUtc,
            string? RejectionReason,
            bool WarehouseStockReserved,
            string? ShippingNotes,
            List<MatrixCalculatedItemDto> Items,
            DateTime CreatedAtUtc,
            DateTime UpdatedAtUtc)
        {
            this.Id = Id;
            this.TenantId = TenantId;
            this.CorporateAccountId = CorporateAccountId;
            this.CompanyName = CompanyName;
            this.BranchId = BranchId;
            this.BranchName = BranchName;
            this.SubmittedByOfficerId = SubmittedByOfficerId;
            this.SubmitterName = SubmitterName;
            this.SubmitterRole = SubmitterRole;
            this.OrderNumber = OrderNumber;
            this.ExternalPoNumber = ExternalPoNumber;
            this.Status = Status;
            this.TotalUnits = TotalUnits;
            this.Subtotal = Subtotal;
            this.VolumeDiscountPercent = VolumeDiscountPercent;
            this.DiscountAmount = DiscountAmount;
            this.TaxAmount = TaxAmount;
            this.ShippingFee = ShippingFee;
            this.GrandTotal = GrandTotal;
            this.CreditHoldAmount = CreditHoldAmount;
            this.RequiresApproval = RequiresApproval;
            this.ApprovalReason = ApprovalReason;
            this.ApprovedByOfficerId = ApprovedByOfficerId;
            this.ApproverName = ApproverName;
            this.ApprovedAtUtc = ApprovedAtUtc;
            this.RejectionReason = RejectionReason;
            this.WarehouseStockReserved = WarehouseStockReserved;
            this.ShippingNotes = ShippingNotes;
            this.Items = Items;
            this.CreatedAtUtc = CreatedAtUtc;
            this.UpdatedAtUtc = UpdatedAtUtc;
        }
    }

    internal record CreditLedgerEntryRecord(
        Guid Id,
        Guid CorporateAccountId,
        string EntryType,
        decimal Amount,
        string ReferenceNumber,
        string ReferenceType,
        string Notes,
        DateTime CreatedAtUtc);

    internal class CorporateInvoiceRecord
    {
        public Guid Id { get; set; }
        public Guid CorporateAccountId { get; set; }
        public string InvoiceNumber { get; set; }
        public Guid? PurchaseOrderId { get; set; }
        public string? ExternalPoNumber { get; set; }
        public decimal TotalAmount { get; set; }
        public decimal AmountPaid { get; set; }
        public decimal BalanceDue { get; set; }
        public string Status { get; set; }
        public DateTime IssuedAtUtc { get; set; }
        public DateTime DueDateUtc { get; set; }
        public DateTime? PaidAtUtc { get; set; }

        public CorporateInvoiceRecord(
            Guid id,
            Guid corporateAccountId,
            string invoiceNumber,
            Guid? purchaseOrderId,
            string? externalPoNumber,
            decimal totalAmount,
            decimal amountPaid,
            decimal balanceDue,
            string status,
            DateTime issuedAtUtc,
            DateTime dueDateUtc,
            DateTime? paidAtUtc)
        {
            Id = id;
            CorporateAccountId = corporateAccountId;
            InvoiceNumber = invoiceNumber;
            PurchaseOrderId = purchaseOrderId;
            ExternalPoNumber = externalPoNumber;
            TotalAmount = totalAmount;
            AmountPaid = amountPaid;
            BalanceDue = balanceDue;
            Status = status;
            IssuedAtUtc = issuedAtUtc;
            DueDateUtc = dueDateUtc;
            PaidAtUtc = paidAtUtc;
        }
    }

    #endregion
}
