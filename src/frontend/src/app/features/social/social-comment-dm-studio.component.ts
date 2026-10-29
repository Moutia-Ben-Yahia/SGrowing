import { Component, OnInit, signal, computed, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { ApiService } from '../../core/services/api.service';
import { AuthService } from '../../core/services/auth.service';
import {
  SocialPlatform,
  SocialRuleMatchType,
  CommentProcessingStatus,
  SocialCommentRuleDto,
  IncomingSocialCommentDto,
  TwoPhaseReactionResultDto,
  CommentProcessingLogDto,
  SocialCommentFunnelMetricsDto,
  PlatformFunnelBreakdownDto
} from '../../core/models/social.models';

@Component({
  selector: 'app-social-comment-dm-studio',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterLink],
  template: `
    <div class="flex flex-col min-h-screen bg-slate-950 text-slate-100 font-sans" dir="ltr">
      <!-- Top Navigation Header -->
      <header class="h-16 border-b border-slate-800 bg-slate-900/60 backdrop-blur px-6 flex items-center justify-between shrink-0 sticky top-0 z-20">
        <div class="flex items-center gap-3">
          <a routerLink="/social" class="text-xs text-slate-400 hover:text-white flex items-center gap-1 transition px-2.5 py-1.5 rounded-lg bg-slate-800/60 hover:bg-slate-800">
            ← Social Studio
          </a>
          <span class="text-slate-700">|</span>
          <div class="w-9 h-9 rounded-xl bg-gradient-to-tr from-cyan-500 via-indigo-600 to-fuchsia-600 text-white flex items-center justify-center font-bold text-sm shadow-lg shadow-cyan-950/50">
            💬
          </div>
          <div>
            <div class="flex items-center gap-2">
              <h1 class="text-base font-bold text-white tracking-tight">Social Comment-to-DM Inbound Lead Engine</h1>
              <span class="px-2 py-0.5 rounded-full text-[10px] font-semibold bg-cyan-500/10 text-cyan-400 border border-cyan-500/30">
                Horizon 14 • Active
              </span>
            </div>
            <p class="text-xs text-slate-400">Two-phase atomic reaction: public viral reply + private direct DM with VIP coupon</p>
          </div>
        </div>

        <div class="flex items-center gap-3">
          <div class="hidden md:flex items-center gap-2 text-xs text-slate-400 bg-slate-900/80 px-3 py-1.5 rounded-lg border border-slate-800">
            <span class="w-2 h-2 rounded-full bg-emerald-400 animate-pulse"></span>
            <span>4 Platforms Active: IG, TikTok, FB, YouTube</span>
          </div>

          <button
            (click)="refreshAll()"
            [disabled]="isLoading()"
            class="px-3.5 py-1.5 rounded-lg text-xs font-medium bg-slate-800 hover:bg-slate-700 text-slate-200 border border-slate-700 flex items-center gap-1.5 transition">
            <span [class.animate-spin]="isLoading()">🔄</span>
            <span>Refresh</span>
          </button>
        </div>
      </header>

      <!-- Navigation Tabs -->
      <div class="border-b border-slate-800 bg-slate-900/30 px-6 flex items-center gap-2">
        <button
          (click)="activeTab.set('simulator')"
          class="px-4 py-3 text-xs font-medium border-b-2 transition flex items-center gap-2"
          [class]="activeTab() === 'simulator' ? 'border-cyan-400 text-cyan-300 bg-cyan-950/20' : 'border-transparent text-slate-400 hover:text-slate-200'">
          <span>⚡ Live Comment Simulator</span>
          <span class="px-1.5 py-0.2 rounded text-[10px] bg-cyan-500/20 text-cyan-300">Live</span>
        </button>

        <button
          (click)="activeTab.set('rules')"
          class="px-4 py-3 text-xs font-medium border-b-2 transition flex items-center gap-2"
          [class]="activeTab() === 'rules' ? 'border-purple-400 text-purple-300 bg-purple-950/20' : 'border-transparent text-slate-400 hover:text-slate-200'">
          <span>⚙️ Keyword Rules Engine</span>
          <span class="px-1.5 py-0.2 rounded text-[10px] bg-purple-500/20 text-purple-300">{{ rules().length }}</span>
        </button>

        <button
          (click)="activeTab.set('funnel')"
          class="px-4 py-3 text-xs font-medium border-b-2 transition flex items-center gap-2"
          [class]="activeTab() === 'funnel' ? 'border-emerald-400 text-emerald-300 bg-emerald-950/20' : 'border-transparent text-slate-400 hover:text-slate-200'">
          <span>📊 Inbound Conversion Funnel</span>
          <span class="px-1.5 py-0.2 rounded text-[10px] bg-emerald-500/20 text-emerald-300">
            \${{ funnelMetrics()?.totalRevenue || 0 | number:'1.0-0' }}
          </span>
        </button>

        <button
          (click)="activeTab.set('logs')"
          class="px-4 py-3 text-xs font-medium border-b-2 transition flex items-center gap-2"
          [class]="activeTab() === 'logs' ? 'border-indigo-400 text-indigo-300 bg-indigo-950/20' : 'border-transparent text-slate-400 hover:text-slate-200'">
          <span>📜 Reaction Audit Logs</span>
          <span class="px-1.5 py-0.2 rounded text-[10px] bg-slate-800 text-slate-300">{{ logs().length }}</span>
        </button>
      </div>

      <!-- Main Content Area -->
      <main class="flex-1 p-6 max-w-7xl w-full mx-auto space-y-6">

        <!-- ========================================================================= -->
        <!-- TAB 1: LIVE COMMENT SIMULATOR                                              -->
        <!-- ========================================================================= -->
        @if (activeTab() === 'simulator') {
          <div class="grid grid-cols-1 lg:grid-cols-12 gap-6">
            <!-- Left Column: Comment Ingestion Simulator Form -->
            <div class="lg:col-span-5 bg-slate-900/70 border border-slate-800 rounded-2xl p-5 space-y-5 shadow-xl">
              <div>
                <h2 class="text-sm font-semibold text-white flex items-center gap-2">
                  <span class="w-2 h-2 rounded-full bg-cyan-400"></span>
                  Simulate Social Media Comment Event
                </h2>
                <p class="text-xs text-slate-400 mt-1">
                  Triggers webhook normalizer, evaluates keyword rules, enforces jitter & deduplication guardrails.
                </p>
              </div>

              <!-- Platform Picker -->
              <div>
                <label class="block text-xs font-medium text-slate-300 mb-2">Target Social Platform</label>
                <div class="grid grid-cols-4 gap-2">
                  @for (p of ['Instagram', 'TikTok', 'Facebook', 'YouTube']; track p) {
                    <button
                      type="button"
                      (click)="simPlatform.set(p)"
                      class="py-2 px-2.5 rounded-xl border text-xs font-medium flex flex-col items-center justify-center gap-1 transition"
                      [class]="simPlatform() === p 
                        ? 'bg-cyan-500/10 border-cyan-500 text-cyan-300 shadow-md shadow-cyan-950/40' 
                        : 'bg-slate-950 border-slate-800 text-slate-400 hover:border-slate-700'">
                      <span>{{ getPlatformIcon(p) }}</span>
                      <span>{{ p }}</span>
                    </button>
                  }
                </div>
              </div>

              <!-- Post Context -->
              <div class="space-y-3">
                <div>
                  <label class="block text-xs font-medium text-slate-300 mb-1">Post Title / Reel Caption</label>
                  <input
                    type="text"
                    [ngModel]="simPostTitle()"
                    (ngModelChange)="simPostTitle.set($event)"
                    placeholder="e.g. 🔥 Black Friday Launch: Comment 'PRICE' to get 20% off!"
                    class="w-full bg-slate-950 border border-slate-800 rounded-xl px-3.5 py-2 text-xs text-slate-200 focus:outline-none focus:border-cyan-500 transition" />
                </div>

                <div class="grid grid-cols-2 gap-3">
                  <div>
                    <label class="block text-xs font-medium text-slate-300 mb-1">Post ID</label>
                    <input
                      type="text"
                      [ngModel]="simPostId()"
                      (ngModelChange)="simPostId.set($event)"
                      placeholder="e.g. post_reel_842"
                      class="w-full bg-slate-950 border border-slate-800 rounded-xl px-3 py-2 text-xs text-slate-200 focus:outline-none focus:border-cyan-500 transition" />
                  </div>
                  <div>
                    <label class="block text-xs font-medium text-slate-300 mb-1">Commenter Handle</label>
                    <input
                      type="text"
                      [ngModel]="simCommenter()"
                      (ngModelChange)="simCommenter.set($event)"
                      placeholder="e.g. emily_travels"
                      class="w-full bg-slate-950 border border-slate-800 rounded-xl px-3 py-2 text-xs text-slate-200 focus:outline-none focus:border-cyan-500 transition" />
                  </div>
                </div>
              </div>

              <!-- Comment Presets -->
              <div>
                <label class="block text-xs font-medium text-slate-300 mb-1.5">Quick Inbound Presets</label>
                <div class="flex flex-wrap gap-1.5">
                  <button
                    type="button"
                    (click)="applyPreset('PRICE please! How much is shipping?')"
                    class="px-2.5 py-1 rounded-lg text-[11px] bg-slate-800 hover:bg-slate-700 text-cyan-300 border border-slate-700 transition">
                    "PRICE please!"
                  </button>
                  <button
                    type="button"
                    (click)="applyPreset('COUPON')"
                    class="px-2.5 py-1 rounded-lg text-[11px] bg-slate-800 hover:bg-slate-700 text-purple-300 border border-slate-700 transition">
                    "COUPON" (Exact)
                  </button>
                  <button
                    type="button"
                    (click)="applyPreset('How much does this cost? Can you send me the link?')"
                    class="px-2.5 py-1 rounded-lg text-[11px] bg-slate-800 hover:bg-slate-700 text-emerald-300 border border-slate-700 transition">
                    "How much? Send link"
                  </button>
                  <button
                    type="button"
                    (click)="applyPreset('BUY NOW')"
                    class="px-2.5 py-1 rounded-lg text-[11px] bg-slate-800 hover:bg-slate-700 text-amber-300 border border-slate-700 transition">
                    "BUY NOW"
                  </button>
                  <button
                    type="button"
                    (click)="applyPreset('This is a scam do not buy, terrible fake product!')"
                    class="px-2.5 py-1 rounded-lg text-[11px] bg-rose-950/40 hover:bg-rose-900/50 text-rose-300 border border-rose-800/40 transition">
                    "Scam fake" (Negative Sentiment)
                  </button>
                </div>
              </div>

              <!-- Comment Textarea -->
              <div>
                <label class="block text-xs font-medium text-slate-300 mb-1">Incoming Comment Text</label>
                <textarea
                  rows="3"
                  [ngModel]="simComment()"
                  (ngModelChange)="simComment.set($event)"
                  placeholder="Type or paste any user comment..."
                  class="w-full bg-slate-950 border border-slate-800 rounded-xl p-3 text-xs text-slate-100 focus:outline-none focus:border-cyan-500 transition"></textarea>
              </div>

              <!-- Submit Button -->
              <button
                type="button"
                (click)="simulateComment()"
                [disabled]="isSimulating() || !simComment().trim()"
                class="w-full py-2.5 px-4 rounded-xl font-medium text-xs bg-gradient-to-r from-cyan-500 via-indigo-600 to-fuchsia-600 hover:from-cyan-400 hover:to-fuchsia-500 text-white shadow-lg shadow-cyan-950/50 flex items-center justify-center gap-2 transition disabled:opacity-50 disabled:cursor-not-allowed">
                @if (isSimulating()) {
                  <span class="animate-spin">⏳</span>
                  <span>Executing Two-Phase Reaction Engine...</span>
                } @else {
                  <span>⚡ Dispatch Inbound Webhook</span>
                }
              </button>
            </div>

            <!-- Right Column: Two-Phase Reaction Live Display -->
            <div class="lg:col-span-7 space-y-4">
              @if (!lastResult()) {
                <div class="h-full min-h-[460px] bg-slate-900/40 border border-dashed border-slate-800 rounded-2xl flex flex-col items-center justify-center p-8 text-center space-y-4">
                  <div class="w-16 h-16 rounded-2xl bg-cyan-500/10 border border-cyan-500/20 text-cyan-400 flex items-center justify-center text-3xl">
                    🚀
                  </div>
                  <div class="max-w-md">
                    <h3 class="text-sm font-semibold text-white">Ready for Two-Phase Reaction</h3>
                    <p class="text-xs text-slate-400 mt-1">
                      Click <strong class="text-cyan-400">Dispatch Inbound Webhook</strong> or choose a preset to witness atomic public engagement boost and private DM delivery with customized UTM tracking.
                    </p>
                  </div>
                  <div class="flex items-center gap-4 text-xs text-slate-500">
                    <span class="flex items-center gap-1">🔒 1-DM/Post Dedup</span>
                    <span class="flex items-center gap-1">⏱️ 5-30s Human Jitter</span>
                    <span class="flex items-center gap-1">🛡️ Anti-Shadowban Limiter</span>
                  </div>
                </div>
              } @else {
                <!-- Reaction Status Banner -->
                <div class="rounded-2xl p-4 border flex items-center justify-between"
                  [class]="getStatusBannerClass(lastResult()!.status)">
                  <div class="flex items-center gap-3">
                    <span class="text-xl">{{ getStatusIcon(lastResult()!.status) }}</span>
                    <div>
                      <div class="flex items-center gap-2">
                        <span class="text-xs font-bold uppercase tracking-wider">{{ getStatusLabel(lastResult()!.status) }}</span>
                        @if (lastResult()!.matched) {
                          <span class="px-2 py-0.5 rounded text-[10px] bg-cyan-950 text-cyan-300 border border-cyan-800">
                            Matched: {{ lastResult()!.matchedRuleName }} [{{ lastResult()!.matchedKeyword }}]
                          </span>
                        }
                      </div>
                      <p class="text-xs mt-0.5 opacity-90">{{ lastResult()!.message }}</p>
                    </div>
                  </div>

                  @if (lastResult()!.jitterDelaySeconds > 0) {
                    <div class="text-right">
                      <span class="text-[10px] uppercase font-bold text-slate-400 block">Jitter Delay</span>
                      <span class="text-xs font-mono font-bold text-amber-300">{{ lastResult()!.jitterDelaySeconds }}s</span>
                    </div>
                  }
                </div>

                <!-- Two-Phase Container -->
                <div class="grid grid-cols-1 md:grid-cols-2 gap-4">
                  <!-- Phase 1: Public Engagement Comment Reply -->
                  <div class="bg-slate-900/80 border border-slate-800 rounded-2xl p-4 space-y-3">
                    <div class="flex items-center justify-between">
                      <span class="text-[11px] font-bold uppercase tracking-wider text-cyan-400 flex items-center gap-1.5">
                        <span class="w-1.5 h-1.5 rounded-full bg-cyan-400"></span>
                        Phase 1: Public Reply
                      </span>
                      <span class="text-[10px] px-2 py-0.5 rounded-full bg-cyan-950 text-cyan-300 border border-cyan-800">
                        Boosts Post Algorithm
                      </span>
                    </div>

                    <!-- Simulated Social Post Thread -->
                    <div class="bg-slate-950 border border-slate-800/80 rounded-xl p-3 space-y-3">
                      <!-- User's Comment -->
                      <div class="flex items-start gap-2.5">
                        <div class="w-7 h-7 rounded-full bg-slate-800 border border-slate-700 flex items-center justify-center text-xs font-bold text-slate-300 shrink-0">
                          {{ lastResult()!.commenterUsername.charAt(0).toUpperCase() }}
                        </div>
                        <div class="text-xs">
                          <span class="font-semibold text-slate-200">{{ '@' + lastResult()!.commenterUsername }}</span>
                          <p class="text-slate-400 mt-0.5">{{ simComment() }}</p>
                        </div>
                      </div>

                      <!-- Brand's Public Reply -->
                      @if (lastResult()!.publicReply) {
                        <div class="ml-6 pl-3 border-l-2 border-cyan-500/50 flex items-start gap-2.5 pt-1">
                          <div class="w-6 h-6 rounded-full bg-cyan-600 flex items-center justify-center text-[10px] font-bold text-white shrink-0">
                            SG
                          </div>
                          <div class="text-xs">
                            <div class="flex items-center gap-1">
                              <span class="font-semibold text-cyan-300">SGrowing Official</span>
                              <span class="text-[9px] bg-cyan-500/20 text-cyan-400 px-1 rounded">Verified</span>
                            </div>
                            <p class="text-slate-200 mt-0.5 bg-slate-900/90 p-2 rounded-lg border border-slate-800">
                              {{ lastResult()!.publicReply }}
                            </p>
                          </div>
                        </div>
                      } @else {
                        <p class="text-xs text-slate-500 italic ml-6">No public reply generated.</p>
                      }
                    </div>

                    <div class="text-[10px] text-slate-500 flex items-center justify-between">
                      <span>Platform: {{ simPlatform() }}</span>
                      <span>Rotated from 5+ templates</span>
                    </div>
                  </div>

                  <!-- Phase 2: Private 1-on-1 Direct Message -->
                  <div class="bg-slate-900/80 border border-slate-800 rounded-2xl p-4 space-y-3">
                    <div class="flex items-center justify-between">
                      <span class="text-[11px] font-bold uppercase tracking-wider text-purple-400 flex items-center gap-1.5">
                        <span class="w-1.5 h-1.5 rounded-full bg-purple-400"></span>
                        Phase 2: Private DM
                      </span>
                      @if (lastResult()!.couponCode) {
                        <span class="text-[10px] px-2 py-0.5 rounded-full bg-purple-950 text-purple-300 border border-purple-800 font-mono font-bold">
                          {{ lastResult()!.couponCode }}
                        </span>
                      }
                    </div>

                    <!-- Simulated DM Frame -->
                    <div class="bg-slate-950 border border-slate-800/80 rounded-xl p-3 space-y-3">
                      <div class="flex items-center justify-between pb-2 border-b border-slate-800/60 text-[11px] text-slate-400">
                        <span class="flex items-center gap-1.5">
                          <span class="w-2 h-2 rounded-full bg-emerald-400"></span>
                          Direct Message to {{ '@' + lastResult()!.commenterUsername }}
                        </span>
                        <span>Now</span>
                      </div>

                      @if (lastResult()!.privateDm) {
                        <div class="bg-gradient-to-br from-indigo-950/60 to-purple-950/60 border border-purple-900/40 rounded-xl p-3 text-xs space-y-2 text-slate-200 whitespace-pre-line">
                          {{ lastResult()!.privateDm }}
                        </div>

                        <!-- Tracked Checkout URL Preview -->
                        <div class="bg-slate-900 p-2 rounded-lg border border-slate-800 text-[10px] break-all font-mono text-cyan-300">
                          🔗 {{ lastResult()!.checkoutUrl }}
                        </div>

                        <!-- Funnel Conversion Simulation Action Buttons -->
                        <div class="pt-2 flex items-center gap-2">
                          <button
                            type="button"
                            (click)="simulateClick(lastResult()!.couponCode!)"
                            class="flex-1 py-1.5 px-2.5 rounded-lg text-xs font-medium bg-cyan-900/40 hover:bg-cyan-800/60 text-cyan-200 border border-cyan-700/50 transition flex items-center justify-center gap-1">
                            <span>🖱️ Test Click Link</span>
                          </button>
                          <button
                            type="button"
                            (click)="simulateConversion(lastResult()!.couponCode!, 99.00)"
                            class="flex-1 py-1.5 px-2.5 rounded-lg text-xs font-medium bg-emerald-900/40 hover:bg-emerald-800/60 text-emerald-200 border border-emerald-700/50 transition flex items-center justify-center gap-1">
                            <span>💰 Test Buy ($99)</span>
                          </button>
                        </div>
                      } @else {
                        <div class="p-4 text-center text-xs text-slate-500 italic">
                          {{ lastResult()!.status === 3 ? 'DM skipped because user already received one on this post.' : 'No direct message sent.' }}
                        </div>
                      }
                    </div>

                    <div class="text-[10px] text-slate-500 flex items-center justify-between">
                      <span>Idempotency: Tenant:Post:User</span>
                      <span>UTM Tracked</span>
                    </div>
                  </div>
                </div>
              }
            </div>
          </div>
        }

        <!-- ========================================================================= -->
        <!-- TAB 2: KEYWORD RULES ENGINE                                                -->
        <!-- ========================================================================= -->
        @if (activeTab() === 'rules') {
          <div class="space-y-6">
            <div class="flex items-center justify-between">
              <div>
                <h2 class="text-sm font-semibold text-white">Keyword Matching Rules & Inbound Workflows</h2>
                <p class="text-xs text-slate-400">Configure trigger keywords, match engines (Exact, Contains, Regex, Intent), and customizable two-phase templates.</p>
              </div>
              <button
                type="button"
                (click)="openNewRuleModal()"
                class="px-3.5 py-2 rounded-xl text-xs font-medium bg-gradient-to-r from-purple-600 to-indigo-600 hover:from-purple-500 hover:to-indigo-500 text-white shadow-lg shadow-purple-950/40 flex items-center gap-1.5 transition">
                <span>➕ Add Inbound Rule</span>
              </button>
            </div>

            <!-- Rules Grid -->
            <div class="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-4">
              @for (rule of rules(); track rule.id) {
                <div class="bg-slate-900/80 border border-slate-800 rounded-2xl p-5 space-y-4 hover:border-slate-700 transition flex flex-col justify-between">
                  <div class="space-y-3">
                    <div class="flex items-start justify-between">
                      <div>
                        <h3 class="text-xs font-bold text-white tracking-tight">{{ rule.name }}</h3>
                        <div class="flex items-center gap-1.5 mt-1">
                          <span class="px-2 py-0.5 rounded text-[10px] font-semibold bg-purple-500/20 text-purple-300 border border-purple-500/30">
                            {{ getMatchTypeName(rule.matchType) }}
                          </span>
                          <span class="px-2 py-0.5 rounded text-[10px] bg-slate-800 text-slate-300 border border-slate-700">
                            {{ rule.platform ? getPlatformName(rule.platform) : 'All Platforms' }}
                          </span>
                        </div>
                      </div>

                      <span class="w-2.5 h-2.5 rounded-full" [class]="rule.isEnabled ? 'bg-emerald-400' : 'bg-slate-600'"></span>
                    </div>

                    <!-- Keywords List -->
                    <div>
                      <span class="text-[10px] font-medium text-slate-400 uppercase tracking-wider block mb-1">Keywords</span>
                      <div class="flex flex-wrap gap-1 max-h-16 overflow-y-auto">
                        @for (kw of rule.keywords; track kw) {
                          <span class="px-2 py-0.5 rounded text-[10px] font-mono bg-slate-950 text-cyan-300 border border-slate-800">
                            {{ kw }}
                          </span>
                        }
                      </div>
                    </div>

                    <!-- Public Reply Pool snippet -->
                    <div>
                      <span class="text-[10px] font-medium text-slate-400 uppercase tracking-wider block mb-1">
                        Public Replies ({{ rule.publicReplyTemplates.length }} templates)
                      </span>
                      <p class="text-xs text-slate-300 line-clamp-1 italic bg-slate-950 p-2 rounded-lg border border-slate-800/80">
                        "{{ rule.publicReplyTemplates[0] }}"
                      </p>
                    </div>

                    <!-- Private DM Preview -->
                    <div>
                      <span class="text-[10px] font-medium text-slate-400 uppercase tracking-wider block mb-1">Private DM Template</span>
                      <p class="text-xs text-slate-400 line-clamp-2 bg-slate-950 p-2 rounded-lg border border-slate-800/80 font-mono">
                        {{ rule.privateDmTemplate }}
                      </p>
                    </div>
                  </div>

                  <!-- Footer specs & edit action -->
                  <div class="pt-3 border-t border-slate-800 flex items-center justify-between text-[11px]">
                    <div class="text-slate-400">
                      <span class="text-cyan-400 font-bold">{{ rule.discountPercent }}% OFF</span>
                      <span class="text-slate-600 mx-1">•</span>
                      <span>Prefix: {{ rule.discountPrefix }}</span>
                    </div>

                    <div class="flex items-center gap-1.5">
                      <button
                        (click)="editRule(rule)"
                        class="px-2.5 py-1 rounded-lg bg-slate-800 hover:bg-slate-700 text-slate-200 text-xs transition">
                        Edit
                      </button>
                      <button
                        (click)="deleteRule(rule.id)"
                        class="px-2.5 py-1 rounded-lg bg-rose-950/40 hover:bg-rose-900/60 text-rose-300 text-xs transition">
                        Delete
                      </button>
                    </div>
                  </div>
                </div>
              }
            </div>

            <!-- Rule Edit/Create Modal -->
            @if (isEditingRule()) {
              <div class="fixed inset-0 z-50 bg-slate-950/80 backdrop-blur-sm flex items-center justify-center p-4">
                <div class="bg-slate-900 border border-slate-800 rounded-2xl max-w-2xl w-full max-h-[90vh] overflow-y-auto p-6 space-y-5 shadow-2xl">
                  <div class="flex items-center justify-between pb-3 border-b border-slate-800">
                    <h3 class="text-sm font-bold text-white">
                      {{ editingRule().id ? 'Edit Social Comment Rule' : 'Create New Inbound Rule' }}
                    </h3>
                    <button (click)="isEditingRule.set(false)" class="text-slate-400 hover:text-white text-sm">✕</button>
                  </div>

                  <div class="grid grid-cols-2 gap-4">
                    <div>
                      <label class="block text-xs font-medium text-slate-300 mb-1">Rule Name</label>
                      <input
                        type="text"
                        [(ngModel)]="currentRuleForm.name"
                        class="w-full bg-slate-950 border border-slate-800 rounded-xl px-3 py-2 text-xs text-slate-100 focus:outline-none focus:border-cyan-500" />
                    </div>

                    <div>
                      <label class="block text-xs font-medium text-slate-300 mb-1">Platform Target</label>
                      <select
                        [(ngModel)]="currentRuleForm.platform"
                        class="w-full bg-slate-950 border border-slate-800 rounded-xl px-3 py-2 text-xs text-slate-100 focus:outline-none focus:border-cyan-500">
                        <option value="All">All Platforms (Instagram, TikTok, FB, YouTube)</option>
                        <option value="Instagram">Instagram</option>
                        <option value="TikTok">TikTok</option>
                        <option value="Facebook">Facebook</option>
                        <option value="YouTube">YouTube</option>
                      </select>
                    </div>

                    <div>
                      <label class="block text-xs font-medium text-slate-300 mb-1">Matching Engine</label>
                      <select
                        [(ngModel)]="currentRuleForm.matchType"
                        class="w-full bg-slate-950 border border-slate-800 rounded-xl px-3 py-2 text-xs text-slate-100 focus:outline-none focus:border-cyan-500">
                        <option [ngValue]="1">Exact Match (Standalone word/phrase)</option>
                        <option [ngValue]="2">Contains (Substring match)</option>
                        <option [ngValue]="3">Regex Pattern</option>
                        <option [ngValue]="4">Intent & Sentiment (Commercial with negative filter)</option>
                      </select>
                    </div>

                    <div>
                      <label class="block text-xs font-medium text-slate-300 mb-1">Discount %</label>
                      <input
                        type="number"
                        [(ngModel)]="currentRuleForm.discountPercent"
                        class="w-full bg-slate-950 border border-slate-800 rounded-xl px-3 py-2 text-xs text-slate-100 focus:outline-none focus:border-cyan-500" />
                    </div>
                  </div>

                  <!-- Keywords Input -->
                  <div>
                    <label class="block text-xs font-medium text-slate-300 mb-1">Trigger Keywords (comma separated)</label>
                    <input
                      type="text"
                      [(ngModel)]="keywordInputText"
                      placeholder="e.g. PRICE, BUY, INFO, COUPON, LINK, HOW MUCH, DISCOUNT"
                      class="w-full bg-slate-950 border border-slate-800 rounded-xl px-3 py-2 text-xs text-slate-100 focus:outline-none focus:border-cyan-500" />
                  </div>

                  <!-- Private DM Template -->
                  <div>
                    <label class="block text-xs font-medium text-slate-300 mb-1">
                      Private DM Template (use {{ '{{discount_code}}' }} and {{ '{{checkout_url}}' }})
                    </label>
                    <textarea
                      rows="4"
                      [(ngModel)]="currentRuleForm.privateDmTemplate"
                      class="w-full bg-slate-950 border border-slate-800 rounded-xl p-3 text-xs text-slate-100 font-mono focus:outline-none focus:border-cyan-500"></textarea>
                  </div>

                  <div class="grid grid-cols-2 gap-4">
                    <div>
                      <label class="block text-xs font-medium text-slate-300 mb-1">Min Jitter (Seconds)</label>
                      <input
                        type="number"
                        [(ngModel)]="currentRuleForm.minJitterSeconds"
                        class="w-full bg-slate-950 border border-slate-800 rounded-xl px-3 py-2 text-xs text-slate-100" />
                    </div>
                    <div>
                      <label class="block text-xs font-medium text-slate-300 mb-1">Max Jitter (Seconds)</label>
                      <input
                        type="number"
                        [(ngModel)]="currentRuleForm.maxJitterSeconds"
                        class="w-full bg-slate-950 border border-slate-800 rounded-xl px-3 py-2 text-xs text-slate-100" />
                    </div>
                  </div>

                  <div class="flex items-center justify-end gap-3 pt-3 border-t border-slate-800">
                    <button
                      type="button"
                      (click)="isEditingRule.set(false)"
                      class="px-4 py-2 rounded-xl text-xs bg-slate-800 hover:bg-slate-700 text-slate-300 transition">
                      Cancel
                    </button>
                    <button
                      type="button"
                      (click)="saveRule()"
                      class="px-5 py-2 rounded-xl text-xs font-medium bg-gradient-to-r from-cyan-500 to-indigo-600 hover:from-cyan-400 hover:to-indigo-500 text-white shadow-lg transition">
                      Save Rule
                    </button>
                  </div>
                </div>
              </div>
            }
          </div>
        }

        <!-- ========================================================================= -->
        <!-- TAB 3: INBOUND CONVERSION FUNNEL                                           -->
        <!-- ========================================================================= -->
        @if (activeTab() === 'funnel') {
          <div class="space-y-6">
            <div>
              <h2 class="text-sm font-semibold text-white">End-to-End Inbound Lead Conversion Funnel</h2>
              <p class="text-xs text-slate-400">Live analytics tracked across every comment, matched intent, delivered DM, link click, and order.</p>
            </div>

            <!-- Funnel Metric Stat Cards -->
            <div class="grid grid-cols-2 md:grid-cols-4 lg:grid-cols-7 gap-3">
              <div class="bg-slate-900/80 border border-slate-800 rounded-2xl p-4">
                <span class="text-[10px] uppercase font-bold text-slate-400 block">Total Comments</span>
                <span class="text-xl font-bold text-white mt-1 block">{{ funnelMetrics()?.totalComments || 0 }}</span>
                <span class="text-[10px] text-slate-500">Across 4 platforms</span>
              </div>

              <div class="bg-slate-900/80 border border-slate-800 rounded-2xl p-4">
                <span class="text-[10px] uppercase font-bold text-cyan-400 block">Matched Intent</span>
                <span class="text-xl font-bold text-cyan-300 mt-1 block">{{ funnelMetrics()?.matchedIntent || 0 }}</span>
                <span class="text-[10px] text-cyan-500/80">{{ funnelMetrics()?.matchRatePercentage || 0 }}% match rate</span>
              </div>

              <div class="bg-slate-900/80 border border-slate-800 rounded-2xl p-4">
                <span class="text-[10px] uppercase font-bold text-indigo-400 block">Public Replies</span>
                <span class="text-xl font-bold text-indigo-300 mt-1 block">{{ funnelMetrics()?.publicRepliesSent || 0 }}</span>
                <span class="text-[10px] text-indigo-500/80">Viral algorithm boost</span>
              </div>

              <div class="bg-slate-900/80 border border-slate-800 rounded-2xl p-4">
                <span class="text-[10px] uppercase font-bold text-purple-400 block">DMs Delivered</span>
                <span class="text-xl font-bold text-purple-300 mt-1 block">{{ funnelMetrics()?.dmsDelivered || 0 }}</span>
                <span class="text-[10px] text-purple-500/80">Dedup: {{ funnelMetrics()?.duplicateSkipped || 0 }} skipped</span>
              </div>

              <div class="bg-slate-900/80 border border-slate-800 rounded-2xl p-4">
                <span class="text-[10px] uppercase font-bold text-amber-400 block">Link Clicks</span>
                <span class="text-xl font-bold text-amber-300 mt-1 block">{{ funnelMetrics()?.linkClicks || 0 }}</span>
                <span class="text-[10px] text-amber-500/80">{{ funnelMetrics()?.clickThroughRatePercentage || 0 }}% CTR</span>
              </div>

              <div class="bg-slate-900/80 border border-slate-800 rounded-2xl p-4">
                <span class="text-[10px] uppercase font-bold text-emerald-400 block">Conversions</span>
                <span class="text-xl font-bold text-emerald-300 mt-1 block">{{ funnelMetrics()?.conversions || 0 }}</span>
                <span class="text-[10px] text-emerald-500/80">{{ funnelMetrics()?.conversionRatePercentage || 0 }}% Conv</span>
              </div>

              <div class="bg-slate-900/80 border border-slate-800 rounded-2xl p-4">
                <span class="text-[10px] uppercase font-bold text-emerald-400 block">Revenue</span>
                <span class="text-xl font-bold text-emerald-400 mt-1 block">
                  \${{ funnelMetrics()?.totalRevenue || 0 | number:'1.2-2' }}
                </span>
                <span class="text-[10px] text-emerald-500/80">Direct attributed</span>
              </div>
            </div>

            <!-- Visual Funnel Progress Bar -->
            <div class="bg-slate-900/80 border border-slate-800 rounded-2xl p-5 space-y-4">
              <h3 class="text-xs font-bold text-slate-300 uppercase tracking-wider">Multi-Stage Funnel Flow</h3>
              <div class="space-y-3">
                <!-- Comments -> Matched -->
                <div>
                  <div class="flex justify-between text-xs mb-1">
                    <span class="text-slate-300">1. Comments Received</span>
                    <span class="font-mono text-slate-400">{{ funnelMetrics()?.totalComments || 0 }} (100%)</span>
                  </div>
                  <div class="h-3 w-full bg-slate-950 rounded-full overflow-hidden border border-slate-800">
                    <div class="h-full bg-slate-500 rounded-full" style="width: 100%"></div>
                  </div>
                </div>

                <!-- Matched Intent -->
                <div>
                  <div class="flex justify-between text-xs mb-1">
                    <span class="text-cyan-300">2. Inbound Intent Matched</span>
                    <span class="font-mono text-cyan-400">
                      {{ funnelMetrics()?.matchedIntent || 0 }} ({{ funnelMetrics()?.matchRatePercentage || 0 }}%)
                    </span>
                  </div>
                  <div class="h-3 w-full bg-slate-950 rounded-full overflow-hidden border border-slate-800">
                    <div class="h-full bg-cyan-500 rounded-full transition-all duration-500" 
                      [style.width.%]="funnelMetrics()?.matchRatePercentage || 0"></div>
                  </div>
                </div>

                <!-- DMs Delivered -->
                <div>
                  <div class="flex justify-between text-xs mb-1">
                    <span class="text-purple-300">3. Private DMs Delivered (1-on-1 VIP Link)</span>
                    <span class="font-mono text-purple-400">
                      {{ funnelMetrics()?.dmsDelivered || 0 }}
                    </span>
                  </div>
                  <div class="h-3 w-full bg-slate-950 rounded-full overflow-hidden border border-slate-800">
                    <div class="h-full bg-purple-500 rounded-full transition-all duration-500"
                      [style.width.%]="funnelMetrics()?.totalComments ? ((funnelMetrics()?.dmsDelivered || 0) / funnelMetrics()!.totalComments) * 100 : 0"></div>
                  </div>
                </div>

                <!-- Link Clicks -->
                <div>
                  <div class="flex justify-between text-xs mb-1">
                    <span class="text-amber-300">4. DM Checkout Clicks (CTR)</span>
                    <span class="font-mono text-amber-400">
                      {{ funnelMetrics()?.linkClicks || 0 }} ({{ funnelMetrics()?.clickThroughRatePercentage || 0 }}% of DMs)
                    </span>
                  </div>
                  <div class="h-3 w-full bg-slate-950 rounded-full overflow-hidden border border-slate-800">
                    <div class="h-full bg-amber-500 rounded-full transition-all duration-500"
                      [style.width.%]="funnelMetrics()?.clickThroughRatePercentage || 0"></div>
                  </div>
                </div>

                <!-- Conversions -->
                <div>
                  <div class="flex justify-between text-xs mb-1">
                    <span class="text-emerald-300">5. Completed Order Conversions</span>
                    <span class="font-mono text-emerald-400">
                      {{ funnelMetrics()?.conversions || 0 }} ({{ funnelMetrics()?.conversionRatePercentage || 0 }}% Conv Rate)
                    </span>
                  </div>
                  <div class="h-3 w-full bg-slate-950 rounded-full overflow-hidden border border-slate-800">
                    <div class="h-full bg-emerald-500 rounded-full transition-all duration-500"
                      [style.width.%]="funnelMetrics()?.conversionRatePercentage || 0"></div>
                  </div>
                </div>
              </div>
            </div>

            <!-- Platform Breakdown Cards -->
            <div class="grid grid-cols-1 md:grid-cols-4 gap-4">
              @for (pName of ['Instagram', 'TikTok', 'Facebook', 'YouTube']; track pName) {
                @let breakdown = funnelMetrics()?.byPlatform?.[pName];
                <div class="bg-slate-900/80 border border-slate-800 rounded-2xl p-4 space-y-3">
                  <div class="flex items-center justify-between">
                    <span class="text-xs font-bold text-white flex items-center gap-1.5">
                      <span>{{ getPlatformIcon(pName) }}</span>
                      <span>{{ pName }}</span>
                    </span>
                    <span class="text-[10px] text-slate-400">{{ breakdown?.comments || 0 }} comments</span>
                  </div>

                  <div class="space-y-1 text-xs">
                    <div class="flex justify-between text-slate-300">
                      <span>DMs Sent:</span>
                      <span class="font-mono text-purple-400">{{ breakdown?.dmsSent || 0 }}</span>
                    </div>
                    <div class="flex justify-between text-slate-300">
                      <span>Clicks:</span>
                      <span class="font-mono text-amber-400">{{ breakdown?.clicks || 0 }}</span>
                    </div>
                    <div class="flex justify-between text-slate-300">
                      <span>Orders:</span>
                      <span class="font-mono text-emerald-400">{{ breakdown?.conversions || 0 }}</span>
                    </div>
                    <div class="flex justify-between text-slate-300 pt-1 border-t border-slate-800 font-bold">
                      <span>Revenue:</span>
                      <span class="font-mono text-emerald-400">\${{ breakdown?.revenue || 0 | number:'1.2-2' }}</span>
                    </div>
                  </div>
                </div>
              }
            </div>
          </div>
        }

        <!-- ========================================================================= -->
        <!-- TAB 4: REACTION AUDIT LOGS TABLE                                           -->
        <!-- ========================================================================= -->
        @if (activeTab() === 'logs') {
          <div class="bg-slate-900/80 border border-slate-800 rounded-2xl overflow-hidden shadow-xl space-y-4 p-5">
            <div class="flex items-center justify-between">
              <div>
                <h2 class="text-sm font-semibold text-white">Real-Time Inbound Reaction Logs</h2>
                <p class="text-xs text-slate-400">Complete audit trail of processed comments, jitter calculations, and guardrail decisions.</p>
              </div>
              <button
                (click)="loadLogs()"
                class="px-3 py-1.5 rounded-lg text-xs bg-slate-800 hover:bg-slate-700 text-slate-200 border border-slate-700 transition">
                Refresh Logs
              </button>
            </div>

            <div class="overflow-x-auto">
              <table class="w-full text-left text-xs text-slate-300">
                <thead class="bg-slate-950/80 text-[10px] uppercase font-bold text-slate-400 border-b border-slate-800">
                  <tr>
                    <th class="py-3 px-4">Status</th>
                    <th class="py-3 px-3">Platform</th>
                    <th class="py-3 px-3">Commenter</th>
                    <th class="py-3 px-4">Comment Text</th>
                    <th class="py-3 px-3">Matched Rule</th>
                    <th class="py-3 px-3">Coupon</th>
                    <th class="py-3 px-3">Jitter</th>
                    <th class="py-3 px-3">Processed</th>
                    <th class="py-3 px-3 text-right">Actions</th>
                  </tr>
                </thead>
                <tbody class="divide-y divide-slate-800/60 font-sans">
                  @for (log of logs(); track log.id) {
                    <tr class="hover:bg-slate-850/40 transition">
                      <td class="py-3 px-4">
                        <span class="px-2 py-0.5 rounded-full text-[10px] font-semibold"
                          [class]="getLogBadgeClass(log.status)">
                          {{ getStatusLabel(log.status) }}
                        </span>
                      </td>
                      <td class="py-3 px-3 font-medium text-slate-200">
                        {{ getPlatformName(log.platform) }}
                      </td>
                      <td class="py-3 px-3 font-semibold text-cyan-300">
                        {{ '@' + log.commenterUsername }}
                      </td>
                      <td class="py-3 px-4 max-w-xs truncate text-slate-300" [title]="log.commentText">
                        "{{ log.commentText }}"
                      </td>
                      <td class="py-3 px-3 text-slate-400">
                        {{ log.matchedRuleName || '—' }}
                      </td>
                      <td class="py-3 px-3 font-mono font-bold text-purple-300">
                        {{ log.couponCode || '—' }}
                      </td>
                      <td class="py-3 px-3 font-mono text-amber-300">
                        {{ log.jitterSeconds > 0 ? log.jitterSeconds + 's' : '—' }}
                      </td>
                      <td class="py-3 px-3 text-slate-500 whitespace-nowrap">
                        {{ log.processedAtUtc | date:'shortTime' }}
                      </td>
                      <td class="py-3 px-3 text-right whitespace-nowrap space-x-1">
                        @if (log.couponCode) {
                          <button
                            (click)="simulateClick(log.couponCode)"
                            [disabled]="log.hasClicked"
                            class="px-2 py-1 rounded text-[10px] bg-cyan-950 hover:bg-cyan-900 text-cyan-300 border border-cyan-800 disabled:opacity-40 transition">
                            {{ log.hasClicked ? '✓ Clicked' : 'Click' }}
                          </button>
                          <button
                            (click)="simulateConversion(log.couponCode, 99)"
                            [disabled]="log.hasConverted"
                            class="px-2 py-1 rounded text-[10px] bg-emerald-950 hover:bg-emerald-900 text-emerald-300 border border-emerald-800 disabled:opacity-40 transition">
                            {{ log.hasConverted ? '✓ Sold ($' + log.orderValue + ')' : 'Convert' }}
                          </button>
                        } @else {
                          <span class="text-slate-600">—</span>
                        }
                      </td>
                    </tr>
                  } @empty {
                    <tr>
                      <td colspan="9" class="py-8 text-center text-slate-500 italic">
                        No comment reactions recorded yet. Use the Live Comment Simulator to test!
                      </td>
                    </tr>
                  }
                </tbody>
              </table>
            </div>
          </div>
        }
      </main>
    </div>
  `
})
export class SocialCommentDmStudioComponent implements OnInit {
  private api = inject(ApiService);
  private auth = inject(AuthService);

  // Active view tab
  public activeTab = signal<'simulator' | 'rules' | 'funnel' | 'logs'>('simulator');
  public isLoading = signal<boolean>(false);
  public isSimulating = signal<boolean>(false);
  public isEditingRule = signal<boolean>(false);

  // State data
  public rules = signal<SocialCommentRuleDto[]>([]);
  public funnelMetrics = signal<SocialCommentFunnelMetricsDto | null>(null);
  public logs = signal<CommentProcessingLogDto[]>([]);
  public lastResult = signal<TwoPhaseReactionResultDto | null>(null);

  // Simulator Form Signals
  public simPlatform = signal<string>('Instagram');
  public simPostTitle = signal<string>('🔥 Exclusive VIP Drop: Comment "PRICE" below for instant discount!');
  public simPostId = signal<string>('reel_vip_942');
  public simCommenter = signal<string>('sophia_creator');
  public simComment = signal<string>('PRICE please!! Where can I buy this?');

  // Rule Editing Form
  public editingRule = signal<Partial<SocialCommentRuleDto>>({});
  public currentRuleForm: Partial<SocialCommentRuleDto> = {};
  public keywordInputText = '';

  private get tenantId(): string {
    return this.auth.currentUser()?.tenantId || '11111111-1111-1111-1111-111111111111';
  }

  ngOnInit(): void {
    this.refreshAll();
  }

  public refreshAll(): void {
    this.isLoading.set(true);
    this.loadRules();
    this.loadFunnel();
    this.loadLogs();
    setTimeout(() => this.isLoading.set(false), 500);
  }

  public loadRules(): void {
    this.api.getSocialCommentRules(this.tenantId).subscribe({
      next: (rules) => this.rules.set(rules),
      error: (err) => console.error('Failed to load rules', err)
    });
  }

  public loadFunnel(): void {
    this.api.getSocialCommentFunnel(this.tenantId).subscribe({
      next: (funnel) => this.funnelMetrics.set(funnel),
      error: (err) => console.error('Failed to load funnel', err)
    });
  }

  public loadLogs(): void {
    this.api.getSocialCommentLogs(this.tenantId, 50).subscribe({
      next: (logs) => this.logs.set(logs),
      error: (err) => console.error('Failed to load logs', err)
    });
  }

  public applyPreset(text: string): void {
    this.simComment.set(text);
  }

  public simulateComment(): void {
    if (!this.simComment().trim()) return;

    this.isSimulating.set(true);
    const payload: IncomingSocialCommentDto = {
      tenantId: this.tenantId,
      platform: this.simPlatform(),
      postId: this.simPostId(),
      commentId: 'cmt_' + Math.random().toString(36).substring(2, 9),
      commenterId: 'usr_' + this.simCommenter().toLowerCase().replace(/[^a-z0-9]/g, ''),
      commenterUsername: this.simCommenter(),
      commentText: this.simComment(),
      postTitle: this.simPostTitle()
    };

    this.api.simulateSocialCommentWebhook(payload, this.tenantId).subscribe({
      next: (res) => {
        this.lastResult.set(res);
        this.isSimulating.set(false);
        this.loadFunnel();
        this.loadLogs();
      },
      error: (err) => {
        console.error('Simulation error', err);
        this.isSimulating.set(false);
      }
    });
  }

  public simulateClick(couponCode: string): void {
    this.api.recordSocialCommentClick(couponCode, this.tenantId).subscribe({
      next: () => {
        this.loadFunnel();
        this.loadLogs();
      }
    });
  }

  public simulateConversion(couponCode: string, orderValue: number): void {
    this.api.recordSocialCommentConversion(couponCode, orderValue, this.tenantId).subscribe({
      next: () => {
        this.loadFunnel();
        this.loadLogs();
      }
    });
  }

  public openNewRuleModal(): void {
    this.currentRuleForm = {
      name: 'New Inbound Trigger Rule',
      platform: null,
      matchType: SocialRuleMatchType.Contains,
      keywords: ['PRICE', 'BUY', 'INFO'],
      discountPrefix: 'VIP',
      discountPercent: 20,
      minJitterSeconds: 5,
      maxJitterSeconds: 30,
      isEnabled: true,
      privateDmTemplate: 'Hey @{{username}}! 👋 Here is your exclusive discount code: {{discount_code}}\n\nDirect checkout: {{checkout_url}}'
    };
    this.keywordInputText = 'PRICE, BUY, INFO, COUPON';
    this.isEditingRule.set(true);
  }

  public editRule(rule: SocialCommentRuleDto): void {
    this.currentRuleForm = { ...rule };
    this.keywordInputText = rule.keywords.join(', ');
    this.isEditingRule.set(true);
  }

  public saveRule(): void {
    const kws = this.keywordInputText
      .split(',')
      .map(k => k.trim())
      .filter(k => k.length > 0);

    const payload: Partial<SocialCommentRuleDto> = {
      ...this.currentRuleForm,
      tenantId: this.tenantId,
      keywords: kws
    };

    this.api.saveSocialCommentRule(payload, this.tenantId).subscribe({
      next: () => {
        this.isEditingRule.set(false);
        this.loadRules();
      },
      error: (err) => console.error('Save rule error', err)
    });
  }

  public deleteRule(id: string): void {
    if (!confirm('Are you sure you want to delete this rule?')) return;
    this.api.deleteSocialCommentRule(id, this.tenantId).subscribe({
      next: () => this.loadRules(),
      error: (err) => console.error('Delete rule error', err)
    });
  }

  // Helpers
  public getPlatformIcon(platform: string | SocialPlatform): string {
    const p = typeof platform === 'number' ? this.getPlatformName(platform) : platform;
    switch (p?.toLowerCase()) {
      case 'instagram': return '📸';
      case 'tiktok': return '🎵';
      case 'facebook': return '📘';
      case 'youtube': return '▶️';
      case 'x': return '🐦';
      default: return '💬';
    }
  }

  public getPlatformName(platform: SocialPlatform | null | undefined): string {
    if (!platform) return 'All';
    switch (platform) {
      case SocialPlatform.Instagram: return 'Instagram';
      case SocialPlatform.TikTok: return 'TikTok';
      case SocialPlatform.Facebook: return 'Facebook';
      case SocialPlatform.YouTube: return 'YouTube';
      case SocialPlatform.X: return 'X';
      case SocialPlatform.LinkedIn: return 'LinkedIn';
      default: return 'Social';
    }
  }

  public getMatchTypeName(type: SocialRuleMatchType): string {
    switch (type) {
      case SocialRuleMatchType.Exact: return 'Exact Match';
      case SocialRuleMatchType.Contains: return 'Contains';
      case SocialRuleMatchType.Regex: return 'Regex';
      case SocialRuleMatchType.IntentSentiment: return 'Intent & Sentiment';
      default: return 'Rule';
    }
  }

  public getStatusLabel(status: CommentProcessingStatus): string {
    switch (status) {
      case CommentProcessingStatus.DmDelivered: return 'DM Delivered';
      case CommentProcessingStatus.RepliedPublicly: return 'Replied Publicly';
      case CommentProcessingStatus.DuplicateSkipped: return 'Duplicate Skipped';
      case CommentProcessingStatus.RateLimitThrottled: return 'Rate Limited';
      case CommentProcessingStatus.NoMatch: return 'No Match';
      default: return 'Processed';
    }
  }

  public getStatusIcon(status: CommentProcessingStatus): string {
    switch (status) {
      case CommentProcessingStatus.DmDelivered: return '✅';
      case CommentProcessingStatus.RepliedPublicly: return '💬';
      case CommentProcessingStatus.DuplicateSkipped: return '⚠️';
      case CommentProcessingStatus.RateLimitThrottled: return '🛑';
      case CommentProcessingStatus.NoMatch: return 'ℹ️';
      default: return '⚡';
    }
  }

  public getStatusBannerClass(status: CommentProcessingStatus): string {
    switch (status) {
      case CommentProcessingStatus.DmDelivered:
        return 'bg-emerald-950/40 border-emerald-800/80 text-emerald-200';
      case CommentProcessingStatus.DuplicateSkipped:
        return 'bg-amber-950/40 border-amber-800/80 text-amber-200';
      case CommentProcessingStatus.RateLimitThrottled:
        return 'bg-rose-950/40 border-rose-800/80 text-rose-200';
      case CommentProcessingStatus.NoMatch:
        return 'bg-slate-900 border-slate-800 text-slate-300';
      default:
        return 'bg-indigo-950/40 border-indigo-800/80 text-indigo-200';
    }
  }

  public getLogBadgeClass(status: CommentProcessingStatus): string {
    switch (status) {
      case CommentProcessingStatus.DmDelivered:
        return 'bg-emerald-500/20 text-emerald-300 border border-emerald-500/40';
      case CommentProcessingStatus.RepliedPublicly:
        return 'bg-cyan-500/20 text-cyan-300 border border-cyan-500/40';
      case CommentProcessingStatus.DuplicateSkipped:
        return 'bg-amber-500/20 text-amber-300 border border-amber-500/40';
      case CommentProcessingStatus.RateLimitThrottled:
        return 'bg-rose-500/20 text-rose-300 border border-rose-500/40';
      case CommentProcessingStatus.NoMatch:
        return 'bg-slate-800 text-slate-400 border border-slate-700';
      default:
        return 'bg-slate-800 text-slate-300';
    }
  }
}
