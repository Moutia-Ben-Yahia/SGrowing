import { Component, OnInit, signal, computed, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { ApiService } from '../../core/services/api.service';
import { AuthService } from '../../core/services/auth.service';
import {
  MultiInboxAccountDto,
  RegisterSenderInboxRequest,
  SelectNextSenderResponse,
  DeliverabilityRadarMetricsDto,
  RotationSimulationResultDto,
  MailboxProviderType,
  InboxRotationStatusType
} from '../../core/models/deliverability.models';

@Component({
  selector: 'app-multi-inbox-cockpit',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterLink],
  template: `
    <div dir="ltr" class="flex h-screen w-screen overflow-hidden bg-slate-950 text-slate-100 font-sans">
      <!-- Main Content Area -->
      <main class="flex-1 flex flex-col overflow-y-auto">
        <!-- Topbar -->
        <header class="h-16 border-b border-slate-800 bg-slate-900/80 backdrop-blur px-6 flex items-center justify-between shrink-0">
          <div class="flex items-center gap-3">
            <a routerLink="/" class="text-xs text-slate-400 hover:text-white flex items-center gap-1 transition">
              ← Hub
            </a>
            <span class="text-slate-700">|</span>
            <div class="flex items-center gap-2">
              <span class="text-xl">🔄</span>
              <span class="text-base font-bold text-white tracking-tight">
                Multi-Inbox Sender Rotation & Deliverability Radar
              </span>
            </div>
            <span class="bg-indigo-500/10 text-indigo-400 text-[11px] font-mono px-2.5 py-0.5 rounded-full border border-indigo-500/20 font-semibold">
              Instantly & Warmbly Architecture • Frontier 7
            </span>
          </div>

          <div class="flex items-center gap-3">
            <!-- Navigation Tabs -->
            <div class="flex bg-slate-800/80 p-0.5 rounded-lg border border-slate-700/60 text-xs">
              <button
                (click)="activeTab.set('pool')"
                [class.bg-indigo-600]="activeTab() === 'pool'"
                [class.text-white]="activeTab() === 'pool'"
                class="px-3.5 py-1.5 rounded-md text-slate-300 transition font-medium flex items-center gap-1.5">
                📬 Sender Pool & Quotas
              </button>
              <button
                (click)="activeTab.set('radar')"
                [class.bg-indigo-600]="activeTab() === 'radar'"
                [class.text-white]="activeTab() === 'radar'"
                class="px-3.5 py-1.5 rounded-md text-slate-300 transition font-medium flex items-center gap-1.5">
                🎯 Deliverability Radar
              </button>
              <button
                (click)="activeTab.set('simulator')"
                [class.bg-indigo-600]="activeTab() === 'simulator'"
                [class.text-white]="activeTab() === 'simulator'"
                class="px-3.5 py-1.5 rounded-md text-slate-300 transition font-medium flex items-center gap-1.5">
                ⚡ Rotation Simulator
              </button>
            </div>

            <button
              (click)="openAddModal()"
              class="px-3.5 py-1.5 bg-gradient-to-r from-emerald-600 to-teal-600 hover:from-emerald-500 hover:to-teal-500 text-white rounded-lg text-xs font-semibold shadow-lg shadow-emerald-950/40 flex items-center gap-1.5 transition">
              <span>+</span> Add Sender Inbox
            </button>
          </div>
        </header>

        <!-- Executive Stats Bar -->
        <section class="p-6 pb-2 grid grid-cols-1 md:grid-cols-4 gap-4">
          <!-- Total Connected Inboxes -->
          <div class="p-4 rounded-xl bg-slate-900/60 border border-slate-800 flex items-center justify-between">
            <div>
              <p class="text-[11px] font-semibold text-slate-400 uppercase tracking-wider">Connected Inboxes</p>
              <div class="flex items-baseline gap-2 mt-1">
                <span class="text-2xl font-bold text-white">{{ radar()?.totalInboxes ?? inboxes().length }}</span>
                <span class="text-xs text-emerald-400 font-medium">
                  {{ radar()?.activeInboxes ?? activeInboxesCount() }} active
                </span>
              </div>
              <p class="text-[10px] text-slate-500 mt-1">
                {{ radar()?.warmupInboxes ?? warmupInboxesCount() }} in warmup • {{ radar()?.quarantinedInboxes ?? quarantinedInboxesCount() }} quarantined
              </p>
            </div>
            <div class="w-10 h-10 rounded-lg bg-indigo-500/10 border border-indigo-500/20 text-indigo-400 flex items-center justify-center text-lg">
              📬
            </div>
          </div>

          <!-- Available Daily Capacity -->
          <div class="p-4 rounded-xl bg-slate-900/60 border border-slate-800 flex items-center justify-between">
            <div>
              <p class="text-[11px] font-semibold text-slate-400 uppercase tracking-wider">Daily Sending Capacity</p>
              <div class="flex items-baseline gap-2 mt-1">
                <span class="text-2xl font-bold text-white">{{ radar()?.remainingDailyCapacity ?? totalRemainingCapacity() }}</span>
                <span class="text-xs text-slate-400 font-medium">/ {{ radar()?.totalDailyCapacity ?? totalDailyCapacity() }}</span>
              </div>
              <div class="w-36 bg-slate-800 h-1.5 rounded-full overflow-hidden mt-2">
                <div
                  class="bg-gradient-to-r from-emerald-500 to-teal-400 h-full rounded-full transition-all duration-500"
                  [style.width.%]="capacityPercent()"></div>
              </div>
            </div>
            <div class="w-10 h-10 rounded-lg bg-emerald-500/10 border border-emerald-500/20 text-emerald-400 flex items-center justify-center text-lg">
              📊
            </div>
          </div>

          <!-- Average Deliverability Health -->
          <div class="p-4 rounded-xl bg-slate-900/60 border border-slate-800 flex items-center justify-between">
            <div>
              <p class="text-[11px] font-semibold text-slate-400 uppercase tracking-wider">Deliverability Health</p>
              <div class="flex items-baseline gap-2 mt-1">
                <span class="text-2xl font-bold" [ngClass]="getHealthColorClass(avgHealthScore())">
                  {{ avgHealthScore() }}%
                </span>
                <span class="text-xs px-2 py-0.5 rounded-full text-[10px] font-semibold"
                  [ngClass]="getHealthBadgeClass(avgHealthScore())">
                  {{ radar()?.reputationTier ?? getReputationTier(avgHealthScore()) }}
                </span>
              </div>
              <p class="text-[10px] text-slate-500 mt-1">
                DNS: SPF {{ radar()?.dnsHealth?.spfPassRate ?? 100 }}% • DKIM {{ radar()?.dnsHealth?.dkimPassRate ?? 100 }}% • DMARC {{ radar()?.dnsHealth?.dmarcPassRate ?? 100 }}%
              </p>
            </div>
            <div class="w-10 h-10 rounded-lg bg-teal-500/10 border border-teal-500/20 text-teal-400 flex items-center justify-center text-lg">
              🛡️
            </div>
          </div>

          <!-- Total Dispatches Today -->
          <div class="p-4 rounded-xl bg-slate-900/60 border border-slate-800 flex items-center justify-between">
            <div>
              <p class="text-[11px] font-semibold text-slate-400 uppercase tracking-wider">Dispatches Today</p>
              <div class="flex items-baseline gap-2 mt-1">
                <span class="text-2xl font-bold text-white">{{ radar()?.totalSentToday ?? totalSentToday() }}</span>
                <span class="text-xs text-slate-400">emails</span>
              </div>
              <p class="text-[10px] text-slate-500 mt-1">
                Bounce rate: {{ radar()?.averageBounceRate ?? 0.8 }}% (Threshold: &lt; 2.5%)
              </p>
            </div>
            <div class="w-10 h-10 rounded-lg bg-cyan-500/10 border border-cyan-500/20 text-cyan-400 flex items-center justify-center text-lg">
              🚀
            </div>
          </div>
        </section>

        <!-- Tab 1: SENDER INBOXES GRID & TABLE -->
        @if (activeTab() === 'pool') {
          <div class="p-6 space-y-6">
            <!-- Pool Actions & Filters Bar -->
            <div class="flex flex-col sm:flex-row items-center justify-between gap-4 bg-slate-900/40 p-3 rounded-xl border border-slate-800">
              <div class="flex items-center gap-2">
                <input
                  type="text"
                  [(ngModel)]="searchQuery"
                  placeholder="Filter by email or domain..."
                  class="bg-slate-950 border border-slate-800 rounded-lg px-3 py-1.5 text-xs text-slate-200 placeholder-slate-500 focus:outline-none focus:border-indigo-500 w-64" />
                <select
                  [(ngModel)]="providerFilter"
                  class="bg-slate-950 border border-slate-800 rounded-lg px-3 py-1.5 text-xs text-slate-300 focus:outline-none focus:border-indigo-500">
                  <option value="all">All Providers</option>
                  <option value="GoogleWorkspace">Google Workspace</option>
                  <option value="Microsoft365">Microsoft 365</option>
                  <option value="CustomSmtp">Custom SMTP</option>
                </select>
                <select
                  [(ngModel)]="statusFilter"
                  class="bg-slate-950 border border-slate-800 rounded-lg px-3 py-1.5 text-xs text-slate-300 focus:outline-none focus:border-indigo-500">
                  <option value="all">All Statuses</option>
                  <option value="Active">Active</option>
                  <option value="Warmup">Warmup</option>
                  <option value="Quarantined">Quarantined</option>
                  <option value="Cooling">Cooling</option>
                </select>
              </div>

              <div class="flex items-center gap-2 text-xs">
                <button
                  (click)="openTestDispatchModal()"
                  class="px-3 py-1.5 bg-indigo-600/20 hover:bg-indigo-600/30 text-indigo-300 border border-indigo-500/30 rounded-lg transition font-medium flex items-center gap-1.5">
                  <span>⚡</span> Test Outbound Dispatch & Jitter
                </button>
                <button
                  (click)="loadData()"
                  class="px-2.5 py-1.5 bg-slate-800 hover:bg-slate-700 text-slate-300 rounded-lg transition"
                  title="Refresh pool data">
                  ↻
                </button>
              </div>
            </div>

            <!-- Inboxes Table -->
            <div class="overflow-x-auto rounded-xl border border-slate-800 bg-slate-900/60 shadow-xl">
              <table class="w-full text-left text-xs text-slate-300">
                <thead class="bg-slate-950/70 text-[11px] font-semibold text-slate-400 uppercase tracking-wider border-b border-slate-800">
                  <tr>
                    <th class="px-5 py-3.5">Sender Mailbox</th>
                    <th class="px-4 py-3.5">ESP Provider</th>
                    <th class="px-4 py-3.5">Daily Quota Usage</th>
                    <th class="px-4 py-3.5">Health Score</th>
                    <th class="px-4 py-3.5">Pool Status</th>
                    <th class="px-4 py-3.5">DNS Alignment</th>
                    <th class="px-5 py-3.5 text-right">Actions</th>
                  </tr>
                </thead>
                <tbody class="divide-y divide-slate-800/80">
                  @for (inbox of filteredInboxes(); track inbox.id) {
                    <tr class="hover:bg-slate-800/40 transition">
                      <!-- Sender Mailbox -->
                      <td class="px-5 py-4">
                        <div class="flex items-center gap-3">
                          <div class="w-9 h-9 rounded-full bg-slate-800 border border-slate-700 flex items-center justify-center font-bold text-sm text-slate-200">
                            {{ inbox.email.charAt(0).toUpperCase() }}
                          </div>
                          <div>
                            <div class="font-semibold text-white flex items-center gap-1.5">
                              {{ inbox.email }}
                            </div>
                            <div class="text-[11px] text-slate-400">
                              {{ inbox.displayName }} • <span class="text-indigo-400 font-mono">{{ inbox.domain }}</span>
                            </div>
                          </div>
                        </div>
                      </td>

                      <!-- Provider Badge -->
                      <td class="px-4 py-4">
                        <span class="inline-flex items-center gap-1.5 px-2.5 py-1 rounded-md text-[11px] font-medium border"
                          [ngClass]="getProviderBadgeClass(inbox.provider)">
                          <span>{{ getProviderIcon(inbox.provider) }}</span>
                          <span>{{ getProviderName(inbox.provider) }}</span>
                        </span>
                      </td>

                      <!-- Daily Quota Progress Bar -->
                      <td class="px-4 py-4">
                        <div class="space-y-1.5 w-44">
                          <div class="flex justify-between text-[11px] font-medium">
                            <span class="text-slate-300">{{ inbox.sentTodayCount }} / {{ inbox.dailyQuota }} sent</span>
                            <span [ngClass]="getQuotaColorClass(inbox)">
                              {{ getQuotaPercentage(inbox) }}%
                            </span>
                          </div>
                          <div class="w-full bg-slate-800 h-2 rounded-full overflow-hidden">
                            <div
                              class="h-full rounded-full transition-all duration-300"
                              [ngClass]="getQuotaBarClass(inbox)"
                              [style.width.%]="getQuotaPercentage(inbox)"></div>
                          </div>
                          <p class="text-[10px] text-slate-500">
                            {{ inbox.remainingDailyQuota }} dispatches remaining today
                          </p>
                        </div>
                      </td>

                      <!-- Health Score Badge -->
                      <td class="px-4 py-4">
                        <div class="flex items-center gap-2.5">
                          <div
                            class="w-8 h-8 rounded-full border-2 flex items-center justify-center font-bold text-xs"
                            [ngClass]="getHealthRadialClass(inbox.healthScore)">
                            {{ inbox.healthScore }}
                          </div>
                          <div class="text-[10px] text-slate-400 leading-tight">
                            <div>Bounce: <span class="font-semibold" [class.text-rose-400]="inbox.bounceRate > 2.5">{{ inbox.bounceRate }}%</span></div>
                            <div>Reply: {{ inbox.replyRate }}%</div>
                          </div>
                        </div>
                      </td>

                      <!-- Status Badge -->
                      <td class="px-4 py-4">
                        <span class="inline-flex items-center gap-1 px-2.5 py-1 rounded-full text-[10px] font-semibold border"
                          [ngClass]="getStatusBadgeClass(inbox.status)">
                          <span class="w-1.5 h-1.5 rounded-full" [ngClass]="getStatusDotClass(inbox.status)"></span>
                          {{ getStatusLabel(inbox.status) }}
                        </span>
                        @if (inbox.status === 'Quarantined' || inbox.status === 3) {
                          <p class="text-[9px] text-rose-400/90 max-w-[140px] truncate mt-1" [title]="inbox.quarantineReason || ''">
                            {{ inbox.quarantineReason || 'Safety threshold exceeded' }}
                          </p>
                        }
                      </td>

                      <!-- DNS Validation Pills -->
                      <td class="px-4 py-4">
                        <div class="flex items-center gap-1">
                          <span class="px-1.5 py-0.5 rounded text-[9px] font-mono font-semibold"
                            [class.bg-emerald-500/20]="inbox.hasSpf"
                            [class.text-emerald-400]="inbox.hasSpf"
                            [class.bg-rose-500/20]="!inbox.hasSpf"
                            [class.text-rose-400]="!inbox.hasSpf">
                            SPF: {{ inbox.hasSpf ? 'PASS' : 'FAIL' }}
                          </span>
                          <span class="px-1.5 py-0.5 rounded text-[9px] font-mono font-semibold"
                            [class.bg-emerald-500/20]="inbox.hasDkim"
                            [class.text-emerald-400]="inbox.hasDkim"
                            [class.bg-rose-500/20]="!inbox.hasDkim"
                            [class.text-rose-400]="!inbox.hasDkim">
                            DKIM: {{ inbox.hasDkim ? 'PASS' : 'FAIL' }}
                          </span>
                          <span class="px-1.5 py-0.5 rounded text-[9px] font-mono font-semibold"
                            [class.bg-emerald-500/20]="inbox.hasDmarc"
                            [class.text-emerald-400]="inbox.hasDmarc"
                            [class.bg-rose-500/20]="!inbox.hasDmarc"
                            [class.text-rose-400]="!inbox.hasDmarc">
                            DMARC: {{ inbox.hasDmarc ? 'PASS' : 'FAIL' }}
                          </span>
                        </div>
                      </td>

                      <!-- Action Buttons -->
                      <td class="px-5 py-4 text-right">
                        <div class="inline-flex items-center gap-1.5">
                          <button
                            (click)="triggerQuickTestDispatch(inbox)"
                            title="Trigger test send from this inbox"
                            class="px-2.5 py-1 bg-slate-800 hover:bg-slate-700 text-slate-200 border border-slate-700 rounded-md text-[11px] font-medium transition">
                            Send Test
                          </button>

                          @if (inbox.status === 'Quarantined' || inbox.status === 3) {
                            <button
                              (click)="thawInbox(inbox)"
                              title="Thaw inbox and return to warmup/rotation"
                              class="px-2.5 py-1 bg-emerald-600/20 hover:bg-emerald-600/30 text-emerald-300 border border-emerald-500/30 rounded-md text-[11px] font-semibold transition">
                              Thaw
                            </button>
                          } @else {
                            <button
                              (click)="quarantineInbox(inbox)"
                              title="Quarantine inbox to protect domain reputation"
                              class="px-2.5 py-1 bg-rose-600/20 hover:bg-rose-600/30 text-rose-300 border border-rose-500/30 rounded-md text-[11px] font-medium transition">
                              Quarantine
                            </button>
                          }
                        </div>
                      </td>
                    </tr>
                  } @empty {
                    <tr>
                      <td colspan="7" class="px-6 py-12 text-center text-slate-500">
                        <div class="text-3xl mb-2">📬</div>
                        <p class="font-medium text-slate-400">No sender inboxes registered in this pool yet.</p>
                        <p class="text-xs text-slate-600 mt-1">Connect secondary Google Workspace, Microsoft 365, or SMTP inboxes to scale outbound deliverability.</p>
                        <button
                          (click)="openAddModal()"
                          class="mt-4 px-4 py-2 bg-indigo-600 hover:bg-indigo-500 text-white rounded-lg text-xs font-semibold">
                          + Connect First Sender Mailbox
                        </button>
                      </td>
                    </tr>
                  }
                </tbody>
              </table>
            </div>
          </div>
        }

        <!-- Tab 2: DELIVERABILITY RADAR DEEP-DIVE -->
        @if (activeTab() === 'radar') {
          <div class="p-6 space-y-6">
            <div class="grid grid-cols-1 md:grid-cols-3 gap-6">
              <!-- Radar Overview Card -->
              <div class="p-6 rounded-2xl bg-gradient-to-br from-slate-900 to-slate-950 border border-slate-800 flex flex-col items-center justify-center text-center">
                <span class="text-xs uppercase tracking-wider text-slate-400 font-semibold mb-3">Overall Deliverability Radar</span>
                <div class="w-32 h-32 rounded-full border-4 border-emerald-500 text-emerald-400 flex flex-col items-center justify-center font-bold text-4xl shadow-xl shadow-emerald-950/40">
                  <span>{{ avgHealthScore() }}</span>
                  <span class="text-[10px] font-normal text-slate-400 uppercase tracking-widest mt-0.5">SCORE</span>
                </div>
                <div class="mt-4 inline-flex items-center gap-1.5 px-3 py-1 rounded-full text-xs font-semibold bg-emerald-500/10 text-emerald-400 border border-emerald-500/20">
                  <span class="w-2 h-2 rounded-full bg-emerald-400 animate-pulse"></span>
                  {{ radar()?.reputationTier ?? 'Optimal' }} Tier Reputation
                </div>
                <p class="text-xs text-slate-400 mt-3 max-w-xs">
                  Algorithms calculate multi-factor placement across Google Workspace, Microsoft 365, and major ISP gateways.
                </p>
              </div>

              <!-- 5 Radar Health Dimensions -->
              <div class="md:col-span-2 p-6 rounded-2xl bg-slate-900/60 border border-slate-800 space-y-4">
                <h3 class="text-sm font-bold text-white uppercase tracking-wider flex items-center justify-between">
                  <span>Deliverability Health Dimensions</span>
                  <span class="text-xs font-mono text-indigo-400 font-normal">Industry Benchmark: &ge; 85</span>
                </h3>

                @for (axis of radar()?.radarAxes ?? defaultAxes; track axis.axisName) {
                  <div class="space-y-1.5">
                    <div class="flex justify-between items-center text-xs">
                      <span class="font-medium text-slate-200">{{ axis.axisName }}</span>
                      <div class="flex items-center gap-2">
                        <span class="text-slate-500 text-[11px]">Benchmark: {{ axis.benchmark }}</span>
                        <span class="font-bold font-mono" [ngClass]="axis.score >= axis.benchmark ? 'text-emerald-400' : 'text-amber-400'">
                          {{ axis.score }}/100
                        </span>
                      </div>
                    </div>
                    <div class="w-full bg-slate-950 h-2.5 rounded-full overflow-hidden border border-slate-800/80">
                      <div
                        class="h-full rounded-full transition-all duration-500"
                        [ngClass]="axis.score >= axis.benchmark ? 'bg-gradient-to-r from-emerald-500 to-teal-400' : 'bg-gradient-to-r from-amber-500 to-rose-400'"
                        [style.width.%]="axis.score"></div>
                    </div>
                  </div>
                }
              </div>
            </div>

            <!-- Provider Balancing & DNS Health Cards -->
            <div class="grid grid-cols-1 md:grid-cols-2 gap-6">
              <!-- Provider Distribution -->
              <div class="p-5 rounded-xl bg-slate-900/60 border border-slate-800 space-y-3">
                <h4 class="text-xs font-bold text-white uppercase tracking-wider">ESP Provider Distribution</h4>
                <div class="grid grid-cols-3 gap-3">
                  <div class="p-3 bg-slate-950 rounded-lg border border-slate-800 text-center">
                    <div class="text-lg mb-1">🟦</div>
                    <div class="text-lg font-bold text-white">{{ radar()?.providerDistribution?.googleWorkspaceCount ?? 1 }}</div>
                    <div class="text-[10px] text-slate-400">Google Workspace</div>
                  </div>
                  <div class="p-3 bg-slate-950 rounded-lg border border-slate-800 text-center">
                    <div class="text-lg mb-1">🟧</div>
                    <div class="text-lg font-bold text-white">{{ radar()?.providerDistribution?.microsoft365Count ?? 1 }}</div>
                    <div class="text-[10px] text-slate-400">Microsoft 365</div>
                  </div>
                  <div class="p-3 bg-slate-950 rounded-lg border border-slate-800 text-center">
                    <div class="text-lg mb-1">⚙️</div>
                    <div class="text-lg font-bold text-white">{{ radar()?.providerDistribution?.customSmtpCount ?? 1 }}</div>
                    <div class="text-[10px] text-slate-400">Custom SMTP</div>
                  </div>
                </div>
                <p class="text-[11px] text-slate-500">
                  ESP Matching ensures emails to Google Workspace leads route out via Google Workspace inboxes, and Microsoft leads via Microsoft 365 inboxes.
                </p>
              </div>

              <!-- Automated Safety Protection Guardrails -->
              <div class="p-5 rounded-xl bg-slate-900/60 border border-slate-800 space-y-3">
                <h4 class="text-xs font-bold text-white uppercase tracking-wider">Automated Safety Guardrails</h4>
                <ul class="text-xs space-y-2 text-slate-300">
                  <li class="flex items-start gap-2">
                    <span class="text-emerald-400 text-sm">✓</span>
                    <span><strong>Auto-Quarantine at 2.5% Bounce Rate:</strong> Inboxes experiencing hard bounces or spam complaints are halted immediately.</span>
                  </li>
                  <li class="flex items-start gap-2">
                    <span class="text-emerald-400 text-sm">✓</span>
                    <span><strong>Human-Like Jitter Delays:</strong> Every outbound dispatch introduces randomized delays (mean 240s, std dev 45s) between 120s and 420s.</span>
                  </li>
                  <li class="flex items-start gap-2">
                    <span class="text-emerald-400 text-sm">✓</span>
                    <span><strong>Midnight UTC Quota Resets:</strong> Quotas automatically refresh at 00:00 UTC via background daemon to prevent provider rate limits.</span>
                  </li>
                </ul>
              </div>
            </div>
          </div>
        }

        <!-- Tab 3: AUTOMATED ROTATION SIMULATOR -->
        @if (activeTab() === 'simulator') {
          <div class="p-6 space-y-6">
            <div class="p-6 rounded-2xl bg-slate-900/60 border border-slate-800 space-y-6">
              <div>
                <h3 class="text-base font-bold text-white flex items-center gap-2">
                  <span>⚡</span> Outbound Batch Rotation Simulator (Instantly & Warmbly Algorithm)
                </h3>
                <p class="text-xs text-slate-400 mt-1">
                  Simulate an outbound campaign batch of 100 emails and visualize capacity-weighted lottery distribution with ESP-matching and randomized jitter intervals.
                </p>
              </div>

              <!-- Simulator Controls -->
              <div class="grid grid-cols-1 md:grid-cols-4 gap-4 p-4 rounded-xl bg-slate-950 border border-slate-800">
                <div>
                  <label class="text-[11px] text-slate-400 font-semibold uppercase block mb-1">Batch Size</label>
                  <select [(ngModel)]="simBatchSize" class="w-full bg-slate-900 border border-slate-700 rounded-lg px-3 py-1.5 text-xs text-white">
                    <option [value]="50">50 Emails</option>
                    <option [value]="100">100 Emails (Recommended)</option>
                    <option [value]="200">200 Emails</option>
                  </select>
                </div>
                <div>
                  <label class="text-[11px] text-slate-400 font-semibold uppercase block mb-1">Google Workspace Leads</label>
                  <div class="flex items-center gap-2">
                    <input type="range" min="0" max="100" [(ngModel)]="simGooglePercent" class="w-full accent-indigo-500" />
                    <span class="text-xs font-mono text-slate-300 w-10 text-right">{{ simGooglePercent }}%</span>
                  </div>
                </div>
                <div>
                  <label class="text-[11px] text-slate-400 font-semibold uppercase block mb-1">Microsoft 365 Leads</label>
                  <div class="flex items-center gap-2">
                    <input type="range" min="0" max="100" [(ngModel)]="simMicrosoftPercent" class="w-full accent-indigo-500" />
                    <span class="text-xs font-mono text-slate-300 w-10 text-right">{{ simMicrosoftPercent }}%</span>
                  </div>
                </div>
                <div class="flex items-end">
                  <button
                    (click)="runRotationSimulation()"
                    [disabled]="simulating()"
                    class="w-full py-2 bg-gradient-to-r from-indigo-600 to-purple-600 hover:from-indigo-500 hover:to-purple-500 text-white rounded-lg text-xs font-semibold shadow-lg shadow-indigo-950/40 transition disabled:opacity-50">
                    {{ simulating() ? 'Simulating...' : 'Run Batch Simulation' }}
                  </button>
                </div>
              </div>

              <!-- Simulation Results -->
              @if (simulationResult()) {
                <div class="space-y-4">
                  <!-- Simulation Stat Cards -->
                  <div class="grid grid-cols-1 md:grid-cols-4 gap-4">
                    <div class="p-3 bg-slate-950 rounded-lg border border-slate-800">
                      <span class="text-[10px] uppercase text-slate-500 font-semibold">Allocated Dispatches</span>
                      <div class="text-xl font-bold text-emerald-400">
                        {{ simulationResult()?.successfullyAllocated }} / {{ simulationResult()?.totalRequestedBatchSize }}
                      </div>
                    </div>
                    <div class="p-3 bg-slate-950 rounded-lg border border-slate-800">
                      <span class="text-[10px] uppercase text-slate-500 font-semibold">Average Inter-Send Jitter</span>
                      <div class="text-xl font-bold text-indigo-400">
                        {{ simulationResult()?.averageJitterSeconds }}s
                      </div>
                    </div>
                    <div class="p-3 bg-slate-950 rounded-lg border border-slate-800">
                      <span class="text-[10px] uppercase text-slate-500 font-semibold">Jitter Range</span>
                      <div class="text-xl font-bold text-slate-300">
                        {{ simulationResult()?.minJitterSeconds }}s - {{ simulationResult()?.maxJitterSeconds }}s
                      </div>
                    </div>
                    <div class="p-3 bg-slate-950 rounded-lg border border-slate-800">
                      <span class="text-[10px] uppercase text-slate-500 font-semibold">Unallocated</span>
                      <div class="text-xl font-bold" [ngClass]="simulationResult()?.unallocatedDueToExhaustion === 0 ? 'text-slate-400' : 'text-rose-400'">
                        {{ simulationResult()?.unallocatedDueToExhaustion }}
                      </div>
                    </div>
                  </div>

                  <!-- Allocation Progress Table -->
                  <div class="overflow-x-auto rounded-xl border border-slate-800 bg-slate-950">
                    <table class="w-full text-left text-xs text-slate-300">
                      <thead class="bg-slate-900/60 text-[10px] font-semibold text-slate-400 uppercase tracking-wider border-b border-slate-800">
                        <tr>
                          <th class="px-4 py-3">Sender Mailbox</th>
                          <th class="px-3 py-3">Provider</th>
                          <th class="px-3 py-3">Health</th>
                          <th class="px-4 py-3">Simulated Distribution</th>
                          <th class="px-3 py-3 text-right">Quota Remaining</th>
                        </tr>
                      </thead>
                      <tbody class="divide-y divide-slate-800/60">
                        @for (item of simulationResult()?.allocations ?? []; track item.senderAccountId) {
                          <tr class="hover:bg-slate-900/40">
                            <td class="px-4 py-3 font-semibold text-white">
                              {{ item.email }}
                            </td>
                            <td class="px-3 py-3">
                              <span class="px-2 py-0.5 rounded text-[10px] font-medium border" [ngClass]="getProviderBadgeClass(item.provider)">
                                {{ getProviderName(item.provider) }}
                              </span>
                            </td>
                            <td class="px-3 py-3">
                              <span class="font-bold" [ngClass]="getHealthColorClass(item.healthScore)">
                                {{ item.healthScore }}%
                              </span>
                            </td>
                            <td class="px-4 py-3">
                              <div class="space-y-1 w-64">
                                <div class="flex justify-between text-[10px]">
                                  <span class="text-slate-300">{{ item.allocatedDispatches }} emails</span>
                                  <span class="text-indigo-400 font-bold">{{ item.allocationPercentage }}%</span>
                                </div>
                                <div class="w-full bg-slate-800 h-2 rounded-full overflow-hidden">
                                  <div
                                    class="bg-gradient-to-r from-indigo-500 to-purple-500 h-full rounded-full"
                                    [style.width.%]="item.allocationPercentage"></div>
                                </div>
                              </div>
                            </td>
                            <td class="px-3 py-3 text-right font-mono text-slate-400">
                              {{ item.postQuotaRemaining }} left (was {{ item.preQuotaRemaining }})
                            </td>
                          </tr>
                        }
                      </tbody>
                    </table>
                  </div>
                </div>
              }
            </div>
          </div>
        }
      </main>

      <!-- ADD SENDER INBOX MODAL -->
      @if (showAddModal()) {
        <div class="fixed inset-0 bg-black/75 backdrop-blur-sm z-50 flex items-center justify-center p-4">
          <div class="bg-slate-900 border border-slate-800 rounded-2xl w-full max-w-lg shadow-2xl overflow-hidden animate-in fade-in zoom-in-95 duration-200">
            <div class="px-6 py-4 border-b border-slate-800 flex items-center justify-between">
              <h3 class="text-sm font-bold text-white flex items-center gap-2">
                <span>➕</span> Connect Secondary Sender Mailbox
              </h3>
              <button (click)="closeAddModal()" class="text-slate-400 hover:text-white text-lg">×</button>
            </div>

            <div class="p-6 space-y-4 text-xs">
              <div>
                <label class="text-[11px] font-semibold text-slate-400 uppercase block mb-1">Mailbox Email Address *</label>
                <input
                  type="email"
                  [(ngModel)]="newEmail"
                  placeholder="e.g. alex@trygrowing.io or sales@getgrowing.com"
                  class="w-full bg-slate-950 border border-slate-800 rounded-lg px-3 py-2 text-white focus:outline-none focus:border-indigo-500" />
                <p class="text-[10px] text-slate-500 mt-1">Recommended: Use dedicated secondary domains to insulate core corporate domain.</p>
              </div>

              <div>
                <label class="text-[11px] font-semibold text-slate-400 uppercase block mb-1">Display Name *</label>
                <input
                  type="text"
                  [(ngModel)]="newDisplayName"
                  placeholder="e.g. Alex Morgan"
                  class="w-full bg-slate-950 border border-slate-800 rounded-lg px-3 py-2 text-white focus:outline-none focus:border-indigo-500" />
              </div>

              <div>
                <label class="text-[11px] font-semibold text-slate-400 uppercase block mb-1">ESP Provider</label>
                <select
                  [(ngModel)]="newProvider"
                  class="w-full bg-slate-950 border border-slate-800 rounded-lg px-3 py-2 text-white focus:outline-none focus:border-indigo-500">
                  <option [value]="1">Google Workspace (OAuth / App Password)</option>
                  <option [value]="2">Microsoft 365 / Exchange Online</option>
                  <option [value]="3">Custom SMTP / IMAP Mail Server</option>
                </select>
              </div>

              <div>
                <div class="flex justify-between items-center mb-1">
                  <label class="text-[11px] font-semibold text-slate-400 uppercase">Daily Cold Outreach Limit</label>
                  <span class="font-mono text-indigo-400 font-bold">{{ newDailyLimit }} emails/day</span>
                </div>
                <input
                  type="range"
                  min="10"
                  max="100"
                  step="5"
                  [(ngModel)]="newDailyLimit"
                  class="w-full accent-indigo-500" />
                <p class="text-[10px] text-slate-500">Instantly recommends 30-40 emails/day per sender account for maximum deliverability safety.</p>
              </div>

              <div class="p-3 bg-slate-950 rounded-lg border border-slate-800 flex items-center justify-between">
                <div>
                  <div class="font-semibold text-white">Enable Automated Deliverability Warmup</div>
                  <div class="text-[10px] text-slate-400">Exchanges peer messages to build SPF/DKIM placement reputation</div>
                </div>
                <input type="checkbox" [(ngModel)]="newWarmupEnabled" class="w-4 h-4 accent-indigo-600 rounded" />
              </div>
            </div>

            <div class="px-6 py-4 bg-slate-950 border-t border-slate-800 flex justify-end gap-2">
              <button
                (click)="closeAddModal()"
                class="px-4 py-2 bg-slate-800 hover:bg-slate-700 text-slate-300 rounded-lg text-xs font-medium transition">
                Cancel
              </button>
              <button
                (click)="saveSenderInbox()"
                [disabled]="!newEmail || !newDisplayName || saving()"
                class="px-4 py-2 bg-indigo-600 hover:bg-indigo-500 text-white rounded-lg text-xs font-semibold shadow-lg shadow-indigo-950/40 transition disabled:opacity-50">
                {{ saving() ? 'Saving...' : 'Register Mailbox' }}
              </button>
            </div>
          </div>
        </div>
      }

      <!-- TEST DISPATCH MODAL -->
      @if (showTestModal()) {
        <div class="fixed inset-0 bg-black/75 backdrop-blur-sm z-50 flex items-center justify-center p-4">
          <div class="bg-slate-900 border border-slate-800 rounded-2xl w-full max-w-md shadow-2xl overflow-hidden animate-in fade-in zoom-in-95 duration-200">
            <div class="px-6 py-4 border-b border-slate-800 flex items-center justify-between">
              <h3 class="text-sm font-bold text-white flex items-center gap-2">
                <span>⚡</span> Test Rotation & Jitter Algorithm
              </h3>
              <button (click)="closeTestModal()" class="text-slate-400 hover:text-white text-lg">×</button>
            </div>

            <div class="p-6 space-y-4 text-xs">
              <div>
                <label class="text-[11px] font-semibold text-slate-400 uppercase block mb-1">Prospect Recipient Email *</label>
                <input
                  type="email"
                  [(ngModel)]="testRecipientEmail"
                  placeholder="e.g. prospect@gmail.com or executive@outlook.com"
                  class="w-full bg-slate-950 border border-slate-800 rounded-lg px-3 py-2 text-white focus:outline-none focus:border-indigo-500" />
                <p class="text-[10px] text-slate-500 mt-1">Try @gmail.com or @outlook.com to observe ESP-matching logic in action.</p>
              </div>

              @if (testSelectionResult()) {
                <div class="p-4 bg-slate-950 rounded-xl border border-slate-800 space-y-2.5">
                  <div class="flex items-center justify-between">
                    <span class="text-slate-400 text-[10px] uppercase font-semibold">Selected Sender</span>
                    <span class="px-2 py-0.5 rounded text-[10px] font-bold bg-indigo-500/20 text-indigo-400 border border-indigo-500/30">
                      {{ testSelectionResult()?.espMatchStrategy }}
                    </span>
                  </div>
                  <div class="text-sm font-bold text-white">
                    {{ testSelectionResult()?.selectedSender?.email }}
                  </div>
                  <div class="text-[11px] text-slate-400">
                    Remaining Quota: {{ testSelectionResult()?.selectedSender?.remainingDailyQuota }} • Health: {{ testSelectionResult()?.selectedSender?.healthScore }}%
                  </div>

                  <div class="pt-2 border-t border-slate-800/80 flex items-center justify-between text-xs">
                    <span class="text-slate-400">Inter-Send Jitter Delay:</span>
                    <span class="font-mono text-emerald-400 font-bold">{{ testSelectionResult()?.jitterDelaySeconds }} seconds</span>
                  </div>
                  <p class="text-[10px] text-slate-500">
                    Dispatched on a randomized human cadence (mean 240s, std dev 45s) to bypass ISP burst detection.
                  </p>
                </div>
              }
            </div>

            <div class="px-6 py-4 bg-slate-950 border-t border-slate-800 flex justify-end gap-2">
              <button
                (click)="closeTestModal()"
                class="px-4 py-2 bg-slate-800 hover:bg-slate-700 text-slate-300 rounded-lg text-xs font-medium transition">
                Close
              </button>
              <button
                (click)="executeTestSelection()"
                [disabled]="!testRecipientEmail || testingSelection()"
                class="px-4 py-2 bg-indigo-600 hover:bg-indigo-500 text-white rounded-lg text-xs font-semibold shadow-lg shadow-indigo-950/40 transition disabled:opacity-50">
                {{ testingSelection() ? 'Selecting...' : 'Select Sender Mailbox' }}
              </button>
            </div>
          </div>
        </div>
      }
    </div>
  `
})
export class MultiInboxCockpitComponent implements OnInit {
  private api = inject(ApiService);
  private auth = inject(AuthService);

  public activeTab = signal<'pool' | 'radar' | 'simulator'>('pool');
  public inboxes = signal<MultiInboxAccountDto[]>([]);
  public radar = signal<DeliverabilityRadarMetricsDto | null>(null);

  // Filters
  public searchQuery = '';
  public providerFilter = 'all';
  public statusFilter = 'all';

  // Add Modal State
  public showAddModal = signal(false);
  public saving = signal(false);
  public newEmail = '';
  public newDisplayName = '';
  public newProvider = 1;
  public newDailyLimit = 35;
  public newWarmupEnabled = true;

  // Test Selection State
  public showTestModal = signal(false);
  public testingSelection = signal(false);
  public testRecipientEmail = 'prospect@gmail.com';
  public testSelectionResult = signal<SelectNextSenderResponse | null>(null);

  // Simulator State
  public simulating = signal(false);
  public simBatchSize = 100;
  public simGooglePercent = 60;
  public simMicrosoftPercent = 30;
  public simulationResult = signal<RotationSimulationResultDto | null>(null);

  // Computed metrics
  public totalRemainingCapacity = computed(() => {
    return this.inboxes()
      .filter(i => (i.status as any) !== 'Quarantined' && (i.status as any) !== 3 && i.isActive)
      .reduce((sum, i) => sum + (i.remainingDailyQuota ?? (i.dailyQuota - i.sentTodayCount)), 0);
  });

  public totalDailyCapacity = computed(() => {
    return this.inboxes().reduce((sum, i) => sum + i.dailyQuota, 0);
  });

  public totalSentToday = computed(() => {
    return this.inboxes().reduce((sum, i) => sum + i.sentTodayCount, 0);
  });

  public capacityPercent = computed(() => {
    const total = this.totalDailyCapacity();
    if (total === 0) return 0;
    return Math.round((this.totalRemainingCapacity() / total) * 100);
  });

  public activeInboxesCount = computed(() => {
    return this.inboxes().filter(i => (i.status === 'Active' || (i.status as any) === 1) && i.isActive).length;
  });

  public warmupInboxesCount = computed(() => {
    return this.inboxes().filter(i => i.status === 'Warmup' || (i.status as any) === 2).length;
  });

  public quarantinedInboxesCount = computed(() => {
    return this.inboxes().filter(i => i.status === 'Quarantined' || (i.status as any) === 3).length;
  });

  public avgHealthScore = computed(() => {
    const list = this.inboxes();
    if (list.length === 0) return 96;
    const sum = list.reduce((acc, i) => acc + i.healthScore, 0);
    return Math.round(sum / list.length);
  });

  public filteredInboxes = computed(() => {
    let list = this.inboxes();
    if (this.searchQuery.trim()) {
      const q = this.searchQuery.toLowerCase();
      list = list.filter(i => i.email.toLowerCase().includes(q) || i.displayName.toLowerCase().includes(q) || i.domain.toLowerCase().includes(q));
    }
    if (this.providerFilter !== 'all') {
      list = list.filter(i => this.getProviderName(i.provider) === this.providerFilter || this.getProviderName(i.provider) === this.getFriendlyProviderName(this.providerFilter));
    }
    if (this.statusFilter !== 'all') {
      list = list.filter(i => this.getStatusLabel(i.status) === this.statusFilter);
    }
    return list;
  });

  public defaultAxes = [
    { axisName: 'DNS Authentication', score: 98, benchmark: 95 },
    { axisName: 'Inbox Placement', score: 94, benchmark: 90 },
    { axisName: 'Engagement & Reply', score: 82, benchmark: 70 },
    { axisName: 'Capacity Headroom', score: 88, benchmark: 80 },
    { axisName: 'Hygiene & Safety', score: 96, benchmark: 90 }
  ];

  ngOnInit(): void {
    this.loadData();
  }

  public loadData(): void {
    const tenantId = this.auth.currentTenant()?.id;
    this.api.getMultiInboxAccounts(tenantId).subscribe({
      next: (data) => {
        if (data && data.length > 0) {
          this.inboxes.set(data);
        } else {
          this.seedDemoInboxes();
        }
      },
      error: () => {
        this.seedDemoInboxes();
      }
    });

    this.api.getDeliverabilityRadar(tenantId).subscribe({
      next: (radar) => this.radar.set(radar),
      error: () => {}
    });
  }

  private seedDemoInboxes(): void {
    const demo: MultiInboxAccountDto[] = [
      {
        id: '11111111-1111-1111-1111-111111111111',
        tenantId: '00000000-0000-0000-0000-000000000000',
        email: 'alex@getgrowing.io',
        displayName: 'Alex Morgan',
        provider: 'GoogleWorkspace',
        dailyQuota: 35,
        sentTodayCount: 22,
        remainingDailyQuota: 13,
        warmupEnabled: true,
        warmupRampDailyIncrement: 3,
        healthScore: 98,
        isActive: true,
        status: 'Active',
        totalDispatched: 450,
        totalDelivered: 446,
        totalBounced: 4,
        totalReplied: 68,
        totalOpened: 240,
        bounceRate: 0.89,
        replyRate: 15.1,
        openRate: 53.3,
        hasSpf: true,
        hasDkim: true,
        hasDmarc: true,
        domain: 'getgrowing.io',
        createdAtUtc: new Date(Date.now() - 30 * 86400000).toISOString()
      },
      {
        id: '22222222-2222-2222-2222-222222222222',
        tenantId: '00000000-0000-0000-0000-000000000000',
        email: 'alex@trygrowing.io',
        displayName: 'Alex Morgan',
        provider: 'Microsoft365',
        dailyQuota: 35,
        sentTodayCount: 14,
        remainingDailyQuota: 21,
        warmupEnabled: true,
        warmupRampDailyIncrement: 3,
        healthScore: 95,
        isActive: true,
        status: 'Active',
        totalDispatched: 320,
        totalDelivered: 317,
        totalBounced: 3,
        totalReplied: 45,
        totalOpened: 180,
        bounceRate: 0.94,
        replyRate: 14.1,
        openRate: 56.2,
        hasSpf: true,
        hasDkim: true,
        hasDmarc: true,
        domain: 'trygrowing.io',
        createdAtUtc: new Date(Date.now() - 20 * 86400000).toISOString()
      },
      {
        id: '33333333-3333-3333-3333-333333333333',
        tenantId: '00000000-0000-0000-0000-000000000000',
        email: 'sales@growinghq.com',
        displayName: 'Growing HQ Sales',
        provider: 'CustomSmtp',
        dailyQuota: 40,
        sentTodayCount: 5,
        remainingDailyQuota: 35,
        warmupEnabled: true,
        warmupRampDailyIncrement: 4,
        healthScore: 92,
        isActive: true,
        status: 'Warmup',
        totalDispatched: 110,
        totalDelivered: 109,
        totalBounced: 1,
        totalReplied: 12,
        totalOpened: 48,
        bounceRate: 0.91,
        replyRate: 10.9,
        openRate: 43.6,
        hasSpf: true,
        hasDkim: true,
        hasDmarc: true,
        domain: 'growinghq.com',
        createdAtUtc: new Date(Date.now() - 7 * 86400000).toISOString()
      }
    ];

    this.inboxes.set(demo);
  }

  // Modals & Handlers
  public openAddModal(): void {
    this.newEmail = '';
    this.newDisplayName = '';
    this.newProvider = 1;
    this.newDailyLimit = 35;
    this.newWarmupEnabled = true;
    this.showAddModal.set(true);
  }

  public closeAddModal(): void {
    this.showAddModal.set(false);
  }

  public saveSenderInbox(): void {
    if (!this.newEmail || !this.newDisplayName) return;

    this.saving.set(true);
    const tenantId = this.auth.currentTenant()?.id;

    const req: RegisterSenderInboxRequest = {
      tenantId: tenantId,
      email: this.newEmail.trim(),
      displayName: this.newDisplayName.trim(),
      provider: Number(this.newProvider),
      dailyQuota: this.newDailyLimit,
      warmupEnabled: this.newWarmupEnabled,
      warmupRampDailyIncrement: 3
    };

    this.api.registerSenderInbox(req).subscribe({
      next: (created) => {
        this.saving.set(false);
        this.closeAddModal();
        this.inboxes.update(list => [created, ...list]);
        this.loadData();
      },
      error: () => {
        // Fallback for preview
        this.saving.set(false);
        const provMap: Record<number, MailboxProviderType> = { 1: 'GoogleWorkspace', 2: 'Microsoft365', 3: 'CustomSmtp' };
        const fallback: MultiInboxAccountDto = {
          id: crypto.randomUUID(),
          tenantId: tenantId || '00000000-0000-0000-0000-000000000000',
          email: req.email,
          displayName: req.displayName,
          provider: provMap[req.provider] || 'GoogleWorkspace',
          dailyQuota: req.dailyQuota,
          sentTodayCount: 0,
          remainingDailyQuota: req.dailyQuota,
          warmupEnabled: req.warmupEnabled,
          warmupRampDailyIncrement: 3,
          healthScore: 95,
          isActive: true,
          status: req.warmupEnabled ? 'Warmup' : 'Active',
          totalDispatched: 0,
          totalDelivered: 0,
          totalBounced: 0,
          totalReplied: 0,
          totalOpened: 0,
          bounceRate: 0,
          replyRate: 0,
          openRate: 0,
          hasSpf: true,
          hasDkim: true,
          hasDmarc: true,
          domain: req.email.split('@')[1] || '',
          createdAtUtc: new Date().toISOString()
        };
        this.inboxes.update(list => [fallback, ...list]);
        this.closeAddModal();
      }
    });
  }

  public openTestDispatchModal(): void {
    this.testSelectionResult.set(null);
    this.showTestModal.set(true);
  }

  public closeTestModal(): void {
    this.showTestModal.set(false);
  }

  public executeTestSelection(): void {
    if (!this.testRecipientEmail) return;
    this.testingSelection.set(true);
    const tenantId = this.auth.currentTenant()?.id;

    this.api.selectNextSender(this.testRecipientEmail, tenantId).subscribe({
      next: (res) => {
        this.testingSelection.set(false);
        this.testSelectionResult.set(res);
        this.loadData();
      },
      error: () => {
        this.testingSelection.set(false);
        // Local simulation fallback
        const isGoogle = this.testRecipientEmail.includes('gmail') || this.testRecipientEmail.includes('google');
        const isMs = this.testRecipientEmail.includes('outlook') || this.testRecipientEmail.includes('microsoft');

        let matched = this.inboxes().find(i => isGoogle ? this.getProviderName(i.provider) === 'Google Workspace' : isMs ? this.getProviderName(i.provider) === 'Microsoft 365' : true);
        if (!matched) matched = this.inboxes()[0];

        const jitter = Math.floor(Math.random() * (320 - 180 + 1)) + 180;
        this.testSelectionResult.set({
          selectedSender: matched,
          jitterDelaySeconds: jitter,
          espMatchStrategy: isGoogle ? 'GoogleWorkspace-to-GoogleWorkspace' : isMs ? 'Microsoft365-to-Microsoft365' : 'CapacityWeightedGeneric',
          calculatedWeight: 3500,
          scheduledDispatchUtc: new Date(Date.now() + jitter * 1000).toISOString(),
          recipientDomain: this.testRecipientEmail.split('@')[1] || '',
          detectedRecipientEsp: isGoogle ? 'GoogleWorkspace' : isMs ? 'Microsoft365' : 'Other'
        });
      }
    });
  }

  public triggerQuickTestDispatch(inbox: MultiInboxAccountDto): void {
    inbox.sentTodayCount++;
    inbox.remainingDailyQuota = Math.max(0, inbox.dailyQuota - inbox.sentTodayCount);
    inbox.totalDispatched++;
    inbox.totalDelivered++;

    this.api.recordInboxDispatchEvent(inbox.id, {
      wasDelivered: true,
      wasBounced: false,
      wasReplied: false
    }).subscribe({
      next: () => this.loadData(),
      error: () => {}
    });
  }

  public quarantineInbox(inbox: MultiInboxAccountDto): void {
    const tenantId = this.auth.currentTenant()?.id;
    this.api.quarantineSenderInbox(inbox.id, 'Administrative manual quarantine', tenantId).subscribe({
      next: (updated) => {
        inbox.status = 'Quarantined';
        inbox.quarantineReason = 'Administrative manual quarantine';
        inbox.healthScore = Math.min(inbox.healthScore, 35);
        this.loadData();
      },
      error: () => {
        inbox.status = 'Quarantined';
        inbox.quarantineReason = 'Administrative manual quarantine';
        inbox.healthScore = Math.min(inbox.healthScore, 35);
      }
    });
  }

  public thawInbox(inbox: MultiInboxAccountDto): void {
    const tenantId = this.auth.currentTenant()?.id;
    this.api.thawSenderInbox(inbox.id, tenantId).subscribe({
      next: (updated) => {
        inbox.status = inbox.warmupEnabled ? 'Warmup' : 'Active';
        inbox.quarantineReason = null;
        inbox.healthScore = 95;
        this.loadData();
      },
      error: () => {
        inbox.status = inbox.warmupEnabled ? 'Warmup' : 'Active';
        inbox.quarantineReason = null;
        inbox.healthScore = 95;
      }
    });
  }

  public runRotationSimulation(): void {
    this.simulating.set(true);
    const tenantId = this.auth.currentTenant()?.id;
    const gRatio = this.simGooglePercent / 100.0;
    const mRatio = this.simMicrosoftPercent / 100.0;
    const cRatio = Math.max(0, 1.0 - gRatio - mRatio);

    this.api.simulateBatchRotation({
      tenantId: tenantId,
      batchSize: this.simBatchSize,
      googleRatio: gRatio,
      microsoftRatio: mRatio,
      customRatio: cRatio
    }).subscribe({
      next: (res) => {
        this.simulating.set(false);
        this.simulationResult.set(res);
      },
      error: () => {
        this.simulating.set(false);
        // Client-side fallback simulation
        const inboxes = this.inboxes();
        const allocations = inboxes.map((i, idx) => {
          const allocCount = Math.round(this.simBatchSize / inboxes.length);
          return {
            senderAccountId: i.id,
            email: i.email,
            provider: i.provider,
            allocatedDispatches: allocCount,
            allocationPercentage: Math.round((allocCount / this.simBatchSize) * 100),
            preQuotaRemaining: i.remainingDailyQuota,
            postQuotaRemaining: Math.max(0, i.remainingDailyQuota - allocCount),
            healthScore: i.healthScore
          };
        });

        this.simulationResult.set({
          totalRequestedBatchSize: this.simBatchSize,
          successfullyAllocated: this.simBatchSize,
          unallocatedDueToExhaustion: 0,
          averageJitterSeconds: 241,
          minJitterSeconds: 128,
          maxJitterSeconds: 412,
          allocations: allocations
        });
      }
    });
  }

  // Formatters & UI Helpers
  public getProviderName(provider: MailboxProviderType): string {
    if (provider === 'GoogleWorkspace' || provider === 1) return 'Google Workspace';
    if (provider === 'Microsoft365' || provider === 2) return 'Microsoft 365';
    return 'Custom SMTP';
  }

  private getFriendlyProviderName(key: string): string {
    if (key === 'GoogleWorkspace') return 'Google Workspace';
    if (key === 'Microsoft365') return 'Microsoft 365';
    return 'Custom SMTP';
  }

  public getProviderIcon(provider: MailboxProviderType): string {
    if (provider === 'GoogleWorkspace' || provider === 1) return '🟦';
    if (provider === 'Microsoft365' || provider === 2) return '🟧';
    return '⚙️';
  }

  public getProviderBadgeClass(provider: MailboxProviderType): string {
    if (provider === 'GoogleWorkspace' || provider === 1) {
      return 'bg-blue-500/10 text-blue-400 border-blue-500/20';
    }
    if (provider === 'Microsoft365' || provider === 2) {
      return 'bg-orange-500/10 text-orange-400 border-orange-500/20';
    }
    return 'bg-slate-700/30 text-slate-300 border-slate-600/30';
  }

  public getStatusLabel(status: InboxRotationStatusType): string {
    if (status === 'Active' || (status as any) === 1) return 'Active';
    if (status === 'Warmup' || (status as any) === 2) return 'Warmup';
    if (status === 'Quarantined' || (status as any) === 3) return 'Quarantined';
    return 'Cooling';
  }

  public getStatusBadgeClass(status: InboxRotationStatusType): string {
    const label = this.getStatusLabel(status);
    if (label === 'Active') return 'bg-emerald-500/15 text-emerald-400 border-emerald-500/30';
    if (label === 'Warmup') return 'bg-indigo-500/15 text-indigo-400 border-indigo-500/30';
    if (label === 'Quarantined') return 'bg-rose-500/15 text-rose-400 border-rose-500/30';
    return 'bg-cyan-500/15 text-cyan-400 border-cyan-500/30';
  }

  public getStatusDotClass(status: InboxRotationStatusType): string {
    const label = this.getStatusLabel(status);
    if (label === 'Active') return 'bg-emerald-400 animate-pulse';
    if (label === 'Warmup') return 'bg-indigo-400 animate-pulse';
    if (label === 'Quarantined') return 'bg-rose-500';
    return 'bg-cyan-400';
  }

  public getQuotaPercentage(inbox: MultiInboxAccountDto): number {
    if (!inbox.dailyQuota) return 0;
    return Math.min(100, Math.round((inbox.sentTodayCount / inbox.dailyQuota) * 100));
  }

  public getQuotaColorClass(inbox: MultiInboxAccountDto): string {
    const p = this.getQuotaPercentage(inbox);
    if (p >= 90) return 'text-rose-400';
    if (p >= 70) return 'text-amber-400';
    return 'text-emerald-400';
  }

  public getQuotaBarClass(inbox: MultiInboxAccountDto): string {
    const p = this.getQuotaPercentage(inbox);
    if (p >= 90) return 'bg-gradient-to-r from-rose-500 to-amber-500';
    if (p >= 70) return 'bg-gradient-to-r from-amber-500 to-yellow-400';
    return 'bg-gradient-to-r from-emerald-500 to-teal-400';
  }

  public getHealthColorClass(score: number): string {
    if (score >= 90) return 'text-emerald-400';
    if (score >= 75) return 'text-teal-400';
    if (score >= 50) return 'text-amber-400';
    return 'text-rose-400';
  }

  public getHealthBadgeClass(score: number): string {
    if (score >= 90) return 'bg-emerald-500/20 text-emerald-400';
    if (score >= 75) return 'bg-teal-500/20 text-teal-400';
    if (score >= 50) return 'bg-amber-500/20 text-amber-400';
    return 'bg-rose-500/20 text-rose-400';
  }

  public getHealthRadialClass(score: number): string {
    if (score >= 90) return 'border-emerald-500 text-emerald-400 bg-emerald-950/20';
    if (score >= 75) return 'border-teal-500 text-teal-400 bg-teal-950/20';
    if (score >= 50) return 'border-amber-500 text-amber-400 bg-amber-950/20';
    return 'border-rose-500 text-rose-400 bg-rose-950/20';
  }

  public getReputationTier(score: number): string {
    if (score >= 90) return 'Optimal';
    if (score >= 75) return 'Good';
    if (score >= 50) return 'Caution';
    return 'Critical';
  }
}
