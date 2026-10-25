namespace SGrowing.Core.Channels;

public interface IChannelAdapter
{
    string ChannelType { get; }
    InboundChannelMessage? ParseInboundWebhook(Guid tenantId, string rawJson, IDictionary<string, string>? headers = null);
}
