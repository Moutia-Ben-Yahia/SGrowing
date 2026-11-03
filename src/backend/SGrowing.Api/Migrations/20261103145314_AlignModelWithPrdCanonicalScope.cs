using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SGrowing.Api.Migrations
{
    /// <inheritdoc />
    public partial class AlignModelWithPrdCanonicalScope : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CarrierPerformanceMetrics");

            migrationBuilder.DropTable(
                name: "CarrierRateCards");

            migrationBuilder.DropTable(
                name: "CountryTradeConfigs");

            migrationBuilder.DropTable(
                name: "CourierProviders");

            migrationBuilder.DropTable(
                name: "CourierRemittanceStatements");

            migrationBuilder.DropTable(
                name: "CustomsCommercialInvoices");

            migrationBuilder.DropTable(
                name: "DeliveryDrivers");

            migrationBuilder.DropTable(
                name: "DoorstepProofOfDeliveries");

            migrationBuilder.DropTable(
                name: "DriverCashReconciliations");

            migrationBuilder.DropTable(
                name: "DriverMobileSessions");

            migrationBuilder.DropTable(
                name: "DriverRunSheets");

            migrationBuilder.DropTable(
                name: "ExchangeCourierSwapManifests");

            migrationBuilder.DropTable(
                name: "FxExchangeRates");

            migrationBuilder.DropTable(
                name: "InterHubStockTransfers");

            migrationBuilder.DropTable(
                name: "ItemReturnInspections");

            migrationBuilder.DropTable(
                name: "ManifestParcelItems");

            migrationBuilder.DropTable(
                name: "OrderExchangeRequests");

            migrationBuilder.DropTable(
                name: "PickSlipItems");

            migrationBuilder.DropTable(
                name: "ProofOfDeliveryRecords");

            migrationBuilder.DropTable(
                name: "RegionalHubProximityRules");

            migrationBuilder.DropTable(
                name: "RemittanceItemMatches");

            migrationBuilder.DropTable(
                name: "RunSheetStops");

            migrationBuilder.DropTable(
                name: "ShippingManifests");

            migrationBuilder.DropTable(
                name: "StockTransferManifests");

            migrationBuilder.DropTable(
                name: "SupplierPurchaseOrders");

            migrationBuilder.DropTable(
                name: "TimesheetEntries");

            migrationBuilder.DropTable(
                name: "WarehouseRecords");

            migrationBuilder.DropTable(
                name: "WarehouseRmaInspections");

            migrationBuilder.DropTable(
                name: "CourierManifestRunSheets");

            migrationBuilder.DropTable(
                name: "WarehousePickSlips");

            migrationBuilder.EnsureSchema(
                name: "advertising");

            migrationBuilder.EnsureSchema(
                name: "customer_data");

            migrationBuilder.EnsureSchema(
                name: "automation");

            migrationBuilder.AddColumn<Guid>(
                name: "WorkspaceId",
                table: "WorkflowGraphs",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "WorkspaceId",
                table: "WorkflowExecutions",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "WorkspaceId",
                table: "Users",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "WorkspaceId",
                table: "Deals",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "WorkspaceId",
                table: "Contacts",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "WorkspaceId",
                table: "Companies",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "audience_syncs",
                schema: "advertising",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AudienceId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    TenantId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Platform = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    ValidHashesCount = table.Column<int>(type: "integer", nullable: false),
                    SyncedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_audience_syncs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "campaign_roas_snapshots",
                schema: "advertising",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CampaignId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    TenantId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Platform = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Spend = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    Revenue = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    CurrentRoas = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    RecommendedSpend = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    Action = table.Column<string>(type: "text", nullable: false),
                    RecordedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_campaign_roas_snapshots", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "conversions",
                schema: "advertising",
                columns: table => new
                {
                    EventId = table.Column<string>(type: "text", nullable: false),
                    TenantId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Platform = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    EventName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    ConversionValue = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    Currency = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    PayloadHash = table.Column<string>(type: "text", nullable: false),
                    DispatchedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    IsDeduplicated = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_conversions", x => x.EventId);
                });

            migrationBuilder.CreateTable(
                name: "identities",
                schema: "customer_data",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Type = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Value = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    CanonicalProfileId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_identities", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "identity_links",
                schema: "customer_data",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    SourceIdentifierJson = table.Column<string>(type: "jsonb", nullable: false),
                    TargetIdentifierJson = table.Column<string>(type: "jsonb", nullable: false),
                    ConfidenceScore = table.Column<double>(type: "double precision", nullable: false),
                    MatchReason = table.Column<string>(type: "text", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_identity_links", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "profiles",
                schema: "customer_data",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    CanonicalProfileId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    UnifiedTraitsJson = table.Column<string>(type: "jsonb", nullable: false),
                    FirstSeenUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    LastActiveUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_profiles", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "telemetry_traces",
                schema: "automation",
                columns: table => new
                {
                    ExecutionId = table.Column<string>(type: "text", nullable: false),
                    TenantId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    WorkflowId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    ContactId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    TotalDurationMs = table.Column<int>(type: "integer", nullable: false),
                    StepsJson = table.Column<string>(type: "jsonb", nullable: false),
                    ExecutedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_telemetry_traces", x => x.ExecutionId);
                });

            migrationBuilder.CreateTable(
                name: "visitor_sessions",
                schema: "customer_data",
                columns: table => new
                {
                    SessionId = table.Column<string>(type: "text", nullable: false),
                    TenantId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    AnonymousId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    StartedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    EndedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    PageViewsJson = table.Column<string>(type: "jsonb", nullable: false),
                    UtmParametersJson = table.Column<string>(type: "jsonb", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_visitor_sessions", x => x.SessionId);
                });

            migrationBuilder.CreateTable(
                name: "workflows",
                schema: "automation",
                columns: table => new
                {
                    WorkflowId = table.Column<string>(type: "text", nullable: false),
                    TenantId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    TriggerNodeId = table.Column<string>(type: "text", nullable: false),
                    NodesJson = table.Column<string>(type: "jsonb", nullable: false),
                    EdgesJson = table.Column<string>(type: "jsonb", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_workflows", x => x.WorkflowId);
                });

            migrationBuilder.CreateTable(
                name: "Workspaces",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Slug = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Status = table.Column<string>(type: "text", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Workspaces", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_audience_syncs_TenantId_AudienceId",
                schema: "advertising",
                table: "audience_syncs",
                columns: new[] { "TenantId", "AudienceId" });

            migrationBuilder.CreateIndex(
                name: "IX_audience_syncs_TenantId_Platform",
                schema: "advertising",
                table: "audience_syncs",
                columns: new[] { "TenantId", "Platform" });

            migrationBuilder.CreateIndex(
                name: "IX_campaign_roas_snapshots_TenantId_CampaignId",
                schema: "advertising",
                table: "campaign_roas_snapshots",
                columns: new[] { "TenantId", "CampaignId" });

            migrationBuilder.CreateIndex(
                name: "IX_campaign_roas_snapshots_TenantId_Platform",
                schema: "advertising",
                table: "campaign_roas_snapshots",
                columns: new[] { "TenantId", "Platform" });

            migrationBuilder.CreateIndex(
                name: "IX_conversions_TenantId_Platform_EventId",
                schema: "advertising",
                table: "conversions",
                columns: new[] { "TenantId", "Platform", "EventId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_identities_TenantId_CanonicalProfileId",
                schema: "customer_data",
                table: "identities",
                columns: new[] { "TenantId", "CanonicalProfileId" });

            migrationBuilder.CreateIndex(
                name: "IX_identities_TenantId_Type_Value",
                schema: "customer_data",
                table: "identities",
                columns: new[] { "TenantId", "Type", "Value" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_identity_links_TenantId",
                schema: "customer_data",
                table: "identity_links",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_profiles_TenantId_CanonicalProfileId",
                schema: "customer_data",
                table: "profiles",
                columns: new[] { "TenantId", "CanonicalProfileId" });

            migrationBuilder.CreateIndex(
                name: "IX_telemetry_traces_TenantId_Status",
                schema: "automation",
                table: "telemetry_traces",
                columns: new[] { "TenantId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_telemetry_traces_TenantId_WorkflowId",
                schema: "automation",
                table: "telemetry_traces",
                columns: new[] { "TenantId", "WorkflowId" });

            migrationBuilder.CreateIndex(
                name: "IX_visitor_sessions_TenantId_AnonymousId",
                schema: "customer_data",
                table: "visitor_sessions",
                columns: new[] { "TenantId", "AnonymousId" });

            migrationBuilder.CreateIndex(
                name: "IX_workflows_TenantId_WorkflowId",
                schema: "automation",
                table: "workflows",
                columns: new[] { "TenantId", "WorkflowId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Workspaces_TenantId_Slug",
                table: "Workspaces",
                columns: new[] { "TenantId", "Slug" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "audience_syncs",
                schema: "advertising");

            migrationBuilder.DropTable(
                name: "campaign_roas_snapshots",
                schema: "advertising");

            migrationBuilder.DropTable(
                name: "conversions",
                schema: "advertising");

            migrationBuilder.DropTable(
                name: "identities",
                schema: "customer_data");

            migrationBuilder.DropTable(
                name: "identity_links",
                schema: "customer_data");

            migrationBuilder.DropTable(
                name: "profiles",
                schema: "customer_data");

            migrationBuilder.DropTable(
                name: "telemetry_traces",
                schema: "automation");

            migrationBuilder.DropTable(
                name: "visitor_sessions",
                schema: "customer_data");

            migrationBuilder.DropTable(
                name: "workflows",
                schema: "automation");

            migrationBuilder.DropTable(
                name: "Workspaces");

            migrationBuilder.DropColumn(
                name: "WorkspaceId",
                table: "WorkflowGraphs");

            migrationBuilder.DropColumn(
                name: "WorkspaceId",
                table: "WorkflowExecutions");

            migrationBuilder.DropColumn(
                name: "WorkspaceId",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "WorkspaceId",
                table: "Deals");

            migrationBuilder.DropColumn(
                name: "WorkspaceId",
                table: "Contacts");

            migrationBuilder.DropColumn(
                name: "WorkspaceId",
                table: "Companies");

            migrationBuilder.CreateTable(
                name: "CarrierPerformanceMetrics",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AverageTransitDays = table.Column<decimal>(type: "numeric", nullable: false),
                    CourierName = table.Column<string>(type: "text", nullable: false),
                    CourierProviderId = table.Column<Guid>(type: "uuid", nullable: false),
                    DeliveredParcels = table.Column<int>(type: "integer", nullable: false),
                    DeliveryRatePercentage = table.Column<decimal>(type: "numeric", nullable: false),
                    GovernorateOrWilaya = table.Column<string>(type: "text", nullable: false),
                    LastUpdatedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ReturnFee = table.Column<decimal>(type: "numeric", nullable: false),
                    ReturnedParcels = table.Column<int>(type: "integer", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    TotalDispatchedParcels = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CarrierPerformanceMetrics", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CarrierRateCards",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    BaseFee = table.Column<decimal>(type: "numeric", nullable: false),
                    BaseWeightKg = table.Column<decimal>(type: "numeric", nullable: false),
                    CodCollectionPercentFee = table.Column<decimal>(type: "numeric", nullable: false),
                    CourierName = table.Column<string>(type: "text", nullable: false),
                    CourierProviderId = table.Column<Guid>(type: "uuid", nullable: false),
                    ExtraKgFee = table.Column<decimal>(type: "numeric", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    RemoteZoneSurcharge = table.Column<decimal>(type: "numeric", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CarrierRateCards", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CountryTradeConfigs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CountryCode = table.Column<string>(type: "text", nullable: false),
                    CountryName = table.Column<string>(type: "text", nullable: false),
                    CurrencyCode = table.Column<string>(type: "text", nullable: false),
                    DefaultVatPercentage = table.Column<decimal>(type: "numeric", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    NationalPhoneNumberLength = table.Column<int>(type: "integer", nullable: false),
                    PhonePrefix = table.Column<string>(type: "text", nullable: false),
                    StandardShippingFee = table.Column<decimal>(type: "numeric", nullable: false),
                    SupportsCod = table.Column<bool>(type: "boolean", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CountryTradeConfigs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CourierManifestRunSheets",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CourierName = table.Column<string>(type: "text", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DriverName = table.Column<string>(type: "text", nullable: true),
                    DriverPhone = table.Column<string>(type: "text", nullable: true),
                    DriverSignatureData = table.Column<string>(type: "text", nullable: true),
                    HandedOverAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ManifestReference = table.Column<string>(type: "text", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    TotalCodValue = table.Column<decimal>(type: "numeric", nullable: false),
                    TotalParcels = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CourierManifestRunSheets", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CourierProviders",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ApiCode = table.Column<string>(type: "text", nullable: false),
                    ApiEndpoint = table.Column<string>(type: "text", nullable: false),
                    ApiKey = table.Column<string>(type: "text", nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    SupportedCountries = table.Column<string>(type: "text", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CourierProviders", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CourierRemittanceStatements",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CourierName = table.Column<string>(type: "text", nullable: false),
                    CourierProviderId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    NetRemittedAmount = table.Column<decimal>(type: "numeric", nullable: false),
                    ReconciledAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ReconciledBy = table.Column<string>(type: "text", nullable: true),
                    StatementDateUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    StatementReference = table.Column<string>(type: "text", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    TotalCashCollected = table.Column<decimal>(type: "numeric", nullable: false),
                    TotalCourierFeesDeducted = table.Column<decimal>(type: "numeric", nullable: false),
                    TotalParcelsReported = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CourierRemittanceStatements", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CustomsCommercialInvoices",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ClearanceIssuedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ConsigneeAddress = table.Column<string>(type: "text", nullable: false),
                    ConsigneeCustomerName = table.Column<string>(type: "text", nullable: false),
                    ConsigneePhoneE164 = table.Column<string>(type: "text", nullable: false),
                    Currency = table.Column<string>(type: "text", nullable: false),
                    DestinationCountry = table.Column<string>(type: "text", nullable: false),
                    ExporterCountry = table.Column<string>(type: "text", nullable: false),
                    ExporterName = table.Column<string>(type: "text", nullable: false),
                    Incoterm = table.Column<string>(type: "text", nullable: false),
                    InvoiceNumber = table.Column<string>(type: "text", nullable: false),
                    ItemsJson = table.Column<string>(type: "text", nullable: false),
                    OrderId = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    TotalDeclaredCustomsValue = table.Column<decimal>(type: "numeric", nullable: false),
                    TotalWeightKg = table.Column<decimal>(type: "numeric", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CustomsCommercialInvoices", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "DeliveryDrivers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CashInHandBalance = table.Column<decimal>(type: "numeric", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    FullName = table.Column<string>(type: "text", nullable: false),
                    HubWarehouseId = table.Column<Guid>(type: "uuid", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    LicensePlate = table.Column<string>(type: "text", nullable: false),
                    PhoneNumber = table.Column<string>(type: "text", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    TotalDeliveriesCompleted = table.Column<int>(type: "integer", nullable: false),
                    VehicleType = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DeliveryDrivers", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "DoorstepProofOfDeliveries",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CashCollected = table.Column<decimal>(type: "numeric", nullable: false),
                    CustomerSignatureBase64 = table.Column<string>(type: "text", nullable: false),
                    DeliveredAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DriverId = table.Column<Guid>(type: "uuid", nullable: false),
                    GpsLatitude = table.Column<double>(type: "double precision", nullable: false),
                    GpsLongitude = table.Column<double>(type: "double precision", nullable: false),
                    OrderId = table.Column<Guid>(type: "uuid", nullable: false),
                    OrderNumber = table.Column<string>(type: "text", nullable: false),
                    ParcelPhotoUrl = table.Column<string>(type: "text", nullable: false),
                    PaymentMethod = table.Column<string>(type: "text", nullable: false),
                    RecipientName = table.Column<string>(type: "text", nullable: false),
                    RecipientRelation = table.Column<string>(type: "text", nullable: false),
                    RunSheetStopId = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DoorstepProofOfDeliveries", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "DriverCashReconciliations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ActualCashTurnedIn = table.Column<decimal>(type: "numeric", nullable: false),
                    DriverId = table.Column<Guid>(type: "uuid", nullable: false),
                    ExpectedCash = table.Column<decimal>(type: "numeric", nullable: false),
                    Notes = table.Column<string>(type: "text", nullable: false),
                    ReconciledAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ReconciledBy = table.Column<string>(type: "text", nullable: false),
                    RunSheetId = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DriverCashReconciliations", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "DriverMobileSessions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    BatteryLevelPercent = table.Column<int>(type: "integer", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DeviceModel = table.Column<string>(type: "text", nullable: false),
                    DriverId = table.Column<Guid>(type: "uuid", nullable: false),
                    DriverName = table.Column<string>(type: "text", nullable: false),
                    IsOnline = table.Column<bool>(type: "boolean", nullable: false),
                    LastGpsLatitude = table.Column<double>(type: "double precision", nullable: false),
                    LastGpsLongitude = table.Column<double>(type: "double precision", nullable: false),
                    LastSyncAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    SessionToken = table.Column<string>(type: "text", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DriverMobileSessions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "DriverRunSheets",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CompletedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DateUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DeliveredParcels = table.Column<int>(type: "integer", nullable: false),
                    DriverId = table.Column<Guid>(type: "uuid", nullable: false),
                    HubWarehouseId = table.Column<Guid>(type: "uuid", nullable: false),
                    RefusedParcels = table.Column<int>(type: "integer", nullable: false),
                    SheetNumber = table.Column<string>(type: "text", nullable: false),
                    StartedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    TotalCashActuallyCollected = table.Column<decimal>(type: "numeric", nullable: false),
                    TotalCashToCollect = table.Column<decimal>(type: "numeric", nullable: false),
                    TotalParcels = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DriverRunSheets", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ExchangeCourierSwapManifests",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    BundledSwapDeliveryFee = table.Column<decimal>(type: "numeric", nullable: false),
                    CashToCollectOnDelivery = table.Column<decimal>(type: "numeric", nullable: false),
                    CourierName = table.Column<string>(type: "text", nullable: false),
                    CourierProviderId = table.Column<Guid>(type: "uuid", nullable: false),
                    CustomerAddress = table.Column<string>(type: "text", nullable: false),
                    DeliveredAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DispatchedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ExchangeRequestId = table.Column<Guid>(type: "uuid", nullable: false),
                    Governorate = table.Column<string>(type: "text", nullable: false),
                    HandoverNewParcelConfirmed = table.Column<bool>(type: "boolean", nullable: false),
                    ReturnParcelCollectedConfirmed = table.Column<bool>(type: "boolean", nullable: false),
                    SwapTrackingBarcode = table.Column<string>(type: "text", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ExchangeCourierSwapManifests", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "FxExchangeRates",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    BaseCurrency = table.Column<string>(type: "text", nullable: false),
                    IsPegged = table.Column<bool>(type: "boolean", nullable: false),
                    LastUpdatedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Rate = table.Column<decimal>(type: "numeric", nullable: false),
                    TargetCurrency = table.Column<string>(type: "text", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FxExchangeRates", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "InterHubStockTransfers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DestinationWarehouseId = table.Column<Guid>(type: "uuid", nullable: false),
                    DestinationWarehouseName = table.Column<string>(type: "text", nullable: false),
                    DispatchedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ItemsJson = table.Column<string>(type: "text", nullable: false),
                    ReceivedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    SourceWarehouseId = table.Column<Guid>(type: "uuid", nullable: false),
                    SourceWarehouseName = table.Column<string>(type: "text", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    TransferNumber = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InterHubStockTransfers", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ItemReturnInspections",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ExchangeRequestId = table.Column<Guid>(type: "uuid", nullable: false),
                    InspectedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    InspectorNotes = table.Column<string>(type: "text", nullable: false),
                    QualityGrade = table.Column<int>(type: "integer", nullable: false),
                    RestockedToPhysicalWms = table.Column<bool>(type: "boolean", nullable: false),
                    ReturnedSku = table.Column<string>(type: "text", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ItemReturnInspections", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "OrderExchangeRequests",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AdditionalCashAdjustment = table.Column<decimal>(type: "numeric", nullable: false),
                    CompletedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CustomerName = table.Column<string>(type: "text", nullable: false),
                    CustomerPhone = table.Column<string>(type: "text", nullable: false),
                    OriginalOrderId = table.Column<Guid>(type: "uuid", nullable: false),
                    OriginalProductId = table.Column<Guid>(type: "uuid", nullable: false),
                    OriginalSku = table.Column<string>(type: "text", nullable: false),
                    OriginalTrackingNumber = table.Column<string>(type: "text", nullable: false),
                    OriginalVariantDescription = table.Column<string>(type: "text", nullable: false),
                    Reason = table.Column<string>(type: "text", nullable: false),
                    ReplacementProductId = table.Column<Guid>(type: "uuid", nullable: false),
                    ReplacementSku = table.Column<string>(type: "text", nullable: false),
                    ReplacementVariantDescription = table.Column<string>(type: "text", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    SwapManifestId = table.Column<Guid>(type: "uuid", nullable: true),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OrderExchangeRequests", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ProofOfDeliveryRecords",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CapturedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CustomerSignatureData = table.Column<string>(type: "text", nullable: false),
                    DoorstepPhotoUrl = table.Column<string>(type: "text", nullable: false),
                    GeoLatitude = table.Column<double>(type: "double precision", nullable: true),
                    GeoLongitude = table.Column<double>(type: "double precision", nullable: true),
                    RunSheetStopId = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProofOfDeliveryRecords", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "RegionalHubProximityRules",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    GovernorateOrWilaya = table.Column<string>(type: "text", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    Priority = table.Column<int>(type: "integer", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    WarehouseId = table.Column<Guid>(type: "uuid", nullable: false),
                    WarehouseName = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RegionalHubProximityRules", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "RemittanceItemMatches",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CourierFeeDeducted = table.Column<decimal>(type: "numeric", nullable: false),
                    DifferenceAmount = table.Column<decimal>(type: "numeric", nullable: false),
                    DiscrepancyType = table.Column<int>(type: "integer", nullable: false),
                    DisputeTicketNumber = table.Column<string>(type: "text", nullable: true),
                    HoldbackAmount = table.Column<decimal>(type: "numeric", nullable: false),
                    IsResolved = table.Column<bool>(type: "boolean", nullable: false),
                    OrderId = table.Column<Guid>(type: "uuid", nullable: true),
                    ReportedCashCollected = table.Column<decimal>(type: "numeric", nullable: false),
                    ReportedStatus = table.Column<string>(type: "text", nullable: false),
                    ResolutionNotes = table.Column<string>(type: "text", nullable: true),
                    StatementId = table.Column<Guid>(type: "uuid", nullable: false),
                    SystemExpectedAmount = table.Column<decimal>(type: "numeric", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    TrackingNumber = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RemittanceItemMatches", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "RunSheetStops",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ActualPaymentCollected = table.Column<decimal>(type: "numeric", nullable: false),
                    AmountToCollect = table.Column<decimal>(type: "numeric", nullable: false),
                    CompletedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CustomerName = table.Column<string>(type: "text", nullable: false),
                    CustomerPhone = table.Column<string>(type: "text", nullable: false),
                    Delegation = table.Column<string>(type: "text", nullable: false),
                    DeliveredAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    FailureReason = table.Column<string>(type: "text", nullable: true),
                    Governorate = table.Column<string>(type: "text", nullable: false),
                    OrderId = table.Column<Guid>(type: "uuid", nullable: false),
                    OrderNumber = table.Column<string>(type: "text", nullable: false),
                    PaymentMethodAtDoorstep = table.Column<string>(type: "text", nullable: false),
                    ProofOfDeliveryId = table.Column<Guid>(type: "uuid", nullable: true),
                    RunSheetId = table.Column<Guid>(type: "uuid", nullable: false),
                    ShippingAddress = table.Column<string>(type: "text", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    StopSequence = table.Column<int>(type: "integer", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RunSheetStops", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ShippingManifests",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CourierName = table.Column<string>(type: "text", nullable: false),
                    CourierProviderId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ExpectedFreightCost = table.Column<decimal>(type: "numeric", nullable: false),
                    HandedOverAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ManifestNumber = table.Column<string>(type: "text", nullable: false),
                    OrderCount = table.Column<int>(type: "integer", nullable: false),
                    OrderIdsJson = table.Column<string>(type: "text", nullable: false),
                    Status = table.Column<string>(type: "text", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    TotalCashAmount = table.Column<decimal>(type: "numeric", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ShippingManifests", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "StockTransferManifests",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DestinationWarehouseId = table.Column<Guid>(type: "uuid", nullable: false),
                    DestinationWarehouseName = table.Column<string>(type: "text", nullable: false),
                    ManifestNumber = table.Column<string>(type: "text", nullable: false),
                    Quantity = table.Column<int>(type: "integer", nullable: false),
                    Sku = table.Column<string>(type: "text", nullable: false),
                    SourceWarehouseId = table.Column<Guid>(type: "uuid", nullable: false),
                    SourceWarehouseName = table.Column<string>(type: "text", nullable: false),
                    Status = table.Column<string>(type: "text", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StockTransferManifests", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SupplierPurchaseOrders",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Currency = table.Column<string>(type: "text", nullable: false),
                    DestinationWarehouseId = table.Column<Guid>(type: "uuid", nullable: false),
                    ItemsJson = table.Column<string>(type: "text", nullable: false),
                    PoNumber = table.Column<string>(type: "text", nullable: false),
                    ReceivedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Status = table.Column<string>(type: "text", nullable: false),
                    SupplierName = table.Column<string>(type: "text", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    TotalCost = table.Column<decimal>(type: "numeric", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SupplierPurchaseOrders", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "TimesheetEntries",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ApprovedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ApprovedBy = table.Column<string>(type: "text", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DealId = table.Column<Guid>(type: "uuid", nullable: true),
                    DealTitle = table.Column<string>(type: "text", nullable: true),
                    Description = table.Column<string>(type: "text", nullable: false),
                    HourlyRate = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    HoursWorked = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    Status = table.Column<string>(type: "text", nullable: false),
                    TaskId = table.Column<Guid>(type: "uuid", nullable: true),
                    TaskTitle = table.Column<string>(type: "text", nullable: true),
                    TeamMemberId = table.Column<Guid>(type: "uuid", nullable: false),
                    TeamMemberName = table.Column<string>(type: "text", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    TotalLaborCost = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    WorkDateUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TimesheetEntries", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "WarehousePickSlips",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AssignedPickerId = table.Column<string>(type: "text", nullable: true),
                    CompletedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    OrderId = table.Column<Guid>(type: "uuid", nullable: false),
                    OrderNumber = table.Column<string>(type: "text", nullable: false),
                    PickSlipNumber = table.Column<string>(type: "text", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    WarehouseId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WarehousePickSlips", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "WarehouseRecords",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Address = table.Column<string>(type: "text", nullable: false),
                    CapacityUnits = table.Column<int>(type: "integer", nullable: false),
                    City = table.Column<string>(type: "text", nullable: false),
                    Code = table.Column<string>(type: "text", nullable: false),
                    Country = table.Column<string>(type: "text", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    Latitude = table.Column<double>(type: "double precision", nullable: false),
                    Longitude = table.Column<double>(type: "double precision", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    RowVersion = table.Column<long>(type: "bigint", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WarehouseRecords", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "WarehouseRmaInspections",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Condition = table.Column<int>(type: "integer", nullable: false),
                    InspectedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    InspectorId = table.Column<string>(type: "text", nullable: false),
                    InspectorNotes = table.Column<string>(type: "text", nullable: true),
                    OrderId = table.Column<Guid>(type: "uuid", nullable: false),
                    RestockedToWarehouseId = table.Column<Guid>(type: "uuid", nullable: true),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    TrackingNumber = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WarehouseRmaInspections", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ManifestParcelItems",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CodAmount = table.Column<decimal>(type: "numeric", nullable: false),
                    CourierManifestRunSheetId = table.Column<Guid>(type: "uuid", nullable: true),
                    CustomerName = table.Column<string>(type: "text", nullable: false),
                    Governorate = table.Column<string>(type: "text", nullable: false),
                    ManifestId = table.Column<Guid>(type: "uuid", nullable: false),
                    OrderId = table.Column<Guid>(type: "uuid", nullable: false),
                    ScannedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    TrackingNumber = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ManifestParcelItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ManifestParcelItems_CourierManifestRunSheets_CourierManifes~",
                        column: x => x.CourierManifestRunSheetId,
                        principalTable: "CourierManifestRunSheets",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "PickSlipItems",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Barcode = table.Column<string>(type: "text", nullable: false),
                    PickSlipId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProductName = table.Column<string>(type: "text", nullable: false),
                    QuantityPicked = table.Column<int>(type: "integer", nullable: false),
                    QuantityRequested = table.Column<int>(type: "integer", nullable: false),
                    Sku = table.Column<string>(type: "text", nullable: false),
                    WarehousePickSlipId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PickSlipItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PickSlipItems_WarehousePickSlips_WarehousePickSlipId",
                        column: x => x.WarehousePickSlipId,
                        principalTable: "WarehousePickSlips",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_ManifestParcelItems_CourierManifestRunSheetId",
                table: "ManifestParcelItems",
                column: "CourierManifestRunSheetId");

            migrationBuilder.CreateIndex(
                name: "IX_PickSlipItems_WarehousePickSlipId",
                table: "PickSlipItems",
                column: "WarehousePickSlipId");

            migrationBuilder.CreateIndex(
                name: "IX_StockTransferManifests_TenantId_ManifestNumber",
                table: "StockTransferManifests",
                columns: new[] { "TenantId", "ManifestNumber" });

            migrationBuilder.CreateIndex(
                name: "IX_TimesheetEntries_TenantId_DealId",
                table: "TimesheetEntries",
                columns: new[] { "TenantId", "DealId" });

            migrationBuilder.CreateIndex(
                name: "IX_TimesheetEntries_TenantId_TeamMemberId",
                table: "TimesheetEntries",
                columns: new[] { "TenantId", "TeamMemberId" });

            migrationBuilder.CreateIndex(
                name: "IX_WarehouseRecords_TenantId_Code",
                table: "WarehouseRecords",
                columns: new[] { "TenantId", "Code" });

            migrationBuilder.CreateIndex(
                name: "IX_WarehouseRecords_TenantId_IsActive_Latitude_Longitude",
                table: "WarehouseRecords",
                columns: new[] { "TenantId", "IsActive", "Latitude", "Longitude" });
        }
    }
}
