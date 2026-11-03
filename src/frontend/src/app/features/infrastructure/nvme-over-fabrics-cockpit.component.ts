import { Component, signal, ChangeDetectionStrategy } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';

export interface NvmeSubsystem {
  nqn: string;
  alias: string;
  transport: string;
  listenIp: string;
  capacityTb: number;
  anaState: string;
}

export interface FailoverEvent {
  eventId: string;
  nqn: string;
  primaryCtrl: string;
  backupCtrl: string;
  durationMs: number;
  zeroDataLoss: boolean;
}

@Component({
  selector: 'app-nvme-over-fabrics-cockpit',
  standalone: true,
  imports: [CommonModule, FormsModule],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="cockpit-container" dir="ltr" data-testid="nvme-over-fabrics-cockpit">
      <header class="cockpit-header">
        <div class="header-titles">
          <h1>Horizon 190: High-Throughput NVMe-over-Fabrics (NVMe-oF) Storage Mesh</h1>
          <p class="subtitle">Multi-Cloud RDMA/TCP NVMe-oF Subsystems, Asymmetric Namespace Access (ANA) & Sub-10ms Failover</p>
        </div>
        <div class="header-badges">
          <span class="badge badge-accent" data-testid="subsystems-count">Subsystems: {{ subsystems().length }}</span>
          <span class="badge badge-success" data-testid="throughput-stat">Aggregate IOPS: 3.85M | 128.4 Gbps</span>
        </div>
      </header>

      <div class="cockpit-grid">
        <!-- Subsystems List -->
        <section class="card subsystems-card">
          <h2>Active NVMe-oF Target Subsystems</h2>
          <div class="subsystems-list">
            @for (sub of subsystems(); track sub.nqn) {
              <div 
                class="sub-card" 
                [class.selected]="selectedSubsystem()?.nqn === sub.nqn"
                (click)="selectSubsystem(sub)"
                [attr.data-testid]="'subsystem-card-' + sub.alias">
                <div class="sub-top">
                  <h3>{{ sub.alias }}</h3>
                  <span class="transport-pill">{{ sub.transport }}</span>
                </div>
                <div class="nqn-label">{{ sub.nqn }}</div>
                <div class="sub-meta">
                  <span>Capacity: <strong>{{ sub.capacityTb }} TB</strong></span>
                  <span>Listen IP: <strong>{{ sub.listenIp }}</strong></span>
                  <span class="ana-state" data-testid="ana-multipath-state">ANA: {{ sub.anaState }}</span>
                </div>
              </div>
            }
          </div>
        </section>

        <!-- ANA Multipath Failover Simulator -->
        <section class="card failover-card">
          <h2>ANA Multipath Failover Simulator</h2>
          @if (selectedSubsystem(); as sub) {
            <div class="failover-panel">
              <div class="selected-header">
                <h3>Subsystem: {{ sub.alias }}</h3>
                <p>Status: {{ sub.anaState }} across Dual RoCEv2/TCP Fabrics</p>
              </div>

              <button 
                type="button" 
                class="btn-primary" 
                (click)="simulateAnaFailover()"
                data-testid="simulate-failover-btn">
                Simulate PCIe Link Degradation & Trigger ANA Failover
              </button>

              @if (lastFailover(); as ev) {
                <div class="failover-box" data-testid="failover-event-box">
                  <div class="f-header">
                    <span class="f-id">{{ ev.eventId }}</span>
                    <span class="dur-pill">{{ ev.durationMs }}ms (Sub-10ms Target)</span>
                  </div>
                  <p><strong>Primary Controller:</strong> {{ ev.primaryCtrl }} &rarr; Disengaged</p>
                  <p><strong>Backup Controller:</strong> {{ ev.backupCtrl }} &rarr; Active ANA Optimized</p>
                  <div class="lossless-badge">
                    🛡️ Zero Data Loss Verified (In-Flight Write Buffers Drained)
                  </div>
                </div>
              }
            </div>
          }
        </section>

        <!-- Storage Performance Scorecard -->
        <section class="card perf-card">
          <h2>Fabric Storage Telemetry</h2>
          <div class="perf-list">
            <div class="p-row">
              <span>Average Read Latency:</span>
              <strong class="text-success">8.2 µs</strong>
            </div>
            <div class="p-row">
              <span>Average Write Latency:</span>
              <strong class="text-success">11.4 µs</strong>
            </div>
            <div class="p-row">
              <span>Namespace Block Format:</span>
              <strong>4096-Byte Native Sector</strong>
            </div>
            <div class="p-row">
              <span>Queue Depth Capacity:</span>
              <strong>128 Commands / Submission Queue</strong>
            </div>
          </div>
        </section>
      </div>
    </div>
  `,
  styles: [`
    .cockpit-container {
      padding: 1.5rem;
      background: #0f172a;
      color: #f8fafc;
      min-height: 100vh;
      font-family: system-ui, -apple-system, sans-serif;
    }
    .cockpit-header {
      display: flex;
      justify-content: space-between;
      align-items: center;
      margin-bottom: 2rem;
      border-bottom: 1px solid #334155;
      padding-bottom: 1rem;
    }
    .cockpit-header h1 { margin: 0; font-size: 1.5rem; color: #38bdf8; }
    .subtitle { margin: 0.25rem 0 0 0; color: #94a3b8; font-size: 0.9rem; }
    .header-badges { display: flex; gap: 0.75rem; }
    .badge {
      padding: 0.35rem 0.75rem;
      border-radius: 9999px;
      font-size: 0.8rem;
      font-weight: 600;
    }
    .badge-accent { background: #0369a1; color: #e0f2fe; }
    .badge-success { background: #15803d; color: #dcfce7; }
    .cockpit-grid {
      display: grid;
      grid-template-columns: repeat(auto-fit, minmax(340px, 1fr));
      gap: 1.5rem;
    }
    .card {
      background: #1e293b;
      border: 1px solid #334155;
      border-radius: 0.75rem;
      padding: 1.25rem;
    }
    .card h2 {
      margin-top: 0;
      font-size: 1.15rem;
      color: #e2e8f0;
      border-bottom: 1px solid #334155;
      padding-bottom: 0.5rem;
    }
    .subsystems-list { display: flex; flex-direction: column; gap: 0.75rem; }
    .sub-card {
      background: #0f172a;
      border: 1px solid #334155;
      border-radius: 0.5rem;
      padding: 0.75rem;
      cursor: pointer;
      transition: all 0.2s ease;
    }
    .sub-card:hover, .sub-card.selected {
      border-color: #38bdf8;
      background: #1e293b;
    }
    .sub-top { display: flex; justify-content: space-between; align-items: center; }
    .sub-top h3 { margin: 0; font-size: 0.95rem; color: #f8fafc; }
    .transport-pill { font-size: 0.75rem; background: #0369a1; padding: 0.2rem 0.4rem; border-radius: 0.25rem; }
    .nqn-label { font-size: 0.75rem; color: #64748b; font-family: monospace; margin: 0.25rem 0; }
    .sub-meta { display: flex; gap: 1rem; font-size: 0.8rem; color: #94a3b8; }
    .ana-state { color: #4ade80; }
    .failover-panel { display: flex; flex-direction: column; gap: 0.75rem; }
    .btn-primary {
      background: #2563eb;
      color: #fff;
      border: none;
      border-radius: 0.375rem;
      padding: 0.5rem 1rem;
      font-weight: 600;
      cursor: pointer;
      transition: background 0.2s;
    }
    .btn-primary:hover { background: #1d4ed8; }
    .failover-box {
      margin-top: 1rem;
      background: #0f172a;
      border: 1px solid #16a34a;
      border-radius: 0.5rem;
      padding: 0.75rem;
      font-size: 0.85rem;
    }
    .f-header { display: flex; justify-content: space-between; margin-bottom: 0.5rem; }
    .dur-pill { color: #4ade80; font-weight: 600; }
    .lossless-badge { color: #38bdf8; margin-top: 0.4rem; font-weight: 600; }
    .perf-list { display: flex; flex-direction: column; gap: 0.6rem; font-size: 0.85rem; }
    .p-row { display: flex; justify-content: space-between; }
    .p-row span { color: #94a3b8; }
    .text-success { color: #4ade80; }
  `]
})
export class NvmeOverFabricsCockpitComponent {
  subsystems = signal<NvmeSubsystem[]>([
    {
      nqn: 'nqn.2026-11.io.sgrowing:ai-checkpoint-pool-01',
      alias: 'AI Checkpoint High-IOPS Tier',
      transport: 'RdmaRocev2',
      listenIp: '10.240.10.15',
      capacityTb: 20.0,
      anaState: 'Optimized'
    },
    {
      nqn: 'nqn.2026-11.io.sgrowing:vector-embedding-cache-02',
      alias: 'Vector Embedding Low-Latency Cache',
      transport: 'Tcp',
      listenIp: '10.240.10.28',
      capacityTb: 5.0,
      anaState: 'Optimized'
    }
  ]);

  selectedSubsystem = signal<NvmeSubsystem | null>(this.subsystems()[0]);
  lastFailover = signal<FailoverEvent | null>(null);

  selectSubsystem(sub: NvmeSubsystem) {
    this.selectedSubsystem.set(sub);
  }

  simulateAnaFailover() {
    const sub = this.selectedSubsystem();
    if (!sub) return;

    this.lastFailover.set({
      eventId: 'ANA-FAIL-' + Math.floor(Math.random() * 9000 + 1000),
      nqn: sub.nqn,
      primaryCtrl: 'CTRL-NVME-PCIE-01',
      backupCtrl: 'CTRL-NVME-PCIE-02',
      durationMs: 6,
      zeroDataLoss: true
    });

    const updatedSub = { ...sub, anaState: 'FailoverActive' };
    this.selectedSubsystem.set(updatedSub);
    this.subsystems.update(items =>
      items.map(item => item.nqn === sub.nqn ? updatedSub : item)
    );
  }
}
