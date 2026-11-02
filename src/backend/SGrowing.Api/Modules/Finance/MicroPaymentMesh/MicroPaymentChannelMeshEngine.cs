namespace SGrowing.Api.Modules.Finance.MicroPaymentMesh;

public class MicroPaymentChannelMeshEngine
{
    private record GraphEdge(string ChannelId, string FromNode, string ToNode, decimal AvailableBalance, decimal Fee);

    public PaymentRouteResult RouteMicroPayment(RoutePaymentRequest request)
    {
        if (string.Equals(request.SourceNode, request.DestinationNode, StringComparison.OrdinalIgnoreCase))
        {
            return new PaymentRouteResult(
                PaymentHash: request.PaymentHash,
                SourceNode: request.SourceNode,
                DestinationNode: request.DestinationNode,
                AmountTokens: request.PaymentAmountTokens,
                TotalFeeTokens: 0m,
                Hops: [],
                IsSuccessful: true,
                StatusMessage: "Source and destination are identical; zero-hop intra-node transfer."
            );
        }

        // Build adjacency graph
        var adj = new Dictionary<string, List<GraphEdge>>(StringComparer.OrdinalIgnoreCase);

        void AddEdge(string from, string to, string channelId, decimal balance, decimal feeBase, decimal feePpm)
        {
            if (!adj.ContainsKey(from))
            {
                adj[from] = [];
            }

            decimal fee = Math.Round(feeBase + (request.PaymentAmountTokens * (feePpm / 1_000_000m)), 4);
            adj[from].Add(new GraphEdge(channelId, from, to, balance, fee));
        }

        foreach (var ch in request.ActiveChannels.Where(c => c.IsOpen))
        {
            // A -> B direction
            AddEdge(ch.NodeA, ch.NodeB, ch.ChannelId, ch.BalanceA, ch.FeeBaseTokens, ch.FeeProportionalPpm);
            // B -> A direction
            AddEdge(ch.NodeB, ch.NodeA, ch.ChannelId, ch.BalanceB, ch.FeeBaseTokens, ch.FeeProportionalPpm);
        }

        // Dijkstra algorithm minimizing cumulative fee with capacity constraint
        var dist = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase);
        var prevEdge = new Dictionary<string, GraphEdge>(StringComparer.OrdinalIgnoreCase);
        var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var priorityQueue = new PriorityQueue<string, decimal>();

        dist[request.SourceNode] = 0m;
        priorityQueue.Enqueue(request.SourceNode, 0m);

        while (priorityQueue.Count > 0)
        {
            var u = priorityQueue.Dequeue();

            if (visited.Contains(u))
            {
                continue;
            }

            visited.Add(u);

            if (string.Equals(u, request.DestinationNode, StringComparison.OrdinalIgnoreCase))
            {
                break;
            }

            if (!adj.TryGetValue(u, out var edges))
            {
                continue;
            }

            foreach (var edge in edges)
            {
                // Must have sufficient balance to forward payment amount
                if (edge.AvailableBalance < request.PaymentAmountTokens)
                {
                    continue;
                }

                decimal altDist = dist[u] + edge.Fee;
                if (!dist.ContainsKey(edge.ToNode) || altDist < dist[edge.ToNode])
                {
                    dist[edge.ToNode] = altDist;
                    prevEdge[edge.ToNode] = edge;
                    priorityQueue.Enqueue(edge.ToNode, altDist);
                }
            }
        }

        if (!dist.ContainsKey(request.DestinationNode))
        {
            return new PaymentRouteResult(
                PaymentHash: request.PaymentHash,
                SourceNode: request.SourceNode,
                DestinationNode: request.DestinationNode,
                AmountTokens: request.PaymentAmountTokens,
                TotalFeeTokens: 0m,
                Hops: [],
                IsSuccessful: false,
                StatusMessage: "No routing path with sufficient liquidity capacity found."
            );
        }

        // Reconstruct path
        var hops = new List<MicroPaymentHop>();
        var curr = request.DestinationNode;
        decimal totalFee = 0m;

        while (prevEdge.TryGetValue(curr, out var edge))
        {
            hops.Add(new MicroPaymentHop(
                ChannelId: edge.ChannelId,
                FromNode: edge.FromNode,
                ToNode: edge.ToNode,
                AmountTokens: request.PaymentAmountTokens,
                FeeTokens: edge.Fee
            ));
            totalFee += edge.Fee;
            curr = edge.FromNode;
        }

        hops.Reverse();

        if (totalFee > request.MaxAllowedFeeTokens)
        {
            return new PaymentRouteResult(
                PaymentHash: request.PaymentHash,
                SourceNode: request.SourceNode,
                DestinationNode: request.DestinationNode,
                AmountTokens: request.PaymentAmountTokens,
                TotalFeeTokens: totalFee,
                Hops: hops,
                IsSuccessful: false,
                StatusMessage: $"Routing fee ({totalFee} tokens) exceeds maximum allowance ({request.MaxAllowedFeeTokens} tokens)."
            );
        }

        return new PaymentRouteResult(
            PaymentHash: request.PaymentHash,
            SourceNode: request.SourceNode,
            DestinationNode: request.DestinationNode,
            AmountTokens: request.PaymentAmountTokens,
            TotalFeeTokens: totalFee,
            Hops: hops,
            IsSuccessful: true,
            StatusMessage: $"Payment successfully routed through {hops.Count} hop(s)."
        );
    }
}
