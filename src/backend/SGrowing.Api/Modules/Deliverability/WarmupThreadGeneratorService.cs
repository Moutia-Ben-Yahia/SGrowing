namespace SGrowing.Api.Modules.Deliverability;

public enum WarmupDialogueTopic
{
    ProductInquiry,
    PartnershipProposal,
    MeetingFollowUp,
    FeatureRequest,
    InvoiceBillingClarification,
    ConferenceNetworking
}

public record WarmupTurnMessage(
    int TurnIndex,
    string SenderEmail,
    string RecipientEmail,
    string Subject,
    string Body,
    string MessageId,
    string? InReplyTo,
    string? References,
    bool MarkAsImportant,
    bool StarMessage,
    bool RescueFromSpam,
    int EmulatedReadingDelaySeconds
);

public record WarmupDialogueThread(
    Guid ThreadId,
    WarmupDialogueTopic Topic,
    string ThreadSubject,
    List<WarmupTurnMessage> Turns
);

public interface IWarmupThreadGeneratorService
{
    WarmupDialogueThread GenerateThread(
        string senderEmail,
        string peerEmail,
        WarmupDialogueTopic topic,
        int turnsCount = 4,
        int? seed = null);
}

public class WarmupThreadGeneratorService : IWarmupThreadGeneratorService
{
    public WarmupDialogueThread GenerateThread(
        string senderEmail,
        string peerEmail,
        WarmupDialogueTopic topic,
        int turnsCount = 4,
        int? seed = null)
    {
        var random = seed.HasValue ? new Random(seed.Value) : new Random();
        var threadId = Guid.NewGuid();
        turnsCount = Math.Clamp(turnsCount, 2, 8);

        var (subject, turnTemplates) = GetDialogueScript(topic);
        var turns = new List<WarmupTurnMessage>();

        string? previousMessageId = null;
        string? allReferences = null;

        for (int i = 0; i < turnsCount; i++)
        {
            bool isSenderToPeer = (i % 2 == 0);
            var from = isSenderToPeer ? senderEmail : peerEmail;
            var to = isSenderToPeer ? peerEmail : senderEmail;

            var fromDomain = from.Contains('@') ? from.Split('@')[1] : "sgrowing.io";
            var fromUser = from.Contains('@') ? from.Split('@')[0] : from;
            var toUser = to.Contains('@') ? to.Split('@')[0] : to;

            var turnSubject = i == 0 ? subject : $"Re: {subject}";
            var messageId = $"<{Guid.NewGuid():N}.sgrowing-warmup@{fromDomain}>";

            var scriptBody = turnTemplates[i % turnTemplates.Length];
            var personalizedBody = scriptBody
                .Replace("{{SenderName}}", fromUser)
                .Replace("{{RecipientName}}", toUser);

            // In warmup swarms, the first incoming response turn (i == 1) or later guarantees engagement
            bool markImportant = (i >= 1) && (i == 1 || random.Next(100) < 80);
            bool star = (i == 1) && (random.Next(100) < 50);
            bool rescueSpam = (i == 0) && (random.Next(100) < 30);
            int delaySeconds = random.Next(30, 240);

            turns.Add(new WarmupTurnMessage(
                TurnIndex: i + 1,
                SenderEmail: from,
                RecipientEmail: to,
                Subject: turnSubject,
                Body: personalizedBody,
                MessageId: messageId,
                InReplyTo: previousMessageId,
                References: allReferences,
                MarkAsImportant: markImportant,
                StarMessage: star,
                RescueFromSpam: rescueSpam,
                EmulatedReadingDelaySeconds: delaySeconds
            ));

            allReferences = string.IsNullOrEmpty(allReferences) ? messageId : $"{allReferences} {messageId}";
            previousMessageId = messageId;
        }

        return new WarmupDialogueThread(threadId, topic, subject, turns);
    }

    private static (string Subject, string[] Turns) GetDialogueScript(WarmupDialogueTopic topic)
    {
        return topic switch
        {
            WarmupDialogueTopic.ProductInquiry => (
                "Quick inquiry regarding your enterprise platform API limits",
                [
                    "Hi {{RecipientName}},\n\nI was reviewing your documentation regarding rate limits on high-volume webhooks. Could you clarify if the 5,000 req/min quota is per tenant or shared across cluster instances?\n\nThanks,\n{{SenderName}}",
                    "Hello {{RecipientName}},\n\nThanks for reaching out! The 5,000 req/min quota is isolated per tenant organization, with automatic burst buffering. Are you planning an upcoming migration or bulk load?\n\nBest regards,\n{{SenderName}}",
                    "Hi {{RecipientName}},\n\nYes, exactly. We are migrating our event streams early next quarter. Would it be possible to schedule a brief 15-minute alignment call with your technical architect?\n\nBest,\n{{SenderName}}",
                    "Certainly {{RecipientName}}! I have open slots this Thursday at 2 PM UTC or Friday at 10 AM UTC. Feel free to send an invite for whichever suits you best.\n\nCheers,\n{{SenderName}}"
                ]
            ),
            WarmupDialogueTopic.PartnershipProposal => (
                "Exploring strategic co-marketing integration between our products",
                [
                    "Dear {{RecipientName}},\n\nI noticed our shared mutual customers frequently connect our CRM pipelines. Would you be open to exploring a verified integration listing on our marketplace?\n\nRegards,\n{{SenderName}}",
                    "Hi {{RecipientName}},\n\nThat sounds very promising. We have been receiving customer requests for native sync. Do you have developer documentation on your OAuth flow?\n\nBest,\n{{SenderName}}",
                    "Hello {{RecipientName}},\n\nI just shared our private developer sandbox credentials to your team inbox. Please review the schema and let me know your thoughts.\n\nThanks,\n{{SenderName}}",
                    "Great! Our integrations team is reviewing the endpoints today. Let's touch base again on Monday with our feedback.\n\nHave a great weekend,\n{{SenderName}}"
                ]
            ),
            WarmupDialogueTopic.InvoiceBillingClarification => (
                "Update on Q2 invoice payment reconciliation",
                [
                    "Hi {{RecipientName}},\n\nCould you please confirm if invoice #INV-2026-8812 was processed with the updated VAT registration number?\n\nThank you,\n{{SenderName}}",
                    "Hi {{RecipientName}},\n\nYes, we received the amended remittance advice and updated your account records. The updated receipt has been issued.\n\nRegards,\n{{SenderName}}",
                    "Received, thank you for the swift turnaround! Everything matches our accounting ledger now.\n\nBest,\n{{SenderName}}",
                    "You're very welcome! Let us know if you need any other tax certificates for the upcoming audit.\n\nWarm regards,\n{{SenderName}}"
                ]
            ),
            _ => (
                "Follow-up on recent discussion regarding deliverability best practices",
                [
                    "Hi {{RecipientName}},\n\nFollowing up on our exchange last week regarding custom domain DKIM alignment. Did you run the SPF record validation tool?\n\nBest,\n{{SenderName}}",
                    "Hello {{RecipientName}},\n\nYes, we verified the DMARC policy is currently set to quarantine with 100% enforcement. All test emails are landing cleanly in primary inbox.\n\nThanks,\n{{SenderName}}",
                    "Terrific news! That explains the consistent reputation score improvement over the past 14 days.\n\nBest,\n{{SenderName}}",
                    "Agreed! Thanks again for your guidance through the mailbox warming phase.\n\nCheers,\n{{SenderName}}"
                ]
            )
        };
    }
}
