namespace SGrowing.Api.Modules.Sales.DealCopilot;

public enum DealRiskSeverity
{
    Low,
    Medium,
    High,
    Critical
}

public record DealRiskFactor(
    string Name,
    DealRiskSeverity Severity,
    string Description
);

public record PipelineDeal(
    string DealId,
    string AccountName,
    double Amount,
    string Stage, // "Discovery", "Demo", "Proposal", "Negotiation", "ClosedWon"
    int DaysInactive,
    double WinProbabilityPercent,
    string RiskLevel, // "Healthy", "ModerateRisk", "SevereRisk"
    List<DealRiskFactor> RiskFactors,
    string PrescribedAction,
    string AssignedAe
);

public record InterventionActionRequest(
    string DealId,
    string ActionType // "DraftReengagementEmail", "DispatchBattlecard", "ScheduleSecurityReview"
);

public record InterventionActionResult(
    string DealId,
    bool Success,
    double NewWinProbabilityPercent,
    string ExecutionMessage
);
