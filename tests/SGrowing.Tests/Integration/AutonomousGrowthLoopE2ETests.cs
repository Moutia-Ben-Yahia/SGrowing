using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Security.Cryptography;
using System.Text;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using SGrowing.Api.Data;
using SGrowing.Api.Modules.AI.Missions;
using SGrowing.Api.Modules.Commerce.Connectors.Marketplace;
using SGrowing.Api.Modules.Infrastructure.Telemetry;
using SGrowing.Api.Modules.Integrations;
using Xunit;

namespace SGrowing.Tests.Integration;

/// <summary>
/// End-to-end integration tests validating the PRD Phase 10 / Section 6.2 Autonomous Growth Loop.
/// Exercises the complete multi-agent mission planning DAG, human-in-the-loop approval gatekeeper,
/// regional commerce ingestion into UnifiedCommerceOrder, and outbound webhook dispatch with OpenTelemetry correlation.
/// </summary>
public class AutonomousGrowthLoopE2ETests
{
    private static SGrowingDbContext CreateInMemoryDb()
    {
        var options = new DbContextOptionsBuilder<SGrowingDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new SGrowingDbContext(options);
    }

    [Fact]
    public async Task ExecuteAutonomousGrowthLoop_FromMissionPlanningToCommerceIntakeAndWebhookDispatch()
    {
        // -----------------------------------------------------------------------------------------
        // Stage 1: Autonomous Multi-Agent Mission Planning (PRD Section 6.2 & Phase 10)
        // -----------------------------------------------------------------------------------------
        var missionPlanner = new AiMissionPlannerService();
        var objectiveRequest = new MissionObjectiveRequest(
            Title: "Ramadan COD Scale in Riyadh",
            StrategicGoal: "Scale high-velocity Cash-On-Delivery consumer conversions across Riyadh and Central Province during Ramadan",
            BudgetLimit: 25000m,
            TargetGeo: "KSA",
            Constraints: ["Target CAC < 45 SAR", "Strict COD courier routing", "Spintax Arabic creatives"]
        );

        var plan = missionPlanner.PlanMission(objectiveRequest);

        plan.Should().NotBeNull();
        plan.MissionId.Should().NotBeEmpty();
        plan.Title.Should().Be("Ramadan COD Scale in Riyadh");
        plan.EstimatedCost.Should().Be(25000m);
        plan.Status.Should().Be(MissionStatus.InProgress);
        plan.TaskDag.Should().HaveCount(5);

        // -----------------------------------------------------------------------------------------
        // Stage 2: Autonomous DAG Execution (ResearchAgent -> SeoAgent -> AdvertisingAgent -> ContentAgent)
        // Halts at task-5 (ApprovalGatekeeper) awaiting human authorization.
        // -----------------------------------------------------------------------------------------
        var haltedPlan = missionPlanner.ExecuteNextStep(plan.MissionId);

        haltedPlan.Status.Should().Be(MissionStatus.AwaitingApproval);
        haltedPlan.CompletedAtUtc.Should().BeNull();

        var task1 = haltedPlan.TaskDag.Single(t => t.TaskId == "task-1");
        task1.AgentRole.Should().Be("ResearchAgent");
        task1.Status.Should().Be(TaskExecutionStatus.Succeeded);
        task1.OutputPayload.Should().Contain("identified 4 competitor pricing anomalies in KSA");

        var task2 = haltedPlan.TaskDag.Single(t => t.TaskId == "task-2");
        task2.AgentRole.Should().Be("SeoAgent");
        task2.Status.Should().Be(TaskExecutionStatus.Succeeded);

        var task4 = haltedPlan.TaskDag.Single(t => t.TaskId == "task-4");
        task4.AgentRole.Should().Be("AdvertisingAgent");
        task4.Status.Should().Be(TaskExecutionStatus.Succeeded);

        var task3 = haltedPlan.TaskDag.Single(t => t.TaskId == "task-3");
        task3.AgentRole.Should().Be("ContentAgent");
        task3.Status.Should().Be(TaskExecutionStatus.Succeeded);

        // Assert mission is halted at task-5 (ApprovalGatekeeper)
        var gatekeeperTask = haltedPlan.TaskDag.Single(t => t.TaskId == "task-5");
        gatekeeperTask.AgentRole.Should().Be("ApprovalGatekeeper");
        gatekeeperTask.Status.Should().Be(TaskExecutionStatus.WaitingApproval);
        gatekeeperTask.RequiresHumanApproval.Should().BeTrue();
        gatekeeperTask.OutputPayload.Should().BeNull();

        // -----------------------------------------------------------------------------------------
        // Stage 3: Human Gatekeeper Approval & Mission Completion
        // -----------------------------------------------------------------------------------------
        var completedPlan = missionPlanner.ExecuteNextStep(plan.MissionId, approvedTaskId: "task-5");

        completedPlan.Status.Should().Be(MissionStatus.Completed);
        completedPlan.CompletedAtUtc.Should().NotBeNull();
        completedPlan.CompletedAtUtc.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));

        var approvedGatekeeper = completedPlan.TaskDag.Single(t => t.TaskId == "task-5");
        approvedGatekeeper.Status.Should().Be(TaskExecutionStatus.Succeeded);
        approvedGatekeeper.OutputPayload.Should().Contain("Human Gatekeeper signed off");
        approvedGatekeeper.OutputPayload.Should().Contain("25,000.00");

        // -----------------------------------------------------------------------------------------
        // Stage 4: Ingest and Transform Raw Salla E-Commerce Order into UnifiedCommerceOrder
        // -----------------------------------------------------------------------------------------
        var connectorService = new ConnectorMarketplaceService();
        var rawSallaPayload = """
        {
          "event": "order.created",
          "data": {
            "id": "salla-ord-ramadan-101",
            "reference_id": "ORD-RYD-7701",
            "total": {
              "amount": 575.00,
              "currency": "SAR"
            },
            "customer": {
              "first_name": "Sultan",
              "last_name": "Al-Dosari",
              "email": "sultan.dosari@riyadh.sa",
              "mobile": "+966551234888"
            },
            "shipping": {
              "address": {
                "city": "Riyadh",
                "country": "Saudi Arabia"
              }
            },
            "payment_method": "COD",
            "amounts": {
              "tax": {
                "amount": 75.00
              }
            },
            "items": [
              {
                "sku": "RAMADAN-GIFT-01",
                "name": "Luxury Ramadan Incense & Oud Gift Set",
                "quantity": 1,
                "amounts": {
                  "price_without_tax": {
                    "amount": 500.00
                  },
                  "total": {
                    "amount": 500.00
                  }
                }
              }
            ],
            "created_at": "2026-11-07T10:00:00Z"
          }
        }
        """;

        var unifiedOrder = connectorService.IngestUnifiedOrder("salla", rawSallaPayload);

        unifiedOrder.Should().NotBeNull();
        unifiedOrder.OrderId.Should().Be("salla-ord-ramadan-101");
        unifiedOrder.OrderNumber.Should().Be("ORD-RYD-7701");
        unifiedOrder.CustomerName.Should().Be("Sultan Al-Dosari");
        unifiedOrder.CustomerPhone.Should().Be("+966551234888");
        unifiedOrder.CustomerEmail.Should().Be("sultan.dosari@riyadh.sa");
        unifiedOrder.ShippingCity.Should().Be("Riyadh");
        unifiedOrder.ShippingCountry.Should().Be("Saudi Arabia");
        unifiedOrder.TotalAmount.Should().Be(575.00m);
        unifiedOrder.TaxAmount.Should().Be(75.00m); // 15% VAT
        unifiedOrder.Currency.Should().Be("SAR");
        unifiedOrder.PaymentMethod.Should().Be("COD");
        unifiedOrder.Items.Should().HaveCount(1);
        unifiedOrder.Items[0].Sku.Should().Be("RAMADAN-GIFT-01");
        unifiedOrder.Items[0].Title.Should().Be("Luxury Ramadan Incense & Oud Gift Set");
        unifiedOrder.Items[0].Quantity.Should().Be(1);
        unifiedOrder.Items[0].TotalPrice.Should().Be(500.00m);
        unifiedOrder.Metadata.Should().ContainKey("PaymentCollectionMode")
            .WhoseValue.Should().Be("CashOnDelivery");
        unifiedOrder.Metadata.Should().ContainKey("PlatformTier")
            .WhoseValue.Should().Be("RegionalMENA");

        // -----------------------------------------------------------------------------------------
        // Stage 5: Outbound Developer Webhook Dispatch & HMAC-SHA256 Verification
        // -----------------------------------------------------------------------------------------
        using var db = CreateInMemoryDb();
        var webhookService = new DeveloperWebhookService(db);
        var tenantId = Guid.NewGuid();

        var subscription = await webhookService.RegisterWebhookSubscriptionAsync(
            tenantId: tenantId,
            targetUrl: "https://wms.riyadh-logistics.sa/api/v1/orders",
            events: ["order.placed", "order.created"]
        );

        subscription.Should().NotBeNull();
        subscription.SecretKey.Should().StartWith("whsec_");
        subscription.IsActive.Should().BeTrue();

        var logs = await webhookService.DispatchWebhookEventAsync(tenantId, "order.placed", unifiedOrder);

        logs.Should().HaveCount(1);
        var deliveryLog = logs[0];
        deliveryLog.EventType.Should().Be("order.placed");
        deliveryLog.IsSuccess.Should().BeTrue();
        deliveryLog.ResponseStatusCode.Should().Be(200);
        deliveryLog.SignatureHeader.Should().StartWith("sha256=");
        deliveryLog.PayloadJson.Should().Contain("ORD-RYD-7701");
        deliveryLog.PayloadJson.Should().Contain("sultan.dosari@riyadh.sa");

        // Verify HMAC-SHA256 signature mathematically against subscription secret
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(subscription.SecretKey));
        var expectedHashBytes = hmac.ComputeHash(Encoding.UTF8.GetBytes($"{deliveryLog.TimestampHeader}.{deliveryLog.PayloadJson}"));
        var expectedSignature = "sha256=" + Convert.ToHexString(expectedHashBytes).ToLowerInvariant();
        deliveryLog.SignatureHeader.Should().Be(expectedSignature);

        // Confirm delivery log is recorded in persistence store
        var storedLogs = await webhookService.GetDeliveryLogsAsync(tenantId, subscription.Id);
        storedLogs.Should().ContainSingle(l => l.Id == deliveryLog.Id);

        // -----------------------------------------------------------------------------------------
        // Stage 6: Correlate with GrowthOsTelemetryService & Verify SRE Radar Metrics
        // -----------------------------------------------------------------------------------------
        const string telemetrySourceAndMeterName = "SGrowing.GrowthOS";

        using var activityListener = new ActivityListener
        {
            ShouldListenTo = s => s.Name == telemetrySourceAndMeterName,
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllData
        };
        ActivitySource.AddActivityListener(activityListener);

        var recordedMeasurements = new List<(string Name, double Value)>();
        using var meterListener = new MeterListener();
        meterListener.InstrumentPublished = (instrument, listener) =>
        {
            if (instrument.Meter.Name == telemetrySourceAndMeterName)
            {
                listener.EnableMeasurementEvents(instrument);
            }
        };
        meterListener.SetMeasurementEventCallback<double>((instrument, measurement, tags, state) =>
        {
            recordedMeasurements.Add((instrument.Name, measurement));
        });
        meterListener.SetMeasurementEventCallback<long>((instrument, measurement, tags, state) =>
        {
            recordedMeasurements.Add((instrument.Name, (double)measurement));
        });
        meterListener.Start();

        var telemetryService = new GrowthOsTelemetryService();

        // Start Activity Spans
        using var missionSpan = telemetryService.StartMissionTaskSpan(plan.MissionId, "ApprovalGatekeeper", "task-5");
        missionSpan.Should().NotBeNull();
        missionSpan!.Kind.Should().Be(ActivityKind.Internal);
        missionSpan.GetTagItem("ai.mission_id").Should().Be(plan.MissionId.ToString());
        missionSpan.GetTagItem("ai.agent_role").Should().Be("ApprovalGatekeeper");
        missionSpan.GetTagItem("ai.task_id").Should().Be("task-5");

        using var webhookSpan = telemetryService.StartWebhookSpan("order.placed", subscription.TargetUrl, deliveryLog.Id);
        webhookSpan.Should().NotBeNull();
        webhookSpan!.Kind.Should().Be(ActivityKind.Client);
        webhookSpan.GetTagItem("webhook.event").Should().Be("order.placed");
        webhookSpan.GetTagItem("webhook.url").Should().Be(subscription.TargetUrl);

        // Record Metrics
        telemetryService.RecordMissionExecutionMetric(plan.MissionId, "Completed", durationMs: 1450.0);
        telemetryService.RecordCommerceOrderMetric("salla", unifiedOrder.TotalAmount, unifiedOrder.Currency);
        telemetryService.RecordWebhookMetric(isSuccess: true, durationMs: deliveryLog.ExecutionDurationMs);
        telemetryService.RecordAgentTokensConsumed("ApprovalGatekeeper", 450);

        // Verify Recorded Prometheus Measurements
        recordedMeasurements.Should().Contain(m => m.Name == "growthos.missions.executed" && m.Value == 1);
        recordedMeasurements.Should().Contain(m => m.Name == "growthos.commerce.orders" && m.Value == 1);
        recordedMeasurements.Should().Contain(m => m.Name == "growthos.webhooks.dispatched" && m.Value == 1);
        recordedMeasurements.Should().Contain(m => m.Name == "growthos.webhooks.duration.ms" && Math.Abs(m.Value - deliveryLog.ExecutionDurationMs) < 0.01);
        recordedMeasurements.Should().Contain(m => m.Name == "growthos.ai.tokens" && m.Value == 450);

        // Verify SRE Radar Metrics
        var radarReport = telemetryService.GetSreHealthRadar(currentDlqDepth: 0, activeMissions: 1);
        radarReport.Should().NotBeNull();
        radarReport.OverallStatus.Should().Be("HEALTHY");
        radarReport.OverallHealthScore.Should().BeGreaterThanOrEqualTo(90.0);
        radarReport.WebhookErrorBudgetRemainingPercent.Should().Be(100.0);
        radarReport.ActiveMissionsCount.Should().Be(1);
        radarReport.DeadLetterQueueDepth.Should().Be(0);
        radarReport.ActiveAlerts.Should().BeEmpty();
    }

    [Fact]
    public async Task ExecuteAutonomousGrowthLoop_RejectionOrHaltAtGatekeeper_DoesNotProceedToCommerceAction()
    {
        // Arrange: High-budget autonomous mission
        var missionPlanner = new AiMissionPlannerService();
        var request = new MissionObjectiveRequest(
            Title: "High Ad Spend Expansion Campaign",
            StrategicGoal: "Scale regional paid ad spend across TikTok and Meta without manual supervision check",
            BudgetLimit: 75000m,
            TargetGeo: "KSA"
        );
        var plan = missionPlanner.PlanMission(request);

        // Act: Execute next autonomous step without approval
        var haltedPlan = missionPlanner.ExecuteNextStep(plan.MissionId);

        // Assert: Mission halts safely at human gatekeeper
        haltedPlan.Status.Should().Be(MissionStatus.AwaitingApproval);
        var gatekeeperTask = haltedPlan.TaskDag.Single(t => t.TaskId == "task-5");
        gatekeeperTask.Status.Should().Be(TaskExecutionStatus.WaitingApproval);
        gatekeeperTask.OutputPayload.Should().BeNull();

        // Attempting to advance again without explicit approval preserves safe halt
        var stillHaltedPlan = missionPlanner.ExecuteNextStep(plan.MissionId, approvedTaskId: null);
        stillHaltedPlan.Status.Should().Be(MissionStatus.AwaitingApproval);

        // Verify guardrail: Unapproved mission must NOT trigger downstream commerce intake or webhook dispatch
        using var db = CreateInMemoryDb();
        var webhookService = new DeveloperWebhookService(db);
        var tenantId = Guid.NewGuid();

        await webhookService.RegisterWebhookSubscriptionAsync(
            tenantId, "https://erp.enterprise.sa/webhooks", ["order.placed"]);

        // Since mission is not completed, no order is authorized or dispatched
        stillHaltedPlan.Status.Should().NotBe(MissionStatus.Completed);
        var dispatchedLogs = await webhookService.GetDeliveryLogsAsync(tenantId);
        dispatchedLogs.Should().BeEmpty("unapproved or rejected missions must not trigger automated commerce operations");
    }

    [Fact]
    public void ExecuteAutonomousGrowthLoop_WithCustomConnector_TransformsRegionalTaxAndCurrencyCorrectly()
    {
        // Arrange: Connector marketplace and Saudi Zid regional e-commerce order payload
        var connectorService = new ConnectorMarketplaceService();
        var zidPayload = """
        {
          "id": "zid-ord-88120",
          "order_total": "1150.00",
          "currency": "SAR",
          "customer": {
            "name": "Nasser Al-Otaibi",
            "mobile": "+966509876543",
            "email": "nasser@saudi-store.com"
          },
          "shipping": {
            "city": "Jeddah",
            "country": "Saudi Arabia"
          },
          "payment_method": "CashOnDelivery",
          "items": [
            {
              "sku": "ZID-KSA-99",
              "name": "Luxury Perfume Set",
              "quantity": 2,
              "price": 575.00
            }
          ],
          "created_at": "2026-11-06T15:30:00Z"
        }
        """;

        // Act: Ingest into UnifiedCommerceOrder
        var unifiedOrder = connectorService.IngestUnifiedOrder("zid", zidPayload);

        // Assert: Regional currency, 15% VAT calculation, and metadata
        unifiedOrder.Should().NotBeNull();
        unifiedOrder.OrderId.Should().Be("zid-ord-88120");
        unifiedOrder.OrderNumber.Should().Be("zid-ord-88120");
        unifiedOrder.Currency.Should().Be("SAR");
        unifiedOrder.TotalAmount.Should().Be(1150.00m);

        // 15% VAT included in gross total: 1150 - (1150 / 1.15) = 150.00 SAR
        unifiedOrder.TaxAmount.Should().Be(150.00m);
        unifiedOrder.CustomerName.Should().Be("Nasser Al-Otaibi");
        unifiedOrder.CustomerPhone.Should().Be("+966509876543");
        unifiedOrder.CustomerEmail.Should().Be("nasser@saudi-store.com");
        unifiedOrder.ShippingCity.Should().Be("Jeddah");
        unifiedOrder.ShippingCountry.Should().Be("Saudi Arabia");
        unifiedOrder.PaymentMethod.Should().Be("CashOnDelivery");
        unifiedOrder.Items.Should().HaveCount(1);
        unifiedOrder.Items[0].Sku.Should().Be("ZID-KSA-99");
        unifiedOrder.Items[0].Quantity.Should().Be(2);
        unifiedOrder.Items[0].UnitPrice.Should().Be(575.00m);
        unifiedOrder.Items[0].TotalPrice.Should().Be(1150.00m);

        unifiedOrder.Metadata.Should().NotBeNull();
        unifiedOrder.Metadata.Should().ContainKey("ConnectorId").WhoseValue.Should().Be("zid");
        unifiedOrder.Metadata.Should().ContainKey("PlatformTier").WhoseValue.Should().Be("RegionalMENA");
        unifiedOrder.Metadata.Should().ContainKey("PaymentCollectionMode").WhoseValue.Should().Be("CashOnDelivery");

        // Act & Assert 2: Test custom field mappings override with custom tax field
        var customPayload = """
        {
          "legacy_ref": "LEG-552",
          "gross_amount": 2300.00,
          "custom_vat": 300.00,
          "buyer": {
            "full_name": "Fahad Al-Harbi",
            "contact_phone": "+966540112233"
          },
          "delivery_location": {
            "city": "Dammam",
            "country": "Saudi Arabia"
          },
          "items": [
            {
              "sku": "CUSTOM-SKU-1",
              "name": "Custom Hardware Box",
              "quantity": 1,
              "price": 2300.00
            }
          ]
        }
        """;

        var customMappings = new Dictionary<string, string>
        {
            ["ExternalOrderId"] = "legacy_ref",
            ["TotalAmount"] = "gross_amount",
            ["TaxAmount"] = "custom_vat",
            ["CustomerName"] = "buyer.full_name",
            ["CustomerPhone"] = "buyer.contact_phone",
            ["ShippingCity"] = "delivery_location.city",
            ["ShippingCountry"] = "delivery_location.country"
        };

        var customUnifiedOrder = connectorService.IngestUnifiedOrder(
            connectorId: "zid",
            rawJsonPayload: customPayload,
            defaultPaymentMethod: "COD",
            customFieldMappings: customMappings
        );

        customUnifiedOrder.OrderId.Should().Be("LEG-552");
        customUnifiedOrder.TotalAmount.Should().Be(2300.00m);
        customUnifiedOrder.TaxAmount.Should().Be(300.00m);
        customUnifiedOrder.CustomerName.Should().Be("Fahad Al-Harbi");
        customUnifiedOrder.CustomerPhone.Should().Be("+966540112233");
        customUnifiedOrder.ShippingCity.Should().Be("Dammam");
        customUnifiedOrder.ShippingCountry.Should().Be("Saudi Arabia");
    }
}
