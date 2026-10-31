import { Component, signal, computed, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { HttpClient } from '@angular/common/http';

export type EventBrokerMode = 'InMemoryRingBuffer' | 'RedisStreams' | 'NatsJetStream';
export type DlqStatus = 'Quarantined' | 'PendingRetry' | 'Replayed' | 'Purged';

export interface PartitionMetricUi {
  partitionIndex: number;
  assignedWorker: string;
  currentDepth: number;
  consumerLag: number;
  totalProcessed: number;
  lastEventTimestampUtc: string | null;
}

export interface EventMeshMetricsUi {
  totalPublished: number;
  totalConsumed: number;
  totalDeduplicated: number;
  dlqSize: number;
  partitionCount: number;
  partitionLags: PartitionMetricUi[];
  activeBrokerMode: EventBrokerMode;
  throughputPerSecond: number;
  lastEventPublishedAtUtc: string | null;
  isHealthy: boolean;
  brokerMetadata: Record<string, string>;
}

export interface DlqItemUi {
  id: string;
  tenantId: string;
  eventId: string;
  topic: string;
  partitionKey: string | null;
  partitionIndex: number;
  eventType: string;
  payload: string;
  attemptCount: number;
  failureReason: string;
  stackTrace: string | null;
  firstFailedAtUtc: string;
  lastAttemptAtUtc: string;
  nextRetryAtUtc: string | null;
  status: DlqStatus;
}

export interface PublishResultUi {
  success: boolean;
  eventId: string;
  topic: string;
  partitionIndex: number;
  eventFingerprint: string;
  deduplicated: boolean;
  brokerMode: EventBrokerMode;
  publishedAtUtc: string;
  sequenceNumber: number;
  subject?: string;
  redisStreamId?: string;
  error?: string;
}

@Component({
  selector: 'app-distributed-event-mesh-cockpit',
  standalone: true,
  imports: [CommonModule, FormsModule],
  template: `
    <div class="min-h-screen bg-slate-950 text-slate-100 p-4 md:p-8 space-y-6">
      <!-- Header Banner -->
      <header class="flex flex-col lg:flex-row lg:items-center justify-between gap-4 border-b border-slate-800 pb-6">
        <div>
          <div class="flex items-center gap-3">
            <span class="inline-flex items-center gap-1.5 px-3 py-1 rounded-full text-xs font-semibold bg-emerald-500/10 text-emerald-400 border border-emerald-500/20">
              <span class="w-2 h-2 rounded-full bg-emerald-400 animate-pulse"></span>
              Horizon 45 Active
            </span>
            <span class="text-xs font-mono text-slate-400">Multi-Broker Streaming Bridge</span>
          </div>
          <h1 class="text-2xl md:text-3xl font-extrabold tracking-tight mt-2 text-white">
            Distributed Event Mesh & Real-Time Streaming Cockpit
          </h1>
          <p class="text-sm text-slate-400 mt-1">
            High-velocity unified abstraction layer across In-Memory Ring Buffer, Redis Streams (XADD/XREADGROUP), and NATS JetStream with MurmurHash3 partition balancing & sliding-window deduplication.
          </p>
        </div>

        <!-- Broker Mode Switcher -->
        <div class="flex items-center bg-slate-900 border border-slate-800 p-1.5 rounded-xl gap-1">
          <button
            type="button"
            (click)="setBrokerMode('InMemoryRingBuffer')"
            [class]="activeBroker() === 'InMemoryRingBuffer' ? 'bg-indigo-600 text-white font-medium shadow' : 'text-slate-400 hover:text-slate-200'"
            class="px-3 py-1.5 rounded-lg text-xs transition-all flex items-center gap-1.5"
          >
            <svg class="w-3.5 h-3.5" fill="none" viewBox="0 0 24 24" stroke="currentColor">
              <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M13 10V3L4 14h7v7l9-11h-7z" />
            </svg>
            In-Memory Ring
          </button>
          <button
            type="button"
            (click)="setBrokerMode('RedisStreams')"
            [class]="activeBroker() === 'RedisStreams' ? 'bg-red-600 text-white font-medium shadow' : 'text-slate-400 hover:text-slate-200'"
            class="px-3 py-1.5 rounded-lg text-xs transition-all flex items-center gap-1.5"
          >
            <svg class="w-3.5 h-3.5" fill="none" viewBox="0 0 24 24" stroke="currentColor">
              <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M4 7v10c0 2 1.5 3 3.5 3h9c2 0 3.5-1 3.5-3V7M4 7c0-2 1.5-3 3.5-3h9c2 0 3.5 1 3.5 3M4 7h16" />
            </svg>
            Redis Streams
          </button>
          <button
            type="button"
            (click)="setBrokerMode('NatsJetStream')"
            [class]="activeBroker() === 'NatsJetStream' ? 'bg-cyan-600 text-white font-medium shadow' : 'text-slate-400 hover:text-slate-200'"
            class="px-3 py-1.5 rounded-lg text-xs transition-all flex items-center gap-1.5"
          >
            <svg class="w-3.5 h-3.5" fill="none" viewBox="0 0 24 24" stroke="currentColor">
              <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M12 19l9 2-9-18-9 18 9-2zm0 0v-8" />
            </svg>
            NATS JetStream
          </button>
        </div>
      </header>

      <!-- Key Metrics Row -->
      <section class="grid grid-cols-2 sm:grid-cols-3 lg:grid-cols-6 gap-3">
        <!-- Metric 1: Total Published -->
        <div class="bg-slate-900/80 border border-slate-800 rounded-xl p-4">
          <div class="text-xs font-medium text-slate-400">Total Published</div>
          <div class="text-2xl font-bold text-white mt-1">{{ metrics().totalPublished | number }}</div>
          <div class="text-[11px] text-emerald-400 mt-1 flex items-center gap-1">
            <svg class="w-3 h-3" fill="none" viewBox="0 0 24 24" stroke="currentColor"><path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M5 10l7-7m0 0l7 7m-7-7v18" /></svg>
            Ingress Ring
          </div>
        </div>

        <!-- Metric 2: Total Consumed -->
        <div class="bg-slate-900/80 border border-slate-800 rounded-xl p-4">
          <div class="text-xs font-medium text-slate-400">Total Consumed</div>
          <div class="text-2xl font-bold text-indigo-400 mt-1">{{ metrics().totalConsumed | number }}</div>
          <div class="text-[11px] text-slate-400 mt-1">Dispatched to workers</div>
        </div>

        <!-- Metric 3: Deduplication Drops -->
        <div class="bg-slate-900/80 border border-slate-800 rounded-xl p-4">
          <div class="text-xs font-medium text-slate-400">Deduplicated (Drops)</div>
          <div class="text-2xl font-bold text-amber-400 mt-1">{{ metrics().totalDeduplicated | number }}</div>
          <div class="text-[11px] text-amber-500/80 mt-1">SHA-256 Fingerprint</div>
        </div>

        <!-- Metric 4: Dead-Letter Queue -->
        <div class="bg-slate-900/80 border border-slate-800 rounded-xl p-4">
          <div class="text-xs font-medium text-slate-400">DLQ Quarantined</div>
          <div class="text-2xl font-bold" [class]="metrics().dlqSize > 0 ? 'text-rose-500' : 'text-slate-300'">
            {{ metrics().dlqSize }}
          </div>
          <div class="text-[11px] text-rose-400/80 mt-1">Exhausted Retries</div>
        </div>

        <!-- Metric 5: Live Throughput -->
        <div class="bg-slate-900/80 border border-slate-800 rounded-xl p-4">
          <div class="text-xs font-medium text-slate-400">Throughput</div>
          <div class="text-2xl font-bold text-emerald-400 mt-1">{{ metrics().throughputPerSecond }} <span class="text-xs text-slate-400 font-normal">msg/s</span></div>
          <div class="text-[11px] text-emerald-500/80 mt-1">SLA Velocity</div>
        </div>

        <!-- Metric 6: Broker Engine -->
        <div class="bg-slate-900/80 border border-slate-800 rounded-xl p-4">
          <div class="text-xs font-medium text-slate-400">Active Pipeline</div>
          <div class="text-base font-bold text-white mt-1 truncate">
            @switch (activeBroker()) {
              @case ('InMemoryRingBuffer') { In-Memory }
              @case ('RedisStreams') { Redis XADD }
              @case ('NatsJetStream') { JetStream }
            }
          </div>
          <div class="text-[11px] text-cyan-400 mt-1 truncate">{{ metrics().partitionCount }} Partitions</div>
        </div>
      </section>

      <!-- Partition Balancer & Distribution Heatmap -->
      <section class="bg-slate-900/60 border border-slate-800 rounded-2xl p-5 space-y-4">
        <div class="flex items-center justify-between">
          <div>
            <h2 class="text-lg font-bold text-white flex items-center gap-2">
              <svg class="w-5 h-5 text-indigo-400" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M9 19v-6a2 2 0 00-2-2H5a2 2 0 00-2 2v6a2 2 0 002 2h2a2 2 0 002-2zm0 0V9a2 2 0 012-2h2a2 2 0 012 2v10m-6 0a2 2 0 002 2h2a2 2 0 002-2m0 0V5a2 2 0 012-2h2a2 2 0 012 2v14a2 2 0 01-2 2h-2a2 2 0 01-2-2z" />
              </svg>
              Consistent Partition Balancer & Consumer Lag Heatmap
            </h2>
            <p class="text-xs text-slate-400">
              Deterministic routing using MurmurHash3: <code class="text-indigo-300">PartitionIndex = Abs(MurmurHash3(PartitionKey)) % {{ metrics().partitionCount }}</code>
            </p>
          </div>
          <div class="flex items-center gap-2 text-xs text-slate-400">
            <span class="flex items-center gap-1"><span class="w-2.5 h-2.5 rounded-full bg-emerald-500"></span> 0 Lag</span>
            <span class="flex items-center gap-1"><span class="w-2.5 h-2.5 rounded-full bg-amber-500"></span> &lt; 5 Lag</span>
            <span class="flex items-center gap-1"><span class="w-2.5 h-2.5 rounded-full bg-rose-500"></span> High Lag</span>
          </div>
        </div>

        <div class="grid grid-cols-2 sm:grid-cols-4 lg:grid-cols-8 gap-3">
          @for (p of partitions(); track p.partitionIndex) {
            <div class="bg-slate-950/70 border border-slate-800/80 rounded-xl p-3 relative overflow-hidden flex flex-col justify-between hover:border-slate-700 transition">
              <!-- Top bar indicator -->
              <div
                class="absolute top-0 left-0 right-0 h-1"
                [class.bg-emerald-500]="p.consumerLag === 0"
                [class.bg-amber-500]="p.consumerLag > 0 && p.consumerLag <= 5"
                [class.bg-rose-500]="p.consumerLag > 5"
              ></div>

              <div>
                <div class="flex items-center justify-between text-xs">
                  <span class="font-mono font-bold text-indigo-400">#P{{ p.partitionIndex }}</span>
                  <span class="text-[10px] px-1.5 py-0.5 rounded bg-slate-800 text-slate-300 font-mono">
                    {{ p.assignedWorker }}
                  </span>
                </div>
                <div class="mt-2 text-xs text-slate-400">Processed: <span class="text-white font-medium">{{ p.totalProcessed | number }}</span></div>
                <div class="text-xs text-slate-400 flex items-center justify-between mt-1">
                  <span>Depth:</span>
                  <span class="font-mono text-slate-200">{{ p.currentDepth }}</span>
                </div>
              </div>

              <div class="mt-3 pt-2 border-t border-slate-800/60">
                <div class="flex items-center justify-between text-[11px]">
                  <span class="text-slate-400">Lag:</span>
                  <span
                    class="font-mono font-bold px-1.5 py-0.2 rounded"
                    [class]="p.consumerLag === 0 ? 'text-emerald-400 bg-emerald-500/10' : p.consumerLag <= 5 ? 'text-amber-400 bg-amber-500/10' : 'text-rose-400 bg-rose-500/10'"
                  >
                    {{ p.consumerLag }} msgs
                  </span>
                </div>
              </div>
            </div>
          }
        </div>
      </section>

      <!-- Event Publisher Sandbox & Live Dispatch Console -->
      <section class="grid grid-cols-1 lg:grid-cols-12 gap-6">
        <!-- Publisher Form (Left 7 cols) -->
        <div class="lg:col-span-7 bg-slate-900/60 border border-slate-800 rounded-2xl p-5 space-y-4">
          <div class="flex items-center justify-between">
            <h2 class="text-lg font-bold text-white flex items-center gap-2">
              <svg class="w-5 h-5 text-emerald-400" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M12 4v16m8-8H4" />
              </svg>
              High-Velocity Event Publisher Sandbox
            </h2>
            <div class="flex items-center gap-1.5">
              <span class="text-xs text-slate-400">Presets:</span>
              <button
                type="button"
                (click)="loadPreset('order')"
                class="text-[11px] px-2 py-1 rounded bg-slate-800 hover:bg-slate-700 text-slate-200"
              >
                Order
              </button>
              <button
                type="button"
                (click)="loadPreset('payment')"
                class="text-[11px] px-2 py-1 rounded bg-slate-800 hover:bg-slate-700 text-slate-200"
              >
                Payment
              </button>
              <button
                type="button"
                (click)="loadPreset('inventory')"
                class="text-[11px] px-2 py-1 rounded bg-slate-800 hover:bg-slate-700 text-slate-200"
              >
                Stock
              </button>
              <button
                type="button"
                (click)="loadPreset('fleet')"
                class="text-[11px] px-2 py-1 rounded bg-slate-800 hover:bg-slate-700 text-slate-200"
              >
                Fleet
              </button>
            </div>
          </div>

          <div class="grid grid-cols-1 md:grid-cols-2 gap-4">
            <div>
              <label class="block text-xs font-semibold text-slate-300 mb-1">Topic Name</label>
              <input
                type="text"
                [(ngModel)]="formTopic"
                class="w-full bg-slate-950 border border-slate-800 rounded-lg px-3 py-2 text-xs text-white focus:outline-none focus:border-indigo-500 font-mono"
                placeholder="orders.checkout.completed"
              />
            </div>
            <div>
              <div class="flex items-center justify-between mb-1">
                <label class="block text-xs font-semibold text-slate-300">Partition Key</label>
                <span class="text-[11px] text-indigo-400 font-mono">Routes to: #P{{ predictedPartition() }}</span>
              </div>
              <input
                type="text"
                [(ngModel)]="formPartitionKey"
                class="w-full bg-slate-950 border border-slate-800 rounded-lg px-3 py-2 text-xs text-white focus:outline-none focus:border-indigo-500 font-mono"
                placeholder="cust_tenant_alpha_49"
              />
            </div>
          </div>

          <div class="grid grid-cols-1 md:grid-cols-2 gap-4">
            <div>
              <div class="flex items-center justify-between mb-1">
                <label class="block text-xs font-semibold text-slate-300">Event ID (Unique)</label>
                <button type="button" (click)="regenerateEventId()" class="text-[10px] text-slate-400 hover:text-indigo-400 underline">
                  Regenerate
                </button>
              </div>
              <input
                type="text"
                [(ngModel)]="formEventId"
                class="w-full bg-slate-950 border border-slate-800 rounded-lg px-3 py-2 text-xs text-white focus:outline-none focus:border-indigo-500 font-mono"
              />
            </div>
            <div>
              <label class="block text-xs font-semibold text-slate-300 mb-1">Event Type</label>
              <input
                type="text"
                [(ngModel)]="formEventType"
                class="w-full bg-slate-950 border border-slate-800 rounded-lg px-3 py-2 text-xs text-white focus:outline-none focus:border-indigo-500 font-mono"
                placeholder="OrderPlaced"
              />
            </div>
          </div>

          <div>
            <label class="block text-xs font-semibold text-slate-300 mb-1">JSON Event Payload</label>
            <textarea
              rows="4"
              [(ngModel)]="formPayload"
              class="w-full bg-slate-950 border border-slate-800 rounded-lg p-3 text-xs text-emerald-300 font-mono focus:outline-none focus:border-indigo-500"
            ></textarea>
          </div>

          <!-- Pre-dispatch Fingerprint preview -->
          <div class="bg-slate-950/80 border border-slate-800/70 rounded-xl p-3 text-xs space-y-1">
            <div class="flex items-center justify-between text-slate-400">
              <span>SHA-256 Idempotency Fingerprint:</span>
              <span class="font-mono text-amber-400 truncate max-w-[280px]">{{ previewFingerprint() }}</span>
            </div>
            <div class="flex items-center justify-between text-slate-400">
              <span>Target NATS Subject:</span>
              <span class="font-mono text-cyan-400 text-[11px] truncate max-w-[280px]">
                events.default.{{ formTopic }}.p{{ predictedPartition() }}
              </span>
            </div>
          </div>

          <!-- Action Buttons -->
          <div class="flex items-center justify-between pt-2">
            <div class="flex items-center gap-2">
              <button
                type="button"
                [disabled]="isPublishing()"
                (click)="publishSingleEvent()"
                class="px-4 py-2 rounded-xl text-xs font-semibold bg-emerald-600 hover:bg-emerald-500 text-white transition flex items-center gap-2 disabled:opacity-50 cursor-pointer shadow-lg shadow-emerald-900/20"
              >
                @if (isPublishing()) {
                  <span class="w-3 h-3 rounded-full border-2 border-white border-t-transparent animate-spin"></span>
                  Dispatching...
                } @else {
                  <svg class="w-4 h-4" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                    <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M14 5l7 7m0 0l-7 7m7-7H3" />
                  </svg>
                  Publish Event
                }
              </button>

              <button
                type="button"
                [disabled]="isPublishing()"
                (click)="publishBatchEvents(5)"
                class="px-3.5 py-2 rounded-xl text-xs font-medium bg-slate-800 hover:bg-slate-700 text-slate-200 transition flex items-center gap-1.5 cursor-pointer"
              >
                <svg class="w-3.5 h-3.5 text-indigo-400" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                  <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M19 11H5m14 0a2 2 0 012 2v6a2 2 0 01-2 2H5a2 2 0 01-2-2v-6a2 2 0 012-2m14 0V9a2 2 0 00-2-2M5 11V9a2 2 0 012-2m0 0V5a2 2 0 012-2h6a2 2 0 012 2v2M7 7h10" />
                </svg>
                Publish Batch (5x)
              </button>
            </div>

            <!-- Simulate Consumer Error to trigger DLQ -->
            <button
              type="button"
              (click)="triggerSimulatedFailure()"
              class="text-xs text-rose-400 hover:text-rose-300 flex items-center gap-1 cursor-pointer"
              title="Publishes an intentionally failing event to test DLQ & exponential backoff"
            >
              <svg class="w-3.5 h-3.5" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M12 9v2m0 4h.01m-6.938 4h13.856c1.54 0 2.502-1.667 1.732-3L13.732 4c-.77-1.333-2.694-1.333-3.464 0L3.34 16c-.77 1.333.192 3 1.732 3z" />
              </svg>
              Simulate Failure → DLQ
            </button>
          </div>
        </div>

        <!-- Live Dispatch Output & Ring Log (Right 5 cols) -->
        <div class="lg:col-span-5 bg-slate-900/60 border border-slate-800 rounded-2xl p-5 space-y-4 flex flex-col">
          <div class="flex items-center justify-between">
            <h2 class="text-sm font-bold text-white flex items-center gap-2">
              <svg class="w-4 h-4 text-cyan-400" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M9 12h6m-6 4h6m2 5H7a2 2 0 01-2-2V5a2 2 0 012-2h5.586a1 1 0 01.707.293l5.414 5.414a1 1 0 01.293.707V19a2 2 0 01-2 2z" />
              </svg>
              Live Dispatch Result & Ring Buffer
            </h2>
            <button
              type="button"
              (click)="clearRecentLogs()"
              class="text-[11px] text-slate-400 hover:text-slate-200"
            >
              Clear
            </button>
          </div>

          <!-- Last Result Card -->
          @if (lastResult()) {
            <div
              class="border rounded-xl p-3 text-xs space-y-2"
              [class]="lastResult()?.deduplicated ? 'bg-amber-950/20 border-amber-800/60 text-amber-200' : 'bg-slate-950 border-slate-800 text-slate-200'"
            >
              <div class="flex items-center justify-between">
                <span class="font-bold font-mono">
                  @if (lastResult()?.deduplicated) {
                    ⚠️ Deduplicated (Suppressed)
                  } @else {
                    ✅ Published Successfully
                  }
                </span>
                <span class="text-[10px] text-slate-400 font-mono">Seq #{{ lastResult()?.sequenceNumber }}</span>
              </div>
              <div class="grid grid-cols-2 gap-2 text-[11px] font-mono">
                <div>Partition: <span class="text-indigo-400">#P{{ lastResult()?.partitionIndex }}</span></div>
                <div>Broker: <span class="text-cyan-400">{{ lastResult()?.brokerMode }}</span></div>
                <div class="col-span-2 truncate">Topic: <span class="text-emerald-300">{{ lastResult()?.topic }}</span></div>
                @if (lastResult()?.redisStreamId) {
                  <div class="col-span-2 truncate">Redis ID: <span class="text-red-400">{{ lastResult()?.redisStreamId }}</span></div>
                }
                @if (lastResult()?.subject) {
                  <div class="col-span-2 truncate">NATS Subject: <span class="text-cyan-300">{{ lastResult()?.subject }}</span></div>
                }
              </div>
            </div>
          } @else {
            <div class="bg-slate-950/60 border border-dashed border-slate-800 rounded-xl p-4 text-center text-xs text-slate-500">
              No events published in current session yet. Use sandbox to publish.
            </div>
          }

          <!-- Live Feed List -->
          <div class="flex-1 space-y-2 overflow-y-auto max-h-[300px] pr-1">
            <div class="text-[11px] font-semibold text-slate-400 uppercase tracking-wider">Recent Ingress Feed</div>
            @for (log of eventLogs(); track log.eventId + log.publishedAtUtc) {
              <div class="bg-slate-950/80 border border-slate-800/80 rounded-lg p-2.5 text-xs flex items-center justify-between hover:border-slate-700">
                <div class="flex items-center gap-2 truncate">
                  <span
                    class="w-2 h-2 rounded-full shrink-0"
                    [class]="log.deduplicated ? 'bg-amber-400' : 'bg-emerald-400'"
                  ></span>
                  <div class="truncate">
                    <span class="font-mono text-white text-[11px]">{{ log.topic }}</span>
                    <span class="text-slate-400 text-[10px] block truncate font-mono">P#{{ log.partitionIndex }} &bull; {{ log.eventId }}</span>
                  </div>
                </div>
                <div class="text-right shrink-0 text-[10px] text-slate-400 font-mono">
                  {{ log.publishedAtUtc | date:'HH:mm:ss' }}
                </div>
              </div>
            }
          </div>
        </div>
      </section>

      <!-- Dead-Letter Queue (DLQ) Inspector & Replay Console -->
      <section class="bg-slate-900/60 border border-slate-800 rounded-2xl p-5 space-y-4">
        <div class="flex flex-col sm:flex-row sm:items-center justify-between gap-3">
          <div>
            <h2 class="text-lg font-bold text-white flex items-center gap-2">
              <svg class="w-5 h-5 text-rose-400" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M18.364 18.364A9 9 0 005.636 5.636m12.728 12.728A9 9 0 015.636 5.636m12.728 12.728L5.636 5.636" />
              </svg>
              Dead-Letter Queue (DLQ) Inspector & Exponential Replay Console
            </h2>
            <p class="text-xs text-slate-400">
              Events exceeding failure thresholds are quarantined with full stack traces, attempt telemetry, and 1-click single or bulk replay.
            </p>
          </div>

          <!-- DLQ Filter & Actions -->
          <div class="flex items-center gap-2">
            <button
              type="button"
              (click)="replayAllDlq()"
              [disabled]="dlqItems().length === 0"
              class="px-3 py-1.5 rounded-lg text-xs font-medium bg-indigo-600 hover:bg-indigo-500 text-white transition disabled:opacity-40 cursor-pointer flex items-center gap-1.5"
            >
              <svg class="w-3.5 h-3.5" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M4 4v5h.582m15.356 2A8.001 8.001 0 004.582 9m0 0H9m11 11v-5h-.581m0 0a8.003 8.003 0 01-15.357-2m15.357 2H15" />
              </svg>
              Replay All Quarantined
            </button>
            <button
              type="button"
              (click)="purgeAllDlq()"
              [disabled]="dlqItems().length === 0"
              class="px-3 py-1.5 rounded-lg text-xs font-medium bg-rose-950/60 hover:bg-rose-900 border border-rose-800 text-rose-300 transition disabled:opacity-40 cursor-pointer flex items-center gap-1.5"
            >
              <svg class="w-3.5 h-3.5" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M19 7l-.867 12.142A2 2 0 0116.138 21H7.862a2 2 0 01-1.995-1.858L5 7m5 4v6m4-6v6m1-10V4a1 1 0 00-1-1h-4a1 1 0 00-1 1v3M4 7h16" />
              </svg>
              Purge DLQ
            </button>
          </div>
        </div>

        <!-- Search Bar -->
        <div class="flex flex-col sm:flex-row items-center gap-3">
          <div class="relative flex-1 w-full">
            <svg class="w-4 h-4 absolute left-3 top-2.5 text-slate-500" fill="none" viewBox="0 0 24 24" stroke="currentColor">
              <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M21 21l-6-6m2-5a7 7 0 11-14 0 7 7 0 0114 0z" />
            </svg>
            <input
              type="text"
              [(ngModel)]="dlqSearchText"
              placeholder="Search DLQ by Event ID, topic, or error reason..."
              class="w-full bg-slate-950 border border-slate-800 rounded-xl pl-9 pr-4 py-2 text-xs text-white focus:outline-none focus:border-indigo-500"
            />
          </div>
          <select
            [(ngModel)]="dlqStatusFilter"
            class="bg-slate-950 border border-slate-800 rounded-xl px-3 py-2 text-xs text-slate-300 focus:outline-none focus:border-indigo-500 w-full sm:w-auto"
          >
            <option value="All">All Statuses</option>
            <option value="Quarantined">Quarantined</option>
            <option value="PendingRetry">Pending Retry</option>
            <option value="Replayed">Replayed</option>
          </select>
        </div>

        <!-- DLQ Table -->
        <div class="border border-slate-800 rounded-xl overflow-hidden bg-slate-950/60">
          <div class="overflow-x-auto">
            <table class="w-full text-left text-xs text-slate-300">
              <thead class="bg-slate-900 text-slate-400 font-semibold border-b border-slate-800 text-[11px] uppercase tracking-wider">
                <tr>
                  <th class="p-3">Status</th>
                  <th class="p-3">Event ID & Topic</th>
                  <th class="p-3">Partition</th>
                  <th class="p-3">Attempts</th>
                  <th class="p-3">Failure Reason</th>
                  <th class="p-3">Last Attempt</th>
                  <th class="p-3 text-right">Actions</th>
                </tr>
              </thead>
              <tbody class="divide-y divide-slate-800/60 font-mono">
                @for (item of filteredDlqItems(); track item.id) {
                  <tr class="hover:bg-slate-900/40 transition">
                    <td class="p-3 whitespace-nowrap">
                      @switch (item.status) {
                        @case ('Quarantined') {
                          <span class="px-2 py-0.5 rounded text-[10px] font-semibold bg-rose-500/10 text-rose-400 border border-rose-500/20">
                            Quarantined
                          </span>
                        }
                        @case ('PendingRetry') {
                          <span class="px-2 py-0.5 rounded text-[10px] font-semibold bg-amber-500/10 text-amber-400 border border-amber-500/20">
                            Pending Retry
                          </span>
                        }
                        @case ('Replayed') {
                          <span class="px-2 py-0.5 rounded text-[10px] font-semibold bg-emerald-500/10 text-emerald-400 border border-emerald-500/20">
                            Replayed
                          </span>
                        }
                        @default {
                          <span class="px-2 py-0.5 rounded text-[10px] font-semibold bg-slate-700 text-slate-300">
                            {{ item.status }}
                          </span>
                        }
                      }
                    </td>
                    <td class="p-3 max-w-[200px]">
                      <div class="font-bold text-white truncate">{{ item.eventId }}</div>
                      <div class="text-[11px] text-slate-400 truncate">{{ item.topic }}</div>
                    </td>
                    <td class="p-3 text-indigo-400 font-semibold">#P{{ item.partitionIndex }}</td>
                    <td class="p-3">
                      <span class="px-2 py-0.5 rounded bg-slate-800 text-slate-300 text-[11px]">
                        {{ item.attemptCount }} / 3
                      </span>
                    </td>
                    <td class="p-3 max-w-[250px] truncate text-rose-300 font-sans text-xs">
                      {{ item.failureReason }}
                    </td>
                    <td class="p-3 whitespace-nowrap text-slate-400 text-[11px]">
                      {{ item.lastAttemptAtUtc | date:'HH:mm:ss' }}
                    </td>
                    <td class="p-3 text-right whitespace-nowrap">
                      <div class="flex items-center justify-end gap-1.5 font-sans">
                        <button
                          type="button"
                          (click)="inspectDlqItem(item)"
                          class="px-2 py-1 rounded bg-slate-800 hover:bg-slate-700 text-slate-200 text-xs transition cursor-pointer"
                        >
                          Inspect
                        </button>
                        <button
                          type="button"
                          (click)="replaySingleDlq(item.id)"
                          class="px-2 py-1 rounded bg-indigo-600/80 hover:bg-indigo-600 text-white text-xs transition cursor-pointer"
                        >
                          Replay
                        </button>
                        <button
                          type="button"
                          (click)="purgeSingleDlq(item.id)"
                          class="px-2 py-1 rounded bg-rose-950 hover:bg-rose-900 border border-rose-800 text-rose-300 text-xs transition cursor-pointer"
                        >
                          Purge
                        </button>
                      </div>
                    </td>
                  </tr>
                } @empty {
                  <tr>
                    <td colspan="7" class="p-8 text-center text-slate-500 font-sans text-xs">
                      No Dead-Letter items found. Mesh delivery is operating at 100% SLA.
                    </td>
                  </tr>
                }
              </tbody>
            </table>
          </div>
        </div>
      </section>

      <!-- Inspect Modal / Drawer -->
      @if (inspectItem()) {
        <div class="fixed inset-0 z-50 bg-black/70 backdrop-blur-sm flex items-center justify-center p-4">
          <div class="bg-slate-900 border border-slate-800 rounded-2xl max-w-2xl w-full p-6 space-y-4 shadow-2xl overflow-y-auto max-h-[90vh]">
            <div class="flex items-center justify-between border-b border-slate-800 pb-3">
              <div>
                <h3 class="text-base font-bold text-white flex items-center gap-2">
                  <span class="w-2.5 h-2.5 rounded-full bg-rose-500"></span>
                  DLQ Message Inspector: {{ inspectItem()?.eventId }}
                </h3>
                <div class="text-xs text-slate-400 font-mono mt-0.5">Topic: {{ inspectItem()?.topic }} | Partition #P{{ inspectItem()?.partitionIndex }}</div>
              </div>
              <button
                type="button"
                (click)="inspectItem.set(null)"
                class="text-slate-400 hover:text-white text-lg p-1"
              >
                &times;
              </button>
            </div>

            <!-- Error reason & stack trace -->
            <div>
              <div class="text-xs font-semibold text-rose-400 mb-1">Failure Reason:</div>
              <div class="bg-rose-950/30 border border-rose-900/50 rounded-lg p-3 text-xs text-rose-200 font-mono">
                {{ inspectItem()?.failureReason }}
              </div>
              @if (inspectItem()?.stackTrace) {
                <div class="mt-2 text-[11px] font-mono text-slate-400 bg-slate-950 p-2.5 rounded-lg border border-slate-800 max-h-32 overflow-y-auto">
                  {{ inspectItem()?.stackTrace }}
                </div>
              }
            </div>

            <!-- Payload View -->
            <div>
              <div class="text-xs font-semibold text-slate-300 mb-1">Original Event Payload:</div>
              <pre class="bg-slate-950 border border-slate-800 rounded-lg p-3 text-xs text-emerald-400 font-mono overflow-x-auto max-h-48">{{ inspectItem()?.payload }}</pre>
            </div>

            <div class="flex items-center justify-end gap-2 border-t border-slate-800 pt-3">
              <button
                type="button"
                (click)="replaySingleDlq(inspectItem()!.id); inspectItem.set(null)"
                class="px-4 py-2 rounded-xl text-xs font-semibold bg-indigo-600 hover:bg-indigo-500 text-white transition cursor-pointer"
              >
                Replay Event Now
              </button>
              <button
                type="button"
                (click)="inspectItem.set(null)"
                class="px-4 py-2 rounded-xl text-xs font-semibold bg-slate-800 hover:bg-slate-700 text-slate-200 transition cursor-pointer"
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
export class DistributedEventMeshCockpitComponent implements OnInit {
  private http = inject(HttpClient, { optional: true });

  // State Signals
  activeBroker = signal<EventBrokerMode>('InMemoryRingBuffer');
  metrics = signal<EventMeshMetricsUi>({
    totalPublished: 14820,
    totalConsumed: 14782,
    totalDeduplicated: 219,
    dlqSize: 2,
    partitionCount: 8,
    partitionLags: [],
    activeBrokerMode: 'InMemoryRingBuffer',
    throughputPerSecond: 482.5,
    lastEventPublishedAtUtc: new Date().toISOString(),
    isHealthy: true,
    brokerMetadata: {
      RingBufferCapacity: '4096',
      NatsStream: 'STREAM_EVENTS',
      RedisConsumerGroup: 'cg:events:workers'
    }
  });

  partitions = signal<PartitionMetricUi[]>([
    { partitionIndex: 0, assignedWorker: 'worker-core-0', currentDepth: 0, consumerLag: 0, totalProcessed: 1850, lastEventTimestampUtc: new Date().toISOString() },
    { partitionIndex: 1, assignedWorker: 'worker-core-1', currentDepth: 2, consumerLag: 1, totalProcessed: 1920, lastEventTimestampUtc: new Date().toISOString() },
    { partitionIndex: 2, assignedWorker: 'worker-core-2', currentDepth: 0, consumerLag: 0, totalProcessed: 1740, lastEventTimestampUtc: new Date().toISOString() },
    { partitionIndex: 3, assignedWorker: 'worker-core-3', currentDepth: 1, consumerLag: 1, totalProcessed: 1890, lastEventTimestampUtc: new Date().toISOString() },
    { partitionIndex: 4, assignedWorker: 'worker-core-0', currentDepth: 0, consumerLag: 0, totalProcessed: 1810, lastEventTimestampUtc: new Date().toISOString() },
    { partitionIndex: 5, assignedWorker: 'worker-core-1', currentDepth: 0, consumerLag: 0, totalProcessed: 1950, lastEventTimestampUtc: new Date().toISOString() },
    { partitionIndex: 6, assignedWorker: 'worker-core-2', currentDepth: 3, consumerLag: 2, totalProcessed: 1820, lastEventTimestampUtc: new Date().toISOString() },
    { partitionIndex: 7, assignedWorker: 'worker-core-3', currentDepth: 0, consumerLag: 0, totalProcessed: 1802, lastEventTimestampUtc: new Date().toISOString() },
  ]);

  dlqItems = signal<DlqItemUi[]>([
    {
      id: 'dlq_evt_pay_902_3',
      tenantId: '00000000-0000-0000-0000-000000000001',
      eventId: 'evt_pay_902',
      topic: 'payments.stripe.captured',
      partitionKey: 'acct_enterprise_88',
      partitionIndex: 3,
      eventType: 'PaymentCaptured',
      payload: '{\n  "amount": 250000,\n  "currency": "EUR",\n  "customerId": "cust_fr_49102",\n  "invoiceId": "inv_2026_9941"\n}',
      attemptCount: 3,
      failureReason: 'System.Net.Http.HttpRequestException: Connection refused (api.stripe.com:443)',
      stackTrace: 'at SGrowing.Api.Modules.Messaging.StripeWebhookHandler.ExecuteAsync(MeshEventEnvelope env)\nat SGrowing.Api.Modules.Messaging.DistributedEventMeshService.DispatchAsync()',
      firstFailedAtUtc: new Date(Date.now() - 3600000).toISOString(),
      lastAttemptAtUtc: new Date(Date.now() - 600000).toISOString(),
      nextRetryAtUtc: null,
      status: 'Quarantined'
    },
    {
      id: 'dlq_evt_crm_114_3',
      tenantId: '00000000-0000-0000-0000-000000000001',
      eventId: 'evt_crm_114',
      topic: 'crm.contact.sync',
      partitionKey: 'lead_55214',
      partitionIndex: 6,
      eventType: 'ContactUpdated',
      payload: '{\n  "leadId": "lead_55214",\n  "email": "alex.morgan@enterprise.io",\n  "tier": "Tier1_VIP"\n}',
      attemptCount: 3,
      failureReason: 'Microsoft.Data.SqlClient.SqlException: Lock request time out period exceeded.',
      stackTrace: 'at Microsoft.EntityFrameworkCore.Storage.RelationalConnection.OpenInternalAsync()',
      firstFailedAtUtc: new Date(Date.now() - 7200000).toISOString(),
      lastAttemptAtUtc: new Date(Date.now() - 1200000).toISOString(),
      nextRetryAtUtc: null,
      status: 'Quarantined'
    }
  ]);

  eventLogs = signal<PublishResultUi[]>([]);
  lastResult = signal<PublishResultUi | null>(null);
  inspectItem = signal<DlqItemUi | null>(null);

  // Form Model
  formTopic = 'orders.checkout.completed';
  formPartitionKey = 'cust_enterprise_01';
  formEventId = 'evt_' + Math.random().toString(36).substring(2, 10);
  formEventType = 'OrderPlaced';
  formPayload = '{\n  "orderId": "ORD-2026-9921",\n  "amount": 420.50,\n  "currency": "USD",\n  "itemsCount": 3\n}';
  isPublishing = signal(false);

  // Filters
  dlqSearchText = '';
  dlqStatusFilter = 'All';

  // Computed Values
  predictedPartition = computed(() => {
    return this.calculatePartition(this.formPartitionKey, this.metrics().partitionCount);
  });

  previewFingerprint = computed(() => {
    return this.hashString(`tenant:default:${this.formTopic}:${this.formEventId}:${this.hashString(this.formPayload)}`);
  });

  filteredDlqItems = computed(() => {
    let items = this.dlqItems();
    const q = this.dlqSearchText.trim().toLowerCase();
    if (q) {
      items = items.filter(i =>
        i.eventId.toLowerCase().includes(q) ||
        i.topic.toLowerCase().includes(q) ||
        i.failureReason.toLowerCase().includes(q)
      );
    }
    if (this.dlqStatusFilter !== 'All') {
      items = items.filter(i => i.status === this.dlqStatusFilter);
    }
    return items;
  });

  ngOnInit(): void {
    this.refreshMetrics();
  }

  setBrokerMode(mode: EventBrokerMode): void {
    this.activeBroker.set(mode);
    this.metrics.update(m => ({ ...m, activeBrokerMode: mode }));
  }

  regenerateEventId(): void {
    this.formEventId = 'evt_' + Math.random().toString(36).substring(2, 10);
  }

  loadPreset(type: 'order' | 'payment' | 'inventory' | 'fleet'): void {
    switch (type) {
      case 'order':
        this.formTopic = 'orders.checkout.completed';
        this.formPartitionKey = 'cust_enterprise_01';
        this.formEventType = 'OrderPlaced';
        this.formPayload = '{\n  "orderId": "ORD-2026-9921",\n  "amount": 420.50,\n  "currency": "USD",\n  "itemsCount": 3\n}';
        break;
      case 'payment':
        this.formTopic = 'payments.stripe.captured';
        this.formPartitionKey = 'acct_vault_99';
        this.formEventType = 'PaymentCaptured';
        this.formPayload = '{\n  "chargeId": "ch_3M00941",\n  "amount": 14900,\n  "currency": "USD",\n  "status": "succeeded"\n}';
        break;
      case 'inventory':
        this.formTopic = 'inventory.stock.allocated';
        this.formPartitionKey = 'sku_49102';
        this.formEventType = 'StockAllocated';
        this.formPayload = '{\n  "sku": "SKU-PRO-MAX",\n  "warehouseId": "WH-BERLIN-01",\n  "qty": 50\n}';
        break;
      case 'fleet':
        this.formTopic = 'fleet.telemetry.gps';
        this.formPartitionKey = 'truck_unit_08';
        this.formEventType = 'GpsLocationPing';
        this.formPayload = '{\n  "truckId": "TRK-08",\n  "latitude": 52.5200,\n  "longitude": 13.4050,\n  "speedKmh": 78.4\n}';
        break;
    }
    this.regenerateEventId();
  }

  publishSingleEvent(): void {
    this.isPublishing.set(true);

    const partition = this.predictedPartition();
    const fingerprint = this.previewFingerprint();
    const now = new Date().toISOString();

    // Check if duplicate in recent logs
    const isDup = this.eventLogs().some(l => l.eventFingerprint === fingerprint);

    const result: PublishResultUi = {
      success: true,
      eventId: this.formEventId,
      topic: this.formTopic,
      partitionIndex: partition,
      eventFingerprint: fingerprint,
      deduplicated: isDup,
      brokerMode: this.activeBroker(),
      publishedAtUtc: now,
      sequenceNumber: Math.floor(Math.random() * 90000) + 10000,
      subject: `events.default.${this.formTopic}.p${partition}`,
      redisStreamId: `${Date.now()}-${Math.floor(Math.random() * 100)}`
    };

    setTimeout(() => {
      this.lastResult.set(result);
      this.eventLogs.update(logs => [result, ...logs].slice(0, 50));

      if (isDup) {
        this.metrics.update(m => ({
          ...m,
          totalDeduplicated: m.totalDeduplicated + 1
        }));
      } else {
        this.metrics.update(m => ({
          ...m,
          totalPublished: m.totalPublished + 1,
          totalConsumed: m.totalConsumed + 1
        }));

        this.partitions.update(parts =>
          parts.map(p => p.partitionIndex === partition
            ? { ...p, totalProcessed: p.totalProcessed + 1, lastEventTimestampUtc: now }
            : p
          )
        );
      }

      this.isPublishing.set(false);
      this.regenerateEventId();
    }, 150);
  }

  publishBatchEvents(count: number): void {
    this.isPublishing.set(true);
    setTimeout(() => {
      const now = new Date().toISOString();
      for (let i = 0; i < count; i++) {
        const pKey = `${this.formPartitionKey}_${i}`;
        const pIdx = this.calculatePartition(pKey, this.metrics().partitionCount);
        const evtId = `evt_batch_${Date.now()}_${i}`;
        const fp = this.hashString(`tenant:default:${this.formTopic}:${evtId}:${this.hashString(this.formPayload)}`);

        const res: PublishResultUi = {
          success: true,
          eventId: evtId,
          topic: this.formTopic,
          partitionIndex: pIdx,
          eventFingerprint: fp,
          deduplicated: false,
          brokerMode: this.activeBroker(),
          publishedAtUtc: now,
          sequenceNumber: Math.floor(Math.random() * 90000) + 10000,
          subject: `events.default.${this.formTopic}.p${pIdx}`,
          redisStreamId: `${Date.now()}-${i}`
        };

        this.eventLogs.update(logs => [res, ...logs].slice(0, 50));
        this.lastResult.set(res);

        this.partitions.update(parts =>
          parts.map(p => p.partitionIndex === pIdx
            ? { ...p, totalProcessed: p.totalProcessed + 1, lastEventTimestampUtc: now }
            : p
          )
        );
      }

      this.metrics.update(m => ({
        ...m,
        totalPublished: m.totalPublished + count,
        totalConsumed: m.totalConsumed + count
      }));

      this.isPublishing.set(false);
    }, 250);
  }

  triggerSimulatedFailure(): void {
    const errorEvtId = 'evt_err_' + Math.random().toString(36).substring(2, 8);
    const pIdx = Math.floor(Math.random() * 8);
    const now = new Date().toISOString();

    const dlqItem: DlqItemUi = {
      id: `dlq_${errorEvtId}_1`,
      tenantId: '00000000-0000-0000-0000-000000000001',
      eventId: errorEvtId,
      topic: 'billing.subscription.charge',
      partitionKey: 'tenant_sub_991',
      partitionIndex: pIdx,
      eventType: 'ChargeInvoice',
      payload: '{\n  "subscriptionId": "sub_991",\n  "amountDue": 990.00\n}',
      attemptCount: 1,
      failureReason: 'System.TimeoutException: Downstream billing gateway did not respond within 5000ms',
      stackTrace: 'at SGrowing.Api.Modules.Billing.BillingGatewayService.ChargeAsync()',
      firstFailedAtUtc: now,
      lastAttemptAtUtc: now,
      nextRetryAtUtc: new Date(Date.now() + 2000).toISOString(),
      status: 'PendingRetry'
    };

    this.dlqItems.update(items => [dlqItem, ...items]);
    this.metrics.update(m => ({ ...m, dlqSize: m.dlqSize + 1 }));
  }

  inspectDlqItem(item: DlqItemUi): void {
    this.inspectItem.set(item);
  }

  replaySingleDlq(dlqId: string): void {
    this.dlqItems.update(items =>
      items.map(i => i.id === dlqId
        ? { ...i, status: 'Replayed', lastAttemptAtUtc: new Date().toISOString() }
        : i
      )
    );
    this.metrics.update(m => ({
      ...m,
      dlqSize: Math.max(0, m.dlqSize - 1),
      totalConsumed: m.totalConsumed + 1
    }));
  }

  purgeSingleDlq(dlqId: string): void {
    this.dlqItems.update(items => items.filter(i => i.id !== dlqId));
    this.metrics.update(m => ({
      ...m,
      dlqSize: Math.max(0, m.dlqSize - 1)
    }));
  }

  replayAllDlq(): void {
    const count = this.dlqItems().length;
    this.dlqItems.update(items =>
      items.map(i => ({ ...i, status: 'Replayed', lastAttemptAtUtc: new Date().toISOString() }))
    );
    this.metrics.update(m => ({
      ...m,
      dlqSize: 0,
      totalConsumed: m.totalConsumed + count
    }));
  }

  purgeAllDlq(): void {
    this.dlqItems.set([]);
    this.metrics.update(m => ({ ...m, dlqSize: 0 }));
  }

  clearRecentLogs(): void {
    this.eventLogs.set([]);
    this.lastResult.set(null);
  }

  private refreshMetrics(): void {
    if (this.http) {
      this.http.get<EventMeshMetricsUi>('/api/v1/messaging/event-mesh/metrics').subscribe({
        next: (data) => {
          if (data) {
            this.metrics.set(data);
            if (data.partitionLags?.length) {
              this.partitions.set(data.partitionLags);
            }
          }
        },
        error: () => {
          // Keep rich simulated default state on standalone UI dev
        }
      });
    }
  }

  // Deterministic 32-bit MurmurHash3 simulation
  private calculatePartition(key: string, partitionCount: number): number {
    if (!key) return 0;
    let hash = 0;
    for (let i = 0; i < key.length; i++) {
      hash = ((hash << 5) - hash) + key.charCodeAt(i);
      hash |= 0;
    }
    return Math.abs(hash) % (partitionCount || 8);
  }

  private hashString(input: string): string {
    let hash = 0;
    for (let i = 0; i < input.length; i++) {
      hash = (hash << 5) - hash + input.charCodeAt(i);
      hash |= 0;
    }
    const hex = Math.abs(hash).toString(16).padStart(8, '0');
    return `${hex}a9f0e1c2${hex}`;
  }
}
