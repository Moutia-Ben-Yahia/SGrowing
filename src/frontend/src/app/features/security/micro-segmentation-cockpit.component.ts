import { Component, ChangeDetectionStrategy, signal, computed } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';

export interface ServiceIdentityRecord {
  spiffeId: string;
  serviceName: string;
  namespace: string;
  certExpiresAt: string;
  isMtlsActive: boolean;
}

export interface SecurityPolicyRecord {
  ruleId: string;
  sourceService: string;
  targetService: string;
  allowedPathPrefix: string;
  action: 'Allow' | 'Deny';
}

@Component({
  selector: 'app-micro-segmentation-cockpit',
  standalone: true,
  imports: [CommonModule, FormsModule],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="p-6 bg-slate-900 text-slate-100 min-h-screen" dir="ltr" data-testid="micro-segmentation-cockpit">
      <header class="mb-6 flex flex-col md:flex-row md:items-center justify-between gap-4 border-b border-slate-800 pb-4">
        <div>
          <div class="flex items-center gap-3">
            <span class="px-2.5 py-1 text-xs font-semibold rounded bg-red-500/20 text-red-400 border border-red-500/30">Horizon 85</span>
            <span class="text-xs text-slate-400">Zero-Trust Internal Network Defense</span>
          </div>
          <h1 class="text-2xl font-bold tracking-tight text-white mt-1">Autonomous Zero-Trust Micro-Segmentation & mTLS Mesh</h1>
          <p class="text-sm text-slate-400">Cryptographic SPIFFE identities, short-lived x509 SVID rotation, and L7 lateral movement denial.</p>
        </div>
        <div class="flex items-center gap-3">
          <button (click)="testRogueCall()" data-testid="test-rogue-call-btn" class="px-4 py-2 bg-red-600 hover:bg-red-500 text-white rounded font-medium text-sm transition-colors shadow">
            Simulate Lateral Breach Attempt
          </button>
        </div>
      </header>

      <!-- KPI Summary -->
      <section class="grid grid-cols-1 md:grid-cols-4 gap-4 mb-6">
        <div class="bg-slate-800/80 border border-slate-700/60 p-4 rounded-xl" data-testid="kpi-authenticated-services">
          <div class="text-xs font-medium text-slate-400 uppercase">Authenticated Mesh Services</div>
          <div class="text-2xl font-bold text-emerald-400 mt-1">{{ services().length }} Services</div>
          <div class="text-xs text-slate-400 mt-1">100% Active mTLS Encryption</div>
        </div>
        <div class="bg-slate-800/80 border border-slate-700/60 p-4 rounded-xl" data-testid="kpi-intercepted-threats">
          <div class="text-xs font-medium text-slate-400 uppercase">Rogue Requests Intercepted</div>
          <div class="text-2xl font-bold text-rose-400 mt-1">{{ interceptedCount() }}</div>
          <div class="text-xs text-slate-400 mt-1">Zero lateral traversal permitted</div>
        </div>
        <div class="bg-slate-800/80 border border-slate-700/60 p-4 rounded-xl" data-testid="kpi-active-policies">
          <div class="text-xs font-medium text-slate-400 uppercase">L7 Micro-Segmentation Rules</div>
          <div class="text-2xl font-bold text-white mt-1">{{ rules().length }} Policies</div>
          <div class="text-xs text-slate-400 mt-1">Default-Deny Zero-Trust Posture</div>
        </div>
        <div class="bg-slate-800/80 border border-slate-700/60 p-4 rounded-xl" data-testid="kpi-cert-health">
          <div class="text-xs font-medium text-slate-400 uppercase">Certificate Authority Health</div>
          <div class="text-2xl font-bold text-cyan-400 mt-1">Healthy</div>
          <div class="text-xs text-slate-400 mt-1">Automated SPIFFE SVID rotation</div>
        </div>
      </section>

      <!-- Services List -->
      <div class="bg-slate-800/60 border border-slate-700/60 rounded-xl p-5 mb-6">
        <h2 class="text-base font-semibold text-white mb-4">Cryptographic SPIFFE Service Identities & mTLS Keyrings</h2>
        <div class="grid grid-cols-1 md:grid-cols-2 gap-4">
          @for (svc of services(); track svc.serviceName) {
            <div class="bg-slate-900/60 border border-slate-700/50 p-4 rounded-lg" [attr.data-testid]="'service-card-' + svc.serviceName">
              <div class="flex items-center justify-between mb-2">
                <span class="text-sm font-bold text-white">{{ svc.serviceName }}</span>
                <span class="px-2 py-0.5 text-xs rounded bg-emerald-500/20 text-emerald-400 border border-emerald-500/30">
                  mTLS Active
                </span>
              </div>
              <div class="text-xs font-mono text-slate-400 mb-2 truncate">{{ svc.spiffeId }}</div>
              <div class="flex items-center justify-between text-xs pt-2 border-t border-slate-800">
                <span class="text-slate-400">Expires: {{ svc.certExpiresAt | date:'mediumDate' }}</span>
                <button (click)="rotateCert(svc)" [attr.data-testid]="'rotate-cert-btn-' + svc.serviceName" class="text-xs text-indigo-400 hover:text-indigo-300 font-medium">
                  Rotate SVID
                </button>
              </div>
            </div>
          }
        </div>
      </div>

      <!-- Policy Rules Table -->
      <div class="bg-slate-800/60 border border-slate-700/60 rounded-xl p-5">
        <h2 class="text-base font-semibold text-white mb-4">L7 Ingress/Egress Micro-Segmentation Policy Matrix</h2>
        <div class="overflow-x-auto">
          <table class="w-full text-left text-sm" data-testid="policies-table">
            <thead class="text-xs text-slate-400 uppercase border-b border-slate-700/60 bg-slate-800/40">
              <tr>
                <th class="p-3">Rule ID</th>
                <th class="p-3">Source Service</th>
                <th class="p-3">Target Service</th>
                <th class="p-3">Allowed Path Prefix</th>
                <th class="p-3">Action</th>
              </tr>
            </thead>
            <tbody class="divide-y divide-slate-700/40">
              @for (r of rules(); track r.ruleId) {
                <tr class="hover:bg-slate-700/20" [attr.data-testid]="'rule-row-' + r.ruleId">
                  <td class="p-3 font-mono text-xs text-slate-400">{{ r.ruleId }}</td>
                  <td class="p-3 font-semibold text-white">{{ r.sourceService }}</td>
                  <td class="p-3 text-slate-300">{{ r.targetService }}</td>
                  <td class="p-3 font-mono text-xs text-slate-300">{{ r.allowedPathPrefix }}</td>
                  <td class="p-3">
                    <span [ngClass]="{
                      'bg-emerald-500/20 text-emerald-400 border-emerald-500/30': r.action === 'Allow',
                      'bg-rose-500/20 text-rose-300 border-rose-500/30': r.action === 'Deny'
                    }" class="px-2 py-0.5 text-xs rounded border font-medium">
                      {{ r.action }}
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
export class MicroSegmentationCockpitComponent {
  services = signal<ServiceIdentityRecord[]>([
    {
      spiffeId: 'spiffe://sgrowing.internal/ns/core/sa/api-gateway',
      serviceName: 'api-gateway',
      namespace: 'core',
      certExpiresAt: '2026-11-10T12:00:00Z',
      isMtlsActive: true
    },
    {
      spiffeId: 'spiffe://sgrowing.internal/ns/finance/sa/billing-service',
      serviceName: 'billing-service',
      namespace: 'finance',
      certExpiresAt: '2026-11-10T12:00:00Z',
      isMtlsActive: true
    },
    {
      spiffeId: 'spiffe://sgrowing.internal/ns/analytics/sa/telemetry-agent',
      serviceName: 'telemetry-agent',
      namespace: 'analytics',
      certExpiresAt: '2026-11-10T12:00:00Z',
      isMtlsActive: true
    },
    {
      spiffeId: 'spiffe://sgrowing.internal/ns/sec/sa/vault-keyring',
      serviceName: 'vault-keyring',
      namespace: 'sec',
      certExpiresAt: '2026-11-10T12:00:00Z',
      isMtlsActive: true
    }
  ]);

  rules = signal<SecurityPolicyRecord[]>([
    {
      ruleId: 'pol_01',
      sourceService: 'api-gateway',
      targetService: 'billing-service',
      allowedPathPrefix: '/api/v1/billing',
      action: 'Allow'
    },
    {
      ruleId: 'pol_02',
      sourceService: 'billing-service',
      targetService: 'vault-keyring',
      allowedPathPrefix: '/api/v1/security/ephemeral',
      action: 'Allow'
    },
    {
      ruleId: 'pol_03',
      sourceService: 'telemetry-agent',
      targetService: 'vault-keyring',
      allowedPathPrefix: '*',
      action: 'Deny'
    }
  ]);

  interceptedCount = signal<number>(142);

  rotateCert(service: ServiceIdentityRecord) {
    this.services.update(list => list.map(s => {
      if (s.serviceName === service.serviceName) {
        return {
          ...s,
          certExpiresAt: '2026-12-15T12:00:00Z'
        };
      }
      return s;
    }));
  }

  testRogueCall() {
    this.interceptedCount.update(c => c + 1);
  }
}
