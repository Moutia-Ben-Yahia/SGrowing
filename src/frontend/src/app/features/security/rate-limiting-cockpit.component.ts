import { Component, OnInit, signal, computed, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { HttpClient } from '@angular/common/http';

export interface RateLimitPolicy {
  tierName: string;
  requestsPerMinute: number;
  burstCapacity: number;
  windowSeconds: number;
  description?: string;
  effectiveLimit?: number;
}

export interface RateLimitDecision {
  isAllowed: boolean;
  statusCode: number;
  reason: string;
  tier: string;
  clientKey: string;
  keyType: string;
  currentWeightedCount: number;
  limit: number;
  remaining: number;
  resetSeconds: number;
  retryAfterSeconds?: number | null;
  headers: Record<string, string>;
  timestamp: string;
  banLevel: number;
}

export interface RateLimitBanEntry {
  key: string;
  keyType: string;
  reason: string;
  banLevel: number;
  bannedAt: string;
  expiresAt?: string | null;
  isAutomated: boolean;
  consecutiveViolations: number;
}

export interface RateLimitWhitelistEntry {
  key: string;
  keyType: string;
  reason: string;
  addedAt: string;
  expiresAt?: string | null;
}

export interface ThrottledClientStat {
  key: string;
  keyType: string;
  throttledCount: number;
  lastThrottledAt: string;
  reason: string;
  consecutiveViolations: number;
}

export interface RateLimiterTelemetry {
  totalRequestsAnalyzed: number;
  totalThrottledRequests: number;
  totalBlacklistedRequests: number;
  totalWhitelistedRequests: number;
  rejectionRatePercentage: number;
  peakThroughputReqPerSec: number;
  topThrottledClients: ThrottledClientStat[];
  activeBlacklistedCount: number;
  activeWhitelistedCount: number;
}

export interface TrafficEvaluationItem {
  id: string;
  timestamp: string;
  clientKey: string;
  keyType: string;
  routeType: string;
  tier: string;
  isAllowed: boolean;
  statusCode: number;
  limit: number;
  remaining: number;
  reason: string;
  banLevel: number;
}

@Component({
  selector: 'app-rate-limiting-cockpit',
  standalone: true,
  imports: [CommonModule, FormsModule],
  template: `
    <div dir="ltr" class="flex h-screen w-screen overflow-hidden bg-slate-950 text-slate-100 font-sans select-none">
      <!-- Main Container -->
      <main class="flex-1 flex flex-col overflow-y-auto">
        <!-- Topbar -->
        <header class="h-16 border-b border-slate-800 bg-slate-900/90 backdrop-blur px-6 flex items-center justify-between shrink-0 sticky top-0 z-30">
          <div class="flex items-center gap-3">
            <span class="text-2xl">🛡️</span>
            <div>
              <div class="flex items-center gap-2">
                <h1 class="text-base font-bold text-white tracking-tight">
                  Distributed Rate Limiting & Sliding Window Token Bucket Guard
                </h1>
                <span class="text-xs px-2 py-0.5 rounded font-mono bg-cyan-950 text-cyan-400 border border-cyan-800/80">
                  Sub-Second Windowing
                </span>
                <span class="text-xs px-2 py-0.5 rounded font-mono bg-emerald-950 text-emerald-400 border border-emerald-800/80">
                  RFC 6585 Active
                </span>
              </div>
              <p class="text-xs text-slate-400">
                Horizon 27 — Hybrid sliding window counter, multi-tier quotas, progressive penalty cooldown, and real-time anti-abuse
              </p>
            </div>
          </div>

          <div class="flex items-center gap-3">
            <button
              (click)="loadTelemetryAndData()"
              [disabled]="isLoading()"
              class="px-3 py-1.5 text-xs font-semibold rounded-lg bg-slate-800 hover:bg-slate-700 text-slate-200 border border-slate-700 flex items-center gap-1.5 transition-colors disabled:opacity-50"
            >
              <span [class.animate-spin]="isLoading()">🔄</span> Refresh Telemetry
            </button>
            <div class="h-6 w-px bg-slate-800"></div>
            <span class="inline-flex items-center gap-1.5 px-2.5 py-1 rounded-full text-xs font-medium bg-emerald-500/10 text-emerald-400 border border-emerald-500/20">
              <span class="w-1.5 h-1.5 rounded-full bg-emerald-400 animate-pulse"></span>
              Anti-Abuse Mesh Armed
            </span>
          </div>
        </header>

        <!-- Navigation Tabs -->
        <div class="px-6 border-b border-slate-800 bg-slate-900/50 flex gap-2 pt-2">
          <button
            (click)="activeTab.set('cockpit')"
            [class.border-cyan-500]="activeTab() === 'cockpit'"
            [class.text-cyan-400]="activeTab() === 'cockpit'"
            [class.border-transparent]="activeTab() !== 'cockpit'"
            [class.text-slate-400]="activeTab() !== 'cockpit'"
            class="px-4 py-2.5 text-xs font-semibold border-b-2 flex items-center gap-2 hover:text-slate-200 transition-colors"
          >
            <span>📊</span> Telemetry & Live Stream
          </button>
          <button
            (click)="activeTab.set('simulator')"
            [class.border-cyan-500]="activeTab() === 'simulator'"
            [class.text-cyan-400]="activeTab() === 'simulator'"
            [class.border-transparent]="activeTab() !== 'simulator'"
            [class.text-slate-400]="activeTab() !== 'simulator'"
            class="px-4 py-2.5 text-xs font-semibold border-b-2 flex items-center gap-2 hover:text-slate-200 transition-colors"
          >
            <span>⚡</span> Request Evaluator & Simulator
          </button>
          <button
            (click)="activeTab.set('policies')"
            [class.border-cyan-500]="activeTab() === 'policies'"
            [class.text-cyan-400]="activeTab() === 'policies'"
            [class.border-transparent]="activeTab() !== 'policies'"
            [class.text-slate-400]="activeTab() !== 'policies'"
            class="px-4 py-2.5 text-xs font-semibold border-b-2 flex items-center gap-2 hover:text-slate-200 transition-colors"
          >
            <span>⚙️</span> Tier Quota Policies Matrix
          </button>
          <button
            (click)="activeTab.set('blacklist')"
            [class.border-cyan-500]="activeTab() === 'blacklist'"
            [class.text-cyan-400]="activeTab() === 'blacklist'"
            [class.border-transparent]="activeTab() !== 'blacklist'"
            [class.text-slate-400]="activeTab() !== 'blacklist'"
            class="px-4 py-2.5 text-xs font-semibold border-b-2 flex items-center gap-2 hover:text-slate-200 transition-colors"
          >
            <span>🚫</span> Offender Blacklist & Cooldown ({{ blacklist().length }})
          </button>
          <button
            (click)="activeTab.set('whitelist')"
            [class.border-cyan-500]="activeTab() === 'whitelist'"
            [class.text-cyan-400]="activeTab() === 'whitelist'"
            [class.border-transparent]="activeTab() !== 'whitelist'"
            [class.text-slate-400]="activeTab() !== 'whitelist'"
            class="px-4 py-2.5 text-xs font-semibold border-b-2 flex items-center gap-2 hover:text-slate-200 transition-colors"
          >
            <span>✅</span> Trusted Whitelist ({{ whitelist().length }})
          </button>
        </div>

        <!-- Content Area -->
        <div class="p-6 space-y-6 flex-1">
          <!-- KPI Cards Strip (always visible at top of cockpit) -->
          <div class="grid grid-cols-1 md:grid-cols-5 gap-4">
            <div class="p-4 rounded-xl bg-slate-900/80 border border-slate-800 shadow-lg relative overflow-hidden">
              <div class="text-xs font-medium text-slate-400">Total Analyzed</div>
              <div class="mt-2 text-2xl font-black text-white font-mono tracking-tight">
                {{ telemetry().totalRequestsAnalyzed | number }}
              </div>
              <div class="mt-1 flex items-center gap-1.5 text-xs text-cyan-400">
                <span>⚡</span> Sub-second accuracy
              </div>
              <div class="absolute -right-4 -bottom-4 w-16 h-16 bg-cyan-500/5 rounded-full blur-xl pointer-events-none"></div>
            </div>

            <div class="p-4 rounded-xl bg-slate-900/80 border border-slate-800 shadow-lg relative overflow-hidden">
              <div class="text-xs font-medium text-slate-400">Throttled (429s)</div>
              <div class="mt-2 text-2xl font-black text-rose-400 font-mono tracking-tight">
                {{ telemetry().totalThrottledRequests | number }}
              </div>
              <div class="mt-1 flex items-center gap-1.5 text-xs text-rose-300">
                <span>⚠️</span> Rejections triggered
              </div>
              <div class="absolute -right-4 -bottom-4 w-16 h-16 bg-rose-500/5 rounded-full blur-xl pointer-events-none"></div>
            </div>

            <div class="p-4 rounded-xl bg-slate-900/80 border border-slate-800 shadow-lg relative overflow-hidden">
              <div class="text-xs font-medium text-slate-400">Rejection Rate</div>
              <div class="mt-2 text-2xl font-black text-amber-400 font-mono tracking-tight">
                {{ telemetry().rejectionRatePercentage }}%
              </div>
              <div class="mt-1 flex items-center gap-1.5 text-xs text-amber-300">
                <span>🎯</span> Abuse drop ratio
              </div>
              <div class="absolute -right-4 -bottom-4 w-16 h-16 bg-amber-500/5 rounded-full blur-xl pointer-events-none"></div>
            </div>

            <div class="p-4 rounded-xl bg-slate-900/80 border border-slate-800 shadow-lg relative overflow-hidden">
              <div class="text-xs font-medium text-slate-400">Peak Ingest Speed</div>
              <div class="mt-2 text-2xl font-black text-purple-400 font-mono tracking-tight">
                {{ telemetry().peakThroughputReqPerSec }} <span class="text-xs font-normal text-slate-400">req/s</span>
              </div>
              <div class="mt-1 flex items-center gap-1.5 text-xs text-purple-300">
                <span>🚀</span> Max recorded throughput
              </div>
              <div class="absolute -right-4 -bottom-4 w-16 h-16 bg-purple-500/5 rounded-full blur-xl pointer-events-none"></div>
            </div>

            <div class="p-4 rounded-xl bg-slate-900/80 border border-slate-800 shadow-lg relative overflow-hidden">
              <div class="text-xs font-medium text-slate-400">Active Bans</div>
              <div class="mt-2 text-2xl font-black text-red-400 font-mono tracking-tight">
                {{ blacklist().length }} <span class="text-xs font-normal text-slate-400">keys</span>
              </div>
              <div class="mt-1 flex items-center gap-1.5 text-xs text-red-300">
                <span>🔒</span> Cooldowns active
              </div>
              <div class="absolute -right-4 -bottom-4 w-16 h-16 bg-red-500/5 rounded-full blur-xl pointer-events-none"></div>
            </div>
          </div>

          <!-- TAB 1: Cockpit Overview & Real-Time Traffic Stream -->
          @if (activeTab() === 'cockpit') {
            <div class="grid grid-cols-1 lg:grid-cols-3 gap-6">
              <!-- Left 2 Cols: Real-Time Traffic Stream -->
              <div class="lg:col-span-2 space-y-4">
                <div class="p-5 rounded-xl bg-slate-900/80 border border-slate-800 shadow-lg">
                  <div class="flex items-center justify-between mb-4">
                    <div class="flex items-center gap-2">
                      <span class="text-lg">📡</span>
                      <div>
                        <h2 class="text-sm font-bold text-white">Live Evaluated Traffic Stream</h2>
                        <p class="text-xs text-slate-400">Rolling requests with status, remaining tokens, and progressive penalty flags</p>
                      </div>
                    </div>
                    <div class="flex items-center gap-2">
                      <input
                        type="text"
                        [ngModel]="filterText()"
                        (ngModelChange)="filterText.set($event)"
                        placeholder="Filter by IP, key, tier..."
                        class="px-2.5 py-1 text-xs rounded-lg bg-slate-950 border border-slate-800 text-slate-200 focus:outline-none focus:border-cyan-500 w-48"
                      />
                      <button
                        (click)="clearTrafficLog()"
                        class="px-2.5 py-1 text-xs rounded-lg bg-slate-800 hover:bg-slate-700 text-slate-300 border border-slate-700 transition-colors"
                      >
                        Clear
                      </button>
                    </div>
                  </div>

                  <div class="overflow-x-auto">
                    <table class="w-full text-left text-xs text-slate-300">
                      <thead class="bg-slate-950/70 text-slate-400 uppercase font-mono border-b border-slate-800 text-[10px]">
                        <tr>
                          <th class="py-2.5 px-3">Status</th>
                          <th class="py-2.5 px-3">Client Key</th>
                          <th class="py-2.5 px-3">Type</th>
                          <th class="py-2.5 px-3">Tier / Route</th>
                          <th class="py-2.5 px-3">Quota</th>
                          <th class="py-2.5 px-3">Reason / Details</th>
                          <th class="py-2.5 px-3 text-right">Actions</th>
                        </tr>
                      </thead>
                      <tbody class="divide-y divide-slate-800/60 font-mono text-xs">
                        @for (item of filteredTrafficLog(); track item.id) {
                          <tr class="hover:bg-slate-800/30 transition-colors">
                            <td class="py-2.5 px-3">
                              @if (item.statusCode === 200) {
                                <span class="px-2 py-0.5 rounded text-[10px] font-bold bg-emerald-950 text-emerald-400 border border-emerald-800">
                                  200 OK
                                </span>
                              } @else if (item.statusCode === 429) {
                                <span class="px-2 py-0.5 rounded text-[10px] font-bold bg-rose-950 text-rose-400 border border-rose-800 animate-pulse">
                                  429 TOO MANY
                                </span>
                              } @else {
                                <span class="px-2 py-0.5 rounded text-[10px] font-bold bg-amber-950 text-amber-400 border border-amber-800">
                                  {{ item.statusCode }} BLOCKED
                                </span>
                              }
                            </td>
                            <td class="py-2.5 px-3 font-semibold text-slate-200 truncate max-w-[150px]">
                              {{ item.clientKey }}
                            </td>
                            <td class="py-2.5 px-3">
                              <span class="px-1.5 py-0.5 rounded text-[10px] bg-slate-800 text-slate-300">
                                {{ item.keyType }}
                              </span>
                            </td>
                            <td class="py-2.5 px-3 text-slate-400">
                              {{ item.tier }}
                            </td>
                            <td class="py-2.5 px-3">
                              <span [class.text-emerald-400]="item.remaining > 5" [class.text-amber-400]="item.remaining <= 5 && item.remaining > 0" [class.text-rose-400]="item.remaining === 0">
                                {{ item.remaining }} / {{ item.limit }}
                              </span>
                            </td>
                            <td class="py-2.5 px-3 text-slate-400 text-[11px] truncate max-w-[200px]" [title]="item.reason">
                              {{ item.reason }}
                            </td>
                            <td class="py-2.5 px-3 text-right space-x-1">
                              @if (item.statusCode === 429) {
                                <button
                                  (click)="quickBan(item.clientKey, item.keyType, item.reason)"
                                  class="px-2 py-0.5 text-[10px] rounded bg-rose-900/60 hover:bg-rose-800 text-rose-200 border border-rose-700/60 transition-colors"
                                  title="Add to Blacklist"
                                >
                                  Ban
                                </button>
                              }
                              <button
                                (click)="resetKeyCounters(item.clientKey)"
                                class="px-2 py-0.5 text-[10px] rounded bg-slate-800 hover:bg-slate-700 text-slate-300 border border-slate-700 transition-colors"
                                title="Reset Bucket Counters"
                              >
                                Reset
                              </button>
                            </td>
                          </tr>
                        } @empty {
                          <tr>
                            <td colspan="7" class="py-6 text-center text-slate-500 text-xs">
                              No recent traffic events. Use the Request Simulator to test traffic patterns.
                            </td>
                          </tr>
                        }
                      </tbody>
                    </table>
                  </div>
                </div>
              </div>

              <!-- Right 1 Col: Top Throttled Offender Leaderboard -->
              <div class="space-y-4">
                <div class="p-5 rounded-xl bg-slate-900/80 border border-slate-800 shadow-lg">
                  <div class="flex items-center justify-between mb-3">
                    <div class="flex items-center gap-2">
                      <span class="text-lg">🎯</span>
                      <div>
                        <h2 class="text-sm font-bold text-white">Top Throttled Attackers</h2>
                        <p class="text-xs text-slate-400">High-frequency abusers and botnets</p>
                      </div>
                    </div>
                  </div>

                  <div class="space-y-3">
                    @for (offender of telemetry().topThrottledClients; track offender.key) {
                      <div class="p-3 rounded-lg bg-slate-950 border border-slate-800/80 flex flex-col gap-1.5 hover:border-rose-900/60 transition-colors">
                        <div class="flex items-center justify-between">
                          <span class="font-mono text-xs font-bold text-white truncate max-w-[160px]" [title]="offender.key">
                            {{ offender.key }}
                          </span>
                          <span class="px-2 py-0.5 rounded text-[10px] font-mono font-bold bg-rose-950 text-rose-400 border border-rose-900/80">
                            {{ offender.throttledCount | number }} 429s
                          </span>
                        </div>
                        <div class="text-[11px] text-slate-400 truncate" [title]="offender.reason">
                          {{ offender.reason }}
                        </div>
                        <div class="flex items-center justify-between pt-1 border-t border-slate-900 text-[10px] text-slate-500 font-mono">
                          <span>Type: {{ offender.keyType }}</span>
                          <div class="flex items-center gap-1.5">
                            <button
                              (click)="quickBan(offender.key, offender.keyType, offender.reason)"
                              class="text-rose-400 hover:text-rose-300 underline"
                            >
                              Ban
                            </button>
                            <span>•</span>
                            <button
                              (click)="resetKeyCounters(offender.key)"
                              class="text-cyan-400 hover:text-cyan-300 underline"
                            >
                              Reset
                            </button>
                          </div>
                        </div>
                      </div>
                    }
                  </div>
                </div>

                <!-- Sliding Window Formula Explainer -->
                <div class="p-4 rounded-xl bg-gradient-to-br from-cyan-950/40 to-slate-900 border border-cyan-800/40 shadow-lg text-xs space-y-2">
                  <div class="font-bold text-cyan-300 flex items-center gap-1.5">
                    <span>📐</span> Sliding Window Formula
                  </div>
                  <div class="p-2 rounded bg-slate-950 font-mono text-[11px] text-cyan-200 border border-cyan-900/40">
                    Count = Current + Previous * (1 - (Δt / Window))
                  </div>
                  <p class="text-[11px] text-slate-400 leading-relaxed">
                    Weighted decay provides sub-second smoothing over fixed window boundaries, preventing boundary-burst starvation while token buckets absorb momentary surges.
                  </p>
                </div>
              </div>
            </div>
          }

          <!-- TAB 2: Interactive Request Evaluator & Simulator -->
          @if (activeTab() === 'simulator') {
            <div class="grid grid-cols-1 lg:grid-cols-2 gap-6">
              <!-- Simulator Controls Card -->
              <div class="p-5 rounded-xl bg-slate-900/80 border border-slate-800 shadow-lg space-y-5">
                <div class="flex items-center gap-2">
                  <span class="text-xl">⚡</span>
                  <div>
                    <h2 class="text-sm font-bold text-white">Interactive Ingest Guard Simulator</h2>
                    <p class="text-xs text-slate-400">Simulate incoming traffic against sliding window counter and inspect RFC headers</p>
                  </div>
                </div>

                <!-- Presets Bar -->
                <div>
                  <label class="block text-xs font-medium text-slate-400 mb-1.5">Quick Client Presets</label>
                  <div class="grid grid-cols-2 sm:grid-cols-4 gap-2">
                    <button
                      (click)="applyPreset('normal')"
                      class="px-2.5 py-1.5 text-xs rounded-lg bg-slate-950 hover:bg-slate-800 border border-slate-800 text-slate-300 text-left truncate transition-colors"
                    >
                      🟢 Normal User
                    </button>
                    <button
                      (click)="applyPreset('scraper')"
                      class="px-2.5 py-1.5 text-xs rounded-lg bg-slate-950 hover:bg-slate-800 border border-slate-800 text-slate-300 text-left truncate transition-colors"
                    >
                      🔴 Botnet Scraper
                    </button>
                    <button
                      (click)="applyPreset('enterprise')"
                      class="px-2.5 py-1.5 text-xs rounded-lg bg-slate-950 hover:bg-slate-800 border border-slate-800 text-slate-300 text-left truncate transition-colors"
                    >
                      🟣 VIP Enterprise
                    </button>
                    <button
                      (click)="applyPreset('webhook')"
                      class="px-2.5 py-1.5 text-xs rounded-lg bg-slate-950 hover:bg-slate-800 border border-slate-800 text-slate-300 text-left truncate transition-colors"
                    >
                      🟠 Webhook Sender
                    </button>
                  </div>
                </div>

                <!-- Simulation Form -->
                <div class="space-y-3 font-mono text-xs">
                  <div>
                    <label class="block text-slate-400 mb-1">Client IP Address</label>
                    <input
                      type="text"
                      [ngModel]="simulatedIp()"
                      (ngModelChange)="simulatedIp.set($event)"
                      class="w-full px-3 py-2 rounded-lg bg-slate-950 border border-slate-800 text-white focus:outline-none focus:border-cyan-500 font-mono text-xs"
                      placeholder="e.g. 203.0.113.19"
                    />
                  </div>

                  <div>
                    <label class="block text-slate-400 mb-1">API Key (Optional)</label>
                    <input
                      type="text"
                      [ngModel]="simulatedApiKey()"
                      (ngModelChange)="simulatedApiKey.set($event)"
                      class="w-full px-3 py-2 rounded-lg bg-slate-950 border border-slate-800 text-white focus:outline-none focus:border-cyan-500 font-mono text-xs"
                      placeholder="e.g. ak_live_client_token_491"
                    />
                  </div>

                  <div class="grid grid-cols-2 gap-3">
                    <div>
                      <label class="block text-slate-400 mb-1">Tenant Tier</label>
                      <select
                        [ngModel]="selectedTier()"
                        (ngModelChange)="selectedTier.set($event)"
                        class="w-full px-3 py-2 rounded-lg bg-slate-950 border border-slate-800 text-white focus:outline-none focus:border-cyan-500 font-mono text-xs"
                      >
                        <option value="FreeTier">FreeTier (60/m + 15 burst)</option>
                        <option value="ProTier">ProTier (600/m + 60 burst)</option>
                        <option value="EnterpriseTier">EnterpriseTier (6000/m + 300)</option>
                        <option value="PublicWebhookTier">PublicWebhookTier (120/m + 30)</option>
                        <option value="AiInferenceTier">AiInferenceTier (30/m + 5)</option>
                      </select>
                    </div>

                    <div>
                      <label class="block text-slate-400 mb-1">Route Type</label>
                      <select
                        [ngModel]="simulatedRoute()"
                        (ngModelChange)="simulatedRoute.set($event)"
                        class="w-full px-3 py-2 rounded-lg bg-slate-950 border border-slate-800 text-white focus:outline-none focus:border-cyan-500 font-mono text-xs"
                      >
                        <option value="api">Standard API (/api/v1/...)</option>
                        <option value="webhook">Inbound Webhook (/webhooks/...)</option>
                        <option value="ai-inference">AI LLM Inference (/ai/inference)</option>
                      </select>
                    </div>
                  </div>

                  <div class="grid grid-cols-2 gap-3">
                    <div>
                      <label class="block text-slate-400 mb-1">Request Cost (Tokens)</label>
                      <input
                        type="number"
                        min="1"
                        max="50"
                        [ngModel]="simulatedCost()"
                        (ngModelChange)="simulatedCost.set($event)"
                        class="w-full px-3 py-2 rounded-lg bg-slate-950 border border-slate-800 text-white focus:outline-none focus:border-cyan-500 font-mono text-xs"
                      />
                    </div>

                    <div>
                      <label class="block text-slate-400 mb-1">Burst Multiplier</label>
                      <div class="flex gap-1.5">
                        <button
                          type="button"
                          (click)="simulatedBurstCount.set(1)"
                          [class.bg-cyan-600]="simulatedBurstCount() === 1"
                          [class.bg-slate-950]="simulatedBurstCount() !== 1"
                          class="flex-1 py-2 text-xs rounded border border-slate-800 font-bold transition-colors"
                        >
                          1x
                        </button>
                        <button
                          type="button"
                          (click)="simulatedBurstCount.set(10)"
                          [class.bg-cyan-600]="simulatedBurstCount() === 10"
                          [class.bg-slate-950]="simulatedBurstCount() !== 10"
                          class="flex-1 py-2 text-xs rounded border border-slate-800 font-bold transition-colors"
                        >
                          10x
                        </button>
                        <button
                          type="button"
                          (click)="simulatedBurstCount.set(75)"
                          [class.bg-cyan-600]="simulatedBurstCount() === 75"
                          [class.bg-slate-950]="simulatedBurstCount() !== 75"
                          class="flex-1 py-2 text-xs rounded border border-slate-800 font-bold transition-colors"
                        >
                          75x Burst
                        </button>
                      </div>
                    </div>
                  </div>
                </div>

                <!-- Simulation Action Button -->
                <div class="pt-2 flex gap-3">
                  <button
                    (click)="simulateRequest()"
                    [disabled]="isSimulating()"
                    class="flex-1 py-2.5 text-xs font-bold rounded-lg bg-cyan-600 hover:bg-cyan-500 text-white shadow-lg shadow-cyan-600/20 flex items-center justify-center gap-2 transition-all disabled:opacity-50"
                  >
                    @if (isSimulating()) {
                      <span class="animate-spin">🔄</span> Evaluating Ingest Guard...
                    } @else {
                      <span>🚀</span> Fire Request Simulation ({{ simulatedBurstCount() }}x)
                    }
                  </button>
                  <button
                    (click)="resetKeyCounters(simulatedIp())"
                    class="px-4 py-2.5 text-xs font-semibold rounded-lg bg-slate-800 hover:bg-slate-700 text-slate-300 border border-slate-700 transition-colors"
                  >
                    Reset Bucket
                  </button>
                </div>
              </div>

              <!-- Simulator Inspection Response Card -->
              <div class="space-y-4">
                @if (latestDecision(); as dec) {
                  <div
                    [class.border-emerald-800]="dec.statusCode === 200"
                    [class.border-rose-800]="dec.statusCode === 429"
                    [class.border-amber-800]="dec.statusCode !== 200 && dec.statusCode !== 429"
                    class="p-5 rounded-xl bg-slate-900/90 border shadow-xl space-y-4 relative overflow-hidden"
                  >
                    <!-- Status Header -->
                    <div class="flex items-center justify-between">
                      <div class="flex items-center gap-3">
                        @if (dec.statusCode === 200) {
                          <div class="w-10 h-10 rounded-xl bg-emerald-950 border border-emerald-700 flex items-center justify-center text-xl">
                            ✅
                          </div>
                          <div>
                            <div class="text-xs font-mono font-bold text-emerald-400">STATUS 200 OK — PERMITTED</div>
                            <div class="text-xs text-slate-300 font-mono">{{ dec.reason }}</div>
                          </div>
                        } @else if (dec.statusCode === 429) {
                          <div class="w-10 h-10 rounded-xl bg-rose-950 border border-rose-700 flex items-center justify-center text-xl animate-pulse">
                            🚫
                          </div>
                          <div>
                            <div class="text-xs font-mono font-bold text-rose-400">STATUS 429 TOO MANY REQUESTS</div>
                            <div class="text-xs text-rose-300 font-mono">{{ dec.reason }}</div>
                          </div>
                        } @else {
                          <div class="w-10 h-10 rounded-xl bg-amber-950 border border-amber-700 flex items-center justify-center text-xl">
                            ⛔
                          </div>
                          <div>
                            <div class="text-xs font-mono font-bold text-amber-400">STATUS {{ dec.statusCode }} FORBIDDEN</div>
                            <div class="text-xs text-amber-300 font-mono">{{ dec.reason }}</div>
                          </div>
                        }
                      </div>

                      @if (dec.banLevel > 0) {
                        <span class="px-2.5 py-1 rounded-full text-xs font-mono font-bold bg-rose-950 text-rose-400 border border-rose-800 animate-bounce">
                          Level {{ dec.banLevel }} Penalty Applied
                        </span>
                      }
                    </div>

                    <!-- Quota Gauge Bar -->
                    <div class="space-y-1.5">
                      <div class="flex justify-between text-xs font-mono text-slate-400">
                        <span>Window Capacity Consumed</span>
                        <span class="text-white font-bold">{{ dec.currentWeightedCount | number:'1.1-1' }} / {{ dec.limit }}</span>
                      </div>
                      <div class="w-full h-3 rounded-full bg-slate-950 overflow-hidden p-0.5 border border-slate-800">
                        <div
                          [style.width.%]="calcGaugePercentage(dec)"
                          [class.bg-emerald-500]="dec.statusCode === 200 && dec.remaining > 10"
                          [class.bg-amber-500]="dec.statusCode === 200 && dec.remaining <= 10"
                          [class.bg-rose-500]="dec.statusCode === 429"
                          class="h-full rounded-full transition-all duration-300"
                        ></div>
                      </div>
                      <div class="flex justify-between text-[11px] font-mono text-slate-500">
                        <span>Remaining: {{ dec.remaining }}</span>
                        <span>Resets in: {{ dec.resetSeconds }}s</span>
                      </div>
                    </div>

                    <!-- RFC Headers Inspector -->
                    <div class="space-y-2 pt-2 border-t border-slate-800">
                      <div class="text-xs font-bold text-slate-300 flex items-center justify-between">
                        <span>RFC-Compliant Response Headers Generator</span>
                        <span class="text-[10px] font-mono text-cyan-400">draft-ietf-httpapi-ratelimit</span>
                      </div>
                      <div class="p-3 rounded-lg bg-slate-950 border border-slate-800 font-mono text-xs space-y-1.5">
                        <div class="flex items-center justify-between text-slate-300">
                          <span class="text-slate-500 font-semibold">RateLimit-Limit:</span>
                          <span class="text-cyan-400 font-bold">{{ dec.headers['RateLimit-Limit'] || dec.limit }}</span>
                        </div>
                        <div class="flex items-center justify-between text-slate-300">
                          <span class="text-slate-500 font-semibold">RateLimit-Remaining:</span>
                          <span [class.text-rose-400]="dec.remaining === 0" [class.text-emerald-400]="dec.remaining > 0" class="font-bold">
                            {{ dec.headers['RateLimit-Remaining'] || dec.remaining }}
                          </span>
                        </div>
                        <div class="flex items-center justify-between text-slate-300">
                          <span class="text-slate-500 font-semibold">RateLimit-Reset:</span>
                          <span class="text-purple-400 font-bold">{{ dec.headers['RateLimit-Reset'] || dec.resetSeconds }}s</span>
                        </div>
                        @if (dec.headers['Retry-After'] || dec.retryAfterSeconds) {
                          <div class="flex items-center justify-between text-rose-300 bg-rose-950/40 p-1.5 rounded border border-rose-900/60 font-bold">
                            <span class="text-rose-400">Retry-After:</span>
                            <span class="text-rose-200">{{ dec.headers['Retry-After'] || dec.retryAfterSeconds }} seconds</span>
                          </div>
                        }
                      </div>
                    </div>

                    <!-- Diagnostics Metadata -->
                    <div class="grid grid-cols-2 gap-2 text-[11px] font-mono text-slate-400 pt-1">
                      <div>Key: <span class="text-white">{{ dec.clientKey }}</span></div>
                      <div>Type: <span class="text-white">{{ dec.keyType }}</span></div>
                      <div>Tier: <span class="text-white">{{ dec.tier }}</span></div>
                      <div>Evaluated: <span class="text-white">{{ dec.timestamp | date:'HH:mm:ss.SSS' }}</span></div>
                    </div>
                  </div>
                } @else {
                  <div class="p-12 rounded-xl bg-slate-900/80 border border-slate-800 text-center text-slate-500 space-y-3">
                    <span class="text-4xl">🔬</span>
                    <div class="text-sm font-semibold text-slate-400">No Request Evaluated Yet</div>
                    <p class="text-xs text-slate-500 max-w-sm mx-auto">
                      Choose a client IP preset or enter parameters on the left, then click "Fire Request Simulation" to inspect sliding window behavior and RFC headers.
                    </p>
                  </div>
                }
              </div>
            </div>
          }

          <!-- TAB 3: Tier Quota Policies Matrix -->
          @if (activeTab() === 'policies') {
            <div class="space-y-4">
              <div class="p-5 rounded-xl bg-slate-900/80 border border-slate-800 shadow-lg">
                <div class="flex items-center justify-between mb-4">
                  <div>
                    <h2 class="text-sm font-bold text-white">Multi-Tier Tenant Rate Limiting Quotas</h2>
                    <p class="text-xs text-slate-400">Configure requests per minute, burst capacity buffers, and sliding window duration</p>
                  </div>
                </div>

                <div class="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-4">
                  @for (tier of tierList(); track tier.tierName) {
                    <div class="p-4 rounded-xl bg-slate-950 border border-slate-800 space-y-4 hover:border-cyan-800/60 transition-colors">
                      <div class="flex items-center justify-between">
                        <span class="font-bold text-white text-sm flex items-center gap-2">
                          <span class="text-base">💎</span> {{ tier.tierName }}
                        </span>
                        <span class="px-2 py-0.5 rounded text-[10px] font-mono bg-cyan-950 text-cyan-400 border border-cyan-800/60">
                          Total Cap: {{ tier.requestsPerMinute + tier.burstCapacity }}/m
                        </span>
                      </div>

                      <p class="text-xs text-slate-400 min-h-[32px]">
                        {{ tier.description || 'Configurable tenant quota tier' }}
                      </p>

                      <div class="space-y-2.5 font-mono text-xs">
                        <div>
                          <label class="block text-slate-500 text-[10px] uppercase">Base Limit (req/min)</label>
                          <input
                            type="number"
                            min="1"
                            [(ngModel)]="tier.requestsPerMinute"
                            class="w-full px-2.5 py-1.5 rounded bg-slate-900 border border-slate-800 text-white focus:outline-none focus:border-cyan-500"
                          />
                        </div>

                        <div>
                          <label class="block text-slate-500 text-[10px] uppercase">Burst Capacity (Tokens)</label>
                          <input
                            type="number"
                            min="0"
                            [(ngModel)]="tier.burstCapacity"
                            class="w-full px-2.5 py-1.5 rounded bg-slate-900 border border-slate-800 text-white focus:outline-none focus:border-cyan-500"
                          />
                        </div>

                        <div>
                          <label class="block text-slate-500 text-[10px] uppercase">Window (Seconds)</label>
                          <input
                            type="number"
                            min="10"
                            [(ngModel)]="tier.windowSeconds"
                            class="w-full px-2.5 py-1.5 rounded bg-slate-900 border border-slate-800 text-white focus:outline-none focus:border-cyan-500"
                          />
                        </div>
                      </div>

                      <button
                        (click)="saveTierPolicy(tier)"
                        class="w-full py-2 text-xs font-semibold rounded-lg bg-slate-800 hover:bg-cyan-600 hover:text-white text-slate-300 border border-slate-700 transition-colors"
                      >
                        💾 Save Tier Policy
                      </button>
                    </div>
                  }
                </div>
              </div>
            </div>
          }

          <!-- TAB 4: Offender Blacklist & Cooldown -->
          @if (activeTab() === 'blacklist') {
            <div class="space-y-4">
              <!-- Add Blacklist Entry Bar -->
              <div class="p-4 rounded-xl bg-slate-900/80 border border-slate-800 flex flex-wrap items-center justify-between gap-3">
                <div class="flex items-center gap-2">
                  <span class="text-xl">🚫</span>
                  <div>
                    <h2 class="text-sm font-bold text-white">Active Security Blacklist & Penalty Cooldowns</h2>
                    <p class="text-xs text-slate-400">Offenders subject to progressive penalty bans (5m, 15m, 1h, Permanent)</p>
                  </div>
                </div>

                <div class="flex items-center gap-2">
                  <input
                    type="text"
                    [ngModel]="newBanKey()"
                    (ngModelChange)="newBanKey.set($event)"
                    placeholder="IP or ApiKey to ban..."
                    class="px-3 py-1.5 text-xs rounded-lg bg-slate-950 border border-slate-800 text-white focus:outline-none focus:border-rose-500 font-mono w-48"
                  />
                  <select
                    [ngModel]="newBanType()"
                    (ngModelChange)="newBanType.set($event)"
                    class="px-2.5 py-1.5 text-xs rounded-lg bg-slate-950 border border-slate-800 text-white focus:outline-none focus:border-rose-500 font-mono"
                  >
                    <option value="IP">IP</option>
                    <option value="ApiKey">ApiKey</option>
                    <option value="Tenant">Tenant</option>
                  </select>
                  <input
                    type="text"
                    [ngModel]="newBanReason()"
                    (ngModelChange)="newBanReason.set($event)"
                    placeholder="Reason..."
                    class="px-3 py-1.5 text-xs rounded-lg bg-slate-950 border border-slate-800 text-white focus:outline-none focus:border-rose-500 w-44"
                  />
                  <button
                    (click)="addManualBan()"
                    class="px-3.5 py-1.5 text-xs font-bold rounded-lg bg-rose-600 hover:bg-rose-500 text-white transition-colors"
                  >
                    + Ban Key
                  </button>
                </div>
              </div>

              <!-- Blacklist Table -->
              <div class="p-5 rounded-xl bg-slate-900/80 border border-slate-800 shadow-lg overflow-x-auto">
                <table class="w-full text-left text-xs text-slate-300 font-mono">
                  <thead class="bg-slate-950/70 text-slate-400 uppercase text-[10px] border-b border-slate-800">
                    <tr>
                      <th class="py-2.5 px-3">Key</th>
                      <th class="py-2.5 px-3">Type</th>
                      <th class="py-2.5 px-3">Ban Level</th>
                      <th class="py-2.5 px-3">Escalation Source</th>
                      <th class="py-2.5 px-3">Reason</th>
                      <th class="py-2.5 px-3">Expires At</th>
                      <th class="py-2.5 px-3 text-right">Actions</th>
                    </tr>
                  </thead>
                  <tbody class="divide-y divide-slate-800/60">
                    @for (ban of blacklist(); track ban.key) {
                      <tr class="hover:bg-slate-800/30 transition-colors">
                        <td class="py-3 px-3 font-bold text-white">
                          {{ ban.key }}
                        </td>
                        <td class="py-3 px-3">
                          <span class="px-2 py-0.5 rounded text-[10px] bg-slate-800 text-slate-300">
                            {{ ban.keyType }}
                          </span>
                        </td>
                        <td class="py-3 px-3">
                          <span
                            [class.bg-amber-950]="ban.banLevel === 1"
                            [class.text-amber-400]="ban.banLevel === 1"
                            [class.border-amber-800]="ban.banLevel === 1"
                            [class.bg-rose-950]="ban.banLevel >= 2"
                            [class.text-rose-400]="ban.banLevel >= 2"
                            [class.border-rose-800]="ban.banLevel >= 2"
                            class="px-2 py-0.5 rounded text-[10px] font-bold border"
                          >
                            Level {{ ban.banLevel }} ({{ getBanDurationLabel(ban.banLevel) }})
                          </span>
                        </td>
                        <td class="py-3 px-3">
                          @if (ban.isAutomated) {
                            <span class="text-cyan-400">⚡ Automated Machine</span>
                          } @else {
                            <span class="text-slate-400">👤 Admin Manual</span>
                          }
                        </td>
                        <td class="py-3 px-3 text-slate-400 text-[11px] truncate max-w-[220px]" [title]="ban.reason">
                          {{ ban.reason }}
                        </td>
                        <td class="py-3 px-3 text-slate-400">
                          {{ ban.expiresAt ? (ban.expiresAt | date:'short') : 'Permanent' }}
                        </td>
                        <td class="py-3 px-3 text-right">
                          <button
                            (click)="unbanKey(ban.key)"
                            class="px-2.5 py-1 text-xs rounded bg-slate-800 hover:bg-emerald-600 hover:text-white text-emerald-400 border border-slate-700 transition-colors"
                          >
                            Unban
                          </button>
                        </td>
                      </tr>
                    } @empty {
                      <tr>
                        <td colspan="7" class="py-8 text-center text-slate-500 text-xs">
                          No active blacklist bans. All offending keys have cleared cooldown.
                        </td>
                      </tr>
                    }
                  </tbody>
                </table>
              </div>
            </div>
          }

          <!-- TAB 5: Trusted Whitelist -->
          @if (activeTab() === 'whitelist') {
            <div class="space-y-4">
              <!-- Add Whitelist Entry Bar -->
              <div class="p-4 rounded-xl bg-slate-900/80 border border-slate-800 flex flex-wrap items-center justify-between gap-3">
                <div class="flex items-center gap-2">
                  <span class="text-xl">✅</span>
                  <div>
                    <h2 class="text-sm font-bold text-white">Trusted Anti-Abuse Whitelist</h2>
                    <p class="text-xs text-slate-400">Certified proxies, webhooks, and enterprise partners bypassing rate limiting</p>
                  </div>
                </div>

                <div class="flex items-center gap-2">
                  <input
                    type="text"
                    [ngModel]="newWhiteKey()"
                    (ngModelChange)="newWhiteKey.set($event)"
                    placeholder="IP or ApiKey to whitelist..."
                    class="px-3 py-1.5 text-xs rounded-lg bg-slate-950 border border-slate-800 text-white focus:outline-none focus:border-emerald-500 font-mono w-48"
                  />
                  <select
                    [ngModel]="newWhiteType()"
                    (ngModelChange)="newWhiteType.set($event)"
                    class="px-2.5 py-1.5 text-xs rounded-lg bg-slate-950 border border-slate-800 text-white focus:outline-none focus:border-emerald-500 font-mono"
                  >
                    <option value="IP">IP</option>
                    <option value="ApiKey">ApiKey</option>
                  </select>
                  <input
                    type="text"
                    [ngModel]="newWhiteReason()"
                    (ngModelChange)="newWhiteReason.set($event)"
                    placeholder="Reason..."
                    class="px-3 py-1.5 text-xs rounded-lg bg-slate-950 border border-slate-800 text-white focus:outline-none focus:border-emerald-500 w-44"
                  />
                  <button
                    (click)="addManualWhitelist()"
                    class="px-3.5 py-1.5 text-xs font-bold rounded-lg bg-emerald-600 hover:bg-emerald-500 text-white transition-colors"
                  >
                    + Whitelist Key
                  </button>
                </div>
              </div>

              <!-- Whitelist Table -->
              <div class="p-5 rounded-xl bg-slate-900/80 border border-slate-800 shadow-lg overflow-x-auto">
                <table class="w-full text-left text-xs text-slate-300 font-mono">
                  <thead class="bg-slate-950/70 text-slate-400 uppercase text-[10px] border-b border-slate-800">
                    <tr>
                      <th class="py-2.5 px-3">Key</th>
                      <th class="py-2.5 px-3">Type</th>
                      <th class="py-2.5 px-3">Reason</th>
                      <th class="py-2.5 px-3">Added At</th>
                      <th class="py-2.5 px-3">Expires At</th>
                      <th class="py-2.5 px-3 text-right">Actions</th>
                    </tr>
                  </thead>
                  <tbody class="divide-y divide-slate-800/60">
                    @for (white of whitelist(); track white.key) {
                      <tr class="hover:bg-slate-800/30 transition-colors">
                        <td class="py-3 px-3 font-bold text-emerald-400">
                          {{ white.key }}
                        </td>
                        <td class="py-3 px-3">
                          <span class="px-2 py-0.5 rounded text-[10px] bg-slate-800 text-slate-300">
                            {{ white.keyType }}
                          </span>
                        </td>
                        <td class="py-3 px-3 text-slate-300">
                          {{ white.reason }}
                        </td>
                        <td class="py-3 px-3 text-slate-400">
                          {{ white.addedAt | date:'short' }}
                        </td>
                        <td class="py-3 px-3 text-slate-400">
                          {{ white.expiresAt ? (white.expiresAt | date:'short') : 'Permanent' }}
                        </td>
                        <td class="py-3 px-3 text-right">
                          <button
                            (click)="removeFromWhitelist(white.key)"
                            class="px-2.5 py-1 text-xs rounded bg-slate-800 hover:bg-rose-600 hover:text-white text-rose-400 border border-slate-700 transition-colors"
                          >
                            Remove
                          </button>
                        </td>
                      </tr>
                    }
                  </tbody>
                </table>
              </div>
            </div>
          }
        </div>
      </main>

      <!-- Toast Notification Notification -->
      @if (toastMessage(); as toast) {
        <div class="fixed bottom-6 right-6 z-50 px-4 py-2.5 rounded-lg bg-slate-900 border border-cyan-500/80 text-white text-xs font-semibold shadow-2xl flex items-center gap-2 animate-fade-in">
          <span>🔔</span> {{ toast }}
        </div>
      }
    </div>
  `
})
export class RateLimitingCockpitComponent implements OnInit {
  private http = inject(HttpClient);

  // Active Tab
  activeTab = signal<'cockpit' | 'simulator' | 'policies' | 'blacklist' | 'whitelist'>('cockpit');
  isLoading = signal<boolean>(false);
  isSimulating = signal<boolean>(false);
  toastMessage = signal<string | null>(null);

  // Telemetry State
  telemetry = signal<RateLimiterTelemetry>({
    totalRequestsAnalyzed: 142850,
    totalThrottledRequests: 3420,
    totalBlacklistedRequests: 1840,
    totalWhitelistedRequests: 8120,
    rejectionRatePercentage: 2.39,
    peakThroughputReqPerSec: 412.5,
    topThrottledClients: [
      { key: '198.51.100.42', keyType: 'IP', throttledCount: 1420, lastThrottledAt: new Date().toISOString(), reason: 'Credential stuffing brute-force attack', consecutiveViolations: 8 },
      { key: '203.0.113.88', keyType: 'IP', throttledCount: 892, lastThrottledAt: new Date().toISOString(), reason: 'Headless scraper swarm', consecutiveViolations: 6 },
      { key: 'ak_live_rogue_bot_991', keyType: 'ApiKey', throttledCount: 612, lastThrottledAt: new Date().toISOString(), reason: 'High-frequency token harvesting', consecutiveViolations: 10 },
      { key: '192.0.2.14', keyType: 'IP', throttledCount: 345, lastThrottledAt: new Date().toISOString(), reason: 'Webhook HMAC replay attack', consecutiveViolations: 14 },
      { key: 'tenant_spammer_882', keyType: 'Tenant', throttledCount: 151, lastThrottledAt: new Date().toISOString(), reason: 'Batch polling loop anomaly', consecutiveViolations: 5 }
    ],
    activeBlacklistedCount: 4,
    activeWhitelistedCount: 3
  });

  // Policies Matrix
  policies = signal<Record<string, RateLimitPolicy>>({
    FreeTier: { tierName: 'FreeTier', requestsPerMinute: 60, burstCapacity: 15, windowSeconds: 60, description: 'Standard free tier quota with 15-request burst buffer' },
    ProTier: { tierName: 'ProTier', requestsPerMinute: 600, burstCapacity: 60, windowSeconds: 60, description: 'Production high-throughput tier with 60-request burst allowance' },
    EnterpriseTier: { tierName: 'EnterpriseTier', requestsPerMinute: 6000, burstCapacity: 300, windowSeconds: 60, description: 'Mission-critical enterprise tier with 300-request burst tolerance' },
    PublicWebhookTier: { tierName: 'PublicWebhookTier', requestsPerMinute: 120, burstCapacity: 30, windowSeconds: 60, description: 'Inbound webhook ingest guard for external payment & CRM hooks' },
    AiInferenceTier: { tierName: 'AiInferenceTier', requestsPerMinute: 30, burstCapacity: 5, windowSeconds: 60, description: 'LLM completion and streaming inference token quota protection' }
  });

  tierList = computed(() => Object.values(this.policies()));

  // Blacklist & Whitelist
  blacklist = signal<RateLimitBanEntry[]>([
    { key: '198.51.100.42', keyType: 'IP', reason: 'Automated Escalation: 5+ consecutive 429 violations within 1m (Level 3 Penalty: 60m ban)', banLevel: 3, bannedAt: new Date(Date.now() - 20 * 60000).toISOString(), expiresAt: new Date(Date.now() + 40 * 60000).toISOString(), isAutomated: true, consecutiveViolations: 8 },
    { key: '203.0.113.88', keyType: 'IP', reason: 'Automated Escalation: 5+ consecutive 429 violations within 1m (Level 2 Penalty: 15m ban)', banLevel: 2, bannedAt: new Date(Date.now() - 5 * 60000).toISOString(), expiresAt: new Date(Date.now() + 10 * 60000).toISOString(), isAutomated: true, consecutiveViolations: 6 },
    { key: '192.0.2.14', keyType: 'IP', reason: 'Manual Blacklist: Webhook HMAC replay signature attack vector', banLevel: 4, bannedAt: new Date(Date.now() - 120 * 60000).toISOString(), expiresAt: null, isAutomated: false, consecutiveViolations: 14 },
    { key: 'ak_live_rogue_bot_991', keyType: 'ApiKey', reason: 'Automated Escalation: Distributed token harvesting crawler', banLevel: 3, bannedAt: new Date(Date.now() - 12 * 60000).toISOString(), expiresAt: new Date(Date.now() + 48 * 60000).toISOString(), isAutomated: true, consecutiveViolations: 10 }
  ]);

  whitelist = signal<RateLimitWhitelistEntry[]>([
    { key: '127.0.0.1', keyType: 'IP', reason: 'Local loopback proxy and healthcheck probe', addedAt: new Date(Date.now() - 30 * 86400000).toISOString(), expiresAt: null },
    { key: '10.0.0.1', keyType: 'IP', reason: 'Internal Kubernetes ingress gateway', addedAt: new Date(Date.now() - 14 * 86400000).toISOString(), expiresAt: null },
    { key: 'ak_partner_stripe_webhook_verified', keyType: 'ApiKey', reason: 'Verified Stripe webhook delivery engine', addedAt: new Date(Date.now() - 7 * 86400000).toISOString(), expiresAt: null }
  ]);

  // Real-time Traffic Log
  trafficLog = signal<TrafficEvaluationItem[]>([
    { id: '1', timestamp: new Date().toISOString(), clientKey: '198.51.100.42', keyType: 'IP', routeType: 'api', tier: 'Blacklisted', isAllowed: false, statusCode: 429, limit: 0, remaining: 0, reason: 'Access blocked by anti-abuse guard (Level 3 Penalty)', banLevel: 3 },
    { id: '2', timestamp: new Date(Date.now() - 1200).toISOString(), clientKey: '203.0.113.19', keyType: 'IP', routeType: 'api', tier: 'FreeTier', isAllowed: true, statusCode: 200, limit: 75, remaining: 71, reason: 'Request permitted within quota.', banLevel: 0 },
    { id: '3', timestamp: new Date(Date.now() - 3400).toISOString(), clientKey: 'ak_partner_stripe_webhook_verified', keyType: 'ApiKey', routeType: 'webhook', tier: 'Whitelisted', isAllowed: true, statusCode: 200, limit: 999999, remaining: 999999, reason: 'Request allowed via whitelist', banLevel: 0 },
    { id: '4', timestamp: new Date(Date.now() - 5600).toISOString(), clientKey: '203.0.113.88', keyType: 'IP', routeType: 'ai-inference', tier: 'Blacklisted', isAllowed: false, statusCode: 429, limit: 0, remaining: 0, reason: 'Access blocked by anti-abuse guard (Level 2 Penalty)', banLevel: 2 }
  ]);

  filterText = signal<string>('');

  filteredTrafficLog = computed(() => {
    const q = this.filterText().trim().toLowerCase();
    if (!q) return this.trafficLog();
    return this.trafficLog().filter(t =>
      t.clientKey.toLowerCase().includes(q) ||
      t.tier.toLowerCase().includes(q) ||
      t.reason.toLowerCase().includes(q)
    );
  });

  // Simulator Inputs
  simulatedIp = signal<string>('203.0.113.19');
  simulatedApiKey = signal<string>('');
  selectedTier = signal<string>('FreeTier');
  simulatedRoute = signal<string>('api');
  simulatedCost = signal<number>(1);
  simulatedBurstCount = signal<number>(1);
  latestDecision = signal<RateLimitDecision | null>(null);

  // Manual Ban & Whitelist Form Inputs
  newBanKey = signal<string>('');
  newBanType = signal<string>('IP');
  newBanReason = signal<string>('Manual administrator block');

  newWhiteKey = signal<string>('');
  newWhiteType = signal<string>('IP');
  newWhiteReason = signal<string>('Manual trusted partner whitelist');

  ngOnInit(): void {
    this.loadTelemetryAndData();
  }

  loadTelemetryAndData(): void {
    this.isLoading.set(true);

    // Fetch telemetry from API
    this.http.get<RateLimiterTelemetry>('/api/v1/security/rate-limiter/telemetry')
      .subscribe({
        next: (data) => {
          if (data) this.telemetry.set(data);
          this.isLoading.set(false);
        },
        error: () => {
          this.isLoading.set(false);
        }
      });

    // Fetch policies
    this.http.get<Record<string, RateLimitPolicy>>('/api/v1/security/rate-limiter/policies')
      .subscribe({
        next: (data) => {
          if (data) this.policies.set(data);
        },
        error: () => {}
      });

    // Fetch blacklist
    this.http.get<RateLimitBanEntry[]>('/api/v1/security/rate-limiter/blacklist')
      .subscribe({
        next: (data) => {
          if (data && data.length > 0) this.blacklist.set(data);
        },
        error: () => {}
      });

    // Fetch whitelist
    this.http.get<RateLimitWhitelistEntry[]>('/api/v1/security/rate-limiter/whitelist')
      .subscribe({
        next: (data) => {
          if (data && data.length > 0) this.whitelist.set(data);
        },
        error: () => {}
      });
  }

  applyPreset(preset: 'normal' | 'scraper' | 'enterprise' | 'webhook'): void {
    switch (preset) {
      case 'normal':
        this.simulatedIp.set('203.0.113.19');
        this.simulatedApiKey.set('');
        this.selectedTier.set('FreeTier');
        this.simulatedRoute.set('api');
        this.simulatedBurstCount.set(1);
        break;
      case 'scraper':
        this.simulatedIp.set('198.51.100.42');
        this.simulatedApiKey.set('');
        this.selectedTier.set('FreeTier');
        this.simulatedRoute.set('api');
        this.simulatedBurstCount.set(75);
        break;
      case 'enterprise':
        this.simulatedIp.set('198.51.100.99');
        this.simulatedApiKey.set('ak_live_enterprise_vip_001');
        this.selectedTier.set('EnterpriseTier');
        this.simulatedRoute.set('api');
        this.simulatedBurstCount.set(10);
        break;
      case 'webhook':
        this.simulatedIp.set('54.187.205.12');
        this.simulatedApiKey.set('ak_partner_stripe_webhook_verified');
        this.selectedTier.set('PublicWebhookTier');
        this.simulatedRoute.set('webhook');
        this.simulatedBurstCount.set(1);
        break;
    }
    this.showToast(`Applied preset: ${preset.toUpperCase()}`);
  }

  simulateRequest(): void {
    this.isSimulating.set(true);

    const payload = {
      clientIp: this.simulatedIp(),
      apiKey: this.simulatedApiKey() || undefined,
      routeType: this.simulatedRoute(),
      tier: this.selectedTier(),
      cost: this.simulatedCost()
    };

    const count = this.simulatedBurstCount();

    // Execute requests
    let remainingRequests = count;
    const runStep = () => {
      this.http.post<RateLimitDecision>('/api/v1/security/rate-limiter/check', payload)
        .subscribe({
          next: (res) => {
            this.handleDecisionResult(res);
            remainingRequests--;
            if (remainingRequests > 0) {
              runStep();
            } else {
              this.isSimulating.set(false);
              this.loadTelemetryAndData();
            }
          },
          error: (err) => {
            // Handle 429 response structure
            if (err.error && typeof err.error === 'object' && 'statusCode' in err.error) {
              this.handleDecisionResult(err.error as RateLimitDecision);
            } else {
              // Local fallback simulation
              const fallback = this.generateLocalFallbackDecision();
              this.handleDecisionResult(fallback);
            }
            remainingRequests--;
            if (remainingRequests > 0) {
              runStep();
            } else {
              this.isSimulating.set(false);
            }
          }
        });
    };

    runStep();
  }

  private handleDecisionResult(dec: RateLimitDecision): void {
    this.latestDecision.set(dec);

    // Append to live traffic stream
    const item: TrafficEvaluationItem = {
      id: Math.random().toString(36).substring(2, 9),
      timestamp: dec.timestamp || new Date().toISOString(),
      clientKey: dec.clientKey,
      keyType: dec.keyType,
      routeType: this.simulatedRoute(),
      tier: dec.tier,
      isAllowed: dec.isAllowed,
      statusCode: dec.statusCode,
      limit: dec.limit,
      remaining: dec.remaining,
      reason: dec.reason,
      banLevel: dec.banLevel
    };

    this.trafficLog.update(list => [item, ...list.slice(0, 49)]);

    if (dec.statusCode === 429) {
      this.showToast(`🚨 Request Throttled: 429 Too Many Requests!`);
    } else {
      this.showToast(`✅ Request Permitted (Remaining: ${dec.remaining})`);
    }
  }

  private generateLocalFallbackDecision(): RateLimitDecision {
    const tier = this.policies()[this.selectedTier()] || this.policies()['FreeTier'];
    const limit = tier.requestsPerMinute + tier.burstCapacity;
    const isBot = this.simulatedIp() === '198.51.100.42';

    if (isBot) {
      return {
        isAllowed: false,
        statusCode: 429,
        reason: 'Automated Escalation: 5+ consecutive 429 violations within 1m (Level 3 Penalty: 60m ban)',
        tier: 'Blacklisted',
        clientKey: this.simulatedIp(),
        keyType: 'IP',
        currentWeightedCount: 999,
        limit: 0,
        remaining: 0,
        resetSeconds: 2400,
        retryAfterSeconds: 2400,
        headers: {
          'RateLimit-Limit': '0',
          'RateLimit-Remaining': '0',
          'RateLimit-Reset': '2400',
          'Retry-After': '2400'
        },
        timestamp: new Date().toISOString(),
        banLevel: 3
      };
    }

    return {
      isAllowed: true,
      statusCode: 200,
      reason: 'Request permitted within quota.',
      tier: tier.tierName,
      clientKey: this.simulatedIp(),
      keyType: 'IP',
      currentWeightedCount: 14.5,
      limit: limit,
      remaining: Math.max(0, limit - 15),
      resetSeconds: 42,
      retryAfterSeconds: null,
      headers: {
        'RateLimit-Limit': limit.toString(),
        'RateLimit-Remaining': Math.max(0, limit - 15).toString(),
        'RateLimit-Reset': '42'
      },
      timestamp: new Date().toISOString(),
      banLevel: 0
    };
  }

  saveTierPolicy(tier: RateLimitPolicy): void {
    this.http.put(`/api/v1/security/rate-limiter/policies/${tier.tierName}`, {
      requestsPerMinute: tier.requestsPerMinute,
      burstCapacity: tier.burstCapacity,
      windowSeconds: tier.windowSeconds
    }).subscribe({
      next: () => {
        this.showToast(`Policy for '${tier.tierName}' updated successfully.`);
      },
      error: () => {
        this.showToast(`Policy for '${tier.tierName}' updated (local preview).`);
      }
    });
  }

  quickBan(key: string, keyType: string, reason: string): void {
    this.http.post<RateLimitBanEntry>('/api/v1/security/rate-limiter/blacklist', {
      key,
      keyType,
      reason: reason || 'Manual quick ban from traffic stream',
      durationMinutes: 60,
      banLevel: 2
    }).subscribe({
      next: (entry) => {
        this.blacklist.update(list => [entry, ...list.filter(b => b.key !== key)]);
        this.showToast(`Banned key '${key}' for 60 minutes.`);
      },
      error: () => {
        const mockBan: RateLimitBanEntry = {
          key,
          keyType,
          reason,
          banLevel: 2,
          bannedAt: new Date().toISOString(),
          expiresAt: new Date(Date.now() + 60 * 60000).toISOString(),
          isAutomated: false,
          consecutiveViolations: 1
        };
        this.blacklist.update(list => [mockBan, ...list.filter(b => b.key !== key)]);
        this.showToast(`Banned key '${key}' for 60m (local).`);
      }
    });
  }

  addManualBan(): void {
    const key = this.newBanKey().trim();
    if (!key) return;

    this.http.post<RateLimitBanEntry>('/api/v1/security/rate-limiter/blacklist', {
      key,
      keyType: this.newBanType(),
      reason: this.newBanReason().trim() || 'Manual administrator block',
      durationMinutes: 60,
      banLevel: 2
    }).subscribe({
      next: (entry) => {
        this.blacklist.update(list => [entry, ...list.filter(b => b.key !== key)]);
        this.newBanKey.set('');
        this.showToast(`Key '${key}' added to blacklist.`);
      },
      error: () => {
        const mockBan: RateLimitBanEntry = {
          key,
          keyType: this.newBanType(),
          reason: this.newBanReason().trim(),
          banLevel: 2,
          bannedAt: new Date().toISOString(),
          expiresAt: new Date(Date.now() + 60 * 60000).toISOString(),
          isAutomated: false,
          consecutiveViolations: 1
        };
        this.blacklist.update(list => [mockBan, ...list.filter(b => b.key !== key)]);
        this.newBanKey.set('');
        this.showToast(`Key '${key}' added to blacklist (local).`);
      }
    });
  }

  unbanKey(key: string): void {
    this.http.delete(`/api/v1/security/rate-limiter/blacklist/${encodeURIComponent(key)}`)
      .subscribe({
        next: () => {
          this.blacklist.update(list => list.filter(b => b.key !== key));
          this.showToast(`Key '${key}' unbanned successfully.`);
        },
        error: () => {
          this.blacklist.update(list => list.filter(b => b.key !== key));
          this.showToast(`Key '${key}' unbanned (local).`);
        }
      });
  }

  addManualWhitelist(): void {
    const key = this.newWhiteKey().trim();
    if (!key) return;

    this.http.post<RateLimitWhitelistEntry>('/api/v1/security/rate-limiter/whitelist', {
      key,
      keyType: this.newWhiteType(),
      reason: this.newWhiteReason().trim() || 'Manual trusted partner whitelist'
    }).subscribe({
      next: (entry) => {
        this.whitelist.update(list => [entry, ...list.filter(w => w.key !== key)]);
        this.newWhiteKey.set('');
        this.showToast(`Key '${key}' added to whitelist.`);
      },
      error: () => {
        const mockWhite: RateLimitWhitelistEntry = {
          key,
          keyType: this.newWhiteType(),
          reason: this.newWhiteReason().trim(),
          addedAt: new Date().toISOString(),
          expiresAt: null
        };
        this.whitelist.update(list => [mockWhite, ...list.filter(w => w.key !== key)]);
        this.newWhiteKey.set('');
        this.showToast(`Key '${key}' added to whitelist (local).`);
      }
    });
  }

  removeFromWhitelist(key: string): void {
    this.http.delete(`/api/v1/security/rate-limiter/whitelist/${encodeURIComponent(key)}`)
      .subscribe({
        next: () => {
          this.whitelist.update(list => list.filter(w => w.key !== key));
          this.showToast(`Key '${key}' removed from whitelist.`);
        },
        error: () => {
          this.whitelist.update(list => list.filter(w => w.key !== key));
          this.showToast(`Key '${key}' removed from whitelist (local).`);
        }
      });
  }

  resetKeyCounters(key: string): void {
    this.http.post('/api/v1/security/rate-limiter/reset-key', { key })
      .subscribe({
        next: () => {
          this.showToast(`Counters reset for key '${key}'.`);
          this.loadTelemetryAndData();
        },
        error: () => {
          this.showToast(`Counters reset for key '${key}' (local).`);
        }
      });
  }

  clearTrafficLog(): void {
    this.trafficLog.set([]);
  }

  calcGaugePercentage(dec: RateLimitDecision): number {
    if (!dec.limit || dec.limit <= 0) return 100;
    const pct = (dec.currentWeightedCount / dec.limit) * 100;
    return Math.min(100, Math.max(2, Math.round(pct)));
  }

  getBanDurationLabel(banLevel: number): string {
    switch (banLevel) {
      case 1: return '5 Minutes';
      case 2: return '15 Minutes';
      case 3: return '1 Hour';
      default: return 'Permanent';
    }
  }

  showToast(message: string): void {
    this.toastMessage.set(message);
    setTimeout(() => {
      if (this.toastMessage() === message) {
        this.toastMessage.set(null);
      }
    }, 3500);
  }
}
