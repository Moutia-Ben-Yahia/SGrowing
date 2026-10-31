using System.Text.Json;
using SGrowing.Core.Entities;
using SGrowing.Api.Modules.Commerce;
using SGrowing.Api.Modules.Commerce.Shipping;
using SGrowing.Api.Modules.Gateways;
using SGrowing.Api.Modules.Marketing;
using SGrowing.Api.Modules.Outbound;

namespace SGrowing.Api;

public record IngestEventDto(Guid ContactId, string EventName, string? PayloadJson);
public record SubmitFeedbackDto(Guid SurveyFormId, Guid ContactId, int Score, string? FeedbackText);
public record KbQueryDto(string Question);
public record ResolveContactDto(string? Email, string? Phone, string? FirstName, string? LastName);
public record BookSlotDto(Guid SlotId, Guid ContactId, DateTime ScheduledTimeUtc);
public record CreateCheckoutDto(Guid ContactId, Guid ConversationId, string Title, decimal Amount, string? Currency = "USD");
public record AutonomousAgentQueryDto(Guid ConversationId, Guid ContactId, string Message);
public record VoiceTurnDto(Guid SessionId, string CallerSpeechTranscript);
public record CreateSocialPostDto(string Content, List<SocialPlatform> Platforms, DateTime? ScheduledAtUtc = null, List<string>? MediaUrls = null);
public record DeflectCommentDto(SocialPlatform Platform, string PostExternalId, string CommenterUsername, string CommentText);
public record CreateCompanyDto(string Name, string Domain, string? Industry = null, decimal Revenue = 0, Guid? ParentCompanyId = null);
public record AssociateCompanyDto(Guid ContactId, string ContactEmail);
public record TriggerWorkflowDto(string InputPayloadJson);

public record StreamAudioChunkDto(Guid TenantId, Guid SessionId, string CallSid, string StreamSid, string PayloadBase64, long SequenceNumber);
public record WarmTransferDto(Guid TenantId, Guid SessionId, string TargetSipUri, string Reason, string Summary);
public record IndexDocumentDto(Guid TenantId, Guid DocumentId, string Title, string Content, string? SourceUrl = null);
public record SearchRagDto(Guid TenantId, string Query, int TopK = 3);
public record EnrichLeadDto(Guid TenantId, string DomainOrEmail);
public record CreateContractDto(Guid TenantId, string Title, string ContractType, string SignerName, string SignerEmail, decimal ContractValue, Guid? CompanyId = null, Guid? DealId = null);
public record SignContractDto(string SignerSignatureName, string? SignedPdfUrl = null);

public record CreateProductDto(Guid TenantId, string Name, string Slug, string Description, ProductType ProductType, decimal BasePrice, string? Currency = "USD");
public record CreateVariantDto(Guid TenantId, string Sku, string Name, decimal Price, int InventoryStock = 100, string? AttributesJson = "{}");
public record CreateQuoteDto(Guid TenantId, Guid ContactId, List<CreateQuoteLineItemRequest> Items, Guid? CompanyId = null, Guid? DealId = null, decimal DiscountPercent = 0, decimal TaxPercent = 0, string? Currency = "USD", string? Notes = null);
public record CreateWebhookSubscriptionDto(Guid TenantId, string TargetUrl, string SecretToken, string? SubscribedEvents = "*");
public record PrivacyErasureDto(Guid TenantId, string ContactEmail);

public record RecordTouchpointDto(Guid TenantId, Guid ContactId, string Channel, string CampaignName, TouchpointStage Stage, Guid? DealId = null, decimal Cost = 0, string? UtmSource = null, string? UtmMedium = null, string? UtmCampaign = null);
public record CalculateAttributionDto(Guid TenantId, Guid DealId, AttributionModelType Model = AttributionModelType.WShaped);
public record RecordUsageDto(Guid TenantId, UsageMetricType Metric, long Quantity, string? Metadata = null);
public record CheckEntitlementDto(Guid TenantId, UsageMetricType Metric, long IncomingQuantity);
public record EvaluateQuietHoursDto(Guid TenantId, DateTime RecipientLocalTime, bool IsUrgent = false);
public record InspectPromptDto(string UserPrompt);
public record RouteModelDto(AiTaskComplexity Complexity, string? TaskHint = null);
public record RegisterToolDto(Guid TenantId, string Name, string Description, string SchemaJson, string TargetUrl, string? HttpMethod = "POST");
public record ExecuteToolDto(Guid TenantId, string ToolName, string ArgumentsJson);

// Omnichannel COD, Logistics, WMS, Cadences, CRM 360 & Portal DTOs
public record CreateCodOrderDto(Guid TenantId, string OrderNumber, string CustomerName, string PhoneNumber, string ShippingAddress, string GovernorateOrWilaya, string DelegationOrCommune, decimal TotalAmount, string? CountryCode = "TN", string? CodCurrency = "TND", decimal ShippingFee = 7.0m, string? AlternatePhone = null, string? ItemsJson = "[]", string? Notes = null);
public record AcquireLockDto(Guid TenantId, Guid OrderId, string AgentId, string AgentName, int LeaseMinutes = 3);
public record ReleaseLockDto(Guid TenantId, Guid OrderId, string AgentId);
public record RecordOutcomeDto(Guid TenantId, Guid OrderId, string AgentId, string AgentName, CallOutcome Outcome, string? Notes = null, DateTime? ScheduledCallbackUtc = null);
public record SendWaInteractiveDto(Guid TenantId, Guid OrderId);
public record HandleWaResponseDto(Guid TenantId, Guid OrderId, string Action);

public record DispatchShipmentDto(Guid TenantId, Guid OrderId, Guid CourierProviderId);
public record GenerateManifestDto(Guid TenantId, Guid CourierProviderId, List<Guid> OrderIds);
public record IngestCourierWebhookDto(Guid TenantId, string TrackingNumber, CourierShipmentStatus Status, decimal? CashRemitted = null, string? Notes = null);

public record WmsStockActionDto(Guid TenantId, Guid WarehouseId, string Sku, int Quantity, Guid OrderId, string? PerformedBy = null);
public record WmsQuarantineRestockDto(Guid TenantId, Guid WarehouseId, string Sku, int Quantity, bool IsDamaged, string? InspectorId = null);

public record CreateCadenceDto(Guid TenantId, string Name, string Description, string TargetAudience);
public record AddCadenceStepDto(int StepNumber, int DayOffset, CadenceChannel Channel, string? SubjectTemplate, string BodyTemplate);
public record EnrollCadenceLeadDto(Guid TenantId, Guid CadenceId, string LeadName, string Email, string PhoneNumber, string? Company = null);
public record ProcessDueStepsDto(Guid TenantId);
public record CadenceReplyStopDto(Guid TenantId, string EmailOrPhone, string Channel, string ReplyText);
public record CompleteTaskDto(Guid TenantId, string Outcome);

public record ResolveCustomerDto(Guid TenantId, string Name, string Phone, string? Email = null, CustomerChannelIdentityType? ChannelType = null, string? ChannelValue = null);
public record MergeCustomersDto(Guid TenantId, Guid PrimaryCustomerId, Guid DuplicateCustomerId, string? PerformedBy = null);

public record PublicTrackingDto(string OrderNumber, string Phone);
public record SubmitRmaDto(Guid TenantId, string OrderNumber, string Phone, RmaReason Reason, string? Notes = null, List<string>? PhotoUrls = null);
public record ReviewRmaDto(Guid TenantId, Guid RmaId, bool IsApproved, string ResolutionNotes, bool DispatchReverseCourier = false, Guid? CourierId = null);

// Conversion Mesh, Courier Clearing, In-Chat Commerce & Storefront DTOs
public record TrackConversionDto(Guid TenantId, AdPlatform Platform, ConversionEventType EventType, decimal ValueAmount, string? Currency = "USD", string? Email = null, string? Phone = null, string? FirstName = null, string? LastName = null, string? City = null, string? Country = null, string? Fbc = null, string? Fbp = null, string? UserAgent = null, string? IpAddress = null, string? CustomDataJson = null, string? EventId = null);
public record ReconcileRemittanceDto(Guid TenantId, Guid CourierProviderId, string StatementReference, List<ReportedRemittanceItemDto> Items, string? ReconciledBy = null);
public record ResolveDiscrepancyDto(Guid TenantId, Guid MatchId, string ResolutionNotes);
public record ParseChatCartDto(string MessageText, decimal DefaultUnitPrice = 65.0m, string? Currency = "TND");
public record CreateChatOrderDto(Guid TenantId, Guid ConversationId, InChatCartIntent Intent, Guid? WarehouseId = null);
public record PushFulfillmentDto(Guid TenantId, Guid OrderId, string CourierName, string TrackingNumber);
public record GenerateSheetsRowDto(Guid TenantId, Guid OrderId);

// Autonomous Fulfillment & Operations Expansion DTOs
public record QueueVoiceOrderDto(Guid TenantId, Guid OrderId, Guid? CampaignId = null);
public record ProcessVoiceTurnDto(Guid TenantId, Guid AttemptId, string? SpeechText, string? DtmfDigit);
public record CreatePickSlipDto(Guid TenantId, Guid OrderId, Guid WarehouseId);
public record ScanPickItemDto(Guid TenantId, Guid PickSlipId, string ScannedBarcodeOrSku);
public record CompletePackDto(Guid TenantId, Guid PickSlipId, string TrackingNumber, string CourierName);
public record ScanManifestParcelDto(Guid TenantId, Guid ManifestId, string TrackingNumber);
public record CloseManifestDto(Guid TenantId, Guid ManifestId, string DriverName, string DriverPhone, string DriverSignature);
public record InspectRmaParcelDto(Guid TenantId, string TrackingNumber, string InspectorId, RmaItemCondition Condition, string? Notes, Guid WarehouseId);
public record SyncMetaCatalogDto(Guid TenantId, string CatalogId);
public record WhatsAppInboundOrderWebhookDto(Guid TenantId, string CustomerPhone, string CustomerName, string CatalogId, List<WhatsAppInboundOrderItem> Items, string? ShippingAddress = null, string? ShippingGovernorate = null);
public record RegisterDomainDto(Guid TenantId, string Hostname, bool IsPrimary = true);
public record VerifyDomainDnsDto(Guid TenantId, Guid DomainId, bool SimulateValidDns = true);
public record UpsertBrandTokensDto(Guid TenantId, string StorefrontSlug, string BrandName, string PrimaryColorHex, string SecondaryColorHex, string FontFamily, string LogoUrl, string FaviconUrl, string SupportEmail, string SupportPhone, string SmsSenderId, string WhatsAppSenderPhone);
public record GenerateDepositLinkDto(Guid TenantId, Guid OrderId, string CustomerPhone, decimal DepositAmount = 10.0m, string Provider = "Flouci");
public record SettleDepositDto(Guid TenantId, Guid LinkId);

// Enterprise Operations Expansion DTOs
public record AutoAssignCarrierDto(Guid TenantId, Guid OrderId, decimal PackageWeightKg = 1.0m);
public record SlaBreachFallbackDto(Guid TenantId, string CurrentTrackingNumber);
public record ScheduleRetentionDto(Guid TenantId, Guid OrderId);
public record SubmitRetentionSurveyDto(Guid TenantId, Guid SurveyId, int Rating, string? Notes = null);
public record ProcessReplenishmentsDto(Guid TenantId, DateTime CurrentTimeUtc);
public record CreateTransferDto(Guid TenantId, Guid SourceWarehouseId, Guid DestinationWarehouseId, string Sku, int Quantity);
public record ActionTransferDto(Guid TenantId, Guid TransferId);
public record EvaluateRopDto(Guid TenantId, Guid WarehouseId, string Sku, int DailyDemand = 5, int LeadDays = 7, decimal UnitCost = 25.0m);
public record ProcessLiveCommentDto(Guid TenantId, Guid SessionId, string CommenterUsername, string CommentText, string? CustomerPhone = null, string? Governorate = null);

// Enterprise Scale Expansion DTOs
public record TenantActionDto(Guid TenantId);
public record RequestExchangeDto(Guid TenantId, Guid OrderId, string OriginalSku, string ReplacementSku, string ReplacementDesc, string Reason, decimal CashAdjustment = 0m);
public record DispatchSwapManifestDto(Guid TenantId, Guid ExchangeRequestId, Guid CourierProviderId, string CourierName, decimal SwapDeliveryFee = 9.5m);
public record ConfirmDoorstepSwapDto(Guid TenantId, Guid ManifestId, bool HandoverNewParcel = true, bool ReturnParcelCollected = true);
public record ReturnIntakeInspectDto(Guid TenantId, Guid ExchangeRequestId, SwapInspectionResult Grade, string Notes = "");
public record GenerateRestockOrderDto(Guid TenantId, Guid AlertId, Guid SupplierId, decimal UnitCost);

// Next-Gen Strategic Frontiers DTOs
public record CalculateRoasDto(Guid TenantId, AdPlatformType Platform, string AdSetId, string AdSetName, DateTime Date, decimal AdSpend, List<Guid> AttributedOrderIds);
public record GenerateRunSheetDto(Guid TenantId, Guid DriverId, Guid HubWarehouseId, List<Guid> OrderIds);
public record DeliverStopDto(Guid TenantId, StopDeliveryStatus Status, string PaymentMethod = "CashOnDelivery", string? FailureReason = null, string SignatureData = "", string PhotoUrl = "", double? Lat = null, double? Lon = null);
public record ReconcileCashDto(Guid TenantId, decimal ActualCashTurnedIn, string ReconciledBy, string Notes = "");
public record ExecuteFlowTurnDto(Guid TenantId, Guid FlowId, string CustomerPhone, string Message);
public record IntakeAgingDto(Guid TenantId, Guid ProductId, string Sku, string ProductName, Guid WarehouseId, int Quantity, decimal CostPrice, decimal RetailPrice, int DaysInWarehouse);
public record GenerateBundleDto(Guid TenantId, string WinningSku, string AgingSku, string BundleName, decimal BundleDiscountPct = 30m);

// Horizon 1-4 Strategic DTOs
public record StartDriverSessionDto(Guid TenantId, Guid DriverId, string DriverName, string? DeviceModel);
public record SubmitPodDto(Guid TenantId, Guid StopId, Guid OrderId, string OrderNumber, Guid DriverId, string SignatureBase64, string PhotoUrl, decimal CashCollected, string RecipientName, string? RecipientRelation, double? Lat, double? Lng, string? PaymentMethod);
public record DriverGpsHeartbeatDto(Guid TenantId, Guid DriverId, double Latitude, double Longitude, int BatteryPercent);
public record CreateCheckoutSessionDto(Guid TenantId, string CartItemsJson, decimal Subtotal);
public record UpdateCheckoutDetailsDto(Guid TenantId, string SessionKey, string CustomerName, string CustomerPhone, string WilayaCode, string DelegationName, string StreetAddress);
public record SendOtpDto(Guid TenantId, string SessionKey);
public record VerifyOtpDto(Guid TenantId, string SessionKey, string OtpInput);
public record ApplyUpsellDto(Guid TenantId, string SessionKey, string UpsellSku, decimal UpsellAmount);
public record CompleteCheckoutDto(Guid TenantId, string SessionKey);
public record GenerateUgcScriptsDto(Guid TenantId, string Sku, string ProductName, string? Language, string? TargetAudience);
public record IngestCompetitorAdDto(Guid TenantId, string CompetitorName, string Platform, string HookSnippet, string OfferDetails, decimal EstimatedDailySpend, string? ThreatLevel);
public record TrackCompetitorPriceDto(Guid TenantId, string Sku, string CompetitorName, string Url, decimal CompetitorPrice, decimal OurPrice);
public record EstablishCreditDto(Guid TenantId, Guid CompanyId, string CompanyName, decimal CreditLimit, int PaymentTermsDays, string? CreditScoreRating);
public record InvoiceDrawdownDto(Guid TenantId, Guid CompanyId, decimal InvoiceAmount);
public record PaymentSettlementDto(Guid TenantId, Guid CompanyId, decimal PaymentAmount);
public record RequestFactoringDto(Guid TenantId, string CourierId, string CourierName, decimal InTransitFloat, decimal DeliveredFloat, decimal DiscountRate = 2.5m);
public record CreateApiKeyDto(Guid TenantId, string Name, List<string> Scopes, bool IsLive = true, int RateLimit = 120);
public record ValidateKeyDto(string SecretKey);
public record RegisterWebhookDto(Guid TenantId, string TargetUrl, List<string> Events);
public record DispatchWebhookDto(Guid TenantId, string EventType, object Payload);

// Track 1-3 Extension DTOs
public record SendWhatsAppMessageDto(string InstanceName, string RecipientPhone, string Message);
public record GenerateCourierLabelDto(string CourierProviderName, CourierLabelRequestDto Details);
public record IngestCourierWebhookGatewayDto(string CourierProviderName, CourierTrackingWebhookPayloadDto Payload);
public record SemanticSearchRequestDto(Guid TenantId, string Query, int TopK = 5, double MinScore = 0.35);
public record IndexKnowledgeRequestDto(Guid TenantId);
