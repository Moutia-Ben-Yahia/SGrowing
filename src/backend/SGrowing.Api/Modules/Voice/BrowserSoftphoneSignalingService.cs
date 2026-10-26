namespace SGrowing.Api.Modules.Voice;

public class SoftphoneConnectionConfig
{
    public string SessionToken { get; set; } = string.Empty;
    public string WebSocketUrl { get; set; } = string.Empty;
    public string[] IceServers { get; set; } = new[] { "stun:stun.l.google.com:19302" };
    public int SampleRate { get; set; } = 16000;
}

public class BrowserSoftphoneSignalingService
{
    public SoftphoneConnectionConfig CreateSessionConfig(Guid tenantId, Guid userId, string host = "api.sgrowing.com")
    {
        var token = "sp_tok_" + Guid.NewGuid().ToString("N");
        return new SoftphoneConnectionConfig
        {
            SessionToken = token,
            WebSocketUrl = $"wss://{host}/ws/voice/softphone?token={token}&tenantId={tenantId}&userId={userId}",
            IceServers = new[]
            {
                "stun:stun.l.google.com:19302",
                "stun:stun1.l.google.com:19302"
            },
            SampleRate = 16000
        };
    }
}
