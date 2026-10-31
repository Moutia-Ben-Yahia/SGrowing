using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SGrowing.Api.Migrations
{
    /// <inheritdoc />
    public partial class EnterpriseHorizonsExpansion : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "ToNumber",
                table: "VoiceCallSessions",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<string>(
                name: "FromNumber",
                table: "VoiceCallSessions",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<string>(
                name: "CallSid",
                table: "VoiceCallSessions",
                type: "character varying(100)",
                maxLength: 100,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<string>(
                name: "Status",
                table: "TouchpointExecutions",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AddColumn<string>(
                name: "RefreshToken",
                table: "SocialAccounts",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "RefreshTokenRequired",
                table: "SocialAccounts",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "TokenExpiresAtUtc",
                table: "SocialAccounts",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "ExpectedFreightCost",
                table: "ShippingManifests",
                type: "numeric",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<bool>(
                name: "HasDkim",
                table: "SenderAccounts",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "HasDmarc",
                table: "SenderAccounts",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "HasSpf",
                table: "SenderAccounts",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "LastDispatchedAtUtc",
                table: "SenderAccounts",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "QuarantineReason",
                table: "SenderAccounts",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "QuarantinedAtUtc",
                table: "SenderAccounts",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Status",
                table: "SenderAccounts",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTime>(
                name: "ThawedAtUtc",
                table: "SenderAccounts",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "TotalBounced",
                table: "SenderAccounts",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "TotalDelivered",
                table: "SenderAccounts",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "TotalDispatched",
                table: "SenderAccounts",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "TotalOpened",
                table: "SenderAccounts",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "TotalReplied",
                table: "SenderAccounts",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "TotalSpamComplaints",
                table: "SenderAccounts",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "DisputeTicketNumber",
                table: "RemittanceItemMatches",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "HoldbackAmount",
                table: "RemittanceItemMatches",
                type: "numeric",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<bool>(
                name: "IsPrivateNote",
                table: "Messages",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<long>(
                name: "MaxAiTokensPerMonth",
                table: "FeatureEntitlements",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<int>(
                name: "MaxCarrierWaybills",
                table: "FeatureEntitlements",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "MaxVoiceMinutesPerMonth",
                table: "FeatureEntitlements",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "MaxWarmupMailboxes",
                table: "FeatureEntitlements",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<long>(
                name: "MonthlyIncludedColdEmails",
                table: "FeatureEntitlements",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<long>(
                name: "StorageMbLimit",
                table: "FeatureEntitlements",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<DateTime>(
                name: "ExpectedCloseDate",
                table: "Deals",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "OwnerId",
                table: "Deals",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "AccumulatedPausedMinutes",
                table: "Conversations",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTime>(
                name: "PausedAtUtc",
                table: "Conversations",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Country",
                table: "Contacts",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ConfirmedVia",
                table: "CodOrders",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "InteractiveConfirmationSentAtUtc",
                table: "CodOrders",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "RequiresPhoneCall",
                table: "CodOrders",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "AdOptimizationRules",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    Platform = table.Column<int>(type: "integer", nullable: false),
                    RuleName = table.Column<string>(type: "text", nullable: false),
                    MinSpendThreshold = table.Column<decimal>(type: "numeric", nullable: false),
                    MinDeliveryRateCutoff = table.Column<decimal>(type: "numeric", nullable: false),
                    MinDeliveredRoasToScale = table.Column<decimal>(type: "numeric", nullable: false),
                    DailyBudgetScaleStepPercentage = table.Column<decimal>(type: "numeric", nullable: false),
                    IsAutoExecuteEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AdOptimizationRules", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AdPlatformExecutionLogs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    RuleId = table.Column<Guid>(type: "uuid", nullable: true),
                    Platform = table.Column<int>(type: "integer", nullable: false),
                    AdSetId = table.Column<string>(type: "text", nullable: false),
                    Action = table.Column<int>(type: "integer", nullable: false),
                    Reason = table.Column<string>(type: "text", nullable: false),
                    CurrentDailyBudget = table.Column<decimal>(type: "numeric", nullable: false),
                    NewDailyBudget = table.Column<decimal>(type: "numeric", nullable: true),
                    ExecutionSuccess = table.Column<bool>(type: "boolean", nullable: false),
                    ExecutedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AdPlatformExecutionLogs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AdSetDeliveredRoasMetrics",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    Platform = table.Column<int>(type: "integer", nullable: false),
                    AdSetId = table.Column<string>(type: "text", nullable: false),
                    AdSetName = table.Column<string>(type: "text", nullable: false),
                    DateUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    TotalAdSpend = table.Column<decimal>(type: "numeric", nullable: false),
                    TotalOrdersAttributed = table.Column<int>(type: "integer", nullable: false),
                    DeliveredOrdersCount = table.Column<int>(type: "integer", nullable: false),
                    ReturnedOrdersCount = table.Column<int>(type: "integer", nullable: false),
                    DeliveryRatePercentage = table.Column<decimal>(type: "numeric", nullable: false),
                    GrossAttributedGmv = table.Column<decimal>(type: "numeric", nullable: false),
                    DeliveredNetCashRemitted = table.Column<decimal>(type: "numeric", nullable: false),
                    TotalCogs = table.Column<decimal>(type: "numeric", nullable: false),
                    TotalCourierShippingFees = table.Column<decimal>(type: "numeric", nullable: false),
                    NetContributionMargin = table.Column<decimal>(type: "numeric", nullable: false),
                    RealDeliveredRoas = table.Column<decimal>(type: "numeric", nullable: false),
                    StandardPixelRoas = table.Column<decimal>(type: "numeric", nullable: false),
                    RoasLeakagePercentage = table.Column<decimal>(type: "numeric", nullable: false),
                    CalculatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AdSetDeliveredRoasMetrics", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AeoBrandMentions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    BrandName = table.Column<string>(type: "text", nullable: false),
                    AiEngine = table.Column<string>(type: "text", nullable: false),
                    PromptQuery = table.Column<string>(type: "text", nullable: false),
                    IsBrandMentioned = table.Column<bool>(type: "boolean", nullable: false),
                    MentionSentiment = table.Column<string>(type: "text", nullable: false),
                    SnippetText = table.Column<string>(type: "text", nullable: false),
                    CitationRank = table.Column<int>(type: "integer", nullable: true),
                    CapturedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AeoBrandMentions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AgentHeartbeatRecords",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    RoleKey = table.Column<string>(type: "text", nullable: false),
                    AgentName = table.Column<string>(type: "text", nullable: false),
                    Status = table.Column<string>(type: "text", nullable: false),
                    FindingsSummary = table.Column<string>(type: "text", nullable: false),
                    RecommendedAction = table.Column<string>(type: "text", nullable: false),
                    ActionsExecutedCount = table.Column<int>(type: "integer", nullable: false),
                    ExecutedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AgentHeartbeatRecords", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AgentMissions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    Title = table.Column<string>(type: "text", nullable: false),
                    TargetGoal = table.Column<string>(type: "text", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    AutonomyMode = table.Column<string>(type: "text", nullable: false),
                    TotalBudgetUsd = table.Column<decimal>(type: "numeric", nullable: false),
                    SpentBudgetUsd = table.Column<decimal>(type: "numeric", nullable: false),
                    TokensConsumed = table.Column<long>(type: "bigint", nullable: false),
                    CurrentStepIndex = table.Column<int>(type: "integer", nullable: false),
                    ReasoningTrace = table.Column<string>(type: "text", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CompletedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AgentMissions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AgentPersonas",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    RoleKey = table.Column<string>(type: "text", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    Layer = table.Column<string>(type: "text", nullable: false),
                    PrimaryObjective = table.Column<string>(type: "text", nullable: false),
                    AutonomousTrigger = table.Column<string>(type: "text", nullable: false),
                    ModelPreference = table.Column<string>(type: "text", nullable: false),
                    Status = table.Column<string>(type: "text", nullable: false),
                    LastHeartbeatUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AgentPersonas", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AiAgentBudgets",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    MonthlyTokenLimit = table.Column<long>(type: "bigint", nullable: false),
                    TokensConsumed = table.Column<long>(type: "bigint", nullable: false),
                    MaxMonthlyCostUsd = table.Column<decimal>(type: "numeric", nullable: false),
                    CostSpentUsd = table.Column<decimal>(type: "numeric", nullable: false),
                    IsHardCapEnforced = table.Column<bool>(type: "boolean", nullable: false),
                    CurrentMonthStartUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AiAgentBudgets", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AiToolExecutionLogs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    AgentName = table.Column<string>(type: "text", nullable: false),
                    ToolName = table.Column<string>(type: "text", nullable: false),
                    ArgumentsJson = table.Column<string>(type: "text", nullable: false),
                    ResultJson = table.Column<string>(type: "text", nullable: false),
                    ExecutionDurationMs = table.Column<int>(type: "integer", nullable: false),
                    TokensConsumed = table.Column<int>(type: "integer", nullable: false),
                    CostUsd = table.Column<decimal>(type: "numeric", nullable: false),
                    ApprovalStatus = table.Column<int>(type: "integer", nullable: false),
                    ApprovedByUserId = table.Column<string>(type: "text", nullable: true),
                    ExecutedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AiToolExecutionLogs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AndroidSmsDispatchRecords",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    DeviceId = table.Column<Guid>(type: "uuid", nullable: false),
                    SimSlot = table.Column<int>(type: "integer", nullable: false),
                    RecipientPhone = table.Column<string>(type: "text", nullable: false),
                    MessageBody = table.Column<string>(type: "text", nullable: false),
                    DispatchStatus = table.Column<string>(type: "text", nullable: false),
                    ErrorMessage = table.Column<string>(type: "text", nullable: true),
                    SentAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DeliveredAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AndroidSmsDispatchRecords", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AndroidSmsGatewayDevices",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    DeviceName = table.Column<string>(type: "text", nullable: false),
                    DeviceIdentifier = table.Column<string>(type: "text", nullable: false),
                    FcmToken = table.Column<string>(type: "text", nullable: false),
                    Sim1Operator = table.Column<string>(type: "text", nullable: false),
                    Sim2Operator = table.Column<string>(type: "text", nullable: false),
                    BatteryPercent = table.Column<int>(type: "integer", nullable: false),
                    DailySentCount = table.Column<int>(type: "integer", nullable: false),
                    DailyQuotaLimit = table.Column<int>(type: "integer", nullable: false),
                    IsOnline = table.Column<bool>(type: "boolean", nullable: false),
                    LastHeartbeatUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AndroidSmsGatewayDevices", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AutonomousFunnelRuns",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    ConceptPrompt = table.Column<string>(type: "text", nullable: false),
                    TargetSku = table.Column<string>(type: "text", nullable: true),
                    Status = table.Column<string>(type: "text", nullable: false),
                    CurrentStepNumber = table.Column<int>(type: "integer", nullable: false),
                    CurrentStepName = table.Column<string>(type: "text", nullable: false),
                    ProgressPercentage = table.Column<int>(type: "integer", nullable: false),
                    GeneratedTitle = table.Column<string>(type: "text", nullable: false),
                    GeneratedHeadline = table.Column<string>(type: "text", nullable: false),
                    ValueProposition = table.Column<string>(type: "text", nullable: false),
                    GeneratedHtmlPreview = table.Column<string>(type: "text", nullable: false),
                    PerformanceScore = table.Column<int>(type: "integer", nullable: false),
                    SeoScore = table.Column<int>(type: "integer", nullable: false),
                    AccessibilityScore = table.Column<int>(type: "integer", nullable: false),
                    DeploymentUrl = table.Column<string>(type: "text", nullable: false),
                    GitHubPrUrl = table.Column<string>(type: "text", nullable: true),
                    GitHubBranchName = table.Column<string>(type: "text", nullable: true),
                    StartedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CompletedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AutonomousFunnelRuns", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "B2BCreditAccounts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uuid", nullable: false),
                    CompanyName = table.Column<string>(type: "text", nullable: false),
                    CreditLimit = table.Column<decimal>(type: "numeric", nullable: false),
                    OutstandingBalance = table.Column<decimal>(type: "numeric", nullable: false),
                    PaymentTermsDays = table.Column<int>(type: "integer", nullable: false),
                    CreditScoreRating = table.Column<string>(type: "text", nullable: false),
                    IsFrozen = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_B2BCreditAccounts", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "BillingInvoiceRecords",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    InvoiceNumber = table.Column<string>(type: "text", nullable: false),
                    AmountDue = table.Column<decimal>(type: "numeric", nullable: false),
                    Amount = table.Column<decimal>(type: "numeric", nullable: false),
                    Currency = table.Column<string>(type: "text", nullable: false),
                    Status = table.Column<string>(type: "text", nullable: false),
                    StripeInvoiceId = table.Column<string>(type: "text", nullable: false),
                    HostedInvoiceUrl = table.Column<string>(type: "text", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BillingInvoiceRecords", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "BookingEventTypes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    Title = table.Column<string>(type: "text", nullable: false),
                    Slug = table.Column<string>(type: "text", nullable: false),
                    Description = table.Column<string>(type: "text", nullable: false),
                    DurationMinutes = table.Column<int>(type: "integer", nullable: false),
                    BufferBeforeMinutes = table.Column<int>(type: "integer", nullable: false),
                    BufferAfterMinutes = table.Column<int>(type: "integer", nullable: false),
                    MinimumNoticeHours = table.Column<int>(type: "integer", nullable: false),
                    IsRoundRobin = table.Column<bool>(type: "boolean", nullable: false),
                    AssignedHostIdsJson = table.Column<string>(type: "text", nullable: false),
                    HostWeightsJson = table.Column<string>(type: "text", nullable: false),
                    ColorHex = table.Column<string>(type: "text", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BookingEventTypes", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "BranchRecords",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    Region = table.Column<string>(type: "text", nullable: false),
                    City = table.Column<string>(type: "text", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BranchRecords", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CalendarBookingRecords",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    EventTypeId = table.Column<Guid>(type: "uuid", nullable: false),
                    HostUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    HostName = table.Column<string>(type: "text", nullable: false),
                    AttendeeName = table.Column<string>(type: "text", nullable: false),
                    AttendeeEmail = table.Column<string>(type: "text", nullable: false),
                    AttendeePhone = table.Column<string>(type: "text", nullable: false),
                    StartTimeUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    EndTimeUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    TimeZone = table.Column<string>(type: "text", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    MeetingUrl = table.Column<string>(type: "text", nullable: false),
                    IcsUid = table.Column<string>(type: "text", nullable: false),
                    Notes = table.Column<string>(type: "text", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CalendarBookingRecords", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CallCenterOrderLocks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    OrderId = table.Column<Guid>(type: "uuid", nullable: false),
                    OrderNumber = table.Column<string>(type: "text", nullable: false),
                    LockedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    LockedByUserName = table.Column<string>(type: "text", nullable: false),
                    LockedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ExpiresAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CallCenterOrderLocks", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CampaignAdSpendRecords",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    Platform = table.Column<int>(type: "integer", nullable: false),
                    CampaignId = table.Column<string>(type: "text", nullable: false),
                    CampaignName = table.Column<string>(type: "text", nullable: false),
                    AdSetId = table.Column<string>(type: "text", nullable: false),
                    AdSetName = table.Column<string>(type: "text", nullable: false),
                    SpendAmount = table.Column<decimal>(type: "numeric", nullable: false),
                    Currency = table.Column<string>(type: "text", nullable: false),
                    DateUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Impressions = table.Column<int>(type: "integer", nullable: false),
                    Clicks = table.Column<int>(type: "integer", nullable: false),
                    ReportedPixelPurchases = table.Column<int>(type: "integer", nullable: false),
                    ReportedPixelRevenue = table.Column<decimal>(type: "numeric", nullable: false),
                    IngestedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CampaignAdSpendRecords", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CartReservationLocks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    WarehouseId = table.Column<Guid>(type: "uuid", nullable: false),
                    Sku = table.Column<string>(type: "text", nullable: false),
                    Quantity = table.Column<int>(type: "integer", nullable: false),
                    CartId = table.Column<Guid>(type: "uuid", nullable: false),
                    OrderId = table.Column<Guid>(type: "uuid", nullable: true),
                    Status = table.Column<string>(type: "text", nullable: false),
                    ExpiresAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ReleasedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CartReservationLocks", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ClientPortalFeedbacks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uuid", nullable: false),
                    TaskId = table.Column<Guid>(type: "uuid", nullable: false),
                    ClientName = table.Column<string>(type: "text", nullable: false),
                    ClientEmail = table.Column<string>(type: "text", nullable: false),
                    StatusVote = table.Column<string>(type: "text", nullable: false),
                    FeedbackNotes = table.Column<string>(type: "text", nullable: false),
                    SubmittedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ClientPortalFeedbacks", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ClientPortalTokens",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uuid", nullable: false),
                    Token = table.Column<string>(type: "text", nullable: false),
                    ClientContactEmail = table.Column<string>(type: "text", nullable: false),
                    ClientOrganizationName = table.Column<string>(type: "text", nullable: false),
                    ExpiresAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    IsRevoked = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ClientPortalTokens", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CodCheckoutSessions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    SessionKey = table.Column<string>(type: "text", nullable: false),
                    CartItemsJson = table.Column<string>(type: "text", nullable: false),
                    Subtotal = table.Column<decimal>(type: "numeric", nullable: false),
                    ShippingFee = table.Column<decimal>(type: "numeric", nullable: false),
                    DiscountAmount = table.Column<decimal>(type: "numeric", nullable: false),
                    CustomerName = table.Column<string>(type: "text", nullable: false),
                    CustomerPhone = table.Column<string>(type: "text", nullable: false),
                    WilayaCode = table.Column<string>(type: "text", nullable: false),
                    WilayaName = table.Column<string>(type: "text", nullable: false),
                    DelegationName = table.Column<string>(type: "text", nullable: false),
                    StreetAddress = table.Column<string>(type: "text", nullable: false),
                    OtpCode = table.Column<string>(type: "text", nullable: false),
                    IsOtpVerified = table.Column<bool>(type: "boolean", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    PlacedOrderId = table.Column<string>(type: "text", nullable: true),
                    UpsellOfferAcceptedSku = table.Column<string>(type: "text", nullable: true),
                    UpsellAmount = table.Column<decimal>(type: "numeric", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CompletedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CodCheckoutSessions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CodNetMarginLedgerEntries",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    OrderId = table.Column<Guid>(type: "uuid", nullable: false),
                    OrderNumber = table.Column<string>(type: "text", nullable: false),
                    GrossRevenue = table.Column<decimal>(type: "numeric", nullable: false),
                    Cogs = table.Column<decimal>(type: "numeric", nullable: false),
                    PackagingCost = table.Column<decimal>(type: "numeric", nullable: false),
                    AdSpendCost = table.Column<decimal>(type: "numeric", nullable: false),
                    CourierShippingFee = table.Column<decimal>(type: "numeric", nullable: false),
                    ReturnRatePercent = table.Column<decimal>(type: "numeric", nullable: false),
                    ReturnPenaltyFee = table.Column<decimal>(type: "numeric", nullable: false),
                    ReturnRiskCost = table.Column<decimal>(type: "numeric", nullable: false),
                    NetProfit = table.Column<decimal>(type: "numeric", nullable: false),
                    NetMarginPercent = table.Column<decimal>(type: "numeric", nullable: false),
                    CalculatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CodNetMarginLedgerEntries", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CommunityFeedbackPosts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    Title = table.Column<string>(type: "text", nullable: false),
                    Slug = table.Column<string>(type: "text", nullable: false),
                    DescriptionMarkdown = table.Column<string>(type: "text", nullable: false),
                    UpvoteCount = table.Column<int>(type: "integer", nullable: false),
                    RoadmapStatus = table.Column<string>(type: "text", nullable: false),
                    CanonicalPostId = table.Column<Guid>(type: "uuid", nullable: true),
                    AuthorEmail = table.Column<string>(type: "text", nullable: false),
                    OfficialResponse = table.Column<string>(type: "text", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CommunityFeedbackPosts", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CompetitorAdRecords",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    CompetitorName = table.Column<string>(type: "text", nullable: false),
                    FanPageId = table.Column<string>(type: "text", nullable: false),
                    Platform = table.Column<string>(type: "text", nullable: false),
                    AdCreativeId = table.Column<string>(type: "text", nullable: false),
                    HookSnippet = table.Column<string>(type: "text", nullable: false),
                    OfferDetails = table.Column<string>(type: "text", nullable: false),
                    EstimatedDailySpend = table.Column<decimal>(type: "numeric", nullable: false),
                    ThreatLevel = table.Column<string>(type: "text", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    FirstSeenAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    LastSeenAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CompetitorAdRecords", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CompetitorPriceTrackers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    TargetSku = table.Column<string>(type: "text", nullable: false),
                    CompetitorName = table.Column<string>(type: "text", nullable: false),
                    CompetitorUrl = table.Column<string>(type: "text", nullable: false),
                    CompetitorPrice = table.Column<decimal>(type: "numeric", nullable: false),
                    OurPrice = table.Column<decimal>(type: "numeric", nullable: false),
                    PriceDifferencePercentage = table.Column<decimal>(type: "numeric", nullable: false),
                    RecommendedCounterAction = table.Column<string>(type: "text", nullable: false),
                    LastCheckedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CompetitorPriceTrackers", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ConversationPresenceSessions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    ConversationId = table.Column<Guid>(type: "uuid", nullable: false),
                    AgentUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    AgentName = table.Column<string>(type: "text", nullable: false),
                    IsTyping = table.Column<bool>(type: "boolean", nullable: false),
                    LastHeartbeatUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ExpiresAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ConversationPresenceSessions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CrawlerSecretRecords",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    Provider = table.Column<string>(type: "text", nullable: false),
                    MaskedApiKey = table.Column<string>(type: "text", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    HealthStatus = table.Column<string>(type: "text", nullable: false),
                    LastLatencyMs = table.Column<int>(type: "integer", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CrawlerSecretRecords", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CryptographicPdfRecords",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    DocumentTitle = table.Column<string>(type: "text", nullable: false),
                    DocumentContent = table.Column<string>(type: "text", nullable: false),
                    OriginalSha256 = table.Column<string>(type: "text", nullable: false),
                    SealedSha256 = table.Column<string>(type: "text", nullable: false),
                    SignaturePlacementJson = table.Column<string>(type: "text", nullable: false),
                    AuditTrailHashChainJson = table.Column<string>(type: "text", nullable: false),
                    Status = table.Column<string>(type: "text", nullable: false),
                    SealedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CryptographicPdfRecords", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CsatSurveyResults",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    ConversationId = table.Column<Guid>(type: "uuid", nullable: false),
                    CustomerContactId = table.Column<Guid>(type: "uuid", nullable: false),
                    CustomerName = table.Column<string>(type: "text", nullable: false),
                    Score = table.Column<int>(type: "integer", nullable: false),
                    Channel = table.Column<string>(type: "text", nullable: false),
                    FeedbackText = table.Column<string>(type: "text", nullable: false),
                    RecordedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CsatSurveyResults", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CurrencyRates",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CurrencyCode = table.Column<string>(type: "text", nullable: false),
                    RateToBaseUsd = table.Column<decimal>(type: "numeric", nullable: false),
                    LastUpdatedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CurrencyRates", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CustomFieldDefinitions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    EntityType = table.Column<string>(type: "text", nullable: false),
                    FieldName = table.Column<string>(type: "text", nullable: false),
                    FieldLabel = table.Column<string>(type: "text", nullable: false),
                    FieldType = table.Column<int>(type: "integer", nullable: false),
                    ValidationRegex = table.Column<string>(type: "text", nullable: true),
                    IsRequired = table.Column<bool>(type: "boolean", nullable: false),
                    DefaultValue = table.Column<string>(type: "text", nullable: true),
                    OptionsJson = table.Column<string>(type: "text", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CustomFieldDefinitions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CustomFieldValues",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    EntityId = table.Column<Guid>(type: "uuid", nullable: false),
                    FieldDefinitionId = table.Column<Guid>(type: "uuid", nullable: false),
                    ValueString = table.Column<string>(type: "text", nullable: true),
                    ValueNumber = table.Column<decimal>(type: "numeric", nullable: true),
                    ValueBoolean = table.Column<bool>(type: "boolean", nullable: true),
                    ValueDateTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CustomFieldValues", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "DealMarginRecords",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    DealId = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    DealTitle = table.Column<string>(type: "text", nullable: false),
                    Revenue = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    CostOfGoodsSold = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    CourierShippingFees = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    OperatorLaborHours = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    OperatorHourlyRate = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    OperatorLaborCost = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    TrueGrossMargin = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    GrossMarginPercentage = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    CalculatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DealMarginRecords", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "DeliveryDrivers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    FullName = table.Column<string>(type: "text", nullable: false),
                    PhoneNumber = table.Column<string>(type: "text", nullable: false),
                    VehicleType = table.Column<string>(type: "text", nullable: false),
                    LicensePlate = table.Column<string>(type: "text", nullable: false),
                    HubWarehouseId = table.Column<Guid>(type: "uuid", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    CashInHandBalance = table.Column<decimal>(type: "numeric", nullable: false),
                    TotalDeliveriesCompleted = table.Column<int>(type: "integer", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DeliveryDrivers", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "DigitalAssets",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    Title = table.Column<string>(type: "text", nullable: false),
                    FileName = table.Column<string>(type: "text", nullable: false),
                    StoragePath = table.Column<string>(type: "text", nullable: false),
                    MimeType = table.Column<string>(type: "text", nullable: false),
                    FileSizeBytes = table.Column<long>(type: "bigint", nullable: false),
                    MaxAllowedDownloads = table.Column<int>(type: "integer", nullable: false),
                    ExpiryHours = table.Column<int>(type: "integer", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DigitalAssets", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "DigitalDownloadTokens",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    Token = table.Column<string>(type: "text", nullable: false),
                    AssetId = table.Column<Guid>(type: "uuid", nullable: false),
                    OrderId = table.Column<Guid>(type: "uuid", nullable: true),
                    ContactId = table.Column<Guid>(type: "uuid", nullable: true),
                    DownloadCount = table.Column<int>(type: "integer", nullable: false),
                    MaxAllowedDownloads = table.Column<int>(type: "integer", nullable: false),
                    ExpiresAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    IsRevoked = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DigitalDownloadTokens", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "DocumentAccessGates",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    DocumentId = table.Column<Guid>(type: "uuid", nullable: false),
                    IsEmailRequired = table.Column<bool>(type: "boolean", nullable: false),
                    PasscodeHash = table.Column<string>(type: "text", nullable: true),
                    IsWatermarkEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    WatermarkPattern = table.Column<string>(type: "text", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DocumentAccessGates", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "DocumentDwellTelemetries",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    DocumentId = table.Column<Guid>(type: "uuid", nullable: false),
                    PageNumber = table.Column<int>(type: "integer", nullable: false),
                    ViewerEmail = table.Column<string>(type: "text", nullable: false),
                    ViewerIp = table.Column<string>(type: "text", nullable: false),
                    DwellTimeSeconds = table.Column<int>(type: "integer", nullable: false),
                    MaxScrollPercent = table.Column<int>(type: "integer", nullable: false),
                    CapturedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DocumentDwellTelemetries", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "DocumentEnvelopes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    Title = table.Column<string>(type: "text", nullable: false),
                    DocumentUrl = table.Column<string>(type: "text", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    ChecksumSha256 = table.Column<string>(type: "text", nullable: false),
                    IsSequentialOrder = table.Column<bool>(type: "boolean", nullable: false),
                    AuditCertificateJson = table.Column<string>(type: "text", nullable: false),
                    CompletedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DocumentEnvelopes", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "DomainDnsHealthRecords",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    DomainName = table.Column<string>(type: "text", nullable: false),
                    HasSpf = table.Column<bool>(type: "boolean", nullable: false),
                    SpfRecord = table.Column<string>(type: "text", nullable: false),
                    HasDkim = table.Column<bool>(type: "boolean", nullable: false),
                    DkimSelector = table.Column<string>(type: "text", nullable: false),
                    HasDmarc = table.Column<bool>(type: "boolean", nullable: false),
                    DmarcPolicy = table.Column<string>(type: "text", nullable: false),
                    HasMx = table.Column<bool>(type: "boolean", nullable: false),
                    MxHost = table.Column<string>(type: "text", nullable: false),
                    OverallHealthScore = table.Column<int>(type: "integer", nullable: false),
                    OverallScore = table.Column<int>(type: "integer", nullable: false),
                    DmarcRecord = table.Column<string>(type: "text", nullable: false),
                    IssuesJson = table.Column<string>(type: "text", nullable: false),
                    LastCheckedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DomainDnsHealthRecords", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "DoorstepProofOfDeliveries",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    RunSheetStopId = table.Column<Guid>(type: "uuid", nullable: false),
                    OrderId = table.Column<Guid>(type: "uuid", nullable: false),
                    OrderNumber = table.Column<string>(type: "text", nullable: false),
                    DriverId = table.Column<Guid>(type: "uuid", nullable: false),
                    CustomerSignatureBase64 = table.Column<string>(type: "text", nullable: false),
                    ParcelPhotoUrl = table.Column<string>(type: "text", nullable: false),
                    CashCollected = table.Column<decimal>(type: "numeric", nullable: false),
                    PaymentMethod = table.Column<string>(type: "text", nullable: false),
                    RecipientName = table.Column<string>(type: "text", nullable: false),
                    RecipientRelation = table.Column<string>(type: "text", nullable: false),
                    GpsLatitude = table.Column<double>(type: "double precision", nullable: false),
                    GpsLongitude = table.Column<double>(type: "double precision", nullable: false),
                    DeliveredAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
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
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    RunSheetId = table.Column<Guid>(type: "uuid", nullable: false),
                    DriverId = table.Column<Guid>(type: "uuid", nullable: false),
                    ExpectedCash = table.Column<decimal>(type: "numeric", nullable: false),
                    ActualCashTurnedIn = table.Column<decimal>(type: "numeric", nullable: false),
                    Notes = table.Column<string>(type: "text", nullable: false),
                    ReconciledBy = table.Column<string>(type: "text", nullable: false),
                    ReconciledAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
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
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    DriverId = table.Column<Guid>(type: "uuid", nullable: false),
                    DriverName = table.Column<string>(type: "text", nullable: false),
                    SessionToken = table.Column<string>(type: "text", nullable: false),
                    DeviceModel = table.Column<string>(type: "text", nullable: false),
                    LastGpsLatitude = table.Column<double>(type: "double precision", nullable: false),
                    LastGpsLongitude = table.Column<double>(type: "double precision", nullable: false),
                    BatteryLevelPercent = table.Column<int>(type: "integer", nullable: false),
                    IsOnline = table.Column<bool>(type: "boolean", nullable: false),
                    LastSyncAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
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
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    DriverId = table.Column<Guid>(type: "uuid", nullable: false),
                    HubWarehouseId = table.Column<Guid>(type: "uuid", nullable: false),
                    SheetNumber = table.Column<string>(type: "text", nullable: false),
                    DateUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    TotalParcels = table.Column<int>(type: "integer", nullable: false),
                    DeliveredParcels = table.Column<int>(type: "integer", nullable: false),
                    RefusedParcels = table.Column<int>(type: "integer", nullable: false),
                    TotalCashToCollect = table.Column<decimal>(type: "numeric", nullable: false),
                    TotalCashActuallyCollected = table.Column<decimal>(type: "numeric", nullable: false),
                    StartedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CompletedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DriverRunSheets", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "DynamicFormDefinitions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    Title = table.Column<string>(type: "text", nullable: false),
                    Slug = table.Column<string>(type: "text", nullable: false),
                    Description = table.Column<string>(type: "text", nullable: false),
                    FieldsSchemaJson = table.Column<string>(type: "text", nullable: false),
                    SubmitAction = table.Column<string>(type: "text", nullable: false),
                    ActionPayload = table.Column<string>(type: "text", nullable: false),
                    SubmissionCount = table.Column<int>(type: "integer", nullable: false),
                    IsPublished = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DynamicFormDefinitions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "DynamicFormSubmissions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    FormId = table.Column<Guid>(type: "uuid", nullable: false),
                    RespondentContactId = table.Column<Guid>(type: "uuid", nullable: true),
                    RespondentIp = table.Column<string>(type: "text", nullable: false),
                    AnswersJson = table.Column<string>(type: "text", nullable: false),
                    CompletedInSeconds = table.Column<int>(type: "integer", nullable: false),
                    SubmittedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DynamicFormSubmissions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "DynamicMarkdownPolicies",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    Tier = table.Column<int>(type: "integer", nullable: false),
                    MinDaysInStock = table.Column<int>(type: "integer", nullable: false),
                    MaxDaysInStock = table.Column<int>(type: "integer", nullable: false),
                    RecommendedDiscountPercentage = table.Column<decimal>(type: "numeric", nullable: false),
                    MinimumMarginFloorPercentage = table.Column<decimal>(type: "numeric", nullable: false),
                    AutoApplyToStorefront = table.Column<bool>(type: "boolean", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DynamicMarkdownPolicies", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "EmailSpamAnalysisReports",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    Subject = table.Column<string>(type: "text", nullable: false),
                    BodyHtmlPreview = table.Column<string>(type: "text", nullable: false),
                    SpamScore = table.Column<int>(type: "integer", nullable: false),
                    TriggerWordsDetectedJson = table.Column<string>(type: "text", nullable: false),
                    HasUnsubscribeHeader = table.Column<bool>(type: "boolean", nullable: false),
                    ImageToTextRatio = table.Column<double>(type: "double precision", nullable: false),
                    Verdict = table.Column<string>(type: "text", nullable: false),
                    RiskLevel = table.Column<string>(type: "text", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EmailSpamAnalysisReports", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "EnrichmentExecutionLogs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    ContactId = table.Column<Guid>(type: "uuid", nullable: true),
                    ProspectQuery = table.Column<string>(type: "text", nullable: false),
                    ProviderUsed = table.Column<string>(type: "text", nullable: false),
                    StepIndex = table.Column<int>(type: "integer", nullable: false),
                    OutcomeStatus = table.Column<string>(type: "text", nullable: false),
                    ConfidenceScore = table.Column<decimal>(type: "numeric", nullable: false),
                    CreditCostCharged = table.Column<int>(type: "integer", nullable: false),
                    LatencyMs = table.Column<long>(type: "bigint", nullable: false),
                    EnrichedPayloadJson = table.Column<string>(type: "text", nullable: false),
                    ExecutedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EnrichmentExecutionLogs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "FactoringReceivableRecords",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    CourierProviderId = table.Column<string>(type: "text", nullable: false),
                    CourierName = table.Column<string>(type: "text", nullable: false),
                    InTransitCashFloat = table.Column<decimal>(type: "numeric", nullable: false),
                    VerifiedDeliveredCash = table.Column<decimal>(type: "numeric", nullable: false),
                    AdvanceDiscountRatePercentage = table.Column<decimal>(type: "numeric", nullable: false),
                    Status = table.Column<string>(type: "text", nullable: false),
                    FundedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FactoringReceivableRecords", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "FeatureFeedbackPosts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    Title = table.Column<string>(type: "text", nullable: false),
                    Description = table.Column<string>(type: "text", nullable: false),
                    Category = table.Column<string>(type: "text", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    UpvoteCount = table.Column<int>(type: "integer", nullable: false),
                    AuthorEmail = table.Column<string>(type: "text", nullable: false),
                    AuthorName = table.Column<string>(type: "text", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FeatureFeedbackPosts", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "FeatureFeedbackVotes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    PostId = table.Column<Guid>(type: "uuid", nullable: false),
                    VoterIdentifier = table.Column<string>(type: "text", nullable: false),
                    VotedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FeatureFeedbackVotes", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "FeedbackPostVotes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    PostId = table.Column<Guid>(type: "uuid", nullable: false),
                    VoterEmail = table.Column<string>(type: "text", nullable: false),
                    VotedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FeedbackPostVotes", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "FlowDefinitions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    TriggerKeyword = table.Column<string>(type: "text", nullable: false),
                    Description = table.Column<string>(type: "text", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    NodesJson = table.Column<string>(type: "text", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FlowDefinitions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "FlowExecutionSessions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    FlowDefinitionId = table.Column<Guid>(type: "uuid", nullable: false),
                    CustomerPhone = table.Column<string>(type: "text", nullable: false),
                    CurrentNodeKey = table.Column<string>(type: "text", nullable: false),
                    VariablesJson = table.Column<string>(type: "text", nullable: false),
                    DetectedLanguage = table.Column<string>(type: "text", nullable: false),
                    SentimentScore = table.Column<decimal>(type: "numeric", nullable: false),
                    IsEscalatedToHuman = table.Column<bool>(type: "boolean", nullable: false),
                    StartedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    LastInteractionAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FlowExecutionSessions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "FunnelConversionEvents",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    FunnelId = table.Column<Guid>(type: "uuid", nullable: false),
                    StepId = table.Column<Guid>(type: "uuid", nullable: true),
                    EventType = table.Column<int>(type: "integer", nullable: false),
                    OrderId = table.Column<Guid>(type: "uuid", nullable: true),
                    CustomerPhone = table.Column<string>(type: "text", nullable: true),
                    EventValue = table.Column<decimal>(type: "numeric", nullable: false),
                    CapturedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FunnelConversionEvents", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "FunnelSplitTests",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    FunnelRunId = table.Column<Guid>(type: "uuid", nullable: false),
                    Status = table.Column<string>(type: "text", nullable: false),
                    VariantA_Name = table.Column<string>(type: "text", nullable: false),
                    VariantA_Headline = table.Column<string>(type: "text", nullable: false),
                    VariantA_TrafficRatio = table.Column<double>(type: "double precision", nullable: false),
                    VariantA_Views = table.Column<int>(type: "integer", nullable: false),
                    VariantA_Conversions = table.Column<int>(type: "integer", nullable: false),
                    VariantB_Name = table.Column<string>(type: "text", nullable: false),
                    VariantB_Headline = table.Column<string>(type: "text", nullable: false),
                    VariantB_TrafficRatio = table.Column<double>(type: "double precision", nullable: false),
                    VariantB_Views = table.Column<int>(type: "integer", nullable: false),
                    VariantB_Conversions = table.Column<int>(type: "integer", nullable: false),
                    WinnerVariant = table.Column<string>(type: "text", nullable: true),
                    ConfidenceScore = table.Column<double>(type: "double precision", nullable: false),
                    CalculatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    RowVersion = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FunnelSplitTests", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "FunnelSteps",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    FunnelId = table.Column<Guid>(type: "uuid", nullable: false),
                    StepName = table.Column<string>(type: "text", nullable: false),
                    StepType = table.Column<int>(type: "integer", nullable: false),
                    StepOrder = table.Column<int>(type: "integer", nullable: false),
                    TargetUrlOrSlug = table.Column<string>(type: "text", nullable: false),
                    PageTemplateJson = table.Column<string>(type: "text", nullable: false),
                    VisitorsCount = table.Column<int>(type: "integer", nullable: false),
                    ConversionsCount = table.Column<int>(type: "integer", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FunnelSteps", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "HostAvailabilitySchedules",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    HostUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    HostName = table.Column<string>(type: "text", nullable: false),
                    TimeZone = table.Column<string>(type: "text", nullable: false),
                    WeeklyHoursJson = table.Column<string>(type: "text", nullable: false),
                    BlackoutDatesJson = table.Column<string>(type: "text", nullable: false),
                    Weight = table.Column<double>(type: "double precision", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HostAvailabilitySchedules", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "InAppMicroSurveyRecords",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    Title = table.Column<string>(type: "text", nullable: false),
                    TriggerAction = table.Column<string>(type: "text", nullable: false),
                    RecontactDays = table.Column<int>(type: "integer", nullable: false),
                    DisplayLimitPercent = table.Column<int>(type: "integer", nullable: false),
                    StepsJson = table.Column<string>(type: "text", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InAppMicroSurveyRecords", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Invoices",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    DealId = table.Column<Guid>(type: "uuid", nullable: true),
                    ContactId = table.Column<Guid>(type: "uuid", nullable: true),
                    InvoiceNumber = table.Column<string>(type: "text", nullable: false),
                    ClientName = table.Column<string>(type: "text", nullable: false),
                    ClientEmail = table.Column<string>(type: "text", nullable: true),
                    Currency = table.Column<string>(type: "text", nullable: false),
                    ExchangeRate = table.Column<decimal>(type: "numeric", nullable: false),
                    SubTotal = table.Column<decimal>(type: "numeric", nullable: false),
                    TaxPercentage = table.Column<decimal>(type: "numeric", nullable: false),
                    TaxAmount = table.Column<decimal>(type: "numeric", nullable: false),
                    TotalAmount = table.Column<decimal>(type: "numeric", nullable: false),
                    TotalBaseAmountUsd = table.Column<decimal>(type: "numeric", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    DueDateUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    PaidAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Notes = table.Column<string>(type: "text", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Invoices", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "JourneyDefinitions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    Description = table.Column<string>(type: "text", nullable: false),
                    TriggerEvent = table.Column<string>(type: "text", nullable: false),
                    ConversionEventsJson = table.Column<string>(type: "text", nullable: false),
                    NodesJson = table.Column<string>(type: "text", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_JourneyDefinitions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "JourneyInstances",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    JourneyDefinitionId = table.Column<Guid>(type: "uuid", nullable: false),
                    ContactId = table.Column<Guid>(type: "uuid", nullable: false),
                    CurrentNodeId = table.Column<string>(type: "text", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    WaitUntilEventName = table.Column<string>(type: "text", nullable: true),
                    WaitUntilExpiresAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    NextExecutionAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    AttributesJson = table.Column<string>(type: "text", nullable: false),
                    ExecutionHistoryJson = table.Column<string>(type: "text", nullable: false),
                    EnrolledAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CompletedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_JourneyInstances", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "LiquidationBundleOffers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    BundleName = table.Column<string>(type: "text", nullable: false),
                    WinningProductSku = table.Column<string>(type: "text", nullable: false),
                    AgingProductSku = table.Column<string>(type: "text", nullable: false),
                    OriginalCombinedPrice = table.Column<decimal>(type: "numeric", nullable: false),
                    DiscountedBundlePrice = table.Column<decimal>(type: "numeric", nullable: false),
                    IncentiveDiscountPercentage = table.Column<decimal>(type: "numeric", nullable: false),
                    ActiveInChatCpq = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LiquidationBundleOffers", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "MailboxWarmupCadences",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    SenderEmail = table.Column<string>(type: "text", nullable: false),
                    DailyTargetLimit = table.Column<int>(type: "integer", nullable: false),
                    DailyTarget = table.Column<int>(type: "integer", nullable: false),
                    CurrentDayNumber = table.Column<int>(type: "integer", nullable: false),
                    WarmupRampCurve = table.Column<string>(type: "text", nullable: false),
                    TotalSimulatedSent = table.Column<int>(type: "integer", nullable: false),
                    TotalSentLifetime = table.Column<int>(type: "integer", nullable: false),
                    CurrentDailyVolume = table.Column<int>(type: "integer", nullable: false),
                    SentToday = table.Column<int>(type: "integer", nullable: false),
                    TotalSimulatedReceived = table.Column<int>(type: "integer", nullable: false),
                    SpamRescueCount = table.Column<int>(type: "integer", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    StartedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MailboxWarmupCadences", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "MediaVaultRecords",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    FileName = table.Column<string>(type: "text", nullable: false),
                    ContentType = table.Column<string>(type: "text", nullable: false),
                    FileSizeBytes = table.Column<long>(type: "bigint", nullable: false),
                    StorageKey = table.Column<string>(type: "text", nullable: false),
                    Status = table.Column<string>(type: "text", nullable: false),
                    ExpiresAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MediaVaultRecords", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "MicroSurveyResponsePartials",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    SurveyId = table.Column<Guid>(type: "uuid", nullable: false),
                    RespondentUserId = table.Column<string>(type: "text", nullable: false),
                    AnswersJson = table.Column<string>(type: "text", nullable: false),
                    LastCompletedStepIndex = table.Column<int>(type: "integer", nullable: false),
                    IsFinished = table.Column<bool>(type: "boolean", nullable: false),
                    DropoffStep = table.Column<string>(type: "text", nullable: false),
                    StartedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    LastUpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MicroSurveyResponsePartials", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "NewsletterBroadcasts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    ArticleId = table.Column<Guid>(type: "uuid", nullable: false),
                    Subject = table.Column<string>(type: "text", nullable: false),
                    RecipientSegment = table.Column<string>(type: "text", nullable: false),
                    TotalRecipientsCount = table.Column<int>(type: "integer", nullable: false),
                    DeliveredCount = table.Column<int>(type: "integer", nullable: false),
                    OpenedCount = table.Column<int>(type: "integer", nullable: false),
                    ClickedCount = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    SentAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NewsletterBroadcasts", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "OneClickUpsellOffers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    FunnelId = table.Column<Guid>(type: "uuid", nullable: false),
                    StepId = table.Column<Guid>(type: "uuid", nullable: false),
                    OfferTitle = table.Column<string>(type: "text", nullable: false),
                    ProductSku = table.Column<string>(type: "text", nullable: false),
                    Headline = table.Column<string>(type: "text", nullable: false),
                    ImageUrl = table.Column<string>(type: "text", nullable: false),
                    OriginalPrice = table.Column<decimal>(type: "numeric", nullable: false),
                    UpsellPrice = table.Column<decimal>(type: "numeric", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OneClickUpsellOffers", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "OrderBumpOffers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    FunnelId = table.Column<Guid>(type: "uuid", nullable: false),
                    StepId = table.Column<Guid>(type: "uuid", nullable: false),
                    Title = table.Column<string>(type: "text", nullable: false),
                    Headline = table.Column<string>(type: "text", nullable: false),
                    Description = table.Column<string>(type: "text", nullable: false),
                    ProductSku = table.Column<string>(type: "text", nullable: false),
                    OriginalPrice = table.Column<decimal>(type: "numeric", nullable: false),
                    DiscountedPrice = table.Column<decimal>(type: "numeric", nullable: false),
                    BadgeText = table.Column<string>(type: "text", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OrderBumpOffers", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "OrganizationRecords",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    Code = table.Column<string>(type: "text", nullable: false),
                    Currency = table.Column<string>(type: "text", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OrganizationRecords", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PageAuditResults",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    AuditReportId = table.Column<Guid>(type: "uuid", nullable: false),
                    PageUrl = table.Column<string>(type: "text", nullable: false),
                    StatusCode = table.Column<int>(type: "integer", nullable: false),
                    Title = table.Column<string>(type: "text", nullable: false),
                    MetaDescription = table.Column<string>(type: "text", nullable: false),
                    H1Tag = table.Column<string>(type: "text", nullable: false),
                    HasCanonical = table.Column<bool>(type: "boolean", nullable: false),
                    HasOpenGraph = table.Column<bool>(type: "boolean", nullable: false),
                    HasSchemaOrgJsonLd = table.Column<bool>(type: "boolean", nullable: false),
                    LoadTimeMs = table.Column<int>(type: "integer", nullable: false),
                    HealthScore = table.Column<int>(type: "integer", nullable: false),
                    IssuesJson = table.Column<string>(type: "text", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PageAuditResults", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Payments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    InvoiceId = table.Column<Guid>(type: "uuid", nullable: false),
                    Amount = table.Column<decimal>(type: "numeric", nullable: false),
                    Currency = table.Column<string>(type: "text", nullable: false),
                    PaymentMethod = table.Column<string>(type: "text", nullable: false),
                    TransactionReference = table.Column<string>(type: "text", nullable: true),
                    PaidAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Status = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Payments", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PimVariantMatrices",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    BaseSku = table.Column<string>(type: "text", nullable: false),
                    VariantSku = table.Column<string>(type: "text", nullable: false),
                    AttributesJson = table.Column<string>(type: "text", nullable: false),
                    Price = table.Column<decimal>(type: "numeric", nullable: false),
                    StockQuantity = table.Column<int>(type: "integer", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PimVariantMatrices", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ProductLicenseKeys",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    LicenseKey = table.Column<string>(type: "text", nullable: false),
                    OrderId = table.Column<Guid>(type: "uuid", nullable: true),
                    ProductSku = table.Column<string>(type: "text", nullable: false),
                    MaxSeats = table.Column<int>(type: "integer", nullable: false),
                    ActivatedSeats = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<string>(type: "text", nullable: false),
                    ExpiresAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProductLicenseKeys", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Projects",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    Description = table.Column<string>(type: "text", nullable: false),
                    Status = table.Column<string>(type: "text", nullable: false),
                    DealId = table.Column<Guid>(type: "uuid", nullable: true),
                    ContractId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    TargetLaunchDateUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Projects", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ProofOfDeliveryRecords",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    RunSheetStopId = table.Column<Guid>(type: "uuid", nullable: false),
                    CustomerSignatureData = table.Column<string>(type: "text", nullable: false),
                    DoorstepPhotoUrl = table.Column<string>(type: "text", nullable: false),
                    GeoLatitude = table.Column<double>(type: "double precision", nullable: true),
                    GeoLongitude = table.Column<double>(type: "double precision", nullable: true),
                    CapturedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProofOfDeliveryRecords", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PublicationArticles",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    Title = table.Column<string>(type: "text", nullable: false),
                    Slug = table.Column<string>(type: "text", nullable: false),
                    Excerpt = table.Column<string>(type: "text", nullable: false),
                    ContentLexicalAstJson = table.Column<string>(type: "text", nullable: false),
                    HtmlContent = table.Column<string>(type: "text", nullable: false),
                    FeaturedImageUrl = table.Column<string>(type: "text", nullable: false),
                    AuthorName = table.Column<string>(type: "text", nullable: false),
                    Visibility = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    PublishedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ScheduledAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ViewCount = table.Column<int>(type: "integer", nullable: false),
                    EstimatedReadMinutes = table.Column<int>(type: "integer", nullable: false),
                    MetaTitle = table.Column<string>(type: "text", nullable: false),
                    MetaDescription = table.Column<string>(type: "text", nullable: false),
                    CanonicalUrl = table.Column<string>(type: "text", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PublicationArticles", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "RunSheetStops",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    RunSheetId = table.Column<Guid>(type: "uuid", nullable: false),
                    OrderId = table.Column<Guid>(type: "uuid", nullable: false),
                    OrderNumber = table.Column<string>(type: "text", nullable: false),
                    StopSequence = table.Column<int>(type: "integer", nullable: false),
                    CustomerName = table.Column<string>(type: "text", nullable: false),
                    CustomerPhone = table.Column<string>(type: "text", nullable: false),
                    ShippingAddress = table.Column<string>(type: "text", nullable: false),
                    Governorate = table.Column<string>(type: "text", nullable: false),
                    Delegation = table.Column<string>(type: "text", nullable: false),
                    AmountToCollect = table.Column<decimal>(type: "numeric", nullable: false),
                    PaymentMethodAtDoorstep = table.Column<string>(type: "text", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    ActualPaymentCollected = table.Column<decimal>(type: "numeric", nullable: false),
                    ProofOfDeliveryId = table.Column<Guid>(type: "uuid", nullable: true),
                    FailureReason = table.Column<string>(type: "text", nullable: true),
                    DeliveredAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CompletedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RunSheetStops", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SalesCommissionPlans",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    PlanName = table.Column<string>(type: "text", nullable: false),
                    BaseRatePercent = table.Column<decimal>(type: "numeric", nullable: false),
                    QuotaThresholdPercent = table.Column<decimal>(type: "numeric", nullable: false),
                    AcceleratedRatePercent = table.Column<decimal>(type: "numeric", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SalesCommissionPlans", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SalesCommissionStatements",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    PeriodId = table.Column<Guid>(type: "uuid", nullable: false),
                    SalesRepUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    SalesRepName = table.Column<string>(type: "text", nullable: false),
                    DealsClosedCount = table.Column<int>(type: "integer", nullable: false),
                    TotalDealsValue = table.Column<decimal>(type: "numeric", nullable: false),
                    CalculatedCommission = table.Column<decimal>(type: "numeric", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    ApprovedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Notes = table.Column<string>(type: "text", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SalesCommissionStatements", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SalesForecastPeriods",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    StartDateUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    EndDateUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    TargetQuotaAmount = table.Column<decimal>(type: "numeric", nullable: false),
                    ClosedWonRevenue = table.Column<decimal>(type: "numeric", nullable: false),
                    WeightedForecastRevenue = table.Column<decimal>(type: "numeric", nullable: false),
                    UnweightedPipelineRevenue = table.Column<decimal>(type: "numeric", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SalesForecastPeriods", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SalesForecastRepQuotas",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    PeriodId = table.Column<Guid>(type: "uuid", nullable: false),
                    SalesRepUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    SalesRepName = table.Column<string>(type: "text", nullable: false),
                    QuotaAmount = table.Column<decimal>(type: "numeric", nullable: false),
                    ClosedWonAmount = table.Column<decimal>(type: "numeric", nullable: false),
                    CommitAmount = table.Column<decimal>(type: "numeric", nullable: false),
                    BestCaseAmount = table.Column<decimal>(type: "numeric", nullable: false),
                    PipelineAmount = table.Column<decimal>(type: "numeric", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SalesForecastRepQuotas", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SalesFunnels",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    Slug = table.Column<string>(type: "text", nullable: false),
                    Description = table.Column<string>(type: "text", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    TotalVisitors = table.Column<int>(type: "integer", nullable: false),
                    TotalOrders = table.Column<int>(type: "integer", nullable: false),
                    TotalRevenue = table.Column<decimal>(type: "numeric", nullable: false),
                    Currency = table.Column<string>(type: "text", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SalesFunnels", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SeoAuditReports",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    TargetUrl = table.Column<string>(type: "text", nullable: false),
                    OverallHealthScore = table.Column<int>(type: "integer", nullable: false),
                    ScannedPagesCount = table.Column<int>(type: "integer", nullable: false),
                    BrokenLinksCount = table.Column<int>(type: "integer", nullable: false),
                    MissingMetaCount = table.Column<int>(type: "integer", nullable: false),
                    MissingH1Count = table.Column<int>(type: "integer", nullable: false),
                    SlowPagesCount = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CompletedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SeoAuditReports", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SeoMultipageGraphReports",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    AuditReportId = table.Column<Guid>(type: "uuid", nullable: true),
                    RootDomain = table.Column<string>(type: "text", nullable: false),
                    TotalPagesDiscovered = table.Column<int>(type: "integer", nullable: false),
                    OrphanPageCount = table.Column<int>(type: "integer", nullable: false),
                    OrphanPageUrlsJson = table.Column<string>(type: "text", nullable: false),
                    DuplicateTitleCount = table.Column<int>(type: "integer", nullable: false),
                    DuplicateTitleClustersJson = table.Column<string>(type: "text", nullable: false),
                    RedirectChainCount = table.Column<int>(type: "integer", nullable: false),
                    RedirectChainsJson = table.Column<string>(type: "text", nullable: false),
                    EvaluatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SeoMultipageGraphReports", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SerpKeywordTrackings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    Keyword = table.Column<string>(type: "text", nullable: false),
                    TargetUrl = table.Column<string>(type: "text", nullable: false),
                    CountryCode = table.Column<string>(type: "text", nullable: false),
                    SearchEngine = table.Column<string>(type: "text", nullable: false),
                    CurrentRank = table.Column<int>(type: "integer", nullable: true),
                    PreviousRank = table.Column<int>(type: "integer", nullable: true),
                    BestRank = table.Column<int>(type: "integer", nullable: true),
                    SearchVolumeMonthly = table.Column<int>(type: "integer", nullable: false),
                    RankHistoryJson = table.Column<string>(type: "text", nullable: false),
                    TrackedDateUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SerpKeywordTrackings", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SerpRankHistoryRecords",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    KeywordId = table.Column<Guid>(type: "uuid", nullable: true),
                    Keyword = table.Column<string>(type: "text", nullable: false),
                    TargetUrl = table.Column<string>(type: "text", nullable: false),
                    Position = table.Column<int>(type: "integer", nullable: false),
                    PreviousPosition = table.Column<int>(type: "integer", nullable: false),
                    DeltaPosition = table.Column<int>(type: "integer", nullable: false),
                    DeviceType = table.Column<string>(type: "text", nullable: false),
                    SearchEngine = table.Column<string>(type: "text", nullable: false),
                    CountryCode = table.Column<string>(type: "text", nullable: false),
                    SerpFeaturesJson = table.Column<string>(type: "text", nullable: false),
                    RecordedDateUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SerpRankHistoryRecords", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "StockAgingLedgers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProductId = table.Column<Guid>(type: "uuid", nullable: false),
                    Sku = table.Column<string>(type: "text", nullable: false),
                    ProductName = table.Column<string>(type: "text", nullable: false),
                    WarehouseId = table.Column<Guid>(type: "uuid", nullable: false),
                    Quantity = table.Column<int>(type: "integer", nullable: false),
                    UnitCostPrice = table.Column<decimal>(type: "numeric", nullable: false),
                    OriginalRetailPrice = table.Column<decimal>(type: "numeric", nullable: false),
                    DaysInWarehouse = table.Column<int>(type: "integer", nullable: false),
                    Tier = table.Column<int>(type: "integer", nullable: false),
                    CurrentEffectivePrice = table.Column<decimal>(type: "numeric", nullable: false),
                    CurrentMarkdownPercentage = table.Column<decimal>(type: "numeric", nullable: false),
                    ReceivedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    LastEvaluatedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StockAgingLedgers", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "StockTransferManifests",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    ManifestNumber = table.Column<string>(type: "text", nullable: false),
                    SourceWarehouseId = table.Column<Guid>(type: "uuid", nullable: false),
                    SourceWarehouseName = table.Column<string>(type: "text", nullable: false),
                    DestinationWarehouseId = table.Column<Guid>(type: "uuid", nullable: false),
                    DestinationWarehouseName = table.Column<string>(type: "text", nullable: false),
                    Sku = table.Column<string>(type: "text", nullable: false),
                    Quantity = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<string>(type: "text", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StockTransferManifests", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "TaskComments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TaskId = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    AuthorName = table.Column<string>(type: "text", nullable: false),
                    Content = table.Column<string>(type: "text", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TaskComments", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "TaskItems",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    Title = table.Column<string>(type: "text", nullable: false),
                    Description = table.Column<string>(type: "text", nullable: false),
                    Status = table.Column<string>(type: "text", nullable: false),
                    Priority = table.Column<string>(type: "text", nullable: false),
                    AssigneeName = table.Column<string>(type: "text", nullable: true),
                    AssigneeAvatar = table.Column<string>(type: "text", nullable: true),
                    Position = table.Column<int>(type: "integer", nullable: false),
                    SubtasksCompleted = table.Column<int>(type: "integer", nullable: false),
                    SubtasksTotal = table.Column<int>(type: "integer", nullable: false),
                    DueDateUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TaskItems", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "TeamMemberRecords",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TeamId = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    FullName = table.Column<string>(type: "text", nullable: false),
                    Role = table.Column<string>(type: "text", nullable: false),
                    HourlyLaborRate = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TeamMemberRecords", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "TeamRecords",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    BranchId = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    Department = table.Column<string>(type: "text", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TeamRecords", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "TelemetryEvents",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    SessionId = table.Column<string>(type: "text", nullable: false),
                    VisitorId = table.Column<string>(type: "text", nullable: false),
                    EventName = table.Column<string>(type: "text", nullable: false),
                    Url = table.Column<string>(type: "text", nullable: false),
                    Referrer = table.Column<string>(type: "text", nullable: false),
                    DeviceType = table.Column<string>(type: "text", nullable: false),
                    Browser = table.Column<string>(type: "text", nullable: false),
                    CountryCode = table.Column<string>(type: "text", nullable: false),
                    City = table.Column<string>(type: "text", nullable: false),
                    PropertiesJson = table.Column<string>(type: "text", nullable: false),
                    TimestampUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TelemetryEvents", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "TenantApiKeys",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    KeyPrefix = table.Column<string>(type: "text", nullable: false),
                    KeyMasked = table.Column<string>(type: "text", nullable: false),
                    HashedSecret = table.Column<string>(type: "text", nullable: false),
                    AllowedScopesJson = table.Column<string>(type: "text", nullable: false),
                    RateLimitPerMinute = table.Column<int>(type: "integer", nullable: false),
                    IsRevoked = table.Column<bool>(type: "boolean", nullable: false),
                    LastUsedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TenantApiKeys", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "TenantRebillingConfigs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AgencyTenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    SubAccountTenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    SmsMarkupMultiplier = table.Column<decimal>(type: "numeric", nullable: false),
                    VoiceMarkupMultiplier = table.Column<decimal>(type: "numeric", nullable: false),
                    AiTokenMarkupMultiplier = table.Column<decimal>(type: "numeric", nullable: false),
                    AutoRebillThresholdUsd = table.Column<decimal>(type: "numeric", nullable: false),
                    AutoRebillAmountUsd = table.Column<decimal>(type: "numeric", nullable: false),
                    IsAutoRebillEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TenantRebillingConfigs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "TenantSnapshotRecords",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    SnapshotName = table.Column<string>(type: "text", nullable: false),
                    Category = table.Column<string>(type: "text", nullable: false),
                    Version = table.Column<string>(type: "text", nullable: false),
                    BlueprintJson = table.Column<string>(type: "text", nullable: false),
                    Description = table.Column<string>(type: "text", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TenantSnapshotRecords", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "TimesheetEntries",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    TeamMemberId = table.Column<Guid>(type: "uuid", nullable: false),
                    TeamMemberName = table.Column<string>(type: "text", nullable: false),
                    TaskId = table.Column<Guid>(type: "uuid", nullable: true),
                    TaskTitle = table.Column<string>(type: "text", nullable: true),
                    DealId = table.Column<Guid>(type: "uuid", nullable: true),
                    DealTitle = table.Column<string>(type: "text", nullable: true),
                    Description = table.Column<string>(type: "text", nullable: false),
                    HoursWorked = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    HourlyRate = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    TotalLaborCost = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    Status = table.Column<string>(type: "text", nullable: false),
                    WorkDateUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ApprovedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ApprovedBy = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TimesheetEntries", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "UgcVideoScripts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProductSku = table.Column<string>(type: "text", nullable: false),
                    ProductName = table.Column<string>(type: "text", nullable: false),
                    Language = table.Column<string>(type: "text", nullable: false),
                    TargetAudience = table.Column<string>(type: "text", nullable: false),
                    HookAngle = table.Column<int>(type: "integer", nullable: false),
                    HookText = table.Column<string>(type: "text", nullable: false),
                    ScenesJson = table.Column<string>(type: "text", nullable: false),
                    CallToAction = table.Column<string>(type: "text", nullable: false),
                    EstimatedDurationSeconds = table.Column<int>(type: "integer", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UgcVideoScripts", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "VisitorSessions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    SessionId = table.Column<string>(type: "text", nullable: false),
                    VisitorId = table.Column<string>(type: "text", nullable: false),
                    CountryCode = table.Column<string>(type: "text", nullable: false),
                    City = table.Column<string>(type: "text", nullable: false),
                    DeviceType = table.Column<string>(type: "text", nullable: false),
                    Browser = table.Column<string>(type: "text", nullable: false),
                    InitialReferrer = table.Column<string>(type: "text", nullable: false),
                    PagesViewedCount = table.Column<int>(type: "integer", nullable: false),
                    PageViewsCount = table.Column<int>(type: "integer", nullable: false),
                    SessionDurationSeconds = table.Column<int>(type: "integer", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    StartedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    LastActiveAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VisitorSessions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "VoiceCallAmdRecords",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    CallSessionId = table.Column<Guid>(type: "uuid", nullable: true),
                    DestinationPhone = table.Column<string>(type: "text", nullable: false),
                    DetectedClassification = table.Column<string>(type: "text", nullable: false),
                    AmdConfidence = table.Column<decimal>(type: "numeric", nullable: false),
                    DetectionDurationMs = table.Column<int>(type: "integer", nullable: false),
                    VoicemailDropped = table.Column<bool>(type: "boolean", nullable: false),
                    AudioDropFileUrl = table.Column<string>(type: "text", nullable: false),
                    EvaluatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VoiceCallAmdRecords", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "VoicemailDropTemplates",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    TemplateName = table.Column<string>(type: "text", nullable: false),
                    AudioFileUrl = table.Column<string>(type: "text", nullable: false),
                    DynamicTextPrompt = table.Column<string>(type: "text", nullable: false),
                    UseTtsSynthesis = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VoicemailDropTemplates", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "WarehouseRecords",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    Code = table.Column<string>(type: "text", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    Address = table.Column<string>(type: "text", nullable: false),
                    City = table.Column<string>(type: "text", nullable: false),
                    Country = table.Column<string>(type: "text", nullable: false),
                    Latitude = table.Column<double>(type: "double precision", nullable: false),
                    Longitude = table.Column<double>(type: "double precision", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CapacityUnits = table.Column<int>(type: "integer", nullable: false),
                    RowVersion = table.Column<long>(type: "bigint", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WarehouseRecords", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "WarehouseStockRecords",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    WarehouseId = table.Column<Guid>(type: "uuid", nullable: false),
                    Sku = table.Column<string>(type: "text", nullable: false),
                    ProductName = table.Column<string>(type: "text", nullable: false),
                    QuantityAvailable = table.Column<int>(type: "integer", nullable: false),
                    QuantityReserved = table.Column<int>(type: "integer", nullable: false),
                    UnitCost = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    RowVersion = table.Column<long>(type: "bigint", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WarehouseStockRecords", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "WarmupExchangeLogs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    WarmupCadenceId = table.Column<Guid>(type: "uuid", nullable: false),
                    PeerInboxEmail = table.Column<string>(type: "text", nullable: false),
                    Direction = table.Column<string>(type: "text", nullable: false),
                    OpenedByPeer = table.Column<bool>(type: "boolean", nullable: false),
                    TimestampUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WarmupExchangeLogs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "WaterfallEnrichmentCascades",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    CascadeName = table.Column<string>(type: "text", nullable: false),
                    ProviderChainJson = table.Column<string>(type: "text", nullable: false),
                    TargetDataType = table.Column<string>(type: "text", nullable: false),
                    MinConfidenceScore = table.Column<decimal>(type: "numeric", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WaterfallEnrichmentCascades", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "WebhookDeliveryAttemptLogs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    SubscriptionId = table.Column<Guid>(type: "uuid", nullable: false),
                    EventType = table.Column<string>(type: "text", nullable: false),
                    PayloadJson = table.Column<string>(type: "text", nullable: false),
                    ResponseStatusCode = table.Column<int>(type: "integer", nullable: false),
                    IsSuccess = table.Column<bool>(type: "boolean", nullable: false),
                    ExecutionDurationMs = table.Column<long>(type: "bigint", nullable: false),
                    AttemptedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    AttemptNumber = table.Column<int>(type: "integer", nullable: false),
                    MaxRetries = table.Column<int>(type: "integer", nullable: false),
                    NextAttemptAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IsDeadLetter = table.Column<bool>(type: "boolean", nullable: false),
                    DeadLetterReason = table.Column<string>(type: "text", nullable: true),
                    SignatureHeader = table.Column<string>(type: "text", nullable: true),
                    TimestampHeader = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WebhookDeliveryAttemptLogs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "WebhookSubscriptionRecords",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    TargetUrl = table.Column<string>(type: "text", nullable: false),
                    SecretKey = table.Column<string>(type: "text", nullable: false),
                    SubscribedEventsJson = table.Column<string>(type: "text", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    FailureCount = table.Column<int>(type: "integer", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WebhookSubscriptionRecords", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "WebResearchRecords",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    Domain = table.Column<string>(type: "text", nullable: false),
                    CompanyName = table.Column<string>(type: "text", nullable: false),
                    Engine = table.Column<string>(type: "text", nullable: false),
                    CorporateSummary = table.Column<string>(type: "text", nullable: false),
                    ExecutiveBiosJson = table.Column<string>(type: "text", nullable: false),
                    TechTagsJson = table.Column<string>(type: "text", nullable: false),
                    SalesHooksJson = table.Column<string>(type: "text", nullable: false),
                    ConfidenceScore = table.Column<int>(type: "integer", nullable: false),
                    ResearchedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WebResearchRecords", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "WhatsAppInstances",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    InstanceName = table.Column<string>(type: "text", nullable: false),
                    PhoneNumber = table.Column<string>(type: "text", nullable: true),
                    Status = table.Column<string>(type: "text", nullable: false),
                    PairingMode = table.Column<string>(type: "text", nullable: false),
                    PairingCode = table.Column<string>(type: "text", nullable: true),
                    QrData = table.Column<string>(type: "text", nullable: true),
                    MemoryFootprintMb = table.Column<int>(type: "integer", nullable: false),
                    MessagesSent = table.Column<long>(type: "bigint", nullable: false),
                    MessagesReceived = table.Column<long>(type: "bigint", nullable: false),
                    ConnectedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WhatsAppInstances", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "WhatsAppInteractiveOrderConfirmations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    OrderId = table.Column<Guid>(type: "uuid", nullable: false),
                    OrderNumber = table.Column<string>(type: "text", nullable: false),
                    CustomerPhone = table.Column<string>(type: "text", nullable: false),
                    TotalAmount = table.Column<decimal>(type: "numeric", nullable: false),
                    Currency = table.Column<string>(type: "text", nullable: false),
                    MessageId = table.Column<string>(type: "text", nullable: false),
                    Status = table.Column<string>(type: "text", nullable: false),
                    SelectedButtonId = table.Column<string>(type: "text", nullable: true),
                    AddressUpdateNote = table.Column<string>(type: "text", nullable: true),
                    DispatchedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    RespondedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    FallbackToDialerTriggered = table.Column<bool>(type: "boolean", nullable: false),
                    FallbackTriggeredAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WhatsAppInteractiveOrderConfirmations", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "WhatsAppNewsletterChannels",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    ChannelJid = table.Column<string>(type: "text", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    Description = table.Column<string>(type: "text", nullable: false),
                    InviteCode = table.Column<string>(type: "text", nullable: false),
                    SubscriberCount = table.Column<int>(type: "integer", nullable: false),
                    IsMuted = table.Column<bool>(type: "boolean", nullable: false),
                    IsVerified = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WhatsAppNewsletterChannels", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "WhatsAppNewsletterPosts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    ChannelId = table.Column<Guid>(type: "uuid", nullable: false),
                    ServerId = table.Column<string>(type: "text", nullable: false),
                    Content = table.Column<string>(type: "text", nullable: false),
                    MediaUrl = table.Column<string>(type: "text", nullable: true),
                    ReactionCountsJson = table.Column<string>(type: "text", nullable: false),
                    PublishedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WhatsAppNewsletterPosts", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "WhatsAppPairingSessions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    InstanceName = table.Column<string>(type: "text", nullable: false),
                    PhoneNumber = table.Column<string>(type: "text", nullable: false),
                    PairingCode = table.Column<string>(type: "text", nullable: false),
                    Status = table.Column<string>(type: "text", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ExpiresAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ConnectedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CompanionOs = table.Column<string>(type: "text", nullable: false),
                    CompanionPlatform = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WhatsAppPairingSessions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "WilayaZones",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Code = table.Column<string>(type: "text", nullable: false),
                    NameAr = table.Column<string>(type: "text", nullable: false),
                    NameFr = table.Column<string>(type: "text", nullable: false),
                    CountryCode = table.Column<string>(type: "text", nullable: false),
                    StandardShippingFee = table.Column<decimal>(type: "numeric", nullable: false),
                    EstimatedDeliveryDays = table.Column<int>(type: "integer", nullable: false),
                    DelegationsJson = table.Column<string>(type: "text", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WilayaZones", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "WorkflowDefinitionRecords",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    Description = table.Column<string>(type: "text", nullable: false),
                    TriggerType = table.Column<string>(type: "text", nullable: false),
                    NodesJson = table.Column<string>(type: "text", nullable: false),
                    EdgesJson = table.Column<string>(type: "text", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WorkflowDefinitionRecords", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AgentMissionSteps",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    MissionId = table.Column<Guid>(type: "uuid", nullable: false),
                    StepIndex = table.Column<int>(type: "integer", nullable: false),
                    ToolName = table.Column<string>(type: "text", nullable: false),
                    InputArgumentsJson = table.Column<string>(type: "text", nullable: false),
                    OutputResultJson = table.Column<string>(type: "text", nullable: false),
                    ReasoningThought = table.Column<string>(type: "text", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    ExecutionDurationMs = table.Column<double>(type: "double precision", nullable: false),
                    TokensConsumed = table.Column<int>(type: "integer", nullable: false),
                    CostUsd = table.Column<decimal>(type: "numeric", nullable: false),
                    ApprovalRequired = table.Column<bool>(type: "boolean", nullable: false),
                    ApprovedBy = table.Column<string>(type: "text", nullable: true),
                    ApprovedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ExecutedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    AgentMissionId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AgentMissionSteps", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AgentMissionSteps_AgentMissions_AgentMissionId",
                        column: x => x.AgentMissionId,
                        principalTable: "AgentMissions",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "EnvelopeSigners",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    EnvelopeId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    Email = table.Column<string>(type: "text", nullable: false),
                    Role = table.Column<int>(type: "integer", nullable: false),
                    OrderIndex = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    SignatureData = table.Column<string>(type: "text", nullable: false),
                    IpAddress = table.Column<string>(type: "text", nullable: false),
                    VerificationToken = table.Column<string>(type: "text", nullable: false),
                    SignedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DocumentEnvelopeId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EnvelopeSigners", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EnvelopeSigners_DocumentEnvelopes_DocumentEnvelopeId",
                        column: x => x.DocumentEnvelopeId,
                        principalTable: "DocumentEnvelopes",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "InvoiceLineItems",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    InvoiceId = table.Column<Guid>(type: "uuid", nullable: false),
                    Description = table.Column<string>(type: "text", nullable: false),
                    Quantity = table.Column<int>(type: "integer", nullable: false),
                    UnitPrice = table.Column<decimal>(type: "numeric", nullable: false),
                    TotalPrice = table.Column<decimal>(type: "numeric", nullable: false),
                    InvoiceRecordId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InvoiceLineItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_InvoiceLineItems_Invoices_InvoiceRecordId",
                        column: x => x.InvoiceRecordId,
                        principalTable: "Invoices",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_VoiceCallSessions_TenantId_CallSid",
                table: "VoiceCallSessions",
                columns: new[] { "TenantId", "CallSid" });

            migrationBuilder.CreateIndex(
                name: "IX_VoiceCallSessions_TenantId_ContactId",
                table: "VoiceCallSessions",
                columns: new[] { "TenantId", "ContactId" });

            migrationBuilder.CreateIndex(
                name: "IX_TouchpointExecutions_TenantId_Status_ScheduledAtUtc",
                table: "TouchpointExecutions",
                columns: new[] { "TenantId", "Status", "ScheduledAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_CadenceTouchpoints_TenantId_CampaignId",
                table: "CadenceTouchpoints",
                columns: new[] { "TenantId", "CampaignId" });

            migrationBuilder.CreateIndex(
                name: "IX_AgentHeartbeatRecords_TenantId_ExecutedAtUtc",
                table: "AgentHeartbeatRecords",
                columns: new[] { "TenantId", "ExecutedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_AgentMissionSteps_AgentMissionId",
                table: "AgentMissionSteps",
                column: "AgentMissionId");

            migrationBuilder.CreateIndex(
                name: "IX_AgentPersonas_RoleKey",
                table: "AgentPersonas",
                column: "RoleKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AutonomousFunnelRuns_TenantId_StartedAtUtc",
                table: "AutonomousFunnelRuns",
                columns: new[] { "TenantId", "StartedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_BranchRecords_TenantId_OrganizationId",
                table: "BranchRecords",
                columns: new[] { "TenantId", "OrganizationId" });

            migrationBuilder.CreateIndex(
                name: "IX_ClientPortalFeedbacks_TenantId_ProjectId",
                table: "ClientPortalFeedbacks",
                columns: new[] { "TenantId", "ProjectId" });

            migrationBuilder.CreateIndex(
                name: "IX_ClientPortalFeedbacks_TenantId_TaskId",
                table: "ClientPortalFeedbacks",
                columns: new[] { "TenantId", "TaskId" });

            migrationBuilder.CreateIndex(
                name: "IX_ClientPortalTokens_TenantId_ProjectId",
                table: "ClientPortalTokens",
                columns: new[] { "TenantId", "ProjectId" });

            migrationBuilder.CreateIndex(
                name: "IX_ClientPortalTokens_Token",
                table: "ClientPortalTokens",
                column: "Token",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CrawlerSecretRecords_TenantId_Provider",
                table: "CrawlerSecretRecords",
                columns: new[] { "TenantId", "Provider" });

            migrationBuilder.CreateIndex(
                name: "IX_DealMarginRecords_TenantId_DealId",
                table: "DealMarginRecords",
                columns: new[] { "TenantId", "DealId" });

            migrationBuilder.CreateIndex(
                name: "IX_DealMarginRecords_TenantId_OrganizationId",
                table: "DealMarginRecords",
                columns: new[] { "TenantId", "OrganizationId" });

            migrationBuilder.CreateIndex(
                name: "IX_EnvelopeSigners_DocumentEnvelopeId",
                table: "EnvelopeSigners",
                column: "DocumentEnvelopeId");

            migrationBuilder.CreateIndex(
                name: "IX_InvoiceLineItems_InvoiceRecordId",
                table: "InvoiceLineItems",
                column: "InvoiceRecordId");

            migrationBuilder.CreateIndex(
                name: "IX_MediaVaultRecords_TenantId_StorageKey",
                table: "MediaVaultRecords",
                columns: new[] { "TenantId", "StorageKey" });

            migrationBuilder.CreateIndex(
                name: "IX_OrganizationRecords_TenantId_Code",
                table: "OrganizationRecords",
                columns: new[] { "TenantId", "Code" });

            migrationBuilder.CreateIndex(
                name: "IX_Projects_TenantId_ContractId",
                table: "Projects",
                columns: new[] { "TenantId", "ContractId" });

            migrationBuilder.CreateIndex(
                name: "IX_Projects_TenantId_DealId",
                table: "Projects",
                columns: new[] { "TenantId", "DealId" });

            migrationBuilder.CreateIndex(
                name: "IX_StockTransferManifests_TenantId_ManifestNumber",
                table: "StockTransferManifests",
                columns: new[] { "TenantId", "ManifestNumber" });

            migrationBuilder.CreateIndex(
                name: "IX_TaskComments_TenantId_TaskId",
                table: "TaskComments",
                columns: new[] { "TenantId", "TaskId" });

            migrationBuilder.CreateIndex(
                name: "IX_TaskItems_TenantId_ProjectId_Status",
                table: "TaskItems",
                columns: new[] { "TenantId", "ProjectId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_TeamMemberRecords_TenantId_TeamId",
                table: "TeamMemberRecords",
                columns: new[] { "TenantId", "TeamId" });

            migrationBuilder.CreateIndex(
                name: "IX_TeamRecords_TenantId_BranchId",
                table: "TeamRecords",
                columns: new[] { "TenantId", "BranchId" });

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
                name: "IX_WarehouseStockRecords_TenantId_WarehouseId_Sku",
                table: "WarehouseStockRecords",
                columns: new[] { "TenantId", "WarehouseId", "Sku" });

            migrationBuilder.CreateIndex(
                name: "IX_WebResearchRecords_TenantId_Domain",
                table: "WebResearchRecords",
                columns: new[] { "TenantId", "Domain" });

            migrationBuilder.CreateIndex(
                name: "IX_WhatsAppInstances_TenantId_InstanceName",
                table: "WhatsAppInstances",
                columns: new[] { "TenantId", "InstanceName" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_WhatsAppInteractiveOrderConfirmations_TenantId_OrderId",
                table: "WhatsAppInteractiveOrderConfirmations",
                columns: new[] { "TenantId", "OrderId" });

            migrationBuilder.CreateIndex(
                name: "IX_WhatsAppInteractiveOrderConfirmations_TenantId_Status",
                table: "WhatsAppInteractiveOrderConfirmations",
                columns: new[] { "TenantId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_WhatsAppPairingSessions_TenantId_InstanceName",
                table: "WhatsAppPairingSessions",
                columns: new[] { "TenantId", "InstanceName" });

            migrationBuilder.CreateIndex(
                name: "IX_WhatsAppPairingSessions_TenantId_PairingCode",
                table: "WhatsAppPairingSessions",
                columns: new[] { "TenantId", "PairingCode" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AdOptimizationRules");

            migrationBuilder.DropTable(
                name: "AdPlatformExecutionLogs");

            migrationBuilder.DropTable(
                name: "AdSetDeliveredRoasMetrics");

            migrationBuilder.DropTable(
                name: "AeoBrandMentions");

            migrationBuilder.DropTable(
                name: "AgentHeartbeatRecords");

            migrationBuilder.DropTable(
                name: "AgentMissionSteps");

            migrationBuilder.DropTable(
                name: "AgentPersonas");

            migrationBuilder.DropTable(
                name: "AiAgentBudgets");

            migrationBuilder.DropTable(
                name: "AiToolExecutionLogs");

            migrationBuilder.DropTable(
                name: "AndroidSmsDispatchRecords");

            migrationBuilder.DropTable(
                name: "AndroidSmsGatewayDevices");

            migrationBuilder.DropTable(
                name: "AutonomousFunnelRuns");

            migrationBuilder.DropTable(
                name: "B2BCreditAccounts");

            migrationBuilder.DropTable(
                name: "BillingInvoiceRecords");

            migrationBuilder.DropTable(
                name: "BookingEventTypes");

            migrationBuilder.DropTable(
                name: "BranchRecords");

            migrationBuilder.DropTable(
                name: "CalendarBookingRecords");

            migrationBuilder.DropTable(
                name: "CallCenterOrderLocks");

            migrationBuilder.DropTable(
                name: "CampaignAdSpendRecords");

            migrationBuilder.DropTable(
                name: "CartReservationLocks");

            migrationBuilder.DropTable(
                name: "ClientPortalFeedbacks");

            migrationBuilder.DropTable(
                name: "ClientPortalTokens");

            migrationBuilder.DropTable(
                name: "CodCheckoutSessions");

            migrationBuilder.DropTable(
                name: "CodNetMarginLedgerEntries");

            migrationBuilder.DropTable(
                name: "CommunityFeedbackPosts");

            migrationBuilder.DropTable(
                name: "CompetitorAdRecords");

            migrationBuilder.DropTable(
                name: "CompetitorPriceTrackers");

            migrationBuilder.DropTable(
                name: "ConversationPresenceSessions");

            migrationBuilder.DropTable(
                name: "CrawlerSecretRecords");

            migrationBuilder.DropTable(
                name: "CryptographicPdfRecords");

            migrationBuilder.DropTable(
                name: "CsatSurveyResults");

            migrationBuilder.DropTable(
                name: "CurrencyRates");

            migrationBuilder.DropTable(
                name: "CustomFieldDefinitions");

            migrationBuilder.DropTable(
                name: "CustomFieldValues");

            migrationBuilder.DropTable(
                name: "DealMarginRecords");

            migrationBuilder.DropTable(
                name: "DeliveryDrivers");

            migrationBuilder.DropTable(
                name: "DigitalAssets");

            migrationBuilder.DropTable(
                name: "DigitalDownloadTokens");

            migrationBuilder.DropTable(
                name: "DocumentAccessGates");

            migrationBuilder.DropTable(
                name: "DocumentDwellTelemetries");

            migrationBuilder.DropTable(
                name: "DomainDnsHealthRecords");

            migrationBuilder.DropTable(
                name: "DoorstepProofOfDeliveries");

            migrationBuilder.DropTable(
                name: "DriverCashReconciliations");

            migrationBuilder.DropTable(
                name: "DriverMobileSessions");

            migrationBuilder.DropTable(
                name: "DriverRunSheets");

            migrationBuilder.DropTable(
                name: "DynamicFormDefinitions");

            migrationBuilder.DropTable(
                name: "DynamicFormSubmissions");

            migrationBuilder.DropTable(
                name: "DynamicMarkdownPolicies");

            migrationBuilder.DropTable(
                name: "EmailSpamAnalysisReports");

            migrationBuilder.DropTable(
                name: "EnrichmentExecutionLogs");

            migrationBuilder.DropTable(
                name: "EnvelopeSigners");

            migrationBuilder.DropTable(
                name: "FactoringReceivableRecords");

            migrationBuilder.DropTable(
                name: "FeatureFeedbackPosts");

            migrationBuilder.DropTable(
                name: "FeatureFeedbackVotes");

            migrationBuilder.DropTable(
                name: "FeedbackPostVotes");

            migrationBuilder.DropTable(
                name: "FlowDefinitions");

            migrationBuilder.DropTable(
                name: "FlowExecutionSessions");

            migrationBuilder.DropTable(
                name: "FunnelConversionEvents");

            migrationBuilder.DropTable(
                name: "FunnelSplitTests");

            migrationBuilder.DropTable(
                name: "FunnelSteps");

            migrationBuilder.DropTable(
                name: "HostAvailabilitySchedules");

            migrationBuilder.DropTable(
                name: "InAppMicroSurveyRecords");

            migrationBuilder.DropTable(
                name: "InvoiceLineItems");

            migrationBuilder.DropTable(
                name: "JourneyDefinitions");

            migrationBuilder.DropTable(
                name: "JourneyInstances");

            migrationBuilder.DropTable(
                name: "LiquidationBundleOffers");

            migrationBuilder.DropTable(
                name: "MailboxWarmupCadences");

            migrationBuilder.DropTable(
                name: "MediaVaultRecords");

            migrationBuilder.DropTable(
                name: "MicroSurveyResponsePartials");

            migrationBuilder.DropTable(
                name: "NewsletterBroadcasts");

            migrationBuilder.DropTable(
                name: "OneClickUpsellOffers");

            migrationBuilder.DropTable(
                name: "OrderBumpOffers");

            migrationBuilder.DropTable(
                name: "OrganizationRecords");

            migrationBuilder.DropTable(
                name: "PageAuditResults");

            migrationBuilder.DropTable(
                name: "Payments");

            migrationBuilder.DropTable(
                name: "PimVariantMatrices");

            migrationBuilder.DropTable(
                name: "ProductLicenseKeys");

            migrationBuilder.DropTable(
                name: "Projects");

            migrationBuilder.DropTable(
                name: "ProofOfDeliveryRecords");

            migrationBuilder.DropTable(
                name: "PublicationArticles");

            migrationBuilder.DropTable(
                name: "RunSheetStops");

            migrationBuilder.DropTable(
                name: "SalesCommissionPlans");

            migrationBuilder.DropTable(
                name: "SalesCommissionStatements");

            migrationBuilder.DropTable(
                name: "SalesForecastPeriods");

            migrationBuilder.DropTable(
                name: "SalesForecastRepQuotas");

            migrationBuilder.DropTable(
                name: "SalesFunnels");

            migrationBuilder.DropTable(
                name: "SeoAuditReports");

            migrationBuilder.DropTable(
                name: "SeoMultipageGraphReports");

            migrationBuilder.DropTable(
                name: "SerpKeywordTrackings");

            migrationBuilder.DropTable(
                name: "SerpRankHistoryRecords");

            migrationBuilder.DropTable(
                name: "StockAgingLedgers");

            migrationBuilder.DropTable(
                name: "StockTransferManifests");

            migrationBuilder.DropTable(
                name: "TaskComments");

            migrationBuilder.DropTable(
                name: "TaskItems");

            migrationBuilder.DropTable(
                name: "TeamMemberRecords");

            migrationBuilder.DropTable(
                name: "TeamRecords");

            migrationBuilder.DropTable(
                name: "TelemetryEvents");

            migrationBuilder.DropTable(
                name: "TenantApiKeys");

            migrationBuilder.DropTable(
                name: "TenantRebillingConfigs");

            migrationBuilder.DropTable(
                name: "TenantSnapshotRecords");

            migrationBuilder.DropTable(
                name: "TimesheetEntries");

            migrationBuilder.DropTable(
                name: "UgcVideoScripts");

            migrationBuilder.DropTable(
                name: "VisitorSessions");

            migrationBuilder.DropTable(
                name: "VoiceCallAmdRecords");

            migrationBuilder.DropTable(
                name: "VoicemailDropTemplates");

            migrationBuilder.DropTable(
                name: "WarehouseRecords");

            migrationBuilder.DropTable(
                name: "WarehouseStockRecords");

            migrationBuilder.DropTable(
                name: "WarmupExchangeLogs");

            migrationBuilder.DropTable(
                name: "WaterfallEnrichmentCascades");

            migrationBuilder.DropTable(
                name: "WebhookDeliveryAttemptLogs");

            migrationBuilder.DropTable(
                name: "WebhookSubscriptionRecords");

            migrationBuilder.DropTable(
                name: "WebResearchRecords");

            migrationBuilder.DropTable(
                name: "WhatsAppInstances");

            migrationBuilder.DropTable(
                name: "WhatsAppInteractiveOrderConfirmations");

            migrationBuilder.DropTable(
                name: "WhatsAppNewsletterChannels");

            migrationBuilder.DropTable(
                name: "WhatsAppNewsletterPosts");

            migrationBuilder.DropTable(
                name: "WhatsAppPairingSessions");

            migrationBuilder.DropTable(
                name: "WilayaZones");

            migrationBuilder.DropTable(
                name: "WorkflowDefinitionRecords");

            migrationBuilder.DropTable(
                name: "AgentMissions");

            migrationBuilder.DropTable(
                name: "DocumentEnvelopes");

            migrationBuilder.DropTable(
                name: "Invoices");

            migrationBuilder.DropIndex(
                name: "IX_VoiceCallSessions_TenantId_CallSid",
                table: "VoiceCallSessions");

            migrationBuilder.DropIndex(
                name: "IX_VoiceCallSessions_TenantId_ContactId",
                table: "VoiceCallSessions");

            migrationBuilder.DropIndex(
                name: "IX_TouchpointExecutions_TenantId_Status_ScheduledAtUtc",
                table: "TouchpointExecutions");

            migrationBuilder.DropIndex(
                name: "IX_CadenceTouchpoints_TenantId_CampaignId",
                table: "CadenceTouchpoints");

            migrationBuilder.DropColumn(
                name: "RefreshToken",
                table: "SocialAccounts");

            migrationBuilder.DropColumn(
                name: "RefreshTokenRequired",
                table: "SocialAccounts");

            migrationBuilder.DropColumn(
                name: "TokenExpiresAtUtc",
                table: "SocialAccounts");

            migrationBuilder.DropColumn(
                name: "ExpectedFreightCost",
                table: "ShippingManifests");

            migrationBuilder.DropColumn(
                name: "HasDkim",
                table: "SenderAccounts");

            migrationBuilder.DropColumn(
                name: "HasDmarc",
                table: "SenderAccounts");

            migrationBuilder.DropColumn(
                name: "HasSpf",
                table: "SenderAccounts");

            migrationBuilder.DropColumn(
                name: "LastDispatchedAtUtc",
                table: "SenderAccounts");

            migrationBuilder.DropColumn(
                name: "QuarantineReason",
                table: "SenderAccounts");

            migrationBuilder.DropColumn(
                name: "QuarantinedAtUtc",
                table: "SenderAccounts");

            migrationBuilder.DropColumn(
                name: "Status",
                table: "SenderAccounts");

            migrationBuilder.DropColumn(
                name: "ThawedAtUtc",
                table: "SenderAccounts");

            migrationBuilder.DropColumn(
                name: "TotalBounced",
                table: "SenderAccounts");

            migrationBuilder.DropColumn(
                name: "TotalDelivered",
                table: "SenderAccounts");

            migrationBuilder.DropColumn(
                name: "TotalDispatched",
                table: "SenderAccounts");

            migrationBuilder.DropColumn(
                name: "TotalOpened",
                table: "SenderAccounts");

            migrationBuilder.DropColumn(
                name: "TotalReplied",
                table: "SenderAccounts");

            migrationBuilder.DropColumn(
                name: "TotalSpamComplaints",
                table: "SenderAccounts");

            migrationBuilder.DropColumn(
                name: "DisputeTicketNumber",
                table: "RemittanceItemMatches");

            migrationBuilder.DropColumn(
                name: "HoldbackAmount",
                table: "RemittanceItemMatches");

            migrationBuilder.DropColumn(
                name: "IsPrivateNote",
                table: "Messages");

            migrationBuilder.DropColumn(
                name: "MaxAiTokensPerMonth",
                table: "FeatureEntitlements");

            migrationBuilder.DropColumn(
                name: "MaxCarrierWaybills",
                table: "FeatureEntitlements");

            migrationBuilder.DropColumn(
                name: "MaxVoiceMinutesPerMonth",
                table: "FeatureEntitlements");

            migrationBuilder.DropColumn(
                name: "MaxWarmupMailboxes",
                table: "FeatureEntitlements");

            migrationBuilder.DropColumn(
                name: "MonthlyIncludedColdEmails",
                table: "FeatureEntitlements");

            migrationBuilder.DropColumn(
                name: "StorageMbLimit",
                table: "FeatureEntitlements");

            migrationBuilder.DropColumn(
                name: "ExpectedCloseDate",
                table: "Deals");

            migrationBuilder.DropColumn(
                name: "OwnerId",
                table: "Deals");

            migrationBuilder.DropColumn(
                name: "AccumulatedPausedMinutes",
                table: "Conversations");

            migrationBuilder.DropColumn(
                name: "PausedAtUtc",
                table: "Conversations");

            migrationBuilder.DropColumn(
                name: "Country",
                table: "Contacts");

            migrationBuilder.DropColumn(
                name: "ConfirmedVia",
                table: "CodOrders");

            migrationBuilder.DropColumn(
                name: "InteractiveConfirmationSentAtUtc",
                table: "CodOrders");

            migrationBuilder.DropColumn(
                name: "RequiresPhoneCall",
                table: "CodOrders");

            migrationBuilder.AlterColumn<string>(
                name: "ToNumber",
                table: "VoiceCallSessions",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(50)",
                oldMaxLength: 50);

            migrationBuilder.AlterColumn<string>(
                name: "FromNumber",
                table: "VoiceCallSessions",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(50)",
                oldMaxLength: 50);

            migrationBuilder.AlterColumn<string>(
                name: "CallSid",
                table: "VoiceCallSessions",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(100)",
                oldMaxLength: 100);

            migrationBuilder.AlterColumn<string>(
                name: "Status",
                table: "TouchpointExecutions",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(50)",
                oldMaxLength: 50);
        }
    }
}
