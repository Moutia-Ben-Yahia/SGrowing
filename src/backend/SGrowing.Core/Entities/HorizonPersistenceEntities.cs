using System;

namespace SGrowing.Core.Entities;

/// <summary>
/// Persisted cashflow forecast projection per tenant and horizon date.
/// Schema: analytics
/// </summary>
public class CashflowForecastEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public DateTime ForecastDateUtc { get; set; }
    public decimal ProjectedCashBalance { get; set; }
    public decimal NetBurnRate { get; set; }
    public int RunwayDays { get; set; }
    public decimal ConfidenceIntervalLower { get; set; }
    public decimal ConfidenceIntervalUpper { get; set; }
    public string DeficitSeverity { get; set; } = "Healthy"; // Healthy, Warning, Critical
    public DateTime CalculatedAtUtc { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Persisted SEO topic cluster mapping pillar keyword to subtopics.
/// Schema: seo
/// </summary>
public class TopicalClusterEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public string ClusterId { get; set; } = string.Empty;
    public string PillarTopic { get; set; } = string.Empty;
    public string TargetKeyword { get; set; } = string.Empty;
    public double TopicalAuthorityScore { get; set; }
    public int TotalSubArticles { get; set; }
    public int MissingSubTopicsCount { get; set; }
    public string CoveredEntitiesJson { get; set; } = "[]";
    public string RecommendedClusterExpansionsJson { get; set; } = "[]";
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Persisted CPQ dynamic price elasticity curve per product.
/// Schema: sales
/// </summary>
public class DynamicElasticityPricingEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public string ProductId { get; set; } = string.Empty;
    public decimal BasePrice { get; set; }
    public decimal CurrentElasticityScore { get; set; }
    public string ElasticityCategory { get; set; } = "Inelastic"; // Inelastic, Unitary, Elastic
    public decimal OptimalPrice { get; set; }
    public decimal MaxContributionMargin { get; set; }
    public decimal RecommendedDiscountFloorPercent { get; set; } = 5.0m;
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Persisted contextual multi-armed bandit state for retention offers.
/// Schema: retention
/// </summary>
public class RetentionBanditArmEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public string ArmId { get; set; } = string.Empty;
    public string IncentiveType { get; set; } = string.Empty;
    public double Alpha { get; set; } = 2.0;
    public double Beta { get; set; } = 2.0;
    public int TotalTrials { get; set; }
    public decimal TotalLtvRetainedDzd { get; set; }
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
}
