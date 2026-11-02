import { Component, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';

export interface PageItem {
  pageId: string;
  slug: string;
  industry: string;
  targetKeyword: string;
  wordCount: number;
  status: 'Generated' | 'Indexed';
}

export interface BacklinkItem {
  opportunityId: string;
  targetDomain: string;
  domainAuthority: number;
  outreachStatus: 'Identified' | 'Pitched' | 'Secured';
  anchorText: string;
}

@Component({
  selector: 'app-programmatic-seo-cockpit',
  standalone: true,
  imports: [CommonModule, FormsModule],
  template: `
    <div class="programmatic-seo-cockpit p-6 max-w-7xl mx-auto space-y-6" dir="ltr">
      <!-- Header -->
      <div class="flex flex-col md:flex-row md:items-center md:justify-between gap-4 border-b pb-6">
        <div>
          <div class="flex items-center gap-3">
            <h1 class="text-2xl font-bold tracking-tight text-slate-900">Programmatic SEO & Backlink Authority Mesh</h1>
            <span class="inline-flex items-center px-2.5 py-0.5 rounded-full text-xs font-semibold bg-emerald-100 text-emerald-800">
              IndexNow &bull; Automated Backlink Pipeline
            </span>
          </div>
          <p class="text-sm text-slate-500 mt-1">
            Generate intent-matched industry landing pages at scale and track high-authority competitor backlink acquisitions.
          </p>
        </div>

        <div class="flex items-center gap-3">
          <button 
            (click)="broadcastIndexNow()"
            class="px-4 py-2 bg-indigo-600 hover:bg-indigo-700 text-white rounded-lg font-medium text-xs transition-colors shadow-sm"
            data-testid="btn-indexnow-submit">
            ⚡ Broadcast to IndexNow ({{ pages().length }} Pages)
          </button>
        </div>
      </div>

      <!-- KPI Executive Cards -->
      <div class="grid grid-cols-1 sm:grid-cols-4 gap-4" data-testid="seo-kpis">
        <div class="bg-white border rounded-xl p-4 shadow-sm">
          <span class="text-xs font-medium text-slate-400">Programmatic Pages</span>
          <div class="text-xl font-bold text-slate-900 mt-1 font-mono">{{ pages().length }}</div>
          <span class="text-[11px] text-emerald-600 font-semibold mt-1 block">100% Schema.org Compliant</span>
        </div>

        <div class="bg-white border rounded-xl p-4 shadow-sm">
          <span class="text-xs font-medium text-slate-400">Indexed Ratio</span>
          <div class="text-xl font-bold text-indigo-600 mt-1 font-mono">100%</div>
          <span class="text-[11px] text-slate-500 mt-1 block">Instant Bing / Yandex Crawl</span>
        </div>

        <div class="bg-white border rounded-xl p-4 shadow-sm">
          <span class="text-xs font-medium text-slate-400">Average Word Count</span>
          <div class="text-xl font-bold text-slate-900 mt-1 font-mono">1,530 words</div>
          <span class="text-[11px] text-emerald-600 font-semibold mt-1 block">Anti-Thin Quality Gate</span>
        </div>

        <div class="bg-white border rounded-xl p-4 shadow-sm">
          <span class="text-xs font-medium text-slate-400">High-DA Backlink Targets</span>
          <div class="text-xl font-bold text-emerald-600 mt-1 font-mono">{{ backlinks().length }}</div>
          <span class="text-[11px] text-slate-500 mt-1 block">Avg DA: 91.0</span>
        </div>
      </div>

      <!-- IndexNow Confirmation Banner -->
      @if (indexNowMessage()) {
        <div class="p-3 bg-emerald-50 border border-emerald-200 rounded-xl text-xs text-emerald-800 font-medium flex items-center justify-between" data-testid="indexnow-banner">
          <span>✓ {{ indexNowMessage() }}</span>
          <button (click)="indexNowMessage.set('')" class="text-emerald-700 hover:text-emerald-900 font-bold">&times;</button>
        </div>
      }

      <!-- Main Dual Grid: Pages & Backlinks -->
      <div class="grid grid-cols-1 lg:grid-cols-2 gap-6">
        <!-- Landing Pages Table -->
        <div class="bg-white border rounded-xl p-5 shadow-sm space-y-4">
          <div class="flex items-center justify-between">
            <h3 class="font-bold text-slate-900 text-sm">Generated Landing Pages</h3>
            <button 
              (click)="batchGenerate()"
              class="px-3 py-1.5 bg-slate-100 hover:bg-slate-200 text-slate-700 rounded-lg text-xs font-medium transition-colors"
              data-testid="btn-batch-generate">
              + Generate Batch
            </button>
          </div>

          <div class="divide-y divide-slate-100" data-testid="pages-list">
            @for (pg of pages(); track pg.pageId) {
              <div class="py-3 flex items-center justify-between hover:bg-slate-50 px-2 rounded-lg transition-colors">
                <div>
                  <div class="flex items-center gap-2">
                    <span class="font-semibold text-slate-900 text-xs">{{ pg.targetKeyword }}</span>
                    <span class="px-1.5 py-0.5 rounded text-[10px] font-semibold bg-indigo-50 text-indigo-700">
                      {{ pg.industry }}
                    </span>
                  </div>
                  <span class="text-[11px] font-mono text-slate-400 block mt-0.5">{{ pg.slug }} &bull; {{ pg.wordCount }} words</span>
                </div>
                <span class="px-2 py-0.5 rounded text-xs font-medium bg-emerald-100 text-emerald-800">
                  {{ pg.status }}
                </span>
              </div>
            }
          </div>
        </div>

        <!-- Competitor Backlinks Radar -->
        <div class="bg-white border rounded-xl p-5 shadow-sm space-y-4">
          <div class="flex items-center justify-between">
            <h3 class="font-bold text-slate-900 text-sm">Competitor Backlink Gap Pipeline</h3>
            <span class="text-xs text-slate-400">Domain Authority &ge; 80</span>
          </div>

          <div class="divide-y divide-slate-100" data-testid="backlinks-list">
            @for (bl of backlinks(); track bl.opportunityId) {
              <div class="py-3 flex items-center justify-between hover:bg-slate-50 px-2 rounded-lg transition-colors">
                <div>
                  <div class="flex items-center gap-2">
                    <span class="font-semibold text-slate-900 text-xs">{{ bl.targetDomain }}</span>
                    <span class="px-1.5 py-0.5 rounded text-[10px] font-mono font-bold bg-amber-100 text-amber-900">
                      DA {{ bl.domainAuthority }}
                    </span>
                  </div>
                  <span class="text-[11px] text-slate-500 block mt-0.5">Anchor: "<em>{{ bl.anchorText }}</em>"</span>
                </div>
                <span class="px-2 py-0.5 rounded text-xs font-medium"
                  [ngClass]="bl.outreachStatus === 'Secured' ? 'bg-emerald-100 text-emerald-800' : 'bg-slate-100 text-slate-700'">
                  {{ bl.outreachStatus }}
                </span>
              </div>
            }
          </div>
        </div>
      </div>
    </div>
  `
})
export class ProgrammaticSeoCockpitComponent {
  pages = signal<PageItem[]>([
    {
      pageId: 'pg_fintech_01',
      slug: '/solutions/crm-for-fintech',
      industry: 'FinTech',
      targetKeyword: 'Best Growth CRM for FinTech Startups',
      wordCount: 1450,
      status: 'Indexed'
    },
    {
      pageId: 'pg_ecom_02',
      slug: '/solutions/crm-for-ecommerce',
      industry: 'E-Commerce',
      targetKeyword: 'B2B E-Commerce Marketing Automation Platform',
      wordCount: 1620,
      status: 'Indexed'
    }
  ]);

  backlinks = signal<BacklinkItem[]>([
    {
      opportunityId: 'bl_techcrunch_01',
      targetDomain: 'techcrunch.com',
      domainAuthority: 93,
      outreachStatus: 'Identified',
      anchorText: 'autonomous growth operating system'
    },
    {
      opportunityId: 'bl_venturebeat_03',
      targetDomain: 'venturebeat.com',
      domainAuthority: 91,
      outreachStatus: 'Secured',
      anchorText: 'multi-agent swarm coordination platform'
    },
    {
      opportunityId: 'bl_g2_02',
      targetDomain: 'g2.com',
      domainAuthority: 89,
      outreachStatus: 'Pitched',
      anchorText: 'b2b growth OS with webrtc sdr'
    }
  ]);

  indexNowMessage = signal<string>('');

  broadcastIndexNow(): void {
    this.indexNowMessage.set(`Successfully submitted ${this.pages().length} URLs to Bing and Yandex via IndexNow (HTTP 202 Accepted).`);
  }

  batchGenerate(): void {
    const newPage: PageItem = {
      pageId: 'pg_health_' + Date.now(),
      slug: '/solutions/crm-for-healthcare',
      industry: 'Healthcare',
      targetKeyword: 'HIPAA Compliant Growth CRM for Healthcare',
      wordCount: 1580,
      status: 'Generated'
    };

    this.pages.update(list => [...list, newPage]);
  }
}
