namespace MarketingStudio.Api.Modules.Outbound;

public enum ReplySentiment
{
    Neutral = 1,
    Interested = 2,
    Unsubscribe = 3,
    OutOfOffice = 4
}

public class ReplySentimentClassifier
{
    public ReplySentiment Classify(string messageContent)
    {
        if (string.IsNullOrWhiteSpace(messageContent)) return ReplySentiment.Neutral;

        var text = messageContent.ToLowerInvariant();

        // 1. Check Unsubscribe / Opt-out
        if (text.Contains("unsubscribe") || text.Contains("remove me") || text.Contains("stop") || text.Contains("not interested") || text.Contains("opt out"))
        {
            return ReplySentiment.Unsubscribe;
        }

        // 2. Check Out of Office
        if (text.Contains("out of the office") || text.Contains("out of office") || text.Contains("annual leave") || text.Contains("auto-reply") || text.Contains("vacation"))
        {
            return ReplySentiment.OutOfOffice;
        }

        // 3. Check Positive / Interested
        if (text.Contains("demo") || text.Contains("call") || text.Contains("schedule") || text.Contains("pricing") || text.Contains("interested") || text.Contains("send more info") || text.Contains("times"))
        {
            return ReplySentiment.Interested;
        }

        return ReplySentiment.Neutral;
    }
}
