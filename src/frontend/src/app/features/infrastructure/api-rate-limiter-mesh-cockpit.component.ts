import { Component, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';

export interface ClientBucketProfile {
  clientId: string;
  tier: string;
  maxCapacityTokens: number;
  availableTokens: number;
  refillRateTokensPerSecond: number;
}

export interface QuarantinedIpRecord {
  ipAddress: string;
  reason: string;
  entropyScore: number;
  edgeSyncStatus: string;
}

@Component({
  selector: 'app-api-rate-limiter-mesh-cockpit',
  standalone: true,
  imports: [CommonModule, FormsModule],
  template: `
    <div dir="ltr" data-testid="api-rate-limiter-mesh-cockpit" class="p-6 bg-slate-900 text-slate-100 min-h-screen">
      <header class="mb-8 border-b border-slate-800 pb-4 flex justify-between items-center">
        <div>
          <h1 class="text-2xl font-bold tracking-tight text-white flex items-center gap-2">
            <span class="inline-block w-3 h-3 rounded-full bg-cyan-500 animate-pulse"></span>
            Zero-Trust API Gateway Rate Limiting & Token-Bucket DDoS Defense
          </h1>
          <p class="text-slate-400 text-sm mt-1">
            Distributed In-Memory Token Bucket, Entropy Scraper Detection & Progressive Tarpit Mitigation
          </p>
        </div>
        <div class="flex gap-3">
          <button (click)="quarantineSampleIp()" class="px-4 py-2 bg-rose-600 hover:bg-rose-500 text-white font-medium rounded-lg shadow-sm transition">
            Quarantine Malicious IP
          </button>
        </div>
      </header>

      <!-- KPI Metrics Grid -->
      <section class="grid grid-cols-1 md:grid-cols-4 gap-4 mb-8">
        <div class="bg-slate-800/60 border border-slate-700/50 p-4 rounded-xl">
          <span class="text-xs font-semibold text-slate-400 uppercase tracking-wider">Active Token Buckets</span>
          <p class="text-2xl font-extrabold text-white mt-1">{{ buckets().length }}</p>
          <span class="text-xs text-cyan-400">Multi-Cloud Edge Synced</span>
        </div>
        <div class="bg-slate-800/60 border border-slate-700/50 p-4 rounded-xl">
          <span class="text-xs font-semibold text-slate-400 uppercase tracking-wider">Quarantined IPs</span>
          <p class="text-2xl font-extrabold text-rose-400 mt-1">{{ quarantinedIps().length }}</p>
          <span class="text-xs text-slate-400">High-Entropy Scrapers</span>
        </div>
        <div class="bg-slate-800/60 border border-slate-700/50 p-4 rounded-xl">
          <span class="text-xs font-semibold text-slate-400 uppercase tracking-wider">Tarpit Delay</span>
          <p class="text-2xl font-extrabold text-amber-400 mt-1">250ms</p>
          <span class="text-xs text-slate-400">Progressive Rate Limiting</span>
        </div>
        <div class="bg-slate-800/60 border border-slate-700/50 p-4 rounded-xl">
          <span class="text-xs font-semibold text-slate-400 uppercase tracking-wider">Evaluated 24h</span>
          <p class="text-2xl font-extrabold text-emerald-400 mt-1">14.5M</p>
          <span class="text-xs text-emerald-400">0 False Positives</span>
        </div>
      </section>

      <!-- Main Panels -->
      <div class="grid grid-cols-1 lg:grid-cols-3 gap-6">
        <!-- Client Token Buckets -->
        <section class="lg:col-span-2 bg-slate-800/40 border border-slate-700/50 rounded-xl p-5">
          <h2 class="text-lg font-semibold text-white mb-4">Client Rate Limit Buckets</h2>
          <div class="overflow-x-auto">
            <table class="w-full text-left border-collapse text-sm">
              <thead>
                <tr class="border-b border-slate-700 text-slate-400 text-xs">
                  <th class="pb-3 font-medium">Client ID</th>
                  <th class="pb-3 font-medium">Tier</th>
                  <th class="pb-3 font-medium">Available Tokens</th>
                  <th class="pb-3 font-medium">Capacity</th>
                  <th class="pb-3 font-medium">Refill Rate</th>
                </tr>
              </thead>
              <tbody class="divide-y divide-slate-800">
                <tr *ngFor="let b of buckets()" class="hover:bg-slate-800/50 transition">
                  <td class="py-3 font-mono text-xs text-cyan-300 font-semibold">{{ b.clientId }}</td>
                  <td class="py-3">
                    <span class="px-2 py-0.5 text-xs rounded font-medium bg-slate-700 text-slate-200">
                      {{ b.tier }}
                    </span>
                  </td>
                  <td class="py-3 font-mono font-bold" [ngClass]="b.availableTokens < 100 ? 'text-rose-400' : 'text-emerald-400'">
                    {{ b.availableTokens.toLocaleString() }}
                  </td>
                  <td class="py-3 font-mono text-slate-400">{{ b.maxCapacityTokens.toLocaleString() }}</td>
                  <td class="py-3 font-mono text-indigo-300 text-xs">{{ b.refillRateTokensPerSecond }} t/s</td>
                </tr>
              </tbody>
            </table>
          </div>
        </section>

        <!-- Quarantined IPs -->
        <section class="bg-slate-800/40 border border-slate-700/50 rounded-xl p-5">
          <h2 class="text-lg font-semibold text-white mb-4">Edge IP Quarantine</h2>
          <div class="space-y-3">
            <div *ngFor="let q of quarantinedIps()" class="p-3 bg-slate-900/60 border border-rose-500/30 rounded-lg">
              <div class="flex justify-between items-center mb-1">
                <span class="font-mono text-xs text-rose-400 font-bold">{{ q.ipAddress }}</span>
                <span class="px-2 py-0.5 text-[10px] bg-rose-500/20 text-rose-300 rounded font-semibold font-mono">
                  Entropy: {{ q.entropyScore }}
                </span>
              </div>
              <p class="text-xs text-slate-300">{{ q.reason }}</p>
              <div class="mt-2 text-[10px] font-mono text-emerald-400">
                Status: {{ q.edgeSyncStatus }}
              </div>
            </div>
          </div>
        </section>
      </div>
    </div>
  `
})
export class ApiRateLimiterMeshCockpitComponent {
  buckets = signal<ClientBucketProfile[]>([
    { clientId: 'CLI-ENT-ACME-01', tier: 'ENTERPRISE_UNLIMITED', maxCapacityTokens: 50000, availableTokens: 48500, refillRateTokensPerSecond: 500.0 },
    { clientId: 'CLI-SCALE-TECH-02', tier: 'GROWTH_SCALE', maxCapacityTokens: 10000, availableTokens: 9200, refillRateTokensPerSecond: 100.0 },
    { clientId: 'CLI-STARTER-APP-03', tier: 'STARTER_TIER', maxCapacityTokens: 1000, availableTokens: 45, refillRateTokensPerSecond: 10.0 }
  ]);

  quarantinedIps = signal<QuarantinedIpRecord[]>([
    {
      ipAddress: '185.220.101.5',
      reason: 'Distributed Credential Stuffing & High-Entropy Header Scrape',
      entropyScore: 0.985,
      edgeSyncStatus: 'SYNCED_ALL_EDGE_POPS'
    }
  ]);

  quarantineSampleIp() {
    const newIp: QuarantinedIpRecord = {
      ipAddress: '198.51.100.77',
      reason: 'Abusive Token Burst & Automated Scraper Profile',
      entropyScore: 0.945,
      edgeSyncStatus: 'SYNCING_EDGE_NODES'
    };
    this.quarantinedIps.update(list => [newIp, ...list]);
  }
}
