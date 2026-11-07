import { Component, ChangeDetectionStrategy, signal, computed, Optional, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { TranslationService } from '../../core/services/translation.service';

export type TicketPriority = 'Low' | 'Medium' | 'High' | 'Urgent';
export type SupportTier = 'Tier1_Frontline' | 'Tier2_TechnicalSpecialist' | 'Tier3_EngineeringOps' | 'ExecutiveOnCall';
export type EscalationState = 'Normal' | 'WarningSlaDueSoon' | 'EscalatedTier2' | 'EscalatedTier3' | 'Resolved';
export type CustomerSentiment = 'Positive' | 'Neutral' | 'Frustrated' | 'CriticalChurnRisk';

export interface TicketEscalationLogRecord {
  logId: string;
  ticketId: string;
  fromTier: SupportTier;
  toTier: SupportTier;
  reason: string;
  triggeredBy: string;
  timestampUtc: string;
}

export interface SupportTicketRecord {
  ticketId: string;
  tenantId: string;
  ticketNumber: string;
  subject: string;
  customerName: string;
  customerEmail: string;
  companyName: string;
  priority: TicketPriority;
  currentTier: SupportTier;
  state: EscalationState;
  sentiment: CustomerSentiment;
  assignedAgent: string;
  category: string;
  createdAtUtc: string;
  slaDueAtUtc: string;
  firstResponseAtUtc: string | null;
  resolvedAtUtc: string | null;
  resolutionNotes: string | null;
  escalationLogs: TicketEscalationLogRecord[];
  tags: string[];
}

export interface SuggestCannedMacroResponse {
  ticketId: string;
  macroTitle: string;
  recommendedResponse: string;
  targetSentiment: CustomerSentiment;
  actionSteps: string[];
  confidenceScore: number;
}

@Component({
  selector: 'app-support-escalation-desk-studio',
  standalone: true,
  imports: [CommonModule, FormsModule],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="min-h-screen bg-slate-950 text-slate-100 p-6 flex flex-col gap-6" dir="ltr" data-testid="support-escalation-desk-studio">
      @let totalCount = tickets().length;
      @let activeCount = activeTicketsCount();
      @let compRate = slaComplianceRate();
      @let warningCount = warningSlaCount();

      <!-- Header Section -->
      <header class="flex flex-col lg:flex-row lg:items-center justify-between gap-4 border-b border-slate-800 pb-5">
        <div>
          <div class="flex items-center gap-2 mb-1">
            <span class="px-2.5 py-0.5 text-xs font-semibold rounded-full bg-cyan-500/20 text-cyan-400 border border-cyan-500/30">
              PRD Domain H2 & B1 · Autonomous SLA Dispatch
            </span>
            <span class="text-xs text-slate-400">Multi-Tier Escalation Desk & Live Radar</span>
          </div>
          <h1 class="text-2xl font-bold tracking-tight text-white flex items-center gap-3">
            Autonomous Support Ticket SLA Escalation & Multi-Tier Dispatch Desk
          </h1>
          <p class="text-sm text-slate-400">
            Intelligent breach radar, cross-tier autonomous rerouting, sentiment triaging, and AI Copilot canned macros.
          </p>
        </div>

        <div class="flex flex-wrap items-center gap-3">
          <button
            (click)="openCreateModal()"
            data-testid="create-ticket-btn"
            class="px-4 py-2 bg-cyan-600 hover:bg-cyan-500 active:bg-cyan-700 text-white font-medium text-sm rounded-lg transition-colors shadow-lg shadow-cyan-900/30 flex items-center gap-2">
            <span>+</span> Log New Ticket
          </button>
          <button
            (click)="triggerAutomatedSlaEvaluation()"
            data-testid="evaluate-radar-btn"
            class="px-3.5 py-2 bg-slate-800 hover:bg-slate-700 text-slate-200 border border-slate-700 font-medium text-sm rounded-lg transition-colors flex items-center gap-2">
            <span>⚡</span> Evaluate SLA Radar
          </button>
          <button
            (click)="resetToDefaults()"
            data-testid="reset-btn"
            class="px-3 py-2 bg-slate-900 hover:bg-slate-800 text-slate-400 hover:text-slate-200 border border-slate-800 text-sm rounded-lg transition-colors">
            ↺ Reset
          </button>
        </div>
      </header>

      <!-- Top KPI Metric Cards -->
      <section class="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-5 gap-4" data-testid="kpi-metrics-grid">
        <div class="bg-slate-900/90 border border-slate-800 p-4 rounded-xl shadow-sm" data-testid="kpi-active-tickets">
          <div class="text-xs font-medium text-slate-400 uppercase tracking-wider">Active Tickets</div>
          <div class="text-2xl font-bold text-white mt-1">{{ activeCount }} <span class="text-xs text-slate-500 font-normal">/ {{ totalCount }} Total</span></div>
          <div class="text-xs text-slate-400 mt-1">Pending resolution</div>
        </div>

        <div class="bg-slate-900/90 border border-slate-800 p-4 rounded-xl shadow-sm" data-testid="kpi-warning-sla">
          <div class="text-xs font-medium text-slate-400 uppercase tracking-wider">SLA Warning / At-Risk</div>
          <div class="text-2xl font-bold text-amber-400 mt-1">{{ warningCount }}</div>
          <div class="text-xs text-amber-500/80 mt-1">&le; 30m before breach</div>
        </div>

        <div class="bg-slate-900/90 border border-slate-800 p-4 rounded-xl shadow-sm" data-testid="kpi-escalated-tiers">
          <div class="text-xs font-medium text-slate-400 uppercase tracking-wider">Escalated Tier 2/3</div>
          <div class="text-2xl font-bold text-rose-400 mt-1">{{ escalatedCount() }}</div>
          <div class="text-xs text-slate-400 mt-1">Specialist & SWAT desks</div>
        </div>

        <div class="bg-slate-900/90 border border-slate-800 p-4 rounded-xl shadow-sm" data-testid="kpi-sla-compliance">
          <div class="text-xs font-medium text-slate-400 uppercase tracking-wider">SLA Compliance Rate</div>
          <div class="text-2xl font-bold mt-1" [ngClass]="compRate >= 80 ? 'text-emerald-400' : 'text-rose-400'">
            {{ compRate }}%
          </div>
          <div class="text-xs text-slate-400 mt-1">Target: &ge; 95%</div>
        </div>

        <div class="bg-slate-900/90 border border-slate-800 p-4 rounded-xl shadow-sm" data-testid="kpi-resolved-today">
          <div class="text-xs font-medium text-slate-400 uppercase tracking-wider">Resolved Today</div>
          <div class="text-2xl font-bold text-emerald-400 mt-1">{{ resolvedTodayCount() }}</div>
          <div class="text-xs text-slate-400 mt-1">Closed successfully</div>
        </div>
      </section>

      <!-- Filter and Search Toolbar -->
      <div class="bg-slate-900/80 border border-slate-800 p-4 rounded-xl flex flex-col md:flex-row items-center justify-between gap-4">
        <div class="flex flex-wrap items-center gap-3 w-full md:w-auto">
          <div class="relative flex-1 md:w-72">
            <input
              type="text"
              [(ngModel)]="searchQuery"
              placeholder="Search by ticket #, company, customer..."
              data-testid="search-input"
              class="w-full bg-slate-950 border border-slate-700 rounded-lg px-3.5 py-1.5 text-sm text-slate-200 placeholder-slate-500 focus:outline-none focus:border-cyan-500" />
          </div>

          <select
            [(ngModel)]="selectedPriorityFilter"
            data-testid="priority-filter"
            class="bg-slate-950 border border-slate-700 text-sm text-slate-300 rounded-lg px-3 py-1.5 focus:outline-none focus:border-cyan-500">
            <option value="ALL">All Priorities</option>
            <option value="Urgent">Urgent</option>
            <option value="High">High</option>
            <option value="Medium">Medium</option>
            <option value="Low">Low</option>
          </select>

          <select
            [(ngModel)]="selectedSentimentFilter"
            data-testid="sentiment-filter"
            class="bg-slate-950 border border-slate-700 text-sm text-slate-300 rounded-lg px-3 py-1.5 focus:outline-none focus:border-cyan-500">
            <option value="ALL">All Sentiments</option>
            <option value="CriticalChurnRisk">Critical Churn Risk</option>
            <option value="Frustrated">Frustrated</option>
            <option value="Neutral">Neutral</option>
            <option value="Positive">Positive</option>
          </select>
        </div>

        <div class="text-xs text-slate-400 flex items-center gap-3">
          <span class="flex items-center gap-1.5"><span class="w-2.5 h-2.5 rounded-full bg-emerald-400"></span> Normal</span>
          <span class="flex items-center gap-1.5"><span class="w-2.5 h-2.5 rounded-full bg-amber-400 animate-pulse"></span> Warning &le;30m</span>
          <span class="flex items-center gap-1.5"><span class="w-2.5 h-2.5 rounded-full bg-rose-500"></span> Escalated / Breached</span>
        </div>
      </div>

      <!-- Multi-Tier Escalation Triage Kanban Columns -->
      <section class="grid grid-cols-1 md:grid-cols-2 xl:grid-cols-4 gap-5" data-testid="multi-tier-kanban">
        @for (tier of tiers; track tier.key) {
          <div class="bg-slate-900/70 border border-slate-800 rounded-xl p-4 flex flex-col gap-3 min-h-[500px]" [attr.data-testid]="'tier-column-' + tier.key">
            <!-- Column Header -->
            <div class="flex items-center justify-between pb-3 border-b border-slate-800">
              <div class="flex items-center gap-2">
                <span class="text-base font-semibold text-white">{{ tier.title }}</span>
                <span class="px-2 py-0.5 text-xs font-bold rounded-full bg-slate-800 text-slate-300">
                  {{ getTierTickets(tier.key).length }}
                </span>
              </div>
              <span class="text-xs text-slate-500">{{ tier.subtitle }}</span>
            </div>

            <!-- Ticket Cards -->
            <div class="flex flex-col gap-3 flex-1 overflow-y-auto">
              @for (ticket of getTierTickets(tier.key); track ticket.ticketId) {
                <div
                  class="bg-slate-950 border rounded-lg p-3.5 flex flex-col gap-2.5 transition-all hover:border-slate-600 shadow-md"
                  [ngClass]="{
                    'border-rose-500/50 bg-rose-950/10': ticket.sentiment === 'CriticalChurnRisk' || ticket.state === 'EscalatedTier3',
                    'border-amber-500/50 bg-amber-950/10': ticket.state === 'WarningSlaDueSoon',
                    'border-slate-800': ticket.state === 'Normal' || ticket.state === 'Resolved'
                  }"
                  [attr.data-testid]="'ticket-card-' + ticket.ticketId">
                  
                  <!-- Card Header: Number & Badges -->
                  <div class="flex items-start justify-between gap-2">
                    <div>
                      <span class="text-xs font-mono font-semibold text-cyan-400">{{ ticket.ticketNumber }}</span>
                      <h4 class="text-sm font-semibold text-white line-clamp-1 mt-0.5">{{ ticket.subject }}</h4>
                    </div>

                    <!-- Priority Badge with @switch -->
                    @switch (ticket.priority) {
                      @case ('Urgent') {
                        <span class="px-2 py-0.5 text-[10px] font-bold uppercase rounded bg-rose-500/20 text-rose-400 border border-rose-500/30">Urgent</span>
                      }
                      @case ('High') {
                        <span class="px-2 py-0.5 text-[10px] font-bold uppercase rounded bg-amber-500/20 text-amber-400 border border-amber-500/30">High</span>
                      }
                      @case ('Medium') {
                        <span class="px-2 py-0.5 text-[10px] font-bold uppercase rounded bg-sky-500/20 text-sky-400 border border-sky-500/30">Medium</span>
                      }
                      @default {
                        <span class="px-2 py-0.5 text-[10px] font-bold uppercase rounded bg-slate-500/20 text-slate-400 border border-slate-500/30">Low</span>
                      }
                    }
                  </div>

                  <!-- Customer & Company -->
                  <div class="text-xs text-slate-400 flex flex-col gap-0.5">
                    <div class="text-slate-200 font-medium">{{ ticket.customerName }} &middot; <span class="text-slate-400 font-normal">{{ ticket.companyName }}</span></div>
                    <div class="text-slate-500 text-[11px] flex items-center justify-between">
                      <span>Owner: {{ ticket.assignedAgent }}</span>
                      <span class="px-1.5 py-0.2 rounded text-[10px]"
                        [ngClass]="{
                          'bg-rose-500/20 text-rose-300': ticket.sentiment === 'CriticalChurnRisk',
                          'bg-amber-500/20 text-amber-300': ticket.sentiment === 'Frustrated',
                          'bg-emerald-500/20 text-emerald-300': ticket.sentiment === 'Positive',
                          'bg-slate-800 text-slate-400': ticket.sentiment === 'Neutral'
                        }">
                        {{ ticket.sentiment }}
                      </span>
                    </div>
                  </div>

                  <!-- SLA Countdown Radar Bar -->
                  <div class="bg-slate-900 border border-slate-800/80 rounded p-2 text-xs flex flex-col gap-1.5" [attr.data-testid]="'sla-radar-' + ticket.ticketId">
                    <div class="flex items-center justify-between text-[11px]">
                      <span class="text-slate-400">SLA Due Clock:</span>
                      <span class="font-semibold"
                        [ngClass]="getSlaClockClass(ticket)">
                        {{ getSlaCountdownText(ticket) }}
                      </span>
                    </div>

                    <div class="w-full bg-slate-800 rounded-full h-1.5 overflow-hidden">
                      <div
                        class="h-1.5 rounded-full transition-all"
                        [ngClass]="getSlaProgressColor(ticket)"
                        [style.width.%]="getSlaProgressPercent(ticket)">
                      </div>
                    </div>
                  </div>

                  <!-- Action Buttons -->
                  <div class="flex items-center justify-between gap-2 pt-1 border-t border-slate-800/80">
                    <div class="flex items-center gap-1.5">
                      <button
                        (click)="openMacroCopilot(ticket)"
                        [attr.data-testid]="'macro-btn-' + ticket.ticketId"
                        title="AI Macro Suggestion Copilot"
                        class="px-2 py-1 bg-cyan-950 hover:bg-cyan-900 text-cyan-300 border border-cyan-800/60 rounded text-xs transition-colors flex items-center gap-1">
                        <span>🤖</span> Macro
                      </button>
                      <button
                        (click)="openEscalateModal(ticket)"
                        [attr.data-testid]="'escalate-btn-' + ticket.ticketId"
                        [disabled]="ticket.state === 'Resolved'"
                        class="px-2 py-1 bg-slate-800 hover:bg-slate-700 disabled:opacity-40 text-slate-200 border border-slate-700 rounded text-xs transition-colors">
                        Escalate
                      </button>
                    </div>

                    @if (ticket.state !== 'Resolved') {
                      <button
                        (click)="openResolveModal(ticket)"
                        [attr.data-testid]="'resolve-btn-' + ticket.ticketId"
                        class="px-2.5 py-1 bg-emerald-950 hover:bg-emerald-900 text-emerald-300 border border-emerald-800/60 rounded text-xs transition-colors">
                        ✓ Resolve
                      </button>
                    } @else {
                      <span class="text-[11px] text-emerald-400 font-medium">✓ Closed</span>
                    }
                  </div>
                </div>
              } @empty {
                <div class="flex flex-col items-center justify-center p-8 text-center text-slate-500 border border-dashed border-slate-800 rounded-lg h-36">
                  <span class="text-xl mb-1">📭</span>
                  <p class="text-xs">No tickets in this tier</p>
                </div>
              }
            </div>
          </div>
        }
      </section>

      <!-- AI Macro Copilot Drawer / Modal -->
      @if (activeMacroTicket(); as ticket) {
        <div class="fixed inset-0 z-50 bg-slate-950/80 backdrop-blur-sm flex items-center justify-center p-4" data-testid="macro-copilot-modal">
          <div class="bg-slate-900 border border-slate-800 rounded-2xl w-full max-w-2xl overflow-hidden shadow-2xl flex flex-col">
            <div class="p-5 border-b border-slate-800 flex items-center justify-between bg-slate-950/40">
              <div class="flex items-center gap-2.5">
                <span class="p-2 rounded-lg bg-cyan-500/20 text-cyan-400">🤖</span>
                <div>
                  <h3 class="text-base font-bold text-white">AI Macro Suggestion Copilot</h3>
                  <p class="text-xs text-slate-400">Adaptive empathy response & autonomous remediation plan</p>
                </div>
              </div>
              <button (click)="closeMacroCopilot()" class="text-slate-400 hover:text-white text-lg">&times;</button>
            </div>

            <div class="p-6 flex flex-col gap-4 text-sm max-h-[75vh] overflow-y-auto">
              <!-- Ticket Brief Context -->
              <div class="bg-slate-950 p-3.5 rounded-xl border border-slate-800 flex items-center justify-between text-xs">
                <div>
                  <span class="font-mono text-cyan-400 font-semibold">{{ ticket.ticketNumber }}</span> &middot;
                  <span class="text-white font-medium">{{ ticket.customerName }}</span> ({{ ticket.companyName }})
                </div>
                <div class="flex items-center gap-2">
                  <span class="px-2 py-0.5 rounded bg-slate-800 text-slate-300 font-medium">{{ ticket.sentiment }}</span>
                  <span class="px-2 py-0.5 rounded bg-cyan-500/20 text-cyan-300 font-semibold">Match Score: {{ (currentMacro()?.confidenceScore || 0.95) * 100 }}%</span>
                </div>
              </div>

              <!-- Recommended Title -->
              <div>
                <label class="block text-xs font-semibold text-slate-400 uppercase tracking-wider mb-1">Recommended Macro Playbook</label>
                <div class="text-white font-semibold text-base bg-slate-950/70 p-2.5 rounded-lg border border-slate-800" data-testid="macro-title">
                  {{ currentMacro()?.macroTitle }}
                </div>
              </div>

              <!-- Response Editor / Preview -->
              <div>
                <div class="flex items-center justify-between mb-1">
                  <label class="text-xs font-semibold text-slate-400 uppercase tracking-wider">Canned Empathy Response</label>
                  <span class="text-[11px] text-cyan-400">Tuned for {{ ticket.sentiment }} customer sentiment</span>
                </div>
                <textarea
                  [(ngModel)]="editableMacroResponse"
                  rows="6"
                  data-testid="macro-response-input"
                  class="w-full bg-slate-950 border border-slate-800 rounded-xl p-3 text-sm text-slate-200 focus:outline-none focus:border-cyan-500 leading-relaxed font-sans"></textarea>
              </div>

              <!-- Action Remediation Steps -->
              <div>
                <label class="block text-xs font-semibold text-slate-400 uppercase tracking-wider mb-2">Automated SOP Remediation Steps</label>
                <ul class="flex flex-col gap-2">
                  @for (step of currentMacro()?.actionSteps; track step) {
                    <li class="flex items-start gap-2.5 text-xs text-slate-300 bg-slate-950/60 p-2.5 rounded-lg border border-slate-800/80">
                      <span class="text-emerald-400 font-bold">✓</span>
                      <span>{{ step }}</span>
                    </li>
                  }
                </ul>
              </div>
            </div>

            <div class="p-4 border-t border-slate-800 bg-slate-950/40 flex items-center justify-end gap-3">
              <button (click)="closeMacroCopilot()" class="px-4 py-2 bg-slate-800 hover:bg-slate-700 text-slate-300 text-sm rounded-lg">
                Close
              </button>
              <button
                (click)="applyMacroToTicket(ticket)"
                data-testid="apply-macro-btn"
                class="px-4 py-2 bg-cyan-600 hover:bg-cyan-500 text-white font-semibold text-sm rounded-lg shadow-lg shadow-cyan-900/30 flex items-center gap-2">
                <span>✓</span> Apply & Send to Customer
              </button>
            </div>
          </div>
        </div>
      }

      <!-- Escalate Modal -->
      @if (escalatingTicket(); as ticket) {
        <div class="fixed inset-0 z-50 bg-slate-950/80 backdrop-blur-sm flex items-center justify-center p-4" data-testid="escalate-modal">
          <div class="bg-slate-900 border border-slate-800 rounded-2xl w-full max-w-lg overflow-hidden shadow-2xl flex flex-col">
            <div class="p-5 border-b border-slate-800 flex items-center justify-between">
              <div>
                <h3 class="text-base font-bold text-white">Escalate Ticket {{ ticket.ticketNumber }}</h3>
                <p class="text-xs text-slate-400">{{ ticket.subject }}</p>
              </div>
              <button (click)="closeEscalateModal()" class="text-slate-400 hover:text-white">&times;</button>
            </div>

            <div class="p-6 flex flex-col gap-4 text-sm">
              <div>
                <label class="block text-xs font-semibold text-slate-400 uppercase mb-1">Target Support Tier</label>
                <select [(ngModel)]="targetEscalateTier" data-testid="target-tier-select" class="w-full bg-slate-950 border border-slate-700 rounded-lg px-3 py-2 text-slate-200">
                  <option value="Tier2_TechnicalSpecialist">Tier 2 · Technical Specialist</option>
                  <option value="Tier3_EngineeringOps">Tier 3 · Engineering Ops SWAT</option>
                  <option value="ExecutiveOnCall">Executive On-Call / VP Success</option>
                </select>
              </div>

              <div>
                <label class="block text-xs font-semibold text-slate-400 uppercase mb-1">Escalation Reason & Notes</label>
                <textarea
                  [(ngModel)]="escalateReason"
                  rows="3"
                  data-testid="escalate-reason-input"
                  placeholder="Describe root trigger, telemetry clues, or client urgency..."
                  class="w-full bg-slate-950 border border-slate-700 rounded-lg p-3 text-slate-200"></textarea>
              </div>
            </div>

            <div class="p-4 border-t border-slate-800 flex justify-end gap-3">
              <button (click)="closeEscalateModal()" class="px-4 py-2 bg-slate-800 text-slate-300 text-sm rounded-lg">Cancel</button>
              <button (click)="confirmEscalate(ticket)" data-testid="confirm-escalate-btn" class="px-4 py-2 bg-rose-600 hover:bg-rose-500 text-white font-semibold text-sm rounded-lg">
                Dispatch Escalation
              </button>
            </div>
          </div>
        </div>
      }

      <!-- Resolve Modal -->
      @if (resolvingTicket(); as ticket) {
        <div class="fixed inset-0 z-50 bg-slate-950/80 backdrop-blur-sm flex items-center justify-center p-4" data-testid="resolve-modal">
          <div class="bg-slate-900 border border-slate-800 rounded-2xl w-full max-w-lg overflow-hidden shadow-2xl flex flex-col">
            <div class="p-5 border-b border-slate-800 flex items-center justify-between">
              <div>
                <h3 class="text-base font-bold text-white">Resolve Ticket {{ ticket.ticketNumber }}</h3>
                <p class="text-xs text-slate-400">{{ ticket.subject }}</p>
              </div>
              <button (click)="closeResolveModal()" class="text-slate-400 hover:text-white">&times;</button>
            </div>

            <div class="p-6 flex flex-col gap-4 text-sm">
              <div>
                <label class="block text-xs font-semibold text-slate-400 uppercase mb-1">Resolution Summary & Root Cause</label>
                <textarea
                  [(ngModel)]="resolveNotes"
                  rows="4"
                  data-testid="resolve-notes-input"
                  placeholder="Explain fix deployed, root cause remediation, and customer confirmation..."
                  class="w-full bg-slate-950 border border-slate-700 rounded-lg p-3 text-slate-200"></textarea>
              </div>
            </div>

            <div class="p-4 border-t border-slate-800 flex justify-end gap-3">
              <button (click)="closeResolveModal()" class="px-4 py-2 bg-slate-800 text-slate-300 text-sm rounded-lg">Cancel</button>
              <button (click)="confirmResolve(ticket)" data-testid="confirm-resolve-btn" class="px-4 py-2 bg-emerald-600 hover:bg-emerald-500 text-white font-semibold text-sm rounded-lg">
                Confirm Resolution
              </button>
            </div>
          </div>
        </div>
      }

      <!-- Create Ticket Modal -->
      @if (showCreateModal()) {
        <div class="fixed inset-0 z-50 bg-slate-950/80 backdrop-blur-sm flex items-center justify-center p-4" data-testid="create-modal">
          <div class="bg-slate-900 border border-slate-800 rounded-2xl w-full max-w-lg overflow-hidden shadow-2xl flex flex-col">
            <div class="p-5 border-b border-slate-800 flex items-center justify-between">
              <h3 class="text-base font-bold text-white">Log New Support Ticket</h3>
              <button (click)="closeCreateModal()" class="text-slate-400 hover:text-white">&times;</button>
            </div>

            <div class="p-6 flex flex-col gap-3 text-sm max-h-[75vh] overflow-y-auto">
              <div>
                <label class="block text-xs font-medium text-slate-400 mb-1">Subject / Issue Summary</label>
                <input type="text" [(ngModel)]="newSubject" data-testid="new-subject" class="w-full bg-slate-950 border border-slate-700 rounded-lg px-3 py-2 text-slate-200" placeholder="e.g. Inbound Webhook Failures on Checkout" />
              </div>

              <div class="grid grid-cols-2 gap-3">
                <div>
                  <label class="block text-xs font-medium text-slate-400 mb-1">Customer Name</label>
                  <input type="text" [(ngModel)]="newCustomerName" data-testid="new-customer-name" class="w-full bg-slate-950 border border-slate-700 rounded-lg px-3 py-2 text-slate-200" placeholder="e.g. Faisal Al-Harbi" />
                </div>
                <div>
                  <label class="block text-xs font-medium text-slate-400 mb-1">Company Name</label>
                  <input type="text" [(ngModel)]="newCompanyName" data-testid="new-company-name" class="w-full bg-slate-950 border border-slate-700 rounded-lg px-3 py-2 text-slate-200" placeholder="e.g. Saudi Retail Cloud" />
                </div>
              </div>

              <div class="grid grid-cols-2 gap-3">
                <div>
                  <label class="block text-xs font-medium text-slate-400 mb-1">Priority</label>
                  <select [(ngModel)]="newPriority" data-testid="new-priority" class="w-full bg-slate-950 border border-slate-700 rounded-lg px-3 py-2 text-slate-200">
                    <option value="Urgent">Urgent (30m SLA)</option>
                    <option value="High">High (60m SLA)</option>
                    <option value="Medium">Medium (4h SLA)</option>
                    <option value="Low">Low (24h SLA)</option>
                  </select>
                </div>
                <div>
                  <label class="block text-xs font-medium text-slate-400 mb-1">Sentiment</label>
                  <select [(ngModel)]="newSentiment" data-testid="new-sentiment" class="w-full bg-slate-950 border border-slate-700 rounded-lg px-3 py-2 text-slate-200">
                    <option value="Neutral">Neutral</option>
                    <option value="Frustrated">Frustrated</option>
                    <option value="CriticalChurnRisk">Critical Churn Risk</option>
                    <option value="Positive">Positive</option>
                  </select>
                </div>
              </div>

              <div>
                <label class="block text-xs font-medium text-slate-400 mb-1">Category</label>
                <input type="text" [(ngModel)]="newCategory" data-testid="new-category" class="w-full bg-slate-950 border border-slate-700 rounded-lg px-3 py-2 text-slate-200" placeholder="e.g. Webhooks, Database, Billing" />
              </div>

              <div>
                <label class="block text-xs font-medium text-slate-400 mb-1">Initial Notes</label>
                <textarea rows="3" [(ngModel)]="newInitialNotes" data-testid="new-initial-notes" class="w-full bg-slate-950 border border-slate-700 rounded-lg p-3 text-slate-200" placeholder="Customer description of the error..."></textarea>
              </div>
            </div>

            <div class="p-4 border-t border-slate-800 flex justify-end gap-3">
              <button (click)="closeCreateModal()" class="px-4 py-2 bg-slate-800 text-slate-300 text-sm rounded-lg">Cancel</button>
              <button (click)="submitCreateTicket()" data-testid="submit-create-ticket-btn" class="px-4 py-2 bg-cyan-600 hover:bg-cyan-500 text-white font-semibold text-sm rounded-lg">
                Create & Monitor Ticket
              </button>
            </div>
          </div>
        </div>
      }
    </div>
  `
})
export class SupportEscalationDeskStudioComponent {
  public translation: TranslationService | null = null;

  public readonly tiers: { key: SupportTier; title: string; subtitle: string }[] = [
    { key: 'Tier1_Frontline', title: 'Tier 1 · Frontline', subtitle: 'Triage & First Response' },
    { key: 'Tier2_TechnicalSpecialist', title: 'Tier 2 · Tech Specialist', subtitle: 'Deep Telemetry & DB Traces' },
    { key: 'Tier3_EngineeringOps', title: 'Tier 3 · SWAT Desk', subtitle: 'Core DevOps & Hotfixes' },
    { key: 'ExecutiveOnCall', title: 'Executive On-Call', subtitle: 'VP Success & Churn Rescue' }
  ];

  // Primary Signals
  public readonly tickets = signal<SupportTicketRecord[]>(this.getInitialSeededTickets());
  public readonly searchQuery = signal<string>('');
  public readonly selectedPriorityFilter = signal<string>('ALL');
  public readonly selectedSentimentFilter = signal<string>('ALL');

  // Modal / Interaction Signals
  public readonly activeMacroTicket = signal<SupportTicketRecord | null>(null);
  public readonly currentMacro = signal<SuggestCannedMacroResponse | null>(null);
  public editableMacroResponse: string = '';

  public readonly escalatingTicket = signal<SupportTicketRecord | null>(null);
  public targetEscalateTier: SupportTier = 'Tier2_TechnicalSpecialist';
  public escalateReason: string = '';

  public readonly resolvingTicket = signal<SupportTicketRecord | null>(null);
  public resolveNotes: string = '';

  public readonly showCreateModal = signal<boolean>(false);
  public newSubject: string = '';
  public newCustomerName: string = '';
  public newCompanyName: string = '';
  public newPriority: TicketPriority = 'High';
  public newSentiment: CustomerSentiment = 'Frustrated';
  public newCategory: string = 'Webhooks';
  public newInitialNotes: string = '';

  // Computed KPIs
  public readonly activeTicketsCount = computed(() =>
    this.tickets().filter(t => t.state !== 'Resolved').length
  );

  public readonly warningSlaCount = computed(() => {
    const now = Date.now();
    return this.tickets().filter(t => {
      if (t.state === 'Resolved') return false;
      if (t.state === 'WarningSlaDueSoon') return true;
      const dueTime = new Date(t.slaDueAtUtc).getTime();
      return dueTime - now <= 30 * 60000;
    }).length;
  });

  public readonly escalatedCount = computed(() =>
    this.tickets().filter(t => t.state === 'EscalatedTier2' || t.state === 'EscalatedTier3').length
  );

  public readonly resolvedTodayCount = computed(() =>
    this.tickets().filter(t => t.state === 'Resolved').length
  );

  public readonly slaComplianceRate = computed(() => {
    const list = this.tickets();
    if (list.length === 0) return 100;
    const now = Date.now();
    const breached = list.filter(t => {
      const due = new Date(t.slaDueAtUtc).getTime();
      if (t.resolvedAtUtc) {
        return new Date(t.resolvedAtUtc).getTime() > due;
      }
      return now > due;
    }).length;
    return Math.round(((list.length - breached) / list.length) * 100);
  });

  constructor(@Optional() translationService?: TranslationService) {
    if (translationService) {
      this.translation = translationService;
    } else {
      try {
        this.translation = inject(TranslationService);
      } catch {
        this.translation = new TranslationService();
      }
    }
  }

  // Filtered tickets per tier
  public getTierTickets(tier: SupportTier): SupportTicketRecord[] {
    const query = this.searchQuery().toLowerCase().trim();
    const prio = this.selectedPriorityFilter();
    const sent = this.selectedSentimentFilter();

    return this.tickets().filter(t => {
      if (t.currentTier !== tier) return false;
      if (prio !== 'ALL' && t.priority !== prio) return false;
      if (sent !== 'ALL' && t.sentiment !== sent) return false;

      if (query) {
        const matchesNum = t.ticketNumber.toLowerCase().includes(query);
        const matchesSub = t.subject.toLowerCase().includes(query);
        const matchesCust = t.customerName.toLowerCase().includes(query);
        const matchesComp = t.companyName.toLowerCase().includes(query);
        if (!matchesNum && !matchesSub && !matchesCust && !matchesComp) return false;
      }
      return true;
    });
  }

  // SLA Countdown & Visual Helpers
  public getSlaCountdownText(ticket: SupportTicketRecord): string {
    if (ticket.state === 'Resolved') return 'Complied (Closed)';
    const due = new Date(ticket.slaDueAtUtc).getTime();
    const diffMs = due - Date.now();

    if (diffMs <= 0) {
      const overdueMins = Math.abs(Math.round(diffMs / 60000));
      return `BREACHED +${overdueMins}m`;
    }

    const mins = Math.round(diffMs / 60000);
    if (mins < 60) return `${mins}m remaining`;
    const hours = Math.floor(mins / 60);
    const remMins = mins % 60;
    return `${hours}h ${remMins}m left`;
  }

  public getSlaClockClass(ticket: SupportTicketRecord): string {
    if (ticket.state === 'Resolved') return 'text-emerald-400';
    const due = new Date(ticket.slaDueAtUtc).getTime();
    const diff = due - Date.now();
    if (diff <= 0) return 'text-rose-400 animate-pulse';
    if (diff <= 30 * 60000) return 'text-amber-400';
    return 'text-slate-300';
  }

  public getSlaProgressPercent(ticket: SupportTicketRecord): number {
    if (ticket.state === 'Resolved') return 100;
    const start = new Date(ticket.createdAtUtc).getTime();
    const end = new Date(ticket.slaDueAtUtc).getTime();
    const total = end - start;
    if (total <= 0) return 100;
    const elapsed = Date.now() - start;
    const pct = Math.min(100, Math.max(0, Math.round((elapsed / total) * 100)));
    return pct;
  }

  public getSlaProgressColor(ticket: SupportTicketRecord): string {
    if (ticket.state === 'Resolved') return 'bg-emerald-500';
    const due = new Date(ticket.slaDueAtUtc).getTime();
    const diff = due - Date.now();
    if (diff <= 0) return 'bg-rose-500';
    if (diff <= 30 * 60000) return 'bg-amber-400';
    return 'bg-cyan-500';
  }

  // Macro Copilot
  public openMacroCopilot(ticket: SupportTicketRecord): void {
    this.activeMacroTicket.set(ticket);
    const macro = this.generateMacroSuggestion(ticket);
    this.currentMacro.set(macro);
    this.editableMacroResponse = macro.recommendedResponse;
  }

  public closeMacroCopilot(): void {
    this.activeMacroTicket.set(null);
    this.currentMacro.set(null);
    this.editableMacroResponse = '';
  }

  public applyMacroToTicket(ticket: SupportTicketRecord): void {
    // Updates ticket log and resolution notes with applied macro
    const now = new Date().toISOString();
    const updated = this.tickets().map(t => {
      if (t.ticketId === ticket.ticketId) {
        const log: TicketEscalationLogRecord = {
          logId: `log-${Date.now()}`,
          ticketId: t.ticketId,
          fromTier: t.currentTier,
          toTier: t.currentTier,
          reason: `AI Macro applied: ${this.currentMacro()?.macroTitle}`,
          triggeredBy: 'AI Copilot Dispatcher',
          timestampUtc: now
        };
        return {
          ...t,
          resolutionNotes: this.editableMacroResponse,
          escalationLogs: [...t.escalationLogs, log]
        };
      }
      return t;
    });

    this.tickets.set(updated);
    this.closeMacroCopilot();
  }

  // Escalate Actions
  public openEscalateModal(ticket: SupportTicketRecord): void {
    this.escalatingTicket.set(ticket);
    this.targetEscalateTier = 'Tier2_TechnicalSpecialist';
    this.escalateReason = '';
  }

  public closeEscalateModal(): void {
    this.escalatingTicket.set(null);
    this.escalateReason = '';
  }

  public confirmEscalate(ticket: SupportTicketRecord): void {
    const now = new Date().toISOString();
    const target = this.targetEscalateTier;
    const newState: EscalationState = target === 'Tier2_TechnicalSpecialist'
      ? 'EscalatedTier2'
      : 'EscalatedTier3';

    const newAgent = target === 'Tier2_TechnicalSpecialist'
      ? 'Zaid Al-Najjar (Tier 2 Technical Specialist)'
      : target === 'Tier3_EngineeringOps'
      ? 'DevOps Core SWAT Desk'
      : 'Executive Success Desk';

    const log: TicketEscalationLogRecord = {
      logId: `log-${Date.now()}`,
      ticketId: ticket.ticketId,
      fromTier: ticket.currentTier,
      toTier: target,
      reason: this.escalateReason || 'Autonomous threshold escalation dispatch',
      triggeredBy: 'Escalation Desk Studio',
      timestampUtc: now
    };

    const updated = this.tickets().map(t => {
      if (t.ticketId === ticket.ticketId) {
        return {
          ...t,
          currentTier: target,
          state: newState,
          assignedAgent: newAgent,
          escalationLogs: [...t.escalationLogs, log],
          tags: [...new Set([...t.tags, `Escalated-${target}`])]
        };
      }
      return t;
    });

    this.tickets.set(updated);
    this.closeEscalateModal();
  }

  // Resolve Actions
  public openResolveModal(ticket: SupportTicketRecord): void {
    this.resolvingTicket.set(ticket);
    this.resolveNotes = '';
  }

  public closeResolveModal(): void {
    this.resolvingTicket.set(null);
    this.resolveNotes = '';
  }

  public confirmResolve(ticket: SupportTicketRecord): void {
    const now = new Date().toISOString();
    const log: TicketEscalationLogRecord = {
      logId: `log-${Date.now()}`,
      ticketId: ticket.ticketId,
      fromTier: ticket.currentTier,
      toTier: ticket.currentTier,
      reason: `Ticket resolved: ${this.resolveNotes || 'Verified and closed'}`,
      triggeredBy: 'Support Dispatch Lead',
      timestampUtc: now
    };

    const updated = this.tickets().map(t => {
      if (t.ticketId === ticket.ticketId) {
        return {
          ...t,
          state: 'Resolved' as EscalationState,
          resolvedAtUtc: now,
          resolutionNotes: this.resolveNotes || 'Resolved successfully',
          escalationLogs: [...t.escalationLogs, log],
          tags: [...new Set([...t.tags, 'Resolved'])]
        };
      }
      return t;
    });

    this.tickets.set(updated);
    this.closeResolveModal();
  }

  // Create Ticket
  public openCreateModal(): void {
    this.newSubject = '';
    this.newCustomerName = '';
    this.newCompanyName = '';
    this.newPriority = 'High';
    this.newSentiment = 'Frustrated';
    this.newCategory = 'Webhooks';
    this.newInitialNotes = '';
    this.showCreateModal.set(true);
  }

  public closeCreateModal(): void {
    this.showCreateModal.set(false);
  }

  public submitCreateTicket(): void {
    if (!this.newSubject || !this.newCustomerName) return;

    const now = new Date();
    const slaMins = this.newPriority === 'Urgent' ? 30 : this.newPriority === 'High' ? 60 : 240;
    const due = new Date(now.getTime() + slaMins * 60000);
    const tid = `ticket-${Date.now()}`;
    const tNum = `TICK-2026-${Math.floor(1000 + Math.random() * 9000)}`;

    const newTicket: SupportTicketRecord = {
      ticketId: tid,
      tenantId: '11111111-1111-1111-1111-111111111111',
      ticketNumber: tNum,
      subject: this.newSubject,
      customerName: this.newCustomerName,
      customerEmail: `${this.newCustomerName.toLowerCase().replace(/\s+/g, '.')}@clientcorp.sa`,
      companyName: this.newCompanyName || 'Saudi Enterprise Client',
      priority: this.newPriority,
      currentTier: 'Tier1_Frontline',
      state: this.newPriority === 'Urgent' ? 'WarningSlaDueSoon' : 'Normal',
      sentiment: this.newSentiment,
      assignedAgent: 'Hala Al-Mansoor (Frontline Desk)',
      category: this.newCategory || 'General',
      createdAtUtc: now.toISOString(),
      slaDueAtUtc: due.toISOString(),
      firstResponseAtUtc: null,
      resolvedAtUtc: null,
      resolutionNotes: null,
      escalationLogs: [
        {
          logId: `log-${Date.now()}`,
          ticketId: tid,
          fromTier: 'Tier1_Frontline',
          toTier: 'Tier1_Frontline',
          reason: `Ticket created: ${this.newInitialNotes || 'Initial inbound report'}`,
          triggeredBy: this.newCustomerName,
          timestampUtc: now.toISOString()
        }
      ],
      tags: ['Inbound', this.newCategory, this.newPriority]
    };

    this.tickets.set([newTicket, ...this.tickets()]);
    this.closeCreateModal();
  }

  // Radar evaluation trigger
  public triggerAutomatedSlaEvaluation(): void {
    const now = Date.now();
    const updated = this.tickets().map(t => {
      if (t.state === 'Resolved') return t;
      const due = new Date(t.slaDueAtUtc).getTime();
      if (due - now <= 30 * 60000 && t.state === 'Normal') {
        return { ...t, state: 'WarningSlaDueSoon' as EscalationState };
      }
      return t;
    });
    this.tickets.set(updated);
  }

  public resetToDefaults(): void {
    this.tickets.set(this.getInitialSeededTickets());
    this.searchQuery.set('');
    this.selectedPriorityFilter.set('ALL');
    this.selectedSentimentFilter.set('ALL');
  }

  // Generate Macro Helper
  private generateMacroSuggestion(ticket: SupportTicketRecord): SuggestCannedMacroResponse {
    if (ticket.sentiment === 'CriticalChurnRisk') {
      return {
        ticketId: ticket.ticketId,
        macroTitle: 'Executive Incident Hotline & High-Priority Bridge',
        recommendedResponse: `Dear ${ticket.customerName},\n\nWe recognize the critical operational impact on ${ticket.companyName} regarding '${ticket.subject}'. This case has been escalated directly to our Tier 3 Engineering SWAT and Executive Command Desk. Our team is actively investigating with root-cause checkpoints scheduled every 20 minutes until complete restoration.`,
        targetSentiment: ticket.sentiment,
        actionSteps: [
          'Establish live bridge with Customer Success Executive',
          'Engage SRE On-Call lead for diagnostics',
          'Issue proactive SLA service-credit memo'
        ],
        confidenceScore: 0.98
      };
    } else if (ticket.sentiment === 'Frustrated') {
      return {
        ticketId: ticket.ticketId,
        macroTitle: 'Specialist Deep-Dive & Root-Cause Assurance',
        recommendedResponse: `Hello ${ticket.customerName},\n\nThank you for reaching out. We apologize for the friction caused by '${ticket.subject}'. A dedicated Tier 2 Technical Specialist has taken full ownership of your ticket and is reviewing system telemetry and error logs right now. We will provide our next diagnostic update within 45 minutes.`,
        targetSentiment: ticket.sentiment,
        actionSteps: [
          'Query telemetry cluster for correlated trace spans',
          'Verify tenant webhook retry queues',
          'Deliver interim update within 45 minutes'
        ],
        confidenceScore: 0.94
      };
    } else {
      return {
        ticketId: ticket.ticketId,
        macroTitle: 'Standard Resolution Walkthrough & Verification',
        recommendedResponse: `Hello ${ticket.customerName},\n\nThank you for contacting SGrowing Support. We have verified your request for '${ticket.subject}'. Our support team has analyzed the configuration and prepared actionable resolution steps for your account at ${ticket.companyName}.`,
        targetSentiment: ticket.sentiment,
        actionSteps: [
          'Walk through settings verification steps',
          'Validate API credentials and tenant permissions',
          'Confirm customer resolution and solicit CSAT feedback'
        ],
        confidenceScore: 0.91
      };
    }
  }

  private getInitialSeededTickets(): SupportTicketRecord[] {
    const now = Date.now();
    return [
      {
        ticketId: '10000000-0000-0000-0000-000000000001',
        tenantId: '11111111-1111-1111-1111-111111111111',
        ticketNumber: 'TICK-2026-1041',
        subject: 'Payment Webhook Delivery Delay in Jeddah Hub',
        customerName: 'Tariq Al-Otaibi',
        customerEmail: 'tariq@redsealogistics.sa',
        companyName: 'RedSea Logistics KSA',
        priority: 'High',
        currentTier: 'Tier1_Frontline',
        state: 'WarningSlaDueSoon',
        sentiment: 'Frustrated',
        assignedAgent: 'Hala Al-Mansoor (Frontline Desk)',
        category: 'Webhooks',
        createdAtUtc: new Date(now - 45 * 60000).toISOString(),
        slaDueAtUtc: new Date(now + 15 * 60000).toISOString(),
        firstResponseAtUtc: new Date(now - 35 * 60000).toISOString(),
        resolvedAtUtc: null,
        resolutionNotes: null,
        escalationLogs: [
          {
            logId: 'seed-log-1',
            ticketId: '10000000-0000-0000-0000-000000000001',
            fromTier: 'Tier1_Frontline',
            toTier: 'Tier1_Frontline',
            reason: 'Ticket logged via customer portal. SLA clock running.',
            triggeredBy: 'System Ingestion',
            timestampUtc: new Date(now - 45 * 60000).toISOString()
          }
        ],
        tags: ['Inbound', 'Webhooks', 'High', 'KSA-West']
      },
      {
        ticketId: '10000000-0000-0000-0000-000000000002',
        tenantId: '11111111-1111-1111-1111-111111111111',
        ticketNumber: 'TICK-2026-1042',
        subject: 'Database Replication Timeout on Bulk Order Ingest',
        customerName: 'Khalid Al-Harbi',
        customerEmail: 'khalid.harbi@rajhicommerce.com',
        companyName: 'Al-Rajhi Retail Group',
        priority: 'Urgent',
        currentTier: 'Tier2_TechnicalSpecialist',
        state: 'EscalatedTier2',
        sentiment: 'Frustrated',
        assignedAgent: 'Zaid Al-Najjar (Tier 2 Technical Specialist)',
        category: 'Database',
        createdAtUtc: new Date(now - 90 * 60000).toISOString(),
        slaDueAtUtc: new Date(now - 10 * 60000).toISOString(), // Breached
        firstResponseAtUtc: new Date(now - 70 * 60000).toISOString(),
        resolvedAtUtc: null,
        resolutionNotes: null,
        escalationLogs: [
          {
            logId: 'seed-log-2',
            ticketId: '10000000-0000-0000-0000-000000000002',
            fromTier: 'Tier1_Frontline',
            toTier: 'Tier1_Frontline',
            reason: 'Initial inquiry received regarding API 504 timeouts.',
            triggeredBy: 'Nouf Al-Qurashi',
            timestampUtc: new Date(now - 90 * 60000).toISOString()
          },
          {
            logId: 'seed-log-3',
            ticketId: '10000000-0000-0000-0000-000000000002',
            fromTier: 'Tier1_Frontline',
            toTier: 'Tier2_TechnicalSpecialist',
            reason: 'Query plan degradation detected on replica cluster. Escalated to Tier 2 specialist.',
            triggeredBy: 'Nouf Al-Qurashi',
            timestampUtc: new Date(now - 50 * 60000).toISOString()
          }
        ],
        tags: ['Inbound', 'Database', 'Urgent', 'Escalated-Tier2']
      },
      {
        ticketId: '10000000-0000-0000-0000-000000000003',
        tenantId: '11111111-1111-1111-1111-111111111111',
        ticketNumber: 'TICK-2026-1043',
        subject: 'Zero-Day Checkout Auth Exception for Enterprise Tenants',
        customerName: 'Dr. Amina Al-Zahrani',
        customerEmail: 'amina@saudipostcloud.gov.sa',
        companyName: 'Saudi Post Cloud Infrastructure',
        priority: 'Urgent',
        currentTier: 'Tier3_EngineeringOps',
        state: 'EscalatedTier3',
        sentiment: 'CriticalChurnRisk',
        assignedAgent: 'DevOps Core SWAT Desk',
        category: 'Security',
        createdAtUtc: new Date(now - 180 * 60000).toISOString(),
        slaDueAtUtc: new Date(now - 60 * 60000).toISOString(), // Breached
        firstResponseAtUtc: new Date(now - 160 * 60000).toISOString(),
        resolvedAtUtc: null,
        resolutionNotes: null,
        escalationLogs: [
          {
            logId: 'seed-log-4',
            ticketId: '10000000-0000-0000-0000-000000000003',
            fromTier: 'Tier1_Frontline',
            toTier: 'Tier2_TechnicalSpecialist',
            reason: 'Customer reported token signature validation failure across distributed nodes.',
            triggeredBy: 'Hala Al-Mansoor',
            timestampUtc: new Date(now - 140 * 60000).toISOString()
          },
          {
            logId: 'seed-log-5',
            ticketId: '10000000-0000-0000-0000-000000000003',
            fromTier: 'Tier2_TechnicalSpecialist',
            toTier: 'Tier3_EngineeringOps',
            reason: 'Security key cache synchronization bug identified. Escalated to SWAT for hotfix deployment.',
            triggeredBy: 'Zaid Al-Najjar',
            timestampUtc: new Date(now - 100 * 60000).toISOString()
          }
        ],
        tags: ['Inbound', 'Security', 'Urgent', 'Critical-Risk', 'Escalated-Tier3']
      },
      {
        ticketId: '10000000-0000-0000-0000-000000000004',
        tenantId: '11111111-1111-1111-1111-111111111111',
        ticketNumber: 'TICK-2026-1044',
        subject: 'Custom Domain DNS Validation Guidance Request',
        customerName: 'Sami Bakhsh',
        customerEmail: 'sami@riyadhretail.com',
        companyName: 'Riyadh Retail Holdings',
        priority: 'Low',
        currentTier: 'Tier1_Frontline',
        state: 'Normal',
        sentiment: 'Positive',
        assignedAgent: 'Nouf Al-Qurashi (Frontline Desk)',
        category: 'Configuration',
        createdAtUtc: new Date(now - 20 * 60000).toISOString(),
        slaDueAtUtc: new Date(now + 23 * 3600000).toISOString(),
        firstResponseAtUtc: new Date(now - 15 * 60000).toISOString(),
        resolvedAtUtc: null,
        resolutionNotes: null,
        escalationLogs: [
          {
            logId: 'seed-log-6',
            ticketId: '10000000-0000-0000-0000-000000000004',
            fromTier: 'Tier1_Frontline',
            toTier: 'Tier1_Frontline',
            reason: 'DNS record validation steps provided to client administrator.',
            triggeredBy: 'Nouf Al-Qurashi',
            timestampUtc: new Date(now - 20 * 60000).toISOString()
          }
        ],
        tags: ['Inbound', 'Configuration', 'Low']
      },
      {
        ticketId: '10000000-0000-0000-0000-000000000005',
        tenantId: '11111111-1111-1111-1111-111111111111',
        ticketNumber: 'TICK-2026-1040',
        subject: 'Arabic Fiscal Invoice RTL Formatting Fixed',
        customerName: 'Maryam Al-Sabah',
        customerEmail: 'maryam@gulftrading.com',
        companyName: 'Gulf Trading Enterprises',
        priority: 'Medium',
        currentTier: 'Tier1_Frontline',
        state: 'Resolved',
        sentiment: 'Positive',
        assignedAgent: 'Hala Al-Mansoor (Frontline Desk)',
        category: 'Billing',
        createdAtUtc: new Date(now - 6 * 3600000).toISOString(),
        slaDueAtUtc: new Date(now - 2 * 3600000).toISOString(),
        firstResponseAtUtc: new Date(now - 5 * 3600000).toISOString(),
        resolvedAtUtc: new Date(now - 1 * 3600000).toISOString(),
        resolutionNotes: 'Adjusted CSS stylesheet direction to RTL and regenerated ZATCA compliant invoice QR code.',
        escalationLogs: [
          {
            logId: 'seed-log-7',
            ticketId: '10000000-0000-0000-0000-000000000005',
            fromTier: 'Tier1_Frontline',
            toTier: 'Tier1_Frontline',
            reason: 'Report of inverted table columns in generated PDF invoices.',
            triggeredBy: 'System Ingestion',
            timestampUtc: new Date(now - 6 * 3600000).toISOString()
          },
          {
            logId: 'seed-log-8',
            ticketId: '10000000-0000-0000-0000-000000000005',
            fromTier: 'Tier1_Frontline',
            toTier: 'Tier1_Frontline',
            reason: 'Ticket marked resolved: Adjustments deployed and validated with customer.',
            triggeredBy: 'Hala Al-Mansoor',
            timestampUtc: new Date(now - 1 * 3600000).toISOString()
          }
        ],
        tags: ['Inbound', 'Billing', 'ZATCA', 'Resolved']
      }
    ];
  }
}
