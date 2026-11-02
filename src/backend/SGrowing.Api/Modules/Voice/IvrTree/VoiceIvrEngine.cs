using System.Collections.Concurrent;

namespace SGrowing.Api.Modules.Voice.IvrTree;

public interface IVoiceIvrEngine
{
    IReadOnlyList<IvrTree> ListTrees();
    IvrTree? GetTree(string treeId);
    SimulateIvrCallResponse SimulateCall(SimulateIvrCallRequest request);
    VoiceBroadcastCampaign CreateBroadcast(VoiceBroadcastCampaign campaign);
    VoiceBroadcastCampaign? GetBroadcast(string campaignId);
}

public sealed class VoiceIvrEngine : IVoiceIvrEngine
{
    private readonly ConcurrentDictionary<string, IvrTree> _trees = new();
    private readonly ConcurrentDictionary<string, VoiceBroadcastCampaign> _campaigns = new();

    public VoiceIvrEngine()
    {
        SeedDefaultTrees();
        SeedDefaultCampaigns();
    }

    public IReadOnlyList<IvrTree> ListTrees()
    {
        return _trees.Values.OrderByDescending(t => t.CreatedAtUtc).ToList();
    }

    public IvrTree? GetTree(string treeId)
    {
        _trees.TryGetValue(treeId, out var tree);
        return tree;
    }

    public SimulateIvrCallResponse SimulateCall(SimulateIvrCallRequest request)
    {
        var sessionId = "call_" + Guid.NewGuid().ToString("N")[..8];
        var log = new List<string>();

        if (!_trees.TryGetValue(request.TreeId, out var tree))
        {
            return new SimulateIvrCallResponse(
                sessionId,
                request.CallerNumber,
                "none",
                false,
                new[] { $"Error: Tree '{request.TreeId}' not found." }
            );
        }

        var nodeMap = tree.Nodes.ToDictionary(n => n.Id, n => n);
        if (!nodeMap.TryGetValue(tree.EntryNodeId, out var currentNode))
        {
            return new SimulateIvrCallResponse(
                sessionId,
                request.CallerNumber,
                "none",
                false,
                new[] { $"Error: Entry node '{tree.EntryNodeId}' not found." }
            );
        }

        log.Add($"[Call Connected] Caller {request.CallerNumber} entering tree '{tree.Name}'");
        log.Add($"[Node: {currentNode.Title}] Prompt: \"{currentNode.PromptText}\"");

        var inputIdx = 0;
        while (inputIdx < request.Inputs.Count && currentNode != null)
        {
            var input = request.Inputs[inputIdx++];
            log.Add($"[Caller Input] Received DTMF/Speech: '{input}'");

            if (currentNode.Routes.TryGetValue(input, out var nextNodeId) && nodeMap.TryGetValue(nextNodeId, out var nextNode))
            {
                currentNode = nextNode;
                log.Add($"[Route Match] Moving to node '{currentNode.Title}'");
                log.Add($"[Node: {currentNode.Title}] Prompt: \"{currentNode.PromptText}\"");
            }
            else if (!string.IsNullOrEmpty(currentNode.FallbackNodeId) && nodeMap.TryGetValue(currentNode.FallbackNodeId, out var fallbackNode))
            {
                currentNode = fallbackNode;
                log.Add($"[Fallback] Routing to fallback node '{currentNode.Title}'");
                log.Add($"[Node: {currentNode.Title}] Prompt: \"{currentNode.PromptText}\"");
            }
            else
            {
                log.Add($"[End of Branch] No further routes configured from '{currentNode.Title}'.");
                break;
            }

            if (currentNode.Type == "TransferToAgent" || currentNode.Type == "Voicemail")
            {
                log.Add($"[Terminal Action] Terminal node '{currentNode.Type}' reached.");
                break;
            }
        }

        log.Add($"[Call Disconnected] Session {sessionId} completed.");

        return new SimulateIvrCallResponse(
            SessionId: sessionId,
            CallerNumber: request.CallerNumber,
            FinalNodeId: currentNode?.Id ?? "unknown",
            IsCompleted: true,
            ExecutionLog: log
        );
    }

    public VoiceBroadcastCampaign CreateBroadcast(VoiceBroadcastCampaign campaign)
    {
        _campaigns[campaign.Id] = campaign;
        return campaign;
    }

    public VoiceBroadcastCampaign? GetBroadcast(string campaignId)
    {
        _campaigns.TryGetValue(campaignId, out var cmp);
        return cmp;
    }

    private void SeedDefaultTrees()
    {
        var node1 = new IvrNode("node_welcome", "Welcome & Language Gate", "MenuPrompt", "Thanks for calling SGrowing Enterprise. Press 1 for Sales, 2 for Technical Support.", new[] { "1", "2" }, new Dictionary<string, string> { { "1", "node_sales" }, { "2", "node_support" } }, "node_support");
        var node2 = new IvrNode("node_sales", "Sales AI SDR Qualifier", "SpeechRecognition", "Connecting you to our Autonomous SDR. What is your company ARR range?", Array.Empty<string>(), new Dictionary<string, string> { { "Enterprise", "node_transfer_ae" } }, "node_transfer_ae");
        var node3 = new IvrNode("node_support", "Tier 2 Support Queue", "Voicemail", "All support engineers are assisting customers. Leave your message after the tone.", Array.Empty<string>(), new Dictionary<string, string>(), null);
        var node4 = new IvrNode("node_transfer_ae", "Direct Account Executive Bridge", "TransferToAgent", "Bridging call to Senior Enterprise AE in San Francisco.", Array.Empty<string>(), new Dictionary<string, string>(), null);

        var tree = new IvrTree("tree_enterprise_main", "Global Enterprise Inbound Ingestion", "Active", "node_welcome", new[] { node1, node2, node3, node4 }, DateTime.UtcNow);
        _trees[tree.Id] = tree;
    }

    private void SeedDefaultCampaigns()
    {
        var cmp = new VoiceBroadcastCampaign("cmp_q4_outbound", "Q4 Enterprise VIP Summit Invitation", "+1-800-555-0199", 500, 25, true, "Running", 382, 41);
        _campaigns[cmp.Id] = cmp;
    }
}
