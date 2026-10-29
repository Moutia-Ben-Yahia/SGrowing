import { Component, OnInit, signal, computed, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { HttpClient } from '@angular/common/http';

export interface CrawlSummaryDto {
  crawlId: string;
  tenantId: string;
  startingUrl: string;
  status: string;
  overallHealthScore: number;
  crawlabilityScore: number;
  metadataScore: number;
  linkIntegrityScore: number;
  totalPagesCrawled: number;
  brokenLinksCount: number;
  missingTitleCount: number;
  missingMetaCount: number;
  missingH1Count: number;
  multipleH1Count: number;
  missingCanonicalCount: number;
  orphanPagesCount: number;
  deepPagesCount: number;
  avgResponseTimeMs: number;
  statusDistribution: Record<string, number>;
  issueBreakdown: Record<string, number>;
  startedAtUtc: string;
  completedAtUtc: string;
}

export interface CrawledPageDetailDto {
  id: string;
  crawlId: string;
  url: string;
  path: string;
  statusCode: number;
  responseTimeMs: number;
  title: string;
  titleLength: number;
  metaDescription: string;
  metaDescriptionLength: number;
  h1Count: number;
  h1Tag: string;
  hasCanonical: boolean;
  canonicalUrl?: string;
  isCanonicalSelfReferential: boolean;
  hasOpenGraph: boolean;
  openGraphTagsPresent: string[];
  inboundLinksCount: number;
  outboundLinksCount: number;
  pageRank: number;
  pageRankFormatted: string;
  crawlDepth: number;
  isOrphan: boolean;
  issues: string[];
  crawledAtUtc: string;
}

export interface SerpRankPointDto {
  date: string;
  position: number;
}

export interface SerpKeywordRankItemDto {
  id: string;
  tenantId: string;
  keyword: string;
  targetUrl: string;
  countryCode: string;
  searchEngine: string;
  currentPosition: number;
  previousPosition?: number;
  delta: string;
  status: string; // 'improved' | 'declined' | 'unchanged' | 'new'
  bestPosition: number;
  history: SerpRankPointDto[];
  lastCheckedUtc: string;
}

export interface SerpRankingsOverviewDto {
  totalTrackedKeywords: number;
  top3Count: number;
  top3Percentage: number;
  top10Count: number;
  top10Percentage: number;
  top30Count: number;
  top30Percentage: number;
  overallVisibilityScore: number;
  keywords: SerpKeywordRankItemDto[];
}

@Component({
  selector: 'app-seo-crawler-studio',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterLink],
  template: `
    <div dir="ltr" class="flex h-screen w-screen overflow-hidden bg-slate-950 text-slate-100 font-sans">
      <!-- Main Content Container -->
      <main class="flex-1 flex flex-col overflow-y-auto">
        <!-- Studio Header -->
        <header class="h-16 border-b border-slate-800 bg-slate-900/80 backdrop-blur px-6 flex items-center justify-between shrink-0 sticky top-0 z-20">
          <div class="flex items-center gap-3">
            <a routerLink="/" class="text-xs text-slate-400 hover:text-white flex items-center gap-1 transition">
              ← Dashboard
            </a>
            <span class="text-slate-700">|</span>
            <div class="flex items-center gap-2">
              <span class="text-lg">🕸️</span>
              <span class="text-base font-semibold text-white tracking-tight">
                Seonaut & Serpbear High-Concurrency Technical SEO Studio
              </span>
            </div>
            <span class="bg-indigo-500/20 text-indigo-400 text-[10px] font-mono px-2.5 py-0.5 rounded-full border border-indigo-500/30">
              Horizon 13 • PageRank & SERP Engine
            </span>
          </div>

          <!-- Studio Navigation Tabs -->
          <div class="flex items-center gap-3">
            <div class="flex bg-slate-800/80 p-1 rounded-xl border border-slate-700/60 text-xs shadow-inner">
              <button
                type="button"
                (click)="activeTab.set('crawler')"
                [class.bg-indigo-600]="activeTab() === 'crawler'"
                [class.text-white]="activeTab() === 'crawler'"
                [class.shadow-md]="activeTab() === 'crawler'"
                class="px-3.5 py-1.5 rounded-lg text-slate-300 transition flex items-center gap-1.5 font-medium">
                <span>🕸️</span> Site Crawler & Audit
              </button>
              <button
                type="button"
                (click)="activeTab.set('serp')"
                [class.bg-indigo-600]="activeTab() === 'serp'"
                [class.text-white]="activeTab() === 'serp'"
                [class.shadow-md]="activeTab() === 'serp'"
                class="px-3.5 py-1.5 rounded-lg text-slate-300 transition flex items-center gap-1.5 font-medium">
                <span>📈</span> SERP Keyword Tracker
              </button>
            </div>
          </div>
        </header>

        <!-- Studio Body -->
        <div class="p-6 space-y-6 flex-1 max-w-7xl mx-auto w-full">

          <!-- TAB 1: Site Crawler & Technical Audit -->
          @if (activeTab() === 'crawler') {
            <!-- Top Health Overview Cards -->
            <div class="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-4 gap-4">
              <!-- Card 1: Circular Health Gauge -->
              <div class="p-5 rounded-2xl bg-gradient-to-br from-slate-900 to-slate-950 border border-slate-800/90 shadow-xl flex items-center gap-4 relative overflow-hidden">
                <div class="relative w-20 h-20 shrink-0 flex items-center justify-center">
                  <svg class="w-20 h-20 -rotate-90 transform" viewBox="0 0 36 36">
                    <!-- Background circle -->
                    <path
                      class="text-slate-800"
                      stroke-width="3.5"
                      stroke="currentColor"
                      fill="none"
                      d="M18 2.0845 a 15.9155 15.9155 0 0 1 0 31.831 a 15.9155 15.9155 0 0 1 0 -31.831"
                    />
                    <!-- Progress circle -->
                    <path
                      [class.text-emerald-500]="(summary()?.overallHealthScore || 0) >= 80"
                      [class.text-amber-500]="(summary()?.overallHealthScore || 0) < 80 && (summary()?.overallHealthScore || 0) >= 60"
                      [class.text-rose-500]="(summary()?.overallHealthScore || 0) < 60"
                      stroke-dasharray="100, 100"
                      [attr.stroke-dashoffset]="100 - (summary()?.overallHealthScore || 88)"
                      stroke-width="3.5"
                      stroke-linecap="round"
                      stroke="currentColor"
                      fill="none"
                      d="M18 2.0845 a 15.9155 15.9155 0 0 1 0 31.831 a 15.9155 15.9155 0 0 1 0 -31.831"
                    />
                  </svg>
                  <div class="absolute inset-0 flex flex-col items-center justify-center text-center">
                    <span class="text-xl font-bold font-mono tracking-tight"
                      [class.text-emerald-400]="(summary()?.overallHealthScore || 0) >= 80"
                      [class.text-amber-400]="(summary()?.overallHealthScore || 0) < 80 && (summary()?.overallHealthScore || 0) >= 60"
                      [class.text-rose-400]="(summary()?.overallHealthScore || 0) < 60">
                      {{ summary()?.overallHealthScore ?? 88 }}
                    </span>
                    <span class="text-[9px] uppercase tracking-wider text-slate-400 font-medium">/ 100</span>
                  </div>
                </div>

                <div class="flex-1 min-w-0">
                  <span class="text-xs uppercase tracking-wider text-slate-400 font-semibold block mb-1">Site Health</span>
                  <p class="text-xs text-slate-300 truncate font-mono">
                    {{ summary()?.startingUrl || startUrl() }}
                  </p>
                  <div class="flex items-center gap-2 mt-2 text-[11px] text-slate-400">
                    <span>Meta: <strong class="text-slate-200">{{ summary()?.metadataScore ?? 85 }}%</strong></span>
                    <span>•</span>
                    <span>Link: <strong class="text-slate-200">{{ summary()?.linkIntegrityScore ?? 90 }}%</strong></span>
                  </div>
                </div>
              </div>

              <!-- Card 2: Total Pages Crawled -->
              <div class="p-5 rounded-2xl bg-slate-900/70 border border-slate-800/80 shadow-lg flex flex-col justify-between">
                <div>
                  <div class="flex items-center justify-between">
                    <span class="text-xs text-slate-400 uppercase tracking-wider font-semibold">Total Pages Crawled</span>
                    <span class="text-slate-500">📄</span>
                  </div>
                  <h3 class="text-2xl font-bold text-white mt-2 font-mono">
                    {{ summary()?.totalPagesCrawled ?? crawledPages().length }}
                  </h3>
                </div>
                <div class="flex items-center justify-between text-xs text-slate-400 mt-3 pt-3 border-t border-slate-800/60">
                  <span>Max Limit:</span>
                  <span class="font-mono text-slate-300">{{ maxPages() }} Pages</span>
                </div>
              </div>

              <!-- Card 3: Broken Links & Errors -->
              <div class="p-5 rounded-2xl bg-slate-900/70 border border-slate-800/80 shadow-lg flex flex-col justify-between">
                <div>
                  <div class="flex items-center justify-between">
                    <span class="text-xs text-slate-400 uppercase tracking-wider font-semibold">Broken Links</span>
                    <span class="text-rose-400">⚠️</span>
                  </div>
                  <h3 class="text-2xl font-bold font-mono mt-2"
                    [class.text-rose-400]="(summary()?.brokenLinksCount || 0) > 0"
                    [class.text-emerald-400]="(summary()?.brokenLinksCount || 0) === 0">
                    {{ summary()?.brokenLinksCount ?? 0 }}
                  </h3>
                </div>
                <div class="flex items-center justify-between text-xs text-slate-400 mt-3 pt-3 border-t border-slate-800/60">
                  <span>Orphan Pages:</span>
                  <span class="font-mono text-amber-400 font-semibold">{{ summary()?.orphanPagesCount ?? 0 }}</span>
                </div>
              </div>

              <!-- Card 4: Avg Response Time -->
              <div class="p-5 rounded-2xl bg-slate-900/70 border border-slate-800/80 shadow-lg flex flex-col justify-between">
                <div>
                  <div class="flex items-center justify-between">
                    <span class="text-xs text-slate-400 uppercase tracking-wider font-semibold">Avg Response Time</span>
                    <span class="text-cyan-400">⚡</span>
                  </div>
                  <h3 class="text-2xl font-bold text-cyan-400 mt-2 font-mono">
                    {{ summary()?.avgResponseTimeMs ? (summary()?.avgResponseTimeMs + ' ms') : '145 ms' }}
                  </h3>
                </div>
                <div class="flex items-center justify-between text-xs text-slate-400 mt-3 pt-3 border-t border-slate-800/60">
                  <span>Crawl Depth:</span>
                  <span class="font-mono text-slate-300">Level {{ maxDepth() }} max</span>
                </div>
              </div>
            </div>

            <!-- Start New Crawl Action Bar -->
            <div class="p-5 rounded-2xl bg-slate-900/90 border border-slate-800 shadow-xl space-y-4">
              <div class="flex flex-col lg:flex-row lg:items-center gap-4">
                <!-- Target URL Input -->
                <div class="flex-1 relative">
                  <label class="block text-xs font-semibold uppercase tracking-wider text-slate-400 mb-1.5">
                    Target Site Base URL
                  </label>
                  <div class="relative">
                    <span class="absolute inset-y-0 left-0 pl-3 flex items-center pointer-events-none text-slate-500 text-sm">
                      🌐
                    </span>
                    <input
                      type="url"
                      [(ngModel)]="startUrl"
                      placeholder="https://store.sgrowing.io"
                      class="w-full bg-slate-950 border border-slate-700/80 rounded-xl pl-9 pr-4 py-2.5 text-sm text-white focus:outline-none focus:border-indigo-500 font-mono shadow-inner"
                    />
                  </div>
                </div>

                <!-- Max Pages Slider -->
                <div class="w-full lg:w-60">
                  <div class="flex justify-between items-center mb-1.5">
                    <label class="text-xs font-semibold uppercase tracking-wider text-slate-400">
                      Max Pages
                    </label>
                    <span class="text-xs font-mono font-bold text-indigo-400 bg-indigo-500/10 px-2 py-0.5 rounded border border-indigo-500/20">
                      {{ maxPages() }} Pages
                    </span>
                  </div>
                  <input
                    type="range"
                    min="25"
                    max="200"
                    step="25"
                    [(ngModel)]="maxPages"
                    class="w-full accent-indigo-500 cursor-pointer h-2 bg-slate-800 rounded-lg"
                  />
                </div>

                <!-- Max Depth Selection -->
                <div class="w-full lg:w-40">
                  <label class="block text-xs font-semibold uppercase tracking-wider text-slate-400 mb-1.5">
                    Max Depth
                  </label>
                  <select
                    [(ngModel)]="maxDepth"
                    class="w-full bg-slate-950 border border-slate-700/80 rounded-xl px-3 py-2.5 text-sm text-slate-200 focus:outline-none focus:border-indigo-500 font-mono">
                    <option [value]="2">2 Levels</option>
                    <option [value]="3">3 Levels (Standard)</option>
                    <option [value]="4">4 Levels (Deep)</option>
                    <option [value]="5">5 Levels (Full Site)</option>
                  </select>
                </div>

                <!-- Launch Button -->
                <div class="lg:self-end">
                  <button
                    type="button"
                    (click)="triggerCrawl()"
                    [disabled]="isCrawling()"
                    class="w-full lg:w-auto px-6 py-2.5 rounded-xl font-semibold text-sm text-white bg-indigo-600 hover:bg-indigo-500 active:scale-[0.98] transition shadow-lg shadow-indigo-600/30 flex items-center justify-center gap-2 disabled:opacity-50 disabled:cursor-not-allowed">
                    @if (isCrawling()) {
                      <svg class="animate-spin h-4 w-4 text-white" viewBox="0 0 24 24" fill="none">
                        <circle class="opacity-25" cx="12" cy="12" r="10" stroke="currentColor" stroke-width="4"></circle>
                        <path class="opacity-75" fill="currentColor" d="M4 12a8 8 0 018-8V0C5.373 0 0 5.373 0 12h4zm2 5.291A7.962 7.962 0 014 12H0c0 3.042 1.135 5.824 3 7.938l3-2.647z"></path>
                      </svg>
                      <span>Crawling Site...</span>
                    } @else {
                      <span>🚀</span>
                      <span>Start Recursive Crawl</span>
                    }
                  </button>
                </div>
              </div>

              <!-- Live Telemetry Status Banner -->
              @if (isCrawling()) {
                <div class="p-3 bg-indigo-950/40 border border-indigo-800/60 rounded-xl flex items-center justify-between text-xs text-indigo-300 animate-pulse font-mono">
                  <div class="flex items-center gap-2">
                    <span class="w-2 h-2 rounded-full bg-indigo-400 animate-ping"></span>
                    <span>High-concurrency worker pool exploring internal link graph across <strong>{{ startUrl() }}</strong>...</span>
                  </div>
                  <span>Concurrency: 5 goroutines/workers</span>
                </div>
              }
            </div>

            <!-- Issues Filter Pills Bar -->
            <div class="flex flex-wrap items-center justify-between gap-3 pt-2">
              <div class="flex flex-wrap items-center gap-2">
                <button
                  type="button"
                  (click)="activeFilter.set('all')"
                  [class.bg-indigo-600]="activeFilter() === 'all'"
                  [class.text-white]="activeFilter() === 'all'"
                  [class.bg-slate-900]="activeFilter() !== 'all'"
                  [class.text-slate-300]="activeFilter() !== 'all'"
                  class="px-3 py-1.5 rounded-lg text-xs font-medium border border-slate-800 hover:border-slate-700 transition flex items-center gap-1.5 shadow-sm">
                  <span>All Pages</span>
                  <span class="bg-black/30 px-1.5 py-0.2 rounded text-[11px] font-mono">{{ crawledPages().length }}</span>
                </button>

                <button
                  type="button"
                  (click)="activeFilter.set('broken')"
                  [class.bg-rose-600]="activeFilter() === 'broken'"
                  [class.text-white]="activeFilter() === 'broken'"
                  [class.bg-slate-900]="activeFilter() !== 'broken'"
                  [class.text-slate-300]="activeFilter() !== 'broken'"
                  class="px-3 py-1.5 rounded-lg text-xs font-medium border border-slate-800 hover:border-slate-700 transition flex items-center gap-1.5 shadow-sm">
                  <span class="text-rose-400">●</span>
                  <span>Broken (404/500)</span>
                  <span class="bg-black/30 px-1.5 py-0.2 rounded text-[11px] font-mono text-rose-300">{{ countBrokenPages() }}</span>
                </button>

                <button
                  type="button"
                  (click)="activeFilter.set('missing_h1')"
                  [class.bg-amber-600]="activeFilter() === 'missing_h1'"
                  [class.text-white]="activeFilter() === 'missing_h1'"
                  [class.bg-slate-900]="activeFilter() !== 'missing_h1'"
                  [class.text-slate-300]="activeFilter() !== 'missing_h1'"
                  class="px-3 py-1.5 rounded-lg text-xs font-medium border border-slate-800 hover:border-slate-700 transition flex items-center gap-1.5 shadow-sm">
                  <span class="text-amber-400">●</span>
                  <span>Missing H1</span>
                  <span class="bg-black/30 px-1.5 py-0.2 rounded text-[11px] font-mono text-amber-300">{{ countMissingH1() }}</span>
                </button>

                <button
                  type="button"
                  (click)="activeFilter.set('meta_issues')"
                  [class.bg-violet-600]="activeFilter() === 'meta_issues'"
                  [class.text-white]="activeFilter() === 'meta_issues'"
                  [class.bg-slate-900]="activeFilter() !== 'meta_issues'"
                  [class.text-slate-300]="activeFilter() !== 'meta_issues'"
                  class="px-3 py-1.5 rounded-lg text-xs font-medium border border-slate-800 hover:border-slate-700 transition flex items-center gap-1.5 shadow-sm">
                  <span class="text-violet-400">●</span>
                  <span>Meta Issues</span>
                  <span class="bg-black/30 px-1.5 py-0.2 rounded text-[11px] font-mono text-violet-300">{{ countMetaIssues() }}</span>
                </button>

                <button
                  type="button"
                  (click)="activeFilter.set('orphan')"
                  [class.bg-orange-600]="activeFilter() === 'orphan'"
                  [class.text-white]="activeFilter() === 'orphan'"
                  [class.bg-slate-900]="activeFilter() !== 'orphan'"
                  [class.text-slate-300]="activeFilter() !== 'orphan'"
                  class="px-3 py-1.5 rounded-lg text-xs font-medium border border-slate-800 hover:border-slate-700 transition flex items-center gap-1.5 shadow-sm">
                  <span class="text-orange-400">●</span>
                  <span>Orphan Pages</span>
                  <span class="bg-black/30 px-1.5 py-0.2 rounded text-[11px] font-mono text-orange-300">{{ countOrphans() }}</span>
                </button>
              </div>

              <!-- Search filter in table -->
              <div class="relative w-full sm:w-64">
                <input
                  type="text"
                  [(ngModel)]="searchQuery"
                  placeholder="Filter URL or title..."
                  class="w-full bg-slate-900 border border-slate-800 rounded-lg px-3 py-1.5 text-xs text-white placeholder-slate-500 focus:outline-none focus:border-indigo-500"
                />
              </div>
            </div>

            <!-- Crawled Pages Table Card -->
            <div class="rounded-2xl bg-slate-900/80 border border-slate-800 shadow-xl overflow-hidden">
              <div class="overflow-x-auto">
                <table class="w-full text-left text-xs">
                  <thead class="bg-slate-950/70 border-b border-slate-800 text-slate-400 uppercase font-semibold text-[11px]">
                    <tr>
                      <th class="px-4 py-3">Status</th>
                      <th class="px-4 py-3">Page Path & Title</th>
                      <th class="px-4 py-3 text-center">Inbound Links</th>
                      <th class="px-4 py-3 text-center">PageRank Equity</th>
                      <th class="px-4 py-3 text-center">Latency</th>
                      <th class="px-4 py-3">Discovered Issues</th>
                    </tr>
                  </thead>
                  <tbody class="divide-y divide-slate-800/60 font-sans">
                    @for (page of filteredPages(); track page.url) {
                      <tr class="hover:bg-slate-800/30 transition">
                        <!-- Status Badge -->
                        <td class="px-4 py-3 whitespace-nowrap">
                          <span
                            class="px-2.5 py-1 rounded-md text-[11px] font-mono font-bold"
                            [class.bg-emerald-500-10]="page.statusCode === 200"
                            [class.text-emerald-400]="page.statusCode === 200"
                            [class.border]="true"
                            [class.border-emerald-500-20]="page.statusCode === 200"
                            [class.bg-rose-500-10]="page.statusCode >= 400"
                            [class.text-rose-400]="page.statusCode >= 400"
                            [class.border-rose-500-20]="page.statusCode >= 400"
                            [class.bg-sky-500-10]="page.statusCode >= 300 && page.statusCode < 400"
                            [class.text-sky-400]="page.statusCode >= 300 && page.statusCode < 400"
                            [class.border-sky-500-20]="page.statusCode >= 300 && page.statusCode < 400">
                            {{ page.statusCode }} {{ getStatusLabel(page.statusCode) }}
                          </span>
                        </td>

                        <!-- URL Path & Title -->
                        <td class="px-4 py-3 max-w-md">
                          <div class="font-mono text-slate-200 font-semibold truncate hover:text-indigo-400 cursor-pointer flex items-center gap-1.5"
                            [title]="page.url">
                            <span>{{ page.path }}</span>
                            @if (page.isOrphan) {
                              <span class="bg-orange-500/20 text-orange-400 text-[10px] px-1.5 py-0.2 rounded border border-orange-500/30 font-sans">
                                Orphan
                              </span>
                            }
                          </div>
                          <div class="text-slate-400 text-[11px] truncate mt-0.5" [title]="page.title">
                            {{ page.title || '(No title tag detected)' }}
                          </div>
                        </td>

                        <!-- Inbound Links -->
                        <td class="px-4 py-3 text-center whitespace-nowrap">
                          <span class="inline-flex items-center gap-1 px-2.5 py-1 rounded-full bg-slate-800 border border-slate-700/60 font-mono text-[11px] text-slate-300">
                            <span>↳</span>
                            <strong>{{ page.inboundLinksCount }}</strong> inlinks
                          </span>
                        </td>

                        <!-- PageRank Score Pill -->
                        <td class="px-4 py-3 text-center whitespace-nowrap">
                          <span class="inline-flex items-center px-2.5 py-1 rounded-full bg-gradient-to-r from-purple-900/40 to-indigo-900/40 border border-indigo-700/40 font-mono text-[11px] font-semibold text-indigo-300">
                            {{ page.pageRankFormatted }}
                          </span>
                        </td>

                        <!-- Latency -->
                        <td class="px-4 py-3 text-center whitespace-nowrap font-mono text-[11px]"
                          [class.text-emerald-400]="page.responseTimeMs <= 300"
                          [class.text-amber-400]="page.responseTimeMs > 300 && page.responseTimeMs <= 800"
                          [class.text-rose-400]="page.responseTimeMs > 800">
                          {{ page.responseTimeMs }} ms
                        </td>

                        <!-- Discovered Issues -->
                        <td class="px-4 py-3">
                          <div class="flex flex-wrap gap-1 max-w-xs">
                            @if (page.issues.length === 0) {
                              <span class="text-emerald-400 text-[11px] flex items-center gap-1">
                                ✓ Clean Page
                              </span>
                            } @else {
                              @for (issue of page.issues; track issue) {
                                <span class="px-2 py-0.5 rounded text-[10px] font-mono bg-slate-800 text-slate-300 border border-slate-700/60"
                                  [class.border-rose-500-30]="issue.includes('Broken') || issue.includes('404')"
                                  [class.text-rose-300]="issue.includes('Broken') || issue.includes('404')"
                                  [class.border-amber-500-30]="issue.includes('H1') || issue.includes('Meta')"
                                  [class.text-amber-300]="issue.includes('H1') || issue.includes('Meta')">
                                  {{ issue }}
                                </span>
                              }
                            }
                          </div>
                        </td>
                      </tr>
                    } @empty {
                      <tr>
                        <td colspan="6" class="px-4 py-8 text-center text-slate-500">
                          No crawled pages match the current filter.
                        </td>
                      </tr>
                    }
                  </tbody>
                </table>
              </div>
            </div>
          }

          <!-- TAB 2: SERP Keyword Position Tracker -->
          @if (activeTab() === 'serp') {
            <!-- SERP Metrics Header Cards -->
            <div class="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-4 gap-4">
              <!-- Metric 1: Visibility Score -->
              <div class="p-5 rounded-2xl bg-gradient-to-br from-indigo-950/60 to-slate-900 border border-indigo-800/40 shadow-xl flex flex-col justify-between">
                <div>
                  <span class="text-xs uppercase tracking-wider text-indigo-300 font-semibold block mb-1">
                    Search Visibility Index
                  </span>
                  <h3 class="text-3xl font-bold font-mono text-white mt-1">
                    {{ serpOverview()?.overallVisibilityScore || 72.4 }}%
                  </h3>
                </div>
                <div class="text-xs text-indigo-300/80 mt-3 pt-3 border-t border-indigo-800/40 flex items-center justify-between">
                  <span>Keywords Tracked:</span>
                  <span class="font-mono font-bold text-white">{{ serpOverview()?.totalTrackedKeywords || serpKeywords().length }}</span>
                </div>
              </div>

              <!-- Metric 2: Top 3 Positions -->
              <div class="p-5 rounded-2xl bg-slate-900/70 border border-slate-800/80 shadow-lg flex flex-col justify-between">
                <div>
                  <span class="text-xs text-slate-400 uppercase tracking-wider font-semibold block mb-1">
                    Top 3 Rankings
                  </span>
                  <h3 class="text-3xl font-bold font-mono text-emerald-400 mt-1">
                    {{ serpOverview()?.top3Count || 3 }}
                  </h3>
                </div>
                <div class="text-xs text-slate-400 mt-3 pt-3 border-t border-slate-800/60 flex items-center justify-between">
                  <span>Share of Total:</span>
                  <span class="font-mono font-bold text-emerald-400">{{ serpOverview()?.top3Percentage || 37.5 }}%</span>
                </div>
              </div>

              <!-- Metric 3: Top 10 Positions (Page 1) -->
              <div class="p-5 rounded-2xl bg-slate-900/70 border border-slate-800/80 shadow-lg flex flex-col justify-between">
                <div>
                  <span class="text-xs text-slate-400 uppercase tracking-wider font-semibold block mb-1">
                    Top 10 (Page 1)
                  </span>
                  <h3 class="text-3xl font-bold font-mono text-sky-400 mt-1">
                    {{ serpOverview()?.top10Count || 6 }}
                  </h3>
                </div>
                <div class="text-xs text-slate-400 mt-3 pt-3 border-t border-slate-800/60 flex items-center justify-between">
                  <span>Share of Total:</span>
                  <span class="font-mono font-bold text-sky-400">{{ serpOverview()?.top10Percentage || 75.0 }}%</span>
                </div>
              </div>

              <!-- Metric 4: Top 30 Positions -->
              <div class="p-5 rounded-2xl bg-slate-900/70 border border-slate-800/80 shadow-lg flex flex-col justify-between">
                <div>
                  <span class="text-xs text-slate-400 uppercase tracking-wider font-semibold block mb-1">
                    Top 30 Positions
                  </span>
                  <h3 class="text-3xl font-bold font-mono text-purple-400 mt-1">
                    {{ serpOverview()?.top30Count || 7 }}
                  </h3>
                </div>
                <div class="text-xs text-slate-400 mt-3 pt-3 border-t border-slate-800/60 flex items-center justify-between">
                  <span>Share of Total:</span>
                  <span class="font-mono font-bold text-purple-400">{{ serpOverview()?.top30Percentage || 87.5 }}%</span>
                </div>
              </div>
            </div>

            <!-- Action Bar: Add Tracked Keyword -->
            <div class="flex items-center justify-between">
              <div class="text-sm font-semibold text-slate-300 flex items-center gap-2">
                <span>Daily SERP Positions Across Geographies</span>
                <span class="text-xs text-slate-500 font-normal">(Google Search Engine Results)</span>
              </div>
              <button
                type="button"
                (click)="showTrackModal.set(true)"
                class="px-4 py-2 rounded-xl text-xs font-semibold text-white bg-indigo-600 hover:bg-indigo-500 transition shadow-md shadow-indigo-600/30 flex items-center gap-1.5">
                <span>+</span> Track New Keyword
              </button>
            </div>

            <!-- SERP Keywords Table -->
            <div class="rounded-2xl bg-slate-900/80 border border-slate-800 shadow-xl overflow-hidden">
              <div class="overflow-x-auto">
                <table class="w-full text-left text-xs">
                  <thead class="bg-slate-950/70 border-b border-slate-800 text-slate-400 uppercase font-semibold text-[11px]">
                    <tr>
                      <th class="px-4 py-3">Keyword & URL</th>
                      <th class="px-4 py-3 text-center">Country</th>
                      <th class="px-4 py-3 text-center">Current Rank</th>
                      <th class="px-4 py-3 text-center">Movement</th>
                      <th class="px-4 py-3 text-center">Best Rank</th>
                      <th class="px-4 py-3 text-center">Position Trend (5 Days)</th>
                    </tr>
                  </thead>
                  <tbody class="divide-y divide-slate-800/60 font-sans">
                    @for (item of serpKeywords(); track item.id) {
                      <tr class="hover:bg-slate-800/30 transition">
                        <!-- Keyword & URL -->
                        <td class="px-4 py-3 max-w-sm">
                          <div class="font-semibold text-white text-sm flex items-center gap-1.5">
                            <span>{{ item.keyword }}</span>
                          </div>
                          <div class="text-slate-400 font-mono text-[11px] truncate mt-0.5 hover:text-indigo-400 cursor-pointer"
                            [title]="item.targetUrl">
                            {{ item.targetUrl }}
                          </div>
                        </td>

                        <!-- Country Flag & Code -->
                        <td class="px-4 py-3 text-center whitespace-nowrap">
                          <span class="inline-flex items-center gap-1 px-2.5 py-1 rounded-md bg-slate-800 border border-slate-700/60 font-mono text-xs text-slate-200">
                            <span>{{ getCountryFlag(item.countryCode) }}</span>
                            <span>{{ item.countryCode }}</span>
                          </span>
                        </td>

                        <!-- Current Rank -->
                        <td class="px-4 py-3 text-center whitespace-nowrap">
                          <span
                            class="inline-block px-3 py-1 rounded-full text-xs font-mono font-bold shadow-sm"
                            [class.bg-emerald-500-20]="item.currentPosition <= 3"
                            [class.text-emerald-400]="item.currentPosition <= 3"
                            [class.border]="true"
                            [class.border-emerald-500-30]="item.currentPosition <= 3"
                            [class.bg-sky-500-20]="item.currentPosition > 3 && item.currentPosition <= 10"
                            [class.text-sky-400]="item.currentPosition > 3 && item.currentPosition <= 10"
                            [class.border-sky-500-30]="item.currentPosition > 3 && item.currentPosition <= 10"
                            [class.bg-slate-800]="item.currentPosition > 10"
                            [class.text-slate-300]="item.currentPosition > 10">
                            #{{ item.currentPosition }}
                          </span>
                        </td>

                        <!-- Delta Movement Badge -->
                        <td class="px-4 py-3 text-center whitespace-nowrap">
                          @if (item.status === 'improved') {
                            <span class="inline-flex items-center gap-0.5 px-2 py-0.5 rounded text-[11px] font-mono font-bold bg-emerald-500/10 text-emerald-400 border border-emerald-500/20">
                              ▲ {{ item.delta }}
                            </span>
                          } @else if (item.status === 'declined') {
                            <span class="inline-flex items-center gap-0.5 px-2 py-0.5 rounded text-[11px] font-mono font-bold bg-rose-500/10 text-rose-400 border border-rose-500/20">
                              ▼ {{ item.delta }}
                            </span>
                          } @else if (item.status === 'new') {
                            <span class="inline-flex items-center px-2 py-0.5 rounded text-[11px] font-mono font-bold bg-indigo-500/10 text-indigo-400 border border-indigo-500/20">
                              ★ New
                            </span>
                          } @else {
                            <span class="inline-flex items-center px-2 py-0.5 rounded text-[11px] font-mono text-slate-400 bg-slate-800">
                              — 0
                            </span>
                          }
                        </td>

                        <!-- Best Rank -->
                        <td class="px-4 py-3 text-center whitespace-nowrap font-mono text-slate-300 text-xs">
                          #{{ item.bestPosition }}
                        </td>

                        <!-- Sparkline Trend Dots -->
                        <td class="px-4 py-3 text-center whitespace-nowrap">
                          <div class="flex items-center justify-center gap-1.5">
                            @for (point of item.history; track point.date) {
                              <div
                                class="w-2.5 h-2.5 rounded-full transition hover:scale-125"
                                [class.bg-emerald-400]="point.position <= 3"
                                [class.bg-sky-400]="point.position > 3 && point.position <= 10"
                                [class.bg-indigo-400]="point.position > 10 && point.position <= 30"
                                [class.bg-slate-600]="point.position > 30"
                                [title]="point.date + ': #' + point.position">
                              </div>
                            }
                          </div>
                        </td>
                      </tr>
                    }
                  </tbody>
                </table>
              </div>
            </div>
          }

        </div>
      </main>

      <!-- Track New Keyword Modal Dialog -->
      @if (showTrackModal()) {
        <div class="fixed inset-0 bg-black/70 backdrop-blur-sm flex items-center justify-center p-4 z-50">
          <div class="bg-slate-900 border border-slate-800 rounded-2xl w-full max-w-md p-6 shadow-2xl space-y-4">
            <div class="flex items-center justify-between pb-3 border-b border-slate-800">
              <h3 class="text-base font-semibold text-white flex items-center gap-2">
                <span>📈</span> Track New Keyword Position
              </h3>
              <button
                type="button"
                (click)="showTrackModal.set(false)"
                class="text-slate-400 hover:text-white transition">
                ✕
              </button>
            </div>

            <div class="space-y-3 text-xs">
              <div>
                <label class="block font-medium text-slate-400 mb-1">Search Keyword</label>
                <input
                  type="text"
                  [(ngModel)]="newKeywordName"
                  placeholder="e.g. livraison express tunisie"
                  class="w-full bg-slate-950 border border-slate-700 rounded-xl px-3 py-2 text-white placeholder-slate-600 focus:outline-none focus:border-indigo-500 font-mono"
                />
              </div>

              <div>
                <label class="block font-medium text-slate-400 mb-1">Target Landing URL</label>
                <input
                  type="url"
                  [(ngModel)]="newKeywordUrl"
                  placeholder="https://store.sgrowing.io/shop"
                  class="w-full bg-slate-950 border border-slate-700 rounded-xl px-3 py-2 text-white placeholder-slate-600 focus:outline-none focus:border-indigo-500 font-mono"
                />
              </div>

              <div class="grid grid-cols-2 gap-3">
                <div>
                  <label class="block font-medium text-slate-400 mb-1">Target Country</label>
                  <select
                    [(ngModel)]="newKeywordCountry"
                    class="w-full bg-slate-950 border border-slate-700 rounded-xl px-3 py-2 text-white focus:outline-none focus:border-indigo-500">
                    <option value="TN">🇹🇳 Tunisia (TN)</option>
                    <option value="EG">🇪🇬 Egypt (EG)</option>
                    <option value="DZ">🇩🇿 Algeria (DZ)</option>
                    <option value="MA">🇲🇦 Morocco (MA)</option>
                    <option value="SA">🇸🇦 Saudi Arabia (SA)</option>
                    <option value="US">🇺🇸 United States (US)</option>
                    <option value="FR">🇫🇷 France (FR)</option>
                  </select>
                </div>

                <div>
                  <label class="block font-medium text-slate-400 mb-1">Initial Rank (1-100)</label>
                  <input
                    type="number"
                    min="1"
                    max="100"
                    [(ngModel)]="newKeywordRank"
                    class="w-full bg-slate-950 border border-slate-700 rounded-xl px-3 py-2 text-white focus:outline-none focus:border-indigo-500 font-mono"
                  />
                </div>
              </div>
            </div>

            <div class="flex items-center justify-end gap-3 pt-3 border-t border-slate-800">
              <button
                type="button"
                (click)="showTrackModal.set(false)"
                class="px-4 py-2 rounded-xl text-xs font-semibold text-slate-400 hover:text-white transition">
                Cancel
              </button>
              <button
                type="button"
                (click)="submitTrackKeyword()"
                class="px-5 py-2 rounded-xl text-xs font-semibold text-white bg-indigo-600 hover:bg-indigo-500 transition shadow-md shadow-indigo-600/30">
                Start Tracking
              </button>
            </div>
          </div>
        </div>
      }
    </div>
  `,
  styles: [`
    .bg-emerald-500-10 { background-color: rgba(16, 185, 129, 0.1); }
    .border-emerald-500-20 { border-color: rgba(16, 185, 129, 0.2); }
    .bg-emerald-500-20 { background-color: rgba(16, 185, 129, 0.2); }
    .border-emerald-500-30 { border-color: rgba(16, 185, 129, 0.3); }

    .bg-rose-500-10 { background-color: rgba(244, 63, 94, 0.1); }
    .border-rose-500-20 { border-color: rgba(244, 63, 94, 0.2); }
    .border-rose-500-30 { border-color: rgba(244, 63, 94, 0.3); }

    .bg-sky-500-10 { background-color: rgba(14, 165, 233, 0.1); }
    .border-sky-500-20 { border-color: rgba(14, 165, 233, 0.2); }
    .bg-sky-500-20 { background-color: rgba(14, 165, 233, 0.2); }
    .border-sky-500-30 { border-color: rgba(14, 165, 233, 0.3); }

    .border-amber-500-30 { border-color: rgba(245, 158, 11, 0.3); }
  `]
})
export class SeoCrawlerStudioComponent implements OnInit {
  private readonly http = inject(HttpClient);

  public readonly activeTab = signal<'crawler' | 'serp'>('crawler');
  public readonly isCrawling = signal<boolean>(false);
  public readonly startUrl = signal<string>('https://store.sgrowing.io');
  public readonly maxPages = signal<number>(50);
  public readonly maxDepth = signal<number>(3);
  public readonly activeFilter = signal<'all' | 'broken' | 'missing_h1' | 'meta_issues' | 'orphan'>('all');
  public readonly searchQuery = signal<string>('');

  public readonly summary = signal<CrawlSummaryDto | null>(null);
  public readonly crawledPages = signal<CrawledPageDetailDto[]>([]);
  public readonly serpOverview = signal<SerpRankingsOverviewDto | null>(null);
  public readonly serpKeywords = signal<SerpKeywordRankItemDto[]>([]);

  // Track modal form state
  public readonly showTrackModal = signal<boolean>(false);
  public newKeywordName = '';
  public newKeywordUrl = '';
  public newKeywordCountry = 'TN';
  public newKeywordRank = 6;

  public ngOnInit(): void {
    this.loadInitialDemoData();
    this.fetchSerpRankings();
  }

  public triggerCrawl(): void {
    if (!this.startUrl()) return;

    this.isCrawling.set(true);

    const payload = {
      url: this.startUrl(),
      maxPages: this.maxPages(),
      maxDepth: this.maxDepth()
    };

    this.http.post<CrawlSummaryDto>('/api/v1/seo/crawler/start', payload).subscribe({
      next: (res) => {
        this.summary.set(res);
        this.fetchCrawledPages(res.crawlId);
        this.isCrawling.set(false);
      },
      error: () => {
        // Fallback demo simulation if backend is running disconnected
        this.simulateCrawlExecution();
        this.isCrawling.set(false);
      }
    });
  }

  public fetchCrawledPages(crawlId: string): void {
    this.http.get<{ items: CrawledPageDetailDto[] }>(`/api/v1/seo/crawler/${crawlId}/pages?pageSize=100`).subscribe({
      next: (res) => {
        if (res?.items) {
          this.crawledPages.set(res.items);
        }
      },
      error: () => {}
    });
  }

  public fetchSerpRankings(): void {
    this.http.get<SerpRankingsOverviewDto>('/api/v1/seo/serp/rankings').subscribe({
      next: (res) => {
        if (res) {
          this.serpOverview.set(res);
          this.serpKeywords.set(res.keywords || []);
        }
      },
      error: () => {}
    });
  }

  public submitTrackKeyword(): void {
    if (!this.newKeywordName || !this.newKeywordUrl) return;

    const payload = {
      keyword: this.newKeywordName,
      targetUrl: this.newKeywordUrl,
      countryCode: this.newKeywordCountry,
      initialPosition: this.newKeywordRank,
      searchEngine: 'Google'
    };

    this.http.post<SerpKeywordRankItemDto>('/api/v1/seo/serp/track-keyword', payload).subscribe({
      next: (item) => {
        this.serpKeywords.update(list => [item, ...list]);
        this.showTrackModal.set(false);
        this.fetchSerpRankings();
      },
      error: () => {
        // Local simulation fallback
        const mockItem: SerpKeywordRankItemDto = {
          id: Math.random().toString(),
          tenantId: '11111111-1111-1111-1111-111111111111',
          keyword: this.newKeywordName,
          targetUrl: this.newKeywordUrl,
          countryCode: this.newKeywordCountry,
          searchEngine: 'Google',
          currentPosition: this.newKeywordRank,
          previousPosition: undefined,
          delta: 'New',
          status: 'new',
          bestPosition: this.newKeywordRank,
          history: [
            { date: '2026-10-27', position: this.newKeywordRank + 2 },
            { date: '2026-10-28', position: this.newKeywordRank + 1 },
            { date: '2026-10-29', position: this.newKeywordRank }
          ],
          lastCheckedUtc: new Date().toISOString()
        };
        this.serpKeywords.update(list => [mockItem, ...list]);
        this.showTrackModal.set(false);
      }
    });
  }

  public filteredPages = computed(() => {
    let list = this.crawledPages();
    const filter = this.activeFilter();
    const query = this.searchQuery().toLowerCase().trim();

    if (filter === 'broken') {
      list = list.filter(p => p.statusCode >= 400 || p.issues.some(i => i.includes('Broken') || i.includes('404')));
    } else if (filter === 'missing_h1') {
      list = list.filter(p => p.h1Count === 0 || p.issues.includes('MissingH1'));
    } else if (filter === 'meta_issues') {
      list = list.filter(p => p.issues.some(i => i.includes('Meta') || i.includes('Title') || i.includes('Canonical')));
    } else if (filter === 'orphan') {
      list = list.filter(p => p.isOrphan);
    }

    if (query) {
      list = list.filter(p => p.url.toLowerCase().includes(query) || p.title.toLowerCase().includes(query));
    }

    return list;
  });

  public countBrokenPages = computed(() => {
    return this.crawledPages().filter(p => p.statusCode >= 400 || p.issues.some(i => i.includes('Broken') || i.includes('404'))).length;
  });

  public countMissingH1 = computed(() => {
    return this.crawledPages().filter(p => p.h1Count === 0 || p.issues.includes('MissingH1')).length;
  });

  public countMetaIssues = computed(() => {
    return this.crawledPages().filter(p => p.issues.some(i => i.includes('Meta') || i.includes('Title') || i.includes('Canonical'))).length;
  });

  public countOrphans = computed(() => {
    return this.crawledPages().filter(p => p.isOrphan).length;
  });

  public getStatusLabel(statusCode: number): string {
    switch (statusCode) {
      case 200: return 'OK';
      case 301: return 'Moved';
      case 302: return 'Found';
      case 404: return 'Not Found';
      case 500: return 'Server Error';
      default: return '';
    }
  }

  public getCountryFlag(code: string): string {
    switch (code?.toUpperCase()) {
      case 'TN': return '🇹🇳';
      case 'EG': return '🇪🇬';
      case 'DZ': return '🇩🇿';
      case 'MA': return '🇲🇦';
      case 'SA': return '🇸🇦';
      case 'US': return '🇺🇸';
      case 'FR': return '🇫🇷';
      default: return '🌐';
    }
  }

  private loadInitialDemoData(): void {
    const demoSummary: CrawlSummaryDto = {
      crawlId: 'c1111111-2222-3333-4444-555555555555',
      tenantId: '11111111-1111-1111-1111-111111111111',
      startingUrl: 'https://store.sgrowing.io',
      status: 'Completed',
      overallHealthScore: 88,
      crawlabilityScore: 95,
      metadataScore: 82,
      linkIntegrityScore: 90,
      totalPagesCrawled: 6,
      brokenLinksCount: 1,
      missingTitleCount: 0,
      missingMetaCount: 1,
      missingH1Count: 1,
      multipleH1Count: 0,
      missingCanonicalCount: 0,
      orphanPagesCount: 1,
      deepPagesCount: 0,
      avgResponseTimeMs: 165,
      statusDistribution: { '200': 5, '404': 1 },
      issueBreakdown: { 'MissingMetaDescription': 1, 'MissingH1': 1, 'BrokenLink_404': 1, 'OrphanPage': 1 },
      startedAtUtc: new Date().toISOString(),
      completedAtUtc: new Date().toISOString()
    };
    this.summary.set(demoSummary);

    const demoPages: CrawledPageDetailDto[] = [
      {
        id: 'p1',
        crawlId: demoSummary.crawlId,
        url: 'https://store.sgrowing.io/',
        path: '/',
        statusCode: 200,
        responseTimeMs: 142,
        title: 'SGrowing Store - North Africa Premium E-Commerce COD',
        titleLength: 52,
        metaDescription: 'Shop premier apparel and electronics with prompt doorstep delivery across Tunisia, Algeria and Morocco.',
        metaDescriptionLength: 108,
        h1Count: 1,
        h1Tag: 'Elevate Your Shopping',
        hasCanonical: true,
        canonicalUrl: 'https://store.sgrowing.io/',
        isCanonicalSelfReferential: true,
        hasOpenGraph: true,
        openGraphTagsPresent: ['og:title', 'og:image', 'og:description'],
        inboundLinksCount: 5,
        outboundLinksCount: 12,
        pageRank: 0.32,
        pageRankFormatted: 'PR 2.24',
        crawlDepth: 0,
        isOrphan: false,
        issues: [],
        crawledAtUtc: new Date().toISOString()
      },
      {
        id: 'p2',
        crawlId: demoSummary.crawlId,
        url: 'https://store.sgrowing.io/shop/shoes',
        path: '/shop/shoes',
        statusCode: 200,
        responseTimeMs: 195,
        title: 'Chaussures & Baskets Cuir - Collection 2026',
        titleLength: 42,
        metaDescription: 'Découvrez notre collection de chaussures en cuir véritable fabriquées à la main avec livraison rapide en 24h.',
        metaDescriptionLength: 110,
        h1Count: 1,
        h1Tag: 'Baskets & Chaussures Cuir',
        hasCanonical: true,
        canonicalUrl: 'https://store.sgrowing.io/shop/shoes',
        isCanonicalSelfReferential: true,
        hasOpenGraph: true,
        openGraphTagsPresent: ['og:title', 'og:image', 'og:description'],
        inboundLinksCount: 3,
        outboundLinksCount: 8,
        pageRank: 0.24,
        pageRankFormatted: 'PR 1.68',
        crawlDepth: 1,
        isOrphan: false,
        issues: [],
        crawledAtUtc: new Date().toISOString()
      },
      {
        id: 'p3',
        crawlId: demoSummary.crawlId,
        url: 'https://store.sgrowing.io/checkout',
        path: '/checkout',
        statusCode: 200,
        responseTimeMs: 230,
        title: 'Checkout',
        titleLength: 8,
        metaDescription: '',
        metaDescriptionLength: 0,
        h1Count: 0,
        h1Tag: '',
        hasCanonical: true,
        canonicalUrl: 'https://store.sgrowing.io/checkout',
        isCanonicalSelfReferential: true,
        hasOpenGraph: false,
        openGraphTagsPresent: [],
        inboundLinksCount: 2,
        outboundLinksCount: 1,
        pageRank: 0.15,
        pageRankFormatted: 'PR 1.05',
        crawlDepth: 1,
        isOrphan: false,
        issues: ['TitleTooShort', 'MissingMetaDescription', 'MissingH1', 'IncompleteOpenGraph'],
        crawledAtUtc: new Date().toISOString()
      },
      {
        id: 'p4',
        crawlId: demoSummary.crawlId,
        url: 'https://store.sgrowing.io/broken-campaign',
        path: '/broken-campaign',
        statusCode: 404,
        responseTimeMs: 85,
        title: '404 Not Found',
        titleLength: 13,
        metaDescription: '',
        metaDescriptionLength: 0,
        h1Count: 1,
        h1Tag: 'Page Not Found',
        hasCanonical: false,
        isCanonicalSelfReferential: false,
        hasOpenGraph: false,
        openGraphTagsPresent: [],
        inboundLinksCount: 1,
        outboundLinksCount: 0,
        pageRank: 0.08,
        pageRankFormatted: 'PR 0.56',
        crawlDepth: 2,
        isOrphan: false,
        issues: ['BrokenLink_404', 'MissingCanonical', 'MissingMetaDescription'],
        crawledAtUtc: new Date().toISOString()
      },
      {
        id: 'p5',
        crawlId: demoSummary.crawlId,
        url: 'https://store.sgrowing.io/promotions/secret-cod',
        path: '/promotions/secret-cod',
        statusCode: 200,
        responseTimeMs: 115,
        title: 'Offres Secrètes Cash On Delivery Exclusives',
        titleLength: 43,
        metaDescription: 'Accédez aux offres VIP réservées aux clients fidèles avec réductions supplémentaires sur vos commandes groupées.',
        metaDescriptionLength: 114,
        h1Count: 1,
        h1Tag: 'Offres Secrètes',
        hasCanonical: true,
        canonicalUrl: 'https://store.sgrowing.io/promotions/secret-cod',
        isCanonicalSelfReferential: true,
        hasOpenGraph: true,
        openGraphTagsPresent: ['og:title', 'og:image', 'og:description'],
        inboundLinksCount: 0,
        outboundLinksCount: 3,
        pageRank: 0.06,
        pageRankFormatted: 'PR 0.42',
        crawlDepth: 2,
        isOrphan: true,
        issues: ['OrphanPage'],
        crawledAtUtc: new Date().toISOString()
      }
    ];
    this.crawledPages.set(demoPages);

    const demoKeywords: SerpKeywordRankItemDto[] = [
      {
        id: 'k1',
        tenantId: '11111111-1111-1111-1111-111111111111',
        keyword: 'baskets cuir tunisie',
        targetUrl: 'https://store.sgrowing.io/shop/shoes',
        countryCode: 'TN',
        searchEngine: 'Google',
        currentPosition: 2,
        previousPosition: 4,
        delta: '+2',
        status: 'improved',
        bestPosition: 1,
        history: [
          { date: '2026-10-25', position: 6 },
          { date: '2026-10-26', position: 5 },
          { date: '2026-10-27', position: 5 },
          { date: '2026-10-28', position: 4 },
          { date: '2026-10-29', position: 2 }
        ],
        lastCheckedUtc: new Date().toISOString()
      },
      {
        id: 'k2',
        tenantId: '11111111-1111-1111-1111-111111111111',
        keyword: 'livraison express tunisie cod',
        targetUrl: 'https://store.sgrowing.io/delivery',
        countryCode: 'TN',
        searchEngine: 'Google',
        currentPosition: 4,
        previousPosition: 7,
        delta: '+3',
        status: 'improved',
        bestPosition: 4,
        history: [
          { date: '2026-10-25', position: 10 },
          { date: '2026-10-26', position: 9 },
          { date: '2026-10-27', position: 8 },
          { date: '2026-10-28', position: 7 },
          { date: '2026-10-29', position: 4 }
        ],
        lastCheckedUtc: new Date().toISOString()
      },
      {
        id: 'k3',
        tenantId: '11111111-1111-1111-1111-111111111111',
        keyword: 'omnichannel ecommerce crm',
        targetUrl: 'https://sgrowing.io/features/crm',
        countryCode: 'US',
        searchEngine: 'Google',
        currentPosition: 8,
        previousPosition: 8,
        delta: '0',
        status: 'unchanged',
        bestPosition: 6,
        history: [
          { date: '2026-10-25', position: 8 },
          { date: '2026-10-26', position: 7 },
          { date: '2026-10-27', position: 8 },
          { date: '2026-10-28', position: 8 },
          { date: '2026-10-29', position: 8 }
        ],
        lastCheckedUtc: new Date().toISOString()
      },
      {
        id: 'k4',
        tenantId: '11111111-1111-1111-1111-111111111111',
        keyword: 'plateforme dropshipping algerie',
        targetUrl: 'https://store.sgrowing.io/dz',
        countryCode: 'DZ',
        searchEngine: 'Google',
        currentPosition: 12,
        previousPosition: 9,
        delta: '-3',
        status: 'declined',
        bestPosition: 7,
        history: [
          { date: '2026-10-25', position: 7 },
          { date: '2026-10-26', position: 8 },
          { date: '2026-10-27', position: 9 },
          { date: '2026-10-28', position: 9 },
          { date: '2026-10-29', position: 12 }
        ],
        lastCheckedUtc: new Date().toISOString()
      },
      {
        id: 'k5',
        tenantId: '11111111-1111-1111-1111-111111111111',
        keyword: 'ai agent inbound marketing',
        targetUrl: 'https://sgrowing.io/agents',
        countryCode: 'US',
        searchEngine: 'Google',
        currentPosition: 1,
        previousPosition: undefined,
        delta: 'New',
        status: 'new',
        bestPosition: 1,
        history: [
          { date: '2026-10-29', position: 1 }
        ],
        lastCheckedUtc: new Date().toISOString()
      }
    ];
    this.serpKeywords.set(demoKeywords);
  }

  private simulateCrawlExecution(): void {
    // Generate simulated dynamic result based on user input URL
    const url = this.startUrl();
    const count = Math.min(this.maxPages(), 8);
    const updatedSummary: CrawlSummaryDto = {
      crawlId: Math.random().toString(),
      tenantId: '11111111-1111-1111-1111-111111111111',
      startingUrl: url,
      status: 'Completed',
      overallHealthScore: 92,
      crawlabilityScore: 98,
      metadataScore: 89,
      linkIntegrityScore: 92,
      totalPagesCrawled: count,
      brokenLinksCount: 0,
      missingTitleCount: 0,
      missingMetaCount: 1,
      missingH1Count: 0,
      multipleH1Count: 0,
      missingCanonicalCount: 0,
      orphanPagesCount: 0,
      deepPagesCount: 0,
      avgResponseTimeMs: 135,
      statusDistribution: { '200': count },
      issueBreakdown: { 'MetaDescriptionTooShort': 1 },
      startedAtUtc: new Date().toISOString(),
      completedAtUtc: new Date().toISOString()
    };
    this.summary.set(updatedSummary);
  }
}
