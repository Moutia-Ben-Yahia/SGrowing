using System;
using System.Collections.Generic;

namespace SGrowing.Api.Modules.Voice.CallTransferMesh;

public enum CallTransferState
{
    AiHandling,
    AgentPrewarming,
    ThreeWayBridge,
    TransferCompleted,
    CallEnded
}

public record LiveCallSession(
    string CallId,
    string CallerPhoneNumber,
    string CustomerName,
    string IntentClassification,
    double CallerSentimentScore,
    string AssignedAiPersona,
    string? TargetHumanSpecialist,
    CallTransferState State,
    DateTime StartedAtUtc
);

public record InitiateWarmTransferRequest(
    string CallId,
    string TargetHumanSpecialist
);

public record CompleteTransferRequest(
    string CallId
);
