import { Component, ChangeDetectionStrategy, signal, computed } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';

export interface VaultSecretItem {
  secretId: string;
  name: string;
  type: 'DatabaseMasterCredentials' | 'JwtSigningKey' | 'TlsPrivateKey' | 'StripeApiKey';
  version: number;
  phase: 'ActivePrimary' | 'DeprecatedSecondary' | 'ZeroizedPurged';
  ageInDays: number;
}

@Component({
  selector: 'app-secret-vault-mesh-cockpit',
  standalone: true,
  imports: [CommonModule, FormsModule],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="p-6 bg-slate-900 text-slate-100 min-h-screen" dir="ltr" data-testid="secret-vault-mesh-cockpit">
      <header class="mb-6 flex flex-col md:flex-row md:items-center justify-between gap-4 border-b border-slate-800 pb-4">
        <div>
          <div class="flex items-center gap-3">
            <span class="px-2.5 py-1 text-xs font-semibold rounded bg-rose-500/20 text-rose-400 border border-rose-500/30">Horizon 90</span>
            <span class="text-xs text-slate-400">Cryptographic Mesh & Zeroization Defense</span>
          </div>
          <h1 class="text-2xl font-bold tracking-tight text-white mt-1">Multi-Region Secret Rotation & Vault Zeroization Mesh</h1>
          <p class="text-sm text-slate-400">Cryptographic key versioning, zero-downtime rolling rotation, and emergency multi-region RAM zeroization.</p>
        </div>
      </header>

      <!-- KPI Summary -->
      <section class="grid grid-cols-1 md:grid-cols-4 gap-4 mb-6">
        <div class="bg-slate-800/80 border border-slate-700/60 p-4 rounded-xl" data-testid="kpi-active-keys">
          <div class="text-xs font-medium text-slate-400 uppercase">Active Master Keys</div>
          <div class="text-2xl font-bold text-emerald-400 mt-1">{{ activeSecretsCount() }} / {{ secrets().length }}</div>
          <div class="text-xs text-slate-400 mt-1">Multi-region synchronised</div>
        </div>
        <div class="bg-slate-800/80 border border-slate-700/60 p-4 rounded-xl" data-testid="kpi-average-key-age">
          <div class="text-xs font-medium text-slate-400 uppercase">Average Key Age</div>
          <div class="text-2xl font-bold text-cyan-400 mt-1">{{ averageAge() | number:'1.0-0' }} Days</div>
          <div class="text-xs text-slate-400 mt-1">Policy threshold: < 90 days</div>
        </div>
        <div class="bg-slate-800/80 border border-slate-700/60 p-4 rounded-xl" data-testid="kpi-zeroized-keys">
          <div class="text-xs font-medium text-slate-400 uppercase">Zeroized & Purged Keys</div>
          <div class="text-2xl font-bold text-rose-400 mt-1">{{ zeroizedCount() }}</div>
          <div class="text-xs text-slate-400 mt-1">Cryptographically scrubbed RAM</div>
        </div>
        <div class="bg-slate-800/80 border border-slate-700/60 p-4 rounded-xl" data-testid="kpi-rotation-cadence">
          <div class="text-xs font-medium text-slate-400 uppercase">Automated Rotation SLA</div>
          <div class="text-2xl font-bold text-white mt-1">100% Compliant</div>
          <div class="text-xs text-slate-400 mt-1">Hardware HSM / KMS Backed</div>
        </div>
      </section>

      <!-- Secrets Grid -->
      <section class="grid grid-cols-1 lg:grid-cols-3 gap-6">
        <div class="lg:col-span-2 space-y-4">
          <h2 class="text-lg font-semibold text-white">Multi-Region Secret Registry</h2>
          <div class="space-y-3">
            @for (sec of secrets(); track sec.secretId) {
              <div
                class="bg-slate-800/60 border border-slate-700 p-4 rounded-xl hover:border-slate-600 transition-all cursor-pointer"
                [class.ring-2]="selectedSecret()?.secretId === sec.secretId"
                [class.ring-rose-500]="selectedSecret()?.secretId === sec.secretId"
                (click)="selectSecret(sec)"
                data-testid="secret-vault-card"
              >
                <div class="flex items-center justify-between">
                  <div>
                    <h3 class="font-semibold text-white text-base">{{ sec.name }}</h3>
                    <p class="text-xs text-slate-400 mt-0.5">ID: {{ sec.secretId }} &bull; Type: {{ sec.type }}</p>
                  </div>
                  <div class="text-right">
                    <span class="text-xs px-2.5 py-1 rounded font-medium"
                      [class.bg-emerald-500/20]="sec.phase === 'ActivePrimary'"
                      [class.text-emerald-400]="sec.phase === 'ActivePrimary'"
                      [class.bg-rose-500/20]="sec.phase === 'ZeroizedPurged'"
                      [class.text-rose-400]="sec.phase === 'ZeroizedPurged'"
                      [class.border]="true"
                      [class.border-emerald-500/30]="sec.phase === 'ActivePrimary'"
                      [class.border-rose-500/30]="sec.phase === 'ZeroizedPurged'"
                    >
                      {{ sec.phase }}
                    </span>
                    <div class="text-xs text-slate-400 mt-1">Version: v{{ sec.version }} &bull; Age: {{ sec.ageInDays }}d</div>
                  </div>
                </div>

                <div class="mt-4 flex items-center justify-between pt-2 border-t border-slate-700/60">
                  <div class="text-xs text-slate-400">
                    KMS Encryption: <span class="font-mono text-slate-200">ChaCha20-Poly1305 / AES-256-GCM</span>
                  </div>

                  <div class="flex items-center gap-2">
                    @if (sec.phase === 'ZeroizedPurged') {
                      <span class="text-xs px-2.5 py-1 rounded bg-rose-500/20 text-rose-400 border border-rose-500/30 font-medium" data-testid="zeroized-alert-badge">
                        Purged From Memory
                      </span>
                    } @else {
                      <button
                        (click)="rotateSecret(sec.secretId); $event.stopPropagation()"
                        data-testid="btn-rotate-secret"
                        class="px-3 py-1 bg-sky-600 hover:bg-sky-500 text-white text-xs font-semibold rounded shadow transition-colors"
                      >
                        Rotate Secret
                      </button>
                      <button
                        (click)="zeroizeSecret(sec.secretId); $event.stopPropagation()"
                        data-testid="btn-zeroize-secret"
                        class="px-3 py-1 bg-rose-700 hover:bg-rose-600 text-white text-xs font-semibold rounded shadow transition-colors"
                      >
                        Zeroize
                      </button>
                    }
                  </div>
                </div>
              </div>
            }
          </div>
        </div>

        <!-- Detail Sidebar -->
        <div class="bg-slate-800/60 border border-slate-700 p-5 rounded-xl flex flex-col justify-between">
          <div>
            <h2 class="text-lg font-semibold text-white mb-3">Cryptographic Key Envelope</h2>
            @if (selectedSecret(); as sel) {
              <div class="space-y-4">
                <div class="p-3 bg-slate-900/60 rounded-lg border border-slate-700/60">
                  <div class="text-xs text-slate-400 uppercase">Selected Secret</div>
                  <div class="text-base font-bold text-white mt-0.5">{{ sel.name }}</div>
                  <div class="text-xs text-rose-400 mt-1">Lifecycle State: {{ sel.phase }}</div>
                </div>

                <div class="space-y-2 text-sm">
                  <div class="flex justify-between py-1 border-b border-slate-700/50">
                    <span class="text-slate-400">Current Generation:</span>
                    <span class="font-mono text-white">v{{ sel.version }}</span>
                  </div>
                  <div class="flex justify-between py-1 border-b border-slate-700/50">
                    <span class="text-slate-400">Key Age:</span>
                    <span class="font-mono text-cyan-300">{{ sel.ageInDays }} days</span>
                  </div>
                  <div class="flex justify-between py-1 border-b border-slate-700/50">
                    <span class="text-slate-400">Multi-Region Quorum:</span>
                    <span class="font-mono text-emerald-400">Synced across 3 Regions</span>
                  </div>
                </div>

                @if (sel.phase !== 'ZeroizedPurged') {
                  <div class="pt-2 flex flex-col gap-2">
                    <button
                      (click)="rotateSecret(sel.secretId)"
                      class="w-full py-2 bg-gradient-to-r from-sky-600 to-blue-600 hover:from-sky-500 hover:to-blue-500 text-white font-semibold rounded text-sm shadow transition-all"
                    >
                      Execute Rolling Key Rotation
                    </button>
                    <button
                      (click)="zeroizeSecret(sel.secretId)"
                      class="w-full py-2 bg-rose-700 hover:bg-rose-600 text-white font-semibold rounded text-sm shadow transition-all"
                    >
                      Emergency Zeroize Key
                    </button>
                  </div>
                }
              </div>
            } @else {
              <p class="text-xs text-slate-400">Select a secret to inspect envelope.</p>
            }
          </div>

          <div class="mt-6 p-3 bg-slate-900/80 rounded border border-slate-800 text-xs text-slate-400">
            <span class="text-rose-400 font-semibold">Zero-Trust Memory Scrubbing:</span> Zeroization overwrites memory locations with cryptographically secure random bytes before freeing pages.
          </div>
        </div>
      </section>
    </div>
  `
})
export class SecretVaultMeshCockpitComponent {
  secrets = signal<VaultSecretItem[]>([
    {
      secretId: 'sec_db_master_01',
      name: 'PostgreSQL Master Shard Root',
      type: 'DatabaseMasterCredentials',
      version: 3,
      phase: 'ActivePrimary',
      ageInDays: 14
    },
    {
      secretId: 'sec_jwt_ed25519_02',
      name: 'JWT Ed25519 Token Signing Key',
      type: 'JwtSigningKey',
      version: 7,
      phase: 'ActivePrimary',
      ageInDays: 28
    },
    {
      secretId: 'sec_tls_wildcard_03',
      name: 'Wildcard *.sgrowing.io TLS Private Key',
      type: 'TlsPrivateKey',
      version: 2,
      phase: 'ActivePrimary',
      ageInDays: 45
    },
    {
      secretId: 'sec_stripe_live_04',
      name: 'Stripe Production Billing Secret Key',
      type: 'StripeApiKey',
      version: 4,
      phase: 'ActivePrimary',
      ageInDays: 60
    }
  ]);

  selectedSecret = signal<VaultSecretItem | null>(this.secrets()[0]);

  activeSecretsCount = computed(() =>
    this.secrets().filter(s => s.phase === 'ActivePrimary').length
  );

  zeroizedCount = computed(() =>
    this.secrets().filter(s => s.phase === 'ZeroizedPurged').length
  );

  averageAge = computed(() => {
    const list = this.secrets().filter(s => s.phase === 'ActivePrimary');
    if (!list.length) return 0;
    return list.reduce((sum, s) => sum + s.ageInDays, 0) / list.length;
  });

  selectSecret(secret: VaultSecretItem): void {
    this.selectedSecret.set(secret);
  }

  rotateSecret(secretId: string): void {
    this.secrets.update(list =>
      list.map(s => s.secretId === secretId ? { ...s, version: s.version + 1, ageInDays: 0, phase: 'ActivePrimary' } : s)
    );
    if (this.selectedSecret()?.secretId === secretId) {
      this.selectedSecret.update(curr => curr ? { ...curr, version: curr.version + 1, ageInDays: 0, phase: 'ActivePrimary' } : null);
    }
  }

  zeroizeSecret(secretId: string): void {
    this.secrets.update(list =>
      list.map(s => s.secretId === secretId ? { ...s, phase: 'ZeroizedPurged', ageInDays: 0 } : s)
    );
    if (this.selectedSecret()?.secretId === secretId) {
      this.selectedSecret.update(curr => curr ? { ...curr, phase: 'ZeroizedPurged', ageInDays: 0 } : null);
    }
  }
}
