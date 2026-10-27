using System.Net.Sockets;
using System.Text;

namespace SGrowing.Api.Modules.Gateways;

public record SmtpDiagnosticResultDto(
    string Host,
    int Port,
    bool IsReachable,
    long LatencyMs,
    string? BannerResponse,
    bool SupportsStartTls,
    string? ErrorMessage
);

public interface ISmtpSocketClient
{
    Task<SmtpDiagnosticResultDto> ProbeSmtpServerAsync(string host, int port = 587, CancellationToken ct = default);
}

public class SmtpSocketClientAdapter : ISmtpSocketClient
{
    public async Task<SmtpDiagnosticResultDto> ProbeSmtpServerAsync(string host, int port = 587, CancellationToken ct = default)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        try
        {
            using var tcpClient = new TcpClient();
            var connectTask = tcpClient.ConnectAsync(host, port);
            var completed = await Task.WhenAny(connectTask, Task.Delay(3000, ct));

            if (completed != connectTask || !tcpClient.Connected)
            {
                return new SmtpDiagnosticResultDto(
                    host,
                    port,
                    false,
                    sw.ElapsedMilliseconds,
                    null,
                    false,
                    "TCP Connection timed out or refused."
                );
            }

            sw.Stop();
            using var stream = tcpClient.GetStream();
            stream.ReadTimeout = 2000;

            var buffer = new byte[1024];
            var bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length, ct);
            var banner = Encoding.ASCII.GetString(buffer, 0, bytesRead).Trim();

            // Send EHLO
            var ehloCmd = Encoding.ASCII.GetBytes($"EHLO sgrowing.io\r\n");
            await stream.WriteAsync(ehloCmd, 0, ehloCmd.Length, ct);

            bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length, ct);
            var ehloResponse = Encoding.ASCII.GetString(buffer, 0, bytesRead);
            var supportsStartTls = ehloResponse.Contains("STARTTLS", StringComparison.OrdinalIgnoreCase);

            return new SmtpDiagnosticResultDto(
                host,
                port,
                true,
                sw.ElapsedMilliseconds,
                banner,
                supportsStartTls,
                null
            );
        }
        catch (Exception ex)
        {
            return new SmtpDiagnosticResultDto(
                host,
                port,
                false,
                sw.ElapsedMilliseconds,
                null,
                false,
                ex.Message
            );
        }
    }
}
