namespace SGrowing.Api.Modules.Infrastructure.MultiCloud;

/// <summary>
/// Autonomous disaster recovery drill autopilot coordinator validating RTO and RPO metrics
/// non-destructively across sovereign and public multi-cloud topologies.
/// </summary>
public interface IAutonomousDrAutopilotService
{
    /// <summary>
    /// Executes an autonomous non-destructive disaster recovery failover drill.
    /// Simulates cloud blackout or WAN partition, measures RTO/RPO convergence, and restores topology.
    /// </summary>
    DisasterRecoveryDrillReport ExecuteDrill(TriggerDrDrillRequest request);

    /// <summary>
    /// Retrieves historical disaster recovery drill reports.
    /// </summary>
    IReadOnlyList<DisasterRecoveryDrillReport> GetDrillHistory();

    /// <summary>
    /// Retrieves the most recently executed disaster recovery drill report.
    /// </summary>
    DisasterRecoveryDrillReport? GetLatestReport();
}
