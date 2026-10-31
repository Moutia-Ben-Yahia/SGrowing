import { Component, OnInit, OnDestroy, signal, computed, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { HttpClient } from '@angular/common/http';
import { RouterLink } from '@angular/router';

export enum AgentPresenceState {
  Online = 'Online',
  ActiveOnTicket = 'ActiveOnTicket',
  ViewingTicket = 'ViewingTicket',
  ComposingReply = 'ComposingReply',
  Idle = 'Idle',
  Offline = 'Offline'
}

export enum CollisionLockState {
  Unlocked = 'Unlocked',
  SoftViewing = 'SoftViewing',
  HardComposingLock = 'HardComposingLock'
}

export interface AgentPresenceSessionDto {
  agentId: string;
  agentName: string;
  agentEmail: string;
  role: string;
  state: AgentPresenceState;
  currentTicketId?: string | null;
  lastHeartbeatUtc: string;
  stateChangedUtc: string;
  activeWorkload: number;
  isTyping: boolean;
  draftSnippet?: string | null;
}

export interface TicketViewerInfoDto {
  agentId: string;
  agentName: string;
  viewingSinceUtc: string;
  lastHeartbeatUtc: string;
  state: AgentPresenceState;
  isTyping: boolean;
  draftSnippet?: string | null;
}

export interface TypingBeaconDto {
  ticketId: string;
  agentId: string;
  agentName: string;
  isTyping: boolean;
  draftSnippetPreview?: string | null;
  startedTypingAtUtc: string;
  lastTypingPingUtc: string;
  leaseRemainingSeconds: number;
}

export interface TicketItemDto {
  ticketId: string;
  ticketTitle: string;
  customerName: string;
  priority: string;
  assignedAgentId?: string | null;
  assignedAgentName?: string | null;
  customerMessagePending: boolean;
  lockState: CollisionLockState;
  lockedByAgentId?: string | null;
  lockedByAgentName?: string | null;
  lockAcquiredAtUtc?: string | null;
  lockExpiresAtUtc?: string | null;
  lockDraftSnippet?: string | null;
}

export interface TicketCollisionStatusDto {
  ticketId: string;
  ticketTitle: string;
  lockState: CollisionLockState;
  lockedByAgentId?: string | null;
  lockedByAgentName?: string | null;
  lockAcquiredAtUtc?: string | null;
  lockExpiresAtUtc?: string | null;
  remainingLeaseSeconds: number;
  isBlocked: boolean;
  warningMessage?: string | null;
  viewers: TicketViewerInfoDto[];
  typingBeacon?: TypingBeaconDto | null;
}

export interface TicketHandoffEventDto {
  eventId: string;
  ticketId: string;
  ticketTitle: string;
  customerName: string;
  previousAgentId?: string | null;
  previousAgentName: string;
  newAgentId: string;
  newAgentName: string;
  reason: string;
  timestampUtc: string;
}

export interface PresenceMetricsDto {
  totalOnlineAgents: number;
  activeTicketsUnderCollisionLock: number;
  totalPreventedCollisions: number;
  totalAutoReassignments: number;
  averageReplyTimeSeconds: number;
  activeViewersCount: number;
  totalMonitoredTickets: number;
  generatedAtUtc: string;
}

@Component({
  selector: 'app-agent-presence-cockpit',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterLink],
  template: `
    <div dir="ltr" class="min-h-screen bg-slate-950 text-slate-100 font-sans p-4 md:p-6 space-y-6">
      <!-- HEADER & TOPBAR -->
      <header class="flex flex-col md:flex-row md:items-center justify-between gap-4 border-b border-slate-800/80 pb-5">
        <div class="space-y-1">
          <div class="flex items-center gap-2">
            <a routerLink="/" class="text-xs text-slate-400 hover:text-white transition flex items-center gap-1">
              <svg class="w-3.5 h-3.5" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M10 19l-7-7m0 0l7-7m-7 7h18"/>
              </svg>
              Omnichannel Hub
            </a>
            <span class="text-slate-700">•</span>
            <span class="inline-flex items-center gap-1.5 px-2.5 py-0.5 rounded-full text-xs font-medium bg-emerald-500/10 text-emerald-400 border border-emerald-500/20">
              <span class="w-1.5 h-1.5 rounded-full bg-emerald-400 animate-pulse"></span>
              Live Mesh Active
            </span>
            <span class="text-xs font-mono text-slate-400">15s Lease Renewal</span>
          </div>
          <h1 class="text-2xl font-bold tracking-tight text-white flex items-center gap-2">
            <span>🛡️</span> Collision Prevention & Presence Mesh
          </h1>
          <p class="text-xs text-slate-400 max-w-2xl">
            Real-time multi-agent concurrency protection: prevents duplicate customer replies with hard composing locks, tracks ticket viewers, and executes automated handoffs on agent abandonment.
          </p>
        </div>

        <!-- ACTIONS BAR -->
        <div class="flex flex-wrap items-center gap-2.5">
          <button
            (click)="triggerAutoHandoffSweep()"
            [disabled]="isSweepLoading()"
            class="inline-flex items-center gap-2 px-3.5 py-2 rounded-lg text-xs font-semibold bg-violet-600 hover:bg-violet-500 text-white transition shadow-sm disabled:opacity-50">
            <svg class="w-4 h-4" [class.animate-spin]="isSweepLoading()" fill="none" stroke="currentColor" viewBox="0 0 24 24">
              <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M4 4v5h.582m15.356 2A8.001 8.001 0 004.582 9m0 0H9m11 11v-5h-.581m0 0a8.003 8.003 0 01-15.357-2m15.357 2H15"/>
            </svg>
            <span>Run Abandonment Sweep</span>
          </button>

          <button
            (click)="refreshAllData()"
            class="inline-flex items-center gap-1.5 px-3 py-2 rounded-lg text-xs font-medium bg-slate-800 hover:bg-slate-700 text-slate-200 border border-slate-700 transition">
            <svg class="w-3.5 h-3.5" fill="none" stroke="currentColor" viewBox="0 0 24 24">
              <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M4 4v5h.582m15.356 2A8.001 8.001 0 004.582 9m0 0H9m11 11v-5h-.581m0 0a8.003 8.003 0 01-15.357-2m15.357 2H15"/>
            </svg>
            Refresh
          </button>

          <button
            (click)="resetDemoData()"
            class="inline-flex items-center gap-1.5 px-3 py-2 rounded-lg text-xs font-medium bg-rose-500/10 hover:bg-rose-500/20 text-rose-300 border border-rose-500/30 transition">
            Reset Demo
          </button>
        </div>
      </header>

      <!-- METRICS DASHBOARD TILES -->
      <section class="grid grid-cols-2 md:grid-cols-5 gap-3.5">
        <div class="bg-slate-900/70 border border-slate-800 rounded-xl p-3.5 space-y-1">
          <div class="flex items-center justify-between text-slate-400 text-xs">
            <span>Online Operators</span>
            <span class="w-2 h-2 rounded-full bg-emerald-400 animate-ping"></span>
          </div>
          <div class="text-2xl font-bold text-emerald-400 font-mono">{{ metrics()?.totalOnlineAgents ?? 3 }}</div>
          <div class="text-[10px] text-slate-400">Connected to presence mesh</div>
        </div>

        <div class="bg-slate-900/70 border border-slate-800 rounded-xl p-3.5 space-y-1">
          <div class="flex items-center justify-between text-slate-400 text-xs">
            <span>Active Write Locks</span>
            <span class="text-violet-400">🔒</span>
          </div>
          <div class="text-2xl font-bold text-violet-400 font-mono">{{ metrics()?.activeTicketsUnderCollisionLock ?? 1 }}</div>
          <div class="text-[10px] text-slate-400">Draft inputs locked by agent</div>
        </div>

        <div class="bg-slate-900/70 border border-slate-800 rounded-xl p-3.5 space-y-1">
          <div class="flex items-center justify-between text-slate-400 text-xs">
            <span>Collisions Prevented</span>
            <span class="text-amber-400">🛡️</span>
          </div>
          <div class="text-2xl font-bold text-amber-400 font-mono">{{ metrics()?.totalPreventedCollisions ?? 14 }}</div>
          <div class="text-[10px] text-slate-400">Duplicate customer replies blocked</div>
        </div>

        <div class="bg-slate-900/70 border border-slate-800 rounded-xl p-3.5 space-y-1">
          <div class="flex items-center justify-between text-slate-400 text-xs">
            <span>Auto-Reassignments</span>
            <span class="text-blue-400">⚡</span>
          </div>
          <div class="text-2xl font-bold text-blue-400 font-mono">{{ metrics()?.totalAutoReassignments ?? 6 }}</div>
          <div class="text-[10px] text-slate-400">Rescued from idle/offline ops</div>
        </div>

        <div class="bg-slate-900/70 border border-slate-800 rounded-xl p-3.5 space-y-1 col-span-2 md:col-span-1">
          <div class="flex items-center justify-between text-slate-400 text-xs">
            <span>Avg Reply Duration</span>
            <span class="text-cyan-400">⏱️</span>
          </div>
          <div class="text-2xl font-bold text-cyan-400 font-mono">{{ (metrics()?.averageReplyTimeSeconds ?? 42.5) | number:'1.1-1' }}s</div>
          <div class="text-[10px] text-slate-400">Across completed resolutions</div>
        </div>
      </section>

      <!-- MAIN STAGE: COLLISION SIMULATION & DUAL AGENT SPLIT VIEW -->
      <section class="bg-slate-900/70 border border-slate-800 rounded-2xl p-5 space-y-5">
        <div class="flex flex-col sm:flex-row sm:items-center justify-between gap-3 border-b border-slate-800/80 pb-4">
          <div class="space-y-0.5">
            <h2 class="text-base font-semibold text-white flex items-center gap-2">
              <span>⚡</span> Ticket Collision Simulation Sandbox
            </h2>
            <p class="text-xs text-slate-400">
              Select a conversation ticket below and experience dual-agent concurrency locking in real time.
            </p>
          </div>

          <!-- TICKET SELECTOR -->
          <div class="flex items-center gap-2">
            <label class="text-xs text-slate-400">Selected Ticket:</label>
            <select
              [ngModel]="selectedTicketId()"
              (ngModelChange)="onSelectTicket($event)"
              class="bg-slate-950 border border-slate-700 rounded-lg px-3 py-1.5 text-xs text-white focus:outline-none focus:border-violet-500">
              @for (t of tickets(); track t.ticketId) {
                <option [value]="t.ticketId">
                  {{ t.ticketTitle }} ({{ t.priority }})
                </option>
              }
            </select>
          </div>
        </div>

        <!-- DUAL OPERATOR VIEWPORT -->
        <div class="grid grid-cols-1 md:grid-cols-2 gap-5">
          <!-- OPERATOR 1 CARD (Simulating Operator A: Alex Vance) -->
          <div class="bg-slate-950/90 border rounded-xl p-4 space-y-3.5 transition"
            [ngClass]="isOp1HoldingLock() ? 'border-violet-500/50 shadow-lg shadow-violet-950/30' : 'border-slate-800'">
            <div class="flex items-center justify-between">
              <div class="flex items-center gap-2.5">
                <div class="w-8 h-8 rounded-full bg-violet-600/30 border border-violet-500/40 text-violet-300 font-bold text-xs flex items-center justify-center">
                  AV
                </div>
                <div>
                  <div class="text-xs font-semibold text-white flex items-center gap-1.5">
                    Operator 1: Alex Vance
                    <span class="text-[10px] text-slate-400 font-normal">(Senior Support)</span>
                  </div>
                  <div class="text-[10px] text-slate-400">alex.vance&#64;sgrowing.io</div>
                </div>
              </div>

              <div class="flex items-center gap-2">
                @if (isOp1HoldingLock()) {
                  <span class="inline-flex items-center gap-1 px-2 py-0.5 rounded text-[10px] font-semibold bg-violet-500/20 text-violet-300 border border-violet-500/40 animate-pulse">
                    <span>🔒</span> Holding Write Lock
                  </span>
                } @else {
                  <span class="inline-flex items-center gap-1 px-2 py-0.5 rounded text-[10px] font-medium bg-slate-800 text-slate-300">
                    Viewing Ticket
                  </span>
                }
              </div>
            </div>

            <!-- OP 1 REPLY BOX -->
            <div class="space-y-1.5">
              <div class="flex items-center justify-between text-[11px] text-slate-400">
                <label>Customer Reply Draft</label>
                @if (isOp1HoldingLock()) {
                  <span class="text-violet-400 font-mono text-[10px]">
                    Typing Beacon Active • {{ simStatus()?.remainingLeaseSeconds ?? 60 }}s lease remaining
                  </span>
                }
              </div>
              <textarea
                rows="4"
                [ngModel]="op1Draft()"
                (ngModelChange)="onOp1Typing($event)"
                placeholder="Type customer reply as Alex Vance... (triggers real-time typing beacon and draft lease)"
                class="w-full bg-slate-900 border border-slate-700/80 rounded-lg p-2.5 text-xs text-slate-200 placeholder-slate-600 focus:outline-none focus:border-violet-500 resize-none"></textarea>
            </div>

            <!-- OP 1 CONTROLS -->
            <div class="flex flex-wrap items-center gap-2 pt-1">
              <button
                (click)="op1AcquireLock()"
                [disabled]="isOp1HoldingLock()"
                class="px-3 py-1.5 rounded-lg text-xs font-semibold bg-violet-600 hover:bg-violet-500 text-white transition disabled:opacity-40 disabled:cursor-not-allowed">
                Acquire Write Lock
              </button>

              <button
                (click)="op1SubmitReply()"
                [disabled]="!isOp1HoldingLock()"
                class="px-3 py-1.5 rounded-lg text-xs font-semibold bg-emerald-600 hover:bg-emerald-500 text-white transition disabled:opacity-40 disabled:cursor-not-allowed">
                Submit Reply & Free Lock
              </button>

              <button
                (click)="op1NavigateAway()"
                [disabled]="!isOp1HoldingLock()"
                class="px-3 py-1.5 rounded-lg text-xs font-medium bg-slate-800 hover:bg-slate-700 text-slate-300 transition disabled:opacity-40 disabled:cursor-not-allowed">
                Navigate Away
              </button>
            </div>
          </div>

          <!-- OPERATOR 2 CARD (Simulating Operator B: Elena Rostova) -->
          <div class="bg-slate-950/90 border rounded-xl p-4 space-y-3.5 transition"
            [ngClass]="op2BlockedWarning() ? 'border-amber-500/50 shadow-lg shadow-amber-950/30' : 'border-slate-800'">
            <div class="flex items-center justify-between">
              <div class="flex items-center gap-2.5">
                <div class="w-8 h-8 rounded-full bg-blue-600/30 border border-blue-500/40 text-blue-300 font-bold text-xs flex items-center justify-center">
                  ER
                </div>
                <div>
                  <div class="text-xs font-semibold text-white flex items-center gap-1.5">
                    Operator 2: Elena Rostova
                    <span class="text-[10px] text-slate-400 font-normal">(Success Lead)</span>
                  </div>
                  <div class="text-[10px] text-slate-400">elena.rostova&#64;sgrowing.io</div>
                </div>
              </div>

              <div class="flex items-center gap-2">
                @if (isOp1HoldingLock()) {
                  <span class="inline-flex items-center gap-1 px-2 py-0.5 rounded text-[10px] font-semibold bg-amber-500/20 text-amber-300 border border-amber-500/30">
                    <span>🛡️</span> Collision Blocked
                  </span>
                } @else {
                  <span class="inline-flex items-center gap-1 px-2 py-0.5 rounded text-[10px] font-medium bg-slate-800 text-slate-300">
                    Ready to Reply
                  </span>
                }
              </div>
            </div>

            <!-- COLLISION SHIELD WARNING BANNER -->
            @if (isOp1HoldingLock()) {
              <div class="bg-amber-500/10 border border-amber-500/30 rounded-lg p-2.5 flex items-start gap-2.5 text-xs text-amber-200 animate-fadeIn">
                <span class="text-base leading-none">⚠️</span>
                <div class="space-y-0.5 flex-1">
                  <div class="font-semibold text-amber-300">Collision Shield Active</div>
                  <div class="text-[11px] text-amber-200/90">
                    Operator 1 (Alex Vance) is actively replying to this customer. Input is locked to prevent duplicate customer responses.
                  </div>
                </div>
              </div>
            }

            <!-- OP 2 REPLY BOX (LOCKED WHEN OP 1 WRITES) -->
            <div class="space-y-1.5">
              <div class="flex items-center justify-between text-[11px] text-slate-400">
                <label>Customer Reply Draft</label>
                @if (isOp1HoldingLock()) {
                  <span class="text-rose-400 text-[10px] font-mono">🔒 Input Disabled</span>
                }
              </div>
              <textarea
                rows="4"
                [disabled]="isOp1HoldingLock()"
                [ngModel]="op2Draft()"
                (ngModelChange)="op2Draft.set($event)"
                placeholder="Elena Rostova draft input (automatically disabled when Operator 1 acquires lock)..."
                class="w-full bg-slate-900 border border-slate-700/80 rounded-lg p-2.5 text-xs text-slate-200 placeholder-slate-600 focus:outline-none focus:border-blue-500 resize-none disabled:opacity-40 disabled:cursor-not-allowed"></textarea>
            </div>

            <!-- OP 2 CONTROLS -->
            <div class="flex flex-wrap items-center gap-2 pt-1">
              <button
                (click)="op2AttemptWrite()"
                class="px-3 py-1.5 rounded-lg text-xs font-semibold bg-blue-600 hover:bg-blue-500 text-white transition">
                Attempt to Reply (Test Collision Block)
              </button>

              <button
                (click)="op2ForceAdminOverride()"
                [disabled]="!isOp1HoldingLock()"
                class="px-3 py-1.5 rounded-lg text-xs font-semibold bg-rose-600/80 hover:bg-rose-600 text-white transition disabled:opacity-40 disabled:cursor-not-allowed">
                Supervisor Override Lock
              </button>
            </div>
          </div>
        </div>

        <!-- TYPING BEACON SNIPPET BAR -->
        @if (simStatus()?.typingBeacon?.isTyping) {
          <div class="bg-violet-950/40 border border-violet-800/40 rounded-xl p-3 flex items-center justify-between text-xs text-violet-200">
            <div class="flex items-center gap-2">
              <span class="flex h-2 w-2 relative">
                <span class="animate-ping absolute inline-flex h-full w-full rounded-full bg-violet-400 opacity-75"></span>
                <span class="relative inline-flex rounded-full h-2 w-2 bg-violet-500"></span>
              </span>
              <span class="font-medium text-white">{{ simStatus()?.typingBeacon?.agentName }} is typing live draft:</span>
              <span class="italic text-slate-300 font-mono">"{{ simStatus()?.typingBeacon?.draftSnippetPreview }}"</span>
            </div>
            <div class="text-[11px] font-mono text-violet-400">
              Lease: {{ simStatus()?.typingBeacon?.leaseRemainingSeconds }}s
            </div>
          </div>
        }
      </section>

      <!-- TWO-COLUMN LAYOUT: TEAM ROSTER & ACTIVE LOCKS MONITOR -->
      <div class="grid grid-cols-1 lg:grid-cols-2 gap-6">
        <!-- TEAM PRESENCE ROSTER -->
        <section class="bg-slate-900/70 border border-slate-800 rounded-2xl p-5 space-y-4">
          <div class="flex items-center justify-between border-b border-slate-800/80 pb-3">
            <div>
              <h2 class="text-sm font-semibold text-white flex items-center gap-2">
                <span>👥</span> Real-Time Operator Roster
              </h2>
              <p class="text-xs text-slate-400">Tracked with 15-second heartbeat leases & 30s/60s auto-expiry</p>
            </div>
            <span class="text-xs font-mono text-slate-400">{{ agents().length }} operators</span>
          </div>

          <!-- ROSTER CARDS -->
          <div class="space-y-2.5 max-h-[360px] overflow-y-auto pr-1">
            @for (agent of agents(); track agent.agentId) {
              <div class="bg-slate-950/80 border border-slate-800/80 rounded-xl p-3 flex items-center justify-between gap-3">
                <div class="flex items-center gap-3">
                  <div class="w-9 h-9 rounded-full bg-slate-800 border border-slate-700 text-white font-bold text-xs flex items-center justify-center shrink-0">
                    {{ getInitials(agent.agentName) }}
                  </div>
                  <div>
                    <div class="flex items-center gap-2">
                      <span class="text-xs font-semibold text-white">{{ agent.agentName }}</span>
                      <span [ngClass]="getPresencePillClass(agent.state)" class="px-2 py-0.5 rounded-full text-[10px] font-medium border">
                        {{ agent.state }}
                      </span>
                    </div>
                    <div class="text-[10px] text-slate-400 flex items-center gap-2 mt-0.5">
                      <span>{{ agent.role }}</span>
                      <span>•</span>
                      <span>Workload: <strong class="text-white">{{ agent.activeWorkload }}</strong> tickets</span>
                    </div>
                  </div>
                </div>

                <div class="flex items-center gap-1.5 shrink-0">
                  <button
                    (click)="simulateAgentHeartbeat(agent.agentId, agent.agentName)"
                    title="Send immediate presence heartbeat"
                    class="p-1.5 rounded-lg bg-slate-800 hover:bg-slate-700 text-slate-300 text-xs transition">
                    💓 Ping
                  </button>
                  <button
                    (click)="simulateAgentIdle(agent.agentId)"
                    title="Set agent to Idle"
                    class="p-1.5 rounded-lg bg-amber-500/10 hover:bg-amber-500/20 text-amber-300 text-xs transition">
                    💤 Idle
                  </button>
                </div>
              </div>
            }
          </div>
        </section>

        <!-- ACTIVE COLLISION LOCKS MONITOR -->
        <section class="bg-slate-900/70 border border-slate-800 rounded-2xl p-5 space-y-4">
          <div class="flex items-center justify-between border-b border-slate-800/80 pb-3">
            <div>
              <h2 class="text-sm font-semibold text-white flex items-center gap-2">
                <span>🔒</span> Active Collision Locks Monitor
              </h2>
              <p class="text-xs text-slate-400">Live table of tickets under HardComposingLock</p>
            </div>
            <span class="text-xs font-mono text-violet-400 font-semibold">{{ lockedTickets().length }} locked</span>
          </div>

          <div class="space-y-2.5 max-h-[360px] overflow-y-auto pr-1">
            @if (lockedTickets().length === 0) {
              <div class="p-8 text-center text-slate-500 text-xs rounded-xl bg-slate-950/40 border border-dashed border-slate-800">
                No tickets currently under hard write lock. Use the simulation sandbox above to test collision locks.
              </div>
            } @else {
              @for (ticket of lockedTickets(); track ticket.ticketId) {
                <div class="bg-slate-950/80 border border-violet-900/40 rounded-xl p-3.5 space-y-2">
                  <div class="flex items-start justify-between gap-2">
                    <div>
                      <div class="text-xs font-semibold text-white">{{ ticket.ticketTitle }}</div>
                      <div class="text-[10px] text-slate-400 flex items-center gap-1.5 mt-0.5">
                        <span>Customer: <strong class="text-slate-300">{{ ticket.customerName }}</strong></span>
                        <span>•</span>
                        <span class="text-amber-400 font-medium">Priority: {{ ticket.priority }}</span>
                      </div>
                    </div>

                    <button
                      (click)="adminUnlock(ticket.ticketId)"
                      class="px-2.5 py-1 rounded-lg text-[10px] font-semibold bg-rose-500/20 hover:bg-rose-500/30 text-rose-300 border border-rose-500/40 transition shrink-0">
                      Force Release
                    </button>
                  </div>

                  <div class="flex items-center justify-between text-[11px] bg-slate-900/60 rounded-lg p-2 border border-slate-800/80">
                    <div class="flex items-center gap-2">
                      <span class="text-slate-400">Locked By:</span>
                      <span class="font-semibold text-violet-300">{{ ticket.lockedByAgentName }}</span>
                    </div>
                    @if (ticket.lockDraftSnippet) {
                      <div class="text-[10px] italic text-slate-400 truncate max-w-[200px]">
                        "{{ ticket.lockDraftSnippet }}"
                      </div>
                    }
                  </div>
                </div>
              }
            }
          </div>
        </section>
      </div>

      <!-- AUTO-HANDOFF EVENT AUDIT LOG -->
      <section class="bg-slate-900/70 border border-slate-800 rounded-2xl p-5 space-y-4">
        <div class="flex items-center justify-between border-b border-slate-800/80 pb-3">
          <div>
            <h2 class="text-sm font-semibold text-white flex items-center gap-2">
              <span>📜</span> Auto-Handoff Event Audit Feed
            </h2>
            <p class="text-xs text-slate-400">Automatic ticket reassignment on operator abandonment (>5 min inactivity with pending customer message)</p>
          </div>
          <span class="text-xs font-mono text-slate-400">{{ auditEvents().length }} logged events</span>
        </div>

        <div class="space-y-2 max-h-[300px] overflow-y-auto pr-1">
          @if (auditEvents().length === 0) {
            <div class="p-6 text-center text-slate-500 text-xs rounded-xl bg-slate-950/40 border border-dashed border-slate-800">
              No auto-handoff events recorded yet. Click "Run Abandonment Sweep" to evaluate idle/offline operators.
            </div>
          } @else {
            @for (event of auditEvents(); track event.eventId) {
              <div class="bg-slate-950/80 border border-slate-800/80 rounded-xl p-3 flex flex-col sm:flex-row sm:items-center justify-between gap-3">
                <div class="space-y-1">
                  <div class="flex items-center gap-2">
                    <span class="px-2 py-0.5 rounded text-[10px] font-mono bg-blue-500/20 text-blue-300 border border-blue-500/30">
                      REASSIGNED
                    </span>
                    <span class="text-xs font-semibold text-white">{{ event.ticketTitle }}</span>
                    <span class="text-[10px] text-slate-400 font-mono">({{ event.customerName }})</span>
                  </div>
                  <div class="text-[11px] text-slate-300">
                    <span class="text-rose-400 font-medium">{{ event.previousAgentName }}</span>
                    <span class="text-slate-500 mx-1.5">➔</span>
                    <span class="text-emerald-400 font-medium">{{ event.newAgentName }}</span>
                    <span class="text-slate-400 ml-2">({{ event.reason }})</span>
                  </div>
                </div>

                <div class="text-[10px] font-mono text-slate-500 shrink-0">
                  {{ event.timestampUtc | date:'HH:mm:ss' }} UTC
                </div>
              </div>
            }
          }
        </div>
      </section>
    </div>
  `
})
export class AgentPresenceCockpitComponent implements OnInit, OnDestroy {
  private readonly http = inject(HttpClient);
  private heartbeatInterval: any;

  // State Signals
  readonly metrics = signal<PresenceMetricsDto | null>(null);
  readonly agents = signal<AgentPresenceSessionDto[]>([]);
  readonly tickets = signal<TicketItemDto[]>([]);
  readonly auditEvents = signal<TicketHandoffEventDto[]>([]);
  readonly isSweepLoading = signal<boolean>(false);

  // Simulation Sandbox Signals
  readonly selectedTicketId = signal<string>('aaaa0102-0000-0000-0000-000000000002');
  readonly simStatus = signal<TicketCollisionStatusDto | null>(null);
  readonly op1Draft = signal<string>('Hello Starlight team, checking your webhook...');
  readonly op2Draft = signal<string>('');
  readonly isOp1HoldingLock = signal<boolean>(true);
  readonly op2BlockedWarning = signal<boolean>(false);

  // Computed signals
  readonly lockedTickets = computed(() => {
    return this.tickets().filter(t => t.lockState === CollisionLockState.HardComposingLock);
  });

  readonly op1AgentId = '11111111-1111-1111-1111-111111111111'; // Alex Vance
  readonly op2AgentId = '22222222-2222-2222-2222-222222222222'; // Elena Rostova

  ngOnInit(): void {
    this.refreshAllData();

    // 15-second heartbeat poll for live presence mesh
    this.heartbeatInterval = setInterval(() => {
      this.refreshMetricsAndRoster();
    }, 15000);
  }

  ngOnDestroy(): void {
    if (this.heartbeatInterval) {
      clearInterval(this.heartbeatInterval);
    }
  }

  refreshAllData(): void {
    this.refreshMetricsAndRoster();
    this.refreshTickets();
    this.refreshAuditEvents();
    this.refreshSimulationStatus();
  }

  refreshMetricsAndRoster(): void {
    this.http.get<PresenceMetricsDto>('/api/v1/conversations/presence/metrics').subscribe({
      next: data => this.metrics.set(data),
      error: () => {}
    });

    this.http.get<{ agents: AgentPresenceSessionDto[] }>('/api/v1/conversations/presence/team-status').subscribe({
      next: data => this.agents.set(data.agents),
      error: () => {}
    });
  }

  refreshTickets(): void {
    this.http.get<TicketItemDto[]>('/api/v1/conversations/presence/tickets').subscribe({
      next: data => this.tickets.set(data),
      error: () => {}
    });
  }

  refreshAuditEvents(): void {
    this.http.get<TicketHandoffEventDto[]>('/api/v1/conversations/presence/auto-handoff/audit?limit=20').subscribe({
      next: data => this.auditEvents.set(data),
      error: () => {}
    });
  }

  refreshSimulationStatus(): void {
    const tid = this.selectedTicketId();
    if (!tid) return;

    this.http.get<TicketCollisionStatusDto>(`/api/v1/conversations/presence/tickets/${tid}/status?agentId=${this.op2AgentId}`).subscribe({
      next: status => {
        this.simStatus.set(status);
        this.isOp1HoldingLock.set(status.lockState === CollisionLockState.HardComposingLock && status.lockedByAgentId === this.op1AgentId);
      },
      error: () => {}
    });
  }

  onSelectTicket(newTicketId: string): void {
    this.selectedTicketId.set(newTicketId);
    this.refreshSimulationStatus();
  }

  onOp1Typing(text: string): void {
    this.op1Draft.set(text);
    const tid = this.selectedTicketId();

    // Send typing heartbeat to keep lock lease alive & broadcast snippet
    this.http.post('/api/v1/conversations/presence/heartbeat', {
      agentId: this.op1AgentId,
      agentName: 'Alex Vance',
      agentEmail: 'alex.vance@sgrowing.io',
      ticketId: tid,
      state: AgentPresenceState.ComposingReply,
      isTyping: true,
      draftSnippet: text
    }).subscribe({
      next: () => this.refreshSimulationStatus(),
      error: () => {}
    });
  }

  op1AcquireLock(): void {
    const tid = this.selectedTicketId();
    this.http.post(`/api/v1/conversations/presence/tickets/${tid}/lock`, {
      agentId: this.op1AgentId,
      agentName: 'Alex Vance',
      draftSnippet: this.op1Draft()
    }).subscribe({
      next: () => {
        this.isOp1HoldingLock.set(true);
        this.refreshAllData();
      },
      error: () => {}
    });
  }

  op1SubmitReply(): void {
    const tid = this.selectedTicketId();
    this.http.post(`/api/v1/conversations/presence/tickets/${tid}/unlock`, {
      agentId: this.op1AgentId,
      reason: 0, // ReplySubmitted
      isAdminOverride: false
    }).subscribe({
      next: () => {
        this.isOp1HoldingLock.set(false);
        this.op1Draft.set('');
        this.refreshAllData();
      },
      error: () => {}
    });
  }

  op1NavigateAway(): void {
    const tid = this.selectedTicketId();
    this.http.post(`/api/v1/conversations/presence/tickets/${tid}/unlock`, {
      agentId: this.op1AgentId,
      reason: 1, // NavigatedAway
      isAdminOverride: false
    }).subscribe({
      next: () => {
        this.isOp1HoldingLock.set(false);
        this.refreshAllData();
      },
      error: () => {}
    });
  }

  op2AttemptWrite(): void {
    const tid = this.selectedTicketId();
    this.http.post<any>(`/api/v1/conversations/presence/tickets/${tid}/lock`, {
      agentId: this.op2AgentId,
      agentName: 'Elena Rostova',
      draftSnippet: this.op2Draft()
    }).subscribe({
      next: res => {
        if (!res.success) {
          this.op2BlockedWarning.set(true);
          setTimeout(() => this.op2BlockedWarning.set(false), 3000);
        }
        this.refreshAllData();
      },
      error: () => {}
    });
  }

  op2ForceAdminOverride(): void {
    const tid = this.selectedTicketId();
    this.http.post(`/api/v1/conversations/presence/tickets/${tid}/unlock`, {
      agentId: this.op2AgentId,
      reason: 3, // AdminOverride
      isAdminOverride: true
    }).subscribe({
      next: () => {
        this.isOp1HoldingLock.set(false);
        this.refreshAllData();
      },
      error: () => {}
    });
  }

  adminUnlock(ticketId: string): void {
    this.http.post(`/api/v1/conversations/presence/tickets/${ticketId}/unlock`, {
      agentId: this.op1AgentId,
      reason: 3, // AdminOverride
      isAdminOverride: true
    }).subscribe({
      next: () => this.refreshAllData(),
      error: () => {}
    });
  }

  triggerAutoHandoffSweep(): void {
    this.isSweepLoading.set(true);
    this.http.post('/api/v1/conversations/presence/auto-handoff/sweep', {
      abandonmentThresholdMinutes: 5.0
    }).subscribe({
      next: () => {
        this.isSweepLoading.set(false);
        this.refreshAllData();
      },
      error: () => this.isSweepLoading.set(false)
    });
  }

  simulateAgentHeartbeat(agentId: string, agentName: string): void {
    this.http.post('/api/v1/conversations/presence/heartbeat', {
      agentId,
      agentName,
      ticketId: null,
      state: AgentPresenceState.Online,
      isTyping: false
    }).subscribe({
      next: () => this.refreshMetricsAndRoster(),
      error: () => {}
    });
  }

  simulateAgentIdle(agentId: string): void {
    this.http.post('/api/v1/conversations/presence/heartbeat', {
      agentId,
      ticketId: null,
      state: AgentPresenceState.Idle,
      isTyping: false
    }).subscribe({
      next: () => this.refreshMetricsAndRoster(),
      error: () => {}
    });
  }

  resetDemoData(): void {
    this.http.post('/api/v1/conversations/presence/reset', {}).subscribe({
      next: () => {
        this.isOp1HoldingLock.set(true);
        this.op1Draft.set('Hello Starlight team, checking your webhook...');
        this.op2Draft.set('');
        this.refreshAllData();
      },
      error: () => {}
    });
  }

  getInitials(name: string): string {
    if (!name) return 'OP';
    const parts = name.trim().split(' ');
    if (parts.length === 1) return parts[0].substring(0, 2).toUpperCase();
    return (parts[0][0] + parts[parts.length - 1][0]).toUpperCase();
  }

  getPresencePillClass(state: AgentPresenceState): string {
    switch (state) {
      case AgentPresenceState.Online:
        return 'bg-emerald-500/10 text-emerald-400 border-emerald-500/20';
      case AgentPresenceState.ActiveOnTicket:
        return 'bg-cyan-500/10 text-cyan-400 border-cyan-500/20';
      case AgentPresenceState.ViewingTicket:
        return 'bg-blue-500/10 text-blue-400 border-blue-500/20';
      case AgentPresenceState.ComposingReply:
        return 'bg-violet-500/10 text-violet-400 border-violet-500/20 animate-pulse';
      case AgentPresenceState.Idle:
        return 'bg-amber-500/10 text-amber-400 border-amber-500/20';
      case AgentPresenceState.Offline:
        return 'bg-slate-500/10 text-slate-400 border-slate-500/20';
      default:
        return 'bg-slate-800 text-slate-400 border-slate-700';
    }
  }
}
