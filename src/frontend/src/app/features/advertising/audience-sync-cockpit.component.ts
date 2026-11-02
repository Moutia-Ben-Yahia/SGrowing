import { Component, ChangeDetectionStrategy, signal, computed } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';

export interface DspConnectorView {
  platform: string;
  name: string;
  isConnected: boolean;
  syncedAudiences: number;
  hashedRecords: number;
  avgLatencyMs: number;
  lastSync: string;
}

export interface SyncJobView {
  jobId: string;
  platform: string;
  cohortName: string;
  recordsCount: number;
  status: 'StreamedToDsp' | 'RecoveredAndStreamed' | 'RateLimited' | 'Queued';
  latencyMs: number;
  timestamp: string;
}

@Component({
  selector: 'app-audience-sync-cockpit',
  standalone: true,
  imports: [CommonModule, FormsModule],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="p-6 max-w-7xl mx-auto space-y-6" dir="ltr" data-testid="audience-sync-cockpit">
      <!-- Header -->
      <div class="flex flex-col md:flex-row md:items-center justify-between gap-4 border-b border-slate-700/60 pb-6">
        <div>
          <div class="flex items-center gap-2 text-xs font-semibold uppercase tracking-wider text-blue-400">
            <span class="inline-block w-2 h-2 rounded-full bg-blue-400 animate-pulse"></span>
            DSP Audience Sync & Ad Retargeting Mesh • Horizon 57
          </div>
          <h1 class="text-3xl font-extrabold text-white tracking-tight mt-1">
            Real-Time Dynamic Cohort Sync & DSP Audience Mesh
          </h1>
          <p class="text-slate-400 text-sm mt-1">
            Continuous streaming of SHA-256 hashed customer cohorts into Google Customer Match, Meta Custom Audiences & TikTok Manager.
          </p>
        </div>

        <div class="flex items-center gap-3">
          <button
            (click)="streamCohortSync()"
            data-testid="stream-sync-btn"
            class="px-4 py-2 bg-blue-600 hover:bg-blue-500 text-white rounded-lg text-sm font-semibold transition-colors flex items-center gap-2 shadow-lg shadow-blue-600/20"
          >
            <span>⚡</span> Stream Cohort Sync
          </button>
        </div>
      </div>

      <!-- KPI Overview Stats -->
      <div class="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-4 gap-4">
        <div class="bg-slate-800/80 border border-slate-700/60 rounded-xl p-4">
          <div class="text-slate-400 text-xs font-medium uppercase">Active Hashed Audience Records</div>
          <div class="text-2xl font-bold text-white mt-1" data-testid="stat-hashed-records">
            {{ totalHashedRecords() | number:'1.0-0' }}
          </div>
          <div class="text-blue-400 text-xs mt-1">Continuous Real-Time Mesh</div>
        </div>
        <div class="bg-slate-800/80 border border-slate-700/60 rounded-xl p-4">
          <div class="text-slate-400 text-xs font-medium uppercase">Connected DSP Platforms</div>
          <div class="text-2xl font-bold text-emerald-400 mt-1" data-testid="stat-connected-dsps">
            {{ connectedDspsCount() }} / {{ connectors().length }}
          </div>
          <div class="text-slate-400 text-xs mt-1">Google • Meta • TikTok • LinkedIn</div>
        </div>
        <div class="bg-slate-800/80 border border-slate-700/60 rounded-xl p-4">
          <div class="text-slate-400 text-xs font-medium uppercase">Average Sync Latency</div>
          <div class="text-2xl font-bold text-sky-400 mt-1" data-testid="stat-avg-latency">
            {{ avgLatency() }} ms
          </div>
          <div class="text-emerald-400 text-xs mt-1">Sub-second Streaming Target Met</div>
        </div>
        <div class="bg-slate-800/80 border border-slate-700/60 rounded-xl p-4">
          <div class="text-slate-400 text-xs font-medium uppercase">Match Rate Efficiency</div>
          <div class="text-2xl font-bold text-indigo-400 mt-1">87.3%</div>
          <div class="text-slate-400 text-xs mt-1">Zero-Party Salted Matching</div>
        </div>
      </div>

      <!-- Main Stage: Connectors & Live Sync Pipeline -->
      <div class="grid grid-cols-1 lg:grid-cols-12 gap-6">
        <!-- Connectors List (7 cols) -->
        <div class="lg:col-span-7 bg-slate-800/80 border border-slate-700/60 rounded-xl overflow-hidden">
          <div class="p-4 border-b border-slate-700/60 flex items-center justify-between">
            <h2 class="text-lg font-bold text-white">Configured DSP Connectors</h2>
            <span class="text-xs text-slate-400">{{ connectors().length }} Active Integrations</span>
          </div>

          <div class="divide-y divide-slate-700/40">
            @for (conn of connectors(); track conn.platform) {
              <div
                class="p-4 hover:bg-slate-700/30 transition-colors flex flex-col md:flex-row md:items-center justify-between gap-3"
                [attr.data-testid]="'connector-row-' + conn.platform"
              >
                <div>
                  <div class="flex items-center gap-2">
                    <span class="font-bold text-white text-sm">{{ conn.name }}</span>
                    <span
                      [class]="conn.isConnected ? 'bg-emerald-900/60 text-emerald-300 border-emerald-700/60' : 'bg-rose-900/60 text-rose-300 border-rose-700/60'"
                      class="px-2 py-0.5 rounded text-[10px] font-semibold border"
                    >
                      {{ conn.isConnected ? 'Connected' : 'Disconnected' }}
                    </span>
                  </div>
                  <div class="text-xs text-slate-400 mt-1">
                    Platform: <span class="font-mono text-slate-300">{{ conn.platform }}</span> • Latency: <strong class="text-white">{{ conn.avgLatencyMs }} ms</strong>
                  </div>
                  <div class="text-xs text-slate-400 mt-0.5">
                    Hashed Audience Pool: <strong class="text-blue-400">{{ conn.hashedRecords | number:'1.0-0' }}</strong> identities
                  </div>
                </div>

                <div class="text-right self-end md:self-auto">
                  <div class="text-xs text-slate-400">Audiences Active</div>
                  <div class="text-base font-extrabold text-white font-mono">{{ conn.syncedAudiences }}</div>
                </div>
              </div>
            }
          </div>
        </div>

        <!-- Sync Job Stream & Recovery Console (5 cols) -->
        <div class="lg:col-span-5 bg-slate-800/80 border border-slate-700/60 rounded-xl p-5 space-y-4">
          <div class="flex items-center justify-between border-b border-slate-700/60 pb-3">
            <div>
              <h3 class="font-bold text-white text-base">Streaming Job Ledger</h3>
              <p class="text-xs text-slate-400">SHA-256 Incremental Push Engine</p>
            </div>
            <span class="text-xs px-2.5 py-1 rounded bg-blue-950 text-blue-300 border border-blue-800">
              Live Mesh
            </span>
          </div>

          <div class="space-y-3">
            @for (job of syncJobs(); track job.jobId) {
              <div class="p-3 bg-slate-900 rounded-xl border border-slate-800 space-y-2 text-xs" [attr.data-testid]="'sync-job-' + job.jobId">
                <div class="flex items-center justify-between">
                  <span class="font-bold text-white">{{ job.cohortName }}</span>
                  <span
                    [class]="job.status === 'StreamedToDsp' ? 'bg-emerald-900/60 text-emerald-300 border-emerald-700/60' : 'bg-sky-900/60 text-sky-300 border-sky-700/60'"
                    class="px-2 py-0.5 rounded text-[10px] font-semibold border"
                    data-testid="sync-status-badge"
                  >
                    {{ job.status }}
                  </span>
                </div>

                <div class="text-slate-400 flex items-center justify-between text-[11px]">
                  <span>Target: <strong class="text-slate-300">{{ job.platform }}</strong></span>
                  <span class="font-mono text-emerald-400">{{ job.recordsCount | number:'1.0-0' }} records</span>
                </div>

                <div class="flex items-center justify-between text-slate-500 text-[10px] pt-1 border-t border-slate-800/60">
                  <span>Job: {{ job.jobId }}</span>
                  <span class="font-mono text-sky-400">{{ job.latencyMs }} ms</span>
                </div>
              </div>
            }
          </div>

          <button
            (click)="retryFailedSync()"
            data-testid="retry-sync-btn"
            class="w-full py-2 bg-slate-700 hover:bg-slate-600 text-slate-200 rounded-lg text-xs font-semibold transition-colors"
          >
            ↺ Re-Sync Buffered / Rate-Limited Chunks
          </button>
        </div>
      </div>
    </div>
  `
})
export class AudienceSyncCockpitComponent {
  readonly connectors = signal<DspConnectorView[]>([
    {
      platform: 'GoogleCustomerMatch',
      name: 'Google Ads Enterprise MCC',
      isConnected: true,
      syncedAudiences: 12,
      hashedRecords: 485000,
      avgLatencyMs: 245.2,
      lastSync: '5 mins ago'
    },
    {
      platform: 'MetaCustomAudience',
      name: 'Meta Business Manager Global',
      isConnected: true,
      syncedAudiences: 18,
      hashedRecords: 620000,
      avgLatencyMs: 180.5,
      lastSync: '2 mins ago'
    },
    {
      platform: 'TikTokAudienceManager',
      name: 'TikTok Ads Manager Business Center',
      isConnected: true,
      syncedAudiences: 8,
      hashedRecords: 290000,
      avgLatencyMs: 310.0,
      lastSync: '15 mins ago'
    },
    {
      platform: 'LinkedInMatchedAudience',
      name: 'LinkedIn Campaign Manager',
      isConnected: true,
      syncedAudiences: 6,
      hashedRecords: 140000,
      avgLatencyMs: 420.8,
      lastSync: '1 hour ago'
    }
  ]);

  readonly syncJobs = signal<SyncJobView[]>([
    {
      jobId: 'sync_meta_vip_01',
      platform: 'MetaCustomAudience',
      cohortName: 'Enterprise High LTV VIPs',
      recordsCount: 3500,
      status: 'StreamedToDsp',
      latencyMs: 142.5,
      timestamp: 'Just now'
    },
    {
      jobId: 'sync_google_churn_02',
      platform: 'GoogleCustomerMatch',
      cohortName: 'Cart Abandoners 7-Day',
      recordsCount: 8200,
      status: 'StreamedToDsp',
      latencyMs: 198.0,
      timestamp: '3 mins ago'
    }
  ]);

  readonly totalHashedRecords = computed(() =>
    this.connectors().reduce((acc, c) => acc + c.hashedRecords, 0)
  );

  readonly connectedDspsCount = computed(() =>
    this.connectors().filter(c => c.isConnected).length
  );

  readonly avgLatency = computed(() => {
    const list = this.connectors();
    if (!list.length) return 0;
    return Math.round(list.reduce((acc, c) => acc + c.avgLatencyMs, 0) / list.length);
  });

  streamCohortSync(): void {
    const newJob: SyncJobView = {
      jobId: `sync_tiktok_${Date.now()}`,
      platform: 'TikTokAudienceManager',
      cohortName: 'Trial Expired Re-engagement',
      recordsCount: 4200,
      status: 'StreamedToDsp',
      latencyMs: 154.2,
      timestamp: 'Just now'
    };

    this.syncJobs.update(list => [newJob, ...list]);
  }

  retryFailedSync(): void {
    const retriedJob: SyncJobView = {
      jobId: `sync_recovered_${Date.now()}`,
      platform: 'LinkedInMatchedAudience',
      cohortName: 'Target ABM Account Decision Makers',
      recordsCount: 1200,
      status: 'RecoveredAndStreamed',
      latencyMs: 215.0,
      timestamp: 'Just now'
    };

    this.syncJobs.update(list => [retriedJob, ...list]);
  }
}
