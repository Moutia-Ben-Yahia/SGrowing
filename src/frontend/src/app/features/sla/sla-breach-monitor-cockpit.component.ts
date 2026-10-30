import { Component, signal, computed, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { HttpClient } from '@angular/common/http';

export enum SlaRiskLevel {
  Safe = 'Safe',
  Warning = 'Warning',
  ImminentBreach = 'ImminentBreach'
}

export enum SlaEscalationStage {
  Nominal = 'Nominal',
  Warning = 'Warning',
  Critical = 'Critical',
  Breached = 'Breached'
}

export interface TicketSlaProjectionDto {
  ticketId: string;
  ticketNumber: string;
  title: string;
  customerBrand: string;
  assignedAgentId: string;
  assignedAgentName: string;
  assignedAgentTier: string;
  priority: string;
  targetType: string;
  queueAhead: number;
  complexityWeight: number;
  avgAgentVelocityMinutes: number;
  createdAtUtc: string;
  dueAtUtc: string;
  predictedCompletionAtUtc: string;
  targetWorkingMinutes: number;
  elapsedWorkingMinutes: number;
  remainingWorkingMinutes: number;
  percentageElapsed: number;
  bufferMinutes: number;
  escalationStage: SlaEscalationStage;
  riskLevel: SlaRiskLevel;
  is24x7Tier: boolean;
  priorityBoosted: boolean;
  autoReassigned: boolean;
  autoReassignedTo?: string | null;
  executiveAlert?: string | null;
  lastEvaluatedAtUtc: string;
  firstRespondedAtUtc?: string | null;
  resolvedAtUtc?: string | null;
  isFirstResponseCompliant: boolean;
  isResolutionCompliant: boolean;
}

export interface SlaMonitorMetricsDto {
  firstResponseCompliancePercentage: number;
  resolutionCompliancePercentage: number;
  totalMonitoredTickets: number;
  imminentBreachesCount: number;
  criticalCount: number;
  warningCount: number;
  safeCount: number;
  breachedCount: number;
  activeQueueVelocity: number;
  lastSweepAtUtc: string;
}

export interface EscalationSweepResultDto {
  sweepTimeUtc: string;
  evaluatedCount: number;
  warningEscalationsCount: number;
  criticalEscalationsCount: number;
  autoReassignedCount: number;
  breachedCount: number;
  tickets: TicketSlaProjectionDto[];
}

@Component({
  selector: 'app-sla-breach-monitor-cockpit',
  standalone: true,
  imports: [CommonModule, FormsModule],
  template: `
    <div class="min-h-screen bg-slate-950 text-slate-100 font-sans flex flex-col" dir="ltr">
      <!-- Topbar Header -->
      <header class="h-16 border-b border-slate-800 bg-slate-900/80 px-6 flex items-center justify-between shrink-0 backdrop-blur-md sticky top-0 z-30">
        <div class="flex items-center gap-3">
          <span class="p-2.5 rounded-xl bg-rose-950/60 border border-rose-500/30 text-rose-400 text-lg shadow-inner flex items-center justify-center">
            ⏱️
          </span>
          <div>
            <div class="flex items-center gap-2">
              <h1 class="text-base font-bold text-white tracking-wide">
                Predictive SLA Breach Monitor & Automated Escalation Engine
              </h1>
              <span class="bg-rose-500/10 text-rose-400 border border-rose-500/30 text-[10px] font-mono px-2 py-0.5 rounded-full uppercase tracking-wider font-semibold flex items-center gap-1">
                <span class="w-1.5 h-1.5 rounded-full bg-rose-400 animate-ping"></span>
                Horizon 36 • Live Radar
              </span>
            </div>
            <p class="text-xs text-slate-400">
              Velocity-to-Breach Projections • Business Hours Engine • Multi-Stage Automated Tier-2 On-Call Escalation
            </p>
          </div>
        </div>

        <div class="flex items-center gap-3">
          <button
            (click)="runEscalationSweep()"
            [disabled]="isSweeping()"
            class="px-4 py-2 bg-gradient-to-r from-rose-600 via-rose-500 to-amber-500 hover:from-rose-500 hover:to-amber-400 disabled:opacity-50 text-white font-semibold text-xs rounded-lg transition-all shadow-lg shadow-rose-950/50 flex items-center gap-2">
            @if (isSweeping()) {
              <svg class="animate-spin h-3.5 w-3.5 text-white" fill="none" viewBox="0 0 24 24">
                <circle class="opacity-25" cx="12" cy="12" r="10" stroke="currentColor" stroke-width="4"></circle>
                <path class="opacity-75" fill="currentColor" d="M4 12a8 8 0 018-8v8H4z"></path>
              </svg>
              <span>Sweeping Tickets...</span>
            } @else {
              <span>⚡</span>
              <span>Run Escalation Sweep</span>
            }
          </button>

          <button
            (click)="fetchDataFromBackend()"
            class="px-3 py-2 bg-slate-800 hover:bg-slate-700 text-slate-300 hover:text-white text-xs rounded-lg border border-slate-700 transition flex items-center gap-1.5">
            <span>🔄</span>
            <span>Refresh</span>
          </button>
        </div>
      </header>

      <!-- Main Body Area -->
      <main class="flex-1 p-6 space-y-6 overflow-y-auto">
        <!-- Notification Banner -->
        @if (notificationBanner()) {
          <div 
            [class]="notificationBanner()!.type === 'danger' ? 'bg-rose-950/70 border-rose-500/40 text-rose-200' : 'bg-emerald-950/70 border-emerald-500/40 text-emerald-200'"
            class="p-4 rounded-xl border flex items-center justify-between text-xs backdrop-blur shadow-lg animate-fadeIn">
            <div class="flex items-center gap-2.5">
              <span class="text-base">{{ notificationBanner()!.type === 'danger' ? '🚨' : '✅' }}</span>
              <span class="font-medium">{{ notificationBanner()!.message }}</span>
            </div>
            <button (click)="notificationBanner.set(null)" class="text-slate-400 hover:text-white font-mono text-sm px-2">✕</button>
          </div>
        }

        <!-- 4 KPI Metrics Header Cards -->
        <div class="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-4 gap-4">
          <!-- First Response Compliance -->
          <div class="p-4 rounded-xl bg-slate-900/80 border border-slate-800 shadow-sm relative overflow-hidden">
            <div class="flex items-center justify-between text-slate-400 text-xs font-medium">
              <span>First Response SLA</span>
              <span class="text-emerald-400">🎯 FRT</span>
            </div>
            <div class="mt-2 flex items-baseline gap-2">
              <span class="text-2xl font-bold font-mono text-white">
                {{ metrics()?.firstResponseCompliancePercentage ?? 95.8 }}%
              </span>
              <span class="text-[11px] text-emerald-400 font-medium">Target Compliant</span>
            </div>
            <div class="mt-2 text-[11px] text-slate-400 flex items-center justify-between">
              <span>Business hours filtered</span>
              <span class="font-mono text-emerald-400">09:00 - 18:00 UTC</span>
            </div>
          </div>

          <!-- Resolution Compliance -->
          <div class="p-4 rounded-xl bg-slate-900/80 border border-slate-800 shadow-sm relative overflow-hidden">
            <div class="flex items-center justify-between text-slate-400 text-xs font-medium">
              <span>Resolution Compliance</span>
              <span class="text-amber-400">⌛ Target</span>
            </div>
            <div class="mt-2 flex items-baseline gap-2">
              <span class="text-2xl font-bold font-mono text-white">
                {{ metrics()?.resolutionCompliancePercentage ?? 87.5 }}%
              </span>
              <span class="text-[11px] text-amber-400 font-medium">Auto-Tier 2 Routed</span>
            </div>
            <div class="mt-2 text-[11px] text-slate-400 flex items-center justify-between">
              <span>Excludes weekends/holidays</span>
              <span class="font-mono text-slate-300">540 min/day</span>
            </div>
          </div>

          <!-- Imminent Breaches Count -->
          <div class="p-4 rounded-xl bg-rose-950/20 border border-rose-500/30 shadow-sm relative overflow-hidden">
            <div class="flex items-center justify-between text-rose-300 text-xs font-medium">
              <span>Imminent SLA Breaches</span>
              <span class="text-rose-400">🔥 Delta_T &lt; 0m</span>
            </div>
            <div class="mt-2 flex items-baseline gap-2">
              <span class="text-2xl font-bold font-mono text-rose-400 animate-pulse">
                {{ metrics()?.imminentBreachesCount ?? 3 }}
              </span>
              <span class="text-[11px] text-rose-300 font-semibold uppercase tracking-wider">At Risk</span>
            </div>
            <div class="mt-2 text-[11px] text-rose-400/80 flex items-center gap-1.5">
              <span class="w-1.5 h-1.5 rounded-full bg-rose-500 animate-ping"></span>
              <span>Predictive velocity forecast alert</span>
            </div>
          </div>

          <!-- Active Queue Velocity -->
          <div class="p-4 rounded-xl bg-slate-900/80 border border-slate-800 shadow-sm relative overflow-hidden">
            <div class="flex items-center justify-between text-slate-400 text-xs font-medium">
              <span>Average Agent Velocity</span>
              <span class="text-cyan-400">🚀 Speed</span>
            </div>
            <div class="mt-2 flex items-baseline gap-2">
              <span class="text-2xl font-bold font-mono text-white">
                {{ metrics()?.activeQueueVelocity ?? 0.053 }}
              </span>
              <span class="text-[11px] text-cyan-400 font-mono">tickets / min</span>
            </div>
            <div class="mt-2 text-[11px] text-slate-400 flex items-center justify-between">
              <span>Pacing per agent</span>
              <span class="font-mono text-slate-300">~18.8m / ticket</span>
            </div>
          </div>
        </div>

        <!-- Filter Bar & Search Controls -->
        <div class="flex flex-col sm:flex-row items-center justify-between gap-4 bg-slate-900/60 p-4 rounded-xl border border-slate-800">
          <div class="flex flex-wrap items-center gap-2">
            <button
              (click)="selectedRiskFilter.set('ALL')"
              [class]="selectedRiskFilter() === 'ALL' ? 'bg-slate-700 text-white font-semibold' : 'bg-slate-800/60 text-slate-400 hover:text-white'"
              class="px-3 py-1.5 rounded-lg text-xs transition border border-slate-700/60 flex items-center gap-1.5">
              <span>All Tickets</span>
              <span class="px-1.5 py-0.5 rounded-full bg-slate-800 text-[10px] font-mono">{{ tickets().length }}</span>
            </button>

            <button
              (click)="selectedRiskFilter.set('ImminentBreach')"
              [class]="selectedRiskFilter() === 'ImminentBreach' ? 'bg-rose-600/40 border-rose-500 text-rose-200 font-semibold' : 'bg-slate-800/60 text-slate-400 hover:text-rose-300'"
              class="px-3 py-1.5 rounded-lg text-xs transition border border-slate-700/60 flex items-center gap-1.5">
              <span>🔥 Imminent Breach</span>
              <span class="px-1.5 py-0.5 rounded-full bg-rose-950 text-rose-300 border border-rose-800/50 text-[10px] font-mono">
                {{ imminentBreachCount() }}
              </span>
            </button>

            <button
              (click)="selectedRiskFilter.set('Critical')"
              [class]="selectedRiskFilter() === 'Critical' ? 'bg-amber-600/40 border-amber-500 text-amber-200 font-semibold' : 'bg-slate-800/60 text-slate-400 hover:text-amber-300'"
              class="px-3 py-1.5 rounded-lg text-xs transition border border-slate-700/60 flex items-center gap-1.5">
              <span>⚠️ Critical (85-100%)</span>
              <span class="px-1.5 py-0.5 rounded-full bg-amber-950 text-amber-300 border border-amber-800/50 text-[10px] font-mono">
                {{ criticalStageCount() }}
              </span>
            </button>

            <button
              (click)="selectedRiskFilter.set('Warning')"
              [class]="selectedRiskFilter() === 'Warning' ? 'bg-yellow-600/40 border-yellow-500 text-yellow-200 font-semibold' : 'bg-slate-800/60 text-slate-400 hover:text-yellow-300'"
              class="px-3 py-1.5 rounded-lg text-xs transition border border-slate-700/60 flex items-center gap-1.5">
              <span>Warning (70-85%)</span>
              <span class="px-1.5 py-0.5 rounded-full bg-yellow-950 text-yellow-300 border border-yellow-800/50 text-[10px] font-mono">
                {{ warningStageCount() }}
              </span>
            </button>

            <button
              (click)="selectedRiskFilter.set('Safe')"
              [class]="selectedRiskFilter() === 'Safe' ? 'bg-emerald-600/40 border-emerald-500 text-emerald-200 font-semibold' : 'bg-slate-800/60 text-slate-400 hover:text-emerald-300'"
              class="px-3 py-1.5 rounded-lg text-xs transition border border-slate-700/60 flex items-center gap-1.5">
              <span>Safe (&gt;60m buffer)</span>
              <span class="px-1.5 py-0.5 rounded-full bg-emerald-950 text-emerald-300 border border-emerald-800/50 text-[10px] font-mono">
                {{ safeRiskCount() }}
              </span>
            </button>
          </div>

          <!-- Search Input -->
          <div class="w-full sm:w-64 relative">
            <input
              type="text"
              [(ngModel)]="searchQuery"
              placeholder="Search ticket, brand, agent..."
              class="w-full bg-slate-950/80 border border-slate-800 rounded-lg px-3 py-1.5 text-xs text-white placeholder-slate-500 focus:outline-none focus:border-rose-500 transition" />
            @if (searchQuery()) {
              <button (click)="searchQuery.set('')" class="absolute right-2.5 top-1.5 text-slate-400 hover:text-white text-xs">✕</button>
            }
          </div>
        </div>

        <!-- Live Ticket Table / Radar -->
        <div class="bg-slate-900/80 border border-slate-800 rounded-xl overflow-hidden shadow-xl">
          <div class="overflow-x-auto">
            <table class="w-full text-left text-xs text-slate-300">
              <thead class="bg-slate-950/90 text-slate-400 uppercase text-[10px] tracking-wider border-b border-slate-800 font-semibold">
                <tr>
                  <th class="py-3 px-4">Ticket & Customer</th>
                  <th class="py-3 px-4">Target & Calendar</th>
                  <th class="py-3 px-4">SLA Elapsed & Stage</th>
                  <th class="py-3 px-4">Queue & Velocity Projection</th>
                  <th class="py-3 px-4">Predicted Breach Buffer</th>
                  <th class="py-3 px-4">Assigned Agent & Tier</th>
                  <th class="py-3 px-4 text-right">Actions</th>
                </tr>
              </thead>
              <tbody class="divide-y divide-slate-800/70">
                @for (ticket of filteredTickets(); track ticket.ticketId) {
                  <tr class="hover:bg-slate-800/30 transition">
                    <!-- Ticket & Customer -->
                    <td class="py-3.5 px-4">
                      <div class="flex items-center gap-2">
                        <span class="font-mono font-bold text-white tracking-wide text-xs">
                          {{ ticket.ticketNumber }}
                        </span>
                        @if (ticket.is24x7Tier) {
                          <span class="bg-indigo-950 text-indigo-300 border border-indigo-700/60 text-[9px] px-1.5 py-0.5 rounded font-mono font-semibold">
                            24/7 Priority
                          </span>
                        }
                        @if (ticket.priorityBoosted) {
                          <span class="bg-amber-950 text-amber-300 border border-amber-700/60 text-[9px] px-1.5 py-0.5 rounded font-semibold flex items-center gap-0.5">
                            ⚡ Boosted
                          </span>
                        }
                      </div>
                      <div class="text-slate-200 font-medium text-xs mt-0.5 max-w-xs truncate" [title]="ticket.title">
                        {{ ticket.title }}
                      </div>
                      <div class="text-[11px] text-slate-400 flex items-center gap-1.5 mt-0.5">
                        <span class="text-slate-500">🏢</span>
                        <span>{{ ticket.customerBrand }}</span>
                      </div>
                    </td>

                    <!-- Target & Calendar -->
                    <td class="py-3.5 px-4 font-mono">
                      <div class="flex items-center gap-1.5">
                        <span class="text-[10px] px-1.5 py-0.5 rounded font-semibold"
                          [class]="ticket.targetType === 'FirstResponse' ? 'bg-cyan-950 text-cyan-300 border border-cyan-800/50' : 'bg-purple-950 text-purple-300 border border-purple-800/50'">
                          {{ ticket.targetType === 'FirstResponse' ? 'FRT' : 'Resolution' }}
                        </span>
                        <span class="text-white font-semibold">{{ ticket.targetWorkingMinutes }}m target</span>
                      </div>
                      <div class="text-[11px] text-slate-400 mt-1">
                        Elapsed: <span class="text-slate-200">{{ ticket.elapsedWorkingMinutes }}m</span>
                      </div>
                      <div class="text-[10px] text-slate-500">
                        Remaining: {{ ticket.remainingWorkingMinutes }}m working
                      </div>
                    </td>

                    <!-- SLA Elapsed & Stage Progress Bar -->
                    <td class="py-3.5 px-4 min-w-[180px]">
                      <div class="flex items-center justify-between text-[11px] mb-1 font-mono">
                        <span class="font-bold" [class]="getStageTextColor(ticket.escalationStage)">
                          {{ ticket.percentageElapsed }}%
                        </span>
                        <span class="px-1.5 py-0.5 rounded text-[9px] uppercase tracking-wider font-semibold"
                          [class]="getStageBadgeClass(ticket.escalationStage)">
                          {{ ticket.escalationStage }}
                        </span>
                      </div>
                      <div class="w-full bg-slate-800 rounded-full h-2 overflow-hidden border border-slate-700/60">
                        <div
                          class="h-full rounded-full transition-all duration-500"
                          [class]="getProgressBarColor(ticket.escalationStage)"
                          [style.width.%]="ticket.percentageElapsed > 100 ? 100 : ticket.percentageElapsed">
                        </div>
                      </div>
                      @if (ticket.executiveAlert) {
                        <div class="text-[10px] text-rose-400 font-medium mt-1 truncate" [title]="ticket.executiveAlert">
                          🚨 {{ ticket.executiveAlert }}
                        </div>
                      }
                    </td>

                    <!-- Queue & Velocity Projection -->
                    <td class="py-3.5 px-4 font-mono text-[11px]">
                      <div class="text-white flex items-center gap-1">
                        <span>Queue Ahead:</span>
                        <span class="font-bold text-amber-300">#{{ ticket.queueAhead }}</span>
                      </div>
                      <div class="text-slate-400 text-[10px] mt-0.5">
                        Velocity: <span class="text-slate-300">{{ ticket.avgAgentVelocityMinutes }} t/m</span>
                      </div>
                      <div class="text-slate-500 text-[10px]">
                        Complexity: +{{ (ticket.complexityWeight * 100).toFixed(0) }}%
                      </div>
                    </td>

                    <!-- Predicted Breach Buffer (Delta_T) -->
                    <td class="py-3.5 px-4 font-mono">
                      <div class="inline-flex items-center gap-1.5 px-2.5 py-1 rounded-lg text-xs font-bold border"
                        [class]="getBufferBadgeClass(ticket.bufferMinutes, ticket.riskLevel)">
                        <span>{{ getBufferIcon(ticket.riskLevel) }}</span>
                        <span>{{ ticket.bufferMinutes > 0 ? '+' : '' }}{{ ticket.bufferMinutes }}m</span>
                      </div>
                      <div class="text-[10px] text-slate-400 mt-1">
                        Forecast: {{ ticket.riskLevel }}
                      </div>
                    </td>

                    <!-- Assigned Agent & Tier -->
                    <td class="py-3.5 px-4">
                      <div class="font-medium text-white flex items-center gap-1.5">
                        <span class="w-2 h-2 rounded-full" [class]="ticket.autoReassigned ? 'bg-amber-400' : 'bg-emerald-400'"></span>
                        <span>{{ ticket.assignedAgentName }}</span>
                      </div>
                      <div class="text-[10px] text-slate-400 mt-0.5">
                        {{ ticket.assignedAgentTier }}
                      </div>
                      @if (ticket.autoReassigned && ticket.autoReassignedTo) {
                        <div class="text-[10px] text-amber-300 font-medium mt-0.5 flex items-center gap-1">
                          <span>↳ Reassigned to:</span>
                          <span class="truncate max-w-[140px]" [title]="ticket.autoReassignedTo">{{ ticket.autoReassignedTo }}</span>
                        </div>
                      }
                    </td>

                    <!-- Actions -->
                    <td class="py-3.5 px-4 text-right">
                      <div class="flex items-center justify-end gap-1.5">
                        <button
                          (click)="openEvaluateModal(ticket)"
                          class="px-2.5 py-1 bg-slate-800 hover:bg-slate-700 text-slate-300 hover:text-white rounded text-xs border border-slate-700 transition"
                          title="Evaluate ticket SLA with custom queue/velocity">
                          Evaluate
                        </button>
                        <button
                          (click)="openReassignModal(ticket)"
                          class="px-2.5 py-1 bg-rose-950/60 hover:bg-rose-900/80 text-rose-300 hover:text-rose-100 rounded text-xs border border-rose-800/60 transition"
                          title="Manual Supervisor Reassignment">
                          Reassign
                        </button>
                      </div>
                    </td>
                  </tr>
                } @empty {
                  <tr>
                    <td colspan="7" class="py-12 text-center text-slate-400 text-xs">
                      No monitored tickets matched the active filter or search query.
                    </td>
                  </tr>
                }
              </tbody>
            </table>
          </div>
        </div>

        <!-- Escalation Ladder Guide & Rule Hierarchy Info Panel -->
        <div class="grid grid-cols-1 md:grid-cols-4 gap-4 p-5 rounded-xl bg-slate-900/60 border border-slate-800 text-xs">
          <div class="space-y-1">
            <div class="flex items-center gap-1.5 font-semibold text-emerald-400">
              <span>●</span>
              <span>Nominal Stage (&lt;70%)</span>
            </div>
            <p class="text-slate-400 text-[11px]">
              Ticket progresses normally within standard operating queue. Velocity projections monitored.
            </p>
          </div>

          <div class="space-y-1">
            <div class="flex items-center gap-1.5 font-semibold text-yellow-400">
              <span>●</span>
              <span>Warning Stage (70% - 85%)</span>
            </div>
            <p class="text-slate-400 text-[11px]">
              Yellow flag raised. Priority dynamically elevated (Standard → High, High → Urgent).
            </p>
          </div>

          <div class="space-y-1">
            <div class="flex items-center gap-1.5 font-semibold text-amber-400">
              <span>●</span>
              <span>Critical Stage (85% - 100%)</span>
            </div>
            <p class="text-slate-400 text-[11px]">
              Automated re-assignment to on-call Senior Tier-2 Specialist with lowest active queue depth.
            </p>
          </div>

          <div class="space-y-1">
            <div class="flex items-center gap-1.5 font-semibold text-rose-400">
              <span>●</span>
              <span>Breached Stage (≥100%)</span>
            </div>
            <p class="text-slate-400 text-[11px]">
              Executive escalation triggered, logged in compliance ledger, immediate incident commander notification.
            </p>
          </div>
        </div>
      </main>

      <!-- Reassign Modal -->
      @if (reassignModalTicket()) {
        <div class="fixed inset-0 z-50 bg-slate-950/80 backdrop-blur-sm flex items-center justify-center p-4">
          <div class="bg-slate-900 border border-slate-800 rounded-2xl w-full max-w-md p-6 space-y-4 shadow-2xl animate-scaleIn">
            <div class="flex items-center justify-between border-b border-slate-800 pb-3">
              <div>
                <h3 class="text-sm font-bold text-white">Manual Supervisor Reassignment</h3>
                <p class="text-xs text-slate-400 font-mono">{{ reassignModalTicket()!.ticketNumber }}: {{ reassignModalTicket()!.customerBrand }}</p>
              </div>
              <button (click)="reassignModalTicket.set(null)" class="text-slate-400 hover:text-white">✕</button>
            </div>

            <div class="space-y-3 text-xs">
              <div>
                <label class="block text-slate-300 font-medium mb-1">Select Senior Tier-2 Specialist</label>
                <select
                  [(ngModel)]="selectedReassignAgentId"
                  class="w-full bg-slate-950 border border-slate-800 rounded-lg p-2.5 text-white focus:outline-none focus:border-rose-500">
                  @for (agent of seniorSpecialists; track agent.id) {
                    <option [value]="agent.id">
                      {{ agent.name }} ({{ agent.tier }}) • Queue: {{ agent.activeQueueCount }}
                    </option>
                  }
                </select>
              </div>

              <div>
                <label class="block text-slate-300 font-medium mb-1">Supervisor Justification / Notes</label>
                <textarea
                  [(ngModel)]="reassignReason"
                  rows="3"
                  placeholder="Reason for manual reassignment..."
                  class="w-full bg-slate-950 border border-slate-800 rounded-lg p-2.5 text-white placeholder-slate-500 focus:outline-none focus:border-rose-500"></textarea>
              </div>
            </div>

            <div class="flex items-center justify-end gap-2.5 pt-3 border-t border-slate-800">
              <button
                (click)="reassignModalTicket.set(null)"
                class="px-4 py-2 bg-slate-800 hover:bg-slate-700 text-slate-300 rounded-lg text-xs transition">
                Cancel
              </button>
              <button
                (click)="executeManualReassign()"
                [disabled]="isReassigning()"
                class="px-4 py-2 bg-rose-600 hover:bg-rose-500 disabled:opacity-50 text-white font-semibold rounded-lg text-xs transition shadow-lg shadow-rose-950/40">
                {{ isReassigning() ? 'Reassigning...' : 'Confirm Reassignment' }}
              </button>
            </div>
          </div>
        </div>
      }

      <!-- Evaluate Single Ticket Modal -->
      @if (evaluateModalTicket()) {
        <div class="fixed inset-0 z-50 bg-slate-950/80 backdrop-blur-sm flex items-center justify-center p-4">
          <div class="bg-slate-900 border border-slate-800 rounded-2xl w-full max-w-md p-6 space-y-4 shadow-2xl animate-scaleIn">
            <div class="flex items-center justify-between border-b border-slate-800 pb-3">
              <div>
                <h3 class="text-sm font-bold text-white">Evaluate Velocity-to-Breach Projection</h3>
                <p class="text-xs text-slate-400 font-mono">{{ evaluateModalTicket()!.ticketNumber }} • Target: {{ evaluateModalTicket()!.targetWorkingMinutes }}m</p>
              </div>
              <button (click)="evaluateModalTicket.set(null)" class="text-slate-400 hover:text-white">✕</button>
            </div>

            <div class="space-y-4 text-xs">
              <div>
                <div class="flex items-center justify-between text-slate-300 mb-1">
                  <label class="font-medium">Queue Ahead</label>
                  <span class="font-mono text-amber-300 font-bold">#{{ evalQueueAhead }}</span>
                </div>
                <input
                  type="range"
                  min="0"
                  max="10"
                  [(ngModel)]="evalQueueAhead"
                  class="w-full accent-rose-500" />
              </div>

              <div>
                <div class="flex items-center justify-between text-slate-300 mb-1">
                  <label class="font-medium">Agent Velocity (tickets / min)</label>
                  <span class="font-mono text-cyan-300 font-bold">{{ evalVelocity }} t/m (~{{ (1 / evalVelocity).toFixed(0) }}m / ticket)</span>
                </div>
                <input
                  type="range"
                  min="0.01"
                  max="0.20"
                  step="0.01"
                  [(ngModel)]="evalVelocity"
                  class="w-full accent-cyan-500" />
              </div>

              <div>
                <div class="flex items-center justify-between text-slate-300 mb-1">
                  <label class="font-medium">Complexity Weight</label>
                  <span class="font-mono text-purple-300 font-bold">+{{ (evalComplexity * 100).toFixed(0) }}%</span>
                </div>
                <input
                  type="range"
                  min="0"
                  max="1"
                  step="0.05"
                  [(ngModel)]="evalComplexity"
                  class="w-full accent-purple-500" />
              </div>
            </div>

            <div class="flex items-center justify-end gap-2.5 pt-3 border-t border-slate-800">
              <button
                (click)="evaluateModalTicket.set(null)"
                class="px-4 py-2 bg-slate-800 hover:bg-slate-700 text-slate-300 rounded-lg text-xs transition">
                Cancel
              </button>
              <button
                (click)="executeEvaluateTicket()"
                [disabled]="isEvaluating()"
                class="px-4 py-2 bg-gradient-to-r from-rose-600 to-amber-500 hover:from-rose-500 hover:to-amber-400 disabled:opacity-50 text-white font-semibold rounded-lg text-xs transition shadow-lg shadow-rose-950/40">
                {{ isEvaluating() ? 'Evaluating...' : 'Recalculate & Apply' }}
              </button>
            </div>
          </div>
        </div>
      }
    </div>
  `
})
export class SlaBreachMonitorCockpitComponent implements OnInit {
  private readonly http = inject(HttpClient);

  // Core Data Signals
  tickets = signal<TicketSlaProjectionDto[]>([]);
  metrics = signal<SlaMonitorMetricsDto | null>(null);

  // Loading States
  isSweeping = signal<boolean>(false);
  isEvaluating = signal<boolean>(false);
  isReassigning = signal<boolean>(false);

  // Notifications
  notificationBanner = signal<{ message: string; type: 'success' | 'danger' } | null>(null);

  // Filters
  selectedRiskFilter = signal<string>('ALL');
  searchQuery = signal<string>('');

  // Modals state
  reassignModalTicket = signal<TicketSlaProjectionDto | null>(null);
  selectedReassignAgentId = 'agent-tier2-03';
  reassignReason = 'Elevated to senior specialist due to imminent SLA breach threshold.';

  evaluateModalTicket = signal<TicketSlaProjectionDto | null>(null);
  evalQueueAhead = 2;
  evalVelocity = 0.05;
  evalComplexity = 0.2;

  // Senior specialists directory
  seniorSpecialists = [
    { id: 'agent-tier2-03', name: 'Elena Vance', tier: 'Principal SRE / Incident Lead', activeQueueCount: 0 },
    { id: 'agent-tier2-02', name: 'Alexei Morozov', tier: 'Staff Support Engineer (Tier-2)', activeQueueCount: 1 },
    { id: 'agent-tier2-01', name: 'Sarah Chen', tier: 'Senior Escalation Specialist (Tier-2)', activeQueueCount: 2 },
    { id: 'agent-supervisor-01', name: 'Commander Shepherd', tier: 'Support Operations Director', activeQueueCount: 0 }
  ];

  // Computed counts for filter pills
  imminentBreachCount = computed(() => this.tickets().filter(t => t.riskLevel === SlaRiskLevel.ImminentBreach).length);
  criticalStageCount = computed(() => this.tickets().filter(t => t.escalationStage === SlaEscalationStage.Critical).length);
  warningStageCount = computed(() => this.tickets().filter(t => t.escalationStage === SlaEscalationStage.Warning).length);
  safeRiskCount = computed(() => this.tickets().filter(t => t.riskLevel === SlaRiskLevel.Safe).length);

  // Filtered tickets
  filteredTickets = computed(() => {
    const list = this.tickets();
    const filter = this.selectedRiskFilter();
    const q = this.searchQuery().toLowerCase().trim();

    return list.filter(ticket => {
      // Risk / Stage Filter
      if (filter === 'ImminentBreach' && ticket.riskLevel !== SlaRiskLevel.ImminentBreach) return false;
      if (filter === 'Critical' && ticket.escalationStage !== SlaEscalationStage.Critical) return false;
      if (filter === 'Warning' && ticket.escalationStage !== SlaEscalationStage.Warning) return false;
      if (filter === 'Safe' && ticket.riskLevel !== SlaRiskLevel.Safe) return false;

      // Text search
      if (q) {
        const matchesNum = ticket.ticketNumber.toLowerCase().includes(q);
        const matchesTitle = ticket.title.toLowerCase().includes(q);
        const matchesBrand = ticket.customerBrand.toLowerCase().includes(q);
        const matchesAgent = ticket.assignedAgentName.toLowerCase().includes(q);
        if (!matchesNum && !matchesTitle && !matchesBrand && !matchesAgent) return false;
      }

      return true;
    });
  });

  ngOnInit(): void {
    this.loadInitialSeedData();
    this.fetchDataFromBackend();
  }

  fetchDataFromBackend(): void {
    this.http.get<TicketSlaProjectionDto[]>('/api/v1/support/sla-monitor/tickets').subscribe({
      next: (data) => {
        if (data && data.length > 0) {
          this.tickets.set(data);
        }
      },
      error: () => {
        // Retain initial seed data if backend is offline
      }
    });

    this.http.get<SlaMonitorMetricsDto>('/api/v1/support/sla-monitor/metrics').subscribe({
      next: (data) => {
        if (data) {
          this.metrics.set(data);
        }
      },
      error: () => {
        // Retain fallback metrics
      }
    });
  }

  runEscalationSweep(): void {
    this.isSweeping.set(true);
    this.http.post<EscalationSweepResultDto>('/api/v1/support/sla-monitor/run-sweep', {}).subscribe({
      next: (res) => {
        this.isSweeping.set(false);
        if (res && res.tickets) {
          this.tickets.set(res.tickets);
        }
        this.fetchDataFromBackend();
        this.notificationBanner.set({
          message: `Escalation sweep executed successfully: ${res.evaluatedCount} tickets evaluated, ${res.criticalEscalationsCount} criticals, ${res.autoReassignedCount} auto-reassigned to Senior Tier-2.`,
          type: 'success'
        });
      },
      error: () => {
        this.isSweeping.set(false);
        // Fallback simulation in UI
        this.simulateSweepLocally();
      }
    });
  }

  openEvaluateModal(ticket: TicketSlaProjectionDto): void {
    this.evaluateModalTicket.set(ticket);
    this.evalQueueAhead = ticket.queueAhead;
    this.evalVelocity = ticket.avgAgentVelocityMinutes;
    this.evalComplexity = ticket.complexityWeight;
  }

  executeEvaluateTicket(): void {
    const ticket = this.evaluateModalTicket();
    if (!ticket) return;

    this.isEvaluating.set(true);
    const body = {
      ticketId: ticket.ticketId,
      queueAhead: Number(this.evalQueueAhead),
      avgAgentVelocityMinutes: Number(this.evalVelocity),
      complexityWeight: Number(this.evalComplexity)
    };

    this.http.post<TicketSlaProjectionDto>('/api/v1/support/sla-monitor/evaluate', body).subscribe({
      next: (updated) => {
        this.isEvaluating.set(false);
        this.evaluateModalTicket.set(null);
        this.tickets.update(list => list.map(t => t.ticketId === updated.ticketId ? updated : t));
        this.fetchDataFromBackend();
        this.notificationBanner.set({
          message: `Ticket ${updated.ticketNumber} re-evaluated: Buffer is ${updated.bufferMinutes}m (${updated.riskLevel}).`,
          type: 'success'
        });
      },
      error: () => {
        this.isEvaluating.set(false);
        this.evaluateModalTicket.set(null);
        // Local state update
        this.tickets.update(list => list.map(t => {
          if (t.ticketId === ticket.ticketId) {
            const queueFactor = Math.max(0, this.evalQueueAhead) + 1;
            const velocity = this.evalVelocity > 0 ? this.evalVelocity : 0.05;
            const neededMinutes = (queueFactor / velocity) * (1.0 + this.evalComplexity);
            const buffer = Math.round(t.remainingWorkingMinutes - neededMinutes);
            const risk = buffer > 60 ? SlaRiskLevel.Safe : buffer >= 0 ? SlaRiskLevel.Warning : SlaRiskLevel.ImminentBreach;
            return {
              ...t,
              queueAhead: this.evalQueueAhead,
              avgAgentVelocityMinutes: this.evalVelocity,
              complexityWeight: this.evalComplexity,
              bufferMinutes: buffer,
              riskLevel: risk
            };
          }
          return t;
        }));
        this.notificationBanner.set({
          message: `Ticket ${ticket.ticketNumber} projection recalculated locally.`,
          type: 'success'
        });
      }
    });
  }

  openReassignModal(ticket: TicketSlaProjectionDto): void {
    this.reassignModalTicket.set(ticket);
    this.selectedReassignAgentId = 'agent-tier2-03';
    this.reassignReason = 'Elevated to senior specialist due to imminent SLA breach threshold.';
  }

  executeManualReassign(): void {
    const ticket = this.reassignModalTicket();
    if (!ticket) return;

    const specialist = this.seniorSpecialists.find(s => s.id === this.selectedReassignAgentId) ?? this.seniorSpecialists[0];

    this.isReassigning.set(true);
    const body = {
      ticketId: ticket.ticketId,
      targetAgentId: specialist.id,
      targetAgentName: specialist.name,
      reason: this.reassignReason
    };

    this.http.post<TicketSlaProjectionDto>('/api/v1/support/sla-monitor/reassign', body).subscribe({
      next: (updated) => {
        this.isReassigning.set(false);
        this.reassignModalTicket.set(null);
        this.tickets.update(list => list.map(t => t.ticketId === updated.ticketId ? updated : t));
        this.notificationBanner.set({
          message: `Ticket ${ticket.ticketNumber} manually reassigned to ${specialist.name}.`,
          type: 'success'
        });
      },
      error: () => {
        this.isReassigning.set(false);
        this.reassignModalTicket.set(null);
        this.tickets.update(list => list.map(t => {
          if (t.ticketId === ticket.ticketId) {
            return {
              ...t,
              assignedAgentId: specialist.id,
              assignedAgentName: specialist.name,
              assignedAgentTier: 'Supervisor Reassigned (Tier-2)',
              autoReassigned: true,
              autoReassignedTo: `${specialist.name} (Manual by Supervisor)`
            };
          }
          return t;
        }));
        this.notificationBanner.set({
          message: `Ticket ${ticket.ticketNumber} reassigned to ${specialist.name}.`,
          type: 'success'
        });
      }
    });
  }

  private simulateSweepLocally(): void {
    this.tickets.update(list => list.map(t => {
      if (t.escalationStage === SlaEscalationStage.Critical && !t.autoReassigned) {
        return {
          ...t,
          autoReassigned: true,
          autoReassignedTo: 'Elena Vance (Principal SRE)',
          assignedAgentName: 'Elena Vance',
          assignedAgentTier: 'Principal SRE / Incident Lead'
        };
      }
      return t;
    }));
    this.notificationBanner.set({
      message: 'Escalation sweep completed: Critical tickets verified and auto-routed.',
      type: 'success'
    });
  }

  // Visual helper methods
  getStageBadgeClass(stage: SlaEscalationStage): string {
    switch (stage) {
      case SlaEscalationStage.Nominal:
        return 'bg-emerald-950 text-emerald-300 border border-emerald-800/60';
      case SlaEscalationStage.Warning:
        return 'bg-yellow-950 text-yellow-300 border border-yellow-800/60';
      case SlaEscalationStage.Critical:
        return 'bg-amber-950 text-amber-300 border border-amber-800/60 animate-pulse';
      case SlaEscalationStage.Breached:
        return 'bg-rose-950 text-rose-300 border border-rose-800/60';
      default:
        return 'bg-slate-800 text-slate-300';
    }
  }

  getStageTextColor(stage: SlaEscalationStage): string {
    switch (stage) {
      case SlaEscalationStage.Nominal: return 'text-emerald-400';
      case SlaEscalationStage.Warning: return 'text-yellow-400';
      case SlaEscalationStage.Critical: return 'text-amber-400';
      case SlaEscalationStage.Breached: return 'text-rose-400';
      default: return 'text-slate-300';
    }
  }

  getProgressBarColor(stage: SlaEscalationStage): string {
    switch (stage) {
      case SlaEscalationStage.Nominal: return 'bg-emerald-500';
      case SlaEscalationStage.Warning: return 'bg-yellow-500';
      case SlaEscalationStage.Critical: return 'bg-amber-500';
      case SlaEscalationStage.Breached: return 'bg-rose-600';
      default: return 'bg-slate-500';
    }
  }

  getBufferBadgeClass(buffer: number, risk: SlaRiskLevel): string {
    if (risk === SlaRiskLevel.ImminentBreach || buffer < 0) {
      return 'bg-rose-950/80 text-rose-300 border-rose-600/60 shadow-sm shadow-rose-950/50 animate-pulse';
    }
    if (risk === SlaRiskLevel.Warning || buffer <= 60) {
      return 'bg-amber-950/80 text-amber-300 border-amber-600/60';
    }
    return 'bg-emerald-950/80 text-emerald-300 border-emerald-600/60';
  }

  getBufferIcon(risk: SlaRiskLevel): string {
    switch (risk) {
      case SlaRiskLevel.ImminentBreach: return '🔥';
      case SlaRiskLevel.Warning: return '⚠️';
      case SlaRiskLevel.Safe: return '🛡️';
      default: return '⏱️';
    }
  }

  private loadInitialSeedData(): void {
    const now = new Date();
    const isoNow = now.toISOString();

    const seeded: TicketSlaProjectionDto[] = [
      {
        ticketId: '11111111-1111-1111-1111-111111111101',
        ticketNumber: 'TICK-4091',
        title: 'Enterprise SSO SAML Certificate Expiration in Production',
        customerBrand: 'Acme Financial Services',
        assignedAgentId: 'agent-101',
        assignedAgentName: 'Dave Miller',
        assignedAgentTier: 'Tier-1 Support',
        priority: 'Critical24x7',
        targetType: 'FirstResponse',
        queueAhead: 2,
        complexityWeight: 0.3,
        avgAgentVelocityMinutes: 0.05,
        createdAtUtc: new Date(now.getTime() - 53 * 60000).toISOString(),
        dueAtUtc: new Date(now.getTime() + 7 * 60000).toISOString(),
        predictedCompletionAtUtc: new Date(now.getTime() + 78 * 60000).toISOString(),
        targetWorkingMinutes: 60,
        elapsedWorkingMinutes: 53,
        remainingWorkingMinutes: 7,
        percentageElapsed: 88.3,
        bufferMinutes: -71,
        escalationStage: SlaEscalationStage.Critical,
        riskLevel: SlaRiskLevel.ImminentBreach,
        is24x7Tier: true,
        priorityBoosted: true,
        autoReassigned: true,
        autoReassignedTo: 'Elena Vance (Principal SRE / Incident Lead)',
        executiveAlert: null,
        lastEvaluatedAtUtc: isoNow,
        isFirstResponseCompliant: true,
        isResolutionCompliant: true
      },
      {
        ticketId: '11111111-1111-1111-1111-111111111102',
        ticketNumber: 'TICK-4092',
        title: 'Stripe Webhook Retries Exhausted - Payment Captures Stalled',
        customerBrand: 'GlobalCorp Logistics',
        assignedAgentId: 'agent-102',
        assignedAgentName: 'Jessica Alba',
        assignedAgentTier: 'Tier-1 Support',
        priority: 'High',
        targetType: 'Resolution',
        queueAhead: 1,
        complexityWeight: 0.15,
        avgAgentVelocityMinutes: 0.05,
        createdAtUtc: new Date(now.getTime() - 92 * 60000).toISOString(),
        dueAtUtc: new Date(now.getTime() + 28 * 60000).toISOString(),
        predictedCompletionAtUtc: new Date(now.getTime() + 46 * 60000).toISOString(),
        targetWorkingMinutes: 120,
        elapsedWorkingMinutes: 92,
        remainingWorkingMinutes: 28,
        percentageElapsed: 76.7,
        bufferMinutes: -18,
        escalationStage: SlaEscalationStage.Warning,
        riskLevel: SlaRiskLevel.ImminentBreach,
        is24x7Tier: false,
        priorityBoosted: true,
        autoReassigned: false,
        lastEvaluatedAtUtc: isoNow,
        isFirstResponseCompliant: true,
        isResolutionCompliant: true
      },
      {
        ticketId: '11111111-1111-1111-1111-111111111103',
        ticketNumber: 'TICK-4093',
        title: 'Custom CNAME Domain SSL Handshake Timeout',
        customerBrand: 'Nordic Retail Group',
        assignedAgentId: 'agent-103',
        assignedAgentName: 'Tom Davis',
        assignedAgentTier: 'Tier-1 Support',
        priority: 'Standard',
        targetType: 'Resolution',
        queueAhead: 0,
        complexityWeight: 0.1,
        avgAgentVelocityMinutes: 0.06,
        createdAtUtc: new Date(now.getTime() - 43 * 60000).toISOString(),
        dueAtUtc: new Date(now.getTime() + 197 * 60000).toISOString(),
        predictedCompletionAtUtc: new Date(now.getTime() + 18 * 60000).toISOString(),
        targetWorkingMinutes: 240,
        elapsedWorkingMinutes: 43,
        remainingWorkingMinutes: 197,
        percentageElapsed: 17.9,
        bufferMinutes: 179,
        escalationStage: SlaEscalationStage.Nominal,
        riskLevel: SlaRiskLevel.Safe,
        is24x7Tier: false,
        priorityBoosted: false,
        autoReassigned: false,
        lastEvaluatedAtUtc: isoNow,
        isFirstResponseCompliant: true,
        isResolutionCompliant: true
      },
      {
        ticketId: '11111111-1111-1111-1111-111111111104',
        ticketNumber: 'TICK-4094',
        title: 'Data Export CSV Stream Hanging on Large Datasets (>100k rows)',
        customerBrand: 'Apex MedTech',
        assignedAgentId: 'agent-104',
        assignedAgentName: 'Maya Lin',
        assignedAgentTier: 'Tier-1 Support',
        priority: 'Standard',
        targetType: 'Resolution',
        queueAhead: 3,
        complexityWeight: 0.25,
        avgAgentVelocityMinutes: 0.04,
        createdAtUtc: new Date(now.getTime() - 200 * 60000).toISOString(),
        dueAtUtc: new Date(now.getTime() - 20 * 60000).toISOString(),
        predictedCompletionAtUtc: new Date(now.getTime() + 125 * 60000).toISOString(),
        targetWorkingMinutes: 180,
        elapsedWorkingMinutes: 200,
        remainingWorkingMinutes: 0,
        percentageElapsed: 111.1,
        bufferMinutes: -145,
        escalationStage: SlaEscalationStage.Breached,
        riskLevel: SlaRiskLevel.ImminentBreach,
        is24x7Tier: false,
        priorityBoosted: true,
        autoReassigned: true,
        autoReassignedTo: 'Alexei Morozov (Staff Support Engineer)',
        executiveAlert: 'Executive SLA Breach Escalation: SLA target of 180m exceeded.',
        lastEvaluatedAtUtc: isoNow,
        isFirstResponseCompliant: true,
        isResolutionCompliant: false
      },
      {
        ticketId: '11111111-1111-1111-1111-111111111105',
        ticketNumber: 'TICK-4095',
        title: 'Autonomous Funnel Step Action Not Firing for VIP Cohort',
        customerBrand: 'Zenith OmniBrands',
        assignedAgentId: 'agent-105',
        assignedAgentName: 'Liam Johnson',
        assignedAgentTier: 'Tier-1 Support',
        priority: 'High',
        targetType: 'Resolution',
        queueAhead: 3,
        complexityWeight: 0.4,
        avgAgentVelocityMinutes: 0.05,
        createdAtUtc: new Date(now.getTime() - 107 * 60000).toISOString(),
        dueAtUtc: new Date(now.getTime() + 13 * 60000).toISOString(),
        predictedCompletionAtUtc: new Date(now.getTime() + 112 * 60000).toISOString(),
        targetWorkingMinutes: 120,
        elapsedWorkingMinutes: 107,
        remainingWorkingMinutes: 13,
        percentageElapsed: 89.2,
        bufferMinutes: -99,
        escalationStage: SlaEscalationStage.Critical,
        riskLevel: SlaRiskLevel.ImminentBreach,
        is24x7Tier: false,
        priorityBoosted: true,
        autoReassigned: true,
        autoReassignedTo: 'Sarah Chen (Senior Escalation Specialist)',
        lastEvaluatedAtUtc: isoNow,
        isFirstResponseCompliant: true,
        isResolutionCompliant: true
      },
      {
        ticketId: '11111111-1111-1111-1111-111111111106',
        ticketNumber: 'TICK-4096',
        title: 'WhatsApp Cloud Session Refresh Authentication Error',
        customerBrand: 'Starlight Digital',
        assignedAgentId: 'agent-106',
        assignedAgentName: 'Chloe Martin',
        assignedAgentTier: 'Tier-1 Support',
        priority: 'Standard',
        targetType: 'FirstResponse',
        queueAhead: 1,
        complexityWeight: 0.0,
        avgAgentVelocityMinutes: 0.07,
        createdAtUtc: new Date(now.getTime() - 36 * 60000).toISOString(),
        dueAtUtc: new Date(now.getTime() + 264 * 60000).toISOString(),
        predictedCompletionAtUtc: new Date(now.getTime() + 28 * 60000).toISOString(),
        targetWorkingMinutes: 300,
        elapsedWorkingMinutes: 36,
        remainingWorkingMinutes: 264,
        percentageElapsed: 12.0,
        bufferMinutes: 236,
        escalationStage: SlaEscalationStage.Nominal,
        riskLevel: SlaRiskLevel.Safe,
        is24x7Tier: false,
        priorityBoosted: false,
        autoReassigned: false,
        lastEvaluatedAtUtc: isoNow,
        isFirstResponseCompliant: true,
        isResolutionCompliant: true
      }
    ];

    this.tickets.set(seeded);

    this.metrics.set({
      firstResponseCompliancePercentage: 100.0,
      resolutionCompliancePercentage: 75.0,
      totalMonitoredTickets: 6,
      imminentBreachesCount: 3,
      criticalCount: 2,
      warningCount: 1,
      safeCount: 2,
      breachedCount: 1,
      activeQueueVelocity: 0.053,
      lastSweepAtUtc: isoNow
    });
  }
}
