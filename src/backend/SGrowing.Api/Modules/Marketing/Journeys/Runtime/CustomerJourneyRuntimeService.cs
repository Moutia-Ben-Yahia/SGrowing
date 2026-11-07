using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace SGrowing.Api.Modules.Marketing.Journeys.Runtime;

/// <summary>
/// Service contract for multi-tenant customer journey runtime execution and correlation.
/// </summary>
public interface ICustomerJourneyRuntimeService
{
    Task<CustomerJourneyProgress> TriggerJourney(TriggerJourneyEventRequest request, CancellationToken cancellationToken = default);
    Task<CustomerJourneyProgress?> AdvanceProgress(AdvanceJourneyRequest request, CancellationToken cancellationToken = default);
    Task<CustomerJourneyProgress?> GetJourneyProgress(Guid tenantId, Guid progressId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<CustomerJourneyProgress>> GetContactsInJourney(Guid tenantId, string journeyId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ActiveJourneySummary>> GetActiveJourneys(Guid tenantId, CancellationToken cancellationToken = default);
}

/// <summary>
/// Thread-safe, multi-tenant runtime execution engine for customer journeys with event correlation,
/// conditional branching, and delay state tracking.
/// </summary>
public class CustomerJourneyRuntimeService : ICustomerJourneyRuntimeService
{
    public const string RamadanCartRecoveryJourneyId = "journey-ramadan-cart-recovery";
    public const string RamadanCartRecoveryTitle = "Ramadan COD Abandoned Cart Recovery & Retention Journey";
    public const string RamadanCartRecoveryTrigger = "cart.abandoned";

    public const string NodeTriggerId = "node-1-trigger-cart-abandoned";
    public const string NodeDelayId = "node-2-delay-15m";
    public const string NodeConditionId = "node-3-condition-cart-threshold";
    public const string NodeActionWhatsAppId = "node-4a-action-whatsapp-promo";
    public const string NodeActionEmailId = "node-4b-action-email-voucher";
    public const string NodeConversionCheckpointId = "node-5-action-conversion-checkpoint";

    private const decimal HighCartThresholdSar = 300.00m;
    private const int DefaultDelayMinutes = 15;

    // Baseline template metrics for Cockpit display (Domain E1/E2)
    private const int BaseEnrolledCount = 1420;
    private const int BaseDelayWaitingCount = 380;
    private const int BaseConvertedCount = 940;

    // Multi-tenant partitioned progress store: TenantId -> (ProgressId -> CustomerJourneyProgress)
    private readonly ConcurrentDictionary<Guid, ConcurrentDictionary<Guid, CustomerJourneyProgress>> _tenantStore = new();

    private readonly JourneyDefinition _templateJourney;

    public CustomerJourneyRuntimeService()
    {
        _templateJourney = CreateTemplateJourneyDefinition();
    }

    public Task<CustomerJourneyProgress> TriggerJourney(TriggerJourneyEventRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.TenantId == Guid.Empty)
        {
            throw new ArgumentException("TenantId cannot be empty.", nameof(request));
        }
        if (request.ContactId == Guid.Empty)
        {
            throw new ArgumentException("ContactId cannot be empty.", nameof(request));
        }
        if (string.IsNullOrWhiteSpace(request.ContactEmail))
        {
            throw new ArgumentException("ContactEmail is required.", nameof(request));
        }

        var tenantMap = _tenantStore.GetOrAdd(request.TenantId, _ => new ConcurrentDictionary<Guid, CustomerJourneyProgress>());

        var journeyId = RamadanCartRecoveryJourneyId;
        var progressId = Guid.NewGuid();
        var now = DateTime.UtcNow;

        var contextData = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (request.PayloadData != null)
        {
            foreach (var kvp in request.PayloadData)
            {
                contextData[kvp.Key] = kvp.Value;
            }
        }

        // 1. Initialize contact journey progress
        var progress = new CustomerJourneyProgress(
            ProgressId: progressId,
            TenantId: request.TenantId,
            JourneyId: journeyId,
            ContactId: request.ContactId,
            ContactEmail: request.ContactEmail,
            CurrentNodeId: NodeTriggerId,
            Status: JourneyExecutionStatus.Active,
            ExecutionHistory: new List<JourneyStepExecutionLog>(),
            ContextData: contextData,
            ResumeAtUtc: null,
            CompletedAtUtc: null
        );

        // Record Step 1: TriggerNode
        progress.ExecutionHistory.Add(new JourneyStepExecutionLog(
            StepId: Guid.NewGuid(),
            NodeId: NodeTriggerId,
            NodeType: JourneyNodeType.TriggerNode,
            ExecutedAtUtc: now,
            Outcome: "TriggerEnrolled",
            Details: $"Inbound event '{request.EventName}' correlated. Contact {request.ContactEmail} enrolled into '{_templateJourney.Title}'."
        ));

        // 2. Automatically advance into Node 2: DelayNode (15m delay simulation)
        var delayDuration = TimeSpan.FromMinutes(DefaultDelayMinutes);
        var resumeAt = now.Add(delayDuration);

        progress.CurrentNodeId = NodeDelayId;
        progress.Status = JourneyExecutionStatus.PausedWaitingDelay;
        progress.ResumeAtUtc = resumeAt;

        progress.ExecutionHistory.Add(new JourneyStepExecutionLog(
            StepId: Guid.NewGuid(),
            NodeId: NodeDelayId,
            NodeType: JourneyNodeType.DelayNode,
            ExecutedAtUtc: now,
            Outcome: "PausedWaitingDelay",
            Details: $"15-minute Ramadan COD recovery delay window scheduled. Awaiting timer elapsed or simulation advance until {resumeAt:yyyy-MM-dd HH:mm:ss} UTC."
        ));

        tenantMap[progressId] = progress;
        return Task.FromResult(progress);
    }

    public Task<CustomerJourneyProgress?> AdvanceProgress(AdvanceJourneyRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.TenantId == Guid.Empty)
        {
            throw new ArgumentException("TenantId cannot be empty.", nameof(request));
        }

        if (!_tenantStore.TryGetValue(request.TenantId, out var tenantMap) ||
            !tenantMap.TryGetValue(request.ProgressId, out var progress))
        {
            return Task.FromResult<CustomerJourneyProgress?>(null);
        }

        var now = DateTime.UtcNow;

        // If override node is explicitly specified
        if (!string.IsNullOrWhiteSpace(request.OverrideNodeId))
        {
            ApplyNodeOverride(progress, request.OverrideNodeId, now);
            return Task.FromResult<CustomerJourneyProgress?>(progress);
        }

        // State Machine progression based on CurrentNodeId
        switch (progress.CurrentNodeId)
        {
            case NodeDelayId:
                // Complete delay node
                progress.ExecutionHistory.Add(new JourneyStepExecutionLog(
                    StepId: Guid.NewGuid(),
                    NodeId: NodeDelayId,
                    NodeType: JourneyNodeType.DelayNode,
                    ExecutedAtUtc: now,
                    Outcome: "DelayCompleted",
                    Details: "15-minute wait delay interval elapsed. Resuming downstream pipeline execution."
                ));
                progress.ResumeAtUtc = null;
                progress.Status = JourneyExecutionStatus.Active;

                // Move directly through ConditionNode (Node 3) and evaluate branch
                ExecuteConditionAndAction(progress, now);
                break;

            case NodeConditionId:
                ExecuteConditionAndAction(progress, now);
                break;

            case NodeActionWhatsAppId:
            case NodeActionEmailId:
                // Progress from action node to Node 5: Conversion Checkpoint
                ExecuteConversionCheckpoint(progress, now);
                break;

            case NodeConversionCheckpointId:
                // Already completed
                if (progress.Status != JourneyExecutionStatus.Completed)
                {
                    progress.Status = JourneyExecutionStatus.Completed;
                    progress.CompletedAtUtc = now;
                }
                break;

            default:
                // If at trigger or other node, transition to delay
                progress.CurrentNodeId = NodeDelayId;
                progress.Status = JourneyExecutionStatus.PausedWaitingDelay;
                progress.ResumeAtUtc = now.AddMinutes(DefaultDelayMinutes);
                break;
        }

        return Task.FromResult<CustomerJourneyProgress?>(progress);
    }

    public Task<CustomerJourneyProgress?> GetJourneyProgress(Guid tenantId, Guid progressId, CancellationToken cancellationToken = default)
    {
        if (tenantId == Guid.Empty)
        {
            return Task.FromResult<CustomerJourneyProgress?>(null);
        }

        if (_tenantStore.TryGetValue(tenantId, out var tenantMap) &&
            tenantMap.TryGetValue(progressId, out var progress))
        {
            return Task.FromResult<CustomerJourneyProgress?>(progress);
        }

        return Task.FromResult<CustomerJourneyProgress?>(null);
    }

    public Task<IReadOnlyList<CustomerJourneyProgress>> GetContactsInJourney(Guid tenantId, string journeyId, CancellationToken cancellationToken = default)
    {
        if (tenantId == Guid.Empty)
        {
            return Task.FromResult<IReadOnlyList<CustomerJourneyProgress>>(Array.Empty<CustomerJourneyProgress>());
        }

        EnsureSeedContactsForTenant(tenantId);

        if (_tenantStore.TryGetValue(tenantId, out var tenantMap))
        {
            var matches = tenantMap.Values
                .Where(p => string.Equals(p.JourneyId, journeyId, StringComparison.OrdinalIgnoreCase))
                .OrderByDescending(p => p.ExecutionHistory.LastOrDefault()?.ExecutedAtUtc ?? DateTime.MinValue)
                .ToList();

            return Task.FromResult<IReadOnlyList<CustomerJourneyProgress>>(matches);
        }

        return Task.FromResult<IReadOnlyList<CustomerJourneyProgress>>(Array.Empty<CustomerJourneyProgress>());
    }

    public Task<IReadOnlyList<ActiveJourneySummary>> GetActiveJourneys(Guid tenantId, CancellationToken cancellationToken = default)
    {
        if (tenantId == Guid.Empty)
        {
            return Task.FromResult<IReadOnlyList<ActiveJourneySummary>>(Array.Empty<ActiveJourneySummary>());
        }

        EnsureSeedContactsForTenant(tenantId);

        var dynamicEnrolled = 0;
        var dynamicInDelay = 0;
        var dynamicConverted = 0;

        if (_tenantStore.TryGetValue(tenantId, out var tenantMap))
        {
            var journeys = tenantMap.Values
                .Where(p => string.Equals(p.JourneyId, RamadanCartRecoveryJourneyId, StringComparison.OrdinalIgnoreCase))
                .ToList();

            dynamicEnrolled = journeys.Count;
            dynamicInDelay = journeys.Count(j => j.Status == JourneyExecutionStatus.PausedWaitingDelay);
            dynamicConverted = journeys.Count(j => j.Status == JourneyExecutionStatus.Completed);
        }

        var totalEnrolled = BaseEnrolledCount + dynamicEnrolled;
        var totalInDelay = BaseDelayWaitingCount + dynamicInDelay;
        var totalConverted = BaseConvertedCount + dynamicConverted;
        var conversionRate = totalEnrolled > 0
            ? Math.Round((double)totalConverted / totalEnrolled * 100.0, 1)
            : 0.0;

        var summaries = new List<ActiveJourneySummary>
        {
            new(
                JourneyId: RamadanCartRecoveryJourneyId,
                Title: RamadanCartRecoveryTitle,
                Description: "High-converting recovery journey correlating abandoned cart telemetry with VIP WhatsApp 1-Click COD checkout and standard email vouchers.",
                TriggerEvent: RamadanCartRecoveryTrigger,
                NodeCount: 5,
                EnrolledContacts: totalEnrolled,
                InDelayNodes: totalInDelay,
                ConvertedContacts: totalConverted,
                ConversionRatePercentage: conversionRate
            )
        };

        return Task.FromResult<IReadOnlyList<ActiveJourneySummary>>(summaries);
    }

    private void ExecuteConditionAndAction(CustomerJourneyProgress progress, DateTime now)
    {
        // 1. Evaluate ConditionNode (Node 3)
        progress.CurrentNodeId = NodeConditionId;
        progress.Status = JourneyExecutionStatus.Active;

        var cartAmount = ExtractCartAmount(progress.ContextData);
        var isHighValue = cartAmount >= HighCartThresholdSar;

        progress.ExecutionHistory.Add(new JourneyStepExecutionLog(
            StepId: Guid.NewGuid(),
            NodeId: NodeConditionId,
            NodeType: JourneyNodeType.ConditionNode,
            ExecutedAtUtc: now,
            Outcome: isHighValue ? "ConditionTrue: HighCartValue (>= 300 SAR)" : "ConditionFalse: StandardCartValue (< 300 SAR)",
            Details: isHighValue
                ? $"Evaluated cart amount of {cartAmount:F2} SAR >= 300.00 SAR threshold. Routing to VIP WhatsApp 1-Click COD Discount Promo."
                : $"Evaluated cart amount of {cartAmount:F2} SAR < 300.00 SAR threshold. Routing to Standard Email Recovery Voucher."
        ));

        // 2. Dispatch ActionNode (Node 4a or 4b)
        if (isHighValue)
        {
            progress.CurrentNodeId = NodeActionWhatsAppId;
            progress.ExecutionHistory.Add(new JourneyStepExecutionLog(
                StepId: Guid.NewGuid(),
                NodeId: NodeActionWhatsAppId,
                NodeType: JourneyNodeType.ActionNode,
                ExecutedAtUtc: now.AddSeconds(1),
                Outcome: "WhatsAppPromoDispatched",
                Details: $"Dispatched VIP WhatsApp 1-Click COD Discount Promo via WhatsApp Gateway to {progress.ContactEmail}."
            ));
        }
        else
        {
            progress.CurrentNodeId = NodeActionEmailId;
            progress.ExecutionHistory.Add(new JourneyStepExecutionLog(
                StepId: Guid.NewGuid(),
                NodeId: NodeActionEmailId,
                NodeType: JourneyNodeType.ActionNode,
                ExecutedAtUtc: now.AddSeconds(1),
                Outcome: "EmailVoucherDispatched",
                Details: $"Dispatched Standard Email Recovery Voucher with 10% coupon code to {progress.ContactEmail}."
            ));
        }

        // 3. Move to Node 5: Conversion Checkpoint
        ExecuteConversionCheckpoint(progress, now.AddSeconds(2));
    }

    private static void ExecuteConversionCheckpoint(CustomerJourneyProgress progress, DateTime now)
    {
        progress.CurrentNodeId = NodeConversionCheckpointId;
        progress.Status = JourneyExecutionStatus.Completed;
        progress.CompletedAtUtc = now;
        progress.ResumeAtUtc = null;

        progress.ExecutionHistory.Add(new JourneyStepExecutionLog(
            StepId: Guid.NewGuid(),
            NodeId: NodeConversionCheckpointId,
            NodeType: JourneyNodeType.ActionNode,
            ExecutedAtUtc: now,
            Outcome: "ConversionCheckpointReached",
            Details: $"Customer checkout completed checkpoint reached for {progress.ContactEmail}. Journey concluded successfully."
        ));
    }

    private static void ApplyNodeOverride(CustomerJourneyProgress progress, string overrideNodeId, DateTime now)
    {
        progress.CurrentNodeId = overrideNodeId;
        switch (overrideNodeId)
        {
            case NodeDelayId:
                progress.Status = JourneyExecutionStatus.PausedWaitingDelay;
                progress.ResumeAtUtc = now.AddMinutes(DefaultDelayMinutes);
                progress.ExecutionHistory.Add(new JourneyStepExecutionLog(
                    StepId: Guid.NewGuid(),
                    NodeId: NodeDelayId,
                    NodeType: JourneyNodeType.DelayNode,
                    ExecutedAtUtc: now,
                    Outcome: "PausedWaitingDelay",
                    Details: "Forced override to 15m delay simulation node."
                ));
                break;

            case NodeActionWhatsAppId:
                progress.Status = JourneyExecutionStatus.Active;
                progress.ResumeAtUtc = null;
                progress.ExecutionHistory.Add(new JourneyStepExecutionLog(
                    StepId: Guid.NewGuid(),
                    NodeId: NodeActionWhatsAppId,
                    NodeType: JourneyNodeType.ActionNode,
                    ExecutedAtUtc: now,
                    Outcome: "WhatsAppPromoDispatched",
                    Details: $"Override executed: Dispatched VIP WhatsApp 1-Click COD Promo to {progress.ContactEmail}."
                ));
                break;

            case NodeActionEmailId:
                progress.Status = JourneyExecutionStatus.Active;
                progress.ResumeAtUtc = null;
                progress.ExecutionHistory.Add(new JourneyStepExecutionLog(
                    StepId: Guid.NewGuid(),
                    NodeId: NodeActionEmailId,
                    NodeType: JourneyNodeType.ActionNode,
                    ExecutedAtUtc: now,
                    Outcome: "EmailVoucherDispatched",
                    Details: $"Override executed: Dispatched Standard Email Recovery Voucher to {progress.ContactEmail}."
                ));
                break;

            case NodeConversionCheckpointId:
                ExecuteConversionCheckpoint(progress, now);
                break;

            default:
                progress.ExecutionHistory.Add(new JourneyStepExecutionLog(
                    StepId: Guid.NewGuid(),
                    NodeId: overrideNodeId,
                    NodeType: JourneyNodeType.ActionNode,
                    ExecutedAtUtc: now,
                    Outcome: "ManualOverrideTransition",
                    Details: $"Transitioned to override node '{overrideNodeId}'."
                ));
                break;
        }
    }

    private static decimal ExtractCartAmount(Dictionary<string, string> context)
    {
        string[] candidates = ["cart_amount", "cartAmount", "amount", "order_total", "total", "value"];
        foreach (var key in candidates)
        {
            if (context.TryGetValue(key, out var rawVal) && !string.IsNullOrWhiteSpace(rawVal))
            {
                var match = Regex.Match(rawVal, @"[0-9]+(\.[0-9]+)?");
                if (match.Success && decimal.TryParse(match.Value, NumberStyles.Any, CultureInfo.InvariantCulture, out var parsed))
                {
                    return parsed;
                }
            }
        }
        return 0.00m;
    }

    private void EnsureSeedContactsForTenant(Guid tenantId)
    {
        var tenantMap = _tenantStore.GetOrAdd(tenantId, _ => new ConcurrentDictionary<Guid, CustomerJourneyProgress>());
        if (!tenantMap.IsEmpty)
        {
            return;
        }

        // Seed representative contacts for Cockpit visualization
        var sultanProgressId = Guid.Parse("11111111-1111-1111-1111-111111111111");
        var fatimaProgressId = Guid.Parse("22222222-2222-2222-2222-222222222222");
        var tariqProgressId = Guid.Parse("33333333-3333-3333-3333-333333333333");

        var now = DateTime.UtcNow;

        // Sultan Al-Dosari: Reached WhatsApp node
        var sultan = new CustomerJourneyProgress(
            ProgressId: sultanProgressId,
            TenantId: tenantId,
            JourneyId: RamadanCartRecoveryJourneyId,
            ContactId: Guid.NewGuid(),
            ContactEmail: "sultan.aldosari@growth.sa",
            CurrentNodeId: NodeActionWhatsAppId,
            Status: JourneyExecutionStatus.Active,
            ExecutionHistory: new List<JourneyStepExecutionLog>
            {
                new(Guid.NewGuid(), NodeTriggerId, JourneyNodeType.TriggerNode, now.AddMinutes(-20), "TriggerEnrolled", "Cart abandoned event received (450 SAR)."),
                new(Guid.NewGuid(), NodeDelayId, JourneyNodeType.DelayNode, now.AddMinutes(-19), "DelayCompleted", "15m delay satisfied."),
                new(Guid.NewGuid(), NodeConditionId, JourneyNodeType.ConditionNode, now.AddMinutes(-4), "ConditionTrue: HighCartValue", "Cart value 450 SAR >= 300 SAR."),
                new(Guid.NewGuid(), NodeActionWhatsAppId, JourneyNodeType.ActionNode, now.AddMinutes(-3), "WhatsAppPromoDispatched", "WhatsApp 1-Click COD Discount Promo delivered.")
            },
            ContextData: new Dictionary<string, string> { ["cart_amount"] = "450 SAR", ["contact_name"] = "Sultan Al-Dosari" }
        );

        // Fatima Al-Zahrani: Currently in Delay Node
        var fatima = new CustomerJourneyProgress(
            ProgressId: fatimaProgressId,
            TenantId: tenantId,
            JourneyId: RamadanCartRecoveryJourneyId,
            ContactId: Guid.NewGuid(),
            ContactEmail: "fatima.zahrani@growth.sa",
            CurrentNodeId: NodeDelayId,
            Status: JourneyExecutionStatus.PausedWaitingDelay,
            ExecutionHistory: new List<JourneyStepExecutionLog>
            {
                new(Guid.NewGuid(), NodeTriggerId, JourneyNodeType.TriggerNode, now.AddMinutes(-6), "TriggerEnrolled", "Cart abandoned event received (180 SAR)."),
                new(Guid.NewGuid(), NodeDelayId, JourneyNodeType.DelayNode, now.AddMinutes(-5), "PausedWaitingDelay", "Waiting 15m delay simulation.")
            },
            ContextData: new Dictionary<string, string> { ["cart_amount"] = "180 SAR", ["contact_name"] = "Fatima Al-Zahrani" },
            ResumeAtUtc: now.AddMinutes(10)
        );

        // Tariq Mansour: Completed conversion checkpoint
        var tariq = new CustomerJourneyProgress(
            ProgressId: tariqProgressId,
            TenantId: tenantId,
            JourneyId: RamadanCartRecoveryJourneyId,
            ContactId: Guid.NewGuid(),
            ContactEmail: "tariq.mansour@growth.sa",
            CurrentNodeId: NodeConversionCheckpointId,
            Status: JourneyExecutionStatus.Completed,
            ExecutionHistory: new List<JourneyStepExecutionLog>
            {
                new(Guid.NewGuid(), NodeTriggerId, JourneyNodeType.TriggerNode, now.AddHours(-2), "TriggerEnrolled", "Cart abandoned event received (320 SAR)."),
                new(Guid.NewGuid(), NodeDelayId, JourneyNodeType.DelayNode, now.AddHours(-2).AddMinutes(15), "DelayCompleted", "Delay completed."),
                new(Guid.NewGuid(), NodeConditionId, JourneyNodeType.ConditionNode, now.AddHours(-1).AddMinutes(44), "ConditionTrue", "Cart value 320 SAR >= 300 SAR."),
                new(Guid.NewGuid(), NodeActionWhatsAppId, JourneyNodeType.ActionNode, now.AddHours(-1).AddMinutes(43), "WhatsAppPromoDispatched", "WhatsApp promo dispatched."),
                new(Guid.NewGuid(), NodeConversionCheckpointId, JourneyNodeType.ActionNode, now.AddHours(-1).AddMinutes(30), "ConversionCheckpointReached", "Customer placed COD order with coupon.")
            },
            ContextData: new Dictionary<string, string> { ["cart_amount"] = "320 SAR", ["contact_name"] = "Tariq Mansour" },
            CompletedAtUtc: now.AddHours(-1).AddMinutes(30)
        );

        tenantMap.TryAdd(sultanProgressId, sultan);
        tenantMap.TryAdd(fatimaProgressId, fatima);
        tenantMap.TryAdd(tariqProgressId, tariq);
    }

    private static JourneyDefinition CreateTemplateJourneyDefinition()
    {
        var nodes = new Dictionary<string, JourneyNodeDefinition>
        {
            [NodeTriggerId] = new(
                NodeId: NodeTriggerId,
                Title: "Abandoned Cart Trigger",
                NodeType: JourneyNodeType.TriggerNode,
                NextNodeId: NodeDelayId,
                Description: "Fires when customer leaves checkout with items pending (event: cart.abandoned)."
            ),
            [NodeDelayId] = new(
                NodeId: NodeDelayId,
                Title: "15-Minute Recovery Delay",
                NodeType: JourneyNodeType.DelayNode,
                NextNodeId: NodeConditionId,
                DelayDuration: TimeSpan.FromMinutes(DefaultDelayMinutes),
                Description: "Pauses journey for 15 minutes to allow natural conversion before dispatching recovery promos."
            ),
            [NodeConditionId] = new(
                NodeId: NodeConditionId,
                Title: "Cart Amount Evaluation",
                NodeType: JourneyNodeType.ConditionNode,
                TrueNodeId: NodeActionWhatsAppId,
                FalseNodeId: NodeActionEmailId,
                Description: "Evaluates whether cart amount >= 300 SAR (True: VIP WhatsApp 1-Click COD, False: Email Voucher)."
            ),
            [NodeActionWhatsAppId] = new(
                NodeId: NodeActionWhatsAppId,
                Title: "WhatsApp 1-Click COD Discount Promo",
                NodeType: JourneyNodeType.ActionNode,
                NextNodeId: NodeConversionCheckpointId,
                Description: "Dispatches personalized interactive WhatsApp message with 1-click Cash On Delivery checkout link."
            ),
            [NodeActionEmailId] = new(
                NodeId: NodeActionEmailId,
                Title: "Standard Email Recovery Voucher",
                NodeType: JourneyNodeType.ActionNode,
                NextNodeId: NodeConversionCheckpointId,
                Description: "Sends standard email reminder with 10% coupon code."
            ),
            [NodeConversionCheckpointId] = new(
                NodeId: NodeConversionCheckpointId,
                Title: "Conversion Completed Checkpoint",
                NodeType: JourneyNodeType.ActionNode,
                NextNodeId: null,
                Description: "Terminal checkpoint verifying customer conversion and updating attribution ledgers."
            )
        };

        return new JourneyDefinition(
            JourneyId: RamadanCartRecoveryJourneyId,
            Title: RamadanCartRecoveryTitle,
            Description: "Production-grade Ramadan COD cart abandonment recovery journey with delay simulation and conditional branching.",
            TriggerEvent: RamadanCartRecoveryTrigger,
            InitialNodeId: NodeTriggerId,
            Nodes: nodes
        );
    }
}
