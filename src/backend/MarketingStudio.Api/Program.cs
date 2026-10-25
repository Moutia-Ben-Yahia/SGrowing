using MarketingStudio.Api.Data;
using MarketingStudio.Api.Hubs;
using MarketingStudio.Api.Modules.AI;
using MarketingStudio.Api.Modules.Channels;
using MarketingStudio.Api.Modules.Conversations;
using MarketingStudio.Api.Modules.CRM;
using MarketingStudio.Api.Modules.Feedback;
using MarketingStudio.Api.Modules.Journeys;
using MarketingStudio.Api.Modules.KnowledgeBase;
using MarketingStudio.Api.Modules.Outbound;
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

// 2. Core & Outbox Services
builder.Services.AddScoped<IOutbox, OutboxService>();
builder.Services.AddScoped<IRealtimeNotifier, RealtimeNotifier>();
builder.Services.AddScoped<IAiCopilotService, AiCopilotService>();
builder.Services.AddScoped<ChannelIngressService>();

// 3. Outbound AI SDR Services
builder.Services.AddScoped<MailboxService>();
builder.Services.AddSingleton<IAiSdrService, AiSdrService>();
builder.Services.AddSingleton<ReplySentimentClassifier>();
builder.Services.AddScoped<ProspectReplyHandoffService>();
builder.Services.AddScoped<CadenceDispatcherService>();
builder.Services.AddScoped<CreateCampaignHandler>();
builder.Services.AddScoped<EnrollProspectsHandler>();

// 4. Journeys, Feedback & Knowledge Base
builder.Services.AddScoped<JourneyExecutionService>();
builder.Services.AddScoped<FeedbackService>();
builder.Services.AddScoped<HelpCenterRagService>();

// 4. Channel Adapters
builder.Services.AddSingleton<WhatsAppCloudAdapter>();
builder.Services.AddSingleton<EvolutionApiAdapter>();
builder.Services.AddSingleton<EmailAdapter>();

// 5. Inbound Slices Handlers
builder.Services.AddScoped<CreateContactHandler>();
builder.Services.AddScoped<GetContactHandler>();
builder.Services.AddScoped<SearchContactsHandler>();
builder.Services.AddScoped<SendMessageHandler>();
builder.Services.AddScoped<ChangeStatusHandler>();
builder.Services.AddScoped<GetConversationMessagesHandler>();
builder.Services.AddScoped<ApproveAiDraftHandler>();

// 6. SignalR & CORS
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

// Webhook Ingress (with Reply Sentiment Handoff hook)
app.MapPost("/api/webhooks/{channel}", async (
    string channel,
    [FromQuery] Guid tenantId,
    [FromQuery] Guid inboxId,
    HttpRequest request,
    ChannelIngressService ingressService,
    ProspectReplyHandoffService handoffService,
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
    if (!res.IsSuccess)
        return Results.BadRequest(new { error = res.Error });

    // Evaluate prospect reply sentiment for outbound handoff
    await handoffService.HandleReplyAsync(tenantId, res.Value.SenderId ?? Guid.Empty, res.Value.ConversationId, parsed.Content);

    return Results.Ok(new { status = "Ingested", messageId = res.Value.Id });
});

// ----------------- Outbound AI SDR Endpoints -----------------

// Mailboxes
app.MapGet("/api/outbound/mailboxes", async ([FromQuery] Guid tenantId, MarketingStudioDbContext db) =>
{
    var mailboxes = await db.SenderAccounts.Where(s => s.TenantId == tenantId).ToListAsync();
    return Results.Ok(mailboxes);
});

app.MapPost("/api/outbound/mailboxes", async ([FromBody] SenderAccount sender, MarketingStudioDbContext db) =>
{
    db.SenderAccounts.Add(sender);
    await db.SaveChangesAsync();
    return Results.Ok(sender);
});

// Campaigns & Cadences
app.MapGet("/api/outbound/campaigns", async ([FromQuery] Guid tenantId, MarketingStudioDbContext db) =>
{
    var campaigns = await db.OutboundCampaigns.Where(c => c.TenantId == tenantId).ToListAsync();
    return Results.Ok(campaigns);
});

app.MapPost("/api/outbound/campaigns", async ([FromBody] CreateCampaignRequest req, CreateCampaignHandler handler) =>
{
    var res = await handler.HandleAsync(req);
    return res.IsSuccess ? Results.Ok(res.Value) : Results.BadRequest(new { error = res.Error });
});

app.MapPost("/api/outbound/campaigns/{id:guid}/enroll", async (Guid id, [FromBody] List<Guid> contactIds, [FromQuery] Guid tenantId, EnrollProspectsHandler handler) =>
{
    var res = await handler.HandleAsync(new EnrollProspectsRequest(tenantId, id, contactIds));
    return res.IsSuccess ? Results.Ok(res.Value) : Results.BadRequest(new { error = res.Error });
});

app.MapPost("/api/outbound/dispatch", async ([FromQuery] Guid tenantId, CadenceDispatcherService dispatcher) =>
{
    var count = await dispatcher.ProcessDueCadencesAsync(tenantId);
    return Results.Ok(new { processed = count });
});

// Deals / CRM Pipeline
app.MapGet("/api/crm/deals", async ([FromQuery] Guid tenantId, MarketingStudioDbContext db) =>
{
    var deals = await db.Deals.Where(d => d.TenantId == tenantId).OrderByDescending(d => d.CreatedAtUtc).ToListAsync();
    return Results.Ok(deals);
});

// ----------------- Customer Journeys Endpoints -----------------
app.MapPost("/api/events", async ([FromQuery] Guid tenantId, [FromBody] IngestEventDto req, JourneyExecutionService service) =>
{
    var res = await service.IngestEventAsync(tenantId, req.ContactId, req.EventName, req.PayloadJson ?? "{}");
    return res.IsSuccess ? Results.Ok(res.Value) : Results.BadRequest(new { error = res.Error });
});

app.MapGet("/api/journeys", async ([FromQuery] Guid tenantId, MarketingStudioDbContext db) =>
{
    var journeys = await db.CustomerJourneys.Where(j => j.TenantId == tenantId).ToListAsync();
    return Results.Ok(journeys);
});

app.MapPost("/api/journeys/dispatch", async ([FromQuery] Guid tenantId, JourneyExecutionService service) =>
{
    var count = await service.ProcessDueStepsAsync(tenantId);
    return Results.Ok(new { processed = count });
});

// ----------------- Feedback & CSAT Endpoints -----------------
app.MapGet("/api/feedback/surveys", async ([FromQuery] Guid tenantId, MarketingStudioDbContext db) =>
{
    var forms = await db.SurveyForms.Where(f => f.TenantId == tenantId).ToListAsync();
    return Results.Ok(forms);
});

app.MapPost("/api/feedback/submit", async ([FromQuery] Guid tenantId, [FromBody] SubmitFeedbackDto req, FeedbackService service) =>
{
    var res = await service.SubmitFeedbackAsync(tenantId, req.SurveyFormId, req.ContactId, req.Score, req.FeedbackText);
    return res.IsSuccess ? Results.Ok(res.Value) : Results.BadRequest(new { error = res.Error });
});

app.MapGet("/api/feedback/responses", async ([FromQuery] Guid tenantId, MarketingStudioDbContext db) =>
{
    var responses = await db.SurveyResponses.Where(r => r.TenantId == tenantId).OrderByDescending(r => r.CreatedAtUtc).ToListAsync();
    return Results.Ok(responses);
});

// ----------------- Knowledge Base & RAG Endpoints -----------------
app.MapGet("/api/kb/articles", async ([FromQuery] Guid tenantId, MarketingStudioDbContext db) =>
{
    var articles = await db.HelpArticles.Where(a => a.TenantId == tenantId && a.IsPublished).ToListAsync();
    return Results.Ok(articles);
});

app.MapPost("/api/kb/query", async ([FromQuery] Guid tenantId, [FromBody] KbQueryDto req, HelpCenterRagService ragService) =>
{
    var res = await ragService.GenerateAiDeflectionReplyAsync(tenantId, req.Question);
    return Results.Ok(res);
});

// Real-Time SignalR Hub
app.MapHub<OmniHub>("/hubs/omni");

app.Run();

public record IngestEventDto(Guid ContactId, string EventName, string? PayloadJson);
public record SubmitFeedbackDto(Guid SurveyFormId, Guid ContactId, int Score, string? FeedbackText);
public record KbQueryDto(string Question);

// Required for WebApplicationFactory in integration tests
public partial class Program { }
