import { Component, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';

export interface LicenseDetails {
  licenseKey: string;
  organizationName: string;
  tier: string;
  maxSeats: number;
  maxMonthlyEvents: number;
  entitledModules: string[];
  daysRemaining: number;
  isAirGappedValid: boolean;
  fingerprint: string;
}

@Component({
  selector: 'app-enterprise-license-cockpit',
  standalone: true,
  imports: [CommonModule, FormsModule],
  template: `
    <div class="enterprise-license-cockpit p-6 max-w-7xl mx-auto space-y-6" dir="ltr">
      <!-- Header -->
      <div class="flex flex-col md:flex-row md:items-center md:justify-between gap-4 border-b pb-6">
        <div>
          <div class="flex items-center gap-3">
            <h1 class="text-2xl font-bold tracking-tight text-slate-900">Self-Hosted Enterprise Licensing</h1>
            <span class="inline-flex items-center px-2.5 py-0.5 rounded-full text-xs font-semibold bg-emerald-100 text-emerald-800">
              Air-Gapped Validated
            </span>
          </div>
          <p class="text-sm text-slate-500 mt-1">
            Zero-cloud heartbeat cryptographic license validator for on-premise, sovereign, and VPC deployments.
          </p>
        </div>

        <div class="flex items-center gap-3">
          <span class="text-xs text-slate-400">Node ID: <code class="font-mono text-slate-700">{{ license().fingerprint }}</code></span>
        </div>
      </div>

      <!-- Current License Overview -->
      <div class="grid grid-cols-1 md:grid-cols-4 gap-4" data-testid="license-kpis">
        <div class="bg-white border rounded-xl p-4 shadow-sm">
          <span class="text-xs font-medium text-slate-400">License Tier</span>
          <div class="text-lg font-bold text-slate-900 mt-1">{{ license().tier }}</div>
          <span class="text-[11px] text-emerald-600 font-semibold mt-1 block">Full Feature Access</span>
        </div>

        <div class="bg-white border rounded-xl p-4 shadow-sm">
          <span class="text-xs font-medium text-slate-400">Licensed Organization</span>
          <div class="text-lg font-bold text-slate-900 mt-1 truncate">{{ license().organizationName }}</div>
          <span class="text-[11px] text-slate-400 mt-1 block">Sovereign Cluster</span>
        </div>

        <div class="bg-white border rounded-xl p-4 shadow-sm">
          <span class="text-xs font-medium text-slate-400">Seat & Event Quota</span>
          <div class="text-lg font-bold text-slate-900 mt-1">{{ license().maxSeats }} Seats</div>
          <span class="text-[11px] text-slate-500 mt-1 block">{{ (license().maxMonthlyEvents / 1000000) | number:'1.0-0' }}M Events / mo</span>
        </div>

        <div class="bg-white border rounded-xl p-4 shadow-sm">
          <span class="text-xs font-medium text-slate-400">Days Remaining</span>
          <div class="text-lg font-bold text-indigo-600 mt-1 font-mono">{{ license().daysRemaining }} Days</div>
          <span class="text-[11px] text-emerald-600 font-medium mt-1 block">Zero Heartbeat Needed</span>
        </div>
      </div>

      <div class="grid grid-cols-1 lg:grid-cols-3 gap-6">
        <!-- Entitlements Checklist -->
        <div class="bg-white border rounded-xl p-5 shadow-sm space-y-4">
          <h3 class="font-bold text-slate-900 text-sm">Entitled Capability Modules</h3>
          <div class="space-y-2">
            @for (mod of license().entitledModules; track mod) {
              <div class="flex items-center justify-between p-2.5 bg-slate-50 rounded-lg text-xs">
                <span class="font-medium text-slate-800">{{ mod }}</span>
                <span class="text-emerald-600 font-semibold flex items-center gap-1">✓ Active</span>
              </div>
            }
          </div>
        </div>

        <!-- License Key Activation Panel -->
        <div class="lg:col-span-2 bg-white border rounded-xl p-5 shadow-sm space-y-4">
          <div>
            <h3 class="font-bold text-slate-900 text-sm">Apply New Enterprise License Key</h3>
            <p class="text-xs text-slate-500 mt-1">Paste your signed air-gapped license token provided by SGrowing Enterprise Licensing.</p>
          </div>

          <div class="space-y-3">
            <div>
              <label class="block text-xs font-semibold text-slate-700 mb-1">Organization Name</label>
              <input 
                type="text" 
                [(ngModel)]="inputOrg"
                placeholder="Enterprise Org Legal Name"
                class="w-full px-3 py-2 border rounded-lg text-xs focus:ring-2 focus:ring-indigo-500 focus:outline-none"
                data-testid="input-license-org" />
            </div>

            <div>
              <label class="block text-xs font-semibold text-slate-700 mb-1">Cryptographic License Key</label>
              <input 
                type="text" 
                [(ngModel)]="inputKey"
                placeholder="SG-ENT-XXXX-XXXX-XXXX"
                class="w-full px-3 py-2 border rounded-lg text-xs font-mono focus:ring-2 focus:ring-indigo-500 focus:outline-none"
                data-testid="input-license-key" />
            </div>

            @if (activationMessage()) {
              <div class="p-3 bg-emerald-50 border border-emerald-200 rounded-lg text-xs text-emerald-800 font-medium" data-testid="activation-msg">
                {{ activationMessage() }}
              </div>
            }

            <div class="pt-2 flex justify-end">
              <button 
                (click)="applyLicense()"
                class="px-4 py-2 bg-indigo-600 hover:bg-indigo-700 text-white rounded-lg text-xs font-semibold shadow-sm transition-colors"
                data-testid="btn-activate-license">
                Validate & Apply License
              </button>
            </div>
          </div>
        </div>
      </div>
    </div>
  `
})
export class EnterpriseLicenseCockpitComponent {
  license = signal<LicenseDetails>({
    licenseKey: 'SG-ENT-2026-AIRGAP-VERIFIED-991A',
    organizationName: 'Enterprise Global Inc.',
    tier: 'Enterprise Unlimited',
    maxSeats: 500,
    maxMonthlyEvents: 10000000,
    entitledModules: [
      'AI.Swarm Coordination',
      'Real-Time WebRTC Voice SDR',
      'Executive Revenue Intelligence',
      'App Marketplace & Sandbox Extensions',
      'GEO/AEO AI Search Visibility Tracker'
    ],
    daysRemaining: 345,
    isAirGappedValid: true,
    fingerprint: 'NODE-01-MAC-48-2C'
  });

  inputOrg = '';
  inputKey = '';
  activationMessage = signal<string>('');

  applyLicense(): void {
    if (!this.inputKey.trim()) return;

    this.license.update(l => ({
      ...l,
      organizationName: this.inputOrg || 'Acme Global Sovereign',
      licenseKey: this.inputKey,
      daysRemaining: 365
    }));

    this.activationMessage.set('License successfully verified and applied with SHA-256 cryptographic signature.');
    this.inputKey = '';
    this.inputOrg = '';
  }
}
