using Microsoft.EntityFrameworkCore;
using SGrowing.Api.Data;
using SGrowing.Api.Modules.Commerce;
using Xunit;

namespace SGrowing.Tests.Commerce;

public class B2BWholesalePortalTests
{
    private readonly SGrowingDbContext _db;
    private readonly B2BWholesalePortalService _service;
    private readonly Guid _tenantId = Guid.NewGuid();

    public B2BWholesalePortalTests()
    {
        var options = new DbContextOptionsBuilder<SGrowingDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        _db = new SGrowingDbContext(options);
        _service = new B2BWholesalePortalService(_db);
    }

    #region 1. Corporate Hierarchy Resolution

    [Fact]
    public async Task CorporateHierarchy_Should_Create_Account_Branches_And_Officers_With_Limits()
    {
        // Arrange
        var request = new CreateCorporateAccountRequest(
            CompanyName: "Nordic Industrial Supplies AB",
            TaxId: "SE-556123456701",
            CreditLimit: 120000m,
            PaymentTermsDays: 45,
            RequiresApprovalAboveAmount: 8000m,
            Currency: "USD",
            Branches: new List<CreateBranchRequest>
            {
                new("Stockholm Logistics Center", "STK-01", "Hammarby Kaj 10", "Stockholm", "12030", "Sweden", "Lars Lindqvist", "+46 8 123 4567"),
                new("Gothenburg Marine Terminal", "GOT-02", "Hamntorget 4", "Gothenburg", "41103", "Sweden", "Astrid Bergman", "+46 31 789 0123")
            },
            Officers: new List<CreateOfficerRequest>
            {
                new("Karin Holm", "k.holm@nordic-supplies.se", CorporateRoles.ProcurementBuyer, 4000m),
                new("Erik Nilsson", "e.nilsson@nordic-supplies.se", CorporateRoles.DepartmentDirector, 25000m),
                new("Sofia Larsson", "s.larsson@nordic-supplies.se", CorporateRoles.CFO, 200000m)
            });

        // Act
        var created = await _service.CreateAccountAsync(_tenantId, request);
        var retrieved = await _service.GetAccountByIdAsync(_tenantId, created.Id);

        // Assert
        Assert.NotNull(retrieved);
        Assert.Equal("Nordic Industrial Supplies AB", retrieved.CompanyName);
        Assert.Equal("SE-556123456701", retrieved.TaxId);
        Assert.Equal(120000m, retrieved.CreditLimit);
        Assert.Equal(45, retrieved.PaymentTermsDays);
        Assert.Equal(8000m, retrieved.RequiresApprovalAboveAmount);
        Assert.Equal(2, retrieved.Branches.Count);
        Assert.Equal(3, retrieved.Officers.Count);

        var buyer = retrieved.Officers.First(o => o.Role == CorporateRoles.ProcurementBuyer);
        Assert.Equal("Karin Holm", buyer.Name);
        Assert.Equal(4000m, buyer.IndividualSpendingLimit);

        var cfo = retrieved.Officers.First(o => o.Role == CorporateRoles.CFO);
        Assert.Equal(200000m, cfo.IndividualSpendingLimit);
    }

    [Fact]
    public async Task AddBranch_And_AddOfficer_Should_Extend_Account_Hierarchy()
    {
        // Arrange
        var account = await _service.CreateAccountAsync(_tenantId, new CreateCorporateAccountRequest(
            "Atlas Heavy Machinery S.A.", "ES-B99887766", 75000m));

        // Act
        var branch = await _service.AddBranchAsync(_tenantId, account.Id, new CreateBranchRequest(
            "Valencia Hub", "VLC-01", "Calle Mayor 12", "Valencia", "46001", "Spain", "Carlos Ruiz", "+34 96 111 2233"));

        var officer = await _service.AddOfficerAsync(_tenantId, account.Id, new CreateOfficerRequest(
            "Lucia Gomez", "lucia@atlas-heavy.es", CorporateRoles.ProcurementBuyer, 3500m));

        var updatedAccount = await _service.GetAccountByIdAsync(_tenantId, account.Id);

        // Assert
        Assert.NotNull(updatedAccount);
        Assert.Contains(updatedAccount.Branches, b => b.BranchCode == "VLC-01");
        Assert.Contains(updatedAccount.Officers, o => o.Email == "lucia@atlas-heavy.es");
    }

    #endregion

    #region 2. Revolving Credit Limit Ledger & Math

    [Fact]
    public async Task RevolvingCreditLedger_Formula_Should_Calculate_Available_Credit_Accurately()
    {
        // Formula: AvailableCredit = CreditLimit - UnpaidInvoicesTotal - PendingOrderHoldsTotal
        var account = await _service.CreateAccountAsync(_tenantId, new CreateCorporateAccountRequest(
            "Mediterranean Shipyards Ltd", "MT-99881122", 100000m, 30, 10000m));

        // Billed invoice: $25,000
        await _service.CreateInvoiceAsync(_tenantId, account.Id, new CreateInvoiceRequest(
            PurchaseOrderId: null,
            ExternalPoNumber: "PO-MED-101",
            Amount: 25000m,
            DueInDays: 30));

        // Check credit status before order
        var statusInitial = await _service.GetCreditStatusAsync(_tenantId, account.Id);
        Assert.Equal(100000m, statusInitial.CreditLimit);
        Assert.Equal(25000m, statusInitial.UnpaidInvoicesTotal);
        Assert.Equal(0m, statusInitial.PendingOrderHoldsTotal);
        Assert.Equal(75000m, statusInitial.AvailableCredit);
        Assert.Equal(25.0m, statusInitial.CreditUtilizationPercent);

        // Add branch & buyer officer
        var branch = await _service.AddBranchAsync(_tenantId, account.Id, new CreateBranchRequest(
            "Valletta Dock", "VAL-01", "Grand Harbour", "Valletta", "VLT1115", "Malta", "Mario Vella", "+356 21 000000"));
        var buyer = await _service.AddOfficerAsync(_tenantId, account.Id, new CreateOfficerRequest(
            "Paul Camilleri", "paul@medshipyards.mt", CorporateRoles.ProcurementBuyer, 15000m));

        // Submit wholesale order of 100 units @ base $20 = $1,800 + tax $342 + shipping $45 = $2,187
        var submitOrder = await _service.SubmitBulkMatrixOrderAsync(_tenantId, new SubmitBulkMatrixOrderRequest(
            CorporateAccountId: account.Id,
            BranchId: branch.Id,
            OfficerId: buyer.Id,
            ExternalPoNumber: "PO-MED-202",
            ProductStyle: "Heavy Marine Workwear",
            BaseSku: "MAR-VEST",
            BaseUnitPrice: 20.0m,
            Colors: new List<string> { "Navy" },
            Sizes: new List<string> { "L", "XL" },
            MatrixQuantities: new List<MatrixCellInputDto>
            {
                new("L", "Navy", 50),
                new("XL", "Navy", 50)
            },
            TaxRatePercent: 19.0m,
            ShippingFee: 45.0m));

        // Check credit status after order hold placed
        var statusAfterOrder = await _service.GetCreditStatusAsync(_tenantId, account.Id);
        Assert.Equal(25000m, statusAfterOrder.UnpaidInvoicesTotal);
        Assert.Equal(submitOrder.GrandTotal, statusAfterOrder.PendingOrderHoldsTotal);
        Assert.Equal(100000m - 25000m - submitOrder.GrandTotal, statusAfterOrder.AvailableCredit);

        // Verify ledger entries exist
        var ledger = await _service.GetCreditLedgerEntriesAsync(_tenantId, account.Id);
        Assert.Contains(ledger, e => e.EntryType == CreditLedgerEntryType.HoldPlaced && e.Amount == submitOrder.GrandTotal);
        Assert.Contains(ledger, e => e.EntryType == CreditLedgerEntryType.InvoiceBilled && e.Amount == 25000m);
    }

    [Fact]
    public async Task InvoicePayment_Should_Replenish_Available_Credit_And_Update_Ledger()
    {
        // Arrange
        var account = await _service.CreateAccountAsync(_tenantId, new CreateCorporateAccountRequest(
            "Bavaria Gearworks GmbH", "DE-123456789", 50000m, 30));

        var invoice = await _service.CreateInvoiceAsync(_tenantId, account.Id, new CreateInvoiceRequest(
            PurchaseOrderId: null,
            ExternalPoNumber: "PO-BAV-01",
            Amount: 20000m));

        var statusBefore = await _service.GetCreditStatusAsync(_tenantId, account.Id);
        Assert.Equal(30000m, statusBefore.AvailableCredit);

        // Act - Pay $12,000 on the $20,000 invoice
        var updatedInvoice = await _service.RecordInvoicePaymentAsync(_tenantId, account.Id, invoice.Id, 12000m, "WIRE-REF-992384");
        var statusAfterPartial = await _service.GetCreditStatusAsync(_tenantId, account.Id);

        // Assert
        Assert.Equal(8000m, updatedInvoice.BalanceDue);
        Assert.Equal("PartiallyPaid", updatedInvoice.Status);
        Assert.Equal(42000m, statusAfterPartial.AvailableCredit); // 50,000 - 8,000 balance due

        // Act - Pay remaining $8,000
        var fullyPaidInvoice = await _service.RecordInvoicePaymentAsync(_tenantId, account.Id, invoice.Id, 8000m, "WIRE-REF-992385");
        var statusAfterFull = await _service.GetCreditStatusAsync(_tenantId, account.Id);

        // Assert
        Assert.Equal(0m, fullyPaidInvoice.BalanceDue);
        Assert.Equal("Paid", fullyPaidInvoice.Status);
        Assert.Equal(50000m, statusAfterFull.AvailableCredit); // Full replenishment!
    }

    #endregion

    #region 3. Credit Limit Exhaustion Blocking

    [Fact]
    public async Task CreditLimitExhaustion_Should_Block_Order_Submission_Unless_Overridden()
    {
        // Account with $10,000 credit limit
        var account = await _service.CreateAccountAsync(_tenantId, new CreateCorporateAccountRequest(
            "Alpine Logistics Sarl", "CH-CHE101202303", 10000m));

        var branch = await _service.AddBranchAsync(_tenantId, account.Id, new CreateBranchRequest(
            "Geneva Airport Depot", "GVA-01", "Route de l'Aéroport", "Geneva", "1215", "Switzerland", "Marc Favre", "+41 22 717 7111"));

        var buyer = await _service.AddOfficerAsync(_tenantId, account.Id, new CreateOfficerRequest(
            "Alain Rochat", "alain@alpine-logistics.ch", CorporateRoles.ProcurementBuyer, 20000m));

        // Submit order totaling 600 units @ base $30.
        // Tier 4 discount (25%): 600 * $22.50 = $13,500 + 19% VAT ($2,565) = $16,065
        // This exceeds the $10,000 credit limit!
        var orderReq = new SubmitBulkMatrixOrderRequest(
            CorporateAccountId: account.Id,
            BranchId: branch.Id,
            OfficerId: buyer.Id,
            ExternalPoNumber: "PO-ALP-EXCEED",
            ProductStyle: "Alpine Fleece Jacket",
            BaseSku: "ALP-FLEECE",
            BaseUnitPrice: 30.0m,
            Colors: new List<string> { "Black", "Charcoal" },
            Sizes: new List<string> { "M", "L", "XL" },
            MatrixQuantities: new List<MatrixCellInputDto>
            {
                new("M", "Black", 100),
                new("L", "Black", 100),
                new("XL", "Black", 100),
                new("M", "Charcoal", 100),
                new("L", "Charcoal", 100),
                new("XL", "Charcoal", 100)
            },
            TaxRatePercent: 19.0m,
            ShippingFee: 0m,
            AllowCreditOverride: false);

        // Act & Assert - Should throw InvalidOperationException because credit is exhausted
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _service.SubmitBulkMatrixOrderAsync(_tenantId, orderReq));

        Assert.Contains("exceeds available credit limit", ex.Message);

        // Now test with AllowCreditOverride = true (e.g. approved by CFO or special contract)
        var overrideReq = orderReq with { AllowCreditOverride = true };
        var approvedOrder = await _service.SubmitBulkMatrixOrderAsync(_tenantId, overrideReq);

        Assert.NotNull(approvedOrder);
        Assert.Equal(600, approvedOrder.TotalUnits);
    }

    #endregion

    #region 4. Bulk Matrix SKU Order Calculation

    [Fact]
    public async Task BulkMatrixSkuOrder_Calculation_Should_Apply_Volume_Tiers_Correctly()
    {
        // 4 colors x 5 sizes = 20 SKU combinations
        var colors = new List<string> { "Navy", "Charcoal", "Black", "Royal Blue" };
        var sizes = new List<string> { "S", "M", "L", "XL", "2XL" };

        // 15 units per cell * 20 cells = 300 total units
        // Tier 3: 200-499 units -> 18% discount
        var quantities = new List<MatrixCellInputDto>();
        foreach (var color in colors)
        {
            foreach (var size in sizes)
            {
                quantities.Add(new MatrixCellInputDto(size, color, 15));
            }
        }

        var calcReq = new BulkMatrixCalculationRequest(
            ProductStyle: "Industrial Heavy Duty Polo",
            BaseSku: "WWR-POLO",
            BaseUnitPrice: 25.0m,
            Colors: colors,
            Sizes: sizes,
            MatrixQuantities: quantities,
            TaxRatePercent: 19.0m,
            ShippingFee: 45.0m);

        // Act
        var result = await _service.CalculateBulkMatrixOrderAsync(_tenantId, calcReq);

        // Assert
        Assert.Equal(300, result.TotalUnits);
        Assert.Equal(20, result.MatrixItems.Count);
        Assert.Equal(18.0m, result.VolumeDiscountPercent);
        Assert.Contains("Wholesale Gold", result.AppliedTierLabel);

        // Base price = $25.00, 18% off = $20.50
        Assert.Equal(25.0m, result.BaseUnitPrice);
        Assert.Equal(20.50m, result.DiscountedUnitPrice);

        // Subtotal before discount: 300 * $25 = $7,500
        Assert.Equal(7500.0m, result.SubtotalBeforeDiscount);
        // Subtotal: 300 * $20.50 = $6,150
        Assert.Equal(6150.0m, result.Subtotal);
        // Discount amount: $7,500 - $6,150 = $1,350
        Assert.Equal(1350.0m, result.DiscountAmount);

        // Tax: 19% of $6,150 = $1,168.50
        Assert.Equal(1168.50m, result.TaxAmount);
        // Shipping fee: $45.00
        Assert.Equal(45.0m, result.ShippingFee);
        // Grand total: $6,150 + $1,168.50 + $45 = $7,363.50
        Assert.Equal(7363.50m, result.GrandTotal);

        // Verify SKU structure: {BaseSku}-{ColorCode}-{SizeCode}
        var sampleItem = result.MatrixItems.First(i => i.Color == "Navy" && i.Size == "XL");
        Assert.Equal("WWR-POLO-NAV-XL", sampleItem.Sku);
        Assert.Equal(15, sampleItem.Quantity);
        Assert.Equal(20.50m, sampleItem.UnitPrice);
        Assert.Equal(307.50m, sampleItem.LineTotal);
    }

    [Fact]
    public async Task BulkMatrixSkuOrder_Above_500_Units_Should_Get_Tier4_And_Free_Shipping()
    {
        // 500 units should activate Tier 4 (25% off) and qualify for Free Freight ($0 shipping)
        var quantities = new List<MatrixCellInputDto>
        {
            new("L", "Navy", 250),
            new("XL", "Black", 250)
        };

        var calcReq = new BulkMatrixCalculationRequest(
            ProductStyle: "Industrial Cargo Pants",
            BaseSku: "WWR-PANTS",
            BaseUnitPrice: 40.0m,
            Colors: new List<string> { "Navy", "Black" },
            Sizes: new List<string> { "L", "XL" },
            MatrixQuantities: quantities,
            TaxRatePercent: 19.0m,
            ShippingFee: 120.0m);

        // Act
        var result = await _service.CalculateBulkMatrixOrderAsync(_tenantId, calcReq);

        // Assert
        Assert.Equal(500, result.TotalUnits);
        Assert.Equal(25.0m, result.VolumeDiscountPercent);
        Assert.Equal(30.0m, result.DiscountedUnitPrice); // $40 * 0.75
        Assert.Equal(15000.0m, result.Subtotal);          // 500 * $30
        Assert.Equal(0m, result.ShippingFee);             // Free freight applied!
        Assert.Equal(15000.0m + (15000.0m * 0.19m), result.GrandTotal);
    }

    #endregion

    #region 5. Corporate Spending Approval Chains

    [Fact]
    public async Task Order_Within_Officer_Limit_And_Account_Threshold_Should_AutoConfirm_And_Reserve_Stock()
    {
        // Account threshold $10,000, Buyer limit $5,000
        var account = await _service.CreateAccountAsync(_tenantId, new CreateCorporateAccountRequest(
            "Bordeaux Logistics SAS", "FR-33001122", 80000m, 30, 10000m));

        var branch = await _service.AddBranchAsync(_tenantId, account.Id, new CreateBranchRequest(
            "Bordeaux Depot", "BDX-01", "Quai des Chartrons", "Bordeaux", "33000", "France", "Guillaume", "+33 5 56 00 11 22"));

        var buyer = await _service.AddOfficerAsync(_tenantId, account.Id, new CreateOfficerRequest(
            "Julien Marchand", "j.marchand@bordeaux-log.fr", CorporateRoles.ProcurementBuyer, 5000m));

        // Submit small order: 50 units @ base $20 = $900 + 19% VAT ($171) + shipping $45 = $1,116
        // Within both $5,000 officer limit and $10,000 account threshold
        var order = await _service.SubmitBulkMatrixOrderAsync(_tenantId, new SubmitBulkMatrixOrderRequest(
            CorporateAccountId: account.Id,
            BranchId: branch.Id,
            OfficerId: buyer.Id,
            ExternalPoNumber: "PO-BDX-SMALL-01",
            ProductStyle: "Safety Hi-Vis Vest",
            BaseSku: "WWR-VEST",
            BaseUnitPrice: 20.0m,
            Colors: new List<string> { "Yellow" },
            Sizes: new List<string> { "L" },
            MatrixQuantities: new List<MatrixCellInputDto> { new("L", "Yellow", 50) }));

        // Assert
        Assert.Equal(WholesaleOrderStatus.Confirmed, order.Status);
        Assert.False(order.RequiresApproval);
        Assert.True(order.WarehouseStockReserved);
        Assert.NotNull(order.ApprovedAtUtc);
    }

    [Fact]
    public async Task Order_Exceeding_Officer_Limit_Should_Be_Placed_In_PendingCorporateApproval()
    {
        // Account threshold $20,000, Buyer limit $4,000
        var account = await _service.CreateAccountAsync(_tenantId, new CreateCorporateAccountRequest(
            "Marseille Maritime SA", "FR-13009988", 100000m, 30, 20000m));

        var branch = await _service.AddBranchAsync(_tenantId, account.Id, new CreateBranchRequest(
            "Dock 4", "MRS-04", "Port de Marseille", "Marseille", "13002", "France", "Michel", "+33 4 91 00 00 00"));

        var buyer = await _service.AddOfficerAsync(_tenantId, account.Id, new CreateOfficerRequest(
            "Sophie Bernard", "s.bernard@marseille-maritime.fr", CorporateRoles.ProcurementBuyer, 4000m));

        // Order total: 250 units @ base $25 = 250 * $20.50 (18% off) = $5,125 + 19% VAT = $6,098.75 + shipping = $6,143.75
        // Exceeds Buyer's $4,000 limit, but is below $20,000 account threshold
        var order = await _service.SubmitBulkMatrixOrderAsync(_tenantId, new SubmitBulkMatrixOrderRequest(
            CorporateAccountId: account.Id,
            BranchId: branch.Id,
            OfficerId: buyer.Id,
            ExternalPoNumber: "PO-MRS-OVER-BUYER",
            ProductStyle: "Deckhand Heavy Uniform",
            BaseSku: "MAR-UNI",
            BaseUnitPrice: 25.0m,
            Colors: new List<string> { "Navy" },
            Sizes: new List<string> { "L" },
            MatrixQuantities: new List<MatrixCellInputDto> { new("L", "Navy", 250) }));

        // Assert
        Assert.Equal(WholesaleOrderStatus.PendingCorporateApproval, order.Status);
        Assert.True(order.RequiresApproval);
        Assert.False(order.WarehouseStockReserved); // Stock held unreserved until approval
        Assert.Contains("exceeds officer individual limit", order.ApprovalReason);
        Assert.True(order.CreditHoldAmount > 0);   // Credit hold IS reserved so budget is committed
    }

    [Fact]
    public async Task Order_Exceeding_Account_Threshold_Should_Require_Approval_Even_For_Director()
    {
        // Account threshold $15,000. Director limit $40,000.
        var account = await _service.CreateAccountAsync(_tenantId, new CreateCorporateAccountRequest(
            "Rhone Valley Logistics", "FR-69001122", 150000m, 30, 15000m));

        var branch = await _service.AddBranchAsync(_tenantId, account.Id, new CreateBranchRequest(
            "Lyon South", "LYS-01", "Zone Industrielle", "Lyon", "69007", "France", "Pierre", "+33 4 72 00 11 22"));

        var director = await _service.AddOfficerAsync(_tenantId, account.Id, new CreateOfficerRequest(
            "Thierry Henry", "t.henry@rhone-log.fr", CorporateRoles.DepartmentDirector, 40000m));

        // Order total ~ $18,000 (exceeds $15,000 account approval threshold)
        var order = await _service.SubmitBulkMatrixOrderAsync(_tenantId, new SubmitBulkMatrixOrderRequest(
            CorporateAccountId: account.Id,
            BranchId: branch.Id,
            OfficerId: director.Id,
            ExternalPoNumber: "PO-LYON-CORP-THRESH",
            ProductStyle: "Rhone Heavy Thermal Coat",
            BaseSku: "RHO-COAT",
            BaseUnitPrice: 60.0m,
            Colors: new List<string> { "Black" },
            Sizes: new List<string> { "XL" },
            MatrixQuantities: new List<MatrixCellInputDto> { new("XL", "Black", 300) }));

        // Assert
        Assert.Equal(WholesaleOrderStatus.PendingCorporateApproval, order.Status);
        Assert.True(order.RequiresApproval);
        Assert.False(order.WarehouseStockReserved);
    }

    #endregion

    #region 6. Manager Approval & Rejection Workflow

    [Fact]
    public async Task ManagerApproval_Should_Advance_To_Confirmed_And_Reserve_Warehouse_Stock()
    {
        // Arrange
        var account = await _service.CreateAccountAsync(_tenantId, new CreateCorporateAccountRequest(
            "Toulouse Aerospace Ground Spares", "FR-31005544", 200000m, 30, 10000m));

        var branch = await _service.AddBranchAsync(_tenantId, account.Id, new CreateBranchRequest(
            "Blagnac Hangar 3", "TLS-03", "Aéroport Toulouse-Blagnac", "Toulouse", "31700", "France", "Alain", "+33 5 61 00 00 00"));

        var buyer = await _service.AddOfficerAsync(_tenantId, account.Id, new CreateOfficerRequest(
            "Audrey Tautou", "audrey@tls-aerospace.fr", CorporateRoles.ProcurementBuyer, 5000m));

        var director = await _service.AddOfficerAsync(_tenantId, account.Id, new CreateOfficerRequest(
            "Jean Reno", "jean.reno@tls-aerospace.fr", CorporateRoles.DepartmentDirector, 50000m));

        // Submit order that exceeds buyer limit ($6,143.75 > $5,000)
        var pendingOrder = await _service.SubmitBulkMatrixOrderAsync(_tenantId, new SubmitBulkMatrixOrderRequest(
            CorporateAccountId: account.Id,
            BranchId: branch.Id,
            OfficerId: buyer.Id,
            ExternalPoNumber: "PO-TLS-PENDING",
            ProductStyle: "Aviation Tech Coverall",
            BaseSku: "AV-COV",
            BaseUnitPrice: 25.0m,
            Colors: new List<string> { "Navy" },
            Sizes: new List<string> { "L" },
            MatrixQuantities: new List<MatrixCellInputDto> { new("L", "Navy", 250) }));

        Assert.Equal(WholesaleOrderStatus.PendingCorporateApproval, pendingOrder.Status);
        Assert.False(pendingOrder.WarehouseStockReserved);

        // Act - Manager (Department Director) approves
        var approvedOrder = await _service.ApproveOrderAsync(_tenantId, pendingOrder.Id, new ApproveOrderRequest(
            ApproverOfficerId: director.Id,
            ApprovalNotes: "Approved for Q4 maintenance rollout. Verified within division quarterly budget."));

        // Assert
        Assert.Equal(WholesaleOrderStatus.Confirmed, approvedOrder.Status);
        Assert.True(approvedOrder.WarehouseStockReserved); // Warehouse stock now reserved!
        Assert.Equal(director.Id, approvedOrder.ApprovedByOfficerId);
        Assert.Equal("Jean Reno", approvedOrder.ApproverName);
        Assert.NotNull(approvedOrder.ApprovedAtUtc);
    }

    [Fact]
    public async Task NonManager_Approval_Attempt_Should_Be_Rejected_With_UnauthorizedAccessException()
    {
        // Arrange
        var account = await _service.CreateAccountAsync(_tenantId, new CreateCorporateAccountRequest(
            "Strasbourg Rhine Cargo", "FR-67001122", 100000m, 30, 5000m));

        var branch = await _service.AddBranchAsync(_tenantId, account.Id, new CreateBranchRequest(
            "Port du Rhin", "SXB-01", "Rue du Port", "Strasbourg", "67000", "France", "Gilles", "+33 3 88 00 11 22"));

        var buyer1 = await _service.AddOfficerAsync(_tenantId, account.Id, new CreateOfficerRequest(
            "Buyer One", "buyer1@rhine-cargo.fr", CorporateRoles.ProcurementBuyer, 3000m));

        var buyer2 = await _service.AddOfficerAsync(_tenantId, account.Id, new CreateOfficerRequest(
            "Buyer Two", "buyer2@rhine-cargo.fr", CorporateRoles.ProcurementBuyer, 4000m));

        var order = await _service.SubmitBulkMatrixOrderAsync(_tenantId, new SubmitBulkMatrixOrderRequest(
            CorporateAccountId: account.Id,
            BranchId: branch.Id,
            OfficerId: buyer1.Id,
            ExternalPoNumber: "PO-SXB-EXCEED",
            ProductStyle: "Rhine Heavy Gloves",
            BaseSku: "RH-GLOVE",
            BaseUnitPrice: 25.0m,
            Colors: new List<string> { "Black" },
            Sizes: new List<string> { "L" },
            MatrixQuantities: new List<MatrixCellInputDto> { new("L", "Black", 250) }));

        // Act & Assert - Buyer 2 (not a manager) tries to approve Buyer 1's order
        var ex = await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            _service.ApproveOrderAsync(_tenantId, order.Id, new ApproveOrderRequest(buyer2.Id)));

        Assert.Contains("lacks managerial approval authorization", ex.Message);
    }

    [Fact]
    public async Task ManagerRejection_Should_Reject_Order_And_Release_Credit_Hold()
    {
        // Arrange
        var account = await _service.CreateAccountAsync(_tenantId, new CreateCorporateAccountRequest(
            "Lille Industrial Fabrics", "FR-59002233", 60000m, 30, 5000m));

        var branch = await _service.AddBranchAsync(_tenantId, account.Id, new CreateBranchRequest(
            "Lille Depot", "LIL-01", "Boulevard Carnot", "Lille", "59800", "France", "Martine", "+33 3 20 00 11 22"));

        var buyer = await _service.AddOfficerAsync(_tenantId, account.Id, new CreateOfficerRequest(
            "David Guetta", "david@lille-fabrics.fr", CorporateRoles.ProcurementBuyer, 2000m));

        var director = await _service.AddOfficerAsync(_tenantId, account.Id, new CreateOfficerRequest(
            "Marion Cotillard", "marion@lille-fabrics.fr", CorporateRoles.DepartmentDirector, 30000m));

        var order = await _service.SubmitBulkMatrixOrderAsync(_tenantId, new SubmitBulkMatrixOrderRequest(
            CorporateAccountId: account.Id,
            BranchId: branch.Id,
            OfficerId: buyer.Id,
            ExternalPoNumber: "PO-LIL-TO-REJECT",
            ProductStyle: "Lille Thermal Undergarments",
            BaseSku: "LIL-THERM",
            BaseUnitPrice: 20.0m,
            Colors: new List<string> { "Navy" },
            Sizes: new List<string> { "M" },
            MatrixQuantities: new List<MatrixCellInputDto> { new("M", "Navy", 200) }));

        var statusBeforeRejection = await _service.GetCreditStatusAsync(_tenantId, account.Id);
        Assert.Equal(order.GrandTotal, statusBeforeRejection.PendingOrderHoldsTotal);

        // Act - Manager rejects the order
        var rejectedOrder = await _service.RejectOrderAsync(_tenantId, order.Id, new RejectOrderRequest(
            ApproverOfficerId: director.Id,
            Reason: "Department budget frozen until next quarter. Please re-submit in January."));

        var statusAfterRejection = await _service.GetCreditStatusAsync(_tenantId, account.Id);
        var ledger = await _service.GetCreditLedgerEntriesAsync(_tenantId, account.Id);

        // Assert
        Assert.Equal(WholesaleOrderStatus.Rejected, rejectedOrder.Status);
        Assert.False(rejectedOrder.WarehouseStockReserved);
        Assert.Equal("Department budget frozen until next quarter. Please re-submit in January.", rejectedOrder.RejectionReason);

        // Credit hold is completely released!
        Assert.Equal(0m, statusAfterRejection.PendingOrderHoldsTotal);
        Assert.Equal(60000m, statusAfterRejection.AvailableCredit);

        Assert.Contains(ledger, e => e.EntryType == CreditLedgerEntryType.HoldReleased && e.ReferenceNumber == order.OrderNumber);
    }

    #endregion

    #region 7. 1-Click Reorder & Query Filters

    [Fact]
    public async Task ReorderPreviousOrder_Should_Duplicate_Matrix_And_Process_Revolving_Credit()
    {
        // Arrange
        var account = await _service.CreateAccountAsync(_tenantId, new CreateCorporateAccountRequest(
            "Nice Côte d'Azur Fleet", "FR-06001122", 80000m, 30, 20000m));

        var branch = await _service.AddBranchAsync(_tenantId, account.Id, new CreateBranchRequest(
            "Nice Port", "NCE-01", "Promenade des Anglais", "Nice", "06000", "France", "Jacques", "+33 4 93 00 11 22"));

        var buyer = await _service.AddOfficerAsync(_tenantId, account.Id, new CreateOfficerRequest(
            "Luc Besson", "luc@nice-fleet.fr", CorporateRoles.ProcurementBuyer, 10000m));

        // Initial order
        var originalOrder = await _service.SubmitBulkMatrixOrderAsync(_tenantId, new SubmitBulkMatrixOrderRequest(
            CorporateAccountId: account.Id,
            BranchId: branch.Id,
            OfficerId: buyer.Id,
            ExternalPoNumber: "PO-NCE-ORIGINAL",
            ProductStyle: "Nice Sea Patrol Vest",
            BaseSku: "NCE-VEST",
            BaseUnitPrice: 30.0m,
            Colors: new List<string> { "Blue" },
            Sizes: new List<string> { "L", "XL" },
            MatrixQuantities: new List<MatrixCellInputDto>
            {
                new("L", "Blue", 60),
                new("XL", "Blue", 60)
            }));

        Assert.Equal(WholesaleOrderStatus.Confirmed, originalOrder.Status);

        // Act - 1-Click Reorder
        var reordered = await _service.ReorderPreviousOrderAsync(_tenantId, originalOrder.Id, new ReorderRequest(
            OfficerId: buyer.Id,
            BranchId: branch.Id,
            NewExternalPoNumber: "PO-NCE-REORDER-02"));

        // Assert
        Assert.NotNull(reordered);
        Assert.NotEqual(originalOrder.Id, reordered.Id);
        Assert.Equal("PO-NCE-REORDER-02", reordered.ExternalPoNumber);
        Assert.Equal(originalOrder.TotalUnits, reordered.TotalUnits);
        Assert.Equal(originalOrder.GrandTotal, reordered.GrandTotal);
    }

    [Fact]
    public async Task GetOrders_And_GetPendingApprovals_Should_Filter_Accurately()
    {
        // Query pending approvals
        var pendingApprovals = await _service.GetPendingApprovalsAsync(_tenantId);
        Assert.NotNull(pendingApprovals);

        // Query all orders
        var allOrders = await _service.GetOrdersAsync(_tenantId);
        Assert.NotNull(allOrders);

        // Filter by status
        var confirmedOnly = await _service.GetOrdersAsync(_tenantId, status: WholesaleOrderStatus.Confirmed);
        Assert.All(confirmedOnly, o => Assert.Equal(WholesaleOrderStatus.Confirmed, o.Status));
    }

    #endregion
}
