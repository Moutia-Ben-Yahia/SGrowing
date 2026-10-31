using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SGrowing.Api.Data;
using SGrowing.Api.Modules.Commerce;
using SGrowing.Api.Modules.Commerce.AntiFraud;
using SGrowing.Api.Modules.Commerce.Connectors;
using SGrowing.Api.Modules.Commerce.Shipping;
using SGrowing.Api.Modules.Commerce.Warehouses;
using SGrowing.Api.Modules.CRM;
using SGrowing.Api.Modules.Integrations;
using SGrowing.Api.Modules.Marketing;
using SGrowing.Api.Modules.Sales;
using SGrowing.Api.Modules.Tenancy;
using SGrowing.Core.Entities;

namespace SGrowing.Api.Modules.Commerce;

public static class CommerceEndpoints
{
    public static IEndpointRouteBuilder MapCommerceEndpoints(this IEndpointRouteBuilder app)
    {
        // Booking Slots & Checkout Links
        app.MapGet("/api/sales/booking/slots", async ([FromQuery] Guid tenantId, SGrowingDbContext db) =>
        {
            var slots = await db.AppointmentSlots.Where(s => s.TenantId == tenantId && s.IsActive).ToListAsync();
            return Results.Ok(slots);
        });

        app.MapPost("/api/sales/booking/slots", async ([FromBody] AppointmentSlot slot, SGrowingDbContext db) =>
        {
            db.AppointmentSlots.Add(slot);
            await db.SaveChangesAsync();
            return Results.Ok(slot);
        });

        app.MapPost("/api/sales/booking/book", async ([FromQuery] Guid tenantId, [FromBody] BookSlotDto req, AppointmentBookingService bookingService) =>
        {
            var res = await bookingService.BookSlotAsync(tenantId, req.SlotId, req.ContactId, req.ScheduledTimeUtc);
            return res.IsSuccess ? Results.Ok(res.Value) : Results.BadRequest(new { error = res.Error });
        });

        app.MapPost("/api/sales/checkout/create", async ([FromQuery] Guid tenantId, [FromBody] CreateCheckoutDto req, CheckoutLinkService checkoutService) =>
        {
            var res = await checkoutService.CreateCheckoutLinkAsync(tenantId, req.ContactId, req.ConversationId, req.Title, req.Amount, req.Currency ?? "USD");
            return res.IsSuccess ? Results.Ok(res.Value) : Results.BadRequest(new { error = res.Error });
        });

        app.MapPost("/api/sales/checkout/{id:guid}/pay", async (Guid id, [FromQuery] Guid tenantId, CheckoutLinkService checkoutService) =>
        {
            var res = await checkoutService.MarkPaidAsync(tenantId, id);
            return res.IsSuccess ? Results.Ok(res.Value) : Results.BadRequest(new { error = res.Error });
        });

        // PIM Catalog & In-Chat CPQ
        app.MapPost("/api/commerce/products", async ([FromBody] CreateProductDto req, ProductCatalogService catalog) =>
        {
            var product = await catalog.CreateProductAsync(req.TenantId, req.Name, req.Slug, req.Description, req.ProductType, req.BasePrice, req.Currency ?? "USD");
            return Results.Ok(product);
        });

        app.MapGet("/api/commerce/products", async ([FromQuery] Guid tenantId, ProductCatalogService catalog) =>
        {
            var products = await catalog.GetProductsAsync(tenantId);
            return Results.Ok(products);
        });

        app.MapPost("/api/commerce/products/{id:guid}/variants", async (Guid id, [FromBody] CreateVariantDto req, ProductCatalogService catalog) =>
        {
            var variant = await catalog.AddVariantAsync(req.TenantId, id, req.Sku, req.Name, req.Price, req.InventoryStock, req.AttributesJson ?? "{}");
            return Results.Ok(variant);
        });

        app.MapPost("/api/commerce/quotes", async ([FromBody] CreateQuoteDto req, InChatCpqService cpq) =>
        {
            var quote = await cpq.CreateQuoteAsync(req.TenantId, req.ContactId, req.Items, req.CompanyId, req.DealId, req.DiscountPercent, req.TaxPercent, req.Currency ?? "USD", req.Notes);
            return Results.Ok(quote);
        });

        app.MapPost("/api/commerce/quotes/{id:guid}/message", async (Guid id, [FromQuery] Guid tenantId, InChatCpqService cpq) =>
        {
            var message = await cpq.GenerateInChatQuoteMessageAsync(tenantId, id);
            return Results.Ok(new { formattedMessage = message });
        });

        app.MapPost("/api/commerce/quotes/{id:guid}/accept", async (Guid id, [FromQuery] Guid tenantId, InChatCpqService cpq) =>
        {
            var quote = await cpq.AcceptQuoteAsync(tenantId, id);
            return Results.Ok(quote);
        });

        // Payment & Store Webhooks
        app.MapPost("/api/webhooks/stripe", async ([FromQuery] Guid tenantId, [FromBody] JsonElement payload, StripePaymentWebhookHandler stripeHandler) =>
        {
            var result = await stripeHandler.ProcessStripeEventAsync(tenantId, payload.ToString());
            return Results.Ok(result);
        });

        app.MapPost("/api/integrations/shopify/webhook", async ([FromQuery] Guid tenantId, [FromHeader(Name = "X-Shopify-Topic")] string? topic, [FromBody] JsonElement payload, ShopifyWebhookReceiver receiver) =>
        {
            var result = await receiver.ProcessWebhookAsync(tenantId, topic ?? "checkouts/create", payload.ToString());
            return Results.Ok(result);
        });

        app.MapPost("/api/integrations/carts/abandoned/scan", async ([FromQuery] Guid tenantId, AbandonedCartRecoveryService recoveryService) =>
        {
            var results = await recoveryService.TriggerRecoveryScanAsync(tenantId);
            return Results.Ok(results);
        });

        // COD Orders & Confirmation
        app.MapPost("/api/cod/orders", async ([FromBody] CreateCodOrderDto req, SGrowingDbContext db, ICodConfirmationService codService) =>
        {
            var order = CodOrder.Create(req.TenantId, req.OrderNumber, req.CustomerName, req.PhoneNumber, req.ShippingAddress, req.GovernorateOrWilaya, req.DelegationOrCommune, req.TotalAmount, req.CountryCode ?? "TN", req.CodCurrency ?? "TND", req.ShippingFee, req.AlternatePhone, req.ItemsJson ?? "[]", req.Notes);
            db.CodOrders.Add(order);
            await db.SaveChangesAsync();
            var risk = await codService.EvaluateRiskAsync(req.TenantId, order);
            return Results.Ok(new { Order = order, Risk = risk });
        });

        app.MapGet("/api/cod/orders", async ([FromQuery] Guid tenantId, SGrowingDbContext db) =>
        {
            var orders = await db.CodOrders.Where(o => o.TenantId == tenantId).OrderByDescending(o => o.CreatedAtUtc).ToListAsync();
            return Results.Ok(orders);
        });

        app.MapPost("/api/cod/orders/lock", async ([FromBody] AcquireLockDto req, ICodConfirmationService codService) =>
        {
            var result = await codService.AcquireLockAsync(req.TenantId, req.OrderId, req.AgentId, req.AgentName, req.LeaseMinutes <= 0 ? 3 : req.LeaseMinutes);
            return Results.Ok(result);
        });

        app.MapPost("/api/cod/orders/release-lock", async ([FromBody] ReleaseLockDto req, ICodConfirmationService codService) =>
        {
            var success = await codService.ReleaseLockAsync(req.TenantId, req.OrderId, req.AgentId);
            return Results.Ok(new { Success = success });
        });

        app.MapPost("/api/cod/orders/call-outcome", async ([FromBody] RecordOutcomeDto req, ICodConfirmationService codService) =>
        {
            var order = await codService.RecordCallOutcomeAsync(req.TenantId, req.OrderId, req.AgentId, req.AgentName, req.Outcome, req.Notes, req.ScheduledCallbackUtc);
            return Results.Ok(order);
        });

        app.MapPost("/api/cod/orders/interactive-wa", async ([FromBody] SendWaInteractiveDto req, ICodConfirmationService codService) =>
        {
            var payload = await codService.GenerateInteractiveWhatsAppPayloadAsync(req.TenantId, req.OrderId);
            return Results.Ok(new { Payload = payload });
        });

        app.MapPost("/api/cod/orders/interactive-response", async ([FromBody] HandleWaResponseDto req, ICodConfirmationService codService) =>
        {
            var order = await codService.HandleCustomerInteractiveResponseAsync(req.TenantId, req.OrderId, req.Action);
            return Results.Ok(order);
        });

        // Logistics & Manifests
        app.MapPost("/api/logistics/dispatch", async ([FromBody] DispatchShipmentDto req, ICourierLogisticsHubService logisticsHub) =>
        {
            var shipment = await logisticsHub.DispatchOrderAsync(req.TenantId, req.OrderId, req.CourierProviderId);
            return Results.Ok(shipment);
        });

        app.MapPost("/api/logistics/manifests", async ([FromBody] GenerateManifestDto req, ICourierLogisticsHubService logisticsHub) =>
        {
            var manifest = await logisticsHub.GenerateBordereauManifestAsync(req.TenantId, req.CourierProviderId, req.OrderIds);
            return Results.Ok(manifest);
        });

        app.MapPost("/api/logistics/webhooks/tracking", async ([FromBody] IngestCourierWebhookDto req, ICourierLogisticsHubService logisticsHub) =>
        {
            var shipment = await logisticsHub.ProcessTrackingWebhookAsync(req.TenantId, req.TrackingNumber, req.Status, req.CashRemitted, req.Notes);
            return Results.Ok(shipment);
        });

        // Defensive WMS
        app.MapGet("/api/wms/inventory", async ([FromQuery] Guid tenantId, [FromQuery] Guid warehouseId, [FromQuery] string sku, IDefensiveWmsService wmsService) =>
        {
            var summary = await wmsService.GetStockSummaryAsync(tenantId, warehouseId, sku);
            return Results.Ok(summary);
        });

        app.MapPost("/api/wms/reserve", async ([FromBody] WmsStockActionDto req, IDefensiveWmsService wmsService) =>
        {
            var inv = await wmsService.ReserveStockOnCheckoutAsync(req.TenantId, req.WarehouseId, req.Sku, req.Quantity, req.OrderId, req.PerformedBy ?? "Checkout");
            return Results.Ok(inv);
        });

        app.MapPost("/api/wms/commit", async ([FromBody] WmsStockActionDto req, IDefensiveWmsService wmsService) =>
        {
            var inv = await wmsService.CommitStockOnConfirmationAsync(req.TenantId, req.WarehouseId, req.Sku, req.Quantity, req.OrderId, req.PerformedBy ?? "ConfirmationAgent");
            return Results.Ok(inv);
        });

        app.MapPost("/api/wms/dispatch", async ([FromBody] WmsStockActionDto req, IDefensiveWmsService wmsService) =>
        {
            var inv = await wmsService.DispatchStockAsync(req.TenantId, req.WarehouseId, req.Sku, req.Quantity, req.OrderId, req.PerformedBy ?? "LogisticsCourier");
            return Results.Ok(inv);
        });

        app.MapPost("/api/wms/quarantine/restock", async ([FromBody] WmsQuarantineRestockDto req, IDefensiveWmsService wmsService) =>
        {
            var inv = await wmsService.ScanAndRestockQuarantineItemAsync(req.TenantId, req.WarehouseId, req.Sku, req.Quantity, req.IsDamaged, req.InspectorId ?? "WarehouseInspector");
            return Results.Ok(inv);
        });

        // Portal & RMA
        app.MapPost("/api/portal/tracking", async ([FromBody] PublicTrackingDto req, ICustomerSelfServicePortalService portalService) =>
        {
            var tracking = await portalService.LookupTrackingAsync(req.OrderNumber, req.Phone);
            return Results.Ok(tracking);
        });

        app.MapPost("/api/portal/rma/submit", async ([FromBody] SubmitRmaDto req, ICustomerSelfServicePortalService portalService) =>
        {
            var rma = await portalService.SubmitRmaRequestAsync(req.TenantId, req.OrderNumber, req.Phone, req.Reason, req.Notes, req.PhotoUrls);
            return Results.Ok(rma);
        });

        app.MapPost("/api/portal/rma/review", async ([FromBody] ReviewRmaDto req, ICustomerSelfServicePortalService portalService) =>
        {
            var rma = await portalService.ReviewRmaRequestAsync(req.TenantId, req.RmaId, req.IsApproved, req.ResolutionNotes, req.DispatchReverseCourier, req.CourierId);
            return Results.Ok(rma);
        });

        // Server-Side Conversions & Remittance Clearing
        app.MapPost("/api/tracking/conversions", async ([FromBody] TrackConversionDto req, IServerConversionMeshService mesh) =>
        {
            var ev = await mesh.TrackConversionAsync(req.TenantId, req.Platform, req.EventType, req.ValueAmount, req.Currency ?? "USD", req.Email, req.Phone, req.FirstName, req.LastName, req.City, req.Country, req.Fbc, req.Fbp, req.UserAgent, req.IpAddress, req.CustomDataJson, req.EventId);
            var emq = mesh.CalculateEventMatchQualityScore(ev);
            return Results.Ok(new { Event = ev, MatchQualityScore = emq });
        });

        app.MapGet("/api/tracking/conversions", async ([FromQuery] Guid tenantId, [FromQuery] int limit, IServerConversionMeshService mesh) =>
        {
            var events = await mesh.GetRecentConversionsAsync(tenantId, limit <= 0 ? 50 : limit);
            return Results.Ok(events);
        });

        app.MapPost("/api/remittance/reconcile", async ([FromBody] ReconcileRemittanceDto req, ICourierRemittanceClearingService clearingService) =>
        {
            var result = await clearingService.ReconcileStatementAsync(req.TenantId, req.CourierProviderId, req.StatementReference, req.Items, req.ReconciledBy ?? "FinanceManager");
            return Results.Ok(result);
        });

        app.MapPost("/api/remittance/discrepancies/resolve", async ([FromBody] ResolveDiscrepancyDto req, ICourierRemittanceClearingService clearingService) =>
        {
            var match = await clearingService.ResolveDiscrepancyAsync(req.TenantId, req.MatchId, req.ResolutionNotes);
            return Results.Ok(match);
        });

        app.MapGet("/api/remittance/profitability", async ([FromQuery] Guid tenantId, [FromQuery] DateTime startDate, [FromQuery] DateTime endDate, [FromQuery] decimal cogsPercent, ICourierRemittanceClearingService clearingService) =>
        {
            var summary = await clearingService.CalculateNetProfitabilityAsync(tenantId, startDate, endDate, cogsPercent <= 0 ? 35.0m : cogsPercent);
            return Results.Ok(summary);
        });

        // In-Chat Commerce & Cart Builder
        app.MapPost("/api/chat-commerce/parse", ([FromBody] ParseChatCartDto req, IConversationalCartEngine cartEngine) =>
        {
            var intent = cartEngine.ParseCartIntent(req.MessageText, req.DefaultUnitPrice <= 0 ? 65.0m : req.DefaultUnitPrice, req.Currency ?? "TND");
            return Results.Ok(intent);
        });

        app.MapPost("/api/chat-commerce/orders", async ([FromBody] CreateChatOrderDto req, IConversationalCartEngine cartEngine) =>
        {
            var result = await cartEngine.CreateInChatOrderAsync(req.TenantId, req.ConversationId, req.Intent, req.WarehouseId);
            return Results.Ok(result);
        });

        // External Storefront & Sheets
        app.MapPost("/api/storefront/shopify/webhook", async ([FromQuery] Guid tenantId, [FromBody] JsonElement payload, IExternalStoreAndSheetsService storeService) =>
        {
            var order = await storeService.IngestExternalStoreOrderAsync(tenantId, ExternalStorePlatform.Shopify, payload.GetRawText());
            return Results.Ok(order);
        });

        app.MapPost("/api/storefront/shopify/fulfill", async ([FromBody] PushFulfillmentDto req, IExternalStoreAndSheetsService storeService) =>
        {
            var record = await storeService.PushFulfillmentUpdateAsync(req.TenantId, req.OrderId, req.CourierName, req.TrackingNumber);
            return Results.Ok(record);
        });

        app.MapPost("/api/storefront/sheets/row", async ([FromBody] GenerateSheetsRowDto req, IExternalStoreAndSheetsService storeService) =>
        {
            var row = await storeService.GenerateGoogleSheetsRowAsync(req.TenantId, req.OrderId);
            return Results.Ok(new { Row = row });
        });

        // Barcode Fulfillment Desk
        app.MapPost("/api/fulfillment/pickslips", async ([FromBody] CreatePickSlipDto req, IBarcodeFulfillmentService fulfillmentService) =>
        {
            var slip = await fulfillmentService.CreatePickSlipAsync(req.TenantId, req.OrderId, req.WarehouseId);
            return Results.Ok(slip);
        });

        app.MapPost("/api/fulfillment/pickslips/scan", async ([FromBody] ScanPickItemDto req, IBarcodeFulfillmentService fulfillmentService) =>
        {
            var result = await fulfillmentService.ScanItemToPickSlipAsync(req.TenantId, req.PickSlipId, req.ScannedBarcodeOrSku);
            return Results.Ok(result);
        });

        app.MapPost("/api/fulfillment/pickslips/complete-pack", async ([FromBody] CompletePackDto req, IBarcodeFulfillmentService fulfillmentService) =>
        {
            var slip = await fulfillmentService.CompletePackAndAssignAwbAsync(req.TenantId, req.PickSlipId, req.TrackingNumber, req.CourierName);
            return Results.Ok(slip);
        });

        app.MapPost("/api/fulfillment/manifests/scan", async ([FromBody] ScanManifestParcelDto req, IBarcodeFulfillmentService fulfillmentService) =>
        {
            var manifest = await fulfillmentService.ScanParcelToManifestAsync(req.TenantId, req.ManifestId, req.TrackingNumber);
            return Results.Ok(manifest);
        });

        app.MapPost("/api/fulfillment/manifests/close-sign", async ([FromBody] CloseManifestDto req, IBarcodeFulfillmentService fulfillmentService) =>
        {
            var manifest = await fulfillmentService.CloseAndSignManifestAsync(req.TenantId, req.ManifestId, req.DriverName, req.DriverPhone, req.DriverSignature);
            return Results.Ok(manifest);
        });

        app.MapPost("/api/fulfillment/rma/inspect", async ([FromBody] InspectRmaParcelDto req, IBarcodeFulfillmentService fulfillmentService) =>
        {
            var rma = await fulfillmentService.InspectAndRestockRmaParcelAsync(req.TenantId, req.TrackingNumber, req.InspectorId, req.Condition, req.Notes, req.WarehouseId);
            return Results.Ok(rma);
        });

        // Storefront White-Labeling & Custom Domains
        app.MapPost("/api/storefronts/domains/register", async ([FromBody] RegisterDomainDto req, IStorefrontDomainService domainService) =>
        {
            var domain = await domainService.RegisterCustomDomainAsync(req.TenantId, req.Hostname, req.IsPrimary);
            return Results.Ok(domain);
        });

        app.MapPost("/api/storefronts/domains/verify-dns", async ([FromBody] VerifyDomainDnsDto req, IStorefrontDomainService domainService) =>
        {
            var domain = await domainService.VerifyDomainDnsAsync(req.TenantId, req.DomainId, req.SimulateValidDns);
            return Results.Ok(domain);
        });

        app.MapPost("/api/storefronts/branding/tokens", async ([FromBody] UpsertBrandTokensDto req, IStorefrontDomainService domainService) =>
        {
            var tokens = new BrandThemeTokens
            {
                StorefrontSlug = req.StorefrontSlug,
                BrandName = req.BrandName,
                PrimaryColorHex = req.PrimaryColorHex,
                SecondaryColorHex = req.SecondaryColorHex,
                FontFamily = req.FontFamily,
                LogoUrl = req.LogoUrl,
                FaviconUrl = req.FaviconUrl,
                SupportEmail = req.SupportEmail,
                SupportPhone = req.SupportPhone,
                SmsSenderId = req.SmsSenderId,
                WhatsAppSenderPhone = req.WhatsAppSenderPhone
            };
            var result = await domainService.UpsertBrandThemeTokensAsync(req.TenantId, tokens);
            return Results.Ok(result);
        });

        app.MapGet("/api/v1/public/storefront/{slugOrDomain}", async (string slugOrDomain, IStorefrontDomainService domainService) =>
        {
            var config = await domainService.GetPublicStorefrontConfigAsync(slugOrDomain);
            return config != null ? Results.Ok(config) : Results.NotFound();
        });

        // Customer Reputation & Micro-Deposit Gateway
        app.MapGet("/api/commerce/reputation/evaluate", async ([FromQuery] Guid tenantId, [FromQuery] string phone, [FromQuery] string? email, ICustomerReputationService repService) =>
        {
            var result = await repService.EvaluateCustomerReputationAsync(tenantId, phone, email);
            return Results.Ok(result);
        });

        app.MapPost("/api/commerce/reputation/deposit-link", async ([FromBody] GenerateDepositLinkDto req, ICustomerReputationService repService) =>
        {
            var link = await repService.GenerateMicroDepositLinkAsync(req.TenantId, req.OrderId, req.CustomerPhone, req.DepositAmount, req.Provider);
            return Results.Ok(link);
        });

        app.MapPost("/api/commerce/reputation/settle-deposit", async ([FromBody] SettleDepositDto req, ICustomerReputationService repService) =>
        {
            var order = await repService.RecordMicroDepositSettlementAsync(req.TenantId, req.LinkId);
            return Results.Ok(order);
        });

        // Smart Courier Routing
        app.MapGet("/api/commerce/routing/rank", async ([FromQuery] Guid tenantId, [FromQuery] string governorate, [FromQuery] decimal weight, [FromQuery] decimal codAmount, ISmartCourierRoutingService routingService) =>
        {
            var recommendations = await routingService.RankCarriersForOrderAsync(tenantId, governorate, weight, codAmount);
            return Results.Ok(recommendations);
        });

        app.MapPost("/api/commerce/routing/auto-assign", async ([FromBody] AutoAssignCarrierDto req, ISmartCourierRoutingService routingService) =>
        {
            var optimal = await routingService.AutoAssignOptimalCarrierAsync(req.TenantId, req.OrderId, req.PackageWeightKg);
            return Results.Ok(optimal);
        });

        app.MapPost("/api/commerce/routing/sla-breach-fallback", async ([FromBody] SlaBreachFallbackDto req, ISmartCourierRoutingService routingService) =>
        {
            var order = await routingService.HandlePickupSlaBreachFallbackAsync(req.TenantId, req.CurrentTrackingNumber);
            return Results.Ok(order);
        });

        // Multi-Warehouse Proximity & Inter-Hub Transfers
        app.MapGet("/api/warehouses/proximity-allocate", async ([FromQuery] Guid tenantId, [FromQuery] string governorate, [FromQuery] string sku, [FromQuery] int quantity, IMultiWarehouseService whService) =>
        {
            var allocation = await whService.AllocateOrderToOptimalWarehouseAsync(tenantId, governorate, sku, quantity);
            return Results.Ok(allocation);
        });

        app.MapPost("/api/warehouses/transfers", async ([FromBody] CreateTransferDto req, IMultiWarehouseService whService) =>
        {
            var transfer = await whService.CreateInterHubTransferAsync(req.TenantId, req.SourceWarehouseId, req.DestinationWarehouseId, req.Sku, req.Quantity);
            return Results.Ok(transfer);
        });

        app.MapPost("/api/warehouses/transfers/dispatch", async ([FromBody] ActionTransferDto req, IMultiWarehouseService whService) =>
        {
            var transfer = await whService.DispatchInterHubTransferAsync(req.TenantId, req.TransferId);
            return Results.Ok(transfer);
        });

        app.MapPost("/api/warehouses/transfers/receive", async ([FromBody] ActionTransferDto req, IMultiWarehouseService whService) =>
        {
            var transfer = await whService.ReceiveInterHubTransferAsync(req.TenantId, req.TransferId);
            return Results.Ok(transfer);
        });

        app.MapPost("/api/warehouses/evaluate-rop", async ([FromBody] EvaluateRopDto req, IMultiWarehouseService whService) =>
        {
            var po = await whService.EvaluateReorderPointAndGeneratePoAsync(req.TenantId, req.WarehouseId, req.Sku, req.DailyDemand, req.LeadDays, req.UnitCost);
            return Results.Ok(po);
        });

        // Reverse Logistics & Drop-and-Swap
        app.MapGet("/api/commerce/exchanges", async ([FromQuery] Guid tenantId, [FromQuery] ExchangeStatus? status, IReverseExchangeService reverseService) =>
        {
            var list = await reverseService.ListExchangeRequestsAsync(tenantId, status);
            return Results.Ok(list);
        });

        app.MapPost("/api/commerce/exchanges/request", async ([FromBody] RequestExchangeDto req, IReverseExchangeService reverseService) =>
        {
            var result = await reverseService.RequestExchangeAsync(req.TenantId, req.OrderId, req.OriginalSku, req.ReplacementSku, req.ReplacementDesc, req.Reason, req.CashAdjustment);
            return Results.Ok(result);
        });

        app.MapPost("/api/commerce/exchanges/manifest", async ([FromBody] DispatchSwapManifestDto req, IReverseExchangeService reverseService) =>
        {
            var manifest = await reverseService.ApproveAndDispatchSwapManifestAsync(req.TenantId, req.ExchangeRequestId, req.CourierProviderId, req.CourierName, req.SwapDeliveryFee);
            return Results.Ok(manifest);
        });

        app.MapPost("/api/commerce/exchanges/doorstep-confirm", async ([FromBody] ConfirmDoorstepSwapDto req, IReverseExchangeService reverseService) =>
        {
            var result = await reverseService.ConfirmDoorstepSwapAsync(req.TenantId, req.ManifestId, req.HandoverNewParcel, req.ReturnParcelCollected);
            return Results.Ok(result);
        });

        app.MapPost("/api/commerce/exchanges/intake-inspect", async ([FromBody] ReturnIntakeInspectDto req, IReverseExchangeService reverseService) =>
        {
            var inspection = await reverseService.ProcessWarehouseReturnIntakeAsync(req.TenantId, req.ExchangeRequestId, req.Grade, req.Notes);
            return Results.Ok(inspection);
        });

        // Cross-Border Trade & Customs Invoices
        app.MapGet("/api/commerce/cross-border/countries", async ([FromQuery] Guid tenantId, ICrossBorderTradeService tradeService) =>
        {
            var countries = await tradeService.ListSupportedCountriesAsync(tenantId);
            return Results.Ok(countries);
        });

        app.MapPost("/api/commerce/cross-border/countries", async ([FromBody] CountryTradeConfig config, ICrossBorderTradeService tradeService) =>
        {
            var result = await tradeService.ConfigureCountryAsync(config.TenantId, config);
            return Results.Ok(result);
        });

        app.MapGet("/api/commerce/cross-border/invoices", async ([FromQuery] Guid tenantId, ICrossBorderTradeService tradeService) =>
        {
            var invoices = await tradeService.ListCommercialInvoicesAsync(tenantId);
            return Results.Ok(invoices);
        });

        // Demand Forecasting & Restock
        app.MapGet("/api/commerce/forecasting/alerts", async ([FromQuery] Guid tenantId, [FromQuery] RestockPriority? minPriority, IDemandForecastingService forecastService) =>
        {
            var alerts = await forecastService.ListStockoutAlertsAsync(tenantId, minPriority);
            return Results.Ok(alerts);
        });

        app.MapGet("/api/commerce/forecasting/suppliers", async ([FromQuery] Guid tenantId, IDemandForecastingService forecastService) =>
        {
            var suppliers = await forecastService.ListSuppliersAsync(tenantId);
            return Results.Ok(suppliers);
        });

        app.MapGet("/api/commerce/forecasting/restock-orders", async ([FromQuery] Guid tenantId, IDemandForecastingService forecastService) =>
        {
            var orders = await forecastService.ListRestockOrdersAsync(tenantId);
            return Results.Ok(orders);
        });

        app.MapPost("/api/commerce/forecasting/restock-orders", async ([FromBody] GenerateRestockOrderDto req, IDemandForecastingService forecastService) =>
        {
            var order = await forecastService.GenerateAutomatedRestockOrderAsync(req.TenantId, req.AlertId, req.SupplierId, req.UnitCost);
            return Results.Ok(order);
        });

        // In-House Fleet & Driver Management
        app.MapGet("/api/commerce/fleet/drivers", async ([FromQuery] Guid tenantId, IInHouseFleetService fleetService) =>
        {
            var drivers = await fleetService.ListDriversAsync(tenantId);
            return Results.Ok(drivers);
        });

        app.MapPost("/api/commerce/fleet/drivers", async ([FromBody] DeliveryDriver driver, IInHouseFleetService fleetService) =>
        {
            var result = await fleetService.RegisterDriverAsync(driver.TenantId, driver);
            return Results.Ok(result);
        });

        app.MapPost("/api/commerce/fleet/run-sheets", async ([FromBody] GenerateRunSheetDto req, IInHouseFleetService fleetService) =>
        {
            var sheet = await fleetService.GenerateRunSheetAsync(req.TenantId, req.DriverId, req.HubWarehouseId, req.OrderIds);
            return Results.Ok(sheet);
        });

        app.MapPost("/api/commerce/fleet/stops/{stopId}/deliver", async (Guid stopId, [FromBody] DeliverStopDto req, IInHouseFleetService fleetService) =>
        {
            var result = await fleetService.RecordStopDeliveryAsync(req.TenantId, stopId, req.Status, req.PaymentMethod, req.FailureReason, req.SignatureData, req.PhotoUrl, req.Lat, req.Lon);
            return Results.Ok(result);
        });

        app.MapPost("/api/commerce/fleet/run-sheets/{runSheetId}/reconcile", async (Guid runSheetId, [FromBody] ReconcileCashDto req, IInHouseFleetService fleetService) =>
        {
            var recon = await fleetService.ReconcileDriverCashAsync(req.TenantId, runSheetId, req.ActualCashTurnedIn, req.ReconciledBy, req.Notes);
            return Results.Ok(recon);
        });

        // Inventory Liquidation & Markdown
        app.MapGet("/api/commerce/liquidation/aging", async ([FromQuery] Guid tenantId, [FromQuery] StockAgeTier? tier, IInventoryLiquidationService liquidationService) =>
        {
            var list = await liquidationService.ListAgingLedgerAsync(tenantId, tier);
            return Results.Ok(list);
        });

        app.MapPost("/api/commerce/liquidation/intake-aging", async ([FromBody] IntakeAgingDto req, IInventoryLiquidationService liquidationService) =>
        {
            var ledger = await liquidationService.RecordStockIntakeAgingAsync(req.TenantId, req.ProductId, req.Sku, req.ProductName, req.WarehouseId, req.Quantity, req.CostPrice, req.RetailPrice, req.DaysInWarehouse);
            return Results.Ok(ledger);
        });

        app.MapPost("/api/commerce/liquidation/bundles", async ([FromBody] GenerateBundleDto req, IInventoryLiquidationService liquidationService) =>
        {
            var bundle = await liquidationService.GenerateLiquidationBundleAsync(req.TenantId, req.WinningSku, req.AgingSku, req.BundleName, req.BundleDiscountPct);
            return Results.Ok(bundle);
        });

        app.MapGet("/api/commerce/liquidation/bundles", async ([FromQuery] Guid tenantId, IInventoryLiquidationService liquidationService) =>
        {
            var bundles = await liquidationService.ListActiveBundleOffersAsync(tenantId);
            return Results.Ok(bundles);
        });

        // Courier Companion Mobile PWA
        app.MapPost("/api/commerce/courier/session", async ([FromBody] StartDriverSessionDto req, ICourierCompanionService courierService) =>
        {
            var sess = await courierService.StartDriverSessionAsync(req.TenantId, req.DriverId, req.DriverName, req.DeviceModel ?? "Android PWA");
            return Results.Ok(sess);
        });

        app.MapGet("/api/commerce/courier/run-sheet", async ([FromQuery] Guid tenantId, [FromQuery] Guid driverId, ICourierCompanionService courierService) =>
        {
            var sheet = await courierService.GetActiveRunSheetAsync(tenantId, driverId);
            return Results.Ok(sheet);
        });

        app.MapGet("/api/commerce/courier/run-sheets/{runSheetId}/stops", async (Guid runSheetId, [FromQuery] Guid tenantId, ICourierCompanionService courierService) =>
        {
            var stops = await courierService.GetRunSheetStopsAsync(tenantId, runSheetId);
            return Results.Ok(stops);
        });

        app.MapPost("/api/commerce/courier/pod", async ([FromBody] SubmitPodDto req, ICourierCompanionService courierService) =>
        {
            var pod = await courierService.SubmitDoorstepProofOfDeliveryAsync(
                req.TenantId, req.StopId, req.OrderId, req.OrderNumber, req.DriverId,
                req.SignatureBase64, req.PhotoUrl, req.CashCollected, req.RecipientName,
                req.RecipientRelation ?? "Self", req.Lat ?? 36.8065, req.Lng ?? 10.1815, req.PaymentMethod ?? "CashOnDelivery"
            );
            return Results.Ok(pod);
        });

        app.MapGet("/api/commerce/courier/cash-summary", async ([FromQuery] Guid tenantId, [FromQuery] Guid driverId, ICourierCompanionService courierService) =>
        {
            var summary = await courierService.GetDriverCashSummaryAsync(tenantId, driverId);
            return Results.Ok(summary);
        });

        app.MapPost("/api/commerce/courier/gps-heartbeat", async ([FromBody] DriverGpsHeartbeatDto req, ICourierCompanionService courierService) =>
        {
            var ok = await courierService.PingDriverGpsHeartbeatAsync(req.TenantId, req.DriverId, req.Latitude, req.Longitude, req.BatteryPercent);
            return Results.Ok(new { success = ok });
        });

        // Headless 1-Page COD Checkout
        app.MapPost("/api/commerce/checkout/session", async ([FromBody] CreateCheckoutSessionDto req, ICodCheckoutService checkoutService) =>
        {
            var sess = await checkoutService.CreateSessionAsync(req.TenantId, req.CartItemsJson, req.Subtotal);
            return Results.Ok(sess);
        });

        app.MapGet("/api/commerce/checkout/wilayas", async ([FromQuery] string countryCode, ICodCheckoutService checkoutService) =>
        {
            var wilayas = await checkoutService.GetWilayasAsync(string.IsNullOrWhiteSpace(countryCode) ? "TN" : countryCode);
            return Results.Ok(wilayas);
        });

        app.MapPost("/api/commerce/checkout/details", async ([FromBody] UpdateCheckoutDetailsDto req, ICodCheckoutService checkoutService) =>
        {
            var sess = await checkoutService.UpdateCustomerDetailsAsync(req.TenantId, req.SessionKey, req.CustomerName, req.CustomerPhone, req.WilayaCode, req.DelegationName, req.StreetAddress);
            return Results.Ok(sess);
        });

        app.MapPost("/api/commerce/checkout/send-otp", async ([FromBody] SendOtpDto req, ICodCheckoutService checkoutService) =>
        {
            var code = await checkoutService.SendPhoneOtpAsync(req.TenantId, req.SessionKey);
            return Results.Ok(new { success = true, simulatedOtp = code });
        });

        app.MapPost("/api/commerce/checkout/verify-otp", async ([FromBody] VerifyOtpDto req, ICodCheckoutService checkoutService) =>
        {
            var ok = await checkoutService.VerifyPhoneOtpAsync(req.TenantId, req.SessionKey, req.OtpInput);
            return Results.Ok(new { verified = ok });
        });

        app.MapPost("/api/commerce/checkout/apply-upsell", async ([FromBody] ApplyUpsellDto req, ICodCheckoutService checkoutService) =>
        {
            var sess = await checkoutService.ApplyUpsellOfferAsync(req.TenantId, req.SessionKey, req.UpsellSku, req.UpsellAmount);
            return Results.Ok(sess);
        });

        app.MapPost("/api/commerce/checkout/complete", async ([FromBody] CompleteCheckoutDto req, ICodCheckoutService checkoutService) =>
        {
            var confirmation = await checkoutService.CompleteCodOrderAsync(req.TenantId, req.SessionKey);
            return Results.Ok(confirmation);
        });

        return app;
    }
}
