using System.Text.Json;
using System.Text.RegularExpressions;
using SGrowing.Core.Entities;

namespace SGrowing.Api.Modules.Automation;

public class BotSimulatorService : IBotSimulatorService
{
    private static readonly Regex VariableRegex = new(@"\{\{\s*([a-zA-Z0-9_.]+)\s*\}\}", RegexOptions.Compiled);

    public BotSimulationSession StartSimulation(
        WorkflowGraph workflow,
        Dictionary<string, object>? initialVariables = null)
    {
        var session = new BotSimulationSession(
            SessionId: Guid.NewGuid(),
            WorkflowId: workflow.Id,
            CurrentNodeId: null,
            Variables: initialVariables != null ? new Dictionary<string, object>(initialVariables, StringComparer.OrdinalIgnoreCase) : new(StringComparer.OrdinalIgnoreCase),
            Transcript: [],
            IsWaitingForUserInput: false,
            IsHandedOverToHuman: false,
            IsCompleted: false,
            TotalStepsExecuted: 0
        );

        // Run until first pause/wait for user input or completion
        var initialStep = SimulateUserMessage(workflow, session, userMessage: null);
        return new BotSimulationSession(
            SessionId: session.SessionId,
            WorkflowId: workflow.Id,
            CurrentNodeId: initialStep.CurrentNodeId,
            Variables: initialStep.UpdatedVariables,
            Transcript: session.Transcript,
            IsWaitingForUserInput: initialStep.IsWaitingForUserInput,
            IsHandedOverToHuman: initialStep.IsHandedOverToHuman,
            IsCompleted: initialStep.IsCompleted,
            TotalStepsExecuted: session.Transcript.Count
        );
    }

    public BotSimulationStepResult SimulateUserMessage(
        WorkflowGraph workflow,
        BotSimulationSession session,
        string? userMessage = null)
    {
        var logs = new List<string>();
        var newMessages = new List<BotTranscriptMessage>();
        var state = new Dictionary<string, object>(session.Variables, StringComparer.OrdinalIgnoreCase);

        // Record user message if provided
        if (!string.IsNullOrWhiteSpace(userMessage))
        {
            state["last_user_message"] = userMessage;
            state["user_input"] = userMessage;
            var userMsg = new BotTranscriptMessage(
                Sender: "User",
                Content: userMessage,
                Cards: null,
                IsTypingIndicator: false,
                DelayMilliseconds: 0,
                TimestampUtc: DateTime.UtcNow
            );
            session.Transcript.Add(userMsg);
            newMessages.Add(userMsg);
            logs.Add($"[User Input] \"{userMessage}\"");
        }

        var nodes = ParseNodes(workflow.NodesJson);
        if (nodes.Count == 0)
        {
            return new BotSimulationStepResult(
                SessionId: session.SessionId,
                CurrentNodeId: null,
                NewMessages: newMessages,
                UpdatedVariables: state,
                IsWaitingForUserInput: false,
                IsHandedOverToHuman: false,
                IsCompleted: true,
                ExecutionTraceLogs: ["Workflow contains no nodes."]
            );
        }

        var nodeMap = nodes.ToDictionary(n => n.Id, n => n, StringComparer.OrdinalIgnoreCase);

        // Determine starting node
        WorkflowNodeDefinition? currentNode = null;
        if (!string.IsNullOrEmpty(session.CurrentNodeId) && nodeMap.TryGetValue(session.CurrentNodeId, out var resumeNode))
        {
            // If we were waiting at a node and received user input, move to next node
            currentNode = GetNextNode(resumeNode, nodes, nodeMap);
        }
        else
        {
            currentNode = nodes.FirstOrDefault(n => IsTriggerNode(n.Type)) ?? nodes[0];
        }

        bool waitingForInput = false;
        bool handedOver = false;
        bool completed = false;
        int steps = 0;
        const int maxSteps = 100;

        while (currentNode != null && steps < maxSteps)
        {
            steps++;
            var nodeType = currentNode.Type.Trim();
            var nodeName = string.IsNullOrWhiteSpace(currentNode.Name) ? currentNode.Id : currentNode.Name;
            logs.Add($"[Executing Step] Node '{nodeName}' of type '{nodeType}'");

            if (IsConditionNode(nodeType))
            {
                bool isTrue = EvaluateCondition(currentNode, state);
                logs.Add($"[Condition '{nodeName}'] Evaluated to {isTrue}");
                var nextId = isTrue ? currentNode.TrueNext : currentNode.FalseNext;
                currentNode = (!string.IsNullOrEmpty(nextId) && nodeMap.TryGetValue(nextId, out var nextNode)) ? nextNode : null;
            }
            else if (IsSendMessageNode(nodeType))
            {
                var text = RenderTemplate(GetStringConfig(currentNode.Config, "message", "text", "content") ?? "Hello from SGrowing AI Bot!", state);
                var botMsg = new BotTranscriptMessage(
                    Sender: "Bot",
                    Content: text,
                    Cards: null,
                    IsTypingIndicator: false,
                    DelayMilliseconds: null,
                    TimestampUtc: DateTime.UtcNow
                );
                session.Transcript.Add(botMsg);
                newMessages.Add(botMsg);

                // Check if node is prompt waiting for input
                bool waitForInput = GetBoolConfig(currentNode.Config, "waitForReply", "waitForInput", "prompt");
                if (waitForInput)
                {
                    waitingForInput = true;
                    break;
                }

                currentNode = GetNextNode(currentNode, nodes, nodeMap);
            }
            else if (IsCarouselNode(nodeType))
            {
                var text = RenderTemplate(GetStringConfig(currentNode.Config, "message", "title") ?? "Featured Options:", state);
                var cards = ExtractCarouselCards(currentNode.Config, state);
                var botMsg = new BotTranscriptMessage(
                    Sender: "Bot",
                    Content: text,
                    Cards: cards,
                    IsTypingIndicator: false,
                    DelayMilliseconds: null,
                    TimestampUtc: DateTime.UtcNow
                );
                session.Transcript.Add(botMsg);
                newMessages.Add(botMsg);
                logs.Add($"[Carousel '{nodeName}'] Dispatched {cards.Count} interactive cards");

                waitingForInput = true;
                break;
            }
            else if (IsDelayNode(nodeType))
            {
                int delaySec = GetIntConfig(currentNode.Config, "delaySeconds", "duration", "seconds");
                if (delaySec <= 0) delaySec = 2;
                var typingMsg = new BotTranscriptMessage(
                    Sender: "Bot",
                    Content: "...",
                    Cards: null,
                    IsTypingIndicator: true,
                    DelayMilliseconds: delaySec * 1000,
                    TimestampUtc: DateTime.UtcNow
                );
                session.Transcript.Add(typingMsg);
                newMessages.Add(typingMsg);
                logs.Add($"[Typing Delay '{nodeName}'] Emulated {delaySec}s typing bubble");

                currentNode = GetNextNode(currentNode, nodes, nodeMap);
            }
            else if (IsWebhookNode(nodeType))
            {
                ExecuteWebhookWithExtraction(currentNode, state, logs);
                currentNode = GetNextNode(currentNode, nodes, nodeMap);
            }
            else if (IsHumanTakeoverNode(nodeType))
            {
                handedOver = true;
                state["bot_status"] = "HandedOver";
                var handoverMsg = new BotTranscriptMessage(
                    Sender: "Bot",
                    Content: "Connecting you with a live customer care agent...",
                    Cards: null,
                    IsTypingIndicator: false,
                    DelayMilliseconds: 500,
                    TimestampUtc: DateTime.UtcNow
                );
                session.Transcript.Add(handoverMsg);
                newMessages.Add(handoverMsg);
                logs.Add($"[Human Takeover '{nodeName}'] Escalated conversation to human agent queue.");
                break;
            }
            else if (IsTaggingNode(nodeType))
            {
                ExecuteTagging(currentNode, state, logs);
                currentNode = GetNextNode(currentNode, nodes, nodeMap);
            }
            else
            {
                // Generic node
                currentNode = GetNextNode(currentNode, nodes, nodeMap);
            }
        }

        if (currentNode == null && !waitingForInput && !handedOver)
        {
            completed = true;
            logs.Add("[Workflow Completed] Reached terminal node.");
        }

        return new BotSimulationStepResult(
            SessionId: session.SessionId,
            CurrentNodeId: currentNode?.Id,
            NewMessages: newMessages,
            UpdatedVariables: state,
            IsWaitingForUserInput: waitingForInput,
            IsHandedOverToHuman: handedOver,
            IsCompleted: completed,
            ExecutionTraceLogs: logs
        );
    }

    private static void ExecuteWebhookWithExtraction(
        WorkflowNodeDefinition node,
        Dictionary<string, object> state,
        List<string> logs)
    {
        var url = GetStringConfig(node.Config, "url", "endpoint") ?? "https://api.sgrowing.io/data";
        var mockResponseJson = GetStringConfig(node.Config, "mockResponse", "responseJson", "payload");

        logs.Add($"[Webhook Call] Invoked {url}");

        // If mock response or JSON provided, extract variables via dot-path expressions
        if (!string.IsNullOrWhiteSpace(mockResponseJson))
        {
            try
            {
                using var doc = JsonDocument.Parse(mockResponseJson);
                var root = doc.RootElement;

                // Check for extract mappings e.g. {"balance": "data.balance", "status": "status"}
                if (node.Config.TryGetValue("extractMappings", out var mappingsObj))
                {
                    if (mappingsObj is JsonElement mappingsElem && mappingsElem.ValueKind == JsonValueKind.Object)
                    {
                        foreach (var prop in mappingsElem.EnumerateObject())
                        {
                            var targetVar = prop.Name;
                            var jsonPath = prop.Value.GetString();
                            if (!string.IsNullOrEmpty(jsonPath))
                            {
                                var val = ExtractJsonPathValue(root, jsonPath);
                                if (val != null)
                                {
                                    state[targetVar] = val;
                                    logs.Add($"[JSONPath Extract] Saved {targetVar} = '{val}'");
                                }
                            }
                        }
                    }
                    else if (mappingsObj is IDictionary<string, object> dict)
                    {
                        foreach (var (targetVar, jsonPathObj) in dict)
                        {
                            var jsonPath = jsonPathObj?.ToString();
                            if (!string.IsNullOrEmpty(jsonPath))
                            {
                                var val = ExtractJsonPathValue(root, jsonPath);
                                if (val != null)
                                {
                                    state[targetVar] = val;
                                    logs.Add($"[JSONPath Extract] Saved {targetVar} = '{val}'");
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                logs.Add($"[JSONPath Extract Error] Failed to parse response: {ex.Message}");
            }
        }
    }

    public static string? ExtractJsonPathValue(JsonElement root, string path)
    {
        var cleanPath = path.TrimStart('$', '.');
        var parts = cleanPath.Split('.');

        JsonElement current = root;
        foreach (var part in parts)
        {
            if (current.ValueKind == JsonValueKind.Object && current.TryGetProperty(part, out var child))
            {
                current = child;
            }
            else
            {
                return null;
            }
        }

        return current.ValueKind switch
        {
            JsonValueKind.String => current.GetString(),
            JsonValueKind.Number => current.GetRawText(),
            JsonValueKind.True => "true",
            JsonValueKind.False => "false",
            _ => current.GetRawText()
        };
    }

    private static void ExecuteTagging(
        WorkflowNodeDefinition node,
        Dictionary<string, object> state,
        List<string> logs)
    {
        var tag = GetStringConfig(node.Config, "tag", "tagName");
        var action = (GetStringConfig(node.Config, "action") ?? "Add").ToLowerInvariant();

        if (!state.TryGetValue("tags", out var tagsObj) || tagsObj is not List<string> tags)
        {
            tags = new List<string>();
            state["tags"] = tags;
        }

        if (!string.IsNullOrWhiteSpace(tag))
        {
            if (action == "remove")
            {
                tags.Remove(tag);
                logs.Add($"[Tagging] Removed tag '{tag}'");
            }
            else
            {
                if (!tags.Contains(tag)) tags.Add(tag);
                logs.Add($"[Tagging] Added tag '{tag}'");
            }
        }
    }

    private static List<BotCarouselCard> ExtractCarouselCards(Dictionary<string, object> config, Dictionary<string, object> state)
    {
        var list = new List<BotCarouselCard>();

        if (config.TryGetValue("cards", out var cardsObj) && cardsObj is JsonElement cardsElem && cardsElem.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in cardsElem.EnumerateArray())
            {
                var title = item.TryGetProperty("title", out var t) ? RenderTemplate(t.GetString() ?? "", state) : "Option";
                var subtitle = item.TryGetProperty("subtitle", out var s) ? RenderTemplate(s.GetString() ?? "", state) : null;
                var img = item.TryGetProperty("imageUrl", out var i) ? i.GetString() : null;

                var buttons = new List<BotCardButton>();
                if (item.TryGetProperty("buttons", out var btns) && btns.ValueKind == JsonValueKind.Array)
                {
                    foreach (var b in btns.EnumerateArray())
                    {
                        var btnTitle = b.TryGetProperty("title", out var bt) ? bt.GetString() ?? "Select" : "Select";
                        var btnType = b.TryGetProperty("type", out var bp) ? bp.GetString() ?? "Postback" : "Postback";
                        var btnVal = b.TryGetProperty("value", out var bv) ? bv.GetString() ?? "" : "";
                        buttons.Add(new BotCardButton(btnTitle, btnType, btnVal));
                    }
                }

                list.Add(new BotCarouselCard(title, subtitle, img, buttons));
            }
        }

        if (list.Count == 0)
        {
            list.Add(new BotCarouselCard(
                Title: "Standard Growth Plan",
                Subtitle: "50,000 cold outreach credits & multichannel inbox",
                ImageUrl: "https://sgrowing.io/assets/growth-card.png",
                Buttons: [new BotCardButton("Choose Growth", "Postback", "plan_growth")]
            ));
            list.Add(new BotCarouselCard(
                Title: "Enterprise Scale Plan",
                Subtitle: "Unlimited warmup swarm & dedicated deliverability pool",
                ImageUrl: "https://sgrowing.io/assets/enterprise-card.png",
                Buttons: [new BotCardButton("Choose Enterprise", "Postback", "plan_enterprise")]
            ));
        }

        return list;
    }

    private static bool EvaluateCondition(WorkflowNodeDefinition node, Dictionary<string, object> state)
    {
        var field = GetStringConfig(node.Config, "field", "property", "key");
        var op = GetStringConfig(node.Config, "operator", "op") ?? "==";
        var expected = GetStringConfig(node.Config, "value", "expectedValue");

        if (string.IsNullOrEmpty(field) || !state.TryGetValue(field, out var actualObj))
        {
            // Check fallback "contains" on last user message
            if (state.TryGetValue("last_user_message", out var msgObj) && msgObj != null && !string.IsNullOrEmpty(expected))
            {
                return msgObj.ToString()!.Contains(expected, StringComparison.OrdinalIgnoreCase);
            }
            return false;
        }

        if (actualObj is IEnumerable<string> strCollection)
        {
            bool hasItem = strCollection.Any(x => x.Equals(expected, StringComparison.OrdinalIgnoreCase));
            return op switch
            {
                "contains" or "==" or "=" => hasItem,
                "!=" => !hasItem,
                _ => hasItem
            };
        }

        if (actualObj is JsonElement jsonElem)
        {
            var strVal = jsonElem.ValueKind == JsonValueKind.String ? jsonElem.GetString() : jsonElem.GetRawText();
            return op switch
            {
                "==" or "=" => string.Equals(strVal, expected, StringComparison.OrdinalIgnoreCase),
                "!=" => !string.Equals(strVal, expected, StringComparison.OrdinalIgnoreCase),
                "contains" => strVal != null && strVal.Contains(expected ?? "", StringComparison.OrdinalIgnoreCase),
                _ => string.Equals(strVal, expected, StringComparison.OrdinalIgnoreCase)
            };
        }

        var actual = actualObj?.ToString() ?? "";
        return op switch
        {
            "==" or "=" => actual.Equals(expected, StringComparison.OrdinalIgnoreCase),
            "!=" => !actual.Equals(expected, StringComparison.OrdinalIgnoreCase),
            "contains" => actual.Contains(expected ?? "", StringComparison.OrdinalIgnoreCase),
            _ => actual.Equals(expected, StringComparison.OrdinalIgnoreCase)
        };
    }

    private static WorkflowNodeDefinition? GetNextNode(
        WorkflowNodeDefinition current,
        List<WorkflowNodeDefinition> list,
        Dictionary<string, WorkflowNodeDefinition> map)
    {
        if (!string.IsNullOrEmpty(current.Next) && map.TryGetValue(current.Next, out var next))
        {
            return next;
        }

        var idx = list.IndexOf(current);
        if (idx >= 0 && idx + 1 < list.Count)
        {
            return list[idx + 1];
        }

        return null;
    }

    private static List<WorkflowNodeDefinition> ParseNodes(string nodesJson)
    {
        if (string.IsNullOrWhiteSpace(nodesJson)) return [];
        try
        {
            return JsonSerializer.Deserialize<List<WorkflowNodeDefinition>>(nodesJson, new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? [];
        }
        catch
        {
            return [];
        }
    }

    private static string RenderTemplate(string template, Dictionary<string, object> state)
    {
        if (string.IsNullOrEmpty(template)) return string.Empty;
        return VariableRegex.Replace(template, m =>
        {
            var key = m.Groups[1].Value;
            return state.TryGetValue(key, out var val) && val != null ? val.ToString()! : m.Value;
        });
    }

    private static string? GetStringConfig(Dictionary<string, object> config, params string[] keys)
    {
        foreach (var k in keys)
        {
            if (config.TryGetValue(k, out var v) && v != null) return v.ToString();
        }
        return null;
    }

    private static int GetIntConfig(Dictionary<string, object> config, params string[] keys)
    {
        foreach (var k in keys)
        {
            if (config.TryGetValue(k, out var v) && int.TryParse(v?.ToString(), out int parsed)) return parsed;
        }
        return 0;
    }

    private static bool GetBoolConfig(Dictionary<string, object> config, params string[] keys)
    {
        foreach (var k in keys)
        {
            if (config.TryGetValue(k, out var v) && bool.TryParse(v?.ToString(), out bool parsed)) return parsed;
        }
        return false;
    }

    private static bool IsTriggerNode(string type) => type.Equals("Trigger", StringComparison.OrdinalIgnoreCase) || type.Equals("Start", StringComparison.OrdinalIgnoreCase);
    private static bool IsConditionNode(string type) => type.Equals("Condition", StringComparison.OrdinalIgnoreCase) || type.Equals("Decision", StringComparison.OrdinalIgnoreCase);
    private static bool IsSendMessageNode(string type) => type.Equals("SendMessage", StringComparison.OrdinalIgnoreCase) || type.Equals("Message", StringComparison.OrdinalIgnoreCase);
    private static bool IsCarouselNode(string type) => type.Equals("Carousel", StringComparison.OrdinalIgnoreCase) || type.Equals("Card", StringComparison.OrdinalIgnoreCase) || type.Equals("CarouselCard", StringComparison.OrdinalIgnoreCase);
    private static bool IsDelayNode(string type) => type.Equals("Delay", StringComparison.OrdinalIgnoreCase) || type.Equals("TimeDelay", StringComparison.OrdinalIgnoreCase) || type.Equals("Typing", StringComparison.OrdinalIgnoreCase);
    private static bool IsWebhookNode(string type) => type.Equals("Webhook", StringComparison.OrdinalIgnoreCase) || type.Equals("Action:Webhook", StringComparison.OrdinalIgnoreCase);
    private static bool IsHumanTakeoverNode(string type) => type.Equals("HumanTakeover", StringComparison.OrdinalIgnoreCase) || type.Equals("Handover", StringComparison.OrdinalIgnoreCase);
    private static bool IsTaggingNode(string type) => type.Equals("Tagging", StringComparison.OrdinalIgnoreCase) || type.Equals("Tag", StringComparison.OrdinalIgnoreCase);
}
