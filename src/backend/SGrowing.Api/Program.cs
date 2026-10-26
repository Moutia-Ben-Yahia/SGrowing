using SGrowing.Api.Data;
using SGrowing.Api.Hubs;
using SGrowing.Api.Modules.AI;
using SGrowing.Api.Modules.Channels;
using SGrowing.Api.Modules.Conversations;
using SGrowing.Api.Modules.CRM;
using SGrowing.Api.Modules.Feedback;
using SGrowing.Api.Modules.Journeys;
using SGrowing.Api.Modules.KnowledgeBase;
using SGrowing.Api.Modules.Outbound;
using SGrowing.Api.Modules.Automation;
using SGrowing.Api.Modules.Marketing;
using SGrowing.Api.Modules.Security;
using SGrowing.Api.Modules.Voice;
using SGrowing.Api.Services;
using SGrowing.Core.Channels;
using SGrowing.Core.Entities;
using SGrowing.Core.Outbox;
using SGrowing.Core.Tenancy;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// 1. Tenancy & Database
builder.Services.AddScoped<ITenantContext, TenantContext>();
builder.Services.AddDbContext<SGrowingDbContext>((sp, options) =>
{
    var connStr = builder.Configuration.GetConnectionString("DefaultConnection");
    if (!string.IsNullOrEmpty(connStr))
    {
        options.UseNpgsql(connStr);
    }
    else
    {
        options.UseInMemoryDatabase("SGrowingDev");
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

// 4. Channel Adapters (Messaging, Social & SMS)
builder.Services.AddSingleton<WhatsAppCloudAdapter>();
builder.Services.AddSingleton<EvolutionApiAdapter>();
builder.Services.AddSingleton<EmailAdapter>();
builder.Services.AddSingleton<TelegramAdapter>();
builder.Services.AddSingleton<DiscordAdapter>();
builder.Services.AddSingleton<MetaGraphAdapter>();
builder.Services.AddSingleton<XTwitterAdapter>();
builder.Services.AddSingleton<SmsAdapter>();

// 5. Inbound Slices Handlers
builder.Services.AddScoped<CreateContactHandler>();
builder.Services.AddScoped<GetContactHandler>();
builder.Services.AddScoped<SearchContactsHandler>();
builder.Services.AddScoped<SendMessageHandler>();
builder.Services.AddScoped<ChangeStatusHandler>();
builder.Services.AddScoped<GetConversationMessagesHandler>();
builder.Services.AddScoped<ApproveAiDraftHandler>();

// 6. Autonomous AI Agent, SLA, Identity, Broadcasts & Assisted Commerce
builder.Services.AddScoped<IAgentTool, KnowledgeBaseTool>();
builder.Services.AddScoped<IAgentTool, OrderLookupTool>();
builder.Services.AddScoped<IAgentTool, EscalateToHumanTool>();
builder.Services.AddScoped<AutonomousAiAgentService>();
builder.Services.AddScoped<IdentityResolverService>();
builder.Services.AddScoped<BroadcastDispatcherService>();
builder.Services.AddScoped<AppointmentBookingService>();
builder.Services.AddScoped<CheckoutLinkService>();
builder.Services.AddScoped<SlaMonitoringService>();

// 7. Voice & Telephony
builder.Services.AddScoped<VoiceTelephonyService>();
builder.Services.AddScoped<RealtimeVoiceAgentService>();

// 8. Social Publishing & Deflection
builder.Services.AddSingleton<ISocialPostRepository, InMemorySocialPostRepository>();
builder.Services.AddScoped<SocialPublishingService>();
builder.Services.AddScoped<SocialCommentDeflectionService>();

// 9. Multi-Touch AI SDR
builder.Services.AddScoped<LinkedInOutreachAdapter>();
builder.Services.AddScoped<MultiTouchCadenceOrchestrator>();

// 10. B2B CRM Accounts & Visual Workflows
builder.Services.AddScoped<CompanyAccountService>();
builder.Services.AddScoped<WorkflowEngineService>();

// 11. Enterprise Horizons: Voice WebSocket Streaming, OAuth Vault, pgvector RAG, Contracts & Enrichment
builder.Services.AddSingleton<BargeInInterruptionService>();
builder.Services.AddSingleton<StreamingVoiceWebSocketHandler>();
builder.Services.AddSingleton<AesEncryptionService>();
builder.Services.AddSingleton<DocumentChunkingService>();
builder.Services.AddScoped<VectorRagRetrievalService>();
builder.Services.AddScoped<LeadEnrichmentService>();
builder.Services.AddScoped<ContractSigningService>();

// 12. SignalR & CORS
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
    var db = scope.ServiceProvider.GetRequiredService<SGrowingDbContext>();
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
    EmailAdapter email,
    TelegramAdapter telegram,
    DiscordAdapter discord,
    MetaGraphAdapter meta,
    XTwitterAdapter xTwitter,
    SmsAdapter sms) =>
{
    using var reader = new StreamReader(request.Body);
    var body = await reader.ReadToEndAsync();

    InboundChannelMessage? parsed = channel.ToLower() switch
    {
        "whatsapp_cloud" => waCloud.ParseInboundWebhook(tenantId, body),
        "evolution_api" => evoWa.ParseInboundWebhook(tenantId, body),
        "email" => email.ParseInboundWebhook(tenantId, body),
        "telegram" => telegram.ParseInboundWebhook(tenantId, body),
        "discord" => discord.ParseInboundWebhook(tenantId, body),
        "meta_graph" => meta.ParseInboundWebhook(tenantId, body),
        "instagram" => meta.ParseInboundWebhook(tenantId, body),
        "facebook" => meta.ParseInboundWebhook(tenantId, body),
        "threads" => meta.ParseInboundWebhook(tenantId, body),
        "x_twitter" => xTwitter.ParseInboundWebhook(tenantId, body),
        "sms" => sms.ParseInboundWebhook(tenantId, body),
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
app.MapGet("/api/outbound/mailboxes", async ([FromQuery] Guid tenantId, SGrowingDbContext db) =>
{
    var mailboxes = await db.SenderAccounts.Where(s => s.TenantId == tenantId).ToListAsync();
    return Results.Ok(mailboxes);
});

app.MapPost("/api/outbound/mailboxes", async ([FromBody] SenderAccount sender, SGrowingDbContext db) =>
{
    db.SenderAccounts.Add(sender);
    await db.SaveChangesAsync();
    return Results.Ok(sender);
});

// Campaigns & Cadences
app.MapGet("/api/outbound/campaigns", async ([FromQuery] Guid tenantId, SGrowingDbContext db) =>
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
app.MapGet("/api/crm/deals", async ([FromQuery] Guid tenantId, SGrowingDbContext db) =>
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

app.MapGet("/api/journeys", async ([FromQuery] Guid tenantId, SGrowingDbContext db) =>
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
app.MapGet("/api/feedback/surveys", async ([FromQuery] Guid tenantId, SGrowingDbContext db) =>
{
    var forms = await db.SurveyForms.Where(f => f.TenantId == tenantId).ToListAsync();
    return Results.Ok(forms);
});

app.MapPost("/api/feedback/submit", async ([FromQuery] Guid tenantId, [FromBody] SubmitFeedbackDto req, FeedbackService service) =>
{
    var res = await service.SubmitFeedbackAsync(tenantId, req.SurveyFormId, req.ContactId, req.Score, req.FeedbackText);
    return res.IsSuccess ? Results.Ok(res.Value) : Results.BadRequest(new { error = res.Error });
});

app.MapGet("/api/feedback/responses", async ([FromQuery] Guid tenantId, SGrowingDbContext db) =>
{
    var responses = await db.SurveyResponses.Where(r => r.TenantId == tenantId).OrderByDescending(r => r.CreatedAtUtc).ToListAsync();
    return Results.Ok(responses);
});

// ----------------- Knowledge Base & RAG Endpoints -----------------
app.MapGet("/api/kb/articles", async ([FromQuery] Guid tenantId, SGrowingDbContext db) =>
{
    var articles = await db.HelpArticles.Where(a => a.TenantId == tenantId && a.IsPublished).ToListAsync();
    return Results.Ok(articles);
});

app.MapPost("/api/kb/query", async ([FromQuery] Guid tenantId, [FromBody] KbQueryDto req, HelpCenterRagService ragService) =>
{
    var res = await ragService.GenerateAiDeflectionReplyAsync(tenantId, req.Question);
    return Results.Ok(res);
});

// ----------------- Identity 360 Endpoints -----------------
app.MapPost("/api/crm/resolve-contact", async ([FromQuery] Guid tenantId, [FromBody] ResolveContactDto req, IdentityResolverService resolver) =>
{
    var res = await resolver.ResolveOrCreateContactAsync(tenantId, req.Email, req.Phone, req.FirstName, req.LastName);
    return res.IsSuccess ? Results.Ok(res.Value) : Results.BadRequest(new { error = res.Error });
});

app.MapGet("/api/crm/contacts/{id:guid}/timeline", async (Guid id, [FromQuery] Guid tenantId, IdentityResolverService resolver) =>
{
    var res = await resolver.GetUnifiedCustomerTimelineAsync(tenantId, id);
    return res.IsSuccess ? Results.Ok(res.Value) : Results.NotFound(new { error = res.Error });
});

// ----------------- WhatsApp Templates & Broadcasts -----------------
app.MapGet("/api/channels/whatsapp/templates", async ([FromQuery] Guid tenantId, SGrowingDbContext db) =>
{
    var templates = await db.WhatsAppTemplates.Where(t => t.TenantId == tenantId).ToListAsync();
    return Results.Ok(templates);
});

app.MapPost("/api/channels/whatsapp/templates", async ([FromBody] WhatsAppTemplate template, SGrowingDbContext db) =>
{
    db.WhatsAppTemplates.Add(template);
    await db.SaveChangesAsync();
    return Results.Ok(template);
});

app.MapGet("/api/channels/broadcasts", async ([FromQuery] Guid tenantId, SGrowingDbContext db) =>
{
    var broadcasts = await db.BroadcastCampaigns.Where(b => b.TenantId == tenantId).ToListAsync();
    return Results.Ok(broadcasts);
});

app.MapPost("/api/channels/broadcasts", async ([FromBody] BroadcastCampaign campaign, SGrowingDbContext db) =>
{
    db.BroadcastCampaigns.Add(campaign);
    await db.SaveChangesAsync();
    return Results.Ok(campaign);
});

app.MapPost("/api/channels/broadcasts/{id:guid}/dispatch", async (Guid id, [FromQuery] Guid tenantId, BroadcastDispatcherService dispatcher) =>
{
    var res = await dispatcher.DispatchBroadcastAsync(tenantId, id);
    return res.IsSuccess ? Results.Ok(res.Value) : Results.BadRequest(new { error = res.Error });
});

// ----------------- In-Chat Booking & Assisted Checkout -----------------
app.MapGet("/api/sales/booking/slots", async ([FromQuery] Guid tenantId, SGrowingDbContext db) =>
{
    var slots = await db.AppointmentSlots.Where(s => s.TenantId == tenantId && s.IsActive).ToListAsync();
    return Results.Ok(slots);
});

app.MapPost("/api/sales/booking/slots", async ([FromBody] AppointmentSlot slot, SGrowingDbContext db) =>
{
    db.AppointmentSlots.Add(slot);
    await db.SaveChangesAsync();
    return Results.Ok(slot);
});

app.MapPost("/api/sales/booking/book", async ([FromQuery] Guid tenantId, [FromBody] BookSlotDto req, AppointmentBookingService bookingService) =>
{
    var res = await bookingService.BookSlotAsync(tenantId, req.SlotId, req.ContactId, req.ScheduledTimeUtc);
    return res.IsSuccess ? Results.Ok(res.Value) : Results.BadRequest(new { error = res.Error });
});

app.MapPost("/api/sales/checkout/create", async ([FromQuery] Guid tenantId, [FromBody] CreateCheckoutDto req, CheckoutLinkService checkoutService) =>
{
    var res = await checkoutService.CreateCheckoutLinkAsync(tenantId, req.ContactId, req.ConversationId, req.Title, req.Amount, req.Currency ?? "USD");
    return res.IsSuccess ? Results.Ok(res.Value) : Results.BadRequest(new { error = res.Error });
});

app.MapPost("/api/sales/checkout/{id:guid}/pay", async (Guid id, [FromQuery] Guid tenantId, CheckoutLinkService checkoutService) =>
{
    var res = await checkoutService.MarkPaidAsync(tenantId, id);
    return res.IsSuccess ? Results.Ok(res.Value) : Results.BadRequest(new { error = res.Error });
});

// ----------------- SLA Policies & Evaluation -----------------
app.MapGet("/api/sla/policies", async ([FromQuery] Guid tenantId, SGrowingDbContext db) =>
{
    var policies = await db.SlaPolicies.Where(p => p.TenantId == tenantId).ToListAsync();
    return Results.Ok(policies);
});

app.MapPost("/api/sla/policies", async ([FromBody] SlaPolicy policy, SGrowingDbContext db) =>
{
    db.SlaPolicies.Add(policy);
    await db.SaveChangesAsync();
    return Results.Ok(policy);
});

app.MapPost("/api/sla/evaluate", async ([FromQuery] Guid tenantId, SlaMonitoringService slaService) =>
{
    var count = await slaService.EvaluateSlaBreachesAsync(tenantId);
    return Results.Ok(new { breachedCount = count });
});

// ----------------- Autonomous AI Agent -----------------
app.MapPost("/api/ai/agent/handle", async ([FromQuery] Guid tenantId, [FromBody] AutonomousAgentQueryDto req, AutonomousAiAgentService agentService) =>
{
    var res = await agentService.HandleIncomingMessageAsync(tenantId, req.ConversationId, req.ContactId, req.Message);
    return res.IsSuccess ? Results.Ok(res.Value) : Results.BadRequest(new { error = res.Error });
});

// ----------------- Voice & Telephony Endpoints -----------------
app.MapPost("/api/voice/inbound", async (HttpRequest request, [FromQuery] Guid tenantId, [FromQuery] string callSid, [FromQuery] string from, [FromQuery] string to, VoiceTelephonyService voiceService) =>
{
    var twiml = await voiceService.HandleInboundCallWebhookAsync(tenantId, callSid, from, to);
    return Results.Content(twiml, "application/xml");
});

app.MapPost("/api/voice/turn", async ([FromQuery] Guid tenantId, [FromBody] VoiceTurnDto req, RealtimeVoiceAgentService voiceAgent) =>
{
    var responseSpeech = await voiceAgent.ProcessVoiceTurnAsync(tenantId, req.SessionId, req.CallerSpeechTranscript);
    return Results.Ok(new { response = responseSpeech });
});

app.MapGet("/api/voice/sessions", async ([FromQuery] Guid tenantId, SGrowingDbContext db) =>
{
    var sessions = await db.VoiceCallSessions.Where(s => s.TenantId == tenantId).OrderByDescending(s => s.CreatedAtUtc).ToListAsync();
    return Results.Ok(sessions);
});

// ----------------- Social Media Publishing & Deflection -----------------
app.MapGet("/api/social/posts", async ([FromQuery] Guid tenantId, SGrowingDbContext db) =>
{
    var posts = await db.SocialPosts.Where(p => p.TenantId == tenantId).OrderByDescending(p => p.CreatedAtUtc).ToListAsync();
    return Results.Ok(posts);
});

app.MapPost("/api/social/posts", async ([FromQuery] Guid tenantId, [FromBody] CreateSocialPostDto req, SocialPublishingService publishingService) =>
{
    var post = await publishingService.CreatePostAsync(tenantId, req.Content, req.Platforms, req.ScheduledAtUtc, req.MediaUrls);
    return Results.Ok(post);
});

app.MapPost("/api/social/publish-due", async ([FromQuery] Guid tenantId, SocialPublishingService publishingService) =>
{
    var count = await publishingService.PublishDuePostsAsync(tenantId);
    return Results.Ok(new { publishedCount = count });
});

app.MapPost("/api/social/comments/deflect", async ([FromQuery] Guid tenantId, [FromBody] DeflectCommentDto req, SocialCommentDeflectionService deflectionService) =>
{
    var res = await deflectionService.HandlePublicCommentAsync(tenantId, req.Platform, req.PostExternalId, req.CommenterUsername, req.CommentText);
    return Results.Ok(res);
});

// ----------------- Multi-Touch AI SDR Outreach -----------------
app.MapPost("/api/outbound/multitouch/dispatch", async ([FromQuery] Guid tenantId, MultiTouchCadenceOrchestrator orchestrator) =>
{
    var count = await orchestrator.ProcessDueMultiTouchStepsAsync(tenantId);
    return Results.Ok(new { processed = count });
});

// ----------------- B2B CRM Accounts & Companies -----------------
app.MapGet("/api/crm/companies", async ([FromQuery] Guid tenantId, SGrowingDbContext db) =>
{
    var companies = await db.Companies.Where(c => c.TenantId == tenantId).ToListAsync();
    return Results.Ok(companies);
});

app.MapPost("/api/crm/companies", async ([FromQuery] Guid tenantId, [FromBody] CreateCompanyDto req, CompanyAccountService companyService) =>
{
    var company = await companyService.CreateCompanyAsync(tenantId, req.Name, req.Domain, req.Industry, req.Revenue, req.ParentCompanyId);
    return Results.Ok(company);
});

app.MapPost("/api/crm/companies/associate", async ([FromQuery] Guid tenantId, [FromBody] AssociateCompanyDto req, CompanyAccountService companyService) =>
{
    var company = await companyService.AssociateContactByDomainAsync(tenantId, req.ContactId, req.ContactEmail);
    return company != null ? Results.Ok(company) : Results.NotFound(new { error = "No company found matching contact email domain." });
});

// ----------------- Visual Node Workflows -----------------
app.MapGet("/api/workflows", async ([FromQuery] Guid tenantId, SGrowingDbContext db) =>
{
    var workflows = await db.WorkflowGraphs.Where(w => w.TenantId == tenantId).ToListAsync();
    return Results.Ok(workflows);
});

app.MapPost("/api/workflows", async ([FromBody] WorkflowGraph graph, SGrowingDbContext db) =>
{
    db.WorkflowGraphs.Add(graph);
    await db.SaveChangesAsync();
    return Results.Ok(graph);
});

app.MapPost("/api/workflows/{id:guid}/trigger", async (Guid id, [FromQuery] Guid tenantId, [FromBody] TriggerWorkflowDto req, WorkflowEngineService engine) =>
{
    var execution = await engine.ExecuteWorkflowAsync(tenantId, id, req.InputPayloadJson);
    return Results.Ok(execution);
});

// ----------------- Real-Time Streaming Voice Pipeline -----------------
app.MapPost("/api/voice/streaming/frame", ([FromBody] StreamAudioChunkDto req, StreamingVoiceWebSocketHandler handler, SGrowingDbContext db) =>
{
    var session = db.StreamingVoiceCallSessions.FirstOrDefault(s => s.TenantId == req.TenantId && s.Id == req.SessionId)
                  ?? StreamingVoiceCallSession.Create(req.TenantId, req.CallSid, req.StreamSid);

    if (!db.StreamingVoiceCallSessions.Contains(session))
    {
        db.StreamingVoiceCallSessions.Add(session);
    }

    var result = handler.ProcessInboundAudioChunk(session, req.PayloadBase64, req.SequenceNumber);
    db.SaveChanges();

    return Results.Ok(result);
});

app.MapPost("/api/voice/streaming/transfer", ([FromBody] WarmTransferDto req, BargeInInterruptionService transferService, SGrowingDbContext db) =>
{
    var session = db.StreamingVoiceCallSessions.FirstOrDefault(s => s.TenantId == req.TenantId && s.Id == req.SessionId)
                  ?? StreamingVoiceCallSession.Create(req.TenantId, "CA_MOCK", "MZ_MOCK");

    var result = transferService.InitiateWarmSipTransfer(session, req.TargetSipUri, req.Reason, req.Summary);
    db.SaveChanges();

    return Results.Ok(result);
});

// ----------------- PostgreSQL pgvector RAG & Hybrid Search -----------------
app.MapPost("/api/rag/documents/index", async ([FromBody] IndexDocumentDto req, VectorRagRetrievalService ragService) =>
{
    var chunks = await ragService.IndexDocumentAsync(req.TenantId, req.DocumentId, req.Title, req.Content, req.SourceUrl);
    return Results.Ok(new { indexedChunks = chunks.Count, documentId = req.DocumentId });
});

app.MapPost("/api/rag/search", async ([FromBody] SearchRagDto req, VectorRagRetrievalService ragService) =>
{
    var results = await ragService.HybridSearchAsync(req.TenantId, req.Query, req.TopK);
    return Results.Ok(results);
});

app.MapPost("/api/rag/grounded-context", async ([FromBody] SearchRagDto req, VectorRagRetrievalService ragService) =>
{
    var context = await ragService.GenerateGroundedContextAsync(req.TenantId, req.Query, req.TopK);
    return Results.Ok(context);
});

// ----------------- B2B Lead Enrichment & In-Chat E-Signatures -----------------
app.MapPost("/api/crm/leads/enrich", async ([FromBody] EnrichLeadDto req, LeadEnrichmentService enrichmentService) =>
{
    var profile = await enrichmentService.EnrichLeadAsync(req.TenantId, req.DomainOrEmail);
    return Results.Ok(profile);
});

app.MapPost("/api/crm/contracts", async ([FromBody] CreateContractDto req, ContractSigningService contractService) =>
{
    var contract = await contractService.CreateContractAsync(
        req.TenantId, req.Title, req.ContractType, req.SignerName, req.SignerEmail, req.ContractValue, req.CompanyId, req.DealId);
    return Results.Ok(contract);
});

app.MapPost("/api/crm/contracts/{id:guid}/dispatch", async (Guid id, [FromQuery] Guid tenantId, ContractSigningService contractService) =>
{
    var dispatch = await contractService.DispatchInChatContractAsync(tenantId, id);
    return Results.Ok(dispatch);
});

app.MapGet("/api/crm/contracts/sign/{token}", async (string token, ContractSigningService contractService) =>
{
    var contract = await contractService.GetAndMarkViewedAsync(token);
    return contract != null ? Results.Ok(contract) : Results.NotFound();
});

app.MapPost("/api/crm/contracts/sign/{token}", async (string token, [FromBody] SignContractDto req, ContractSigningService contractService) =>
{
    var result = await contractService.CompleteSigningAsync(token, req.SignerSignatureName, req.SignedPdfUrl);
    return Results.Ok(result);
});

// Real-Time SignalR Hub
app.MapHub<OmniHub>("/hubs/omni");

app.Run();

public record IngestEventDto(Guid ContactId, string EventName, string? PayloadJson);
public record SubmitFeedbackDto(Guid SurveyFormId, Guid ContactId, int Score, string? FeedbackText);
public record KbQueryDto(string Question);
public record ResolveContactDto(string? Email, string? Phone, string? FirstName, string? LastName);
public record BookSlotDto(Guid SlotId, Guid ContactId, DateTime ScheduledTimeUtc);
public record CreateCheckoutDto(Guid ContactId, Guid ConversationId, string Title, decimal Amount, string? Currency = "USD");
public record AutonomousAgentQueryDto(Guid ConversationId, Guid ContactId, string Message);
public record VoiceTurnDto(Guid SessionId, string CallerSpeechTranscript);
public record CreateSocialPostDto(string Content, List<SocialPlatform> Platforms, DateTime? ScheduledAtUtc = null, List<string>? MediaUrls = null);
public record DeflectCommentDto(SocialPlatform Platform, string PostExternalId, string CommenterUsername, string CommentText);
public record CreateCompanyDto(string Name, string Domain, string? Industry = null, decimal Revenue = 0, Guid? ParentCompanyId = null);
public record AssociateCompanyDto(Guid ContactId, string ContactEmail);
public record TriggerWorkflowDto(string InputPayloadJson);

public record StreamAudioChunkDto(Guid TenantId, Guid SessionId, string CallSid, string StreamSid, string PayloadBase64, long SequenceNumber);
public record WarmTransferDto(Guid TenantId, Guid SessionId, string TargetSipUri, string Reason, string Summary);
public record IndexDocumentDto(Guid TenantId, Guid DocumentId, string Title, string Content, string? SourceUrl = null);
public record SearchRagDto(Guid TenantId, string Query, int TopK = 3);
public record EnrichLeadDto(Guid TenantId, string DomainOrEmail);
public record CreateContractDto(Guid TenantId, string Title, string ContractType, string SignerName, string SignerEmail, decimal ContractValue, Guid? CompanyId = null, Guid? DealId = null);
public record SignContractDto(string SignerSignatureName, string? SignedPdfUrl = null);

// Required for WebApplicationFactory in integration tests
public partial class Program { }
