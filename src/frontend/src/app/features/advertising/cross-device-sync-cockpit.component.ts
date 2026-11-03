import { Component, signal, ChangeDetectionStrategy } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';

export interface DeviceNode {
  identifierValue: string;
  type: string;
  confidenceScore: number;
}

export interface IdentityCluster {
  clusterId: string;
  householdId: string;
  nodes: DeviceNode[];
  overallConfidence: number;
}

export interface MatchedConversion {
  matchId: string;
  transactionId: string;
  clusterId: string;
  attributedChannel: string;
  amount: number;
  forwardedToCapi: boolean;
}

@Component({
  selector: 'app-cross-device-sync-cockpit',
  standalone: true,
  imports: [CommonModule, FormsModule],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="cockpit-container" dir="ltr" data-testid="cross-device-sync-cockpit">
      <header class="cockpit-header">
        <div class="header-titles">
          <h1>Horizon 182: Cross-Device Graph Sync & CAPI Offline Conversion Matcher</h1>
          <p class="subtitle">Deterministic & Probabilistic Identity Clustering with Server-Side CAPI Dispatch</p>
        </div>
        <div class="header-badges">
          <span class="badge badge-accent" data-testid="active-clusters-count">Active Clusters: {{ clusters().length }}</span>
          <span class="badge badge-success">Match Rate: 91.8%</span>
        </div>
      </header>

      <div class="cockpit-grid">
        <!-- Identity Clusters -->
        <section class="card clusters-card">
          <h2>Resolved Identity Clusters</h2>
          <div class="clusters-list">
            @for (cluster of clusters(); track cluster.clusterId) {
              <div 
                class="cluster-card" 
                [class.selected]="selectedCluster()?.clusterId === cluster.clusterId"
                (click)="selectCluster(cluster)"
                [attr.data-testid]="'cluster-item-' + cluster.clusterId">
                <div class="cluster-header">
                  <h3>{{ cluster.clusterId }}</h3>
                  <span class="conf-pill">{{ cluster.overallConfidence }}% Conf</span>
                </div>
                <div class="hh-tag">Household: {{ cluster.householdId }}</div>
                <div class="devices-summary">{{ cluster.nodes.length }} Linked Device Identifiers</div>
              </div>
            }
          </div>
        </section>

        <!-- Offline POS Conversion Ingestion Simulator -->
        <section class="card pos-card">
          <h2>POS / Offline Receipt Match Simulator</h2>
          <div class="pos-form">
            <label for="pos-tx">Transaction ID:</label>
            <input id="pos-tx" [(ngModel)]="posTxId" placeholder="POS-TX-99881" />

            <label for="pos-email">Customer Hashed Email (SHA-256):</label>
            <input id="pos-email" [(ngModel)]="posEmailHash" />

            <label for="pos-amount">Purchase Amount ($ USD):</label>
            <input id="pos-amount" type="number" [(ngModel)]="posAmount" data-testid="pos-amount-input" />

            <button 
              type="button" 
              class="btn-primary" 
              (click)="matchOfflineConversion()"
              data-testid="match-offline-btn">
              Match Conversion & Forward to CAPI
            </button>
          </div>

          @if (lastMatch(); as match) {
            <div class="match-result-box" data-testid="matched-result-box">
              <div class="match-header">
                <span class="match-pill">Match Confirmed: {{ match.matchId }}</span>
                <span class="forward-status">Forwarded to Meta CAPI & Google OCI</span>
              </div>
              <p><strong>Cluster:</strong> {{ match.clusterId }}</p>
              <p><strong>Attributed Channel:</strong> {{ match.attributedChannel }}</p>
              <p><strong>Gross Amount:</strong> \${{ match.amount }}</p>
            </div>
          }
        </section>

        <!-- Metrics & Graph Inspection -->
        <section class="card metrics-card" data-testid="sync-metrics-card">
          <h2>Identity Graph Inspection</h2>
          @if (selectedCluster(); as cluster) {
            <div class="cluster-details">
              <h3>Cluster Devices: {{ cluster.clusterId }}</h3>
              @for (node of cluster.nodes; track node.identifierValue) {
                <div class="node-row">
                  <span class="node-type">{{ node.type }}:</span>
                  <span class="node-val">{{ node.identifierValue | slice:0:24 }}...</span>
                  <span class="node-conf">({{ node.confidenceScore }}%)</span>
                </div>
              }
            </div>
          }
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
    .clusters-list {
      display: flex;
      flex-direction: column;
      gap: 0.75rem;
    }
    .cluster-card {
      background: #0f172a;
      border: 1px solid #334155;
      border-radius: 0.5rem;
      padding: 0.75rem;
      cursor: pointer;
      transition: all 0.2s ease;
    }
    .cluster-card:hover, .cluster-card.selected {
      border-color: #38bdf8;
      background: #1e293b;
    }
    .cluster-header {
      display: flex;
      justify-content: space-between;
      align-items: center;
    }
    .cluster-header h3 { margin: 0; font-size: 1rem; color: #f8fafc; }
    .conf-pill {
      background: #0369a1;
      font-size: 0.75rem;
      padding: 0.2rem 0.4rem;
      border-radius: 0.25rem;
      color: #e0f2fe;
    }
    .hh-tag { font-size: 0.8rem; color: #94a3b8; margin: 0.25rem 0; }
    .devices-summary { font-size: 0.8rem; color: #38bdf8; }
    .pos-form {
      display: flex;
      flex-direction: column;
      gap: 0.6rem;
    }
    input {
      background: #0f172a;
      border: 1px solid #334155;
      border-radius: 0.375rem;
      color: #f8fafc;
      padding: 0.45rem;
      font-family: inherit;
    }
    .btn-primary {
      background: #2563eb;
      color: #fff;
      border: none;
      border-radius: 0.375rem;
      padding: 0.5rem 1rem;
      font-weight: 600;
      cursor: pointer;
      margin-top: 0.5rem;
      transition: background 0.2s;
    }
    .btn-primary:hover { background: #1d4ed8; }
    .match-result-box {
      margin-top: 1rem;
      background: #0f172a;
      border: 1px solid #16a34a;
      border-radius: 0.5rem;
      padding: 0.75rem;
      font-size: 0.85rem;
    }
    .match-header {
      display: flex;
      justify-content: space-between;
      margin-bottom: 0.5rem;
    }
    .match-pill { background: #15803d; color: #dcfce7; padding: 0.2rem 0.5rem; border-radius: 0.25rem; }
    .forward-status { color: #4ade80; font-size: 0.75rem; }
    .node-row {
      display: flex;
      gap: 0.5rem;
      font-size: 0.8rem;
      margin-bottom: 0.4rem;
    }
    .node-type { color: #94a3b8; font-weight: 600; width: 130px; }
    .node-val { color: #f8fafc; flex: 1; font-family: monospace; }
    .node-conf { color: #38bdf8; }
  `]
})
export class CrossDeviceSyncCockpitComponent {
  clusters = signal<IdentityCluster[]>([
    {
      clusterId: 'CLUSTER-CD-901',
      householdId: 'HH-US-EAST-441',
      overallConfidence: 95.0,
      nodes: [
        { identifierValue: 'e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855', type: 'HashedEmailSha256', confidenceScore: 98 },
        { identifierValue: 'gaid-4819-bb01-92fa', type: 'MobileAdIdGaid', confidenceScore: 92 },
        { identifierValue: 'uid2-tok-8481-9921', type: 'Uid2Token', confidenceScore: 95 }
      ]
    },
    {
      clusterId: 'CLUSTER-CD-902',
      householdId: 'HH-EU-WEST-772',
      overallConfidence: 89.3,
      nodes: [
        { identifierValue: '8f434346648f6b96df89dda901c5176b10a6d83961dd3c1ac88b59b2dc327aa4', type: 'HashedEmailSha256', confidenceScore: 96 },
        { identifierValue: 'idfa-2941-ee44-11fa', type: 'MobileAdIdIdfa', confidenceScore: 90 }
      ]
    }
  ]);

  selectedCluster = signal<IdentityCluster | null>(this.clusters()[0]);
  posTxId = 'POS-TX-99881';
  posEmailHash = 'e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855';
  posAmount = 350.00;
  lastMatch = signal<MatchedConversion | null>(null);

  selectCluster(cluster: IdentityCluster) {
    this.selectedCluster.set(cluster);
  }

  matchOfflineConversion() {
    this.lastMatch.set({
      matchId: 'MATCH-' + Math.floor(Math.random() * 9000 + 1000),
      transactionId: this.posTxId,
      clusterId: this.selectedCluster()?.clusterId ?? 'CLUSTER-CD-901',
      attributedChannel: 'Omnichannel CAPI (Meta / Google OCI)',
      amount: this.posAmount,
      forwardedToCapi: true
    });
  }
}
