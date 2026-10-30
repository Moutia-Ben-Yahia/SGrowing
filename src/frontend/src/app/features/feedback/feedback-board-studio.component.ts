import { Component, OnInit, signal, computed, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { HttpClient } from '@angular/common/http';

export type FeedbackCategoryType = 'FeatureRequest' | 'Integration' | 'BugReport' | 'UxImprovement';
export type FeedbackStatusType = 'Submitted' | 'UnderReview' | 'Planned' | 'InProgress' | 'Completed' | 'Declined';
export type SortOptionType = 'impact' | 'top' | 'newest';
export type ViewModeType = 'board' | 'roadmap';

export interface FeedbackPostUi {
  id: string;
  tenantId: string;
  title: string;
  description: string;
  category: FeedbackCategoryType;
  status: FeedbackStatusType;
  authorEmail: string;
  authorName: string;
  accountId: string;
  accountARR: number;
  upvoteCount: number;
  impactScore: number;
  createdAt: string;
  updatedAt: string;
  completedAt?: string | null;
  estimatedQuarter: string;
  responseNotes?: string | null;
  hasVoted?: boolean;
}

export interface FeedbackCommentUi {
  id: string;
  tenantId: string;
  postId: string;
  authorName: string;
  authorEmail: string;
  isOfficialTeamResponse: boolean;
  content: string;
  createdAt: string;
}

export interface NewIdeaForm {
  title: string;
  description: string;
  category: FeedbackCategoryType;
  authorName: string;
  authorEmail: string;
  accountARR: number;
  estimatedQuarter: string;
}

function calculateImpactScore(upvotes: number, accountArr: number): number {
  if (upvotes <= 0) return 0;
  const safeArr = Math.max(0, accountArr);
  const logFactor = Math.log10(safeArr + 10.0);
  return Math.round(upvotes * logFactor * 100) / 100;
}

@Component({
  selector: 'app-feedback-board-studio',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterLink],
  template: `
    <div dir="ltr" class="flex h-screen w-screen overflow-hidden bg-slate-950 text-slate-100 font-sans selection:bg-indigo-500 selection:text-white">
      <!-- 1. Left Sidebar Navigation -->
      <aside class="w-64 border-r border-slate-800 bg-slate-900/80 flex flex-col justify-between p-4 flex-shrink-0">
        <div>
          <!-- Logo & Brand Header -->
          <div class="flex items-center gap-2.5 mb-6">
            <div class="h-9 w-9 rounded-xl bg-gradient-to-tr from-indigo-500 via-purple-500 to-pink-500 flex items-center justify-center font-bold text-white shadow-lg shadow-indigo-500/25 ring-1 ring-white/20">
              💡
            </div>
            <div>
              <h1 class="text-sm font-semibold tracking-wide text-white">SGrowing</h1>
              <p class="text-xs text-indigo-400 font-medium">Community Roadmap</p>
            </div>
          </div>

          <!-- Quick Navigation -->
          <nav class="space-y-1 mb-6">
            <span class="text-[11px] font-semibold text-slate-400 uppercase tracking-wider px-2">Navigation</span>
            <a routerLink="/" class="w-full flex items-center gap-2.5 px-3 py-2 rounded-lg text-xs font-medium text-slate-400 hover:text-slate-100 hover:bg-slate-800/60 transition">
              <span>📥 Omnichannel Inbox</span>
            </a>
            <a routerLink="/feedback" class="w-full flex items-center gap-2.5 px-3 py-2 rounded-lg text-xs font-medium text-slate-400 hover:text-slate-100 hover:bg-slate-800/60 transition">
              <span>⭐ Surveys & CSAT</span>
            </a>
            <div class="w-full flex items-center gap-2.5 px-3 py-2 rounded-lg text-xs font-medium bg-indigo-600/20 text-indigo-300 border border-indigo-500/30 shadow-sm transition">
              <span>💡 Feedback Board Studio</span>
            </div>
            <a routerLink="/crm" class="w-full flex items-center gap-2.5 px-3 py-2 rounded-lg text-xs font-medium text-slate-400 hover:text-slate-100 hover:bg-slate-800/60 transition">
              <span>👥 Customer 360 & Retention</span>
            </a>
            <a routerLink="/finance" class="w-full flex items-center gap-2.5 px-3 py-2 rounded-lg text-xs font-medium text-slate-400 hover:text-slate-100 hover:bg-slate-800/60 transition">
              <span>📊 Working Capital & Float</span>
            </a>
          </nav>

          <!-- ARR Impact Info Card -->
          <div class="p-3.5 bg-gradient-to-b from-indigo-950/40 via-slate-900 to-slate-900/90 border border-indigo-500/30 rounded-xl space-y-2">
            <div class="flex items-center justify-between">
              <span class="text-xs font-semibold text-indigo-300 flex items-center gap-1.5">
                <span class="h-2 w-2 rounded-full bg-indigo-400 animate-pulse"></span>
                ARR-Weighted Value
              </span>
              <span class="text-[10px] bg-indigo-500/20 text-indigo-300 border border-indigo-500/30 px-1.5 py-0.5 rounded font-mono">
                Log10 Scale
              </span>
            </div>
            <p class="text-[11px] text-slate-300 leading-relaxed">
              Votes are scaled logarithmically by account ARR:
              <code class="text-indigo-300 text-[10px] block mt-1 bg-slate-950/80 p-1 rounded font-mono border border-indigo-500/20">
                Score = Votes × log10(ARR + 10)
              </code>
            </p>
          </div>
        </div>

        <!-- Mode Toggle & User Switcher -->
        <div class="border-t border-slate-800 pt-3 space-y-2">
          <div class="flex items-center justify-between text-xs text-slate-400 px-1">
            <span>Admin Control Mode</span>
            <button
              (click)="toggleAdminMode()"
              [class]="isAdminMode() ? 'bg-indigo-600 text-white' : 'bg-slate-800 text-slate-400 hover:text-slate-200'"
              class="relative inline-flex h-5 w-9 flex-shrink-0 cursor-pointer rounded-full border-2 border-transparent transition-colors duration-200 ease-in-out focus:outline-none">
              <span
                [class]="isAdminMode() ? 'translate-x-4 bg-white' : 'translate-x-0 bg-slate-400'"
                class="pointer-events-none inline-block h-4 w-4 transform rounded-full shadow ring-0 transition duration-200 ease-in-out">
              </span>
            </button>
          </div>
          <p class="text-[10px] text-slate-500 px-1">
            {{ isAdminMode() ? '⚡ Admin Mode: Status management & official notes unlocked.' : '👀 Public Community Mode: Upvote & submit ideas.' }}
          </p>
        </div>
      </aside>

      <!-- 2. Main Studio Content Area -->
      <main class="flex-1 flex flex-col overflow-hidden bg-slate-950">
        <!-- Top Bar: Title, Views & Actions -->
        <header class="h-16 border-b border-slate-800 bg-slate-900/60 backdrop-blur-md px-6 flex items-center justify-between flex-shrink-0">
          <div class="flex items-center gap-4">
            <div>
              <div class="flex items-center gap-2">
                <h2 class="text-base font-semibold text-white tracking-tight">Public Feedback & ARR-Weighted Roadmap</h2>
                @if (isAdminMode()) {
                  <span class="text-[10px] font-medium bg-amber-500/20 text-amber-300 border border-amber-500/30 px-2 py-0.5 rounded-full flex items-center gap-1">
                    👑 Product Council Admin
                  </span>
                }
              </div>
              <p class="text-xs text-slate-400">Collaborative feature prioritization driven by customer voice and revenue impact</p>
            </div>
          </div>

          <div class="flex items-center gap-3">
            <!-- View Mode Switcher -->
            <div class="flex items-center p-1 bg-slate-800/80 rounded-lg border border-slate-700/60 text-xs">
              <button
                (click)="setViewMode('board')"
                [class]="viewMode() === 'board' ? 'bg-indigo-600 text-white shadow-sm' : 'text-slate-400 hover:text-slate-200'"
                class="flex items-center gap-1.5 px-3 py-1 rounded-md transition font-medium">
                <span>📋 Suggestion Feed</span>
              </button>
              <button
                (click)="setViewMode('roadmap')"
                [class]="viewMode() === 'roadmap' ? 'bg-indigo-600 text-white shadow-sm' : 'text-slate-400 hover:text-slate-200'"
                class="flex items-center gap-1.5 px-3 py-1 rounded-md transition font-medium">
                <span>🗺️ Roadmap Kanban</span>
              </button>
            </div>

            <!-- Submit Idea Action -->
            <button
              (click)="openSubmitModal()"
              class="flex items-center gap-1.5 px-4 py-2 bg-gradient-to-r from-indigo-500 to-purple-600 hover:from-indigo-600 hover:to-purple-700 text-white text-xs font-semibold rounded-lg shadow-md shadow-indigo-500/20 border border-indigo-400/30 transition hover:scale-[1.02] active:scale-[0.98]">
              <span>✨ Submit New Idea</span>
            </button>
          </div>
        </header>

        <!-- KPI Metrics Ribbon -->
        <section class="border-b border-slate-800/80 bg-slate-900/30 px-6 py-4 flex-shrink-0">
          <div class="grid grid-cols-4 gap-4 max-w-7xl">
            <!-- KPI 1 -->
            <div class="bg-slate-900/70 border border-slate-800 rounded-xl p-3.5 flex items-center justify-between shadow-sm">
              <div>
                <p class="text-[11px] font-medium text-slate-400">Total Suggestions</p>
                <p class="text-xl font-bold text-white mt-0.5 font-mono">{{ totalIdeas() }}</p>
                <p class="text-[10px] text-slate-500 mt-0.5">Active community ideas</p>
              </div>
              <div class="h-10 w-10 rounded-lg bg-indigo-500/10 border border-indigo-500/20 flex items-center justify-center text-indigo-400 text-lg">
                💡
              </div>
            </div>

            <!-- KPI 2 -->
            <div class="bg-slate-900/70 border border-slate-800 rounded-xl p-3.5 flex items-center justify-between shadow-sm">
              <div>
                <p class="text-[11px] font-medium text-slate-400">Community Upvotes</p>
                <p class="text-xl font-bold text-emerald-400 mt-0.5 font-mono">{{ totalVotes() }}</p>
                <p class="text-[10px] text-emerald-500/80 mt-0.5">Crowdsourced demand signals</p>
              </div>
              <div class="h-10 w-10 rounded-lg bg-emerald-500/10 border border-emerald-500/20 flex items-center justify-center text-emerald-400 text-lg">
                🚀
              </div>
            </div>

            <!-- KPI 3 -->
            <div class="bg-slate-900/70 border border-slate-800 rounded-xl p-3.5 flex items-center justify-between shadow-sm">
              <div>
                <p class="text-[11px] font-medium text-slate-400">Features Shipped</p>
                <p class="text-xl font-bold text-cyan-400 mt-0.5 font-mono">{{ completedVelocity() }}</p>
                <p class="text-[10px] text-cyan-500/80 mt-0.5">Delivered to production</p>
              </div>
              <div class="h-10 w-10 rounded-lg bg-cyan-500/10 border border-cyan-500/20 flex items-center justify-center text-cyan-400 text-lg">
                🏁
              </div>
            </div>

            <!-- KPI 4 -->
            <div class="bg-slate-900/70 border border-slate-800 rounded-xl p-3.5 flex items-center justify-between shadow-sm">
              <div>
                <p class="text-[11px] font-medium text-slate-400">ARR-Influenced Pipeline</p>
                <p class="text-xl font-bold text-purple-400 mt-0.5 font-mono">\${{ formatNumber(totalArrDemand()) }}</p>
                <p class="text-[10px] text-purple-400/80 mt-0.5">Annual recurring revenue backing</p>
              </div>
              <div class="h-10 w-10 rounded-lg bg-purple-500/10 border border-purple-500/20 flex items-center justify-center text-purple-400 text-lg">
                💎
              </div>
            </div>
          </div>
        </section>

        <!-- Filters & Search Toolbar -->
        <section class="border-b border-slate-800 bg-slate-900/40 px-6 py-3 flex items-center justify-between gap-4 flex-shrink-0">
          <!-- Search & Category Filters -->
          <div class="flex items-center gap-3 flex-1 flex-wrap">
            <!-- Search Box -->
            <div class="relative w-64">
              <span class="absolute inset-y-0 left-0 pl-2.5 flex items-center pointer-events-none text-slate-400 text-xs">
                🔍
              </span>
              <input
                type="text"
                [(ngModel)]="searchQuery"
                placeholder="Search ideas, description, quarter..."
                class="w-full pl-8 pr-3 py-1.5 bg-slate-900 border border-slate-700/80 rounded-lg text-xs text-white placeholder-slate-500 focus:outline-none focus:border-indigo-500 transition" />
            </div>

            <!-- Category Pills -->
            <div class="flex items-center gap-1 bg-slate-900/80 p-0.5 rounded-lg border border-slate-800 text-xs">
              @for (cat of categories; track cat.id) {
                <button
                  (click)="selectedCategory.set(cat.id)"
                  [class]="selectedCategory() === cat.id ? 'bg-indigo-600 text-white font-medium shadow-sm' : 'text-slate-400 hover:text-slate-200'"
                  class="px-2.5 py-1 rounded-md text-[11px] transition">
                  {{ cat.label }}
                </button>
              }
            </div>

            <!-- Status Pills -->
            <div class="flex items-center gap-1 bg-slate-900/80 p-0.5 rounded-lg border border-slate-800 text-xs">
              @for (st of statuses; track st.id) {
                <button
                  (click)="selectedStatus.set(st.id)"
                  [class]="selectedStatus() === st.id ? 'bg-indigo-600 text-white font-medium shadow-sm' : 'text-slate-400 hover:text-slate-200'"
                  class="px-2.5 py-1 rounded-md text-[11px] transition">
                  {{ st.label }}
                </button>
              }
            </div>
          </div>

          <!-- Sorting Mode -->
          <div class="flex items-center gap-2 text-xs flex-shrink-0">
            <span class="text-slate-400 text-[11px]">Sort By:</span>
            <select
              [ngModel]="sortBy()"
              (ngModelChange)="sortBy.set($event)"
              class="bg-slate-900 border border-slate-700 text-white text-xs rounded-lg px-2.5 py-1.5 focus:outline-none focus:border-indigo-500">
              <option value="impact">🔥 Impact Score (ARR × Votes)</option>
              <option value="top">👍 Top Voted</option>
              <option value="newest">🕒 Newest First</option>
            </select>
          </div>
        </section>

        <!-- Main Body: Feed View vs Kanban View -->
        <div class="flex-1 overflow-y-auto p-6">
          @if (viewMode() === 'board') {
            <!-- BOARD / FEED VIEW -->
            <div class="max-w-7xl mx-auto space-y-4">
              @if (filteredPosts().length === 0) {
                <div class="text-center py-16 bg-slate-900/40 border border-slate-800/80 rounded-2xl">
                  <span class="text-4xl block mb-2">💡</span>
                  <h3 class="text-sm font-semibold text-white">No feature requests match your filters</h3>
                  <p class="text-xs text-slate-400 mt-1 max-w-sm mx-auto">
                    Try adjusting your search criteria, clear category filters, or be the first to submit this feature suggestion!
                  </p>
                  <button
                    (click)="openSubmitModal()"
                    class="mt-4 px-4 py-2 bg-indigo-600 hover:bg-indigo-500 text-white text-xs font-medium rounded-lg shadow transition">
                    + Submit This Idea
                  </button>
                </div>
              } @else {
                <div class="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-4">
                  @for (post of filteredPosts(); track post.id) {
                    <div
                      (click)="openDetailModal(post)"
                      class="bg-slate-900/70 hover:bg-slate-900 border border-slate-800 hover:border-slate-700 rounded-xl p-4 flex flex-col justify-between transition-all duration-200 cursor-pointer hover:shadow-lg hover:shadow-indigo-500/5 group">
                      
                      <!-- Card Header: Badges & Upvote Button -->
                      <div>
                        <div class="flex items-start justify-between gap-3 mb-2.5">
                          <div class="flex items-center gap-1.5 flex-wrap">
                            <!-- Category Badge -->
                            <span [class]="getCategoryBadgeClass(post.category)" class="text-[10px] font-semibold px-2 py-0.5 rounded-full border">
                              {{ getCategoryLabel(post.category) }}
                            </span>
                            <!-- Status Badge -->
                            <span [class]="getStatusBadgeClass(post.status)" class="text-[10px] font-semibold px-2 py-0.5 rounded-full border">
                              {{ post.status }}
                            </span>
                            <!-- Quarter -->
                            @if (post.estimatedQuarter) {
                              <span class="text-[10px] bg-slate-800 text-slate-300 px-2 py-0.5 rounded border border-slate-700 font-mono">
                                📅 {{ post.estimatedQuarter }}
                              </span>
                            }
                          </div>

                          <!-- Interactive Upvote Button with Optimistic UI -->
                          <button
                            (click)="toggleUpvote(post, $event)"
                            [class]="post.hasVoted ? 'bg-indigo-600 text-white border-indigo-400 ring-2 ring-indigo-500/30' : 'bg-slate-800/80 text-slate-300 border-slate-700 hover:bg-slate-700 hover:text-white'"
                            class="flex flex-col items-center justify-center min-w-[44px] h-[52px] rounded-lg border transition-all duration-150 flex-shrink-0 active:scale-95 group-hover:border-slate-600">
                            <span class="text-xs transition-transform group-active:-translate-y-0.5">▲</span>
                            <span class="text-xs font-bold font-mono">{{ post.upvoteCount }}</span>
                          </button>
                        </div>

                        <!-- Title -->
                        <h4 class="text-sm font-semibold text-white group-hover:text-indigo-300 transition-colors line-clamp-2">
                          {{ post.title }}
                        </h4>

                        <!-- Description -->
                        <p class="text-xs text-slate-400 mt-1.5 line-clamp-3 leading-relaxed">
                          {{ post.description }}
                        </p>

                        <!-- Response Note snippet if present -->
                        @if (post.responseNotes) {
                          <div class="mt-2.5 p-2 bg-indigo-950/40 border border-indigo-500/20 rounded-lg text-[11px] text-indigo-200 flex items-start gap-1.5">
                            <span class="text-xs">💬</span>
                            <span class="line-clamp-2 font-medium">Team: {{ post.responseNotes }}</span>
                          </div>
                        }
                      </div>

                      <!-- Card Footer: Author, Impact Score, ARR -->
                      <div class="mt-4 pt-3 border-t border-slate-800/80 flex items-center justify-between text-[11px] text-slate-400">
                        <div class="flex items-center gap-1.5">
                          <span class="h-5 w-5 rounded-full bg-slate-800 border border-slate-700 flex items-center justify-center text-[10px] font-bold text-indigo-400">
                            {{ post.authorName.charAt(0) || 'U' }}
                          </span>
                          <span class="truncate max-w-[110px]">{{ post.authorName }}</span>
                        </div>

                        <div class="flex items-center gap-2">
                          <!-- Impact Score Pill -->
                          <div class="flex items-center gap-1 px-1.5 py-0.5 bg-slate-800 rounded border border-slate-700 font-mono text-purple-300 text-[10px]" title="Impact Score = Votes × log10(ARR + 10)">
                            <span>⚡</span>
                            <span>{{ post.impactScore }}</span>
                          </div>

                          <!-- ARR Badge -->
                          @if (post.accountARR > 0) {
                            <span class="text-[10px] text-emerald-400 font-mono font-medium">
                              \${{ formatNumber(post.accountARR) }}
                            </span>
                          }
                        </div>
                      </div>

                    </div>
                  }
                </div>
              }
            </div>
          } @else {
            <!-- ROADMAP KANBAN VIEW -->
            <div class="max-w-7xl mx-auto grid grid-cols-1 md:grid-cols-3 gap-6">
              
              <!-- Column 1: Planned -->
              <div class="bg-slate-900/50 border border-slate-800 rounded-2xl flex flex-col overflow-hidden max-h-[calc(100vh-250px)]">
                <div class="p-3.5 border-b border-slate-800 bg-slate-900/90 flex items-center justify-between">
                  <div class="flex items-center gap-2">
                    <span class="h-2.5 w-2.5 rounded-full bg-indigo-400"></span>
                    <h3 class="text-xs font-semibold uppercase tracking-wider text-white">Planned</h3>
                    <span class="text-[11px] font-mono bg-indigo-500/20 text-indigo-300 border border-indigo-500/30 px-1.5 py-0.2 rounded-full">
                      {{ plannedPosts().length }}
                    </span>
                  </div>
                  <span class="text-[11px] font-mono text-slate-400">
                    \${{ formatNumber(columnArr(plannedPosts())) }} ARR
                  </span>
                </div>

                <div class="p-3 overflow-y-auto space-y-3 flex-1">
                  @for (post of plannedPosts(); track post.id) {
                    <div
                      (click)="openDetailModal(post)"
                      class="bg-slate-900/90 hover:bg-slate-850 border border-slate-800 hover:border-indigo-500/50 rounded-xl p-3 cursor-pointer shadow-sm transition">
                      <div class="flex items-start justify-between gap-2 mb-1.5">
                        <span [class]="getCategoryBadgeClass(post.category)" class="text-[9px] font-semibold px-1.5 py-0.5 rounded border">
                          {{ getCategoryLabel(post.category) }}
                        </span>
                        <div class="flex items-center gap-1 text-[11px] font-mono text-indigo-300">
                          <span>▲</span>
                          <span>{{ post.upvoteCount }}</span>
                        </div>
                      </div>
                      <h4 class="text-xs font-semibold text-white line-clamp-2">{{ post.title }}</h4>
                      <p class="text-[11px] text-slate-400 mt-1 line-clamp-2 leading-relaxed">{{ post.description }}</p>
                      
                      <div class="mt-3 pt-2 border-t border-slate-800/80 flex items-center justify-between text-[10px] text-slate-400">
                        <span>📅 {{ post.estimatedQuarter || 'TBD' }}</span>
                        <span class="font-mono text-purple-300">⚡ {{ post.impactScore }}</span>
                      </div>
                    </div>
                  }
                </div>
              </div>

              <!-- Column 2: In Progress -->
              <div class="bg-slate-900/50 border border-slate-800 rounded-2xl flex flex-col overflow-hidden max-h-[calc(100vh-250px)]">
                <div class="p-3.5 border-b border-slate-800 bg-slate-900/90 flex items-center justify-between">
                  <div class="flex items-center gap-2">
                    <span class="h-2.5 w-2.5 rounded-full bg-cyan-400 animate-pulse"></span>
                    <h3 class="text-xs font-semibold uppercase tracking-wider text-white">In Progress</h3>
                    <span class="text-[11px] font-mono bg-cyan-500/20 text-cyan-300 border border-cyan-500/30 px-1.5 py-0.2 rounded-full">
                      {{ inProgressPosts().length }}
                    </span>
                  </div>
                  <span class="text-[11px] font-mono text-slate-400">
                    \${{ formatNumber(columnArr(inProgressPosts())) }} ARR
                  </span>
                </div>

                <div class="p-3 overflow-y-auto space-y-3 flex-1">
                  @for (post of inProgressPosts(); track post.id) {
                    <div
                      (click)="openDetailModal(post)"
                      class="bg-slate-900/90 hover:bg-slate-850 border border-slate-800 hover:border-cyan-500/50 rounded-xl p-3 cursor-pointer shadow-sm transition">
                      <div class="flex items-start justify-between gap-2 mb-1.5">
                        <span [class]="getCategoryBadgeClass(post.category)" class="text-[9px] font-semibold px-1.5 py-0.5 rounded border">
                          {{ getCategoryLabel(post.category) }}
                        </span>
                        <div class="flex items-center gap-1 text-[11px] font-mono text-cyan-300">
                          <span>▲</span>
                          <span>{{ post.upvoteCount }}</span>
                        </div>
                      </div>
                      <h4 class="text-xs font-semibold text-white line-clamp-2">{{ post.title }}</h4>
                      <p class="text-[11px] text-slate-400 mt-1 line-clamp-2 leading-relaxed">{{ post.description }}</p>

                      @if (post.responseNotes) {
                        <div class="mt-2 p-1.5 bg-cyan-950/40 border border-cyan-500/20 rounded text-[10px] text-cyan-200 line-clamp-2">
                          ⚡ {{ post.responseNotes }}
                        </div>
                      }
                      
                      <div class="mt-3 pt-2 border-t border-slate-800/80 flex items-center justify-between text-[10px] text-slate-400">
                        <span>📅 {{ post.estimatedQuarter || 'Current' }}</span>
                        <span class="font-mono text-purple-300">⚡ {{ post.impactScore }}</span>
                      </div>
                    </div>
                  }
                </div>
              </div>

              <!-- Column 3: Completed / Shipped -->
              <div class="bg-slate-900/50 border border-slate-800 rounded-2xl flex flex-col overflow-hidden max-h-[calc(100vh-250px)]">
                <div class="p-3.5 border-b border-slate-800 bg-slate-900/90 flex items-center justify-between">
                  <div class="flex items-center gap-2">
                    <span class="h-2.5 w-2.5 rounded-full bg-emerald-400"></span>
                    <h3 class="text-xs font-semibold uppercase tracking-wider text-white">Completed / Shipped</h3>
                    <span class="text-[11px] font-mono bg-emerald-500/20 text-emerald-300 border border-emerald-500/30 px-1.5 py-0.2 rounded-full">
                      {{ completedPosts().length }}
                    </span>
                  </div>
                  <span class="text-[11px] font-mono text-emerald-400">
                    \${{ formatNumber(columnArr(completedPosts())) }} Delivered
                  </span>
                </div>

                <div class="p-3 overflow-y-auto space-y-3 flex-1">
                  @for (post of completedPosts(); track post.id) {
                    <div
                      (click)="openDetailModal(post)"
                      class="bg-slate-900/90 hover:bg-slate-850 border border-slate-800 hover:border-emerald-500/50 rounded-xl p-3 cursor-pointer shadow-sm transition">
                      <div class="flex items-start justify-between gap-2 mb-1.5">
                        <span [class]="getCategoryBadgeClass(post.category)" class="text-[9px] font-semibold px-1.5 py-0.5 rounded border">
                          {{ getCategoryLabel(post.category) }}
                        </span>
                        <span class="text-[10px] text-emerald-400 font-medium flex items-center gap-1">
                          ✓ Live
                        </span>
                      </div>
                      <h4 class="text-xs font-semibold text-white line-clamp-2">{{ post.title }}</h4>
                      <p class="text-[11px] text-slate-400 mt-1 line-clamp-2 leading-relaxed">{{ post.description }}</p>

                      <div class="mt-3 pt-2 border-t border-slate-800/80 flex items-center justify-between text-[10px] text-slate-400">
                        <span>Released {{ formatDate(post.completedAt || post.updatedAt) }}</span>
                        <span class="font-mono text-emerald-300">👍 {{ post.upvoteCount }} votes</span>
                      </div>
                    </div>
                  }
                </div>
              </div>

            </div>
          }
        </div>
      </main>

      <!-- 3. Detail & Discussion Modal -->
      @if (selectedPost(); as post) {
        <div class="fixed inset-0 z-50 bg-black/70 backdrop-blur-sm flex items-center justify-center p-4">
          <div class="bg-slate-900 border border-slate-800 rounded-2xl w-full max-w-2xl max-h-[90vh] flex flex-col shadow-2xl overflow-hidden animate-in fade-in zoom-in-95 duration-150">
            
            <!-- Modal Header -->
            <div class="p-5 border-b border-slate-800 flex items-start justify-between gap-4 bg-slate-900/80">
              <div class="flex-1">
                <div class="flex items-center gap-2 mb-2 flex-wrap">
                  <span [class]="getCategoryBadgeClass(post.category)" class="text-[10px] font-semibold px-2 py-0.5 rounded-full border">
                    {{ getCategoryLabel(post.category) }}
                  </span>
                  <span [class]="getStatusBadgeClass(post.status)" class="text-[10px] font-semibold px-2 py-0.5 rounded-full border">
                    {{ post.status }}
                  </span>
                  @if (post.estimatedQuarter) {
                    <span class="text-[10px] bg-slate-800 text-slate-300 px-2 py-0.5 rounded border border-slate-700 font-mono">
                      📅 {{ post.estimatedQuarter }}
                    </span>
                  }
                  <span class="text-[10px] bg-purple-500/20 text-purple-300 px-2 py-0.5 rounded font-mono border border-purple-500/30">
                    ⚡ Impact Score: {{ post.impactScore }}
                  </span>
                </div>
                <h3 class="text-base font-bold text-white">{{ post.title }}</h3>
                <p class="text-xs text-slate-400 mt-1">
                  Suggested by <strong class="text-slate-200">{{ post.authorName }}</strong> · {{ formatDate(post.createdAt) }}
                </p>
              </div>

              <!-- Close Button -->
              <button
                (click)="closeDetailModal()"
                class="text-slate-400 hover:text-white p-1 rounded-lg hover:bg-slate-800 transition text-sm">
                ✕
              </button>
            </div>

            <!-- Modal Content (Scrollable) -->
            <div class="p-5 overflow-y-auto space-y-6 flex-1 text-xs">
              
              <!-- Description Section -->
              <div class="space-y-2">
                <h4 class="text-[11px] font-semibold uppercase tracking-wider text-slate-400">Description & Business Case</h4>
                <div class="p-3.5 bg-slate-950/70 border border-slate-800 rounded-xl text-slate-200 leading-relaxed whitespace-pre-line">
                  {{ post.description }}
                </div>
              </div>

              <!-- Admin Status & Response Section (Enabled when isAdminMode or for viewing) -->
              @if (isAdminMode()) {
                <div class="p-4 bg-gradient-to-r from-indigo-950/30 via-purple-950/20 to-slate-900 border border-indigo-500/30 rounded-xl space-y-3">
                  <div class="flex items-center justify-between">
                    <span class="text-xs font-semibold text-indigo-300 flex items-center gap-1.5">
                      👑 Product Management Admin Controls
                    </span>
                    <span class="text-[10px] text-slate-400">State Machine Transition</span>
                  </div>

                  <div class="grid grid-cols-2 gap-3">
                    <div>
                      <label class="text-[11px] text-slate-400 block mb-1">Roadmap Status</label>
                      <select
                        [(ngModel)]="adminStatusEdit"
                        class="w-full bg-slate-900 border border-slate-700 text-white rounded-lg px-2.5 py-1.5 text-xs focus:outline-none focus:border-indigo-500">
                        <option value="Submitted">Submitted (Inbox)</option>
                        <option value="UnderReview">Under Review</option>
                        <option value="Planned">Planned</option>
                        <option value="InProgress">In Progress</option>
                        <option value="Completed">Completed / Shipped</option>
                        <option value="Declined">Declined</option>
                      </select>
                    </div>

                    <div>
                      <label class="text-[11px] text-slate-400 block mb-1">Target Quarter</label>
                      <input
                        type="text"
                        [(ngModel)]="adminQuarterEdit"
                        placeholder="e.g. Q1 2027"
                        class="w-full bg-slate-900 border border-slate-700 text-white rounded-lg px-2.5 py-1.5 text-xs focus:outline-none focus:border-indigo-500" />
                    </div>
                  </div>

                  <div>
                    <label class="text-[11px] text-slate-400 block mb-1">Official Team Response Note</label>
                    <textarea
                      [(ngModel)]="adminNoteEdit"
                      rows="2"
                      placeholder="Public note visible on board & email alert (e.g. In active sprint review)..."
                      class="w-full bg-slate-900 border border-slate-700 text-white rounded-lg p-2 text-xs focus:outline-none focus:border-indigo-500"></textarea>
                  </div>

                  <div class="flex justify-end">
                    <button
                      (click)="saveAdminStatusChange(post)"
                      class="px-3 py-1.5 bg-indigo-600 hover:bg-indigo-500 text-white font-medium rounded-lg text-xs shadow transition">
                      Save Status & Notes
                    </button>
                  </div>
                </div>
              } @else if (post.responseNotes) {
                <!-- Public View of Official Team Response -->
                <div class="p-3.5 bg-indigo-950/40 border border-indigo-500/30 rounded-xl space-y-1">
                  <span class="text-[11px] font-semibold text-indigo-300 flex items-center gap-1.5">
                    🏢 Official SGrowing Product Team Response
                  </span>
                  <p class="text-xs text-indigo-100 leading-relaxed">{{ post.responseNotes }}</p>
                </div>
              }

              <!-- Comments & Discussion Stream -->
              <div class="space-y-3">
                <h4 class="text-[11px] font-semibold uppercase tracking-wider text-slate-400 flex items-center justify-between">
                  <span>Community Discussion</span>
                  <span class="text-slate-500 font-mono">{{ commentsList().length }} comments</span>
                </h4>

                <!-- Existing Comments -->
                <div class="space-y-2.5 max-h-56 overflow-y-auto pr-1">
                  @if (commentsList().length === 0) {
                    <p class="text-[11px] text-slate-500 italic py-2">No discussion comments yet. Be the first to share your feedback or use-case!</p>
                  } @else {
                    @for (comment of commentsList(); track comment.id) {
                      <div
                        [class]="comment.isOfficialTeamResponse ? 'bg-indigo-950/40 border-indigo-500/40' : 'bg-slate-950/60 border-slate-800'"
                        class="p-3 rounded-xl border space-y-1">
                        <div class="flex items-center justify-between">
                          <div class="flex items-center gap-2">
                            <span class="font-semibold text-white text-[11px]">{{ comment.authorName }}</span>
                            @if (comment.isOfficialTeamResponse) {
                              <span class="text-[9px] font-medium bg-emerald-500/20 text-emerald-300 border border-emerald-500/30 px-1.5 py-0.2 rounded-full">
                                ✓ Official Team
                              </span>
                            }
                          </div>
                          <span class="text-[10px] text-slate-500">{{ formatDate(comment.createdAt) }}</span>
                        </div>
                        <p class="text-slate-300 leading-relaxed text-xs">{{ comment.content }}</p>
                      </div>
                    }
                  }
                </div>

                <!-- Post Comment Form -->
                <div class="pt-2 border-t border-slate-800 space-y-2">
                  <div class="flex gap-2">
                    <input
                      type="text"
                      [(ngModel)]="newCommentAuthor"
                      placeholder="Your Name / Title"
                      class="w-1/3 bg-slate-950 border border-slate-800 text-white rounded-lg px-2.5 py-1.5 text-xs focus:outline-none focus:border-indigo-500" />
                    <input
                      type="text"
                      [(ngModel)]="newCommentText"
                      (keyup.enter)="submitComment(post)"
                      placeholder="Add to the conversation or specify your merchant workflow..."
                      class="flex-1 bg-slate-950 border border-slate-800 text-white rounded-lg px-2.5 py-1.5 text-xs focus:outline-none focus:border-indigo-500" />
                    <button
                      (click)="submitComment(post)"
                      [disabled]="!newCommentText.trim()"
                      class="px-3 py-1.5 bg-indigo-600 hover:bg-indigo-500 disabled:opacity-40 text-white font-medium rounded-lg text-xs transition">
                      Comment
                    </button>
                  </div>
                </div>

              </div>

            </div>

            <!-- Modal Footer -->
            <div class="p-4 border-t border-slate-800 bg-slate-900/90 flex items-center justify-between">
              <!-- Upvote action inside modal -->
              <button
                (click)="toggleUpvote(post)"
                [class]="post.hasVoted ? 'bg-indigo-600 text-white' : 'bg-slate-800 text-slate-200 hover:bg-slate-700'"
                class="flex items-center gap-2 px-3 py-1.5 rounded-lg text-xs font-semibold transition border border-slate-700">
                <span>{{ post.hasVoted ? '✓ Upvoted' : '▲ Upvote Idea' }}</span>
                <span class="font-mono bg-black/20 px-1.5 py-0.5 rounded text-[10px]">{{ post.upvoteCount }}</span>
              </button>

              <button
                (click)="closeDetailModal()"
                class="px-4 py-1.5 bg-slate-800 hover:bg-slate-700 text-white rounded-lg text-xs font-medium transition">
                Close
              </button>
            </div>

          </div>
        </div>
      }

      <!-- 4. Submit Idea Modal with Live Preview -->
      @if (isSubmitModalOpen()) {
        <div class="fixed inset-0 z-50 bg-black/75 backdrop-blur-sm flex items-center justify-center p-4">
          <div class="bg-slate-900 border border-slate-800 rounded-2xl w-full max-w-4xl max-h-[92vh] flex flex-col shadow-2xl overflow-hidden animate-in fade-in zoom-in-95 duration-150">
            
            <!-- Modal Header -->
            <div class="p-5 border-b border-slate-800 flex items-center justify-between bg-slate-900/90">
              <div class="flex items-center gap-2.5">
                <div class="h-8 w-8 rounded-lg bg-indigo-600/30 text-indigo-400 flex items-center justify-center text-sm border border-indigo-500/30">
                  ✨
                </div>
                <div>
                  <h3 class="text-sm font-bold text-white">Submit New Feature Idea or Improvement</h3>
                  <p class="text-xs text-slate-400">Your suggestion will be visible on the public roadmap with revenue weighting.</p>
                </div>
              </div>
              <button
                (click)="closeSubmitModal()"
                class="text-slate-400 hover:text-white p-1 rounded-lg hover:bg-slate-800 transition text-sm">
                ✕
              </button>
            </div>

            <!-- 2-Column Layout: Form on Left, Live Preview Card on Right -->
            <div class="p-6 overflow-y-auto grid grid-cols-1 md:grid-cols-2 gap-6 flex-1 text-xs">
              
              <!-- Left Column: Inputs -->
              <div class="space-y-4">
                <div>
                  <label class="block text-slate-300 font-medium mb-1">Feature Title <span class="text-rose-400">*</span></label>
                  <input
                    type="text"
                    [(ngModel)]="ideaForm.title"
                    placeholder="e.g. Automated Courier Remittance Splitter"
                    class="w-full bg-slate-950 border border-slate-800 text-white rounded-lg p-2.5 text-xs focus:outline-none focus:border-indigo-500" />
                </div>

                <div class="grid grid-cols-2 gap-3">
                  <div>
                    <label class="block text-slate-300 font-medium mb-1">Category</label>
                    <select
                      [(ngModel)]="ideaForm.category"
                      class="w-full bg-slate-950 border border-slate-800 text-white rounded-lg p-2 text-xs focus:outline-none focus:border-indigo-500">
                      <option value="FeatureRequest">Feature Request</option>
                      <option value="Integration">Integration</option>
                      <option value="UxImprovement">UX Improvement</option>
                      <option value="BugReport">Bug Report</option>
                    </select>
                  </div>

                  <div>
                    <label class="block text-slate-300 font-medium mb-1">Target Quarter</label>
                    <input
                      type="text"
                      [(ngModel)]="ideaForm.estimatedQuarter"
                      placeholder="e.g. Q1 2027"
                      class="w-full bg-slate-950 border border-slate-800 text-white rounded-lg p-2 text-xs focus:outline-none focus:border-indigo-500" />
                  </div>
                </div>

                <div>
                  <label class="block text-slate-300 font-medium mb-1">Detailed Description & Use Case <span class="text-rose-400">*</span></label>
                  <textarea
                    [(ngModel)]="ideaForm.description"
                    rows="4"
                    placeholder="Describe how this feature works, what merchants benefit, and the problem it resolves..."
                    class="w-full bg-slate-950 border border-slate-800 text-white rounded-lg p-2.5 text-xs focus:outline-none focus:border-indigo-500 leading-relaxed"></textarea>
                </div>

                <div class="grid grid-cols-2 gap-3">
                  <div>
                    <label class="block text-slate-300 font-medium mb-1">Your Name</label>
                    <input
                      type="text"
                      [(ngModel)]="ideaForm.authorName"
                      placeholder="e.g. Sami Al-Mansoor"
                      class="w-full bg-slate-950 border border-slate-800 text-white rounded-lg p-2 text-xs focus:outline-none focus:border-indigo-500" />
                  </div>

                  <div>
                    <label class="block text-slate-300 font-medium mb-1">Your Work Email</label>
                    <input
                      type="email"
                      [(ngModel)]="ideaForm.authorEmail"
                      placeholder="sami@merchant.com"
                      class="w-full bg-slate-950 border border-slate-800 text-white rounded-lg p-2 text-xs focus:outline-none focus:border-indigo-500" />
                  </div>
                </div>

                <div>
                  <label class="block text-slate-300 font-medium mb-1">
                    Annual Account Revenue (ARR in USD)
                    <span class="text-[10px] text-slate-400 font-normal">Weights impact formula</span>
                  </label>
                  <div class="relative">
                    <span class="absolute inset-y-0 left-0 pl-3 flex items-center text-slate-400 font-mono">$</span>
                    <input
                      type="number"
                      [(ngModel)]="ideaForm.accountARR"
                      min="0"
                      step="5000"
                      class="w-full pl-7 pr-3 py-2 bg-slate-950 border border-slate-800 text-white rounded-lg text-xs font-mono focus:outline-none focus:border-indigo-500" />
                  </div>
                </div>
              </div>

              <!-- Right Column: Dynamic Live Preview Card -->
              <div class="flex flex-col">
                <div class="flex items-center justify-between mb-2">
                  <span class="text-[11px] font-semibold uppercase tracking-wider text-slate-400">
                    Live Roadmap Card Preview
                  </span>
                  <span class="text-[10px] text-indigo-400 font-medium">Real-time simulation</span>
                </div>

                <div class="flex-1 bg-slate-950/80 border border-indigo-500/30 rounded-2xl p-5 flex flex-col justify-between shadow-inner">
                  <div>
                    <div class="flex items-start justify-between gap-3 mb-3">
                      <div class="flex items-center gap-1.5 flex-wrap">
                        <span [class]="getCategoryBadgeClass(ideaForm.category)" class="text-[10px] font-semibold px-2 py-0.5 rounded-full border">
                          {{ getCategoryLabel(ideaForm.category) }}
                        </span>
                        <span class="text-[10px] bg-slate-800 text-slate-300 px-2 py-0.5 rounded-full border border-slate-700">
                          Submitted
                        </span>
                        @if (ideaForm.estimatedQuarter) {
                          <span class="text-[10px] bg-slate-800 text-slate-300 px-2 py-0.5 rounded border border-slate-700 font-mono">
                            📅 {{ ideaForm.estimatedQuarter }}
                          </span>
                        }
                      </div>

                      <!-- Simulated Upvote Button (1 author vote) -->
                      <div class="flex flex-col items-center justify-center min-w-[44px] h-[50px] rounded-lg border bg-indigo-600/30 text-indigo-300 border-indigo-500/50">
                        <span class="text-xs">▲</span>
                        <span class="text-xs font-bold font-mono">1</span>
                      </div>
                    </div>

                    <h4 class="text-sm font-bold text-white">
                      {{ ideaForm.title || 'Untitled Feature Request...' }}
                    </h4>

                    <p class="text-xs text-slate-400 mt-2 line-clamp-4 leading-relaxed">
                      {{ ideaForm.description || 'Enter a description on the left to see how your submission will appear to the community and engineering council.' }}
                    </p>
                  </div>

                  <!-- Preview Card Footer -->
                  <div class="mt-6 pt-3 border-t border-slate-800/80 flex items-center justify-between text-[11px] text-slate-400">
                    <div class="flex items-center gap-1.5">
                      <span class="h-5 w-5 rounded-full bg-indigo-600/40 border border-indigo-500 flex items-center justify-center text-[10px] font-bold text-white">
                        {{ ideaForm.authorName.charAt(0) || 'A' }}
                      </span>
                      <span class="truncate max-w-[120px]">{{ ideaForm.authorName || 'Author' }}</span>
                    </div>

                    <div class="flex items-center gap-2">
                      <div class="flex items-center gap-1 px-1.5 py-0.5 bg-slate-800 rounded border border-slate-700 font-mono text-purple-300 text-[10px]">
                        <span>⚡</span>
                        <span>{{ previewImpactScore() }}</span>
                      </div>
                      <span class="text-[10px] text-emerald-400 font-mono font-medium">
                        \${{ formatNumber(ideaForm.accountARR) }}
                      </span>
                    </div>
                  </div>
                </div>

                <div class="mt-3 p-2 bg-slate-900 border border-slate-800 rounded-lg text-[10px] text-slate-400">
                  ℹ️ Formula preview: <code class="text-indigo-300 font-mono">1 vote × log10(\${{ ideaForm.accountARR }} + 10) = {{ previewImpactScore() }} pts</code>.
                </div>
              </div>

            </div>

            <!-- Modal Actions -->
            <div class="p-4 border-t border-slate-800 bg-slate-900/90 flex items-center justify-end gap-3">
              <button
                (click)="closeSubmitModal()"
                class="px-4 py-2 bg-slate-800 hover:bg-slate-700 text-white rounded-lg text-xs font-medium transition">
                Cancel
              </button>
              <button
                (click)="submitNewIdea()"
                [disabled]="!ideaForm.title.trim() || !ideaForm.description.trim()"
                class="px-5 py-2 bg-gradient-to-r from-indigo-500 to-purple-600 hover:from-indigo-600 hover:to-purple-700 disabled:opacity-40 text-white text-xs font-semibold rounded-lg shadow-md transition">
                🚀 Publish Suggestion to Roadmap
              </button>
            </div>

          </div>
        </div>
      }

    </div>
  `
})
export class FeedbackBoardStudioComponent implements OnInit {
  private http = inject(HttpClient, { optional: true });

  // Core State Signals
  readonly currentTenantId = signal<string>('00000000-0000-0000-0000-000000000001');
  readonly currentUserEmail = signal<string>('merchant.founder@gcc-growth.io');
  readonly currentUserName = signal<string>('Faisal Al-Kindi');

  readonly isAdminMode = signal<boolean>(false);
  readonly viewMode = signal<ViewModeType>('board');

  // Filtering & Sorting Signals
  readonly selectedCategory = signal<string>('all');
  readonly selectedStatus = signal<string>('all');
  readonly searchQuery = signal<string>('');
  readonly sortBy = signal<SortOptionType>('impact');

  // Modals & Active Selections
  readonly isSubmitModalOpen = signal<boolean>(false);
  readonly selectedPost = signal<FeedbackPostUi | null>(null);
  readonly commentsList = signal<FeedbackCommentUi[]>([]);

  // Admin In-Modal Edit State
  adminStatusEdit: FeedbackStatusType = 'Submitted';
  adminQuarterEdit = '';
  adminNoteEdit = '';

  // New Comment Input
  newCommentAuthor = 'Faisal Al-Kindi';
  newCommentText = '';

  // New Idea Form State
  ideaForm: NewIdeaForm = {
    title: '',
    description: '',
    category: 'FeatureRequest',
    authorName: 'Faisal Al-Kindi',
    authorEmail: 'merchant.founder@gcc-growth.io',
    accountARR: 45000,
    estimatedQuarter: 'Q1 2027'
  };

  // Main Posts Data Signal (Initial Rich Realistic GCC Seeding)
  readonly posts = signal<FeedbackPostUi[]>([
    {
      id: 'f1111111-1111-1111-1111-111111111111',
      tenantId: '00000000-0000-0000-0000-000000000001',
      title: 'Direct WhatsApp Business Catalog Sync',
      description: 'Enable 2-way real-time product catalog, inventory level, and price synchronization directly with Meta WhatsApp Business Cloud API for high-converting social commerce.',
      category: 'Integration',
      status: 'InProgress',
      authorEmail: 'tariq.almansoor@riyadhretail.com',
      authorName: 'Tariq Al-Mansoor',
      accountId: 'ACC-SA-8821',
      accountARR: 142000,
      upvoteCount: 38,
      impactScore: calculateImpactScore(38, 142000),
      createdAt: new Date(Date.now() - 18 * 86400000).toISOString(),
      updatedAt: new Date(Date.now() - 2 * 3600000).toISOString(),
      completedAt: null,
      estimatedQuarter: 'Q4 2026',
      responseNotes: 'Sprint 42 active development. Meta Cloud API webhooks and rate limiters tested. Target rollout mid-November.',
      hasVoted: true
    },
    {
      id: 'f2222222-2222-2222-2222-222222222222',
      tenantId: '00000000-0000-0000-0000-000000000001',
      title: 'ZATCA E-Invoicing Phase 2 QR Integration',
      description: 'Native cryptographic stamp signing, CSID renewal automation, and embedded Phase 2 compliant B2B/B2C QR code generation on all checkout and invoice PDFs.',
      category: 'FeatureRequest',
      status: 'Completed',
      authorEmail: 'layla.finance@jeddahlogistics.sa',
      authorName: 'Layla Al-Harbi',
      accountId: 'ACC-SA-1044',
      accountARR: 285000,
      upvoteCount: 64,
      impactScore: calculateImpactScore(64, 285000),
      createdAt: new Date(Date.now() - 45 * 86400000).toISOString(),
      updatedAt: new Date(Date.now() - 10 * 86400000).toISOString(),
      completedAt: new Date(Date.now() - 10 * 86400000).toISOString(),
      estimatedQuarter: 'Q3 2026',
      responseNotes: 'Successfully certified and deployed to production. Integrated with Fatoora portal.',
      hasVoted: false
    },
    {
      id: 'f3333333-3333-3333-3333-333333333333',
      tenantId: '00000000-0000-0000-0000-000000000001',
      title: 'Zapier & Make.com Webhook Connectors',
      description: 'Certified app directory connectors on Zapier and Make (Integromat) for bidirectional trigger dispatching on order status, payment receipt, and lead qualification.',
      category: 'Integration',
      status: 'Planned',
      authorEmail: 'khalid@gulfgrowth.io',
      authorName: 'Khalid Bin Rashid',
      accountId: 'ACC-AE-4091',
      accountARR: 68000,
      upvoteCount: 27,
      impactScore: calculateImpactScore(27, 68000),
      createdAt: new Date(Date.now() - 12 * 86400000).toISOString(),
      updatedAt: new Date(Date.now() - 86400000).toISOString(),
      completedAt: null,
      estimatedQuarter: 'Q1 2027',
      responseNotes: 'Architecture review passed. OpenAPI schema generation and partner developer accounts configured.',
      hasVoted: false
    },
    {
      id: 'f4444444-4444-4444-4444-444444444444',
      tenantId: '00000000-0000-0000-0000-000000000001',
      title: 'Real-Time Courier Remittance Settlement Radar',
      description: 'Automated cashflow reconciliation tracker tracking pending cash-on-delivery (COD) disbursements across SMSA, Aramex, and J&T Express with SLA dispute warnings.',
      category: 'FeatureRequest',
      status: 'InProgress',
      authorEmail: 'omair.operations@sharqiah-direct.com',
      authorName: 'Omair Farooq',
      accountId: 'ACC-SA-7120',
      accountARR: 195000,
      upvoteCount: 49,
      impactScore: calculateImpactScore(49, 195000),
      createdAt: new Date(Date.now() - 22 * 86400000).toISOString(),
      updatedAt: new Date(Date.now() - 6 * 3600000).toISOString(),
      completedAt: null,
      estimatedQuarter: 'Q4 2026',
      responseNotes: 'Remittance webhook parsing implemented. Beta testing with 5 high-volume COD merchants.',
      hasVoted: true
    },
    {
      id: 'f5555555-5555-5555-5555-555555555555',
      tenantId: '00000000-0000-0000-0000-000000000001',
      title: 'Multi-Currency Dynamic Forex Checkout',
      description: 'Allow cross-border shoppers across GCC (SAR, AED, KWD, BHD, QAR, OMR) to browse and pay in local currencies with automatic daily central bank FX pegging.',
      category: 'FeatureRequest',
      status: 'Planned',
      authorEmail: 'sara.k@dubaimarketplace.ae',
      authorName: 'Sara Kazim',
      accountId: 'ACC-AE-3190',
      accountARR: 110000,
      upvoteCount: 31,
      impactScore: calculateImpactScore(31, 110000),
      createdAt: new Date(Date.now() - 15 * 86400000).toISOString(),
      updatedAt: new Date(Date.now() - 2 * 86400000).toISOString(),
      completedAt: null,
      estimatedQuarter: 'Q1 2027',
      responseNotes: 'Scheduled for Horizon 32 after payment gateway multi-acquirer routing upgrades.',
      hasVoted: false
    },
    {
      id: 'f6666666-6666-6666-6666-666666666666',
      tenantId: '00000000-0000-0000-0000-000000000001',
      title: 'Automated Abandoned Cart Voice Dialer',
      description: 'Trigger proactive AI phone dialer calls within 15 minutes of checkout drop-off offering instant discount incentives and addressing delivery hesitation.',
      category: 'UxImprovement',
      status: 'Planned',
      authorEmail: 'marwan@turboquick.sa',
      authorName: 'Marwan Al-Sayed',
      accountId: 'ACC-SA-5542',
      accountARR: 54000,
      upvoteCount: 19,
      impactScore: calculateImpactScore(19, 54000),
      createdAt: new Date(Date.now() - 8 * 86400000).toISOString(),
      updatedAt: new Date(Date.now() - 86400000).toISOString(),
      completedAt: null,
      estimatedQuarter: 'Q1 2027',
      responseNotes: 'Queued for research. Telephony compliance and do-not-call filtering required.',
      hasVoted: false
    },
    {
      id: 'f7777777-7777-7777-7777-777777777777',
      tenantId: '00000000-0000-0000-0000-000000000001',
      title: 'Granular Role-Based Access Control (RBAC) & Audit Log Export',
      description: 'Custom permission sets for warehouse dispatchers, financial auditors, and customer success reps with cryptographically verifiable tamper-evident export.',
      category: 'FeatureRequest',
      status: 'UnderReview',
      authorEmail: 'nasser.ciso@qatarretail.qa',
      authorName: 'Nasser Al-Thani',
      accountId: 'ACC-QA-9011',
      accountARR: 340000,
      upvoteCount: 22,
      impactScore: calculateImpactScore(22, 340000),
      createdAt: new Date(Date.now() - 6 * 86400000).toISOString(),
      updatedAt: new Date(Date.now() - 12 * 3600000).toISOString(),
      completedAt: null,
      estimatedQuarter: 'Q2 2027',
      responseNotes: 'Under enterprise security review with compliance board.',
      hasVoted: false
    },
    {
      id: 'f8888888-8888-8888-8888-888888888888',
      tenantId: '00000000-0000-0000-0000-000000000001',
      title: 'Dark Mode Contrast Fix in Mobile Safari Navigation',
      description: 'Fix sticky header backdrop blur and contrast degradation on iOS Safari when scrolling long catalog listings in dark mode.',
      category: 'BugReport',
      status: 'Completed',
      authorEmail: 'hassan.dev@beirutboutique.com',
      authorName: 'Hassan Daouk',
      accountId: 'ACC-LB-2201',
      accountARR: 28000,
      upvoteCount: 12,
      impactScore: calculateImpactScore(12, 28000),
      createdAt: new Date(Date.now() - 30 * 86400000).toISOString(),
      updatedAt: new Date(Date.now() - 20 * 86400000).toISOString(),
      completedAt: new Date(Date.now() - 20 * 86400000).toISOString(),
      estimatedQuarter: 'Q3 2026',
      responseNotes: 'Resolved in release v3.4.1 via Tailwind backdrop-blur-md CSS update.',
      hasVoted: false
    }
  ]);

  // Categories and Status Filter Definitions
  readonly categories = [
    { id: 'all', label: 'All Categories' },
    { id: 'FeatureRequest', label: 'Features' },
    { id: 'Integration', label: 'Integrations' },
    { id: 'UxImprovement', label: 'UX' },
    { id: 'BugReport', label: 'Bugs' }
  ];

  readonly statuses = [
    { id: 'all', label: 'All Statuses' },
    { id: 'Submitted', label: 'Submitted' },
    { id: 'UnderReview', label: 'Under Review' },
    { id: 'Planned', label: 'Planned' },
    { id: 'InProgress', label: 'In Progress' },
    { id: 'Completed', label: 'Shipped' }
  ];

  // Computed KPIs
  readonly totalIdeas = computed(() => this.posts().length);
  readonly totalVotes = computed(() => this.posts().reduce((sum, p) => sum + p.upvoteCount, 0));
  readonly completedVelocity = computed(() => this.posts().filter(p => p.status === 'Completed').length);
  readonly totalArrDemand = computed(() =>
    this.posts()
      .filter(p => p.status !== 'Declined')
      .reduce((sum, p) => sum + p.accountARR, 0)
  );

  // Filtered & Sorted Posts List
  readonly filteredPosts = computed(() => {
    let result = [...this.posts()];

    // 1. Category Filter
    const cat = this.selectedCategory();
    if (cat !== 'all') {
      result = result.filter(p => p.category === cat);
    }

    // 2. Status Filter
    const st = this.selectedStatus();
    if (st !== 'all') {
      result = result.filter(p => p.status === st);
    }

    // 3. Search Query Filter
    const q = this.searchQuery().trim().toLowerCase();
    if (q) {
      result = result.filter(p =>
        p.title.toLowerCase().includes(q) ||
        p.description.toLowerCase().includes(q) ||
        p.authorName.toLowerCase().includes(q) ||
        p.estimatedQuarter.toLowerCase().includes(q)
      );
    }

    // 4. Sort Mode
    const sort = this.sortBy();
    if (sort === 'impact') {
      result.sort((a, b) => b.impactScore - a.impactScore || b.upvoteCount - a.upvoteCount);
    } else if (sort === 'top') {
      result.sort((a, b) => b.upvoteCount - a.upvoteCount || b.impactScore - a.impactScore);
    } else if (sort === 'newest') {
      result.sort((a, b) => new Date(b.createdAt).getTime() - new Date(a.createdAt).getTime());
    }

    return result;
  });

  // Kanban Columns Computed Lists
  readonly plannedPosts = computed(() =>
    this.posts()
      .filter(p => p.status === 'Planned')
      .sort((a, b) => b.impactScore - a.impactScore)
  );

  readonly inProgressPosts = computed(() =>
    this.posts()
      .filter(p => p.status === 'InProgress')
      .sort((a, b) => b.impactScore - a.impactScore)
  );

  readonly completedPosts = computed(() =>
    this.posts()
      .filter(p => p.status === 'Completed')
      .sort((a, b) => new Date(b.completedAt || b.updatedAt).getTime() - new Date(a.completedAt || a.updatedAt).getTime())
  );

  // Live preview impact score calculation for modal
  readonly previewImpactScore = computed(() => {
    return calculateImpactScore(1, this.ideaForm.accountARR || 0);
  });

  ngOnInit(): void {
    this.fetchPostsFromBackend();
  }

  fetchPostsFromBackend(): void {
    if (!this.http) return;

    this.http.get<FeedbackPostUi[]>(`/api/v1/feedback/board/posts?tenantId=${this.currentTenantId()}`).subscribe({
      next: (data) => {
        if (data && data.length > 0) {
          this.posts.set(data);
        }
      },
      error: () => {
        // Gracefully keep pre-seeded data when backend is not actively serving HTTP
      }
    });
  }

  toggleAdminMode(): void {
    this.isAdminMode.update(val => !val);
  }

  setViewMode(mode: ViewModeType): void {
    this.viewMode.set(mode);
  }

  // Optimistic UI Upvoting
  toggleUpvote(post: FeedbackPostUi, event?: Event): void {
    event?.stopPropagation();

    const currentlyVoted = post.hasVoted ?? false;
    const nextVoted = !currentlyVoted;
    const nextCount = nextVoted ? post.upvoteCount + 1 : Math.max(0, post.upvoteCount - 1);
    const nextImpact = calculateImpactScore(nextCount, post.accountARR);

    // Optimistically update post in list
    this.posts.update(list => list.map(p => {
      if (p.id === post.id) {
        return {
          ...p,
          hasVoted: nextVoted,
          upvoteCount: nextCount,
          impactScore: nextImpact,
          updatedAt: new Date().toISOString()
        };
      }
      return p;
    }));

    // If modal open, sync active post
    if (this.selectedPost()?.id === post.id) {
      this.selectedPost.update(curr => curr ? {
        ...curr,
        hasVoted: nextVoted,
        upvoteCount: nextCount,
        impactScore: nextImpact
      } : null);
    }

    // Call backend API if available
    if (this.http) {
      this.http.post<any>(`/api/v1/feedback/board/posts/${post.id}/vote`, {
        tenantId: post.tenantId || this.currentTenantId(),
        voterEmail: this.currentUserEmail(),
        voterIp: 'browser-client'
      }).subscribe({
        next: (res) => {
          if (res && typeof res.upvoteCount === 'number') {
            this.posts.update(list => list.map(p => {
              if (p.id === post.id) {
                return {
                  ...p,
                  hasVoted: res.isUpvoted,
                  upvoteCount: res.upvoteCount,
                  impactScore: res.impactScore
                };
              }
              return p;
            }));
          }
        },
        error: (err) => {
          console.warn('Anti-gaming or backend vote rejection:', err);
        }
      });
    }
  }

  // Submit Idea Modal
  openSubmitModal(): void {
    this.ideaForm = {
      title: '',
      description: '',
      category: 'FeatureRequest',
      authorName: this.currentUserName(),
      authorEmail: this.currentUserEmail(),
      accountARR: 45000,
      estimatedQuarter: 'Q1 2027'
    };
    this.isSubmitModalOpen.set(true);
  }

  closeSubmitModal(): void {
    this.isSubmitModalOpen.set(false);
  }

  submitNewIdea(): void {
    if (!this.ideaForm.title.trim() || !this.ideaForm.description.trim()) return;

    const newPostId = 'f-' + Math.random().toString(36).substring(2, 9) + '-' + Date.now();
    const arr = Math.max(0, this.ideaForm.accountARR || 0);
    const initialVotes = 1;
    const impact = calculateImpactScore(initialVotes, arr);

    const newPost: FeedbackPostUi = {
      id: newPostId,
      tenantId: this.currentTenantId(),
      title: this.ideaForm.title.trim(),
      description: this.ideaForm.description.trim(),
      category: this.ideaForm.category,
      status: 'Submitted',
      authorEmail: this.ideaForm.authorEmail.trim() || this.currentUserEmail(),
      authorName: this.ideaForm.authorName.trim() || this.currentUserName(),
      accountId: 'ACC-NEW',
      accountARR: arr,
      upvoteCount: initialVotes,
      impactScore: impact,
      createdAt: new Date().toISOString(),
      updatedAt: new Date().toISOString(),
      completedAt: null,
      estimatedQuarter: this.ideaForm.estimatedQuarter.trim(),
      responseNotes: null,
      hasVoted: true
    };

    // Optimistically prepend to board
    this.posts.update(list => [newPost, ...list]);
    this.closeSubmitModal();

    // Call backend API if available
    if (this.http) {
      this.http.post<FeedbackPostUi>(`/api/v1/feedback/board/posts`, {
        tenantId: this.currentTenantId(),
        title: newPost.title,
        description: newPost.description,
        category: newPost.category,
        authorEmail: newPost.authorEmail,
        authorName: newPost.authorName,
        accountARR: newPost.accountARR,
        estimatedQuarter: newPost.estimatedQuarter
      }).subscribe({
        next: (created) => {
          if (created && created.id) {
            this.posts.update(list => list.map(p => p.id === newPostId ? created : p));
          }
        },
        error: (err) => console.warn('Could not post to backend:', err)
      });
    }
  }

  // Detail Modal
  openDetailModal(post: FeedbackPostUi): void {
    this.selectedPost.set(post);
    this.adminStatusEdit = post.status;
    this.adminQuarterEdit = post.estimatedQuarter || '';
    this.adminNoteEdit = post.responseNotes || '';

    // Seed mock comments for post
    const sampleComments: FeedbackCommentUi[] = [
      {
        id: 'c1',
        tenantId: post.tenantId,
        postId: post.id,
        authorName: 'Product Team Lead',
        authorEmail: 'product@sgrowing.io',
        isOfficialTeamResponse: true,
        content: `Thanks for proposing "${post.title}". Our engineering triage review flagged this with high relevance to our GCC logistics and checkout milestone.`,
        createdAt: new Date(new Date(post.createdAt).getTime() + 86400000).toISOString()
      }
    ];

    if (post.responseNotes) {
      sampleComments.push({
        id: 'c2',
        tenantId: post.tenantId,
        postId: post.id,
        authorName: 'Engineering Sprint Manager',
        authorEmail: 'dev-lead@sgrowing.io',
        isOfficialTeamResponse: true,
        content: post.responseNotes,
        createdAt: post.updatedAt
      });
    }

    this.commentsList.set(sampleComments);

    // Call backend for comments if available
    if (this.http) {
      this.http.get<any>(`/api/v1/feedback/board/posts/${post.id}?tenantId=${post.tenantId}&voterEmail=${this.currentUserEmail()}`).subscribe({
        next: (detail) => {
          if (detail && detail.comments) {
            this.commentsList.set(detail.comments);
          }
        },
        error: () => {}
      });
    }
  }

  closeDetailModal(): void {
    this.selectedPost.set(null);
    this.newCommentText = '';
  }

  // Admin Status Update
  saveAdminStatusChange(post: FeedbackPostUi): void {
    const newStatus = this.adminStatusEdit;
    const notes = this.adminNoteEdit.trim();
    const quarter = this.adminQuarterEdit.trim();

    this.posts.update(list => list.map(p => {
      if (p.id === post.id) {
        return {
          ...p,
          status: newStatus,
          responseNotes: notes || p.responseNotes,
          estimatedQuarter: quarter || p.estimatedQuarter,
          completedAt: newStatus === 'Completed' ? new Date().toISOString() : null,
          updatedAt: new Date().toISOString()
        };
      }
      return p;
    }));

    if (this.selectedPost()?.id === post.id) {
      this.selectedPost.update(curr => curr ? {
        ...curr,
        status: newStatus,
        responseNotes: notes || curr.responseNotes,
        estimatedQuarter: quarter || curr.estimatedQuarter,
        completedAt: newStatus === 'Completed' ? new Date().toISOString() : null,
        updatedAt: new Date().toISOString()
      } : null);
    }

    // Call backend API if available
    if (this.http) {
      this.http.patch<FeedbackPostUi>(`/api/v1/feedback/board/posts/${post.id}/status`, {
        tenantId: post.tenantId || this.currentTenantId(),
        newStatus: newStatus,
        responseNotes: notes,
        estimatedQuarter: quarter
      }).subscribe();
    }
  }

  // Comment Submission
  submitComment(post: FeedbackPostUi): void {
    const text = this.newCommentText.trim();
    if (!text) return;

    const author = this.newCommentAuthor.trim() || this.currentUserName();
    const isOfficial = this.isAdminMode();

    const newComment: FeedbackCommentUi = {
      id: 'c-' + Date.now(),
      tenantId: post.tenantId,
      postId: post.id,
      authorName: author,
      authorEmail: this.currentUserEmail(),
      isOfficialTeamResponse: isOfficial,
      content: text,
      createdAt: new Date().toISOString()
    };

    this.commentsList.update(list => [...list, newComment]);
    this.newCommentText = '';

    if (this.http) {
      this.http.post<FeedbackCommentUi>(`/api/v1/feedback/board/posts/${post.id}/comments`, {
        tenantId: post.tenantId || this.currentTenantId(),
        authorName: author,
        authorEmail: this.currentUserEmail(),
        content: text,
        isOfficialTeamResponse: isOfficial
      }).subscribe();
    }
  }

  // Helpers
  columnArr(columnPosts: FeedbackPostUi[]): number {
    return columnPosts.reduce((sum, p) => sum + p.accountARR, 0);
  }

  formatNumber(val: number): string {
    if (!val) return '0';
    if (val >= 1000000) return (val / 1000000).toFixed(1) + 'M';
    if (val >= 1000) return (val / 1000).toFixed(0) + 'k';
    return val.toLocaleString();
  }

  formatDate(dateStr?: string | null): string {
    if (!dateStr) return '';
    try {
      const d = new Date(dateStr);
      return d.toLocaleDateString('en-US', { month: 'short', day: 'numeric', year: 'numeric' });
    } catch {
      return dateStr;
    }
  }

  getCategoryLabel(category: FeedbackCategoryType): string {
    switch (category) {
      case 'FeatureRequest': return 'Feature';
      case 'Integration': return 'Integration';
      case 'BugReport': return 'Bug Report';
      case 'UxImprovement': return 'UX Polish';
      default: return category;
    }
  }

  getCategoryBadgeClass(category: FeedbackCategoryType): string {
    switch (category) {
      case 'FeatureRequest': return 'bg-indigo-500/10 text-indigo-300 border-indigo-500/20';
      case 'Integration': return 'bg-purple-500/10 text-purple-300 border-purple-500/20';
      case 'BugReport': return 'bg-rose-500/10 text-rose-300 border-rose-500/20';
      case 'UxImprovement': return 'bg-teal-500/10 text-teal-300 border-teal-500/20';
      default: return 'bg-slate-800 text-slate-300 border-slate-700';
    }
  }

  getStatusBadgeClass(status: FeedbackStatusType): string {
    switch (status) {
      case 'Submitted': return 'bg-slate-800/80 text-slate-300 border-slate-700';
      case 'UnderReview': return 'bg-amber-500/10 text-amber-300 border-amber-500/20';
      case 'Planned': return 'bg-indigo-500/10 text-indigo-300 border-indigo-500/20';
      case 'InProgress': return 'bg-cyan-500/10 text-cyan-300 border-cyan-500/20';
      case 'Completed': return 'bg-emerald-500/10 text-emerald-300 border-emerald-500/20';
      case 'Declined': return 'bg-rose-500/10 text-rose-300 border-rose-500/20';
      default: return 'bg-slate-800 text-slate-300 border-slate-700';
    }
  }
}
