DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM pg_namespace WHERE nspname = 'advertising') THEN
        CREATE SCHEMA advertising;
    END IF;
END $EF$;


DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM pg_namespace WHERE nspname = 'customer_data') THEN
        CREATE SCHEMA customer_data;
    END IF;
END $EF$;


DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM pg_namespace WHERE nspname = 'automation') THEN
        CREATE SCHEMA automation;
    END IF;
END $EF$;


CREATE TABLE "AbandonedCartRecords" (
    "Id" uuid NOT NULL,
    "TenantId" uuid NOT NULL,
    "StorefrontConnectionId" uuid NOT NULL,
    "ExternalCartId" text NOT NULL,
    "CustomerEmail" text NOT NULL,
    "CustomerPhone" text,
    "CartTotal" numeric NOT NULL,
    "RecoveryUrl" text NOT NULL,
    "ItemsJson" text NOT NULL,
    "Status" integer NOT NULL,
    "RecoveryAttempts" integer NOT NULL,
    "DetectedAtUtc" timestamp with time zone NOT NULL,
    "RecoveredAtUtc" timestamp with time zone,
    CONSTRAINT "PK_AbandonedCartRecords" PRIMARY KEY ("Id")
);


CREATE TABLE "Activities" (
    "Id" uuid NOT NULL,
    "TenantId" uuid NOT NULL,
    "ContactId" uuid NOT NULL,
    "Type" integer NOT NULL,
    "PayloadJson" text NOT NULL,
    "CreatedAtUtc" timestamp with time zone NOT NULL,
    CONSTRAINT "PK_Activities" PRIMARY KEY ("Id")
);


CREATE TABLE "AdOptimizationRules" (
    "Id" uuid NOT NULL,
    "TenantId" uuid NOT NULL,
    "Platform" integer NOT NULL,
    "RuleName" text NOT NULL,
    "MinSpendThreshold" numeric NOT NULL,
    "MinDeliveryRateCutoff" numeric NOT NULL,
    "MinDeliveredRoasToScale" numeric NOT NULL,
    "DailyBudgetScaleStepPercentage" numeric NOT NULL,
    "IsAutoExecuteEnabled" boolean NOT NULL,
    "IsActive" boolean NOT NULL,
    "CreatedAtUtc" timestamp with time zone NOT NULL,
    CONSTRAINT "PK_AdOptimizationRules" PRIMARY KEY ("Id")
);


CREATE TABLE "AdPlatformExecutionLogs" (
    "Id" uuid NOT NULL,
    "TenantId" uuid NOT NULL,
    "RuleId" uuid,
    "Platform" integer NOT NULL,
    "AdSetId" text NOT NULL,
    "Action" integer NOT NULL,
    "Reason" text NOT NULL,
    "CurrentDailyBudget" numeric NOT NULL,
    "NewDailyBudget" numeric,
    "ExecutionSuccess" boolean NOT NULL,
    "ExecutedAtUtc" timestamp with time zone NOT NULL,
    CONSTRAINT "PK_AdPlatformExecutionLogs" PRIMARY KEY ("Id")
);


CREATE TABLE "AdSetDeliveredRoasMetrics" (
    "Id" uuid NOT NULL,
    "TenantId" uuid NOT NULL,
    "Platform" integer NOT NULL,
    "AdSetId" text NOT NULL,
    "AdSetName" text NOT NULL,
    "DateUtc" timestamp with time zone NOT NULL,
    "TotalAdSpend" numeric NOT NULL,
    "TotalOrdersAttributed" integer NOT NULL,
    "DeliveredOrdersCount" integer NOT NULL,
    "ReturnedOrdersCount" integer NOT NULL,
    "DeliveryRatePercentage" numeric NOT NULL,
    "GrossAttributedGmv" numeric NOT NULL,
    "DeliveredNetCashRemitted" numeric NOT NULL,
    "TotalCogs" numeric NOT NULL,
    "TotalCourierShippingFees" numeric NOT NULL,
    "NetContributionMargin" numeric NOT NULL,
    "RealDeliveredRoas" numeric NOT NULL,
    "StandardPixelRoas" numeric NOT NULL,
    "RoasLeakagePercentage" numeric NOT NULL,
    "CalculatedAtUtc" timestamp with time zone NOT NULL,
    CONSTRAINT "PK_AdSetDeliveredRoasMetrics" PRIMARY KEY ("Id")
);


CREATE TABLE "AeoBrandMentions" (
    "Id" uuid NOT NULL,
    "TenantId" uuid NOT NULL,
    "BrandName" text NOT NULL,
    "AiEngine" text NOT NULL,
    "PromptQuery" text NOT NULL,
    "IsBrandMentioned" boolean NOT NULL,
    "MentionSentiment" text NOT NULL,
    "SnippetText" text NOT NULL,
    "CitationRank" integer,
    "CapturedAtUtc" timestamp with time zone NOT NULL,
    CONSTRAINT "PK_AeoBrandMentions" PRIMARY KEY ("Id")
);


CREATE TABLE "AffiliateReferralCodes" (
    "Id" uuid NOT NULL,
    "TenantId" uuid NOT NULL,
    "CreatorId" uuid NOT NULL,
    "Code" text NOT NULL,
    "DiscountType" text NOT NULL,
    "DiscountValue" numeric NOT NULL,
    "UsageCount" integer NOT NULL,
    "TotalReferredGmv" numeric NOT NULL,
    "IsActive" boolean NOT NULL,
    "CreatedAtUtc" timestamp with time zone NOT NULL,
    CONSTRAINT "PK_AffiliateReferralCodes" PRIMARY KEY ("Id")
);


CREATE TABLE "AgentHeartbeatRecords" (
    "Id" uuid NOT NULL,
    "TenantId" uuid NOT NULL,
    "RoleKey" text NOT NULL,
    "AgentName" text NOT NULL,
    "Status" text NOT NULL,
    "FindingsSummary" text NOT NULL,
    "RecommendedAction" text NOT NULL,
    "ActionsExecutedCount" integer NOT NULL,
    "ExecutedAtUtc" timestamp with time zone NOT NULL,
    CONSTRAINT "PK_AgentHeartbeatRecords" PRIMARY KEY ("Id")
);


CREATE TABLE "AgentMissions" (
    "Id" uuid NOT NULL,
    "TenantId" uuid NOT NULL,
    "Title" text NOT NULL,
    "TargetGoal" text NOT NULL,
    "Status" integer NOT NULL,
    "AutonomyMode" text NOT NULL,
    "TotalBudgetUsd" numeric NOT NULL,
    "SpentBudgetUsd" numeric NOT NULL,
    "TokensConsumed" bigint NOT NULL,
    "CurrentStepIndex" integer NOT NULL,
    "ReasoningTrace" text NOT NULL,
    "CreatedAtUtc" timestamp with time zone NOT NULL,
    "CompletedAtUtc" timestamp with time zone,
    CONSTRAINT "PK_AgentMissions" PRIMARY KEY ("Id")
);


CREATE TABLE "AgentPersonas" (
    "Id" uuid NOT NULL,
    "RoleKey" text NOT NULL,
    "Name" text NOT NULL,
    "Layer" text NOT NULL,
    "PrimaryObjective" text NOT NULL,
    "AutonomousTrigger" text NOT NULL,
    "ModelPreference" text NOT NULL,
    "Status" text NOT NULL,
    "LastHeartbeatUtc" timestamp with time zone,
    "CreatedAtUtc" timestamp with time zone NOT NULL,
    CONSTRAINT "PK_AgentPersonas" PRIMARY KEY ("Id")
);


CREATE TABLE "AiAgentBudgets" (
    "Id" uuid NOT NULL,
    "TenantId" uuid NOT NULL,
    "MonthlyTokenLimit" bigint NOT NULL,
    "TokensConsumed" bigint NOT NULL,
    "MaxMonthlyCostUsd" numeric NOT NULL,
    "CostSpentUsd" numeric NOT NULL,
    "IsHardCapEnforced" boolean NOT NULL,
    "CurrentMonthStartUtc" timestamp with time zone NOT NULL,
    "UpdatedAtUtc" timestamp with time zone NOT NULL,
    CONSTRAINT "PK_AiAgentBudgets" PRIMARY KEY ("Id")
);


CREATE TABLE "AiDrafts" (
    "Id" uuid NOT NULL,
    "TenantId" uuid NOT NULL,
    "ConversationId" uuid NOT NULL,
    "SuggestedReply" text NOT NULL,
    "ConfidenceScore" real NOT NULL,
    "SourcesJson" text NOT NULL,
    "Status" integer NOT NULL,
    "CreatedAtUtc" timestamp with time zone NOT NULL,
    CONSTRAINT "PK_AiDrafts" PRIMARY KEY ("Id")
);


CREATE TABLE "AiToolExecutionLogs" (
    "Id" uuid NOT NULL,
    "TenantId" uuid NOT NULL,
    "AgentName" text NOT NULL,
    "ToolName" text NOT NULL,
    "ArgumentsJson" text NOT NULL,
    "ResultJson" text NOT NULL,
    "ExecutionDurationMs" integer NOT NULL,
    "TokensConsumed" integer NOT NULL,
    "CostUsd" numeric NOT NULL,
    "ApprovalStatus" integer NOT NULL,
    "ApprovedByUserId" text,
    "ExecutedAtUtc" timestamp with time zone NOT NULL,
    CONSTRAINT "PK_AiToolExecutionLogs" PRIMARY KEY ("Id")
);


CREATE TABLE "AndroidSmsDispatchRecords" (
    "Id" uuid NOT NULL,
    "TenantId" uuid NOT NULL,
    "DeviceId" uuid NOT NULL,
    "SimSlot" integer NOT NULL,
    "RecipientPhone" text NOT NULL,
    "MessageBody" text NOT NULL,
    "DispatchStatus" text NOT NULL,
    "ErrorMessage" text,
    "SentAtUtc" timestamp with time zone NOT NULL,
    "DeliveredAtUtc" timestamp with time zone,
    CONSTRAINT "PK_AndroidSmsDispatchRecords" PRIMARY KEY ("Id")
);


CREATE TABLE "AndroidSmsGatewayDevices" (
    "Id" uuid NOT NULL,
    "TenantId" uuid NOT NULL,
    "DeviceName" text NOT NULL,
    "DeviceIdentifier" text NOT NULL,
    "FcmToken" text NOT NULL,
    "Sim1Operator" text NOT NULL,
    "Sim2Operator" text NOT NULL,
    "BatteryPercent" integer NOT NULL,
    "DailySentCount" integer NOT NULL,
    "DailyQuotaLimit" integer NOT NULL,
    "IsOnline" boolean NOT NULL,
    "LastHeartbeatUtc" timestamp with time zone NOT NULL,
    CONSTRAINT "PK_AndroidSmsGatewayDevices" PRIMARY KEY ("Id")
);


CREATE TABLE "AppointmentBookings" (
    "Id" uuid NOT NULL,
    "TenantId" uuid NOT NULL,
    "SlotId" uuid NOT NULL,
    "ContactId" uuid NOT NULL,
    "ScheduledTimeUtc" timestamp with time zone NOT NULL,
    "Status" integer NOT NULL,
    "MeetingUrl" text NOT NULL,
    "CreatedAtUtc" timestamp with time zone NOT NULL,
    CONSTRAINT "PK_AppointmentBookings" PRIMARY KEY ("Id")
);


CREATE TABLE "AppointmentSlots" (
    "Id" uuid NOT NULL,
    "TenantId" uuid NOT NULL,
    "Title" text NOT NULL,
    "DurationMinutes" integer NOT NULL,
    "HostAgentName" text NOT NULL,
    "IsActive" boolean NOT NULL,
    "CreatedAtUtc" timestamp with time zone NOT NULL,
    CONSTRAINT "PK_AppointmentSlots" PRIMARY KEY ("Id")
);


CREATE TABLE "AttributionTouchpoints" (
    "Id" uuid NOT NULL,
    "TenantId" uuid NOT NULL,
    "ContactId" uuid NOT NULL,
    "DealId" uuid,
    "Channel" text NOT NULL,
    "CampaignName" text NOT NULL,
    "UtmSource" text,
    "UtmMedium" text,
    "UtmCampaign" text,
    "Stage" integer NOT NULL,
    "Cost" numeric NOT NULL,
    "OccurredAtUtc" timestamp with time zone NOT NULL,
    CONSTRAINT "PK_AttributionTouchpoints" PRIMARY KEY ("Id")
);


CREATE TABLE advertising.audience_syncs (
    "Id" uuid NOT NULL,
    "AudienceId" character varying(100) NOT NULL,
    "TenantId" character varying(100) NOT NULL,
    "Platform" character varying(50) NOT NULL,
    "ValidHashesCount" integer NOT NULL,
    "SyncedAtUtc" timestamp with time zone NOT NULL,
    CONSTRAINT "PK_audience_syncs" PRIMARY KEY ("Id")
);


CREATE TABLE "AutomatedRestockOrders" (
    "Id" uuid NOT NULL,
    "TenantId" uuid NOT NULL,
    "SupplierId" uuid NOT NULL,
    "SupplierName" text NOT NULL,
    "ProductId" uuid NOT NULL,
    "Sku" text NOT NULL,
    "ProductName" text NOT NULL,
    "QuantityToOrder" integer NOT NULL,
    "UnitPurchaseCost" numeric NOT NULL,
    "Status" integer NOT NULL,
    "PlacedAtUtc" timestamp with time zone,
    "ExpectedDeliveryDateUtc" timestamp with time zone,
    "CreatedAtUtc" timestamp with time zone NOT NULL,
    CONSTRAINT "PK_AutomatedRestockOrders" PRIMARY KEY ("Id")
);


CREATE TABLE "AutonomousFunnelRuns" (
    "Id" uuid NOT NULL,
    "TenantId" uuid NOT NULL,
    "ConceptPrompt" text NOT NULL,
    "TargetSku" text,
    "Status" text NOT NULL,
    "CurrentStepNumber" integer NOT NULL,
    "CurrentStepName" text NOT NULL,
    "ProgressPercentage" integer NOT NULL,
    "GeneratedTitle" text NOT NULL,
    "GeneratedHeadline" text NOT NULL,
    "ValueProposition" text NOT NULL,
    "GeneratedHtmlPreview" text NOT NULL,
    "PerformanceScore" integer NOT NULL,
    "SeoScore" integer NOT NULL,
    "AccessibilityScore" integer NOT NULL,
    "DeploymentUrl" text NOT NULL,
    "GitHubPrUrl" text,
    "GitHubBranchName" text,
    "StartedAtUtc" timestamp with time zone NOT NULL,
    "CompletedAtUtc" timestamp with time zone,
    CONSTRAINT "PK_AutonomousFunnelRuns" PRIMARY KEY ("Id")
);


CREATE TABLE "B2BCreditAccounts" (
    "Id" uuid NOT NULL,
    "TenantId" uuid NOT NULL,
    "CompanyId" uuid NOT NULL,
    "CompanyName" text NOT NULL,
    "CreditLimit" numeric NOT NULL,
    "OutstandingBalance" numeric NOT NULL,
    "PaymentTermsDays" integer NOT NULL,
    "CreditScoreRating" text NOT NULL,
    "IsFrozen" boolean NOT NULL,
    "CreatedAtUtc" timestamp with time zone NOT NULL,
    CONSTRAINT "PK_B2BCreditAccounts" PRIMARY KEY ("Id")
);


CREATE TABLE "BillingInvoiceRecords" (
    "Id" uuid NOT NULL,
    "TenantId" uuid NOT NULL,
    "InvoiceNumber" text NOT NULL,
    "AmountDue" numeric NOT NULL,
    "Amount" numeric NOT NULL,
    "Currency" text NOT NULL,
    "Status" text NOT NULL,
    "StripeInvoiceId" text NOT NULL,
    "HostedInvoiceUrl" text NOT NULL,
    "CreatedAtUtc" timestamp with time zone NOT NULL,
    CONSTRAINT "PK_BillingInvoiceRecords" PRIMARY KEY ("Id")
);


CREATE TABLE "BlacklistEntries" (
    "Id" uuid NOT NULL,
    "TenantId" uuid NOT NULL,
    "Type" text NOT NULL,
    "Value" text NOT NULL,
    "Reason" text NOT NULL,
    "AddedAtUtc" timestamp with time zone NOT NULL,
    CONSTRAINT "PK_BlacklistEntries" PRIMARY KEY ("Id")
);


CREATE TABLE "BookingEventTypes" (
    "Id" uuid NOT NULL,
    "TenantId" uuid NOT NULL,
    "Title" text NOT NULL,
    "Slug" text NOT NULL,
    "Description" text NOT NULL,
    "DurationMinutes" integer NOT NULL,
    "BufferBeforeMinutes" integer NOT NULL,
    "BufferAfterMinutes" integer NOT NULL,
    "MinimumNoticeHours" integer NOT NULL,
    "IsRoundRobin" boolean NOT NULL,
    "AssignedHostIdsJson" text NOT NULL,
    "HostWeightsJson" text NOT NULL,
    "ColorHex" text NOT NULL,
    "IsActive" boolean NOT NULL,
    "CreatedAtUtc" timestamp with time zone NOT NULL,
    CONSTRAINT "PK_BookingEventTypes" PRIMARY KEY ("Id")
);


CREATE TABLE "BranchRecords" (
    "Id" uuid NOT NULL,
    "OrganizationId" uuid NOT NULL,
    "TenantId" uuid NOT NULL,
    "Name" text NOT NULL,
    "Region" text NOT NULL,
    "City" text NOT NULL,
    "CreatedAtUtc" timestamp with time zone NOT NULL,
    CONSTRAINT "PK_BranchRecords" PRIMARY KEY ("Id")
);


CREATE TABLE "BrandThemeTokens" (
    "Id" uuid NOT NULL,
    "TenantId" uuid NOT NULL,
    "StorefrontSlug" text NOT NULL,
    "BrandName" text NOT NULL,
    "PrimaryColorHex" text NOT NULL,
    "SecondaryColorHex" text NOT NULL,
    "FontFamily" text NOT NULL,
    "LogoUrl" text NOT NULL,
    "FaviconUrl" text NOT NULL,
    "SupportEmail" text NOT NULL,
    "SupportPhone" text NOT NULL,
    "SmsSenderId" text NOT NULL,
    "WhatsAppSenderPhone" text NOT NULL,
    "UpdatedAtUtc" timestamp with time zone NOT NULL,
    CONSTRAINT "PK_BrandThemeTokens" PRIMARY KEY ("Id")
);


CREATE TABLE "BroadcastCampaigns" (
    "Id" uuid NOT NULL,
    "TenantId" uuid NOT NULL,
    "Name" text NOT NULL,
    "TemplateId" uuid NOT NULL,
    "TargetTag" text NOT NULL,
    "Status" integer NOT NULL,
    "TotalRecipients" integer NOT NULL,
    "SentCount" integer NOT NULL,
    "CreatedAtUtc" timestamp with time zone NOT NULL,
    CONSTRAINT "PK_BroadcastCampaigns" PRIMARY KEY ("Id")
);


CREATE TABLE "CadenceSteps" (
    "Id" uuid NOT NULL,
    "TenantId" uuid NOT NULL,
    "CampaignId" uuid NOT NULL,
    "StepNumber" integer NOT NULL,
    "DelayDays" integer NOT NULL,
    "Channel" integer NOT NULL,
    "SubjectTemplate" text NOT NULL,
    "BodyTemplate" text NOT NULL,
    CONSTRAINT "PK_CadenceSteps" PRIMARY KEY ("Id")
);


CREATE TABLE "CadenceTouchpoints" (
    "Id" uuid NOT NULL,
    "TenantId" uuid NOT NULL,
    "CampaignId" uuid NOT NULL,
    "StepNumber" integer NOT NULL,
    "Channel" integer NOT NULL,
    "DelayDays" integer NOT NULL,
    "SubjectTemplate" text,
    "BodyTemplate" text NOT NULL,
    "CreatedAtUtc" timestamp with time zone NOT NULL,
    CONSTRAINT "PK_CadenceTouchpoints" PRIMARY KEY ("Id")
);


CREATE TABLE "CalendarBookingRecords" (
    "Id" uuid NOT NULL,
    "TenantId" uuid NOT NULL,
    "EventTypeId" uuid NOT NULL,
    "HostUserId" uuid NOT NULL,
    "HostName" text NOT NULL,
    "AttendeeName" text NOT NULL,
    "AttendeeEmail" text NOT NULL,
    "AttendeePhone" text NOT NULL,
    "StartTimeUtc" timestamp with time zone NOT NULL,
    "EndTimeUtc" timestamp with time zone NOT NULL,
    "TimeZone" text NOT NULL,
    "Status" integer NOT NULL,
    "MeetingUrl" text NOT NULL,
    "IcsUid" text NOT NULL,
    "Notes" text NOT NULL,
    "CreatedAtUtc" timestamp with time zone NOT NULL,
    CONSTRAINT "PK_CalendarBookingRecords" PRIMARY KEY ("Id")
);


CREATE TABLE "CallCenterOrderLocks" (
    "Id" uuid NOT NULL,
    "TenantId" uuid NOT NULL,
    "OrderId" uuid NOT NULL,
    "OrderNumber" text NOT NULL,
    "LockedByUserId" uuid NOT NULL,
    "LockedByUserName" text NOT NULL,
    "LockedAtUtc" timestamp with time zone NOT NULL,
    "ExpiresAtUtc" timestamp with time zone NOT NULL,
    "IsActive" boolean NOT NULL,
    CONSTRAINT "PK_CallCenterOrderLocks" PRIMARY KEY ("Id")
);


CREATE TABLE "CallOutcomeRecords" (
    "Id" uuid NOT NULL,
    "TenantId" uuid NOT NULL,
    "OrderId" uuid NOT NULL,
    "AgentId" text NOT NULL,
    "AgentName" text NOT NULL,
    "Outcome" integer NOT NULL,
    "Notes" text,
    "ScheduledCallbackUtc" timestamp with time zone,
    "RecordedAtUtc" timestamp with time zone NOT NULL,
    CONSTRAINT "PK_CallOutcomeRecords" PRIMARY KEY ("Id")
);


CREATE TABLE advertising.campaign_roas_snapshots (
    "Id" uuid NOT NULL,
    "CampaignId" character varying(100) NOT NULL,
    "TenantId" character varying(100) NOT NULL,
    "Platform" character varying(50) NOT NULL,
    "Spend" numeric(18,4) NOT NULL,
    "Revenue" numeric(18,4) NOT NULL,
    "CurrentRoas" numeric(18,4) NOT NULL,
    "RecommendedSpend" numeric(18,4) NOT NULL,
    "Action" text NOT NULL,
    "RecordedAtUtc" timestamp with time zone NOT NULL,
    CONSTRAINT "PK_campaign_roas_snapshots" PRIMARY KEY ("Id")
);


CREATE TABLE "CampaignAdSpendRecords" (
    "Id" uuid NOT NULL,
    "TenantId" uuid NOT NULL,
    "Platform" integer NOT NULL,
    "CampaignId" text NOT NULL,
    "CampaignName" text NOT NULL,
    "AdSetId" text NOT NULL,
    "AdSetName" text NOT NULL,
    "SpendAmount" numeric NOT NULL,
    "Currency" text NOT NULL,
    "DateUtc" timestamp with time zone NOT NULL,
    "Impressions" integer NOT NULL,
    "Clicks" integer NOT NULL,
    "ReportedPixelPurchases" integer NOT NULL,
    "ReportedPixelRevenue" numeric NOT NULL,
    "IngestedAtUtc" timestamp with time zone NOT NULL,
    CONSTRAINT "PK_CampaignAdSpendRecords" PRIMARY KEY ("Id")
);


CREATE TABLE "CarrierRemittanceScheduleConfigs" (
    "Id" uuid NOT NULL,
    "TenantId" uuid NOT NULL,
    "CourierProviderId" uuid NOT NULL,
    "CourierName" text NOT NULL,
    "Frequency" integer NOT NULL,
    "RemittanceDayOfWeek" integer NOT NULL,
    "AverageLagDays" integer NOT NULL,
    "ContractFeePerParcel" numeric NOT NULL,
    "IsActive" boolean NOT NULL,
    CONSTRAINT "PK_CarrierRemittanceScheduleConfigs" PRIMARY KEY ("Id")
);


CREATE TABLE "CartReservationLocks" (
    "Id" uuid NOT NULL,
    "TenantId" uuid NOT NULL,
    "WarehouseId" uuid NOT NULL,
    "Sku" text NOT NULL,
    "Quantity" integer NOT NULL,
    "CartId" uuid NOT NULL,
    "OrderId" uuid,
    "Status" text NOT NULL,
    "ExpiresAtUtc" timestamp with time zone NOT NULL,
    "CreatedAtUtc" timestamp with time zone NOT NULL,
    "ReleasedAtUtc" timestamp with time zone,
    CONSTRAINT "PK_CartReservationLocks" PRIMARY KEY ("Id")
);


CREATE TABLE "ChannelCredentials" (
    "Id" uuid NOT NULL,
    "TenantId" uuid NOT NULL,
    "ChannelType" text NOT NULL,
    "EncryptedAccessToken" text NOT NULL,
    "EncryptedAppSecret" text NOT NULL,
    "EncryptedRefreshToken" text,
    "EncryptedSigningKey" text,
    "NonceBase64" text NOT NULL,
    "TagBase64" text NOT NULL,
    "ExpiresAtUtc" timestamp with time zone,
    "IsActive" boolean NOT NULL,
    "CreatedAtUtc" timestamp with time zone NOT NULL,
    "UpdatedAtUtc" timestamp with time zone NOT NULL,
    CONSTRAINT "PK_ChannelCredentials" PRIMARY KEY ("Id")
);


CREATE TABLE "CheckoutLinks" (
    "Id" uuid NOT NULL,
    "TenantId" uuid NOT NULL,
    "ContactId" uuid NOT NULL,
    "ConversationId" uuid NOT NULL,
    "Title" text NOT NULL,
    "Amount" numeric NOT NULL,
    "Currency" text NOT NULL,
    "Status" integer NOT NULL,
    "PaymentUrl" text NOT NULL,
    "CreatedAtUtc" timestamp with time zone NOT NULL,
    CONSTRAINT "PK_CheckoutLinks" PRIMARY KEY ("Id")
);


CREATE TABLE "ClientPortalFeedbacks" (
    "Id" uuid NOT NULL,
    "TenantId" uuid NOT NULL,
    "ProjectId" uuid NOT NULL,
    "TaskId" uuid NOT NULL,
    "ClientName" text NOT NULL,
    "ClientEmail" text NOT NULL,
    "StatusVote" text NOT NULL,
    "FeedbackNotes" text NOT NULL,
    "SubmittedAtUtc" timestamp with time zone NOT NULL,
    CONSTRAINT "PK_ClientPortalFeedbacks" PRIMARY KEY ("Id")
);


CREATE TABLE "ClientPortalTokens" (
    "Id" uuid NOT NULL,
    "TenantId" uuid NOT NULL,
    "ProjectId" uuid NOT NULL,
    "Token" text NOT NULL,
    "ClientContactEmail" text NOT NULL,
    "ClientOrganizationName" text NOT NULL,
    "ExpiresAtUtc" timestamp with time zone NOT NULL,
    "CreatedAtUtc" timestamp with time zone NOT NULL,
    "IsRevoked" boolean NOT NULL,
    CONSTRAINT "PK_ClientPortalTokens" PRIMARY KEY ("Id")
);


CREATE TABLE "CodCheckoutSessions" (
    "Id" uuid NOT NULL,
    "TenantId" uuid NOT NULL,
    "SessionKey" text NOT NULL,
    "CartItemsJson" text NOT NULL,
    "Subtotal" numeric NOT NULL,
    "ShippingFee" numeric NOT NULL,
    "DiscountAmount" numeric NOT NULL,
    "CustomerName" text NOT NULL,
    "CustomerPhone" text NOT NULL,
    "WilayaCode" text NOT NULL,
    "WilayaName" text NOT NULL,
    "DelegationName" text NOT NULL,
    "StreetAddress" text NOT NULL,
    "OtpCode" text NOT NULL,
    "IsOtpVerified" boolean NOT NULL,
    "Status" integer NOT NULL,
    "PlacedOrderId" text,
    "UpsellOfferAcceptedSku" text,
    "UpsellAmount" numeric NOT NULL,
    "CreatedAtUtc" timestamp with time zone NOT NULL,
    "CompletedAtUtc" timestamp with time zone,
    CONSTRAINT "PK_CodCheckoutSessions" PRIMARY KEY ("Id")
);


CREATE TABLE "CodNetMarginLedgerEntries" (
    "Id" uuid NOT NULL,
    "TenantId" uuid NOT NULL,
    "OrderId" uuid NOT NULL,
    "OrderNumber" text NOT NULL,
    "GrossRevenue" numeric NOT NULL,
    "Cogs" numeric NOT NULL,
    "PackagingCost" numeric NOT NULL,
    "AdSpendCost" numeric NOT NULL,
    "CourierShippingFee" numeric NOT NULL,
    "ReturnRatePercent" numeric NOT NULL,
    "ReturnPenaltyFee" numeric NOT NULL,
    "ReturnRiskCost" numeric NOT NULL,
    "NetProfit" numeric NOT NULL,
    "NetMarginPercent" numeric NOT NULL,
    "CalculatedAtUtc" timestamp with time zone NOT NULL,
    CONSTRAINT "PK_CodNetMarginLedgerEntries" PRIMARY KEY ("Id")
);


CREATE TABLE "CodOrders" (
    "Id" uuid NOT NULL,
    "TenantId" uuid NOT NULL,
    "OrderNumber" text NOT NULL,
    "CustomerName" text NOT NULL,
    "PhoneNumber" text NOT NULL,
    "AlternatePhone" text,
    "ShippingAddress" text NOT NULL,
    "GovernorateOrWilaya" text NOT NULL,
    "DelegationOrCommune" text NOT NULL,
    "CountryCode" text NOT NULL,
    "TotalAmount" numeric NOT NULL,
    "ShippingFee" numeric NOT NULL,
    "CodCurrency" text NOT NULL,
    "Status" integer NOT NULL,
    "RiskLevel" integer NOT NULL,
    "RiskScore" integer NOT NULL,
    "FraudFlagsJson" text NOT NULL,
    "ConfirmationChannel" integer,
    "ConfirmedAtUtc" timestamp with time zone,
    "ItemsJson" text NOT NULL,
    "Notes" text,
    "RequiresPhoneCall" boolean NOT NULL,
    "ConfirmedVia" text,
    "InteractiveConfirmationSentAtUtc" timestamp with time zone,
    "CreatedAtUtc" timestamp with time zone NOT NULL,
    CONSTRAINT "PK_CodOrders" PRIMARY KEY ("Id")
);


CREATE TABLE "CommissionEscrowLedgers" (
    "Id" uuid NOT NULL,
    "TenantId" uuid NOT NULL,
    "CreatorId" uuid NOT NULL,
    "OrderId" uuid NOT NULL,
    "ReferralCode" text NOT NULL,
    "OrderTrackingNumber" text NOT NULL,
    "OrderAmount" numeric NOT NULL,
    "CommissionAmount" numeric NOT NULL,
    "Status" integer NOT NULL,
    "HoldReason" text NOT NULL,
    "VoidReason" text,
    "CreatedAtUtc" timestamp with time zone NOT NULL,
    "ClearedAtUtc" timestamp with time zone,
    "VoidedAtUtc" timestamp with time zone,
    "PaidOutAtUtc" timestamp with time zone,
    CONSTRAINT "PK_CommissionEscrowLedgers" PRIMARY KEY ("Id")
);


CREATE TABLE "CommunityFeedbackPosts" (
    "Id" uuid NOT NULL,
    "TenantId" uuid NOT NULL,
    "Title" text NOT NULL,
    "Slug" text NOT NULL,
    "DescriptionMarkdown" text NOT NULL,
    "UpvoteCount" integer NOT NULL,
    "RoadmapStatus" text NOT NULL,
    "CanonicalPostId" uuid,
    "AuthorEmail" text NOT NULL,
    "OfficialResponse" text NOT NULL,
    "CreatedAtUtc" timestamp with time zone NOT NULL,
    CONSTRAINT "PK_CommunityFeedbackPosts" PRIMARY KEY ("Id")
);


CREATE TABLE "Companies" (
    "Id" uuid NOT NULL,
    "TenantId" uuid NOT NULL,
    "WorkspaceId" uuid,
    "Name" character varying(200) NOT NULL,
    "Domain" text NOT NULL,
    "Industry" text,
    "EmployeeCount" integer NOT NULL,
    "AnnualRevenue" numeric NOT NULL,
    "ParentCompanyId" uuid,
    "CustomFieldsJson" text NOT NULL,
    "CreatedAtUtc" timestamp with time zone NOT NULL,
    CONSTRAINT "PK_Companies" PRIMARY KEY ("Id")
);


CREATE TABLE "CompetitorAdRecords" (
    "Id" uuid NOT NULL,
    "TenantId" uuid NOT NULL,
    "CompetitorName" text NOT NULL,
    "FanPageId" text NOT NULL,
    "Platform" text NOT NULL,
    "AdCreativeId" text NOT NULL,
    "HookSnippet" text NOT NULL,
    "OfferDetails" text NOT NULL,
    "EstimatedDailySpend" numeric NOT NULL,
    "ThreatLevel" text NOT NULL,
    "IsActive" boolean NOT NULL,
    "FirstSeenAtUtc" timestamp with time zone NOT NULL,
    "LastSeenAtUtc" timestamp with time zone NOT NULL,
    CONSTRAINT "PK_CompetitorAdRecords" PRIMARY KEY ("Id")
);


CREATE TABLE "CompetitorPriceTrackers" (
    "Id" uuid NOT NULL,
    "TenantId" uuid NOT NULL,
    "TargetSku" text NOT NULL,
    "CompetitorName" text NOT NULL,
    "CompetitorUrl" text NOT NULL,
    "CompetitorPrice" numeric NOT NULL,
    "OurPrice" numeric NOT NULL,
    "PriceDifferencePercentage" numeric NOT NULL,
    "RecommendedCounterAction" text NOT NULL,
    "LastCheckedAtUtc" timestamp with time zone NOT NULL,
    CONSTRAINT "PK_CompetitorPriceTrackers" PRIMARY KEY ("Id")
);


CREATE TABLE "Contacts" (
    "Id" uuid NOT NULL,
    "TenantId" uuid NOT NULL,
    "WorkspaceId" uuid,
    "FirstName" character varying(100) NOT NULL,
    "LastName" character varying(100) NOT NULL,
    "Email" character varying(256),
    "Phone" character varying(50),
    "Country" text,
    "LifecycleStage" integer NOT NULL,
    "CompanyId" uuid,
    "CustomFieldsJson" text NOT NULL,
    "Tags" text[] NOT NULL,
    "CreatedAtUtc" timestamp with time zone NOT NULL,
    "UpdatedAtUtc" timestamp with time zone NOT NULL,
    CONSTRAINT "PK_Contacts" PRIMARY KEY ("Id")
);


CREATE TABLE "ConversationPresenceSessions" (
    "Id" uuid NOT NULL,
    "TenantId" uuid NOT NULL,
    "ConversationId" uuid NOT NULL,
    "AgentUserId" uuid NOT NULL,
    "AgentName" text NOT NULL,
    "IsTyping" boolean NOT NULL,
    "LastHeartbeatUtc" timestamp with time zone NOT NULL,
    "ExpiresAtUtc" timestamp with time zone NOT NULL,
    "IsActive" boolean NOT NULL,
    CONSTRAINT "PK_ConversationPresenceSessions" PRIMARY KEY ("Id")
);


CREATE TABLE "Conversations" (
    "Id" uuid NOT NULL,
    "TenantId" uuid NOT NULL,
    "InboxId" uuid NOT NULL,
    "ContactId" uuid NOT NULL,
    "AssigneeId" uuid,
    "Status" integer NOT NULL,
    "Priority" integer NOT NULL,
    "AiMode" integer NOT NULL,
    "IsSlaBreached" boolean NOT NULL,
    "FirstResponseAtUtc" timestamp with time zone,
    "LastMessageAtUtc" timestamp with time zone,
    "PausedAtUtc" timestamp with time zone,
    "AccumulatedPausedMinutes" integer NOT NULL,
    "CreatedAtUtc" timestamp with time zone NOT NULL,
    "UpdatedAtUtc" timestamp with time zone NOT NULL,
    CONSTRAINT "PK_Conversations" PRIMARY KEY ("Id")
);


CREATE TABLE advertising.conversions (
    "EventId" text NOT NULL,
    "TenantId" character varying(100) NOT NULL,
    "Platform" character varying(50) NOT NULL,
    "EventName" character varying(100) NOT NULL,
    "ConversionValue" numeric(18,4) NOT NULL,
    "Currency" character varying(10) NOT NULL,
    "PayloadHash" text NOT NULL,
    "DispatchedAtUtc" timestamp with time zone NOT NULL,
    "IsDeduplicated" boolean NOT NULL,
    CONSTRAINT "PK_conversions" PRIMARY KEY ("EventId")
);


CREATE TABLE "ConversionTrackingConfigs" (
    "Id" uuid NOT NULL,
    "TenantId" uuid NOT NULL,
    "Platform" integer NOT NULL,
    "PixelOrDatasetId" text NOT NULL,
    "ApiAccessToken" text NOT NULL,
    "TestEventCode" text,
    "IsActive" boolean NOT NULL,
    "CreatedAtUtc" timestamp with time zone NOT NULL,
    CONSTRAINT "PK_ConversionTrackingConfigs" PRIMARY KEY ("Id")
);


CREATE TABLE "CourierFloatValuations" (
    "Id" uuid NOT NULL,
    "TenantId" uuid NOT NULL,
    "InTransitParcelCount" integer NOT NULL,
    "InTransitCashValue" numeric NOT NULL,
    "DeliveredUnremittedParcelCount" integer NOT NULL,
    "DeliveredUnremittedCashValue" numeric NOT NULL,
    "EstimatedCourierFees" numeric NOT NULL,
    "AverageDaysSalesOutstanding" numeric NOT NULL,
    "CalculatedAtUtc" timestamp with time zone NOT NULL,
    CONSTRAINT "PK_CourierFloatValuations" PRIMARY KEY ("Id")
);


CREATE TABLE "CrawlerSecretRecords" (
    "Id" uuid NOT NULL,
    "TenantId" uuid NOT NULL,
    "Provider" text NOT NULL,
    "MaskedApiKey" text NOT NULL,
    "IsActive" boolean NOT NULL,
    "HealthStatus" text NOT NULL,
    "LastLatencyMs" integer NOT NULL,
    "UpdatedAtUtc" timestamp with time zone NOT NULL,
    CONSTRAINT "PK_CrawlerSecretRecords" PRIMARY KEY ("Id")
);


CREATE TABLE "CreatorPayoutRecords" (
    "Id" uuid NOT NULL,
    "TenantId" uuid NOT NULL,
    "CreatorId" uuid NOT NULL,
    "PayoutBatchId" uuid NOT NULL,
    "Amount" numeric NOT NULL,
    "Currency" text NOT NULL,
    "PaymentMethod" text NOT NULL,
    "DestinationAccount" text NOT NULL,
    "TransactionReference" text,
    "Status" integer NOT NULL,
    "IncludedOrdersCount" integer NOT NULL,
    "CreatedAtUtc" timestamp with time zone NOT NULL,
    "ExecutedAtUtc" timestamp with time zone,
    CONSTRAINT "PK_CreatorPayoutRecords" PRIMARY KEY ("Id")
);


CREATE TABLE "CreatorProfiles" (
    "Id" uuid NOT NULL,
    "TenantId" uuid NOT NULL,
    "FullName" text NOT NULL,
    "Handle" text NOT NULL,
    "Email" text NOT NULL,
    "PhoneNumber" text NOT NULL,
    "Platform" integer NOT NULL,
    "CommissionType" integer NOT NULL,
    "CommissionRate" numeric NOT NULL,
    "MinimumPayoutThreshold" numeric NOT NULL,
    "PaymentMethod" text NOT NULL,
    "PayoutDetails" text NOT NULL,
    "TotalEarned" numeric NOT NULL,
    "PendingEscrowBalance" numeric NOT NULL,
    "TotalPaidOut" numeric NOT NULL,
    "IsActive" boolean NOT NULL,
    "CreatedAtUtc" timestamp with time zone NOT NULL,
    CONSTRAINT "PK_CreatorProfiles" PRIMARY KEY ("Id")
);


CREATE TABLE "CryptographicPdfRecords" (
    "Id" uuid NOT NULL,
    "TenantId" uuid NOT NULL,
    "DocumentTitle" text NOT NULL,
    "DocumentContent" text NOT NULL,
    "OriginalSha256" text NOT NULL,
    "SealedSha256" text NOT NULL,
    "SignaturePlacementJson" text NOT NULL,
    "AuditTrailHashChainJson" text NOT NULL,
    "Status" text NOT NULL,
    "SealedAtUtc" timestamp with time zone,
    "CreatedAtUtc" timestamp with time zone NOT NULL,
    CONSTRAINT "PK_CryptographicPdfRecords" PRIMARY KEY ("Id")
);


CREATE TABLE "CsatSurveyResults" (
    "Id" uuid NOT NULL,
    "TenantId" uuid NOT NULL,
    "ConversationId" uuid NOT NULL,
    "CustomerContactId" uuid NOT NULL,
    "CustomerName" text NOT NULL,
    "Score" integer NOT NULL,
    "Channel" text NOT NULL,
    "FeedbackText" text NOT NULL,
    "RecordedAtUtc" timestamp with time zone NOT NULL,
    CONSTRAINT "PK_CsatSurveyResults" PRIMARY KEY ("Id")
);


CREATE TABLE "CurrencyRates" (
    "Id" uuid NOT NULL,
    "CurrencyCode" text NOT NULL,
    "RateToBaseUsd" numeric NOT NULL,
    "LastUpdatedUtc" timestamp with time zone NOT NULL,
    CONSTRAINT "PK_CurrencyRates" PRIMARY KEY ("Id")
);


CREATE TABLE "CustomerEvents" (
    "Id" uuid NOT NULL,
    "TenantId" uuid NOT NULL,
    "ContactId" uuid NOT NULL,
    "EventName" text NOT NULL,
    "PayloadJson" text NOT NULL,
    "OccurredAtUtc" timestamp with time zone NOT NULL,
    CONSTRAINT "PK_CustomerEvents" PRIMARY KEY ("Id")
);


CREATE TABLE "CustomerIdentityLinks" (
    "Id" uuid NOT NULL,
    "TenantId" uuid NOT NULL,
    "UnifiedCustomerId" uuid NOT NULL,
    "IdentityType" integer NOT NULL,
    "IdentityValue" text NOT NULL,
    "LinkedAtUtc" timestamp with time zone NOT NULL,
    CONSTRAINT "PK_CustomerIdentityLinks" PRIMARY KEY ("Id")
);


CREATE TABLE "CustomerJourneys" (
    "Id" uuid NOT NULL,
    "TenantId" uuid NOT NULL,
    "Name" text NOT NULL,
    "TriggerEvent" text NOT NULL,
    "Status" integer NOT NULL,
    "CreatedAtUtc" timestamp with time zone NOT NULL,
    CONSTRAINT "PK_CustomerJourneys" PRIMARY KEY ("Id")
);


CREATE TABLE "CustomerReputationScorecards" (
    "Id" uuid NOT NULL,
    "TenantId" uuid NOT NULL,
    "CustomerPhone" text NOT NULL,
    "CustomerEmail" text,
    "ReputationScore" integer NOT NULL,
    "Tier" integer NOT NULL,
    "TotalOrdersCount" integer NOT NULL,
    "DeliveredOrdersCount" integer NOT NULL,
    "RefusedReturnsCount" integer NOT NULL,
    "CancelledOrdersCount" integer NOT NULL,
    "TotalSpentAmount" numeric NOT NULL,
    "DeliverySuccessRatePercentage" numeric NOT NULL,
    "RequiresMicroDeposit" boolean NOT NULL,
    "LastEvaluatedAtUtc" timestamp with time zone NOT NULL,
    CONSTRAINT "PK_CustomerReputationScorecards" PRIMARY KEY ("Id")
);


CREATE TABLE "CustomerTimelineEvents" (
    "Id" uuid NOT NULL,
    "TenantId" uuid NOT NULL,
    "UnifiedCustomerId" uuid NOT NULL,
    "EventType" text NOT NULL,
    "Summary" text NOT NULL,
    "PayloadJson" text NOT NULL,
    "OccurredAtUtc" timestamp with time zone NOT NULL,
    CONSTRAINT "PK_CustomerTimelineEvents" PRIMARY KEY ("Id")
);


CREATE TABLE "CustomFieldDefinitions" (
    "Id" uuid NOT NULL,
    "TenantId" uuid NOT NULL,
    "EntityType" text NOT NULL,
    "FieldName" text NOT NULL,
    "FieldLabel" text NOT NULL,
    "FieldType" integer NOT NULL,
    "ValidationRegex" text,
    "IsRequired" boolean NOT NULL,
    "DefaultValue" text,
    "OptionsJson" text,
    "CreatedAtUtc" timestamp with time zone NOT NULL,
    CONSTRAINT "PK_CustomFieldDefinitions" PRIMARY KEY ("Id")
);


CREATE TABLE "CustomFieldValues" (
    "Id" uuid NOT NULL,
    "TenantId" uuid NOT NULL,
    "EntityId" uuid NOT NULL,
    "FieldDefinitionId" uuid NOT NULL,
    "ValueString" text,
    "ValueNumber" numeric,
    "ValueBoolean" boolean,
    "ValueDateTime" timestamp with time zone,
    "UpdatedAtUtc" timestamp with time zone NOT NULL,
    CONSTRAINT "PK_CustomFieldValues" PRIMARY KEY ("Id")
);


CREATE TABLE "DealMarginRecords" (
    "Id" uuid NOT NULL,
    "TenantId" uuid NOT NULL,
    "DealId" uuid NOT NULL,
    "OrganizationId" uuid NOT NULL,
    "DealTitle" text NOT NULL,
    "Revenue" numeric(18,2) NOT NULL,
    "CostOfGoodsSold" numeric(18,2) NOT NULL,
    "CourierShippingFees" numeric(18,2) NOT NULL,
    "OperatorLaborHours" numeric(18,2) NOT NULL,
    "OperatorHourlyRate" numeric(18,2) NOT NULL,
    "OperatorLaborCost" numeric(18,2) NOT NULL,
    "TrueGrossMargin" numeric(18,2) NOT NULL,
    "GrossMarginPercentage" numeric(18,2) NOT NULL,
    "CalculatedAtUtc" timestamp with time zone NOT NULL,
    CONSTRAINT "PK_DealMarginRecords" PRIMARY KEY ("Id")
);


CREATE TABLE "Deals" (
    "Id" uuid NOT NULL,
    "TenantId" uuid NOT NULL,
    "WorkspaceId" uuid,
    "ContactId" uuid NOT NULL,
    "CompanyId" uuid,
    "Title" text NOT NULL,
    "Amount" numeric NOT NULL,
    "Stage" integer NOT NULL,
    "SourceCampaignId" uuid,
    "OwnerId" uuid,
    "ExpectedCloseDate" timestamp with time zone,
    "Notes" text,
    "CreatedAtUtc" timestamp with time zone NOT NULL,
    CONSTRAINT "PK_Deals" PRIMARY KEY ("Id")
);


CREATE TABLE "DigitalAssets" (
    "Id" uuid NOT NULL,
    "TenantId" uuid NOT NULL,
    "Title" text NOT NULL,
    "FileName" text NOT NULL,
    "StoragePath" text NOT NULL,
    "MimeType" text NOT NULL,
    "FileSizeBytes" bigint NOT NULL,
    "MaxAllowedDownloads" integer NOT NULL,
    "ExpiryHours" integer NOT NULL,
    "CreatedAtUtc" timestamp with time zone NOT NULL,
    CONSTRAINT "PK_DigitalAssets" PRIMARY KEY ("Id")
);


CREATE TABLE "DigitalContracts" (
    "Id" uuid NOT NULL,
    "TenantId" uuid NOT NULL,
    "CompanyId" uuid,
    "DealId" uuid,
    "Title" text NOT NULL,
    "ContractType" text NOT NULL,
    "Status" integer NOT NULL,
    "SignerName" text NOT NULL,
    "SignerEmail" text NOT NULL,
    "ContractValue" numeric NOT NULL,
    "SigningToken" text NOT NULL,
    "SignedPdfUrl" text,
    "SignedAtUtc" timestamp with time zone,
    "CreatedAtUtc" timestamp with time zone NOT NULL,
    CONSTRAINT "PK_DigitalContracts" PRIMARY KEY ("Id")
);


CREATE TABLE "DigitalDownloadTokens" (
    "Id" uuid NOT NULL,
    "TenantId" uuid NOT NULL,
    "Token" text NOT NULL,
    "AssetId" uuid NOT NULL,
    "OrderId" uuid,
    "ContactId" uuid,
    "DownloadCount" integer NOT NULL,
    "MaxAllowedDownloads" integer NOT NULL,
    "ExpiresAtUtc" timestamp with time zone NOT NULL,
    "IsRevoked" boolean NOT NULL,
    "CreatedAtUtc" timestamp with time zone NOT NULL,
    CONSTRAINT "PK_DigitalDownloadTokens" PRIMARY KEY ("Id")
);


CREATE TABLE "DocumentAccessGates" (
    "Id" uuid NOT NULL,
    "TenantId" uuid NOT NULL,
    "DocumentId" uuid NOT NULL,
    "IsEmailRequired" boolean NOT NULL,
    "PasscodeHash" text,
    "IsWatermarkEnabled" boolean NOT NULL,
    "WatermarkPattern" text NOT NULL,
    "CreatedAtUtc" timestamp with time zone NOT NULL,
    CONSTRAINT "PK_DocumentAccessGates" PRIMARY KEY ("Id")
);


CREATE TABLE "DocumentChunks" (
    "Id" uuid NOT NULL,
    "TenantId" uuid NOT NULL,
    "DocumentId" uuid NOT NULL,
    "DocumentTitle" text NOT NULL,
    "SourceUrl" text,
    "ChunkIndex" integer NOT NULL,
    "Content" text NOT NULL,
    "TokenCount" integer NOT NULL,
    "EmbeddingJson" text NOT NULL,
    "MetadataJson" text,
    "CreatedAtUtc" timestamp with time zone NOT NULL,
    CONSTRAINT "PK_DocumentChunks" PRIMARY KEY ("Id")
);


CREATE TABLE "DocumentDwellTelemetries" (
    "Id" uuid NOT NULL,
    "TenantId" uuid NOT NULL,
    "DocumentId" uuid NOT NULL,
    "PageNumber" integer NOT NULL,
    "ViewerEmail" text NOT NULL,
    "ViewerIp" text NOT NULL,
    "DwellTimeSeconds" integer NOT NULL,
    "MaxScrollPercent" integer NOT NULL,
    "CapturedAtUtc" timestamp with time zone NOT NULL,
    CONSTRAINT "PK_DocumentDwellTelemetries" PRIMARY KEY ("Id")
);


CREATE TABLE "DocumentEnvelopes" (
    "Id" uuid NOT NULL,
    "TenantId" uuid NOT NULL,
    "Title" text NOT NULL,
    "DocumentUrl" text NOT NULL,
    "Status" integer NOT NULL,
    "ChecksumSha256" text NOT NULL,
    "IsSequentialOrder" boolean NOT NULL,
    "AuditCertificateJson" text NOT NULL,
    "CompletedAtUtc" timestamp with time zone,
    "CreatedAtUtc" timestamp with time zone NOT NULL,
    CONSTRAINT "PK_DocumentEnvelopes" PRIMARY KEY ("Id")
);


CREATE TABLE "DomainDnsHealthRecords" (
    "Id" uuid NOT NULL,
    "TenantId" uuid NOT NULL,
    "DomainName" text NOT NULL,
    "HasSpf" boolean NOT NULL,
    "SpfRecord" text NOT NULL,
    "HasDkim" boolean NOT NULL,
    "DkimSelector" text NOT NULL,
    "HasDmarc" boolean NOT NULL,
    "DmarcPolicy" text NOT NULL,
    "HasMx" boolean NOT NULL,
    "MxHost" text NOT NULL,
    "OverallHealthScore" integer NOT NULL,
    "OverallScore" integer NOT NULL,
    "DmarcRecord" text NOT NULL,
    "IssuesJson" text NOT NULL,
    "LastCheckedAtUtc" timestamp with time zone NOT NULL,
    CONSTRAINT "PK_DomainDnsHealthRecords" PRIMARY KEY ("Id")
);


CREATE TABLE "DynamicAgentTools" (
    "Id" uuid NOT NULL,
    "TenantId" uuid NOT NULL,
    "Name" text NOT NULL,
    "Description" text NOT NULL,
    "ParametersSchemaJson" text NOT NULL,
    "TargetEndpointUrl" text NOT NULL,
    "HttpMethod" text NOT NULL,
    "IsActive" boolean NOT NULL,
    "CreatedAtUtc" timestamp with time zone NOT NULL,
    CONSTRAINT "PK_DynamicAgentTools" PRIMARY KEY ("Id")
);


CREATE TABLE "DynamicFormDefinitions" (
    "Id" uuid NOT NULL,
    "TenantId" uuid NOT NULL,
    "Title" text NOT NULL,
    "Slug" text NOT NULL,
    "Description" text NOT NULL,
    "FieldsSchemaJson" text NOT NULL,
    "SubmitAction" text NOT NULL,
    "ActionPayload" text NOT NULL,
    "SubmissionCount" integer NOT NULL,
    "IsPublished" boolean NOT NULL,
    "CreatedAtUtc" timestamp with time zone NOT NULL,
    CONSTRAINT "PK_DynamicFormDefinitions" PRIMARY KEY ("Id")
);


CREATE TABLE "DynamicFormSubmissions" (
    "Id" uuid NOT NULL,
    "TenantId" uuid NOT NULL,
    "FormId" uuid NOT NULL,
    "RespondentContactId" uuid,
    "RespondentIp" text NOT NULL,
    "AnswersJson" text NOT NULL,
    "CompletedInSeconds" integer NOT NULL,
    "SubmittedAtUtc" timestamp with time zone NOT NULL,
    CONSTRAINT "PK_DynamicFormSubmissions" PRIMARY KEY ("Id")
);


CREATE TABLE "DynamicMarkdownPolicies" (
    "Id" uuid NOT NULL,
    "TenantId" uuid NOT NULL,
    "Tier" integer NOT NULL,
    "MinDaysInStock" integer NOT NULL,
    "MaxDaysInStock" integer NOT NULL,
    "RecommendedDiscountPercentage" numeric NOT NULL,
    "MinimumMarginFloorPercentage" numeric NOT NULL,
    "AutoApplyToStorefront" boolean NOT NULL,
    "IsActive" boolean NOT NULL,
    CONSTRAINT "PK_DynamicMarkdownPolicies" PRIMARY KEY ("Id")
);


CREATE TABLE "EmailSpamAnalysisReports" (
    "Id" uuid NOT NULL,
    "TenantId" uuid NOT NULL,
    "Subject" text NOT NULL,
    "BodyHtmlPreview" text NOT NULL,
    "SpamScore" integer NOT NULL,
    "TriggerWordsDetectedJson" text NOT NULL,
    "HasUnsubscribeHeader" boolean NOT NULL,
    "ImageToTextRatio" double precision NOT NULL,
    "Verdict" text NOT NULL,
    "RiskLevel" text NOT NULL,
    "CreatedAtUtc" timestamp with time zone NOT NULL,
    CONSTRAINT "PK_EmailSpamAnalysisReports" PRIMARY KEY ("Id")
);


CREATE TABLE "EnrichmentExecutionLogs" (
    "Id" uuid NOT NULL,
    "TenantId" uuid NOT NULL,
    "ContactId" uuid,
    "ProspectQuery" text NOT NULL,
    "ProviderUsed" text NOT NULL,
    "StepIndex" integer NOT NULL,
    "OutcomeStatus" text NOT NULL,
    "ConfidenceScore" numeric NOT NULL,
    "CreditCostCharged" integer NOT NULL,
    "LatencyMs" bigint NOT NULL,
    "EnrichedPayloadJson" text NOT NULL,
    "ExecutedAtUtc" timestamp with time zone NOT NULL,
    CONSTRAINT "PK_EnrichmentExecutionLogs" PRIMARY KEY ("Id")
);


CREATE TABLE "ExternalStoreSyncRecords" (
    "Id" uuid NOT NULL,
    "TenantId" uuid NOT NULL,
    "Platform" integer NOT NULL,
    "ExternalOrderId" text NOT NULL,
    "ExternalOrderNumber" text NOT NULL,
    "SGrowingOrderId" uuid NOT NULL,
    "SyncDirection" integer NOT NULL,
    "Status" text NOT NULL,
    "PayloadJson" text NOT NULL,
    "SyncedAtUtc" timestamp with time zone NOT NULL,
    CONSTRAINT "PK_ExternalStoreSyncRecords" PRIMARY KEY ("Id")
);


CREATE TABLE "FactoringReceivableRecords" (
    "Id" uuid NOT NULL,
    "TenantId" uuid NOT NULL,
    "CourierProviderId" text NOT NULL,
    "CourierName" text NOT NULL,
    "InTransitCashFloat" numeric NOT NULL,
    "VerifiedDeliveredCash" numeric NOT NULL,
    "AdvanceDiscountRatePercentage" numeric NOT NULL,
    "Status" text NOT NULL,
    "FundedAtUtc" timestamp with time zone,
    "CreatedAtUtc" timestamp with time zone NOT NULL,
    CONSTRAINT "PK_FactoringReceivableRecords" PRIMARY KEY ("Id")
);


CREATE TABLE "FeatureEntitlements" (
    "Id" uuid NOT NULL,
    "TenantId" uuid NOT NULL,
    "PlanTier" integer NOT NULL,
    "MaxTeamSeats" integer NOT NULL,
    "MonthlyIncludedAiTokens" bigint NOT NULL,
    "MonthlyIncludedVoiceMinutes" integer NOT NULL,
    "MonthlyIncludedMessages" integer NOT NULL,
    "MaxWarmupMailboxes" integer NOT NULL,
    "MonthlyIncludedColdEmails" bigint NOT NULL,
    "MaxCarrierWaybills" integer NOT NULL,
    "StorageMbLimit" bigint NOT NULL,
    "IsCustomDomainEnabled" boolean NOT NULL,
    "IsSlaEscalationEnabled" boolean NOT NULL,
    "MaxAiTokensPerMonth" bigint NOT NULL,
    "MaxVoiceMinutesPerMonth" integer NOT NULL,
    CONSTRAINT "PK_FeatureEntitlements" PRIMARY KEY ("Id")
);


CREATE TABLE "FeatureFeedbackPosts" (
    "Id" uuid NOT NULL,
    "TenantId" uuid NOT NULL,
    "Title" text NOT NULL,
    "Description" text NOT NULL,
    "Category" text NOT NULL,
    "Status" integer NOT NULL,
    "UpvoteCount" integer NOT NULL,
    "AuthorEmail" text NOT NULL,
    "AuthorName" text NOT NULL,
    "CreatedAtUtc" timestamp with time zone NOT NULL,
    CONSTRAINT "PK_FeatureFeedbackPosts" PRIMARY KEY ("Id")
);


CREATE TABLE "FeatureFeedbackVotes" (
    "Id" uuid NOT NULL,
    "TenantId" uuid NOT NULL,
    "PostId" uuid NOT NULL,
    "VoterIdentifier" text NOT NULL,
    "VotedAtUtc" timestamp with time zone NOT NULL,
    CONSTRAINT "PK_FeatureFeedbackVotes" PRIMARY KEY ("Id")
);


CREATE TABLE "FeedbackPostVotes" (
    "Id" uuid NOT NULL,
    "TenantId" uuid NOT NULL,
    "PostId" uuid NOT NULL,
    "VoterEmail" text NOT NULL,
    "VotedAtUtc" timestamp with time zone NOT NULL,
    CONSTRAINT "PK_FeedbackPostVotes" PRIMARY KEY ("Id")
);


CREATE TABLE "FlowDefinitions" (
    "Id" uuid NOT NULL,
    "TenantId" uuid NOT NULL,
    "Name" text NOT NULL,
    "TriggerKeyword" text NOT NULL,
    "Description" text NOT NULL,
    "IsActive" boolean NOT NULL,
    "NodesJson" text NOT NULL,
    "CreatedAtUtc" timestamp with time zone NOT NULL,
    CONSTRAINT "PK_FlowDefinitions" PRIMARY KEY ("Id")
);


CREATE TABLE "FlowExecutionSessions" (
    "Id" uuid NOT NULL,
    "TenantId" uuid NOT NULL,
    "FlowDefinitionId" uuid NOT NULL,
    "CustomerPhone" text NOT NULL,
    "CurrentNodeKey" text NOT NULL,
    "VariablesJson" text NOT NULL,
    "DetectedLanguage" text NOT NULL,
    "SentimentScore" numeric NOT NULL,
    "IsEscalatedToHuman" boolean NOT NULL,
    "StartedAtUtc" timestamp with time zone NOT NULL,
    "LastInteractionAtUtc" timestamp with time zone NOT NULL,
    CONSTRAINT "PK_FlowExecutionSessions" PRIMARY KEY ("Id")
);


CREATE TABLE "FunnelConversionEvents" (
    "Id" uuid NOT NULL,
    "TenantId" uuid NOT NULL,
    "FunnelId" uuid NOT NULL,
    "StepId" uuid,
    "EventType" integer NOT NULL,
    "OrderId" uuid,
    "CustomerPhone" text,
    "EventValue" numeric NOT NULL,
    "CapturedAtUtc" timestamp with time zone NOT NULL,
    CONSTRAINT "PK_FunnelConversionEvents" PRIMARY KEY ("Id")
);


CREATE TABLE "FunnelSplitTests" (
    "Id" uuid NOT NULL,
    "FunnelRunId" uuid NOT NULL,
    "Status" text NOT NULL,
    "VariantA_Name" text NOT NULL,
    "VariantA_Headline" text NOT NULL,
    "VariantA_TrafficRatio" double precision NOT NULL,
    "VariantA_Views" integer NOT NULL,
    "VariantA_Conversions" integer NOT NULL,
    "VariantB_Name" text NOT NULL,
    "VariantB_Headline" text NOT NULL,
    "VariantB_TrafficRatio" double precision NOT NULL,
    "VariantB_Views" integer NOT NULL,
    "VariantB_Conversions" integer NOT NULL,
    "WinnerVariant" text,
    "ConfidenceScore" double precision NOT NULL,
    "CalculatedAtUtc" timestamp with time zone NOT NULL,
    "TenantId" uuid NOT NULL,
    "CreatedAtUtc" timestamp with time zone NOT NULL,
    "UpdatedAtUtc" timestamp with time zone,
    "IsDeleted" boolean NOT NULL,
    "RowVersion" bigint NOT NULL,
    CONSTRAINT "PK_FunnelSplitTests" PRIMARY KEY ("Id")
);


CREATE TABLE "FunnelSteps" (
    "Id" uuid NOT NULL,
    "TenantId" uuid NOT NULL,
    "FunnelId" uuid NOT NULL,
    "StepName" text NOT NULL,
    "StepType" integer NOT NULL,
    "StepOrder" integer NOT NULL,
    "TargetUrlOrSlug" text NOT NULL,
    "PageTemplateJson" text NOT NULL,
    "VisitorsCount" integer NOT NULL,
    "ConversionsCount" integer NOT NULL,
    "CreatedAtUtc" timestamp with time zone NOT NULL,
    CONSTRAINT "PK_FunnelSteps" PRIMARY KEY ("Id")
);


CREATE TABLE "GoogleSheetsSyncConfigs" (
    "Id" uuid NOT NULL,
    "TenantId" uuid NOT NULL,
    "SpreadsheetId" text NOT NULL,
    "SheetTabName" text NOT NULL,
    "WebhookOrEndpointUrl" text NOT NULL,
    "IsActive" boolean NOT NULL,
    "AutoSyncOnConfirmed" boolean NOT NULL,
    "LastSyncedAtUtc" timestamp with time zone,
    "RowsAppendedCount" integer NOT NULL,
    CONSTRAINT "PK_GoogleSheetsSyncConfigs" PRIMARY KEY ("Id")
);


CREATE TABLE "HelpArticles" (
    "Id" uuid NOT NULL,
    "TenantId" uuid NOT NULL,
    "CategoryId" uuid NOT NULL,
    "Title" text NOT NULL,
    "Slug" text NOT NULL,
    "ContentMarkdown" text NOT NULL,
    "IsPublished" boolean NOT NULL,
    "ViewCount" integer NOT NULL,
    "CreatedAtUtc" timestamp with time zone NOT NULL,
    CONSTRAINT "PK_HelpArticles" PRIMARY KEY ("Id")
);


CREATE TABLE "HelpCategories" (
    "Id" uuid NOT NULL,
    "TenantId" uuid NOT NULL,
    "Name" text NOT NULL,
    "Slug" text NOT NULL,
    "OrderIndex" integer NOT NULL,
    "Icon" text NOT NULL,
    CONSTRAINT "PK_HelpCategories" PRIMARY KEY ("Id")
);


CREATE TABLE "HostAvailabilitySchedules" (
    "Id" uuid NOT NULL,
    "TenantId" uuid NOT NULL,
    "HostUserId" uuid NOT NULL,
    "HostName" text NOT NULL,
    "TimeZone" text NOT NULL,
    "WeeklyHoursJson" text NOT NULL,
    "BlackoutDatesJson" text NOT NULL,
    "Weight" double precision NOT NULL,
    "CreatedAtUtc" timestamp with time zone NOT NULL,
    CONSTRAINT "PK_HostAvailabilitySchedules" PRIMARY KEY ("Id")
);


CREATE TABLE customer_data.identities (
    "Id" uuid NOT NULL,
    "TenantId" character varying(100) NOT NULL,
    "Type" character varying(50) NOT NULL,
    "Value" character varying(256) NOT NULL,
    "CanonicalProfileId" character varying(100) NOT NULL,
    "CreatedAtUtc" timestamp with time zone NOT NULL,
    CONSTRAINT "PK_identities" PRIMARY KEY ("Id")
);


CREATE TABLE customer_data.identity_links (
    "Id" uuid NOT NULL,
    "TenantId" character varying(100) NOT NULL,
    "SourceIdentifierJson" jsonb NOT NULL,
    "TargetIdentifierJson" jsonb NOT NULL,
    "ConfidenceScore" double precision NOT NULL,
    "MatchReason" text NOT NULL,
    "CreatedAtUtc" timestamp with time zone NOT NULL,
    CONSTRAINT "PK_identity_links" PRIMARY KEY ("Id")
);


CREATE TABLE "InAppMicroSurveyRecords" (
    "Id" uuid NOT NULL,
    "TenantId" uuid NOT NULL,
    "Title" text NOT NULL,
    "TriggerAction" text NOT NULL,
    "RecontactDays" integer NOT NULL,
    "DisplayLimitPercent" integer NOT NULL,
    "StepsJson" text NOT NULL,
    "IsActive" boolean NOT NULL,
    "CreatedAtUtc" timestamp with time zone NOT NULL,
    CONSTRAINT "PK_InAppMicroSurveyRecords" PRIMARY KEY ("Id")
);


CREATE TABLE "Inboxes" (
    "Id" uuid NOT NULL,
    "TenantId" uuid NOT NULL,
    "Name" character varying(100) NOT NULL,
    "ChannelType" integer NOT NULL,
    "ConfigJson" text NOT NULL,
    "IsActive" boolean NOT NULL,
    "CreatedAtUtc" timestamp with time zone NOT NULL,
    CONSTRAINT "PK_Inboxes" PRIMARY KEY ("Id")
);


CREATE TABLE "InChatCartSessions" (
    "Id" uuid NOT NULL,
    "TenantId" uuid NOT NULL,
    "ConversationId" uuid NOT NULL,
    "CustomerPhone" text NOT NULL,
    "CustomerName" text NOT NULL,
    "DraftOrderId" uuid,
    "Status" integer NOT NULL,
    "CartItemsJson" text NOT NULL,
    "TotalAmount" numeric NOT NULL,
    "Currency" text NOT NULL,
    "CreatedAtUtc" timestamp with time zone NOT NULL,
    "ConfirmedAtUtc" timestamp with time zone,
    CONSTRAINT "PK_InChatCartSessions" PRIMARY KEY ("Id")
);


CREATE TABLE "Invoices" (
    "Id" uuid NOT NULL,
    "TenantId" uuid NOT NULL,
    "DealId" uuid,
    "ContactId" uuid,
    "InvoiceNumber" text NOT NULL,
    "ClientName" text NOT NULL,
    "ClientEmail" text,
    "Currency" text NOT NULL,
    "ExchangeRate" numeric NOT NULL,
    "SubTotal" numeric NOT NULL,
    "TaxPercentage" numeric NOT NULL,
    "TaxAmount" numeric NOT NULL,
    "TotalAmount" numeric NOT NULL,
    "TotalBaseAmountUsd" numeric NOT NULL,
    "Status" integer NOT NULL,
    "DueDateUtc" timestamp with time zone NOT NULL,
    "PaidAtUtc" timestamp with time zone,
    "Notes" text,
    "CreatedAtUtc" timestamp with time zone NOT NULL,
    "UpdatedAtUtc" timestamp with time zone NOT NULL,
    CONSTRAINT "PK_Invoices" PRIMARY KEY ("Id")
);


CREATE TABLE "JourneyDefinitions" (
    "Id" uuid NOT NULL,
    "TenantId" uuid NOT NULL,
    "Name" text NOT NULL,
    "Description" text NOT NULL,
    "TriggerEvent" text NOT NULL,
    "ConversionEventsJson" text NOT NULL,
    "NodesJson" text NOT NULL,
    "IsActive" boolean NOT NULL,
    "CreatedAtUtc" timestamp with time zone NOT NULL,
    "UpdatedAtUtc" timestamp with time zone NOT NULL,
    CONSTRAINT "PK_JourneyDefinitions" PRIMARY KEY ("Id")
);


CREATE TABLE "JourneyEnrollments" (
    "Id" uuid NOT NULL,
    "TenantId" uuid NOT NULL,
    "JourneyId" uuid NOT NULL,
    "ContactId" uuid NOT NULL,
    "CurrentStepOrder" integer NOT NULL,
    "Status" integer NOT NULL,
    "NextExecutionAtUtc" timestamp with time zone,
    "CreatedAtUtc" timestamp with time zone NOT NULL,
    CONSTRAINT "PK_JourneyEnrollments" PRIMARY KEY ("Id")
);


CREATE TABLE "JourneyInstances" (
    "Id" uuid NOT NULL,
    "TenantId" uuid NOT NULL,
    "JourneyDefinitionId" uuid NOT NULL,
    "ContactId" uuid NOT NULL,
    "CurrentNodeId" text NOT NULL,
    "Status" integer NOT NULL,
    "WaitUntilEventName" text,
    "WaitUntilExpiresAtUtc" timestamp with time zone,
    "NextExecutionAtUtc" timestamp with time zone,
    "AttributesJson" text NOT NULL,
    "ExecutionHistoryJson" text NOT NULL,
    "EnrolledAtUtc" timestamp with time zone NOT NULL,
    "CompletedAtUtc" timestamp with time zone,
    "UpdatedAtUtc" timestamp with time zone NOT NULL,
    CONSTRAINT "PK_JourneyInstances" PRIMARY KEY ("Id")
);


CREATE TABLE "LeadEnrichmentProfiles" (
    "Id" uuid NOT NULL,
    "TenantId" uuid NOT NULL,
    "Domain" text NOT NULL,
    "CompanyName" text NOT NULL,
    "Industry" text NOT NULL,
    "EmployeeCountRange" text NOT NULL,
    "EstimatedAnnualRevenue" text NOT NULL,
    "TechStack" text NOT NULL,
    "LinkedinUrl" text,
    "TwitterHandle" text,
    "EnrichedAtUtc" timestamp with time zone NOT NULL,
    CONSTRAINT "PK_LeadEnrichmentProfiles" PRIMARY KEY ("Id")
);


CREATE TABLE "LiquidationBundleOffers" (
    "Id" uuid NOT NULL,
    "TenantId" uuid NOT NULL,
    "BundleName" text NOT NULL,
    "WinningProductSku" text NOT NULL,
    "AgingProductSku" text NOT NULL,
    "OriginalCombinedPrice" numeric NOT NULL,
    "DiscountedBundlePrice" numeric NOT NULL,
    "IncentiveDiscountPercentage" numeric NOT NULL,
    "ActiveInChatCpq" boolean NOT NULL,
    "CreatedAtUtc" timestamp with time zone NOT NULL,
    CONSTRAINT "PK_LiquidationBundleOffers" PRIMARY KEY ("Id")
);


CREATE TABLE "LiveFlashDeals" (
    "Id" uuid NOT NULL,
    "TenantId" uuid NOT NULL,
    "SessionId" uuid NOT NULL,
    "Sku" text NOT NULL,
    "ProductName" text NOT NULL,
    "TriggerKeyword" text NOT NULL,
    "FlashPrice" numeric NOT NULL,
    "OriginalPrice" numeric NOT NULL,
    "MaxQuantity" integer NOT NULL,
    "SoldQuantity" integer NOT NULL,
    "IsActive" boolean NOT NULL,
    CONSTRAINT "PK_LiveFlashDeals" PRIMARY KEY ("Id")
);


CREATE TABLE "LiveStreamSessions" (
    "Id" uuid NOT NULL,
    "TenantId" uuid NOT NULL,
    "Platform" integer NOT NULL,
    "Title" text NOT NULL,
    "StreamExternalId" text NOT NULL,
    "IsActive" boolean NOT NULL,
    "TotalCommentsProcessed" integer NOT NULL,
    "TotalOrdersGenerated" integer NOT NULL,
    "StartedAtUtc" timestamp with time zone NOT NULL,
    "EndedAtUtc" timestamp with time zone,
    CONSTRAINT "PK_LiveStreamSessions" PRIMARY KEY ("Id")
);


CREATE TABLE "MailboxWarmupCadences" (
    "Id" uuid NOT NULL,
    "TenantId" uuid NOT NULL,
    "SenderEmail" text NOT NULL,
    "DailyTargetLimit" integer NOT NULL,
    "DailyTarget" integer NOT NULL,
    "CurrentDayNumber" integer NOT NULL,
    "WarmupRampCurve" text NOT NULL,
    "TotalSimulatedSent" integer NOT NULL,
    "TotalSentLifetime" integer NOT NULL,
    "CurrentDailyVolume" integer NOT NULL,
    "SentToday" integer NOT NULL,
    "TotalSimulatedReceived" integer NOT NULL,
    "SpamRescueCount" integer NOT NULL,
    "IsActive" boolean NOT NULL,
    "StartedAtUtc" timestamp with time zone NOT NULL,
    CONSTRAINT "PK_MailboxWarmupCadences" PRIMARY KEY ("Id")
);


CREATE TABLE "MediaVaultRecords" (
    "Id" uuid NOT NULL,
    "TenantId" uuid NOT NULL,
    "FileName" text NOT NULL,
    "ContentType" text NOT NULL,
    "FileSizeBytes" bigint NOT NULL,
    "StorageKey" text NOT NULL,
    "Status" text NOT NULL,
    "ExpiresAtUtc" timestamp with time zone NOT NULL,
    "CreatedAtUtc" timestamp with time zone NOT NULL,
    CONSTRAINT "PK_MediaVaultRecords" PRIMARY KEY ("Id")
);


CREATE TABLE "Messages" (
    "Id" uuid NOT NULL,
    "TenantId" uuid NOT NULL,
    "ConversationId" uuid NOT NULL,
    "SenderType" integer NOT NULL,
    "SenderId" uuid,
    "ContentType" integer NOT NULL,
    "Content" text NOT NULL,
    "MediaUrl" text,
    "ExternalMessageId" text,
    "DeliveryStatus" integer NOT NULL,
    "IsPrivateNote" boolean NOT NULL,
    "CreatedAtUtc" timestamp with time zone NOT NULL,
    CONSTRAINT "PK_Messages" PRIMARY KEY ("Id")
);


CREATE TABLE "MetaCatalogItems" (
    "Id" uuid NOT NULL,
    "TenantId" uuid NOT NULL,
    "RetailerId" text NOT NULL,
    "Title" text NOT NULL,
    "Description" text NOT NULL,
    "Availability" text NOT NULL,
    "Condition" text NOT NULL,
    "Price" numeric NOT NULL,
    "Currency" text NOT NULL,
    "Link" text NOT NULL,
    "ImageLink" text NOT NULL,
    "Brand" text NOT NULL,
    "GoogleProductCategory" text NOT NULL,
    "LastUpdatedUtc" timestamp with time zone NOT NULL,
    CONSTRAINT "PK_MetaCatalogItems" PRIMARY KEY ("Id")
);


CREATE TABLE "MetaCatalogSyncFeeds" (
    "Id" uuid NOT NULL,
    "TenantId" uuid NOT NULL,
    "CatalogId" text NOT NULL,
    "FeedFormat" text NOT NULL,
    "TotalProducts" integer NOT NULL,
    "LastSyncedAtUtc" timestamp with time zone NOT NULL,
    "SyncStatus" text NOT NULL,
    CONSTRAINT "PK_MetaCatalogSyncFeeds" PRIMARY KEY ("Id")
);


CREATE TABLE "MicroDepositPaymentLinks" (
    "Id" uuid NOT NULL,
    "TenantId" uuid NOT NULL,
    "OrderId" uuid NOT NULL,
    "CustomerPhone" text NOT NULL,
    "DepositAmount" numeric NOT NULL,
    "Currency" text NOT NULL,
    "PaymentUrl" text NOT NULL,
    "PaymentProvider" text NOT NULL,
    "Status" integer NOT NULL,
    "PaidAtUtc" timestamp with time zone,
    "ExpiresAtUtc" timestamp with time zone NOT NULL,
    "CreatedAtUtc" timestamp with time zone NOT NULL,
    CONSTRAINT "PK_MicroDepositPaymentLinks" PRIMARY KEY ("Id")
);


CREATE TABLE "MicroSurveyResponsePartials" (
    "Id" uuid NOT NULL,
    "TenantId" uuid NOT NULL,
    "SurveyId" uuid NOT NULL,
    "RespondentUserId" text NOT NULL,
    "AnswersJson" text NOT NULL,
    "LastCompletedStepIndex" integer NOT NULL,
    "IsFinished" boolean NOT NULL,
    "DropoffStep" text NOT NULL,
    "StartedAtUtc" timestamp with time zone NOT NULL,
    "LastUpdatedAtUtc" timestamp with time zone NOT NULL,
    CONSTRAINT "PK_MicroSurveyResponsePartials" PRIMARY KEY ("Id")
);


CREATE TABLE "NewsletterBroadcasts" (
    "Id" uuid NOT NULL,
    "TenantId" uuid NOT NULL,
    "ArticleId" uuid NOT NULL,
    "Subject" text NOT NULL,
    "RecipientSegment" text NOT NULL,
    "TotalRecipientsCount" integer NOT NULL,
    "DeliveredCount" integer NOT NULL,
    "OpenedCount" integer NOT NULL,
    "ClickedCount" integer NOT NULL,
    "Status" integer NOT NULL,
    "SentAtUtc" timestamp with time zone,
    "CreatedAtUtc" timestamp with time zone NOT NULL,
    CONSTRAINT "PK_NewsletterBroadcasts" PRIMARY KEY ("Id")
);


CREATE TABLE "OneClickUpsellOffers" (
    "Id" uuid NOT NULL,
    "TenantId" uuid NOT NULL,
    "FunnelId" uuid NOT NULL,
    "StepId" uuid NOT NULL,
    "OfferTitle" text NOT NULL,
    "ProductSku" text NOT NULL,
    "Headline" text NOT NULL,
    "ImageUrl" text NOT NULL,
    "OriginalPrice" numeric NOT NULL,
    "UpsellPrice" numeric NOT NULL,
    "IsActive" boolean NOT NULL,
    "CreatedAtUtc" timestamp with time zone NOT NULL,
    CONSTRAINT "PK_OneClickUpsellOffers" PRIMARY KEY ("Id")
);


CREATE TABLE "OrderBumpOffers" (
    "Id" uuid NOT NULL,
    "TenantId" uuid NOT NULL,
    "FunnelId" uuid NOT NULL,
    "StepId" uuid NOT NULL,
    "Title" text NOT NULL,
    "Headline" text NOT NULL,
    "Description" text NOT NULL,
    "ProductSku" text NOT NULL,
    "OriginalPrice" numeric NOT NULL,
    "DiscountedPrice" numeric NOT NULL,
    "BadgeText" text NOT NULL,
    "IsActive" boolean NOT NULL,
    "CreatedAtUtc" timestamp with time zone NOT NULL,
    CONSTRAINT "PK_OrderBumpOffers" PRIMARY KEY ("Id")
);


CREATE TABLE "OrderCollisionLocks" (
    "Id" uuid NOT NULL,
    "TenantId" uuid NOT NULL,
    "OrderId" uuid NOT NULL,
    "AgentId" text NOT NULL,
    "AgentName" text NOT NULL,
    "LockedAtUtc" timestamp with time zone NOT NULL,
    "ExpiresAtUtc" timestamp with time zone NOT NULL,
    "IsActive" boolean NOT NULL,
    CONSTRAINT "PK_OrderCollisionLocks" PRIMARY KEY ("Id")
);


CREATE TABLE "OrganizationRecords" (
    "Id" uuid NOT NULL,
    "TenantId" uuid NOT NULL,
    "Name" text NOT NULL,
    "Code" text NOT NULL,
    "Currency" text NOT NULL,
    "CreatedAtUtc" timestamp with time zone NOT NULL,
    CONSTRAINT "PK_OrganizationRecords" PRIMARY KEY ("Id")
);


CREATE TABLE "OutboundCallAttempts" (
    "Id" uuid NOT NULL,
    "TenantId" uuid NOT NULL,
    "CampaignId" uuid,
    "OrderId" uuid NOT NULL,
    "CustomerPhone" text NOT NULL,
    "CustomerName" text NOT NULL,
    "AttemptNumber" integer NOT NULL,
    "State" integer NOT NULL,
    "SpokenIntent" integer NOT NULL,
    "SpeechTranscript" text,
    "DtmfDigit" text,
    "CallDurationSeconds" integer NOT NULL,
    "ScheduledRetryUtc" timestamp with time zone,
    "CreatedAtUtc" timestamp with time zone NOT NULL,
    "CompletedAtUtc" timestamp with time zone,
    CONSTRAINT "PK_OutboundCallAttempts" PRIMARY KEY ("Id")
);


CREATE TABLE "OutboundCampaigns" (
    "Id" uuid NOT NULL,
    "TenantId" uuid NOT NULL,
    "Name" text NOT NULL,
    "Status" integer NOT NULL,
    "SenderPoolIdsJson" text NOT NULL,
    "CreatedAtUtc" timestamp with time zone NOT NULL,
    CONSTRAINT "PK_OutboundCampaigns" PRIMARY KEY ("Id")
);


CREATE TABLE "OutboxMessages" (
    "Id" uuid NOT NULL,
    "TenantId" uuid NOT NULL,
    "OccurredOnUtc" timestamp with time zone NOT NULL,
    "EventType" character varying(100) NOT NULL,
    "Payload" text NOT NULL,
    "ProcessedOnUtc" timestamp with time zone,
    "RetryCount" integer NOT NULL,
    "Error" text,
    CONSTRAINT "PK_OutboxMessages" PRIMARY KEY ("Id")
);


CREATE TABLE "PageAuditResults" (
    "Id" uuid NOT NULL,
    "TenantId" uuid NOT NULL,
    "AuditReportId" uuid NOT NULL,
    "PageUrl" text NOT NULL,
    "StatusCode" integer NOT NULL,
    "Title" text NOT NULL,
    "MetaDescription" text NOT NULL,
    "H1Tag" text NOT NULL,
    "HasCanonical" boolean NOT NULL,
    "HasOpenGraph" boolean NOT NULL,
    "HasSchemaOrgJsonLd" boolean NOT NULL,
    "LoadTimeMs" integer NOT NULL,
    "HealthScore" integer NOT NULL,
    "IssuesJson" text NOT NULL,
    "CreatedAtUtc" timestamp with time zone NOT NULL,
    CONSTRAINT "PK_PageAuditResults" PRIMARY KEY ("Id")
);


CREATE TABLE "Payments" (
    "Id" uuid NOT NULL,
    "TenantId" uuid NOT NULL,
    "InvoiceId" uuid NOT NULL,
    "Amount" numeric NOT NULL,
    "Currency" text NOT NULL,
    "PaymentMethod" text NOT NULL,
    "TransactionReference" text,
    "PaidAtUtc" timestamp with time zone NOT NULL,
    "Status" text NOT NULL,
    CONSTRAINT "PK_Payments" PRIMARY KEY ("Id")
);


CREATE TABLE "PimVariantMatrices" (
    "Id" uuid NOT NULL,
    "TenantId" uuid NOT NULL,
    "BaseSku" text NOT NULL,
    "VariantSku" text NOT NULL,
    "AttributesJson" text NOT NULL,
    "Price" numeric NOT NULL,
    "StockQuantity" integer NOT NULL,
    "CreatedAtUtc" timestamp with time zone NOT NULL,
    CONSTRAINT "PK_PimVariantMatrices" PRIMARY KEY ("Id")
);


CREATE TABLE "PostPurchaseSurveys" (
    "Id" uuid NOT NULL,
    "TenantId" uuid NOT NULL,
    "OrderId" uuid NOT NULL,
    "CustomerPhone" text NOT NULL,
    "CustomerName" text NOT NULL,
    "Rating" integer,
    "FeedbackNotes" text,
    "SentAtUtc" timestamp with time zone NOT NULL,
    "RespondedAtUtc" timestamp with time zone,
    "DiscountCodeIssued" text,
    CONSTRAINT "PK_PostPurchaseSurveys" PRIMARY KEY ("Id")
);


CREATE TABLE "PrivacyErasureRequests" (
    "Id" uuid NOT NULL,
    "TenantId" uuid NOT NULL,
    "ContactEmail" text NOT NULL,
    "ContactPhone" text,
    "Status" text NOT NULL,
    "ErasedRecordsCount" integer NOT NULL,
    "RequestedAtUtc" timestamp with time zone NOT NULL,
    "CompletedAtUtc" timestamp with time zone,
    CONSTRAINT "PK_PrivacyErasureRequests" PRIMARY KEY ("Id")
);


CREATE TABLE "ProductBurnRateConfigs" (
    "Id" uuid NOT NULL,
    "TenantId" uuid NOT NULL,
    "Sku" text NOT NULL,
    "ProductName" text NOT NULL,
    "EstimatedDaysOfUse" integer NOT NULL,
    "ReminderLeadDays" integer NOT NULL,
    "IncentiveDiscountPercent" numeric NOT NULL,
    "IsActive" boolean NOT NULL,
    CONSTRAINT "PK_ProductBurnRateConfigs" PRIMARY KEY ("Id")
);


CREATE TABLE "ProductLicenseKeys" (
    "Id" uuid NOT NULL,
    "TenantId" uuid NOT NULL,
    "LicenseKey" text NOT NULL,
    "OrderId" uuid,
    "ProductSku" text NOT NULL,
    "MaxSeats" integer NOT NULL,
    "ActivatedSeats" integer NOT NULL,
    "Status" text NOT NULL,
    "ExpiresAtUtc" timestamp with time zone,
    "CreatedAtUtc" timestamp with time zone NOT NULL,
    CONSTRAINT "PK_ProductLicenseKeys" PRIMARY KEY ("Id")
);


CREATE TABLE "Products" (
    "Id" uuid NOT NULL,
    "TenantId" uuid NOT NULL,
    "Name" text NOT NULL,
    "Slug" text NOT NULL,
    "Description" text NOT NULL,
    "ProductType" integer NOT NULL,
    "BasePrice" numeric NOT NULL,
    "Currency" text NOT NULL,
    "IsActive" boolean NOT NULL,
    "CreatedAtUtc" timestamp with time zone NOT NULL,
    CONSTRAINT "PK_Products" PRIMARY KEY ("Id")
);


CREATE TABLE "ProductSalesVelocities" (
    "Id" uuid NOT NULL,
    "TenantId" uuid NOT NULL,
    "ProductId" uuid NOT NULL,
    "Sku" text NOT NULL,
    "ProductName" text NOT NULL,
    "Trailing7DaysUnitsSold" numeric NOT NULL,
    "Trailing30DaysUnitsSold" numeric NOT NULL,
    "DailyRunRateUnits" numeric NOT NULL,
    "ActivePaidAdSpendMultiplier" numeric NOT NULL,
    "WeightedProjectedDailyDemand" numeric NOT NULL,
    "LastCalculatedUtc" timestamp with time zone NOT NULL,
    CONSTRAINT "PK_ProductSalesVelocities" PRIMARY KEY ("Id")
);


CREATE TABLE "ProductVariants" (
    "Id" uuid NOT NULL,
    "TenantId" uuid NOT NULL,
    "ProductId" uuid NOT NULL,
    "SKU" text NOT NULL,
    "Name" text NOT NULL,
    "Price" numeric NOT NULL,
    "InventoryStock" integer NOT NULL,
    "AttributesJson" text NOT NULL,
    "IsActive" boolean NOT NULL,
    CONSTRAINT "PK_ProductVariants" PRIMARY KEY ("Id")
);


CREATE TABLE customer_data.profiles (
    "Id" uuid NOT NULL,
    "TenantId" character varying(100) NOT NULL,
    "CanonicalProfileId" character varying(100) NOT NULL,
    "UnifiedTraitsJson" jsonb NOT NULL,
    "FirstSeenUtc" timestamp with time zone NOT NULL,
    "LastActiveUtc" timestamp with time zone NOT NULL,
    CONSTRAINT "PK_profiles" PRIMARY KEY ("Id")
);


CREATE TABLE "Projects" (
    "Id" uuid NOT NULL,
    "TenantId" uuid NOT NULL,
    "Name" text NOT NULL,
    "Description" text NOT NULL,
    "Status" text NOT NULL,
    "DealId" uuid,
    "ContractId" uuid,
    "CreatedAtUtc" timestamp with time zone NOT NULL,
    "TargetLaunchDateUtc" timestamp with time zone,
    CONSTRAINT "PK_Projects" PRIMARY KEY ("Id")
);


CREATE TABLE "ProspectEnrollments" (
    "Id" uuid NOT NULL,
    "TenantId" uuid NOT NULL,
    "CampaignId" uuid NOT NULL,
    "ContactId" uuid NOT NULL,
    "CurrentStepNumber" integer NOT NULL,
    "Status" integer NOT NULL,
    "PersonalizedHook" text,
    "NextRunAtUtc" timestamp with time zone,
    "LastDispatchedAtUtc" timestamp with time zone,
    "CreatedAtUtc" timestamp with time zone NOT NULL,
    CONSTRAINT "PK_ProspectEnrollments" PRIMARY KEY ("Id")
);


CREATE TABLE "PublicationArticles" (
    "Id" uuid NOT NULL,
    "TenantId" uuid NOT NULL,
    "Title" text NOT NULL,
    "Slug" text NOT NULL,
    "Excerpt" text NOT NULL,
    "ContentLexicalAstJson" text NOT NULL,
    "HtmlContent" text NOT NULL,
    "FeaturedImageUrl" text NOT NULL,
    "AuthorName" text NOT NULL,
    "Visibility" integer NOT NULL,
    "Status" integer NOT NULL,
    "PublishedAtUtc" timestamp with time zone,
    "ScheduledAtUtc" timestamp with time zone,
    "ViewCount" integer NOT NULL,
    "EstimatedReadMinutes" integer NOT NULL,
    "MetaTitle" text NOT NULL,
    "MetaDescription" text NOT NULL,
    "CanonicalUrl" text NOT NULL,
    "CreatedAtUtc" timestamp with time zone NOT NULL,
    CONSTRAINT "PK_PublicationArticles" PRIMARY KEY ("Id")
);


CREATE TABLE "QuietHoursPolicies" (
    "Id" uuid NOT NULL,
    "TenantId" uuid NOT NULL,
    "IsEnabled" boolean NOT NULL,
    "StartHourLocal" integer NOT NULL,
    "EndHourLocal" integer NOT NULL,
    "AllowBypassForUrgent" boolean NOT NULL,
    CONSTRAINT "PK_QuietHoursPolicies" PRIMARY KEY ("Id")
);


CREATE TABLE "QuoteProposals" (
    "Id" uuid NOT NULL,
    "TenantId" uuid NOT NULL,
    "CompanyId" uuid,
    "DealId" uuid,
    "ContactId" uuid NOT NULL,
    "QuoteNumber" text NOT NULL,
    "Status" integer NOT NULL,
    "Subtotal" numeric NOT NULL,
    "DiscountPercent" numeric NOT NULL,
    "TaxPercent" numeric NOT NULL,
    "TotalAmount" numeric NOT NULL,
    "Currency" text NOT NULL,
    "Notes" text,
    "ExpiresAtUtc" timestamp with time zone NOT NULL,
    "CreatedAtUtc" timestamp with time zone NOT NULL,
    CONSTRAINT "PK_QuoteProposals" PRIMARY KEY ("Id")
);


CREATE TABLE "ReplenishmentSchedules" (
    "Id" uuid NOT NULL,
    "TenantId" uuid NOT NULL,
    "OrderId" uuid NOT NULL,
    "CustomerPhone" text NOT NULL,
    "CustomerName" text NOT NULL,
    "Sku" text NOT NULL,
    "ProductName" text NOT NULL,
    "DeliveredAtUtc" timestamp with time zone NOT NULL,
    "ScheduledReminderUtc" timestamp with time zone NOT NULL,
    "IsTriggered" boolean NOT NULL,
    "TriggeredAtUtc" timestamp with time zone,
    "ReorderedAtUtc" timestamp with time zone,
    "DiscountCode" text,
    CONSTRAINT "PK_ReplenishmentSchedules" PRIMARY KEY ("Id")
);


CREATE TABLE "ReturnMerchandiseAuthorizations" (
    "Id" uuid NOT NULL,
    "TenantId" uuid NOT NULL,
    "RmaNumber" text NOT NULL,
    "OrderId" uuid NOT NULL,
    "OrderNumber" text NOT NULL,
    "CustomerPhone" text NOT NULL,
    "Status" integer NOT NULL,
    "Reason" integer NOT NULL,
    "CustomerNotes" text,
    "PhotoEvidenceUrlsJson" text NOT NULL,
    "ResolutionNotes" text,
    "RequestedAtUtc" timestamp with time zone NOT NULL,
    "ResolvedAtUtc" timestamp with time zone,
    CONSTRAINT "PK_ReturnMerchandiseAuthorizations" PRIMARY KEY ("Id")
);


CREATE TABLE "RolePermissionClaims" (
    "Id" uuid NOT NULL,
    "TenantId" uuid NOT NULL,
    "RoleId" uuid NOT NULL,
    "Resource" text NOT NULL,
    "Action" text NOT NULL,
    "IsAllowed" boolean NOT NULL,
    CONSTRAINT "PK_RolePermissionClaims" PRIMARY KEY ("Id")
);


CREATE TABLE "SalesCadenceLeadEnrollments" (
    "Id" uuid NOT NULL,
    "TenantId" uuid NOT NULL,
    "CadenceId" uuid NOT NULL,
    "LeadName" text NOT NULL,
    "Email" text NOT NULL,
    "PhoneNumber" text NOT NULL,
    "Company" text,
    "CurrentStepNumber" integer NOT NULL,
    "State" integer NOT NULL,
    "NextExecutionAtUtc" timestamp with time zone,
    "LastExecutedAtUtc" timestamp with time zone,
    "EnrolledAtUtc" timestamp with time zone NOT NULL,
    CONSTRAINT "PK_SalesCadenceLeadEnrollments" PRIMARY KEY ("Id")
);


CREATE TABLE "SalesCadenceStepItems" (
    "Id" uuid NOT NULL,
    "CadenceId" uuid NOT NULL,
    "StepNumber" integer NOT NULL,
    "DayOffset" integer NOT NULL,
    "Channel" integer NOT NULL,
    "SubjectTemplate" text,
    "BodyTemplate" text NOT NULL,
    CONSTRAINT "PK_SalesCadenceStepItems" PRIMARY KEY ("Id")
);


CREATE TABLE "SalesCommissionPlans" (
    "Id" uuid NOT NULL,
    "TenantId" uuid NOT NULL,
    "PlanName" text NOT NULL,
    "BaseRatePercent" numeric NOT NULL,
    "QuotaThresholdPercent" numeric NOT NULL,
    "AcceleratedRatePercent" numeric NOT NULL,
    "IsActive" boolean NOT NULL,
    "CreatedAtUtc" timestamp with time zone NOT NULL,
    CONSTRAINT "PK_SalesCommissionPlans" PRIMARY KEY ("Id")
);


CREATE TABLE "SalesCommissionStatements" (
    "Id" uuid NOT NULL,
    "TenantId" uuid NOT NULL,
    "PeriodId" uuid NOT NULL,
    "SalesRepUserId" uuid NOT NULL,
    "SalesRepName" text NOT NULL,
    "DealsClosedCount" integer NOT NULL,
    "TotalDealsValue" numeric NOT NULL,
    "CalculatedCommission" numeric NOT NULL,
    "Status" integer NOT NULL,
    "ApprovedAtUtc" timestamp with time zone,
    "Notes" text NOT NULL,
    "CreatedAtUtc" timestamp with time zone NOT NULL,
    CONSTRAINT "PK_SalesCommissionStatements" PRIMARY KEY ("Id")
);


CREATE TABLE "SalesForecastPeriods" (
    "Id" uuid NOT NULL,
    "TenantId" uuid NOT NULL,
    "Name" text NOT NULL,
    "StartDateUtc" timestamp with time zone NOT NULL,
    "EndDateUtc" timestamp with time zone NOT NULL,
    "TargetQuotaAmount" numeric NOT NULL,
    "ClosedWonRevenue" numeric NOT NULL,
    "WeightedForecastRevenue" numeric NOT NULL,
    "UnweightedPipelineRevenue" numeric NOT NULL,
    "CreatedAtUtc" timestamp with time zone NOT NULL,
    CONSTRAINT "PK_SalesForecastPeriods" PRIMARY KEY ("Id")
);


CREATE TABLE "SalesForecastRepQuotas" (
    "Id" uuid NOT NULL,
    "TenantId" uuid NOT NULL,
    "PeriodId" uuid NOT NULL,
    "SalesRepUserId" uuid NOT NULL,
    "SalesRepName" text NOT NULL,
    "QuotaAmount" numeric NOT NULL,
    "ClosedWonAmount" numeric NOT NULL,
    "CommitAmount" numeric NOT NULL,
    "BestCaseAmount" numeric NOT NULL,
    "PipelineAmount" numeric NOT NULL,
    "UpdatedAtUtc" timestamp with time zone NOT NULL,
    CONSTRAINT "PK_SalesForecastRepQuotas" PRIMARY KEY ("Id")
);


CREATE TABLE "SalesFunnels" (
    "Id" uuid NOT NULL,
    "TenantId" uuid NOT NULL,
    "Name" text NOT NULL,
    "Slug" text NOT NULL,
    "Description" text NOT NULL,
    "IsActive" boolean NOT NULL,
    "TotalVisitors" integer NOT NULL,
    "TotalOrders" integer NOT NULL,
    "TotalRevenue" numeric NOT NULL,
    "Currency" text NOT NULL,
    "CreatedAtUtc" timestamp with time zone NOT NULL,
    CONSTRAINT "PK_SalesFunnels" PRIMARY KEY ("Id")
);


CREATE TABLE "SalesOutreachCadences" (
    "Id" uuid NOT NULL,
    "TenantId" uuid NOT NULL,
    "Name" text NOT NULL,
    "Description" text NOT NULL,
    "TargetAudience" text NOT NULL,
    "IsActive" boolean NOT NULL,
    "StepsCount" integer NOT NULL,
    "CreatedAtUtc" timestamp with time zone NOT NULL,
    CONSTRAINT "PK_SalesOutreachCadences" PRIMARY KEY ("Id")
);


CREATE TABLE "SdrTaskItems" (
    "Id" uuid NOT NULL,
    "TenantId" uuid NOT NULL,
    "EnrollmentId" uuid NOT NULL,
    "CadenceId" uuid NOT NULL,
    "LeadName" text NOT NULL,
    "PhoneNumber" text NOT NULL,
    "Email" text NOT NULL,
    "Company" text,
    "TaskType" text NOT NULL,
    "Instruction" text NOT NULL,
    "DueAtUtc" timestamp with time zone NOT NULL,
    "IsCompleted" boolean NOT NULL,
    "CompletedAtUtc" timestamp with time zone,
    "Outcome" text,
    CONSTRAINT "PK_SdrTaskItems" PRIMARY KEY ("Id")
);


CREATE TABLE "SecurityAuditLogEntries" (
    "Id" uuid NOT NULL,
    "TenantId" uuid NOT NULL,
    "UserId" uuid NOT NULL,
    "UserEmail" text NOT NULL,
    "UserRole" text NOT NULL,
    "Action" text NOT NULL,
    "Resource" text NOT NULL,
    "ResourceId" text NOT NULL,
    "Details" text NOT NULL,
    "IpAddress" text NOT NULL,
    "TimestampUtc" timestamp with time zone NOT NULL,
    CONSTRAINT "PK_SecurityAuditLogEntries" PRIMARY KEY ("Id")
);


CREATE TABLE "SenderAccounts" (
    "Id" uuid NOT NULL,
    "TenantId" uuid NOT NULL,
    "Email" text NOT NULL,
    "DisplayName" text NOT NULL,
    "Provider" integer NOT NULL,
    "DailyQuota" integer NOT NULL,
    "SentTodayCount" integer NOT NULL,
    "WarmupEnabled" boolean NOT NULL,
    "WarmupRampDailyIncrement" integer NOT NULL,
    "HealthScore" integer NOT NULL,
    "IsActive" boolean NOT NULL,
    "Status" integer NOT NULL,
    "QuarantineReason" text,
    "QuarantinedAtUtc" timestamp with time zone,
    "ThawedAtUtc" timestamp with time zone,
    "LastDispatchedAtUtc" timestamp with time zone,
    "TotalDispatched" integer NOT NULL,
    "TotalDelivered" integer NOT NULL,
    "TotalBounced" integer NOT NULL,
    "TotalReplied" integer NOT NULL,
    "TotalOpened" integer NOT NULL,
    "TotalSpamComplaints" integer NOT NULL,
    "HasSpf" boolean NOT NULL,
    "HasDkim" boolean NOT NULL,
    "HasDmarc" boolean NOT NULL,
    "CreatedAtUtc" timestamp with time zone NOT NULL,
    CONSTRAINT "PK_SenderAccounts" PRIMARY KEY ("Id")
);


CREATE TABLE "SeoAuditReports" (
    "Id" uuid NOT NULL,
    "TenantId" uuid NOT NULL,
    "TargetUrl" text NOT NULL,
    "OverallHealthScore" integer NOT NULL,
    "ScannedPagesCount" integer NOT NULL,
    "BrokenLinksCount" integer NOT NULL,
    "MissingMetaCount" integer NOT NULL,
    "MissingH1Count" integer NOT NULL,
    "SlowPagesCount" integer NOT NULL,
    "Status" integer NOT NULL,
    "CreatedAtUtc" timestamp with time zone NOT NULL,
    "CompletedAtUtc" timestamp with time zone,
    CONSTRAINT "PK_SeoAuditReports" PRIMARY KEY ("Id")
);


CREATE TABLE "SeoMultipageGraphReports" (
    "Id" uuid NOT NULL,
    "TenantId" uuid NOT NULL,
    "AuditReportId" uuid,
    "RootDomain" text NOT NULL,
    "TotalPagesDiscovered" integer NOT NULL,
    "OrphanPageCount" integer NOT NULL,
    "OrphanPageUrlsJson" text NOT NULL,
    "DuplicateTitleCount" integer NOT NULL,
    "DuplicateTitleClustersJson" text NOT NULL,
    "RedirectChainCount" integer NOT NULL,
    "RedirectChainsJson" text NOT NULL,
    "EvaluatedAtUtc" timestamp with time zone NOT NULL,
    CONSTRAINT "PK_SeoMultipageGraphReports" PRIMARY KEY ("Id")
);


CREATE TABLE "SerpKeywordTrackings" (
    "Id" uuid NOT NULL,
    "TenantId" uuid NOT NULL,
    "Keyword" text NOT NULL,
    "TargetUrl" text NOT NULL,
    "CountryCode" text NOT NULL,
    "SearchEngine" text NOT NULL,
    "CurrentRank" integer,
    "PreviousRank" integer,
    "BestRank" integer,
    "SearchVolumeMonthly" integer NOT NULL,
    "RankHistoryJson" text NOT NULL,
    "TrackedDateUtc" timestamp with time zone NOT NULL,
    CONSTRAINT "PK_SerpKeywordTrackings" PRIMARY KEY ("Id")
);


CREATE TABLE "SerpRankHistoryRecords" (
    "Id" uuid NOT NULL,
    "TenantId" uuid NOT NULL,
    "KeywordId" uuid,
    "Keyword" text NOT NULL,
    "TargetUrl" text NOT NULL,
    "Position" integer NOT NULL,
    "PreviousPosition" integer NOT NULL,
    "DeltaPosition" integer NOT NULL,
    "DeviceType" text NOT NULL,
    "SearchEngine" text NOT NULL,
    "CountryCode" text NOT NULL,
    "SerpFeaturesJson" text NOT NULL,
    "RecordedDateUtc" timestamp with time zone NOT NULL,
    CONSTRAINT "PK_SerpRankHistoryRecords" PRIMARY KEY ("Id")
);


CREATE TABLE "ServerConversionEvents" (
    "Id" uuid NOT NULL,
    "TenantId" uuid NOT NULL,
    "Platform" integer NOT NULL,
    "EventType" integer NOT NULL,
    "EventId" text NOT NULL,
    "EventTimeUtc" timestamp with time zone NOT NULL,
    "HashedEmail" text,
    "HashedPhone" text,
    "HashedFirstName" text,
    "HashedLastName" text,
    "HashedCity" text,
    "HashedCountry" text,
    "Fbc" text,
    "Fbp" text,
    "ClientUserAgent" text,
    "ClientIpAddress" text,
    "ValueAmount" numeric NOT NULL,
    "Currency" text NOT NULL,
    "CustomDataJson" text NOT NULL,
    "Status" integer NOT NULL,
    "DispatchedAtUtc" timestamp with time zone,
    "ApiResponseJson" text,
    "CreatedAtUtc" timestamp with time zone NOT NULL,
    CONSTRAINT "PK_ServerConversionEvents" PRIMARY KEY ("Id")
);


CREATE TABLE "ShipmentRecords" (
    "Id" uuid NOT NULL,
    "TenantId" uuid NOT NULL,
    "OrderId" uuid NOT NULL,
    "CourierProviderId" uuid NOT NULL,
    "CourierName" text NOT NULL,
    "TrackingNumber" text NOT NULL,
    "AwbBarcode" text NOT NULL,
    "LabelPdfUrl" text NOT NULL,
    "Status" integer NOT NULL,
    "CashToCollect" numeric NOT NULL,
    "CashRemitted" numeric NOT NULL,
    "DispatchedAtUtc" timestamp with time zone NOT NULL,
    "DeliveredAtUtc" timestamp with time zone,
    CONSTRAINT "PK_ShipmentRecords" PRIMARY KEY ("Id")
);


CREATE TABLE "SlaPolicies" (
    "Id" uuid NOT NULL,
    "TenantId" uuid NOT NULL,
    "Name" text NOT NULL,
    "Priority" integer NOT NULL,
    "TargetFirstResponseMinutes" integer NOT NULL,
    "TargetResolutionMinutes" integer NOT NULL,
    "IsActive" boolean NOT NULL,
    "CreatedAtUtc" timestamp with time zone NOT NULL,
    CONSTRAINT "PK_SlaPolicies" PRIMARY KEY ("Id")
);


CREATE TABLE "SocialAccounts" (
    "Id" uuid NOT NULL,
    "TenantId" uuid NOT NULL,
    "Platform" integer NOT NULL,
    "AccountName" text NOT NULL,
    "ExternalAccountId" text NOT NULL,
    "AccessToken" text,
    "RefreshToken" text,
    "TokenExpiresAtUtc" timestamp with time zone,
    "RefreshTokenRequired" boolean NOT NULL,
    "IsActive" boolean NOT NULL,
    "CreatedAtUtc" timestamp with time zone NOT NULL,
    CONSTRAINT "PK_SocialAccounts" PRIMARY KEY ("Id")
);


CREATE TABLE "SocialPosts" (
    "Id" uuid NOT NULL,
    "TenantId" uuid NOT NULL,
    "Content" text NOT NULL,
    "MediaUrls" text[] NOT NULL,
    "ScheduledAtUtc" timestamp with time zone,
    "PublishedAtUtc" timestamp with time zone,
    "Status" integer NOT NULL,
    "TargetPlatforms" integer[] NOT NULL,
    "CreatedAtUtc" timestamp with time zone NOT NULL,
    CONSTRAINT "PK_SocialPosts" PRIMARY KEY ("Id")
);


CREATE TABLE "StockAgingLedgers" (
    "Id" uuid NOT NULL,
    "TenantId" uuid NOT NULL,
    "ProductId" uuid NOT NULL,
    "Sku" text NOT NULL,
    "ProductName" text NOT NULL,
    "WarehouseId" uuid NOT NULL,
    "Quantity" integer NOT NULL,
    "UnitCostPrice" numeric NOT NULL,
    "OriginalRetailPrice" numeric NOT NULL,
    "DaysInWarehouse" integer NOT NULL,
    "Tier" integer NOT NULL,
    "CurrentEffectivePrice" numeric NOT NULL,
    "CurrentMarkdownPercentage" numeric NOT NULL,
    "ReceivedAtUtc" timestamp with time zone NOT NULL,
    "LastEvaluatedUtc" timestamp with time zone NOT NULL,
    CONSTRAINT "PK_StockAgingLedgers" PRIMARY KEY ("Id")
);


CREATE TABLE "StockMovements" (
    "Id" uuid NOT NULL,
    "TenantId" uuid NOT NULL,
    "WarehouseId" uuid NOT NULL,
    "ProductId" uuid NOT NULL,
    "Sku" text NOT NULL,
    "OrderId" uuid,
    "MovementType" integer NOT NULL,
    "QuantityChange" integer NOT NULL,
    "PreviousPhysical" integer NOT NULL,
    "NewPhysical" integer NOT NULL,
    "PerformedBy" text NOT NULL,
    "Notes" text,
    "TimestampUtc" timestamp with time zone NOT NULL,
    CONSTRAINT "PK_StockMovements" PRIMARY KEY ("Id")
);


CREATE TABLE "StockoutProjectionAlerts" (
    "Id" uuid NOT NULL,
    "TenantId" uuid NOT NULL,
    "ProductId" uuid NOT NULL,
    "Sku" text NOT NULL,
    "CurrentPhysicalStock" integer NOT NULL,
    "CurrentReservedStock" integer NOT NULL,
    "ProjectedDailyDemand" numeric NOT NULL,
    "EstimatedDaysOfSupplyRemaining" numeric NOT NULL,
    "ProjectedStockoutDateUtc" timestamp with time zone NOT NULL,
    "RecommendedReorderQuantity" integer NOT NULL,
    "Priority" integer NOT NULL,
    "AlertDismissed" boolean NOT NULL,
    "GeneratedAtUtc" timestamp with time zone NOT NULL,
    CONSTRAINT "PK_StockoutProjectionAlerts" PRIMARY KEY ("Id")
);


CREATE TABLE "StorefrontConnections" (
    "Id" uuid NOT NULL,
    "TenantId" uuid NOT NULL,
    "Platform" integer NOT NULL,
    "StoreDomain" text NOT NULL,
    "WebhookSecret" text NOT NULL,
    "AccessToken" text NOT NULL,
    "IsActive" boolean NOT NULL,
    "CreatedAtUtc" timestamp with time zone NOT NULL,
    CONSTRAINT "PK_StorefrontConnections" PRIMARY KEY ("Id")
);


CREATE TABLE "StorefrontDomains" (
    "Id" uuid NOT NULL,
    "TenantId" uuid NOT NULL,
    "Hostname" text NOT NULL,
    "IsPrimary" boolean NOT NULL,
    "TargetCname" text NOT NULL,
    "ValidationStatus" integer NOT NULL,
    "SslStatus" integer NOT NULL,
    "VerifiedAtUtc" timestamp with time zone,
    "CreatedAtUtc" timestamp with time zone NOT NULL,
    CONSTRAINT "PK_StorefrontDomains" PRIMARY KEY ("Id")
);


CREATE TABLE "StreamingVoiceCallSessions" (
    "Id" uuid NOT NULL,
    "TenantId" uuid NOT NULL,
    "CallSid" text NOT NULL,
    "StreamSid" text NOT NULL,
    "Encoding" integer NOT NULL,
    "SampleRate" integer NOT NULL,
    "CurrentVadStatus" integer NOT NULL,
    "IsAgentSpeaking" boolean NOT NULL,
    "InterruptionCount" integer NOT NULL,
    "TotalFramesReceived" bigint NOT NULL,
    "AverageProcessingLatencyMs" double precision NOT NULL,
    "IsTransferred" boolean NOT NULL,
    "TransferTargetSipUri" text,
    "TransferReason" text,
    "CreatedAtUtc" timestamp with time zone NOT NULL,
    "EndedAtUtc" timestamp with time zone,
    CONSTRAINT "PK_StreamingVoiceCallSessions" PRIMARY KEY ("Id")
);


CREATE TABLE "SupplierScorecards" (
    "Id" uuid NOT NULL,
    "TenantId" uuid NOT NULL,
    "SupplierName" text NOT NULL,
    "ContactEmail" text NOT NULL,
    "ContactPhone" text NOT NULL,
    "Country" text NOT NULL,
    "PromisedLeadTimeDays" integer NOT NULL,
    "ActualAvgLeadTimeDays" numeric NOT NULL,
    "DefectRatePercentage" numeric NOT NULL,
    "QualityScoreOutOf100" numeric NOT NULL,
    "TotalPurchaseOrdersCompleted" integer NOT NULL,
    "IsActive" boolean NOT NULL,
    "LastEvaluatedUtc" timestamp with time zone NOT NULL,
    CONSTRAINT "PK_SupplierScorecards" PRIMARY KEY ("Id")
);


CREATE TABLE "SurveyForms" (
    "Id" uuid NOT NULL,
    "TenantId" uuid NOT NULL,
    "Title" text NOT NULL,
    "Type" integer NOT NULL,
    "QuestionPrompt" text NOT NULL,
    "ScaleMax" integer NOT NULL,
    "IsActive" boolean NOT NULL,
    "CreatedAtUtc" timestamp with time zone NOT NULL,
    CONSTRAINT "PK_SurveyForms" PRIMARY KEY ("Id")
);


CREATE TABLE "SurveyResponses" (
    "Id" uuid NOT NULL,
    "TenantId" uuid NOT NULL,
    "SurveyFormId" uuid NOT NULL,
    "ContactId" uuid NOT NULL,
    "Score" integer NOT NULL,
    "FeedbackText" text,
    "Sentiment" integer NOT NULL,
    "CreatedAtUtc" timestamp with time zone NOT NULL,
    CONSTRAINT "PK_SurveyResponses" PRIMARY KEY ("Id")
);


CREATE TABLE "SystemRoles" (
    "Id" uuid NOT NULL,
    "TenantId" uuid NOT NULL,
    "RoleName" text NOT NULL,
    "Description" text NOT NULL,
    "CanViewRawPii" boolean NOT NULL,
    "CanExportData" boolean NOT NULL,
    "CanOverridePricing" boolean NOT NULL,
    "CanApproveDisbursements" boolean NOT NULL,
    "IsSystemDefault" boolean NOT NULL,
    "CreatedAtUtc" timestamp with time zone NOT NULL,
    CONSTRAINT "PK_SystemRoles" PRIMARY KEY ("Id")
);


CREATE TABLE "TaskComments" (
    "Id" uuid NOT NULL,
    "TaskId" uuid NOT NULL,
    "TenantId" uuid NOT NULL,
    "AuthorName" text NOT NULL,
    "Content" text NOT NULL,
    "CreatedAtUtc" timestamp with time zone NOT NULL,
    CONSTRAINT "PK_TaskComments" PRIMARY KEY ("Id")
);


CREATE TABLE "TaskItems" (
    "Id" uuid NOT NULL,
    "ProjectId" uuid NOT NULL,
    "TenantId" uuid NOT NULL,
    "Title" text NOT NULL,
    "Description" text NOT NULL,
    "Status" text NOT NULL,
    "Priority" text NOT NULL,
    "AssigneeName" text,
    "AssigneeAvatar" text,
    "Position" integer NOT NULL,
    "SubtasksCompleted" integer NOT NULL,
    "SubtasksTotal" integer NOT NULL,
    "DueDateUtc" timestamp with time zone,
    "CreatedAtUtc" timestamp with time zone NOT NULL,
    CONSTRAINT "PK_TaskItems" PRIMARY KEY ("Id")
);


CREATE TABLE "TeamMemberRecords" (
    "Id" uuid NOT NULL,
    "TeamId" uuid NOT NULL,
    "TenantId" uuid NOT NULL,
    "FullName" text NOT NULL,
    "Role" text NOT NULL,
    "HourlyLaborRate" numeric(18,2) NOT NULL,
    "CreatedAtUtc" timestamp with time zone NOT NULL,
    CONSTRAINT "PK_TeamMemberRecords" PRIMARY KEY ("Id")
);


CREATE TABLE "TeamRecords" (
    "Id" uuid NOT NULL,
    "BranchId" uuid NOT NULL,
    "OrganizationId" uuid NOT NULL,
    "TenantId" uuid NOT NULL,
    "Name" text NOT NULL,
    "Department" text NOT NULL,
    "CreatedAtUtc" timestamp with time zone NOT NULL,
    CONSTRAINT "PK_TeamRecords" PRIMARY KEY ("Id")
);


CREATE TABLE automation.telemetry_traces (
    "ExecutionId" text NOT NULL,
    "TenantId" character varying(100) NOT NULL,
    "WorkflowId" character varying(100) NOT NULL,
    "ContactId" character varying(100) NOT NULL,
    "Status" character varying(50) NOT NULL,
    "TotalDurationMs" integer NOT NULL,
    "StepsJson" jsonb NOT NULL,
    "ExecutedAtUtc" timestamp with time zone NOT NULL,
    CONSTRAINT "PK_telemetry_traces" PRIMARY KEY ("ExecutionId")
);


CREATE TABLE "TelemetryEvents" (
    "Id" uuid NOT NULL,
    "TenantId" uuid NOT NULL,
    "SessionId" text NOT NULL,
    "VisitorId" text NOT NULL,
    "EventName" text NOT NULL,
    "Url" text NOT NULL,
    "Referrer" text NOT NULL,
    "DeviceType" text NOT NULL,
    "Browser" text NOT NULL,
    "CountryCode" text NOT NULL,
    "City" text NOT NULL,
    "PropertiesJson" text NOT NULL,
    "TimestampUtc" timestamp with time zone NOT NULL,
    CONSTRAINT "PK_TelemetryEvents" PRIMARY KEY ("Id")
);


CREATE TABLE "TenantApiKeys" (
    "Id" uuid NOT NULL,
    "TenantId" uuid NOT NULL,
    "Name" text NOT NULL,
    "KeyPrefix" text NOT NULL,
    "KeyMasked" text NOT NULL,
    "HashedSecret" text NOT NULL,
    "AllowedScopesJson" text NOT NULL,
    "RateLimitPerMinute" integer NOT NULL,
    "IsRevoked" boolean NOT NULL,
    "LastUsedAtUtc" timestamp with time zone,
    "CreatedAtUtc" timestamp with time zone NOT NULL,
    CONSTRAINT "PK_TenantApiKeys" PRIMARY KEY ("Id")
);


CREATE TABLE "TenantRebillingConfigs" (
    "Id" uuid NOT NULL,
    "AgencyTenantId" uuid NOT NULL,
    "SubAccountTenantId" uuid NOT NULL,
    "SmsMarkupMultiplier" numeric NOT NULL,
    "VoiceMarkupMultiplier" numeric NOT NULL,
    "AiTokenMarkupMultiplier" numeric NOT NULL,
    "AutoRebillThresholdUsd" numeric NOT NULL,
    "AutoRebillAmountUsd" numeric NOT NULL,
    "IsAutoRebillEnabled" boolean NOT NULL,
    "UpdatedAtUtc" timestamp with time zone NOT NULL,
    CONSTRAINT "PK_TenantRebillingConfigs" PRIMARY KEY ("Id")
);


CREATE TABLE "Tenants" (
    "Id" uuid NOT NULL,
    "Name" character varying(200) NOT NULL,
    "Slug" character varying(100) NOT NULL,
    "CreatedAtUtc" timestamp with time zone NOT NULL,
    "IsActive" boolean NOT NULL,
    CONSTRAINT "PK_Tenants" PRIMARY KEY ("Id")
);


CREATE TABLE "TenantSnapshotRecords" (
    "Id" uuid NOT NULL,
    "TenantId" uuid NOT NULL,
    "SnapshotName" text NOT NULL,
    "Category" text NOT NULL,
    "Version" text NOT NULL,
    "BlueprintJson" text NOT NULL,
    "Description" text NOT NULL,
    "CreatedAtUtc" timestamp with time zone NOT NULL,
    CONSTRAINT "PK_TenantSnapshotRecords" PRIMARY KEY ("Id")
);


CREATE TABLE "TenantSubscriptions" (
    "Id" uuid NOT NULL,
    "TenantId" uuid NOT NULL,
    "PlanTier" integer NOT NULL,
    "Status" text NOT NULL,
    "MonthlyPrice" numeric NOT NULL,
    "BillingCycleStartUtc" timestamp with time zone NOT NULL,
    "BillingCycleEndUtc" timestamp with time zone NOT NULL,
    CONSTRAINT "PK_TenantSubscriptions" PRIMARY KEY ("Id")
);


CREATE TABLE "TouchpointExecutions" (
    "Id" uuid NOT NULL,
    "TenantId" uuid NOT NULL,
    "TouchpointId" uuid NOT NULL,
    "ContactId" uuid NOT NULL,
    "Channel" integer NOT NULL,
    "ScheduledAtUtc" timestamp with time zone NOT NULL,
    "ExecutedAtUtc" timestamp with time zone,
    "Status" character varying(50) NOT NULL,
    CONSTRAINT "PK_TouchpointExecutions" PRIMARY KEY ("Id")
);


CREATE TABLE "UgcVideoScripts" (
    "Id" uuid NOT NULL,
    "TenantId" uuid NOT NULL,
    "ProductSku" text NOT NULL,
    "ProductName" text NOT NULL,
    "Language" text NOT NULL,
    "TargetAudience" text NOT NULL,
    "HookAngle" integer NOT NULL,
    "HookText" text NOT NULL,
    "ScenesJson" text NOT NULL,
    "CallToAction" text NOT NULL,
    "EstimatedDurationSeconds" integer NOT NULL,
    "CreatedAtUtc" timestamp with time zone NOT NULL,
    CONSTRAINT "PK_UgcVideoScripts" PRIMARY KEY ("Id")
);


CREATE TABLE "UnifiedCustomerProfiles" (
    "Id" uuid NOT NULL,
    "TenantId" uuid NOT NULL,
    "PrimaryName" text NOT NULL,
    "PrimaryEmail" text,
    "PrimaryPhone" text NOT NULL,
    "Company" text,
    "LifecycleStage" integer NOT NULL,
    "TotalOrdersCount" integer NOT NULL,
    "TotalSpent" numeric NOT NULL,
    "DeliverySuccessRate" double precision NOT NULL,
    "CreatedAtUtc" timestamp with time zone NOT NULL,
    "LastSeenAtUtc" timestamp with time zone NOT NULL,
    CONSTRAINT "PK_UnifiedCustomerProfiles" PRIMARY KEY ("Id")
);


CREATE TABLE "UsageMeterRecords" (
    "Id" uuid NOT NULL,
    "TenantId" uuid NOT NULL,
    "Metric" integer NOT NULL,
    "Quantity" bigint NOT NULL,
    "RecordedAtUtc" timestamp with time zone NOT NULL,
    "Metadata" text,
    CONSTRAINT "PK_UsageMeterRecords" PRIMARY KEY ("Id")
);


CREATE TABLE "Users" (
    "Id" uuid NOT NULL,
    "TenantId" uuid NOT NULL,
    "WorkspaceId" uuid,
    "Email" character varying(256) NOT NULL,
    "FullName" text NOT NULL,
    "Role" integer NOT NULL,
    "AvatarUrl" text,
    "PasswordHash" text NOT NULL,
    "PasswordSalt" text NOT NULL,
    "IsActive" boolean NOT NULL,
    "RefreshToken" text,
    "RefreshTokenExpiresAtUtc" timestamp with time zone,
    "LastLoginAtUtc" timestamp with time zone,
    "CreatedAtUtc" timestamp with time zone NOT NULL,
    CONSTRAINT "PK_Users" PRIMARY KEY ("Id")
);


CREATE TABLE customer_data.visitor_sessions (
    "SessionId" text NOT NULL,
    "TenantId" character varying(100) NOT NULL,
    "AnonymousId" character varying(100) NOT NULL,
    "StartedAtUtc" timestamp with time zone NOT NULL,
    "EndedAtUtc" timestamp with time zone,
    "PageViewsJson" jsonb NOT NULL,
    "UtmParametersJson" jsonb NOT NULL,
    CONSTRAINT "PK_visitor_sessions" PRIMARY KEY ("SessionId")
);


CREATE TABLE "VisitorSessions" (
    "Id" uuid NOT NULL,
    "TenantId" uuid NOT NULL,
    "SessionId" text NOT NULL,
    "VisitorId" text NOT NULL,
    "CountryCode" text NOT NULL,
    "City" text NOT NULL,
    "DeviceType" text NOT NULL,
    "Browser" text NOT NULL,
    "InitialReferrer" text NOT NULL,
    "PagesViewedCount" integer NOT NULL,
    "PageViewsCount" integer NOT NULL,
    "SessionDurationSeconds" integer NOT NULL,
    "IsActive" boolean NOT NULL,
    "StartedAtUtc" timestamp with time zone NOT NULL,
    "LastActiveAtUtc" timestamp with time zone NOT NULL,
    CONSTRAINT "PK_VisitorSessions" PRIMARY KEY ("Id")
);


CREATE TABLE "VoiceCallAmdRecords" (
    "Id" uuid NOT NULL,
    "TenantId" uuid NOT NULL,
    "CallSessionId" uuid,
    "DestinationPhone" text NOT NULL,
    "DetectedClassification" text NOT NULL,
    "AmdConfidence" numeric NOT NULL,
    "DetectionDurationMs" integer NOT NULL,
    "VoicemailDropped" boolean NOT NULL,
    "AudioDropFileUrl" text NOT NULL,
    "EvaluatedAtUtc" timestamp with time zone NOT NULL,
    CONSTRAINT "PK_VoiceCallAmdRecords" PRIMARY KEY ("Id")
);


CREATE TABLE "VoiceCallSessions" (
    "Id" uuid NOT NULL,
    "TenantId" uuid NOT NULL,
    "ContactId" uuid NOT NULL,
    "CallSid" character varying(100) NOT NULL,
    "Direction" integer NOT NULL,
    "FromNumber" character varying(50) NOT NULL,
    "ToNumber" character varying(50) NOT NULL,
    "Status" integer NOT NULL,
    "DurationSeconds" integer NOT NULL,
    "RecordingUrl" text,
    "Transcript" text,
    "CreatedAtUtc" timestamp with time zone NOT NULL,
    CONSTRAINT "PK_VoiceCallSessions" PRIMARY KEY ("Id")
);


CREATE TABLE "VoiceConfirmationCampaigns" (
    "Id" uuid NOT NULL,
    "TenantId" uuid NOT NULL,
    "Name" text NOT NULL,
    "LanguageCode" text NOT NULL,
    "MaxRetryAttempts" integer NOT NULL,
    "RetryDelayMinutes" integer NOT NULL,
    "Status" integer NOT NULL,
    "TotalCallsQueued" integer NOT NULL,
    "TotalCallsSucceeded" integer NOT NULL,
    "TotalCallsFailed" integer NOT NULL,
    "CreatedAtUtc" timestamp with time zone NOT NULL,
    CONSTRAINT "PK_VoiceConfirmationCampaigns" PRIMARY KEY ("Id")
);


CREATE TABLE "VoicemailDropTemplates" (
    "Id" uuid NOT NULL,
    "TenantId" uuid NOT NULL,
    "TemplateName" text NOT NULL,
    "AudioFileUrl" text NOT NULL,
    "DynamicTextPrompt" text NOT NULL,
    "UseTtsSynthesis" boolean NOT NULL,
    "CreatedAtUtc" timestamp with time zone NOT NULL,
    CONSTRAINT "PK_VoicemailDropTemplates" PRIMARY KEY ("Id")
);


CREATE TABLE "WarehouseInventories" (
    "Id" uuid NOT NULL,
    "TenantId" uuid NOT NULL,
    "WarehouseId" uuid NOT NULL,
    "ProductId" uuid NOT NULL,
    "Sku" text NOT NULL,
    "PhysicalStock" integer NOT NULL,
    "ReservedStock" integer NOT NULL,
    "CommittedStock" integer NOT NULL,
    "InTransitStock" integer NOT NULL,
    "QuarantineReturnedStock" integer NOT NULL,
    "DamagedStock" integer NOT NULL,
    "LastUpdatedUtc" timestamp with time zone NOT NULL,
    CONSTRAINT "PK_WarehouseInventories" PRIMARY KEY ("Id")
);


CREATE TABLE "Warehouses" (
    "Id" uuid NOT NULL,
    "TenantId" uuid NOT NULL,
    "Name" text NOT NULL,
    "Code" text NOT NULL,
    "Address" text NOT NULL,
    "IsPrimary" boolean NOT NULL,
    "IsActive" boolean NOT NULL,
    "CreatedAtUtc" timestamp with time zone NOT NULL,
    CONSTRAINT "PK_Warehouses" PRIMARY KEY ("Id")
);


CREATE TABLE "WarehouseStockRecords" (
    "Id" uuid NOT NULL,
    "TenantId" uuid NOT NULL,
    "WarehouseId" uuid NOT NULL,
    "Sku" text NOT NULL,
    "ProductName" text NOT NULL,
    "QuantityAvailable" integer NOT NULL,
    "QuantityReserved" integer NOT NULL,
    "UnitCost" numeric(18,2) NOT NULL,
    "RowVersion" bigint NOT NULL,
    "CreatedAtUtc" timestamp with time zone NOT NULL,
    "UpdatedAtUtc" timestamp with time zone,
    CONSTRAINT "PK_WarehouseStockRecords" PRIMARY KEY ("Id")
);


CREATE TABLE "WarmupExchangeLogs" (
    "Id" uuid NOT NULL,
    "TenantId" uuid NOT NULL,
    "WarmupCadenceId" uuid NOT NULL,
    "PeerInboxEmail" text NOT NULL,
    "Direction" text NOT NULL,
    "OpenedByPeer" boolean NOT NULL,
    "TimestampUtc" timestamp with time zone NOT NULL,
    CONSTRAINT "PK_WarmupExchangeLogs" PRIMARY KEY ("Id")
);


CREATE TABLE "WarmupRecords" (
    "Id" uuid NOT NULL,
    "TenantId" uuid NOT NULL,
    "SenderAccountId" uuid NOT NULL,
    "PeerEmail" text NOT NULL,
    "Direction" integer NOT NULL,
    "Status" integer NOT NULL,
    "OccurredAtUtc" timestamp with time zone NOT NULL,
    CONSTRAINT "PK_WarmupRecords" PRIMARY KEY ("Id")
);


CREATE TABLE "WaterfallEnrichmentCascades" (
    "Id" uuid NOT NULL,
    "TenantId" uuid NOT NULL,
    "CascadeName" text NOT NULL,
    "ProviderChainJson" text NOT NULL,
    "TargetDataType" text NOT NULL,
    "MinConfidenceScore" numeric NOT NULL,
    "IsActive" boolean NOT NULL,
    "CreatedAtUtc" timestamp with time zone NOT NULL,
    CONSTRAINT "PK_WaterfallEnrichmentCascades" PRIMARY KEY ("Id")
);


CREATE TABLE "WebhookDeliveryAttemptLogs" (
    "Id" uuid NOT NULL,
    "TenantId" uuid NOT NULL,
    "SubscriptionId" uuid NOT NULL,
    "EventType" text NOT NULL,
    "PayloadJson" text NOT NULL,
    "ResponseStatusCode" integer NOT NULL,
    "IsSuccess" boolean NOT NULL,
    "ExecutionDurationMs" bigint NOT NULL,
    "AttemptedAtUtc" timestamp with time zone NOT NULL,
    "AttemptNumber" integer NOT NULL,
    "MaxRetries" integer NOT NULL,
    "NextAttemptAtUtc" timestamp with time zone,
    "IsDeadLetter" boolean NOT NULL,
    "DeadLetterReason" text,
    "SignatureHeader" text,
    "TimestampHeader" bigint NOT NULL,
    CONSTRAINT "PK_WebhookDeliveryAttemptLogs" PRIMARY KEY ("Id")
);


CREATE TABLE "WebhookDeliveryLogs" (
    "Id" uuid NOT NULL,
    "SubscriptionId" uuid NOT NULL,
    "EventType" text NOT NULL,
    "Payload" text NOT NULL,
    "ResponseStatusCode" integer,
    "IsSuccess" boolean NOT NULL,
    "ErrorMessage" text,
    "DeliveredAtUtc" timestamp with time zone NOT NULL,
    CONSTRAINT "PK_WebhookDeliveryLogs" PRIMARY KEY ("Id")
);


CREATE TABLE "WebhookSubscriptionRecords" (
    "Id" uuid NOT NULL,
    "TenantId" uuid NOT NULL,
    "TargetUrl" text NOT NULL,
    "SecretKey" text NOT NULL,
    "SubscribedEventsJson" text NOT NULL,
    "IsActive" boolean NOT NULL,
    "FailureCount" integer NOT NULL,
    "CreatedAtUtc" timestamp with time zone NOT NULL,
    CONSTRAINT "PK_WebhookSubscriptionRecords" PRIMARY KEY ("Id")
);


CREATE TABLE "WebhookSubscriptions" (
    "Id" uuid NOT NULL,
    "TenantId" uuid NOT NULL,
    "TargetUrl" text NOT NULL,
    "SecretToken" text NOT NULL,
    "SubscribedEvents" text NOT NULL,
    "IsActive" boolean NOT NULL,
    "CreatedAtUtc" timestamp with time zone NOT NULL,
    CONSTRAINT "PK_WebhookSubscriptions" PRIMARY KEY ("Id")
);


CREATE TABLE "WebResearchRecords" (
    "Id" uuid NOT NULL,
    "TenantId" uuid NOT NULL,
    "Domain" text NOT NULL,
    "CompanyName" text NOT NULL,
    "Engine" text NOT NULL,
    "CorporateSummary" text NOT NULL,
    "ExecutiveBiosJson" text NOT NULL,
    "TechTagsJson" text NOT NULL,
    "SalesHooksJson" text NOT NULL,
    "ConfidenceScore" integer NOT NULL,
    "ResearchedAtUtc" timestamp with time zone NOT NULL,
    CONSTRAINT "PK_WebResearchRecords" PRIMARY KEY ("Id")
);


CREATE TABLE "WhatsAppInstances" (
    "Id" uuid NOT NULL,
    "TenantId" uuid NOT NULL,
    "InstanceName" text NOT NULL,
    "PhoneNumber" text,
    "Status" text NOT NULL,
    "PairingMode" text NOT NULL,
    "PairingCode" text,
    "QrData" text,
    "MemoryFootprintMb" integer NOT NULL,
    "MessagesSent" bigint NOT NULL,
    "MessagesReceived" bigint NOT NULL,
    "ConnectedAtUtc" timestamp with time zone,
    "CreatedAtUtc" timestamp with time zone NOT NULL,
    CONSTRAINT "PK_WhatsAppInstances" PRIMARY KEY ("Id")
);


CREATE TABLE "WhatsAppInteractiveOrderConfirmations" (
    "Id" uuid NOT NULL,
    "TenantId" uuid NOT NULL,
    "OrderId" uuid NOT NULL,
    "OrderNumber" text NOT NULL,
    "CustomerPhone" text NOT NULL,
    "TotalAmount" numeric NOT NULL,
    "Currency" text NOT NULL,
    "MessageId" text NOT NULL,
    "Status" text NOT NULL,
    "SelectedButtonId" text,
    "AddressUpdateNote" text,
    "DispatchedAtUtc" timestamp with time zone NOT NULL,
    "RespondedAtUtc" timestamp with time zone,
    "FallbackToDialerTriggered" boolean NOT NULL,
    "FallbackTriggeredAtUtc" timestamp with time zone,
    CONSTRAINT "PK_WhatsAppInteractiveOrderConfirmations" PRIMARY KEY ("Id")
);


CREATE TABLE "WhatsAppNewsletterChannels" (
    "Id" uuid NOT NULL,
    "TenantId" uuid NOT NULL,
    "ChannelJid" text NOT NULL,
    "Name" text NOT NULL,
    "Description" text NOT NULL,
    "InviteCode" text NOT NULL,
    "SubscriberCount" integer NOT NULL,
    "IsMuted" boolean NOT NULL,
    "IsVerified" boolean NOT NULL,
    "CreatedAtUtc" timestamp with time zone NOT NULL,
    "UpdatedAtUtc" timestamp with time zone,
    CONSTRAINT "PK_WhatsAppNewsletterChannels" PRIMARY KEY ("Id")
);


CREATE TABLE "WhatsAppNewsletterPosts" (
    "Id" uuid NOT NULL,
    "TenantId" uuid NOT NULL,
    "ChannelId" uuid NOT NULL,
    "ServerId" text NOT NULL,
    "Content" text NOT NULL,
    "MediaUrl" text,
    "ReactionCountsJson" text NOT NULL,
    "PublishedAtUtc" timestamp with time zone NOT NULL,
    CONSTRAINT "PK_WhatsAppNewsletterPosts" PRIMARY KEY ("Id")
);


CREATE TABLE "WhatsAppPairingSessions" (
    "Id" uuid NOT NULL,
    "TenantId" uuid NOT NULL,
    "InstanceName" text NOT NULL,
    "PhoneNumber" text NOT NULL,
    "PairingCode" text NOT NULL,
    "Status" text NOT NULL,
    "CreatedAtUtc" timestamp with time zone NOT NULL,
    "ExpiresAtUtc" timestamp with time zone NOT NULL,
    "ConnectedAtUtc" timestamp with time zone,
    "CompanionOs" text NOT NULL,
    "CompanionPlatform" text NOT NULL,
    CONSTRAINT "PK_WhatsAppPairingSessions" PRIMARY KEY ("Id")
);


CREATE TABLE "WhatsAppTemplates" (
    "Id" uuid NOT NULL,
    "TenantId" uuid NOT NULL,
    "Name" text NOT NULL,
    "Category" integer NOT NULL,
    "Language" text NOT NULL,
    "Status" integer NOT NULL,
    "BodyText" text NOT NULL,
    "ButtonsJson" text NOT NULL,
    "CreatedAtUtc" timestamp with time zone NOT NULL,
    CONSTRAINT "PK_WhatsAppTemplates" PRIMARY KEY ("Id")
);


CREATE TABLE "WilayaZones" (
    "Id" uuid NOT NULL,
    "Code" text NOT NULL,
    "NameAr" text NOT NULL,
    "NameFr" text NOT NULL,
    "CountryCode" text NOT NULL,
    "StandardShippingFee" numeric NOT NULL,
    "EstimatedDeliveryDays" integer NOT NULL,
    "DelegationsJson" text NOT NULL,
    "IsActive" boolean NOT NULL,
    CONSTRAINT "PK_WilayaZones" PRIMARY KEY ("Id")
);


CREATE TABLE "WorkflowDeadLetterLogs" (
    "Id" uuid NOT NULL,
    "TenantId" uuid NOT NULL,
    "WorkflowId" uuid NOT NULL,
    "ExecutionId" uuid NOT NULL,
    "FailedNodeId" text NOT NULL,
    "FailedNodeType" text NOT NULL,
    "InputPayload" text NOT NULL,
    "ErrorMessage" text NOT NULL,
    "RetryCount" integer NOT NULL,
    "IsResolved" boolean NOT NULL,
    "FailedAtUtc" timestamp with time zone NOT NULL,
    "ResolvedAtUtc" timestamp with time zone,
    CONSTRAINT "PK_WorkflowDeadLetterLogs" PRIMARY KEY ("Id")
);


CREATE TABLE "WorkflowDefinitionRecords" (
    "Id" uuid NOT NULL,
    "TenantId" uuid NOT NULL,
    "Name" text NOT NULL,
    "Description" text NOT NULL,
    "TriggerType" text NOT NULL,
    "NodesJson" text NOT NULL,
    "EdgesJson" text NOT NULL,
    "IsActive" boolean NOT NULL,
    "CreatedAtUtc" timestamp with time zone NOT NULL,
    "UpdatedAtUtc" timestamp with time zone NOT NULL,
    CONSTRAINT "PK_WorkflowDefinitionRecords" PRIMARY KEY ("Id")
);


CREATE TABLE "WorkflowExecutions" (
    "Id" uuid NOT NULL,
    "TenantId" uuid NOT NULL,
    "WorkspaceId" uuid,
    "WorkflowGraphId" uuid NOT NULL,
    "Status" text NOT NULL,
    "ExecutedNodesCount" integer NOT NULL,
    "ExecutionLog" text NOT NULL,
    "CreatedAtUtc" timestamp with time zone NOT NULL,
    CONSTRAINT "PK_WorkflowExecutions" PRIMARY KEY ("Id")
);


CREATE TABLE "WorkflowGraphs" (
    "Id" uuid NOT NULL,
    "TenantId" uuid NOT NULL,
    "WorkspaceId" uuid,
    "Name" text NOT NULL,
    "TriggerType" text NOT NULL,
    "NodesJson" text NOT NULL,
    "IsActive" boolean NOT NULL,
    "CreatedAtUtc" timestamp with time zone NOT NULL,
    CONSTRAINT "PK_WorkflowGraphs" PRIMARY KEY ("Id")
);


CREATE TABLE automation.workflows (
    "WorkflowId" text NOT NULL,
    "TenantId" character varying(100) NOT NULL,
    "Name" character varying(200) NOT NULL,
    "TriggerNodeId" text NOT NULL,
    "NodesJson" jsonb NOT NULL,
    "EdgesJson" jsonb NOT NULL,
    "CreatedAtUtc" timestamp with time zone NOT NULL,
    CONSTRAINT "PK_workflows" PRIMARY KEY ("WorkflowId")
);


CREATE TABLE "Workspaces" (
    "Id" uuid NOT NULL,
    "TenantId" uuid NOT NULL,
    "Name" character varying(200) NOT NULL,
    "Slug" character varying(100) NOT NULL,
    "Status" text NOT NULL,
    "CreatedAtUtc" timestamp with time zone NOT NULL,
    "IsActive" boolean NOT NULL,
    CONSTRAINT "PK_Workspaces" PRIMARY KEY ("Id")
);


CREATE TABLE "AgentMissionSteps" (
    "Id" uuid NOT NULL,
    "MissionId" uuid NOT NULL,
    "StepIndex" integer NOT NULL,
    "ToolName" text NOT NULL,
    "InputArgumentsJson" text NOT NULL,
    "OutputResultJson" text NOT NULL,
    "ReasoningThought" text NOT NULL,
    "Status" integer NOT NULL,
    "ExecutionDurationMs" double precision NOT NULL,
    "TokensConsumed" integer NOT NULL,
    "CostUsd" numeric NOT NULL,
    "ApprovalRequired" boolean NOT NULL,
    "ApprovedBy" text,
    "ApprovedAtUtc" timestamp with time zone,
    "CreatedAtUtc" timestamp with time zone NOT NULL,
    "ExecutedAtUtc" timestamp with time zone,
    "AgentMissionId" uuid,
    CONSTRAINT "PK_AgentMissionSteps" PRIMARY KEY ("Id"),
    CONSTRAINT "FK_AgentMissionSteps_AgentMissions_AgentMissionId" FOREIGN KEY ("AgentMissionId") REFERENCES "AgentMissions" ("Id")
);


CREATE TABLE "JourneySteps" (
    "Id" uuid NOT NULL,
    "TenantId" uuid NOT NULL,
    "JourneyId" uuid NOT NULL,
    "StepOrder" integer NOT NULL,
    "StepType" integer NOT NULL,
    "SubjectTemplate" text,
    "BodyTemplate" text NOT NULL,
    "DelaySeconds" integer NOT NULL,
    "CreatedAtUtc" timestamp with time zone NOT NULL,
    "CustomerJourneyId" uuid,
    CONSTRAINT "PK_JourneySteps" PRIMARY KEY ("Id"),
    CONSTRAINT "FK_JourneySteps_CustomerJourneys_CustomerJourneyId" FOREIGN KEY ("CustomerJourneyId") REFERENCES "CustomerJourneys" ("Id")
);


CREATE TABLE "EnvelopeSigners" (
    "Id" uuid NOT NULL,
    "TenantId" uuid NOT NULL,
    "EnvelopeId" uuid NOT NULL,
    "Name" text NOT NULL,
    "Email" text NOT NULL,
    "Role" integer NOT NULL,
    "OrderIndex" integer NOT NULL,
    "Status" integer NOT NULL,
    "SignatureData" text NOT NULL,
    "IpAddress" text NOT NULL,
    "VerificationToken" text NOT NULL,
    "SignedAtUtc" timestamp with time zone,
    "CreatedAtUtc" timestamp with time zone NOT NULL,
    "DocumentEnvelopeId" uuid,
    CONSTRAINT "PK_EnvelopeSigners" PRIMARY KEY ("Id"),
    CONSTRAINT "FK_EnvelopeSigners_DocumentEnvelopes_DocumentEnvelopeId" FOREIGN KEY ("DocumentEnvelopeId") REFERENCES "DocumentEnvelopes" ("Id")
);


CREATE TABLE "InvoiceLineItems" (
    "Id" uuid NOT NULL,
    "InvoiceId" uuid NOT NULL,
    "Description" text NOT NULL,
    "Quantity" integer NOT NULL,
    "UnitPrice" numeric NOT NULL,
    "TotalPrice" numeric NOT NULL,
    "InvoiceRecordId" uuid,
    CONSTRAINT "PK_InvoiceLineItems" PRIMARY KEY ("Id"),
    CONSTRAINT "FK_InvoiceLineItems_Invoices_InvoiceRecordId" FOREIGN KEY ("InvoiceRecordId") REFERENCES "Invoices" ("Id")
);


CREATE TABLE "QuoteLineItems" (
    "Id" uuid NOT NULL,
    "QuoteProposalId" uuid NOT NULL,
    "ProductId" uuid NOT NULL,
    "ProductVariantId" uuid,
    "Description" text NOT NULL,
    "Quantity" integer NOT NULL,
    "UnitPrice" numeric NOT NULL,
    CONSTRAINT "PK_QuoteLineItems" PRIMARY KEY ("Id"),
    CONSTRAINT "FK_QuoteLineItems_QuoteProposals_QuoteProposalId" FOREIGN KEY ("QuoteProposalId") REFERENCES "QuoteProposals" ("Id") ON DELETE CASCADE
);


CREATE INDEX "IX_Activities_TenantId_ContactId_CreatedAtUtc" ON "Activities" ("TenantId", "ContactId", "CreatedAtUtc");


CREATE INDEX "IX_AgentHeartbeatRecords_TenantId_ExecutedAtUtc" ON "AgentHeartbeatRecords" ("TenantId", "ExecutedAtUtc");


CREATE INDEX "IX_AgentMissionSteps_AgentMissionId" ON "AgentMissionSteps" ("AgentMissionId");


CREATE UNIQUE INDEX "IX_AgentPersonas_RoleKey" ON "AgentPersonas" ("RoleKey");


CREATE INDEX "IX_AiDrafts_TenantId_ConversationId_Status" ON "AiDrafts" ("TenantId", "ConversationId", "Status");


CREATE INDEX "IX_audience_syncs_TenantId_AudienceId" ON advertising.audience_syncs ("TenantId", "AudienceId");


CREATE INDEX "IX_audience_syncs_TenantId_Platform" ON advertising.audience_syncs ("TenantId", "Platform");


CREATE INDEX "IX_AutonomousFunnelRuns_TenantId_StartedAtUtc" ON "AutonomousFunnelRuns" ("TenantId", "StartedAtUtc");


CREATE INDEX "IX_BranchRecords_TenantId_OrganizationId" ON "BranchRecords" ("TenantId", "OrganizationId");


CREATE INDEX "IX_CadenceTouchpoints_TenantId_CampaignId" ON "CadenceTouchpoints" ("TenantId", "CampaignId");


CREATE INDEX "IX_campaign_roas_snapshots_TenantId_CampaignId" ON advertising.campaign_roas_snapshots ("TenantId", "CampaignId");


CREATE INDEX "IX_campaign_roas_snapshots_TenantId_Platform" ON advertising.campaign_roas_snapshots ("TenantId", "Platform");


CREATE INDEX "IX_ClientPortalFeedbacks_TenantId_ProjectId" ON "ClientPortalFeedbacks" ("TenantId", "ProjectId");


CREATE INDEX "IX_ClientPortalFeedbacks_TenantId_TaskId" ON "ClientPortalFeedbacks" ("TenantId", "TaskId");


CREATE INDEX "IX_ClientPortalTokens_TenantId_ProjectId" ON "ClientPortalTokens" ("TenantId", "ProjectId");


CREATE UNIQUE INDEX "IX_ClientPortalTokens_Token" ON "ClientPortalTokens" ("Token");


CREATE INDEX "IX_Companies_TenantId_Domain" ON "Companies" ("TenantId", "Domain");


CREATE INDEX "IX_Contacts_TenantId_Email" ON "Contacts" ("TenantId", "Email");


CREATE INDEX "IX_Contacts_TenantId_Phone" ON "Contacts" ("TenantId", "Phone");


CREATE INDEX "IX_Conversations_TenantId_ContactId" ON "Conversations" ("TenantId", "ContactId");


CREATE INDEX "IX_Conversations_TenantId_Status_LastMessageAtUtc" ON "Conversations" ("TenantId", "Status", "LastMessageAtUtc");


CREATE UNIQUE INDEX "IX_conversions_TenantId_Platform_EventId" ON advertising.conversions ("TenantId", "Platform", "EventId");


CREATE INDEX "IX_CrawlerSecretRecords_TenantId_Provider" ON "CrawlerSecretRecords" ("TenantId", "Provider");


CREATE INDEX "IX_DealMarginRecords_TenantId_DealId" ON "DealMarginRecords" ("TenantId", "DealId");


CREATE INDEX "IX_DealMarginRecords_TenantId_OrganizationId" ON "DealMarginRecords" ("TenantId", "OrganizationId");


CREATE INDEX "IX_EnvelopeSigners_DocumentEnvelopeId" ON "EnvelopeSigners" ("DocumentEnvelopeId");


CREATE INDEX "IX_identities_TenantId_CanonicalProfileId" ON customer_data.identities ("TenantId", "CanonicalProfileId");


CREATE UNIQUE INDEX "IX_identities_TenantId_Type_Value" ON customer_data.identities ("TenantId", "Type", "Value");


CREATE INDEX "IX_identity_links_TenantId" ON customer_data.identity_links ("TenantId");


CREATE INDEX "IX_Inboxes_TenantId_ChannelType" ON "Inboxes" ("TenantId", "ChannelType");


CREATE INDEX "IX_InvoiceLineItems_InvoiceRecordId" ON "InvoiceLineItems" ("InvoiceRecordId");


CREATE INDEX "IX_JourneySteps_CustomerJourneyId" ON "JourneySteps" ("CustomerJourneyId");


CREATE INDEX "IX_MediaVaultRecords_TenantId_StorageKey" ON "MediaVaultRecords" ("TenantId", "StorageKey");


CREATE INDEX "IX_Messages_TenantId_ConversationId_CreatedAtUtc" ON "Messages" ("TenantId", "ConversationId", "CreatedAtUtc");


CREATE INDEX "IX_Messages_TenantId_ExternalMessageId" ON "Messages" ("TenantId", "ExternalMessageId");


CREATE INDEX "IX_OrganizationRecords_TenantId_Code" ON "OrganizationRecords" ("TenantId", "Code");


CREATE INDEX "IX_OutboxMessages_ProcessedOnUtc_OccurredOnUtc" ON "OutboxMessages" ("ProcessedOnUtc", "OccurredOnUtc");


CREATE INDEX "IX_profiles_TenantId_CanonicalProfileId" ON customer_data.profiles ("TenantId", "CanonicalProfileId");


CREATE INDEX "IX_Projects_TenantId_ContractId" ON "Projects" ("TenantId", "ContractId");


CREATE INDEX "IX_Projects_TenantId_DealId" ON "Projects" ("TenantId", "DealId");


CREATE INDEX "IX_QuoteLineItems_QuoteProposalId" ON "QuoteLineItems" ("QuoteProposalId");


CREATE INDEX "IX_TaskComments_TenantId_TaskId" ON "TaskComments" ("TenantId", "TaskId");


CREATE INDEX "IX_TaskItems_TenantId_ProjectId_Status" ON "TaskItems" ("TenantId", "ProjectId", "Status");


CREATE INDEX "IX_TeamMemberRecords_TenantId_TeamId" ON "TeamMemberRecords" ("TenantId", "TeamId");


CREATE INDEX "IX_TeamRecords_TenantId_BranchId" ON "TeamRecords" ("TenantId", "BranchId");


CREATE INDEX "IX_telemetry_traces_TenantId_Status" ON automation.telemetry_traces ("TenantId", "Status");


CREATE INDEX "IX_telemetry_traces_TenantId_WorkflowId" ON automation.telemetry_traces ("TenantId", "WorkflowId");


CREATE UNIQUE INDEX "IX_Tenants_Slug" ON "Tenants" ("Slug");


CREATE INDEX "IX_TouchpointExecutions_TenantId_Status_ScheduledAtUtc" ON "TouchpointExecutions" ("TenantId", "Status", "ScheduledAtUtc");


CREATE UNIQUE INDEX "IX_Users_TenantId_Email" ON "Users" ("TenantId", "Email");


CREATE INDEX "IX_visitor_sessions_TenantId_AnonymousId" ON customer_data.visitor_sessions ("TenantId", "AnonymousId");


CREATE INDEX "IX_VoiceCallSessions_TenantId_CallSid" ON "VoiceCallSessions" ("TenantId", "CallSid");


CREATE INDEX "IX_VoiceCallSessions_TenantId_ContactId" ON "VoiceCallSessions" ("TenantId", "ContactId");


CREATE INDEX "IX_WarehouseStockRecords_TenantId_WarehouseId_Sku" ON "WarehouseStockRecords" ("TenantId", "WarehouseId", "Sku");


CREATE INDEX "IX_WebResearchRecords_TenantId_Domain" ON "WebResearchRecords" ("TenantId", "Domain");


CREATE UNIQUE INDEX "IX_WhatsAppInstances_TenantId_InstanceName" ON "WhatsAppInstances" ("TenantId", "InstanceName");


CREATE INDEX "IX_WhatsAppInteractiveOrderConfirmations_TenantId_OrderId" ON "WhatsAppInteractiveOrderConfirmations" ("TenantId", "OrderId");


CREATE INDEX "IX_WhatsAppInteractiveOrderConfirmations_TenantId_Status" ON "WhatsAppInteractiveOrderConfirmations" ("TenantId", "Status");


CREATE INDEX "IX_WhatsAppPairingSessions_TenantId_InstanceName" ON "WhatsAppPairingSessions" ("TenantId", "InstanceName");


CREATE INDEX "IX_WhatsAppPairingSessions_TenantId_PairingCode" ON "WhatsAppPairingSessions" ("TenantId", "PairingCode");


CREATE UNIQUE INDEX "IX_workflows_TenantId_WorkflowId" ON automation.workflows ("TenantId", "WorkflowId");


CREATE UNIQUE INDEX "IX_Workspaces_TenantId_Slug" ON "Workspaces" ("TenantId", "Slug");


