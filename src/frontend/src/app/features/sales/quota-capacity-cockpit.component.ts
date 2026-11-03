import { Component, signal, ChangeDetectionStrategy } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';

export interface TerritoryCapacity {
  territoryId: string;
  regionName: string;
  tamEur: number;
  assignedQuotaEur: number;
  headcount: number;
  avgQuotaEur: number;
  coverageRatio: number;
  status: string;
}

export interface RebalanceOutcome {
  planId: string;
  territoryId: string;
  newHeadcount: number;
  newAvgQuotaEur: number;
  newCoverage: number;
}

@Component({
  selector: 'app-quota-capacity-cockpit',
  standalone: true,
  imports: [CommonModule, FormsModule],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="cockpit-container" dir="ltr" data-testid="quota-capacity-cockpit">
      <header class="cockpit-header">
        <div class="header-titles">
          <h1>Horizon 186: Sales Quota Capacity & Territory Heatmap Arbiter</h1>
          <p class="subtitle">Dynamic AE Quota Balancing, Pipeline Coverage Optimization & Whitespace Arbitration</p>
        </div>
        <div class="header-badges">
          <span class="badge badge-accent" data-testid="territories-count">Territories: {{ territories().length }}</span>
          <span class="badge badge-success">Target Coverage: 3.5x</span>
        </div>
      </header>

      <div class="cockpit-grid">
        <!-- Territories List -->
        <section class="card territories-card">
          <h2>Sales Territories & Capacity Load</h2>
          <div class="territories-list">
            @for (t of territories(); track t.territoryId) {
              <div 
                class="t-card" 
                [class.selected]="selectedTerritory()?.territoryId === t.territoryId"
                [class.burnout]="t.status === 'OverAllocatedBurnRisk'"
                [class.whitespace]="t.status === 'UnderAllocatedWhitespace'"
                (click)="selectTerritory(t)"
                [attr.data-testid]="'territory-card-' + t.territoryId">
                <div class="t-top">
                  <h3>{{ t.regionName }}</h3>
                  <span class="status-pill">{{ t.status }}</span>
                </div>
                <div class="t-meta">
                  <span>Quota: <strong>€{{ (t.assignedQuotaEur / 1000000).toFixed(1) }}M</strong></span>
                  <span>Headcount: <strong>{{ t.headcount }} AEs</strong></span>
                  <span>Avg Quota: <strong>€{{ (t.avgQuotaEur / 1000000).toFixed(2) }}M</strong></span>
                </div>
                <div class="coverage-bar">
                  <div class="coverage-fill" [style.width.%]="(t.coverageRatio / 5.0) * 100"></div>
                </div>
                <div class="coverage-label">Coverage: {{ t.coverageRatio }}x (Target 3.5x)</div>
              </div>
            }
          </div>
        </section>

        <!-- Capacity Rebalancer Panel -->
        <section class="card rebalancer-card">
          <h2>Territory Rebalancing Simulator</h2>
          @if (selectedTerritory(); as t) {
            <div class="sim-panel">
              <div class="t-selected-header">
                <h3>{{ t.regionName }} ({{ t.territoryId }})</h3>
                <p>Current Rep Average: €{{ (t.avgQuotaEur / 1000000).toFixed(2) }}M</p>
              </div>

              <div class="input-group">
                <label for="hc-add">Add Ramped AEs to Territory:</label>
                <input 
                  id="hc-add" 
                  type="number" 
                  min="1" 
                  max="10" 
                  [(ngModel)]="addedHeadcount" 
                  data-testid="add-headcount-input" />
              </div>

              <button 
                type="button" 
                class="btn-primary" 
                (click)="rebalanceTerritory()"
                data-testid="rebalance-btn">
                Simulate & Apply Capacity Rebalance
              </button>

              @if (activeOutcome(); as out) {
                <div class="outcome-box" data-testid="rebalance-result-box">
                  <div class="outcome-header">
                    <span>Rebalance Plan: {{ out.planId }}</span>
                    <span class="text-success">Status: Balanced</span>
                  </div>
                  <p>New Headcount: <strong>{{ out.newHeadcount }} AEs</strong></p>
                  <p>Optimized Rep Quota: <strong>€{{ (out.newAvgQuotaEur / 1000000).toFixed(2) }}M</strong></p>
                  <p>Target Pipeline Coverage: <strong>{{ out.newCoverage }}x</strong></p>
                </div>
              }
            </div>
          }
        </section>

        <!-- Imbalance Alerts -->
        <section class="card alerts-card" data-testid="imbalance-alerts-list">
          <h2>Active Territory Imbalance Alerts</h2>
          <div class="alerts-list">
            <div class="alert-item danger">
              <h4>Quota Burnout Risk (EMEA-Nordics-UK)</h4>
              <p>Average quota €2.75M exceeds recommended limit. Pipeline coverage at 2.3x.</p>
            </div>
            <div class="alert-item warning">
              <h4>Whitespace Expansion (APAC-Singapore-ANZ)</h4>
              <p>Pipeline coverage at 4.9x with strong TAM. Territory can absorb 3 additional reps.</p>
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
    .territories-list { display: flex; flex-direction: column; gap: 0.75rem; }
    .t-card {
      background: #0f172a;
      border: 1px solid #334155;
      border-radius: 0.5rem;
      padding: 0.75rem;
      cursor: pointer;
      transition: all 0.2s ease;
    }
    .t-card:hover, .t-card.selected {
      border-color: #38bdf8;
      background: #1e293b;
    }
    .t-card.burnout { border-left: 4px solid #ef4444; }
    .t-card.whitespace { border-left: 4px solid #f59e0b; }
    .t-top { display: flex; justify-content: space-between; align-items: center; }
    .t-top h3 { margin: 0; font-size: 1rem; color: #f8fafc; }
    .status-pill { font-size: 0.75rem; padding: 0.2rem 0.4rem; border-radius: 0.25rem; background: #0369a1; color: #e0f2fe; }
    .t-meta { display: flex; gap: 1rem; font-size: 0.8rem; color: #94a3b8; margin: 0.35rem 0; }
    .coverage-bar { background: #334155; height: 6px; border-radius: 3px; overflow: hidden; margin-top: 0.35rem; }
    .coverage-fill { background: #38bdf8; height: 100%; }
    .coverage-label { font-size: 0.75rem; color: #cbd5e1; margin-top: 0.25rem; }
    .sim-panel { display: flex; flex-direction: column; gap: 0.75rem; }
    .input-group { display: flex; flex-direction: column; gap: 0.25rem; }
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
      transition: background 0.2s;
    }
    .btn-primary:hover { background: #1d4ed8; }
    .outcome-box {
      margin-top: 1rem;
      background: #0f172a;
      border: 1px solid #16a34a;
      border-radius: 0.5rem;
      padding: 0.75rem;
      font-size: 0.85rem;
    }
    .outcome-header { display: flex; justify-content: space-between; margin-bottom: 0.5rem; }
    .text-success { color: #4ade80; }
    .alerts-list { display: flex; flex-direction: column; gap: 0.75rem; }
    .alert-item {
      background: #0f172a;
      border-radius: 0.375rem;
      padding: 0.65rem;
      font-size: 0.8rem;
    }
    .alert-item.danger { border-left: 3px solid #ef4444; }
    .alert-item.warning { border-left: 3px solid #f59e0b; }
    .alert-item h4 { margin: 0 0 0.25rem 0; color: #f8fafc; }
    .alert-item p { margin: 0; color: #94a3b8; }
  `]
})
export class QuotaCapacityCockpitComponent {
  territories = signal<TerritoryCapacity[]>([
    {
      territoryId: 'TERR-US-EAST',
      regionName: 'US-East-Enterprise',
      tamEur: 125000000,
      assignedQuotaEur: 28000000,
      headcount: 14,
      avgQuotaEur: 2000000,
      coverageRatio: 3.8,
      status: 'Balanced'
    },
    {
      territoryId: 'TERR-EMEA-NORTH',
      regionName: 'EMEA-Nordics-UK',
      tamEur: 95000000,
      assignedQuotaEur: 22000000,
      headcount: 8,
      avgQuotaEur: 2750000,
      coverageRatio: 2.3,
      status: 'OverAllocatedBurnRisk'
    },
    {
      territoryId: 'TERR-APAC-SOUTH',
      regionName: 'APAC-Singapore-ANZ',
      tamEur: 80000000,
      assignedQuotaEur: 10000000,
      headcount: 5,
      avgQuotaEur: 2000000,
      coverageRatio: 4.9,
      status: 'UnderAllocatedWhitespace'
    }
  ]);

  selectedTerritory = signal<TerritoryCapacity | null>(this.territories()[1]);
  addedHeadcount = 3;
  activeOutcome = signal<RebalanceOutcome | null>(null);

  selectTerritory(t: TerritoryCapacity) {
    this.selectedTerritory.set(t);
    this.activeOutcome.set(null);
  }

  rebalanceTerritory() {
    const t = this.selectedTerritory();
    if (!t) return;

    const newHc = t.headcount + this.addedHeadcount;
    const newAvgQuota = Math.round(t.assignedQuotaEur / newHc);

    this.activeOutcome.set({
      planId: 'PLAN-CAP-' + Math.floor(Math.random() * 9000 + 1000),
      territoryId: t.territoryId,
      newHeadcount: newHc,
      newAvgQuotaEur: newAvgQuota,
      newCoverage: 3.6
    });

    this.territories.update(items =>
      items.map(item => item.territoryId === t.territoryId
        ? { ...item, headcount: newHc, avgQuotaEur: newAvgQuota, coverageRatio: 3.6, status: 'Balanced' }
        : item
      )
    );
  }
}
