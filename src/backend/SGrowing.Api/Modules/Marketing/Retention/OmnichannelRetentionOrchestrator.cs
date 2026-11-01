using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;

namespace SGrowing.Api.Modules.Marketing.Retention;

/// <summary>
/// Contract for orchestrating omnichannel win-back dispatches, randomized holdout experiments, and causal lift tracking.
/// </summary>
public interface IOmnichannelRetentionOrchestrator
{
    /// <summary>
    /// Executes an omnichannel retention intervention with automated 10% holdout control assignment.
    /// </summary>
    RetentionInterventionRecord DispatchIntervention(
        CustomerBehaviorSignals signals,
        RetentionIncentive? forcedIncentive = null,
        bool forceHoldout = false,
        bool bypassHoldout = false,
        Random? random = null);

    /// <summary>
    /// Asynchronously processes an intervention request via API contract.
    /// </summary>
    Task<DispatchInterventionResponse> DispatchInterventionAsync(
        DispatchInterventionRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates the delivery, engagement, or conversion status of an intervention record.
    /// </summary>
    RetentionInterventionRecord? MarkInterventionOutcome(
        Guid interventionId,
        InterventionStatus status,
        decimal revenueSavedDzd = 0m);

    /// <summary>
    /// Retrieves historical intervention records, optionally filtered by customer ID.
    /// </summary>
    IReadOnlyList<RetentionInterventionRecord> GetInterventionHistory(string? customerId = null);

    /// <summary>
    /// Calculates incremental causal conversion lift and net revenue saved comparing treatment vs 10% holdout control.
    /// </summary>
    RetentionExperimentMetrics CalculateCausalLiftMetrics();

    /// <summary>
    /// Computes the cascading fallback sequence across WhatsApp, Voice Concierge, and Targeted Email.
    /// </summary>
    List<RetentionChannel> DetermineCascadeSequence(RetentionChannel primaryChannel);

    /// <summary>
    /// Evaluates whether a customer is randomized into the 10% holdout control cohort.
    /// </summary>
    bool IsCustomerInHoldout(string customerId, Random? random = null);
}

/// <summary>
/// Autonomous Omnichannel Retention Orchestrator managing cascading dispatches and randomized holdouts.
/// </summary>
public class OmnichannelRetentionOrchestrator : IOmnichannelRetentionOrchestrator
{
    private const double HoldoutRatio = 0.10; // 10% randomized holdout control

    private readonly IPredictiveChurnRadarService _radarService;
    private readonly IRetentionPolicyBanditService _banditService;
    private readonly ILogger<OmnichannelRetentionOrchestrator>? _logger;

    private readonly ConcurrentDictionary<Guid, RetentionInterventionRecord> _interventions = new();

    public OmnichannelRetentionOrchestrator(
        IPredictiveChurnRadarService radarService,
        IRetentionPolicyBanditService banditService,
        ILogger<OmnichannelRetentionOrchestrator>? logger = null)
    {
        _radarService = radarService ?? throw new ArgumentNullException(nameof(radarService));
        _banditService = banditService ?? throw new ArgumentNullException(nameof(banditService));
        _logger = logger;
    }

    public RetentionInterventionRecord DispatchIntervention(
        CustomerBehaviorSignals signals,
        RetentionIncentive? forcedIncentive = null,
        bool forceHoldout = false,
        bool bypassHoldout = false,
        Random? random = null)
    {
        ArgumentNullException.ThrowIfNull(signals);
        var rng = random ?? Random.Shared;

        // Assess churn risk profile
        var assessment = _radarService.AssessCustomerRisk(signals);

        // Multi-armed bandit selects recommended incentive and primary channel
        var recommendation = _banditService.RecommendIncentive(signals, assessment, rng);

        bool isHoldout;
        if (forceHoldout)
        {
            isHoldout = true;
        }
        else if (bypassHoldout)
        {
            isHoldout = false;
        }
        else
        {
            isHoldout = IsCustomerInHoldout(signals.CustomerId, rng);
        }

        Guid interventionId = Guid.NewGuid();
        RetentionInterventionRecord record;

        if (isHoldout)
        {
            // 10% Randomized Holdout Control: No incentive sent to measure pure baseline organic conversion
            record = new RetentionInterventionRecord(
                InterventionId: interventionId,
                CustomerId: signals.CustomerId,
                ChannelUsed: recommendation.RecommendedChannel,
                IncentiveApplied: null,
                DispatchStatus: InterventionStatus.Ignored,
                RevenueSavedDzd: 0m,
                IsHoldoutControl: true,
                DispatchedAtUtc: DateTime.UtcNow);

            _logger?.LogInformation(
                "Customer {CustomerId} assigned to 10% HOLDOUT CONTROL cohort. No incentive dispatched.",
                signals.CustomerId);
        }
        else
        {
            // 90% Treatment Group: Dispatched through cascading omnichannel delivery
            var incentive = forcedIncentive ?? recommendation.SelectedIncentive;
            var cascade = DetermineCascadeSequence(recommendation.RecommendedChannel);

            record = new RetentionInterventionRecord(
                InterventionId: interventionId,
                CustomerId: signals.CustomerId,
                ChannelUsed: cascade[0],
                IncentiveApplied: incentive,
                DispatchStatus: InterventionStatus.Dispatched,
                RevenueSavedDzd: 0m,
                IsHoldoutControl: false,
                DispatchedAtUtc: DateTime.UtcNow);

            _logger?.LogInformation(
                "Intervention {InterventionId} DISPATCHED to Customer {CustomerId} via {Channel} with incentive {Incentive} (Cost: {Cost:F0} DZD)",
                interventionId, signals.CustomerId, cascade[0], incentive.Type, incentive.CostDzd);
        }

        _interventions[interventionId] = record;
        return record;
    }

    public Task<DispatchInterventionResponse> DispatchInterventionAsync(
        DispatchInterventionRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var intervention = DispatchIntervention(
            request.Signals,
            request.ForcedIncentive,
            request.ForceHoldout,
            request.BypassHoldout);

        string message = intervention.IsHoldoutControl
            ? "Customer assigned to holdout control group. No incentive dispatched."
            : $"Intervention dispatched via {intervention.ChannelUsed} with {intervention.IncentiveApplied?.Type}.";

        var response = new DispatchInterventionResponse(
            Success: true,
            Intervention: intervention,
            Message: message);

        return Task.FromResult(response);
    }

    public RetentionInterventionRecord? MarkInterventionOutcome(
        Guid interventionId,
        InterventionStatus status,
        decimal revenueSavedDzd = 0m)
    {
        if (!_interventions.TryGetValue(interventionId, out var existing))
        {
            return null;
        }

        var updated = existing with
        {
            DispatchStatus = status,
            RevenueSavedDzd = revenueSavedDzd > 0m ? revenueSavedDzd : existing.RevenueSavedDzd
        };

        _interventions[interventionId] = updated;

        // Feedback bandit engine if it was an active treatment intervention
        if (!updated.IsHoldoutControl && updated.IncentiveApplied is not null)
        {
            if (status == InterventionStatus.Converted)
            {
                _banditService.RecordFeedback(
                    updated.IncentiveApplied.Type,
                    converted: true,
                    revenueSavedDzd: revenueSavedDzd);
            }
            else if (status == InterventionStatus.Ignored)
            {
                _banditService.RecordFeedback(
                    updated.IncentiveApplied.Type,
                    converted: false,
                    revenueSavedDzd: 0m);
            }
        }

        _logger?.LogInformation(
            "Intervention {InterventionId} updated to {Status} (Revenue: {Revenue:F0} DZD, Holdout={Holdout})",
            interventionId, status, revenueSavedDzd, updated.IsHoldoutControl);

        return updated;
    }

    public IReadOnlyList<RetentionInterventionRecord> GetInterventionHistory(string? customerId = null)
    {
        if (string.IsNullOrWhiteSpace(customerId))
        {
            return _interventions.Values
                .OrderByDescending(i => i.DispatchedAtUtc)
                .ToList();
        }

        return _interventions.Values
            .Where(i => string.Equals(i.CustomerId, customerId, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(i => i.DispatchedAtUtc)
            .ToList();
    }

    public RetentionExperimentMetrics CalculateCausalLiftMetrics()
    {
        var all = _interventions.Values.ToList();

        var treatments = all.Where(i => !i.IsHoldoutControl).ToList();
        var controls = all.Where(i => i.IsHoldoutControl).ToList();

        int treatmentConversions = treatments.Count(i => i.DispatchStatus == InterventionStatus.Converted);
        int controlConversions = controls.Count(i => i.DispatchStatus == InterventionStatus.Converted);

        double treatmentRate = treatments.Count > 0
            ? (double)treatmentConversions / treatments.Count
            : 0.0;

        double controlRate = controls.Count > 0
            ? (double)controlConversions / controls.Count
            : 0.0;

        double incrementalLift = Math.Round(treatmentRate - controlRate, 4);

        decimal totalRevenueSaved = treatments
            .Where(i => i.DispatchStatus == InterventionStatus.Converted)
            .Sum(i => i.RevenueSavedDzd);

        decimal totalIncentiveCost = treatments
            .Where(i => i.DispatchStatus == InterventionStatus.Converted && i.IncentiveApplied is not null)
            .Sum(i => i.IncentiveApplied!.CostDzd);

        decimal netRevenueSaved = totalRevenueSaved - totalIncentiveCost;

        return new RetentionExperimentMetrics(
            TotalInterventions: all.Count,
            TreatmentCount: treatments.Count,
            HoldoutControlCount: controls.Count,
            TreatmentConversions: treatmentConversions,
            ControlConversions: controlConversions,
            TreatmentConversionRate: Math.Round(treatmentRate, 4),
            ControlConversionRate: Math.Round(controlRate, 4),
            IncrementalCausalLift: incrementalLift,
            TotalRevenueSavedDzd: totalRevenueSaved,
            TotalIncentiveCostDzd: totalIncentiveCost,
            NetRevenueSavedDzd: netRevenueSaved);
    }

    public List<RetentionChannel> DetermineCascadeSequence(RetentionChannel primaryChannel)
    {
        return primaryChannel switch
        {
            RetentionChannel.AutomatedVoiceConcierge =>
                [RetentionChannel.AutomatedVoiceConcierge, RetentionChannel.WhatsAppInteractive, RetentionChannel.TargetedEmail],

            RetentionChannel.TargetedEmail =>
                [RetentionChannel.TargetedEmail, RetentionChannel.WhatsAppInteractive, RetentionChannel.AutomatedVoiceConcierge],

            _ =>
                [RetentionChannel.WhatsAppInteractive, RetentionChannel.AutomatedVoiceConcierge, RetentionChannel.TargetedEmail]
        };
    }

    public bool IsCustomerInHoldout(string customerId, Random? random = null)
    {
        var rng = random ?? Random.Shared;
        return rng.NextDouble() < HoldoutRatio;
    }
}
