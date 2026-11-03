using System;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using SGrowing.Api.Data;

#nullable disable

namespace SGrowing.Api.Migrations;

[DbContext(typeof(SGrowingDbContext))]
[Migration("20261103070000_GrowthOsPartitioningAndWorkspace")]
public partial class GrowthOsPartitioningAndWorkspace : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.EnsureSchema(name: "customer_data");
        migrationBuilder.EnsureSchema(name: "advertising");
        migrationBuilder.EnsureSchema(name: "automation");

        // Workspaces table
        migrationBuilder.CreateTable(
            name: "Workspaces",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                Slug = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                Status = table.Column<string>(type: "text", nullable: false),
                CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                IsActive = table.Column<bool>(type: "boolean", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_Workspaces", x => x.Id);
            });

        migrationBuilder.CreateIndex(
            name: "IX_Workspaces_TenantId_Slug",
            table: "Workspaces",
            columns: new[] { "TenantId", "Slug" },
            unique: true);

        // Add WorkspaceId column to Contacts, Companies, Deals
        migrationBuilder.AddColumn<Guid>(
            name: "WorkspaceId",
            table: "Contacts",
            type: "uuid",
            nullable: true);

        migrationBuilder.AddColumn<Guid>(
            name: "WorkspaceId",
            table: "Companies",
            type: "uuid",
            nullable: true);

        migrationBuilder.AddColumn<Guid>(
            name: "WorkspaceId",
            table: "Deals",
            type: "uuid",
            nullable: true);

        // Schema customer_data
        migrationBuilder.CreateTable(
            name: "identities",
            schema: "customer_data",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                TenantId = table.Column<string>(type: "text", nullable: false),
                Type = table.Column<string>(type: "text", nullable: false),
                Value = table.Column<string>(type: "text", nullable: false),
                CanonicalProfileId = table.Column<string>(type: "text", nullable: false),
                CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_identities", x => x.Id);
            });

        migrationBuilder.CreateTable(
            name: "profiles",
            schema: "customer_data",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                TenantId = table.Column<string>(type: "text", nullable: false),
                CanonicalProfileId = table.Column<string>(type: "text", nullable: false),
                UnifiedTraitsJson = table.Column<string>(type: "jsonb", nullable: false),
                FirstSeenUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                LastActiveUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_profiles", x => x.Id);
            });

        migrationBuilder.CreateTable(
            name: "identity_links",
            schema: "customer_data",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                TenantId = table.Column<string>(type: "text", nullable: false),
                SourceIdentifierJson = table.Column<string>(type: "jsonb", nullable: false),
                TargetIdentifierJson = table.Column<string>(type: "jsonb", nullable: false),
                ConfidenceScore = table.Column<double>(type: "double precision", nullable: false),
                MatchReason = table.Column<string>(type: "text", nullable: false),
                CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_identity_links", x => x.Id);
            });

        migrationBuilder.CreateTable(
            name: "visitor_sessions",
            schema: "customer_data",
            columns: table => new
            {
                SessionId = table.Column<string>(type: "text", nullable: false),
                TenantId = table.Column<string>(type: "text", nullable: false),
                AnonymousId = table.Column<string>(type: "text", nullable: false),
                StartedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                EndedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                PageViewsJson = table.Column<string>(type: "jsonb", nullable: false),
                UtmParametersJson = table.Column<string>(type: "jsonb", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_visitor_sessions", x => x.SessionId);
            });

        // Schema advertising
        migrationBuilder.CreateTable(
            name: "conversions",
            schema: "advertising",
            columns: table => new
            {
                EventId = table.Column<string>(type: "text", nullable: false),
                TenantId = table.Column<string>(type: "text", nullable: false),
                Platform = table.Column<string>(type: "text", nullable: false),
                EventName = table.Column<string>(type: "text", nullable: false),
                ConversionValue = table.Column<decimal>(type: "numeric", nullable: false),
                Currency = table.Column<string>(type: "text", nullable: false),
                PayloadHash = table.Column<string>(type: "text", nullable: false),
                DispatchedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                IsDeduplicated = table.Column<bool>(type: "boolean", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_conversions", x => x.EventId);
            });

        migrationBuilder.CreateTable(
            name: "audience_syncs",
            schema: "advertising",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                AudienceId = table.Column<string>(type: "text", nullable: false),
                TenantId = table.Column<string>(type: "text", nullable: false),
                Platform = table.Column<string>(type: "text", nullable: false),
                ValidHashesCount = table.Column<int>(type: "integer", nullable: false),
                SyncedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_audience_syncs", x => x.Id);
            });

        migrationBuilder.CreateTable(
            name: "campaign_roas_snapshots",
            schema: "advertising",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                CampaignId = table.Column<string>(type: "text", nullable: false),
                TenantId = table.Column<string>(type: "text", nullable: false),
                Platform = table.Column<string>(type: "text", nullable: false),
                Spend = table.Column<decimal>(type: "numeric", nullable: false),
                Revenue = table.Column<decimal>(type: "numeric", nullable: false),
                CurrentRoas = table.Column<decimal>(type: "numeric", nullable: false),
                RecommendedSpend = table.Column<decimal>(type: "numeric", nullable: false),
                Action = table.Column<string>(type: "text", nullable: false),
                RecordedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_campaign_roas_snapshots", x => x.Id);
            });

        // Schema automation
        migrationBuilder.CreateTable(
            name: "workflows",
            schema: "automation",
            columns: table => new
            {
                WorkflowId = table.Column<string>(type: "text", nullable: false),
                TenantId = table.Column<string>(type: "text", nullable: false),
                Name = table.Column<string>(type: "text", nullable: false),
                TriggerNodeId = table.Column<string>(type: "text", nullable: false),
                NodesJson = table.Column<string>(type: "jsonb", nullable: false),
                EdgesJson = table.Column<string>(type: "jsonb", nullable: false),
                CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_workflows", x => x.WorkflowId);
            });

        migrationBuilder.CreateTable(
            name: "telemetry_traces",
            schema: "automation",
            columns: table => new
            {
                ExecutionId = table.Column<string>(type: "text", nullable: false),
                TenantId = table.Column<string>(type: "text", nullable: false),
                WorkflowId = table.Column<string>(type: "text", nullable: false),
                ContactId = table.Column<string>(type: "text", nullable: false),
                Status = table.Column<string>(type: "text", nullable: false),
                TotalDurationMs = table.Column<int>(type: "integer", nullable: false),
                StepsJson = table.Column<string>(type: "jsonb", nullable: false),
                ExecutedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_telemetry_traces", x => x.ExecutionId);
            });
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "telemetry_traces", schema: "automation");
        migrationBuilder.DropTable(name: "workflows", schema: "automation");
        migrationBuilder.DropTable(name: "campaign_roas_snapshots", schema: "advertising");
        migrationBuilder.DropTable(name: "audience_syncs", schema: "advertising");
        migrationBuilder.DropTable(name: "conversions", schema: "advertising");
        migrationBuilder.DropTable(name: "visitor_sessions", schema: "customer_data");
        migrationBuilder.DropTable(name: "identity_links", schema: "customer_data");
        migrationBuilder.DropTable(name: "profiles", schema: "customer_data");
        migrationBuilder.DropTable(name: "identities", schema: "customer_data");
        migrationBuilder.DropTable(name: "Workspaces");

        migrationBuilder.DropColumn(name: "WorkspaceId", table: "Contacts");
        migrationBuilder.DropColumn(name: "WorkspaceId", table: "Companies");
        migrationBuilder.DropColumn(name: "WorkspaceId", table: "Deals");
    }
}
