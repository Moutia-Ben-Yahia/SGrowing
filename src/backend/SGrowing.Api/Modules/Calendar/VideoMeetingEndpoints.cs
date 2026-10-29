using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace SGrowing.Api.Modules.Calendar;

public record JoinMeetingDto(
    string ParticipantIdentity,
    string ParticipantRole = "attendee",
    string? DisplayName = null,
    bool IsPublisher = true
);

public record UpdateMediaStateRequest(
    string ParticipantIdentity,
    ParticipantMediaStateDto State
);

public record AudioStreamChunkRequest(
    string SpeakerIdentity,
    string? AudioBase64 = null,
    byte[]? AudioBytes = null,
    string? TranscriptHint = null
);

public record EvaluateObjectionRequest(
    string TranscriptSegment
);

public record EndMeetingRequest(
    string SummaryNotes = ""
);

public record CreateRoomRequest(
    Guid? TenantId = null,
    string HostName = "",
    string AttendeeName = ""
);

public static class VideoMeetingEndpoints
{
    public static IEndpointRouteBuilder MapVideoMeetingEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var groups = new[]
        {
            endpoints.MapGroup("/api/v1/meetings").WithTags("Video Consultation & In-Call AI Copilot v1"),
            endpoints.MapGroup("/api/meetings").WithTags("Video Consultation & In-Call AI Copilot")
        };

        foreach (var group in groups)
        {
            // 1. GET Room Info & Verification
            group.MapGet("/rooms/{roomToken}", async (
                string roomToken,
                [FromQuery] Guid? tenantId,
                [FromQuery] string? hostName,
                [FromQuery] string? attendeeName,
                IVideoMeetingRoomService service) =>
            {
                try
                {
                    var room = await service.GetRoomInfoAsync(roomToken);
                    if (room == null)
                    {
                        // Attempt to resolve via DB booking or on-demand session
                        room = await service.CreateOrGetRoomAsync(
                            tenantId ?? Guid.Empty,
                            roomToken,
                            hostName ?? "",
                            attendeeName ?? ""
                        );
                    }
                    return Results.Ok(room);
                }
                catch (KeyNotFoundException ex)
                {
                    return Results.NotFound(new { error = ex.Message });
                }
                catch (InvalidOperationException ex)
                {
                    return Results.BadRequest(new { error = ex.Message });
                }
                catch (Exception ex)
                {
                    return Results.BadRequest(new { error = ex.Message });
                }
            });

            // Optional explicit room provisioning
            group.MapPost("/rooms/{roomToken}", async (
                string roomToken,
                [FromBody] CreateRoomRequest req,
                IVideoMeetingRoomService service) =>
            {
                try
                {
                    var room = await service.CreateOrGetRoomAsync(
                        req.TenantId ?? Guid.Empty,
                        roomToken,
                        req.HostName,
                        req.AttendeeName
                    );
                    return Results.Ok(room);
                }
                catch (Exception ex)
                {
                    return Results.BadRequest(new { error = ex.Message });
                }
            });

            // 2. POST Join Room & Mint WebRTC Access Token
            group.MapPost("/rooms/{roomToken}/join", async (
                string roomToken,
                [FromBody] JoinMeetingDto req,
                IVideoMeetingRoomService service) =>
            {
                try
                {
                    // Ensure room exists
                    var room = await service.GetRoomInfoAsync(roomToken);
                    if (room == null)
                    {
                        room = await service.CreateOrGetRoomAsync(
                            Guid.Empty,
                            roomToken,
                            hostName: req.ParticipantRole == "host" ? (req.DisplayName ?? req.ParticipantIdentity) : "",
                            attendeeName: req.ParticipantRole == "attendee" ? (req.DisplayName ?? req.ParticipantIdentity) : ""
                        );
                    }

                    var tokenResult = await service.MintWebRtcAccessTokenAsync(
                        roomToken,
                        req.ParticipantIdentity,
                        req.ParticipantRole,
                        req.IsPublisher
                    );

                    var currentRoom = await service.GetRoomInfoAsync(roomToken) ?? room;

                    return Results.Ok(new
                    {
                        token = tokenResult,
                        room = currentRoom
                    });
                }
                catch (KeyNotFoundException ex)
                {
                    return Results.NotFound(new { error = ex.Message });
                }
                catch (InvalidOperationException ex)
                {
                    return Results.BadRequest(new { error = ex.Message });
                }
                catch (Exception ex)
                {
                    return Results.BadRequest(new { error = ex.Message });
                }
            });

            // 3. POST Update Media State (Mic, Camera, Screen, Network Quality)
            group.MapPost("/rooms/{roomToken}/media-state", async (
                string roomToken,
                [FromBody] UpdateMediaStateRequest req,
                IVideoMeetingRoomService service) =>
            {
                try
                {
                    var updated = await service.UpdateParticipantStateAsync(
                        roomToken,
                        req.ParticipantIdentity,
                        req.State
                    );
                    return Results.Ok(updated);
                }
                catch (KeyNotFoundException ex)
                {
                    return Results.NotFound(new { error = ex.Message });
                }
                catch (InvalidOperationException ex)
                {
                    return Results.BadRequest(new { error = ex.Message });
                }
                catch (Exception ex)
                {
                    return Results.BadRequest(new { error = ex.Message });
                }
            });

            // 4. POST Stream Audio Chunk for Real-Time Speech Transcription
            group.MapPost("/rooms/{roomToken}/audio-stream", async (
                string roomToken,
                [FromBody] AudioStreamChunkRequest req,
                IVideoMeetingRoomService service) =>
            {
                try
                {
                    byte[] audioBytes = req.AudioBytes ?? Array.Empty<byte>();
                    if (audioBytes.Length == 0 && !string.IsNullOrWhiteSpace(req.AudioBase64))
                    {
                        try
                        {
                            audioBytes = Convert.FromBase64String(req.AudioBase64);
                        }
                        catch
                        {
                            audioBytes = Array.Empty<byte>();
                        }
                    }

                    var result = await service.IngestLiveCallAudioChunkAsync(
                        roomToken,
                        req.SpeakerIdentity,
                        audioBytes,
                        req.TranscriptHint
                    );

                    return Results.Ok(result);
                }
                catch (KeyNotFoundException ex)
                {
                    return Results.NotFound(new { error = ex.Message });
                }
                catch (InvalidOperationException ex)
                {
                    return Results.BadRequest(new { error = ex.Message });
                }
                catch (Exception ex)
                {
                    return Results.BadRequest(new { error = ex.Message });
                }
            });

            // 5. POST Evaluate Objections & Return Battle Cards
            group.MapPost("/rooms/{roomToken}/battle-cards", async (
                string roomToken,
                [FromBody] EvaluateObjectionRequest req,
                IVideoMeetingRoomService service) =>
            {
                try
                {
                    var result = await service.EvaluateInCallObjectionsAndBattleCardsAsync(
                        roomToken,
                        req.TranscriptSegment
                    );
                    return Results.Ok(result);
                }
                catch (Exception ex)
                {
                    return Results.BadRequest(new { error = ex.Message });
                }
            });

            // 6. POST End Meeting Room
            group.MapPost("/rooms/{roomToken}/end", async (
                string roomToken,
                [FromBody] EndMeetingRequest req,
                IVideoMeetingRoomService service) =>
            {
                try
                {
                    var summary = await service.EndMeetingRoomAsync(
                        roomToken,
                        req.SummaryNotes
                    );
                    return Results.Ok(summary);
                }
                catch (KeyNotFoundException ex)
                {
                    return Results.NotFound(new { error = ex.Message });
                }
                catch (Exception ex)
                {
                    return Results.BadRequest(new { error = ex.Message });
                }
            });
        }

        return endpoints;
    }
}
