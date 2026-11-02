import { Component, ChangeDetectionStrategy, signal, computed } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';

export interface CleanRoomParticipant {
  participantId: string;
  organizationName: string;
  role: string;
  cryptographicSaltId: string;
  status: string;
}

export interface AudienceCohortDataset {
  datasetId: string;
  participantId: string;
  cohortName: string;
  hashedIdentityCount: number;
  encryptionScheme: string;
}

export interface CleanRoomOverlapResult {
  queryId: string;
  rawOverlapMatchCount: number;
  privacyNoisedOverlapCount: number;
  matchRatePercent: number;
  attributedRevenue: number;
  privacyPreservingRoas: number;
  epsilonCost: number;
}

@Component({
  selector: 'app-data-clean-room-cockpit',
  standalone: true,
  imports: [CommonModule, FormsModule],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="p-6 max-w-7xl mx-auto space-y-6" dir="ltr" data-testid="data-clean-room-cockpit">
      <!-- Header -->
      <div class="flex flex-col md:flex-row md:items-center justify-between gap-4 border-b border-slate-700/60 pb-6">
        <div>
          <div class="flex items-center gap-2 text-xs font-semibold uppercase tracking-wider text-teal-400">
            <span class="inline-block w-2 h-2 rounded-full bg-teal-400 animate-pulse"></span>
            Zero-Knowledge Privacy & Ad Measurement • Horizon 53
          </div>
          <h1 class="text-3xl font-extrabold text-white tracking-tight mt-1">
            Autonomous Data Clean Room & Zero-Knowledge Measurement
          </h1>
          <p class="text-slate-400 text-sm mt-1">
            Private Set Intersection (PSI), differential privacy noise injection & cross-publisher multi-party attribution.
          </p>
        </div>

        <div class="flex items-center gap-3">
          <button
            (click)="runZeroKnowledgeOverlapQuery()"
            data-testid="run-zk-query-btn"
            class="px-4 py-2 bg-teal-600 hover:bg-teal-500 text-white rounded-lg text-sm font-semibold transition-colors flex items-center gap-2 shadow-lg shadow-teal-600/20"
          >
            <span>🔒</span> Run Zero-Knowledge Match
          </button>
        </div>
      </div>

      <!-- KPI Overview Stats -->
      <div class="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-4 gap-4">
        <div class="bg-slate-800/80 border border-slate-700/60 rounded-xl p-4">
          <div class="text-slate-400 text-xs font-medium uppercase">Secured Hashed Identities</div>
          <div class="text-2xl font-bold text-white mt-1" data-testid="stat-identities">{{ totalIdentities() | number:'1.0-0' }}</div>
          <div class="text-teal-400 text-xs mt-1">SHA-256 + HMAC Salted</div>
        </div>
        <div class="bg-slate-800/80 border border-slate-700/60 rounded-xl p-4">
          <div class="text-slate-400 text-xs font-medium uppercase">Differential Privacy Epsilon</div>
          <div class="text-2xl font-bold text-emerald-400 mt-1" data-testid="stat-epsilon">ε = 0.50</div>
          <div class="text-slate-400 text-xs mt-1">Strict Laplassian Noise Bounds</div>
        </div>
        <div class="bg-slate-800/80 border border-slate-700/60 rounded-xl p-4">
          <div class="text-slate-400 text-xs font-medium uppercase">Overlap Match Rate</div>
          <div class="text-2xl font-bold text-teal-400 mt-1" data-testid="stat-match-rate">{{ queryResult()?.matchRatePercent }}%</div>
          <div class="text-emerald-400 text-xs mt-1">Private Set Intersection (PSI)</div>
        </div>
        <div class="bg-slate-800/80 border border-slate-700/60 rounded-xl p-4">
          <div class="text-slate-400 text-xs font-medium uppercase">Attributed ROAS</div>
          <div class="text-2xl font-bold text-indigo-400 mt-1">{{ queryResult()?.privacyPreservingRoas }}x</div>
          <div class="text-slate-400 text-xs mt-1">Privacy-Preserving Metric</div>
        </div>
      </div>

      <!-- Main Stage: Participants/Cohorts & ZK Overlap Results -->
      <div class="grid grid-cols-1 lg:grid-cols-12 gap-6">
        <!-- Cohorts List (7 cols) -->
        <div class="lg:col-span-7 bg-slate-800/80 border border-slate-700/60 rounded-xl overflow-hidden">
          <div class="p-4 border-b border-slate-700/60 flex items-center justify-between">
            <h2 class="text-lg font-bold text-white">Salted First-Party & Publisher Cohorts</h2>
            <span class="text-xs text-slate-400">{{ datasets().length }} Cohorts Secured</span>
          </div>

          <div class="divide-y divide-slate-700/40">
            @for (ds of datasets(); track ds.datasetId) {
              <div
                class="p-4 hover:bg-slate-700/30 transition-colors flex flex-col md:flex-row md:items-center justify-between gap-3"
                [attr.data-testid]="'dataset-row-' + ds.datasetId"
              >
                <div>
                  <div class="flex items-center gap-2">
                    <span class="font-bold text-white text-sm">{{ ds.cohortName }}</span>
                    <span class="bg-teal-900/60 text-teal-300 border border-teal-700/60 px-2 py-0.5 rounded text-[10px] font-semibold">
                      Salted Ring
                    </span>
                  </div>
                  <div class="text-xs text-slate-400 mt-1">
                    Dataset ID: <span class="font-mono text-slate-300">{{ ds.datasetId }}</span> • Participant: <span class="text-slate-300">{{ ds.participantId }}</span>
                  </div>
                  <div class="text-xs text-slate-400 mt-0.5">
                    Encryption: <span class="text-slate-300">{{ ds.encryptionScheme }}</span>
                  </div>
                </div>

                <div class="text-right self-end md:self-auto">
                  <div class="text-xs text-slate-400">Audience Volume</div>
                  <div class="text-base font-extrabold text-white">{{ ds.hashedIdentityCount | number:'1.0-0' }} IDs</div>
                </div>
              </div>
            }
          </div>
        </div>

        <!-- Overlap Results Viewport (5 cols) -->
        <div class="lg:col-span-5 bg-slate-800/80 border border-slate-700/60 rounded-xl p-5 space-y-4">
          <div class="flex items-center justify-between border-b border-slate-700/60 pb-3">
            <div>
              <h3 class="font-bold text-white text-base">Zero-Knowledge Query Telemetry</h3>
              <p class="text-xs text-slate-400 font-mono">{{ queryResult()?.queryId || 'QUERY-INIT' }}</p>
            </div>
            <span class="text-xs px-2.5 py-1 rounded bg-teal-950 text-teal-300 border border-teal-800">
              PSI Match Verified
            </span>
          </div>

          @if (queryResult(); as result) {
            <div class="p-3.5 bg-slate-900 rounded-xl border border-slate-800 space-y-3" data-testid="zk-result-card">
              <div class="flex items-center justify-between text-xs">
                <span class="text-slate-400">Raw Intersect Match:</span>
                <span class="text-white font-mono font-bold">{{ result.rawOverlapMatchCount | number:'1.0-0' }} users</span>
              </div>
              <div class="flex items-center justify-between text-xs">
                <span class="text-slate-400">Differential Privacy Result:</span>
                <span class="text-teal-400 font-mono font-bold">{{ result.privacyNoisedOverlapCount | number:'1.0-0' }} users</span>
              </div>
              <div class="flex items-center justify-between text-xs">
                <span class="text-slate-400">Attributed Conversion GMV:</span>
                <span class="text-emerald-400 font-mono font-bold">\${{ result.attributedRevenue | number:'1.2-2' }}</span>
              </div>

              <div class="pt-2 border-t border-slate-800 text-xs text-slate-400">
                Privacy Budget: <strong class="text-teal-300">ε = {{ result.epsilonCost }}</strong> consumed without PII leakage.
              </div>
            </div>
          }

          <!-- Clean Room Governance Certificate -->
          <div class="pt-3 border-t border-slate-700/60 space-y-2">
            <div class="text-xs font-bold text-slate-300">Cryptographic Governance Audit:</div>
            <div class="p-2.5 bg-slate-900/60 rounded-lg border border-slate-800 text-[11px] text-slate-300 space-y-1">
              <div>✓ GDPR Art. 25 & CCPA Pseudonymization Compliant</div>
              <div>✓ Zero Raw PII Exchange between Publisher & Advertiser</div>
              <div>✓ Ephemeral Hash Salt Invalidation in 24h</div>
            </div>
          </div>
        </div>
      </div>
    </div>
  `
})
export class DataCleanRoomCockpitComponent {
  readonly participants = signal<CleanRoomParticipant[]>([
    {
      participantId: 'part_acme_brand',
      organizationName: 'Acme Consumer Brands Group',
      role: 'Advertiser',
      cryptographicSaltId: 'salt_acme_sha256_v4',
      status: 'Active'
    },
    {
      participantId: 'part_premium_pub',
      organizationName: 'Global News & Media Network',
      role: 'Publisher',
      cryptographicSaltId: 'salt_pub_sha256_v4',
      status: 'Active'
    }
  ]);

  readonly datasets = signal<AudienceCohortDataset[]>([
    {
      datasetId: 'ds_acme_high_ltv',
      participantId: 'part_acme_brand',
      cohortName: 'Acme 90-Day High LTV Purchasers (First-Party)',
      hashedIdentityCount: 84000,
      encryptionScheme: 'SHA-256 + HMAC Salted Ring'
    },
    {
      datasetId: 'ds_pub_tech_readers',
      participantId: 'part_premium_pub',
      cohortName: 'Premium Publisher Tech & SaaS Readers',
      hashedIdentityCount: 320000,
      encryptionScheme: 'SHA-256 + HMAC Salted Ring'
    }
  ]);

  readonly queryResult = signal<CleanRoomOverlapResult | null>({
    queryId: 'query_zk_784a9c',
    rawOverlapMatchCount: 23940,
    privacyNoisedOverlapCount: 23968,
    matchRatePercent: 28.53,
    attributedRevenue: 922768,
    privacyPreservingRoas: 6.15,
    epsilonCost: 0.50
  });

  readonly totalIdentities = computed(() =>
    this.datasets().reduce((acc, d) => acc + d.hashedIdentityCount, 0)
  );

  runZeroKnowledgeOverlapQuery(): void {
    const raw = Math.round(84000 * (0.28 + Math.random() * 0.05));
    const noised = raw + Math.floor(Math.random() * 40 - 20);

    this.queryResult.set({
      queryId: `query_zk_${Date.now()}`,
      rawOverlapMatchCount: raw,
      privacyNoisedOverlapCount: noised,
      matchRatePercent: Math.round((noised / 84000) * 10000) / 100,
      attributedRevenue: Math.round(noised * 38.5),
      privacyPreservingRoas: 6.25,
      epsilonCost: 0.50
    });
  }
}
