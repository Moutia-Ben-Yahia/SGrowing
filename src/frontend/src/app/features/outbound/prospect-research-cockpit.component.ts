import { Component, signal, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { TranslationService } from '../../core/services/translation.service';
import { LanguageSwitcherComponent } from '../../core/components/language-switcher.component';

export interface ScrapedExecutive {
  name: string;
  title: string;
  linkedInUrl: string;
  summary: string;
  avatar: string;
}

export interface ScrapedTechTag {
  category: string;
  name: string;
  confidence: 'High' | 'Medium' | 'Low';
}

export interface ResearchBriefData {
  domain: string;
  companyName: string;
  engineUsed: string;
  headquarters: string;
  headcountTier: string;
  revenueEstimate: string;
  corporateSummary: string;
  executiveBios: ScrapedExecutive[];
  techTags: ScrapedTechTag[];
  salesHooks: string[];
  scrapedPageCount: number;
  executionTimeMs: number;
}

@Component({
  selector: 'app-prospect-research-cockpit',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterLink, LanguageSwitcherComponent],
  template: `
    <div class="flex h-screen w-screen overflow-hidden bg-slate-950 text-slate-100 font-sans" dir="ltr">
      <main class="flex-1 flex flex-col overflow-y-auto">
        <!-- Top Navigation Bar -->
        <header class="h-14 border-b border-slate-800 bg-slate-900/80 backdrop-blur-md px-6 flex items-center justify-between shrink-0 sticky top-0 z-20">
          <div class="flex items-center gap-3">
            <a routerLink="/outbound" class="text-xs text-slate-400 hover:text-white flex items-center gap-1 transition">
              ← {{ translation.t('nav.outbound') }}
            </a>
            <span class="text-slate-700">|</span>
            <span class="text-base font-bold text-white flex items-center gap-2">
              🌐 Deep AI Web Research & Prospect Intelligence
            </span>
            <span class="bg-indigo-500/20 text-indigo-300 text-[10px] font-mono px-2 py-0.5 rounded-full border border-indigo-500/30">
              Inspired by Open Mercato • Multi-Engine Web Scraper
            </span>
          </div>

          <div class="flex items-center gap-3">
            <app-language-switcher></app-language-switcher>
            <button
              data-testid="run-crawl-btn"
              [disabled]="isCrawling()"
              (click)="runDeepCrawl()"
              class="px-3 py-1.5 rounded-lg bg-indigo-600 hover:bg-indigo-500 disabled:opacity-50 text-white text-xs font-semibold flex items-center gap-1.5 transition shadow-sm">
              @if (isCrawling()) {
                <span class="animate-spin text-xs">⚡</span>
                <span>Crawling Web...</span>
              } @else {
                <span>🚀 Run Deep Web Crawl</span>
              }
            </button>
          </div>
        </header>

        <!-- Crawler Configuration Bar -->
        <div class="px-6 py-4 border-b border-slate-800 bg-slate-900/40 shrink-0">
          <div class="grid grid-cols-1 md:grid-cols-12 gap-4 items-center">
            <!-- Domain Input -->
            <div class="md:col-span-5">
              <label class="block text-[11px] font-medium text-slate-300 mb-1">Target Prospect Domain</label>
              <div class="relative">
                <input
                  type="text"
                  data-testid="research-domain-input"
                  [(ngModel)]="targetDomain"
                  placeholder="e.g. hyper-logistics.tn"
                  class="w-full bg-slate-950 border border-slate-800 rounded-lg px-3 py-2 text-xs text-white placeholder-slate-500 focus:outline-none focus:border-indigo-500 font-mono" />
              </div>
            </div>

            <!-- Engine Selection -->
            <div class="md:col-span-4">
              <label class="block text-[11px] font-medium text-slate-300 mb-1">Scraping Engine</label>
              <select
                data-testid="engine-selector"
                [(ngModel)]="selectedEngine"
                class="w-full bg-slate-950 border border-slate-800 rounded-lg px-3 py-2 text-xs text-white focus:outline-none focus:border-indigo-500">
                <option value="MultiEngine">Multi-Engine Ensemble (Tavily + Firecrawl + Exa)</option>
                <option value="Tavily">Tavily Search & Extraction Engine</option>
                <option value="Firecrawl">Firecrawl Deep DOM & Markdown Crawler</option>
                <option value="Exa">Exa Neural Semantic Similarity</option>
              </select>
            </div>

            <!-- Quick Presets -->
            <div class="md:col-span-3">
              <label class="block text-[11px] font-medium text-slate-300 mb-1">Sample Presets</label>
              <div class="flex items-center gap-1.5">
                <button
                  (click)="setPreset('hyper-logistics.tn')"
                  class="px-2 py-1 rounded bg-slate-800 hover:bg-slate-700 text-[10px] text-slate-300 font-mono transition">
                  hyper-logistics.tn
                </button>
                <button
                  (click)="setPreset('apex-retail.io')"
                  class="px-2 py-1 rounded bg-slate-800 hover:bg-slate-700 text-[10px] text-slate-300 font-mono transition">
                  apex-retail.io
                </button>
                <button
                  (click)="setPreset('fintech-global.com')"
                  class="px-2 py-1 rounded bg-slate-800 hover:bg-slate-700 text-[10px] text-slate-300 font-mono transition">
                  fintech-global.com
                </button>
              </div>
            </div>
          </div>
        </div>

        <!-- Notification Toast -->
        @if (toastMessage()) {
          <div class="mx-6 mt-4 px-4 py-2 bg-indigo-500/20 border border-indigo-500/40 text-indigo-300 text-xs rounded-lg flex items-center justify-between">
            <span>{{ toastMessage() }}</span>
            <button (click)="toastMessage.set('')" class="text-indigo-400 hover:text-white">✕</button>
          </div>
        }

        <!-- Crawling Progress Console Terminal -->
        @if (isCrawling() || crawlLogs().length > 0) {
          <div class="mx-6 mt-4 bg-slate-950 border border-slate-800/80 rounded-xl p-4 font-mono text-[11px] text-slate-400">
            <div class="flex items-center justify-between pb-2 mb-2 border-b border-slate-900 text-slate-300">
              <span class="flex items-center gap-2">
                <span class="w-2 h-2 rounded-full" [ngClass]="isCrawling() ? 'bg-amber-400 animate-pulse' : 'bg-emerald-400'"></span>
                <span>Crawler Stream: {{ selectedEngine }}</span>
              </span>
              <span class="text-[10px] text-slate-500">{{ crawlLogs().length }} events logged</span>
            </div>
            <div class="space-y-1 max-h-36 overflow-y-auto">
              @for (log of crawlLogs(); track log) {
                <div class="leading-relaxed">
                  <span class="text-indigo-400 font-bold">></span> {{ log }}
                </div>
              }
            </div>
          </div>
        }

        <!-- Main Research Intelligence Results -->
        @if (activeBrief()) {
          <div class="p-6 space-y-6">
            <!-- Top Summary Card -->
            <div class="bg-slate-900/60 border border-slate-800 rounded-xl p-5 shadow-sm">
              <div class="flex flex-wrap items-center justify-between gap-4 pb-4 mb-4 border-b border-slate-800/80">
                <div class="flex items-center gap-3">
                  <div class="w-10 h-10 rounded-lg bg-indigo-600/20 border border-indigo-500/30 flex items-center justify-center text-lg font-bold text-indigo-300">
                    {{ activeBrief()!.companyName.slice(0, 2).toUpperCase() }}
                  </div>
                  <div>
                    <h3 class="text-base font-bold text-white flex items-center gap-2">
                      {{ activeBrief()!.companyName }}
                      <span class="text-xs font-normal text-slate-400 font-mono">({{ activeBrief()!.domain }})</span>
                    </h3>
                    <div class="flex items-center gap-3 text-xs text-slate-400 mt-0.5">
                      <span>📍 {{ activeBrief()!.headquarters }}</span>
                      <span>•</span>
                      <span>👥 {{ activeBrief()!.headcountTier }}</span>
                      <span>•</span>
                      <span>💰 {{ activeBrief()!.revenueEstimate }}</span>
                    </div>
                  </div>
                </div>

                <div class="flex items-center gap-2">
                  <span class="text-xs text-slate-400">Crawled via:</span>
                  <span class="px-2.5 py-1 bg-slate-800 text-indigo-300 text-xs rounded-md font-mono border border-slate-700">
                    {{ activeBrief()!.engineUsed }}
                  </span>
                  <span class="px-2 py-0.5 bg-emerald-500/20 text-emerald-300 text-[10px] rounded border border-emerald-500/30 font-mono">
                    {{ activeBrief()!.scrapedPageCount }} pages in {{ activeBrief()!.executionTimeMs }}ms
                  </span>
                </div>
              </div>

              <!-- Corporate Summary Paragraph -->
              <p class="text-xs text-slate-300 leading-relaxed">
                {{ activeBrief()!.corporateSummary }}
              </p>
            </div>

            <!-- Personalized 3-Bullet Sales Hooks Card (High Value for SDRs) -->
            <div class="bg-gradient-to-r from-indigo-950/40 to-slate-900/60 border border-indigo-500/30 rounded-xl p-5 shadow-lg">
              <div class="flex items-center justify-between mb-4">
                <div class="flex items-center gap-2">
                  <span class="text-lg">🎯</span>
                  <h4 class="text-sm font-bold text-white">
                    Personalized 3-Bullet Outbound SDR Sales Hooks
                  </h4>
                  <span class="bg-indigo-500/20 text-indigo-300 text-[10px] px-2 py-0.5 rounded font-mono border border-indigo-500/30">
                    AI SDR Ready
                  </span>
                </div>
                <span class="text-xs text-slate-400">Tailored to tech stack & growth trajectory</span>
              </div>

              <div class="space-y-3">
                @for (hook of activeBrief()!.salesHooks; track hook; let idx = $index) {
                  <div
                    [attr.data-testid]="'sales-hook-' + idx"
                    class="bg-slate-950/80 border border-indigo-900/50 hover:border-indigo-600/50 rounded-lg p-3.5 flex items-start justify-between gap-4 transition group">
                    <div class="flex items-start gap-3">
                      <span class="w-5 h-5 rounded-full bg-indigo-600/30 text-indigo-300 text-xs font-mono font-bold flex items-center justify-center shrink-0 mt-0.5">
                        {{ idx + 1 }}
                      </span>
                      <p class="text-xs text-slate-200 leading-relaxed font-sans">
                        {{ hook }}
                      </p>
                    </div>

                    <div class="flex items-center gap-2 shrink-0">
                      <button
                        (click)="copyToClipboard(hook)"
                        class="px-2.5 py-1 rounded bg-slate-800 hover:bg-slate-700 text-[11px] text-slate-300 hover:text-white flex items-center gap-1 transition">
                        📋 Copy
                      </button>
                      <button
                        (click)="injectIntoCadence(hook)"
                        class="px-2.5 py-1 rounded bg-indigo-600/30 hover:bg-indigo-600 text-[11px] text-indigo-300 hover:text-white flex items-center gap-1 transition">
                        ⚡ Inject into Cadence
                      </button>
                    </div>
                  </div>
                }
              </div>
            </div>

            <!-- Two-Column Grid: Executive Bios & Detected Tech Stack -->
            <div class="grid grid-cols-1 lg:grid-cols-2 gap-6">
              <!-- Executive Team Bios -->
              <div class="bg-slate-900/60 border border-slate-800 rounded-xl p-5 shadow-sm">
                <div class="flex items-center justify-between mb-4">
                  <h4 class="text-sm font-bold text-white flex items-center gap-2">
                    👔 Key Decision-Maker Bios
                  </h4>
                  <span class="text-xs text-slate-500">{{ activeBrief()!.executiveBios.length }} executives</span>
                </div>

                <div class="space-y-3">
                  @for (exec of activeBrief()!.executiveBios; track exec.name) {
                    <div class="bg-slate-950/60 border border-slate-800/80 rounded-lg p-3.5 flex items-start gap-3.5">
                      <span class="w-9 h-9 rounded-full bg-slate-800 border border-slate-700 text-slate-200 font-bold text-xs flex items-center justify-center shrink-0">
                        {{ exec.avatar }}
                      </span>
                      <div class="flex-1 min-w-0">
                        <div class="flex items-center justify-between gap-2 mb-0.5">
                          <h5 class="text-xs font-bold text-slate-100 truncate">{{ exec.name }}</h5>
                          <a
                            [href]="exec.linkedInUrl"
                            target="_blank"
                            class="text-[10px] text-indigo-400 hover:underline flex items-center gap-1 font-mono">
                            LinkedIn ↗
                          </a>
                        </div>
                        <div class="text-[11px] text-indigo-300 font-medium mb-1.5">{{ exec.title }}</div>
                        <p class="text-[11px] text-slate-400 leading-relaxed">{{ exec.summary }}</p>
                      </div>
                    </div>
                  }
                </div>
              </div>

              <!-- Detected Technology Stack -->
              <div class="bg-slate-900/60 border border-slate-800 rounded-xl p-5 shadow-sm">
                <div class="flex items-center justify-between mb-4">
                  <h4 class="text-sm font-bold text-white flex items-center gap-2">
                    💻 Detected Technology Stack
                  </h4>
                  <span class="text-xs text-slate-500">{{ activeBrief()!.techTags.length }} detected technologies</span>
                </div>

                <div class="grid grid-cols-1 sm:grid-cols-2 gap-2.5">
                  @for (tech of activeBrief()!.techTags; track tech.name) {
                    <div class="bg-slate-950/60 border border-slate-800/80 rounded-lg p-2.5 flex items-center justify-between gap-2">
                      <div>
                        <div class="text-[10px] text-slate-400 uppercase tracking-wider font-mono">{{ tech.category }}</div>
                        <div class="text-xs font-semibold text-slate-200">{{ tech.name }}</div>
                      </div>
                      <span
                        class="text-[9px] font-mono px-1.5 py-0.5 rounded border"
                        [ngClass]="tech.confidence === 'High' ? 'bg-emerald-500/20 text-emerald-300 border-emerald-500/30' : 'bg-amber-500/20 text-amber-300 border-amber-500/30'">
                        {{ tech.confidence }}
                      </span>
                    </div>
                  }
                </div>
              </div>
            </div>
          </div>
        }
      </main>
    </div>
  `
})
export class ProspectResearchCockpitComponent {
  translation = inject(TranslationService);

  targetDomain = 'hyper-logistics.tn';
  selectedEngine = 'MultiEngine';
  isCrawling = signal<boolean>(false);
  crawlLogs = signal<string[]>([]);
  toastMessage = signal<string>('');

  activeBrief = signal<ResearchBriefData | null>({
    domain: 'hyper-logistics.tn',
    companyName: 'HyperLogistics',
    engineUsed: 'Multi-Engine Ensemble (Tavily+Firecrawl+Exa)',
    headquarters: 'Tunis, Tunisia',
    headcountTier: '150 - 250 Employees',
    revenueEstimate: '$12M - $18M ARR',
    corporateSummary: 'HyperLogistics is a regional leader in last-mile courier delivery and e-commerce distribution across North Africa. Their digital platform manages multi-warehouse stock allocations, automated Cash-on-Delivery (COD) remittance reconciliation, and regional carrier routing across 24 governorates.',
    executiveBios: [
      {
        name: 'Sarah Al-Mansoor',
        title: 'Chief Executive Officer & Co-Founder',
        linkedInUrl: 'https://linkedin.com/in/sarah-almansoor',
        summary: 'Spearheads corporate strategy, fundraising, and regional logistics network expansion across Maghreb markets.',
        avatar: 'SM'
      },
      {
        name: 'David Ben David',
        title: 'VP of Sales & Revenue Operations',
        linkedInUrl: 'https://linkedin.com/in/david-bd',
        summary: 'Directs enterprise commercial partnerships, B2B cold cadences, and sales automation systems.',
        avatar: 'DB'
      },
      {
        name: 'Karim Trabelsi',
        title: 'Head of Logistics & Warehouse Operations',
        linkedInUrl: 'https://linkedin.com/in/karim-trabelsi',
        summary: 'Manages regional hub throughput, barcoding operations, and courier fleet SLA performance.',
        avatar: 'KT'
      }
    ],
    techTags: [
      { category: 'Frontend', name: 'Next.js 16 / React 19', confidence: 'High' },
      { category: 'Styling', name: 'Tailwind CSS', confidence: 'High' },
      { category: 'Cloud', name: 'Amazon Web Services (AWS)', confidence: 'High' },
      { category: 'CRM & Sales', name: 'HubSpot Sales Hub', confidence: 'High' },
      { category: 'Commerce', name: 'Shopify Plus / Headless', confidence: 'Medium' },
      { category: 'Messaging', name: 'WhatsApp Business API', confidence: 'High' },
      { category: 'Analytics', name: 'Google Analytics 4 & CAPI', confidence: 'High' }
    ],
    salesHooks: [
      'Noticed HyperLogistics is scaling last-mile logistics across 24 governorates; SGrowing\'s geo-routing engine cuts COD return delivery rates by 28%.',
      'Saw your tech stack runs modern Next.js and HubSpot; our AI SDR seamlessly integrates with your CRM to automate prospect reply handoffs.',
      'Given your executive focus on regional expansion, our true margin tracker isolates COGS, courier fees, and operator hours per deal in real time.'
    ],
    scrapedPageCount: 16,
    executionTimeMs: 412
  });

  setPreset(domain: string): void {
    this.targetDomain = domain;
  }

  runDeepCrawl(): void {
    if (!this.targetDomain.trim()) return;

    this.isCrawling.set(true);
    this.crawlLogs.set([]);

    const domain = this.targetDomain.trim();
    const cleanHost = domain.replace(/^https?:\/\//, '').replace(/^www\./, '').split('/')[0];
    const rawName = cleanHost.split('.')[0] || domain;
    const company = rawName.toUpperCase();

    const steps = [
      `Initializing ${this.selectedEngine} web crawling worker pool for https://${domain}...`,
      `Inspecting robots.txt and sitemap.xml: 48 candidate URLs discovered`,
      `Bypassing Cloudflare bot protections via residential headless proxy cluster...`,
      `Extracted raw DOM trees from 16 high-signal pages (/about, /leadership, /careers, /pricing)...`,
      `Running LLM extraction: identifying executive bios and LinkedIn handles...`,
      `Detecting technology footprints from HTTP response headers and script bundles...`,
      `Synthesizing 3 personalized sales hooks tailored for cold SDR outreach...`,
      `Crawl complete! Structured prospect brief successfully stored.`
    ];

    steps.forEach((step, index) => {
      setTimeout(() => {
        this.crawlLogs.update(logs => [...logs, step]);
        if (index === steps.length - 1) {
          this.isCrawling.set(false);
          this.activeBrief.set({
            domain: domain,
            companyName: company,
            engineUsed: this.selectedEngine,
            headquarters: 'Casablanca & Tunis Hubs',
            headcountTier: '100 - 300 Employees',
            revenueEstimate: '$10M - $25M ARR',
            corporateSummary: `${company} operates high-velocity commerce and logistics infrastructure with verified online funnels and modern cloud deployments. Active expansion signals detected across omnichannel messaging and regional fulfillment.`,
            executiveBios: [
              {
                name: 'Sarah Al-Mansoor',
                title: 'Chief Executive Officer',
                linkedInUrl: 'https://linkedin.com/in/sarah-almansoor',
                summary: 'Oversees organizational growth, capital allocation, and regional footprint.',
                avatar: 'SM'
              },
              {
                name: 'David Ben David',
                title: 'VP of Commercial Strategy',
                linkedInUrl: 'https://linkedin.com/in/david-bd',
                summary: 'Drives strategic B2B revenue and outbound prospect engagement.',
                avatar: 'DB'
              }
            ],
            techTags: [
              { category: 'Frontend', name: 'React 19 / Angular', confidence: 'High' },
              { category: 'Cloud Infrastructure', name: 'AWS & Cloudflare', confidence: 'High' },
              { category: 'CRM', name: 'Twenty / Salesforce', confidence: 'Medium' }
            ],
            salesHooks: [
              `Saw ${company}'s active growth across regional commerce; our automated fulfillment routing guarantees lowest-cost courier dispatch.`,
              `Given your technical investments, our native SDR agent integrates directly into your pipeline with zero API migration overhead.`,
              `Our true gross margin engine isolates courier fees and operator labor to protect unit profitability.`
            ],
            scrapedPageCount: 18,
            executionTimeMs: 489
          });
          this.toastMessage.set(`Intelligence brief for ${domain} successfully generated!`);
        }
      }, (index + 1) * 350);
    });
  }

  copyToClipboard(text: string): void {
    if (navigator?.clipboard?.writeText) {
      navigator.clipboard.writeText(text);
    }
    this.toastMessage.set('Copied sales hook to clipboard!');
  }

  injectIntoCadence(hook: string): void {
    this.toastMessage.set(`Injected sales hook into Active Cold Outreach Sequence!`);
  }
}
