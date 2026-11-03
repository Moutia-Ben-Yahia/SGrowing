using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;

namespace SGrowing.Api.Modules.Sales.RfpResponse;

public sealed class RfpResponseEngine
{
    private readonly ConcurrentDictionary<string, RfpQuestionnaireRecord> _questionnaires = new();
    private readonly ConcurrentBag<RfpAnswerRecord> _answers = new();

    public RfpResponseEngine()
    {
        InitializeDefaultData();
    }

    private void InitializeDefaultData()
    {
        var q1 = new RfpQuestionnaireRecord(
            "RFP-SEC-901",
            "Global Morgan Stanley Tech Infrastructure",
            "SECURITY_SOC2_ISO",
            120,
            115,
            96.4,
            "READY_FOR_REVIEW"
        );

        var q2 = new RfpQuestionnaireRecord(
            "RFP-COM-902",
            "Nordic Health System Telephony",
            "TECHNICAL_ARCHITECTURE",
            75,
            68,
            94.8,
            "IN_PROGRESS"
        );

        _questionnaires[q1.QuestionnaireId] = q1;
        _questionnaires[q2.QuestionnaireId] = q2;

        var a1 = new RfpAnswerRecord(
            "ANS-SOC2-001",
            q1.QuestionnaireId,
            "Describe your data encryption posture at rest and in transit across multi-cloud environments.",
            "SGrowing employs AES-256-GCM encryption at rest with customer-managed KMS keys, and TLS 1.3 with mandatory ephemeral mTLS for all inter-pod Kubernetes traffic verified by eBPF ring buffer filters.",
            0.985,
            "SOC2 Type II Report Section 4.2 & ISO/IEC 27001:2022 Annex A.8.24",
            true
        );

        var a2 = new RfpAnswerRecord(
            "ANS-RES-002",
            q2.QuestionnaireId,
            "How does your AI voice agent maintain compliance with statutory patient confidentiality and call recording mandates?",
            "Real-time dual-duplex stream scrubbing redacts all protected healthcare identifiers (PHI) before speech-to-text tokenization, storing cryptographic verification digests only.",
            0.972,
            "Healthcare Information Trust Alliance (HITRUST) CSF v11.2 Certification",
            true
        );

        _answers.Add(a1);
        _answers.Add(a2);
    }

    public RfpOverview GetOverview()
    {
        var qList = _questionnaires.Values.ToList();
        var aList = _answers.ToList();
        var totalQuestions = qList.Sum(q => q.TotalQuestionsCount);
        var avgConf = qList.Count > 0 ? qList.Average(q => q.ConfidenceScoreAvgPct) : 95.5;

        return new RfpOverview(
            qList.Count,
            totalQuestions,
            avgConf,
            184.5,
            qList,
            aList
        );
    }

    public RfpAnswerRecord GenerateAnswer(GenerateRfpAnswerRequest request)
    {
        var answerId = $"ANS-{Guid.NewGuid():N}"[..10].ToUpperInvariant();
        var synthesis = $"SGrowing implements enterprise-grade compliance for '{request.QuestionPrompt}', verified via automated zero-trust attestations and audited controls.";

        var answer = new RfpAnswerRecord(
            answerId,
            request.QuestionnaireId,
            request.QuestionPrompt,
            synthesis,
            0.978,
            "Corporate Compliance Matrix & Architecture Whitepaper 2026",
            true
        );

        _answers.Add(answer);
        return answer;
    }

    public RfpQuestionnaireRecord ImportQuestionnaire(ImportQuestionnaireRequest request)
    {
        var qId = $"RFP-{Guid.NewGuid():N}"[..10].ToUpperInvariant();
        var record = new RfpQuestionnaireRecord(
            qId,
            request.EnterpriseAccountName,
            request.QuestionnaireType,
            request.QuestionsCount,
            0,
            96.0,
            "IN_PROGRESS"
        );

        _questionnaires[qId] = record;
        return record;
    }
}
