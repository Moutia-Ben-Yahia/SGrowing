export interface SeoAuditReportDto {
  id: string;
  tenantId: string;
  targetUrl: string;
  overallHealthScore: number;
  scannedPagesCount: number;
  brokenLinksCount: number;
  missingMetaCount: number;
  missingH1Count: number;
  slowPagesCount: number;
  status: number; // 1: Queued, 2: Crawling, 3: Completed, 4: Failed
  createdAtUtc: string;
  completedAtUtc?: string;
}

export interface PageAuditResultDto {
  id: string;
  tenantId: string;
  auditReportId: string;
  pageUrl: string;
  statusCode: number;
  title: string;
  metaDescription: string;
  h1Tag: string;
  hasCanonical: boolean;
  hasOpenGraph: boolean;
  hasSchemaOrgJsonLd: boolean;
  loadTimeMs: number;
  healthScore: number;
  issuesJson: string; // JSON array of string issue tags
  createdAtUtc: string;
}

export interface SerpKeywordTrackingDto {
  id: string;
  tenantId: string;
  keyword: string;
  targetUrl: string;
  countryCode: string;
  searchEngine: string;
  currentRank?: number;
  previousRank?: number;
  bestRank?: number;
  searchVolumeMonthly: number;
  rankHistoryJson: string;
  trackedDateUtc: string;
}

export interface AeoBrandMentionDto {
  id: string;
  tenantId: string;
  brandName: string;
  aiEngine: string;
  promptQuery: string;
  isBrandMentioned: boolean;
  mentionSentiment: string;
  snippetText: string;
  citationRank?: number;
  capturedAtUtc: string;
}

export interface GeoScoreReportDto {
  tenantId: string;
  brandName: string;
  domainUrl: string;
  overallScore: number;
  citabilityRatePercent: number;
  structuredSchemaScore: number;
  llmsTxtReadinessScore: number;
  passageDensityScore: number;
  aiEnginesEvaluated: string[];
  actionableRecommendations: string[];
  evaluatedAtUtc: string;
}
