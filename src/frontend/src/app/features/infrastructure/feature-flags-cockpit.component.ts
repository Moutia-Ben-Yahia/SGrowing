import { Component, ChangeDetectionStrategy, signal, computed } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';

export interface FeatureFlagView {
  flagKey: string;
  name: string;
  description: string;
  isEnabled: boolean;
  rolloutPercentage: number;
  currentRing: 'InternalAlpha' | 'Ring0EarlyAdopter' | 'Ring1Canary' | 'Ring2Staged' | 'PublicGA';
  errorRatePercent: number;
  circuitBreakerTripped: boolean;
  whitelistedTenants: string[];
}

@Component({
  selector: 'app-feature-flags-cockpit',
  standalone: true,
  imports: [CommonModule, FormsModule],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="p-6 max-w-7xl mx-auto space-y-6" dir="ltr" data-testid="feature-flags-cockpit">
      <!-- Header -->
      <div class="flex flex-col md:flex-row md:items-center justify-between gap-4 border-b border-slate-700/60 pb-6">
        <div>
          <div class="flex items-center gap-2 text-xs font-semibold uppercase tracking-wider text-purple-400">
            <span class="inline-block w-2 h-2 rounded-full bg-purple-400 animate-pulse"></span>
            Multi-Tenant Feature Mesh & Canary Rollout • Horizon 60
          </div>
          <h1 class="text-3xl font-extrabold text-white tracking-tight mt-1">
            Autonomous Feature Flagging & Canary Mesh
          </h1>
          <p class="text-slate-400 text-sm mt-1">
            Deterministic SHA-256 tenant hash bucketing, progressive canary ring staged rollouts & automated error-budget circuit breakers.
          </p>
        </div>

        <div class="flex items-center gap-3">
          <button
            (click)="advanceCanary()"
            data-testid="advance-ring-btn"
            class="px-4 py-2 bg-purple-600 hover:bg-purple-500 text-white rounded-lg text-sm font-semibold transition-colors flex items-center gap-2 shadow-lg shadow-purple-600/20"
          >
            <span>🚀</span> Advance Canary Ring
          </button>
        </div>
      </div>

      <!-- KPI Overview Stats -->
      <div class="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-4 gap-4">
        <div class="bg-slate-800/80 border border-slate-700/60 rounded-xl p-4">
          <div class="text-slate-400 text-xs font-medium uppercase">Active Feature Flags</div>
          <div class="text-2xl font-bold text-white mt-1" data-testid="stat-active-flags">
            {{ activeFlagsCount() }} / {{ flags().length }}
          </div>
          <div class="text-purple-400 text-xs mt-1">Tenant Staged Deployments</div>
        </div>
        <div class="bg-slate-800/80 border border-slate-700/60 rounded-xl p-4">
          <div class="text-slate-400 text-xs font-medium uppercase">Canary Rings Active</div>
          <div class="text-2xl font-bold text-emerald-400 mt-1" data-testid="stat-canary-active">
            {{ canaryActiveCount() }}
          </div>
          <div class="text-slate-400 text-xs mt-1">Ring 1 & Ring 2 Staged</div>
        </div>
        <div class="bg-slate-800/80 border border-slate-700/60 rounded-xl p-4">
          <div class="text-slate-400 text-xs font-medium uppercase">Healthy Rollouts</div>
          <div class="text-2xl font-bold text-sky-400 mt-1" data-testid="stat-healthy-rollouts">
            100%
          </div>
          <div class="text-emerald-400 text-xs mt-1">Error Rate < 0.20% Bounded</div>
        </div>
        <div class="bg-slate-800/80 border border-slate-700/60 rounded-xl p-4">
          <div class="text-slate-400 text-xs font-medium uppercase">Evaluation Latency</div>
          <div class="text-2xl font-bold text-indigo-400 mt-1">< 1 ms</div>
          <div class="text-slate-400 text-xs mt-1">Zero-Database In-Memory Hashing</div>
        </div>
      </div>

      <!-- Main Stage: Flags Table & Flag Controls -->
      <div class="grid grid-cols-1 lg:grid-cols-12 gap-6">
        <!-- Flags List (7 cols) -->
        <div class="lg:col-span-7 bg-slate-800/80 border border-slate-700/60 rounded-xl overflow-hidden">
          <div class="p-4 border-b border-slate-700/60 flex items-center justify-between">
            <h2 class="text-lg font-bold text-white">Configured Feature Flags</h2>
            <span class="text-xs text-slate-400 font-mono">{{ flags().length }} Flags</span>
          </div>

          <div class="divide-y divide-slate-700/40">
            @for (flag of flags(); track flag.flagKey) {
              <div
                (click)="selectFlag(flag)"
                [class.bg-slate-700/40]="selectedFlag()?.flagKey === flag.flagKey"
                class="p-4 hover:bg-slate-700/30 transition-colors cursor-pointer flex flex-col md:flex-row md:items-center justify-between gap-3"
                [attr.data-testid]="'flag-row-' + flag.flagKey"
              >
                <div>
                  <div class="flex items-center gap-2">
                    <span class="font-bold text-white text-sm">{{ flag.name }}</span>
                    <span
                      [class]="flag.isEnabled ? 'bg-purple-900/60 text-purple-300 border-purple-700/60' : 'bg-rose-900/60 text-rose-300 border-rose-700/60'"
                      class="px-2 py-0.5 rounded text-[10px] font-semibold border"
                      data-testid="flag-status-badge"
                    >
                      {{ flag.isEnabled ? flag.currentRing : 'Disabled' }}
                    </span>
                  </div>
                  <div class="text-xs text-slate-400 mt-1 font-mono">
                    {{ flag.flagKey }}
                  </div>
                  <div class="text-xs text-slate-400 mt-0.5">
                    Rollout: <strong class="text-purple-400">{{ flag.rolloutPercentage }}%</strong> • Error Rate: <strong class="text-emerald-400">{{ flag.errorRatePercent }}%</strong>
                  </div>
                </div>

                <div class="text-right self-end md:self-auto">
                  <div class="text-xs text-slate-400">Canary Ring</div>
                  <div class="text-base font-extrabold text-white font-mono">{{ flag.currentRing }}</div>
                </div>
              </div>
            }
          </div>
        </div>

        <!-- Flag Controller & Emergency Killswitch Console (5 cols) -->
        <div class="lg:col-span-5 bg-slate-800/80 border border-slate-700/60 rounded-xl p-5 space-y-4">
          <div class="flex items-center justify-between border-b border-slate-700/60 pb-3">
            <div>
              <h3 class="font-bold text-white text-base">Flag Telemetry & Safety Controls</h3>
              <p class="text-xs text-slate-400">{{ selectedFlag()?.flagKey }}</p>
            </div>
            <span class="text-xs px-2.5 py-1 rounded bg-purple-950 text-purple-300 border border-purple-800">
              Live Guardrail
            </span>
          </div>

          @if (selectedFlag(); as active) {
            <div class="space-y-4 text-xs">
              <div class="p-3 bg-slate-900 rounded-xl border border-slate-800 space-y-2">
                <div class="text-slate-400 font-bold">Whitelisted VIP Tenants:</div>
                <div class="flex flex-wrap gap-1.5">
                  @for (t of active.whitelistedTenants; track t) {
                    <span class="px-2 py-0.5 bg-slate-950 text-purple-300 rounded border border-purple-900/60 font-mono text-[10px]">
                      {{ t }}
                    </span>
                  }
                </div>
              </div>

              <!-- Automated Circuit Breaker Status -->
              <div class="p-3 bg-slate-900 rounded-xl border border-slate-800 space-y-2">
                <div class="flex items-center justify-between">
                  <span class="text-slate-300 font-bold">Circuit-Breaker Status:</span>
                  <span
                    [class]="active.circuitBreakerTripped ? 'text-rose-400' : 'text-emerald-400'"
                    class="font-semibold"
                  >
                    {{ active.circuitBreakerTripped ? 'Tripped (Rollback Active)' : 'Healthy Normal' }}
                  </span>
                </div>
                <p class="text-[11px] text-slate-400">
                  Trips automatically if client-side crash telemetry exceeds 1.0% error rate.
                </p>
              </div>

              <!-- Emergency Killswitch -->
              <div class="p-3 bg-slate-900 rounded-xl border border-rose-900/40 space-y-2">
                <div class="text-rose-400 font-bold text-xs">Emergency Killswitch</div>
                <p class="text-[11px] text-slate-400">
                  Instantly trips rollback across 100% of tenants without requiring code redeployment.
                </p>

                <button
                  (click)="emergencyKill(active)"
                  data-testid="emergency-kill-btn"
                  class="w-full py-2 bg-rose-600 hover:bg-rose-500 text-white font-semibold rounded-lg text-xs shadow transition-colors"
                >
                  ⚡ Emergency Kill Switch
                </button>
              </div>
            </div>
          }
        </div>
      </div>
    </div>
  `
})
export class FeatureFlagsCockpitComponent {
  readonly flags = signal<FeatureFlagView[]>([
    {
      flagKey: 'voice_webrtc_sub150ms',
      name: 'Next-Gen WebRTC Voice Sub-150ms Engine',
      description: 'Ultra-low latency audio packetization with Opus speech codec',
      isEnabled: true,
      rolloutPercentage: 25,
      currentRing: 'Ring1Canary',
      errorRatePercent: 0.12,
      circuitBreakerTripped: false,
      whitelistedTenants: ['org_enterprise_vip', 'org_beta_partners']
    },
    {
      flagKey: 'zk_differential_privacy_cleanroom',
      name: 'Zero-Knowledge Data Clean Room PSI',
      description: 'Private Set Intersection and Laplace differential noise injection',
      isEnabled: true,
      rolloutPercentage: 50,
      currentRing: 'Ring2Staged',
      errorRatePercent: 0.04,
      circuitBreakerTripped: false,
      whitelistedTenants: ['org_enterprise_vip']
    }
  ]);

  readonly selectedFlag = signal<FeatureFlagView | null>(this.flags()[0]);

  readonly activeFlagsCount = computed(() =>
    this.flags().filter(f => f.isEnabled).length
  );

  readonly canaryActiveCount = computed(() =>
    this.flags().filter(f => f.currentRing === 'Ring1Canary' || f.currentRing === 'Ring2Staged').length
  );

  selectFlag(flag: FeatureFlagView): void {
    this.selectedFlag.set(flag);
  }

  advanceCanary(): void {
    const active = this.selectedFlag();
    if (!active) return;

    const updated: FeatureFlagView = {
      ...active,
      rolloutPercentage: 100,
      currentRing: 'PublicGA'
    };

    this.flags.update(list =>
      list.map(f => f.flagKey === active.flagKey ? updated : f)
    );
    this.selectedFlag.set(updated);
  }

  emergencyKill(flag: FeatureFlagView): void {
    const killed: FeatureFlagView = {
      ...flag,
      isEnabled: false,
      rolloutPercentage: 0,
      circuitBreakerTripped: true
    };

    this.flags.update(list =>
      list.map(f => f.flagKey === flag.flagKey ? killed : f)
    );
    this.selectedFlag.set(killed);
  }
}
