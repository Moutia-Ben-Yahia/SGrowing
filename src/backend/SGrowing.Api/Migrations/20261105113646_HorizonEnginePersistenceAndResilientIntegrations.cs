using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SGrowing.Api.Migrations
{
    /// <inheritdoc />
    public partial class HorizonEnginePersistenceAndResilientIntegrations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CashflowForecasts",
                schema: "analytics",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    ForecastDateUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ProjectedCashBalance = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    NetBurnRate = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    RunwayDays = table.Column<int>(type: "integer", nullable: false),
                    ConfidenceIntervalLower = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    ConfidenceIntervalUpper = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    DeficitSeverity = table.Column<string>(type: "text", nullable: false),
                    CalculatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CashflowForecasts", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PricingElasticityModels",
                schema: "sales",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProductId = table.Column<string>(type: "text", nullable: false),
                    BasePrice = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    CurrentElasticityScore = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    ElasticityCategory = table.Column<string>(type: "text", nullable: false),
                    OptimalPrice = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    MaxContributionMargin = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    RecommendedDiscountFloorPercent = table.Column<decimal>(type: "numeric", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PricingElasticityModels", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "RetentionBanditArms",
                schema: "engagement",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    ArmId = table.Column<string>(type: "text", nullable: false),
                    IncentiveType = table.Column<string>(type: "text", nullable: false),
                    Alpha = table.Column<double>(type: "double precision", nullable: false),
                    Beta = table.Column<double>(type: "double precision", nullable: false),
                    TotalTrials = table.Column<int>(type: "integer", nullable: false),
                    TotalLtvRetainedDzd = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RetentionBanditArms", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "TopicClusters",
                schema: "engagement",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    ClusterId = table.Column<string>(type: "text", nullable: false),
                    PillarTopic = table.Column<string>(type: "text", nullable: false),
                    TargetKeyword = table.Column<string>(type: "text", nullable: false),
                    TopicalAuthorityScore = table.Column<double>(type: "double precision", nullable: false),
                    TotalSubArticles = table.Column<int>(type: "integer", nullable: false),
                    MissingSubTopicsCount = table.Column<int>(type: "integer", nullable: false),
                    CoveredEntitiesJson = table.Column<string>(type: "text", nullable: false),
                    RecommendedClusterExpansionsJson = table.Column<string>(type: "text", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TopicClusters", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CashflowForecasts_TenantId_ForecastDateUtc",
                schema: "analytics",
                table: "CashflowForecasts",
                columns: new[] { "TenantId", "ForecastDateUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_PricingElasticityModels_TenantId_ProductId",
                schema: "sales",
                table: "PricingElasticityModels",
                columns: new[] { "TenantId", "ProductId" });

            migrationBuilder.CreateIndex(
                name: "IX_RetentionBanditArms_TenantId_IncentiveType",
                schema: "engagement",
                table: "RetentionBanditArms",
                columns: new[] { "TenantId", "IncentiveType" });

            migrationBuilder.CreateIndex(
                name: "IX_TopicClusters_TenantId_ClusterId",
                schema: "engagement",
                table: "TopicClusters",
                columns: new[] { "TenantId", "ClusterId" });

            migrationBuilder.CreateIndex(
                name: "IX_TopicClusters_TenantId_TargetKeyword",
                schema: "engagement",
                table: "TopicClusters",
                columns: new[] { "TenantId", "TargetKeyword" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CashflowForecasts",
                schema: "analytics");

            migrationBuilder.DropTable(
                name: "PricingElasticityModels",
                schema: "sales");

            migrationBuilder.DropTable(
                name: "RetentionBanditArms",
                schema: "engagement");

            migrationBuilder.DropTable(
                name: "TopicClusters",
                schema: "engagement");
        }
    }
}
