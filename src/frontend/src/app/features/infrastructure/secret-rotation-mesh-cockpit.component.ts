import { Component, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';

export interface ManagedSecretProfile {
  secretId: string;
  secretName: string;
  targetServiceType: string;
  rotationCadenceDays: number;
  multiCloudHsmSyncStatus: string;
}

export interface EphemeralCertRecord {
  certSerialHex: string;
  subjectDistinguishedName: string;
  targetEdgeNode: string;
  validDurationMinutes: number;
  status: string;
}

@Component({
  selector: 'app-secret-rotation-mesh-cockpit',
  standalone: true,
  imports: [CommonModule, FormsModule],
  template: `
    <div dir="ltr" data-testid="secret-rotation-mesh-cockpit" class="p-6 bg-slate-900 text-slate-100 min-h-screen">
      <header class="mb-8 border-b border-slate-800 pb-4 flex justify-between items-center">
        <div>
          <h1 class="text-2xl font-bold tracking-tight text-white flex items-center gap-2">
            <span class="inline-block w-3 h-3 rounded-full bg-teal-500 animate-pulse"></span>
            Multi-Cloud Secret Rotation & Ephemeral mTLS Mesh
          </h1>
          <p class="text-slate-400 text-sm mt-1">
            Zero-Downtime Dual-Credential Credential Swapping & 15-Minute Short-Lived X.509 Sovereign mTLS CA
          </p>
        </div>
        <div class="flex gap-3">
          <button (click)="issueEphemeralCert()" class="px-4 py-2 bg-teal-600 hover:bg-teal-500 text-white font-medium rounded-lg shadow-sm transition">
            Issue 15-Min mTLS Cert
          </button>
        </div>
      </header>

      <!-- KPI Metrics Grid -->
      <section class="grid grid-cols-1 md:grid-cols-4 gap-4 mb-8">
        <div class="bg-slate-800/60 border border-slate-700/50 p-4 rounded-xl">
          <span class="text-xs font-semibold text-slate-400 uppercase tracking-wider">Managed Secrets</span>
          <p class="text-2xl font-extrabold text-white mt-1">{{ secrets().length }}</p>
          <span class="text-xs text-teal-400">AWS / GCP / Azure HSM Synced</span>
        </div>
        <div class="bg-slate-800/60 border border-slate-700/50 p-4 rounded-xl">
          <span class="text-xs font-semibold text-slate-400 uppercase tracking-wider">Ephemeral Certs</span>
          <p class="text-2xl font-extrabold text-indigo-400 mt-1">{{ certs().length }}</p>
          <span class="text-xs text-slate-400">15-Min TTL Zero-Trust</span>
        </div>
        <div class="bg-slate-800/60 border border-slate-700/50 p-4 rounded-xl">
          <span class="text-xs font-semibold text-slate-400 uppercase tracking-wider">30-Day Rotations</span>
          <p class="text-2xl font-extrabold text-emerald-400 mt-1">128</p>
          <span class="text-xs text-slate-400">Automated Pipeline</span>
        </div>
        <div class="bg-slate-800/60 border border-slate-700/50 p-4 rounded-xl">
          <span class="text-xs font-semibold text-slate-400 uppercase tracking-wider">Zero-Downtime SLA</span>
          <p class="text-2xl font-extrabold text-cyan-400 mt-1">100.0%</p>
          <span class="text-xs text-emerald-400">Dual-Credential Overlap</span>
        </div>
      </section>

      <!-- Main Panels -->
      <div class="grid grid-cols-1 lg:grid-cols-3 gap-6">
        <!-- Managed Secrets Table -->
        <section class="lg:col-span-2 bg-slate-800/40 border border-slate-700/50 rounded-xl p-5">
          <h2 class="text-lg font-semibold text-white mb-4">Enterprise Managed Secrets</h2>
          <div class="overflow-x-auto">
            <table class="w-full text-left border-collapse text-sm">
              <thead>
                <tr class="border-b border-slate-700 text-slate-400 text-xs">
                  <th class="pb-3 font-medium">Secret ID</th>
                  <th class="pb-3 font-medium">Secret Name</th>
                  <th class="pb-3 font-medium">Target Service</th>
                  <th class="pb-3 font-medium">Cadence</th>
                  <th class="pb-3 font-medium">HSM Sync Status</th>
                </tr>
              </thead>
              <tbody class="divide-y divide-slate-800">
                <tr *ngFor="let s of secrets()" class="hover:bg-slate-800/50 transition">
                  <td class="py-3 font-mono text-xs text-teal-300 font-semibold">{{ s.secretId }}</td>
                  <td class="py-3 font-medium text-slate-200">{{ s.secretName }}</td>
                  <td class="py-3 text-slate-400 text-xs">{{ s.targetServiceType }}</td>
                  <td class="py-3 font-mono text-indigo-400 text-xs">Every {{ s.rotationCadenceDays }}d</td>
                  <td class="py-3">
                    <span class="px-2 py-0.5 text-[10px] rounded font-semibold font-mono bg-emerald-500/20 text-emerald-300">
                      {{ s.multiCloudHsmSyncStatus }}
                    </span>
                  </td>
                </tr>
              </tbody>
            </table>
          </div>
        </section>

        <!-- Ephemeral Certs List -->
        <section class="bg-slate-800/40 border border-slate-700/50 rounded-xl p-5">
          <h2 class="text-lg font-semibold text-white mb-4">Active Ephemeral mTLS Certs</h2>
          <div class="space-y-3">
            <div *ngFor="let c of certs()" class="p-3 bg-slate-900/60 border border-slate-700/40 rounded-lg">
              <div class="flex justify-between items-center mb-1">
                <span class="font-mono text-xs text-teal-400 font-bold">{{ c.certSerialHex }}</span>
                <span class="px-2 py-0.5 text-xs bg-emerald-500/20 text-emerald-300 rounded font-semibold font-mono">{{ c.validDurationMinutes }}m TTL</span>
              </div>
              <p class="text-xs text-slate-300 font-mono mb-1 truncate">{{ c.subjectDistinguishedName }}</p>
              <div class="text-[10px] font-mono text-slate-400 bg-slate-950 p-1.5 rounded">
                Target Node: {{ c.targetEdgeNode }} | Status: {{ c.status }}
              </div>
            </div>
          </div>
        </section>
      </div>
    </div>
  `
})
export class SecretRotationMeshCockpitComponent {
  secrets = signal<ManagedSecretProfile[]>([
    { secretId: 'SEC-DB-PG-MASTER', secretName: 'PostgreSQL 17 Aurora Multi-Region Cluster Superuser Key', targetServiceType: 'PostgreSQL Replica', rotationCadenceDays: 30, multiCloudHsmSyncStatus: 'SYNCED_AWS_GCP_AZURE_HSM' },
    { secretId: 'SEC-API-STRIPE-LIVE', secretName: 'Stripe Global Payment Gateway Restricted API Key', targetServiceType: 'Stripe Webhook Key', rotationCadenceDays: 60, multiCloudHsmSyncStatus: 'SYNCED_AWS_GCP_AZURE_HSM' }
  ]);

  certs = signal<EphemeralCertRecord[]>([
    { certSerialHex: '0x7F998811AA22', subjectDistinguishedName: 'CN=edge-proxy-fra-01.mesh.sgrowing.io', targetEdgeNode: 'EDGE-POP-FRA-01', validDurationMinutes: 15, status: 'CERT_ACTIVE_VALID' }
  ]);

  issueEphemeralCert() {
    const newCert: EphemeralCertRecord = {
      certSerialHex: `0x${Date.now().toString(16).toUpperCase()}`,
      subjectDistinguishedName: 'CN=edge-proxy-iad-02.mesh.sgrowing.io',
      targetEdgeNode: 'EDGE-POP-IAD-02',
      validDurationMinutes: 15,
      status: 'CERT_ACTIVE_VALID'
    };
    this.certs.update(list => [newCert, ...list]);
  }
}
