import { Component, signal, computed } from '@angular/core';
import { CommonModule } from '@angular/common';

export interface IdentityNodeItem {
  identityGraphId: string;
  canonicalEmailSha256: string;
  phoneSha256: string;
  firstPartyCookieId: string;
  totalStitchedSessionsCount: number;
  uid2TokenActive: boolean;
}

export interface Uid2TokenItem {
  advertisingToken: string;
  refreshToken: string;
  saltBucketId: string;
  tokenStatus: string;
}

@Component({
  selector: 'app-uid2-tokenizer-cockpit',
  standalone: true,
  imports: [CommonModule],
  template: `
    <div class="p-6 max-w-7xl mx-auto space-y-6" dir="ltr" data-testid="uid2-tokenizer-cockpit">
      <!-- Header -->
      <div class="flex flex-col md:flex-row justify-between items-start md:items-center gap-4 bg-slate-900 border border-slate-800 p-6 rounded-2xl shadow-xl">
        <div>
          <div class="flex items-center gap-3">
            <span class="px-3 py-1 text-xs font-semibold rounded-full bg-indigo-500/20 text-indigo-400 border border-indigo-500/30">
              Horizon 112 • Domain D20 & C16
            </span>
            <span class="text-xs text-slate-400">First-Party Identity Graph &amp; UID2 Tokenizer</span>
          </div>
          <h1 class="text-2xl font-bold text-white mt-2">Cookieless Identity Graph &amp; UID2 Tokenizer</h1>
          <p class="text-slate-400 text-sm mt-1">
            Privacy-preserving first-party identity stitching, client-side salt rotation, and Unified ID 2.0 advertising tokenization.
          </p>
        </div>

        <div class="flex items-center gap-3">
          <button
            (click)="generateAdvertisingToken()"
            data-testid="generate-token-btn"
            class="px-4 py-2 bg-indigo-600 hover:bg-indigo-500 text-white text-sm font-medium rounded-xl transition shadow-lg shadow-indigo-600/30 flex items-center gap-2">
            Generate UID2 Token
          </button>
          <button
            (click)="stitchAnonymousSession()"
            data-testid="stitch-session-btn"
            class="px-4 py-2 bg-emerald-600 hover:bg-emerald-500 text-white text-sm font-medium rounded-xl transition shadow-lg shadow-emerald-600/30 flex items-center gap-2">
            Stitch Web Session
          </button>
        </div>
      </div>

      <!-- Metrics -->
      <div class="grid grid-cols-1 md:grid-cols-4 gap-4">
        <div class="bg-slate-900/80 border border-slate-800 p-5 rounded-2xl">
          <span class="text-xs font-medium text-slate-400">Total Stitched Identities</span>
          <p class="text-2xl font-bold text-white mt-1" data-testid="metric-total-identities">{{ totalIdentities() }}</p>
          <span class="text-xs text-emerald-400 mt-1 block">Cross-Device Identity Graph</span>
        </div>

        <div class="bg-slate-900/80 border border-slate-800 p-5 rounded-2xl">
          <span class="text-xs font-medium text-slate-400">Active Advertising Tokens</span>
          <p class="text-2xl font-bold text-indigo-400 mt-1" data-testid="metric-active-tokens">{{ activeTokens() }}</p>
          <span class="text-xs text-indigo-300 mt-1 block">UID2 &amp; EUID Compatible</span>
        </div>

        <div class="bg-slate-900/80 border border-slate-800 p-5 rounded-2xl">
          <span class="text-xs font-medium text-slate-400">Token Refresh Compliance</span>
          <p class="text-2xl font-bold text-emerald-400 mt-1" data-testid="metric-compliance">{{ tokenComplianceRate() }}%</p>
          <span class="text-xs text-slate-400 mt-1 block">Automatic Salt Rotation</span>
        </div>

        <div class="bg-slate-900/80 border border-slate-800 p-5 rounded-2xl">
          <span class="text-xs font-medium text-slate-400">Daily Stitched Touchpoints</span>
          <p class="text-2xl font-bold text-white mt-1" data-testid="metric-daily-touchpoints">{{ dailyTouchpoints() | number }}</p>
          <span class="text-xs text-emerald-400 mt-1 block">+22.4% Identity Match Lift</span>
        </div>
      </div>

      <!-- Identities Table -->
      <div class="bg-slate-900/80 border border-slate-800 rounded-2xl p-6">
        <h2 class="text-lg font-bold text-white mb-4">First-Party Identity Graph Nodes</h2>
        <div class="overflow-x-auto">
          <table class="w-full text-left text-sm text-slate-300">
            <thead class="text-xs uppercase bg-slate-800/60 text-slate-400 border-b border-slate-700">
              <tr>
                <th class="py-3 px-4">Graph ID</th>
                <th class="py-3 px-4">Canonical Email Hash</th>
                <th class="py-3 px-4">1st-Party Cookie</th>
                <th class="py-3 px-4 text-right">Stitched Sessions</th>
                <th class="py-3 px-4 text-right">UID2 Status</th>
              </tr>
            </thead>
            <tbody class="divide-y divide-slate-800">
              @for (id of identities(); track id.identityGraphId) {
                <tr class="hover:bg-slate-800/30 transition">
                  <td class="py-3 px-4 font-mono text-xs text-indigo-300">{{ id.identityGraphId }}</td>
                  <td class="py-3 px-4 font-mono text-xs text-slate-400">{{ id.canonicalEmailSha256 }}</td>
                  <td class="py-3 px-4 font-mono text-xs text-slate-300">{{ id.firstPartyCookieId }}</td>
                  <td class="py-3 px-4 text-right font-semibold text-white">{{ id.totalStitchedSessionsCount }}</td>
                  <td class="py-3 px-4 text-right">
                    <span class="px-2 py-0.5 text-xs rounded-full bg-emerald-500/20 text-emerald-400 border border-emerald-500/30 font-medium">
                      Active
                    </span>
                  </td>
                </tr>
              }
            </tbody>
          </table>
        </div>
      </div>
    </div>
  `
})
export class Uid2TokenizerCockpitComponent {
  totalIdentities = signal<number>(3);
  activeTokens = signal<number>(3);
  tokenComplianceRate = signal<number>(99.8);
  dailyTouchpoints = signal<number>(8450);

  identities = signal<IdentityNodeItem[]>([
    { identityGraphId: 'GRAPH-ID-01', canonicalEmailSha256: '8f4a9b2c8e11a9f029381...', phoneSha256: '99a0b12e847c2...', firstPartyCookieId: 'FP-COOKIE-9901', totalStitchedSessionsCount: 14, uid2TokenActive: true },
    { identityGraphId: 'GRAPH-ID-02', canonicalEmailSha256: '3b2c1a9e8841029410f92...', phoneSha256: '', firstPartyCookieId: 'FP-COOKIE-8814', totalStitchedSessionsCount: 8, uid2TokenActive: true },
    { identityGraphId: 'GRAPH-ID-03', canonicalEmailSha256: '110294f881249b01293ac...', phoneSha256: '44b912c019284...', firstPartyCookieId: 'FP-COOKIE-7721', totalStitchedSessionsCount: 22, uid2TokenActive: true }
  ]);

  generateAdvertisingToken(): void {
    this.activeTokens.update(v => v + 1);
  }

  stitchAnonymousSession(): void {
    this.dailyTouchpoints.update(v => v + 500);
    this.identities.update(list =>
      list.map(i => i.identityGraphId === 'GRAPH-ID-01' ? { ...i, totalStitchedSessionsCount: i.totalStitchedSessionsCount + 1 } : i)
    );
  }
}
