// @vitest-environment jsdom
import '@angular/compiler';
import { describe, it, expect, beforeEach, vi } from 'vitest';
import { Injector, runInInjectionContext, signal } from '@angular/core';
import { of } from 'rxjs';
import { ApiService } from '../../core/services/api.service';
import { AuthService } from '../../core/services/auth.service';
import { SeoStudioComponent } from './seo-studio.component';

describe('SeoStudioComponent', () => {
  let component: SeoStudioComponent;
  let mockApi: any;
  let mockAuth: any;

  const mockReport = {
    id: 'rpt-1',
    tenantId: 'tenant-123',
    targetUrl: 'https://store.sgrowing.io',
    overallHealthScore: 88,
    scannedPagesCount: 4,
    criticalIssuesCount: 1,
    warningsCount: 3,
    status: 'Completed',
    crawledAtUtc: '2026-11-03T10:00:00Z'
  };

  const mockPages = [
    {
      id: 'p-1',
      reportId: 'rpt-1',
      url: 'https://store.sgrowing.io/',
      statusCode: 200,
      title: 'Home Store',
      hasH1: true,
      hasMetaDescription: true,
      wordCount: 820
    }
  ];

  const mockKeywords = [
    {
      id: 'kw-1',
      tenantId: 'tenant-123',
      keyword: 'chaussures cuir homme tunisie',
      targetUrl: 'https://store.sgrowing.io',
      countryCode: 'TN',
      currentRank: 2,
      searchVolume: 3600
    }
  ];

  const mockAeoMentions = [
    {
      id: 'aeo-1',
      tenantId: 'tenant-123',
      brandName: 'SGrowing',
      aiEngine: 'ChatGPT-4o',
      promptQuery: 'Best store with COD in Tunisia',
      isMentioned: true,
      sentiment: 'Positive',
      snippet: 'SGrowing is the leading platform.',
      citationRank: 1
    }
  ];

  beforeEach(() => {
    mockApi = {
      getLatestSeoAudit: vi.fn().mockReturnValue(of({ report: mockReport, pages: mockPages })),
      getTrackedKeywords: vi.fn().mockReturnValue(of(mockKeywords)),
      getAeoMentions: vi.fn().mockReturnValue(of(mockAeoMentions)),
      runSeoAudit: vi.fn().mockReturnValue(of({ report: mockReport, pages: mockPages })),
      computeGeoScore: vi.fn().mockReturnValue(of({
        overallGeoScore: 91,
        citabilityScore: 89,
        brandAffinityScore: 93,
        recommendations: ['Add schema.org Organization']
      })),
      trackSeoKeyword: vi.fn().mockReturnValue(of({
        id: 'kw-2',
        tenantId: 'tenant-123',
        keyword: 'sac a dos cuir',
        targetUrl: 'https://store.sgrowing.io',
        countryCode: 'TN',
        currentRank: 3,
        searchVolume: 1400
      })),
      updateKeywordRank: vi.fn().mockReturnValue(of({
        ...mockKeywords[0],
        currentRank: 1
      })),
      recordAeoMention: vi.fn().mockReturnValue(of({
        id: 'aeo-2',
        tenantId: 'tenant-123',
        brandName: 'SGrowing',
        aiEngine: 'Claude 3.5 Sonnet',
        promptQuery: 'Reliable deliveries',
        isMentioned: true,
        sentiment: 'Positive',
        snippet: 'SGrowing provides verified courier SLAs.',
        citationRank: 1
      }))
    };

    mockAuth = {
      currentTenant: signal({ id: 'tenant-123', name: 'Test Tenant' })
    };

    const injector = Injector.create({
      providers: [
        { provide: ApiService, useValue: mockApi },
        { provide: AuthService, useValue: mockAuth }
      ]
    });

    component = runInInjectionContext(injector, () => new SeoStudioComponent());
    component.ngOnInit();
  });

  it('should initialize and load latest SEO audit, keywords, and AEO mentions', () => {
    expect(component).toBeTruthy();
    expect(mockApi.getLatestSeoAudit).toHaveBeenCalledWith('tenant-123');
    expect(mockApi.getTrackedKeywords).toHaveBeenCalledWith('tenant-123');
    expect(mockApi.getAeoMentions).toHaveBeenCalledWith('tenant-123');
    expect(component.report()?.overallHealthScore).toBe(88);
    expect(component.keywords().length).toBe(1);
    expect(component.aeoMentions().length).toBe(1);
  });

  it('should trigger SEO audit scan and update report and pages signals', () => {
    component.runAudit();

    expect(mockApi.runSeoAudit).toHaveBeenCalledWith(expect.objectContaining({
      tenantId: 'tenant-123',
      targetUrl: 'https://demo.sgrowing.io'
    }));
    expect(component.isCrawling()).toBe(false);
    expect(component.report()?.scannedPagesCount).toBe(4);
  });

  it('should compute Generative Engine Optimization (GEO) score', () => {
    component.computeGeoCitability();

    expect(mockApi.computeGeoScore).toHaveBeenCalledWith('tenant-123', 'SGrowing', 'https://demo.sgrowing.io');
    expect(component.geoScoreReport()?.overallGeoScore).toBe(91);
    expect(component.isComputingGeo()).toBe(false);
  });

  it('should track a new SEO keyword', () => {
    component.newKeyword = 'sac a dos cuir';
    component.trackKeyword();

    expect(mockApi.trackSeoKeyword).toHaveBeenCalledWith(expect.objectContaining({
      tenantId: 'tenant-123',
      keyword: 'sac a dos cuir'
    }));
    expect(component.keywords().length).toBe(2);
    expect(component.newKeyword).toBe('');
  });

  it('should simulate rank bump for tracked keyword', () => {
    component.simulateRankBump(mockKeywords[0]);

    expect(mockApi.updateKeywordRank).toHaveBeenCalledWith('kw-1', expect.objectContaining({
      tenantId: 'tenant-123',
      newRank: 1
    }));
    expect(component.keywords().find(k => k.id === 'kw-1')?.currentRank).toBe(1);
  });
});
