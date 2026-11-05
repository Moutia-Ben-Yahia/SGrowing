using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SGrowing.Api.Migrations
{
    /// <inheritdoc />
    public partial class EnterpriseSchemasAndPersistenceHardening : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "crm");

            migrationBuilder.EnsureSchema(
                name: "ai");

            migrationBuilder.EnsureSchema(
                name: "engagement");

            migrationBuilder.EnsureSchema(
                name: "analytics");

            migrationBuilder.EnsureSchema(
                name: "billing");

            migrationBuilder.EnsureSchema(
                name: "commerce");

            migrationBuilder.EnsureSchema(
                name: "sales");

            migrationBuilder.EnsureSchema(
                name: "support");

            migrationBuilder.EnsureSchema(
                name: "platform");

            migrationBuilder.EnsureSchema(
                name: "identity");

            migrationBuilder.EnsureSchema(
                name: "tenancy");

            migrationBuilder.RenameTable(
                name: "Workspaces",
                newName: "Workspaces",
                newSchema: "tenancy");

            migrationBuilder.RenameTable(
                name: "WorkflowGraphs",
                newName: "WorkflowGraphs",
                newSchema: "automation");

            migrationBuilder.RenameTable(
                name: "WilayaZones",
                newName: "WilayaZones",
                newSchema: "tenancy");

            migrationBuilder.RenameTable(
                name: "WhatsAppTemplates",
                newName: "WhatsAppTemplates",
                newSchema: "engagement");

            migrationBuilder.RenameTable(
                name: "Warehouses",
                newName: "Warehouses",
                newSchema: "commerce");

            migrationBuilder.RenameTable(
                name: "WarehouseInventories",
                newName: "WarehouseInventories",
                newSchema: "commerce");

            migrationBuilder.RenameTable(
                name: "VisitorSessions",
                newName: "VisitorSessions",
                newSchema: "analytics");

            migrationBuilder.RenameTable(
                name: "Users",
                newName: "Users",
                newSchema: "identity");

            migrationBuilder.RenameTable(
                name: "UsageMeterRecords",
                newName: "UsageMeterRecords",
                newSchema: "billing");

            migrationBuilder.RenameTable(
                name: "TenantSubscriptions",
                newName: "TenantSubscriptions",
                newSchema: "billing");

            migrationBuilder.RenameTable(
                name: "Tenants",
                newName: "Tenants",
                newSchema: "tenancy");

            migrationBuilder.RenameTable(
                name: "TenantApiKeys",
                newName: "TenantApiKeys",
                newSchema: "identity");

            migrationBuilder.RenameTable(
                name: "TelemetryEvents",
                newName: "TelemetryEvents",
                newSchema: "analytics");

            migrationBuilder.RenameTable(
                name: "SystemRoles",
                newName: "SystemRoles",
                newSchema: "identity");

            migrationBuilder.RenameTable(
                name: "SurveyResponses",
                newName: "SurveyResponses",
                newSchema: "support");

            migrationBuilder.RenameTable(
                name: "SurveyForms",
                newName: "SurveyForms",
                newSchema: "support");

            migrationBuilder.RenameTable(
                name: "SlaPolicies",
                newName: "SlaPolicies",
                newSchema: "support");

            migrationBuilder.RenameTable(
                name: "SenderAccounts",
                newName: "SenderAccounts",
                newSchema: "engagement");

            migrationBuilder.RenameTable(
                name: "SalesCommissionStatements",
                newName: "SalesCommissionStatements",
                newSchema: "sales");

            migrationBuilder.RenameTable(
                name: "SalesCommissionPlans",
                newName: "SalesCommissionPlans",
                newSchema: "sales");

            migrationBuilder.RenameTable(
                name: "RolePermissionClaims",
                newName: "RolePermissionClaims",
                newSchema: "identity");

            migrationBuilder.RenameTable(
                name: "QuoteProposals",
                newName: "QuoteProposals",
                newSchema: "sales");

            migrationBuilder.RenameTable(
                name: "QuoteLineItems",
                newName: "QuoteLineItems",
                newSchema: "sales");

            migrationBuilder.RenameTable(
                name: "ProductVariants",
                newName: "ProductVariants",
                newSchema: "commerce");

            migrationBuilder.RenameTable(
                name: "Products",
                newName: "Products",
                newSchema: "commerce");

            migrationBuilder.RenameTable(
                name: "OutboxMessages",
                newName: "OutboxMessages",
                newSchema: "platform");

            migrationBuilder.RenameTable(
                name: "Messages",
                newName: "Messages",
                newSchema: "engagement");

            migrationBuilder.RenameTable(
                name: "JourneySteps",
                newName: "JourneySteps",
                newSchema: "automation");

            migrationBuilder.RenameTable(
                name: "Inboxes",
                newName: "Inboxes",
                newSchema: "engagement");

            migrationBuilder.RenameTable(
                name: "HelpCategories",
                newName: "HelpCategories",
                newSchema: "support");

            migrationBuilder.RenameTable(
                name: "HelpArticles",
                newName: "HelpArticles",
                newSchema: "support");

            migrationBuilder.RenameTable(
                name: "FeatureEntitlements",
                newName: "FeatureEntitlements",
                newSchema: "billing");

            migrationBuilder.RenameTable(
                name: "DynamicAgentTools",
                newName: "DynamicAgentTools",
                newSchema: "ai");

            migrationBuilder.RenameTable(
                name: "DocumentChunks",
                newName: "DocumentChunks",
                newSchema: "ai");

            migrationBuilder.RenameTable(
                name: "DigitalContracts",
                newName: "DigitalContracts",
                newSchema: "sales");

            migrationBuilder.RenameTable(
                name: "Deals",
                newName: "Deals",
                newSchema: "crm");

            migrationBuilder.RenameTable(
                name: "CustomFieldValues",
                newName: "CustomFieldValues",
                newSchema: "crm");

            migrationBuilder.RenameTable(
                name: "CustomFieldDefinitions",
                newName: "CustomFieldDefinitions",
                newSchema: "crm");

            migrationBuilder.RenameTable(
                name: "CustomerJourneys",
                newName: "CustomerJourneys",
                newSchema: "automation");

            migrationBuilder.RenameTable(
                name: "Conversations",
                newName: "Conversations",
                newSchema: "engagement");

            migrationBuilder.RenameTable(
                name: "Contacts",
                newName: "Contacts",
                newSchema: "crm");

            migrationBuilder.RenameTable(
                name: "Companies",
                newName: "Companies",
                newSchema: "crm");

            migrationBuilder.RenameTable(
                name: "CodOrders",
                newName: "CodOrders",
                newSchema: "commerce");

            migrationBuilder.RenameTable(
                name: "CartReservationLocks",
                newName: "CartReservationLocks",
                newSchema: "commerce");

            migrationBuilder.RenameTable(
                name: "BroadcastCampaigns",
                newName: "BroadcastCampaigns",
                newSchema: "engagement");

            migrationBuilder.RenameTable(
                name: "BillingInvoiceRecords",
                newName: "BillingInvoiceRecords",
                newSchema: "billing");

            migrationBuilder.RenameTable(
                name: "AttributionTouchpoints",
                newName: "AttributionTouchpoints",
                newSchema: "analytics");

            migrationBuilder.RenameTable(
                name: "AiDrafts",
                newName: "AiDrafts",
                newSchema: "engagement");

            migrationBuilder.RenameTable(
                name: "AiAgentBudgets",
                newName: "AiAgentBudgets",
                newSchema: "ai");

            migrationBuilder.RenameTable(
                name: "AgentMissions",
                newName: "AgentMissions",
                newSchema: "ai");

            migrationBuilder.RenameTable(
                name: "Activities",
                newName: "Activities",
                newSchema: "crm");

            migrationBuilder.AlterColumn<Guid>(
                name: "TenantId",
                schema: "automation",
                table: "workflows",
                type: "uuid",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(100)",
                oldMaxLength: 100);

            migrationBuilder.AlterColumn<Guid>(
                name: "TenantId",
                schema: "customer_data",
                table: "visitor_sessions",
                type: "uuid",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(100)",
                oldMaxLength: 100);

            migrationBuilder.AlterColumn<Guid>(
                name: "TenantId",
                schema: "automation",
                table: "telemetry_traces",
                type: "uuid",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(100)",
                oldMaxLength: 100);

            migrationBuilder.AlterColumn<Guid>(
                name: "TenantId",
                schema: "customer_data",
                table: "profiles",
                type: "uuid",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(100)",
                oldMaxLength: 100);

            migrationBuilder.AlterColumn<Guid>(
                name: "TenantId",
                schema: "customer_data",
                table: "identity_links",
                type: "uuid",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(100)",
                oldMaxLength: 100);

            migrationBuilder.AlterColumn<Guid>(
                name: "TenantId",
                schema: "customer_data",
                table: "identities",
                type: "uuid",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(100)",
                oldMaxLength: 100);

            migrationBuilder.AlterColumn<Guid>(
                name: "TenantId",
                schema: "advertising",
                table: "conversions",
                type: "uuid",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(100)",
                oldMaxLength: 100);

            migrationBuilder.AlterColumn<Guid>(
                name: "TenantId",
                schema: "advertising",
                table: "campaign_roas_snapshots",
                type: "uuid",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(100)",
                oldMaxLength: 100);

            migrationBuilder.AlterColumn<Guid>(
                name: "TenantId",
                schema: "advertising",
                table: "audience_syncs",
                type: "uuid",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(100)",
                oldMaxLength: 100);

            migrationBuilder.AlterColumn<string>(
                name: "Name",
                schema: "automation",
                table: "WorkflowGraphs",
                type: "character varying(200)",
                maxLength: 200,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<decimal>(
                name: "StandardShippingFee",
                schema: "tenancy",
                table: "WilayaZones",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric");

            migrationBuilder.AlterColumn<string>(
                name: "CountryCode",
                schema: "tenancy",
                table: "WilayaZones",
                type: "character varying(10)",
                maxLength: 10,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<string>(
                name: "Code",
                schema: "tenancy",
                table: "WilayaZones",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<string>(
                name: "Name",
                schema: "engagement",
                table: "WhatsAppTemplates",
                type: "character varying(200)",
                maxLength: 200,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<string>(
                name: "Name",
                schema: "commerce",
                table: "Warehouses",
                type: "character varying(200)",
                maxLength: 200,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<string>(
                name: "Code",
                schema: "commerce",
                table: "Warehouses",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<string>(
                name: "Sku",
                schema: "commerce",
                table: "WarehouseInventories",
                type: "character varying(100)",
                maxLength: 100,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<decimal>(
                name: "MonthlyPrice",
                schema: "billing",
                table: "TenantSubscriptions",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric");

            migrationBuilder.AlterColumn<string>(
                name: "KeyPrefix",
                schema: "identity",
                table: "TenantApiKeys",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<string>(
                name: "KeyMasked",
                schema: "identity",
                table: "TenantApiKeys",
                type: "character varying(100)",
                maxLength: 100,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<string>(
                name: "RoleName",
                schema: "identity",
                table: "SystemRoles",
                type: "character varying(100)",
                maxLength: 100,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<string>(
                name: "Title",
                schema: "support",
                table: "SurveyForms",
                type: "character varying(200)",
                maxLength: 200,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<string>(
                name: "Name",
                schema: "support",
                table: "SlaPolicies",
                type: "character varying(200)",
                maxLength: 200,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<string>(
                name: "Email",
                schema: "engagement",
                table: "SenderAccounts",
                type: "character varying(256)",
                maxLength: 256,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<decimal>(
                name: "TotalDealsValue",
                schema: "sales",
                table: "SalesCommissionStatements",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric");

            migrationBuilder.AlterColumn<decimal>(
                name: "CalculatedCommission",
                schema: "sales",
                table: "SalesCommissionStatements",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric");

            migrationBuilder.AlterColumn<decimal>(
                name: "QuotaThresholdPercent",
                schema: "sales",
                table: "SalesCommissionPlans",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric");

            migrationBuilder.AlterColumn<string>(
                name: "PlanName",
                schema: "sales",
                table: "SalesCommissionPlans",
                type: "character varying(200)",
                maxLength: 200,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<decimal>(
                name: "BaseRatePercent",
                schema: "sales",
                table: "SalesCommissionPlans",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric");

            migrationBuilder.AlterColumn<decimal>(
                name: "AcceleratedRatePercent",
                schema: "sales",
                table: "SalesCommissionPlans",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric");

            migrationBuilder.AlterColumn<string>(
                name: "Resource",
                schema: "identity",
                table: "RolePermissionClaims",
                type: "character varying(100)",
                maxLength: 100,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<string>(
                name: "Action",
                schema: "identity",
                table: "RolePermissionClaims",
                type: "character varying(100)",
                maxLength: 100,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<decimal>(
                name: "TotalAmount",
                schema: "sales",
                table: "QuoteProposals",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric");

            migrationBuilder.AlterColumn<decimal>(
                name: "TaxPercent",
                schema: "sales",
                table: "QuoteProposals",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric");

            migrationBuilder.AlterColumn<decimal>(
                name: "Subtotal",
                schema: "sales",
                table: "QuoteProposals",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric");

            migrationBuilder.AlterColumn<string>(
                name: "QuoteNumber",
                schema: "sales",
                table: "QuoteProposals",
                type: "character varying(100)",
                maxLength: 100,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<decimal>(
                name: "DiscountPercent",
                schema: "sales",
                table: "QuoteProposals",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric");

            migrationBuilder.AlterColumn<decimal>(
                name: "UnitPrice",
                schema: "sales",
                table: "QuoteLineItems",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric");

            migrationBuilder.AlterColumn<string>(
                name: "SKU",
                schema: "commerce",
                table: "ProductVariants",
                type: "character varying(100)",
                maxLength: 100,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<decimal>(
                name: "Price",
                schema: "commerce",
                table: "ProductVariants",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric");

            migrationBuilder.AlterColumn<string>(
                name: "Slug",
                schema: "commerce",
                table: "Products",
                type: "character varying(100)",
                maxLength: 100,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<string>(
                name: "Name",
                schema: "commerce",
                table: "Products",
                type: "character varying(200)",
                maxLength: 200,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<decimal>(
                name: "BasePrice",
                schema: "commerce",
                table: "Products",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric");

            migrationBuilder.AlterColumn<string>(
                name: "Slug",
                schema: "support",
                table: "HelpCategories",
                type: "character varying(100)",
                maxLength: 100,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<string>(
                name: "Name",
                schema: "support",
                table: "HelpCategories",
                type: "character varying(200)",
                maxLength: 200,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<string>(
                name: "Title",
                schema: "support",
                table: "HelpArticles",
                type: "character varying(256)",
                maxLength: 256,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<string>(
                name: "Slug",
                schema: "support",
                table: "HelpArticles",
                type: "character varying(150)",
                maxLength: 150,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<string>(
                name: "Name",
                schema: "ai",
                table: "DynamicAgentTools",
                type: "character varying(150)",
                maxLength: 150,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<string>(
                name: "Title",
                schema: "sales",
                table: "DigitalContracts",
                type: "character varying(200)",
                maxLength: 200,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<decimal>(
                name: "ContractValue",
                schema: "sales",
                table: "DigitalContracts",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric");

            migrationBuilder.AlterColumn<string>(
                name: "Title",
                schema: "crm",
                table: "Deals",
                type: "character varying(200)",
                maxLength: 200,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<decimal>(
                name: "Amount",
                schema: "crm",
                table: "Deals",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric");

            migrationBuilder.AlterColumn<decimal>(
                name: "ValueNumber",
                schema: "crm",
                table: "CustomFieldValues",
                type: "numeric(18,4)",
                precision: 18,
                scale: 4,
                nullable: true,
                oldClrType: typeof(decimal),
                oldType: "numeric",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "FieldName",
                schema: "crm",
                table: "CustomFieldDefinitions",
                type: "character varying(100)",
                maxLength: 100,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<string>(
                name: "EntityType",
                schema: "crm",
                table: "CustomFieldDefinitions",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<string>(
                name: "Name",
                schema: "automation",
                table: "CustomerJourneys",
                type: "character varying(200)",
                maxLength: 200,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<decimal>(
                name: "TotalAmount",
                schema: "commerce",
                table: "CodOrders",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric");

            migrationBuilder.AlterColumn<decimal>(
                name: "ShippingFee",
                schema: "commerce",
                table: "CodOrders",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric");

            migrationBuilder.AlterColumn<string>(
                name: "OrderNumber",
                schema: "commerce",
                table: "CodOrders",
                type: "character varying(100)",
                maxLength: 100,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<string>(
                name: "Status",
                schema: "commerce",
                table: "CartReservationLocks",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<string>(
                name: "Sku",
                schema: "commerce",
                table: "CartReservationLocks",
                type: "character varying(100)",
                maxLength: 100,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<string>(
                name: "Name",
                schema: "engagement",
                table: "BroadcastCampaigns",
                type: "character varying(200)",
                maxLength: 200,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<decimal>(
                name: "AmountDue",
                schema: "billing",
                table: "BillingInvoiceRecords",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric");

            migrationBuilder.AlterColumn<decimal>(
                name: "Cost",
                schema: "analytics",
                table: "AttributionTouchpoints",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric");

            migrationBuilder.AlterColumn<decimal>(
                name: "MaxMonthlyCostUsd",
                schema: "ai",
                table: "AiAgentBudgets",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric");

            migrationBuilder.AlterColumn<decimal>(
                name: "CostSpentUsd",
                schema: "ai",
                table: "AiAgentBudgets",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric");

            migrationBuilder.AlterColumn<decimal>(
                name: "TotalBudgetUsd",
                schema: "ai",
                table: "AgentMissions",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric");

            migrationBuilder.AlterColumn<decimal>(
                name: "SpentBudgetUsd",
                schema: "ai",
                table: "AgentMissions",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric");

            migrationBuilder.CreateTable(
                name: "B2BProposalRecords",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    CustomerCompany = table.Column<string>(type: "text", nullable: false),
                    CustomerEmail = table.Column<string>(type: "text", nullable: false),
                    TotalAmount = table.Column<decimal>(type: "numeric", nullable: false),
                    MarginPercent = table.Column<decimal>(type: "numeric", nullable: false),
                    Status = table.Column<string>(type: "text", nullable: false),
                    SignedPdfHash = table.Column<string>(type: "text", nullable: true),
                    SignerName = table.Column<string>(type: "text", nullable: true),
                    SignedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_B2BProposalRecords", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "B2BUsageRecords",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    MetricType = table.Column<string>(type: "text", nullable: false),
                    Quantity = table.Column<long>(type: "bigint", nullable: false),
                    TimestampUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_B2BUsageRecords", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "DevRateLimitBuckets",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    BucketKey = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    CurrentTokens = table.Column<double>(type: "double precision", nullable: false),
                    LastRefillUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DevRateLimitBuckets", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "DevSandboxSessions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    IsSandboxMode = table.Column<bool>(type: "boolean", nullable: false),
                    SimulatedLatencyMs = table.Column<int>(type: "integer", nullable: false),
                    SimulateFailures = table.Column<bool>(type: "boolean", nullable: false),
                    FailureRatePercent = table.Column<double>(type: "double precision", nullable: false),
                    MockHeadersJson = table.Column<string>(type: "text", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DevSandboxSessions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SwarmMissions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    MissionId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    TenantId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Title = table.Column<string>(type: "text", nullable: false),
                    HighLevelGoal = table.Column<string>(type: "text", nullable: false),
                    ScratchpadJson = table.Column<string>(type: "text", nullable: false),
                    StartedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    FinishedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SwarmMissions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "UnifiedInboxThreads",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    CustomerEmail = table.Column<string>(type: "text", nullable: false),
                    CustomerPhone = table.Column<string>(type: "text", nullable: false),
                    ActiveChannelsJson = table.Column<string>(type: "text", nullable: false),
                    Status = table.Column<string>(type: "text", nullable: false),
                    Priority = table.Column<string>(type: "text", nullable: false),
                    SlaDueAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    SlaBreached = table.Column<bool>(type: "boolean", nullable: false),
                    AssignedAgentId = table.Column<string>(type: "text", nullable: true),
                    AiDeflected = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UnifiedInboxThreads", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "B2BProposalLineItemRecords",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ProposalId = table.Column<Guid>(type: "uuid", nullable: false),
                    Sku = table.Column<string>(type: "text", nullable: false),
                    Description = table.Column<string>(type: "text", nullable: false),
                    Quantity = table.Column<int>(type: "integer", nullable: false),
                    UnitPrice = table.Column<decimal>(type: "numeric", nullable: false),
                    DiscountPercent = table.Column<decimal>(type: "numeric", nullable: false),
                    TotalPrice = table.Column<decimal>(type: "numeric", nullable: false),
                    CostPrice = table.Column<decimal>(type: "numeric", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_B2BProposalLineItemRecords", x => x.Id);
                    table.ForeignKey(
                        name: "FK_B2BProposalLineItemRecords_B2BProposalRecords_ProposalId",
                        column: x => x.ProposalId,
                        principalTable: "B2BProposalRecords",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "SwarmTasks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    MissionEntityId = table.Column<Guid>(type: "uuid", nullable: false),
                    TaskId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    MissionId = table.Column<string>(type: "text", nullable: false),
                    Title = table.Column<string>(type: "text", nullable: false),
                    Description = table.Column<string>(type: "text", nullable: false),
                    AssignedTo = table.Column<string>(type: "text", nullable: false),
                    Priority = table.Column<string>(type: "text", nullable: false),
                    Status = table.Column<string>(type: "text", nullable: false),
                    RequiresHumanApproval = table.Column<bool>(type: "boolean", nullable: false),
                    OutputArtifact = table.Column<string>(type: "text", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CompletedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SwarmTasks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SwarmTasks_SwarmMissions_MissionEntityId",
                        column: x => x.MissionEntityId,
                        principalTable: "SwarmMissions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "UnifiedInboxMessages",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ThreadId = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Channel = table.Column<string>(type: "text", nullable: false),
                    SenderId = table.Column<string>(type: "text", nullable: false),
                    SenderName = table.Column<string>(type: "text", nullable: false),
                    Content = table.Column<string>(type: "text", nullable: false),
                    Sentiment = table.Column<string>(type: "text", nullable: false),
                    IsFromCustomer = table.Column<bool>(type: "boolean", nullable: false),
                    TimestampUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UnifiedInboxMessages", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UnifiedInboxMessages_UnifiedInboxThreads_ThreadId",
                        column: x => x.ThreadId,
                        principalTable: "UnifiedInboxThreads",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_WorkflowExecutions_TenantId_CreatedAtUtc",
                table: "WorkflowExecutions",
                columns: new[] { "TenantId", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_WorkflowExecutions_TenantId_Status",
                table: "WorkflowExecutions",
                columns: new[] { "TenantId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_OutboundCampaigns_TenantId_CreatedAtUtc",
                table: "OutboundCampaigns",
                columns: new[] { "TenantId", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_OutboundCampaigns_TenantId_Status",
                table: "OutboundCampaigns",
                columns: new[] { "TenantId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_CustomerEvents_TenantId_ContactId_OccurredAtUtc",
                table: "CustomerEvents",
                columns: new[] { "TenantId", "ContactId", "OccurredAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_CustomerEvents_TenantId_OccurredAtUtc",
                table: "CustomerEvents",
                columns: new[] { "TenantId", "OccurredAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_WorkflowGraphs_TenantId_IsActive",
                schema: "automation",
                table: "WorkflowGraphs",
                columns: new[] { "TenantId", "IsActive" });

            migrationBuilder.CreateIndex(
                name: "IX_WilayaZones_CountryCode_Code",
                schema: "tenancy",
                table: "WilayaZones",
                columns: new[] { "CountryCode", "Code" });

            migrationBuilder.CreateIndex(
                name: "IX_WhatsAppTemplates_TenantId_Name",
                schema: "engagement",
                table: "WhatsAppTemplates",
                columns: new[] { "TenantId", "Name" });

            migrationBuilder.CreateIndex(
                name: "IX_Warehouses_TenantId_Code",
                schema: "commerce",
                table: "Warehouses",
                columns: new[] { "TenantId", "Code" });

            migrationBuilder.CreateIndex(
                name: "IX_WarehouseInventories_TenantId_WarehouseId_Sku",
                schema: "commerce",
                table: "WarehouseInventories",
                columns: new[] { "TenantId", "WarehouseId", "Sku" });

            migrationBuilder.CreateIndex(
                name: "IX_VisitorSessions_TenantId_SessionId",
                schema: "analytics",
                table: "VisitorSessions",
                columns: new[] { "TenantId", "SessionId" });

            migrationBuilder.CreateIndex(
                name: "IX_VisitorSessions_TenantId_VisitorId",
                schema: "analytics",
                table: "VisitorSessions",
                columns: new[] { "TenantId", "VisitorId" });

            migrationBuilder.CreateIndex(
                name: "IX_UsageMeterRecords_TenantId_Metric_RecordedAtUtc",
                schema: "billing",
                table: "UsageMeterRecords",
                columns: new[] { "TenantId", "Metric", "RecordedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_TenantSubscriptions_TenantId_Status",
                schema: "billing",
                table: "TenantSubscriptions",
                columns: new[] { "TenantId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_TenantApiKeys_TenantId_KeyPrefix",
                schema: "identity",
                table: "TenantApiKeys",
                columns: new[] { "TenantId", "KeyPrefix" });

            migrationBuilder.CreateIndex(
                name: "IX_TelemetryEvents_TenantId_TimestampUtc",
                schema: "analytics",
                table: "TelemetryEvents",
                columns: new[] { "TenantId", "TimestampUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_SystemRoles_TenantId_RoleName",
                schema: "identity",
                table: "SystemRoles",
                columns: new[] { "TenantId", "RoleName" });

            migrationBuilder.CreateIndex(
                name: "IX_SurveyResponses_TenantId_ContactId",
                schema: "support",
                table: "SurveyResponses",
                columns: new[] { "TenantId", "ContactId" });

            migrationBuilder.CreateIndex(
                name: "IX_SurveyResponses_TenantId_SurveyFormId",
                schema: "support",
                table: "SurveyResponses",
                columns: new[] { "TenantId", "SurveyFormId" });

            migrationBuilder.CreateIndex(
                name: "IX_SurveyForms_TenantId_IsActive",
                schema: "support",
                table: "SurveyForms",
                columns: new[] { "TenantId", "IsActive" });

            migrationBuilder.CreateIndex(
                name: "IX_SlaPolicies_TenantId_Priority",
                schema: "support",
                table: "SlaPolicies",
                columns: new[] { "TenantId", "Priority" });

            migrationBuilder.CreateIndex(
                name: "IX_SenderAccounts_TenantId_Email",
                schema: "engagement",
                table: "SenderAccounts",
                columns: new[] { "TenantId", "Email" });

            migrationBuilder.CreateIndex(
                name: "IX_SenderAccounts_TenantId_Status",
                schema: "engagement",
                table: "SenderAccounts",
                columns: new[] { "TenantId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_SalesCommissionStatements_TenantId_PeriodId_SalesRepUserId",
                schema: "sales",
                table: "SalesCommissionStatements",
                columns: new[] { "TenantId", "PeriodId", "SalesRepUserId" });

            migrationBuilder.CreateIndex(
                name: "IX_SalesCommissionStatements_TenantId_Status",
                schema: "sales",
                table: "SalesCommissionStatements",
                columns: new[] { "TenantId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_SalesCommissionPlans_TenantId_PlanName",
                schema: "sales",
                table: "SalesCommissionPlans",
                columns: new[] { "TenantId", "PlanName" });

            migrationBuilder.CreateIndex(
                name: "IX_RolePermissionClaims_TenantId_RoleId",
                schema: "identity",
                table: "RolePermissionClaims",
                columns: new[] { "TenantId", "RoleId" });

            migrationBuilder.CreateIndex(
                name: "IX_QuoteProposals_TenantId_ContactId",
                schema: "sales",
                table: "QuoteProposals",
                columns: new[] { "TenantId", "ContactId" });

            migrationBuilder.CreateIndex(
                name: "IX_QuoteProposals_TenantId_QuoteNumber",
                schema: "sales",
                table: "QuoteProposals",
                columns: new[] { "TenantId", "QuoteNumber" });

            migrationBuilder.CreateIndex(
                name: "IX_ProductVariants_TenantId_ProductId_SKU",
                schema: "commerce",
                table: "ProductVariants",
                columns: new[] { "TenantId", "ProductId", "SKU" });

            migrationBuilder.CreateIndex(
                name: "IX_Products_TenantId_Slug",
                schema: "commerce",
                table: "Products",
                columns: new[] { "TenantId", "Slug" });

            migrationBuilder.CreateIndex(
                name: "IX_OutboxMessages_TenantId_OccurredOnUtc",
                schema: "platform",
                table: "OutboxMessages",
                columns: new[] { "TenantId", "OccurredOnUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_Messages_TenantId_CreatedAtUtc",
                schema: "engagement",
                table: "Messages",
                columns: new[] { "TenantId", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_JourneySteps_TenantId_JourneyId",
                schema: "automation",
                table: "JourneySteps",
                columns: new[] { "TenantId", "JourneyId" });

            migrationBuilder.CreateIndex(
                name: "IX_HelpCategories_TenantId_Slug",
                schema: "support",
                table: "HelpCategories",
                columns: new[] { "TenantId", "Slug" });

            migrationBuilder.CreateIndex(
                name: "IX_HelpArticles_TenantId_CategoryId_Slug",
                schema: "support",
                table: "HelpArticles",
                columns: new[] { "TenantId", "CategoryId", "Slug" });

            migrationBuilder.CreateIndex(
                name: "IX_FeatureEntitlements_TenantId_PlanTier",
                schema: "billing",
                table: "FeatureEntitlements",
                columns: new[] { "TenantId", "PlanTier" });

            migrationBuilder.CreateIndex(
                name: "IX_DynamicAgentTools_TenantId_Name",
                schema: "ai",
                table: "DynamicAgentTools",
                columns: new[] { "TenantId", "Name" });

            migrationBuilder.CreateIndex(
                name: "IX_DocumentChunks_TenantId_DocumentId",
                schema: "ai",
                table: "DocumentChunks",
                columns: new[] { "TenantId", "DocumentId" });

            migrationBuilder.CreateIndex(
                name: "IX_DigitalContracts_TenantId_Status",
                schema: "sales",
                table: "DigitalContracts",
                columns: new[] { "TenantId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_DigitalContracts_TenantId_Title",
                schema: "sales",
                table: "DigitalContracts",
                columns: new[] { "TenantId", "Title" });

            migrationBuilder.CreateIndex(
                name: "IX_Deals_TenantId_ContactId",
                schema: "crm",
                table: "Deals",
                columns: new[] { "TenantId", "ContactId" });

            migrationBuilder.CreateIndex(
                name: "IX_Deals_TenantId_CreatedAtUtc",
                schema: "crm",
                table: "Deals",
                columns: new[] { "TenantId", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_Deals_TenantId_Stage",
                schema: "crm",
                table: "Deals",
                columns: new[] { "TenantId", "Stage" });

            migrationBuilder.CreateIndex(
                name: "IX_CustomFieldValues_TenantId_EntityId_FieldDefinitionId",
                schema: "crm",
                table: "CustomFieldValues",
                columns: new[] { "TenantId", "EntityId", "FieldDefinitionId" });

            migrationBuilder.CreateIndex(
                name: "IX_CustomFieldDefinitions_TenantId_EntityType_FieldName",
                schema: "crm",
                table: "CustomFieldDefinitions",
                columns: new[] { "TenantId", "EntityType", "FieldName" });

            migrationBuilder.CreateIndex(
                name: "IX_CustomerJourneys_TenantId_Status",
                schema: "automation",
                table: "CustomerJourneys",
                columns: new[] { "TenantId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_Conversations_TenantId_Status",
                schema: "engagement",
                table: "Conversations",
                columns: new[] { "TenantId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_CodOrders_TenantId_CreatedAtUtc",
                schema: "commerce",
                table: "CodOrders",
                columns: new[] { "TenantId", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_CodOrders_TenantId_OrderNumber",
                schema: "commerce",
                table: "CodOrders",
                columns: new[] { "TenantId", "OrderNumber" });

            migrationBuilder.CreateIndex(
                name: "IX_CodOrders_TenantId_Status",
                schema: "commerce",
                table: "CodOrders",
                columns: new[] { "TenantId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_CartReservationLocks_TenantId_Status_ExpiresAtUtc",
                schema: "commerce",
                table: "CartReservationLocks",
                columns: new[] { "TenantId", "Status", "ExpiresAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_CartReservationLocks_TenantId_WarehouseId_Sku",
                schema: "commerce",
                table: "CartReservationLocks",
                columns: new[] { "TenantId", "WarehouseId", "Sku" });

            migrationBuilder.CreateIndex(
                name: "IX_BroadcastCampaigns_TenantId_Status",
                schema: "engagement",
                table: "BroadcastCampaigns",
                columns: new[] { "TenantId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_BillingInvoiceRecords_TenantId_Status",
                schema: "billing",
                table: "BillingInvoiceRecords",
                columns: new[] { "TenantId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_AttributionTouchpoints_TenantId_ContactId_OccurredAtUtc",
                schema: "analytics",
                table: "AttributionTouchpoints",
                columns: new[] { "TenantId", "ContactId", "OccurredAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_AiAgentBudgets_TenantId_CurrentMonthStartUtc",
                schema: "ai",
                table: "AiAgentBudgets",
                columns: new[] { "TenantId", "CurrentMonthStartUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_AgentMissions_TenantId_Status",
                schema: "ai",
                table: "AgentMissions",
                columns: new[] { "TenantId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_Activities_TenantId_CreatedAtUtc",
                schema: "crm",
                table: "Activities",
                columns: new[] { "TenantId", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_B2BProposalLineItemRecords_ProposalId",
                table: "B2BProposalLineItemRecords",
                column: "ProposalId");

            migrationBuilder.CreateIndex(
                name: "IX_B2BProposalRecords_TenantId_Status",
                table: "B2BProposalRecords",
                columns: new[] { "TenantId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_B2BUsageRecords_TenantId_MetricType_TimestampUtc",
                table: "B2BUsageRecords",
                columns: new[] { "TenantId", "MetricType", "TimestampUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_DevRateLimitBuckets_BucketKey",
                table: "DevRateLimitBuckets",
                column: "BucketKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DevSandboxSessions_TenantId",
                table: "DevSandboxSessions",
                column: "TenantId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SwarmMissions_TenantId_MissionId",
                table: "SwarmMissions",
                columns: new[] { "TenantId", "MissionId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SwarmTasks_MissionEntityId_TaskId",
                table: "SwarmTasks",
                columns: new[] { "MissionEntityId", "TaskId" });

            migrationBuilder.CreateIndex(
                name: "IX_UnifiedInboxMessages_ThreadId_TimestampUtc",
                table: "UnifiedInboxMessages",
                columns: new[] { "ThreadId", "TimestampUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_UnifiedInboxThreads_TenantId_CustomerEmail",
                table: "UnifiedInboxThreads",
                columns: new[] { "TenantId", "CustomerEmail" });

            migrationBuilder.CreateIndex(
                name: "IX_UnifiedInboxThreads_TenantId_CustomerPhone",
                table: "UnifiedInboxThreads",
                columns: new[] { "TenantId", "CustomerPhone" });

            migrationBuilder.CreateIndex(
                name: "IX_UnifiedInboxThreads_TenantId_Status",
                table: "UnifiedInboxThreads",
                columns: new[] { "TenantId", "Status" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "B2BProposalLineItemRecords");

            migrationBuilder.DropTable(
                name: "B2BUsageRecords");

            migrationBuilder.DropTable(
                name: "DevRateLimitBuckets");

            migrationBuilder.DropTable(
                name: "DevSandboxSessions");

            migrationBuilder.DropTable(
                name: "SwarmTasks");

            migrationBuilder.DropTable(
                name: "UnifiedInboxMessages");

            migrationBuilder.DropTable(
                name: "B2BProposalRecords");

            migrationBuilder.DropTable(
                name: "SwarmMissions");

            migrationBuilder.DropTable(
                name: "UnifiedInboxThreads");

            migrationBuilder.DropIndex(
                name: "IX_WorkflowExecutions_TenantId_CreatedAtUtc",
                table: "WorkflowExecutions");

            migrationBuilder.DropIndex(
                name: "IX_WorkflowExecutions_TenantId_Status",
                table: "WorkflowExecutions");

            migrationBuilder.DropIndex(
                name: "IX_OutboundCampaigns_TenantId_CreatedAtUtc",
                table: "OutboundCampaigns");

            migrationBuilder.DropIndex(
                name: "IX_OutboundCampaigns_TenantId_Status",
                table: "OutboundCampaigns");

            migrationBuilder.DropIndex(
                name: "IX_CustomerEvents_TenantId_ContactId_OccurredAtUtc",
                table: "CustomerEvents");

            migrationBuilder.DropIndex(
                name: "IX_CustomerEvents_TenantId_OccurredAtUtc",
                table: "CustomerEvents");

            migrationBuilder.DropIndex(
                name: "IX_WorkflowGraphs_TenantId_IsActive",
                schema: "automation",
                table: "WorkflowGraphs");

            migrationBuilder.DropIndex(
                name: "IX_WilayaZones_CountryCode_Code",
                schema: "tenancy",
                table: "WilayaZones");

            migrationBuilder.DropIndex(
                name: "IX_WhatsAppTemplates_TenantId_Name",
                schema: "engagement",
                table: "WhatsAppTemplates");

            migrationBuilder.DropIndex(
                name: "IX_Warehouses_TenantId_Code",
                schema: "commerce",
                table: "Warehouses");

            migrationBuilder.DropIndex(
                name: "IX_WarehouseInventories_TenantId_WarehouseId_Sku",
                schema: "commerce",
                table: "WarehouseInventories");

            migrationBuilder.DropIndex(
                name: "IX_VisitorSessions_TenantId_SessionId",
                schema: "analytics",
                table: "VisitorSessions");

            migrationBuilder.DropIndex(
                name: "IX_VisitorSessions_TenantId_VisitorId",
                schema: "analytics",
                table: "VisitorSessions");

            migrationBuilder.DropIndex(
                name: "IX_UsageMeterRecords_TenantId_Metric_RecordedAtUtc",
                schema: "billing",
                table: "UsageMeterRecords");

            migrationBuilder.DropIndex(
                name: "IX_TenantSubscriptions_TenantId_Status",
                schema: "billing",
                table: "TenantSubscriptions");

            migrationBuilder.DropIndex(
                name: "IX_TenantApiKeys_TenantId_KeyPrefix",
                schema: "identity",
                table: "TenantApiKeys");

            migrationBuilder.DropIndex(
                name: "IX_TelemetryEvents_TenantId_TimestampUtc",
                schema: "analytics",
                table: "TelemetryEvents");

            migrationBuilder.DropIndex(
                name: "IX_SystemRoles_TenantId_RoleName",
                schema: "identity",
                table: "SystemRoles");

            migrationBuilder.DropIndex(
                name: "IX_SurveyResponses_TenantId_ContactId",
                schema: "support",
                table: "SurveyResponses");

            migrationBuilder.DropIndex(
                name: "IX_SurveyResponses_TenantId_SurveyFormId",
                schema: "support",
                table: "SurveyResponses");

            migrationBuilder.DropIndex(
                name: "IX_SurveyForms_TenantId_IsActive",
                schema: "support",
                table: "SurveyForms");

            migrationBuilder.DropIndex(
                name: "IX_SlaPolicies_TenantId_Priority",
                schema: "support",
                table: "SlaPolicies");

            migrationBuilder.DropIndex(
                name: "IX_SenderAccounts_TenantId_Email",
                schema: "engagement",
                table: "SenderAccounts");

            migrationBuilder.DropIndex(
                name: "IX_SenderAccounts_TenantId_Status",
                schema: "engagement",
                table: "SenderAccounts");

            migrationBuilder.DropIndex(
                name: "IX_SalesCommissionStatements_TenantId_PeriodId_SalesRepUserId",
                schema: "sales",
                table: "SalesCommissionStatements");

            migrationBuilder.DropIndex(
                name: "IX_SalesCommissionStatements_TenantId_Status",
                schema: "sales",
                table: "SalesCommissionStatements");

            migrationBuilder.DropIndex(
                name: "IX_SalesCommissionPlans_TenantId_PlanName",
                schema: "sales",
                table: "SalesCommissionPlans");

            migrationBuilder.DropIndex(
                name: "IX_RolePermissionClaims_TenantId_RoleId",
                schema: "identity",
                table: "RolePermissionClaims");

            migrationBuilder.DropIndex(
                name: "IX_QuoteProposals_TenantId_ContactId",
                schema: "sales",
                table: "QuoteProposals");

            migrationBuilder.DropIndex(
                name: "IX_QuoteProposals_TenantId_QuoteNumber",
                schema: "sales",
                table: "QuoteProposals");

            migrationBuilder.DropIndex(
                name: "IX_ProductVariants_TenantId_ProductId_SKU",
                schema: "commerce",
                table: "ProductVariants");

            migrationBuilder.DropIndex(
                name: "IX_Products_TenantId_Slug",
                schema: "commerce",
                table: "Products");

            migrationBuilder.DropIndex(
                name: "IX_OutboxMessages_TenantId_OccurredOnUtc",
                schema: "platform",
                table: "OutboxMessages");

            migrationBuilder.DropIndex(
                name: "IX_Messages_TenantId_CreatedAtUtc",
                schema: "engagement",
                table: "Messages");

            migrationBuilder.DropIndex(
                name: "IX_JourneySteps_TenantId_JourneyId",
                schema: "automation",
                table: "JourneySteps");

            migrationBuilder.DropIndex(
                name: "IX_HelpCategories_TenantId_Slug",
                schema: "support",
                table: "HelpCategories");

            migrationBuilder.DropIndex(
                name: "IX_HelpArticles_TenantId_CategoryId_Slug",
                schema: "support",
                table: "HelpArticles");

            migrationBuilder.DropIndex(
                name: "IX_FeatureEntitlements_TenantId_PlanTier",
                schema: "billing",
                table: "FeatureEntitlements");

            migrationBuilder.DropIndex(
                name: "IX_DynamicAgentTools_TenantId_Name",
                schema: "ai",
                table: "DynamicAgentTools");

            migrationBuilder.DropIndex(
                name: "IX_DocumentChunks_TenantId_DocumentId",
                schema: "ai",
                table: "DocumentChunks");

            migrationBuilder.DropIndex(
                name: "IX_DigitalContracts_TenantId_Status",
                schema: "sales",
                table: "DigitalContracts");

            migrationBuilder.DropIndex(
                name: "IX_DigitalContracts_TenantId_Title",
                schema: "sales",
                table: "DigitalContracts");

            migrationBuilder.DropIndex(
                name: "IX_Deals_TenantId_ContactId",
                schema: "crm",
                table: "Deals");

            migrationBuilder.DropIndex(
                name: "IX_Deals_TenantId_CreatedAtUtc",
                schema: "crm",
                table: "Deals");

            migrationBuilder.DropIndex(
                name: "IX_Deals_TenantId_Stage",
                schema: "crm",
                table: "Deals");

            migrationBuilder.DropIndex(
                name: "IX_CustomFieldValues_TenantId_EntityId_FieldDefinitionId",
                schema: "crm",
                table: "CustomFieldValues");

            migrationBuilder.DropIndex(
                name: "IX_CustomFieldDefinitions_TenantId_EntityType_FieldName",
                schema: "crm",
                table: "CustomFieldDefinitions");

            migrationBuilder.DropIndex(
                name: "IX_CustomerJourneys_TenantId_Status",
                schema: "automation",
                table: "CustomerJourneys");

            migrationBuilder.DropIndex(
                name: "IX_Conversations_TenantId_Status",
                schema: "engagement",
                table: "Conversations");

            migrationBuilder.DropIndex(
                name: "IX_CodOrders_TenantId_CreatedAtUtc",
                schema: "commerce",
                table: "CodOrders");

            migrationBuilder.DropIndex(
                name: "IX_CodOrders_TenantId_OrderNumber",
                schema: "commerce",
                table: "CodOrders");

            migrationBuilder.DropIndex(
                name: "IX_CodOrders_TenantId_Status",
                schema: "commerce",
                table: "CodOrders");

            migrationBuilder.DropIndex(
                name: "IX_CartReservationLocks_TenantId_Status_ExpiresAtUtc",
                schema: "commerce",
                table: "CartReservationLocks");

            migrationBuilder.DropIndex(
                name: "IX_CartReservationLocks_TenantId_WarehouseId_Sku",
                schema: "commerce",
                table: "CartReservationLocks");

            migrationBuilder.DropIndex(
                name: "IX_BroadcastCampaigns_TenantId_Status",
                schema: "engagement",
                table: "BroadcastCampaigns");

            migrationBuilder.DropIndex(
                name: "IX_BillingInvoiceRecords_TenantId_Status",
                schema: "billing",
                table: "BillingInvoiceRecords");

            migrationBuilder.DropIndex(
                name: "IX_AttributionTouchpoints_TenantId_ContactId_OccurredAtUtc",
                schema: "analytics",
                table: "AttributionTouchpoints");

            migrationBuilder.DropIndex(
                name: "IX_AiAgentBudgets_TenantId_CurrentMonthStartUtc",
                schema: "ai",
                table: "AiAgentBudgets");

            migrationBuilder.DropIndex(
                name: "IX_AgentMissions_TenantId_Status",
                schema: "ai",
                table: "AgentMissions");

            migrationBuilder.DropIndex(
                name: "IX_Activities_TenantId_CreatedAtUtc",
                schema: "crm",
                table: "Activities");

            migrationBuilder.RenameTable(
                name: "Workspaces",
                schema: "tenancy",
                newName: "Workspaces");

            migrationBuilder.RenameTable(
                name: "WorkflowGraphs",
                schema: "automation",
                newName: "WorkflowGraphs");

            migrationBuilder.RenameTable(
                name: "WilayaZones",
                schema: "tenancy",
                newName: "WilayaZones");

            migrationBuilder.RenameTable(
                name: "WhatsAppTemplates",
                schema: "engagement",
                newName: "WhatsAppTemplates");

            migrationBuilder.RenameTable(
                name: "Warehouses",
                schema: "commerce",
                newName: "Warehouses");

            migrationBuilder.RenameTable(
                name: "WarehouseInventories",
                schema: "commerce",
                newName: "WarehouseInventories");

            migrationBuilder.RenameTable(
                name: "VisitorSessions",
                schema: "analytics",
                newName: "VisitorSessions");

            migrationBuilder.RenameTable(
                name: "Users",
                schema: "identity",
                newName: "Users");

            migrationBuilder.RenameTable(
                name: "UsageMeterRecords",
                schema: "billing",
                newName: "UsageMeterRecords");

            migrationBuilder.RenameTable(
                name: "TenantSubscriptions",
                schema: "billing",
                newName: "TenantSubscriptions");

            migrationBuilder.RenameTable(
                name: "Tenants",
                schema: "tenancy",
                newName: "Tenants");

            migrationBuilder.RenameTable(
                name: "TenantApiKeys",
                schema: "identity",
                newName: "TenantApiKeys");

            migrationBuilder.RenameTable(
                name: "TelemetryEvents",
                schema: "analytics",
                newName: "TelemetryEvents");

            migrationBuilder.RenameTable(
                name: "SystemRoles",
                schema: "identity",
                newName: "SystemRoles");

            migrationBuilder.RenameTable(
                name: "SurveyResponses",
                schema: "support",
                newName: "SurveyResponses");

            migrationBuilder.RenameTable(
                name: "SurveyForms",
                schema: "support",
                newName: "SurveyForms");

            migrationBuilder.RenameTable(
                name: "SlaPolicies",
                schema: "support",
                newName: "SlaPolicies");

            migrationBuilder.RenameTable(
                name: "SenderAccounts",
                schema: "engagement",
                newName: "SenderAccounts");

            migrationBuilder.RenameTable(
                name: "SalesCommissionStatements",
                schema: "sales",
                newName: "SalesCommissionStatements");

            migrationBuilder.RenameTable(
                name: "SalesCommissionPlans",
                schema: "sales",
                newName: "SalesCommissionPlans");

            migrationBuilder.RenameTable(
                name: "RolePermissionClaims",
                schema: "identity",
                newName: "RolePermissionClaims");

            migrationBuilder.RenameTable(
                name: "QuoteProposals",
                schema: "sales",
                newName: "QuoteProposals");

            migrationBuilder.RenameTable(
                name: "QuoteLineItems",
                schema: "sales",
                newName: "QuoteLineItems");

            migrationBuilder.RenameTable(
                name: "ProductVariants",
                schema: "commerce",
                newName: "ProductVariants");

            migrationBuilder.RenameTable(
                name: "Products",
                schema: "commerce",
                newName: "Products");

            migrationBuilder.RenameTable(
                name: "OutboxMessages",
                schema: "platform",
                newName: "OutboxMessages");

            migrationBuilder.RenameTable(
                name: "Messages",
                schema: "engagement",
                newName: "Messages");

            migrationBuilder.RenameTable(
                name: "JourneySteps",
                schema: "automation",
                newName: "JourneySteps");

            migrationBuilder.RenameTable(
                name: "Inboxes",
                schema: "engagement",
                newName: "Inboxes");

            migrationBuilder.RenameTable(
                name: "HelpCategories",
                schema: "support",
                newName: "HelpCategories");

            migrationBuilder.RenameTable(
                name: "HelpArticles",
                schema: "support",
                newName: "HelpArticles");

            migrationBuilder.RenameTable(
                name: "FeatureEntitlements",
                schema: "billing",
                newName: "FeatureEntitlements");

            migrationBuilder.RenameTable(
                name: "DynamicAgentTools",
                schema: "ai",
                newName: "DynamicAgentTools");

            migrationBuilder.RenameTable(
                name: "DocumentChunks",
                schema: "ai",
                newName: "DocumentChunks");

            migrationBuilder.RenameTable(
                name: "DigitalContracts",
                schema: "sales",
                newName: "DigitalContracts");

            migrationBuilder.RenameTable(
                name: "Deals",
                schema: "crm",
                newName: "Deals");

            migrationBuilder.RenameTable(
                name: "CustomFieldValues",
                schema: "crm",
                newName: "CustomFieldValues");

            migrationBuilder.RenameTable(
                name: "CustomFieldDefinitions",
                schema: "crm",
                newName: "CustomFieldDefinitions");

            migrationBuilder.RenameTable(
                name: "CustomerJourneys",
                schema: "automation",
                newName: "CustomerJourneys");

            migrationBuilder.RenameTable(
                name: "Conversations",
                schema: "engagement",
                newName: "Conversations");

            migrationBuilder.RenameTable(
                name: "Contacts",
                schema: "crm",
                newName: "Contacts");

            migrationBuilder.RenameTable(
                name: "Companies",
                schema: "crm",
                newName: "Companies");

            migrationBuilder.RenameTable(
                name: "CodOrders",
                schema: "commerce",
                newName: "CodOrders");

            migrationBuilder.RenameTable(
                name: "CartReservationLocks",
                schema: "commerce",
                newName: "CartReservationLocks");

            migrationBuilder.RenameTable(
                name: "BroadcastCampaigns",
                schema: "engagement",
                newName: "BroadcastCampaigns");

            migrationBuilder.RenameTable(
                name: "BillingInvoiceRecords",
                schema: "billing",
                newName: "BillingInvoiceRecords");

            migrationBuilder.RenameTable(
                name: "AttributionTouchpoints",
                schema: "analytics",
                newName: "AttributionTouchpoints");

            migrationBuilder.RenameTable(
                name: "AiDrafts",
                schema: "engagement",
                newName: "AiDrafts");

            migrationBuilder.RenameTable(
                name: "AiAgentBudgets",
                schema: "ai",
                newName: "AiAgentBudgets");

            migrationBuilder.RenameTable(
                name: "AgentMissions",
                schema: "ai",
                newName: "AgentMissions");

            migrationBuilder.RenameTable(
                name: "Activities",
                schema: "crm",
                newName: "Activities");

            migrationBuilder.AlterColumn<string>(
                name: "TenantId",
                schema: "automation",
                table: "workflows",
                type: "character varying(100)",
                maxLength: 100,
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AlterColumn<string>(
                name: "TenantId",
                schema: "customer_data",
                table: "visitor_sessions",
                type: "character varying(100)",
                maxLength: 100,
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AlterColumn<string>(
                name: "TenantId",
                schema: "automation",
                table: "telemetry_traces",
                type: "character varying(100)",
                maxLength: 100,
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AlterColumn<string>(
                name: "TenantId",
                schema: "customer_data",
                table: "profiles",
                type: "character varying(100)",
                maxLength: 100,
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AlterColumn<string>(
                name: "TenantId",
                schema: "customer_data",
                table: "identity_links",
                type: "character varying(100)",
                maxLength: 100,
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AlterColumn<string>(
                name: "TenantId",
                schema: "customer_data",
                table: "identities",
                type: "character varying(100)",
                maxLength: 100,
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AlterColumn<string>(
                name: "TenantId",
                schema: "advertising",
                table: "conversions",
                type: "character varying(100)",
                maxLength: 100,
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AlterColumn<string>(
                name: "TenantId",
                schema: "advertising",
                table: "campaign_roas_snapshots",
                type: "character varying(100)",
                maxLength: 100,
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AlterColumn<string>(
                name: "TenantId",
                schema: "advertising",
                table: "audience_syncs",
                type: "character varying(100)",
                maxLength: 100,
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AlterColumn<string>(
                name: "Name",
                table: "WorkflowGraphs",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(200)",
                oldMaxLength: 200);

            migrationBuilder.AlterColumn<decimal>(
                name: "StandardShippingFee",
                table: "WilayaZones",
                type: "numeric",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric(18,2)",
                oldPrecision: 18,
                oldScale: 2);

            migrationBuilder.AlterColumn<string>(
                name: "CountryCode",
                table: "WilayaZones",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(10)",
                oldMaxLength: 10);

            migrationBuilder.AlterColumn<string>(
                name: "Code",
                table: "WilayaZones",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(50)",
                oldMaxLength: 50);

            migrationBuilder.AlterColumn<string>(
                name: "Name",
                table: "WhatsAppTemplates",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(200)",
                oldMaxLength: 200);

            migrationBuilder.AlterColumn<string>(
                name: "Name",
                table: "Warehouses",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(200)",
                oldMaxLength: 200);

            migrationBuilder.AlterColumn<string>(
                name: "Code",
                table: "Warehouses",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(50)",
                oldMaxLength: 50);

            migrationBuilder.AlterColumn<string>(
                name: "Sku",
                table: "WarehouseInventories",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(100)",
                oldMaxLength: 100);

            migrationBuilder.AlterColumn<decimal>(
                name: "MonthlyPrice",
                table: "TenantSubscriptions",
                type: "numeric",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric(18,2)",
                oldPrecision: 18,
                oldScale: 2);

            migrationBuilder.AlterColumn<string>(
                name: "KeyPrefix",
                table: "TenantApiKeys",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(50)",
                oldMaxLength: 50);

            migrationBuilder.AlterColumn<string>(
                name: "KeyMasked",
                table: "TenantApiKeys",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(100)",
                oldMaxLength: 100);

            migrationBuilder.AlterColumn<string>(
                name: "RoleName",
                table: "SystemRoles",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(100)",
                oldMaxLength: 100);

            migrationBuilder.AlterColumn<string>(
                name: "Title",
                table: "SurveyForms",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(200)",
                oldMaxLength: 200);

            migrationBuilder.AlterColumn<string>(
                name: "Name",
                table: "SlaPolicies",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(200)",
                oldMaxLength: 200);

            migrationBuilder.AlterColumn<string>(
                name: "Email",
                table: "SenderAccounts",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(256)",
                oldMaxLength: 256);

            migrationBuilder.AlterColumn<decimal>(
                name: "TotalDealsValue",
                table: "SalesCommissionStatements",
                type: "numeric",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric(18,2)",
                oldPrecision: 18,
                oldScale: 2);

            migrationBuilder.AlterColumn<decimal>(
                name: "CalculatedCommission",
                table: "SalesCommissionStatements",
                type: "numeric",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric(18,2)",
                oldPrecision: 18,
                oldScale: 2);

            migrationBuilder.AlterColumn<decimal>(
                name: "QuotaThresholdPercent",
                table: "SalesCommissionPlans",
                type: "numeric",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric(18,2)",
                oldPrecision: 18,
                oldScale: 2);

            migrationBuilder.AlterColumn<string>(
                name: "PlanName",
                table: "SalesCommissionPlans",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(200)",
                oldMaxLength: 200);

            migrationBuilder.AlterColumn<decimal>(
                name: "BaseRatePercent",
                table: "SalesCommissionPlans",
                type: "numeric",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric(18,2)",
                oldPrecision: 18,
                oldScale: 2);

            migrationBuilder.AlterColumn<decimal>(
                name: "AcceleratedRatePercent",
                table: "SalesCommissionPlans",
                type: "numeric",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric(18,2)",
                oldPrecision: 18,
                oldScale: 2);

            migrationBuilder.AlterColumn<string>(
                name: "Resource",
                table: "RolePermissionClaims",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(100)",
                oldMaxLength: 100);

            migrationBuilder.AlterColumn<string>(
                name: "Action",
                table: "RolePermissionClaims",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(100)",
                oldMaxLength: 100);

            migrationBuilder.AlterColumn<decimal>(
                name: "TotalAmount",
                table: "QuoteProposals",
                type: "numeric",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric(18,2)",
                oldPrecision: 18,
                oldScale: 2);

            migrationBuilder.AlterColumn<decimal>(
                name: "TaxPercent",
                table: "QuoteProposals",
                type: "numeric",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric(18,2)",
                oldPrecision: 18,
                oldScale: 2);

            migrationBuilder.AlterColumn<decimal>(
                name: "Subtotal",
                table: "QuoteProposals",
                type: "numeric",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric(18,2)",
                oldPrecision: 18,
                oldScale: 2);

            migrationBuilder.AlterColumn<string>(
                name: "QuoteNumber",
                table: "QuoteProposals",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(100)",
                oldMaxLength: 100);

            migrationBuilder.AlterColumn<decimal>(
                name: "DiscountPercent",
                table: "QuoteProposals",
                type: "numeric",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric(18,2)",
                oldPrecision: 18,
                oldScale: 2);

            migrationBuilder.AlterColumn<decimal>(
                name: "UnitPrice",
                table: "QuoteLineItems",
                type: "numeric",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric(18,2)",
                oldPrecision: 18,
                oldScale: 2);

            migrationBuilder.AlterColumn<string>(
                name: "SKU",
                table: "ProductVariants",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(100)",
                oldMaxLength: 100);

            migrationBuilder.AlterColumn<decimal>(
                name: "Price",
                table: "ProductVariants",
                type: "numeric",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric(18,2)",
                oldPrecision: 18,
                oldScale: 2);

            migrationBuilder.AlterColumn<string>(
                name: "Slug",
                table: "Products",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(100)",
                oldMaxLength: 100);

            migrationBuilder.AlterColumn<string>(
                name: "Name",
                table: "Products",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(200)",
                oldMaxLength: 200);

            migrationBuilder.AlterColumn<decimal>(
                name: "BasePrice",
                table: "Products",
                type: "numeric",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric(18,2)",
                oldPrecision: 18,
                oldScale: 2);

            migrationBuilder.AlterColumn<string>(
                name: "Slug",
                table: "HelpCategories",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(100)",
                oldMaxLength: 100);

            migrationBuilder.AlterColumn<string>(
                name: "Name",
                table: "HelpCategories",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(200)",
                oldMaxLength: 200);

            migrationBuilder.AlterColumn<string>(
                name: "Title",
                table: "HelpArticles",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(256)",
                oldMaxLength: 256);

            migrationBuilder.AlterColumn<string>(
                name: "Slug",
                table: "HelpArticles",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(150)",
                oldMaxLength: 150);

            migrationBuilder.AlterColumn<string>(
                name: "Name",
                table: "DynamicAgentTools",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(150)",
                oldMaxLength: 150);

            migrationBuilder.AlterColumn<string>(
                name: "Title",
                table: "DigitalContracts",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(200)",
                oldMaxLength: 200);

            migrationBuilder.AlterColumn<decimal>(
                name: "ContractValue",
                table: "DigitalContracts",
                type: "numeric",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric(18,2)",
                oldPrecision: 18,
                oldScale: 2);

            migrationBuilder.AlterColumn<string>(
                name: "Title",
                table: "Deals",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(200)",
                oldMaxLength: 200);

            migrationBuilder.AlterColumn<decimal>(
                name: "Amount",
                table: "Deals",
                type: "numeric",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric(18,2)",
                oldPrecision: 18,
                oldScale: 2);

            migrationBuilder.AlterColumn<decimal>(
                name: "ValueNumber",
                table: "CustomFieldValues",
                type: "numeric",
                nullable: true,
                oldClrType: typeof(decimal),
                oldType: "numeric(18,4)",
                oldPrecision: 18,
                oldScale: 4,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "FieldName",
                table: "CustomFieldDefinitions",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(100)",
                oldMaxLength: 100);

            migrationBuilder.AlterColumn<string>(
                name: "EntityType",
                table: "CustomFieldDefinitions",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(50)",
                oldMaxLength: 50);

            migrationBuilder.AlterColumn<string>(
                name: "Name",
                table: "CustomerJourneys",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(200)",
                oldMaxLength: 200);

            migrationBuilder.AlterColumn<decimal>(
                name: "TotalAmount",
                table: "CodOrders",
                type: "numeric",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric(18,2)",
                oldPrecision: 18,
                oldScale: 2);

            migrationBuilder.AlterColumn<decimal>(
                name: "ShippingFee",
                table: "CodOrders",
                type: "numeric",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric(18,2)",
                oldPrecision: 18,
                oldScale: 2);

            migrationBuilder.AlterColumn<string>(
                name: "OrderNumber",
                table: "CodOrders",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(100)",
                oldMaxLength: 100);

            migrationBuilder.AlterColumn<string>(
                name: "Status",
                table: "CartReservationLocks",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(50)",
                oldMaxLength: 50);

            migrationBuilder.AlterColumn<string>(
                name: "Sku",
                table: "CartReservationLocks",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(100)",
                oldMaxLength: 100);

            migrationBuilder.AlterColumn<string>(
                name: "Name",
                table: "BroadcastCampaigns",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(200)",
                oldMaxLength: 200);

            migrationBuilder.AlterColumn<decimal>(
                name: "AmountDue",
                table: "BillingInvoiceRecords",
                type: "numeric",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric(18,2)",
                oldPrecision: 18,
                oldScale: 2);

            migrationBuilder.AlterColumn<decimal>(
                name: "Cost",
                table: "AttributionTouchpoints",
                type: "numeric",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric(18,2)",
                oldPrecision: 18,
                oldScale: 2);

            migrationBuilder.AlterColumn<decimal>(
                name: "MaxMonthlyCostUsd",
                table: "AiAgentBudgets",
                type: "numeric",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric(18,2)",
                oldPrecision: 18,
                oldScale: 2);

            migrationBuilder.AlterColumn<decimal>(
                name: "CostSpentUsd",
                table: "AiAgentBudgets",
                type: "numeric",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric(18,2)",
                oldPrecision: 18,
                oldScale: 2);

            migrationBuilder.AlterColumn<decimal>(
                name: "TotalBudgetUsd",
                table: "AgentMissions",
                type: "numeric",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric(18,2)",
                oldPrecision: 18,
                oldScale: 2);

            migrationBuilder.AlterColumn<decimal>(
                name: "SpentBudgetUsd",
                table: "AgentMissions",
                type: "numeric",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric(18,2)",
                oldPrecision: 18,
                oldScale: 2);
        }
    }
}
