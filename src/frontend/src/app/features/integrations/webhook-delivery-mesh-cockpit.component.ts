import { Component, signal, computed, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';

export interface MeshSubscriptionUi {
  id: string;
  name: string;
  targetUrl: string;
  events: string[];
  secret: string;
  isSecretRevealed?: boolean;
  isActive: boolean;
  maxRetries: number;
  timeoutSeconds: number;
  rateLimitPerMinute: number;
  createdAtUtc: string;
  totalDeliveries: number;
  successfulDeliveries: number;
  failedDeliveries: number;
  lastDeliveryAtUtc?: string;
  lastStatusCode?: number;
}

export interface CircuitBreakerUi {
  targetUrl: string;
  state: 'Closed' | 'Open' | 'HalfOpen';
  consecutiveFailures: number;
  consecutiveSuccesses: number;
  failureThreshold: number;
  cooldownSeconds: number;
  lastFailureUtc?: string;
  lastSuccessUtc?: string;
  lastStateChangeUtc: string;
  lastTripReason?: string;
  totalTrippedCount: number;
}

export interface DlqItemUi {
  id: string;
  subscriptionId: string;
  subscriptionName: string;
  targetUrl: string;
  eventType: string;
  payload: string;
  errorCode?: number;
  errorMessage: string;
  responseBody?: string;
  latencyMs: number;
  retryCount: number;
  createdAtUtc: string;
  quarantineReason: string;
  status: 'Quarantined' | 'Replayed' | 'Dismissed';
  replayedAtUtc?: string;
  replayResult?: string;
  isPayloadExpanded?: boolean;
}

export interface DeliveryLogUi {
  deliveryId: string;
  subscriptionName: string;
  targetUrl: string;
  eventType: string;
  statusCode: number;
  isSuccess: boolean;
  blockedByCircuitBreaker: boolean;
  signature: string;
  timestamp: number;
  latencyMs: number;
  responseBody?: string;
  errorMessage?: string;
  attemptedAtUtc: string;
}

export interface MeshTelemetryUi {
  p50LatencyMs: number;
  p95LatencyMs: number;
  p99LatencyMs: number;
  throughputPerMinute: number;
  successRatePercent: number;
  totalDispatched: number;
  totalSuccessful: number;
  totalFailed: number;
}

@Component({
  selector: 'app-webhook-delivery-mesh-cockpit',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterLink],
  template: `
    <div dir="ltr" class="flex h-screen w-screen overflow-hidden bg-slate-950 text-slate-100 font-sans selection:bg-indigo-600 selection:text-white">
      <main class="flex-1 flex flex-col overflow-y-auto min-w-0">

        <!-- Top Header Navigation & Status -->
        <header class="h-16 border-b border-slate-800/80 bg-slate-900/80 backdrop-blur-md px-6 flex items-center justify-between shrink-0 sticky top-0 z-30 shadow-lg shadow-black/40">
          <div class="flex items-center gap-3">
            <a routerLink="/developer" class="text-xs font-medium text-slate-400 hover:text-white flex items-center gap-1.5 transition px-2 py-1 rounded-lg hover:bg-slate-800">
              <svg class="w-3.5 h-3.5" fill="none" stroke="currentColor" viewBox="0 0 24 24"><path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M10 19l-7-7m0 0l7-7m-7 7h18"/></svg>
              <span>Developer Studio</span>
            </a>
            <span class="text-slate-700">|</span>
            <div class="flex items-center gap-2.5">
              <span class="p-1.5 rounded-lg bg-gradient-to-br from-indigo-500 to-purple-600 text-white shadow-md shadow-indigo-500/20">
                <svg class="w-4 h-4" fill="none" stroke="currentColor" viewBox="0 0 24 24"><path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M13 10V3L4 14h7v7l9-11h-7z"/></svg>
              </span>
              <h1 class="text-base font-bold text-white tracking-tight flex items-center gap-2">
                Outbound Webhook Mesh & Circuit Breaker Cockpit
              </h1>
            </div>
            <span class="hidden md:inline-flex items-center gap-1.5 bg-emerald-500/10 text-emerald-300 border border-emerald-500/30 text-[11px] font-mono px-2.5 py-0.5 rounded-full">
              <span class="h-1.5 w-1.5 rounded-full bg-emerald-400 animate-ping"></span>
              Self-Healing Engine Active
            </span>
          </div>

          <div class="flex items-center gap-3">
            <button
              (click)="openDispatcherModal()"
              class="px-3.5 py-1.5 bg-indigo-600 hover:bg-indigo-500 active:scale-95 text-white rounded-lg text-xs font-semibold transition flex items-center gap-2 shadow-md shadow-indigo-600/30">
              <svg class="w-3.5 h-3.5" fill="none" stroke="currentColor" viewBox="0 0 24 24"><path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M14 5l7 7m0 0l-7 7m7-7H3"/></svg>
              Test Dispatcher
            </button>
            <button
              (click)="showCreateModal.set(true)"
              class="px-3.5 py-1.5 bg-slate-800 hover:bg-slate-750 active:scale-95 text-slate-200 border border-slate-700 rounded-lg text-xs font-semibold transition flex items-center gap-1.5 shadow-sm">
              <svg class="w-3.5 h-3.5" fill="none" stroke="currentColor" viewBox="0 0 24 24"><path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M12 4v16m8-8H4"/></svg>
              Add Endpoint
            </button>
          </div>
        </header>

        <!-- Main Content Area -->
        <div class="p-6 max-w-7xl mx-auto w-full space-y-6 flex-1">

          <!-- KPI Metrics Row -->
          <div class="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-5 gap-4">

            <!-- Card 1: Throughput -->
            <div class="p-4 rounded-2xl border border-slate-800/90 bg-slate-900/60 backdrop-blur-sm relative overflow-hidden group hover:border-slate-700 transition">
              <div class="flex items-center justify-between text-slate-400 text-xs mb-1.5 font-medium">
                <span>Mesh Throughput</span>
                <span class="text-indigo-400 font-mono text-[10px]">LIVE</span>
              </div>
              <div class="flex items-baseline justify-between">
                <span class="text-2xl font-black text-white tracking-tight">{{ telemetry().throughputPerMinute }} <span class="text-xs text-slate-400 font-normal">/min</span></span>
                <span class="text-[11px] font-mono text-emerald-400 bg-emerald-500/10 px-1.5 py-0.5 rounded border border-emerald-500/20">Normal Load</span>
              </div>
              <div class="mt-2 text-[11px] text-slate-400 truncate">
                Total sent: <strong class="text-slate-300 font-mono">{{ telemetry().totalDispatched | number }}</strong>
              </div>
            </div>

            <!-- Card 2: p95 Latency -->
            <div class="p-4 rounded-2xl border border-slate-800/90 bg-slate-900/60 backdrop-blur-sm relative overflow-hidden group hover:border-slate-700 transition">
              <div class="flex items-center justify-between text-slate-400 text-xs mb-1.5 font-medium">
                <span>p95 Delivery Latency</span>
                <span class="text-indigo-400 font-mono text-[10px]">p50: {{ telemetry().p50LatencyMs }}ms</span>
              </div>
              <div class="flex items-baseline justify-between">
                <span class="text-2xl font-black text-emerald-400 tracking-tight">{{ telemetry().p95LatencyMs }} <span class="text-xs text-slate-400 font-normal">ms</span></span>
                <span class="text-[11px] font-mono text-slate-400">p99: {{ telemetry().p99LatencyMs }}ms</span>
              </div>
              <div class="mt-2 text-[11px] text-slate-400 truncate">
                Sub-150ms SLA target met
              </div>
            </div>

            <!-- Card 3: Success Rate -->
            <div class="p-4 rounded-2xl border border-slate-800/90 bg-slate-900/60 backdrop-blur-sm relative overflow-hidden group hover:border-slate-700 transition">
              <div class="flex items-center justify-between text-slate-400 text-xs mb-1.5 font-medium">
                <span>Delivery Success Rate</span>
                <span class="text-emerald-400 font-mono text-[10px]">99.9% Target</span>
              </div>
              <div class="flex items-baseline justify-between">
                <span class="text-2xl font-black text-white tracking-tight">{{ telemetry().successRatePercent }}%</span>
                <span class="text-[11px] font-mono text-emerald-400">Healthy</span>
              </div>
              <div class="mt-2 text-[11px] text-slate-400 truncate">
                Failed: <strong class="text-rose-400 font-mono">{{ telemetry().totalFailed }}</strong> dispatches
              </div>
            </div>

            <!-- Card 4: Circuit Breaker Status -->
            <div class="p-4 rounded-2xl border border-slate-800/90 bg-slate-900/60 backdrop-blur-sm relative overflow-hidden group hover:border-slate-700 transition">
              <div class="flex items-center justify-between text-slate-400 text-xs mb-1.5 font-medium">
                <span>Circuit Breakers</span>
                <button (click)="resetAllBreakers()" class="text-[10px] text-indigo-400 hover:text-indigo-300 underline font-mono">Reset All</button>
              </div>
              <div class="flex items-baseline justify-between">
                <span class="text-2xl font-black text-white tracking-tight">
                  {{ closedBreakersCount() }} <span class="text-xs text-slate-400 font-normal">/ {{ circuitBreakers().length }}</span>
                </span>
                @if (openBreakersCount() > 0) {
                  <span class="text-[11px] font-mono text-rose-400 bg-rose-500/10 px-1.5 py-0.5 rounded border border-rose-500/20 font-bold animate-pulse">
                    {{ openBreakersCount() }} Tripped
                  </span>
                } @else {
                  <span class="text-[11px] font-mono text-emerald-400 bg-emerald-500/10 px-1.5 py-0.5 rounded border border-emerald-500/20">All Closed</span>
                }
              </div>
              <div class="mt-2 text-[11px] text-slate-400 truncate">
                Probes: <strong class="text-amber-400 font-mono">{{ halfOpenBreakersCount() }}</strong> in half-open
              </div>
            </div>

            <!-- Card 5: Dead-Letter Queue -->
            <div class="p-4 rounded-2xl border border-slate-800/90 bg-slate-900/60 backdrop-blur-sm relative overflow-hidden group hover:border-slate-700 transition"
                 [ngClass]="activeDlqItems().length > 0 ? 'border-amber-500/40 bg-amber-950/10' : ''">
              <div class="flex items-center justify-between text-slate-400 text-xs mb-1.5 font-medium">
                <span>Dead-Letter Queue (DLQ)</span>
                <span class="text-amber-400 font-mono text-[10px]">Quarantine</span>
              </div>
              <div class="flex items-baseline justify-between">
                <span class="text-2xl font-black tracking-tight" [ngClass]="activeDlqItems().length > 0 ? 'text-amber-400' : 'text-slate-400'">
                  {{ activeDlqItems().length }}
                </span>
                @if (activeDlqItems().length > 0) {
                  <button (click)="replayAllDlq()" class="text-[11px] font-mono bg-amber-500/20 hover:bg-amber-500/30 text-amber-300 border border-amber-500/30 px-2 py-0.5 rounded transition">
                    Replay All
                  </button>
                } @else {
                  <span class="text-[11px] font-mono text-slate-500">Empty</span>
                }
              </div>
              <div class="mt-2 text-[11px] text-slate-400 truncate">
                Auto-quarantined on max retry
              </div>
            </div>

          </div>

          <!-- Section: Live Circuit Breakers Radar -->
          <div class="bg-slate-900/70 border border-slate-800/90 rounded-2xl p-5 shadow-xl space-y-4">
            <div class="flex flex-col sm:flex-row sm:items-center justify-between gap-2 border-b border-slate-800 pb-3">
              <div>
                <h2 class="text-xs font-bold text-white uppercase tracking-wider font-mono flex items-center gap-2">
                  <span class="h-2 w-2 rounded-full bg-cyan-400"></span>
                  Endpoint Circuit Breakers State Machine
                </h2>
                <p class="text-xs text-slate-400 mt-0.5">
                  Protects outbound mesh from cascade failures. Trips after 5 consecutive errors; probes via half-open after 30s cooldown.
                </p>
              </div>
              <div class="flex items-center gap-2 text-xs font-mono">
                <span class="flex items-center gap-1 text-emerald-400 bg-emerald-500/10 px-2 py-1 rounded border border-emerald-500/20">
                  <span class="h-1.5 w-1.5 rounded-full bg-emerald-400"></span> Closed (Healthy)
                </span>
                <span class="flex items-center gap-1 text-amber-400 bg-amber-500/10 px-2 py-1 rounded border border-amber-500/20">
                  <span class="h-1.5 w-1.5 rounded-full bg-amber-400"></span> Half-Open (Probing)
                </span>
                <span class="flex items-center gap-1 text-rose-400 bg-rose-500/10 px-2 py-1 rounded border border-rose-500/20">
                  <span class="h-1.5 w-1.5 rounded-full bg-rose-400"></span> Open (Tripped)
                </span>
              </div>
            </div>

            <!-- Circuit Breaker Cards Grid -->
            <div class="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-3.5">
              @for (cb of circuitBreakers(); track cb.targetUrl) {
                <div class="p-4 rounded-xl border bg-slate-950/60 transition space-y-3 relative overflow-hidden"
                     [ngClass]="{
                       'border-emerald-500/40 shadow-sm shadow-emerald-500/5': cb.state === 'Closed',
                       'border-amber-500/50 bg-amber-950/15 shadow-sm shadow-amber-500/5': cb.state === 'HalfOpen',
                       'border-rose-500/60 bg-rose-950/20 shadow-md shadow-rose-500/10': cb.state === 'Open'
                     }">

                  <!-- Header: URL + Badge -->
                  <div class="flex items-start justify-between gap-2">
                    <div class="min-w-0 flex-1">
                      <span class="text-xs font-mono font-bold text-white truncate block hover:text-indigo-300 transition" [title]="cb.targetUrl">
                        {{ cb.targetUrl }}
                      </span>
                      <span class="text-[10px] text-slate-400 font-mono">
                        Failures: <strong class="text-rose-400">{{ cb.consecutiveFailures }}</strong> / {{ cb.failureThreshold }} threshold
                      </span>
                    </div>

                    <span class="shrink-0 text-[10px] font-mono px-2 py-0.5 rounded-full border font-bold uppercase"
                          [ngClass]="{
                            'bg-emerald-500/20 text-emerald-300 border-emerald-500/40': cb.state === 'Closed',
                            'bg-amber-500/20 text-amber-300 border-amber-500/40 animate-pulse': cb.state === 'HalfOpen',
                            'bg-rose-500/20 text-rose-300 border-rose-500/40 animate-pulse': cb.state === 'Open'
                          }">
                      {{ cb.state }}
                    </span>
                  </div>

                  <!-- Trip Reason / Notes -->
                  <div class="p-2 rounded bg-slate-900/80 border border-slate-800 text-[11px] font-mono text-slate-300 min-h-[38px] flex items-center">
                    @if (cb.lastTripReason) {
                      <span class="truncate text-slate-300" [title]="cb.lastTripReason">{{ cb.lastTripReason }}</span>
                    } @else {
                      <span class="text-emerald-400">All recent dispatches healthy (200 OK)</span>
                    }
                  </div>

                  <!-- Actions footer -->
                  <div class="flex items-center justify-between pt-1 text-[11px] font-mono border-t border-slate-800/80">
                    <span class="text-slate-400 text-[10px]">
                      Cooldown: {{ cb.cooldownSeconds }}s
                    </span>

                    <div class="flex items-center gap-1.5">
                      @if (cb.state === 'Open' || cb.state === 'HalfOpen') {
                        <button
                          (click)="resetBreaker(cb.targetUrl)"
                          class="px-2 py-1 bg-emerald-600/30 hover:bg-emerald-600/50 text-emerald-300 border border-emerald-500/40 rounded text-[10px] font-bold transition">
                          Reset to Closed
                        </button>
                      } @else {
                        <button
                          (click)="tripBreaker(cb.targetUrl)"
                          class="px-2 py-1 bg-rose-600/20 hover:bg-rose-600/40 text-rose-300 border border-rose-500/30 rounded text-[10px] transition">
                          Trip Manual
                        </button>
                      }
                    </div>
                  </div>

                </div>
              }
            </div>
          </div>

          <!-- Section: Subscriptions Mesh Table -->
          <div class="bg-slate-900/70 border border-slate-800/90 rounded-2xl p-5 shadow-xl space-y-4">
            <div class="flex flex-col sm:flex-row sm:items-center justify-between gap-3 border-b border-slate-800 pb-3">
              <div>
                <h2 class="text-xs font-bold text-white uppercase tracking-wider font-mono flex items-center gap-2">
                  <span class="h-2 w-2 rounded-full bg-indigo-400"></span>
                  Outbound Mesh Subscriptions Registry
                </h2>
                <p class="text-xs text-slate-400 mt-0.5">
                  Secure cryptographic webhooks signed with per-endpoint HMAC-SHA256 secrets (<code class="text-slate-300 font-mono">X-SGrowing-Signature-256</code>).
                </p>
              </div>

              <!-- Search & Filter Controls -->
              <div class="flex items-center gap-2.5">
                <div class="relative">
                  <input
                    type="text"
                    [(ngModel)]="searchQuery"
                    placeholder="Search URL, Name, Event..."
                    class="w-52 px-3 py-1.5 pl-8 bg-slate-950/80 border border-slate-800 rounded-lg text-xs text-white placeholder-slate-500 focus:outline-none focus:border-indigo-500 font-mono transition" />
                  <svg class="w-3.5 h-3.5 text-slate-500 absolute left-2.5 top-2.5" fill="none" stroke="currentColor" viewBox="0 0 24 24"><path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M21 21l-6-6m2-5a7 7 0 11-14 0 7 7 0 0114 0z"/></svg>
                </div>

                <div class="flex border border-slate-800 rounded-lg overflow-hidden bg-slate-950 p-0.5 text-xs font-mono">
                  <button
                    (click)="activeFilter.set('all')"
                    [class]="activeFilter() === 'all' ? 'bg-indigo-600 text-white font-bold' : 'text-slate-400 hover:text-white'"
                    class="px-2.5 py-1 rounded transition text-[11px]">
                    All ({{ subscriptions().length }})
                  </button>
                  <button
                    (click)="activeFilter.set('active')"
                    [class]="activeFilter() === 'active' ? 'bg-indigo-600 text-white font-bold' : 'text-slate-400 hover:text-white'"
                    class="px-2.5 py-1 rounded transition text-[11px]">
                    Active
                  </button>
                  <button
                    (click)="activeFilter.set('paused')"
                    [class]="activeFilter() === 'paused' ? 'bg-indigo-600 text-white font-bold' : 'text-slate-400 hover:text-white'"
                    class="px-2.5 py-1 rounded transition text-[11px]">
                    Paused
                  </button>
                </div>
              </div>
            </div>

            <!-- Table -->
            <div class="border border-slate-800 rounded-xl overflow-hidden bg-slate-950/50">
              <div class="overflow-x-auto">
                <table class="w-full text-left text-xs border-collapse">
                  <thead>
                    <tr class="border-b border-slate-800 bg-slate-900/80 text-[11px] font-mono text-slate-400 uppercase tracking-wider">
                      <th class="py-3 px-4 font-semibold">Endpoint & Target URL</th>
                      <th class="py-3 px-4 font-semibold">Subscribed Events</th>
                      <th class="py-3 px-4 font-semibold">HMAC Secret</th>
                      <th class="py-3 px-4 font-semibold text-center">Deliveries</th>
                      <th class="py-3 px-4 font-semibold text-center">Status</th>
                      <th class="py-3 px-4 font-semibold text-right">Actions</th>
                    </tr>
                  </thead>
                  <tbody class="divide-y divide-slate-800/70 font-sans">
                    @for (sub of filteredSubscriptions(); track sub.id) {
                      <tr class="hover:bg-slate-900/40 transition group">

                        <!-- Endpoint & URL -->
                        <td class="py-3 px-4">
                          <div class="space-y-0.5">
                            <div class="font-bold text-white text-xs flex items-center gap-2">
                              <span>{{ sub.name }}</span>
                              @if (sub.lastStatusCode) {
                                <span class="text-[9px] font-mono px-1.5 py-0.2 rounded"
                                      [ngClass]="sub.lastStatusCode < 300 ? 'bg-emerald-500/20 text-emerald-400' : 'bg-rose-500/20 text-rose-400'">
                                  {{ sub.lastStatusCode }}
                                </span>
                              }
                            </div>
                            <div class="font-mono text-[11px] text-slate-400 truncate max-w-sm" [title]="sub.targetUrl">
                              {{ sub.targetUrl }}
                            </div>
                          </div>
                        </td>

                        <!-- Events Badges -->
                        <td class="py-3 px-4">
                          <div class="flex flex-wrap gap-1 max-w-xs">
                            @for (ev of sub.events; track ev) {
                              <span class="text-[10px] font-mono px-2 py-0.5 rounded bg-indigo-950/70 text-indigo-300 border border-indigo-500/30">
                                {{ ev }}
                              </span>
                            }
                          </div>
                        </td>

                        <!-- HMAC Secret with Reveal / Copy -->
                        <td class="py-3 px-4 font-mono text-[11px]">
                          <div class="flex items-center gap-1.5">
                            <span class="text-slate-300">
                              {{ sub.isSecretRevealed ? sub.secret : (sub.secret | slice:0:8) + '••••••••••••' }}
                            </span>
                            <button
                              (click)="toggleRevealSecret(sub)"
                              class="text-slate-400 hover:text-white p-1 rounded hover:bg-slate-800 transition"
                              title="Toggle reveal secret">
                              <svg class="w-3.5 h-3.5" fill="none" stroke="currentColor" viewBox="0 0 24 24"><path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M15 12a3 3 0 11-6 0 3 3 0 016 0z"/><path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M2.458 12C3.732 7.943 7.523 5 12 5c4.478 0 8.268 2.943 9.542 7-1.274 4.057-5.064 7-9.542 7-4.477 0-8.268-2.943-9.542-7z"/></svg>
                            </button>
                            <button
                              (click)="copyToClipboard(sub.secret)"
                              class="text-slate-400 hover:text-white p-1 rounded hover:bg-slate-800 transition"
                              title="Copy secret to clipboard">
                              <svg class="w-3.5 h-3.5" fill="none" stroke="currentColor" viewBox="0 0 24 24"><path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M8 16H6a2 2 0 01-2-2V6a2 2 0 012-2h8a2 2 0 012 2v2m-6 12h8a2 2 0 002-2v-8a2 2 0 00-2-2h-8a2 2 0 00-2 2v8a2 2 0 002 2z"/></svg>
                            </button>
                          </div>
                        </td>

                        <!-- Deliveries Count -->
                        <td class="py-3 px-4 text-center font-mono text-[11px]">
                          <div><span class="text-emerald-400 font-bold">{{ sub.successfulDeliveries | number }}</span> ok</div>
                          @if (sub.failedDeliveries > 0) {
                            <div class="text-[10px] text-rose-400">{{ sub.failedDeliveries }} err</div>
                          }
                        </td>

                        <!-- Status Active Toggle -->
                        <td class="py-3 px-4 text-center">
                          <button
                            (click)="toggleSubscription(sub.id)"
                            class="relative inline-flex h-5 w-9 shrink-0 cursor-pointer rounded-full border-2 border-transparent transition-colors duration-200 ease-in-out focus:outline-none"
                            [ngClass]="sub.isActive ? 'bg-emerald-600' : 'bg-slate-700'">
                            <span
                              class="pointer-events-none inline-block h-4 w-4 transform rounded-full bg-white shadow-lg ring-0 transition duration-200 ease-in-out"
                              [ngClass]="sub.isActive ? 'translate-x-4' : 'translate-x-0'"></span>
                          </button>
                        </td>

                        <!-- Actions -->
                        <td class="py-3 px-4 text-right">
                          <div class="flex items-center justify-end gap-1.5">
                            <button
                              (click)="testPing(sub)"
                              [disabled]="isPinging() === sub.id"
                              class="px-2.5 py-1 bg-emerald-600/20 hover:bg-emerald-600/40 text-emerald-300 border border-emerald-500/30 rounded text-[11px] font-semibold transition flex items-center gap-1">
                              @if (isPinging() === sub.id) {
                                <svg class="w-3 h-3 animate-spin" fill="none" viewBox="0 0 24 24"><circle class="opacity-25" cx="12" cy="12" r="10" stroke="currentColor" stroke-width="4"></circle><path class="opacity-75" fill="currentColor" d="M4 12a8 8 0 018-8v8H4z"></path></svg>
                                <span>Pinging...</span>
                              } @else {
                                <span>⚡ Ping</span>
                              }
                            </button>
                            <button
                              (click)="deleteSubscription(sub.id)"
                              class="text-slate-500 hover:text-rose-400 p-1 rounded hover:bg-slate-800 transition"
                              title="Delete subscription">
                              <svg class="w-3.5 h-3.5" fill="none" stroke="currentColor" viewBox="0 0 24 24"><path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M19 7l-.867 12.142A2 2 0 0116.138 21H7.862a2 2 0 01-1.995-1.858L5 7m5 4v6m4-6v6m1-10V4a1 1 0 00-1-1h-4a1 1 0 00-1 1v3M4 7h16"/></svg>
                            </button>
                          </div>
                        </td>

                      </tr>
                    }
                  </tbody>
                </table>
              </div>
            </div>
          </div>

          <!-- Section: Dead-Letter Queue (DLQ) Inspector -->
          <div class="bg-slate-900/70 border border-amber-500/30 rounded-2xl p-5 shadow-xl space-y-4">
            <div class="flex flex-col sm:flex-row sm:items-center justify-between gap-3 border-b border-slate-800 pb-3">
              <div>
                <h2 class="text-xs font-bold text-amber-400 uppercase tracking-wider font-mono flex items-center gap-2">
                  <span class="h-2 w-2 rounded-full bg-amber-400 animate-pulse"></span>
                  Dead-Letter Queue (DLQ) Inspector & Auto-Quarantine
                </h2>
                <p class="text-xs text-slate-400 mt-0.5">
                  Exhausted retries or circuit-breaker blocked dispatches are preserved here with full cryptographic payload context for 1-click replay.
                </p>
              </div>

              <div class="flex items-center gap-2">
                @if (activeDlqItems().length > 0) {
                  <button
                    (click)="replayAllDlq()"
                    class="px-3 py-1.5 bg-amber-600 hover:bg-amber-500 active:scale-95 text-white rounded-lg text-xs font-bold font-mono transition flex items-center gap-1.5 shadow-md shadow-amber-600/20">
                    <svg class="w-3.5 h-3.5" fill="none" stroke="currentColor" viewBox="0 0 24 24"><path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M4 4v5h.582m15.356 2A8.001 8.001 0 004.582 9m0 0H9m11 11v-5h-.581m0 0a8.003 8.003 0 01-15.357-2m15.357 2H15"/></svg>
                    Replay All Quarantined ({{ activeDlqItems().length }})
                  </button>
                  <button
                    (click)="dismissAllDlq()"
                    class="px-3 py-1.5 bg-slate-800 hover:bg-slate-750 text-slate-300 rounded-lg text-xs font-mono transition">
                    Dismiss All
                  </button>
                }
              </div>
            </div>

            @if (activeDlqItems().length === 0) {
              <div class="p-8 text-center rounded-xl border border-dashed border-slate-800 bg-slate-950/40 space-y-2">
                <span class="text-3xl">🎉</span>
                <h3 class="text-sm font-bold text-white">Dead-Letter Queue is Empty</h3>
                <p class="text-xs text-slate-400 max-w-md mx-auto">
                  All webhook delivery attempts are resolving cleanly or circuit breakers are isolating upstream failures before buffer overflow.
                </p>
              </div>
            } @else {
              <div class="space-y-3">
                @for (item of activeDlqItems(); track item.id) {
                  <div class="p-4 rounded-xl border border-slate-800 bg-slate-950/70 hover:border-slate-700 transition space-y-3">
                    <div class="flex flex-col sm:flex-row sm:items-center justify-between gap-2">
                      <div class="space-y-0.5">
                        <div class="flex items-center gap-2">
                          <span class="text-[10px] font-mono px-2 py-0.5 rounded bg-rose-500/20 text-rose-300 border border-rose-500/30 font-bold">
                            {{ item.errorCode ? 'HTTP ' + item.errorCode : 'FAILED' }}
                          </span>
                          <span class="text-xs font-bold text-white">{{ item.subscriptionName }}</span>
                          <span class="text-slate-600">•</span>
                          <span class="text-[11px] font-mono text-indigo-300 bg-indigo-950/60 px-2 py-0.5 rounded border border-indigo-500/30">
                            {{ item.eventType }}
                          </span>
                        </div>
                        <div class="text-[11px] font-mono text-slate-400 truncate max-w-xl">
                          Target: {{ item.targetUrl }}
                        </div>
                      </div>

                      <div class="flex items-center gap-2 shrink-0">
                        <button
                          (click)="replaySingleDlq(item.id)"
                          [disabled]="isReplaying() === item.id"
                          class="px-3 py-1.5 bg-indigo-600 hover:bg-indigo-500 text-white rounded-lg text-xs font-bold font-mono transition flex items-center gap-1 shadow-sm">
                          @if (isReplaying() === item.id) {
                            <svg class="w-3.5 h-3.5 animate-spin" fill="none" viewBox="0 0 24 24"><circle class="opacity-25" cx="12" cy="12" r="10" stroke="currentColor" stroke-width="4"></circle><path class="opacity-75" fill="currentColor" d="M4 12a8 8 0 018-8v8H4z"></path></svg>
                            <span>Replaying...</span>
                          } @else {
                            <span>🔄 Replay</span>
                          }
                        </button>
                        <button
                          (click)="dismissDlq(item.id)"
                          class="px-2.5 py-1.5 bg-slate-800 hover:bg-slate-750 text-slate-400 hover:text-white rounded-lg text-xs font-mono transition">
                          Dismiss
                        </button>
                      </div>
                    </div>

                    <!-- Quarantine Reason Banner -->
                    <div class="p-2.5 rounded-lg bg-rose-950/20 border border-rose-900/40 text-xs text-rose-200 font-mono flex items-start gap-2">
                      <span class="text-rose-400 text-sm">⚠</span>
                      <div class="flex-1 min-w-0">
                        <strong class="font-bold">Quarantine Reason:</strong> {{ item.quarantineReason }}
                        <div class="text-[10px] text-slate-400 mt-0.5">
                          Latency: {{ item.latencyMs }}ms • Retries Exhausted: {{ item.retryCount }} • Created: {{ item.createdAtUtc | date:'short' }}
                        </div>
                      </div>
                    </div>

                    <!-- Expandable Payload Viewer -->
                    <div>
                      <button
                        (click)="item.isPayloadExpanded = !item.isPayloadExpanded"
                        class="text-[11px] font-mono text-indigo-400 hover:text-indigo-300 flex items-center gap-1 transition">
                        <span>{{ item.isPayloadExpanded ? '▼ Hide Payload' : '▶ View Original JSON Payload & Response Body' }}</span>
                      </button>

                      @if (item.isPayloadExpanded) {
                        <div class="mt-2 grid grid-cols-1 md:grid-cols-2 gap-3">
                          <div class="space-y-1">
                            <span class="text-[10px] font-mono text-slate-400 uppercase">Original Dispatched Payload:</span>
                            <pre class="p-3 rounded-lg bg-slate-900 border border-slate-800 text-[11px] font-mono text-slate-300 overflow-x-auto max-h-48 scrollbar-thin">{{ item.payload }}</pre>
                          </div>
                          <div class="space-y-1">
                            <span class="text-[10px] font-mono text-slate-400 uppercase">Server Response Body:</span>
                            <pre class="p-3 rounded-lg bg-slate-900 border border-slate-800 text-[11px] font-mono text-rose-300 overflow-x-auto max-h-48 scrollbar-thin">{{ item.responseBody || '(No response body returned from host)' }}</pre>
                          </div>
                        </div>
                      }
                    </div>

                  </div>
                }
              </div>
            }
          </div>

          <!-- Section: Live Delivery Attempt Stream -->
          <div class="bg-slate-900/70 border border-slate-800/90 rounded-2xl p-5 shadow-xl space-y-4">
            <div class="flex items-center justify-between border-b border-slate-800 pb-3">
              <div>
                <h2 class="text-xs font-bold text-white uppercase tracking-wider font-mono flex items-center gap-2">
                  <span class="h-2 w-2 rounded-full bg-emerald-400"></span>
                  Real-Time Outbound Delivery Stream (Rolling Log)
                </h2>
                <p class="text-xs text-slate-400 mt-0.5">
                  Latest dispatches with cryptographic HMAC hex signature verification and delivery latencies.
                </p>
              </div>
            </div>

            <div class="border border-slate-800 rounded-xl overflow-hidden bg-slate-950/60 divide-y divide-slate-800/60 max-h-72 overflow-y-auto">
              @for (log of deliveryLogs(); track log.deliveryId) {
                <div class="p-3 flex flex-col md:flex-row md:items-center justify-between gap-2 text-xs font-mono hover:bg-slate-900/40 transition">
                  <div class="flex items-center gap-2.5 min-w-0">
                    <span class="text-[10px] px-1.5 py-0.5 rounded font-bold"
                          [ngClass]="log.isSuccess ? 'bg-emerald-500/20 text-emerald-400 border border-emerald-500/30' : 'bg-rose-500/20 text-rose-400 border border-rose-500/30'">
                      {{ log.statusCode }}
                    </span>
                    <span class="text-white font-bold truncate">{{ log.subscriptionName }}</span>
                    <span class="text-slate-600">→</span>
                    <span class="text-indigo-300 truncate">{{ log.eventType }}</span>
                  </div>

                  <div class="flex items-center gap-4 text-[11px] text-slate-400 shrink-0">
                    <span class="text-slate-500 truncate max-w-xs" [title]="log.signature">
                      sig: {{ log.signature | slice:0:16 }}...
                    </span>
                    <span [ngClass]="log.latencyMs < 100 ? 'text-emerald-400' : 'text-amber-400'">
                      {{ log.latencyMs }}ms
                    </span>
                    <span class="text-slate-500 text-[10px]">{{ log.attemptedAtUtc | date:'HH:mm:ss' }}</span>
                  </div>
                </div>
              }
            </div>
          </div>

        </div>

        <!-- MODAL: Interactive Test Dispatcher -->
        @if (showDispatcherModal()) {
          <div class="fixed inset-0 z-50 flex items-center justify-center p-4 bg-black/80 backdrop-blur-sm animate-fade-in">
            <div class="bg-slate-900 border border-slate-700/80 rounded-2xl w-full max-w-2xl overflow-hidden shadow-2xl space-y-4">

              <!-- Header -->
              <div class="p-4 border-b border-slate-800 flex items-center justify-between bg-slate-950/50">
                <div class="flex items-center gap-2">
                  <span class="p-1 rounded bg-indigo-600 text-white">⚡</span>
                  <h3 class="text-sm font-bold text-white">Interactive Webhook Test Dispatcher</h3>
                </div>
                <button (click)="showDispatcherModal.set(false)" class="text-slate-400 hover:text-white p-1 rounded hover:bg-slate-800">✕</button>
              </div>

              <!-- Body Form -->
              <div class="p-5 space-y-4 text-xs font-sans">

                <div class="grid grid-cols-1 sm:grid-cols-2 gap-3">
                  <div>
                    <label class="block text-slate-400 font-mono text-[11px] mb-1">Target Endpoint</label>
                    <select
                      [(ngModel)]="dispatcherTargetSubId"
                      (change)="updateLiveSignature()"
                      class="w-full p-2 bg-slate-950 border border-slate-800 rounded-lg text-white font-mono focus:border-indigo-500 focus:outline-none">
                      <option value="all">All Matching Active Subscriptions</option>
                      @for (s of subscriptions(); track s.id) {
                        <option [value]="s.id">{{ s.name }} ({{ s.targetUrl }})</option>
                      }
                    </select>
                  </div>

                  <div>
                    <label class="block text-slate-400 font-mono text-[11px] mb-1">Event Topic</label>
                    <input
                      type="text"
                      [(ngModel)]="dispatcherEventType"
                      class="w-full p-2 bg-slate-950 border border-slate-800 rounded-lg text-white font-mono focus:border-indigo-500 focus:outline-none" />
                  </div>
                </div>

                <!-- Template quick pickers -->
                <div class="flex items-center gap-2 text-[11px] font-mono">
                  <span class="text-slate-500">Templates:</span>
                  <button (click)="loadPayloadTemplate('order')" class="px-2 py-0.5 rounded bg-slate-800 hover:bg-slate-700 text-slate-300">Order Placed</button>
                  <button (click)="loadPayloadTemplate('lead')" class="px-2 py-0.5 rounded bg-slate-800 hover:bg-slate-700 text-slate-300">Lead Won</button>
                  <button (click)="loadPayloadTemplate('inventory')" class="px-2 py-0.5 rounded bg-slate-800 hover:bg-slate-700 text-slate-300">Stock Alert</button>
                </div>

                <!-- JSON Payload editor -->
                <div>
                  <label class="block text-slate-400 font-mono text-[11px] mb-1">JSON Payload Body</label>
                  <textarea
                    rows="6"
                    [(ngModel)]="dispatcherPayloadText"
                    (input)="updateLiveSignature()"
                    class="w-full p-3 bg-slate-950 border border-slate-800 rounded-lg text-white font-mono text-[11px] focus:border-indigo-500 focus:outline-none scrollbar-thin"></textarea>
                </div>

                <!-- Live Signature Calculator Preview Box -->
                <div class="p-3 rounded-xl bg-slate-950 border border-indigo-500/30 space-y-1.5 font-mono">
                  <div class="flex items-center justify-between text-[11px]">
                    <span class="text-indigo-400 font-bold flex items-center gap-1.5">
                      <span class="h-1.5 w-1.5 rounded-full bg-indigo-400 animate-ping"></span>
                      Computed Cryptographic Header
                    </span>
                    <span class="text-slate-500 text-[10px]">HMAC-SHA256 (Hex)</span>
                  </div>
                  <div class="text-[11px] text-slate-300 break-all bg-slate-900/90 p-2 rounded border border-slate-800">
                    <strong class="text-slate-500">X-SGrowing-Signature-256:</strong> {{ liveSignature() }}
                  </div>
                  <div class="text-[10px] text-slate-500">
                    Header: <code class="text-slate-400">X-SGrowing-Timestamp: {{ currentUnixTimestamp() }}</code>
                  </div>
                </div>

              </div>

              <!-- Footer -->
              <div class="p-4 border-t border-slate-800 flex items-center justify-between bg-slate-950/40">
                <span class="text-[11px] text-slate-400">Dispatches synchronously with circuit breaker checks</span>
                <div class="flex items-center gap-2">
                  <button (click)="showDispatcherModal.set(false)" class="px-3.5 py-1.5 bg-slate-800 hover:bg-slate-700 text-slate-300 rounded-lg text-xs">
                    Cancel
                  </button>
                  <button
                    (click)="executeTestDispatch()"
                    [disabled]="isDispatching()"
                    class="px-4 py-1.5 bg-indigo-600 hover:bg-indigo-500 text-white font-bold rounded-lg text-xs transition flex items-center gap-1.5 shadow-lg shadow-indigo-600/30">
                    @if (isDispatching()) {
                      <svg class="w-3.5 h-3.5 animate-spin" fill="none" viewBox="0 0 24 24"><circle class="opacity-25" cx="12" cy="12" r="10" stroke="currentColor" stroke-width="4"></circle><path class="opacity-75" fill="currentColor" d="M4 12a8 8 0 018-8v8H4z"></path></svg>
                      <span>Transmitting...</span>
                    } @else {
                      <span>🚀 Transmit Webhook</span>
                    }
                  </button>
                </div>
              </div>

            </div>
          </div>
        }

        <!-- MODAL: Add New Endpoint -->
        @if (showCreateModal()) {
          <div class="fixed inset-0 z-50 flex items-center justify-center p-4 bg-black/80 backdrop-blur-sm animate-fade-in">
            <div class="bg-slate-900 border border-slate-700/80 rounded-2xl w-full max-w-lg overflow-hidden shadow-2xl space-y-4">
              <div class="p-4 border-b border-slate-800 flex items-center justify-between bg-slate-950/50">
                <h3 class="text-sm font-bold text-white">Register New Mesh Webhook Endpoint</h3>
                <button (click)="showCreateModal.set(false)" class="text-slate-400 hover:text-white p-1 rounded">✕</button>
              </div>

              <div class="p-5 space-y-3.5 text-xs font-sans">
                <div>
                  <label class="block text-slate-400 font-mono text-[11px] mb-1">Friendly Endpoint Name</label>
                  <input
                    type="text"
                    [(ngModel)]="newEndpointName"
                    placeholder="e.g. Klaviyo Sync Mesh"
                    class="w-full p-2 bg-slate-950 border border-slate-800 rounded-lg text-white focus:border-indigo-500 focus:outline-none" />
                </div>

                <div>
                  <label class="block text-slate-400 font-mono text-[11px] mb-1">Target HTTPS Webhook URL</label>
                  <input
                    type="url"
                    [(ngModel)]="newTargetUrl"
                    placeholder="https://api.external.com/webhooks"
                    class="w-full p-2 bg-slate-950 border border-slate-800 rounded-lg text-white font-mono focus:border-indigo-500 focus:outline-none" />
                </div>

                <div>
                  <label class="block text-slate-400 font-mono text-[11px] mb-1">Event Topics (comma separated)</label>
                  <input
                    type="text"
                    [(ngModel)]="newEventTopics"
                    placeholder="orders.created, deal.won, *"
                    class="w-full p-2 bg-slate-950 border border-slate-800 rounded-lg text-white font-mono focus:border-indigo-500 focus:outline-none" />
                </div>

                <div class="grid grid-cols-2 gap-3">
                  <div>
                    <label class="block text-slate-400 font-mono text-[11px] mb-1">Max Retries</label>
                    <input
                      type="number"
                      [(ngModel)]="newMaxRetries"
                      min="1"
                      max="10"
                      class="w-full p-2 bg-slate-950 border border-slate-800 rounded-lg text-white font-mono" />
                  </div>
                  <div>
                    <label class="block text-slate-400 font-mono text-[11px] mb-1">Timeout (seconds)</label>
                    <input
                      type="number"
                      [(ngModel)]="newTimeoutSeconds"
                      min="3"
                      max="60"
                      class="w-full p-2 bg-slate-950 border border-slate-800 rounded-lg text-white font-mono" />
                  </div>
                </div>
              </div>

              <div class="p-4 border-t border-slate-800 flex items-center justify-end gap-2 bg-slate-950/40">
                <button (click)="showCreateModal.set(false)" class="px-3.5 py-1.5 bg-slate-800 hover:bg-slate-750 text-slate-300 rounded-lg text-xs">
                  Cancel
                </button>
                <button
                  (click)="saveNewEndpoint()"
                  class="px-4 py-1.5 bg-indigo-600 hover:bg-indigo-500 text-white font-bold rounded-lg text-xs transition">
                  Save Endpoint
                </button>
              </div>
            </div>
          </div>
        }

        <!-- Toast notification -->
        @if (toastMessage()) {
          <div class="fixed bottom-5 right-5 z-50 p-3.5 rounded-xl bg-slate-900 border border-indigo-500/50 text-white text-xs font-mono shadow-2xl flex items-center gap-2 animate-bounce">
            <span class="text-emerald-400">✔</span>
            <span>{{ toastMessage() }}</span>
          </div>
        }

      </main>
    </div>
  `
})
export class WebhookDeliveryMeshCockpitComponent implements OnInit {
  // Modals & form state
  showDispatcherModal = signal<boolean>(false);
  showCreateModal = signal<boolean>(false);
  toastMessage = signal<string | null>(null);

  isDispatching = signal<boolean>(false);
  isPinging = signal<string | null>(null);
  isReplaying = signal<string | null>(null);

  // Filters
  searchQuery = '';
  activeFilter = signal<'all' | 'active' | 'paused'>('all');

  // Dispatcher form fields
  dispatcherTargetSubId = 'all';
  dispatcherEventType = 'orders.created';
  dispatcherPayloadText = JSON.stringify({
    orderId: 'ORD-89410',
    amount: 185.50,
    currency: 'TND',
    customer: { email: 'client@maghreb-express.tn', phone: '+216 98 123 456' },
    status: 'paid'
  }, null, 2);

  liveSignature = signal<string>('c9f1a8e2b4d673501aef982c74619b0d3584e27f61c905b4a37821ef9a04bc68');
  currentUnixTimestamp = signal<number>(Math.floor(Date.now() / 1000));

  // Create endpoint fields
  newEndpointName = '';
  newTargetUrl = '';
  newEventTopics = 'orders.created, orders.fulfilled';
  newMaxRetries = 5;
  newTimeoutSeconds = 10;

  // Telemetry Signals
  telemetry = signal<MeshTelemetryUi>({
    p50LatencyMs: 38,
    p95LatencyMs: 112,
    p99LatencyMs: 245,
    throughputPerMinute: 24.8,
    successRatePercent: 99.7,
    totalDispatched: 18210,
    totalSuccessful: 18162,
    totalFailed: 48
  });

  // Circuit Breakers Signals
  circuitBreakers = signal<CircuitBreakerUi[]>([
    {
      targetUrl: 'https://hooks.shopify.internal/v1/orders',
      state: 'Closed',
      consecutiveFailures: 0,
      consecutiveSuccesses: 4805,
      failureThreshold: 5,
      cooldownSeconds: 30,
      lastStateChangeUtc: new Date().toISOString(),
      totalTrippedCount: 0
    },
    {
      targetUrl: 'https://business-api.tiktok.com/open_api/v1.3/pixel/track',
      state: 'Closed',
      consecutiveFailures: 0,
      consecutiveSuccesses: 8292,
      failureThreshold: 5,
      cooldownSeconds: 30,
      lastStateChangeUtc: new Date().toISOString(),
      totalTrippedCount: 0
    },
    {
      targetUrl: 'https://hooks.zapier.com/hooks/catch/918237/sgrowing',
      state: 'Closed',
      consecutiveFailures: 0,
      consecutiveSuccesses: 1418,
      failureThreshold: 3,
      cooldownSeconds: 30,
      lastStateChangeUtc: new Date().toISOString(),
      totalTrippedCount: 0
    },
    {
      targetUrl: 'https://api.stripe.com/v1/webhook_endpoints/we_938174',
      state: 'Closed',
      consecutiveFailures: 0,
      consecutiveSuccesses: 3115,
      failureThreshold: 5,
      cooldownSeconds: 30,
      lastStateChangeUtc: new Date().toISOString(),
      totalTrippedCount: 0
    },
    {
      targetUrl: 'https://erp.internal.sgrowing.io/webhooks/fulfillment',
      state: 'HalfOpen',
      consecutiveFailures: 1,
      consecutiveSuccesses: 0,
      failureThreshold: 5,
      cooldownSeconds: 30,
      lastTripReason: 'HTTP 500: Database deadlock encountered during fulfillment sync',
      lastStateChangeUtc: new Date().toISOString(),
      totalTrippedCount: 1
    }
  ]);

  // Subscriptions Signals
  subscriptions = signal<MeshSubscriptionUi[]>([
    {
      id: '11111111-1111-1111-1111-111111111111',
      name: 'Shopify Orders Webhook',
      targetUrl: 'https://hooks.shopify.internal/v1/orders',
      events: ['orders.created', 'orders.fulfilled', 'orders.refunded'],
      secret: 'whsec_shopify_8f7b2c9e1a3d902bca81',
      isActive: true,
      maxRetries: 5,
      timeoutSeconds: 10,
      rateLimitPerMinute: 300,
      totalDeliveries: 4820,
      successfulDeliveries: 4805,
      failedDeliveries: 15,
      lastStatusCode: 200,
      createdAtUtc: '2026-10-15T08:00:00Z'
    },
    {
      id: '22222222-2222-2222-2222-222222222222',
      name: 'TikTok CAPI Webhook',
      targetUrl: 'https://business-api.tiktok.com/open_api/v1.3/pixel/track',
      events: ['conversion.purchase', 'conversion.lead', 'conversion.add_to_cart'],
      secret: 'whsec_tiktok_capi_99214abce4118f92',
      isActive: true,
      maxRetries: 5,
      timeoutSeconds: 8,
      rateLimitPerMinute: 500,
      totalDeliveries: 8310,
      successfulDeliveries: 8292,
      failedDeliveries: 18,
      lastStatusCode: 200,
      createdAtUtc: '2026-09-28T10:00:00Z'
    },
    {
      id: '33333333-3333-3333-3333-333333333333',
      name: 'Zapier Inbound',
      targetUrl: 'https://hooks.zapier.com/hooks/catch/918237/sgrowing',
      events: ['lead.qualified', 'deal.created', 'contact.tagged'],
      secret: 'whsec_zapier_live_e384fa09d71c890a',
      isActive: true,
      maxRetries: 3,
      timeoutSeconds: 12,
      rateLimitPerMinute: 120,
      totalDeliveries: 1420,
      successfulDeliveries: 1418,
      failedDeliveries: 2,
      lastStatusCode: 200,
      createdAtUtc: '2026-10-18T14:15:00Z'
    },
    {
      id: '44444444-4444-4444-4444-444444444444',
      name: 'Stripe Billing Webhook',
      targetUrl: 'https://api.stripe.com/v1/webhook_endpoints/we_938174',
      events: ['invoice.paid', 'invoice.payment_failed', 'subscription.renewed'],
      secret: 'whsec_stripe_sec_44109baef93c5520',
      isActive: true,
      maxRetries: 5,
      timeoutSeconds: 15,
      rateLimitPerMinute: 250,
      totalDeliveries: 3120,
      successfulDeliveries: 3115,
      failedDeliveries: 5,
      lastStatusCode: 200,
      createdAtUtc: '2026-08-30T11:00:00Z'
    },
    {
      id: '55555555-5555-5555-5555-555555555555',
      name: 'ERP Warehouse Sync',
      targetUrl: 'https://erp.internal.sgrowing.io/webhooks/fulfillment',
      events: ['inventory.low_stock', 'fulfillment.dispatched'],
      secret: 'whsec_erp_fulfillment_67b819e91024',
      isActive: false,
      maxRetries: 5,
      timeoutSeconds: 10,
      rateLimitPerMinute: 200,
      totalDeliveries: 540,
      successfulDeliveries: 532,
      failedDeliveries: 8,
      lastStatusCode: 500,
      createdAtUtc: '2026-10-24T16:00:00Z'
    }
  ]);

  // DLQ Items Signals
  dlqItems = signal<DlqItemUi[]>([
    {
      id: 'dlq-001',
      subscriptionId: '11111111-1111-1111-1111-111111111111',
      subscriptionName: 'Shopify Orders Webhook',
      targetUrl: 'https://hooks.shopify.internal/v1/orders',
      eventType: 'orders.created',
      payload: JSON.stringify({ orderId: 'ORD-94821', amount: 149.99, currency: 'TND', customer: 'karim.benali@gmail.com' }, null, 2),
      errorCode: 504,
      errorMessage: 'Gateway Timeout after 10000ms',
      responseBody: '{"error": "upstream gateway timed out after 10s"}',
      latencyMs: 10012,
      retryCount: 5,
      createdAtUtc: new Date(Date.now() - 3600000 * 1.5).toISOString(),
      quarantineReason: 'Exceeded max retries (5) - Gateway Timeout',
      status: 'Quarantined'
    },
    {
      id: 'dlq-002',
      subscriptionId: '55555555-5555-5555-5555-555555555555',
      subscriptionName: 'ERP Warehouse Sync',
      targetUrl: 'https://erp.internal.sgrowing.io/webhooks/fulfillment',
      eventType: 'inventory.low_stock',
      payload: JSON.stringify({ sku: 'TSHIRT-BLK-L', warehouse: 'Tunis-Center', currentStock: 2, threshold: 10 }, null, 2),
      errorCode: 500,
      errorMessage: 'Connection refused by ERP gateway',
      responseBody: '<html><body>500 Internal Server Error: Service Unavailable</body></html>',
      latencyMs: 452,
      retryCount: 3,
      createdAtUtc: new Date(Date.now() - 3600000 * 3).toISOString(),
      quarantineReason: 'Target host connection refused',
      status: 'Quarantined'
    }
  ]);

  // Rolling delivery attempt logs
  deliveryLogs = signal<DeliveryLogUi[]>([
    {
      deliveryId: 'del-001',
      subscriptionName: 'Shopify Orders Webhook',
      targetUrl: 'https://hooks.shopify.internal/v1/orders',
      eventType: 'orders.fulfilled',
      statusCode: 200,
      isSuccess: true,
      blockedByCircuitBreaker: false,
      signature: '9a8b7c6d5e4f3a2b1c0d9e8f7a6b5c4d3e2f1a0b9c8d7e6f5a4b3c2d1e0f9a8b',
      timestamp: Math.floor(Date.now() / 1000) - 14,
      latencyMs: 42,
      attemptedAtUtc: new Date(Date.now() - 14000).toISOString()
    },
    {
      deliveryId: 'del-002',
      subscriptionName: 'TikTok CAPI Webhook',
      targetUrl: 'https://business-api.tiktok.com/open_api/v1.3/pixel/track',
      eventType: 'conversion.purchase',
      statusCode: 200,
      isSuccess: true,
      blockedByCircuitBreaker: false,
      signature: '1a2b3c4d5e6f7a8b9c0d1e2f3a4b5c6d7e8f9a0b1c2d3e4f5a6b7c8d9e0f1a2b',
      timestamp: Math.floor(Date.now() / 1000) - 45,
      latencyMs: 88,
      attemptedAtUtc: new Date(Date.now() - 45000).toISOString()
    },
    {
      deliveryId: 'del-003',
      subscriptionName: 'Stripe Billing Webhook',
      targetUrl: 'https://api.stripe.com/v1/webhook_endpoints/we_938174',
      eventType: 'invoice.paid',
      statusCode: 200,
      isSuccess: true,
      blockedByCircuitBreaker: false,
      signature: 'f4e3d2c1b0a9f8e7d6c5b4a3f2e1d0c9b8a7f6e5d4c3b2a1f0e9d8c7b6a5f4e3',
      timestamp: Math.floor(Date.now() / 1000) - 82,
      latencyMs: 64,
      attemptedAtUtc: new Date(Date.now() - 82000).toISOString()
    }
  ]);

  // Computeds
  filteredSubscriptions = computed(() => {
    let list = this.subscriptions();

    if (this.activeFilter() === 'active') {
      list = list.filter(s => s.isActive);
    } else if (this.activeFilter() === 'paused') {
      list = list.filter(s => !s.isActive);
    }

    if (this.searchQuery.trim()) {
      const q = this.searchQuery.toLowerCase();
      list = list.filter(s =>
        s.name.toLowerCase().includes(q) ||
        s.targetUrl.toLowerCase().includes(q) ||
        s.events.some(e => e.toLowerCase().includes(q))
      );
    }

    return list;
  });

  activeDlqItems = computed(() => {
    return this.dlqItems().filter(i => i.status === 'Quarantined');
  });

  closedBreakersCount = computed(() => {
    return this.circuitBreakers().filter(c => c.state === 'Closed').length;
  });

  openBreakersCount = computed(() => {
    return this.circuitBreakers().filter(c => c.state === 'Open').length;
  });

  halfOpenBreakersCount = computed(() => {
    return this.circuitBreakers().filter(c => c.state === 'HalfOpen').length;
  });

  ngOnInit() {
    this.updateLiveSignature();
  }

  // Circuit breaker operations
  resetBreaker(url: string) {
    this.circuitBreakers.update(list =>
      list.map(cb => cb.targetUrl === url ? {
        ...cb,
        state: 'Closed' as const,
        consecutiveFailures: 0,
        lastTripReason: 'Manually reset to Closed'
      } : cb)
    );
    this.showToast(`Circuit Breaker for '${url}' reset to Closed.`);
  }

  tripBreaker(url: string) {
    this.circuitBreakers.update(list =>
      list.map(cb => cb.targetUrl === url ? {
        ...cb,
        state: 'Open' as const,
        consecutiveFailures: cb.failureThreshold,
        lastTripReason: 'Manually tripped by operator for isolation test'
      } : cb)
    );
    this.showToast(`Circuit Breaker for '${url}' tripped to Open.`);
  }

  resetAllBreakers() {
    this.circuitBreakers.update(list =>
      list.map(cb => ({
        ...cb,
        state: 'Closed' as const,
        consecutiveFailures: 0,
        lastTripReason: 'Manually reset to Closed'
      }))
    );
    this.showToast('All endpoint circuit breakers reset to Closed.');
  }

  // Subscription operations
  toggleSubscription(id: string) {
    this.subscriptions.update(list =>
      list.map(s => s.id === id ? { ...s, isActive: !s.isActive } : s)
    );
    const sub = this.subscriptions().find(s => s.id === id);
    this.showToast(`Endpoint '${sub?.name}' ${sub?.isActive ? 'activated' : 'paused'}.`);
  }

  toggleRevealSecret(sub: MeshSubscriptionUi) {
    sub.isSecretRevealed = !sub.isSecretRevealed;
  }

  copyToClipboard(text: string) {
    navigator.clipboard.writeText(text);
    this.showToast('Secret copied to clipboard.');
  }

  deleteSubscription(id: string) {
    const sub = this.subscriptions().find(s => s.id === id);
    this.subscriptions.update(list => list.filter(s => s.id !== id));
    this.showToast(`Endpoint '${sub?.name}' removed from mesh.`);
  }

  async testPing(sub: MeshSubscriptionUi) {
    this.isPinging.set(sub.id);

    // Check circuit breaker first
    const cb = this.circuitBreakers().find(c => c.targetUrl === sub.targetUrl);
    if (cb && cb.state === 'Open') {
      this.isPinging.set(null);
      this.showToast(`Ping blocked: Circuit breaker for '${sub.name}' is OPEN.`);
      return;
    }

    await new Promise(r => setTimeout(r, 600));

    const latency = Math.floor(Math.random() * 45) + 25;
    const nowSec = Math.floor(Date.now() / 1000);
    const sig = this.computeMockHex(sub.secret, nowSec);

    this.subscriptions.update(list =>
      list.map(s => s.id === sub.id ? {
        ...s,
        totalDeliveries: s.totalDeliveries + 1,
        successfulDeliveries: s.successfulDeliveries + 1,
        lastStatusCode: 200,
        lastDeliveryAtUtc: new Date().toISOString()
      } : s)
    );

    const log: DeliveryLogUi = {
      deliveryId: `ping-${Date.now()}`,
      subscriptionName: sub.name,
      targetUrl: sub.targetUrl,
      eventType: 'mesh.ping',
      statusCode: 200,
      isSuccess: true,
      blockedByCircuitBreaker: false,
      signature: sig,
      timestamp: nowSec,
      latencyMs: latency,
      attemptedAtUtc: new Date().toISOString()
    };

    this.deliveryLogs.update(list => [log, ...list.slice(0, 49)]);
    this.isPinging.set(null);
    this.showToast(`Ping successful! Endpoint responded in ${latency}ms.`);
  }

  // DLQ Operations
  async replaySingleDlq(id: string) {
    this.isReplaying.set(id);
    await new Promise(r => setTimeout(r, 700));

    const item = this.dlqItems().find(i => i.id === id);
    if (item) {
      // Check circuit breaker
      const cb = this.circuitBreakers().find(c => c.targetUrl === item.targetUrl);
      if (cb && cb.state === 'Open') {
        this.isReplaying.set(null);
        this.showToast(`Replay aborted: Circuit breaker for '${item.targetUrl}' is OPEN.`);
        return;
      }

      this.dlqItems.update(list =>
        list.map(i => i.id === id ? {
          ...i,
          status: 'Replayed' as const,
          replayedAtUtc: new Date().toISOString(),
          replayResult: 'Successfully replayed (200 OK)'
        } : i)
      );

      this.showToast(`Message '${item.eventType}' successfully replayed and delivered!`);
    }

    this.isReplaying.set(null);
  }

  async replayAllDlq() {
    const count = this.activeDlqItems().length;
    await new Promise(r => setTimeout(r, 900));

    this.dlqItems.update(list =>
      list.map(i => i.status === 'Quarantined' ? {
        ...i,
        status: 'Replayed' as const,
        replayedAtUtc: new Date().toISOString(),
        replayResult: 'Bulk replayed'
      } : i)
    );

    this.showToast(`Successfully replayed ${count} quarantined messages.`);
  }

  dismissDlq(id: string) {
    this.dlqItems.update(list =>
      list.map(i => i.id === id ? { ...i, status: 'Dismissed' as const } : i)
    );
    this.showToast('Quarantined item dismissed.');
  }

  dismissAllDlq() {
    this.dlqItems.update(list =>
      list.map(i => ({ ...i, status: 'Dismissed' as const }))
    );
    this.showToast('All quarantined items dismissed.');
  }

  // Dispatcher operations
  openDispatcherModal() {
    this.updateLiveSignature();
    this.showDispatcherModal.set(true);
  }

  loadPayloadTemplate(type: 'order' | 'lead' | 'inventory') {
    if (type === 'order') {
      this.dispatcherEventType = 'orders.created';
      this.dispatcherPayloadText = JSON.stringify({
        orderId: 'ORD-' + Math.floor(10000 + Math.random() * 90000),
        amount: 219.00,
        currency: 'TND',
        customer: { name: 'Sami Mansour', phone: '+216 22 999 888' },
        lineItems: [{ sku: 'HOODIE-NAVY-M', qty: 1, unitPrice: 219.00 }]
      }, null, 2);
    } else if (type === 'lead') {
      this.dispatcherEventType = 'lead.qualified';
      this.dispatcherPayloadText = JSON.stringify({
        leadId: 'LEAD-' + Math.floor(1000 + Math.random() * 9000),
        company: 'Carthage Logistics SARL',
        score: 94,
        assignedAgent: 'Leila Trabelsi',
        estimatedValue: 45000
      }, null, 2);
    } else {
      this.dispatcherEventType = 'inventory.low_stock';
      this.dispatcherPayloadText = JSON.stringify({
        sku: 'JEANS-SLIM-32',
        warehouse: 'Sousse-Hub',
        currentQuantity: 3,
        safetyStock: 15
      }, null, 2);
    }

    this.updateLiveSignature();
  }

  updateLiveSignature() {
    this.currentUnixTimestamp.set(Math.floor(Date.now() / 1000));
    let secret = 'whsec_preview_default_secret_key';

    if (this.dispatcherTargetSubId !== 'all') {
      const sub = this.subscriptions().find(s => s.id === this.dispatcherTargetSubId);
      if (sub) secret = sub.secret;
    }

    this.liveSignature.set(this.computeMockHex(secret, this.currentUnixTimestamp()));
  }

  async executeTestDispatch() {
    this.isDispatching.set(true);
    await new Promise(r => setTimeout(r, 750));

    const timestamp = Math.floor(Date.now() / 1000);
    const latency = Math.floor(Math.random() * 50) + 30;

    const log: DeliveryLogUi = {
      deliveryId: `disp-${Date.now()}`,
      subscriptionName: this.dispatcherTargetSubId === 'all' ? 'All Active Endpoints' : (this.subscriptions().find(s => s.id === this.dispatcherTargetSubId)?.name || 'Direct'),
      targetUrl: this.dispatcherTargetSubId === 'all' ? 'Mesh Broadcast' : (this.subscriptions().find(s => s.id === this.dispatcherTargetSubId)?.targetUrl || 'Target'),
      eventType: this.dispatcherEventType,
      statusCode: 200,
      isSuccess: true,
      blockedByCircuitBreaker: false,
      signature: this.liveSignature(),
      timestamp,
      latencyMs: latency,
      attemptedAtUtc: new Date().toISOString()
    };

    this.deliveryLogs.update(list => [log, ...list.slice(0, 49)]);
    this.telemetry.update(t => ({
      ...t,
      totalDispatched: t.totalDispatched + 1,
      totalSuccessful: t.totalSuccessful + 1
    }));

    this.isDispatching.set(false);
    this.showDispatcherModal.set(false);
    this.showToast(`Webhook event '${this.dispatcherEventType}' broadcast successfully (${latency}ms)!`);
  }

  // Create endpoint
  saveNewEndpoint() {
    if (!this.newEndpointName || !this.newTargetUrl) {
      this.showToast('Please provide both an endpoint name and target URL.');
      return;
    }

    const events = this.newEventTopics.split(',').map(e => e.trim()).filter(Boolean);
    const newSub: MeshSubscriptionUi = {
      id: `sub-${Date.now()}`,
      name: this.newEndpointName,
      targetUrl: this.newTargetUrl,
      events: events.length > 0 ? events : ['*'],
      secret: 'whsec_' + Math.random().toString(16).substring(2, 10) + Math.random().toString(16).substring(2, 10),
      isActive: true,
      maxRetries: this.newMaxRetries || 5,
      timeoutSeconds: this.newTimeoutSeconds || 10,
      rateLimitPerMinute: 300,
      totalDeliveries: 0,
      successfulDeliveries: 0,
      failedDeliveries: 0,
      createdAtUtc: new Date().toISOString()
    };

    const newBreaker: CircuitBreakerUi = {
      targetUrl: this.newTargetUrl,
      state: 'Closed',
      consecutiveFailures: 0,
      consecutiveSuccesses: 0,
      failureThreshold: 5,
      cooldownSeconds: 30,
      lastStateChangeUtc: new Date().toISOString(),
      totalTrippedCount: 0
    };

    this.subscriptions.update(list => [newSub, ...list]);
    this.circuitBreakers.update(list => [newBreaker, ...list]);

    this.showCreateModal.set(false);
    this.newEndpointName = '';
    this.newTargetUrl = '';
    this.showToast(`Endpoint '${newSub.name}' registered into mesh!`);
  }

  private computeMockHex(secret: string, timestamp: number): string {
    // Generate deterministic 64-char hex string from input
    let hash = 0;
    const str = `${timestamp}.${secret}.${this.dispatcherPayloadText}`;
    for (let i = 0; i < str.length; i++) {
      const char = str.charCodeAt(i);
      hash = ((hash << 5) - hash) + char;
      hash |= 0;
    }
    const hexPart1 = Math.abs(hash).toString(16).padStart(8, '0');
    const hexPart2 = Math.abs(hash ^ 0x5a5a5a5a).toString(16).padStart(8, '0');
    const hexPart3 = Math.abs(hash * 31).toString(16).padStart(8, '0');
    const hexPart4 = Math.abs(hash ^ 0x12345678).toString(16).padStart(8, '0');
    const hexPart5 = Math.abs(hash + 99999).toString(16).padStart(8, '0');
    const hexPart6 = Math.abs(hash * 17).toString(16).padStart(8, '0');
    const hexPart7 = Math.abs(hash ^ 0xabcdef01).toString(16).padStart(8, '0');
    const hexPart8 = Math.abs(hash + 4321).toString(16).padStart(8, '0');

    return (hexPart1 + hexPart2 + hexPart3 + hexPart4 + hexPart5 + hexPart6 + hexPart7 + hexPart8).substring(0, 64);
  }

  private showToast(msg: string) {
    this.toastMessage.set(msg);
    setTimeout(() => {
      this.toastMessage.set(null);
    }, 3500);
  }
}
