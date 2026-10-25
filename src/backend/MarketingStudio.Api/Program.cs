using MarketingStudio.Api.Data;
using MarketingStudio.Api.Hubs;
using MarketingStudio.Api.Modules.AI;
using MarketingStudio.Api.Modules.Channels;
using MarketingStudio.Api.Modules.Conversations;
using MarketingStudio.Api.Modules.CRM;
using MarketingStudio.Api.Services;
using MarketingStudio.Core.Channels;
using MarketingStudio.Core.Entities;
using MarketingStudio.Core.Outbox;
using MarketingStudio.Core.Tenancy;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// 1. Tenancy & Database
builder.Services.AddScoped<ITenantContext, TenantContext>();
builder.Services.AddDbContext<MarketingStudioDbContext>((sp, options) =>
{
    var connStr = builder.Configuration.GetConnectionString("DefaultConnection");
    if (!string.IsNullOrEmpty(connStr))
    {
        options.UseNpgsql(connStr);
    }
    else
    {
        options.UseInMemoryDatabase("MarketingStudioDev");
    }
});

// 2. Core Services
builder.Services.AddScoped<IOutbox, OutboxService>();
builder.Services.AddScoped<IRealtimeNotifier, RealtimeNotifier>();
builder.Services.AddScoped<IAiCopilotService, AiCopilotService>();
builder.Services.AddScoped<ChannelIngressService>();

// 3. Channel Adapters
builder.Services.AddSingleton<WhatsAppCloudAdapter>();
builder.Services.AddSingleton<EvolutionApiAdapter>();
builder.Services.AddSingleton<EmailAdapter>();

// 4. Slices Handlers
builder.Services.AddScoped<CreateContactHandler>();
builder.Services.AddScoped<GetContactHandler>();
builder.Services.AddScoped<SearchContactsHandler>();
builder.Services.AddScoped<SendMessageHandler>();
builder.Services.AddScoped<ChangeStatusHandler>();
builder.Services.AddScoped<GetConversationMessagesHandler>();
builder.Services.AddScoped<ApproveAiDraftHandler>();

// 5. SignalR & CORS
builder.Services.AddSignalR();
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.WithOrigins("http://localhost:4200", "https://localhost:4200")
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials();
    });
});

var app = builder.Build();

app.UseCors();

// Ensure in-memory dev db is seeded if needed
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<MarketingStudioDbContext>();
    db.Database.EnsureCreated();
}

// ----------------- API Endpoints -----------------

app.MapGet("/api/health", () => Results.Ok(new { status = "Healthy", framework = ".NET 10", timestamp = DateTime.UtcNow }));

// CRM Contacts
app.MapPost("/api/crm/contacts", async ([FromBody] CreateContactRequest req, CreateContactHandler handler) =>
{
    var res = await handler.HandleAsync(req);
    return res.IsSuccess ? Results.Ok(res.Value) : Results.BadRequest(new { error = res.Error });
});

app.MapGet("/api/crm/contacts/{id:guid}", async (Guid id, [FromQuery] Guid tenantId, GetContactHandler handler) =>
{
    var res = await handler.HandleAsync(tenantId, id);
    return res.IsSuccess ? Results.Ok(res.Value) : Results.NotFound(new { error = res.Error });
});

app.MapGet("/api/crm/contacts", async ([FromQuery] Guid tenantId, [FromQuery] string? query, SearchContactsHandler handler) =>
{
    var res = await handler.HandleAsync(new SearchContactsQuery(tenantId, query ?? ""));
    return res.IsSuccess ? Results.Ok(res.Value) : Results.BadRequest(new { error = res.Error });
});

// Conversations & Messages
app.MapGet("/api/conversations/{id:guid}/messages", async (Guid id, [FromQuery] Guid tenantId, GetConversationMessagesHandler handler) =>
{
    var res = await handler.HandleAsync(tenantId, id);
    return res.IsSuccess ? Results.Ok(res.Value) : Results.NotFound(new { error = res.Error });
});

app.MapPost("/api/conversations/{id:guid}/messages", async (Guid id, [FromBody] SendMessageRequest req, SendMessageHandler handler) =>
{
    var res = await handler.HandleAsync(req with { ConversationId = id });
    return res.IsSuccess ? Results.Ok(res.Value) : Results.BadRequest(new { error = res.Error });
});

app.MapPost("/api/conversations/{id:guid}/status", async (Guid id, [FromBody] ChangeStatusRequest req, ChangeStatusHandler handler) =>
{
    var res = await handler.HandleAsync(req with { ConversationId = id });
    return res.IsSuccess ? Results.Ok(res.Value) : Results.BadRequest(new { error = res.Error });
});

// AI Draft Approval
app.MapPost("/api/ai/drafts/{id:guid}/approve", async (Guid id, [FromBody] ApproveAiDraftRequest req, ApproveAiDraftHandler handler) =>
{
    var res = await handler.HandleAsync(req with { DraftId = id });
    return res.IsSuccess ? Results.Ok(res.Value) : Results.BadRequest(new { error = res.Error });
});

// Webhook Ingress
app.MapPost("/api/webhooks/{channel}", async (
    string channel,
    [FromQuery] Guid tenantId,
    [FromQuery] Guid inboxId,
    HttpRequest request,
    ChannelIngressService ingressService,
    WhatsAppCloudAdapter waCloud,
    EvolutionApiAdapter evoWa,
    EmailAdapter email) =>
{
    using var reader = new StreamReader(request.Body);
    var body = await reader.ReadToEndAsync();

    InboundChannelMessage? parsed = channel.ToLower() switch
    {
        "whatsapp_cloud" => waCloud.ParseInboundWebhook(tenantId, body),
        "evolution_api" => evoWa.ParseInboundWebhook(tenantId, body),
        "email" => email.ParseInboundWebhook(tenantId, body),
        _ => null
    };

    if (parsed == null)
        return Results.BadRequest(new { error = $"Could not parse webhook for channel: {channel}" });

    var res = await ingressService.IngestAsync(inboxId, parsed);
    return res.IsSuccess ? Results.Ok(new { status = "Ingested", messageId = res.Value.Id }) : Results.BadRequest(new { error = res.Error });
});

// Real-Time SignalR Hub
app.MapHub<OmniHub>("/hubs/omni");

app.Run();

// Required for WebApplicationFactory in integration tests
public partial class Program { }
