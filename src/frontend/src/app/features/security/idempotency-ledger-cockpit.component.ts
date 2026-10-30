import { Component, signal, computed, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { HttpClient } from '@angular/common/http';

export interface IdempotencyRecord {
  key: string;
  tenantId: string;
  httpMethod: string;
  requestPath: string;
  fingerprint: string;
  status: 'InProgress' | 'Completed' | 'Failed' | 'PayloadMismatch';
  createdAt: string;
  updatedAt: string;
  expiresAt: string;
  lockTtlSeconds: number;
  responseStatusCode?: number | null;
  responseHeaders?: Record<string, string> | null;
  responseBody?: string | null;
  replayCount: number;
  lastReplayedAt?: string | null;
  errorMessage?: string | null;
  isExpired: boolean;
}

export interface IdempotencyMetrics {
  totalTrackedKeys: number;
  replayHits: number;
  conflictsDetected: number;
  mismatchesBlocked: number;
  replayHitRatePct: number;
  totalRequestsProcessed: number;
  activeLocksCount: number;
  completedCount: number;
}

export interface SimulateResponse {
  statusCode: number;
  success: boolean;
  replayDetected: boolean;
  status: string;
  message: string;
  fingerprint?: string | null;
  headers: Record<string, string>;
  responseBody?: string | null;
  replayCount: number;
  expiresAt?: string | null;
  executionDurationMs: number;
  metrics: IdempotencyMetrics;
}

@Component({
  selector: 'app-idempotency-ledger-cockpit',
  standalone: true,
  imports: [CommonModule, FormsModule],
  template: `
    <div class="flex h-screen w-screen overflow-hidden bg-slate-950 text-slate-100 font-sans" dir="ltr">
      <main class="flex-1 flex flex-col overflow-y-auto">
        <!-- Topbar / Header -->
        <header class="h-16 border-b border-slate-800 bg-slate-900/80 px-6 flex items-center justify-between shrink-0 backdrop-blur-md sticky top-0 z-30">
          <div class="flex items-center gap-3">
            <span class="p-2.5 rounded-xl bg-violet-950/60 border border-violet-500/30 text-violet-400 text-xl shadow-inner">
              ⚡
            </span>
            <div>
              <div class="flex items-center gap-2">
                <h1 class="text-base font-bold text-white tracking-wide">
                  Distributed Multi-Tenant Idempotency Key & Safe Replay Ledger
                </h1>
                <span class="bg-violet-500/10 text-violet-400 border border-violet-500/30 text-[10px] font-mono px-2 py-0.5 rounded-full uppercase tracking-wider font-semibold">
                  Horizon 37 • Active
                </span>
              </div>
              <p class="text-xs text-slate-400">
                SHA-256 Fingerprinting • Distributed In-Flight Locks (60s) • Deterministic Replay Cache (24h) • Payload Tampering Guard
              </p>
            </div>
          </div>

          <div class="flex items-center gap-2.5">
            <button
              (click)="loadData()"
              [disabled]="loading()"
              class="px-3 py-1.5 text-xs font-medium rounded-lg bg-slate-800 hover:bg-slate-700 text-slate-200 border border-slate-700 transition flex items-center gap-1.5 disabled:opacity-50">
              <span [class.animate-spin]="loading()">🔄</span>
              <span>Refresh Ledger</span>
            </button>
            <button
              (click)="resetLedger()"
              class="px-3 py-1.5 text-xs font-medium rounded-lg bg-rose-950/50 hover:bg-rose-900/60 text-rose-300 border border-rose-800/40 transition flex items-center gap-1.5">
              <span>🧹</span>
              <span>Reset Ledger</span>
            </button>
          </div>
        </header>

        <div class="p-6 space-y-6 max-w-7xl mx-auto w-full">
          <!-- KPI Cards Grid -->
          <div class="grid grid-cols-1 md:grid-cols-4 gap-4">
            <!-- Total Keys Tracked -->
            <div class="p-5 rounded-2xl bg-slate-900/90 border border-slate-800/80 shadow-lg relative overflow-hidden group hover:border-violet-500/40 transition">
              <div class="absolute -right-4 -bottom-4 w-20 h-20 bg-violet-500/5 rounded-full blur-xl group-hover:bg-violet-500/10 transition"></div>
              <div class="flex items-center justify-between text-xs font-medium text-slate-400 mb-2">
                <span>Total Keys Tracked</span>
                <span class="p-1 rounded bg-violet-500/10 text-violet-400 text-xs">🔑</span>
              </div>
              <div class="flex items-baseline gap-2">
                <span class="text-2xl font-black text-white tracking-tight font-mono">
                  {{ metrics().totalTrackedKeys }}
                </span>
                <span class="text-xs text-slate-400">active entries</span>
              </div>
              <div class="mt-2 text-[11px] text-slate-500 flex items-center gap-2">
                <span class="text-emerald-400">● {{ metrics().completedCount }} Completed</span>
                <span class="text-amber-400">● {{ metrics().activeLocksCount }} In Flight</span>
              </div>
            </div>

            <!-- Replay Hits -->
            <div class="p-5 rounded-2xl bg-slate-900/90 border border-slate-800/80 shadow-lg relative overflow-hidden group hover:border-emerald-500/40 transition">
              <div class="absolute -right-4 -bottom-4 w-20 h-20 bg-emerald-500/5 rounded-full blur-xl group-hover:bg-emerald-500/10 transition"></div>
              <div class="flex items-center justify-between text-xs font-medium text-slate-400 mb-2">
                <span>Replay Hits (Safe Replay)</span>
                <span class="p-1 rounded bg-emerald-500/10 text-emerald-400 text-xs">🔁</span>
              </div>
              <div class="flex items-baseline gap-2">
                <span class="text-2xl font-black text-emerald-400 tracking-tight font-mono">
                  {{ metrics().replayHits }}
                </span>
                <span class="text-xs font-semibold text-emerald-400/90">
                  ({{ metrics().replayHitRatePct }}% rate)
                </span>
              </div>
              <div class="mt-2 text-[11px] text-slate-500">
                Cached responses served with deterministic headers
              </div>
            </div>

            <!-- Double-Submits Prevented -->
            <div class="p-5 rounded-2xl bg-slate-900/90 border border-slate-800/80 shadow-lg relative overflow-hidden group hover:border-amber-500/40 transition">
              <div class="absolute -right-4 -bottom-4 w-20 h-20 bg-amber-500/5 rounded-full blur-xl group-hover:bg-amber-500/10 transition"></div>
              <div class="flex items-center justify-between text-xs font-medium text-slate-400 mb-2">
                <span>Double-Submits Prevented</span>
                <span class="p-1 rounded bg-amber-500/10 text-amber-400 text-xs">⚠️</span>
              </div>
              <div class="flex items-baseline gap-2">
                <span class="text-2xl font-black text-amber-400 tracking-tight font-mono">
                  {{ metrics().conflictsDetected }}
                </span>
                <span class="text-xs text-amber-400/80 font-mono">HTTP 409</span>
              </div>
              <div class="mt-2 text-[11px] text-slate-500">
                In-flight race conditions blocked by distributed lock
              </div>
            </div>

            <!-- Payload Mismatches Caught -->
            <div class="p-5 rounded-2xl bg-slate-900/90 border border-slate-800/80 shadow-lg relative overflow-hidden group hover:border-rose-500/40 transition">
              <div class="absolute -right-4 -bottom-4 w-20 h-20 bg-rose-500/5 rounded-full blur-xl group-hover:bg-rose-500/10 transition"></div>
              <div class="flex items-center justify-between text-xs font-medium text-slate-400 mb-2">
                <span>Payload Mismatches Caught</span>
                <span class="p-1 rounded bg-rose-500/10 text-rose-400 text-xs">🛡️</span>
              </div>
              <div class="flex items-baseline gap-2">
                <span class="text-2xl font-black text-rose-400 tracking-tight font-mono">
                  {{ metrics().mismatchesBlocked }}
                </span>
                <span class="text-xs text-rose-400/80 font-mono">HTTP 422</span>
              </div>
              <div class="mt-2 text-[11px] text-slate-500">
                Altered payloads reusing existing keys strictly rejected
              </div>
            </div>
          </div>

          <!-- Main Interactive Simulator & Inspector -->
          <div class="grid grid-cols-1 lg:grid-cols-12 gap-6">
            <!-- Left: Simulator Control Panel (7 cols) -->
            <div class="lg:col-span-7 rounded-2xl bg-slate-900/90 border border-slate-800/80 shadow-xl overflow-hidden flex flex-col">
              <div class="px-5 py-4 border-b border-slate-800/80 bg-slate-900/50 flex items-center justify-between">
                <div class="flex items-center gap-2.5">
                  <span class="w-2 h-2 rounded-full bg-violet-400 animate-pulse"></span>
                  <h2 class="text-sm font-semibold text-white tracking-wide">
                    Interactive Idempotency Simulator
                  </h2>
                </div>
                <div class="flex items-center gap-1.5 text-xs">
                  <span class="text-slate-400">Presets:</span>
                  <button
                    (click)="applyPreset('payment')"
                    class="px-2 py-0.5 rounded bg-slate-800 hover:bg-slate-700 text-slate-300 transition text-[11px]">
                    Payment
                  </button>
                  <button
                    (click)="applyPreset('checkout')"
                    class="px-2 py-0.5 rounded bg-slate-800 hover:bg-slate-700 text-slate-300 transition text-[11px]">
                    Checkout
                  </button>
                  <button
                    (click)="applyPreset('webhook')"
                    class="px-2 py-0.5 rounded bg-slate-800 hover:bg-slate-700 text-slate-300 transition text-[11px]">
                    Webhook
                  </button>
                </div>
              </div>

              <div class="p-5 space-y-4 flex-1">
                <!-- Idempotency Key & Tenant -->
                <div class="grid grid-cols-1 sm:grid-cols-2 gap-3">
                  <div>
                    <label class="block text-xs font-medium text-slate-400 mb-1">
                      Idempotency Key (<code class="text-violet-400">X-Idempotency-Key</code>)
                    </label>
                    <div class="flex gap-2">
                      <input
                        type="text"
                        [(ngModel)]="simKey"
                        class="flex-1 px-3 py-1.5 rounded-lg bg-slate-950 border border-slate-800 text-white font-mono text-xs focus:outline-none focus:border-violet-500 transition"
                        placeholder="e.g. idem_tx_9981a7" />
                      <button
                        (click)="generateRandomKey()"
                        title="Generate random UUID key"
                        class="px-2.5 py-1.5 rounded-lg bg-slate-800 hover:bg-slate-700 text-slate-200 border border-slate-700 text-xs font-mono transition">
                        🎲 New
                      </button>
                    </div>
                  </div>

                  <div>
                    <label class="block text-xs font-medium text-slate-400 mb-1">
                      Tenant Partition (<code class="text-slate-300">X-Tenant-Id</code>)
                    </label>
                    <input
                      type="text"
                      [(ngModel)]="simTenant"
                      class="w-full px-3 py-1.5 rounded-lg bg-slate-950 border border-slate-800 text-white font-mono text-xs focus:outline-none focus:border-violet-500 transition"
                      placeholder="tenant-default" />
                  </div>
                </div>

                <!-- Method and Path -->
                <div class="grid grid-cols-1 sm:grid-cols-12 gap-3">
                  <div class="sm:col-span-3">
                    <label class="block text-xs font-medium text-slate-400 mb-1">HTTP Method</label>
                    <select
                      [(ngModel)]="simMethod"
                      class="w-full px-3 py-1.5 rounded-lg bg-slate-950 border border-slate-800 text-white font-semibold text-xs focus:outline-none focus:border-violet-500 transition">
                      <option value="POST">POST</option>
                      <option value="PUT">PUT</option>
                      <option value="PATCH">PATCH</option>
                      <option value="DELETE">DELETE</option>
                    </select>
                  </div>
                  <div class="sm:col-span-9">
                    <label class="block text-xs font-medium text-slate-400 mb-1">Request Path</label>
                    <input
                      type="text"
                      [(ngModel)]="simPath"
                      class="w-full px-3 py-1.5 rounded-lg bg-slate-950 border border-slate-800 text-white font-mono text-xs focus:outline-none focus:border-violet-500 transition"
                      placeholder="/api/v1/payments/charges" />
                  </div>
                </div>

                <!-- Request Body -->
                <div>
                  <div class="flex items-center justify-between mb-1">
                    <label class="text-xs font-medium text-slate-400">Request Body (JSON Payload)</label>
                    <button
                      (click)="formatJsonBody()"
                      class="text-[11px] text-violet-400 hover:text-violet-300 transition">
                      Format JSON
                    </button>
                  </div>
                  <textarea
                    rows="5"
                    [(ngModel)]="simBody"
                    class="w-full p-3 rounded-lg bg-slate-950 border border-slate-800 text-emerald-300 font-mono text-xs focus:outline-none focus:border-violet-500 transition resize-y"></textarea>
                </div>

                <!-- 4 Interactive Simulator Execution Buttons -->
                <div class="pt-2">
                  <p class="text-xs font-semibold text-slate-400 uppercase tracking-wider mb-2">
                    Execute Test Scenario
                  </p>
                  <div class="grid grid-cols-1 sm:grid-cols-2 gap-2.5">
                    <!-- 1. Send Initial Request -->
                    <button
                      (click)="runSimulation('normal')"
                      [disabled]="simulating()"
                      class="p-2.5 rounded-xl bg-violet-600 hover:bg-violet-500 text-white text-xs font-semibold shadow-md transition flex items-center justify-between disabled:opacity-50">
                      <div class="flex items-center gap-2 text-left">
                        <span class="text-base">🚀</span>
                        <div>
                          <div class="font-bold">Send Initial Request</div>
                          <div class="text-[10px] text-violet-200">Acquire lock & process (201/200)</div>
                        </div>
                      </div>
                      <span class="text-violet-300 font-mono">→</span>
                    </button>

                    <!-- 2. Replay Same Request -->
                    <button
                      (click)="runSimulation('replay')"
                      [disabled]="simulating()"
                      class="p-2.5 rounded-xl bg-emerald-600 hover:bg-emerald-500 text-white text-xs font-semibold shadow-md transition flex items-center justify-between disabled:opacity-50">
                      <div class="flex items-center gap-2 text-left">
                        <span class="text-base">🔁</span>
                        <div>
                          <div class="font-bold">Replay Same Request</div>
                          <div class="text-[10px] text-emerald-200">Safe replay + cached headers</div>
                        </div>
                      </div>
                      <span class="text-emerald-300 font-mono">→</span>
                    </button>

                    <!-- 3. Send Tampered Payload -->
                    <button
                      (click)="runSimulation('mismatch')"
                      [disabled]="simulating()"
                      class="p-2.5 rounded-xl bg-rose-600 hover:bg-rose-500 text-white text-xs font-semibold shadow-md transition flex items-center justify-between disabled:opacity-50">
                      <div class="flex items-center gap-2 text-left">
                        <span class="text-base">🚨</span>
                        <div>
                          <div class="font-bold">Send Tampered Payload</div>
                          <div class="text-[10px] text-rose-200">Payload mismatch caught (422)</div>
                        </div>
                      </div>
                      <span class="text-rose-300 font-mono">→</span>
                    </button>

                    <!-- 4. Simulate Concurrent Double-Submit -->
                    <button
                      (click)="runSimulation('conflict')"
                      [disabled]="simulating()"
                      class="p-2.5 rounded-xl bg-amber-600 hover:bg-amber-500 text-white text-xs font-semibold shadow-md transition flex items-center justify-between disabled:opacity-50">
                      <div class="flex items-center gap-2 text-left">
                        <span class="text-base">⚔️</span>
                        <div>
                          <div class="font-bold">Simulate Double-Submit</div>
                          <div class="text-[10px] text-amber-200">Concurrent lock conflict (409)</div>
                        </div>
                      </div>
                      <span class="text-amber-300 font-mono">→</span>
                    </button>
                  </div>
                </div>
              </div>
            </div>

            <!-- Right: Response & Header Inspector (5 cols) -->
            <div class="lg:col-span-5 rounded-2xl bg-slate-900/90 border border-slate-800/80 shadow-xl overflow-hidden flex flex-col">
              <div class="px-5 py-4 border-b border-slate-800/80 bg-slate-900/50 flex items-center justify-between">
                <div class="flex items-center gap-2.5">
                  <span class="text-slate-400 text-sm">🔎</span>
                  <h2 class="text-sm font-semibold text-white tracking-wide">
                    Live Response Inspector
                  </h2>
                </div>
                @if (lastResponse()) {
                  <div class="flex items-center gap-2">
                    <span class="text-[11px] font-mono text-slate-400">
                      ⏱️ {{ lastResponse()!.executionDurationMs }}ms
                    </span>
                    <span
                      [class]="getStatusBadgeClass(lastResponse()!.statusCode)"
                      class="px-2.5 py-0.5 rounded-full text-xs font-mono font-bold tracking-wide">
                      HTTP {{ lastResponse()!.statusCode }}
                    </span>
                  </div>
                }
              </div>

              <div class="p-5 flex-1 flex flex-col space-y-4">
                @if (!lastResponse()) {
                  <div class="flex-1 flex flex-col items-center justify-center text-center p-8 text-slate-500">
                    <span class="text-4xl mb-3">📡</span>
                    <p class="text-sm font-medium text-slate-400">No simulation executed yet</p>
                    <p class="text-xs text-slate-600 mt-1">
                      Choose an action above to execute initial request, safe replay, tamper detection, or concurrency collision.
                    </p>
                  </div>
                } @else {
                  <!-- Replay Banner if detected -->
                  @if (lastResponse()!.replayDetected) {
                    <div class="p-3 rounded-xl bg-emerald-950/70 border border-emerald-500/40 text-emerald-200 text-xs flex items-center justify-between shadow-inner">
                      <div class="flex items-center gap-2">
                        <span class="text-base">✨</span>
                        <div>
                          <div class="font-bold text-emerald-300">DETERMINISTIC REPLAY DETECTED</div>
                          <div class="text-[11px] text-emerald-400/90">Served from idempotency cache without re-executing logic</div>
                        </div>
                      </div>
                      <span class="px-2 py-0.5 rounded bg-emerald-500/20 text-emerald-300 font-mono text-[11px] font-bold">
                        Count: {{ lastResponse()!.replayCount }}
                      </span>
                    </div>
                  } @else if (lastResponse()!.statusCode === 409) {
                    <div class="p-3 rounded-xl bg-amber-950/70 border border-amber-500/40 text-amber-200 text-xs flex items-center gap-2 shadow-inner">
                      <span class="text-base">⚠️</span>
                      <div>
                        <div class="font-bold text-amber-300">CONCURRENT DOUBLE-SUBMIT PREVENTED</div>
                        <div class="text-[11px] text-amber-400/90">In-flight lock actively held; second submission was safely rejected</div>
                      </div>
                    </div>
                  } @else if (lastResponse()!.statusCode === 422) {
                    <div class="p-3 rounded-xl bg-rose-950/70 border border-rose-500/40 text-rose-200 text-xs flex items-center gap-2 shadow-inner">
                      <span class="text-base">🛑</span>
                      <div>
                        <div class="font-bold text-rose-300">PAYLOAD MISMATCH BLOCKED</div>
                        <div class="text-[11px] text-rose-400/90">Key was previously completed with different parameters/body</div>
                      </div>
                    </div>
                  } @else {
                    <div class="p-3 rounded-xl bg-violet-950/60 border border-violet-500/30 text-violet-200 text-xs flex items-center gap-2 shadow-inner">
                      <span class="text-base">✅</span>
                      <div>
                        <div class="font-bold text-violet-300">FRESH IDEMPOTENT EXECUTION</div>
                        <div class="text-[11px] text-violet-400/90">Lock acquired, operation completed, and response cached for 24h</div>
                      </div>
                    </div>
                  }

                  <!-- Fingerprint preview -->
                  @if (lastResponse()!.fingerprint) {
                    <div>
                      <span class="text-[11px] font-medium text-slate-400 block mb-1">
                        SHA-256 Request Fingerprint
                      </span>
                      <div class="p-2 rounded-lg bg-slate-950 border border-slate-800 text-[11px] font-mono text-violet-300 truncate select-all">
                        {{ lastResponse()!.fingerprint }}
                      </div>
                    </div>
                  }

                  <!-- Response Headers -->
                  <div>
                    <span class="text-[11px] font-medium text-slate-400 block mb-1">
                      Response Headers
                    </span>
                    <div class="p-2.5 rounded-lg bg-slate-950 border border-slate-800 font-mono text-[11px] space-y-1 max-h-32 overflow-y-auto">
                      @for (item of getHeaderEntries(lastResponse()!.headers); track item[0]) {
                        <div class="flex items-center gap-2">
                          <span class="text-slate-400">{{ item[0] }}:</span>
                          <span class="text-amber-300 font-semibold">{{ item[1] }}</span>
                        </div>
                      }
                    </div>
                  </div>

                  <!-- Response Body -->
                  <div class="flex-1 flex flex-col">
                    <span class="text-[11px] font-medium text-slate-400 block mb-1">
                      Response Body Payload
                    </span>
                    <pre class="flex-1 p-3 rounded-lg bg-slate-950 border border-slate-800 text-emerald-300 font-mono text-xs overflow-auto max-h-48 whitespace-pre-wrap select-all">{{ formatResponseBody(lastResponse()!.responseBody) }}</pre>
                  </div>
                }
              </div>
            </div>
          </div>

          <!-- Live Idempotency Records Table -->
          <div class="rounded-2xl bg-slate-900/90 border border-slate-800/80 shadow-xl overflow-hidden">
            <div class="px-6 py-4 border-b border-slate-800/80 bg-slate-900/50 flex flex-col sm:flex-row sm:items-center justify-between gap-3">
              <div class="flex items-center gap-3">
                <span class="p-1.5 rounded-lg bg-slate-800 text-slate-300 text-sm">📜</span>
                <div>
                  <h2 class="text-sm font-semibold text-white tracking-wide">
                    Live Idempotency Ledger Records
                  </h2>
                  <p class="text-xs text-slate-400">
                    Inspected state machine entries, replay counters, and TTL countdowns
                  </p>
                </div>
              </div>

              <!-- Filter Controls -->
              <div class="flex items-center gap-2.5">
                <input
                  type="text"
                  [(ngModel)]="searchQuery"
                  placeholder="Filter by key or path..."
                  class="px-3 py-1.5 rounded-lg bg-slate-950 border border-slate-800 text-xs text-white placeholder-slate-500 focus:outline-none focus:border-violet-500 transition w-44" />

                <select
                  [(ngModel)]="statusFilter"
                  (change)="loadData()"
                  class="px-3 py-1.5 rounded-lg bg-slate-950 border border-slate-800 text-xs text-slate-200 focus:outline-none focus:border-violet-500 transition">
                  <option value="">All Statuses</option>
                  <option value="Completed">Completed</option>
                  <option value="InProgress">InProgress</option>
                  <option value="PayloadMismatch">PayloadMismatch</option>
                  <option value="Failed">Failed</option>
                </select>
              </div>
            </div>

            <!-- Table -->
            <div class="overflow-x-auto">
              <table class="w-full text-left border-collapse text-xs">
                <thead>
                  <tr class="border-b border-slate-800 bg-slate-950/50 text-slate-400 uppercase tracking-wider text-[10px]">
                    <th class="py-3 px-4 font-semibold">Idempotency Key</th>
                    <th class="py-3 px-4 font-semibold">Method & Route</th>
                    <th class="py-3 px-4 font-semibold">Fingerprint (SHA-256)</th>
                    <th class="py-3 px-4 font-semibold">Status</th>
                    <th class="py-3 px-4 font-semibold text-center">Replays</th>
                    <th class="py-3 px-4 font-semibold">Expires In</th>
                    <th class="py-3 px-4 font-semibold text-right">Actions</th>
                  </tr>
                </thead>
                <tbody class="divide-y divide-slate-800/60 font-mono">
                  @if (filteredRecords().length === 0) {
                    <tr>
                      <td colspan="7" class="py-8 text-center text-slate-500 font-sans text-xs">
                        No idempotency records match the current filter.
                      </td>
                    </tr>
                  } @else {
                    @for (row of filteredRecords(); track row.key) {
                      <tr class="hover:bg-slate-800/40 transition">
                        <!-- Key -->
                        <td class="py-3 px-4 text-violet-300 font-semibold">
                          <div class="flex items-center gap-1.5">
                            <span class="truncate max-w-[160px]" [title]="row.key">{{ row.key }}</span>
                            <button
                              (click)="selectRecordForSimulator(row)"
                              title="Load into simulator"
                              class="text-slate-500 hover:text-violet-400 text-[10px]">
                              📋
                            </button>
                          </div>
                        </td>

                        <!-- Method & Route -->
                        <td class="py-3 px-4 font-sans">
                          <div class="flex items-center gap-1.5">
                            <span [class]="getMethodBadgeClass(row.httpMethod)" class="px-1.5 py-0.5 rounded text-[10px] font-bold font-mono">
                              {{ row.httpMethod }}
                            </span>
                            <span class="text-slate-300 font-mono text-[11px] truncate max-w-[200px]" [title]="row.requestPath">
                              {{ row.requestPath }}
                            </span>
                          </div>
                        </td>

                        <!-- Fingerprint -->
                        <td class="py-3 px-4 text-slate-400 text-[11px]">
                          <span class="bg-slate-950 px-2 py-0.5 rounded border border-slate-800 text-slate-300 font-mono" [title]="row.fingerprint">
                            sha256:{{ row.fingerprint.slice(0, 10) }}...
                          </span>
                        </td>

                        <!-- Status Badge -->
                        <td class="py-3 px-4">
                          <span [class]="getStatusClass(row.status)" class="px-2 py-0.5 rounded-full text-[10px] font-sans font-semibold tracking-wide uppercase">
                            {{ row.status }}
                          </span>
                        </td>

                        <!-- Replays -->
                        <td class="py-3 px-4 text-center">
                          <span
                            [class]="row.replayCount > 0 ? 'bg-emerald-500/10 text-emerald-400 border border-emerald-500/30' : 'bg-slate-800 text-slate-400'"
                            class="px-2 py-0.5 rounded-full text-[10px] font-mono font-bold">
                            {{ row.replayCount }}
                          </span>
                        </td>

                        <!-- Expires In -->
                        <td class="py-3 px-4 font-sans text-xs text-slate-400">
                          {{ getExpiresInText(row.expiresAt) }}
                        </td>

                        <!-- Actions -->
                        <td class="py-3 px-4 text-right font-sans">
                          <button
                            (click)="invalidateRecord(row.key)"
                            title="Invalidate and purge key from ledger"
                            class="px-2 py-1 rounded bg-rose-950/40 hover:bg-rose-900/60 text-rose-300 border border-rose-800/40 transition text-[11px]">
                            Invalidate
                          </button>
                        </td>
                      </tr>
                    }
                  }
                </tbody>
              </table>
            </div>
          </div>
        </div>
      </main>
    </div>
  `
})
export class IdempotencyLedgerCockpitComponent implements OnInit {
  private http = inject(HttpClient);

  // Signals
  records = signal<IdempotencyRecord[]>([]);
  metrics = signal<IdempotencyMetrics>({
    totalTrackedKeys: 6,
    replayHits: 11,
    conflictsDetected: 3,
    mismatchesBlocked: 2,
    replayHitRatePct: 50.0,
    totalRequestsProcessed: 22,
    activeLocksCount: 1,
    completedCount: 4
  });
  loading = signal<boolean>(false);
  simulating = signal<boolean>(false);
  lastResponse = signal<SimulateResponse | null>(null);

  // Form Inputs
  simKey = 'idem_pi_9f81a7b2c01d';
  simTenant = 'tenant-default';
  simMethod = 'POST';
  simPath = '/api/v1/payments/intents';
  simBody = JSON.stringify({
    amount: 14999,
    currency: 'USD',
    customer_id: 'cus_9941a87b',
    payment_method: 'pm_card_visa',
    capture_method: 'automatic'
  }, null, 2);

  searchQuery = '';
  statusFilter = '';

  // Computed
  filteredRecords = computed(() => {
    let list = this.records();
    const query = this.searchQuery.trim().toLowerCase();
    if (query) {
      list = list.filter(r =>
        r.key.toLowerCase().includes(query) ||
        r.requestPath.toLowerCase().includes(query) ||
        r.fingerprint.toLowerCase().includes(query)
      );
    }
    const filter = this.statusFilter.trim();
    if (filter) {
      list = list.filter(r => r.status === filter);
    }
    return list;
  });

  ngOnInit(): void {
    this.loadData();
  }

  loadData(): void {
    this.loading.set(true);
    const params: Record<string, string> = {
      tenantId: this.simTenant || 'tenant-default'
    };
    if (this.statusFilter) {
      params['status'] = this.statusFilter;
    }

    this.http.get<{ records: IdempotencyRecord[]; totalCount: number; metrics: IdempotencyMetrics }>(
      '/api/v1/security/idempotency/ledger',
      { params }
    ).subscribe({
      next: (res) => {
        this.records.set(res.records || []);
        if (res.metrics) {
          this.metrics.set(res.metrics);
        }
        this.loading.set(false);
      },
      error: () => {
        // Fallback realistic demo dataset if backend is in isolated dev
        this.records.set(this.getFallbackRecords());
        this.loading.set(false);
      }
    });
  }

  runSimulation(mode: 'normal' | 'replay' | 'conflict' | 'mismatch'): void {
    this.simulating.set(true);
    const payload = {
      tenantId: this.simTenant,
      idempotencyKey: this.simKey,
      method: this.simMethod,
      path: this.simPath,
      bodyJson: this.simBody,
      mode: mode
    };

    this.http.post<SimulateResponse>('/api/v1/security/idempotency/simulate', payload).subscribe({
      next: (res) => {
        this.lastResponse.set(res);
        if (res.metrics) {
          this.metrics.set(res.metrics);
        }
        this.simulating.set(false);
        this.loadData();
      },
      error: (err) => {
        // Handle 409 or 422 which HttpClient treats as errors
        if (err.error && err.error.statusCode) {
          this.lastResponse.set(err.error as SimulateResponse);
          if (err.error.metrics) {
            this.metrics.set(err.error.metrics);
          }
        } else {
          // Construct simulated fallback response
          const simulatedResp = this.constructFallbackSimulation(mode);
          this.lastResponse.set(simulatedResp);
        }
        this.simulating.set(false);
        this.loadData();
      }
    });
  }

  invalidateRecord(key: string): void {
    const tenant = this.simTenant || 'tenant-default';
    this.http.delete(`/api/v1/security/idempotency/${encodeURIComponent(key)}?tenantId=${encodeURIComponent(tenant)}`).subscribe({
      next: () => {
        this.loadData();
      },
      error: () => {
        this.records.update(prev => prev.filter(r => r.key !== key));
      }
    });
  }

  resetLedger(): void {
    this.http.post('/api/v1/security/idempotency/reset', {}).subscribe({
      next: () => {
        this.loadData();
      },
      error: () => {
        this.records.set([]);
        this.metrics.set({
          totalTrackedKeys: 0,
          replayHits: 0,
          conflictsDetected: 0,
          mismatchesBlocked: 0,
          replayHitRatePct: 0.0,
          totalRequestsProcessed: 0,
          activeLocksCount: 0,
          completedCount: 0
        });
      }
    });
  }

  generateRandomKey(): void {
    const chars = '0123456789abcdef';
    let rand = '';
    for (let i = 0; i < 12; i++) {
      rand += chars[Math.floor(Math.random() * chars.length)];
    }
    this.simKey = `idem_tx_${rand}`;
  }

  applyPreset(preset: 'payment' | 'checkout' | 'webhook'): void {
    if (preset === 'payment') {
      this.simKey = 'idem_pi_' + Math.random().toString(16).substring(2, 10);
      this.simMethod = 'POST';
      this.simPath = '/api/v1/payments/intents';
      this.simBody = JSON.stringify({
        amount: 14999,
        currency: 'USD',
        customer_id: 'cus_9941a87b',
        payment_method: 'pm_card_visa',
        capture_method: 'automatic'
      }, null, 2);
    } else if (preset === 'checkout') {
      this.simKey = 'idem_chk_' + Math.random().toString(16).substring(2, 10);
      this.simMethod = 'POST';
      this.simPath = '/api/v1/orders/checkout';
      this.simBody = JSON.stringify({
        cart_id: 'cart_88319a',
        shipping_address_id: 'addr_4412',
        coupon_code: 'ENTERPRISE20',
        total_cents: 8900
      }, null, 2);
    } else if (preset === 'webhook') {
      this.simKey = 'idem_wh_' + Math.random().toString(16).substring(2, 10);
      this.simMethod = 'POST';
      this.simPath = '/api/v1/webhooks/incoming/shopify';
      this.simBody = JSON.stringify({
        event: 'orders/fulfilled',
        order_id: 992314,
        tracking_number: '1Z9999999999999999'
      }, null, 2);
    }
  }

  formatJsonBody(): void {
    try {
      const parsed = JSON.parse(this.simBody);
      this.simBody = JSON.stringify(parsed, null, 2);
    } catch {
      // Keep as-is if invalid JSON
    }
  }

  selectRecordForSimulator(row: IdempotencyRecord): void {
    this.simKey = row.key;
    this.simMethod = row.httpMethod;
    this.simPath = row.requestPath;
    if (row.responseBody) {
      this.simBody = this.formatResponseBody(row.responseBody);
    }
  }

  getStatusBadgeClass(statusCode: number): string {
    if (statusCode >= 200 && statusCode < 300) {
      return 'bg-emerald-500/20 text-emerald-300 border border-emerald-500/40';
    } else if (statusCode === 409) {
      return 'bg-amber-500/20 text-amber-300 border border-amber-500/40';
    } else if (statusCode === 422) {
      return 'bg-rose-500/20 text-rose-300 border border-rose-500/40';
    }
    return 'bg-slate-700 text-slate-300 border border-slate-600';
  }

  getStatusClass(status: string): string {
    switch (status) {
      case 'Completed':
        return 'bg-emerald-500/10 text-emerald-400 border border-emerald-500/30';
      case 'InProgress':
        return 'bg-amber-500/10 text-amber-400 border border-amber-500/30 animate-pulse';
      case 'PayloadMismatch':
        return 'bg-rose-500/10 text-rose-400 border border-rose-500/30';
      case 'Failed':
        return 'bg-red-500/10 text-red-400 border border-red-500/30';
      default:
        return 'bg-slate-800 text-slate-300';
    }
  }

  getMethodBadgeClass(method: string): string {
    switch (method.toUpperCase()) {
      case 'POST':
        return 'bg-violet-900/40 text-violet-300 border border-violet-700/50';
      case 'PUT':
        return 'bg-sky-900/40 text-sky-300 border border-sky-700/50';
      case 'PATCH':
        return 'bg-amber-900/40 text-amber-300 border border-amber-700/50';
      case 'DELETE':
        return 'bg-rose-900/40 text-rose-300 border border-rose-700/50';
      default:
        return 'bg-slate-800 text-slate-300';
    }
  }

  getExpiresInText(expiresAt: string): string {
    const diff = new Date(expiresAt).getTime() - Date.now();
    if (diff <= 0) return 'Expired';
    const hours = Math.floor(diff / (1000 * 60 * 60));
    const mins = Math.floor((diff % (1000 * 60 * 60)) / (1000 * 60));
    const secs = Math.floor((diff % (1000 * 60)) / 1000);
    if (hours > 0) return `${hours}h ${mins}m left`;
    if (mins > 0) return `${mins}m ${secs}s left`;
    return `${secs}s left`;
  }

  getHeaderEntries(headers?: Record<string, string>): Array<[string, string]> {
    return headers ? Object.entries(headers) : [];
  }

  formatResponseBody(body?: string | null): string {
    if (!body) return 'null';
    try {
      const parsed = JSON.parse(body);
      return JSON.stringify(parsed, null, 2);
    } catch {
      return body;
    }
  }

  private constructFallbackSimulation(mode: string): SimulateResponse {
    const isConflict = mode === 'conflict';
    const isMismatch = mode === 'mismatch';
    const isReplay = mode === 'replay';
    const statusCode = isConflict ? 409 : isMismatch ? 422 : isReplay ? 200 : 201;

    return {
      statusCode,
      success: !isConflict && !isMismatch,
      replayDetected: isReplay,
      status: isConflict ? 'InProgress' : isMismatch ? 'PayloadMismatch' : 'Completed',
      message: isConflict
        ? 'Concurrent in-flight collision prevented'
        : isMismatch
        ? 'Payload mismatch blocked'
        : isReplay
        ? 'Idempotent replay detected'
        : 'Initial request processed',
      fingerprint: '3b89a1c8902f4a8b792100df124e88319a99887766554433221100ffeeddccba',
      headers: {
        'X-Idempotency-Key': this.simKey,
        'X-Idempotency-Replay': isReplay ? 'true' : 'false',
        'Content-Type': 'application/json'
      },
      responseBody: JSON.stringify({
        status: isConflict ? 'conflict' : isMismatch ? 'error' : 'confirmed',
        statusCode,
        simulated: true
      }, null, 2),
      replayCount: isReplay ? 1 : 0,
      expiresAt: new Date(Date.now() + 86400000).toISOString(),
      executionDurationMs: 3,
      metrics: this.metrics()
    };
  }

  private getFallbackRecords(): IdempotencyRecord[] {
    const now = Date.now();
    return [
      {
        key: 'idem_pi_9f81a7b2c01d',
        tenantId: 'tenant-default',
        httpMethod: 'POST',
        requestPath: '/api/v1/payments/intents',
        fingerprint: '3b89a1c8902f4a8b792100df124e88319a99887766554433221100ffeeddccba',
        status: 'Completed',
        createdAt: new Date(now - 45 * 60000).toISOString(),
        updatedAt: new Date(now - 5 * 60000).toISOString(),
        expiresAt: new Date(now + 23 * 3600000).toISOString(),
        lockTtlSeconds: 86400,
        responseStatusCode: 200,
        responseHeaders: { 'X-Payment-Gateway': 'Stripe-v2026', 'Content-Type': 'application/json' },
        responseBody: '{"id": "pi_3MtwxZLkdIwHu7ix28a3tqZ9", "amount": 14999, "currency": "usd", "status": "succeeded"}',
        replayCount: 3,
        lastReplayedAt: new Date(now - 5 * 60000).toISOString(),
        isExpired: false
      },
      {
        key: 'idem_chk_5b72e104a3f1',
        tenantId: 'tenant-default',
        httpMethod: 'POST',
        requestPath: '/api/v1/orders/checkout',
        fingerprint: 'a12bc900f81239845781a99b456ce12984712903847120938471209384712093',
        status: 'Completed',
        createdAt: new Date(now - 120 * 60000).toISOString(),
        updatedAt: new Date(now - 30 * 60000).toISOString(),
        expiresAt: new Date(now + 22 * 3600000).toISOString(),
        lockTtlSeconds: 86400,
        responseStatusCode: 201,
        responseHeaders: { 'Content-Type': 'application/json' },
        responseBody: '{"order_id": "ord_20261030_994", "status": "confirmed"}',
        replayCount: 1,
        lastReplayedAt: new Date(now - 30 * 60000).toISOString(),
        isExpired: false
      },
      {
        key: 'idem_wh_3c49d881902f',
        tenantId: 'tenant-default',
        httpMethod: 'POST',
        requestPath: '/api/v1/webhooks/incoming/shopify',
        fingerprint: '887766554433221100ffeeddccbbaa99887766554433221100ffeeddccbbaa99',
        status: 'Completed',
        createdAt: new Date(now - 240 * 60000).toISOString(),
        updatedAt: new Date(now - 12 * 60000).toISOString(),
        expiresAt: new Date(now + 20 * 3600000).toISOString(),
        lockTtlSeconds: 86400,
        responseStatusCode: 200,
        responseHeaders: { 'Content-Type': 'application/json' },
        responseBody: '{"acknowledged": true, "webhook_id": "wh_dispatch_4401"}',
        replayCount: 5,
        lastReplayedAt: new Date(now - 12 * 60000).toISOString(),
        isExpired: false
      },
      {
        key: 'idem_sub_8a1200df124e',
        tenantId: 'tenant-default',
        httpMethod: 'POST',
        requestPath: '/api/v1/billing/subscriptions/sub_8831/renew',
        fingerprint: 'ff11223344556677889900aabbccddeeff0011223344556677889900aabbccdd',
        status: 'InProgress',
        createdAt: new Date(now - 10000).toISOString(),
        updatedAt: new Date(now - 10000).toISOString(),
        expiresAt: new Date(now + 50000).toISOString(),
        lockTtlSeconds: 60,
        replayCount: 0,
        isExpired: false
      },
      {
        key: 'idem_refund_7e33a101b4c9',
        tenantId: 'tenant-default',
        httpMethod: 'POST',
        requestPath: '/api/v1/payments/refunds',
        fingerprint: '99001122334455667788aabbccddeeff0011223344556677889900aabbccddee',
        status: 'PayloadMismatch',
        createdAt: new Date(now - 3600000).toISOString(),
        updatedAt: new Date(now - 15 * 60000).toISOString(),
        expiresAt: new Date(now + 23 * 3600000).toISOString(),
        lockTtlSeconds: 86400,
        responseStatusCode: 422,
        replayCount: 0,
        errorMessage: 'Payload checksum altered during subsequent submission with same idempotency key.',
        isExpired: false
      },
      {
        key: 'idem_payout_110a9c84e55f',
        tenantId: 'tenant-default',
        httpMethod: 'POST',
        requestPath: '/api/v1/transfers/payouts',
        fingerprint: '556677889900aabbccddeeff0011223344556677889900aabbccddeeff001122',
        status: 'Completed',
        createdAt: new Date(now - 6 * 3600000).toISOString(),
        updatedAt: new Date(now - 3600000).toISOString(),
        expiresAt: new Date(now + 18 * 3600000).toISOString(),
        lockTtlSeconds: 86400,
        responseStatusCode: 200,
        responseHeaders: { 'Content-Type': 'application/json' },
        responseBody: '{"payout_id": "po_99011492", "status": "paid"}',
        replayCount: 2,
        lastReplayedAt: new Date(now - 3600000).toISOString(),
        isExpired: false
      }
    ];
  }
}
