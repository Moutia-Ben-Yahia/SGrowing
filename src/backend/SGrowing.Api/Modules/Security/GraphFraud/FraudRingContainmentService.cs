using System.Globalization;

namespace SGrowing.Api.Modules.Security.GraphFraud;

public interface IFraudRingContainmentService
{
    Task<QuarantineActionResponse> QuarantineClusterAsync(
        string clusterId,
        string? reason = null,
        string? operatorId = null,
        CancellationToken cancellationToken = default);

    IReadOnlyList<ContainmentAuditEvent> GetAuditTrail();
    ContainmentAuditEvent? GetAuditEventByCluster(string clusterId);
}

public class FraudRingContainmentService : IFraudRingContainmentService
{
    private readonly IIdentityGraphEngine _engine;
    private readonly IGraphClusterAnalyzerService _analyzer;
    private readonly List<ContainmentAuditEvent> _auditLog = [];
    private readonly Lock _lock = new();

    public FraudRingContainmentService(
        IIdentityGraphEngine engine,
        IGraphClusterAnalyzerService analyzer)
    {
        _engine = engine ?? throw new ArgumentNullException(nameof(engine));
        _analyzer = analyzer ?? throw new ArgumentNullException(nameof(analyzer));
    }

    public Task<QuarantineActionResponse> QuarantineClusterAsync(
        string clusterId,
        string? reason = null,
        string? operatorId = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(clusterId);

        var cluster = _analyzer.GetClusterById(clusterId);
        if (cluster == null)
        {
            // Cluster not found in cache or graph
            return Task.FromResult(new QuarantineActionResponse
            {
                QuarantinedNodesCount = 0,
                FrozenOrdersCount = 0,
                FrozenEscrowAmount = 0m,
                CancelledWaybills = []
            });
        }

        var memberIds = cluster.MemberNodeIds;
        var frozenOrdersCount = 0;
        decimal frozenEscrowAmount = 0m;
        var cancelledWaybills = new List<string>();

        // 1. Quarantine every node in the cluster
        foreach (var nodeId in memberIds)
        {
            _engine.SetNodeQuarantine(nodeId, true);

            var node = _engine.GetNode(nodeId);
            if (node == null) continue;

            if (node.NodeType == GraphNodeType.Customer)
            {
                // Inspect orders to freeze
                var pendingOrders = 2; // baseline simulated orders
                if (node.Properties.TryGetValue("pending_orders", out var poStr) &&
                    int.TryParse(poStr, NumberStyles.Integer, CultureInfo.InvariantCulture, out var poVal))
                {
                    pendingOrders = poVal;
                }

                frozenOrdersCount += pendingOrders;

                // Inspect escrow amount to freeze
                decimal escrow = 450.00m; // baseline escrow amount in MAD / USD
                if (node.Properties.TryGetValue("escrow_balance", out var ebStr) &&
                    decimal.TryParse(ebStr, NumberStyles.Float, CultureInfo.InvariantCulture, out var ebVal))
                {
                    escrow = ebVal;
                }

                frozenEscrowAmount += escrow;

                for (var o = 1; o <= pendingOrders; o++)
                {
                    var cleanId = node.NodeId.Replace(":", "-").Replace("/", "-");
                    cancelledWaybills.Add($"WB-FRAUD-{cleanId}-ORD{o:D3}");
                }
            }
        }

        var actionSummary = new QuarantineActionResponse
        {
            QuarantinedNodesCount = memberIds.Count,
            FrozenOrdersCount = frozenOrdersCount,
            FrozenEscrowAmount = frozenEscrowAmount,
            CancelledWaybills = cancelledWaybills
        };

        var auditEvent = new ContainmentAuditEvent
        {
            EventId = Guid.NewGuid().ToString("N"),
            ClusterId = clusterId,
            AffectedNodeIds = [.. memberIds],
            ActionSummary = actionSummary,
            Reason = reason ?? "Automated Graph Fraud Ring Containment",
            OperatorId = operatorId ?? "SYSTEM_AUTO_CONTAINMENT",
            TimestampUtc = DateTime.UtcNow
        };

        lock (_lock)
        {
            _auditLog.Add(auditEvent);
        }

        return Task.FromResult(actionSummary);
    }

    public IReadOnlyList<ContainmentAuditEvent> GetAuditTrail()
    {
        lock (_lock)
        {
            return [.. _auditLog];
        }
    }

    public ContainmentAuditEvent? GetAuditEventByCluster(string clusterId)
    {
        if (string.IsNullOrWhiteSpace(clusterId)) return null;

        lock (_lock)
        {
            return _auditLog.LastOrDefault(e => string.Equals(e.ClusterId, clusterId, StringComparison.OrdinalIgnoreCase));
        }
    }
}
