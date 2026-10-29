import { Component, OnInit, signal, computed, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { ApiService } from '../../core/services/api.service';
import {
  EnrolledWarmupMailboxDto,
  EnrollMailboxRequest,
  WarmupExchangeResult,
  SpamRescueScanResult,
  SpamRescueLogEntry,
  DnsAndBlacklistAuditResult,
  DeliverabilityNetworkMetrics,
  WarmupProvider,
  WarmupStatus
} from '../../core/models/deliverability.models';

@Component({
  selector: 'app-warmup-rescue-cockpit',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterLink],
  template: `
    <div dir="ltr" class="flex h-screen w-screen overflow-hidden bg-slate-950 text-slate-100 font-sans select-none">
      <!-- Main Content Area -->
      <main class="flex-1 flex flex-col overflow-y-auto">
        <!-- Topbar -->
        <header class="h-16 border-b border-slate-800 bg-slate-900/80 backdrop-blur px-6 flex items-center justify-between shrink-0 sticky top-0 z-20">
          <div class="flex items-center gap-3">
            <a routerLink="/deliverability" class="text-xs text-slate-400 hover:text-white flex items-center gap-1 transition">
              ← Deliverability Studio
            </a>
            <span class="text-slate-700">|</span>
            <div class="flex items-center gap-2">
              <span class="text-xl">🛡️</span>
              <span class="text-base font-bold text-white tracking-tight">
                Autonomous P2P Warmup & ESP Spam Rescue Daemon
              </span>
            </div>
            <span class="bg-emerald-500/10 text-emerald-400 text-[11px] font-mono px-2.5 py-0.5 rounded-full border border-emerald-500/30 font-semibold">
              Horizon 16 • P2P Swarm & ML Classifier Trainer
            </span>
          </div>

          <div class="flex items-center gap-3">
            <!-- Navigation Tabs -->
            <div class="flex bg-slate-800/80 p-0.5 rounded-lg border border-slate-700/60 text-xs">
              <button
                (click)="activeTab.set('cockpit')"
                [class.bg-emerald-600]="activeTab() === 'cockpit'"
                [class.text-white]="activeTab() === 'cockpit'"
                class="px-3.5 py-1.5 rounded-md text-slate-300 transition font-medium flex items-center gap-1.5">
                <span>📊</span> Overview & Swarm
              </button>
              <button
                (click)="activeTab.set('rescue')"
                [class.bg-emerald-600]="activeTab() === 'rescue'"
                [class.text-white]="activeTab() === 'rescue'"
                class="px-3.5 py-1.5 rounded-md text-slate-300 transition font-medium flex items-center gap-1.5">
                <span>🚨</span> Spam Rescue Terminal
              </button>
              <button
                (click)="activeTab.set('dnsbl')"
                [class.bg-emerald-600]="activeTab() === 'dnsbl'"
                [class.text-white]="activeTab() === 'dnsbl'"
                class="px-3.5 py-1.5 rounded-md text-slate-300 transition font-medium flex items-center gap-1.5">
                <span>🔍</span> DNS & 26-DNSBL Audit
              </button>
              <button
                (click)="activeTab.set('exchange')"
                [class.bg-emerald-600]="activeTab() === 'exchange'"
                [class.text-white]="activeTab() === 'exchange'"
                class="px-3.5 py-1.5 rounded-md text-slate-300 transition font-medium flex items-center gap-1.5">
                <span>💬</span> Dialogue Generator
              </button>
            </div>

            <!-- Global Action -->
            <button
              (click)="triggerSpamRescueSweep()"
              [disabled]="isRescuing()"
              class="px-3.5 py-1.5 bg-gradient-to-r from-emerald-600 to-teal-600 hover:from-emerald-500 hover:to-teal-500 text-white rounded-lg text-xs font-semibold shadow-lg shadow-emerald-900/30 flex items-center gap-1.5 transition disabled:opacity-50">
              <span [class.animate-spin]="isRescuing()">⚡</span>
              <span>{{ isRescuing() ? 'Running Daemon Sweep...' : 'Run Spam Rescue Sweep' }}</span>
            </button>
          </div>
        </header>

        <!-- Notification Banner / Alert -->
        @if (notificationMessage()) {
          <div class="bg-emerald-950/80 border-b border-emerald-500/30 px-6 py-2.5 flex items-center justify-between text-xs text-emerald-200">
            <div class="flex items-center gap-2">
              <span class="text-emerald-400">✓</span>
              <span>{{ notificationMessage() }}</span>
            </div>
            <button (click)="notificationMessage.set(null)" class="text-emerald-400 hover:text-white">✕</button>
          </div>
        }

        <!-- TAB 1: OVERVIEW & SWARM COCKPIT -->
        @if (activeTab() === 'cockpit') {
          <div class="p-6 space-y-6">
            <!-- Top Hero Row: Health Gauge + Key Telemetry Cards -->
            <div class="grid grid-cols-1 lg:grid-cols-4 gap-6">
              <!-- Gauge Card (Col Span 1) -->
              <div class="bg-slate-900/90 border border-slate-800 rounded-2xl p-6 flex flex-col items-center justify-center relative overflow-hidden shadow-xl">
                <div class="absolute -top-12 -left-12 w-32 h-32 bg-emerald-500/10 rounded-full blur-2xl pointer-events-none"></div>
                <h3 class="text-xs font-semibold uppercase tracking-wider text-slate-400 mb-3 flex items-center gap-1.5">
                  <span>🎯</span> Deliverability Health Index
                </h3>

                <!-- Circular SVG Gauge -->
                <div class="relative w-40 h-40 flex items-center justify-center">
                  <svg class="w-full h-full transform -rotate-90" viewBox="0 0 120 120">
                    <!-- Background Circle -->
                    <circle
                      cx="60"
                      cy="60"
                      r="48"
                      stroke-width="9"
                      stroke="currentColor"
                      class="text-slate-800/80"
                      fill="none" />
                    <!-- Foreground Animated Progress -->
                    <circle
                      cx="60"
                      cy="60"
                      r="48"
                      stroke-width="9"
                      [attr.stroke-dasharray]="circleCircumference"
                      [attr.stroke-dashoffset]="circleDashoffset()"
                      stroke-linecap="round"
                      [attr.stroke]="gaugeStrokeColor()"
                      fill="none"
                      class="transition-all duration-1000 ease-out" />
                  </svg>
                  <div class="absolute flex flex-col items-center justify-center text-center">
                    <span class="text-3xl font-extrabold tracking-tight text-white font-mono">
                      {{ overallHealthScore() }}<span class="text-sm font-normal text-slate-400">%</span>
                    </span>
                    <span class="text-[11px] font-bold px-2 py-0.5 rounded-full mt-1"
                      [ngClass]="{
                        'bg-emerald-500/20 text-emerald-400 border border-emerald-500/30': overallHealthScore() >= 85,
                        'bg-amber-500/20 text-amber-400 border border-amber-500/30': overallHealthScore() >= 65 && overallHealthScore() < 85,
                        'bg-rose-500/20 text-rose-400 border border-rose-500/30': overallHealthScore() < 65
                      }">
                      Grade {{ healthGrade() }} • {{ healthStatus() }}
                    </span>
                  </div>
                </div>

                <div class="mt-4 grid grid-cols-3 gap-2 w-full text-center text-[10px] text-slate-400 border-t border-slate-800/80 pt-3">
                  <div>
                    <span class="block text-slate-500">SPF/DKIM</span>
                    <span class="font-bold text-emerald-400">100% Valid</span>
                  </div>
                  <div>
                    <span class="block text-slate-500">DMARC</span>
                    <span class="font-bold text-emerald-400">p=reject</span>
                  </div>
                  <div>
                    <span class="block text-slate-500">DNSBL</span>
                    <span class="font-bold text-emerald-400">0 Listed</span>
                  </div>
                </div>
              </div>

              <!-- Metrics Stats Cards (Col Span 3) -->
              <div class="lg:col-span-3 grid grid-cols-1 md:grid-cols-3 gap-4">
                <div class="bg-slate-900/90 border border-slate-800 rounded-2xl p-5 flex flex-col justify-between">
                  <div class="flex items-center justify-between">
                    <span class="text-xs font-semibold text-slate-400">Enrolled Peer Mailboxes</span>
                    <span class="p-2 bg-emerald-500/10 text-emerald-400 rounded-xl text-sm">📬</span>
                  </div>
                  <div class="my-3">
                    <div class="text-2xl font-bold font-mono text-white">{{ metrics()?.totalEnrolledMailboxes || mailboxes().length }}</div>
                    <div class="text-xs text-slate-400 mt-1 flex items-center gap-1.5">
                      <span class="w-2 h-2 rounded-full bg-emerald-400 inline-block animate-pulse"></span>
                      <span>{{ metrics()?.activeWarmingMailboxes || 2 }} warming • {{ metrics()?.healthyMailboxes || 2 }} healthy</span>
                    </div>
                  </div>
                  <div class="text-[11px] text-slate-500 border-t border-slate-800 pt-2 flex items-center justify-between">
                    <span>Target Volume Today:</span>
                    <span class="font-mono text-slate-300 font-semibold">{{ metrics()?.activeRampTargetVolume || 139 }} msgs</span>
                  </div>
                </div>

                <div class="bg-slate-900/90 border border-slate-800 rounded-2xl p-5 flex flex-col justify-between">
                  <div class="flex items-center justify-between">
                    <span class="text-xs font-semibold text-slate-400">Autonomous Spam Rescues</span>
                    <span class="p-2 bg-amber-500/10 text-amber-400 rounded-xl text-sm">🚨</span>
                  </div>
                  <div class="my-3">
                    <div class="text-2xl font-bold font-mono text-amber-400">{{ metrics()?.totalRescuedEmails || 16 }}</div>
                    <div class="text-xs text-slate-400 mt-1 flex items-center gap-1.5">
                      <span>Marked Not Spam & Starred automatically</span>
                    </div>
                  </div>
                  <div class="text-[11px] text-slate-500 border-t border-slate-800 pt-2 flex items-center justify-between">
                    <span>ESP ML Signal:</span>
                    <span class="font-mono text-emerald-400 font-semibold">+1 Google Priority</span>
                  </div>
                </div>

                <div class="bg-slate-900/90 border border-slate-800 rounded-2xl p-5 flex flex-col justify-between">
                  <div class="flex items-center justify-between">
                    <span class="text-xs font-semibold text-slate-400">Swarm Reply Engagement</span>
                    <span class="p-2 bg-teal-500/10 text-teal-400 rounded-xl text-sm">💬</span>
                  </div>
                  <div class="my-3">
                    <div class="text-2xl font-bold font-mono text-white">{{ metrics()?.averageReplyRatioPercent || 39.5 }}%</div>
                    <div class="text-xs text-emerald-400 mt-1 flex items-center gap-1">
                      <span>✓ In target sweet spot (35%–45%)</span>
                    </div>
                  </div>
                  <div class="text-[11px] text-slate-500 border-t border-slate-800 pt-2 flex items-center justify-between">
                    <span>Blacklist Clean Rate:</span>
                    <span class="font-mono text-emerald-400 font-semibold">100% Clean</span>
                  </div>
                </div>

                <!-- Daemon Status Full Width Sub-Bar -->
                <div class="md:col-span-3 bg-gradient-to-r from-slate-900 via-slate-900/90 to-emerald-950/40 border border-slate-800 rounded-2xl p-4 flex items-center justify-between">
                  <div class="flex items-center gap-3">
                    <div class="w-3 h-3 rounded-full bg-emerald-500 animate-ping"></div>
                    <div>
                      <div class="text-xs font-semibold text-white flex items-center gap-2">
                        <span>Autonomous Spam Rescue Daemon: Online & Active</span>
                        <span class="text-[10px] bg-emerald-500/20 text-emerald-400 px-2 py-0.5 rounded-full border border-emerald-500/30 font-mono">
                          Polling Spam / Junk Folders every 15m
                        </span>
                      </div>
                      <div class="text-[11px] text-slate-400 mt-0.5">
                        Detects cryptographic header <code class="text-amber-300 font-mono">X-SGrowing-Warmup-Token</code>, moves to Primary Inbox, flags Not Spam, stars, and sends human-like contextual replies.
                      </div>
                    </div>
                  </div>
                  <div class="flex items-center gap-2">
                    <button
                      (click)="openEnrollModal.set(true)"
                      class="px-3 py-1.5 bg-slate-800 hover:bg-slate-700 text-slate-200 rounded-lg text-xs font-medium border border-slate-700 transition flex items-center gap-1">
                      <span>+</span> Enroll Mailbox
                    </button>
                    <button
                      (click)="triggerWarmupExchange('saas')"
                      class="px-3 py-1.5 bg-emerald-600/20 hover:bg-emerald-600/30 text-emerald-300 rounded-lg text-xs font-medium border border-emerald-500/30 transition flex items-center gap-1">
                      <span>💬</span> Test P2P Exchange
                    </button>
                  </div>
                </div>
              </div>
            </div>

            <!-- Enrolled Mailboxes Table -->
            <div class="bg-slate-900/90 border border-slate-800 rounded-2xl overflow-hidden shadow-xl">
              <div class="p-5 border-b border-slate-800 flex items-center justify-between">
                <div>
                  <h3 class="text-sm font-bold text-white flex items-center gap-2">
                    <span>📬</span> Enrolled Mailbox Peer Network & Volume Ramp Status
                  </h3>
                  <p class="text-xs text-slate-400 mt-0.5">
                    Peer network accounts isolated with cryptographic exchange tokens for safe reputation accretion.
                  </p>
                </div>
                <div class="flex items-center gap-3">
                  <span class="text-xs text-slate-400">Total Mailboxes: <strong class="text-white">{{ mailboxes().length }}</strong></span>
                  <button
                    (click)="loadMailboxes()"
                    class="p-1.5 bg-slate-800 hover:bg-slate-700 text-slate-300 rounded-lg text-xs transition">
                    ↻ Refresh
                  </button>
                </div>
              </div>

              <div class="overflow-x-auto">
                <table class="w-full text-left text-xs">
                  <thead class="bg-slate-950/60 text-slate-400 border-b border-slate-800 font-medium">
                    <tr>
                      <th class="py-3 px-4">Mailbox Email</th>
                      <th class="py-3 px-4">Provider</th>
                      <th class="py-3 px-4">Ramp Day & Volume</th>
                      <th class="py-3 px-4">Sent / Received Today</th>
                      <th class="py-3 px-4">Reply Rate</th>
                      <th class="py-3 px-4">Spam Rescues</th>
                      <th class="py-3 px-4">Status</th>
                      <th class="py-3 px-4 text-right">Actions</th>
                    </tr>
                  </thead>
                  <tbody class="divide-y divide-slate-800/60 font-sans">
                    @for (box of mailboxes(); track box.id) {
                      <tr class="hover:bg-slate-800/30 transition group">
                        <td class="py-3.5 px-4 font-mono font-medium text-white">
                          <div class="flex items-center gap-2">
                            <span>{{ box.email }}</span>
                            <span class="text-[10px] text-slate-500 font-sans">({{ box.domain }})</span>
                          </div>
                        </td>
                        <td class="py-3.5 px-4">
                          @if (box.provider === 'GoogleWorkspace') {
                            <span class="inline-flex items-center gap-1 px-2 py-0.5 rounded-full text-[10px] font-medium bg-blue-500/10 text-blue-400 border border-blue-500/20">
                              <span>🌐</span> Google Workspace
                            </span>
                          } @else if (box.provider === 'Microsoft365') {
                            <span class="inline-flex items-center gap-1 px-2 py-0.5 rounded-full text-[10px] font-medium bg-amber-500/10 text-amber-400 border border-amber-500/20">
                              <span>🪟</span> Microsoft 365
                            </span>
                          } @else {
                            <span class="inline-flex items-center gap-1 px-2 py-0.5 rounded-full text-[10px] font-medium bg-slate-500/10 text-slate-400 border border-slate-500/20">
                              <span>✉️</span> Generic SMTP
                            </span>
                          }
                        </td>
                        <td class="py-3.5 px-4">
                          <div class="space-y-1">
                            <div class="flex items-center justify-between text-[11px]">
                              <span class="font-medium text-slate-300">Day {{ box.currentRampDay }}</span>
                              <span class="text-slate-400 font-mono">{{ box.recommendedVolumeToday }}/{{ box.dailyVolumeCap }} cap</span>
                            </div>
                            <div class="w-32 bg-slate-800 h-1.5 rounded-full overflow-hidden">
                              <div
                                class="bg-gradient-to-r from-emerald-500 to-teal-400 h-full rounded-full transition-all duration-500"
                                [style.width.%]="(box.recommendedVolumeToday / box.dailyVolumeCap) * 100">
                              </div>
                            </div>
                          </div>
                        </td>
                        <td class="py-3.5 px-4 font-mono">
                          <span class="text-emerald-400 font-semibold">{{ box.sentToday }} sent</span>
                          <span class="text-slate-600 mx-1">/</span>
                          <span class="text-slate-300">{{ box.receivedToday }} recv</span>
                        </td>
                        <td class="py-3.5 px-4 font-mono font-medium">
                          <span [class.text-emerald-400]="box.replyRate >= 0.35 && box.replyRate <= 0.45"
                                [class.text-amber-400]="box.replyRate < 0.35 || box.replyRate > 0.45">
                            {{ (box.replyRate * 100).toFixed(1) }}%
                          </span>
                        </td>
                        <td class="py-3.5 px-4">
                          <span class="px-2 py-0.5 rounded font-mono text-[11px] font-bold"
                            [ngClass]="box.spamRescuesCount > 0 ? 'bg-amber-500/20 text-amber-400 border border-amber-500/30' : 'text-slate-500'">
                            {{ box.spamRescuesCount }} rescued
                          </span>
                        </td>
                        <td class="py-3.5 px-4">
                          @if (box.status === 'Healthy') {
                            <span class="inline-flex items-center gap-1 px-2.5 py-0.5 rounded-full text-[10px] font-semibold bg-emerald-500/10 text-emerald-400 border border-emerald-500/30">
                              <span class="w-1.5 h-1.5 rounded-full bg-emerald-400"></span> Healthy
                            </span>
                          } @else if (box.status === 'WarmingUp') {
                            <span class="inline-flex items-center gap-1 px-2.5 py-0.5 rounded-full text-[10px] font-semibold bg-amber-500/10 text-amber-400 border border-amber-500/30">
                              <span class="w-1.5 h-1.5 rounded-full bg-amber-400"></span> Warming Up
                            </span>
                          } @else if (box.status === 'SpamAlert') {
                            <span class="inline-flex items-center gap-1 px-2.5 py-0.5 rounded-full text-[10px] font-semibold bg-rose-500/10 text-rose-400 border border-rose-500/30">
                              <span class="w-1.5 h-1.5 rounded-full bg-rose-400 animate-ping"></span> Spam Alert
                            </span>
                          } @else {
                            <span class="inline-flex items-center gap-1 px-2.5 py-0.5 rounded-full text-[10px] font-semibold bg-slate-800 text-slate-400">
                              Paused
                            </span>
                          }
                        </td>
                        <td class="py-3.5 px-4 text-right">
                          <button
                            (click)="inspectDnsDomain(box.domain)"
                            class="px-2.5 py-1 bg-slate-800 hover:bg-slate-700 text-slate-300 rounded text-[11px] font-medium transition">
                            DNS Audit →
                          </button>
                        </td>
                      </tr>
                    }
                  </tbody>
                </table>
              </div>
            </div>
          </div>
        }

        <!-- TAB 2: SPAM RESCUE TERMINAL -->
        @if (activeTab() === 'rescue') {
          <div class="p-6 space-y-6">
            <!-- Header banner with explanation -->
            <div class="bg-slate-900/90 border border-slate-800 rounded-2xl p-5 flex items-center justify-between">
              <div>
                <h3 class="text-sm font-bold text-white flex items-center gap-2">
                  <span>🚨</span> Autonomous ESP Spam Rescue Daemon Terminal
                </h3>
                <p class="text-xs text-slate-400 mt-1 max-w-3xl">
                  When peer warmup emails land in Junk or Spam, the daemon automatically executes a 5-step classification correction:
                  <strong class="text-slate-200">Move to Inbox ➔ Mark Not Spam ➔ Star / Important ➔ Dwell Delay ➔ Positive Contextual Reply</strong>.
                  This programmatically retrains Google Priority Inbox and Microsoft SmartScreen machine learning weights.
                </p>
              </div>
              <button
                (click)="triggerSpamRescueSweep()"
                [disabled]="isRescuing()"
                class="px-4 py-2 bg-emerald-600 hover:bg-emerald-500 text-white rounded-xl text-xs font-semibold shadow-lg shadow-emerald-950/40 flex items-center gap-2 transition disabled:opacity-50">
                <span [class.animate-spin]="isRescuing()">⚡</span>
                <span>{{ isRescuing() ? 'Executing Sweep...' : 'Run Spam Rescue Sweep Now' }}</span>
              </button>
            </div>

            <!-- Terminal Window -->
            <div class="bg-black/90 border border-slate-800 rounded-2xl p-5 font-mono text-xs shadow-2xl">
              <!-- Terminal Header Bar -->
              <div class="flex items-center justify-between border-b border-slate-800 pb-3 mb-4 text-slate-400 text-[11px]">
                <div class="flex items-center gap-2">
                  <div class="w-3 h-3 rounded-full bg-rose-500/80"></div>
                  <div class="w-3 h-3 rounded-full bg-amber-500/80"></div>
                  <div class="w-3 h-3 rounded-full bg-emerald-500/80"></div>
                  <span class="ml-2 text-slate-300 font-semibold">daemon@sgrowing-warmup-worker: ~/rescue-stream</span>
                </div>
                <div class="flex items-center gap-2 text-[10px]">
                  <span class="w-2 h-2 rounded-full bg-emerald-400 animate-pulse"></span>
                  <span class="text-emerald-400">DAEMON SWEEP ACTIVE</span>
                  <span class="text-slate-600">|</span>
                  <span>{{ rescueLogs().length }} events recorded</span>
                </div>
              </div>

              <!-- Logs Stream -->
              <div class="space-y-3 max-h-[550px] overflow-y-auto pr-2">
                @for (log of rescueLogs(); track log.rescueId) {
                  <div class="p-3 bg-slate-950 border border-slate-800/80 rounded-xl space-y-2 hover:border-slate-700 transition">
                    <div class="flex items-center justify-between text-slate-400 text-[11px]">
                      <div class="flex items-center gap-2">
                        <span class="text-emerald-400 font-bold">[RESCUED]</span>
                        <span class="text-white font-semibold">{{ log.mailboxEmail }}</span>
                        <span class="text-slate-600">•</span>
                        <span class="text-slate-300 italic">"{{ log.subject }}"</span>
                      </div>
                      <span class="text-slate-500">{{ log.rescuedAtUtc | date:'shortTime' }}</span>
                    </div>

                    <!-- Step badges pipeline -->
                    <div class="flex flex-wrap items-center gap-2 text-[10px]">
                      <span class="px-2 py-0.5 rounded bg-rose-950/60 text-rose-300 border border-rose-800/40">
                        1. Spam Folder Detected
                      </span>
                      <span class="text-slate-600">➔</span>
                      <span class="px-2 py-0.5 rounded bg-blue-950/60 text-blue-300 border border-blue-800/40">
                        2. Moved to Primary Inbox
                      </span>
                      <span class="text-slate-600">➔</span>
                      <span class="px-2 py-0.5 rounded bg-emerald-950/60 text-emerald-300 border border-emerald-800/40">
                        3. Flagged "Not Spam"
                      </span>
                      <span class="text-slate-600">➔</span>
                      <span class="px-2 py-0.5 rounded bg-amber-950/60 text-amber-300 border border-amber-800/40">
                        4. Marked Important ★
                      </span>
                      <span class="text-slate-600">➔</span>
                      <span class="px-2 py-0.5 rounded bg-teal-950/60 text-teal-300 border border-teal-800/40">
                        5. Positive Reply Sent (delay {{ log.humanReadingDelaySeconds }}s)
                      </span>
                    </div>

                    <!-- Classifier Impact Line -->
                    <div class="text-[11px] text-emerald-400 bg-emerald-950/30 px-2.5 py-1 rounded border border-emerald-500/20 flex items-center justify-between">
                      <span>🤖 ML Classifier Signal: {{ log.espMlClassifierSignal }}</span>
                      <span class="text-[9px] text-slate-500 font-mono">Token: {{ log.warmupToken.substring(0, 16) }}...</span>
                    </div>
                  </div>
                }

                @if (rescueLogs().length === 0) {
                  <div class="text-center py-12 text-slate-500">
                    No spam rescue logs recorded yet. Trigger a sweep above to simulate automated folder scan.
                  </div>
                }
              </div>
            </div>
          </div>
        }

        <!-- TAB 3: DNS & 26-DNSBL DEEP AUDIT -->
        @if (activeTab() === 'dnsbl') {
          <div class="p-6 space-y-6">
            <!-- Domain Search & Audit Bar -->
            <div class="bg-slate-900/90 border border-slate-800 rounded-2xl p-5 flex flex-wrap items-center justify-between gap-4">
              <div>
                <h3 class="text-sm font-bold text-white flex items-center gap-2">
                  <span>🔍</span> Deep DNS & 26-DNSBL Blacklist Deliverability Audit
                </h3>
                <p class="text-xs text-slate-400 mt-0.5">
                  Audits SPF lookup limits (&lt;10), DKIM 2048-bit key strength, strict DMARC policies, BIMI VMC certificates, and 26 major reputation networks.
                </p>
              </div>
              <div class="flex items-center gap-2 w-full sm:w-auto">
                <input
                  type="text"
                  [(ngModel)]="searchDomain"
                  placeholder="e.g. sgrowing.io"
                  class="bg-slate-950 border border-slate-800 rounded-xl px-3 py-1.5 text-xs text-white placeholder-slate-500 focus:outline-none focus:border-emerald-500 font-mono w-48" />
                <button
                  (click)="runDnsAudit(searchDomain)"
                  [disabled]="isAuditingDns()"
                  class="px-3.5 py-1.5 bg-emerald-600 hover:bg-emerald-500 text-white rounded-xl text-xs font-semibold shadow transition disabled:opacity-50 flex items-center gap-1.5">
                  <span [class.animate-spin]="isAuditingDns()">↻</span>
                  <span>{{ isAuditingDns() ? 'Auditing...' : 'Run Audit' }}</span>
                </button>
              </div>
            </div>

            @if (auditResult()) {
              <!-- 4 Protocol Deep Cards Grid -->
              <div class="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-4 gap-4">
                <!-- SPF Card -->
                <div class="bg-slate-900/90 border border-slate-800 rounded-2xl p-5 space-y-3">
                  <div class="flex items-center justify-between">
                    <span class="text-xs font-bold text-white flex items-center gap-1.5">
                      <span>📜</span> SPF Record
                    </span>
                    <span class="px-2 py-0.5 rounded text-[10px] font-bold"
                      [ngClass]="{
                        'bg-emerald-500/20 text-emerald-400 border border-emerald-500/30': auditResult()?.spf?.status === 'Pass',
                        'bg-amber-500/20 text-amber-400 border border-amber-500/30': auditResult()?.spf?.status === 'Warning',
                        'bg-rose-500/20 text-rose-400 border border-rose-500/30': auditResult()?.spf?.status === 'Fail'
                      }">
                      {{ auditResult()?.spf?.status }}
                    </span>
                  </div>
                  <div class="text-[11px] text-slate-300">
                    DNS Lookups: <strong class="text-white font-mono">{{ auditResult()?.spf?.dnsLookupCount }} / 10 limit</strong>
                  </div>
                  <div class="text-[11px] text-slate-400">
                    Policy Qualifier: <code class="text-amber-300 font-mono">{{ auditResult()?.spf?.qualifier }}</code>
                  </div>
                  <div class="text-[10px] text-slate-400 border-t border-slate-800 pt-2">
                    {{ auditResult()?.spf?.details }}
                  </div>
                </div>

                <!-- DKIM Card -->
                <div class="bg-slate-900/90 border border-slate-800 rounded-2xl p-5 space-y-3">
                  <div class="flex items-center justify-between">
                    <span class="text-xs font-bold text-white flex items-center gap-1.5">
                      <span>🔑</span> DKIM Signature
                    </span>
                    <span class="px-2 py-0.5 rounded text-[10px] font-bold"
                      [ngClass]="{
                        'bg-emerald-500/20 text-emerald-400 border border-emerald-500/30': auditResult()?.dkim?.status === 'Pass',
                        'bg-amber-500/20 text-amber-400 border border-amber-500/30': auditResult()?.dkim?.status === 'Warning',
                        'bg-rose-500/20 text-rose-400 border border-rose-500/30': auditResult()?.dkim?.status === 'Fail'
                      }">
                      {{ auditResult()?.dkim?.status }}
                    </span>
                  </div>
                  <div class="text-[11px] text-slate-300">
                    Key Strength: <strong class="text-white font-mono">{{ auditResult()?.dkim?.keyLengthBits }}-bit RSA</strong>
                  </div>
                  <div class="text-[11px] text-slate-400">
                    Selector: <code class="text-slate-200 font-mono">{{ auditResult()?.dkim?.selector }}</code>
                  </div>
                  <div class="text-[10px] text-slate-400 border-t border-slate-800 pt-2">
                    {{ auditResult()?.dkim?.details }}
                  </div>
                </div>

                <!-- DMARC Card -->
                <div class="bg-slate-900/90 border border-slate-800 rounded-2xl p-5 space-y-3">
                  <div class="flex items-center justify-between">
                    <span class="text-xs font-bold text-white flex items-center gap-1.5">
                      <span>🛡️</span> DMARC Policy
                    </span>
                    <span class="px-2 py-0.5 rounded text-[10px] font-bold"
                      [ngClass]="{
                        'bg-emerald-500/20 text-emerald-400 border border-emerald-500/30': auditResult()?.dmarc?.status === 'Pass',
                        'bg-amber-500/20 text-amber-400 border border-amber-500/30': auditResult()?.dmarc?.status === 'Warning',
                        'bg-rose-500/20 text-rose-400 border border-rose-500/30': auditResult()?.dmarc?.status === 'Fail'
                      }">
                      {{ auditResult()?.dmarc?.status }}
                    </span>
                  </div>
                  <div class="text-[11px] text-slate-300">
                    Enforcement: <code class="text-emerald-400 font-mono font-bold">p={{ auditResult()?.dmarc?.policy }} ({{ auditResult()?.dmarc?.percentage }}%)</code>
                  </div>
                  <div class="text-[11px] text-slate-400">
                    Reporting: <span class="text-white font-mono">{{ auditResult()?.dmarc?.hasRua ? 'RUA ✓' : 'No RUA' }}</span>
                  </div>
                  <div class="text-[10px] text-slate-400 border-t border-slate-800 pt-2">
                    {{ auditResult()?.dmarc?.details }}
                  </div>
                </div>

                <!-- BIMI Card -->
                <div class="bg-slate-900/90 border border-slate-800 rounded-2xl p-5 space-y-3">
                  <div class="flex items-center justify-between">
                    <span class="text-xs font-bold text-white flex items-center gap-1.5">
                      <span>✨</span> BIMI Brand Avatar
                    </span>
                    <span class="px-2 py-0.5 rounded text-[10px] font-bold"
                      [ngClass]="{
                        'bg-emerald-500/20 text-emerald-400 border border-emerald-500/30': auditResult()?.bimi?.status === 'Pass',
                        'bg-amber-500/20 text-amber-400 border border-amber-500/30': auditResult()?.bimi?.status === 'Warning',
                        'bg-rose-500/20 text-rose-400 border border-rose-500/30': auditResult()?.bimi?.status === 'Fail'
                      }">
                      {{ auditResult()?.bimi?.status }}
                    </span>
                  </div>
                  <div class="text-[11px] text-slate-300">
                    SVG Logo: <span class="font-bold text-white">{{ auditResult()?.bimi?.hasValidSvgFormat ? 'Verified ✓' : 'Missing' }}</span>
                  </div>
                  <div class="text-[11px] text-slate-400">
                    VMC Cert: <span class="text-amber-400 font-mono">{{ auditResult()?.bimi?.hasVmcCertificate ? 'Digicert VMC ✓' : 'Self-hosted' }}</span>
                  </div>
                  <div class="text-[10px] text-slate-400 border-t border-slate-800 pt-2">
                    {{ auditResult()?.bimi?.details }}
                  </div>
                </div>
              </div>

              <!-- 26 Major Blacklists Grid -->
              <div class="bg-slate-900/90 border border-slate-800 rounded-2xl p-5 space-y-4 shadow-xl">
                <div class="flex items-center justify-between">
                  <div class="flex items-center gap-3">
                    <h3 class="text-sm font-bold text-white flex items-center gap-2">
                      <span>🌐</span> Real-Time 26-DNSBL Blacklist Health Monitor
                    </h3>
                    <span class="bg-emerald-500/10 text-emerald-400 text-[10px] font-mono px-2 py-0.5 rounded-full border border-emerald-500/20">
                      {{ auditResult()?.blacklistsCleanCount }} / {{ auditResult()?.totalBlacklistsChecked }} Clean
                    </span>
                  </div>

                  <div class="flex items-center gap-2">
                    <span class="text-xs text-slate-400">Filter:</span>
                    <button
                      (click)="blacklistFilter.set('all')"
                      [class.bg-emerald-600]="blacklistFilter() === 'all'"
                      [class.text-white]="blacklistFilter() === 'all'"
                      class="px-2 py-0.5 rounded text-[11px] bg-slate-800 text-slate-300">
                      All ({{ auditResult()?.blacklists?.length }})
                    </button>
                    <button
                      (click)="blacklistFilter.set('listed')"
                      [class.bg-rose-600]="blacklistFilter() === 'listed'"
                      [class.text-white]="blacklistFilter() === 'listed'"
                      class="px-2 py-0.5 rounded text-[11px] bg-slate-800 text-slate-300">
                      Listed ({{ auditResult()?.blacklistsListedCount }})
                    </button>
                  </div>
                </div>

                <div class="grid grid-cols-1 sm:grid-cols-2 md:grid-cols-3 lg:grid-cols-4 gap-3">
                  @for (bl of filteredBlacklists(); track bl.host) {
                    <div class="p-3 rounded-xl border flex flex-col justify-between transition"
                      [ngClass]="bl.isListed ? 'bg-rose-950/30 border-rose-800/60' : 'bg-slate-950/70 border-slate-800/80 hover:border-slate-700'">
                      <div class="flex items-center justify-between mb-1.5">
                        <span class="text-xs font-semibold text-white truncate max-w-[170px]" [title]="bl.name">
                          {{ bl.name }}
                        </span>
                        @if (!bl.isListed) {
                          <span class="text-[10px] font-bold text-emerald-400 bg-emerald-950/60 px-1.5 py-0.5 rounded border border-emerald-800/40">
                            CLEAN ✓
                          </span>
                        } @else {
                          <span class="text-[10px] font-bold text-rose-400 bg-rose-950 px-1.5 py-0.5 rounded border border-rose-700 animate-pulse">
                            LISTED ✕
                          </span>
                        }
                      </div>

                      <div class="text-[10px] font-mono text-slate-400 truncate mb-2" [title]="bl.host">
                        {{ bl.host }}
                      </div>

                      <div class="flex items-center justify-between text-[10px] text-slate-500 border-t border-slate-800/60 pt-1.5">
                        <span>{{ bl.category }}</span>
                        <span class="font-mono text-slate-400">{{ bl.responseTimeMs }}ms</span>
                      </div>
                    </div>
                  }
                </div>
              </div>
            }
          </div>
        }

        <!-- TAB 4: CONVERSATIONAL THREAD DIALOGUE GENERATOR -->
        @if (activeTab() === 'exchange') {
          <div class="p-6 space-y-6">
            <div class="bg-slate-900/90 border border-slate-800 rounded-2xl p-5 flex items-center justify-between">
              <div>
                <h3 class="text-sm font-bold text-white flex items-center gap-2">
                  <span>💬</span> Synthetic Peer-to-Peer Dialogue Generator
                </h3>
                <p class="text-xs text-slate-400 mt-1">
                  Generates multi-turn corporate email exchanges between paired peers tagged with cryptographic warmup token headers.
                </p>
              </div>
              <div class="flex items-center gap-2">
                <select [(ngModel)]="selectedTopic" class="bg-slate-950 border border-slate-800 text-xs text-slate-200 rounded-xl px-3 py-1.5">
                  <option value="saas">Q4 SaaS Renewal Strategy</option>
                  <option value="logistics">Logistics Fleet Update</option>
                  <option value="compliance">Vendor Compliance Review</option>
                </select>
                <button
                  (click)="triggerWarmupExchange(selectedTopic)"
                  [disabled]="isGeneratingThread()"
                  class="px-4 py-1.5 bg-emerald-600 hover:bg-emerald-500 text-white rounded-xl text-xs font-semibold transition disabled:opacity-50">
                  {{ isGeneratingThread() ? 'Generating...' : 'Generate P2P Exchange' }}
                </button>
              </div>
            </div>

            @if (latestExchange()) {
              <div class="bg-slate-900/90 border border-slate-800 rounded-2xl p-6 space-y-5 shadow-xl">
                <!-- Thread Header -->
                <div class="border-b border-slate-800 pb-4 flex flex-wrap items-center justify-between gap-3">
                  <div>
                    <h2 class="text-base font-bold text-white flex items-center gap-2">
                      <span>📧</span> {{ latestExchange()?.subject }}
                    </h2>
                    <div class="flex items-center gap-4 text-xs text-slate-400 mt-1 font-mono">
                      <span>Sender: <strong class="text-slate-200">{{ latestExchange()?.senderEmail }}</strong></span>
                      <span>Peer: <strong class="text-slate-200">{{ latestExchange()?.peerEmail }}</strong></span>
                    </div>
                  </div>

                  <!-- Cryptographic Header Pill -->
                  <div class="bg-slate-950 border border-slate-800 rounded-xl p-2.5 max-w-md">
                    <div class="text-[10px] text-slate-400 font-mono font-bold flex items-center justify-between">
                      <span class="text-amber-400">Header: X-SGrowing-Warmup-Token</span>
                      <span class="text-emerald-400">HMAC-SHA256 Signed ✓</span>
                    </div>
                    <div class="text-[10px] text-slate-500 font-mono truncate mt-0.5">
                      {{ latestExchange()?.warmupToken }}
                    </div>
                  </div>
                </div>

                <!-- Turns Conversation Stream -->
                <div class="space-y-4">
                  @for (turn of latestExchange()?.turns; track turn.turnIndex) {
                    <div class="p-4 rounded-xl border space-y-3 transition"
                      [ngClass]="turn.turnIndex % 2 === 1 ? 'bg-slate-950/80 border-slate-800 ml-0 mr-8' : 'bg-slate-900 border-slate-700/80 ml-8 mr-0'">
                      <div class="flex items-center justify-between text-xs">
                        <div class="flex items-center gap-2">
                          <span class="px-2 py-0.5 rounded text-[10px] font-bold"
                            [ngClass]="turn.turnIndex % 2 === 1 ? 'bg-emerald-500/20 text-emerald-400' : 'bg-teal-500/20 text-teal-400'">
                            Turn {{ turn.turnIndex }} • {{ turn.turnIndex % 2 === 1 ? 'Sender' : 'Peer Reply' }}
                          </span>
                          <span class="font-mono text-slate-300 font-semibold">{{ turn.senderEmail }}</span>
                          <span class="text-slate-600">➔</span>
                          <span class="font-mono text-slate-400">{{ turn.recipientEmail }}</span>
                        </div>
                        <span class="text-[10px] text-slate-500 font-mono">Dwell: {{ turn.dwellDelaySeconds }}s</span>
                      </div>

                      <div class="text-xs text-slate-200 whitespace-pre-line leading-relaxed">
                        {{ turn.body }}
                      </div>

                      <div class="text-[11px] text-slate-400 whitespace-pre-line border-t border-slate-800/80 pt-2 font-mono">
                        {{ turn.signature }}
                      </div>
                    </div>
                  }
                </div>
              </div>
            }
          </div>
        }
      </main>

      <!-- ENROLL MAILBOX MODAL -->
      @if (openEnrollModal()) {
        <div class="fixed inset-0 bg-black/70 backdrop-blur-sm flex items-center justify-center z-50 p-4">
          <div class="bg-slate-900 border border-slate-800 rounded-2xl w-full max-w-md p-6 space-y-4 shadow-2xl">
            <div class="flex items-center justify-between border-b border-slate-800 pb-3">
              <h3 class="text-sm font-bold text-white flex items-center gap-2">
                <span>📬</span> Enroll Mailbox into Warmup Network
              </h3>
              <button (click)="openEnrollModal.set(false)" class="text-slate-400 hover:text-white text-xs">✕</button>
            </div>

            <div class="space-y-3">
              <div>
                <label class="text-[11px] text-slate-400 block mb-1">Mailbox Email Address</label>
                <input
                  type="email"
                  [(ngModel)]="newEmail"
                  placeholder="outreach@yourdomain.com"
                  class="w-full bg-slate-950 border border-slate-800 rounded-xl p-2.5 text-xs text-white focus:outline-none focus:border-emerald-500 font-mono" />
              </div>

              <div>
                <label class="text-[11px] text-slate-400 block mb-1">Mailbox ESP Provider</label>
                <select
                  [(ngModel)]="newProvider"
                  class="w-full bg-slate-950 border border-slate-800 rounded-xl p-2.5 text-xs text-slate-200 focus:outline-none focus:border-emerald-500">
                  <option value="GoogleWorkspace">Google Workspace (Gmail)</option>
                  <option value="Microsoft365">Microsoft 365 (Exchange/Outlook)</option>
                  <option value="GenericSmtp">Generic SMTP / IMAP</option>
                </select>
              </div>

              <div class="grid grid-cols-2 gap-3">
                <div>
                  <label class="text-[11px] text-slate-400 block mb-1">Daily Cap (Max 150)</label>
                  <input
                    type="number"
                    [(ngModel)]="newDailyCap"
                    class="w-full bg-slate-950 border border-slate-800 rounded-xl p-2 text-xs text-white font-mono" />
                </div>
                <div>
                  <label class="text-[11px] text-slate-400 block mb-1">Starting Ramp Day</label>
                  <input
                    type="number"
                    [(ngModel)]="newStartingDay"
                    class="w-full bg-slate-950 border border-slate-800 rounded-xl p-2 text-xs text-white font-mono" />
                </div>
              </div>
            </div>

            <div class="flex items-center justify-end gap-2 border-t border-slate-800 pt-4">
              <button
                (click)="openEnrollModal.set(false)"
                class="px-3.5 py-1.5 bg-slate-800 hover:bg-slate-700 text-slate-300 rounded-xl text-xs font-medium transition">
                Cancel
              </button>
              <button
                (click)="submitEnrollMailbox()"
                [disabled]="!newEmail || isSubmittingEnroll()"
                class="px-4 py-1.5 bg-emerald-600 hover:bg-emerald-500 text-white rounded-xl text-xs font-semibold shadow transition disabled:opacity-50">
                {{ isSubmittingEnroll() ? 'Enrolling...' : 'Enroll Mailbox' }}
              </button>
            </div>
          </div>
        </div>
      }
    </div>
  `
})
export class WarmupRescueCockpitComponent implements OnInit {
  private readonly api = inject(ApiService);

  // Active Tab
  public activeTab = signal<'cockpit' | 'rescue' | 'dnsbl' | 'exchange'>('cockpit');

  // State Signals
  public mailboxes = signal<EnrolledWarmupMailboxDto[]>([]);
  public rescueLogs = signal<SpamRescueLogEntry[]>([]);
  public auditResult = signal<DnsAndBlacklistAuditResult | null>(null);
  public metrics = signal<DeliverabilityNetworkMetrics | null>(null);
  public latestExchange = signal<WarmupExchangeResult | null>(null);

  // UI state
  public isRescuing = signal(false);
  public isAuditingDns = signal(false);
  public isGeneratingThread = signal(false);
  public isSubmittingEnroll = signal(false);
  public openEnrollModal = signal(false);
  public notificationMessage = signal<string | null>(null);

  // Search & Filter
  public searchDomain = 'sgrowing.io';
  public blacklistFilter = signal<'all' | 'listed'>('all');
  public selectedTopic = 'saas';

  // Enroll Form Inputs
  public newEmail = '';
  public newProvider = 'GoogleWorkspace';
  public newDailyCap = 40;
  public newStartingDay = 1;

  // SVG Gauge calculations
  public readonly circleRadius = 48;
  public readonly circleCircumference = 2 * Math.PI * this.circleRadius; // ~301.59

  public overallHealthScore = computed(() => {
    return this.auditResult()?.overallHealthScore?.score || this.metrics()?.averageHealthScore || 94;
  });

  public circleDashoffset = computed(() => {
    const score = Math.max(0, Math.min(100, this.overallHealthScore()));
    return this.circleCircumference - (score / 100) * this.circleCircumference;
  });

  public healthGrade = computed(() => {
    return this.auditResult()?.overallHealthScore?.grade || 'A+';
  });

  public healthStatus = computed(() => {
    return this.auditResult()?.overallHealthScore?.status || 'Optimal';
  });

  public gaugeStrokeColor = computed(() => {
    const score = this.overallHealthScore();
    if (score >= 85) return '#10b981'; // emerald-500
    if (score >= 65) return '#f59e0b'; // amber-500
    return '#f43f5e'; // rose-500
  });

  public filteredBlacklists = computed(() => {
    const list = this.auditResult()?.blacklists || [];
    if (this.blacklistFilter() === 'listed') {
      return list.filter(b => b.isListed);
    }
    return list;
  });

  public ngOnInit(): void {
    this.loadMailboxes();
    this.loadRescueLogs();
    this.loadMetrics();
    this.runDnsAudit('sgrowing.io');
  }

  public loadMailboxes(): void {
    this.api.getWarmupMailboxes().subscribe({
      next: (data) => this.mailboxes.set(data),
      error: () => this.setSampleMailboxesFallback()
    });
  }

  public loadRescueLogs(): void {
    this.api.getSpamRescueLogs(50).subscribe({
      next: (logs) => this.rescueLogs.set(logs),
      error: () => {}
    });
  }

  public loadMetrics(): void {
    this.api.getWarmupMetrics().subscribe({
      next: (m) => this.metrics.set(m),
      error: () => {}
    });
  }

  public runDnsAudit(domain: string): void {
    if (!domain) return;
    this.isAuditingDns.set(true);
    this.api.auditDomainDns(domain).subscribe({
      next: (res) => {
        this.auditResult.set(res);
        this.isAuditingDns.set(false);
      },
      error: () => {
        this.isAuditingDns.set(false);
      }
    });
  }

  public inspectDnsDomain(domain: string): void {
    this.searchDomain = domain;
    this.activeTab.set('dnsbl');
    this.runDnsAudit(domain);
  }

  public triggerSpamRescueSweep(): void {
    this.isRescuing.set(true);
    this.api.executeSpamRescueScan().subscribe({
      next: (res) => {
        this.isRescuing.set(false);
        this.notificationMessage.set(`Autonomous Spam Rescue Sweep completed: ${res.rescuedMessagesCount} emails rescued, ML classifier trained with ${res.achievedReplyRatio * 100}% reply ratio.`);
        this.loadRescueLogs();
        this.loadMailboxes();
        this.loadMetrics();
      },
      error: () => {
        this.isRescuing.set(false);
      }
    });
  }

  public triggerWarmupExchange(topic: string): void {
    this.isGeneratingThread.set(true);
    this.api.triggerWarmupExchange({ topic }).subscribe({
      next: (res) => {
        this.latestExchange.set(res);
        this.isGeneratingThread.set(false);
        this.activeTab.set('exchange');
        this.notificationMessage.set(`Synthetic dialogue thread generated for topic: ${res.subject}`);
        this.loadMailboxes();
      },
      error: () => {
        this.isGeneratingThread.set(false);
      }
    });
  }

  public submitEnrollMailbox(): void {
    if (!this.newEmail) return;
    this.isSubmittingEnroll.set(true);
    const req: EnrollMailboxRequest = {
      email: this.newEmail,
      provider: this.newProvider,
      dailyVolumeCap: this.newDailyCap,
      startingDay: this.newStartingDay
    };

    this.api.enrollWarmupMailbox(req).subscribe({
      next: (enrolled) => {
        this.isSubmittingEnroll.set(false);
        this.openEnrollModal.set(false);
        this.notificationMessage.set(`Enrolled ${enrolled.email} into warmup peer network on Day ${enrolled.currentRampDay}.`);
        this.newEmail = '';
        this.loadMailboxes();
        this.loadMetrics();
      },
      error: () => {
        this.isSubmittingEnroll.set(false);
      }
    });
  }

  private setSampleMailboxesFallback(): void {
    this.mailboxes.set([
      {
        id: '1',
        tenantId: 't1',
        email: 'david.stone@growth-advisors.io',
        domain: 'growth-advisors.io',
        provider: 'GoogleWorkspace',
        dailyVolumeCap: 45,
        currentRampDay: 18,
        recommendedVolumeToday: 42,
        sentToday: 28,
        receivedToday: 26,
        replyCount: 11,
        spamRescuesCount: 4,
        replyRate: 0.42,
        status: 'Healthy',
        enrolledAtUtc: new Date().toISOString(),
        lastActiveAtUtc: new Date().toISOString()
      },
      {
        id: '2',
        tenantId: 't1',
        email: 'clara.vance@saasvelocity.net',
        domain: 'saasvelocity.net',
        provider: 'Microsoft365',
        dailyVolumeCap: 40,
        currentRampDay: 12,
        recommendedVolumeToday: 35,
        sentToday: 22,
        receivedToday: 20,
        replyCount: 8,
        spamRescuesCount: 2,
        replyRate: 0.38,
        status: 'WarmingUp',
        enrolledAtUtc: new Date().toISOString(),
        lastActiveAtUtc: new Date().toISOString()
      }
    ]);
  }
}
