using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace SGrowing.Core.Diagnostics;

public static class SGrowingTelemetry
{
    public const string ActivitySourceName = "SGrowing.Platform";
    public const string MeterName = "SGrowing.Platform.Metrics";

    public static readonly ActivitySource ActivitySource = new(ActivitySourceName, "1.0.0");
    public static readonly Meter Meter = new(MeterName, "1.0.0");

    // Standard Metrics
    public static readonly Counter<long> OrdersPlacedCounter = Meter.CreateCounter<long>(
        "sgrowing_orders_placed_total",
        "orders",
        "Total number of checkout orders placed");

    public static readonly Counter<long> DeliveriesCompletedCounter = Meter.CreateCounter<long>(
        "sgrowing_deliveries_completed_total",
        "deliveries",
        "Total number of physical doorstep deliveries successfully completed");

    public static readonly Counter<long> DlqRetriesCounter = Meter.CreateCounter<long>(
        "sgrowing_dlq_retries_total",
        "retries",
        "Total dead-letter queue exponential backoff retry attempts");

    public static readonly UpDownCounter<long> ActiveCourierDriversCounter = Meter.CreateUpDownCounter<long>(
        "sgrowing_active_drivers_current",
        "drivers",
        "Current number of in-house courier drivers actively on route");

    // Ingested Capability Expansion Metrics
    public static readonly Counter<long> WarehouseAllocationsCounter = Meter.CreateCounter<long>(
        "sgrowing_warehouse_allocations_total",
        "allocations",
        "Total number of multi-warehouse order allocations executed");

    public static readonly Counter<long> WhatsAppDispatchesCounter = Meter.CreateCounter<long>(
        "sgrowing_whatsapp_dispatches_total",
        "messages",
        "Total number of WhatsApp gateway messages dispatched");

    public static readonly Counter<long> AutonomousFunnelsGeneratedCounter = Meter.CreateCounter<long>(
        "sgrowing_autonomous_funnels_total",
        "funnels",
        "Total number of 15-step autonomous funnels generated and deployed");

    public static readonly Counter<long> AgentHeartbeatsExecutedCounter = Meter.CreateCounter<long>(
        "sgrowing_agent_heartbeats_total",
        "heartbeats",
        "Total number of autonomous agent heartbeat cycles executed");

    public static readonly Counter<long> MediaVaultUploadsCounter = Meter.CreateCounter<long>(
        "sgrowing_media_vault_uploads_total",
        "uploads",
        "Total number of pre-signed direct-to-S3 uploads generated");

    public static readonly Counter<long> AudioTranscriptionsCounter = Meter.CreateCounter<long>(
        "sgrowing_audio_transcriptions_total",
        "transcriptions",
        "Total number of WhatsApp inbound voice notes transcribed via Whisper");

    public static readonly Counter<long> InvoicesReconciledCounter = Meter.CreateCounter<long>(
        "sgrowing_invoices_reconciled_total",
        "invoices",
        "Total number of B2B invoices reconciled and settled via payment webhooks");

    public static readonly Counter<long> WorkflowsExecutedCounter = Meter.CreateCounter<long>(
        "sgrowing_workflows_executed_total",
        "executions",
        "Total number of visual node workflow graphs executed");

    public static readonly Counter<long> SlaBreachesEscalatedCounter = Meter.CreateCounter<long>(
        "sgrowing_sla_breaches_escalated_total",
        "breaches",
        "Total number of customer SLA breaches escalated to Urgent with supervisor alert");

    public static readonly Counter<long> AgentRemediationsCounter = Meter.CreateCounter<long>(
        "sgrowing_agent_remediations_total",
        "remediations",
        "Total number of autonomous self-healing agent remediation actions executed");

    public static readonly Counter<long> EventFanoutsDispatchedCounter = Meter.CreateCounter<long>(
        "sgrowing_event_fanouts_dispatched_total",
        "events",
        "Total real-time multi-broker events fanned out across webhooks, websockets, and queues");

    public static readonly Counter<long> CartReservationsAcquiredCounter = Meter.CreateCounter<long>(
        "sgrowing_cart_reservations_acquired_total",
        "reservations",
        "Total time-bound cart inventory reservation locks acquired");

    public static readonly Counter<long> CartReservationsExpiredCounter = Meter.CreateCounter<long>(
        "sgrowing_cart_reservations_expired_total",
        "expirations",
        "Total abandoned cart reservations swept and returned to available inventory");

    public static readonly Counter<long> WhatsAppNewsletterBroadcastsCounter = Meter.CreateCounter<long>(
        "sgrowing_whatsapp_newsletter_broadcasts_total",
        "broadcasts",
        "Total WhatsApp newsletter posts broadcasted to public channels");

    public static readonly Counter<long> AiModelFallbacksTriggeredCounter = Meter.CreateCounter<long>(
        "sgrowing_ai_model_fallbacks_total",
        "fallbacks",
        "Total AI multi-model dynamic failovers and provider fallback executions");

    public static readonly Counter<long> CallCenterCollisionLocksAcquiredCounter = Meter.CreateCounter<long>(
        "sgrowing_callcenter_collision_locks_acquired_total",
        "locks",
        "Total call center order locks acquired to prevent multi-agent dialing collisions");

    public static readonly Counter<long> CsatSurveysRecordedCounter = Meter.CreateCounter<long>(
        "sgrowing_csat_surveys_recorded_total",
        "surveys",
        "Total post-resolution customer satisfaction survey responses recorded");

    public static readonly Counter<long> TenantSnapshotsDeployedCounter = Meter.CreateCounter<long>(
        "sgrowing_tenant_snapshots_deployed_total",
        "deployments",
        "Total agency tenant blueprint snapshots cloned and deployed to sub-accounts");

    public static readonly Counter<long> WaterfallEnrichmentsExecutedCounter = Meter.CreateCounter<long>(
        "sgrowing_waterfall_enrichments_executed_total",
        "enrichments",
        "Total multi-provider cascade prospect enrichment lookups completed");

    public static readonly Counter<long> AndroidSmsDispatchedCounter = Meter.CreateCounter<long>(
        "sgrowing_android_sms_dispatched_total",
        "sms",
        "Total hardware-powered SMS dispatches sent via connected Android phones");

    public static readonly Counter<long> VoiceAmdDetectionsCounter = Meter.CreateCounter<long>(
        "sgrowing_voice_amd_detections_total",
        "detections",
        "Total acoustic answering machine detection classifications evaluated on outbound calls");
}
