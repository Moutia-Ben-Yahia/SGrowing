import { Component, ChangeDetectionStrategy, signal, computed, inject, Optional } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { TranslationService } from '../../core/services/translation.service';

export interface OutboxItem {
  id: string;
  tenantId: string;
  eventType: string;
  aggregateId: string;
  payloadJson: string;
  deduplicationHash: string;
  status: 'Pending' | 'Processing' | 'Published' | 'Failed' | 'QuarantinedDeadLetter' | 'Replayed';
  retryCount: number;
  maxRetries: number;
  lastError?: string;
  createdAtUtc: string;
  processedAtUtc?: string;
  nextAttemptAtUtc?: string;
  correlationId?: string;
  partitionKey?: string;
  deadLetterReason?: string;
  quarantinedAtUtc?: string;
  headers?: Record<string, string>;
}

export interface DedupLedgerEntry {
  hash: string;
  eventId: string;
  eventType: string;
  aggregateId: string;
  firstSeenAt: string;
  status: string;
}

export interface OutboxStatistics {
  totalEnqueued: number;
  totalPublished: number;
  totalDeduplicationHits: number;
  deadLetterDepth: number;
  currentPending: number;
  successRatePct: number;
  avgLatencyMs: number;
}

@Component({
  selector: 'app-event-outbox-mesh-studio',
  standalone: true,
  imports: [CommonModule, FormsModule],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div
      class="p-6 max-w-7xl mx-auto space-y-6 text-slate-100 min-h-screen"
      [attr.dir]="isRtl() ? 'rtl' : 'ltr'"
      data-testid="event-outbox-studio"
    >
      <!-- Header Banner -->
      <div class="flex flex-col md:flex-row justify-between items-start md:items-center gap-4 bg-slate-900/90 border border-slate-800 p-6 rounded-2xl shadow-2xl backdrop-blur-md">
        <div>
          <div class="flex items-center gap-3 flex-wrap">
            <span class="px-3 py-1 text-xs font-semibold rounded-full bg-cyan-500/20 text-cyan-400 border border-cyan-500/30">
              Horizon 116 • Domain K1 &amp; PRD 8.2 / 8.3
            </span>
            <span class="text-xs text-slate-400 font-mono">
              SHA-256 Idempotency • Exponential Backoff • DLQ Quarantine &amp; Replay
            </span>
          </div>
          <h1 class="text-2xl font-bold text-white mt-2 flex items-center gap-2">
            <span>Enterprise Transactional Outbox &amp; Resilient Streaming Mesh</span>
          </h1>
          <p class="text-slate-400 text-sm mt-1 max-w-3xl">
            Zero-loss transactional event publishing for core enterprise lifecycles (OrderPaid, DealWon, LeadQualified, TicketEscalated)
            with collision suppression, backoff retry policies, and interactive poison message dead-letter quarantine.
          </p>
        </div>

        <div class="flex items-center gap-3 flex-wrap">
          <button
            type="button"
            (click)="dispatchPendingBatch()"
            [disabled]="isDispatching() || stats().currentPending === 0"
            data-testid="dispatch-batch-btn"
            class="px-4 py-2 bg-gradient-to-r from-indigo-600 to-indigo-500 hover:from-indigo-500 hover:to-indigo-400 text-white text-sm font-medium rounded-xl transition shadow-lg shadow-indigo-600/30 flex items-center gap-2 disabled:opacity-50 disabled:cursor-not-allowed cursor-pointer"
          >
            @if (isDispatching()) {
              <span class="animate-spin inline-block w-4 h-4 border-2 border-current border-t-transparent rounded-full"></span>
              <span>Dispatching...</span>
            } @else {
              <span>⚡ Dispatch Pending Batch</span>
            }
          </button>
          <button
            type="button"
            (click)="resetMeshDemo()"
            data-testid="reset-mesh-btn"
            class="px-4 py-2 bg-slate-800 hover:bg-slate-700 text-slate-300 hover:text-white text-sm font-medium rounded-xl transition border border-slate-700 cursor-pointer"
          >
            ↺ Reset State
          </button>
        </div>
      </div>

      <!-- Toast / Alert Notification -->
      @if (statusToast(); as toast) {
        <div
          data-testid="status-toast"
          class="px-4 py-3 rounded-xl border flex items-center justify-between text-sm transition-all"
          [class.bg-emerald-950]="toast.type === 'success'"
          [class.border-emerald-800]="toast.type === 'success'"
          [class.text-emerald-300]="toast.type === 'success'"
          [class.bg-rose-950]="toast.type === 'error'"
          [class.border-rose-800]="toast.type === 'error'"
          [class.text-rose-300]="toast.type === 'error'"
          [class.bg-cyan-950]="toast.type === 'info'"
          [class.border-cyan-800]="toast.type === 'info'"
          [class.text-cyan-300]="toast.type === 'info'"
        >
          <div class="flex items-center gap-2">
            <span>{{ toast.message }}</span>
          </div>
          <button
            type="button"
            (click)="statusToast.set(null)"
            class="text-xs opacity-70 hover:opacity-100 cursor-pointer ml-4"
          >
            ✕
          </button>
        </div>
      }

      <!-- KPI Metrics Grid -->
      <div class="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-6 gap-4">
        <!-- Total Enqueued -->
        <div class="bg-slate-900/80 border border-slate-800 p-5 rounded-2xl shadow-sm">
          <span class="text-xs font-medium text-slate-400">Total Enqueued</span>
          <p class="text-2xl font-bold text-white mt-1" data-testid="metric-total-enqueued">
            {{ stats().totalEnqueued }}
          </p>
          <span class="text-xs text-cyan-400 mt-1 block">Transactional Commit</span>
        </div>

        <!-- Published & Acked -->
        <div class="bg-slate-900/80 border border-slate-800 p-5 rounded-2xl shadow-sm">
          <span class="text-xs font-medium text-slate-400">Published &amp; Ack'd</span>
          <p class="text-2xl font-bold text-emerald-400 mt-1" data-testid="metric-total-published">
            {{ stats().totalPublished }}
          </p>
          <span class="text-xs text-slate-400 mt-1 block">Zero dual-write loss</span>
        </div>

        <!-- Pending in Queue -->
        <div class="bg-slate-900/80 border border-slate-800 p-5 rounded-2xl shadow-sm">
          <span class="text-xs font-medium text-slate-400">Pending Outbox</span>
          <p class="text-2xl font-bold text-amber-400 mt-1" data-testid="metric-pending-count">
            {{ stats().currentPending }}
          </p>
          <span class="text-xs text-slate-400 mt-1 block">Ready for drain</span>
        </div>

        <!-- SHA-256 Deduplication Hits -->
        <div class="bg-slate-900/80 border border-slate-800 p-5 rounded-2xl shadow-sm">
          <span class="text-xs font-medium text-slate-400">Dedup Shielded</span>
          <p class="text-2xl font-bold text-indigo-400 mt-1" data-testid="metric-dedup-hits">
            {{ stats().totalDeduplicationHits }}
          </p>
          <span class="text-xs text-indigo-300 mt-1 block">Duplicates suppressed</span>
        </div>

        <!-- Dead Letter Queue Depth -->
        <div class="bg-slate-900/80 border border-slate-800 p-5 rounded-2xl shadow-sm">
          <span class="text-xs font-medium text-slate-400">DLQ Quarantine</span>
          <p
            class="text-2xl font-bold mt-1"
            [class.text-rose-400]="stats().deadLetterDepth > 0"
            [class.text-slate-400]="stats().deadLetterDepth === 0"
            data-testid="metric-dlq-depth"
          >
            {{ stats().deadLetterDepth }}
          </p>
          <span class="text-xs text-rose-400/80 mt-1 block">Poison isolated</span>
        </div>

        <!-- Success Rate -->
        <div class="bg-slate-900/80 border border-slate-800 p-5 rounded-2xl shadow-sm">
          <span class="text-xs font-medium text-slate-400">Delivery Rate</span>
          <p class="text-2xl font-bold text-emerald-400 mt-1" data-testid="metric-success-rate">
            {{ stats().successRatePct }}%
          </p>
          <span class="text-xs text-slate-400 mt-1 block">Avg {{ stats().avgLatencyMs }}ms</span>
        </div>
      </div>

      <!-- Navigation Tabs & Filter Bar -->
      <div class="flex flex-col sm:flex-row justify-between items-start sm:items-center gap-4 border-b border-slate-800 pb-2">
        <div class="flex items-center gap-2">
          <button
            type="button"
            (click)="setTab('mesh')"
            data-testid="tab-mesh-btn"
            class="px-4 py-2 rounded-xl text-sm font-medium transition cursor-pointer"
            [class.bg-indigo-600]="selectedTab() === 'mesh'"
            [class.text-white]="selectedTab() === 'mesh'"
            [class.bg-slate-800]="selectedTab() !== 'mesh'"
            [class.text-slate-400]="selectedTab() !== 'mesh'"
          >
            Live Outbox Stream ({{ events().length }})
          </button>
          <button
            type="button"
            (click)="setTab('dlq')"
            data-testid="tab-dlq-btn"
            class="px-4 py-2 rounded-xl text-sm font-medium transition cursor-pointer flex items-center gap-2"
            [class.bg-rose-600]="selectedTab() === 'dlq'"
            [class.text-white]="selectedTab() === 'dlq'"
            [class.bg-slate-800]="selectedTab() !== 'dlq'"
            [class.text-slate-400]="selectedTab() !== 'dlq'"
          >
            <span>Dead Letter Queue (DLQ)</span>
            <span class="px-2 py-0.5 text-xs rounded-full bg-rose-500/20 text-rose-300 font-bold border border-rose-500/30">
              {{ dlqCount() }}
            </span>
          </button>
          <button
            type="button"
            (click)="setTab('dedup')"
            data-testid="tab-dedup-btn"
            class="px-4 py-2 rounded-xl text-sm font-medium transition cursor-pointer"
            [class.bg-cyan-600]="selectedTab() === 'dedup'"
            [class.text-white]="selectedTab() === 'dedup'"
            [class.bg-slate-800]="selectedTab() !== 'dedup'"
            [class.text-slate-400]="selectedTab() !== 'dedup'"
          >
            SHA-256 Ledger ({{ ledger().length }})
          </button>
          <button
            type="button"
            (click)="setTab('simulate')"
            data-testid="tab-simulate-btn"
            class="px-4 py-2 rounded-xl text-sm font-medium transition cursor-pointer"
            [class.bg-amber-600]="selectedTab() === 'simulate'"
            [class.text-white]="selectedTab() === 'simulate'"
            [class.bg-slate-800]="selectedTab() !== 'simulate'"
            [class.text-slate-400]="selectedTab() !== 'simulate'"
          >
            Event Generator &amp; Poison Lab
          </button>
        </div>

        <!-- Filter Controls -->
        <div class="flex items-center gap-3 w-full sm:w-auto">
          <select
            [ngModel]="filterEventType()"
            (ngModelChange)="setFilter($event)"
            data-testid="filter-event-type-select"
            class="bg-slate-800 border border-slate-700 text-xs text-slate-300 rounded-xl px-3 py-2 outline-none focus:border-indigo-500"
          >
            <option value="all">All Event Types</option>
            <option value="OrderPaid">OrderPaid</option>
            <option value="DealWon">DealWon</option>
            <option value="LeadQualified">LeadQualified</option>
            <option value="TicketEscalated">TicketEscalated</option>
          </select>
          <input
            type="text"
            [ngModel]="searchTerm()"
            (ngModelChange)="searchTerm.set($event)"
            data-testid="search-input"
            placeholder="Search aggregate or payload..."
            class="bg-slate-800 border border-slate-700 text-xs text-slate-300 rounded-xl px-3 py-2 outline-none focus:border-indigo-500 w-44"
          />
        </div>
      </div>

      <!-- Tab 1: Live Outbox Stream -->
      @if (selectedTab() === 'mesh') {
        <div class="bg-slate-900/80 border border-slate-800 rounded-2xl p-6 shadow-xl space-y-4">
          <div class="flex justify-between items-center">
            <h2 class="text-lg font-bold text-white">Outbox Event Mesh Stream</h2>
            <span class="text-xs text-slate-400 font-mono">Showing {{ filteredEvents().length }} events</span>
          </div>

          <div class="overflow-x-auto">
            <table class="w-full text-left text-sm text-slate-300" data-testid="outbox-stream-table">
              <thead class="text-xs uppercase bg-slate-800/60 text-slate-400 border-b border-slate-700">
                <tr>
                  <th class="py-3 px-4">Event Type</th>
                  <th class="py-3 px-4">Aggregate ID</th>
                  <th class="py-3 px-4">SHA-256 Hash</th>
                  <th class="py-3 px-4 text-center">Retries</th>
                  <th class="py-3 px-4 text-center">Status</th>
                  <th class="py-3 px-4 text-right">Created</th>
                  <th class="py-3 px-4 text-right">Actions</th>
                </tr>
              </thead>
              <tbody class="divide-y divide-slate-800">
                @for (evt of filteredEvents(); track evt.id) {
                  <tr class="hover:bg-slate-800/40 transition">
                    <td class="py-3 px-4">
                      <span
                        class="px-2.5 py-1 text-xs rounded-full font-semibold border"
                        [class.bg-emerald-500/20]="evt.eventType === 'OrderPaid'"
                        [class.text-emerald-400]="evt.eventType === 'OrderPaid'"
                        [class.border-emerald-500/30]="evt.eventType === 'OrderPaid'"
                        [class.bg-cyan-500/20]="evt.eventType === 'DealWon'"
                        [class.text-cyan-400]="evt.eventType === 'DealWon'"
                        [class.border-cyan-500/30]="evt.eventType === 'DealWon'"
                        [class.bg-indigo-500/20]="evt.eventType === 'LeadQualified'"
                        [class.text-indigo-400]="evt.eventType === 'LeadQualified'"
                        [class.border-indigo-500/30]="evt.eventType === 'LeadQualified'"
                        [class.bg-rose-500/20]="evt.eventType === 'TicketEscalated'"
                        [class.text-rose-400]="evt.eventType === 'TicketEscalated'"
                        [class.border-rose-500/30]="evt.eventType === 'TicketEscalated'"
                      >
                        {{ evt.eventType }}
                      </span>
                    </td>
                    <td class="py-3 px-4 font-mono text-xs font-semibold text-slate-200">
                      {{ evt.aggregateId }}
                    </td>
                    <td class="py-3 px-4 font-mono text-xs text-slate-500" title="{{ evt.deduplicationHash }}">
                      {{ evt.deduplicationHash.slice(0, 12) }}...{{ evt.deduplicationHash.slice(-6) }}
                    </td>
                    <td class="py-3 px-4 text-center font-mono text-xs">
                      <span [class.text-rose-400]="evt.retryCount > 0" [class.text-slate-400]="evt.retryCount === 0">
                        {{ evt.retryCount }} / {{ evt.maxRetries }}
                      </span>
                    </td>
                    <td class="py-3 px-4 text-center">
                      <span
                        class="px-2.5 py-0.5 text-xs rounded-full font-medium border"
                        [class.bg-emerald-500/20]="evt.status === 'Published'"
                        [class.text-emerald-400]="evt.status === 'Published'"
                        [class.border-emerald-500/30]="evt.status === 'Published'"
                        [class.bg-amber-500/20]="evt.status === 'Pending'"
                        [class.text-amber-400]="evt.status === 'Pending'"
                        [class.border-amber-500/30]="evt.status === 'Pending'"
                        [class.bg-rose-500/20]="evt.status === 'QuarantinedDeadLetter'"
                        [class.text-rose-400]="evt.status === 'QuarantinedDeadLetter'"
                        [class.border-rose-500/30]="evt.status === 'QuarantinedDeadLetter'"
                        [class.bg-orange-500/20]="evt.status === 'Failed'"
                        [class.text-orange-400]="evt.status === 'Failed'"
                        [class.border-orange-500/30]="evt.status === 'Failed'"
                      >
                        {{ evt.status }}
                      </span>
                    </td>
                    <td class="py-3 px-4 text-right text-xs text-slate-400 font-mono">
                      {{ evt.createdAtUtc | slice:11:19 }}
                    </td>
                    <td class="py-3 px-4 text-right">
                      <button
                        type="button"
                        (click)="inspectEvent(evt)"
                        data-testid="inspect-event-btn"
                        class="px-2 py-1 text-xs bg-slate-800 hover:bg-slate-700 text-indigo-300 rounded-lg transition border border-slate-700 cursor-pointer"
                      >
                        Inspect
                      </button>
                    </td>
                  </tr>
                } @empty {
                  <tr>
                    <td colspan="7" class="py-6 text-center text-slate-500">
                      No events matching current filter or search criteria.
                    </td>
                  </tr>
                }
              </tbody>
            </table>
          </div>
        </div>
      }

      <!-- Tab 2: Dead Letter Queue Quarantine -->
      @if (selectedTab() === 'dlq') {
        <div class="bg-slate-900/80 border border-slate-800 rounded-2xl p-6 shadow-xl space-y-4">
          <div class="flex flex-col sm:flex-row justify-between items-start sm:items-center gap-3">
            <div>
              <h2 class="text-lg font-bold text-white flex items-center gap-2">
                <span>Dead Letter Queue (DLQ) Quarantine</span>
                <span class="px-2.5 py-0.5 text-xs rounded-full bg-rose-500/20 text-rose-400 border border-rose-500/30 font-mono">
                  Poison Isolated: {{ dlqItems().length }}
                </span>
              </h2>
              <p class="text-slate-400 text-xs mt-1">
                Poison messages and unrecoverable downstream failures quarantined after retry exhaustion.
              </p>
            </div>

            <div class="flex items-center gap-2">
              <button
                type="button"
                (click)="replaySelectedDlq()"
                [disabled]="!hasSelectedDlq()"
                data-testid="replay-selected-btn"
                class="px-3 py-1.5 bg-emerald-600 hover:bg-emerald-500 text-white text-xs font-semibold rounded-xl transition shadow disabled:opacity-40 disabled:cursor-not-allowed cursor-pointer"
              >
                ↻ Replay Selected ({{ selectedDlqIds().length }})
              </button>
              <button
                type="button"
                (click)="selectAllDlq()"
                data-testid="select-all-dlq-btn"
                class="px-3 py-1.5 bg-slate-800 hover:bg-slate-700 text-slate-300 text-xs font-medium rounded-xl border border-slate-700 transition cursor-pointer"
              >
                Select All
              </button>
              <button
                type="button"
                (click)="deselectAllDlq()"
                data-testid="deselect-all-dlq-btn"
                class="px-3 py-1.5 bg-slate-800 hover:bg-slate-700 text-slate-400 text-xs font-medium rounded-xl border border-slate-700 transition cursor-pointer"
              >
                Clear Selection
              </button>
            </div>
          </div>

          <div class="overflow-x-auto">
            <table class="w-full text-left text-sm text-slate-300" data-testid="dlq-table">
              <thead class="text-xs uppercase bg-rose-950/40 text-rose-300 border-b border-rose-900/50">
                <tr>
                  <th class="py-3 px-3 w-8">
                    <span class="sr-only">Select</span>
                  </th>
                  <th class="py-3 px-4">Event Type</th>
                  <th class="py-3 px-4">Aggregate ID</th>
                  <th class="py-3 px-4">Quarantine Reason &amp; Last Error</th>
                  <th class="py-3 px-4 text-center">Retries Exhausted</th>
                  <th class="py-3 px-4 text-right">Quarantined At</th>
                  <th class="py-3 px-4 text-right">Actions</th>
                </tr>
              </thead>
              <tbody class="divide-y divide-slate-800">
                @for (item of dlqItems(); track item.id) {
                  <tr class="hover:bg-slate-800/40 transition">
                    <td class="py-3 px-3 text-center">
                      <input
                        type="checkbox"
                        [checked]="isDlqSelected(item.id)"
                        (change)="toggleSelectDlq(item.id)"
                        data-testid="dlq-checkbox"
                        class="rounded text-indigo-600 focus:ring-0 cursor-pointer"
                      />
                    </td>
                    <td class="py-3 px-4 font-semibold text-rose-300 text-xs">
                      {{ item.eventType }}
                    </td>
                    <td class="py-3 px-4 font-mono text-xs text-white">
                      {{ item.aggregateId }}
                    </td>
                    <td class="py-3 px-4 max-w-md">
                      <div class="text-xs text-rose-200 line-clamp-2">
                        {{ item.deadLetterReason || item.lastError || 'Retry count exceeded max threshold' }}
                      </div>
                    </td>
                    <td class="py-3 px-4 text-center font-mono text-xs font-bold text-rose-400">
                      {{ item.retryCount }} / {{ item.maxRetries }}
                    </td>
                    <td class="py-3 px-4 text-right font-mono text-xs text-slate-400">
                      {{ (item.quarantinedAtUtc || item.createdAtUtc) | slice:11:19 }}
                    </td>
                    <td class="py-3 px-4 text-right space-x-1">
                      <button
                        type="button"
                        (click)="replaySingleDlq(item.id)"
                        data-testid="replay-single-btn"
                        class="px-2 py-1 text-xs bg-emerald-600/30 hover:bg-emerald-600/50 text-emerald-300 rounded-lg transition border border-emerald-500/40 cursor-pointer"
                      >
                        Replay
                      </button>
                      <button
                        type="button"
                        (click)="inspectEvent(item)"
                        data-testid="inspect-dlq-btn"
                        class="px-2 py-1 text-xs bg-slate-800 hover:bg-slate-700 text-slate-300 rounded-lg transition border border-slate-700 cursor-pointer"
                      >
                        Inspect
                      </button>
                      <button
                        type="button"
                        (click)="purgeDlq(item.id)"
                        data-testid="purge-dlq-btn"
                        class="px-2 py-1 text-xs bg-rose-900/40 hover:bg-rose-900/70 text-rose-300 rounded-lg transition border border-rose-800 cursor-pointer"
                      >
                        Purge
                      </button>
                    </td>
                  </tr>
                } @empty {
                  <tr>
                    <td colspan="7" class="py-8 text-center text-slate-500">
                      🎉 No poison messages in Dead Letter Queue! All outbox streams healthy.
                    </td>
                  </tr>
                }
              </tbody>
            </table>
          </div>
        </div>
      }

      <!-- Tab 3: SHA-256 Deduplication Ledger -->
      @if (selectedTab() === 'dedup') {
        <div class="bg-slate-900/80 border border-slate-800 rounded-2xl p-6 shadow-xl space-y-4">
          <div class="flex justify-between items-center">
            <div>
              <h2 class="text-lg font-bold text-white">SHA-256 Idempotency Ledger</h2>
              <p class="text-slate-400 text-xs mt-1">
                Deterministic hash ledger preventing at-least-once double processing across partitioned streams.
              </p>
            </div>
            <button
              type="button"
              (click)="simulateDuplicateCollision()"
              data-testid="simulate-collision-btn"
              class="px-3 py-1.5 bg-indigo-600 hover:bg-indigo-500 text-white text-xs font-semibold rounded-xl transition shadow cursor-pointer"
            >
              Simulate Duplicate Hash Collision
            </button>
          </div>

          <div class="overflow-x-auto">
            <table class="w-full text-left text-sm text-slate-300" data-testid="dedup-ledger-table">
              <thead class="text-xs uppercase bg-slate-800/60 text-slate-400 border-b border-slate-700">
                <tr>
                  <th class="py-3 px-4">SHA-256 Hash Key</th>
                  <th class="py-3 px-4">Event Type</th>
                  <th class="py-3 px-4">Aggregate ID</th>
                  <th class="py-3 px-4">First Registered (UTC)</th>
                  <th class="py-3 px-4 text-center">Status</th>
                </tr>
              </thead>
              <tbody class="divide-y divide-slate-800">
                @for (entry of ledger(); track entry.hash) {
                  <tr class="hover:bg-slate-800/40 transition">
                    <td class="py-3 px-4 font-mono text-xs text-cyan-400">
                      {{ entry.hash }}
                    </td>
                    <td class="py-3 px-4 font-semibold text-slate-200 text-xs">
                      {{ entry.eventType }}
                    </td>
                    <td class="py-3 px-4 font-mono text-xs text-slate-300">
                      {{ entry.aggregateId }}
                    </td>
                    <td class="py-3 px-4 font-mono text-xs text-slate-400">
                      {{ entry.firstSeenAt | slice:0:19 }}
                    </td>
                    <td class="py-3 px-4 text-center">
                      <span class="px-2 py-0.5 text-xs rounded-full bg-slate-800 text-slate-300 border border-slate-700 font-medium">
                        {{ entry.status }}
                      </span>
                    </td>
                  </tr>
                }
              </tbody>
            </table>
          </div>
        </div>
      }

      <!-- Tab 4: Event Generator & Poison Lab -->
      @if (selectedTab() === 'simulate') {
        <div class="grid grid-cols-1 md:grid-cols-2 gap-6">
          <!-- Standard Enterprise Event Generator -->
          <div class="bg-slate-900/80 border border-slate-800 rounded-2xl p-6 shadow-xl space-y-4">
            <h2 class="text-lg font-bold text-white">Enterprise Domain Event Intake</h2>
            <p class="text-slate-400 text-xs">
              Simulate transactional staging of standard domain events directly into the outbox.
            </p>

            <div class="grid grid-cols-1 sm:grid-cols-2 gap-3">
              <button
                type="button"
                (click)="simulateEvent('OrderPaid')"
                data-testid="simulate-order-paid-btn"
                class="p-4 bg-slate-800 hover:bg-slate-700/80 border border-slate-700 rounded-xl text-left transition cursor-pointer group"
              >
                <div class="text-xs font-semibold text-emerald-400 group-hover:text-emerald-300">OrderPaid</div>
                <div class="text-xs text-slate-400 mt-1">E-Commerce CPQ &amp; checkout completion</div>
              </button>

              <button
                type="button"
                (click)="simulateEvent('DealWon')"
                data-testid="simulate-deal-won-btn"
                class="p-4 bg-slate-800 hover:bg-slate-700/80 border border-slate-700 rounded-xl text-left transition cursor-pointer group"
              >
                <div class="text-xs font-semibold text-cyan-400 group-hover:text-cyan-300">DealWon</div>
                <div class="text-xs text-slate-400 mt-1">B2B CRM opportunity advance &amp; commission</div>
              </button>

              <button
                type="button"
                (click)="simulateEvent('LeadQualified')"
                data-testid="simulate-lead-qualified-btn"
                class="p-4 bg-slate-800 hover:bg-slate-700/80 border border-slate-700 rounded-xl text-left transition cursor-pointer group"
              >
                <div class="text-xs font-semibold text-indigo-400 group-hover:text-indigo-300">LeadQualified</div>
                <div class="text-xs text-slate-400 mt-1">AI intent scoring threshold trigger</div>
              </button>

              <button
                type="button"
                (click)="simulateEvent('TicketEscalated')"
                data-testid="simulate-ticket-escalated-btn"
                class="p-4 bg-slate-800 hover:bg-slate-700/80 border border-slate-700 rounded-xl text-left transition cursor-pointer group"
              >
                <div class="text-xs font-semibold text-amber-400 group-hover:text-amber-300">TicketEscalated</div>
                <div class="text-xs text-slate-400 mt-1">SLA breach &amp; voice triage escalation</div>
              </button>
            </div>
          </div>

          <!-- Poison Message Chaos Lab -->
          <div class="bg-slate-900/80 border border-rose-900/40 rounded-2xl p-6 shadow-xl space-y-4">
            <h2 class="text-lg font-bold text-rose-300">Poison Message &amp; Chaos Injection Lab</h2>
            <p class="text-slate-400 text-xs">
              Inject malformed or failing payloads to verify exponential backoff retries and Dead Letter Queue isolation.
            </p>

            <div class="space-y-3">
              <button
                type="button"
                (click)="simulatePoisonEvent()"
                data-testid="simulate-poison-btn"
                class="w-full p-4 bg-rose-950/30 hover:bg-rose-950/60 border border-rose-800/60 rounded-xl text-left transition cursor-pointer group"
              >
                <div class="text-xs font-bold text-rose-400 group-hover:text-rose-300">
                  💀 Inject Poison Message (5x Exhaustion → DLQ)
                </div>
                <div class="text-xs text-slate-400 mt-1">
                  Injects an event with crashing downstream payload. Outbox worker retries 5 times with exponential backoff then moves it into DLQ quarantine.
                </div>
              </button>

              <button
                type="button"
                (click)="simulateDuplicateCollision()"
                data-testid="simulate-duplicate-collision-btn"
                class="w-full p-4 bg-indigo-950/30 hover:bg-indigo-950/60 border border-indigo-800/60 rounded-xl text-left transition cursor-pointer group"
              >
                <div class="text-xs font-bold text-indigo-400 group-hover:text-indigo-300">
                  🛡️ Test SHA-256 Idempotency Collision Guard
                </div>
                <div class="text-xs text-slate-400 mt-1">
                  Sends an identical OrderPaid event payload. The outbox service intercepts the duplicate hash and suppresses dual-write.
                </div>
              </button>
            </div>
          </div>
        </div>
      }

      <!-- Inspect Event Modal / Drawer -->
      @if (selectedDetailEvent(); as detail) {
        <div class="fixed inset-0 bg-black/70 backdrop-blur-sm z-50 flex items-center justify-center p-4">
          <div class="bg-slate-900 border border-slate-700 rounded-2xl max-w-2xl w-full p-6 space-y-4 shadow-2xl overflow-hidden">
            <div class="flex justify-between items-center border-b border-slate-800 pb-3">
              <div>
                <span class="text-xs font-mono text-cyan-400">{{ detail.eventType }}</span>
                <h3 class="text-lg font-bold text-white mt-0.5">Aggregate: {{ detail.aggregateId }}</h3>
              </div>
              <button
                type="button"
                (click)="closeInspectModal()"
                data-testid="close-modal-btn"
                class="text-slate-400 hover:text-white text-lg font-bold cursor-pointer"
              >
                ✕
              </button>
            </div>

            <div class="space-y-3 max-h-96 overflow-y-auto text-xs">
              <div>
                <span class="text-slate-400 font-semibold block mb-1">Payload JSON</span>
                <pre class="bg-slate-950 p-3 rounded-xl border border-slate-800 text-slate-300 font-mono overflow-x-auto">{{ detail.payloadJson }}</pre>
              </div>

              <div>
                <span class="text-slate-400 font-semibold block mb-1">Deduplication SHA-256</span>
                <div class="font-mono text-cyan-400 bg-slate-950 p-2 rounded-lg border border-slate-800 break-all">
                  {{ detail.deduplicationHash }}
                </div>
              </div>

              @if (detail.deadLetterReason || detail.lastError) {
                <div>
                  <span class="text-rose-400 font-semibold block mb-1">Diagnostic Failure &amp; DLQ Reason</span>
                  <div class="bg-rose-950/40 p-3 rounded-xl border border-rose-900 text-rose-300 font-mono">
                    {{ detail.deadLetterReason || detail.lastError }}
                  </div>
                </div>
              }

              <div class="grid grid-cols-2 gap-2 text-slate-300">
                <div>Status: <span class="font-bold text-white">{{ detail.status }}</span></div>
                <div>Retry Count: <span class="font-bold text-white">{{ detail.retryCount }} / {{ detail.maxRetries }}</span></div>
                <div>Correlation ID: <span class="font-mono text-slate-400">{{ detail.correlationId || 'N/A' }}</span></div>
                <div>Created (UTC): <span class="font-mono text-slate-400">{{ detail.createdAtUtc | slice:0:19 }}</span></div>
              </div>
            </div>

            <div class="flex justify-end gap-3 pt-3 border-t border-slate-800">
              @if (detail.status === 'QuarantinedDeadLetter') {
                <button
                  type="button"
                  (click)="replaySingleDlq(detail.id); closeInspectModal()"
                  class="px-4 py-2 bg-emerald-600 hover:bg-emerald-500 text-white font-medium rounded-xl transition cursor-pointer text-xs"
                >
                  ↻ Replay Message
                </button>
              }
              <button
                type="button"
                (click)="closeInspectModal()"
                class="px-4 py-2 bg-slate-800 hover:bg-slate-700 text-slate-300 font-medium rounded-xl transition cursor-pointer text-xs"
              >
                Close
              </button>
            </div>
          </div>
        </div>
      }
    </div>
  `
})
export class EventOutboxMeshStudioComponent {
  public readonly translationService: TranslationService | null = null;

  isRtl = computed(() => this.translationService?.currentLocale() === 'ar-AR');

  constructor(@Optional() translationService?: TranslationService) {
    if (translationService) {
      this.translationService = translationService;
    } else {
      try {
        this.translationService = inject(TranslationService, { optional: true });
      } catch {
        this.translationService = null;
      }
    }
  }

  selectedTab = signal<'mesh' | 'dlq' | 'dedup' | 'simulate'>('mesh');
  filterEventType = signal<string>('all');
  searchTerm = signal<string>('');
  selectedDlqIds = signal<string[]>([]);
  selectedDetailEvent = signal<OutboxItem | null>(null);
  isDispatching = signal<boolean>(false);
  statusToast = signal<{ message: string; type: 'success' | 'error' | 'info' } | null>(null);

  stats = signal<OutboxStatistics>({
    totalEnqueued: 4,
    totalPublished: 2,
    totalDeduplicationHits: 0,
    deadLetterDepth: 1,
    currentPending: 1,
    successRatePct: 66.7,
    avgLatencyMs: 14.2
  });

  events = signal<OutboxItem[]>([
    {
      id: 'evt-ord-001',
      tenantId: '11111111-1111-1111-1111-111111111111',
      eventType: 'OrderPaid',
      aggregateId: 'ORD-2026-8812',
      payloadJson: '{"orderId":"ORD-2026-8812","amount":249.99,"currency":"USD","customerEmail":"sarah.connor@cyberdyne.org","paymentMethod":"Stripe"}',
      deduplicationHash: 'e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855',
      status: 'Published',
      retryCount: 0,
      maxRetries: 5,
      createdAtUtc: new Date(Date.now() - 40 * 60000).toISOString(),
      processedAtUtc: new Date(Date.now() - 39 * 60000).toISOString(),
      correlationId: 'corr-ord-8812'
    },
    {
      id: 'evt-deal-002',
      tenantId: '11111111-1111-1111-1111-111111111111',
      eventType: 'DealWon',
      aggregateId: 'DEAL-9904',
      payloadJson: '{"dealId":"DEAL-9904","dealValue":45000.00,"currency":"USD","accountName":"Acme Enterprise Corp","ownerId":"usr_rebecca"}',
      deduplicationHash: '8f434346648f6b96df89dda901c5176b10a6d83961dd3c1ac88b59b2dc327aa4',
      status: 'Published',
      retryCount: 0,
      maxRetries: 5,
      createdAtUtc: new Date(Date.now() - 25 * 60000).toISOString(),
      processedAtUtc: new Date(Date.now() - 24 * 60000).toISOString(),
      correlationId: 'corr-deal-9904'
    },
    {
      id: 'evt-lead-003',
      tenantId: '11111111-1111-1111-1111-111111111111',
      eventType: 'LeadQualified',
      aggregateId: 'LEAD-4410',
      payloadJson: '{"leadId":"LEAD-4410","leadScore":"A_TIER_HOT","source":"InboundWebinar","contactName":"Alex Vance","contactEmail":"alex@blackmesa.com"}',
      deduplicationHash: '5e884898da28047151d0e56f8dc6292773603d0d6aabbdd62a11ef721d1542d8',
      status: 'Pending',
      retryCount: 0,
      maxRetries: 5,
      createdAtUtc: new Date(Date.now() - 5 * 60000).toISOString(),
      correlationId: 'corr-lead-4410'
    },
    {
      id: 'evt-tck-004',
      tenantId: '11111111-1111-1111-1111-111111111111',
      eventType: 'TicketEscalated',
      aggregateId: 'TCK-1002',
      payloadJson: '{"ticketId":"TCK-1002","priority":"CRITICAL_SEV1","escalationReason":"Database replication lag breached SLA threshold","assigneeId":"eng_triage_bot"}',
      deduplicationHash: '4b227777d4dd1fc61c6f884f48641d02b4d121d3fd328cb08b5531fcacdabf8a',
      status: 'QuarantinedDeadLetter',
      retryCount: 5,
      maxRetries: 5,
      lastError: 'Downstream webhook delivery rejected: 504 Gateway Timeout from https://ops-pager.corp.internal/v1/escalations',
      deadLetterReason: 'Exceeded max retry threshold (5 attempts). Last failure: Downstream webhook delivery rejected: 504 Gateway Timeout.',
      createdAtUtc: new Date(Date.now() - 15 * 60000).toISOString(),
      quarantinedAtUtc: new Date(Date.now() - 2 * 60000).toISOString(),
      correlationId: 'corr-tck-1002'
    }
  ]);

  ledger = signal<DedupLedgerEntry[]>([
    {
      hash: 'e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855',
      eventId: 'evt-ord-001',
      eventType: 'OrderPaid',
      aggregateId: 'ORD-2026-8812',
      firstSeenAt: new Date(Date.now() - 40 * 60000).toISOString(),
      status: 'Published'
    },
    {
      hash: '8f434346648f6b96df89dda901c5176b10a6d83961dd3c1ac88b59b2dc327aa4',
      eventId: 'evt-deal-002',
      eventType: 'DealWon',
      aggregateId: 'DEAL-9904',
      firstSeenAt: new Date(Date.now() - 25 * 60000).toISOString(),
      status: 'Published'
    },
    {
      hash: '5e884898da28047151d0e56f8dc6292773603d0d6aabbdd62a11ef721d1542d8',
      eventId: 'evt-lead-003',
      eventType: 'LeadQualified',
      aggregateId: 'LEAD-4410',
      firstSeenAt: new Date(Date.now() - 5 * 60000).toISOString(),
      status: 'Pending'
    },
    {
      hash: '4b227777d4dd1fc61c6f884f48641d02b4d121d3fd328cb08b5531fcacdabf8a',
      eventId: 'evt-tck-004',
      eventType: 'TicketEscalated',
      aggregateId: 'TCK-1002',
      firstSeenAt: new Date(Date.now() - 15 * 60000).toISOString(),
      status: 'Quarantined'
    }
  ]);

  filteredEvents = computed(() => {
    const type = this.filterEventType();
    const search = this.searchTerm().trim().toLowerCase();
    return this.events().filter(e => {
      const matchType = type === 'all' || e.eventType === type;
      const matchSearch =
        !search ||
        e.aggregateId.toLowerCase().includes(search) ||
        e.payloadJson.toLowerCase().includes(search);
      return matchType && matchSearch;
    });
  });

  dlqItems = computed(() => {
    return this.events().filter(e => e.status === 'QuarantinedDeadLetter');
  });

  dlqCount = computed(() => this.dlqItems().length);

  hasSelectedDlq = computed(() => this.selectedDlqIds().length > 0);

  setTab(tab: 'mesh' | 'dlq' | 'dedup' | 'simulate'): void {
    this.selectedTab.set(tab);
  }

  setFilter(type: string): void {
    this.filterEventType.set(type);
  }

  dispatchPendingBatch(): void {
    this.isDispatching.set(true);

    const pending = this.events().filter(e => e.status === 'Pending');
    if (pending.length === 0) {
      this.isDispatching.set(false);
      return;
    }

    const updated = this.events().map(e => {
      if (e.status === 'Pending') {
        return {
          ...e,
          status: 'Published' as const,
          processedAtUtc: new Date().toISOString()
        };
      }
      return e;
    });

    this.events.set(updated);

    // Update stats
    const newlyPublished = pending.length;
    this.stats.update(s => ({
      ...s,
      totalPublished: s.totalPublished + newlyPublished,
      currentPending: 0,
      successRatePct: Math.round(((s.totalPublished + newlyPublished) / (s.totalPublished + newlyPublished + s.deadLetterDepth)) * 1000) / 10
    }));

    this.isDispatching.set(false);
    this.showToast(`Successfully dispatched batch of ${newlyPublished} pending event(s).`, 'success');
  }

  simulateEvent(type: 'OrderPaid' | 'DealWon' | 'LeadQualified' | 'TicketEscalated'): void {
    const id = `evt-${Date.now().toString(36)}`;
    const rand = Math.floor(Math.random() * 9000 + 1000);
    let aggregateId = '';
    let payload = {};

    switch (type) {
      case 'OrderPaid':
        aggregateId = `ORD-2026-${rand}`;
        payload = {
          orderId: aggregateId,
          amount: 149.99,
          currency: 'USD',
          customerEmail: `customer${rand}@sgrowing.io`,
          paymentMethod: 'CreditCard'
        };
        break;
      case 'DealWon':
        aggregateId = `DEAL-${rand}`;
        payload = {
          dealId: aggregateId,
          dealValue: 75000,
          currency: 'USD',
          accountName: `Apex Corp ${rand}`,
          ownerId: 'usr_sarah'
        };
        break;
      case 'LeadQualified':
        aggregateId = `LEAD-${rand}`;
        payload = {
          leadId: aggregateId,
          leadScore: 'HOT_TIER_A',
          source: 'OrganicSearch',
          contactName: `Lead Contact ${rand}`,
          contactEmail: `lead${rand}@domain.com`
        };
        break;
      case 'TicketEscalated':
        aggregateId = `TCK-${rand}`;
        payload = {
          ticketId: aggregateId,
          priority: 'P1_CRITICAL',
          escalationReason: 'Automated SLA Watchdog escalation',
          assigneeId: 'tier3_support'
        };
        break;
    }

    const payloadJson = JSON.stringify(payload);
    const hash = this.mockSha256(aggregateId + payloadJson);

    const newEvt: OutboxItem = {
      id,
      tenantId: '11111111-1111-1111-1111-111111111111',
      eventType: type,
      aggregateId,
      payloadJson,
      deduplicationHash: hash,
      status: 'Pending',
      retryCount: 0,
      maxRetries: 5,
      createdAtUtc: new Date().toISOString(),
      correlationId: `corr-${rand}`
    };

    this.events.update(list => [newEvt, ...list]);
    this.ledger.update(led => [
      {
        hash,
        eventId: id,
        eventType: type,
        aggregateId,
        firstSeenAt: new Date().toISOString(),
        status: 'Pending'
      },
      ...led
    ]);

    this.stats.update(s => ({
      ...s,
      totalEnqueued: s.totalEnqueued + 1,
      currentPending: s.currentPending + 1
    }));

    this.showToast(`Enqueued new ${type} event (${aggregateId}) into outbox.`, 'success');
  }

  simulateDuplicateCollision(): void {
    // Pick first event
    const first = this.events()[0];
    if (!first) return;

    this.stats.update(s => ({
      ...s,
      totalDeduplicationHits: s.totalDeduplicationHits + 1
    }));

    this.showToast(
      `Duplicate event detected! Hash collision prevented for aggregate ${first.aggregateId}. Suppressed double write.`,
      'info'
    );
  }

  simulatePoisonEvent(): void {
    const rand = Math.floor(Math.random() * 9000 + 1000);
    const aggregateId = `TCK-POISON-${rand}`;
    const payload = {
      ticketId: aggregateId,
      __poison__: true,
      errorTrigger: 'Fatal stream parser exception'
    };
    const payloadJson = JSON.stringify(payload);
    const hash = this.mockSha256(aggregateId + payloadJson);
    const id = `evt-poison-${rand}`;

    const poisonEvt: OutboxItem = {
      id,
      tenantId: '11111111-1111-1111-1111-111111111111',
      eventType: 'TicketEscalated',
      aggregateId,
      payloadJson,
      deduplicationHash: hash,
      status: 'QuarantinedDeadLetter',
      retryCount: 5,
      maxRetries: 5,
      lastError: 'Fatal stream parser exception: downstream consumer terminated unexpectedly.',
      deadLetterReason: 'Exceeded max retry threshold (5 attempts). Quarantined to Dead Letter Queue.',
      createdAtUtc: new Date().toISOString(),
      quarantinedAtUtc: new Date().toISOString(),
      correlationId: `corr-poison-${rand}`
    };

    this.events.update(list => [poisonEvt, ...list]);
    this.ledger.update(led => [
      {
        hash,
        eventId: id,
        eventType: 'TicketEscalated',
        aggregateId,
        firstSeenAt: new Date().toISOString(),
        status: 'Quarantined'
      },
      ...led
    ]);

    this.stats.update(s => ({
      ...s,
      totalEnqueued: s.totalEnqueued + 1,
      deadLetterDepth: s.deadLetterDepth + 1,
      successRatePct: Math.round((s.totalPublished / (s.totalPublished + s.deadLetterDepth + 1)) * 1000) / 10
    }));

    this.showToast(`Poison event ${aggregateId} quarantined to Dead Letter Queue after 5 failed retries.`, 'error');
  }

  isDlqSelected(id: string): boolean {
    return this.selectedDlqIds().includes(id);
  }

  toggleSelectDlq(id: string): void {
    this.selectedDlqIds.update(ids =>
      ids.includes(id) ? ids.filter(i => i !== id) : [...ids, id]
    );
  }

  selectAllDlq(): void {
    this.selectedDlqIds.set(this.dlqItems().map(i => i.id));
  }

  deselectAllDlq(): void {
    this.selectedDlqIds.set([]);
  }

  replaySelectedDlq(): void {
    const selected = this.selectedDlqIds();
    if (selected.length === 0) return;

    this.events.update(list =>
      list.map(e => {
        if (selected.includes(e.id)) {
          return {
            ...e,
            status: 'Pending' as const,
            retryCount: 0,
            lastError: undefined,
            deadLetterReason: undefined,
            quarantinedAtUtc: undefined
          };
        }
        return e;
      })
    );

    const count = selected.length;
    this.selectedDlqIds.set([]);

    this.stats.update(s => ({
      ...s,
      deadLetterDepth: Math.max(0, s.deadLetterDepth - count),
      currentPending: s.currentPending + count,
      successRatePct: Math.round((s.totalPublished / Math.max(1, s.totalPublished + Math.max(0, s.deadLetterDepth - count))) * 1000) / 10
    }));

    this.showToast(`Replayed ${count} dead-letter message(s) back to active pending queue.`, 'success');
  }

  replaySingleDlq(id: string): void {
    this.selectedDlqIds.set([id]);
    this.replaySelectedDlq();
  }

  purgeDlq(id: string): void {
    this.events.update(list => list.filter(e => e.id !== id));
    this.ledger.update(led => led.filter(l => l.eventId !== id));
    this.selectedDlqIds.update(ids => ids.filter(i => i !== id));

    this.stats.update(s => ({
      ...s,
      deadLetterDepth: Math.max(0, s.deadLetterDepth - 1),
      successRatePct: Math.round((s.totalPublished / Math.max(1, s.totalPublished + Math.max(0, s.deadLetterDepth - 1))) * 1000) / 10
    }));

    this.showToast(`Purged dead-letter message ${id} from quarantine ledger.`, 'info');
  }

  inspectEvent(evt: OutboxItem): void {
    this.selectedDetailEvent.set(evt);
  }

  closeInspectModal(): void {
    this.selectedDetailEvent.set(null);
  }

  resetMeshDemo(): void {
    this.stats.set({
      totalEnqueued: 4,
      totalPublished: 2,
      totalDeduplicationHits: 0,
      deadLetterDepth: 1,
      currentPending: 1,
      successRatePct: 66.7,
      avgLatencyMs: 14.2
    });
    this.selectedDlqIds.set([]);
    this.selectedDetailEvent.set(null);
    this.showToast('Outbox mesh state reset to initial demo values.', 'info');
  }

  private showToast(message: string, type: 'success' | 'error' | 'info'): void {
    this.statusToast.set({ message, type });
  }

  private mockSha256(input: string): string {
    let hash = 0;
    for (let i = 0; i < input.length; i++) {
      const char = input.charCodeAt(i);
      hash = (hash << 5) - hash + char;
      hash |= 0;
    }
    const hex = Math.abs(hash).toString(16).padStart(8, '0');
    return (hex + hex + hex + hex + hex + hex + hex + hex).slice(0, 64);
  }
}
