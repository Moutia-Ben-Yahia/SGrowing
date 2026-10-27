using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using SGrowing.Api.Data;
using SGrowing.Core.Entities;

namespace SGrowing.Api.Modules.Funnels;

public enum ChatbotNodeType
{
    Message = 1,
    UserInput = 2,
    DecisionSplitter = 3,
    Delay = 4,
    Webhook = 5,
    Script = 6
}

public record ChatbotFlowNodeDto(
    string NodeId,
    ChatbotNodeType NodeType,
    string Title,
    Dictionary<string, object> Parameters,
    List<string> NextNodeIds
);

public record ChatbotFlowDefinitionDto(
    Guid FlowId,
    string Name,
    string Channel,
    List<ChatbotFlowNodeDto> Nodes,
    string EntryNodeId
);

public record FlowExecutionResultDto(
    Guid FlowId,
    string CurrentNodeId,
    bool IsCompleted,
    bool IsWaitingForUserInput,
    Dictionary<string, object> Variables,
    List<string> DispatchedMessages,
    string? WaitingInputPrompt = null
);

public record SocialCommentEventDto(
    string Platform, // "Instagram", "Facebook", "TikTok"
    string PostId,
    string CommentId,
    string CommenterExternalId,
    string CommenterName,
    string CommentText
);

public record SocialCommentReactionDto(
    string PublicReplyComment,
    bool InitiatedPrivateDm,
    string? PrivateDmInitialMessage,
    Guid? TriggeredFlowId
);

public interface IChatbotFlowEngineService
{
    Task<ChatbotFlowDefinitionDto> RegisterFlowAsync(Guid tenantId, string name, string channel, List<ChatbotFlowNodeDto> nodes, string entryNodeId);
    Task<FlowExecutionResultDto> ExecuteFlowStepAsync(Guid tenantId, Guid flowId, string? currentNodeId, Dictionary<string, object> inputVariables, string? userResponse = null);
    Task<SocialCommentReactionDto> ProcessSocialCommentTriggerAsync(Guid tenantId, SocialCommentEventDto commentEvent);
}

public class ChatbotFlowEngineService : IChatbotFlowEngineService
{
    private readonly SGrowingDbContext _db;
    private static readonly Dictionary<Guid, ChatbotFlowDefinitionDto> _flowRegistry = new();

    public ChatbotFlowEngineService(SGrowingDbContext db)
    {
        _db = db;
    }

    public Task<ChatbotFlowDefinitionDto> RegisterFlowAsync(Guid tenantId, string name, string channel, List<ChatbotFlowNodeDto> nodes, string entryNodeId)
    {
        if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("Flow name is required.", nameof(name));
        if (nodes == null || nodes.Count == 0) throw new ArgumentException("At least one node is required.", nameof(nodes));

        var flowId = Guid.NewGuid();
        var def = new ChatbotFlowDefinitionDto(flowId, name, channel, nodes, entryNodeId);
        _flowRegistry[flowId] = def;

        return Task.FromResult(def);
    }

    public Task<FlowExecutionResultDto> ExecuteFlowStepAsync(
        Guid tenantId,
        Guid flowId,
        string? currentNodeId,
        Dictionary<string, object> inputVariables,
        string? userResponse = null)
    {
        if (!_flowRegistry.TryGetValue(flowId, out var flow))
        {
            // Fallback default sample flow
            flow = new ChatbotFlowDefinitionDto(
                flowId,
                "Default Lead Qualification Flow",
                "WhatsApp",
                new List<ChatbotFlowNodeDto>
                {
                    new("node-1", ChatbotNodeType.Message, "Welcome Message", new Dictionary<string, object> { ["text"] = "Hello! Thanks for reaching out. Are you looking to scale your sales?" }, new List<string> { "node-2" }),
                    new("node-2", ChatbotNodeType.UserInput, "Capture Intent", new Dictionary<string, object> { ["prompt"] = "Reply YES to continue or NO to exit.", ["expectedType"] = "string" }, new List<string> { "node-3" }),
                    new("node-3", ChatbotNodeType.DecisionSplitter, "Check Intent", new Dictionary<string, object> { ["variable"] = "intent", ["expected"] = "YES" }, new List<string> { "node-4" }),
                    new("node-4", ChatbotNodeType.Message, "Qualification Success", new Dictionary<string, object> { ["text"] = "Awesome! Let's connect you with an SDR specialist." }, new List<string>())
                },
                "node-1"
            );
            _flowRegistry[flowId] = flow;
        }

        var variables = new Dictionary<string, object>(inputVariables);
        var dispatched = new List<string>();
        var activeNodeId = string.IsNullOrWhiteSpace(currentNodeId) ? flow.EntryNodeId : currentNodeId;

        // If resuming from a user input wait
        if (!string.IsNullOrWhiteSpace(userResponse))
        {
            variables["last_user_response"] = userResponse;
            var waitingNode = flow.Nodes.FirstOrDefault(n => n.NodeId == activeNodeId);
            if (waitingNode != null && waitingNode.NodeType == ChatbotNodeType.UserInput)
            {
                // Advance to next node
                if (waitingNode.NextNodeIds.Count > 0)
                {
                    activeNodeId = waitingNode.NextNodeIds[0];
                }
            }
        }

        while (true)
        {
            var node = flow.Nodes.FirstOrDefault(n => n.NodeId == activeNodeId);
            if (node == null)
            {
                return Task.FromResult(new FlowExecutionResultDto(flowId, activeNodeId, true, false, variables, dispatched));
            }

            switch (node.NodeType)
            {
                case ChatbotNodeType.Message:
                    var text = node.Parameters.TryGetValue("text", out var tVal) ? tVal?.ToString() ?? "" : "Default Bot Message";
                    // Mustache variable replacement
                    foreach (var kvp in variables)
                    {
                        text = text.Replace($"{{{{{kvp.Key}}}}}", kvp.Value?.ToString() ?? "");
                    }
                    dispatched.Add(text);

                    if (node.NextNodeIds.Count > 0)
                    {
                        activeNodeId = node.NextNodeIds[0];
                    }
                    else
                    {
                        return Task.FromResult(new FlowExecutionResultDto(flowId, activeNodeId, true, false, variables, dispatched));
                    }
                    break;

                case ChatbotNodeType.UserInput:
                    var prompt = node.Parameters.TryGetValue("prompt", out var pVal) ? pVal?.ToString() ?? "Please reply:" : "Please reply:";
                    return Task.FromResult(new FlowExecutionResultDto(flowId, node.NodeId, false, true, variables, dispatched, prompt));

                case ChatbotNodeType.DecisionSplitter:
                    var varKey = node.Parameters.TryGetValue("variable", out var vKey) ? vKey?.ToString() ?? "intent" : "intent";
                    var expected = node.Parameters.TryGetValue("expected", out var eVal) ? eVal?.ToString() ?? "" : "";
                    var actual = variables.TryGetValue(varKey, out var actVal) ? actVal?.ToString() ?? "" : "";
                    
                    // Simple condition match or fallback to last user response
                    if (actual.Equals(expected, StringComparison.OrdinalIgnoreCase) || 
                        (variables.TryGetValue("last_user_response", out var resp) && resp?.ToString()?.Contains(expected, StringComparison.OrdinalIgnoreCase) == true))
                    {
                        if (node.NextNodeIds.Count > 0) activeNodeId = node.NextNodeIds[0];
                        else return Task.FromResult(new FlowExecutionResultDto(flowId, activeNodeId, true, false, variables, dispatched));
                    }
                    else
                    {
                        // Fallback or second branch
                        if (node.NextNodeIds.Count > 1) activeNodeId = node.NextNodeIds[1];
                        else return Task.FromResult(new FlowExecutionResultDto(flowId, activeNodeId, true, false, variables, dispatched));
                    }
                    break;

                case ChatbotNodeType.Script:
                    // Simulated QuickJS sandbox transformation
                    variables["script_executed_at"] = DateTime.UtcNow.ToString("o");
                    variables["qualification_score"] = 85;
                    if (node.NextNodeIds.Count > 0) activeNodeId = node.NextNodeIds[0];
                    else return Task.FromResult(new FlowExecutionResultDto(flowId, activeNodeId, true, false, variables, dispatched));
                    break;

                case ChatbotNodeType.Delay:
                case ChatbotNodeType.Webhook:
                default:
                    if (node.NextNodeIds.Count > 0) activeNodeId = node.NextNodeIds[0];
                    else return Task.FromResult(new FlowExecutionResultDto(flowId, activeNodeId, true, false, variables, dispatched));
                    break;
            }
        }
    }

    public Task<SocialCommentReactionDto> ProcessSocialCommentTriggerAsync(Guid tenantId, SocialCommentEventDto commentEvent)
    {
        var text = commentEvent.CommentText.ToUpperInvariant();
        var keywords = new[] { "PRICE", "INFO", "BUY", "HOW", "DETAILS", "START" };

        var matched = keywords.Any(k => text.Contains(k));
        if (matched)
        {
            var publicReply = $"@{commentEvent.CommenterName} Sent you a private direct message with all details and pricing! Check your inbox 🚀";
            var dmInitial = $"Hi {commentEvent.CommenterName}! Thanks for your comment on our post. Here is the full product overview and special discount.";
            var flowId = Guid.NewGuid();

            return Task.FromResult(new SocialCommentReactionDto(publicReply, true, dmInitial, flowId));
        }

        return Task.FromResult(new SocialCommentReactionDto(
            $"@{commentEvent.CommenterName} Thanks for connecting with us!",
            false,
            null,
            null
        ));
    }
}
