import { Component, OnInit, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';

export interface FeedbackPostUi {
  id: string;
  title: string;
  description: string;
  category: string;
  authorName: string;
  upvoteCount: number;
  hasVoted: boolean;
  status: 'Open' | 'Planned' | 'InProgress' | 'Completed';
  createdAtUtc: string;
}

@Component({
  selector: 'app-roadmap-board',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterLink],
  template: `
    <div class="flex h-screen w-screen overflow-hidden bg-slate-950 text-slate-100 font-sans">
      <!-- Left Sidebar Navigation -->
      <aside class="w-64 border-r border-slate-800 bg-slate-900/80 flex flex-col justify-between p-4">
        <div>
          <div class="flex items-center gap-2 mb-6">
            <div class="h-8 w-8 rounded-lg bg-gradient-to-tr from-indigo-500 to-purple-600 flex items-center justify-center font-bold text-white shadow-lg shadow-indigo-500/20">
              🗺️
            </div>
            <div>
              <h1 class="text-sm font-semibold tracking-wide text-white">SGrowing</h1>
              <p class="text-xs text-indigo-400 font-medium">Public Product Roadmap</p>
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
              <a routerLink="/publications" class="w-full flex items-center gap-2 px-3 py-1.5 rounded-lg text-xs text-amber-400 hover:bg-amber-500/10 border border-amber-500/20 transition">
                <span>📰 Publication CMS & Newsletters</span>
              </a>
              <a routerLink="/forecasting" class="w-full flex items-center gap-2 px-3 py-1.5 rounded-lg text-xs text-emerald-400 hover:bg-emerald-500/10 border border-emerald-500/20 transition">
                <span>📈 Sales Forecasts & Quotas</span>
              </a>
              <a routerLink="/roadmap" class="w-full flex items-center gap-2 px-3 py-1.5 rounded-lg text-xs bg-indigo-500/20 text-indigo-300 border border-indigo-500/30 font-medium">
                <span>🗺️ Public Roadmap & Voting</span>
              </a>
            </div>
          </div>

          <!-- Feature Highlight Box -->
          <div class="p-3 bg-gradient-to-b from-slate-800/90 to-slate-900 border border-indigo-500/30 rounded-xl space-y-2">
            <div class="flex items-center justify-between">
              <span class="text-xs font-semibold text-indigo-300 flex items-center gap-1.5">
                <span class="h-2 w-2 rounded-full bg-indigo-400 animate-pulse"></span>
                Canny + Fider Board
              </span>
              <span class="text-[10px] bg-indigo-500/10 text-indigo-300 border border-indigo-500/20 px-1.5 py-0.5 rounded">Upvotes</span>
            </div>
            <p class="text-[11px] text-slate-300 leading-relaxed">
              Transparent community-driven prioritization with 1-click idempotent voting and automatic lifecycle status transitions.
            </p>
          </div>
        </div>
      </aside>

      <!-- Main Stage -->
      <main class="flex-1 flex flex-col overflow-hidden bg-slate-950">
        <!-- Header -->
        <header class="h-16 border-b border-slate-800 bg-slate-900/60 px-6 flex items-center justify-between">
          <div>
            <h2 class="text-base font-semibold text-white">Public Roadmap & Community Feature Voting</h2>
            <p class="text-xs text-slate-400">Discover upcoming capabilities, upvote top priority suggestions, and submit feature requests.</p>
          </div>
          <button
            (click)="showCreateModal.set(true)"
            class="px-3.5 py-1.5 bg-gradient-to-r from-indigo-600 to-purple-600 hover:from-indigo-500 hover:to-purple-500 text-white rounded-lg text-xs font-semibold shadow-lg shadow-indigo-500/20 flex items-center gap-1.5 transition">
            <span>+ Suggest Feature</span>
          </button>
        </header>

        <!-- 4-Column Kanban Board -->
        <div class="flex-1 overflow-x-auto p-6">
          <div class="grid grid-cols-4 gap-5 min-w-[1000px] h-full">
            <!-- Col 1: Open Ideas -->
            <div class="bg-slate-900/40 border border-slate-800 rounded-2xl flex flex-col overflow-hidden">
              <div class="p-4 border-b border-slate-800/80 bg-slate-900/60 flex items-center justify-between">
                <div class="flex items-center gap-2">
                  <span class="h-2.5 w-2.5 rounded-full bg-slate-400"></span>
                  <span class="text-xs font-bold text-white uppercase tracking-wider">Open Ideas</span>
                </div>
                <span class="text-xs text-slate-400 font-mono">{{ getByStatus('Open').length }}</span>
              </div>
              <div class="p-3 space-y-3 flex-1 overflow-y-auto">
                @for (post of getByStatus('Open'); track post.id) {
                  <ng-container *ngTemplateOutlet="postCard; context: { $implicit: post }"></ng-container>
                }
              </div>
            </div>

            <!-- Col 2: Planned -->
            <div class="bg-slate-900/40 border border-slate-800 rounded-2xl flex flex-col overflow-hidden">
              <div class="p-4 border-b border-slate-800/80 bg-slate-900/60 flex items-center justify-between">
                <div class="flex items-center gap-2">
                  <span class="h-2.5 w-2.5 rounded-full bg-blue-400"></span>
                  <span class="text-xs font-bold text-blue-300 uppercase tracking-wider">Planned</span>
                </div>
                <span class="text-xs text-blue-400 font-mono">{{ getByStatus('Planned').length }}</span>
              </div>
              <div class="p-3 space-y-3 flex-1 overflow-y-auto">
                @for (post of getByStatus('Planned'); track post.id) {
                  <ng-container *ngTemplateOutlet="postCard; context: { $implicit: post }"></ng-container>
                }
              </div>
            </div>

            <!-- Col 3: In Progress -->
            <div class="bg-slate-900/40 border border-slate-800 rounded-2xl flex flex-col overflow-hidden">
              <div class="p-4 border-b border-slate-800/80 bg-slate-900/60 flex items-center justify-between">
                <div class="flex items-center gap-2">
                  <span class="h-2.5 w-2.5 rounded-full bg-amber-400 animate-pulse"></span>
                  <span class="text-xs font-bold text-amber-300 uppercase tracking-wider">In Progress</span>
                </div>
                <span class="text-xs text-amber-400 font-mono">{{ getByStatus('InProgress').length }}</span>
              </div>
              <div class="p-3 space-y-3 flex-1 overflow-y-auto">
                @for (post of getByStatus('InProgress'); track post.id) {
                  <ng-container *ngTemplateOutlet="postCard; context: { $implicit: post }"></ng-container>
                }
              </div>
            </div>

            <!-- Col 4: Completed -->
            <div class="bg-slate-900/40 border border-slate-800 rounded-2xl flex flex-col overflow-hidden">
              <div class="p-4 border-b border-slate-800/80 bg-slate-900/60 flex items-center justify-between">
                <div class="flex items-center gap-2">
                  <span class="h-2.5 w-2.5 rounded-full bg-emerald-400"></span>
                  <span class="text-xs font-bold text-emerald-300 uppercase tracking-wider">Completed</span>
                </div>
                <span class="text-xs text-emerald-400 font-mono">{{ getByStatus('Completed').length }}</span>
              </div>
              <div class="p-3 space-y-3 flex-1 overflow-y-auto">
                @for (post of getByStatus('Completed'); track post.id) {
                  <ng-container *ngTemplateOutlet="postCard; context: { $implicit: post }"></ng-container>
                }
              </div>
            </div>
          </div>
        </div>
      </main>
    </div>

    <!-- Reusable Post Card Template -->
    <ng-template #postCard let-post>
      <div class="bg-slate-950/70 border border-slate-800/90 hover:border-slate-700/90 rounded-xl p-3.5 space-y-2.5 transition shadow-sm">
        <div class="flex items-start justify-between gap-2">
          <span class="text-[10px] font-semibold uppercase tracking-wider px-2 py-0.5 rounded bg-slate-800/80 text-slate-300 border border-slate-700/60">
            {{ post.category }}
          </span>
          <div class="flex items-center gap-1">
            <button
              (click)="toggleVote(post)"
              [class.bg-indigo-600]="post.hasVoted"
              [class.text-white]="post.hasVoted"
              [class.border-indigo-500]="post.hasVoted"
              [class.bg-slate-900]="!post.hasVoted"
              [class.text-slate-300]="!post.hasVoted"
              [class.border-slate-800]="!post.hasVoted"
              class="flex items-center gap-1 px-2 py-1 rounded-lg border text-xs font-semibold hover:border-indigo-500/50 transition">
              <span>▲</span>
              <span>{{ post.upvoteCount }}</span>
            </button>
          </div>
        </div>

        <div>
          <h4 class="text-xs font-semibold text-white leading-snug">{{ post.title }}</h4>
          <p class="text-[11px] text-slate-400 mt-1 leading-relaxed">{{ post.description }}</p>
        </div>

        <div class="pt-1 flex items-center justify-between text-[10px] text-slate-500 border-t border-slate-800/60">
          <span>{{ post.authorName }}</span>
          <select
            [ngModel]="post.status"
            (ngModelChange)="moveStatus(post, $event)"
            class="bg-slate-900 border border-slate-800 text-[10px] text-slate-400 rounded px-1.5 py-0.5 focus:outline-none">
            <option value="Open">Open</option>
            <option value="Planned">Planned</option>
            <option value="InProgress">In Progress</option>
            <option value="Completed">Completed</option>
          </select>
        </div>
      </div>
    </ng-template>

    <!-- Create Suggestion Modal -->
    @if (showCreateModal()) {
      <div class="fixed inset-0 bg-slate-950/80 backdrop-blur-sm z-50 flex items-center justify-center p-4">
        <div class="bg-slate-900 border border-slate-800 rounded-2xl w-full max-w-lg p-6 space-y-4 shadow-2xl">
          <div class="flex items-center justify-between border-b border-slate-800 pb-3">
            <h3 class="text-sm font-semibold text-white">Submit Feature Request</h3>
            <button (click)="showCreateModal.set(false)" class="text-slate-400 hover:text-white text-sm">✕</button>
          </div>

          <div class="space-y-3">
            <div>
              <label class="block text-xs font-medium text-slate-300 mb-1">Title</label>
              <input
                type="text"
                [(ngModel)]="newTitle"
                placeholder="e.g. Native QuickBooks Enterprise Two-Way Sync"
                class="w-full bg-slate-950 border border-slate-800 rounded-lg px-3 py-2 text-xs text-white focus:outline-none focus:border-indigo-500" />
            </div>

            <div>
              <label class="block text-xs font-medium text-slate-300 mb-1">Category</label>
              <select
                [(ngModel)]="newCategory"
                class="w-full bg-slate-950 border border-slate-800 rounded-lg px-3 py-2 text-xs text-white focus:outline-none focus:border-indigo-500">
                <option value="Integrations">Integrations & Connectors</option>
                <option value="Analytics">Analytics & Attribution</option>
                <option value="Commerce">Commerce & Checkout</option>
                <option value="CRM">Sales & CRM Workflows</option>
                <option value="Developer">Developer APIs & Webhooks</option>
              </select>
            </div>

            <div>
              <label class="block text-xs font-medium text-slate-300 mb-1">Description / Use Case</label>
              <textarea
                [(ngModel)]="newDescription"
                rows="3"
                placeholder="Explain the problem and why this would be high-value..."
                class="w-full bg-slate-950 border border-slate-800 rounded-lg px-3 py-2 text-xs text-white focus:outline-none focus:border-indigo-500"></textarea>
            </div>
          </div>

          <div class="flex justify-end gap-2 pt-3 border-t border-slate-800">
            <button
              (click)="showCreateModal.set(false)"
              class="px-4 py-2 bg-slate-800 hover:bg-slate-700 text-slate-300 rounded-lg text-xs font-medium transition">
              Cancel
            </button>
            <button
              (click)="savePost()"
              class="px-4 py-2 bg-gradient-to-r from-indigo-600 to-purple-600 hover:from-indigo-500 hover:to-purple-500 text-white rounded-lg text-xs font-semibold shadow-lg shadow-indigo-500/20 transition">
              Submit & Upvote
            </button>
          </div>
        </div>
      </div>
    }
  `
})
export class RoadmapBoardComponent implements OnInit {
  showCreateModal = signal(false);

  newTitle = '';
  newCategory = 'Integrations';
  newDescription = '';

  posts = signal<FeedbackPostUi[]>([
    {
      id: 'post-1',
      title: 'Native QuickBooks Two-Way Journal Sync',
      description: 'Sync B2B factoring invoices and customer payments directly to QBO without Zapier.',
      category: 'Integrations',
      authorName: 'CFO at FinCorp',
      upvoteCount: 42,
      hasVoted: true,
      status: 'Planned',
      createdAtUtc: '2026-10-22T10:00:00Z'
    },
    {
      id: 'post-2',
      title: 'Real-time Funnel Drop-off Telemetry Widget',
      description: 'Visualize checkout step retention waterfalls directly inside the dashboard.',
      category: 'Analytics',
      authorName: 'Growth Director',
      upvoteCount: 88,
      hasVoted: true,
      status: 'InProgress',
      createdAtUtc: '2026-10-24T14:30:00Z'
    },
    {
      id: 'post-3',
      title: 'Cryptographic SHA-256 E-Signature Certificates',
      description: 'Generate tamper-evident signing certificates with Papermark dwell time analytics.',
      category: 'CRM',
      authorName: 'Enterprise Sales VP',
      upvoteCount: 104,
      hasVoted: false,
      status: 'Completed',
      createdAtUtc: '2026-10-18T09:15:00Z'
    },
    {
      id: 'post-4',
      title: 'Multi-Currency Settlement Mesh for Global COD',
      description: 'Support automatic FX conversions and escrow factoring for cross-border freight.',
      category: 'Commerce',
      authorName: 'Global Merchant',
      upvoteCount: 29,
      hasVoted: false,
      status: 'Open',
      createdAtUtc: '2026-10-26T18:00:00Z'
    }
  ]);

  ngOnInit() {}

  getByStatus(status: 'Open' | 'Planned' | 'InProgress' | 'Completed') {
    return this.posts().filter(p => p.status === status);
  }

  toggleVote(post: FeedbackPostUi) {
    if (post.hasVoted) {
      post.hasVoted = false;
      post.upvoteCount--;
    } else {
      post.hasVoted = true;
      post.upvoteCount++;
    }
    this.posts.update(l => [...l]);
  }

  moveStatus(post: FeedbackPostUi, newStatus: 'Open' | 'Planned' | 'InProgress' | 'Completed') {
    post.status = newStatus;
    this.posts.update(l => [...l]);
  }

  savePost() {
    if (!this.newTitle) return;
    const newP: FeedbackPostUi = {
      id: 'post-' + (Math.floor(Math.random() * 900) + 100),
      title: this.newTitle,
      description: this.newDescription || 'Community feature request.',
      category: this.newCategory,
      authorName: 'Current User',
      upvoteCount: 1,
      hasVoted: true,
      status: 'Open',
      createdAtUtc: new Date().toISOString()
    };

    this.posts.update(l => [newP, ...l]);
    this.showCreateModal.set(false);
    this.newTitle = '';
    this.newDescription = '';
  }
}
