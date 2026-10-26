import { Component, OnInit, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';

export interface PublicationArticleUi {
  id: string;
  title: string;
  slug: string;
  excerpt: string;
  authorName: string;
  visibility: 'Public' | 'MembersOnly' | 'PaidTier';
  status: 'Draft' | 'Published';
  wordCount: number;
  estimatedReadMinutes: number;
  viewCount: number;
  publishedAtUtc?: string;
  htmlContent: string;
}

export interface NewsletterBroadcastUi {
  id: string;
  subject: string;
  recipientSegment: string;
  recipientsCount: number;
  deliveredCount: number;
  status: 'Completed' | 'Queued';
  sentAtUtc: string;
}

@Component({
  selector: 'app-publication-studio',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterLink],
  template: `
    <div class="flex h-screen w-screen overflow-hidden bg-slate-950 text-slate-100 font-sans">
      <!-- Left Sidebar Navigation -->
      <aside class="w-64 border-r border-slate-800 bg-slate-900/80 flex flex-col justify-between p-4">
        <div>
          <div class="flex items-center gap-2 mb-6">
            <div class="h-8 w-8 rounded-lg bg-gradient-to-tr from-amber-500 to-orange-500 flex items-center justify-center font-bold text-white shadow-lg shadow-amber-500/20">
              📰
            </div>
            <div>
              <h1 class="text-sm font-semibold tracking-wide text-white">SGrowing</h1>
              <p class="text-xs text-amber-400 font-medium">Ghost + Beehiiv CMS</p>
            </div>
          </div>

          <div class="space-y-1 mb-6">
            <span class="text-xs font-semibold text-slate-400 uppercase tracking-wider px-2">Core Suites</span>
            <div class="space-y-1">
              <a routerLink="/" class="w-full flex items-center gap-2 px-3 py-1.5 rounded-lg text-xs text-slate-300 hover:bg-slate-800/60 transition">
                <span>📥 Omnichannel Inbox</span>
              </a>
              <a routerLink="/documents" class="w-full flex items-center gap-2 px-3 py-1.5 rounded-lg text-xs text-cyan-400 hover:bg-cyan-500/10 border border-cyan-500/20 transition">
                <span>📜 Document Envelopes & Telemetry</span>
              </a>
              <a routerLink="/publications" class="w-full flex items-center gap-2 px-3 py-1.5 rounded-lg text-xs bg-amber-500/20 text-amber-300 border border-amber-500/30 font-medium">
                <span>📰 Publication CMS & Newsletters</span>
              </a>
              <a routerLink="/forecasting" class="w-full flex items-center gap-2 px-3 py-1.5 rounded-lg text-xs text-emerald-400 hover:bg-emerald-500/10 border border-emerald-500/20 transition">
                <span>📈 Sales Forecasts & Quotas</span>
              </a>
              <a routerLink="/roadmap" class="w-full flex items-center gap-2 px-3 py-1.5 rounded-lg text-xs text-indigo-400 hover:bg-indigo-500/10 border border-indigo-500/20 transition">
                <span>🗺️ Public Roadmap & Voting</span>
              </a>
            </div>
          </div>

          <!-- Feature Highlight Box -->
          <div class="p-3 bg-gradient-to-b from-slate-800/90 to-slate-900 border border-amber-500/30 rounded-xl space-y-2">
            <div class="flex items-center justify-between">
              <span class="text-xs font-semibold text-amber-300 flex items-center gap-1.5">
                <span class="h-2 w-2 rounded-full bg-amber-400 animate-pulse"></span>
                Tiered Paywalls
              </span>
              <span class="text-[10px] bg-amber-500/10 text-amber-300 border border-amber-500/20 px-1.5 py-0.5 rounded">Beehiiv Outbox</span>
            </div>
            <p class="text-[11px] text-slate-300 leading-relaxed">
              Monetize content with Ghost-style Public / Free Member / Paid tiers and high-velocity batch email broadcasts.
            </p>
          </div>
        </div>
      </aside>

      <!-- Main Stage -->
      <main class="flex-1 flex flex-col overflow-hidden bg-slate-950">
        <!-- Header -->
        <header class="h-16 border-b border-slate-800 bg-slate-900/60 px-6 flex items-center justify-between">
          <div>
            <h2 class="text-base font-semibold text-white">Headless Publication CMS & MEGA Newsletter Engine</h2>
            <p class="text-xs text-slate-400">Lexical AST editing, Ghost membership paywalls, and high-velocity transactional outbox broadcasts.</p>
          </div>
          <div class="flex items-center gap-3">
            <button
              (click)="showBroadcastModal.set(true)"
              class="px-3.5 py-1.5 bg-slate-800 hover:bg-slate-700 text-slate-200 border border-slate-700 rounded-lg text-xs font-medium transition flex items-center gap-1.5">
              <span>📧 Send Newsletter Blast</span>
            </button>
            <button
              (click)="showCreateModal.set(true)"
              class="px-3.5 py-1.5 bg-gradient-to-r from-amber-600 to-orange-600 hover:from-amber-500 hover:to-orange-500 text-white rounded-lg text-xs font-semibold shadow-lg shadow-amber-500/20 flex items-center gap-1.5 transition">
              <span>+ New Article</span>
            </button>
          </div>
        </header>

        <!-- Body Dashboard -->
        <div class="flex-1 overflow-y-auto p-6 space-y-6">
          <!-- Top Stats Row -->
          <div class="grid grid-cols-4 gap-4">
            <div class="bg-slate-900/60 border border-slate-800 rounded-xl p-4">
              <span class="text-xs font-medium text-slate-400">Published Articles</span>
              <div class="text-2xl font-bold text-white mt-1">{{ publishedCount() }}</div>
              <span class="text-[11px] text-amber-400 font-medium">Across all visibility tiers</span>
            </div>
            <div class="bg-slate-900/60 border border-slate-800 rounded-xl p-4">
              <span class="text-xs font-medium text-slate-400">Total Article Reads</span>
              <div class="text-2xl font-bold text-emerald-400 mt-1">18,420</div>
              <span class="text-[11px] text-emerald-400/80">+24% month-over-month</span>
            </div>
            <div class="bg-slate-900/60 border border-slate-800 rounded-xl p-4">
              <span class="text-xs font-medium text-slate-400">Newsletter Subscribers</span>
              <div class="text-2xl font-bold text-orange-400 mt-1">4,850</div>
              <span class="text-[11px] text-orange-400/80">3,650 Free • 1,200 Paid Tier</span>
            </div>
            <div class="bg-slate-900/60 border border-slate-800 rounded-xl p-4">
              <span class="text-xs font-medium text-slate-400">Broadcast Delivery Rate</span>
              <div class="text-2xl font-bold text-cyan-400 mt-1">99.2%</div>
              <span class="text-[11px] text-cyan-400/80">Transactional Outbox engine</span>
            </div>
          </div>

          <!-- Main 2-Column Split: Articles List vs Interactive Paywall Preview -->
          <div class="grid grid-cols-2 gap-6">
            <!-- Left: Articles List -->
            <div class="bg-slate-900/60 border border-slate-800 rounded-xl overflow-hidden flex flex-col">
              <div class="p-4 border-b border-slate-800 flex items-center justify-between">
                <h3 class="text-sm font-semibold text-white">Articles & Publications</h3>
                <span class="text-xs text-slate-400">Select article to test paywall rendering</span>
              </div>
              <div class="divide-y divide-slate-800/80 overflow-y-auto max-h-[520px]">
                @for (art of articles(); track art.id) {
                  <div
                    (click)="selectedArticle.set(art)"
                    [class.bg-slate-800-40]="selectedArticle()?.id === art.id"
                    class="p-4 hover:bg-slate-800/40 cursor-pointer transition flex items-center justify-between">
                    <div class="space-y-1">
                      <div class="flex items-center gap-2">
                        <span class="text-sm font-medium text-white">{{ art.title }}</span>
                        <span
                          [class.bg-amber-500-10]="art.visibility === 'PaidTier'"
                          [class.text-amber-400]="art.visibility === 'PaidTier'"
                          [class.border-amber-500-20]="art.visibility === 'PaidTier'"
                          [class.bg-blue-500-10]="art.visibility === 'MembersOnly'"
                          [class.text-blue-400]="art.visibility === 'MembersOnly'"
                          [class.border-blue-500-20]="art.visibility === 'MembersOnly'"
                          [class.bg-slate-800]="art.visibility === 'Public'"
                          [class.text-slate-300]="art.visibility === 'Public'"
                          class="text-[10px] px-2 py-0.5 rounded-full border">
                          {{ art.visibility }}
                        </span>
                        <span class="text-[10px] text-slate-400 bg-slate-800 px-1.5 py-0.5 rounded">
                          {{ art.status }}
                        </span>
                      </div>
                      <div class="text-xs text-slate-400 flex items-center gap-2">
                        <span>by {{ art.authorName }}</span>
                        <span>•</span>
                        <span>{{ art.estimatedReadMinutes }} min read</span>
                        <span>•</span>
                        <span>{{ art.viewCount }} views</span>
                      </div>
                    </div>
                    <div>
                      @if (art.status === 'Draft') {
                        <button
                          (click)="publishArticle(art); $event.stopPropagation()"
                          class="px-2.5 py-1 bg-emerald-600/20 hover:bg-emerald-600/30 text-emerald-300 border border-emerald-500/30 rounded text-xs transition">
                          Publish
                        </button>
                      } @else {
                        <span class="text-xs text-emerald-400 font-medium">Live</span>
                      }
                    </div>
                  </div>
                }
              </div>
            </div>

            <!-- Right: Interactive Paywall Preview Simulator -->
            <div class="bg-slate-900/60 border border-slate-800 rounded-xl p-5 space-y-4 flex flex-col">
              <div class="flex items-center justify-between border-b border-slate-800 pb-3">
                <div>
                  <h3 class="text-sm font-semibold text-white">Ghost Paywall Simulation Engine</h3>
                  <p class="text-xs text-slate-400">Toggle reader tier to inspect content gate enforcement</p>
                </div>
                <!-- Reader Persona Selector -->
                <div class="flex items-center gap-1 bg-slate-950 p-1 rounded-lg border border-slate-800 text-xs">
                  <button
                    (click)="readerTier.set('Anonymous')"
                    [class.bg-slate-800]="readerTier() === 'Anonymous'"
                    [class.text-white]="readerTier() === 'Anonymous'"
                    class="px-2 py-1 rounded text-slate-400 transition">
                    Anonymous
                  </button>
                  <button
                    (click)="readerTier.set('FreeMember')"
                    [class.bg-slate-800]="readerTier() === 'FreeMember'"
                    [class.text-white]="readerTier() === 'FreeMember'"
                    class="px-2 py-1 rounded text-slate-400 transition">
                    Free Member
                  </button>
                  <button
                    (click)="readerTier.set('PaidMember')"
                    [class.bg-amber-500-20]="readerTier() === 'PaidMember'"
                    [class.text-amber-300]="readerTier() === 'PaidMember'"
                    class="px-2 py-1 rounded text-slate-400 transition">
                    Paid Member
                  </button>
                </div>
              </div>

              @if (selectedArticle(); as art) {
                <div class="space-y-4 flex-1 overflow-y-auto">
                  <div>
                    <span class="text-[10px] text-amber-400 uppercase tracking-widest font-semibold">{{ art.visibility }} ARTICLE</span>
                    <h2 class="text-lg font-bold text-white mt-1">{{ art.title }}</h2>
                    <div class="text-xs text-slate-400 mt-1 flex items-center gap-2">
                      <span>{{ art.authorName }}</span>
                      <span>•</span>
                      <span>{{ art.estimatedReadMinutes }} min read</span>
                    </div>
                  </div>

                  <!-- Simulated Content Box -->
                  <div class="bg-slate-950/80 border border-slate-800 rounded-xl p-4 space-y-3">
                    <p class="text-xs text-slate-300 italic">{{ art.excerpt }}</p>

                    @if (isReaderGated(art)) {
                      <!-- Gated Paywall View -->
                      <div class="space-y-3 pt-2">
                        <p class="text-xs text-slate-400 line-clamp-2 select-none filter blur-[1px]">
                          {{ art.htmlContent }}
                        </p>

                        <!-- Ghost-Style Paywall Lock Banner -->
                        <div class="paywall-lock-banner p-4 rounded-xl bg-gradient-to-b from-amber-500/10 via-amber-500/5 to-slate-900 border border-amber-500/30 text-center space-y-2">
                          <div class="text-lg">🔒</div>
                          <h4 class="text-xs font-bold text-amber-300">
                            {{ art.visibility === 'PaidTier' ? 'Exclusive Premium Insight' : 'Members Only Content' }}
                          </h4>
                          <p class="text-[11px] text-slate-300 max-w-sm mx-auto">
                            {{ art.visibility === 'PaidTier'
                              ? 'Exclusive Premium Insight. Upgrade to our Premium Membership to unlock full access.'
                              : 'This publication is reserved for free registered members. Subscribe to continue reading.' }}
                          </p>
                          <button class="mt-2 px-4 py-1.5 bg-gradient-to-r from-amber-600 to-orange-600 hover:from-amber-500 hover:to-orange-500 text-white rounded-lg text-xs font-semibold shadow-lg shadow-amber-500/20 transition">
                            {{ art.visibility === 'PaidTier' ? 'Upgrade to Paid Tier ($19/mo)' : 'Sign Up Free' }}
                          </button>
                        </div>
                      </div>
                    } @else {
                      <!-- Full Unlocked Content -->
                      <div class="space-y-2 text-xs text-slate-200 leading-relaxed pt-2">
                        <div class="p-2 bg-emerald-500/10 border border-emerald-500/20 rounded text-[11px] text-emerald-300 flex items-center gap-2">
                          <span>✓ Unlocked:</span>
                          <span>Full membership access granted for {{ readerTier() }}</span>
                        </div>
                        <p>{{ art.htmlContent }}</p>
                      </div>
                    }
                  </div>
                </div>
              }
            </div>
          </div>
        </div>
      </main>
    </div>

    <!-- Create Article Modal -->
    @if (showCreateModal()) {
      <div class="fixed inset-0 bg-slate-950/80 backdrop-blur-sm z-50 flex items-center justify-center p-4">
        <div class="bg-slate-900 border border-slate-800 rounded-2xl w-full max-w-lg p-6 space-y-4 shadow-2xl">
          <div class="flex items-center justify-between border-b border-slate-800 pb-3">
            <h3 class="text-sm font-semibold text-white">Create New Publication Article</h3>
            <button (click)="showCreateModal.set(false)" class="text-slate-400 hover:text-white text-sm">✕</button>
          </div>

          <div class="space-y-3">
            <div>
              <label class="block text-xs font-medium text-slate-300 mb-1">Title</label>
              <input
                type="text"
                [(ngModel)]="newTitle"
                placeholder="e.g. The 2026 SaaS Growth Flywheel"
                class="w-full bg-slate-950 border border-slate-800 rounded-lg px-3 py-2 text-xs text-white focus:outline-none focus:border-amber-500" />
            </div>

            <div>
              <label class="block text-xs font-medium text-slate-300 mb-1">Excerpt / Teaser</label>
              <input
                type="text"
                [(ngModel)]="newExcerpt"
                placeholder="Short teaser shown to public readers before the paywall lock..."
                class="w-full bg-slate-950 border border-slate-800 rounded-lg px-3 py-2 text-xs text-white focus:outline-none focus:border-amber-500" />
            </div>

            <div>
              <label class="block text-xs font-medium text-slate-300 mb-1">Visibility Paywall Tier</label>
              <select
                [(ngModel)]="newVisibility"
                class="w-full bg-slate-950 border border-slate-800 rounded-lg px-3 py-2 text-xs text-white focus:outline-none focus:border-amber-500">
                <option value="Public">Public (Free to all)</option>
                <option value="MembersOnly">Members Only (Requires Free Account)</option>
                <option value="PaidTier">Paid Tier (Requires Active Paid Subscription)</option>
              </select>
            </div>

            <div>
              <label class="block text-xs font-medium text-slate-300 mb-1">Full Article Content (HTML / Lexical)</label>
              <textarea
                [(ngModel)]="newContent"
                rows="4"
                placeholder="Full comprehensive deep dive article text..."
                class="w-full bg-slate-950 border border-slate-800 rounded-lg px-3 py-2 text-xs text-white focus:outline-none focus:border-amber-500"></textarea>
            </div>
          </div>

          <div class="flex justify-end gap-2 pt-3 border-t border-slate-800">
            <button
              (click)="showCreateModal.set(false)"
              class="px-4 py-2 bg-slate-800 hover:bg-slate-700 text-slate-300 rounded-lg text-xs font-medium transition">
              Cancel
            </button>
            <button
              (click)="saveArticle()"
              class="px-4 py-2 bg-gradient-to-r from-amber-600 to-orange-600 hover:from-amber-500 hover:to-orange-500 text-white rounded-lg text-xs font-semibold shadow-lg shadow-amber-500/20 transition">
              Create Article
            </button>
          </div>
        </div>
      </div>
    }

    <!-- Newsletter Broadcast Modal -->
    @if (showBroadcastModal()) {
      <div class="fixed inset-0 bg-slate-950/80 backdrop-blur-sm z-50 flex items-center justify-center p-4">
        <div class="bg-slate-900 border border-slate-800 rounded-2xl w-full max-w-lg p-6 space-y-4 shadow-2xl">
          <div class="flex items-center justify-between border-b border-slate-800 pb-3">
            <div>
              <h3 class="text-sm font-semibold text-white">Dispatch Newsletter Broadcast</h3>
              <p class="text-xs text-slate-400">Beehiiv-style high-velocity batch newsletter engine</p>
            </div>
            <button (click)="showBroadcastModal.set(false)" class="text-slate-400 hover:text-white text-sm">✕</button>
          </div>

          <div class="space-y-3">
            <div>
              <label class="block text-xs font-medium text-slate-300 mb-1">Email Subject Line</label>
              <input
                type="text"
                [(ngModel)]="broadcastSubject"
                placeholder="Issue #43: The Secrets of 80% Retargeting ROI"
                class="w-full bg-slate-950 border border-slate-800 rounded-lg px-3 py-2 text-xs text-white focus:outline-none focus:border-amber-500" />
            </div>

            <div>
              <label class="block text-xs font-medium text-slate-300 mb-1">Recipient Segment</label>
              <select
                [(ngModel)]="broadcastSegment"
                class="w-full bg-slate-950 border border-slate-800 rounded-lg px-3 py-2 text-xs text-white focus:outline-none focus:border-amber-500">
                <option value="AllSubscribers">All Subscribers (4,850)</option>
                <option value="PaidMembers">Paid Tier Members Only (1,200)</option>
                <option value="FreeMembers">Free Tier Members Only (3,650)</option>
              </select>
            </div>

            <div class="p-3 bg-slate-950 border border-slate-800 rounded-xl space-y-1 text-xs text-slate-400">
              <div class="flex justify-between">
                <span>Estimated Audience:</span>
                <span class="text-white font-medium">4,850 readers</span>
              </div>
              <div class="flex justify-between">
                <span>Dispatch Mode:</span>
                <span class="text-cyan-400 font-medium">Transactional Outbox Workers</span>
              </div>
            </div>
          </div>

          <div class="flex justify-end gap-2 pt-3 border-t border-slate-800">
            <button
              (click)="showBroadcastModal.set(false)"
              class="px-4 py-2 bg-slate-800 hover:bg-slate-700 text-slate-300 rounded-lg text-xs font-medium transition">
              Cancel
            </button>
            <button
              (click)="sendBroadcast()"
              class="px-4 py-2 bg-gradient-to-r from-amber-600 to-orange-600 hover:from-amber-500 hover:to-orange-500 text-white rounded-lg text-xs font-semibold shadow-lg shadow-amber-500/20 transition">
              Dispatch to Outbox Now
            </button>
          </div>
        </div>
      </div>
    }
  `
})
export class PublicationStudioComponent implements OnInit {
  showCreateModal = signal(false);
  showBroadcastModal = signal(false);
  selectedArticle = signal<PublicationArticleUi | null>(null);
  readerTier = signal<'Anonymous' | 'FreeMember' | 'PaidMember'>('Anonymous');

  newTitle = '';
  newExcerpt = '';
  newVisibility: 'Public' | 'MembersOnly' | 'PaidTier' = 'PaidTier';
  newContent = '';

  broadcastSubject = 'Weekly Growth Insights #42: Retention Tactics';
  broadcastSegment = 'AllSubscribers';

  articles = signal<PublicationArticleUi[]>([
    {
      id: 'art-1',
      title: 'The 2026 SaaS Growth & Retention Playbook',
      slug: 'saas-retention-playbook',
      excerpt: 'Comprehensive blueprint for reducing churn to sub-1.5% and scaling enterprise ARR.',
      authorName: 'Sarah Connor',
      visibility: 'PaidTier',
      status: 'Published',
      wordCount: 1450,
      estimatedReadMinutes: 8,
      viewCount: 3410,
      publishedAtUtc: '2026-10-24T12:00:00Z',
      htmlContent: 'In this exclusive edition, we unveil the exact cohorts analysis and retention loops deployed by top decacorns...'
    },
    {
      id: 'art-2',
      title: 'Community Weekly Memo: Scaling E-Commerce in Emerging Markets',
      slug: 'community-memo-ecommerce',
      excerpt: 'A review of Cash On Delivery logistics and local payment mesh integration strategies.',
      authorName: 'Editorial Team',
      visibility: 'MembersOnly',
      status: 'Published',
      wordCount: 820,
      estimatedReadMinutes: 4,
      viewCount: 1820,
      publishedAtUtc: '2026-10-25T08:30:00Z',
      htmlContent: 'Registered members get direct access to our verified courier network benchmarks and fleet routing calculations...'
    },
    {
      id: 'art-3',
      title: 'Announcing SGrowing 2.0: Unified Growth Platform',
      slug: 'announcing-sgrowing-2',
      excerpt: 'Public announcement of the all-in-one marketing, sales, CRM and e-commerce OS.',
      authorName: 'Product Team',
      visibility: 'Public',
      status: 'Published',
      wordCount: 500,
      estimatedReadMinutes: 3,
      viewCount: 13190,
      publishedAtUtc: '2026-10-20T10:00:00Z',
      htmlContent: 'Today marks a massive milestone as we release our open-source inspired modular monolith architecture...'
    }
  ]);

  publishedCount = signal(3);

  ngOnInit() {
    this.selectedArticle.set(this.articles()[0]);
  }

  isReaderGated(art: PublicationArticleUi): boolean {
    const tier = this.readerTier();
    if (art.visibility === 'PaidTier') {
      return tier !== 'PaidMember';
    }
    if (art.visibility === 'MembersOnly') {
      return tier === 'Anonymous';
    }
    return false;
  }

  publishArticle(art: PublicationArticleUi) {
    art.status = 'Published';
    art.publishedAtUtc = new Date().toISOString();
    this.publishedCount.update(c => c + 1);
  }

  saveArticle() {
    if (!this.newTitle) return;
    const words = (this.newContent || 'Sample content').split(/\s+/).length;
    const readMinutes = Math.max(1, Math.ceil(words / 200));

    const newArt: PublicationArticleUi = {
      id: 'art-' + (Math.floor(Math.random() * 900) + 100),
      title: this.newTitle,
      slug: this.newTitle.toLowerCase().replace(/[^a-z0-9]+/g, '-'),
      excerpt: this.newExcerpt || 'A brief summary of this article.',
      authorName: 'Editorial Staff',
      visibility: this.newVisibility,
      status: 'Draft',
      wordCount: words,
      estimatedReadMinutes: readMinutes,
      viewCount: 0,
      htmlContent: this.newContent || 'Draft article content...'
    };

    this.articles.update(list => [newArt, ...list]);
    this.selectedArticle.set(newArt);
    this.showCreateModal.set(false);
    this.newTitle = '';
    this.newExcerpt = '';
    this.newContent = '';
  }

  sendBroadcast() {
    this.showBroadcastModal.set(false);
  }
}
