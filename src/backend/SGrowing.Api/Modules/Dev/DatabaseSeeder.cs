using Microsoft.EntityFrameworkCore;
using SGrowing.Api.Data;
using SGrowing.Api.Modules.Auth;
using SGrowing.Core.Entities;

namespace SGrowing.Api.Modules.Dev;

public class DatabaseSeeder
{
    private readonly SGrowingDbContext _db;
    private readonly IPasswordHasher _hasher;

    public DatabaseSeeder(SGrowingDbContext db, IPasswordHasher hasher)
    {
        _db = db;
        _hasher = hasher;
    }

    public async Task<Guid> SeedEnterpriseDemoAsync(Guid? targetTenantId = null)
    {
        // 1. Determine or Create Tenant
        Tenant? tenant;
        if (targetTenantId.HasValue && targetTenantId.Value != Guid.Empty)
        {
            tenant = await _db.Tenants.FirstOrDefaultAsync(t => t.Id == targetTenantId.Value);
            if (tenant == null)
            {
                tenant = new Tenant
                {
                    Id = targetTenantId.Value,
                    Name = "SGrowing Enterprise Labs",
                    Slug = "sgrowing-demo",
                    CreatedAtUtc = DateTime.UtcNow,
                    IsActive = true
                };
                _db.Tenants.Add(tenant);
            }
        }
        else
        {
            tenant = await _db.Tenants.FirstOrDefaultAsync(t => t.Slug == "sgrowing-demo");
            if (tenant == null)
            {
                tenant = new Tenant
                {
                    Id = Guid.NewGuid(),
                    Name = "SGrowing Enterprise Labs",
                    Slug = "sgrowing-demo",
                    CreatedAtUtc = DateTime.UtcNow,
                    IsActive = true
                };
                _db.Tenants.Add(tenant);
            }
        }

        var tenantId = tenant.Id;

        // 2. Ensure Roles Exist
        var existingRoleCount = await _db.SystemRoles.CountAsync(r => r.TenantId == tenantId);
        if (existingRoleCount == 0)
        {
            _db.SystemRoles.AddRange(
                SystemRole.CreateDefault(tenantId, UserSystemRole.SuperAdmin),
                SystemRole.CreateDefault(tenantId, UserSystemRole.OperationsManager),
                SystemRole.CreateDefault(tenantId, UserSystemRole.CallCenterAgent),
                SystemRole.CreateDefault(tenantId, UserSystemRole.WarehousePicker),
                SystemRole.CreateDefault(tenantId, UserSystemRole.FinanceAuditor),
                SystemRole.CreateDefault(tenantId, UserSystemRole.AffiliateCreator)
            );
        }

        // 3. Ensure Demo Users Exist
        var (hash, salt) = _hasher.HashPassword("SecurePassword999!");
        var adminUser = await _db.Users.FirstOrDefaultAsync(u => u.TenantId == tenantId && u.Email == "admin@sgrowing.io");
        if (adminUser == null)
        {
            adminUser = new User
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                Email = "admin@sgrowing.io",
                FullName = "System Administrator",
                Role = UserRole.Admin,
                PasswordHash = hash,
                PasswordSalt = salt,
                IsActive = true,
                CreatedAtUtc = DateTime.UtcNow
            };
            _db.Users.Add(adminUser);
        }

        var agentUser = await _db.Users.FirstOrDefaultAsync(u => u.TenantId == tenantId && u.Email == "agent.sarah@sgrowing.io");
        if (agentUser == null)
        {
            agentUser = new User
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                Email = "agent.sarah@sgrowing.io",
                FullName = "Sarah Mansour",
                Role = UserRole.Agent,
                PasswordHash = hash,
                PasswordSalt = salt,
                IsActive = true,
                CreatedAtUtc = DateTime.UtcNow
            };
            _db.Users.Add(agentUser);
        }

        // 4. Seed CRM Contacts & Companies if not present
        if (!await _db.Contacts.AnyAsync(c => c.TenantId == tenantId))
        {
            var company1 = Company.Create(tenantId, "MedTech Tunisia", "medtech.tn", "Healthcare");
            var company2 = Company.Create(tenantId, "Algiers Logistics SARL", "algertech.dz", "Supply Chain");
            _db.Companies.AddRange(company1, company2);

            var contact1 = Contact.Create(tenantId, "Yassine", "Mansour", "yassine@tunisiamail.tn", "+21622334455", LifecycleStage.Customer);
            contact1.CompanyId = company1.Id;

            var contact2 = Contact.Create(tenantId, "Amina", "Bouazizi", "amina.b@algertech.dz", "+213550123456", LifecycleStage.Lead);
            contact2.CompanyId = company2.Id;

            var contact3 = Contact.Create(tenantId, "Tariq", "Cherif", "tariq@casabusiness.ma", "+212661987654", LifecycleStage.Prospect);
            var contact4 = Contact.Create(tenantId, "Nour", "El Houda", "nour@cairotrade.eg", "+201012345678", LifecycleStage.Customer);
            _db.Contacts.AddRange(contact1, contact2, contact3, contact4);

            // 5. Inboxes & Conversations
            var waInbox = new Inbox { Id = Guid.NewGuid(), TenantId = tenantId, Name = "WhatsApp Official Cloud", ChannelType = ChannelType.WhatsAppCloud, IsActive = true };
            var emailInbox = new Inbox { Id = Guid.NewGuid(), TenantId = tenantId, Name = "Support Desk Email", ChannelType = ChannelType.Email, IsActive = true };
            _db.Inboxes.AddRange(waInbox, emailInbox);

            var conv1 = Conversation.Create(tenantId, waInbox.Id, contact1.Id, AiMode.Copilot);
            var conv2 = Conversation.Create(tenantId, emailInbox.Id, contact2.Id, AiMode.Disabled);
            _db.Conversations.AddRange(conv1, conv2);

            var msg = Message.Create(tenantId, conv1.Id, MessageSenderType.Contact, "Bonjour, je voudrais changer la pointure des baskets en cuir de 42 à 43 s'il vous plaît.");
            _db.Messages.Add(msg);

            var draft = AiDraft.Create(tenantId, conv1.Id, "Bonjour M. Mansour ! C'est tout à fait possible grâce à notre service Drop-and-Swap. Le livreur vous apportera la taille 43 et récupérera la 42 directement à votre porte.", 0.94f);
            _db.AiDrafts.Add(draft);
        }

        // 6. COD Orders & Fraud Scores
        if (!await _db.CodOrders.AnyAsync(o => o.TenantId == tenantId))
        {
            var ord1 = CodOrder.Create(tenantId, "COD-TN-9011", "Yassine Mansour", "+21622334455", "12 Rue de Marseille", "Tunis", "Bab Bhar", 195.0m, "TN", "TND", 7.0m, itemsJson: "[{\"sku\":\"SNEAKER-LEATHER-42\",\"qty\":1}]");
            ord1.Status = CodOrderStatus.Confirmed;
            ord1.RiskLevel = FraudRiskLevel.Low;
            ord1.RiskScore = 6;

            var ord2 = CodOrder.Create(tenantId, "COD-TN-9012", "Marwen Chaabane", "+21698765432", "Avenue Habib Bourguiba", "Sousse", "Kantaoui", 340.0m, "TN", "TND", 7.0m, itemsJson: "[{\"sku\":\"WATCH-CHRONO-STEEL\",\"qty\":1}]");
            ord2.Status = CodOrderStatus.Dispatched;
            ord2.RiskLevel = FraudRiskLevel.Low;
            ord2.RiskScore = 12;

            var ord3 = CodOrder.Create(tenantId, "COD-DZ-8021", "Khaled Benali", "+213550123456", "Rue Didouche Mourad", "Algiers", "Sidi M'Hamed", 14500m, "DZ", "DZD", 600m, itemsJson: "[{\"sku\":\"BAG-LEATHER-MESSENGER\",\"qty\":1}]");
            ord3.Status = CodOrderStatus.PendingConfirmation;
            ord3.RiskLevel = FraudRiskLevel.Medium;
            ord3.RiskScore = 48;

            var ord4 = CodOrder.Create(tenantId, "COD-TN-9014", "Suspicious Buyer", "+21650999888", "Adresse Inconnue", "Ben Arous", "Megrine", 580.0m, "TN", "TND", 7.0m, itemsJson: "[{\"sku\":\"HOODIE-OVERSIZED-BLACK-L\",\"qty\":5}]");
            ord4.Status = CodOrderStatus.PendingConfirmation;
            ord4.RiskLevel = FraudRiskLevel.Fraudulent;
            ord4.RiskScore = 88;
            ord4.FraudFlagsJson = "[\"HighVelocityOrdersFromIp\",\"DisposableAlternateNumber\",\"MismatchGovernorateDelegation\"]";

            var ord5 = CodOrder.Create(tenantId, "COD-TN-9015", "Sami Trabelsi", "+21620112233", "Route de Teniour Km 3", "Sfax", "Sfax Ville", 120.0m, "TN", "TND", 7.0m, itemsJson: "[{\"sku\":\"PERFUME-OUD-ROYAL-100ML\",\"qty\":1}]");
            ord5.Status = CodOrderStatus.Delivered;
            ord5.RiskLevel = FraudRiskLevel.Low;
            ord5.RiskScore = 4;

            _db.CodOrders.AddRange(ord1, ord2, ord3, ord4, ord5);
        }

        // 7. Defensive WMS Inventory
        if (!await _db.WarehouseInventories.AnyAsync(w => w.TenantId == tenantId))
        {
            var whTunis = Guid.NewGuid();
            var whSousse = Guid.NewGuid();

            _db.WarehouseInventories.AddRange(
                new WarehouseInventory { Id = Guid.NewGuid(), TenantId = tenantId, WarehouseId = whTunis, ProductId = Guid.NewGuid(), Sku = "SNEAKER-LEATHER-42", PhysicalStock = 45, ReservedStock = 3 },
                new WarehouseInventory { Id = Guid.NewGuid(), TenantId = tenantId, WarehouseId = whTunis, ProductId = Guid.NewGuid(), Sku = "SNEAKER-LEATHER-43", PhysicalStock = 38, ReservedStock = 2 },
                new WarehouseInventory { Id = Guid.NewGuid(), TenantId = tenantId, WarehouseId = whTunis, ProductId = Guid.NewGuid(), Sku = "HOODIE-OVERSIZED-BLACK-L", PhysicalStock = 120, ReservedStock = 8 },
                new WarehouseInventory { Id = Guid.NewGuid(), TenantId = tenantId, WarehouseId = whSousse, ProductId = Guid.NewGuid(), Sku = "WATCH-CHRONO-STEEL", PhysicalStock = 15, ReservedStock = 1 },
                new WarehouseInventory { Id = Guid.NewGuid(), TenantId = tenantId, WarehouseId = whTunis, ProductId = Guid.NewGuid(), Sku = "BAG-LEATHER-MESSENGER", PhysicalStock = 25, ReservedStock = 4 },
                new WarehouseInventory { Id = Guid.NewGuid(), TenantId = tenantId, WarehouseId = whSousse, ProductId = Guid.NewGuid(), Sku = "PERFUME-OUD-ROYAL-100ML", PhysicalStock = 60, ReservedStock = 5 }
            );
        }

        // 8. Creator & Influencer Affiliates
        if (!await _db.CreatorProfiles.AnyAsync(c => c.TenantId == tenantId))
        {
            var creator1 = CreatorProfile.Create(
                tenantId,
                "Amira Style",
                "amirastyle",
                "amira@creators.tn",
                "+21625112233",
                CreatorPlatform.TikTok,
                CommissionType.Percentage,
                15.0m,
                200.0m,
                "BankTransfer",
                "TN59 1000 1234 5678 9012 3456"
            );
            creator1.TotalEarned = 1680.0m;
            creator1.PendingEscrowBalance = 1250.0m;
            creator1.TotalPaidOut = 430.0m;

            var creator2 = CreatorProfile.Create(
                tenantId,
                "Hassen Tech",
                "hassentech",
                "hassen@youtube.tn",
                "+21699887766",
                CreatorPlatform.YouTube,
                CommissionType.FixedPerDeliveredOrder,
                10.0m,
                100.0m,
                "PostalD17",
                "D17 Wallet: 99887766"
            );
            creator2.TotalEarned = 450.0m;
            creator2.PendingEscrowBalance = 120.0m;
            creator2.TotalPaidOut = 330.0m;

            _db.CreatorProfiles.AddRange(creator1, creator2);

            _db.AffiliateReferralCodes.AddRange(
                AffiliateReferralCode.Create(tenantId, creator1.Id, "AMIRA15", "Percentage", 10m),
                AffiliateReferralCode.Create(tenantId, creator2.Id, "HASSEN10", "Percentage", 5m)
            );
        }

        // 9. Cross-Border Trade Config
        if (!await _db.CountryTradeConfigs.AnyAsync(c => c.TenantId == tenantId))
        {
            _db.CountryTradeConfigs.AddRange(
                CountryTradeConfig.Create(tenantId, "TN", "Tunisia", "TND", "+216", 8, 19m, 7m, true),
                CountryTradeConfig.Create(tenantId, "DZ", "Algeria", "DZD", "+213", 9, 19m, 600m, true),
                CountryTradeConfig.Create(tenantId, "MA", "Morocco", "MAD", "+212", 9, 20m, 35m, true),
                CountryTradeConfig.Create(tenantId, "FR", "France", "EUR", "+33", 9, 20m, 15m, false)
            );
        }

        // 10. Security Audit Trail
        _db.SecurityAuditLogEntries.Add(new SecurityAuditLogEntry
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            UserId = adminUser.Id,
            UserEmail = adminUser.Email,
            UserRole = "SuperAdmin",
            Action = "SeedEnterpriseDemo",
            Resource = "Tenant",
            ResourceId = tenantId.ToString(),
            Details = "Enterprise demo sandbox successfully seeded with CRM, Inboxes, COD Orders, WMS Stock, and Affiliates.",
            IpAddress = "127.0.0.1",
            TimestampUtc = DateTime.UtcNow
        });

        // 11. E-Signature Envelopes & Signers + Dwell Telemetry
        if (!await _db.DocumentEnvelopes.AnyAsync(e => e.TenantId == tenantId))
        {
            var envelope = DocumentEnvelope.Create(
                tenantId,
                "Enterprise Master Services Agreement (MSA) 2026",
                "https://cdn.sgrowing.io/docs/msa_2026.pdf",
                "OFFICIAL INSPIRE MSA ENTERPRISE LEGAL TERMS 2026",
                true
            );
            envelope.Status = EnvelopeStatus.Completed;
            envelope.CompletedAtUtc = DateTime.UtcNow.AddDays(-2);
            _db.DocumentEnvelopes.Add(envelope);

            var signer = EnvelopeSigner.Create(
                tenantId,
                envelope.Id,
                "Yassine Mansour",
                "yassine@tunisiamail.tn",
                SignerRole.Signer,
                1
            );
            signer.Status = SignerStatus.Signed;
            signer.SignedAtUtc = DateTime.UtcNow.AddDays(-2);
            signer.IpAddress = "196.200.14.88";
            signer.SignatureData = "Yassine Mansour (Digital SHA-256 SignOff)";
            _db.EnvelopeSigners.Add(signer);

            _db.DocumentDwellTelemetries.AddRange(
                DocumentDwellTelemetry.Create(tenantId, envelope.Id, 1, "yassine@tunisiamail.tn", "196.200.14.88", 45),
                DocumentDwellTelemetry.Create(tenantId, envelope.Id, 2, "yassine@tunisiamail.tn", "196.200.14.88", 120),
                DocumentDwellTelemetry.Create(tenantId, envelope.Id, 3, "yassine@tunisiamail.tn", "196.200.14.88", 75)
            );
        }

        // 12. Headless Publication CMS Articles & Newsletters
        if (!await _db.PublicationArticles.AnyAsync(a => a.TenantId == tenantId))
        {
            var article1 = PublicationArticle.Create(
                tenantId,
                "The State of Autonomous E-Commerce in 2026",
                "state-of-autonomous-ecommerce-2026",
                "How AI SDR agents, automated courier reconciliation, and edge funnels are reshaping modern retail.",
                "<p>Modern e-commerce has evolved beyond static storefronts into intelligent, real-time autonomous systems...</p>",
                "Editorial Growth Team",
                PublicationVisibility.Public
            );
            article1.Status = PublicationArticleStatus.Published;
            article1.PublishedAtUtc = DateTime.UtcNow.AddDays(-7);
            article1.ViewCount = 1840;

            var article2 = PublicationArticle.Create(
                tenantId,
                "Beating Cash on Delivery Return-To-Origin (RTO) with Pre-Confirmation",
                "beating-cod-rto-pre-confirmation",
                "Tactics for slashing delivery drop-offs using automated WhatsApp confirmation bots and smart routing.",
                "<p>In emerging markets, Cash on Delivery represents over 70% of consumer transactions...</p>",
                "Sarah Mansour",
                PublicationVisibility.PaidTier
            );
            article2.Status = PublicationArticleStatus.Published;
            article2.PublishedAtUtc = DateTime.UtcNow.AddDays(-3);
            article2.ViewCount = 950;

            _db.PublicationArticles.AddRange(article1, article2);

            var broadcast = NewsletterBroadcast.Create(
                tenantId,
                article1.Id,
                "Weekly Growth Dispatch #42: Optimizing Your Pipeline Velocity",
                "AllSubscribers",
                3200
            );
            broadcast.Status = NewsletterBroadcastStatus.Completed;
            broadcast.DeliveredCount = 3180;
            broadcast.OpenedCount = 1520;
            broadcast.ClickedCount = 490;
            broadcast.SentAtUtc = DateTime.UtcNow.AddDays(-4);
            _db.NewsletterBroadcasts.Add(broadcast);
        }

        // 13. Sales Forecasting Periods & Rep Quotas
        if (!await _db.SalesForecastPeriods.AnyAsync(p => p.TenantId == tenantId))
        {
            var period = SalesForecastPeriod.Create(
                tenantId,
                "Q4 2026",
                new DateTime(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc),
                new DateTime(2026, 12, 31, 23, 59, 59, DateTimeKind.Utc),
                250000m
            );
            period.ClosedWonRevenue = 95000m;
            period.WeightedForecastRevenue = 155000m;
            period.UnweightedPipelineRevenue = 210000m;
            _db.SalesForecastPeriods.Add(period);

            _db.SalesForecastRepQuotas.AddRange(
                new SalesForecastRepQuota
                {
                    Id = Guid.NewGuid(),
                    TenantId = tenantId,
                    PeriodId = period.Id,
                    SalesRepUserId = agentUser.Id,
                    SalesRepName = "Sarah Mansour",
                    QuotaAmount = 100000m,
                    ClosedWonAmount = 85000m,
                    CommitAmount = 10000m,
                    BestCaseAmount = 15000m,
                    PipelineAmount = 45000m,
                    UpdatedAtUtc = DateTime.UtcNow
                },
                new SalesForecastRepQuota
                {
                    Id = Guid.NewGuid(),
                    TenantId = tenantId,
                    PeriodId = period.Id,
                    SalesRepUserId = adminUser.Id,
                    SalesRepName = "System Administrator",
                    QuotaAmount = 150000m,
                    ClosedWonAmount = 120000m,
                    CommitAmount = 20000m,
                    BestCaseAmount = 30000m,
                    PipelineAmount = 75000m,
                    UpdatedAtUtc = DateTime.UtcNow
                }
            );
        }

        // 14. Public Roadmap Feature Feedback Posts
        if (!await _db.FeatureFeedbackPosts.AnyAsync(f => f.TenantId == tenantId))
        {
            var p1 = FeatureFeedbackPost.Create(
                tenantId,
                "Real-Time Multi-Currency Settlement & Fx Hedging",
                "Enable automatic multi-currency payouts for cross-border e-commerce across Maghreb and GCC.",
                "Payments",
                "tariq@casabusiness.ma",
                "Tariq Cherif"
            );
            p1.Status = FeedbackStatus.UnderReview;
            p1.UpvoteCount = 42;

            var p2 = FeatureFeedbackPost.Create(
                tenantId,
                "Native 2-Way Shopify & WooCommerce Webhook Sync",
                "Live catalog and stock synchronization with external Shopify stores.",
                "Integrations",
                "amina.b@algertech.dz",
                "Amina Bouazizi"
            );
            p2.Status = FeedbackStatus.Planned;
            p2.UpvoteCount = 89;

            var p3 = FeatureFeedbackPost.Create(
                tenantId,
                "AI SDR Voice Cloning for Inbound Confirmation Calls",
                "Allow uploading 60 seconds of audio to generate branded IVR confirmation voices.",
                "AI & Voice",
                "yassine@tunisiamail.tn",
                "Yassine Mansour"
            );
            p3.Status = FeedbackStatus.InProgress;
            p3.UpvoteCount = 134;

            var p4 = FeatureFeedbackPost.Create(
                tenantId,
                "Driver Mobile Companion Progressive Web App (PWA)",
                "Offline-first delivery runner PWA with barcode camera scanning and doorstep signature capture.",
                "Logistics",
                "nour@cairotrade.eg",
                "Nour El Houda"
            );
            p4.Status = FeedbackStatus.Completed;
            p4.UpvoteCount = 210;

            _db.FeatureFeedbackPosts.AddRange(p1, p2, p3, p4);
        }

        // 15. AI Agent Budget & Tool Execution Logs
        if (!await _db.AiAgentBudgets.AnyAsync(b => b.TenantId == tenantId))
        {
            var budget = AiAgentBudget.Create(tenantId, 5000000, 200.0m, true);
            budget.TokensConsumed = 1240000;
            budget.CostSpentUsd = 49.60m;
            _db.AiAgentBudgets.Add(budget);

            _db.AiToolExecutionLogs.AddRange(
                AiToolExecutionLog.Create(
                    tenantId,
                    "SdrNegotiationAgent",
                    "ApproveOrderDiscount",
                    "{\"orderId\":\"COD-TN-9012\",\"discountPercent\":15}",
                    "{\"status\":\"discount_applied\",\"adjustedAmount\":186.56}",
                    120,
                    450,
                    0.009m,
                    ToolApprovalStatus.Approved
                ),
                AiToolExecutionLog.Create(
                    tenantId,
                    "SupportDispatcherAgent",
                    "LookupCourierStatus",
                    "{\"trackingNumber\":\"YAL-77401928-DZ\"}",
                    "{\"status\":\"Delivered\",\"deliveredDate\":\"2026-10-24\"}",
                    80,
                    180,
                    0.0036m,
                    ToolApprovalStatus.AutoApproved
                )
            );
        }

        await _db.SaveChangesAsync();
        return tenantId;
    }
}
