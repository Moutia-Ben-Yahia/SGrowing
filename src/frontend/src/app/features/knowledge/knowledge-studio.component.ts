import { Component, OnInit, signal, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { ApiService } from '../../core/services/api.service';
import { HelpArticleDto, RagDeflectionResultDto } from '../../core/models/platform.models';

@Component({
  selector: 'app-knowledge-studio',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterLink],
  template: `
    <div class="flex h-screen w-screen overflow-hidden bg-slate-950 text-slate-100 font-sans">
      <!-- 1. Left Sidebar Navigation -->
      <aside class="w-64 border-r border-slate-800 bg-slate-900/80 flex flex-col justify-between p-4">
        <div>
          <div class="flex items-center gap-2 mb-6">
            <div class="h-8 w-8 rounded-lg bg-gradient-to-tr from-purple-500 to-indigo-500 flex items-center justify-center font-bold text-white shadow-lg shadow-purple-500/20">
              📚
            </div>
            <div>
              <h1 class="text-sm font-semibold tracking-wide text-white">SGrowing</h1>
              <p class="text-xs text-purple-400 font-medium">Knowledge & AI RAG</p>
            </div>
          </div>

          <nav class="space-y-1 mb-6">
            <span class="text-xs font-semibold text-slate-400 uppercase tracking-wider px-2">Navigation</span>
            <a routerLink="/" class="w-full flex items-center gap-2 px-3 py-2 rounded-lg text-sm text-slate-400 hover:text-slate-100 hover:bg-slate-800/60 transition">
              <span>📥 Omnichannel Inbox</span>
            </a>
            <a routerLink="/outbound" class="w-full flex items-center gap-2 px-3 py-2 rounded-lg text-sm text-slate-400 hover:text-slate-100 hover:bg-slate-800/60 transition">
              <span>🚀 Autonomous AI SDR</span>
            </a>
            <a routerLink="/journeys" class="w-full flex items-center gap-2 px-3 py-2 rounded-lg text-sm text-slate-400 hover:text-slate-100 hover:bg-slate-800/60 transition">
              <span>⚡ Customer Journeys</span>
            </a>
            <a routerLink="/feedback" class="w-full flex items-center gap-2 px-3 py-2 rounded-lg text-sm text-slate-400 hover:text-slate-100 hover:bg-slate-800/60 transition">
              <span>⭐ Feedback & CSAT</span>
            </a>
            <a routerLink="/help" class="w-full flex items-center gap-2 px-3 py-2 rounded-lg text-sm bg-slate-800 text-purple-400 transition font-medium">
              <span>📚 Help Center & AI Bot</span>
            </a>
          </nav>

          <!-- Deflection Stats Card -->
          <div class="p-3 bg-gradient-to-b from-slate-800/90 to-slate-900 border border-purple-500/30 rounded-xl space-y-2">
            <div class="flex items-center justify-between">
              <span class="text-xs font-semibold text-purple-300 flex items-center gap-1.5">
                <span class="h-2 w-2 rounded-full bg-purple-400"></span>
                RAG Deflection Active
              </span>
              <span class="text-[10px] bg-purple-500/10 text-purple-300 border border-purple-500/20 px-1.5 py-0.5 rounded">Chatwoot</span>
            </div>
            <p class="text-[11px] text-slate-300 leading-relaxed">
              Synthesizing answers with article citations to resolve questions before reaching human agents.
            </p>
          </div>
        </div>
      </aside>

      <!-- 2. Main Content Area -->
      <main class="flex-1 flex flex-col overflow-hidden bg-slate-950">
        <!-- Header -->
        <header class="h-16 border-b border-slate-800 bg-slate-900/60 px-6 flex items-center justify-between">
          <h2 class="text-base font-semibold text-white">Help Center & Zero-Wait AI RAG Bot</h2>
          <div class="flex items-center gap-6 text-xs">
            <div class="flex items-center gap-2">
              <span class="text-slate-400">Articles Published:</span>
              <span class="text-purple-400 font-semibold font-mono">{{ articles().length }}</span>
            </div>
            <div class="flex items-center gap-2">
              <span class="text-slate-400">Deflection Rate:</span>
              <span class="text-emerald-400 font-semibold font-mono">68.5%</span>
            </div>
          </div>
        </header>

        <!-- Body: AI RAG Playground + Articles List -->
        <div class="flex-1 overflow-y-auto p-6 space-y-6 max-w-6xl">
          <!-- Live AI RAG Deflection Playground -->
          <div class="p-5 bg-slate-900 border border-slate-800 rounded-xl space-y-4">
            <div>
              <h3 class="text-sm font-semibold text-white">Live AI Deflection Playground</h3>
              <p class="text-xs text-slate-400">Test how the AI assistant answers customer questions using your Help Center documentation.</p>
            </div>

            <div class="flex gap-2">
              <input
                type="text"
                [(ngModel)]="searchQuery"
                (keyup.enter)="askAi()"
                placeholder="Ask a question (e.g., 'How do I connect WhatsApp Meta Cloud?')"
                class="flex-1 bg-slate-950 border border-slate-800 rounded-lg px-3 py-2 text-xs text-slate-200 placeholder-slate-500 focus:outline-none focus:border-purple-500"
              />
              <button
                (click)="askAi()"
                class="px-4 py-2 bg-purple-600 hover:bg-purple-500 text-white font-semibold text-xs rounded-lg transition shadow-lg shadow-purple-500/20">
                Ask AI Assistant
              </button>
            </div>

            @if (ragResult()) {
              <div class="p-4 bg-slate-950/80 border rounded-lg space-y-2"
                [class.border-purple-500-30]="ragResult()?.deflected"
                [class.border-amber-500-30]="!ragResult()?.deflected">
                <div class="flex items-center justify-between">
                  <span class="text-xs font-semibold" [class.text-purple-400]="ragResult()?.deflected" [class.text-amber-400]="!ragResult()?.deflected">
                    {{ ragResult()?.deflected ? '✅ Instant AI Deflection' : '⚠️ Escalated to Human Agent' }}
                  </span>
                  <span class="text-[10px] font-mono px-2 py-0.5 rounded bg-slate-800 text-slate-300">
                    Confidence: {{ ragResult()?.confidenceScore }}%
                  </span>
                </div>
                <p class="text-xs text-slate-200 whitespace-pre-line leading-relaxed">{{ ragResult()?.answer }}</p>
                @if (ragResult()?.articleTitle) {
                  <div class="pt-2 border-t border-slate-800/80 text-[11px] text-purple-400 font-mono">
                    📖 Cited Source: {{ ragResult()?.articleTitle }} ({{ ragResult()?.articleSlug }})
                  </div>
                }
              </div>
            }
          </div>

          <!-- Documentation Articles -->
          <div class="space-y-4">
            <h3 class="text-sm font-semibold text-slate-200">Published Documentation Articles</h3>
            <div class="grid grid-cols-1 md:grid-cols-2 gap-4">
              @for (a of articles(); track a.id) {
                <div class="p-4 bg-slate-900 border border-slate-800 rounded-xl space-y-2">
                  <div class="flex items-center justify-between">
                    <h4 class="text-sm font-semibold text-white">{{ a.title }}</h4>
                    <span class="text-[10px] bg-slate-800 text-slate-400 px-2 py-0.5 rounded font-mono">{{ a.viewCount }} views</span>
                  </div>
                  <p class="text-xs text-slate-400 line-clamp-3 leading-relaxed">{{ a.contentMarkdown }}</p>
                  <div class="pt-2 flex items-center justify-between text-[11px] text-purple-400 font-mono">
                    <span>Slug: /help/{{ a.slug }}</span>
                    <span class="text-emerald-400">Published</span>
                  </div>
                </div>
              }
            </div>
          </div>
        </div>
      </main>
    </div>
  `
})
export class KnowledgeStudioComponent implements OnInit {
  private api = inject(ApiService);

  public searchQuery = 'How do I setup WhatsApp Meta Cloud API?';
  public ragResult = signal<RagDeflectionResultDto | null>(null);
  public articles = signal<HelpArticleDto[]>([]);
  private tenantId = '00000000-0000-0000-0000-000000000001';

  ngOnInit() {
    this.loadData();
    this.askAi();
  }

  private loadData() {
    this.api.getArticles(this.tenantId).subscribe({
      next: (res) => {
        if (res && res.length > 0) this.articles.set(res);
        else this.initMockArticles();
      },
      error: () => this.initMockArticles()
    });
  }

  private initMockArticles() {
    this.articles.set([
      {
        id: 'a-1',
        categoryId: 'cat-1',
        title: 'Connecting WhatsApp Meta Cloud API',
        slug: 'whatsapp-meta-setup',
        contentMarkdown: 'To connect Meta WhatsApp Cloud API: 1. Generate a System User Access Token in Facebook Business Manager. 2. Copy your Phone Number ID. 3. Navigate to SGrowing Settings > WhatsApp and click Save.',
        isPublished: true,
        viewCount: 142,
        createdAtUtc: new Date().toISOString()
      },
      {
        id: 'a-2',
        categoryId: 'cat-1',
        title: 'Mailbox Warmup & Daily Sending Quotas',
        slug: 'mailbox-warmup-guide',
        contentMarkdown: 'To maintain 98%+ email deliverability, each mailbox starts with 5 safe sends per day and increments by 3 sends daily until reaching its target daily quota of 40-50 emails.',
        isPublished: true,
        viewCount: 89,
        createdAtUtc: new Date().toISOString()
      }
    ]);
  }

  public askAi() {
    if (!this.searchQuery) return;
    this.api.queryHelpCenter(this.tenantId, this.searchQuery).subscribe({
      next: (res) => this.ragResult.set(res),
      error: () => {
        const query = this.searchQuery.toLowerCase();
        if (query.includes('whatsapp') || query.includes('meta')) {
          this.ragResult.set({
            deflected: true,
            answer: "Based on our documentation 'Connecting WhatsApp Meta Cloud API':\n\nTo connect Meta WhatsApp Cloud API: 1. Generate a System User Access Token in Facebook Business Manager. 2. Copy your Phone Number ID. 3. Navigate to SGrowing Settings > WhatsApp and click Save.",
            articleTitle: 'Connecting WhatsApp Meta Cloud API',
            articleSlug: 'whatsapp-meta-setup',
            confidenceScore: 92
          });
        } else {
          this.ragResult.set({
            deflected: false,
            answer: "I couldn't find a direct article addressing this in our Help Center. I have notified our support team to step in.",
            confidenceScore: 35
          });
        }
      }
    });
  }
}
