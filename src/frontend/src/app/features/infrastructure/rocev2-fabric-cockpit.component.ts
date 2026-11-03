import { Component, signal, ChangeDetectionStrategy } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';

export interface RoceNode {
  nodeId: string;
  hostname: string;
  cloudProvider: string;
  gpuCount: number;
  pfcQueue: number;
  losslessEnabled: boolean;
}

export interface DeadlockEvent {
  alertId: string;
  nodeId: string;
  queue: number;
  durationMs: number;
  mitigationAction: string;
  drained: boolean;
}

@Component({
  selector: 'app-rocev2-fabric-cockpit',
  standalone: true,
  imports: [CommonModule, FormsModule],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="cockpit-container" dir="ltr" data-testid="rocev2-fabric-cockpit">
      <header class="cockpit-header">
        <div class="header-titles">
          <h1>Horizon 185: Multi-Cloud RoCEv2 / RDMA over Converged Ethernet Fabric Arbiter</h1>
          <p class="subtitle">Lossless GPU-to-GPU Interconnects, PFC Deadlock Prevention & DCQCN Congestion Tuning</p>
        </div>
        <div class="header-badges">
          <span class="badge badge-accent" data-testid="roce-nodes-count">RoCEv2 Nodes: {{ nodes().length }}</span>
          <span class="badge badge-success">Fabric Latency: 3.8 µs</span>
        </div>
      </header>

      <div class="cockpit-grid">
        <!-- RoCEv2 Node Topology -->
        <section class="card nodes-card">
          <h2>Connected AI GPU Fabric Nodes</h2>
          <div class="nodes-list">
            @for (node of nodes(); track node.nodeId) {
              <div 
                class="node-card" 
                [class.selected]="selectedNode()?.nodeId === node.nodeId"
                (click)="selectNode(node)"
                [attr.data-testid]="'roce-node-' + node.nodeId">
                <div class="node-top">
                  <h3>{{ node.hostname }}</h3>
                  <span class="cloud-pill">{{ node.cloudProvider }}</span>
                </div>
                <div class="node-id">{{ node.nodeId }} (Priority {{ node.pfcQueue }})</div>
                <div class="node-meta">
                  <span>GPUs: <strong>{{ node.gpuCount }}x SXM5</strong></span>
                  <span class="lossless-badge">RoCEv2 Lossless Active</span>
                </div>
              </div>
            }
          </div>
        </section>

        <!-- Buffer Congestion & Deadlock Simulator -->
        <section class="card simulator-card">
          <h2>Fabric Congestion & PFC Watchdog Simulator</h2>
          <div class="sim-form">
            <label for="buffer-slider">Buffer Occupancy (%): {{ bufferOccupancy }}%</label>
            <input 
              id="buffer-slider" 
              type="range" 
              min="10" 
              max="100" 
              [(ngModel)]="bufferOccupancy" 
              data-testid="pfc-buffer-slider" />

            <div class="throughput-display">
              Throughput: <strong>384 Gbps</strong> | ECN Marking: <strong>{{ bufferOccupancy > 65 ? 'Active' : 'Idle' }}</strong>
            </div>

            <button 
              type="button" 
              class="btn-primary" 
              (click)="simulateTelemetry()"
              data-testid="ingest-telemetry-btn">
              Inject Telemetry & Run PFC Arbiter
            </button>
          </div>

          @if (activeDeadlock(); as dl) {
            <div class="deadlock-box" data-testid="deadlock-alert-box">
              <div class="deadlock-header">
                <span class="alert-pill">CRITICAL: PFC Pause Deadlock</span>
                <span class="queue-info">Queue {{ dl.queue }} ({{ dl.durationMs }}ms stuck)</span>
              </div>
              <p><strong>Node:</strong> {{ dl.nodeId }}</p>
              <p><strong>Mitigation:</strong> {{ dl.mitigationAction }}</p>
              <button 
                type="button" 
                class="btn-danger" 
                (click)="drainStuckQueue()"
                data-testid="trigger-watchdog-btn">
                Trigger PFC Watchdog Queue Drain
              </button>
            </div>
          }
        </section>

        <!-- DCQCN Telemetry -->
        <section class="card dcqcn-card">
          <h2>DCQCN Congestion Heuristics</h2>
          <div class="dcqcn-specs">
            <div class="spec-row">
              <span>Alpha Increase Rate:</span>
              <strong>0.05 / step</strong>
            </div>
            <div class="spec-row">
              <span>Beta Decrease Factor:</span>
              <strong>0.50 (ECN Congestion Backoff)</strong>
            </div>
            <div class="spec-row">
              <span>PFC Deadlock Threshold:</span>
              <strong>> 1000 frames/sec & > 90% buffer</strong>
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
    .nodes-list { display: flex; flex-direction: column; gap: 0.75rem; }
    .node-card {
      background: #0f172a;
      border: 1px solid #334155;
      border-radius: 0.5rem;
      padding: 0.75rem;
      cursor: pointer;
      transition: all 0.2s ease;
    }
    .node-card:hover, .node-card.selected {
      border-color: #38bdf8;
      background: #1e293b;
    }
    .node-top { display: flex; justify-content: space-between; align-items: center; }
    .node-top h3 { margin: 0; font-size: 0.95rem; color: #f8fafc; font-family: monospace; }
    .cloud-pill { background: #0284c7; font-size: 0.75rem; padding: 0.2rem 0.4rem; border-radius: 0.25rem; }
    .node-id { font-size: 0.8rem; color: #94a3b8; margin: 0.25rem 0; }
    .node-meta { display: flex; justify-content: space-between; font-size: 0.8rem; }
    .lossless-badge { color: #4ade80; }
    .sim-form { display: flex; flex-direction: column; gap: 0.75rem; }
    .throughput-display { font-size: 0.85rem; color: #cbd5e1; }
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
    .btn-danger {
      background: #dc2626;
      color: #fff;
      border: none;
      border-radius: 0.375rem;
      padding: 0.45rem 0.85rem;
      font-size: 0.8rem;
      font-weight: 600;
      cursor: pointer;
      margin-top: 0.5rem;
    }
    .btn-danger:hover { background: #b91c1c; }
    .deadlock-box {
      margin-top: 1rem;
      background: #0f172a;
      border: 1px solid #ef4444;
      border-radius: 0.5rem;
      padding: 0.75rem;
      font-size: 0.85rem;
    }
    .deadlock-header { display: flex; justify-content: space-between; margin-bottom: 0.5rem; }
    .alert-pill { background: #b91c1c; color: #fee2e2; padding: 0.2rem 0.4rem; border-radius: 0.25rem; font-weight: 600; }
    .queue-info { color: #f87171; }
    .dcqcn-specs { display: flex; flex-direction: column; gap: 0.5rem; font-size: 0.85rem; }
    .spec-row { display: flex; justify-content: space-between; }
    .spec-row span { color: #94a3b8; }
    .spec-row strong { color: #f8fafc; }
  `]
})
export class Rocev2FabricCockpitComponent {
  nodes = signal<RoceNode[]>([
    {
      nodeId: 'ROCE-GPU-01',
      hostname: 'gpu-worker-us-east-h100-01',
      cloudProvider: 'BareMetal',
      gpuCount: 8,
      pfcQueue: 3,
      losslessEnabled: true
    },
    {
      nodeId: 'ROCE-GPU-02',
      hostname: 'gpu-worker-azure-west-b200-02',
      cloudProvider: 'Azure',
      gpuCount: 8,
      pfcQueue: 3,
      losslessEnabled: true
    }
  ]);

  selectedNode = signal<RoceNode | null>(this.nodes()[0]);
  bufferOccupancy = 95;
  activeDeadlock = signal<DeadlockEvent | null>(null);

  selectNode(node: RoceNode) {
    this.selectedNode.set(node);
  }

  simulateTelemetry() {
    if (this.bufferOccupancy >= 90) {
      this.activeDeadlock.set({
        alertId: 'DEADLOCK-' + Math.floor(Math.random() * 9000 + 1000),
        nodeId: this.selectedNode()?.nodeId ?? 'ROCE-GPU-01',
        queue: 3,
        durationMs: 450,
        mitigationAction: 'Watchdog Auto-Drain: Injected transient packet drop on Queue 3 to break PFC pause loop',
        drained: false
      });
    } else {
      this.activeDeadlock.set(null);
    }
  }

  drainStuckQueue() {
    if (this.activeDeadlock()) {
      this.activeDeadlock.update(d => d ? { ...d, drained: true, mitigationAction: 'Queue Drained Successfully: Credit loop restored.' } : null);
    }
  }
}
