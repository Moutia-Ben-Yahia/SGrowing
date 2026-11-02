import { Component, signal, computed } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';

export interface ContentBlockUi {
  id: string;
  type: string;
  title: string;
  preview: string;
}

export interface CmsArticleUi {
  id: string;
  slug: string;
  title: string;
  description: string;
  locale: string;
  voiceScore: number;
  blocks: ContentBlockUi[];
  status: string;
}

@Component({
  selector: 'app-headless-cms-cockpit',
  standalone: true,
  imports: [CommonModule, FormsModule],
  template: `
    <div class="headless-cms-cockpit p-6 max-w-7xl mx-auto space-y-6" dir="ltr">
      <!-- Header -->
      <div class="flex flex-col md:flex-row md:items-center md:justify-between gap-4 border-b pb-6">
        <div>
          <div class="flex items-center gap-3">
            <h1 class="text-2xl font-bold tracking-tight text-slate-900">AI Multilingual Headless CMS & Content Factory</h1>
            <span class="inline-flex items-center px-2.5 py-0.5 rounded-full text-xs font-semibold bg-emerald-100 text-emerald-800">
              Edge SSG Webhooks Active
            </span>
          </div>
          <p class="text-sm text-slate-500 mt-1">
            Build modular dynamic pages, enforce enterprise brand voice compliance, and publish to edge CDNs with zero latency.
          </p>
        </div>

        <div class="flex items-center gap-3">
          <button 
            (click)="showCreateModal.set(true)"
            class="px-4 py-2 bg-emerald-600 hover:bg-emerald-700 text-white rounded-lg font-medium text-xs transition-colors shadow-sm"
            data-testid="btn-create-article">
            + New Modular Article
          </button>
        </div>
      </div>

      <!-- Live Brand Voice & Performance KPIs -->
      <div class="grid grid-cols-1 sm:grid-cols-4 gap-4" data-testid="cms-kpis">
        <div class="bg-white border rounded-xl p-4 shadow-sm">
          <div class="text-xs text-slate-500 font-medium">Published Articles</div>
          <div class="text-2xl font-bold text-slate-900 mt-1 font-mono" data-testid="kpi-articles-count">{{ articles().length }}</div>
          <div class="text-[11px] text-emerald-600 mt-1 font-semibold">&uarr; 100% Edge Synced</div>
        </div>

        <div class="bg-white border rounded-xl p-4 shadow-sm">
          <div class="text-xs text-slate-500 font-medium">Average Brand Voice Score</div>
          <div class="text-2xl font-bold text-slate-900 mt-1 font-mono" data-testid="kpi-voice-score">{{ averageVoiceScore() }}%</div>
          <div class="text-[11px] text-slate-400 mt-1">Flesch-Kincaid Grade 11</div>
        </div>

        <div class="bg-white border rounded-xl p-4 shadow-sm">
          <div class="text-xs text-slate-500 font-medium">Supported Locales</div>
          <div class="text-2xl font-bold text-slate-900 mt-1 font-mono">14 Locales</div>
          <div class="text-[11px] text-indigo-600 mt-1">i18n Automated Translation</div>
        </div>

        <div class="bg-white border rounded-xl p-4 shadow-sm">
          <div class="text-xs text-slate-500 font-medium">Edge SSG Deployment</div>
          <div class="text-2xl font-bold text-emerald-600 mt-1 font-mono">&lt; 180ms</div>
          <div class="text-[11px] text-slate-400 mt-1">Cloudflare & Next.js Revalidation</div>
        </div>
      </div>

      <!-- Main Content Workbench -->
      <div class="grid grid-cols-1 lg:grid-cols-3 gap-6">
        <!-- Articles Feed -->
        <div class="lg:col-span-2 bg-white border rounded-xl p-5 shadow-sm space-y-4">
          <div class="flex items-center justify-between">
            <h3 class="font-bold text-slate-900 text-sm">Dynamic Content Catalog</h3>
            <span class="text-xs text-slate-400 font-mono">{{ articles().length }} entries</span>
          </div>

          <div class="space-y-3" data-testid="articles-list">
            @for (article of articles(); track article.id) {
              <div class="p-4 border rounded-lg hover:border-slate-300 transition-colors space-y-2">
                <div class="flex items-center justify-between">
                  <div class="flex items-center gap-2">
                    <span class="font-bold text-slate-800 text-sm">{{ article.title }}</span>
                    <span class="px-2 py-0.5 rounded text-[10px] font-mono font-semibold bg-slate-100 text-slate-600">
                      /{{ article.slug }}
                    </span>
                  </div>
                  <span class="px-2 py-0.5 rounded-full text-[11px] font-semibold"
                    [ngClass]="article.voiceScore >= 90 ? 'bg-emerald-100 text-emerald-800' : 'bg-amber-100 text-amber-800'">
                    Voice: {{ article.voiceScore }}%
                  </span>
                </div>

                <p class="text-xs text-slate-500">{{ article.description }}</p>

                <!-- Blocks summary -->
                <div class="flex items-center gap-2 pt-1 border-t text-[11px] text-slate-400">
                  <span>{{ article.blocks.length }} Modular Blocks</span>
                  <span>&bull;</span>
                  <span>Locale: {{ article.locale }}</span>
                  <span>&bull;</span>
                  <button 
                    (click)="triggerEdgeRevalidation(article.id)"
                    class="text-emerald-600 hover:underline font-semibold"
                    [attr.data-testid]="'btn-revalidate-' + article.id">
                    Deploy Edge Webhook
                  </button>
                </div>
              </div>
            }
          </div>
        </div>

        <!-- Brand Voice Compliance Inspector -->
        <div class="bg-white border rounded-xl p-5 shadow-sm space-y-4">
          <h3 class="font-bold text-slate-900 text-sm">Brand Voice AI Policy Inspector</h3>
          <p class="text-xs text-slate-500">
            Real-time heuristic evaluation auditing copy against non-compliant marketing claims and buzzword rules.
          </p>

          <div class="space-y-3">
            <div>
              <label class="block text-xs font-semibold text-slate-700 mb-1">Test Copy Segment</label>
              <textarea 
                [(ngModel)]="sampleTextToAudit" 
                rows="4" 
                class="w-full px-3 py-2 border rounded-lg text-xs focus:ring-2 focus:ring-emerald-500 focus:outline-none"
                data-testid="input-audit-copy"></textarea>
            </div>

            <button 
              (click)="auditText()"
              class="w-full py-2 bg-slate-900 hover:bg-slate-800 text-white rounded-lg text-xs font-medium shadow-sm"
              data-testid="btn-run-voice-audit">
              Run AI Brand Voice Audit
            </button>

            @if (auditResult()) {
              <div class="p-3 bg-slate-50 border rounded-lg space-y-2" data-testid="audit-result-card">
                <div class="flex justify-between items-center text-xs">
                  <span class="font-semibold text-slate-700">Compliance Score</span>
                  <span class="font-bold text-emerald-600 font-mono">{{ auditResult()?.score }}%</span>
                </div>
                <div class="text-[11px] text-slate-500">
                  <div>Reading Level: <span class="font-semibold">{{ auditResult()?.readingLevel }}</span></div>
                  <div>Tone: <span class="font-semibold">{{ auditResult()?.tone }}</span></div>
                </div>
              </div>
            }
          </div>
        </div>
      </div>

      <!-- Create Modal -->
      @if (showCreateModal()) {
        <div class="fixed inset-0 bg-slate-900/40 backdrop-blur-sm z-50 flex items-center justify-center p-4">
          <div class="bg-white rounded-2xl max-w-lg w-full p-6 space-y-4 shadow-xl border">
            <div class="flex items-center justify-between border-b pb-3">
              <h3 class="font-bold text-slate-900 text-base">New Modular Content Article</h3>
              <button (click)="showCreateModal.set(false)" class="text-slate-400 hover:text-slate-600">&times;</button>
            </div>

            <div class="space-y-3">
              <div>
                <label class="block text-xs font-semibold text-slate-700 mb-1">Article Title</label>
                <input 
                  type="text" 
                  [(ngModel)]="newTitle" 
                  class="w-full px-3 py-2 border rounded-lg text-xs focus:ring-2 focus:ring-emerald-500 focus:outline-none"
                  data-testid="input-new-title" />
              </div>

              <div>
                <label class="block text-xs font-semibold text-slate-700 mb-1">URL Slug</label>
                <input 
                  type="text" 
                  [(ngModel)]="newSlug" 
                  class="w-full px-3 py-2 border rounded-lg text-xs font-mono focus:ring-2 focus:ring-emerald-500 focus:outline-none"
                  data-testid="input-new-slug" />
              </div>

              <div>
                <label class="block text-xs font-semibold text-slate-700 mb-1">Description</label>
                <textarea 
                  [(ngModel)]="newDescription" 
                  rows="2" 
                  class="w-full px-3 py-2 border rounded-lg text-xs focus:ring-2 focus:ring-emerald-500 focus:outline-none"
                  data-testid="input-new-description"></textarea>
              </div>
            </div>

            <div class="flex justify-end gap-2 pt-3 border-t">
              <button 
                (click)="showCreateModal.set(false)"
                class="px-3 py-1.5 border rounded-lg text-xs font-medium text-slate-600 hover:bg-slate-50">
                Cancel
              </button>
              <button 
                (click)="createArticle()"
                class="px-4 py-1.5 bg-emerald-600 hover:bg-emerald-700 text-white rounded-lg text-xs font-medium shadow-sm"
                data-testid="btn-submit-create">
                Publish Modular Article
              </button>
            </div>
          </div>
        </div>
      }
    </div>
  `
})
export class HeadlessCmsCockpitComponent {
  showCreateModal = signal(false);

  articles = signal<CmsArticleUi[]>([
    {
      id: 'art_1',
      slug: 'autonomous-growth-operating-system',
      title: 'Autonomous Growth Operating System Guide',
      description: 'Architectural overview of multi-agent AI revenue orchestration and real-time voice SDR fleets.',
      locale: 'en-US',
      voiceScore: 95.0,
      status: 'Published',
      blocks: [
        { id: 'b1', type: 'Hero', title: 'Scale Revenue Autonomously', preview: 'Deploy hyper-personalized outreach...' },
        { id: 'b2', type: 'FeatureMatrix', title: 'Core Architectural Pillars', preview: 'CDP, Attribution, Air-Gapped Licensing' }
      ]
    },
    {
      id: 'art_2',
      slug: 'cross-border-fx-tax-compliance',
      title: 'Global Multi-Currency Billing & EU VAT OSS',
      description: 'Managing economic nexus thresholds and currency hedging across international B2B contracts.',
      locale: 'en-US',
      voiceScore: 92.5,
      status: 'Published',
      blocks: [
        { id: 'b3', type: 'Hero', title: 'Frictionless Global Settlements', preview: 'Automated reverse-charge rules...' }
      ]
    }
  ]);

  averageVoiceScore = computed(() => {
    const list = this.articles();
    if (!list.length) return 0;
    const total = list.reduce((sum, a) => sum + a.voiceScore, 0);
    return Math.round((total / list.length) * 10) / 10;
  });

  sampleTextToAudit = 'SGrowing autonomous AI growth agents coordinate outbound campaigns with verifiable multi-touch revenue attribution.';
  auditResult = signal<{ score: number; readingLevel: string; tone: string } | null>({
    score: 96.0,
    readingLevel: 'Grade 11 (Executive)',
    tone: 'Enterprise Precision'
  });

  newTitle = 'Predictive ABM Intent Orchestration';
  newSlug = 'predictive-abm-intent';
  newDescription = 'Dynamic sales territory capacity balancing and real-time buying signals.';

  auditText() {
    const text = this.sampleTextToAudit;
    const score = text.includes('guarantee') ? 60.0 : 94.0;
    this.auditResult.set({
      score,
      readingLevel: 'Grade 11 (Executive Brief)',
      tone: 'Enterprise Precision'
    });
  }

  createArticle() {
    const created: CmsArticleUi = {
      id: 'art_' + Date.now(),
      slug: this.newSlug,
      title: this.newTitle,
      description: this.newDescription,
      locale: 'en-US',
      voiceScore: 94.5,
      status: 'Published',
      blocks: [
        { id: 'b_' + Date.now(), type: 'Hero', title: this.newTitle, preview: this.newDescription }
      ]
    };

    this.articles.update(list => [created, ...list]);
    this.showCreateModal.set(false);
  }

  triggerEdgeRevalidation(articleId: string) {
    // Optimistic feedback indicator
    const target = this.articles().find(a => a.id === articleId);
    if (target) {
      target.status = 'Edge Synced';
    }
  }
}
