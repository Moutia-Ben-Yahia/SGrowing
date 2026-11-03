// @vitest-environment jsdom
import '@angular/compiler';
import { describe, it, expect, beforeEach, vi } from 'vitest';
import { Injector, runInInjectionContext } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { of } from 'rxjs';
import { SeoCrawlerStudioComponent, CrawlSummaryDto, SerpRankingsOverviewDto } from './seo-crawler-studio.component';

describe('SeoCrawlerStudioComponent', () => {
  let component: SeoCrawlerStudioComponent;
  let mockHttp: any;

  const mockSummary: CrawlSummaryDto = {
    crawlId: 'crawl-101',
    tenantId: 'tenant-1',
    startingUrl: 'https://store.sgrowing.io',
    status: 'Completed',
    overallHealthScore: 92,
    crawlabilityScore: 95,
    metadataScore: 88,
    linkIntegrityScore: 94,
    totalPagesCrawled: 42,
    brokenLinksCount: 1,
    missingTitleCount: 0,
    missingMetaCount: 2,
    missingH1Count: 1,
    multipleH1Count: 0,
    missingCanonicalCount: 0,
    orphanPagesCount: 0,
    deepPagesCount: 3,
    avgResponseTimeMs: 120,
    statusDistribution: { '200': 41, '404': 1 },
    issueBreakdown: { MissingMetaDescription: 2, MissingH1: 1 },
    startedAtUtc: '2026-11-03T10:00:00Z',
    completedAtUtc: '2026-11-03T10:02:00Z'
  };

  const mockPages = [
    {
      id: 'page-1',
      crawlId: 'crawl-101',
      url: 'https://store.sgrowing.io/shop',
      path: '/shop',
      statusCode: 200,
      responseTimeMs: 85,
      title: 'Shop Best E-Commerce Catalog',
      titleLength: 29,
      metaDescription: 'Shop our premium catalogue with cash on delivery guaranteed.',
      metaDescriptionLength: 60,
      h1Count: 1,
      h1Tag: 'Shop All Items',
      hasCanonical: true,
      canonicalUrl: 'https://store.sgrowing.io/shop',
      isCanonicalSelfReferential: true,
      hasOpenGraph: true,
      openGraphTagsPresent: ['og:title', 'og:description'],
      inboundLinksCount: 12,
      outboundLinksCount: 24,
      pageRank: 0.85,
      pageRankFormatted: '0.85',
      crawlDepth: 1,
      isOrphan: false,
      issues: [],
      crawledAtUtc: '2026-11-03T10:00:10Z'
    },
    {
      id: 'page-2',
      crawlId: 'crawl-101',
      url: 'https://store.sgrowing.io/legacy-item',
      path: '/legacy-item',
      statusCode: 404,
      responseTimeMs: 45,
      title: '',
      titleLength: 0,
      metaDescription: '',
      metaDescriptionLength: 0,
      h1Count: 0,
      h1Tag: '',
      hasCanonical: false,
      isCanonicalSelfReferential: false,
      hasOpenGraph: false,
      openGraphTagsPresent: [],
      inboundLinksCount: 1,
      outboundLinksCount: 0,
      pageRank: 0.1,
      pageRankFormatted: '0.10',
      crawlDepth: 2,
      isOrphan: false,
      issues: ['BrokenLink', 'MissingH1'],
      crawledAtUtc: '2026-11-03T10:00:20Z'
    }
  ];

  const mockSerpOverview: SerpRankingsOverviewDto = {
    totalTrackedKeywords: 10,
    top3Count: 4,
    top3Percentage: 40,
    top10Count: 8,
    top10Percentage: 80,
    top30Count: 10,
    top30Percentage: 100,
    overallVisibilityScore: 84.5,
    keywords: [
      {
        id: 'kw-1',
        tenantId: 'tenant-1',
        keyword: 'e-commerce tunisie livraison 24h',
        targetUrl: 'https://store.sgrowing.io/fast-delivery',
        countryCode: 'TN',
        searchEngine: 'Google',
        currentPosition: 2,
        previousPosition: 3,
        delta: '+1',
        status: 'improved',
        bestPosition: 1,
        history: [{ date: '2026-11-02', position: 3 }, { date: '2026-11-03', position: 2 }],
        lastCheckedUtc: '2026-11-03T09:00:00Z'
      }
    ]
  };

  beforeEach(() => {
    mockHttp = {
      get: vi.fn().mockImplementation((url: string) => {
        if (url.includes('/pages')) return of({ items: mockPages });
        if (url.includes('/serp/rankings')) return of(mockSerpOverview);
        return of({});
      }),
      post: vi.fn().mockImplementation((url: string) => {
        if (url.includes('/crawler/start')) return of(mockSummary);
        if (url.includes('/track-keyword')) return of(mockSerpOverview.keywords[0]);
        return of({});
      })
    };

    const injector = Injector.create({
      providers: [
        { provide: HttpClient, useValue: mockHttp }
      ]
    });

    component = runInInjectionContext(injector, () => new SeoCrawlerStudioComponent());
    component.ngOnInit();
  });

  it('should initialize and load default demo data and SERP rankings', () => {
    expect(component).toBeTruthy();
    expect(mockHttp.get).toHaveBeenCalledWith('/api/v1/seo/serp/rankings');
    expect(component.serpOverview()?.totalTrackedKeywords).toBe(10);
    expect(component.serpKeywords().length).toBe(1);
    expect(component.activeTab()).toBe('crawler');
  });

  it('should trigger technical crawler run via HTTP and load crawled pages', () => {
    component.startUrl.set('https://store.sgrowing.io');
    component.triggerCrawl();

    expect(mockHttp.post).toHaveBeenCalledWith(
      '/api/v1/seo/crawler/start',
      expect.objectContaining({ url: 'https://store.sgrowing.io' })
    );
    expect(component.summary()?.crawlId).toBe('crawl-101');
    expect(mockHttp.get).toHaveBeenCalledWith(expect.stringContaining('crawl-101/pages'));
  });

  it('should filter crawled pages by broken status', () => {
    component.crawledPages.set(mockPages);

    component.activeFilter.set('broken');
    const broken = component.filteredPages();
    expect(broken.length).toBe(1);
    expect(broken[0].statusCode).toBe(404);

    component.activeFilter.set('all');
    expect(component.filteredPages().length).toBe(2);
  });

  it('should submit new keyword for SERP rank tracking', () => {
    component.newKeywordName = 'chaussure homme tunis';
    component.newKeywordUrl = 'https://store.sgrowing.io/chaussures';
    component.newKeywordCountry = 'TN';
    component.submitTrackKeyword();

    expect(mockHttp.post).toHaveBeenCalledWith(
      '/api/v1/seo/serp/track-keyword',
      expect.objectContaining({ keyword: 'chaussure homme tunis' })
    );
    expect(component.showTrackModal()).toBe(false);
  });
});
