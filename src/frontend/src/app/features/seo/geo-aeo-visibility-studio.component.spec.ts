// @vitest-environment jsdom
import '@angular/compiler';
import { describe, it, expect, beforeEach } from 'vitest';
import { GeoAeoVisibilityStudioComponent } from './geo-aeo-visibility-studio.component';
import { TranslationService } from '../../core/services/translation.service';

describe('GeoAeoVisibilityStudioComponent', () => {
  let component: GeoAeoVisibilityStudioComponent;
  let mockTranslation: TranslationService;

  beforeEach(() => {
    mockTranslation = new TranslationService();
    component = new GeoAeoVisibilityStudioComponent(mockTranslation);
  });

  it('should initialize with high overall GEO score and active citations', () => {
    expect(component).toBeTruthy();
    expect(component.overallGeoScore()).toBeGreaterThan(70);
    expect(component.shareOfModelVoice()).toBeGreaterThan(60);
    expect(component.totalQueriesCount()).toBe(4);
    expect(component.crawlerReadinessPercent()).toBe(100);
    expect(component.activeTab()).toBe('radar');
  });

  it('should switch active tabs between radar, auditor, and governance', () => {
    expect(component.activeTab()).toBe('radar');

    component.setActiveTab('auditor');
    expect(component.activeTab()).toBe('auditor');

    component.setActiveTab('governance');
    expect(component.activeTab()).toBe('governance');

    component.setActiveTab('radar');
    expect(component.activeTab()).toBe('radar');
  });

  it('should filter citations by AI engine', () => {
    component.setEngineFilter('Perplexity');
    expect(component.selectedEngineFilter()).toBe('Perplexity');
    const filtered = component.filteredCitations();
    expect(filtered.length).toBe(1);
    expect(filtered[0].engine).toBe('Perplexity');

    component.setEngineFilter('ChatGPT_Search');
    expect(component.filteredCitations().length).toBe(1);
    expect(component.filteredCitations()[0].engine).toBe('ChatGPT_Search');

    component.setEngineFilter('ALL');
    expect(component.filteredCitations().length).toBe(4);
  });

  it('should filter citations by query keyword search', () => {
    component.searchQueryFilter.set('omnichannel');
    const filtered = component.filteredCitations();
    expect(filtered.length).toBe(1);
    expect(filtered[0].query).toContain('omnichannel');

    component.searchQueryFilter.set('nonexistent keyword query 12345');
    expect(component.filteredCitations().length).toBe(0);

    component.searchQueryFilter.set('');
    expect(component.filteredCitations().length).toBe(4);
  });

  it('should track a new citation query and prepend it to the feed', () => {
    component.newQueryText.set('best b2b enterprise revops copilot');
    component.newQueryEngine.set('ChatGPT_Search');
    component.newQueryRank.set(1);
    component.newQuerySentiment.set('Positive');
    component.newQueryUrl.set('https://sgrowing.io/revops');

    component.trackNewQuery();

    expect(component.totalQueriesCount()).toBe(5);
    const latest = component.citations()[0];
    expect(latest.query).toBe('best b2b enterprise revops copilot');
    expect(latest.engine).toBe('ChatGPT_Search');
    expect(latest.citationRank).toBe(1);
    expect(latest.shareOfVoice).toBe(85.0);
    expect(component.newQueryText()).toBe('');
    expect(component.isAddingQuery()).toBe(false);
  });

  it('should ignore empty new queries when attempting to track', () => {
    const initialCount = component.totalQueriesCount();
    component.newQueryText.set('   ');
    component.trackNewQuery();

    expect(component.totalQueriesCount()).toBe(initialCount);
  });

  it('should run page citability audit on structured content and compute High grade', () => {
    const markdownContent = `
    # High-Performance Growth Engine Architecture

    SGrowing delivers enterprise growth orchestration with integrated marketing attribution, sales pipeline intelligence, and automated courier reconciliation.

    ## What is Generative Engine Optimization?
    Generative Engine Optimization is the systematic engineering of primary content passages to ensure AI search engines accurately surface and cite brand capabilities.

    ## Verified Performance Metrics
    - 10,000 EPS partitioned event streaming with sub-second OLAP projection.
    - ISO 32000-1 PAdES-B-LTV digital contract signing vault.
    - Multi-tenant PostgreSQL 17 with kernel-level Row Level Security.

    | Module | P99 Latency | Availability SLA |
    |---|---|---|
    | Inbound Ingestion | < 15ms | 99.99% |
    | ClickHouse OLAP | < 120ms | 99.95% |
    `;

    component.auditUrl.set('https://sgrowing.io/perf-specs');
    component.auditTitle.set('Performance Specs');
    component.auditContent.set(markdownContent);

    component.runPageAudit();

    const audit = component.latestAudit();
    expect(audit).not.toBeNull();
    expect(audit?.overallScore).toBeGreaterThanOrEqual(75);
    expect(audit?.grade).toBe('High');
    expect(audit?.factualClaimsCount).toBeGreaterThan(0);
    expect(audit?.ambiguousPronounOccurrences).toBe(0);
    expect(audit?.recommendations.length).toBeGreaterThan(0);
  });

  it('should flag ambiguous pronouns and assign lower grade for unstructured vague content', () => {
    const vagueContent = `
    It is great and it helps people do things. When they use it, they will see that it makes them happy.
    It does a lot of work for them so they do not have to do it themselves.
    `;

    component.auditUrl.set('https://example.com/vague');
    component.auditTitle.set('Vague Page');
    component.auditContent.set(vagueContent);

    component.runPageAudit();

    const audit = component.latestAudit();
    expect(audit).not.toBeNull();
    expect(audit?.ambiguousPronounOccurrences).toBeGreaterThan(3);
    expect(audit?.overallScore).toBeLessThan(65);
    expect(audit?.recommendations.some(r => r.includes('ambiguous pronouns'))).toBe(true);
  });

  it('should toggle crawler robots permission and update readiness status', () => {
    expect(component.crawlerReadinessPercent()).toBe(100);

    component.toggleCrawlerAllowed('GPTBot');
    const gptBot = component.manifest().crawlers.find(c => c.botName === 'GPTBot');
    expect(gptBot?.allowedInRobotsTxt).toBe(false);
    expect(gptBot?.status).toBe('Blocked');
    expect(component.crawlerReadinessPercent()).toBe(75);

    // Toggle back
    component.toggleCrawlerAllowed('GPTBot');
    const gptBotRestored = component.manifest().crawlers.find(c => c.botName === 'GPTBot');
    expect(gptBotRestored?.allowedInRobotsTxt).toBe(true);
    expect(gptBotRestored?.status).toBe('Compliant');
    expect(component.crawlerReadinessPercent()).toBe(100);
  });

  it('should ping crawler bot and update lastPingStatus', () => {
    component.pingCrawler('PerplexityBot');
    const bot = component.manifest().crawlers.find(c => c.botName === 'PerplexityBot');
    expect(bot?.lastPingStatus).toBe('Success');
  });

  it('should return correct badge CSS classes for grades, statuses, and engines', () => {
    expect(component.getGradeBadgeClass('High')).toContain('emerald');
    expect(component.getGradeBadgeClass('Moderate')).toContain('amber');
    expect(component.getGradeBadgeClass('NeedsImprovement')).toContain('rose');

    expect(component.getStatusBadgeClass('Compliant')).toContain('emerald');
    expect(component.getStatusBadgeClass('Warning')).toContain('amber');
    expect(component.getStatusBadgeClass('Blocked')).toContain('rose');

    expect(component.getEngineBadgeClass('Perplexity')).toContain('teal');
    expect(component.getEngineBadgeClass('ChatGPT_Search')).toContain('emerald');
    expect(component.getEngineBadgeClass('Google_Gemini_AIO')).toContain('blue');
    expect(component.getEngineBadgeClass('Claude_Web')).toContain('amber');
  });
});
