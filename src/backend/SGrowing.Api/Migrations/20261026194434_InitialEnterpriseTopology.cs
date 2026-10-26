using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SGrowing.Api.Migrations
{
    /// <inheritdoc />
    public partial class InitialEnterpriseTopology : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AbandonedCartRecords",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    StorefrontConnectionId = table.Column<Guid>(type: "uuid", nullable: false),
                    ExternalCartId = table.Column<string>(type: "text", nullable: false),
                    CustomerEmail = table.Column<string>(type: "text", nullable: false),
                    CustomerPhone = table.Column<string>(type: "text", nullable: true),
                    CartTotal = table.Column<decimal>(type: "numeric", nullable: false),
                    RecoveryUrl = table.Column<string>(type: "text", nullable: false),
                    ItemsJson = table.Column<string>(type: "text", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    RecoveryAttempts = table.Column<int>(type: "integer", nullable: false),
                    DetectedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    RecoveredAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AbandonedCartRecords", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Activities",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    ContactId = table.Column<Guid>(type: "uuid", nullable: false),
                    Type = table.Column<int>(type: "integer", nullable: false),
                    PayloadJson = table.Column<string>(type: "text", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Activities", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AffiliateReferralCodes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatorId = table.Column<Guid>(type: "uuid", nullable: false),
                    Code = table.Column<string>(type: "text", nullable: false),
                    DiscountType = table.Column<string>(type: "text", nullable: false),
                    DiscountValue = table.Column<decimal>(type: "numeric", nullable: false),
                    UsageCount = table.Column<int>(type: "integer", nullable: false),
                    TotalReferredGmv = table.Column<decimal>(type: "numeric", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AffiliateReferralCodes", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AiDrafts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    ConversationId = table.Column<Guid>(type: "uuid", nullable: false),
                    SuggestedReply = table.Column<string>(type: "text", nullable: false),
                    ConfidenceScore = table.Column<float>(type: "real", nullable: false),
                    SourcesJson = table.Column<string>(type: "text", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AiDrafts", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AppointmentBookings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    SlotId = table.Column<Guid>(type: "uuid", nullable: false),
                    ContactId = table.Column<Guid>(type: "uuid", nullable: false),
                    ScheduledTimeUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    MeetingUrl = table.Column<string>(type: "text", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AppointmentBookings", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AppointmentSlots",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    Title = table.Column<string>(type: "text", nullable: false),
                    DurationMinutes = table.Column<int>(type: "integer", nullable: false),
                    HostAgentName = table.Column<string>(type: "text", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AppointmentSlots", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AttributionTouchpoints",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    ContactId = table.Column<Guid>(type: "uuid", nullable: false),
                    DealId = table.Column<Guid>(type: "uuid", nullable: true),
                    Channel = table.Column<string>(type: "text", nullable: false),
                    CampaignName = table.Column<string>(type: "text", nullable: false),
                    UtmSource = table.Column<string>(type: "text", nullable: true),
                    UtmMedium = table.Column<string>(type: "text", nullable: true),
                    UtmCampaign = table.Column<string>(type: "text", nullable: true),
                    Stage = table.Column<int>(type: "integer", nullable: false),
                    Cost = table.Column<decimal>(type: "numeric", nullable: false),
                    OccurredAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AttributionTouchpoints", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AutomatedRestockOrders",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    SupplierId = table.Column<Guid>(type: "uuid", nullable: false),
                    SupplierName = table.Column<string>(type: "text", nullable: false),
                    ProductId = table.Column<Guid>(type: "uuid", nullable: false),
                    Sku = table.Column<string>(type: "text", nullable: false),
                    ProductName = table.Column<string>(type: "text", nullable: false),
                    QuantityToOrder = table.Column<int>(type: "integer", nullable: false),
                    UnitPurchaseCost = table.Column<decimal>(type: "numeric", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    PlacedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ExpectedDeliveryDateUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AutomatedRestockOrders", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "BlacklistEntries",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    Type = table.Column<string>(type: "text", nullable: false),
                    Value = table.Column<string>(type: "text", nullable: false),
                    Reason = table.Column<string>(type: "text", nullable: false),
                    AddedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BlacklistEntries", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "BrandThemeTokens",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    StorefrontSlug = table.Column<string>(type: "text", nullable: false),
                    BrandName = table.Column<string>(type: "text", nullable: false),
                    PrimaryColorHex = table.Column<string>(type: "text", nullable: false),
                    SecondaryColorHex = table.Column<string>(type: "text", nullable: false),
                    FontFamily = table.Column<string>(type: "text", nullable: false),
                    LogoUrl = table.Column<string>(type: "text", nullable: false),
                    FaviconUrl = table.Column<string>(type: "text", nullable: false),
                    SupportEmail = table.Column<string>(type: "text", nullable: false),
                    SupportPhone = table.Column<string>(type: "text", nullable: false),
                    SmsSenderId = table.Column<string>(type: "text", nullable: false),
                    WhatsAppSenderPhone = table.Column<string>(type: "text", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BrandThemeTokens", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "BroadcastCampaigns",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    TemplateId = table.Column<Guid>(type: "uuid", nullable: false),
                    TargetTag = table.Column<string>(type: "text", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    TotalRecipients = table.Column<int>(type: "integer", nullable: false),
                    SentCount = table.Column<int>(type: "integer", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BroadcastCampaigns", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CadenceSteps",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    CampaignId = table.Column<Guid>(type: "uuid", nullable: false),
                    StepNumber = table.Column<int>(type: "integer", nullable: false),
                    DelayDays = table.Column<int>(type: "integer", nullable: false),
                    Channel = table.Column<int>(type: "integer", nullable: false),
                    SubjectTemplate = table.Column<string>(type: "text", nullable: false),
                    BodyTemplate = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CadenceSteps", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CadenceTouchpoints",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    CampaignId = table.Column<Guid>(type: "uuid", nullable: false),
                    StepNumber = table.Column<int>(type: "integer", nullable: false),
                    Channel = table.Column<int>(type: "integer", nullable: false),
                    DelayDays = table.Column<int>(type: "integer", nullable: false),
                    SubjectTemplate = table.Column<string>(type: "text", nullable: true),
                    BodyTemplate = table.Column<string>(type: "text", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CadenceTouchpoints", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CallOutcomeRecords",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    OrderId = table.Column<Guid>(type: "uuid", nullable: false),
                    AgentId = table.Column<string>(type: "text", nullable: false),
                    AgentName = table.Column<string>(type: "text", nullable: false),
                    Outcome = table.Column<int>(type: "integer", nullable: false),
                    Notes = table.Column<string>(type: "text", nullable: true),
                    ScheduledCallbackUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    RecordedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CallOutcomeRecords", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CarrierPerformanceMetrics",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    CourierProviderId = table.Column<Guid>(type: "uuid", nullable: false),
                    CourierName = table.Column<string>(type: "text", nullable: false),
                    GovernorateOrWilaya = table.Column<string>(type: "text", nullable: false),
                    TotalDispatchedParcels = table.Column<int>(type: "integer", nullable: false),
                    DeliveredParcels = table.Column<int>(type: "integer", nullable: false),
                    ReturnedParcels = table.Column<int>(type: "integer", nullable: false),
                    DeliveryRatePercentage = table.Column<decimal>(type: "numeric", nullable: false),
                    AverageTransitDays = table.Column<decimal>(type: "numeric", nullable: false),
                    ReturnFee = table.Column<decimal>(type: "numeric", nullable: false),
                    LastUpdatedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
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
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    CourierProviderId = table.Column<Guid>(type: "uuid", nullable: false),
                    CourierName = table.Column<string>(type: "text", nullable: false),
                    BaseWeightKg = table.Column<decimal>(type: "numeric", nullable: false),
                    BaseFee = table.Column<decimal>(type: "numeric", nullable: false),
                    ExtraKgFee = table.Column<decimal>(type: "numeric", nullable: false),
                    RemoteZoneSurcharge = table.Column<decimal>(type: "numeric", nullable: false),
                    CodCollectionPercentFee = table.Column<decimal>(type: "numeric", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CarrierRateCards", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CarrierRemittanceScheduleConfigs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    CourierProviderId = table.Column<Guid>(type: "uuid", nullable: false),
                    CourierName = table.Column<string>(type: "text", nullable: false),
                    Frequency = table.Column<int>(type: "integer", nullable: false),
                    RemittanceDayOfWeek = table.Column<int>(type: "integer", nullable: false),
                    AverageLagDays = table.Column<int>(type: "integer", nullable: false),
                    ContractFeePerParcel = table.Column<decimal>(type: "numeric", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CarrierRemittanceScheduleConfigs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ChannelCredentials",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    ChannelType = table.Column<string>(type: "text", nullable: false),
                    EncryptedAccessToken = table.Column<string>(type: "text", nullable: false),
                    EncryptedAppSecret = table.Column<string>(type: "text", nullable: false),
                    EncryptedRefreshToken = table.Column<string>(type: "text", nullable: true),
                    EncryptedSigningKey = table.Column<string>(type: "text", nullable: true),
                    NonceBase64 = table.Column<string>(type: "text", nullable: false),
                    TagBase64 = table.Column<string>(type: "text", nullable: false),
                    ExpiresAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ChannelCredentials", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CheckoutLinks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    ContactId = table.Column<Guid>(type: "uuid", nullable: false),
                    ConversationId = table.Column<Guid>(type: "uuid", nullable: false),
                    Title = table.Column<string>(type: "text", nullable: false),
                    Amount = table.Column<decimal>(type: "numeric", nullable: false),
                    Currency = table.Column<string>(type: "text", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    PaymentUrl = table.Column<string>(type: "text", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CheckoutLinks", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CodOrders",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    OrderNumber = table.Column<string>(type: "text", nullable: false),
                    CustomerName = table.Column<string>(type: "text", nullable: false),
                    PhoneNumber = table.Column<string>(type: "text", nullable: false),
                    AlternatePhone = table.Column<string>(type: "text", nullable: true),
                    ShippingAddress = table.Column<string>(type: "text", nullable: false),
                    GovernorateOrWilaya = table.Column<string>(type: "text", nullable: false),
                    DelegationOrCommune = table.Column<string>(type: "text", nullable: false),
                    CountryCode = table.Column<string>(type: "text", nullable: false),
                    TotalAmount = table.Column<decimal>(type: "numeric", nullable: false),
                    ShippingFee = table.Column<decimal>(type: "numeric", nullable: false),
                    CodCurrency = table.Column<string>(type: "text", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    RiskLevel = table.Column<int>(type: "integer", nullable: false),
                    RiskScore = table.Column<int>(type: "integer", nullable: false),
                    FraudFlagsJson = table.Column<string>(type: "text", nullable: false),
                    ConfirmationChannel = table.Column<int>(type: "integer", nullable: true),
                    ConfirmedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ItemsJson = table.Column<string>(type: "text", nullable: false),
                    Notes = table.Column<string>(type: "text", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CodOrders", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CommissionEscrowLedgers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatorId = table.Column<Guid>(type: "uuid", nullable: false),
                    OrderId = table.Column<Guid>(type: "uuid", nullable: false),
                    ReferralCode = table.Column<string>(type: "text", nullable: false),
                    OrderTrackingNumber = table.Column<string>(type: "text", nullable: false),
                    OrderAmount = table.Column<decimal>(type: "numeric", nullable: false),
                    CommissionAmount = table.Column<decimal>(type: "numeric", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    HoldReason = table.Column<string>(type: "text", nullable: false),
                    VoidReason = table.Column<string>(type: "text", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ClearedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    VoidedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    PaidOutAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CommissionEscrowLedgers", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Companies",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Domain = table.Column<string>(type: "text", nullable: false),
                    Industry = table.Column<string>(type: "text", nullable: true),
                    EmployeeCount = table.Column<int>(type: "integer", nullable: false),
                    AnnualRevenue = table.Column<decimal>(type: "numeric", nullable: false),
                    ParentCompanyId = table.Column<Guid>(type: "uuid", nullable: true),
                    CustomFieldsJson = table.Column<string>(type: "text", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Companies", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Contacts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    FirstName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    LastName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Email = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    Phone = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    LifecycleStage = table.Column<int>(type: "integer", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uuid", nullable: true),
                    CustomFieldsJson = table.Column<string>(type: "text", nullable: false),
                    Tags = table.Column<List<string>>(type: "text[]", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Contacts", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Conversations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    InboxId = table.Column<Guid>(type: "uuid", nullable: false),
                    ContactId = table.Column<Guid>(type: "uuid", nullable: false),
                    AssigneeId = table.Column<Guid>(type: "uuid", nullable: true),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    Priority = table.Column<int>(type: "integer", nullable: false),
                    AiMode = table.Column<int>(type: "integer", nullable: false),
                    IsSlaBreached = table.Column<bool>(type: "boolean", nullable: false),
                    FirstResponseAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastMessageAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Conversations", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ConversionTrackingConfigs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    Platform = table.Column<int>(type: "integer", nullable: false),
                    PixelOrDatasetId = table.Column<string>(type: "text", nullable: false),
                    ApiAccessToken = table.Column<string>(type: "text", nullable: false),
                    TestEventCode = table.Column<string>(type: "text", nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ConversionTrackingConfigs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CountryTradeConfigs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    CountryCode = table.Column<string>(type: "text", nullable: false),
                    CountryName = table.Column<string>(type: "text", nullable: false),
                    CurrencyCode = table.Column<string>(type: "text", nullable: false),
                    PhonePrefix = table.Column<string>(type: "text", nullable: false),
                    NationalPhoneNumberLength = table.Column<int>(type: "integer", nullable: false),
                    DefaultVatPercentage = table.Column<decimal>(type: "numeric", nullable: false),
                    StandardShippingFee = table.Column<decimal>(type: "numeric", nullable: false),
                    SupportsCod = table.Column<bool>(type: "boolean", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CountryTradeConfigs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CourierFloatValuations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    InTransitParcelCount = table.Column<int>(type: "integer", nullable: false),
                    InTransitCashValue = table.Column<decimal>(type: "numeric", nullable: false),
                    DeliveredUnremittedParcelCount = table.Column<int>(type: "integer", nullable: false),
                    DeliveredUnremittedCashValue = table.Column<decimal>(type: "numeric", nullable: false),
                    EstimatedCourierFees = table.Column<decimal>(type: "numeric", nullable: false),
                    AverageDaysSalesOutstanding = table.Column<decimal>(type: "numeric", nullable: false),
                    CalculatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CourierFloatValuations", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CourierManifestRunSheets",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    CourierName = table.Column<string>(type: "text", nullable: false),
                    ManifestReference = table.Column<string>(type: "text", nullable: false),
                    DriverName = table.Column<string>(type: "text", nullable: true),
                    DriverPhone = table.Column<string>(type: "text", nullable: true),
                    DriverSignatureData = table.Column<string>(type: "text", nullable: true),
                    TotalParcels = table.Column<int>(type: "integer", nullable: false),
                    TotalCodValue = table.Column<decimal>(type: "numeric", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    HandedOverAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
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
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    ApiCode = table.Column<string>(type: "text", nullable: false),
                    SupportedCountries = table.Column<string>(type: "text", nullable: false),
                    ApiEndpoint = table.Column<string>(type: "text", nullable: false),
                    ApiKey = table.Column<string>(type: "text", nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false)
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
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    CourierProviderId = table.Column<Guid>(type: "uuid", nullable: false),
                    CourierName = table.Column<string>(type: "text", nullable: false),
                    StatementReference = table.Column<string>(type: "text", nullable: false),
                    StatementDateUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    TotalParcelsReported = table.Column<int>(type: "integer", nullable: false),
                    TotalCashCollected = table.Column<decimal>(type: "numeric", nullable: false),
                    TotalCourierFeesDeducted = table.Column<decimal>(type: "numeric", nullable: false),
                    NetRemittedAmount = table.Column<decimal>(type: "numeric", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ReconciledAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ReconciledBy = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CourierRemittanceStatements", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CreatorPayoutRecords",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatorId = table.Column<Guid>(type: "uuid", nullable: false),
                    PayoutBatchId = table.Column<Guid>(type: "uuid", nullable: false),
                    Amount = table.Column<decimal>(type: "numeric", nullable: false),
                    Currency = table.Column<string>(type: "text", nullable: false),
                    PaymentMethod = table.Column<string>(type: "text", nullable: false),
                    DestinationAccount = table.Column<string>(type: "text", nullable: false),
                    TransactionReference = table.Column<string>(type: "text", nullable: true),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    IncludedOrdersCount = table.Column<int>(type: "integer", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ExecutedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CreatorPayoutRecords", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CreatorProfiles",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    FullName = table.Column<string>(type: "text", nullable: false),
                    Handle = table.Column<string>(type: "text", nullable: false),
                    Email = table.Column<string>(type: "text", nullable: false),
                    PhoneNumber = table.Column<string>(type: "text", nullable: false),
                    Platform = table.Column<int>(type: "integer", nullable: false),
                    CommissionType = table.Column<int>(type: "integer", nullable: false),
                    CommissionRate = table.Column<decimal>(type: "numeric", nullable: false),
                    MinimumPayoutThreshold = table.Column<decimal>(type: "numeric", nullable: false),
                    PaymentMethod = table.Column<string>(type: "text", nullable: false),
                    PayoutDetails = table.Column<string>(type: "text", nullable: false),
                    TotalEarned = table.Column<decimal>(type: "numeric", nullable: false),
                    PendingEscrowBalance = table.Column<decimal>(type: "numeric", nullable: false),
                    TotalPaidOut = table.Column<decimal>(type: "numeric", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CreatorProfiles", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CustomerEvents",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    ContactId = table.Column<Guid>(type: "uuid", nullable: false),
                    EventName = table.Column<string>(type: "text", nullable: false),
                    PayloadJson = table.Column<string>(type: "text", nullable: false),
                    OccurredAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CustomerEvents", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CustomerIdentityLinks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    UnifiedCustomerId = table.Column<Guid>(type: "uuid", nullable: false),
                    IdentityType = table.Column<int>(type: "integer", nullable: false),
                    IdentityValue = table.Column<string>(type: "text", nullable: false),
                    LinkedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CustomerIdentityLinks", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CustomerJourneys",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    TriggerEvent = table.Column<string>(type: "text", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CustomerJourneys", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CustomerReputationScorecards",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    CustomerPhone = table.Column<string>(type: "text", nullable: false),
                    CustomerEmail = table.Column<string>(type: "text", nullable: true),
                    ReputationScore = table.Column<int>(type: "integer", nullable: false),
                    Tier = table.Column<int>(type: "integer", nullable: false),
                    TotalOrdersCount = table.Column<int>(type: "integer", nullable: false),
                    DeliveredOrdersCount = table.Column<int>(type: "integer", nullable: false),
                    RefusedReturnsCount = table.Column<int>(type: "integer", nullable: false),
                    CancelledOrdersCount = table.Column<int>(type: "integer", nullable: false),
                    TotalSpentAmount = table.Column<decimal>(type: "numeric", nullable: false),
                    DeliverySuccessRatePercentage = table.Column<decimal>(type: "numeric", nullable: false),
                    RequiresMicroDeposit = table.Column<bool>(type: "boolean", nullable: false),
                    LastEvaluatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CustomerReputationScorecards", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CustomerTimelineEvents",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    UnifiedCustomerId = table.Column<Guid>(type: "uuid", nullable: false),
                    EventType = table.Column<string>(type: "text", nullable: false),
                    Summary = table.Column<string>(type: "text", nullable: false),
                    PayloadJson = table.Column<string>(type: "text", nullable: false),
                    OccurredAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CustomerTimelineEvents", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CustomsCommercialInvoices",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    OrderId = table.Column<Guid>(type: "uuid", nullable: false),
                    InvoiceNumber = table.Column<string>(type: "text", nullable: false),
                    ExporterName = table.Column<string>(type: "text", nullable: false),
                    ExporterCountry = table.Column<string>(type: "text", nullable: false),
                    DestinationCountry = table.Column<string>(type: "text", nullable: false),
                    ConsigneeCustomerName = table.Column<string>(type: "text", nullable: false),
                    ConsigneePhoneE164 = table.Column<string>(type: "text", nullable: false),
                    ConsigneeAddress = table.Column<string>(type: "text", nullable: false),
                    Currency = table.Column<string>(type: "text", nullable: false),
                    TotalDeclaredCustomsValue = table.Column<decimal>(type: "numeric", nullable: false),
                    TotalWeightKg = table.Column<decimal>(type: "numeric", nullable: false),
                    Incoterm = table.Column<string>(type: "text", nullable: false),
                    ItemsJson = table.Column<string>(type: "text", nullable: false),
                    ClearanceIssuedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CustomsCommercialInvoices", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Deals",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    ContactId = table.Column<Guid>(type: "uuid", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uuid", nullable: true),
                    Title = table.Column<string>(type: "text", nullable: false),
                    Amount = table.Column<decimal>(type: "numeric", nullable: false),
                    Stage = table.Column<int>(type: "integer", nullable: false),
                    SourceCampaignId = table.Column<Guid>(type: "uuid", nullable: true),
                    Notes = table.Column<string>(type: "text", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Deals", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "DigitalContracts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uuid", nullable: true),
                    DealId = table.Column<Guid>(type: "uuid", nullable: true),
                    Title = table.Column<string>(type: "text", nullable: false),
                    ContractType = table.Column<string>(type: "text", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    SignerName = table.Column<string>(type: "text", nullable: false),
                    SignerEmail = table.Column<string>(type: "text", nullable: false),
                    ContractValue = table.Column<decimal>(type: "numeric", nullable: false),
                    SigningToken = table.Column<string>(type: "text", nullable: false),
                    SignedPdfUrl = table.Column<string>(type: "text", nullable: true),
                    SignedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DigitalContracts", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "DocumentChunks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    DocumentId = table.Column<Guid>(type: "uuid", nullable: false),
                    DocumentTitle = table.Column<string>(type: "text", nullable: false),
                    SourceUrl = table.Column<string>(type: "text", nullable: true),
                    ChunkIndex = table.Column<int>(type: "integer", nullable: false),
                    Content = table.Column<string>(type: "text", nullable: false),
                    TokenCount = table.Column<int>(type: "integer", nullable: false),
                    EmbeddingJson = table.Column<string>(type: "text", nullable: false),
                    MetadataJson = table.Column<string>(type: "text", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DocumentChunks", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "DynamicAgentTools",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    Description = table.Column<string>(type: "text", nullable: false),
                    ParametersSchemaJson = table.Column<string>(type: "text", nullable: false),
                    TargetEndpointUrl = table.Column<string>(type: "text", nullable: false),
                    HttpMethod = table.Column<string>(type: "text", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DynamicAgentTools", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ExchangeCourierSwapManifests",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    ExchangeRequestId = table.Column<Guid>(type: "uuid", nullable: false),
                    CourierProviderId = table.Column<Guid>(type: "uuid", nullable: false),
                    CourierName = table.Column<string>(type: "text", nullable: false),
                    SwapTrackingBarcode = table.Column<string>(type: "text", nullable: false),
                    CustomerAddress = table.Column<string>(type: "text", nullable: false),
                    Governorate = table.Column<string>(type: "text", nullable: false),
                    BundledSwapDeliveryFee = table.Column<decimal>(type: "numeric", nullable: false),
                    CashToCollectOnDelivery = table.Column<decimal>(type: "numeric", nullable: false),
                    HandoverNewParcelConfirmed = table.Column<bool>(type: "boolean", nullable: false),
                    ReturnParcelCollectedConfirmed = table.Column<bool>(type: "boolean", nullable: false),
                    DispatchedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DeliveredAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ExchangeCourierSwapManifests", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ExternalStoreSyncRecords",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    Platform = table.Column<int>(type: "integer", nullable: false),
                    ExternalOrderId = table.Column<string>(type: "text", nullable: false),
                    ExternalOrderNumber = table.Column<string>(type: "text", nullable: false),
                    SGrowingOrderId = table.Column<Guid>(type: "uuid", nullable: false),
                    SyncDirection = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<string>(type: "text", nullable: false),
                    PayloadJson = table.Column<string>(type: "text", nullable: false),
                    SyncedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ExternalStoreSyncRecords", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "FeatureEntitlements",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    PlanTier = table.Column<int>(type: "integer", nullable: false),
                    MaxTeamSeats = table.Column<int>(type: "integer", nullable: false),
                    MonthlyIncludedAiTokens = table.Column<long>(type: "bigint", nullable: false),
                    MonthlyIncludedVoiceMinutes = table.Column<int>(type: "integer", nullable: false),
                    MonthlyIncludedMessages = table.Column<int>(type: "integer", nullable: false),
                    IsCustomDomainEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    IsSlaEscalationEnabled = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FeatureEntitlements", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "FxExchangeRates",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    BaseCurrency = table.Column<string>(type: "text", nullable: false),
                    TargetCurrency = table.Column<string>(type: "text", nullable: false),
                    Rate = table.Column<decimal>(type: "numeric", nullable: false),
                    IsPegged = table.Column<bool>(type: "boolean", nullable: false),
                    LastUpdatedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FxExchangeRates", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "GoogleSheetsSyncConfigs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    SpreadsheetId = table.Column<string>(type: "text", nullable: false),
                    SheetTabName = table.Column<string>(type: "text", nullable: false),
                    WebhookOrEndpointUrl = table.Column<string>(type: "text", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    AutoSyncOnConfirmed = table.Column<bool>(type: "boolean", nullable: false),
                    LastSyncedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    RowsAppendedCount = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GoogleSheetsSyncConfigs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "HelpArticles",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    CategoryId = table.Column<Guid>(type: "uuid", nullable: false),
                    Title = table.Column<string>(type: "text", nullable: false),
                    Slug = table.Column<string>(type: "text", nullable: false),
                    ContentMarkdown = table.Column<string>(type: "text", nullable: false),
                    IsPublished = table.Column<bool>(type: "boolean", nullable: false),
                    ViewCount = table.Column<int>(type: "integer", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HelpArticles", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "HelpCategories",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    Slug = table.Column<string>(type: "text", nullable: false),
                    OrderIndex = table.Column<int>(type: "integer", nullable: false),
                    Icon = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HelpCategories", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Inboxes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    ChannelType = table.Column<int>(type: "integer", nullable: false),
                    ConfigJson = table.Column<string>(type: "text", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Inboxes", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "InChatCartSessions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    ConversationId = table.Column<Guid>(type: "uuid", nullable: false),
                    CustomerPhone = table.Column<string>(type: "text", nullable: false),
                    CustomerName = table.Column<string>(type: "text", nullable: false),
                    DraftOrderId = table.Column<Guid>(type: "uuid", nullable: true),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    CartItemsJson = table.Column<string>(type: "text", nullable: false),
                    TotalAmount = table.Column<decimal>(type: "numeric", nullable: false),
                    Currency = table.Column<string>(type: "text", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ConfirmedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InChatCartSessions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "InterHubStockTransfers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    TransferNumber = table.Column<string>(type: "text", nullable: false),
                    SourceWarehouseId = table.Column<Guid>(type: "uuid", nullable: false),
                    SourceWarehouseName = table.Column<string>(type: "text", nullable: false),
                    DestinationWarehouseId = table.Column<Guid>(type: "uuid", nullable: false),
                    DestinationWarehouseName = table.Column<string>(type: "text", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    ItemsJson = table.Column<string>(type: "text", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DispatchedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ReceivedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
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
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    ExchangeRequestId = table.Column<Guid>(type: "uuid", nullable: false),
                    ReturnedSku = table.Column<string>(type: "text", nullable: false),
                    QualityGrade = table.Column<int>(type: "integer", nullable: false),
                    InspectorNotes = table.Column<string>(type: "text", nullable: false),
                    RestockedToPhysicalWms = table.Column<bool>(type: "boolean", nullable: false),
                    InspectedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ItemReturnInspections", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "JourneyEnrollments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    JourneyId = table.Column<Guid>(type: "uuid", nullable: false),
                    ContactId = table.Column<Guid>(type: "uuid", nullable: false),
                    CurrentStepOrder = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    NextExecutionAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_JourneyEnrollments", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "LeadEnrichmentProfiles",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    Domain = table.Column<string>(type: "text", nullable: false),
                    CompanyName = table.Column<string>(type: "text", nullable: false),
                    Industry = table.Column<string>(type: "text", nullable: false),
                    EmployeeCountRange = table.Column<string>(type: "text", nullable: false),
                    EstimatedAnnualRevenue = table.Column<string>(type: "text", nullable: false),
                    TechStack = table.Column<string>(type: "text", nullable: false),
                    LinkedinUrl = table.Column<string>(type: "text", nullable: true),
                    TwitterHandle = table.Column<string>(type: "text", nullable: true),
                    EnrichedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LeadEnrichmentProfiles", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "LiveFlashDeals",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    SessionId = table.Column<Guid>(type: "uuid", nullable: false),
                    Sku = table.Column<string>(type: "text", nullable: false),
                    ProductName = table.Column<string>(type: "text", nullable: false),
                    TriggerKeyword = table.Column<string>(type: "text", nullable: false),
                    FlashPrice = table.Column<decimal>(type: "numeric", nullable: false),
                    OriginalPrice = table.Column<decimal>(type: "numeric", nullable: false),
                    MaxQuantity = table.Column<int>(type: "integer", nullable: false),
                    SoldQuantity = table.Column<int>(type: "integer", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LiveFlashDeals", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "LiveStreamSessions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    Platform = table.Column<int>(type: "integer", nullable: false),
                    Title = table.Column<string>(type: "text", nullable: false),
                    StreamExternalId = table.Column<string>(type: "text", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    TotalCommentsProcessed = table.Column<int>(type: "integer", nullable: false),
                    TotalOrdersGenerated = table.Column<int>(type: "integer", nullable: false),
                    StartedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    EndedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LiveStreamSessions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Messages",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    ConversationId = table.Column<Guid>(type: "uuid", nullable: false),
                    SenderType = table.Column<int>(type: "integer", nullable: false),
                    SenderId = table.Column<Guid>(type: "uuid", nullable: true),
                    ContentType = table.Column<int>(type: "integer", nullable: false),
                    Content = table.Column<string>(type: "text", nullable: false),
                    MediaUrl = table.Column<string>(type: "text", nullable: true),
                    ExternalMessageId = table.Column<string>(type: "text", nullable: true),
                    DeliveryStatus = table.Column<int>(type: "integer", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Messages", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "MetaCatalogItems",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    RetailerId = table.Column<string>(type: "text", nullable: false),
                    Title = table.Column<string>(type: "text", nullable: false),
                    Description = table.Column<string>(type: "text", nullable: false),
                    Availability = table.Column<string>(type: "text", nullable: false),
                    Condition = table.Column<string>(type: "text", nullable: false),
                    Price = table.Column<decimal>(type: "numeric", nullable: false),
                    Currency = table.Column<string>(type: "text", nullable: false),
                    Link = table.Column<string>(type: "text", nullable: false),
                    ImageLink = table.Column<string>(type: "text", nullable: false),
                    Brand = table.Column<string>(type: "text", nullable: false),
                    GoogleProductCategory = table.Column<string>(type: "text", nullable: false),
                    LastUpdatedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MetaCatalogItems", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "MetaCatalogSyncFeeds",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    CatalogId = table.Column<string>(type: "text", nullable: false),
                    FeedFormat = table.Column<string>(type: "text", nullable: false),
                    TotalProducts = table.Column<int>(type: "integer", nullable: false),
                    LastSyncedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    SyncStatus = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MetaCatalogSyncFeeds", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "MicroDepositPaymentLinks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    OrderId = table.Column<Guid>(type: "uuid", nullable: false),
                    CustomerPhone = table.Column<string>(type: "text", nullable: false),
                    DepositAmount = table.Column<decimal>(type: "numeric", nullable: false),
                    Currency = table.Column<string>(type: "text", nullable: false),
                    PaymentUrl = table.Column<string>(type: "text", nullable: false),
                    PaymentProvider = table.Column<string>(type: "text", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    PaidAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ExpiresAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MicroDepositPaymentLinks", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "OrderCollisionLocks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    OrderId = table.Column<Guid>(type: "uuid", nullable: false),
                    AgentId = table.Column<string>(type: "text", nullable: false),
                    AgentName = table.Column<string>(type: "text", nullable: false),
                    LockedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ExpiresAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OrderCollisionLocks", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "OrderExchangeRequests",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    OriginalOrderId = table.Column<Guid>(type: "uuid", nullable: false),
                    OriginalTrackingNumber = table.Column<string>(type: "text", nullable: false),
                    CustomerPhone = table.Column<string>(type: "text", nullable: false),
                    CustomerName = table.Column<string>(type: "text", nullable: false),
                    Reason = table.Column<string>(type: "text", nullable: false),
                    OriginalProductId = table.Column<Guid>(type: "uuid", nullable: false),
                    OriginalSku = table.Column<string>(type: "text", nullable: false),
                    OriginalVariantDescription = table.Column<string>(type: "text", nullable: false),
                    ReplacementProductId = table.Column<Guid>(type: "uuid", nullable: false),
                    ReplacementSku = table.Column<string>(type: "text", nullable: false),
                    ReplacementVariantDescription = table.Column<string>(type: "text", nullable: false),
                    AdditionalCashAdjustment = table.Column<decimal>(type: "numeric", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    SwapManifestId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CompletedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OrderExchangeRequests", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "OutboundCallAttempts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    CampaignId = table.Column<Guid>(type: "uuid", nullable: true),
                    OrderId = table.Column<Guid>(type: "uuid", nullable: false),
                    CustomerPhone = table.Column<string>(type: "text", nullable: false),
                    CustomerName = table.Column<string>(type: "text", nullable: false),
                    AttemptNumber = table.Column<int>(type: "integer", nullable: false),
                    State = table.Column<int>(type: "integer", nullable: false),
                    SpokenIntent = table.Column<int>(type: "integer", nullable: false),
                    SpeechTranscript = table.Column<string>(type: "text", nullable: true),
                    DtmfDigit = table.Column<string>(type: "text", nullable: true),
                    CallDurationSeconds = table.Column<int>(type: "integer", nullable: false),
                    ScheduledRetryUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CompletedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OutboundCallAttempts", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "OutboundCampaigns",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    SenderPoolIdsJson = table.Column<string>(type: "text", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OutboundCampaigns", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "OutboxMessages",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    OccurredOnUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    EventType = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Payload = table.Column<string>(type: "text", nullable: false),
                    ProcessedOnUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    RetryCount = table.Column<int>(type: "integer", nullable: false),
                    Error = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OutboxMessages", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PostPurchaseSurveys",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    OrderId = table.Column<Guid>(type: "uuid", nullable: false),
                    CustomerPhone = table.Column<string>(type: "text", nullable: false),
                    CustomerName = table.Column<string>(type: "text", nullable: false),
                    Rating = table.Column<int>(type: "integer", nullable: true),
                    FeedbackNotes = table.Column<string>(type: "text", nullable: true),
                    SentAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    RespondedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DiscountCodeIssued = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PostPurchaseSurveys", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PrivacyErasureRequests",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    ContactEmail = table.Column<string>(type: "text", nullable: false),
                    ContactPhone = table.Column<string>(type: "text", nullable: true),
                    Status = table.Column<string>(type: "text", nullable: false),
                    ErasedRecordsCount = table.Column<int>(type: "integer", nullable: false),
                    RequestedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CompletedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PrivacyErasureRequests", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ProductBurnRateConfigs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    Sku = table.Column<string>(type: "text", nullable: false),
                    ProductName = table.Column<string>(type: "text", nullable: false),
                    EstimatedDaysOfUse = table.Column<int>(type: "integer", nullable: false),
                    ReminderLeadDays = table.Column<int>(type: "integer", nullable: false),
                    IncentiveDiscountPercent = table.Column<decimal>(type: "numeric", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProductBurnRateConfigs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Products",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    Slug = table.Column<string>(type: "text", nullable: false),
                    Description = table.Column<string>(type: "text", nullable: false),
                    ProductType = table.Column<int>(type: "integer", nullable: false),
                    BasePrice = table.Column<decimal>(type: "numeric", nullable: false),
                    Currency = table.Column<string>(type: "text", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Products", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ProductSalesVelocities",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProductId = table.Column<Guid>(type: "uuid", nullable: false),
                    Sku = table.Column<string>(type: "text", nullable: false),
                    ProductName = table.Column<string>(type: "text", nullable: false),
                    Trailing7DaysUnitsSold = table.Column<decimal>(type: "numeric", nullable: false),
                    Trailing30DaysUnitsSold = table.Column<decimal>(type: "numeric", nullable: false),
                    DailyRunRateUnits = table.Column<decimal>(type: "numeric", nullable: false),
                    ActivePaidAdSpendMultiplier = table.Column<decimal>(type: "numeric", nullable: false),
                    WeightedProjectedDailyDemand = table.Column<decimal>(type: "numeric", nullable: false),
                    LastCalculatedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProductSalesVelocities", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ProductVariants",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProductId = table.Column<Guid>(type: "uuid", nullable: false),
                    SKU = table.Column<string>(type: "text", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    Price = table.Column<decimal>(type: "numeric", nullable: false),
                    InventoryStock = table.Column<int>(type: "integer", nullable: false),
                    AttributesJson = table.Column<string>(type: "text", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProductVariants", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ProspectEnrollments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    CampaignId = table.Column<Guid>(type: "uuid", nullable: false),
                    ContactId = table.Column<Guid>(type: "uuid", nullable: false),
                    CurrentStepNumber = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    PersonalizedHook = table.Column<string>(type: "text", nullable: true),
                    NextRunAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastDispatchedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProspectEnrollments", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "QuietHoursPolicies",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    IsEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    StartHourLocal = table.Column<int>(type: "integer", nullable: false),
                    EndHourLocal = table.Column<int>(type: "integer", nullable: false),
                    AllowBypassForUrgent = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_QuietHoursPolicies", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "QuoteProposals",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uuid", nullable: true),
                    DealId = table.Column<Guid>(type: "uuid", nullable: true),
                    ContactId = table.Column<Guid>(type: "uuid", nullable: false),
                    QuoteNumber = table.Column<string>(type: "text", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    Subtotal = table.Column<decimal>(type: "numeric", nullable: false),
                    DiscountPercent = table.Column<decimal>(type: "numeric", nullable: false),
                    TaxPercent = table.Column<decimal>(type: "numeric", nullable: false),
                    TotalAmount = table.Column<decimal>(type: "numeric", nullable: false),
                    Currency = table.Column<string>(type: "text", nullable: false),
                    Notes = table.Column<string>(type: "text", nullable: true),
                    ExpiresAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_QuoteProposals", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "RegionalHubProximityRules",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    WarehouseId = table.Column<Guid>(type: "uuid", nullable: false),
                    WarehouseName = table.Column<string>(type: "text", nullable: false),
                    GovernorateOrWilaya = table.Column<string>(type: "text", nullable: false),
                    Priority = table.Column<int>(type: "integer", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false)
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
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    StatementId = table.Column<Guid>(type: "uuid", nullable: false),
                    OrderId = table.Column<Guid>(type: "uuid", nullable: true),
                    TrackingNumber = table.Column<string>(type: "text", nullable: false),
                    ReportedStatus = table.Column<string>(type: "text", nullable: false),
                    ReportedCashCollected = table.Column<decimal>(type: "numeric", nullable: false),
                    SystemExpectedAmount = table.Column<decimal>(type: "numeric", nullable: false),
                    DifferenceAmount = table.Column<decimal>(type: "numeric", nullable: false),
                    CourierFeeDeducted = table.Column<decimal>(type: "numeric", nullable: false),
                    DiscrepancyType = table.Column<int>(type: "integer", nullable: false),
                    ResolutionNotes = table.Column<string>(type: "text", nullable: true),
                    IsResolved = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RemittanceItemMatches", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ReplenishmentSchedules",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    OrderId = table.Column<Guid>(type: "uuid", nullable: false),
                    CustomerPhone = table.Column<string>(type: "text", nullable: false),
                    CustomerName = table.Column<string>(type: "text", nullable: false),
                    Sku = table.Column<string>(type: "text", nullable: false),
                    ProductName = table.Column<string>(type: "text", nullable: false),
                    DeliveredAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ScheduledReminderUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    IsTriggered = table.Column<bool>(type: "boolean", nullable: false),
                    TriggeredAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ReorderedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DiscountCode = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReplenishmentSchedules", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ReturnMerchandiseAuthorizations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    RmaNumber = table.Column<string>(type: "text", nullable: false),
                    OrderId = table.Column<Guid>(type: "uuid", nullable: false),
                    OrderNumber = table.Column<string>(type: "text", nullable: false),
                    CustomerPhone = table.Column<string>(type: "text", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    Reason = table.Column<int>(type: "integer", nullable: false),
                    CustomerNotes = table.Column<string>(type: "text", nullable: true),
                    PhotoEvidenceUrlsJson = table.Column<string>(type: "text", nullable: false),
                    ResolutionNotes = table.Column<string>(type: "text", nullable: true),
                    RequestedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ResolvedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReturnMerchandiseAuthorizations", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "RolePermissionClaims",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    RoleId = table.Column<Guid>(type: "uuid", nullable: false),
                    Resource = table.Column<string>(type: "text", nullable: false),
                    Action = table.Column<string>(type: "text", nullable: false),
                    IsAllowed = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RolePermissionClaims", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SalesCadenceLeadEnrollments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    CadenceId = table.Column<Guid>(type: "uuid", nullable: false),
                    LeadName = table.Column<string>(type: "text", nullable: false),
                    Email = table.Column<string>(type: "text", nullable: false),
                    PhoneNumber = table.Column<string>(type: "text", nullable: false),
                    Company = table.Column<string>(type: "text", nullable: true),
                    CurrentStepNumber = table.Column<int>(type: "integer", nullable: false),
                    State = table.Column<int>(type: "integer", nullable: false),
                    NextExecutionAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastExecutedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    EnrolledAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SalesCadenceLeadEnrollments", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SalesCadenceStepItems",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CadenceId = table.Column<Guid>(type: "uuid", nullable: false),
                    StepNumber = table.Column<int>(type: "integer", nullable: false),
                    DayOffset = table.Column<int>(type: "integer", nullable: false),
                    Channel = table.Column<int>(type: "integer", nullable: false),
                    SubjectTemplate = table.Column<string>(type: "text", nullable: true),
                    BodyTemplate = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SalesCadenceStepItems", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SalesOutreachCadences",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    Description = table.Column<string>(type: "text", nullable: false),
                    TargetAudience = table.Column<string>(type: "text", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    StepsCount = table.Column<int>(type: "integer", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SalesOutreachCadences", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SdrTaskItems",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    EnrollmentId = table.Column<Guid>(type: "uuid", nullable: false),
                    CadenceId = table.Column<Guid>(type: "uuid", nullable: false),
                    LeadName = table.Column<string>(type: "text", nullable: false),
                    PhoneNumber = table.Column<string>(type: "text", nullable: false),
                    Email = table.Column<string>(type: "text", nullable: false),
                    Company = table.Column<string>(type: "text", nullable: true),
                    TaskType = table.Column<string>(type: "text", nullable: false),
                    Instruction = table.Column<string>(type: "text", nullable: false),
                    DueAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    IsCompleted = table.Column<bool>(type: "boolean", nullable: false),
                    CompletedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Outcome = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SdrTaskItems", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SecurityAuditLogEntries",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserEmail = table.Column<string>(type: "text", nullable: false),
                    UserRole = table.Column<string>(type: "text", nullable: false),
                    Action = table.Column<string>(type: "text", nullable: false),
                    Resource = table.Column<string>(type: "text", nullable: false),
                    ResourceId = table.Column<string>(type: "text", nullable: false),
                    Details = table.Column<string>(type: "text", nullable: false),
                    IpAddress = table.Column<string>(type: "text", nullable: false),
                    TimestampUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SecurityAuditLogEntries", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SenderAccounts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    Email = table.Column<string>(type: "text", nullable: false),
                    DisplayName = table.Column<string>(type: "text", nullable: false),
                    Provider = table.Column<int>(type: "integer", nullable: false),
                    DailyQuota = table.Column<int>(type: "integer", nullable: false),
                    SentTodayCount = table.Column<int>(type: "integer", nullable: false),
                    WarmupEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    WarmupRampDailyIncrement = table.Column<int>(type: "integer", nullable: false),
                    HealthScore = table.Column<int>(type: "integer", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SenderAccounts", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ServerConversionEvents",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    Platform = table.Column<int>(type: "integer", nullable: false),
                    EventType = table.Column<int>(type: "integer", nullable: false),
                    EventId = table.Column<string>(type: "text", nullable: false),
                    EventTimeUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    HashedEmail = table.Column<string>(type: "text", nullable: true),
                    HashedPhone = table.Column<string>(type: "text", nullable: true),
                    HashedFirstName = table.Column<string>(type: "text", nullable: true),
                    HashedLastName = table.Column<string>(type: "text", nullable: true),
                    HashedCity = table.Column<string>(type: "text", nullable: true),
                    HashedCountry = table.Column<string>(type: "text", nullable: true),
                    Fbc = table.Column<string>(type: "text", nullable: true),
                    Fbp = table.Column<string>(type: "text", nullable: true),
                    ClientUserAgent = table.Column<string>(type: "text", nullable: true),
                    ClientIpAddress = table.Column<string>(type: "text", nullable: true),
                    ValueAmount = table.Column<decimal>(type: "numeric", nullable: false),
                    Currency = table.Column<string>(type: "text", nullable: false),
                    CustomDataJson = table.Column<string>(type: "text", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    DispatchedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ApiResponseJson = table.Column<string>(type: "text", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ServerConversionEvents", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ShipmentRecords",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    OrderId = table.Column<Guid>(type: "uuid", nullable: false),
                    CourierProviderId = table.Column<Guid>(type: "uuid", nullable: false),
                    CourierName = table.Column<string>(type: "text", nullable: false),
                    TrackingNumber = table.Column<string>(type: "text", nullable: false),
                    AwbBarcode = table.Column<string>(type: "text", nullable: false),
                    LabelPdfUrl = table.Column<string>(type: "text", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    CashToCollect = table.Column<decimal>(type: "numeric", nullable: false),
                    CashRemitted = table.Column<decimal>(type: "numeric", nullable: false),
                    DispatchedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DeliveredAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ShipmentRecords", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ShippingManifests",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    CourierProviderId = table.Column<Guid>(type: "uuid", nullable: false),
                    CourierName = table.Column<string>(type: "text", nullable: false),
                    ManifestNumber = table.Column<string>(type: "text", nullable: false),
                    OrderCount = table.Column<int>(type: "integer", nullable: false),
                    TotalCashAmount = table.Column<decimal>(type: "numeric", nullable: false),
                    Status = table.Column<string>(type: "text", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    HandedOverAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    OrderIdsJson = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ShippingManifests", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SlaPolicies",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    Priority = table.Column<int>(type: "integer", nullable: false),
                    TargetFirstResponseMinutes = table.Column<int>(type: "integer", nullable: false),
                    TargetResolutionMinutes = table.Column<int>(type: "integer", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SlaPolicies", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SocialAccounts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    Platform = table.Column<int>(type: "integer", nullable: false),
                    AccountName = table.Column<string>(type: "text", nullable: false),
                    ExternalAccountId = table.Column<string>(type: "text", nullable: false),
                    AccessToken = table.Column<string>(type: "text", nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SocialAccounts", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SocialPosts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    Content = table.Column<string>(type: "text", nullable: false),
                    MediaUrls = table.Column<List<string>>(type: "text[]", nullable: false),
                    ScheduledAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    PublishedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    TargetPlatforms = table.Column<int[]>(type: "integer[]", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SocialPosts", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "StockMovements",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    WarehouseId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProductId = table.Column<Guid>(type: "uuid", nullable: false),
                    Sku = table.Column<string>(type: "text", nullable: false),
                    OrderId = table.Column<Guid>(type: "uuid", nullable: true),
                    MovementType = table.Column<int>(type: "integer", nullable: false),
                    QuantityChange = table.Column<int>(type: "integer", nullable: false),
                    PreviousPhysical = table.Column<int>(type: "integer", nullable: false),
                    NewPhysical = table.Column<int>(type: "integer", nullable: false),
                    PerformedBy = table.Column<string>(type: "text", nullable: false),
                    Notes = table.Column<string>(type: "text", nullable: true),
                    TimestampUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StockMovements", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "StockoutProjectionAlerts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProductId = table.Column<Guid>(type: "uuid", nullable: false),
                    Sku = table.Column<string>(type: "text", nullable: false),
                    CurrentPhysicalStock = table.Column<int>(type: "integer", nullable: false),
                    CurrentReservedStock = table.Column<int>(type: "integer", nullable: false),
                    ProjectedDailyDemand = table.Column<decimal>(type: "numeric", nullable: false),
                    EstimatedDaysOfSupplyRemaining = table.Column<decimal>(type: "numeric", nullable: false),
                    ProjectedStockoutDateUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    RecommendedReorderQuantity = table.Column<int>(type: "integer", nullable: false),
                    Priority = table.Column<int>(type: "integer", nullable: false),
                    AlertDismissed = table.Column<bool>(type: "boolean", nullable: false),
                    GeneratedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StockoutProjectionAlerts", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "StorefrontConnections",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    Platform = table.Column<int>(type: "integer", nullable: false),
                    StoreDomain = table.Column<string>(type: "text", nullable: false),
                    WebhookSecret = table.Column<string>(type: "text", nullable: false),
                    AccessToken = table.Column<string>(type: "text", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StorefrontConnections", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "StorefrontDomains",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    Hostname = table.Column<string>(type: "text", nullable: false),
                    IsPrimary = table.Column<bool>(type: "boolean", nullable: false),
                    TargetCname = table.Column<string>(type: "text", nullable: false),
                    ValidationStatus = table.Column<int>(type: "integer", nullable: false),
                    SslStatus = table.Column<int>(type: "integer", nullable: false),
                    VerifiedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StorefrontDomains", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "StreamingVoiceCallSessions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    CallSid = table.Column<string>(type: "text", nullable: false),
                    StreamSid = table.Column<string>(type: "text", nullable: false),
                    Encoding = table.Column<int>(type: "integer", nullable: false),
                    SampleRate = table.Column<int>(type: "integer", nullable: false),
                    CurrentVadStatus = table.Column<int>(type: "integer", nullable: false),
                    IsAgentSpeaking = table.Column<bool>(type: "boolean", nullable: false),
                    InterruptionCount = table.Column<int>(type: "integer", nullable: false),
                    TotalFramesReceived = table.Column<long>(type: "bigint", nullable: false),
                    AverageProcessingLatencyMs = table.Column<double>(type: "double precision", nullable: false),
                    IsTransferred = table.Column<bool>(type: "boolean", nullable: false),
                    TransferTargetSipUri = table.Column<string>(type: "text", nullable: true),
                    TransferReason = table.Column<string>(type: "text", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    EndedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StreamingVoiceCallSessions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SupplierPurchaseOrders",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    PoNumber = table.Column<string>(type: "text", nullable: false),
                    SupplierName = table.Column<string>(type: "text", nullable: false),
                    DestinationWarehouseId = table.Column<Guid>(type: "uuid", nullable: false),
                    TotalCost = table.Column<decimal>(type: "numeric", nullable: false),
                    Currency = table.Column<string>(type: "text", nullable: false),
                    Status = table.Column<string>(type: "text", nullable: false),
                    ItemsJson = table.Column<string>(type: "text", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ReceivedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SupplierPurchaseOrders", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SupplierScorecards",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    SupplierName = table.Column<string>(type: "text", nullable: false),
                    ContactEmail = table.Column<string>(type: "text", nullable: false),
                    ContactPhone = table.Column<string>(type: "text", nullable: false),
                    Country = table.Column<string>(type: "text", nullable: false),
                    PromisedLeadTimeDays = table.Column<int>(type: "integer", nullable: false),
                    ActualAvgLeadTimeDays = table.Column<decimal>(type: "numeric", nullable: false),
                    DefectRatePercentage = table.Column<decimal>(type: "numeric", nullable: false),
                    QualityScoreOutOf100 = table.Column<decimal>(type: "numeric", nullable: false),
                    TotalPurchaseOrdersCompleted = table.Column<int>(type: "integer", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    LastEvaluatedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SupplierScorecards", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SurveyForms",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    Title = table.Column<string>(type: "text", nullable: false),
                    Type = table.Column<int>(type: "integer", nullable: false),
                    QuestionPrompt = table.Column<string>(type: "text", nullable: false),
                    ScaleMax = table.Column<int>(type: "integer", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SurveyForms", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SurveyResponses",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    SurveyFormId = table.Column<Guid>(type: "uuid", nullable: false),
                    ContactId = table.Column<Guid>(type: "uuid", nullable: false),
                    Score = table.Column<int>(type: "integer", nullable: false),
                    FeedbackText = table.Column<string>(type: "text", nullable: true),
                    Sentiment = table.Column<int>(type: "integer", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SurveyResponses", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SystemRoles",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    RoleName = table.Column<string>(type: "text", nullable: false),
                    Description = table.Column<string>(type: "text", nullable: false),
                    CanViewRawPii = table.Column<bool>(type: "boolean", nullable: false),
                    CanExportData = table.Column<bool>(type: "boolean", nullable: false),
                    CanOverridePricing = table.Column<bool>(type: "boolean", nullable: false),
                    CanApproveDisbursements = table.Column<bool>(type: "boolean", nullable: false),
                    IsSystemDefault = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SystemRoles", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Tenants",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Slug = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Tenants", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "TenantSubscriptions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    PlanTier = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<string>(type: "text", nullable: false),
                    MonthlyPrice = table.Column<decimal>(type: "numeric", nullable: false),
                    BillingCycleStartUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    BillingCycleEndUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TenantSubscriptions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "TouchpointExecutions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    TouchpointId = table.Column<Guid>(type: "uuid", nullable: false),
                    ContactId = table.Column<Guid>(type: "uuid", nullable: false),
                    Channel = table.Column<int>(type: "integer", nullable: false),
                    ScheduledAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ExecutedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Status = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TouchpointExecutions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "UnifiedCustomerProfiles",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    PrimaryName = table.Column<string>(type: "text", nullable: false),
                    PrimaryEmail = table.Column<string>(type: "text", nullable: true),
                    PrimaryPhone = table.Column<string>(type: "text", nullable: false),
                    Company = table.Column<string>(type: "text", nullable: true),
                    LifecycleStage = table.Column<int>(type: "integer", nullable: false),
                    TotalOrdersCount = table.Column<int>(type: "integer", nullable: false),
                    TotalSpent = table.Column<decimal>(type: "numeric", nullable: false),
                    DeliverySuccessRate = table.Column<double>(type: "double precision", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    LastSeenAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UnifiedCustomerProfiles", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "UsageMeterRecords",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    Metric = table.Column<int>(type: "integer", nullable: false),
                    Quantity = table.Column<long>(type: "bigint", nullable: false),
                    RecordedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Metadata = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UsageMeterRecords", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Users",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    Email = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    FullName = table.Column<string>(type: "text", nullable: false),
                    Role = table.Column<int>(type: "integer", nullable: false),
                    AvatarUrl = table.Column<string>(type: "text", nullable: true),
                    PasswordHash = table.Column<string>(type: "text", nullable: false),
                    PasswordSalt = table.Column<string>(type: "text", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    RefreshToken = table.Column<string>(type: "text", nullable: true),
                    RefreshTokenExpiresAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastLoginAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Users", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "VoiceCallSessions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    ContactId = table.Column<Guid>(type: "uuid", nullable: false),
                    CallSid = table.Column<string>(type: "text", nullable: false),
                    Direction = table.Column<int>(type: "integer", nullable: false),
                    FromNumber = table.Column<string>(type: "text", nullable: false),
                    ToNumber = table.Column<string>(type: "text", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    DurationSeconds = table.Column<int>(type: "integer", nullable: false),
                    RecordingUrl = table.Column<string>(type: "text", nullable: true),
                    Transcript = table.Column<string>(type: "text", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VoiceCallSessions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "VoiceConfirmationCampaigns",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    LanguageCode = table.Column<string>(type: "text", nullable: false),
                    MaxRetryAttempts = table.Column<int>(type: "integer", nullable: false),
                    RetryDelayMinutes = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    TotalCallsQueued = table.Column<int>(type: "integer", nullable: false),
                    TotalCallsSucceeded = table.Column<int>(type: "integer", nullable: false),
                    TotalCallsFailed = table.Column<int>(type: "integer", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VoiceConfirmationCampaigns", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "WarehouseInventories",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    WarehouseId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProductId = table.Column<Guid>(type: "uuid", nullable: false),
                    Sku = table.Column<string>(type: "text", nullable: false),
                    PhysicalStock = table.Column<int>(type: "integer", nullable: false),
                    ReservedStock = table.Column<int>(type: "integer", nullable: false),
                    CommittedStock = table.Column<int>(type: "integer", nullable: false),
                    InTransitStock = table.Column<int>(type: "integer", nullable: false),
                    QuarantineReturnedStock = table.Column<int>(type: "integer", nullable: false),
                    DamagedStock = table.Column<int>(type: "integer", nullable: false),
                    LastUpdatedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WarehouseInventories", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "WarehousePickSlips",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    PickSlipNumber = table.Column<string>(type: "text", nullable: false),
                    OrderId = table.Column<Guid>(type: "uuid", nullable: false),
                    OrderNumber = table.Column<string>(type: "text", nullable: false),
                    WarehouseId = table.Column<Guid>(type: "uuid", nullable: false),
                    AssignedPickerId = table.Column<string>(type: "text", nullable: true),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CompletedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WarehousePickSlips", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "WarehouseRmaInspections",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    TrackingNumber = table.Column<string>(type: "text", nullable: false),
                    OrderId = table.Column<Guid>(type: "uuid", nullable: false),
                    InspectorId = table.Column<string>(type: "text", nullable: false),
                    Condition = table.Column<int>(type: "integer", nullable: false),
                    InspectorNotes = table.Column<string>(type: "text", nullable: true),
                    RestockedToWarehouseId = table.Column<Guid>(type: "uuid", nullable: true),
                    InspectedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WarehouseRmaInspections", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Warehouses",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    Code = table.Column<string>(type: "text", nullable: false),
                    Address = table.Column<string>(type: "text", nullable: false),
                    IsPrimary = table.Column<bool>(type: "boolean", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Warehouses", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "WarmupRecords",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    SenderAccountId = table.Column<Guid>(type: "uuid", nullable: false),
                    PeerEmail = table.Column<string>(type: "text", nullable: false),
                    Direction = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    OccurredAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WarmupRecords", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "WebhookDeliveryLogs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SubscriptionId = table.Column<Guid>(type: "uuid", nullable: false),
                    EventType = table.Column<string>(type: "text", nullable: false),
                    Payload = table.Column<string>(type: "text", nullable: false),
                    ResponseStatusCode = table.Column<int>(type: "integer", nullable: true),
                    IsSuccess = table.Column<bool>(type: "boolean", nullable: false),
                    ErrorMessage = table.Column<string>(type: "text", nullable: true),
                    DeliveredAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WebhookDeliveryLogs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "WebhookSubscriptions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    TargetUrl = table.Column<string>(type: "text", nullable: false),
                    SecretToken = table.Column<string>(type: "text", nullable: false),
                    SubscribedEvents = table.Column<string>(type: "text", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WebhookSubscriptions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "WhatsAppTemplates",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    Category = table.Column<int>(type: "integer", nullable: false),
                    Language = table.Column<string>(type: "text", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    BodyText = table.Column<string>(type: "text", nullable: false),
                    ButtonsJson = table.Column<string>(type: "text", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WhatsAppTemplates", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "WorkflowDeadLetterLogs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    WorkflowId = table.Column<Guid>(type: "uuid", nullable: false),
                    ExecutionId = table.Column<Guid>(type: "uuid", nullable: false),
                    FailedNodeId = table.Column<string>(type: "text", nullable: false),
                    FailedNodeType = table.Column<string>(type: "text", nullable: false),
                    InputPayload = table.Column<string>(type: "text", nullable: false),
                    ErrorMessage = table.Column<string>(type: "text", nullable: false),
                    RetryCount = table.Column<int>(type: "integer", nullable: false),
                    IsResolved = table.Column<bool>(type: "boolean", nullable: false),
                    FailedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ResolvedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WorkflowDeadLetterLogs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "WorkflowExecutions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    WorkflowGraphId = table.Column<Guid>(type: "uuid", nullable: false),
                    Status = table.Column<string>(type: "text", nullable: false),
                    ExecutedNodesCount = table.Column<int>(type: "integer", nullable: false),
                    ExecutionLog = table.Column<string>(type: "text", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WorkflowExecutions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "WorkflowGraphs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    TriggerType = table.Column<string>(type: "text", nullable: false),
                    NodesJson = table.Column<string>(type: "text", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WorkflowGraphs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ManifestParcelItems",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ManifestId = table.Column<Guid>(type: "uuid", nullable: false),
                    OrderId = table.Column<Guid>(type: "uuid", nullable: false),
                    TrackingNumber = table.Column<string>(type: "text", nullable: false),
                    CustomerName = table.Column<string>(type: "text", nullable: false),
                    Governorate = table.Column<string>(type: "text", nullable: false),
                    CodAmount = table.Column<decimal>(type: "numeric", nullable: false),
                    ScannedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CourierManifestRunSheetId = table.Column<Guid>(type: "uuid", nullable: true)
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
                name: "JourneySteps",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    JourneyId = table.Column<Guid>(type: "uuid", nullable: false),
                    StepOrder = table.Column<int>(type: "integer", nullable: false),
                    StepType = table.Column<int>(type: "integer", nullable: false),
                    SubjectTemplate = table.Column<string>(type: "text", nullable: true),
                    BodyTemplate = table.Column<string>(type: "text", nullable: false),
                    DelaySeconds = table.Column<int>(type: "integer", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CustomerJourneyId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_JourneySteps", x => x.Id);
                    table.ForeignKey(
                        name: "FK_JourneySteps_CustomerJourneys_CustomerJourneyId",
                        column: x => x.CustomerJourneyId,
                        principalTable: "CustomerJourneys",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "QuoteLineItems",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    QuoteProposalId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProductId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProductVariantId = table.Column<Guid>(type: "uuid", nullable: true),
                    Description = table.Column<string>(type: "text", nullable: false),
                    Quantity = table.Column<int>(type: "integer", nullable: false),
                    UnitPrice = table.Column<decimal>(type: "numeric", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_QuoteLineItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_QuoteLineItems_QuoteProposals_QuoteProposalId",
                        column: x => x.QuoteProposalId,
                        principalTable: "QuoteProposals",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PickSlipItems",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PickSlipId = table.Column<Guid>(type: "uuid", nullable: false),
                    Sku = table.Column<string>(type: "text", nullable: false),
                    ProductName = table.Column<string>(type: "text", nullable: false),
                    Barcode = table.Column<string>(type: "text", nullable: false),
                    QuantityRequested = table.Column<int>(type: "integer", nullable: false),
                    QuantityPicked = table.Column<int>(type: "integer", nullable: false),
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
                name: "IX_Activities_TenantId_ContactId_CreatedAtUtc",
                table: "Activities",
                columns: new[] { "TenantId", "ContactId", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_AiDrafts_TenantId_ConversationId_Status",
                table: "AiDrafts",
                columns: new[] { "TenantId", "ConversationId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_Companies_TenantId_Domain",
                table: "Companies",
                columns: new[] { "TenantId", "Domain" });

            migrationBuilder.CreateIndex(
                name: "IX_Contacts_TenantId_Email",
                table: "Contacts",
                columns: new[] { "TenantId", "Email" });

            migrationBuilder.CreateIndex(
                name: "IX_Contacts_TenantId_Phone",
                table: "Contacts",
                columns: new[] { "TenantId", "Phone" });

            migrationBuilder.CreateIndex(
                name: "IX_Conversations_TenantId_ContactId",
                table: "Conversations",
                columns: new[] { "TenantId", "ContactId" });

            migrationBuilder.CreateIndex(
                name: "IX_Conversations_TenantId_Status_LastMessageAtUtc",
                table: "Conversations",
                columns: new[] { "TenantId", "Status", "LastMessageAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_Inboxes_TenantId_ChannelType",
                table: "Inboxes",
                columns: new[] { "TenantId", "ChannelType" });

            migrationBuilder.CreateIndex(
                name: "IX_JourneySteps_CustomerJourneyId",
                table: "JourneySteps",
                column: "CustomerJourneyId");

            migrationBuilder.CreateIndex(
                name: "IX_ManifestParcelItems_CourierManifestRunSheetId",
                table: "ManifestParcelItems",
                column: "CourierManifestRunSheetId");

            migrationBuilder.CreateIndex(
                name: "IX_Messages_TenantId_ConversationId_CreatedAtUtc",
                table: "Messages",
                columns: new[] { "TenantId", "ConversationId", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_Messages_TenantId_ExternalMessageId",
                table: "Messages",
                columns: new[] { "TenantId", "ExternalMessageId" });

            migrationBuilder.CreateIndex(
                name: "IX_OutboxMessages_ProcessedOnUtc_OccurredOnUtc",
                table: "OutboxMessages",
                columns: new[] { "ProcessedOnUtc", "OccurredOnUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_PickSlipItems_WarehousePickSlipId",
                table: "PickSlipItems",
                column: "WarehousePickSlipId");

            migrationBuilder.CreateIndex(
                name: "IX_QuoteLineItems_QuoteProposalId",
                table: "QuoteLineItems",
                column: "QuoteProposalId");

            migrationBuilder.CreateIndex(
                name: "IX_Tenants_Slug",
                table: "Tenants",
                column: "Slug",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Users_TenantId_Email",
                table: "Users",
                columns: new[] { "TenantId", "Email" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AbandonedCartRecords");

            migrationBuilder.DropTable(
                name: "Activities");

            migrationBuilder.DropTable(
                name: "AffiliateReferralCodes");

            migrationBuilder.DropTable(
                name: "AiDrafts");

            migrationBuilder.DropTable(
                name: "AppointmentBookings");

            migrationBuilder.DropTable(
                name: "AppointmentSlots");

            migrationBuilder.DropTable(
                name: "AttributionTouchpoints");

            migrationBuilder.DropTable(
                name: "AutomatedRestockOrders");

            migrationBuilder.DropTable(
                name: "BlacklistEntries");

            migrationBuilder.DropTable(
                name: "BrandThemeTokens");

            migrationBuilder.DropTable(
                name: "BroadcastCampaigns");

            migrationBuilder.DropTable(
                name: "CadenceSteps");

            migrationBuilder.DropTable(
                name: "CadenceTouchpoints");

            migrationBuilder.DropTable(
                name: "CallOutcomeRecords");

            migrationBuilder.DropTable(
                name: "CarrierPerformanceMetrics");

            migrationBuilder.DropTable(
                name: "CarrierRateCards");

            migrationBuilder.DropTable(
                name: "CarrierRemittanceScheduleConfigs");

            migrationBuilder.DropTable(
                name: "ChannelCredentials");

            migrationBuilder.DropTable(
                name: "CheckoutLinks");

            migrationBuilder.DropTable(
                name: "CodOrders");

            migrationBuilder.DropTable(
                name: "CommissionEscrowLedgers");

            migrationBuilder.DropTable(
                name: "Companies");

            migrationBuilder.DropTable(
                name: "Contacts");

            migrationBuilder.DropTable(
                name: "Conversations");

            migrationBuilder.DropTable(
                name: "ConversionTrackingConfigs");

            migrationBuilder.DropTable(
                name: "CountryTradeConfigs");

            migrationBuilder.DropTable(
                name: "CourierFloatValuations");

            migrationBuilder.DropTable(
                name: "CourierProviders");

            migrationBuilder.DropTable(
                name: "CourierRemittanceStatements");

            migrationBuilder.DropTable(
                name: "CreatorPayoutRecords");

            migrationBuilder.DropTable(
                name: "CreatorProfiles");

            migrationBuilder.DropTable(
                name: "CustomerEvents");

            migrationBuilder.DropTable(
                name: "CustomerIdentityLinks");

            migrationBuilder.DropTable(
                name: "CustomerReputationScorecards");

            migrationBuilder.DropTable(
                name: "CustomerTimelineEvents");

            migrationBuilder.DropTable(
                name: "CustomsCommercialInvoices");

            migrationBuilder.DropTable(
                name: "Deals");

            migrationBuilder.DropTable(
                name: "DigitalContracts");

            migrationBuilder.DropTable(
                name: "DocumentChunks");

            migrationBuilder.DropTable(
                name: "DynamicAgentTools");

            migrationBuilder.DropTable(
                name: "ExchangeCourierSwapManifests");

            migrationBuilder.DropTable(
                name: "ExternalStoreSyncRecords");

            migrationBuilder.DropTable(
                name: "FeatureEntitlements");

            migrationBuilder.DropTable(
                name: "FxExchangeRates");

            migrationBuilder.DropTable(
                name: "GoogleSheetsSyncConfigs");

            migrationBuilder.DropTable(
                name: "HelpArticles");

            migrationBuilder.DropTable(
                name: "HelpCategories");

            migrationBuilder.DropTable(
                name: "Inboxes");

            migrationBuilder.DropTable(
                name: "InChatCartSessions");

            migrationBuilder.DropTable(
                name: "InterHubStockTransfers");

            migrationBuilder.DropTable(
                name: "ItemReturnInspections");

            migrationBuilder.DropTable(
                name: "JourneyEnrollments");

            migrationBuilder.DropTable(
                name: "JourneySteps");

            migrationBuilder.DropTable(
                name: "LeadEnrichmentProfiles");

            migrationBuilder.DropTable(
                name: "LiveFlashDeals");

            migrationBuilder.DropTable(
                name: "LiveStreamSessions");

            migrationBuilder.DropTable(
                name: "ManifestParcelItems");

            migrationBuilder.DropTable(
                name: "Messages");

            migrationBuilder.DropTable(
                name: "MetaCatalogItems");

            migrationBuilder.DropTable(
                name: "MetaCatalogSyncFeeds");

            migrationBuilder.DropTable(
                name: "MicroDepositPaymentLinks");

            migrationBuilder.DropTable(
                name: "OrderCollisionLocks");

            migrationBuilder.DropTable(
                name: "OrderExchangeRequests");

            migrationBuilder.DropTable(
                name: "OutboundCallAttempts");

            migrationBuilder.DropTable(
                name: "OutboundCampaigns");

            migrationBuilder.DropTable(
                name: "OutboxMessages");

            migrationBuilder.DropTable(
                name: "PickSlipItems");

            migrationBuilder.DropTable(
                name: "PostPurchaseSurveys");

            migrationBuilder.DropTable(
                name: "PrivacyErasureRequests");

            migrationBuilder.DropTable(
                name: "ProductBurnRateConfigs");

            migrationBuilder.DropTable(
                name: "Products");

            migrationBuilder.DropTable(
                name: "ProductSalesVelocities");

            migrationBuilder.DropTable(
                name: "ProductVariants");

            migrationBuilder.DropTable(
                name: "ProspectEnrollments");

            migrationBuilder.DropTable(
                name: "QuietHoursPolicies");

            migrationBuilder.DropTable(
                name: "QuoteLineItems");

            migrationBuilder.DropTable(
                name: "RegionalHubProximityRules");

            migrationBuilder.DropTable(
                name: "RemittanceItemMatches");

            migrationBuilder.DropTable(
                name: "ReplenishmentSchedules");

            migrationBuilder.DropTable(
                name: "ReturnMerchandiseAuthorizations");

            migrationBuilder.DropTable(
                name: "RolePermissionClaims");

            migrationBuilder.DropTable(
                name: "SalesCadenceLeadEnrollments");

            migrationBuilder.DropTable(
                name: "SalesCadenceStepItems");

            migrationBuilder.DropTable(
                name: "SalesOutreachCadences");

            migrationBuilder.DropTable(
                name: "SdrTaskItems");

            migrationBuilder.DropTable(
                name: "SecurityAuditLogEntries");

            migrationBuilder.DropTable(
                name: "SenderAccounts");

            migrationBuilder.DropTable(
                name: "ServerConversionEvents");

            migrationBuilder.DropTable(
                name: "ShipmentRecords");

            migrationBuilder.DropTable(
                name: "ShippingManifests");

            migrationBuilder.DropTable(
                name: "SlaPolicies");

            migrationBuilder.DropTable(
                name: "SocialAccounts");

            migrationBuilder.DropTable(
                name: "SocialPosts");

            migrationBuilder.DropTable(
                name: "StockMovements");

            migrationBuilder.DropTable(
                name: "StockoutProjectionAlerts");

            migrationBuilder.DropTable(
                name: "StorefrontConnections");

            migrationBuilder.DropTable(
                name: "StorefrontDomains");

            migrationBuilder.DropTable(
                name: "StreamingVoiceCallSessions");

            migrationBuilder.DropTable(
                name: "SupplierPurchaseOrders");

            migrationBuilder.DropTable(
                name: "SupplierScorecards");

            migrationBuilder.DropTable(
                name: "SurveyForms");

            migrationBuilder.DropTable(
                name: "SurveyResponses");

            migrationBuilder.DropTable(
                name: "SystemRoles");

            migrationBuilder.DropTable(
                name: "Tenants");

            migrationBuilder.DropTable(
                name: "TenantSubscriptions");

            migrationBuilder.DropTable(
                name: "TouchpointExecutions");

            migrationBuilder.DropTable(
                name: "UnifiedCustomerProfiles");

            migrationBuilder.DropTable(
                name: "UsageMeterRecords");

            migrationBuilder.DropTable(
                name: "Users");

            migrationBuilder.DropTable(
                name: "VoiceCallSessions");

            migrationBuilder.DropTable(
                name: "VoiceConfirmationCampaigns");

            migrationBuilder.DropTable(
                name: "WarehouseInventories");

            migrationBuilder.DropTable(
                name: "WarehouseRmaInspections");

            migrationBuilder.DropTable(
                name: "Warehouses");

            migrationBuilder.DropTable(
                name: "WarmupRecords");

            migrationBuilder.DropTable(
                name: "WebhookDeliveryLogs");

            migrationBuilder.DropTable(
                name: "WebhookSubscriptions");

            migrationBuilder.DropTable(
                name: "WhatsAppTemplates");

            migrationBuilder.DropTable(
                name: "WorkflowDeadLetterLogs");

            migrationBuilder.DropTable(
                name: "WorkflowExecutions");

            migrationBuilder.DropTable(
                name: "WorkflowGraphs");

            migrationBuilder.DropTable(
                name: "CustomerJourneys");

            migrationBuilder.DropTable(
                name: "CourierManifestRunSheets");

            migrationBuilder.DropTable(
                name: "WarehousePickSlips");

            migrationBuilder.DropTable(
                name: "QuoteProposals");
        }
    }
}
