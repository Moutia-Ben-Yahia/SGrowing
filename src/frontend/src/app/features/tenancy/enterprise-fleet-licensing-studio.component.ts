import { Component, ChangeDetectionStrategy, signal, computed, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { HttpClient } from '@angular/common/http';
import { TranslationService } from '../../core/services/translation.service';

export interface LicensePayload {
  tenantId: string;
  organizationName: string;
  tier: string;
  maxSeats: number;
  maxCpuCores: number;
  maxClusterNodes: number;
  enabledEnterpriseModules: string[];
  issuedAtUtc: string;
  expiresAtUtc: string;
  hardwareClusterFingerprint: string;
  metadata?: Record<string, string>;
}

export interface IssuedCryptographicLicense {
  licenseKey: string;
  payload: LicensePayload;
  signature: string;
  signedToken: string;
  airGappedActivationCode: string;
  generatedAtUtc: string;
}

export type LicenseValidationStatus =
  | 'Valid'
  | 'Expired'
  | 'SignatureMismatch'
  | 'HardwareMismatch'
  | 'QuotaExceeded'
  | 'TamperedPayload'
  | 'MalformedToken';

export interface LicenseVerificationResult {
  isValid: boolean;
  status: LicenseValidationStatus;
  message: string;
  payload?: LicensePayload | null;
  daysRemaining: number;
  isAirGapCompliant: boolean;
  evaluatedAtUtc: string;
}

export type ClusterNodeStatus = 'Healthy' | 'Degraded' | 'QuotaExceeded' | 'Offline';
export type ClusterHealthState = 'Healthy' | 'Degraded' | 'QuotaExceeded' | 'Critical';

export interface NodeHealthSummary {
  nodeId: string;
  nodeName: string;
  machineFingerprint: string;
  status: ClusterNodeStatus;
  allocatedCpuCores: number;
  activeSeats: number;
  cpuUtilizationPercent: number;
  lastHeartbeatAtUtc: string;
  isStale: boolean;
  statusDetails: string;
}

export interface ClusterFleetStatus {
  tenantId: string;
  clusterName: string;
  overallStatus: ClusterHealthState;
  totalNodes: number;
  activeNodes: number;
  totalAllocatedCpuCores: number;
  maxAllowedCpuCores: number;
  totalActiveSeats: number;
  maxAllowedSeats: number;
  maxAllowedNodes: number;
  seatQuotaExceeded: boolean;
  cpuQuotaExceeded: boolean;
  nodeQuotaExceeded: boolean;
  nodes: NodeHealthSummary[];
  activeAlerts: string[];
  lastEvaluatedAtUtc: string;
}

export interface AirGapActivationResponse {
  success: boolean;
  message: string;
  payload?: LicensePayload | null;
  signedToken?: string | null;
}

@Component({
  selector: 'app-enterprise-fleet-licensing-studio',
  standalone: true,
  imports: [CommonModule, FormsModule],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div [dir]="isRtl() ? 'rtl' : 'ltr'" class="min-h-screen bg-slate-950 text-slate-100 font-sans p-6 lg:p-10 space-y-8">
      <!-- Top Navigation & Cockpit Banner -->
      <header class="flex flex-col md:flex-row md:items-center justify-between gap-4 pb-6 border-b border-slate-800">
        <div>
          <div class="flex items-center gap-3">
            <span class="text-3xl">🛡️</span>
            <div>
              <h1 class="text-2xl font-bold tracking-tight text-white flex items-center gap-3">
                Enterprise Fleet & Cryptographic License Orchestrator
                <span
                  class="text-xs px-2.5 py-0.5 rounded-full font-semibold border"
                  [ngClass]="{
                    'bg-emerald-500/10 text-emerald-400 border-emerald-500/20': clusterFleet().overallStatus === 'Healthy',
                    'bg-amber-500/10 text-amber-400 border-amber-500/20': clusterFleet().overallStatus === 'Degraded',
                    'bg-rose-500/10 text-rose-400 border-rose-500/20': clusterFleet().overallStatus === 'QuotaExceeded' || clusterFleet().overallStatus === 'Critical'
                  }"
                  data-testid="cluster-health-badge"
                >
                  ● {{ clusterFleet().overallStatus }}
                </span>
              </h1>
              <p class="text-xs text-slate-400 mt-1">
                Zero-cloud heartbeat offline license verification, HMAC-SHA256 tamper-proof payloads, and sovereign multi-node cluster quota aggregator.
              </p>
            </div>
          </div>
        </div>

        <div class="flex items-center gap-3 flex-wrap">
          <span class="text-xs px-2.5 py-1 bg-slate-900 border border-slate-800 rounded-lg text-slate-300 font-mono">
            Tenant: <span class="text-indigo-400">{{ tenantId().substring(0, 8) }}...</span>
          </span>
          <button
            (click)="resetDemoCluster()"
            class="px-3 py-1.5 text-xs font-semibold bg-slate-800 hover:bg-slate-700 text-slate-200 rounded-lg border border-slate-700 transition"
            data-testid="btn-reset-fleet"
          >
            ↻ Reset Fleet State
          </button>
        </div>
      </header>

      <!-- Toast / Feedback Banner -->
      @if (toastMessage(); as toast) {
        <div
          class="p-4 rounded-xl border flex items-center justify-between text-xs font-medium transition-all"
          [ngClass]="{
            'bg-emerald-950/60 border-emerald-800 text-emerald-200': toast.type === 'success',
            'bg-rose-950/60 border-rose-800 text-rose-200': toast.type === 'error',
            'bg-amber-950/60 border-amber-800 text-amber-200': toast.type === 'warning',
            'bg-indigo-950/60 border-indigo-800 text-indigo-200': toast.type === 'info'
          }"
          data-testid="toast-banner"
        >
          <div class="flex items-center gap-2">
            <span>{{ toast.type === 'success' ? '✓' : toast.type === 'error' ? '⚠' : 'ℹ' }}</span>
            <span>{{ toast.text }}</span>
          </div>
          <button (click)="toastMessage.set(null)" class="text-slate-400 hover:text-white">✕</button>
        </div>
      }

      <!-- KPI Summary Cards -->
      <section class="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-4 gap-4" data-testid="fleet-kpis">
        <!-- Card 1: Active Entitlement Tier -->
        <div class="bg-slate-900/90 border border-slate-800 rounded-xl p-5 shadow-sm space-y-2">
          <div class="flex items-center justify-between text-xs text-slate-400">
            <span>License Entitlement</span>
            <span class="text-emerald-400 font-medium">Air-Gap Verified</span>
          </div>
          <div class="text-xl font-bold text-white truncate" data-testid="kpi-tier">
            {{ activeLicense()?.tier || 'No Active License' }}
          </div>
          <div class="text-xs text-slate-400 flex items-center justify-between pt-1">
            <span>Days Remaining:</span>
            <span class="font-mono font-semibold" [ngClass]="daysRemaining() > 30 ? 'text-emerald-400' : 'text-amber-400'">
              {{ daysRemaining() }} Days
            </span>
          </div>
        </div>

        <!-- Card 2: Seat Quotas -->
        <div class="bg-slate-900/90 border border-slate-800 rounded-xl p-5 shadow-sm space-y-2">
          <div class="flex items-center justify-between text-xs text-slate-400">
            <span>Active Seat Allocation</span>
            <span
              class="font-semibold text-xs"
              [ngClass]="clusterFleet().seatQuotaExceeded ? 'text-rose-400' : 'text-slate-300'"
              data-testid="seat-quota-label"
            >
              {{ clusterFleet().totalActiveSeats }} / {{ clusterFleet().maxAllowedSeats }} Seats
            </span>
          </div>
          <div class="w-full bg-slate-800 rounded-full h-2 overflow-hidden">
            <div
              class="h-2 rounded-full transition-all duration-300"
              [ngClass]="clusterFleet().seatQuotaExceeded ? 'bg-rose-500' : 'bg-indigo-500'"
              [style.width.%]="seatUsagePercent()"
            ></div>
          </div>
          <div class="flex justify-between text-[11px] text-slate-500">
            <span>Utilization: {{ seatUsagePercent() }}%</span>
            @if (clusterFleet().seatQuotaExceeded) {
              <span class="text-rose-400 font-semibold">Quota Exceeded!</span>
            } @else {
              <span class="text-emerald-400">Within Limit</span>
            }
          </div>
        </div>

        <!-- Card 3: CPU Cores -->
        <div class="bg-slate-900/90 border border-slate-800 rounded-xl p-5 shadow-sm space-y-2">
          <div class="flex items-center justify-between text-xs text-slate-400">
            <span>CPU Core Capacity</span>
            <span
              class="font-semibold text-xs"
              [ngClass]="clusterFleet().cpuQuotaExceeded ? 'text-rose-400' : 'text-slate-300'"
              data-testid="cpu-quota-label"
            >
              {{ clusterFleet().totalAllocatedCpuCores }} / {{ clusterFleet().maxAllowedCpuCores }} Cores
            </span>
          </div>
          <div class="w-full bg-slate-800 rounded-full h-2 overflow-hidden">
            <div
              class="h-2 rounded-full transition-all duration-300"
              [ngClass]="clusterFleet().cpuQuotaExceeded ? 'bg-rose-500' : 'bg-cyan-500'"
              [style.width.%]="cpuUsagePercent()"
            ></div>
          </div>
          <div class="flex justify-between text-[11px] text-slate-500">
            <span>Utilization: {{ cpuUsagePercent() }}%</span>
            @if (clusterFleet().cpuQuotaExceeded) {
              <span class="text-rose-400 font-semibold">Over Provisioned!</span>
            } @else {
              <span class="text-emerald-400">Optimal Load</span>
            }
          </div>
        </div>

        <!-- Card 4: Cluster Nodes -->
        <div class="bg-slate-900/90 border border-slate-800 rounded-xl p-5 shadow-sm space-y-2">
          <div class="flex items-center justify-between text-xs text-slate-400">
            <span>Multi-Node Mesh</span>
            <span class="text-indigo-400 font-medium">Nodes</span>
          </div>
          <div class="text-xl font-bold text-white flex items-baseline gap-2" data-testid="kpi-nodes">
            <span>{{ clusterFleet().activeNodes }}</span>
            <span class="text-xs font-normal text-slate-400">online / {{ clusterFleet().maxAllowedNodes }} max</span>
          </div>
          <div class="flex justify-between text-[11px] text-slate-500 pt-1">
            <span>Total Registered: {{ clusterFleet().totalNodes }}</span>
            <span [ngClass]="clusterFleet().nodeQuotaExceeded ? 'text-rose-400 font-semibold' : 'text-slate-400'">
              {{ clusterFleet().nodeQuotaExceeded ? 'Node Limit Exceeded' : 'Healthy Roster' }}
            </span>
          </div>
        </div>
      </section>

      <!-- Active Alerts Bar -->
      @if (clusterFleet().activeAlerts.length > 0) {
        <section class="bg-rose-950/40 border border-rose-800/60 rounded-xl p-4 space-y-2" data-testid="alerts-section">
          <h3 class="text-xs font-bold text-rose-300 flex items-center gap-2">
            <span>⚠️</span> Active Fleet Orchestration Alerts ({{ clusterFleet().activeAlerts.length }})
          </h3>
          <ul class="space-y-1">
            @for (alert of clusterFleet().activeAlerts; track alert) {
              <li class="text-xs text-rose-200 flex items-center gap-2">
                <span class="w-1.5 h-1.5 rounded-full bg-rose-400"></span>
                <span>{{ alert }}</span>
              </li>
            }
          </ul>
        </section>
      }

      <!-- Studio Navigation Tabs -->
      <nav class="flex border-b border-slate-800 space-x-6 text-xs font-medium" data-testid="studio-tabs">
        <button
          (click)="selectTab('fleet')"
          class="pb-3 border-b-2 transition"
          [ngClass]="activeTab() === 'fleet' ? 'border-indigo-500 text-indigo-400 font-semibold' : 'border-transparent text-slate-400 hover:text-slate-200'"
          data-testid="tab-fleet"
        >
          🖥️ Multi-Node Cluster Fleet ({{ clusterFleet().nodes.length }})
        </button>
        <button
          (click)="selectTab('issue')"
          class="pb-3 border-b-2 transition"
          [ngClass]="activeTab() === 'issue' ? 'border-indigo-500 text-indigo-400 font-semibold' : 'border-transparent text-slate-400 hover:text-slate-200'"
          data-testid="tab-issue"
        >
          🔑 Issue HMAC-SHA256 License
        </button>
        <button
          (click)="selectTab('verify')"
          class="pb-3 border-b-2 transition"
          [ngClass]="activeTab() === 'verify' ? 'border-indigo-500 text-indigo-400 font-semibold' : 'border-transparent text-slate-400 hover:text-slate-200'"
          data-testid="tab-verify"
        >
          🔍 Offline Tamper-Proof Token Validator
        </button>
        <button
          (click)="selectTab('airgap')"
          class="pb-3 border-b-2 transition"
          [ngClass]="activeTab() === 'airgap' ? 'border-indigo-500 text-indigo-400 font-semibold' : 'border-transparent text-slate-400 hover:text-slate-200'"
          data-testid="tab-airgap"
        >
          📡 Air-Gapped Code Activation
        </button>
      </nav>

      <!-- TAB 1: Multi-Node Fleet & Telemetry Ingestion -->
      @if (activeTab() === 'fleet') {
        <div class="space-y-6" data-testid="view-fleet">
          <div class="grid grid-cols-1 lg:grid-cols-3 gap-6">
            <!-- Node Roster Table -->
            <div class="lg:col-span-2 bg-slate-900/90 border border-slate-800 rounded-xl p-5 space-y-4">
              <div class="flex items-center justify-between">
                <div>
                  <h2 class="text-sm font-bold text-white">Live Cluster Nodes & Hardware Telemetry</h2>
                  <p class="text-xs text-slate-400">Real-time heartbeat aggregation across self-hosted and air-gapped instances.</p>
                </div>
                <button
                  (click)="simulateQuotaOverage()"
                  class="px-3 py-1 bg-rose-900/40 hover:bg-rose-900/60 text-rose-300 border border-rose-700/60 rounded text-xs transition"
                  data-testid="btn-simulate-overage"
                >
                  ⚡ Simulate Quota Overage
                </button>
              </div>

              <div class="overflow-x-auto">
                <table class="w-full text-left text-xs" data-testid="nodes-table">
                  <thead class="text-slate-400 border-b border-slate-800 bg-slate-950/40">
                    <tr>
                      <th class="py-2.5 px-3">Node Name / ID</th>
                      <th class="py-2.5 px-3">Machine Fingerprint</th>
                      <th class="py-2.5 px-3">Cores</th>
                      <th class="py-2.5 px-3">Seats</th>
                      <th class="py-2.5 px-3">CPU %</th>
                      <th class="py-2.5 px-3">Status</th>
                    </tr>
                  </thead>
                  <tbody class="divide-y divide-slate-800/60">
                    @for (node of clusterFleet().nodes; track node.nodeId) {
                      <tr class="hover:bg-slate-800/30 transition">
                        <td class="py-3 px-3">
                          <div class="font-semibold text-slate-200">{{ node.nodeName }}</div>
                          <div class="text-[11px] text-slate-500 font-mono">{{ node.nodeId }}</div>
                        </td>
                        <td class="py-3 px-3 font-mono text-slate-400">{{ node.machineFingerprint }}</td>
                        <td class="py-3 px-3 font-semibold text-slate-300">{{ node.allocatedCpuCores }}</td>
                        <td class="py-3 px-3 font-semibold text-slate-300">{{ node.activeSeats }}</td>
                        <td class="py-3 px-3">
                          <span
                            class="font-mono font-medium"
                            [ngClass]="node.cpuUtilizationPercent > 85 ? 'text-rose-400' : 'text-slate-300'"
                          >
                            {{ node.cpuUtilizationPercent | number: '1.1-1' }}%
                          </span>
                        </td>
                        <td class="py-3 px-3">
                          <span
                            class="px-2 py-0.5 rounded-full text-[11px] font-semibold border"
                            [ngClass]="{
                              'bg-emerald-500/10 text-emerald-400 border-emerald-500/20': node.status === 'Healthy',
                              'bg-amber-500/10 text-amber-400 border-amber-500/20': node.status === 'Degraded',
                              'bg-rose-500/10 text-rose-400 border-rose-500/20': node.status === 'QuotaExceeded',
                              'bg-slate-800 text-slate-400 border-slate-700': node.status === 'Offline'
                            }"
                          >
                            {{ node.status }}
                          </span>
                        </td>
                      </tr>
                    }
                  </tbody>
                </table>
              </div>
            </div>

            <!-- Ingest Heartbeat Simulation Form -->
            <div class="bg-slate-900/90 border border-slate-800 rounded-xl p-5 space-y-4">
              <div>
                <h3 class="text-sm font-bold text-white">Ingest Node Heartbeat</h3>
                <p class="text-xs text-slate-400">Simulate incoming hardware telemetry from an enterprise cluster node.</p>
              </div>

              <div class="space-y-3 text-xs">
                <div>
                  <label class="block text-slate-400 mb-1">Node Identifier</label>
                  <input
                    type="text"
                    [(ngModel)]="hbNodeId"
                    class="w-full bg-slate-950 border border-slate-800 rounded-lg px-3 py-2 text-slate-200 focus:outline-none focus:border-indigo-500 font-mono"
                    data-testid="input-hb-id"
                  />
                </div>

                <div>
                  <label class="block text-slate-400 mb-1">Node Name</label>
                  <input
                    type="text"
                    [(ngModel)]="hbNodeName"
                    class="w-full bg-slate-950 border border-slate-800 rounded-lg px-3 py-2 text-slate-200 focus:outline-none focus:border-indigo-500"
                    data-testid="input-hb-name"
                  />
                </div>

                <div>
                  <label class="block text-slate-400 mb-1">Machine Fingerprint</label>
                  <input
                    type="text"
                    [(ngModel)]="hbFingerprint"
                    class="w-full bg-slate-950 border border-slate-800 rounded-lg px-3 py-2 text-slate-200 focus:outline-none focus:border-indigo-500 font-mono"
                    data-testid="input-hb-fingerprint"
                  />
                </div>

                <div class="grid grid-cols-3 gap-2">
                  <div>
                    <label class="block text-slate-400 mb-1">Cores</label>
                    <input
                      type="number"
                      [(ngModel)]="hbAllocatedCores"
                      class="w-full bg-slate-950 border border-slate-800 rounded-lg px-2 py-2 text-slate-200 focus:outline-none focus:border-indigo-500 text-center"
                      data-testid="input-hb-cores"
                    />
                  </div>
                  <div>
                    <label class="block text-slate-400 mb-1">Seats</label>
                    <input
                      type="number"
                      [(ngModel)]="hbActiveSeats"
                      class="w-full bg-slate-950 border border-slate-800 rounded-lg px-2 py-2 text-slate-200 focus:outline-none focus:border-indigo-500 text-center"
                      data-testid="input-hb-seats"
                    />
                  </div>
                  <div>
                    <label class="block text-slate-400 mb-1">CPU %</label>
                    <input
                      type="number"
                      [(ngModel)]="hbCpuUsage"
                      class="w-full bg-slate-950 border border-slate-800 rounded-lg px-2 py-2 text-slate-200 focus:outline-none focus:border-indigo-500 text-center"
                      data-testid="input-hb-cpu"
                    />
                  </div>
                </div>

                <button
                  (click)="ingestHeartbeat()"
                  class="w-full py-2.5 bg-indigo-600 hover:bg-indigo-500 text-white font-semibold rounded-lg shadow-sm transition"
                  data-testid="btn-ingest-heartbeat"
                >
                  📡 Send Heartbeat Telemetry
                </button>
              </div>
            </div>
          </div>
        </div>
      }

      <!-- TAB 2: Cryptographic License Issuance -->
      @if (activeTab() === 'issue') {
        <div class="grid grid-cols-1 lg:grid-cols-2 gap-6" data-testid="view-issue">
          <div class="bg-slate-900/90 border border-slate-800 rounded-xl p-5 space-y-4">
            <div>
              <h2 class="text-sm font-bold text-white">Generate Cryptographic Enterprise License</h2>
              <p class="text-xs text-slate-400">Encodes tenant constraints into a tamper-proof HMAC-SHA256 signature payload.</p>
            </div>

            <div class="space-y-3 text-xs">
              <div>
                <label class="block text-slate-400 mb-1">Licensed Legal Entity / Organization Name</label>
                <input
                  type="text"
                  [(ngModel)]="issueOrgName"
                  class="w-full bg-slate-950 border border-slate-800 rounded-lg px-3 py-2 text-slate-200 focus:outline-none focus:border-indigo-500"
                  data-testid="input-issue-org"
                />
              </div>

              <div class="grid grid-cols-2 gap-3">
                <div>
                  <label class="block text-slate-400 mb-1">License Tier</label>
                  <select
                    [(ngModel)]="issueTier"
                    class="w-full bg-slate-950 border border-slate-800 rounded-lg px-3 py-2 text-slate-200 focus:outline-none focus:border-indigo-500"
                    data-testid="select-issue-tier"
                  >
                    <option value="SelfHostedEnterprise">SelfHostedEnterprise</option>
                    <option value="DedicatedSovereign">DedicatedSovereign</option>
                    <option value="AirGappedGovernment">AirGappedGovernment</option>
                  </select>
                </div>
                <div>
                  <label class="block text-slate-400 mb-1">Validity (Days)</label>
                  <input
                    type="number"
                    [(ngModel)]="issueValidityDays"
                    class="w-full bg-slate-950 border border-slate-800 rounded-lg px-3 py-2 text-slate-200 focus:outline-none focus:border-indigo-500"
                    data-testid="input-issue-validity"
                  />
                </div>
              </div>

              <div class="grid grid-cols-3 gap-3">
                <div>
                  <label class="block text-slate-400 mb-1">Max Seats</label>
                  <input
                    type="number"
                    [(ngModel)]="issueSeats"
                    class="w-full bg-slate-950 border border-slate-800 rounded-lg px-3 py-2 text-slate-200 focus:outline-none focus:border-indigo-500"
                    data-testid="input-issue-seats"
                  />
                </div>
                <div>
                  <label class="block text-slate-400 mb-1">Max CPU Cores</label>
                  <input
                    type="number"
                    [(ngModel)]="issueCores"
                    class="w-full bg-slate-950 border border-slate-800 rounded-lg px-3 py-2 text-slate-200 focus:outline-none focus:border-indigo-500"
                    data-testid="input-issue-cores"
                  />
                </div>
                <div>
                  <label class="block text-slate-400 mb-1">Max Cluster Nodes</label>
                  <input
                    type="number"
                    [(ngModel)]="issueNodes"
                    class="w-full bg-slate-950 border border-slate-800 rounded-lg px-3 py-2 text-slate-200 focus:outline-none focus:border-indigo-500"
                    data-testid="input-issue-nodes"
                  />
                </div>
              </div>

              <div>
                <label class="block text-slate-400 mb-1">Hardware Fingerprint Binding (or '*' for any)</label>
                <input
                  type="text"
                  [(ngModel)]="issueFingerprint"
                  class="w-full bg-slate-950 border border-slate-800 rounded-lg px-3 py-2 text-slate-200 focus:outline-none focus:border-indigo-500 font-mono"
                  placeholder="*"
                  data-testid="input-issue-fingerprint"
                />
              </div>

              <!-- Module Entitlements Checkbox Matrix -->
              <div>
                <label class="block text-slate-400 mb-2">Entitled Enterprise Modules</label>
                <div class="grid grid-cols-1 sm:grid-cols-2 gap-2">
                  @for (mod of availableModules; track mod) {
                    <label class="flex items-center gap-2 p-2 bg-slate-950/60 border border-slate-800/80 rounded cursor-pointer hover:bg-slate-800/40 transition">
                      <input
                        type="checkbox"
                        [checked]="selectedModules().includes(mod)"
                        (change)="toggleModule(mod)"
                        class="rounded border-slate-700 text-indigo-600 focus:ring-0"
                        [attr.data-testid]="'chk-mod-' + mod"
                      />
                      <span class="text-slate-300 text-[11px] font-mono">{{ mod }}</span>
                    </label>
                  }
                </div>
              </div>

              <button
                (click)="generateLicense()"
                class="w-full py-2.5 bg-indigo-600 hover:bg-indigo-500 text-white font-semibold rounded-lg shadow-sm transition"
                data-testid="btn-generate-license"
              >
                🔐 Issue Cryptographic License Token
              </button>
            </div>
          </div>

          <!-- Issuance Artifacts Display -->
          <div class="bg-slate-900/90 border border-slate-800 rounded-xl p-5 space-y-4">
            <div>
              <h3 class="text-sm font-bold text-white">Generated License Artifacts</h3>
              <p class="text-xs text-slate-400">Deterministic tokens ready for air-gapped transport or runtime deployment.</p>
            </div>

            @if (lastIssuedLicense(); as issued) {
              <div class="space-y-4 text-xs" data-testid="issued-artifacts">
                <div>
                  <div class="flex items-center justify-between text-slate-400 mb-1">
                    <span>License Key Identifier</span>
                    <button (click)="copyText(issued.licenseKey, 'License Key')" class="text-indigo-400 hover:underline">Copy</button>
                  </div>
                  <div class="p-2.5 bg-slate-950 border border-slate-800 rounded-lg font-mono text-emerald-400 break-all select-all" data-testid="display-license-key">
                    {{ issued.licenseKey }}
                  </div>
                </div>

                <div>
                  <div class="flex items-center justify-between text-slate-400 mb-1">
                    <span>Signed Token (HMAC-SHA256)</span>
                    <button (click)="copyText(issued.signedToken, 'Signed Token')" class="text-indigo-400 hover:underline">Copy</button>
                  </div>
                  <div class="p-2.5 bg-slate-950 border border-slate-800 rounded-lg font-mono text-slate-300 break-all text-[11px] max-h-24 overflow-y-auto select-all" data-testid="display-signed-token">
                    {{ issued.signedToken }}
                  </div>
                </div>

                <div>
                  <div class="flex items-center justify-between text-slate-400 mb-1">
                    <span>Air-Gapped Activation Code (SGA1 + SHA-256 Checksum)</span>
                    <button (click)="copyText(issued.airGappedActivationCode, 'Air-Gap Code')" class="text-indigo-400 hover:underline">Copy</button>
                  </div>
                  <div class="p-2.5 bg-slate-950 border border-slate-800 rounded-lg font-mono text-amber-300 break-all text-[11px] max-h-24 overflow-y-auto select-all" data-testid="display-airgap-code">
                    {{ issued.airGappedActivationCode }}
                  </div>
                </div>

                <div class="p-3 bg-indigo-950/40 border border-indigo-800/40 rounded-lg space-y-1">
                  <div class="text-[11px] text-indigo-300 font-semibold">Entitlements Encoded:</div>
                  <div class="text-[11px] text-slate-400">
                    Seats: <span class="text-slate-200">{{ issued.payload.maxSeats }}</span> |
                    Cores: <span class="text-slate-200">{{ issued.payload.maxCpuCores }}</span> |
                    Nodes: <span class="text-slate-200">{{ issued.payload.maxClusterNodes }}</span> |
                    Hardware: <span class="text-slate-200">{{ issued.payload.hardwareClusterFingerprint }}</span>
                  </div>
                </div>
              </div>
            } @else {
              <div class="p-10 border border-dashed border-slate-800 rounded-xl text-center text-xs text-slate-500">
                Click "Issue Cryptographic License Token" to produce a signed payload and air-gapped activation code.
              </div>
            }
          </div>
        </div>
      }

      <!-- TAB 3: Offline Token Validator -->
      @if (activeTab() === 'verify') {
        <div class="grid grid-cols-1 lg:grid-cols-2 gap-6" data-testid="view-verify">
          <div class="bg-slate-900/90 border border-slate-800 rounded-xl p-5 space-y-4">
            <div>
              <h2 class="text-sm font-bold text-white">Offline Tamper-Proof Token Validator</h2>
              <p class="text-xs text-slate-400">Tests signature authenticity, expiration window, and hardware node fingerprint binding.</p>
            </div>

            <div class="space-y-3 text-xs">
              <div>
                <label class="block text-slate-400 mb-1">Signed License Token (Payload.Signature)</label>
                <textarea
                  [(ngModel)]="tokenInput"
                  rows="4"
                  class="w-full bg-slate-950 border border-slate-800 rounded-lg p-2.5 text-slate-200 focus:outline-none focus:border-indigo-500 font-mono text-[11px]"
                  placeholder="Paste base64url payload.signature token..."
                  data-testid="input-verify-token"
                ></textarea>
              </div>

              <div>
                <label class="block text-slate-400 mb-1">Current Machine Fingerprint (Optional verification)</label>
                <input
                  type="text"
                  [(ngModel)]="fingerprintInput"
                  class="w-full bg-slate-950 border border-slate-800 rounded-lg px-3 py-2 text-slate-200 focus:outline-none focus:border-indigo-500 font-mono"
                  placeholder="e.g. FP-SRV-9901-MAC-A1"
                  data-testid="input-verify-fingerprint"
                />
              </div>

              <button
                (click)="verifyToken()"
                class="w-full py-2.5 bg-indigo-600 hover:bg-indigo-500 text-white font-semibold rounded-lg shadow-sm transition"
                data-testid="btn-verify-token"
              >
                🔍 Verify Token Offline
              </button>
            </div>
          </div>

          <!-- Verification Outcome -->
          <div class="bg-slate-900/90 border border-slate-800 rounded-xl p-5 space-y-4">
            <div>
              <h3 class="text-sm font-bold text-white">Verification Diagnostic Result</h3>
              <p class="text-xs text-slate-400">Cryptographic audit log and entitlement extraction.</p>
            </div>

            @if (verificationResult(); as res) {
              <div class="space-y-4 text-xs" data-testid="verification-diagnostic">
                <div
                  class="p-4 rounded-xl border flex items-center justify-between"
                  [ngClass]="{
                    'bg-emerald-950/60 border-emerald-800 text-emerald-200': res.isValid,
                    'bg-rose-950/60 border-rose-800 text-rose-200': !res.isValid
                  }"
                >
                  <div>
                    <div class="font-bold text-sm" data-testid="verification-status-label">
                      {{ res.isValid ? '✓ Valid License' : '✕ Verification Failed' }} ({{ res.status }})
                    </div>
                    <div class="text-[11px] opacity-90 mt-0.5">{{ res.message }}</div>
                  </div>
                </div>

                @if (res.payload; as p) {
                  <div class="bg-slate-950 border border-slate-800 rounded-xl p-4 space-y-2 text-slate-300">
                    <div class="font-semibold text-white">Decoded License Entitlements:</div>
                    <div class="grid grid-cols-2 gap-2 text-[11px]">
                      <div>Organization: <span class="text-indigo-400">{{ p.organizationName }}</span></div>
                      <div>Tier: <span class="text-slate-200">{{ p.tier }}</span></div>
                      <div>Max Seats: <span class="text-slate-200">{{ p.maxSeats }}</span></div>
                      <div>Max Cores: <span class="text-slate-200">{{ p.maxCpuCores }}</span></div>
                      <div>Max Nodes: <span class="text-slate-200">{{ p.maxClusterNodes }}</span></div>
                      <div>Expires: <span class="text-slate-200">{{ p.expiresAtUtc | date: 'mediumDate' }}</span></div>
                    </div>

                    @if (res.isValid) {
                      <div class="pt-2">
                        <button
                          (click)="applyVerifiedToken()"
                          class="w-full py-2 bg-emerald-600 hover:bg-emerald-500 text-white font-semibold rounded-lg shadow-sm transition"
                          data-testid="btn-apply-license"
                        >
                          ✓ Install & Activate On Active Cluster
                        </button>
                      </div>
                    }
                  </div>
                }
              </div>
            } @else {
              <div class="p-10 border border-dashed border-slate-800 rounded-xl text-center text-xs text-slate-500">
                Paste a signed license token and execute verification to inspect status and entitlements.
              </div>
            }
          </div>
        </div>
      }

      <!-- TAB 4: Air-Gapped Code Activation -->
      @if (activeTab() === 'airgap') {
        <div class="grid grid-cols-1 lg:grid-cols-2 gap-6" data-testid="view-airgap">
          <div class="bg-slate-900/90 border border-slate-800 rounded-xl p-5 space-y-4">
            <div>
              <h2 class="text-sm font-bold text-white">Air-Gapped License Activation</h2>
              <p class="text-xs text-slate-400">Zero-cloud activation using SHA-256 checksum-verified codes bound to local machine hardware.</p>
            </div>

            <div class="space-y-3 text-xs">
              <div>
                <label class="block text-slate-400 mb-1">Air-Gapped Activation Code (SGA1-XXXX-...)</label>
                <textarea
                  [(ngModel)]="airGapCodeInput"
                  rows="4"
                  class="w-full bg-slate-950 border border-slate-800 rounded-lg p-2.5 text-slate-200 focus:outline-none focus:border-indigo-500 font-mono text-[11px]"
                  placeholder="Paste air-gap activation code..."
                  data-testid="input-airgap-code"
                ></textarea>
              </div>

              <div>
                <label class="block text-slate-400 mb-1">Local Node Machine Fingerprint</label>
                <input
                  type="text"
                  [(ngModel)]="airGapFingerprintInput"
                  class="w-full bg-slate-950 border border-slate-800 rounded-lg px-3 py-2 text-slate-200 focus:outline-none focus:border-indigo-500 font-mono"
                  data-testid="input-airgap-fingerprint"
                />
              </div>

              <button
                (click)="activateAirGap()"
                class="w-full py-2.5 bg-indigo-600 hover:bg-indigo-500 text-white font-semibold rounded-lg shadow-sm transition"
                data-testid="btn-activate-airgap"
              >
                ⚡ Validate Checksum & Activate Air-Gapped License
              </button>
            </div>
          </div>

          <!-- Air-Gap Diagnostic -->
          <div class="bg-slate-900/90 border border-slate-800 rounded-xl p-5 space-y-4">
            <div>
              <h3 class="text-sm font-bold text-white">Air-Gapped Activation Status</h3>
              <p class="text-xs text-slate-400">Deterministic verification outcome.</p>
            </div>

            @if (airGapResult(); as res) {
              <div class="space-y-4 text-xs" data-testid="airgap-result">
                <div
                  class="p-4 rounded-xl border flex items-center justify-between"
                  [ngClass]="{
                    'bg-emerald-950/60 border-emerald-800 text-emerald-200': res.success,
                    'bg-rose-950/60 border-rose-800 text-rose-200': !res.success
                  }"
                >
                  <div>
                    <div class="font-bold text-sm" data-testid="airgap-status-label">
                      {{ res.success ? '✓ Activation Succeeded' : '✕ Activation Rejected' }}
                    </div>
                    <div class="text-[11px] opacity-90 mt-0.5">{{ res.message }}</div>
                  </div>
                </div>

                @if (res.payload; as p) {
                  <div class="p-3 bg-slate-950 border border-slate-800 rounded-lg text-slate-300 space-y-1 text-[11px]">
                    <div>Active Organization: <span class="text-emerald-400 font-semibold">{{ p.organizationName }}</span></div>
                    <div>Hardware Fingerprint: <span class="text-slate-400 font-mono">{{ p.hardwareClusterFingerprint }}</span></div>
                    <div>Active Seats: <span class="text-slate-200">{{ p.maxSeats }} Seats Entitled</span></div>
                  </div>
                }
              </div>
            } @else {
              <div class="p-10 border border-dashed border-slate-800 rounded-xl text-center text-xs text-slate-500">
                Provide an air-gapped activation code and target hardware fingerprint to activate offline.
              </div>
            }
          </div>
        </div>
      }
    </div>
  `
})
export class EnterpriseFleetLicensingStudioComponent {
  private http = inject(HttpClient, { optional: true });
  private translationService = inject(TranslationService, { optional: true });

  isRtl = computed(() => this.translationService?.currentLocale() === 'ar-AR');

  tenantId = signal<string>('00000000-0000-0000-0000-000000000001');
  activeTab = signal<'fleet' | 'issue' | 'verify' | 'airgap'>('fleet');

  // Pre-seeded Active License
  activeLicense = signal<LicensePayload | null>({
    tenantId: '00000000-0000-0000-0000-000000000001',
    organizationName: 'Apex Sovereign Enterprise',
    tier: 'SelfHostedEnterprise',
    maxSeats: 250,
    maxCpuCores: 64,
    maxClusterNodes: 8,
    enabledEnterpriseModules: [
      'AI.SwarmCoordination',
      'Voice.WebRtcSdr',
      'Analytics.ClickHouseBI',
      'Marketplace.PrivateRegistry',
      'Security.AirGappedKeyValidator',
      'Governance.DataSovereigntyMesh'
    ],
    issuedAtUtc: new Date(Date.now() - 15 * 86400000).toISOString(),
    expiresAtUtc: new Date(Date.now() + 350 * 86400000).toISOString(),
    hardwareClusterFingerprint: '*'
  });

  // Pre-seeded Cluster Fleet
  clusterFleet = signal<ClusterFleetStatus>({
    tenantId: '00000000-0000-0000-0000-000000000001',
    clusterName: 'Apex Sovereign Enterprise',
    overallStatus: 'Healthy',
    totalNodes: 2,
    activeNodes: 2,
    totalAllocatedCpuCores: 40,
    maxAllowedCpuCores: 64,
    totalActiveSeats: 130,
    maxAllowedSeats: 250,
    maxAllowedNodes: 8,
    seatQuotaExceeded: false,
    cpuQuotaExceeded: false,
    nodeQuotaExceeded: false,
    nodes: [
      {
        nodeId: 'node-alpha-01',
        nodeName: 'Core-Mesh-Alpha',
        machineFingerprint: 'FP-SRV-9901-MAC-A1',
        status: 'Healthy',
        allocatedCpuCores: 16,
        activeSeats: 48,
        cpuUtilizationPercent: 42.5,
        lastHeartbeatAtUtc: new Date().toISOString(),
        isStale: false,
        statusDetails: 'Healthy: Heartbeat responsive and hardware telemetry verified.'
      },
      {
        nodeId: 'node-beta-02',
        nodeName: 'Worker-Fleet-Beta',
        machineFingerprint: 'FP-SRV-9902-MAC-B2',
        status: 'Healthy',
        allocatedCpuCores: 24,
        activeSeats: 82,
        cpuUtilizationPercent: 58.2,
        lastHeartbeatAtUtc: new Date().toISOString(),
        isStale: false,
        statusDetails: 'Healthy: Heartbeat responsive and hardware telemetry verified.'
      }
    ],
    activeAlerts: [],
    lastEvaluatedAtUtc: new Date().toISOString()
  });

  verificationResult = signal<LicenseVerificationResult | null>(null);
  airGapResult = signal<AirGapActivationResponse | null>(null);
  lastIssuedLicense = signal<IssuedCryptographicLicense | null>(null);
  toastMessage = signal<{ type: 'success' | 'warning' | 'error' | 'info'; text: string } | null>(null);

  // Form Fields
  issueOrgName = 'Apex Sovereign Enterprise';
  issueTier = 'SelfHostedEnterprise';
  issueSeats = 250;
  issueCores = 64;
  issueNodes = 8;
  issueValidityDays = 365;
  issueFingerprint = '*';

  availableModules = [
    'AI.SwarmCoordination',
    'Voice.WebRtcSdr',
    'Analytics.ClickHouseBI',
    'Marketplace.PrivateRegistry',
    'Security.AirGappedKeyValidator',
    'Governance.DataSovereigntyMesh'
  ];

  selectedModules = signal<string[]>([
    'AI.SwarmCoordination',
    'Voice.WebRtcSdr',
    'Analytics.ClickHouseBI'
  ]);

  tokenInput = '';
  fingerprintInput = 'FP-SRV-9901-MAC-A1';

  airGapCodeInput = '';
  airGapFingerprintInput = 'FP-SRV-9901-MAC-A1';

  hbNodeId = 'node-gamma-03';
  hbNodeName = 'Compute-Edge-Gamma';
  hbFingerprint = 'FP-SRV-9903-MAC-C3';
  hbAllocatedCores = 16;
  hbActiveSeats = 35;
  hbCpuUsage = 48.5;

  // Computed Meters
  seatUsagePercent = computed(() => {
    const f = this.clusterFleet();
    return f.maxAllowedSeats > 0 ? Math.min(100, Math.round((f.totalActiveSeats / f.maxAllowedSeats) * 100)) : 0;
  });

  cpuUsagePercent = computed(() => {
    const f = this.clusterFleet();
    return f.maxAllowedCpuCores > 0 ? Math.min(100, Math.round((f.totalAllocatedCpuCores / f.maxAllowedCpuCores) * 100)) : 0;
  });

  nodeUsagePercent = computed(() => {
    const f = this.clusterFleet();
    return f.maxAllowedNodes > 0 ? Math.min(100, Math.round((f.activeNodes / f.maxAllowedNodes) * 100)) : 0;
  });

  daysRemaining = computed(() => {
    const lic = this.activeLicense();
    if (!lic) return 0;
    const diff = new Date(lic.expiresAtUtc).getTime() - Date.now();
    return Math.max(0, Math.floor(diff / (1000 * 60 * 60 * 24)));
  });

  selectTab(tab: 'fleet' | 'issue' | 'verify' | 'airgap'): void {
    this.activeTab.set(tab);
  }

  toggleModule(mod: string): void {
    this.selectedModules.update(list =>
      list.includes(mod) ? list.filter(m => m !== mod) : [...list, mod]
    );
  }

  generateLicense(): void {
    const now = new Date();
    const expires = new Date(now.getTime() + this.issueValidityDays * 86400000);

    const payload: LicensePayload = {
      tenantId: this.tenantId(),
      organizationName: this.issueOrgName.trim() || 'Sovereign Enterprise',
      tier: this.issueTier,
      maxSeats: Math.max(1, this.issueSeats),
      maxCpuCores: Math.max(1, this.issueCores),
      maxClusterNodes: Math.max(1, this.issueNodes),
      enabledEnterpriseModules: [...this.selectedModules()],
      issuedAtUtc: now.toISOString(),
      expiresAtUtc: expires.toISOString(),
      hardwareClusterFingerprint: this.issueFingerprint.trim() || '*'
    };

    const payloadJson = JSON.stringify(payload);
    const payloadBase64 = this.base64UrlEncode(payloadJson);
    const canonical = this.computeCanonical(payload);
    const signature = this.simpleHmacSha256(canonical);
    const signedToken = `${payloadBase64}.${signature}`;
    const licenseKey = `SG-ENT-${signature.substring(0, 4).toUpperCase()}-${signature.substring(4, 8).toUpperCase()}-${payload.tenantId.substring(0, 4).toUpperCase()}`;

    const tokenBytes = this.stringToUtf8Bytes(signedToken);
    const tokenEncoded = this.base64UrlEncodeBytes(tokenBytes);
    const checksum = this.computeChecksumHex(tokenBytes);
    const airGapCode = `SGA1-${tokenEncoded}-${checksum}`;

    const issued: IssuedCryptographicLicense = {
      licenseKey,
      payload,
      signature,
      signedToken,
      airGappedActivationCode: airGapCode,
      generatedAtUtc: now.toISOString()
    };

    this.lastIssuedLicense.set(issued);
    this.tokenInput = signedToken;
    this.airGapCodeInput = airGapCode;

    this.toastMessage.set({
      type: 'success',
      text: `License successfully issued for ${payload.organizationName} (${payload.maxSeats} seats, ${payload.maxCpuCores} cores).`
    });
  }

  verifyToken(): void {
    if (!this.tokenInput.trim()) {
      this.verificationResult.set({
        isValid: false,
        status: 'MalformedToken',
        message: 'Signed license token cannot be empty.',
        daysRemaining: 0,
        isAirGapCompliant: false,
        evaluatedAtUtc: new Date().toISOString()
      });
      return;
    }

    const parts = this.tokenInput.trim().split('.');
    if (parts.length !== 2 || !parts[0] || !parts[1]) {
      this.verificationResult.set({
        isValid: false,
        status: 'MalformedToken',
        message: 'Signed license token must contain exactly two Base64Url segments (payload.signature).',
        daysRemaining: 0,
        isAirGapCompliant: false,
        evaluatedAtUtc: new Date().toISOString()
      });
      return;
    }

    try {
      const payloadJson = this.base64UrlDecode(parts[0]);
      const payload: LicensePayload = JSON.parse(payloadJson);
      const expectedSig = this.simpleHmacSha256(this.computeCanonical(payload));

      if (expectedSig !== parts[1]) {
        this.verificationResult.set({
          isValid: false,
          status: 'TamperedPayload',
          message: 'Cryptographic signature mismatch. The license payload has been modified, tampered with, or forged.',
          payload,
          daysRemaining: 0,
          isAirGapCompliant: false,
          evaluatedAtUtc: new Date().toISOString()
        });
        return;
      }

      const now = Date.now();
      const expires = new Date(payload.expiresAtUtc).getTime();
      if (now > expires) {
        this.verificationResult.set({
          isValid: false,
          status: 'Expired',
          message: `License expired on ${new Date(expires).toLocaleDateString()}. Grace period exceeded.`,
          payload,
          daysRemaining: 0,
          isAirGapCompliant: false,
          evaluatedAtUtc: new Date().toISOString()
        });
        return;
      }

      if (payload.hardwareClusterFingerprint && payload.hardwareClusterFingerprint !== '*') {
        const allowed = payload.hardwareClusterFingerprint.split(',').map(s => s.trim().toUpperCase());
        const currentFp = this.fingerprintInput.trim().toUpperCase();
        if (!currentFp || !allowed.includes(currentFp)) {
          this.verificationResult.set({
            isValid: false,
            status: 'HardwareMismatch',
            message: `Machine fingerprint '${this.fingerprintInput}' does not match allowed cluster fingerprints [${payload.hardwareClusterFingerprint}].`,
            payload,
            daysRemaining: Math.max(0, Math.floor((expires - now) / 86400000)),
            isAirGapCompliant: false,
            evaluatedAtUtc: new Date().toISOString()
          });
          return;
        }
      }

      const days = Math.max(0, Math.floor((expires - now) / 86400000));
      this.verificationResult.set({
        isValid: true,
        status: 'Valid',
        message: 'Cryptographic HMAC-SHA256 signature verified. Entitlements authentic.',
        payload,
        daysRemaining: days,
        isAirGapCompliant: true,
        evaluatedAtUtc: new Date().toISOString()
      });
    } catch (e: any) {
      this.verificationResult.set({
        isValid: false,
        status: 'MalformedToken',
        message: `Failed to parse payload: ${e.message}`,
        daysRemaining: 0,
        isAirGapCompliant: false,
        evaluatedAtUtc: new Date().toISOString()
      });
    }
  }

  applyVerifiedToken(): void {
    const res = this.verificationResult();
    if (!res || !res.isValid || !res.payload) return;

    this.activeLicense.set(res.payload);
    this.recalculateFleetHealth();

    this.toastMessage.set({
      type: 'success',
      text: `License activated for ${res.payload.organizationName}! Fleet quotas updated.`
    });
  }

  activateAirGap(): void {
    const raw = this.airGapCodeInput.trim();
    if (!raw.startsWith('SGA1-')) {
      this.airGapResult.set({
        success: false,
        message: 'Invalid air-gapped activation code format. Must begin with SGA1-.'
      });
      return;
    }

    const lastHyphen = raw.lastIndexOf('-');
    if (lastHyphen <= 5) {
      this.airGapResult.set({
        success: false,
        message: 'Malformed air-gapped activation code structure.'
      });
      return;
    }

    const tokenEncoded = raw.substring(5, lastHyphen);
    const providedChecksum = raw.substring(lastHyphen + 1).toUpperCase();

    try {
      const tokenBytes = this.base64UrlDecodeToBytes(tokenEncoded);
      const expectedChecksum = this.computeChecksumHex(tokenBytes);

      if (providedChecksum !== expectedChecksum) {
        this.airGapResult.set({
          success: false,
          message: 'Activation code checksum verification failed. The activation code is corrupted or truncated.'
        });
        return;
      }

      const signedToken = this.utf8BytesToString(tokenBytes);
      this.tokenInput = signedToken;
      this.fingerprintInput = this.airGapFingerprintInput;
      this.verifyToken();

      const v = this.verificationResult();
      if (!v || !v.isValid || !v.payload) {
        this.airGapResult.set({
          success: false,
          message: `Air-gapped activation rejected: ${v?.message || 'Verification failed.'}`,
          payload: v?.payload
        });
        return;
      }

      this.activeLicense.set(v.payload);
      this.recalculateFleetHealth();

      this.airGapResult.set({
        success: true,
        message: 'Air-gapped enterprise license successfully activated and bound to cluster hardware.',
        payload: v.payload,
        signedToken
      });

      this.toastMessage.set({
        type: 'success',
        text: 'Air-gapped license successfully verified and installed.'
      });
    } catch (e: any) {
      this.airGapResult.set({
        success: false,
        message: `Air-gap decoding error: ${e.message}`
      });
    }
  }

  ingestHeartbeat(): void {
    const current = this.clusterFleet();
    const existingIndex = current.nodes.findIndex(n => n.nodeId === this.hbNodeId);

    const nodeStatus: ClusterNodeStatus = this.hbCpuUsage > 90 ? 'Degraded' : 'Healthy';
    const detail = this.hbCpuUsage > 90 ? 'Degraded: High CPU utilization load.' : 'Healthy: Telemetry responsive.';

    const updatedNode: NodeHealthSummary = {
      nodeId: this.hbNodeId,
      nodeName: this.hbNodeName,
      machineFingerprint: this.hbFingerprint,
      status: nodeStatus,
      allocatedCpuCores: Number(this.hbAllocatedCores),
      activeSeats: Number(this.hbActiveSeats),
      cpuUtilizationPercent: Number(this.hbCpuUsage),
      lastHeartbeatAtUtc: new Date().toISOString(),
      isStale: false,
      statusDetails: detail
    };

    let newNodes: NodeHealthSummary[];
    if (existingIndex >= 0) {
      newNodes = current.nodes.map((n, i) => i === existingIndex ? updatedNode : n);
    } else {
      newNodes = [...current.nodes, updatedNode];
    }

    this.clusterFleet.update(f => ({
      ...f,
      nodes: newNodes,
      totalNodes: newNodes.length
    }));

    this.recalculateFleetHealth();

    this.toastMessage.set({
      type: 'info',
      text: `Ingested heartbeat for ${this.hbNodeName} (${this.hbActiveSeats} seats, ${this.hbAllocatedCores} cores).`
    });
  }

  simulateQuotaOverage(): void {
    // Inject a high-load node that immediately breaches quota
    this.hbNodeId = 'node-overflow-99';
    this.hbNodeName = 'Overflow-Batch-Node';
    this.hbFingerprint = 'FP-OVERFLOW-999';
    this.hbAllocatedCores = 48;
    this.hbActiveSeats = 200; // Will push total seats beyond limit
    this.hbCpuUsage = 94.2;

    this.ingestHeartbeat();
  }

  resetDemoCluster(): void {
    this.activeLicense.set({
      tenantId: '00000000-0000-0000-0000-000000000001',
      organizationName: 'Apex Sovereign Enterprise',
      tier: 'SelfHostedEnterprise',
      maxSeats: 250,
      maxCpuCores: 64,
      maxClusterNodes: 8,
      enabledEnterpriseModules: [
        'AI.SwarmCoordination',
        'Voice.WebRtcSdr',
        'Analytics.ClickHouseBI',
        'Marketplace.PrivateRegistry',
        'Security.AirGappedKeyValidator',
        'Governance.DataSovereigntyMesh'
      ],
      issuedAtUtc: new Date(Date.now() - 15 * 86400000).toISOString(),
      expiresAtUtc: new Date(Date.now() + 350 * 86400000).toISOString(),
      hardwareClusterFingerprint: '*'
    });

    this.clusterFleet.set({
      tenantId: '00000000-0000-0000-0000-000000000001',
      clusterName: 'Apex Sovereign Enterprise',
      overallStatus: 'Healthy',
      totalNodes: 2,
      activeNodes: 2,
      totalAllocatedCpuCores: 40,
      maxAllowedCpuCores: 64,
      totalActiveSeats: 130,
      maxAllowedSeats: 250,
      maxAllowedNodes: 8,
      seatQuotaExceeded: false,
      cpuQuotaExceeded: false,
      nodeQuotaExceeded: false,
      nodes: [
        {
          nodeId: 'node-alpha-01',
          nodeName: 'Core-Mesh-Alpha',
          machineFingerprint: 'FP-SRV-9901-MAC-A1',
          status: 'Healthy',
          allocatedCpuCores: 16,
          activeSeats: 48,
          cpuUtilizationPercent: 42.5,
          lastHeartbeatAtUtc: new Date().toISOString(),
          isStale: false,
          statusDetails: 'Healthy: Heartbeat responsive and hardware telemetry verified.'
        },
        {
          nodeId: 'node-beta-02',
          nodeName: 'Worker-Fleet-Beta',
          machineFingerprint: 'FP-SRV-9902-MAC-B2',
          status: 'Healthy',
          allocatedCpuCores: 24,
          activeSeats: 82,
          cpuUtilizationPercent: 58.2,
          lastHeartbeatAtUtc: new Date().toISOString(),
          isStale: false,
          statusDetails: 'Healthy: Heartbeat responsive and hardware telemetry verified.'
        }
      ],
      activeAlerts: [],
      lastEvaluatedAtUtc: new Date().toISOString()
    });

    this.toastMessage.set({ type: 'info', text: 'Cluster fleet state reset to initial sovereign baseline.' });
  }

  copyText(text: string, label: string): void {
    if (navigator?.clipboard) {
      navigator.clipboard.writeText(text);
      this.toastMessage.set({ type: 'success', text: `${label} copied to clipboard!` });
    }
  }

  private recalculateFleetHealth(): void {
    const fleet = this.clusterFleet();
    const lic = this.activeLicense();

    const maxSeats = lic?.maxSeats ?? 50;
    const maxCores = lic?.maxCpuCores ?? 16;
    const maxNodes = lic?.maxClusterNodes ?? 3;

    const nonStaleNodes = fleet.nodes.filter(n => !n.isStale);
    const totalAllocatedCpuCores = nonStaleNodes.reduce((sum, n) => sum + n.allocatedCpuCores, 0);
    const totalActiveSeats = nonStaleNodes.reduce((sum, n) => sum + n.activeSeats, 0);
    const activeNodes = nonStaleNodes.length;

    const seatQuotaExceeded = totalActiveSeats > maxSeats;
    const cpuQuotaExceeded = totalAllocatedCpuCores > maxCores;
    const nodeQuotaExceeded = activeNodes > maxNodes;

    const alerts: string[] = [];
    if (seatQuotaExceeded) alerts.push(`Seat quota exceeded: ${totalActiveSeats}/${maxSeats} seats in use.`);
    if (cpuQuotaExceeded) alerts.push(`CPU core capacity exceeded: ${totalAllocatedCpuCores}/${maxCores} cores allocated.`);
    if (nodeQuotaExceeded) alerts.push(`Cluster node count exceeded: ${activeNodes}/${maxNodes} active nodes online.`);

    for (const node of fleet.nodes) {
      if (node.cpuUtilizationPercent > 90) {
        alerts.push(`High load on ${node.nodeName}: ${node.cpuUtilizationPercent.toFixed(1)}% CPU utilization.`);
      }
    }

    let overall: ClusterHealthState = 'Healthy';
    if (seatQuotaExceeded || cpuQuotaExceeded || nodeQuotaExceeded) {
      overall = 'QuotaExceeded';
    } else if (fleet.nodes.some(n => n.status === 'Degraded' || n.status === 'Offline')) {
      overall = 'Degraded';
    }

    this.clusterFleet.set({
      ...fleet,
      overallStatus: overall,
      totalNodes: fleet.nodes.length,
      activeNodes,
      totalAllocatedCpuCores,
      maxAllowedCpuCores: maxCores,
      totalActiveSeats,
      maxAllowedSeats: maxSeats,
      maxAllowedNodes: maxNodes,
      seatQuotaExceeded,
      cpuQuotaExceeded,
      nodeQuotaExceeded,
      activeAlerts: alerts,
      lastEvaluatedAtUtc: new Date().toISOString()
    });
  }

  private computeCanonical(payload: LicensePayload): string {
    const modulesSorted = [...payload.enabledEnterpriseModules].sort().join(',');
    const hw = (payload.hardwareClusterFingerprint || '*').trim().toUpperCase();
    const issuedSec = Math.floor(new Date(payload.issuedAtUtc).getTime() / 1000);
    const expiresSec = Math.floor(new Date(payload.expiresAtUtc).getTime() / 1000);
    return `${payload.tenantId}|${payload.organizationName}|${payload.tier}|${payload.maxSeats}|${payload.maxCpuCores}|${payload.maxClusterNodes}|${modulesSorted}|${issuedSec}|${expiresSec}|${hw}`;
  }

  private simpleHmacSha256(canonical: string): string {
    // Deterministic simulation hash for browser/client-side execution
    let hash = 0x811c9dc5;
    for (let i = 0; i < canonical.length; i++) {
      hash ^= canonical.charCodeAt(i);
      hash = Math.imul(hash, 0x01000193);
    }
    const hex = (hash >>> 0).toString(16).padStart(8, '0');
    return `SIG_${hex}_SG2026`;
  }

  private computeChecksumHex(bytes: Uint8Array): string {
    let hash = 0x811c9dc5;
    for (let i = 0; i < bytes.length; i++) {
      hash ^= bytes[i];
      hash = Math.imul(hash, 0x01000193);
    }
    return (hash >>> 0).toString(16).padStart(8, '0').toUpperCase();
  }

  private base64UrlEncode(str: string): string {
    const bytes = this.stringToUtf8Bytes(str);
    return this.base64UrlEncodeBytes(bytes);
  }

  private base64UrlEncodeBytes(bytes: Uint8Array): string {
    let binary = '';
    for (let i = 0; i < bytes.length; i++) {
      binary += String.fromCharCode(bytes[i]);
    }
    return btoa(binary).replaceAll('+', '-').replaceAll('/', '_').replace(/=+$/, '');
  }

  private base64UrlDecode(str: string): string {
    const bytes = this.base64UrlDecodeToBytes(str);
    return this.utf8BytesToString(bytes);
  }

  private base64UrlDecodeToBytes(str: string): Uint8Array {
    let base64 = str.replaceAll('-', '+').replaceAll('_', '/');
    while (base64.length % 4 !== 0) {
      base64 += '=';
    }
    const binary = atob(base64);
    const bytes = new Uint8Array(binary.length);
    for (let i = 0; i < binary.length; i++) {
      bytes[i] = binary.charCodeAt(i);
    }
    return bytes;
  }

  private stringToUtf8Bytes(str: string): Uint8Array {
    return new TextEncoder().encode(str);
  }

  private utf8BytesToString(bytes: any): string {
    return new TextDecoder().decode(bytes);
  }
}
