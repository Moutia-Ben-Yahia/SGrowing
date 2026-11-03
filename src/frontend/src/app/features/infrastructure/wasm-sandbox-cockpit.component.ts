import { Component, signal, ChangeDetectionStrategy } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';

export interface WasmWorkerItem {
  workerId: string;
  tenantId: string;
  moduleName: string;
  memoryLimitMb: number;
  isolationRuntime: string;
  coldStartLatencyMs: number;
  status: string;
}

export interface SecurityCapabilityItem {
  capabilityId: string;
  workerId: string;
  allowedOutboundDnsRegex: string;
  capabilityPolicyLevel: string;
}

@Component({
  selector: 'app-wasm-sandbox-cockpit',
  standalone: true,
  imports: [CommonModule, FormsModule],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="cockpit-container" dir="ltr" data-testid="wasm-sandbox-cockpit">
      <header class="cockpit-header">
        <div class="badge">Horizon 160 · Edge Micro-Compute & Wasm Isolation</div>
        <h1>Autonomous Multi-Cloud WebAssembly Sandbox & Micro-VM Arbiter</h1>
        <p class="subtitle">Sub-millisecond Wasm cold start, strict memory isolation, capability-based zero-trust security, and tenant worker lifecycle orchestration</p>
      </header>

      <section class="metrics-grid">
        <div class="metric-card" data-testid="metric-running-vms">
          <div class="metric-label">Running Micro-VMs</div>
          <div class="metric-value positive">{{ runningMicroVmsCount() }}</div>
          <div class="metric-sub">Active WebAssembly tenant isolates</div>
        </div>
        <div class="metric-card" data-testid="metric-cold-start">
          <div class="metric-label">Avg Cold Start</div>
          <div class="metric-value positive">{{ avgColdStartMs() }}ms</div>
          <div class="metric-sub">Sub-millisecond AOT instantiations</div>
        </div>
        <div class="metric-card" data-testid="metric-active-tenants">
          <div class="metric-label">Active Tenants</div>
          <div class="metric-value">{{ activeTenantsCount() }}</div>
          <div class="metric-sub">Isolated memory partitions</div>
        </div>
        <div class="metric-card" data-testid="metric-memory-allocated">
          <div class="metric-label">Allocated Memory</div>
          <div class="metric-value">{{ totalMemoryAllocatedMb() }} MB</div>
          <div class="metric-sub">Hard-capped linear memory pages</div>
        </div>
      </section>

      <section class="workers-section">
        <div class="section-header">
          <h2>Active Wasm Micro-VM Workers</h2>
          <button class="action-btn" (click)="deployNewWorker()" data-testid="deploy-worker-btn">
            + Deploy Wasm Worker
          </button>
        </div>
        <div class="table-wrapper">
          <table class="data-table" data-testid="workers-table">
            <thead>
              <tr>
                <th>Worker ID</th>
                <th>Tenant / Module</th>
                <th>Memory Limit</th>
                <th>Runtime</th>
                <th>Cold Start</th>
                <th>Status</th>
              </tr>
            </thead>
            <tbody>
              @for (w of workers(); track w.workerId) {
                <tr [attr.data-testid]="'worker-row-' + w.workerId">
                  <td><code>{{ w.workerId }}</code></td>
                  <td class="primary-cell">
                    <strong>{{ w.moduleName }}</strong>
                    <span class="sub-text">{{ w.tenantId }}</span>
                  </td>
                  <td>{{ w.memoryLimitMb }} MB</td>
                  <td><span class="runtime-badge">{{ w.isolationRuntime }}</span></td>
                  <td><strong>{{ w.coldStartLatencyMs }}ms</strong></td>
                  <td><span class="status-pill status-running">{{ w.status }}</span></td>
                </tr>
              }
            </tbody>
          </table>
        </div>
      </section>

      <section class="capabilities-section">
        <h2>Capability-Based Security Attestations</h2>
        <div class="caps-grid" data-testid="capabilities-grid">
          @for (c of capabilities(); track c.capabilityId) {
            <div class="cap-card" [attr.data-testid]="'cap-card-' + c.capabilityId">
              <div class="cap-header">
                <strong>{{ c.capabilityId }} &rarr; {{ c.workerId }}</strong>
                <span class="policy-badge">{{ c.capabilityPolicyLevel }}</span>
              </div>
              <div class="cap-body">
                <p><strong>Allowed DNS:</strong> <code>{{ c.allowedOutboundDnsRegex }}</code></p>
              </div>
            </div>
          }
        </div>
      </section>
    </div>
  `,
  styles: [`
    .cockpit-container { padding: 24px; max-width: 1200px; margin: 0 auto; font-family: system-ui, -apple-system, sans-serif; color: #1e293b; }
    .cockpit-header { margin-bottom: 24px; }
    .badge { display: inline-block; padding: 4px 10px; border-radius: 9999px; background: #e0e7ff; color: #3730a3; font-size: 12px; font-weight: 600; text-transform: uppercase; margin-bottom: 8px; }
    h1 { font-size: 26px; font-weight: 700; margin: 0 0 6px 0; color: #0f172a; }
    .subtitle { font-size: 14px; color: #64748b; margin: 0; }
    .metrics-grid { display: grid; grid-template-columns: repeat(auto-fit, minmax(240px, 1fr)); gap: 16px; margin-bottom: 28px; }
    .metric-card { background: #ffffff; border: 1px solid #e2e8f0; border-radius: 8px; padding: 18px; box-shadow: 0 1px 3px rgba(0,0,0,0.05); }
    .metric-label { font-size: 13px; font-weight: 500; color: #64748b; margin-bottom: 6px; }
    .metric-value { font-size: 28px; font-weight: 700; color: #0f172a; }
    .metric-value.positive { color: #16a34a; }
    .metric-sub { font-size: 12px; color: #94a3b8; margin-top: 4px; }
    .section-header { display: flex; justify-content: space-between; align-items: center; margin-bottom: 14px; }
    .action-btn { background: #4f46e5; color: #ffffff; border: none; padding: 8px 14px; border-radius: 4px; font-size: 13px; font-weight: 600; cursor: pointer; }
    .table-wrapper { overflow-x: auto; background: #ffffff; border: 1px solid #e2e8f0; border-radius: 8px; }
    .data-table { width: 100%; border-collapse: collapse; text-align: left; font-size: 13px; }
    .data-table th, .data-table td { padding: 12px 16px; border-bottom: 1px solid #e2e8f0; }
    .data-table th { background: #f8fafc; color: #475569; font-weight: 600; }
    .primary-cell strong { display: block; color: #0f172a; }
    .sub-text { font-size: 11px; color: #64748b; display: block; margin-top: 2px; }
    code { background: #f8fafc; padding: 2px 6px; border-radius: 4px; font-size: 11px; }
    .runtime-badge { background: #f1f5f9; color: #334155; font-size: 11px; font-weight: 700; padding: 2px 6px; border-radius: 4px; }
    .status-pill { padding: 2px 8px; border-radius: 9999px; font-size: 11px; font-weight: 600; }
    .status-running { background: #dcfce7; color: #15803d; }
    .caps-grid { display: grid; grid-template-columns: repeat(auto-fit, minmax(320px, 1fr)); gap: 16px; margin-top: 14px; }
    .cap-card { background: #ffffff; border: 1px solid #e2e8f0; border-radius: 8px; padding: 16px; }
    .cap-header { display: flex; justify-content: space-between; margin-bottom: 8px; }
    .policy-badge { background: #dcfce7; color: #15803d; font-size: 11px; font-weight: 700; padding: 2px 8px; border-radius: 4px; }
    .cap-body p { margin: 4px 0; font-size: 12px; }
  `]
})
export class WasmSandboxCockpitComponent {
  runningMicroVmsCount = signal(2);
  avgColdStartMs = signal(0.42);
  activeTenantsCount = signal(2);
  totalMemoryAllocatedMb = signal(96);

  workers = signal<WasmWorkerItem[]>([
    {
      workerId: 'WASM-VM-001',
      tenantId: 'tenant-fintech-core',
      moduleName: 'fx-arbitrage-pricing.wasm',
      memoryLimitMb: 64,
      isolationRuntime: 'WASMTIME_AOT',
      coldStartLatencyMs: 0.48,
      status: 'RUNNING'
    },
    {
      workerId: 'WASM-VM-002',
      tenantId: 'tenant-adtech-rtb',
      moduleName: 'contextual-bid-evaluator.wasm',
      memoryLimitMb: 32,
      isolationRuntime: 'WASMER_LLVM',
      coldStartLatencyMs: 0.35,
      status: 'RUNNING'
    }
  ]);

  capabilities = signal<SecurityCapabilityItem[]>([
    {
      capabilityId: 'CAP-001',
      workerId: 'WASM-VM-001',
      allowedOutboundDnsRegex: '^api\\.marketdata\\.enterprise\\.corp$',
      capabilityPolicyLevel: 'STRICT_ZERO_TRUST'
    },
    {
      capabilityId: 'CAP-002',
      workerId: 'WASM-VM-002',
      allowedOutboundDnsRegex: '^dsp-exchange\\.adtech\\.internal$',
      capabilityPolicyLevel: 'RESTRICTED_EGRESS'
    }
  ]);

  deployNewWorker(): void {
    const newWorker: WasmWorkerItem = {
      workerId: `WASM-VM-00${this.workers().length + 1}`,
      tenantId: 'tenant-ai-telemetry',
      moduleName: 'speech-acoustic-filter.wasm',
      memoryLimitMb: 48,
      isolationRuntime: 'WASMTIME_AOT',
      coldStartLatencyMs: 0.39,
      status: 'RUNNING'
    };
    this.workers.update(list => [...list, newWorker]);
    this.runningMicroVmsCount.update(v => v + 1);
    this.totalMemoryAllocatedMb.update(v => v + 48);
  }
}
