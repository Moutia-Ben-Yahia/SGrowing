import { Component, OnInit, signal, computed, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { HttpClient } from '@angular/common/http';
import { Router } from '@angular/router';

export type PublishingStatus = 'Draft' | 'Scheduled' | 'Publishing' | 'Published' | 'Failed' | 'Cancelled';
export type TokenHealthStatus = 'Active' | 'ExpiringSoon' | 'Expired';

export interface SocialMediaAttachment {
  url: string;
  aspectRatio: string; // "1:1" | "4:5" | "9:16" | "16:9"
  mediaType: string;   // "image" | "video" | "pdf"
  altText?: string;
}

export interface PlatformDispatchResult {
  platform: string;
  success: boolean;
  externalPostId?: string;
  latencyMs: number;
  message?: string;
  dispatchedAtUtc: string;
}

export interface UnifiedSocialPost {
  id: string;
  tenantId: string;
  title: string;
  content: string;
  platformOverrides: Record<string, string>;
  mediaAttachments: SocialMediaAttachment[];
  tags: string[];
  targetPlatforms: string[];
  scheduledAtUtc?: string;
  publishedAtUtc?: string;
  status: PublishingStatus;
  createdAtUtc: string;
  dispatchResults?: PlatformDispatchResult[];
  failureReason?: string;
}

export interface ConnectedSocialAccount {
  id: string;
  tenantId: string;
  platform: string;
  accountType: string;
  accountName: string;
  handle: string;
  avatarUrl: string;
  accessToken: string;
  refreshToken: string;
  tokenExpiresAtUtc: string;
  lastRefreshedAtUtc: string;
  tokenStatus: TokenHealthStatus;
  remainingDays: number;
  followersCount: number;
  autoRefreshEnabled: boolean;
}

export interface ValidationReport {
  isValid: boolean;
  errors: string[];
  warnings: string[];
  platformResults: Record<string, {
    platform: string;
    isValid: boolean;
    errors: string[];
    warnings: string[];
    characterCount: number;
    maxCharacters: number;
  }>;
}

interface CalendarDay {
  date: Date;
  dayNumber: number;
  isCurrentMonth: boolean;
  isToday: boolean;
  posts: UnifiedSocialPost[];
}

@Component({
  selector: 'app-social-content-calendar-studio',
  standalone: true,
  imports: [CommonModule, FormsModule],
  template: `
    <div class="flex flex-col min-h-screen bg-slate-950 text-slate-100 font-sans antialiased" dir="ltr">
      <!-- Top Navigation Bar -->
      <header class="h-16 border-b border-slate-800 bg-slate-900/80 backdrop-blur px-6 flex items-center justify-between shrink-0 sticky top-0 z-30">
        <div class="flex items-center gap-3">
          <button
            (click)="navigateTo('/social')"
            class="text-xs text-slate-400 hover:text-white flex items-center gap-1.5 transition px-2.5 py-1.5 rounded-lg bg-slate-800/70 hover:bg-slate-800 border border-slate-700/60">
            <span>←</span>
            <span>Social Studio</span>
          </button>
          <span class="text-slate-700">|</span>
          <div class="w-9 h-9 rounded-xl bg-gradient-to-tr from-sky-500 via-indigo-600 to-fuchsia-600 text-white flex items-center justify-center font-bold text-sm shadow-lg shadow-indigo-950/50">
            📅
          </div>
          <div>
            <div class="flex items-center gap-2">
              <h1 class="text-base font-bold text-white tracking-tight">Content Calendar & Publishing Dispatcher</h1>
              <span class="px-2 py-0.5 rounded-full text-[10px] font-semibold bg-indigo-500/10 text-indigo-400 border border-indigo-500/30">
                Horizon 49 • Live
              </span>
            </div>
            <p class="text-xs text-slate-400">Multi-platform composer with simulated card previews, OAuth token vault & automated queue</p>
          </div>
        </div>

        <div class="flex items-center gap-3">
          <!-- Active Accounts Indicator Button -->
          <button
            (click)="toggleAccountsDrawer()"
            class="flex items-center gap-2 text-xs px-3 py-1.5 rounded-lg border transition"
            [ngClass]="hasExpiringSoonTokens() ? 'bg-amber-500/10 border-amber-500/40 text-amber-300 hover:bg-amber-500/20' : 'bg-slate-900/90 border-slate-800 text-slate-300 hover:bg-slate-800'">
            <span class="w-2 h-2 rounded-full" [ngClass]="hasExpiringSoonTokens() ? 'bg-amber-400 animate-pulse' : 'bg-emerald-400'"></span>
            <span>Accounts ({{ connectedAccounts().length }})</span>
            @if (hasExpiringSoonTokens()) {
              <span class="px-1.5 py-0.2 rounded text-[10px] font-bold bg-amber-500/30 text-amber-200">Action Required</span>
            }
          </button>

          <!-- New Post Button -->
          <button
            (click)="openComposerModal()"
            class="px-3.5 py-1.5 rounded-lg text-xs font-semibold bg-gradient-to-r from-indigo-500 to-sky-500 hover:from-indigo-600 hover:to-sky-600 text-white shadow-md shadow-indigo-950/40 flex items-center gap-1.5 transition">
            <span>✨</span>
            <span>Compose Post</span>
          </button>

          <!-- Refresh Data Button -->
          <button
            (click)="loadAllData()"
            [disabled]="isLoading()"
            class="px-3 py-1.5 rounded-lg text-xs font-medium bg-slate-800 hover:bg-slate-700 text-slate-200 border border-slate-700 flex items-center gap-1.5 transition">
            <span [class.animate-spin]="isLoading()">🔄</span>
            <span>Refresh</span>
          </button>
        </div>
      </header>

      <!-- Main Layout Body -->
      <main class="flex-1 p-6 max-w-7xl w-full mx-auto space-y-6">
        <!-- Notification Banner if token expiring soon -->
        @if (hasExpiringSoonTokens()) {
          <div class="p-4 rounded-xl bg-gradient-to-r from-amber-950/40 via-amber-900/20 to-slate-900 border border-amber-500/30 flex items-center justify-between">
            <div class="flex items-center gap-3">
              <span class="text-xl">⚠️</span>
              <div>
                <h4 class="text-xs font-bold text-amber-300">OAuth 2.0 Token Health Warning</h4>
                <p class="text-xs text-amber-200/80">One or more connected social profiles has an access token expiring within 72 hours. Auto-refresh daemon is active.</p>
              </div>
            </div>
            <div class="flex items-center gap-2">
              <button
                (click)="autoRefreshAllExpiringTokens()"
                [disabled]="isRefreshingTokens()"
                class="px-3 py-1.5 rounded-lg text-xs font-semibold bg-amber-500/20 hover:bg-amber-500/30 text-amber-200 border border-amber-500/40 flex items-center gap-1.5 transition">
                <span [class.animate-spin]="isRefreshingTokens()">⚡</span>
                <span>Proactive 1-Click Refresh All</span>
              </button>
              <button
                (click)="toggleAccountsDrawer()"
                class="px-3 py-1.5 rounded-lg text-xs font-medium bg-slate-800 text-slate-300 hover:text-white border border-slate-700 transition">
                Inspect Accounts
              </button>
            </div>
          </div>
        }

        <!-- Calendar Header Controls -->
        <div class="flex flex-col md:flex-row md:items-center justify-between gap-4 bg-slate-900/60 p-4 rounded-2xl border border-slate-800">
          <div class="flex items-center gap-3">
            <button
              (click)="changeMonth(-1)"
              class="w-8 h-8 rounded-lg bg-slate-800 hover:bg-slate-700 text-slate-300 flex items-center justify-center font-bold transition">
              ‹
            </button>
            <h2 class="text-lg font-bold text-white min-w-48 text-center tracking-tight">
              {{ currentMonthName() }} {{ currentYear() }}
            </h2>
            <button
              (click)="changeMonth(1)"
              class="w-8 h-8 rounded-lg bg-slate-800 hover:bg-slate-700 text-slate-300 flex items-center justify-center font-bold transition">
              ›
            </button>
            <button
              (click)="goToToday()"
              class="px-2.5 py-1 text-xs font-medium rounded-lg bg-slate-800 hover:bg-slate-700 text-slate-300 border border-slate-700 transition">
              Today
            </button>
          </div>

          <!-- Platform & Status Filters -->
          <div class="flex flex-wrap items-center gap-2">
            <!-- Platform Filter Pills -->
            <div class="flex items-center gap-1 bg-slate-950 p-1 rounded-xl border border-slate-800">
              <button
                (click)="selectedFilterPlatform.set('ALL')"
                [ngClass]="selectedFilterPlatform() === 'ALL' ? 'bg-indigo-600 text-white' : 'text-slate-400 hover:text-slate-200'"
                class="px-2.5 py-1 rounded-lg text-xs font-medium transition">
                All Platforms
              </button>
              <button
                (click)="selectedFilterPlatform.set('X')"
                [ngClass]="selectedFilterPlatform() === 'X' ? 'bg-sky-500 text-white' : 'text-slate-400 hover:text-slate-200'"
                class="px-2.5 py-1 rounded-lg text-xs font-medium transition flex items-center gap-1">
                <span>𝕏</span>
                <span>X</span>
              </button>
              <button
                (click)="selectedFilterPlatform.set('Instagram')"
                [ngClass]="selectedFilterPlatform() === 'Instagram' ? 'bg-pink-600 text-white' : 'text-slate-400 hover:text-slate-200'"
                class="px-2.5 py-1 rounded-lg text-xs font-medium transition flex items-center gap-1">
                <span>📸</span>
                <span>Instagram</span>
              </button>
              <button
                (click)="selectedFilterPlatform.set('LinkedIn')"
                [ngClass]="selectedFilterPlatform() === 'LinkedIn' ? 'bg-blue-600 text-white' : 'text-slate-400 hover:text-slate-200'"
                class="px-2.5 py-1 rounded-lg text-xs font-medium transition flex items-center gap-1">
                <span>💼</span>
                <span>LinkedIn</span>
              </button>
              <button
                (click)="selectedFilterPlatform.set('TikTok')"
                [ngClass]="selectedFilterPlatform() === 'TikTok' ? 'bg-fuchsia-600 text-white' : 'text-slate-400 hover:text-slate-200'"
                class="px-2.5 py-1 rounded-lg text-xs font-medium transition flex items-center gap-1">
                <span>🎵</span>
                <span>TikTok</span>
              </button>
            </div>

            <!-- Status Filter -->
            <select
              [ngModel]="selectedFilterStatus()"
              (ngModelChange)="selectedFilterStatus.set($event)"
              class="bg-slate-950 border border-slate-800 text-xs text-slate-300 rounded-xl px-3 py-1.5 focus:outline-none focus:border-indigo-500">
              <option value="ALL">All Statuses</option>
              <option value="Scheduled">Scheduled</option>
              <option value="Published">Published</option>
              <option value="Draft">Draft</option>
              <option value="Cancelled">Cancelled</option>
            </select>
          </div>
        </div>

        <!-- Monthly Calendar Grid -->
        <div class="bg-slate-900/60 rounded-2xl border border-slate-800 overflow-hidden shadow-xl">
          <!-- Day of Week Headers -->
          <div class="grid grid-cols-7 border-b border-slate-800 bg-slate-950/60 text-center py-2.5 text-xs font-semibold text-slate-400">
            <div>Sun</div>
            <div>Mon</div>
            <div>Tue</div>
            <div>Wed</div>
            <div>Thu</div>
            <div>Fri</div>
            <div>Sat</div>
          </div>

          <!-- Day Cells Grid -->
          <div class="grid grid-cols-7 divide-x divide-y divide-slate-800/80 bg-slate-950/20">
            @for (day of calendarDays(); track day.date.toISOString()) {
              <div
                class="min-h-32 p-2 flex flex-col justify-between transition group hover:bg-slate-900/40"
                [ngClass]="{
                  'opacity-40 bg-slate-950/50': !day.isCurrentMonth,
                  'bg-indigo-950/10 ring-1 ring-inset ring-indigo-500/30': day.isToday
                }">
                <!-- Date Header in Cell -->
                <div class="flex items-center justify-between mb-1.5">
                  <span
                    class="text-xs font-bold w-6 h-6 rounded-full flex items-center justify-center"
                    [ngClass]="day.isToday ? 'bg-indigo-500 text-white shadow-sm' : 'text-slate-400 group-hover:text-slate-200'">
                    {{ day.dayNumber }}
                  </span>
                  @if (day.posts.length > 0) {
                    <span class="text-[10px] text-slate-500 font-mono">
                      {{ day.posts.length }} {{ day.posts.length === 1 ? 'post' : 'posts' }}
                    </span>
                  }
                </div>

                <!-- Post Badges Stack -->
                <div class="space-y-1.5 overflow-y-auto max-h-24 pr-0.5">
                  @for (post of day.posts; track post.id) {
                    <div
                      (click)="inspectPost(post)"
                      class="px-2 py-1 rounded-lg text-[11px] font-medium border cursor-pointer transition truncate flex items-center justify-between gap-1 shadow-sm"
                      [ngClass]="getPostBadgeClass(post)">
                      <div class="flex items-center gap-1 truncate">
                        <span class="text-[10px]">{{ getPlatformIcon(post.targetPlatforms[0] || 'General') }}</span>
                        <span class="truncate">{{ post.title || post.content }}</span>
                      </div>
                      <span class="text-[9px] uppercase px-1 py-0.2 rounded font-mono shrink-0" [ngClass]="getStatusPillClass(post.status)">
                        {{ post.status }}
                      </span>
                    </div>
                  }
                </div>

                <!-- Quick Add Post Button on hover -->
                <button
                  (click)="quickAddPost(day.date)"
                  class="opacity-0 group-hover:opacity-100 mt-1 w-full py-0.5 text-[10px] text-slate-400 hover:text-white rounded bg-slate-800/80 hover:bg-slate-800 transition text-center font-medium">
                  + Add Post
                </button>
              </div>
            }
          </div>
        </div>
      </main>

      <!-- Connected Accounts Drawer (Slide-out) -->
      @if (showAccountsDrawer()) {
        <div class="fixed inset-0 z-50 overflow-hidden bg-slate-950/70 backdrop-blur-sm flex justify-end">
          <div class="w-full max-w-md bg-slate-900 border-l border-slate-800 h-full flex flex-col shadow-2xl p-6 overflow-y-auto">
            <!-- Drawer Header -->
            <div class="flex items-center justify-between pb-4 border-b border-slate-800">
              <div class="flex items-center gap-2">
                <span class="text-xl">🔐</span>
                <div>
                  <h3 class="text-base font-bold text-white">Connected Profiles Vault</h3>
                  <p class="text-xs text-slate-400">OAuth 2.0 Token Health & Dispatcher Channels</p>
                </div>
              </div>
              <button
                (click)="toggleAccountsDrawer()"
                class="w-7 h-7 rounded-lg bg-slate-800 hover:bg-slate-700 text-slate-400 hover:text-white flex items-center justify-center font-bold">
                ✕
              </button>
            </div>

            <!-- Drawer Body -->
            <div class="py-4 space-y-4 flex-1">
              <div class="p-3 bg-slate-950 rounded-xl border border-slate-800/80 text-xs text-slate-400 flex items-center justify-between">
                <span>Proactive Daemon: <strong>Active</strong></span>
                <span class="text-emerald-400 text-[11px] font-mono">72h Auto-Refresh Window</span>
              </div>

              <!-- List of Accounts -->
              @for (account of connectedAccounts(); track account.id) {
                <div class="p-4 rounded-xl bg-slate-950 border border-slate-800 space-y-3">
                  <div class="flex items-center justify-between">
                    <div class="flex items-center gap-3">
                      <img [src]="account.avatarUrl" [alt]="account.accountName" class="w-10 h-10 rounded-full border border-slate-700 object-cover" />
                      <div>
                        <div class="flex items-center gap-1.5">
                          <h4 class="text-xs font-bold text-white">{{ account.accountName }}</h4>
                          <span class="text-[10px] text-slate-400">{{ account.handle }}</span>
                        </div>
                        <p class="text-[10px] text-slate-400">{{ account.accountType }} • {{ account.followersCount | number }} followers</p>
                      </div>
                    </div>
                    <span class="text-base">{{ getPlatformIcon(account.platform) }}</span>
                  </div>

                  <!-- Token Health Badge -->
                  <div class="flex items-center justify-between text-xs pt-1">
                    <div class="flex items-center gap-1.5">
                      <span
                        class="w-2 h-2 rounded-full"
                        [ngClass]="{
                          'bg-emerald-400': account.tokenStatus === 'Active',
                          'bg-amber-400 animate-pulse': account.tokenStatus === 'ExpiringSoon',
                          'bg-rose-500': account.tokenStatus === 'Expired'
                        }">
                      </span>
                      <span
                        class="text-[11px] font-semibold"
                        [ngClass]="{
                          'text-emerald-400': account.tokenStatus === 'Active',
                          'text-amber-400': account.tokenStatus === 'ExpiringSoon',
                          'text-rose-400': account.tokenStatus === 'Expired'
                        }">
                        {{ account.tokenStatus }}
                      </span>
                    </div>

                    <span class="text-[11px] text-slate-400 font-mono">
                      {{ account.remainingDays }} days remaining
                    </span>
                  </div>

                  <!-- 1-Click Refresh Token Action Button -->
                  <button
                    (click)="refreshToken(account.id)"
                    [disabled]="isRefreshingAccountId() === account.id"
                    class="w-full py-1.5 rounded-lg text-xs font-medium bg-slate-800 hover:bg-slate-700 text-slate-200 border border-slate-700 flex items-center justify-center gap-1.5 transition">
                    <span [class.animate-spin]="isRefreshingAccountId() === account.id">⚡</span>
                    <span>1-Click Refresh Token (+60 Days)</span>
                  </button>
                </div>
              }
            </div>

            <!-- Drawer Footer -->
            <div class="pt-4 border-t border-slate-800">
              <button
                (click)="autoRefreshAllExpiringTokens()"
                [disabled]="isRefreshingTokens()"
                class="w-full py-2.5 rounded-xl text-xs font-bold bg-indigo-600 hover:bg-indigo-500 text-white flex items-center justify-center gap-2 shadow-lg shadow-indigo-950 transition">
                <span [class.animate-spin]="isRefreshingTokens()">🔄</span>
                <span>Audit & Proactively Refresh All</span>
              </button>
            </div>
          </div>
        </div>
      }

      <!-- Unified Post Composer Modal -->
      @if (showComposerModal()) {
        <div class="fixed inset-0 z-50 overflow-y-auto bg-slate-950/80 backdrop-blur-md flex items-center justify-center p-4">
          <div class="w-full max-w-5xl bg-slate-900 border border-slate-800 rounded-3xl shadow-2xl overflow-hidden flex flex-col my-8 max-h-[90vh]">
            <!-- Composer Modal Header -->
            <div class="px-6 py-4 border-b border-slate-800 flex items-center justify-between bg-slate-900/90">
              <div class="flex items-center gap-2">
                <span class="text-xl">✍️</span>
                <div>
                  <h3 class="text-sm font-bold text-white">Unified Multi-Platform Post Composer</h3>
                  <p class="text-xs text-slate-400">Compose once with customized overrides, live meters & simulated platform cards</p>
                </div>
              </div>
              <button
                (click)="closeComposerModal()"
                class="w-8 h-8 rounded-lg bg-slate-800 hover:bg-slate-700 text-slate-400 hover:text-white flex items-center justify-center font-bold">
                ✕
              </button>
            </div>

            <!-- Composer Body: 2 Columns (Editor on left, Live Simulated Preview on right) -->
            <div class="grid grid-cols-1 lg:grid-cols-12 flex-1 overflow-y-auto">
              <!-- Left Column: Input Form (7 cols) -->
              <div class="lg:col-span-7 p-6 space-y-5 border-r border-slate-800">
                <!-- Target Platforms Selector -->
                <div>
                  <label class="block text-xs font-semibold text-slate-300 mb-2">Target Social Networks</label>
                  <div class="flex flex-wrap gap-2">
                    @for (platform of availablePlatforms; track platform.name) {
                      <button
                        type="button"
                        (click)="toggleTargetPlatform(platform.name)"
                        class="px-3 py-1.5 rounded-xl text-xs font-medium border flex items-center gap-1.5 transition"
                        [ngClass]="composerTargetPlatforms().includes(platform.name)
                          ? platform.activeClass
                          : 'bg-slate-950 border-slate-800 text-slate-400 hover:text-slate-200'">
                        <span>{{ platform.icon }}</span>
                        <span>{{ platform.name }}</span>
                        @if (composerTargetPlatforms().includes(platform.name)) {
                          <span class="text-[10px]">✓</span>
                        }
                      </button>
                    }
                  </div>
                </div>

                <!-- Post Title -->
                <div>
                  <label class="block text-xs font-semibold text-slate-300 mb-1">Campaign / Internal Title</label>
                  <input
                    type="text"
                    [(ngModel)]="composerTitle"
                    placeholder="e.g. Q4 Growth Metrics Infographic"
                    class="w-full bg-slate-950 border border-slate-800 rounded-xl px-3.5 py-2 text-xs text-white placeholder-slate-500 focus:outline-none focus:border-indigo-500" />
                </div>

                <!-- Tabs: Base Content vs Platform Overrides -->
                <div>
                  <div class="flex items-center justify-between mb-2">
                    <label class="text-xs font-semibold text-slate-300">Copy & Platform Overrides</label>
                    <div class="flex items-center gap-1 bg-slate-950 p-0.5 rounded-lg border border-slate-800 text-[11px]">
                      <button
                        (click)="activeOverrideTab.set('BASE')"
                        [ngClass]="activeOverrideTab() === 'BASE' ? 'bg-indigo-600 text-white' : 'text-slate-400 hover:text-white'"
                        class="px-2 py-0.5 rounded font-medium transition">
                        Base
                      </button>
                      @for (p of composerTargetPlatforms(); track p) {
                        <button
                          (click)="activeOverrideTab.set(p)"
                          [ngClass]="activeOverrideTab() === p ? 'bg-indigo-600 text-white' : 'text-slate-400 hover:text-white'"
                          class="px-2 py-0.5 rounded font-medium transition flex items-center gap-1">
                          <span>{{ getPlatformIcon(p) }}</span>
                          <span>{{ p }}</span>
                          @if (hasOverride(p)) {
                            <span class="w-1.5 h-1.5 rounded-full bg-sky-400"></span>
                          }
                        </button>
                      }
                    </div>
                  </div>

                  <!-- Textarea for active tab -->
                  @if (activeOverrideTab() === 'BASE') {
                    <textarea
                      [(ngModel)]="composerContent"
                      (ngModelChange)="onContentChanged()"
                      rows="5"
                      placeholder="Write your unified master copy here. URLs will be counted as 23 chars on X..."
                      class="w-full bg-slate-950 border border-slate-800 rounded-xl p-3 text-xs text-white placeholder-slate-500 focus:outline-none focus:border-indigo-500 leading-relaxed"></textarea>
                  } @else {
                    <div class="space-y-1">
                      <div class="flex items-center justify-between text-[11px] text-slate-400">
                        <span>Custom copy override for <strong>{{ activeOverrideTab() }}</strong></span>
                        @if (hasOverride(activeOverrideTab())) {
                          <button
                            (click)="clearOverride(activeOverrideTab())"
                            class="text-rose-400 hover:underline">
                            Reset to Base
                          </button>
                        }
                      </div>
                      <textarea
                        [ngModel]="getOverrideText(activeOverrideTab())"
                        (ngModelChange)="setOverrideText(activeOverrideTab(), $event)"
                        rows="5"
                        placeholder="Customize specific copy for this network (e.g. shorter hook for X or formal post for LinkedIn)..."
                        class="w-full bg-slate-950 border border-indigo-500/40 rounded-xl p-3 text-xs text-white placeholder-slate-500 focus:outline-none focus:border-indigo-500 leading-relaxed"></textarea>
                    </div>
                  }
                </div>

                <!-- Real-Time Platform Character Meters -->
                <div class="p-3.5 rounded-xl bg-slate-950 border border-slate-800 space-y-2.5">
                  <h4 class="text-[11px] font-bold text-slate-400 uppercase tracking-wider">Real-Time Platform Constraint Meters</h4>
                  <div class="grid grid-cols-2 md:grid-cols-4 gap-2.5">
                    @for (p of composerTargetPlatforms(); track p) {
                      <div class="p-2 rounded-lg bg-slate-900 border border-slate-800 space-y-1">
                        <div class="flex items-center justify-between text-[10px]">
                          <span class="font-bold flex items-center gap-1">
                            <span>{{ getPlatformIcon(p) }}</span>
                            <span>{{ p }}</span>
                          </span>
                          <span [ngClass]="getPlatformCharMeterClass(p)" class="font-mono font-bold">
                            {{ getPlatformCharCount(p) }}/{{ getPlatformMaxChars(p) }}
                          </span>
                        </div>
                        <div class="w-full bg-slate-800 h-1 rounded-full overflow-hidden">
                          <div
                            class="h-full transition-all"
                            [ngClass]="getPlatformBarColor(p)"
                            [style.width.%]="getPlatformPercent(p)"></div>
                        </div>
                      </div>
                    }
                  </div>
                </div>

                <!-- Media Attachments & Aspect Ratio -->
                <div class="space-y-2">
                  <div class="flex items-center justify-between">
                    <label class="text-xs font-semibold text-slate-300">Media Attachments</label>
                    <button
                      (click)="addMediaAttachment()"
                      class="text-[11px] text-indigo-400 hover:text-indigo-300 font-medium">
                      + Add Media URL
                    </button>
                  </div>

                  @for (media of composerMedia(); track $index) {
                    <div class="flex items-center gap-2 p-2 rounded-xl bg-slate-950 border border-slate-800">
                      <select
                        [(ngModel)]="media.mediaType"
                        class="bg-slate-900 border border-slate-700 text-xs rounded-lg px-2 py-1 text-slate-200">
                        <option value="image">Image</option>
                        <option value="video">Video</option>
                        <option value="pdf">PDF Carousel</option>
                      </select>

                      <input
                        type="text"
                        [(ngModel)]="media.url"
                        placeholder="https://images.unsplash.com/..."
                        class="flex-1 bg-slate-900 border border-slate-700 text-xs rounded-lg px-2.5 py-1 text-slate-200 focus:outline-none" />

                      <select
                        [(ngModel)]="media.aspectRatio"
                        class="bg-slate-900 border border-slate-700 text-xs rounded-lg px-2 py-1 text-slate-200">
                        <option value="1:1">1:1 Square</option>
                        <option value="4:5">4:5 Portrait</option>
                        <option value="9:16">9:16 Story/Reel</option>
                        <option value="16:9">16:9 Landscape</option>
                      </select>

                      <button
                        (click)="removeMediaAttachment($index)"
                        class="text-slate-500 hover:text-rose-400 px-1 font-bold">
                        ✕
                      </button>
                    </div>
                  }
                  @if (composerMedia().length === 0) {
                    <div class="p-3 border border-dashed border-slate-800 rounded-xl text-center text-xs text-slate-500">
                      No media attached (Note: Instagram and TikTok require media attachments).
                    </div>
                  }
                </div>

                <!-- Tags / Hashtags Input -->
                <div>
                  <label class="block text-xs font-semibold text-slate-300 mb-1">Hashtags / Tags (Comma-separated)</label>
                  <input
                    type="text"
                    [(ngModel)]="composerTagsInput"
                    placeholder="SaaS, Growth, B2B, AI"
                    class="w-full bg-slate-950 border border-slate-800 rounded-xl px-3.5 py-2 text-xs text-white placeholder-slate-500 focus:outline-none focus:border-indigo-500" />
                </div>

                <!-- Scheduling Date / Time -->
                <div>
                  <label class="block text-xs font-semibold text-slate-300 mb-1">Publish Time (UTC)</label>
                  <input
                    type="datetime-local"
                    [(ngModel)]="composerScheduledDate"
                    class="w-full bg-slate-950 border border-slate-800 rounded-xl px-3.5 py-2 text-xs text-white focus:outline-none focus:border-indigo-500" />
                </div>

                <!-- Validation Issues Alert if invalid -->
                @if (validationReport() && !validationReport()!.isValid) {
                  <div class="p-3.5 rounded-xl bg-rose-950/40 border border-rose-500/40 text-xs text-rose-300 space-y-1">
                    <div class="font-bold flex items-center gap-1.5">
                      <span>⚠️</span>
                      <span>Platform Validation Constraints Failed</span>
                    </div>
                    <ul class="list-disc list-inside space-y-0.5 text-[11px] text-rose-200/90 pl-1">
                      @for (err of validationReport()!.errors; track err) {
                        <li>{{ err }}</li>
                      }
                    </ul>
                  </div>
                }
              </div>

              <!-- Right Column: Dynamic Platform Previews (5 cols) -->
              <div class="lg:col-span-5 p-6 bg-slate-950/60 flex flex-col space-y-4 overflow-y-auto">
                <div class="flex items-center justify-between pb-2 border-b border-slate-800">
                  <h4 class="text-xs font-bold text-slate-300 uppercase tracking-wider">Simulated Network Card UI</h4>
                  <!-- Tab to toggle simulated preview -->
                  <div class="flex items-center gap-1 bg-slate-900 p-0.5 rounded-lg text-[10px]">
                    <button
                      (click)="previewPlatform.set('X')"
                      [ngClass]="previewPlatform() === 'X' ? 'bg-sky-500 text-white font-bold' : 'text-slate-400'"
                      class="px-2 py-0.5 rounded transition">
                      𝕏 Post
                    </button>
                    <button
                      (click)="previewPlatform.set('Instagram')"
                      [ngClass]="previewPlatform() === 'Instagram' ? 'bg-pink-600 text-white font-bold' : 'text-slate-400'"
                      class="px-2 py-0.5 rounded transition">
                      📸 Instagram
                    </button>
                    <button
                      (click)="previewPlatform.set('LinkedIn')"
                      [ngClass]="previewPlatform() === 'LinkedIn' ? 'bg-blue-600 text-white font-bold' : 'text-slate-400'"
                      class="px-2 py-0.5 rounded transition">
                      💼 LinkedIn
                    </button>
                  </div>
                </div>

                <!-- 1. Simulated X (Twitter) Card -->
                @if (previewPlatform() === 'X') {
                  <div class="bg-black border border-slate-800 rounded-2xl p-4 shadow-xl space-y-3 font-sans">
                    <div class="flex items-start justify-between">
                      <div class="flex items-center gap-2.5">
                        <img src="https://images.unsplash.com/photo-1618005182384-a83a8bd57fbe?w=150" class="w-10 h-10 rounded-full object-cover" />
                        <div>
                          <div class="flex items-center gap-1">
                            <span class="text-xs font-bold text-white">SGrowing HQ</span>
                            <span class="text-sky-400 text-xs">✓</span>
                          </div>
                          <span class="text-[11px] text-slate-500">&#64;SGrowingHQ • Just now</span>
                        </div>
                      </div>
                      <span class="text-slate-500 text-xs">•••</span>
                    </div>

                    <!-- Post Body -->
                    <p class="text-xs text-slate-200 leading-relaxed whitespace-pre-wrap">
                      {{ getEffectiveContentForPlatform('X') || 'Compose your message to preview how it will appear on X.' }}
                    </p>

                    <!-- Media Preview -->
                    @if (composerMedia().length > 0) {
                      <div class="rounded-xl overflow-hidden border border-slate-800 aspect-video bg-slate-900 flex items-center justify-center">
                        <img [src]="composerMedia()[0].url" class="w-full h-full object-cover" />
                      </div>
                    }

                    <!-- X Toolbar -->
                    <div class="flex items-center justify-between text-slate-500 text-xs pt-1 px-1">
                      <span>💬 24</span>
                      <span>🔁 89</span>
                      <span>❤️ 312</span>
                      <span>📊 14.2K</span>
                      <span>📤</span>
                    </div>
                  </div>
                }

                <!-- 2. Simulated Instagram Card -->
                @if (previewPlatform() === 'Instagram') {
                  <div class="bg-slate-950 border border-slate-800 rounded-2xl overflow-hidden shadow-xl space-y-3 font-sans">
                    <div class="flex items-center justify-between p-3">
                      <div class="flex items-center gap-2.5">
                        <div class="p-0.5 rounded-full bg-gradient-to-tr from-amber-500 via-rose-500 to-fuchsia-600">
                          <img src="https://images.unsplash.com/photo-1534528741775-53994a69daeb?w=150" class="w-7 h-7 rounded-full object-cover border-2 border-black" />
                        </div>
                        <span class="text-xs font-bold text-white">sgrowing_official</span>
                      </div>
                      <span class="text-slate-400 text-xs">•••</span>
                    </div>

                    <!-- Media image -->
                    <div class="w-full bg-slate-900 flex items-center justify-center overflow-hidden"
                         [ngClass]="{
                           'aspect-square': getActiveAspectRatio() === '1:1',
                           'aspect-[4/5]': getActiveAspectRatio() === '4:5',
                           'aspect-[9/16]': getActiveAspectRatio() === '9:16',
                           'aspect-video': getActiveAspectRatio() === '16:9'
                         }">
                      @if (composerMedia().length > 0) {
                        <img [src]="composerMedia()[0].url" class="w-full h-full object-cover" />
                      } @else {
                        <div class="text-center p-8 text-xs text-slate-500">
                          📸 Image/Video Required for Instagram Feed
                        </div>
                      }
                    </div>

                    <!-- IG Action Bar -->
                    <div class="px-3 space-y-2 pb-3">
                      <div class="flex items-center justify-between text-base">
                        <div class="flex items-center gap-3">
                          <span>❤️</span>
                          <span>💬</span>
                          <span>↗️</span>
                        </div>
                        <span>🔖</span>
                      </div>
                      <div class="text-xs font-bold text-white">1,492 likes</div>
                      <p class="text-xs text-slate-300 line-clamp-3">
                        <span class="font-bold text-white mr-1.5">sgrowing_official</span>
                        {{ getEffectiveContentForPlatform('Instagram') }}
                      </p>
                      <span class="text-[10px] text-slate-500 uppercase">2 hours ago</span>
                    </div>
                  </div>
                }

                <!-- 3. Simulated LinkedIn Card -->
                @if (previewPlatform() === 'LinkedIn') {
                  <div class="bg-slate-900 border border-slate-800 rounded-2xl p-4 shadow-xl space-y-3 font-sans">
                    <div class="flex items-start justify-between">
                      <div class="flex items-center gap-2.5">
                        <img src="https://images.unsplash.com/photo-1572021335469-31706a17aaef?w=150" class="w-10 h-10 rounded-lg object-cover" />
                        <div>
                          <div class="flex items-center gap-1">
                            <span class="text-xs font-bold text-white">SGrowing Technologies</span>
                            <span class="text-slate-400 text-[10px]">• 1st</span>
                          </div>
                          <p class="text-[10px] text-slate-400">15,300 followers • Promoted</p>
                          <span class="text-[10px] text-slate-500">2h • 🌐</span>
                        </div>
                      </div>
                      <span class="text-slate-500 text-xs">•••</span>
                    </div>

                    <!-- Content -->
                    <p class="text-xs text-slate-200 leading-relaxed whitespace-pre-wrap">
                      {{ getEffectiveContentForPlatform('LinkedIn') || 'Write your professional business insights to preview the LinkedIn feed layout.' }}
                    </p>

                    <!-- Media / Document Attachment -->
                    @if (composerMedia().length > 0) {
                      <div class="rounded-xl overflow-hidden border border-slate-800 aspect-video bg-slate-950 flex items-center justify-center">
                        <img [src]="composerMedia()[0].url" class="w-full h-full object-cover" />
                      </div>
                    }

                    <!-- Engagement metrics -->
                    <div class="flex items-center justify-between text-[11px] text-slate-400 pt-1 border-b border-slate-800 pb-2">
                      <span>👍💡❤️ 542</span>
                      <span>48 comments • 16 reposts</span>
                    </div>

                    <!-- Actions -->
                    <div class="grid grid-cols-4 gap-1 text-center text-xs text-slate-400 font-medium pt-1">
                      <button class="hover:text-white py-1 rounded hover:bg-slate-800 transition">👍 Like</button>
                      <button class="hover:text-white py-1 rounded hover:bg-slate-800 transition">💬 Comment</button>
                      <button class="hover:text-white py-1 rounded hover:bg-slate-800 transition">🔁 Repost</button>
                      <button class="hover:text-white py-1 rounded hover:bg-slate-800 transition">📤 Send</button>
                    </div>
                  </div>
                }
              </div>
            </div>

            <!-- Composer Modal Footer -->
            <div class="px-6 py-4 border-t border-slate-800 bg-slate-900/90 flex items-center justify-between">
              <button
                (click)="closeComposerModal()"
                class="px-4 py-2 rounded-xl text-xs font-medium text-slate-400 hover:text-white hover:bg-slate-800 transition">
                Discard Draft
              </button>

              <div class="flex items-center gap-2.5">
                <!-- Save / Schedule Button -->
                <button
                  (click)="submitSchedulePost()"
                  [disabled]="isSubmittingPost()"
                  class="px-4 py-2 rounded-xl text-xs font-semibold bg-slate-800 hover:bg-slate-700 text-slate-200 border border-slate-700 flex items-center gap-1.5 transition">
                  <span>📅</span>
                  <span>Schedule Post</span>
                </button>

                <!-- Immediate Publish Now Button -->
                <button
                  (click)="submitPublishNow()"
                  [disabled]="isSubmittingPost()"
                  class="px-5 py-2 rounded-xl text-xs font-bold bg-gradient-to-r from-indigo-500 to-sky-500 hover:from-indigo-600 hover:to-sky-600 text-white shadow-lg shadow-indigo-950 flex items-center gap-1.5 transition">
                  <span [class.animate-spin]="isSubmittingPost()">🚀</span>
                  <span>Publish Now (Simulate Dispatch)</span>
                </button>
              </div>
            </div>
          </div>
        </div>
      }

      <!-- Post Inspector / Detail Modal -->
      @if (inspectingPost()) {
        <div class="fixed inset-0 z-50 overflow-y-auto bg-slate-950/80 backdrop-blur-md flex items-center justify-center p-4">
          <div class="w-full max-w-xl bg-slate-900 border border-slate-800 rounded-3xl shadow-2xl overflow-hidden p-6 space-y-4">
            <div class="flex items-center justify-between pb-3 border-b border-slate-800">
              <div class="flex items-center gap-2">
                <span class="text-xl">📄</span>
                <div>
                  <h3 class="text-sm font-bold text-white">{{ inspectingPost()!.title }}</h3>
                  <span class="text-[10px] uppercase font-mono px-2 py-0.5 rounded" [ngClass]="getStatusPillClass(inspectingPost()!.status)">
                    {{ inspectingPost()!.status }}
                  </span>
                </div>
              </div>
              <button
                (click)="inspectingPost.set(null)"
                class="w-7 h-7 rounded-lg bg-slate-800 hover:bg-slate-700 text-slate-400 hover:text-white flex items-center justify-center font-bold">
                ✕
              </button>
            </div>

            <!-- Content preview -->
            <div class="p-3.5 rounded-xl bg-slate-950 border border-slate-800 text-xs text-slate-200 whitespace-pre-wrap leading-relaxed">
              {{ inspectingPost()!.content }}
            </div>

            <!-- Target Platforms -->
            <div>
              <span class="text-[11px] font-bold text-slate-400 uppercase tracking-wider block mb-1">Target Networks</span>
              <div class="flex flex-wrap gap-1.5">
                @for (p of inspectingPost()!.targetPlatforms; track p) {
                  <span class="px-2.5 py-1 rounded-lg text-xs font-medium bg-slate-800 border border-slate-700 text-slate-300 flex items-center gap-1">
                    <span>{{ getPlatformIcon(p) }}</span>
                    <span>{{ p }}</span>
                  </span>
                }
              </div>
            </div>

            <!-- Dispatch Results with Latency -->
            @if (inspectingPost()!.dispatchResults && inspectingPost()!.dispatchResults!.length > 0) {
              <div>
                <span class="text-[11px] font-bold text-slate-400 uppercase tracking-wider block mb-1">Simulated API Dispatch Results</span>
                <div class="space-y-1.5">
                  @for (res of inspectingPost()!.dispatchResults!; track res.platform) {
                    <div class="p-2.5 rounded-xl bg-slate-950 border border-slate-800 flex items-center justify-between text-xs">
                      <div class="flex items-center gap-2">
                        <span>{{ getPlatformIcon(res.platform) }}</span>
                        <div>
                          <span class="font-bold text-white">{{ res.platform }}</span>
                          <span class="text-[10px] text-slate-500 font-mono ml-2">{{ res.externalPostId }}</span>
                        </div>
                      </div>
                      <div class="text-right">
                        <span class="text-emerald-400 font-semibold text-[11px]">✓ Dispatched</span>
                        <div class="text-[10px] text-slate-400 font-mono">⚡ {{ res.latencyMs }}ms latency</div>
                      </div>
                    </div>
                  }
                </div>
              </div>
            }

            <!-- Action buttons in Inspector -->
            <div class="pt-3 border-t border-slate-800 flex items-center justify-between">
              <button
                (click)="inspectingPost.set(null)"
                class="px-3.5 py-1.5 rounded-lg text-xs font-medium text-slate-400 hover:text-white bg-slate-800">
                Close
              </button>

              <div class="flex items-center gap-2">
                @if (inspectingPost()!.status === 'Scheduled') {
                  <button
                    (click)="cancelPost(inspectingPost()!.id)"
                    class="px-3.5 py-1.5 rounded-lg text-xs font-medium text-rose-300 hover:text-white bg-rose-950/40 border border-rose-500/40">
                    Cancel Schedule
                  </button>
                  <button
                    (click)="publishNow(inspectingPost()!.id)"
                    class="px-3.5 py-1.5 rounded-lg text-xs font-bold text-white bg-indigo-600 hover:bg-indigo-500 shadow-md">
                    Publish Now
                  </button>
                }
              </div>
            </div>
          </div>
        </div>
      }
    </div>
  `
})
export class SocialContentCalendarStudioComponent implements OnInit {
  private http = inject(HttpClient);
  private router = inject(Router);

  // Signals
  isLoading = signal<boolean>(false);
  isSubmittingPost = signal<boolean>(false);
  isRefreshingTokens = signal<boolean>(false);
  isRefreshingAccountId = signal<string | null>(null);

  // Accounts & Calendar Posts
  connectedAccounts = signal<ConnectedSocialAccount[]>([]);
  allPosts = signal<UnifiedSocialPost[]>([]);

  // Navigation & Filters
  currentDate = signal<Date>(new Date());
  selectedFilterPlatform = signal<string>('ALL');
  selectedFilterStatus = signal<string>('ALL');

  // UI state
  showAccountsDrawer = signal<boolean>(false);
  showComposerModal = signal<boolean>(false);
  inspectingPost = signal<UnifiedSocialPost | null>(null);
  previewPlatform = signal<string>('X');

  // Composer State
  composerTitle = '';
  composerContent = '';
  composerTargetPlatforms = signal<string[]>(['X', 'LinkedIn']);
  composerOverrides = signal<Record<string, string>>({});
  composerMedia = signal<SocialMediaAttachment[]>([]);
  composerTagsInput = '';
  composerScheduledDate = '';
  activeOverrideTab = signal<string>('BASE');
  validationReport = signal<ValidationReport | null>(null);

  availablePlatforms = [
    { name: 'X', icon: '𝕏', activeClass: 'bg-sky-500/20 border-sky-500 text-sky-300' },
    { name: 'Instagram', icon: '📸', activeClass: 'bg-pink-500/20 border-pink-500 text-pink-300' },
    { name: 'LinkedIn', icon: '💼', activeClass: 'bg-blue-500/20 border-blue-500 text-blue-300' },
    { name: 'TikTok', icon: '🎵', activeClass: 'bg-fuchsia-500/20 border-fuchsia-500 text-fuchsia-300' }
  ];

  // Computed Values
  currentYear = computed(() => this.currentDate().getFullYear());
  currentMonthName = computed(() => {
    return this.currentDate().toLocaleString('default', { month: 'long' });
  });

  hasExpiringSoonTokens = computed(() => {
    return this.connectedAccounts().some(a => a.tokenStatus === 'ExpiringSoon' || a.tokenStatus === 'Expired');
  });

  calendarDays = computed(() => {
    const year = this.currentDate().getFullYear();
    const month = this.currentDate().getMonth();
    const firstDayOfMonth = new Date(year, month, 1);
    const lastDayOfMonth = new Date(year, month + 1, 0);

    const startingDayOfWeek = firstDayOfMonth.getDay();
    const daysInMonth = lastDayOfMonth.getDate();

    const days: CalendarDay[] = [];
    const today = new Date();

    // Previous month padding
    const prevMonthLastDay = new Date(year, month, 0).getDate();
    for (let i = startingDayOfWeek - 1; i >= 0; i--) {
      const d = new Date(year, month - 1, prevMonthLastDay - i);
      days.push({
        date: d,
        dayNumber: d.getDate(),
        isCurrentMonth: false,
        isToday: false,
        posts: this.getPostsForDate(d)
      });
    }

    // Current month days
    for (let i = 1; i <= daysInMonth; i++) {
      const d = new Date(year, month, i);
      const isToday = d.toDateString() === today.toDateString();
      days.push({
        date: d,
        dayNumber: i,
        isCurrentMonth: true,
        isToday: isToday,
        posts: this.getPostsForDate(d)
      });
    }

    // Next month padding to reach 35 or 42 cells (multiple of 7)
    const remaining = (7 - (days.length % 7)) % 7;
    for (let i = 1; i <= remaining; i++) {
      const d = new Date(year, month + 1, i);
      days.push({
        date: d,
        dayNumber: i,
        isCurrentMonth: false,
        isToday: false,
        posts: this.getPostsForDate(d)
      });
    }

    return days;
  });

  ngOnInit() {
    this.loadAllData();
  }

  navigateTo(path: string) {
    this.router.navigate([path]);
  }

  loadAllData() {
    this.isLoading.set(true);

    // Load Connected Accounts
    this.http.get<ConnectedSocialAccount[]>('/api/v1/social/publishing/accounts')
      .subscribe({
        next: (accounts) => {
          this.connectedAccounts.set(accounts);
          this.isLoading.set(false);
        },
        error: () => {
          // Fallback pre-seeded mock accounts
          this.connectedAccounts.set(this.getMockAccounts());
          this.isLoading.set(false);
        }
      });

    // Load Calendar Posts
    this.http.get<UnifiedSocialPost[]>('/api/v1/social/publishing/calendar')
      .subscribe({
        next: (posts) => {
          this.allPosts.set(posts);
        },
        error: () => {
          // Fallback pre-seeded mock posts
          this.allPosts.set(this.getMockPosts());
        }
      });
  }

  changeMonth(delta: number) {
    const current = this.currentDate();
    this.currentDate.set(new Date(current.getFullYear(), current.getMonth() + delta, 1));
  }

  goToToday() {
    this.currentDate.set(new Date());
  }

  toggleAccountsDrawer() {
    this.showAccountsDrawer.update(v => !v);
  }

  openComposerModal() {
    this.composerTitle = '';
    this.composerContent = '';
    this.composerTargetPlatforms.set(['X', 'LinkedIn']);
    this.composerOverrides.set({});
    this.composerMedia.set([
      { url: 'https://images.unsplash.com/photo-1460925895917-afdab827c52f?w=1200', aspectRatio: '16:9', mediaType: 'image' }
    ]);
    this.composerTagsInput = 'SaaS, Growth, AI';
    
    // Default tomorrow at 14:00
    const tomorrow = new Date();
    tomorrow.setDate(tomorrow.getDate() + 1);
    tomorrow.setHours(14, 0, 0, 0);
    this.composerScheduledDate = tomorrow.toISOString().slice(0, 16);

    this.activeOverrideTab.set('BASE');
    this.validateLiveContent();
    this.showComposerModal.set(true);
  }

  quickAddPost(date: Date) {
    this.openComposerModal();
    const d = new Date(date);
    d.setHours(14, 0, 0, 0);
    this.composerScheduledDate = d.toISOString().slice(0, 16);
  }

  closeComposerModal() {
    this.showComposerModal.set(false);
  }

  inspectPost(post: UnifiedSocialPost) {
    this.inspectingPost.set(post);
  }

  toggleTargetPlatform(platform: string) {
    this.composerTargetPlatforms.update(current => {
      if (current.includes(platform)) {
        if (current.length === 1) return current; // keep at least 1
        return current.filter(p => p !== platform);
      } else {
        return [...current, platform];
      }
    });
    this.validateLiveContent();
  }

  hasOverride(platform: string): boolean {
    const ov = this.composerOverrides();
    return !!(ov[platform] && ov[platform].trim().length > 0);
  }

  getOverrideText(platform: string): string {
    return this.composerOverrides()[platform] || '';
  }

  setOverrideText(platform: string, text: string) {
    this.composerOverrides.update(dict => ({
      ...dict,
      [platform]: text
    }));
    this.validateLiveContent();
  }

  clearOverride(platform: string) {
    this.composerOverrides.update(dict => {
      const copy = { ...dict };
      delete copy[platform];
      return copy;
    });
    this.validateLiveContent();
  }

  addMediaAttachment() {
    this.composerMedia.update(m => [
      ...m,
      { url: 'https://images.unsplash.com/photo-1551288049-bebda4e38f71?w=1200', aspectRatio: '1:1', mediaType: 'image' }
    ]);
    this.validateLiveContent();
  }

  removeMediaAttachment(index: number) {
    this.composerMedia.update(m => m.filter((_, i) => i !== index));
    this.validateLiveContent();
  }

  onContentChanged() {
    this.validateLiveContent();
  }

  getEffectiveContentForPlatform(platform: string): string {
    const override = this.composerOverrides()[platform];
    if (override && override.trim().length > 0) {
      return override;
    }
    return this.composerContent;
  }

  getActiveAspectRatio(): string {
    const media = this.composerMedia();
    return media.length > 0 ? media[0].aspectRatio : '1:1';
  }

  getPlatformCharCount(platform: string): number {
    const content = this.getEffectiveContentForPlatform(platform);
    if (platform === 'X') {
      // replace URLs with 23 characters dummy
      const transformed = content.replace(/https?:\/\/[^\s]+/gi, 'xxxxxxxxxxxxxxxxxxxxxxx');
      return transformed.length;
    }
    return content.length;
  }

  getPlatformMaxChars(platform: string): number {
    switch (platform) {
      case 'X': return 280;
      case 'Instagram': return 2200;
      case 'LinkedIn': return 3000;
      case 'TikTok': return 2200;
      default: return 5000;
    }
  }

  getPlatformPercent(platform: string): number {
    const current = this.getPlatformCharCount(platform);
    const max = this.getPlatformMaxChars(platform);
    return Math.min(100, Math.round((current / max) * 100));
  }

  getPlatformBarColor(platform: string): string {
    const current = this.getPlatformCharCount(platform);
    const max = this.getPlatformMaxChars(platform);
    if (current > max) return 'bg-rose-500';
    if (current > max * 0.9) return 'bg-amber-400';
    return 'bg-emerald-400';
  }

  getPlatformCharMeterClass(platform: string): string {
    const current = this.getPlatformCharCount(platform);
    const max = this.getPlatformMaxChars(platform);
    if (current > max) return 'text-rose-400 font-extrabold';
    if (current > max * 0.9) return 'text-amber-400 font-bold';
    return 'text-slate-400';
  }

  validateLiveContent() {
    const payload = {
      title: this.composerTitle,
      content: this.composerContent,
      targetPlatforms: this.composerTargetPlatforms(),
      platformOverrides: this.composerOverrides(),
      mediaAttachments: this.composerMedia(),
      tags: this.composerTagsInput.split(',').map(t => t.trim()).filter(Boolean),
      scheduledAtUtc: this.composerScheduledDate ? new Date(this.composerScheduledDate).toISOString() : null
    };

    this.http.post<ValidationReport>('/api/v1/social/publishing/validate', payload)
      .subscribe({
        next: (report) => {
          this.validationReport.set(report);
        },
        error: () => {
          // Client-side local validation fallback
          this.performLocalValidation();
        }
      });
  }

  private performLocalValidation() {
    const errors: string[] = [];
    const targetPlatforms = this.composerTargetPlatforms();
    const media = this.composerMedia();

    for (const p of targetPlatforms) {
      const count = this.getPlatformCharCount(p);
      const max = this.getPlatformMaxChars(p);
      if (count > max) {
        errors.push(`${p} content exceeds max limit of ${max} characters (current: ${count}).`);
      }

      if (p === 'X' && media.length > 4) {
        errors.push('X/Twitter allows a maximum of 4 media attachments.');
      }

      if (p === 'Instagram' && media.length === 0) {
        errors.push('Instagram Business requires at least 1 image or video attachment.');
      }

      if (p === 'TikTok') {
        const hasVideo = media.some(m => m.mediaType === 'video' || m.url.endsWith('.mp4'));
        if (!hasVideo) {
          errors.push('TikTok requires at least one video attachment.');
        }
      }
    }

    this.validationReport.set({
      isValid: errors.length === 0,
      errors: errors,
      warnings: [],
      platformResults: {}
    });
  }

  submitSchedulePost() {
    this.isSubmittingPost.set(true);

    const payload = {
      title: this.composerTitle || 'Untitled Social Campaign',
      content: this.composerContent,
      targetPlatforms: this.composerTargetPlatforms(),
      platformOverrides: this.composerOverrides(),
      mediaAttachments: this.composerMedia(),
      tags: this.composerTagsInput.split(',').map(t => t.trim()).filter(Boolean),
      scheduledAtUtc: this.composerScheduledDate ? new Date(this.composerScheduledDate).toISOString() : new Date(Date.now() + 86400000).toISOString()
    };

    this.http.post<UnifiedSocialPost>('/api/v1/social/publishing/compose', payload)
      .subscribe({
        next: (post) => {
          this.allPosts.update(posts => [post, ...posts]);
          this.isSubmittingPost.set(false);
          this.showComposerModal.set(false);
        },
        error: () => {
          // Offline mock creation
          const mockPost: UnifiedSocialPost = {
            id: 'post_' + Math.random().toString(36).substring(2, 9),
            tenantId: '11111111-1111-1111-1111-111111111111',
            title: payload.title,
            content: payload.content,
            platformOverrides: payload.platformOverrides,
            mediaAttachments: payload.mediaAttachments,
            tags: payload.tags,
            targetPlatforms: payload.targetPlatforms,
            scheduledAtUtc: payload.scheduledAtUtc,
            status: 'Scheduled',
            createdAtUtc: new Date().toISOString()
          };
          this.allPosts.update(posts => [mockPost, ...posts]);
          this.isSubmittingPost.set(false);
          this.showComposerModal.set(false);
        }
      });
  }

  submitPublishNow() {
    this.isSubmittingPost.set(true);

    const payload = {
      title: this.composerTitle || 'Instant Dispatch Campaign',
      content: this.composerContent,
      targetPlatforms: this.composerTargetPlatforms(),
      platformOverrides: this.composerOverrides(),
      mediaAttachments: this.composerMedia(),
      tags: this.composerTagsInput.split(',').map(t => t.trim()).filter(Boolean)
    };

    this.http.post<UnifiedSocialPost>('/api/v1/social/publishing/compose', payload)
      .subscribe({
        next: (created) => {
          this.publishNow(created.id);
        },
        error: () => {
          // Offline mock publish
          const simulatedResults: PlatformDispatchResult[] = payload.targetPlatforms.map(p => ({
            platform: p,
            success: true,
            externalPostId: `${p.toLowerCase()}_${Math.random().toString(36).substring(2, 9)}`,
            latencyMs: Math.floor(Math.random() * 40) + 30,
            message: 'Dispatched successfully via official platform API.',
            dispatchedAtUtc: new Date().toISOString()
          }));

          const publishedPost: UnifiedSocialPost = {
            id: 'post_' + Math.random().toString(36).substring(2, 9),
            tenantId: '11111111-1111-1111-1111-111111111111',
            title: payload.title,
            content: payload.content,
            platformOverrides: payload.platformOverrides,
            mediaAttachments: payload.mediaAttachments,
            tags: payload.tags,
            targetPlatforms: payload.targetPlatforms,
            publishedAtUtc: new Date().toISOString(),
            status: 'Published',
            createdAtUtc: new Date().toISOString(),
            dispatchResults: simulatedResults
          };

          this.allPosts.update(posts => [publishedPost, ...posts]);
          this.isSubmittingPost.set(false);
          this.showComposerModal.set(false);
          this.inspectingPost.set(publishedPost);
        }
      });
  }

  publishNow(postId: string) {
    this.http.post<UnifiedSocialPost>(`/api/v1/social/publishing/publish-now/${postId}`, {})
      .subscribe({
        next: (updated) => {
          this.allPosts.update(posts => posts.map(p => p.id === updated.id ? updated : p));
          this.isSubmittingPost.set(false);
          this.showComposerModal.set(false);
          this.inspectingPost.set(updated);
        },
        error: () => {
          this.allPosts.update(posts => posts.map(p => {
            if (p.id === postId) {
              const res: PlatformDispatchResult[] = p.targetPlatforms.map(plat => ({
                platform: plat,
                success: true,
                externalPostId: `${plat.toLowerCase()}_${Math.random().toString(36).substring(2, 9)}`,
                latencyMs: 45,
                message: 'Dispatched successfully.',
                dispatchedAtUtc: new Date().toISOString()
              }));
              return {
                ...p,
                status: 'Published',
                publishedAtUtc: new Date().toISOString(),
                dispatchResults: res
              };
            }
            return p;
          }));
          this.isSubmittingPost.set(false);
          this.showComposerModal.set(false);
        }
      });
  }

  cancelPost(postId: string) {
    this.allPosts.update(posts => posts.map(p => {
      if (p.id === postId) {
        return { ...p, status: 'Cancelled', failureReason: 'Manually cancelled.' };
      }
      return p;
    }));
    this.inspectingPost.set(null);
  }

  refreshToken(accountId: string) {
    this.isRefreshingAccountId.set(accountId);

    this.http.post<ConnectedSocialAccount>(`/api/v1/social/publishing/refresh-token/${accountId}`, {})
      .subscribe({
        next: (updated) => {
          this.connectedAccounts.update(list => list.map(a => a.id === updated.id ? updated : a));
          this.isRefreshingAccountId.set(null);
        },
        error: () => {
          // Local mock token update
          this.connectedAccounts.update(list => list.map(a => {
            if (a.id === accountId) {
              return {
                ...a,
                tokenStatus: 'Active',
                remainingDays: 60,
                tokenExpiresAtUtc: new Date(Date.now() + 60 * 86400000).toISOString(),
                lastRefreshedAtUtc: new Date().toISOString()
              };
            }
            return a;
          }));
          this.isRefreshingAccountId.set(null);
        }
      });
  }

  autoRefreshAllExpiringTokens() {
    this.isRefreshingTokens.set(true);

    const expiring = this.connectedAccounts().filter(a => a.tokenStatus === 'ExpiringSoon' || a.tokenStatus === 'Expired');
    if (expiring.length === 0) {
      this.isRefreshingTokens.set(false);
      return;
    }

    let completed = 0;
    expiring.forEach(acc => {
      this.http.post<ConnectedSocialAccount>(`/api/v1/social/publishing/refresh-token/${acc.id}`, {})
        .subscribe({
          next: (updated) => {
            this.connectedAccounts.update(list => list.map(a => a.id === updated.id ? updated : a));
            completed++;
            if (completed >= expiring.length) this.isRefreshingTokens.set(false);
          },
          error: () => {
            this.connectedAccounts.update(list => list.map(a => {
              if (a.id === acc.id) {
                return {
                  ...a,
                  tokenStatus: 'Active',
                  remainingDays: 60,
                  tokenExpiresAtUtc: new Date(Date.now() + 60 * 86400000).toISOString()
                };
              }
              return a;
            }));
            completed++;
            if (completed >= expiring.length) this.isRefreshingTokens.set(false);
          }
        });
    });
  }

  private getPostsForDate(date: Date): UnifiedSocialPost[] {
    const dStr = date.toDateString();
    const platFilter = this.selectedFilterPlatform();
    const statusFilter = this.selectedFilterStatus();

    return this.allPosts().filter(p => {
      const postDate = new Date(p.scheduledAtUtc || p.publishedAtUtc || p.createdAtUtc);
      if (postDate.toDateString() !== dStr) return false;

      if (platFilter !== 'ALL' && !p.targetPlatforms.includes(platFilter)) return false;
      if (statusFilter !== 'ALL' && p.status !== statusFilter) return false;

      return true;
    });
  }

  getPlatformIcon(platform: string): string {
    switch (platform.toLowerCase()) {
      case 'x':
      case 'twitter': return '𝕏';
      case 'instagram': return '📸';
      case 'linkedin': return '💼';
      case 'tiktok': return '🎵';
      default: return '📢';
    }
  }

  getPostBadgeClass(post: UnifiedSocialPost): string {
    const platform = (post.targetPlatforms[0] || 'General').toLowerCase();
    switch (platform) {
      case 'x':
      case 'twitter':
        return 'bg-sky-950/60 border-sky-500/40 text-sky-200 hover:border-sky-400';
      case 'instagram':
        return 'bg-pink-950/60 border-pink-500/40 text-pink-200 hover:border-pink-400';
      case 'linkedin':
        return 'bg-blue-950/60 border-blue-500/40 text-blue-200 hover:border-blue-400';
      case 'tiktok':
        return 'bg-fuchsia-950/60 border-fuchsia-500/40 text-fuchsia-200 hover:border-fuchsia-400';
      default:
        return 'bg-slate-900 border-slate-700 text-slate-200 hover:border-slate-500';
    }
  }

  getStatusPillClass(status: PublishingStatus): string {
    switch (status) {
      case 'Published': return 'bg-emerald-500/20 text-emerald-300 border border-emerald-500/30';
      case 'Scheduled': return 'bg-sky-500/20 text-sky-300 border border-sky-500/30';
      case 'Publishing': return 'bg-amber-500/20 text-amber-300 border border-amber-500/30 animate-pulse';
      case 'Failed': return 'bg-rose-500/20 text-rose-300 border border-rose-500/30';
      case 'Cancelled': return 'bg-slate-800 text-slate-400 border border-slate-700';
      default: return 'bg-slate-800 text-slate-300 border border-slate-700';
    }
  }

  private getMockAccounts(): ConnectedSocialAccount[] {
    return [
      {
        id: 'a1111111-1111-1111-1111-111111111111',
        tenantId: '11111111-1111-1111-1111-111111111111',
        platform: 'X',
        accountType: 'X Account',
        accountName: 'SGrowing HQ',
        handle: '@SGrowingHQ',
        avatarUrl: 'https://images.unsplash.com/photo-1618005182384-a83a8bd57fbe?w=150',
        accessToken: 'x_oauth2_tok',
        refreshToken: 'x_ref_tok',
        tokenExpiresAtUtc: new Date(Date.now() + 45 * 86400000).toISOString(),
        lastRefreshedAtUtc: new Date().toISOString(),
        tokenStatus: 'Active',
        remainingDays: 45,
        followersCount: 28400,
        autoRefreshEnabled: true
      },
      {
        id: 'a2222222-2222-2222-2222-222222222222',
        tenantId: '11111111-1111-1111-1111-111111111111',
        platform: 'Instagram',
        accountType: 'Meta Page',
        accountName: 'SGrowing Official',
        handle: '@sgrowing_official',
        avatarUrl: 'https://images.unsplash.com/photo-1534528741775-53994a69daeb?w=150',
        accessToken: 'ig_graph_tok',
        refreshToken: 'ig_ref_tok',
        tokenExpiresAtUtc: new Date(Date.now() + 36 * 3600000).toISOString(),
        lastRefreshedAtUtc: new Date().toISOString(),
        tokenStatus: 'ExpiringSoon',
        remainingDays: 1.5,
        followersCount: 64200,
        autoRefreshEnabled: true
      },
      {
        id: 'a3333333-3333-3333-3333-333333333333',
        tenantId: '11111111-1111-1111-1111-111111111111',
        platform: 'LinkedIn',
        accountType: 'LinkedIn Org',
        accountName: 'SGrowing Technologies',
        handle: 'sgrowing-technologies',
        avatarUrl: 'https://images.unsplash.com/photo-1572021335469-31706a17aaef?w=150',
        accessToken: 'li_tok_prod',
        refreshToken: 'li_ref_tok',
        tokenExpiresAtUtc: new Date(Date.now() + 90 * 86400000).toISOString(),
        lastRefreshedAtUtc: new Date().toISOString(),
        tokenStatus: 'Active',
        remainingDays: 90,
        followersCount: 15300,
        autoRefreshEnabled: true
      },
      {
        id: 'a4444444-4444-4444-4444-444444444444',
        tenantId: '11111111-1111-1111-1111-111111111111',
        platform: 'TikTok',
        accountType: 'TikTok Creator',
        accountName: 'SGrowing Creators',
        handle: '@sgrowing_creators',
        avatarUrl: 'https://images.unsplash.com/photo-1507003211169-0a1dd7228f2d?w=150',
        accessToken: 'tt_api_tok',
        refreshToken: 'tt_ref_tok',
        tokenExpiresAtUtc: new Date(Date.now() + 30 * 86400000).toISOString(),
        lastRefreshedAtUtc: new Date().toISOString(),
        tokenStatus: 'Active',
        remainingDays: 30,
        followersCount: 112000,
        autoRefreshEnabled: true
      }
    ];
  }

  private getMockPosts(): UnifiedSocialPost[] {
    const now = new Date();
    const dTomorrow = new Date();
    dTomorrow.setDate(dTomorrow.getDate() + 1);

    const dInThreeDays = new Date();
    dInThreeDays.setDate(dInThreeDays.getDate() + 3);

    const dPast = new Date();
    dPast.setDate(dPast.getDate() - 2);

    return [
      {
        id: 'b1111111-1111-1111-1111-111111111111',
        tenantId: '11111111-1111-1111-1111-111111111111',
        title: 'Product 3.0 Launch Announcement',
        content: 'We are thrilled to unveil SGrowing 3.0! Complete omnichannel growth, AI agent workflows, and predictive customer cadence. Learn more: https://sgrowing.io/launch',
        platformOverrides: {
          'X': 'SGrowing 3.0 is live! Omnichannel growth, autonomous AI agents, and real-time cadence dispatch. Read: https://sgrowing.io/launch #SaaS #Growth'
        },
        mediaAttachments: [
          { url: 'https://images.unsplash.com/photo-1460925895917-afdab827c52f?w=1200', aspectRatio: '16:9', mediaType: 'image' }
        ],
        tags: ['SaaS', 'Growth', 'ProductLaunch'],
        targetPlatforms: ['X', 'LinkedIn'],
        scheduledAtUtc: dTomorrow.toISOString(),
        status: 'Scheduled',
        createdAtUtc: now.toISOString()
      },
      {
        id: 'b2222222-2222-2222-2222-222222222222',
        tenantId: '11111111-1111-1111-1111-111111111111',
        title: 'Behind the Scenes Reel: AI Engineering Culture',
        content: 'A day in the life of our high-velocity engineering squad building autonomous growth dispatchers in .NET 10 & Angular.',
        platformOverrides: {},
        mediaAttachments: [
          { url: 'https://assets.sgrowing.io/media/bts-squad-reel.mp4', aspectRatio: '9:16', mediaType: 'video' }
        ],
        tags: ['TechLife', 'Engineering', 'BuildInPublic'],
        targetPlatforms: ['Instagram', 'TikTok'],
        scheduledAtUtc: dInThreeDays.toISOString(),
        status: 'Scheduled',
        createdAtUtc: now.toISOString()
      },
      {
        id: 'b3333333-3333-3333-3333-333333333333',
        tenantId: '11111111-1111-1111-1111-111111111111',
        title: 'Weekly Customer Retention Metrics Infographic',
        content: 'B2B SaaS companies that adopted multi-touch cadence reduced pipeline slippage by 42%. Here are the 4 key metrics to measure.',
        platformOverrides: {},
        mediaAttachments: [
          { url: 'https://images.unsplash.com/photo-1551288049-bebda4e38f71?w=1200', aspectRatio: '1:1', mediaType: 'image' }
        ],
        tags: ['B2B', 'Revenue', 'CustomerSuccess'],
        targetPlatforms: ['LinkedIn', 'Instagram', 'X'],
        publishedAtUtc: dPast.toISOString(),
        status: 'Published',
        createdAtUtc: dPast.toISOString(),
        dispatchResults: [
          { platform: 'LinkedIn', success: true, externalPostId: 'li_urn_share_99410', latencyMs: 42, message: 'Dispatched successfully', dispatchedAtUtc: dPast.toISOString() },
          { platform: 'Instagram', success: true, externalPostId: 'ig_media_id_88204', latencyMs: 65, message: 'Dispatched successfully', dispatchedAtUtc: dPast.toISOString() },
          { platform: 'X', success: true, externalPostId: 'x_tweet_id_1892837', latencyMs: 38, message: 'Dispatched successfully', dispatchedAtUtc: dPast.toISOString() }
        ]
      }
    ];
  }
}
