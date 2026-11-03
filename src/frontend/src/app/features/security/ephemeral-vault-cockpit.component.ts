import { Component, ChangeDetectionStrategy, signal, computed } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';

export interface EphemeralTokenView {
  tokenId: string;
  user: string;
  scope: string;
  reason: string;
  status: 'ActiveValid' | 'RevokedEmergency' | 'ExpiredTtl';
  minutesLeft: number;
  signature: string;
}

@Component({
  selector: 'app-ephemeral-vault-cockpit',
  standalone: true,
  imports: [CommonModule, FormsModule],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="p-6 max-w-7xl mx-auto space-y-6" dir="ltr" data-testid="ephemeral-vault-cockpit">
      <!-- Header -->
      <div class="flex flex-col md:flex-row md:items-center justify-between gap-4 border-b border-slate-700/60 pb-6">
        <div>
          <div class="flex items-center gap-2 text-xs font-semibold uppercase tracking-wider text-rose-400">
            <span class="inline-block w-2 h-2 rounded-full bg-rose-400 animate-pulse"></span>
            Zero-Trust Security & Ephemeral Privilege • Horizon 75
          </div>
          <h1 class="text-3xl font-extrabold text-white tracking-tight mt-1">
            Zero-Trust Ephemeral Credential Vault & JIT Privilege Mesh
          </h1>
          <p class="text-slate-400 text-sm mt-1">
            Time-bounded single-use credentials with short TTL (5-60 min), JIT peer authorization & emergency kill switches.
          </p>
        </div>

        <div class="flex items-center gap-3">
          <button
            (click)="triggerEmergencyKillAll()"
            data-testid="emergency-kill-btn"
            class="px-4 py-2 bg-rose-600 hover:bg-rose-500 text-white rounded-lg text-sm font-semibold transition-colors flex items-center gap-2 shadow-lg shadow-rose-600/20"
          >
            <span>🛑</span> Emergency Invalidate All Credentials
          </button>
        </div>
      </div>

      <!-- KPI Overview Stats -->
      <div class="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-4 gap-4">
        <div class="bg-slate-800/80 border border-slate-700/60 rounded-xl p-4">
          <div class="text-slate-400 text-xs font-medium uppercase">Active Valid Credentials</div>
          <div class="text-2xl font-bold mt-1" [class]="activeCount() > 0 ? 'text-emerald-400' : 'text-slate-400'" data-testid="stat-active-count">
            {{ activeCount() }} Active
          </div>
          <div class="text-slate-400 text-xs mt-1">Single-Use Self-Destructing Tokens</div>
        </div>
        <div class="bg-slate-800/80 border border-slate-700/60 rounded-xl p-4">
          <div class="text-slate-400 text-xs font-medium uppercase">Revoked Emergency Tokens</div>
          <div class="text-2xl font-bold text-rose-400 mt-1" data-testid="stat-revoked-count">
            {{ revokedCount() }}
          </div>
          <div class="text-rose-400 text-xs mt-1">Distributed Mesh Invalidation</div>
        </div>
        <div class="bg-slate-800/80 border border-slate-700/60 rounded-xl p-4">
          <div class="text-slate-400 text-xs font-medium uppercase">Max Allowed Token TTL</div>
          <div class="text-2xl font-bold text-white mt-1" data-testid="stat-max-ttl">
            60 min
          </div>
          <div class="text-emerald-400 text-xs mt-1">Zero Standing Privilege Guarantee</div>
        </div>
        <div class="bg-slate-800/80 border border-slate-700/60 rounded-xl p-4">
          <div class="text-slate-400 text-xs font-medium uppercase">Cryptographic Audit Sealing</div>
          <div class="text-2xl font-bold text-cyan-400 mt-1" data-testid="stat-crypto-status">
            SHA-256 Signed
          </div>
          <div class="text-slate-400 text-xs mt-1">Non-Repudiation Access Log</div>
        </div>
      </div>

      <!-- Active Credentials Ledger -->
      <div class="bg-slate-800/80 border border-slate-700/60 rounded-xl overflow-hidden">
        <div class="p-4 border-b border-slate-700/60 flex items-center justify-between">
          <h2 class="text-lg font-bold text-white">Active Ephemeral Credentials & Countdown Monitor</h2>
          <span class="text-xs text-slate-400 font-mono">{{ tokens().length }} Tracked Tokens</span>
        </div>

        <div class="divide-y divide-slate-700/40">
          @for (tok of tokens(); track tok.tokenId) {
            <div class="p-4 hover:bg-slate-700/30 transition-colors flex flex-col md:flex-row md:items-center justify-between gap-4">
              <div class="space-y-1">
                <div class="flex items-center gap-2">
                  <span class="font-bold text-white text-sm font-mono">{{ tok.tokenId }}</span>
                  <span
                    class="px-2 py-0.5 rounded text-[10px] font-semibold"
                    [class]="tok.status === 'ActiveValid' ? 'bg-emerald-950 text-emerald-300 border border-emerald-800' : 'bg-rose-950 text-rose-300 border border-rose-800'"
                    data-testid="token-status-badge"
                  >
                    {{ tok.status }}
                  </span>
                  <span class="px-2 py-0.5 rounded text-[10px] font-semibold bg-slate-800 text-cyan-300 border border-slate-700">
                    {{ tok.scope }}
                  </span>
                </div>
                <div class="text-xs text-slate-400">
                  User: <strong class="text-slate-200">{{ tok.user }}</strong> • Justification: <span class="italic text-slate-300">{{ tok.reason }}</span>
                </div>
                <div class="text-xs text-slate-400 font-mono text-[11px]">
                  Signature: <span class="text-cyan-400">{{ tok.signature }}</span>
                </div>
              </div>

              <div class="flex items-center gap-4">
                <div class="text-right">
                  <div class="text-xs text-slate-400">TTL Remaining</div>
                  <div class="text-sm font-bold font-mono" [class]="tok.status === 'ActiveValid' ? 'text-emerald-400' : 'text-slate-500'">
                    {{ tok.status === 'ActiveValid' ? tok.minutesLeft + ' min' : '0 min' }}
                  </div>
                </div>

                @if (tok.status === 'ActiveValid') {
                  <button
                    (click)="revokeSingleToken(tok)"
                    data-testid="revoke-token-btn"
                    class="px-3 py-1 bg-rose-600 hover:bg-rose-500 text-white font-semibold rounded text-xs transition-colors"
                  >
                    Revoke Token
                  </button>
                }
              </div>
            </div>
          }
        </div>
      </div>
    </div>
  `
})
export class EphemeralVaultCockpitComponent {
  readonly tokens = signal<EphemeralTokenView[]>([
    {
      tokenId: 'eph_tok_9011',
      user: 'usr_sec_lead',
      scope: 'CustomerPiiAccess',
      reason: 'GDPR subject data erasure verification',
      status: 'ActiveValid',
      minutesLeft: 14,
      signature: '9F3A1D7E4C8B2A6F11223344'
    },
    {
      tokenId: 'eph_tok_9012',
      user: 'usr_sre_operator',
      scope: 'ProdDeploy',
      reason: 'Emergency hotfix Canary ring 0 promotion',
      status: 'ActiveValid',
      minutesLeft: 28,
      signature: 'AA77BB22CC33DD44EE55FF66'
    }
  ]);

  readonly activeCount = computed(() =>
    this.tokens().filter(t => t.status === 'ActiveValid').length
  );

  readonly revokedCount = computed(() =>
    this.tokens().filter(t => t.status === 'RevokedEmergency').length
  );

  revokeSingleToken(tok: EphemeralTokenView): void {
    this.tokens.update(list =>
      list.map(t => t.tokenId === tok.tokenId ? { ...t, status: 'RevokedEmergency' as const, minutesLeft: 0 } : t)
    );
  }

  triggerEmergencyKillAll(): void {
    this.tokens.update(list =>
      list.map(t => ({ ...t, status: 'RevokedEmergency' as const, minutesLeft: 0 }))
    );
  }
}
