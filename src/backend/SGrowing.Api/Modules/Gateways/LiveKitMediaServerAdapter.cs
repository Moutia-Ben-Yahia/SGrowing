using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;

namespace SGrowing.Api.Modules.Gateways;

public interface ILiveKitMediaClient
{
    string GenerateWebRtcRoomToken(string roomName, string participantIdentity, string apiKey, string apiSecret, TimeSpan? validDuration = null);
}

public class LiveKitMediaServerAdapter : ILiveKitMediaClient
{
    public string GenerateWebRtcRoomToken(
        string roomName, 
        string participantIdentity, 
        string apiKey, 
        string apiSecret, 
        TimeSpan? validDuration = null)
    {
        var duration = validDuration ?? TimeSpan.FromHours(4);
        var keyBytes = Encoding.UTF8.GetBytes(apiSecret);
        var securityKey = new SymmetricSecurityKey(keyBytes);
        var credentials = new SigningCredentials(securityKey, SecurityAlgorithms.HmacSha256);

        var tokenHandler = new JwtSecurityTokenHandler();
        var tokenDescriptor = new SecurityTokenDescriptor
        {
            Issuer = apiKey,
            Subject = new ClaimsIdentity(new[]
            {
                new Claim(JwtRegisteredClaimNames.Sub, participantIdentity),
                new Claim("video", System.Text.Json.JsonSerializer.Serialize(new
                {
                    room = roomName,
                    roomJoin = true,
                    canPublish = true,
                    canSubscribe = true,
                    canPublishData = true
                }))
            }),
            Expires = DateTime.UtcNow.Add(duration),
            SigningCredentials = credentials
        };

        var token = tokenHandler.CreateToken(tokenDescriptor);
        return tokenHandler.WriteToken(token);
    }
}
