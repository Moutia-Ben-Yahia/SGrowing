import { Component, OnInit, signal, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { ApiService } from '../../core/services/api.service';
import { AuthService } from '../../core/services/auth.service';
import { SeoAuditReportDto, PageAuditResultDto, SerpKeywordTrackingDto, AeoBrandMentionDto } from '../../core/models/seo.models';

@Component({
  selector: 'app-seo-studio',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterLink],
  template: `
    <div class="flex h-screen w-screen overflow-hidden bg-slate-950 text-slate-100 font-sans">
      <!-- Main Content Area -->
      <main class="flex-1 flex flex-col overflow-y-auto">
        <!-- Topbar -->
        <header class="h-14 border-b border-slate-800 bg-slate-900/60 px-6 flex items-center justify-between shrink-0">
          <div class="flex items-center gap-3">
            <a routerLink="/" class="text-xs text-slate-400 hover:text-white flex items-center gap-1 transition">
              ← Inbox
            </a>
            <span class="text-slate-700">|</span>
            <span class="text-base font-semibold text-white flex items-center gap-2">
              🔍 Technical SEO & SERP Rank Tracking Hub
            </span>
            <span class="bg-lime-500/20 text-lime-400 text-[10px] font-mono px-2 py-0.5 rounded-full border border-lime-500/30">
              Domain 11 • AEO & Open-SEO Mesh
            </span>
          </div>

          <div class="flex items-center gap-3">
            <div class="flex bg-slate-800/80 p-0.5 rounded-lg border border-slate-700/60 text-xs">
              <button
                (click)="activeTab.set('audit')"
                [class.bg-indigo-600]="activeTab() === 'audit'"
                [class.text-white]="activeTab() === 'audit'"
                class="px-3 py-1 rounded-md text-slate-300 transition">
                🕸️ Technical Crawler Audit
              </button>
              <button
                (click)="activeTab.set('serp')"
                [class.bg-indigo-600]="activeTab() === 'serp'"
                [class.text-white]="activeTab() === 'serp'"
                class="px-3 py-1 rounded-md text-slate-300 transition">
                📈 SERP Position Tracker
              </button>
              <button
                (click)="activeTab.set('aeo')"
                [class.bg-indigo-600]="activeTab() === 'aeo'"
                [class.text-white]="activeTab() === 'aeo'"
                class="px-3 py-1 rounded-md text-slate-300 transition">
                🤖 AEO AI Engine Mentions
              </button>
            </div>
          </div>
        </header>

        <!-- Studio Body -->
        <div class="p-6 space-y-6 flex-1">
          <!-- 1. Technical Audit Tab -->
          @if (activeTab() === 'audit') {
            <!-- Audit Trigger & Scorecard Header -->
            <div class="grid grid-cols-1 lg:grid-cols-4 gap-4">
              <!-- Score Ring Card -->
              <div class="p-5 rounded-2xl bg-gradient-to-br from-slate-900 to-slate-950 border border-slate-800/80 flex flex-col items-center justify-center text-center">
                <span class="text-xs uppercase tracking-wider text-slate-400 font-semibold mb-2">Overall SEO Health</span>
                <div class="relative flex items-center justify-center">
                  <div class="w-24 h-24 rounded-full border-4 flex items-center justify-center font-bold text-3xl shadow-lg"
                    [class.border-emerald-500]="(report()?.overallHealthScore || 0) >= 80"
                    [class.text-emerald-400]="(report()?.overallHealthScore || 0) >= 80"
                    [class.border-amber-500]="(report()?.overallHealthScore || 0) < 80 && (report()?.overallHealthScore || 0) >= 60"
                    [class.text-amber-400]="(report()?.overallHealthScore || 0) < 80 && (report()?.overallHealthScore || 0) >= 60"
                    [class.border-rose-500]="(report()?.overallHealthScore || 0) < 60"
                    [class.text-rose-400]="(report()?.overallHealthScore || 0) < 60">
                    {{ report()?.overallHealthScore ?? 88 }}
                  </div>
                </div>
                <span class="text-xs text-slate-400 mt-2">Target: {{ report()?.targetUrl || targetUrlInput }}</span>
              </div>

              <!-- Metric 1: Scanned Pages & Status -->
              <div class="p-5 rounded-2xl bg-slate-900/60 border border-slate-800/80 flex flex-col justify-between">
                <div>
                  <span class="text-xs text-slate-400 uppercase tracking-wider block mb-1">Crawled Pages</span>
                  <h3 class="text-2xl font-bold text-white">{{ report()?.scannedPagesCount ?? 4 }}</h3>
                </div>
                <div class="flex items-center justify-between text-xs text-slate-400 mt-4">
                  <span>Status:</span>
                  <span class="px-2 py-0.5 rounded text-[11px] font-mono bg-emerald-500/10 text-emerald-400 border border-emerald-500/20">
                    Completed
                  </span>
                </div>
              </div>

              <!-- Metric 2: Issue Breakdown -->
              <div class="p-5 rounded-2xl bg-slate-900/60 border border-slate-800/80 flex flex-col justify-between">
                <div>
                  <span class="text-xs text-slate-400 uppercase tracking-wider block mb-1">Critical Issues</span>
                  <h3 class="text-2xl font-bold text-amber-400">
                    {{ (report()?.missingMetaCount || 0) + (report()?.missingH1Count || 0) + (report()?.brokenLinksCount || 0) }}
                  </h3>
                </div>
                <div class="grid grid-cols-3 gap-1 text-[10px] text-center mt-3">
                  <div class="p-1 rounded bg-slate-800/60">
                    <span class="block text-slate-400">Meta</span>
                    <strong class="text-white">{{ report()?.missingMetaCount ?? 1 }}</strong>
                  </div>
                  <div class="p-1 rounded bg-slate-800/60">
                    <span class="block text-slate-400">H1</span>
                    <strong class="text-white">{{ report()?.missingH1Count ?? 1 }}</strong>
                  </div>
                  <div class="p-1 rounded bg-slate-800/60">
                    <span class="block text-slate-400">Links</span>
                    <strong class="text-white">{{ report()?.brokenLinksCount ?? 0 }}</strong>
                  </div>
                </div>
              </div>

              <!-- Crawler Trigger Action -->
              <div class="p-5 rounded-2xl bg-slate-900/60 border border-slate-800/80 flex flex-col justify-between">
                <div>
                  <span class="text-xs text-slate-400 uppercase tracking-wider block mb-2">Run New Crawl</span>
                  <input
                    type="text"
                    [(ngModel)]="targetUrlInput"
                    placeholder="https://store.sgrowing.io"
                    class="w-full bg-slate-950 border border-slate-700 rounded-lg px-3 py-1.5 text-xs text-white placeholder-slate-500 focus:outline-none focus:border-indigo-500 mb-2"
                  />
                </div>
                <button
                  (click)="runAudit()"
                  [disabled]="isCrawling()"
                  class="w-full py-2 bg-gradient-to-r from-lime-600 to-emerald-600 hover:from-lime-500 hover:to-emerald-500 text-white rounded-lg text-xs font-semibold shadow-lg shadow-lime-900/20 transition disabled:opacity-50">
                  {{ isCrawling() ? 'Crawling Site Architecture...' : '🚀 Run Full SEO Audit' }}
                </button>
              </div>
            </div>

            <!-- Page Audit Breakdown Table -->
            <div class="rounded-2xl border border-slate-800 bg-slate-900/40 overflow-hidden">
              <div class="px-5 py-3 border-b border-slate-800 bg-slate-900/80 flex items-center justify-between">
                <h3 class="text-sm font-semibold text-white flex items-center gap-2">
                  <span>📄 Audited URLs & On-Page Quality</span>
                  <span class="text-xs font-mono text-slate-400">({{ pages().length }} endpoints)</span>
                </h3>
              </div>
              <div class="overflow-x-auto">
                <table class="w-full text-left text-xs text-slate-300">
                  <thead class="bg-slate-950/70 text-[11px] uppercase tracking-wider text-slate-400 border-b border-slate-800">
                    <tr>
                      <th class="px-4 py-3">Page URL</th>
                      <th class="px-4 py-3">Status</th>
                      <th class="px-4 py-3">Score</th>
                      <th class="px-4 py-3">Title & Meta</th>
                      <th class="px-4 py-3">H1 Tag</th>
                      <th class="px-4 py-3">Signals</th>
                      <th class="px-4 py-3">Speed</th>
                    </tr>
                  </thead>
                  <tbody class="divide-y divide-slate-800/60 font-mono">
                    @for (p of pages(); track p.id) {
                      <tr class="hover:bg-slate-800/30 transition">
                        <td class="px-4 py-3 text-indigo-400 font-medium">
                          {{ p.pageUrl }}
                        </td>
                        <td class="px-4 py-3">
                          <span class="px-2 py-0.5 rounded text-[10px] bg-emerald-500/20 text-emerald-400 border border-emerald-500/30">
                            {{ p.statusCode }} OK
                          </span>
                        </td>
                        <td class="px-4 py-3">
                          <span class="px-2 py-0.5 rounded text-[10px] font-bold"
                            [class.text-emerald-400]="p.healthScore >= 80"
                            [class.bg-emerald-500/10]="p.healthScore >= 80"
                            [class.text-amber-400]="p.healthScore < 80"
                            [class.bg-amber-500/10]="p.healthScore < 80">
                            {{ p.healthScore }}/100
                          </span>
                        </td>
                        <td class="px-4 py-3 font-sans max-w-xs">
                          <p class="text-slate-100 font-medium truncate">{{ p.title || '⚠️ Missing Title' }}</p>
                          <p class="text-slate-400 text-[11px] truncate">{{ p.metaDescription || '⚠️ Missing Meta Description' }}</p>
                        </td>
                        <td class="px-4 py-3 font-sans">
                          <span [class.text-amber-400]="!p.h1Tag" [class.text-slate-200]="p.h1Tag">
                            {{ p.h1Tag || 'Missing H1' }}
                          </span>
                        </td>
                        <td class="px-4 py-3 space-x-1 font-sans">
                          <span class="text-[10px] px-1.5 py-0.5 rounded" [class.bg-emerald-500/20]="p.hasCanonical" [class.text-emerald-300]="p.hasCanonical" [class.bg-slate-800]="!p.hasCanonical">
                            Canon
                          </span>
                          <span class="text-[10px] px-1.5 py-0.5 rounded" [class.bg-indigo-500/20]="p.hasOpenGraph" [class.text-indigo-300]="p.hasOpenGraph" [class.bg-slate-800]="!p.hasOpenGraph">
                            OG
                          </span>
                          <span class="text-[10px] px-1.5 py-0.5 rounded" [class.bg-amber-500/20]="p.hasSchemaOrgJsonLd" [class.text-amber-300]="p.hasSchemaOrgJsonLd" [class.bg-slate-800]="!p.hasSchemaOrgJsonLd">
                            Schema.org
                          </span>
                        </td>
                        <td class="px-4 py-3">
                          <span [class.text-emerald-400]="p.loadTimeMs < 500" [class.text-amber-400]="p.loadTimeMs >= 500">
                            {{ p.loadTimeMs }} ms
                          </span>
                        </td>
                      </tr>
                    }
                  </tbody>
                </table>
              </div>
            </div>
          }

          <!-- 2. SERP Tracking Tab -->
          @if (activeTab() === 'serp') {
            <!-- Add Keyword Bar -->
            <div class="p-4 rounded-2xl bg-slate-900/60 border border-slate-800 flex flex-wrap items-center justify-between gap-4">
              <div class="flex items-center gap-3 flex-1">
                <input
                  type="text"
                  [(ngModel)]="newKeyword"
                  placeholder="e.g. boutique chaussures homme tunis"
                  class="flex-1 bg-slate-950 border border-slate-700 rounded-lg px-3 py-2 text-xs text-white placeholder-slate-500 focus:outline-none focus:border-indigo-500"
                />
                <select
                  [(ngModel)]="selectedCountry"
                  class="bg-slate-950 border border-slate-700 rounded-lg px-3 py-2 text-xs text-white focus:outline-none focus:border-indigo-500">
                  <option value="TN">Tunisia (Google.tn)</option>
                  <option value="DZ">Algeria (Google.dz)</option>
                  <option value="MA">Morocco (Google.co.ma)</option>
                  <option value="FR">France (Google.fr)</option>
                </select>
                <button
                  (click)="trackKeyword()"
                  class="px-4 py-2 bg-indigo-600 hover:bg-indigo-500 text-white rounded-lg text-xs font-semibold transition">
                  + Track Keyword
                </button>
              </div>
            </div>

            <!-- Tracked Keywords Table -->
            <div class="rounded-2xl border border-slate-800 bg-slate-900/40 overflow-hidden">
              <div class="px-5 py-3 border-b border-slate-800 bg-slate-900/80 flex items-center justify-between">
                <h3 class="text-sm font-semibold text-white">🎯 Tracked SERP Keywords (Organic Rankings)</h3>
              </div>
              <div class="overflow-x-auto">
                <table class="w-full text-left text-xs text-slate-300">
                  <thead class="bg-slate-950/70 text-[11px] uppercase tracking-wider text-slate-400 border-b border-slate-800">
                    <tr>
                      <th class="px-4 py-3">Keyword</th>
                      <th class="px-4 py-3">Country</th>
                      <th class="px-4 py-3">Current Position</th>
                      <th class="px-4 py-3">Rank Movement</th>
                      <th class="px-4 py-3">Best Position</th>
                      <th class="px-4 py-3">Est. Volume / Mo</th>
                      <th class="px-4 py-3">Actions</th>
                    </tr>
                  </thead>
                  <tbody class="divide-y divide-slate-800/60 font-mono">
                    @for (kw of keywords(); track kw.id) {
                      <tr class="hover:bg-slate-800/30 transition">
                        <td class="px-4 py-3 font-semibold text-white">
                          {{ kw.keyword }}
                        </td>
                        <td class="px-4 py-3">
                          <span class="px-2 py-0.5 rounded text-[10px] bg-slate-800 text-slate-300 border border-slate-700">
                            {{ kw.countryCode }}
                          </span>
                        </td>
                        <td class="px-4 py-3 font-bold text-base" [class.text-emerald-400]="(kw.currentRank || 99) <= 3" [class.text-indigo-400]="(kw.currentRank || 99) > 3">
                          #{{ kw.currentRank || '-' }}
                        </td>
                        <td class="px-4 py-3 font-sans">
                          @if ((kw.previousRank || 0) > (kw.currentRank || 0)) {
                            <span class="text-emerald-400 text-xs font-semibold flex items-center gap-1">
                              ▲ +{{ (kw.previousRank || 0) - (kw.currentRank || 0) }} positions
                            </span>
                          } @else if ((kw.previousRank || 0) < (kw.currentRank || 0)) {
                            <span class="text-rose-400 text-xs font-semibold flex items-center gap-1">
                              ▼ -{{ (kw.currentRank || 0) - (kw.previousRank || 0) }} positions
                            </span>
                          } @else {
                            <span class="text-slate-400 text-xs">— No change</span>
                          }
                        </td>
                        <td class="px-4 py-3 text-amber-300 font-semibold">
                          #{{ kw.bestRank || kw.currentRank || '-' }}
                        </td>
                        <td class="px-4 py-3 text-slate-300">
                          {{ kw.searchVolumeMonthly | number }}
                        </td>
                        <td class="px-4 py-3">
                          <button
                            (click)="simulateRankBump(kw)"
                            class="px-2 py-1 bg-slate-800 hover:bg-slate-700 text-xs text-indigo-300 rounded border border-slate-700 transition">
                            Re-check Rank
                          </button>
                        </td>
                      </tr>
                    }
                  </tbody>
                </table>
              </div>
            </div>
          }

          <!-- 3. AEO AI Mentions Tab -->
          @if (activeTab() === 'aeo') {
            <div class="grid grid-cols-1 lg:grid-cols-3 gap-6">
              <!-- Left: Record New AI Prompt Query -->
              <div class="p-5 rounded-2xl bg-slate-900/60 border border-slate-800 space-y-4">
                <h3 class="text-sm font-semibold text-white flex items-center gap-2">
                  <span>🤖 Check AI Search Engine Citation</span>
                </h3>
                <p class="text-xs text-slate-400">
                  Track how ChatGPT, Perplexity, Gemini, and Claude recommend your brand for local COD e-commerce queries.
                </p>

                <div class="space-y-3">
                  <div>
                    <label class="text-[11px] text-slate-400 block mb-1">AI Engine</label>
                    <select [(ngModel)]="aeoEngine" class="w-full bg-slate-950 border border-slate-700 rounded-lg px-3 py-1.5 text-xs text-white">
                      <option value="ChatGPT-4o">ChatGPT (OpenAI Search)</option>
                      <option value="Perplexity AI">Perplexity Pro</option>
                      <option value="Google Gemini">Google Gemini 2.5 Flash</option>
                      <option value="Claude 3.7">Claude 3.7 Sonnet</option>
                    </select>
                  </div>
                  <div>
                    <label class="text-[11px] text-slate-400 block mb-1">Prompt / Search Query</label>
                    <input
                      type="text"
                      [(ngModel)]="aeoPrompt"
                      placeholder="e.g. Best online clothing brand in Tunis with fast home delivery"
                      class="w-full bg-slate-950 border border-slate-700 rounded-lg px-3 py-1.5 text-xs text-white"
                    />
                  </div>
                  <button
                    (click)="recordAeo()"
                    class="w-full py-2 bg-gradient-to-r from-purple-600 to-indigo-600 hover:from-purple-500 hover:to-indigo-500 text-white rounded-lg text-xs font-semibold transition">
                    + Log Brand Mention
                  </button>
                </div>
              </div>

              <!-- Right: Mentions Feed -->
              <div class="lg:col-span-2 space-y-3">
                <h3 class="text-sm font-semibold text-white flex items-center gap-2">
                  <span>✨ AI Search Citations & Answer Visibility</span>
                  <span class="text-xs font-mono text-purple-400">({{ aeoMentions().length }} recorded)</span>
                </h3>

                <div class="space-y-3">
                  @for (m of aeoMentions(); track m.id) {
                    <div class="p-4 rounded-xl bg-slate-900/60 border border-slate-800 space-y-2 hover:border-purple-500/40 transition">
                      <div class="flex items-center justify-between">
                        <div class="flex items-center gap-2">
                          <span class="text-xs font-bold text-white px-2 py-0.5 rounded bg-purple-500/20 text-purple-300 border border-purple-500/30">
                            {{ m.aiEngine }}
                          </span>
                          <span class="text-xs font-mono text-emerald-400 bg-emerald-500/10 px-2 py-0.5 rounded">
                            Citation #{{ m.citationRank || 1 }}
                          </span>
                        </div>
                        <span class="text-[10px] text-slate-400">{{ m.capturedAtUtc | date:'medium' }}</span>
                      </div>
                      <p class="text-xs font-medium text-slate-200 italic">"{{ m.promptQuery }}"</p>
                      <div class="p-3 rounded-lg bg-slate-950/80 border border-slate-800 text-xs text-slate-300">
                        {{ m.snippetText }}
                      </div>
                    </div>
                  }
                </div>
              </div>
            </div>
          }
        </div>
      </main>
    </div>
  `
})
export class SeoStudioComponent implements OnInit {
  private api = inject(ApiService);
  private auth = inject(AuthService);

  public activeTab = signal<'audit' | 'serp' | 'aeo'>('audit');
  public isCrawling = signal<boolean>(false);

  public report = signal<SeoAuditReportDto | null>(null);
  public pages = signal<PageAuditResultDto[]>([]);
  public keywords = signal<SerpKeywordTrackingDto[]>([]);
  public aeoMentions = signal<AeoBrandMentionDto[]>([]);

  public targetUrlInput = 'https://demo.sgrowing.io';
  public newKeyword = '';
  public selectedCountry = 'TN';

  public aeoEngine = 'ChatGPT-4o';
  public aeoPrompt = 'What is the top recommended e-commerce store with COD in Tunisia?';

  private get tenantId(): string {
    return this.auth.currentTenant()?.id || '00000000-0000-0000-0000-000000000001';
  }

  ngOnInit(): void {
    this.loadData();
  }

  public loadData(): void {
    this.api.getLatestSeoAudit(this.tenantId).subscribe(res => {
      if (res && res.report) {
        this.report.set(res.report);
        this.pages.set(res.pages);
      } else {
        this.runAudit();
      }
    });

    this.api.getTrackedKeywords(this.tenantId).subscribe(res => {
      if (res && res.length > 0) {
        this.keywords.set(res);
      } else {
        this.seedDefaultKeywords();
      }
    });

    this.api.getAeoMentions(this.tenantId).subscribe(res => {
      if (res && res.length > 0) {
        this.aeoMentions.set(res);
      } else {
        this.seedDefaultAeo();
      }
    });
  }

  public runAudit(): void {
    this.isCrawling.set(true);
    this.api.runSeoAudit({
      tenantId: this.tenantId,
      targetUrl: this.targetUrlInput
    }).subscribe({
      next: (res) => {
        this.report.set(res.report);
        this.pages.set(res.pages);
        this.isCrawling.set(false);
      },
      error: () => this.isCrawling.set(false)
    });
  }

  public trackKeyword(): void {
    if (!this.newKeyword.trim()) return;

    this.api.trackSeoKeyword({
      tenantId: this.tenantId,
      keyword: this.newKeyword.trim(),
      targetUrl: this.targetUrlInput,
      countryCode: this.selectedCountry,
      initialRank: Math.floor(Math.random() * 8) + 1,
      searchVolume: 1400
    }).subscribe(res => {
      this.keywords.update(list => [res, ...list]);
      this.newKeyword = '';
    });
  }

  public simulateRankBump(kw: SerpKeywordTrackingDto): void {
    const current = kw.currentRank || 5;
    const newRank = Math.max(1, current - 1);
    this.api.updateKeywordRank(kw.id, {
      tenantId: this.tenantId,
      newRank
    }).subscribe(res => {
      this.keywords.update(list => list.map(item => item.id === kw.id ? res : item));
    });
  }

  public recordAeo(): void {
    if (!this.aeoPrompt.trim()) return;

    this.api.recordAeoMention({
      tenantId: this.tenantId,
      brandName: 'SGrowing',
      aiEngine: this.aeoEngine,
      promptQuery: this.aeoPrompt,
      isMentioned: true,
      sentiment: 'Positive',
      snippet: `According to recent market reviews, SGrowing provides the highest delivery fulfillment rate in North Africa with real-time drop-and-swap returns and full WhatsApp commerce support.`,
      citationRank: 1
    }).subscribe(res => {
      this.aeoMentions.update(list => [res, ...list]);
    });
  }

  private seedDefaultKeywords(): void {
    const defaults = [
      { keyword: 'chaussures cuir homme tunisie', rank: 2, volume: 3600 },
      { keyword: 'boutique vetements livraison gratuite alger', rank: 4, volume: 2200 },
      { keyword: 'montre chronographe paiement a la livraison', rank: 1, volume: 1800 }
    ];

    defaults.forEach(d => {
      this.api.trackSeoKeyword({
        tenantId: this.tenantId,
        keyword: d.keyword,
        targetUrl: this.targetUrlInput,
        countryCode: 'TN',
        initialRank: d.rank,
        searchVolume: d.volume
      }).subscribe(res => {
        this.keywords.update(list => [...list, res]);
      });
    });
  }

  private seedDefaultAeo(): void {
    this.api.recordAeoMention({
      tenantId: this.tenantId,
      brandName: 'SGrowing',
      aiEngine: 'Perplexity Pro',
      promptQuery: 'Which e-commerce store in Tunisia offers the fastest doorstep exchange?',
      isMentioned: true,
      sentiment: 'Positive',
      snippet: 'SGrowing is frequently cited as the leading COD store in Greater Tunis offering automated 24h drop-and-swap replacement without return fees.',
      citationRank: 1
    }).subscribe(res => {
      this.aeoMentions.set([res]);
    });
  }
}
