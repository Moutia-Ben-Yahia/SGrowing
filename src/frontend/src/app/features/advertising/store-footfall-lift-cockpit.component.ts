import { Component, signal, ChangeDetectionStrategy } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';

export interface RetailStore {
  storeId: string;
  storeName: string;
  region: string;
  dwellMinutes: number;
  verticesCount: number;
}

export interface LiftAnalysis {
  analysisId: string;
  storeId: string;
  campaignId: string;
  exposedVisits: number;
  controlVisits: number;
  liftPercentage: number;
  cpivEur: number;
}

@Component({
  selector: 'app-store-footfall-lift-cockpit',
  standalone: true,
  imports: [CommonModule, FormsModule],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="cockpit-container" dir="ltr" data-testid="store-footfall-lift-cockpit">
      <header class="cockpit-header">
        <div class="header-titles">
          <h1>Horizon 187: Multi-Touch Store Footfall & Geofence Polygon Lift Matrix</h1>
          <p class="subtitle">Indoor Dwell-Time Spatial Filtering & Incremental Physical Footfall Attribution</p>
        </div>
        <div class="header-badges">
          <span class="badge badge-accent" data-testid="stores-count">Monitored Stores: {{ stores().length }}</span>
          <span class="badge badge-success">Average Lift: +31.1%</span>
        </div>
      </header>

      <div class="cockpit-grid">
        <!-- Stores List -->
        <section class="card stores-card">
          <h2>Registered Retail Store Polygons</h2>
          <div class="stores-list">
            @for (s of stores(); track s.storeId) {
              <div 
                class="store-card" 
                [class.selected]="selectedStore()?.storeId === s.storeId"
                (click)="selectStore(s)"
                [attr.data-testid]="'store-card-' + s.storeId">
                <div class="store-top">
                  <h3>{{ s.storeName }}</h3>
                  <span class="region-pill">{{ s.region }}</span>
                </div>
                <div class="store-meta">
                  <span>Dwell Filter: <strong>&ge; {{ s.dwellMinutes }} mins</strong></span>
                  <span>Polygon: <strong>{{ s.verticesCount }} Vertices</strong></span>
                </div>
              </div>
            }
          </div>
        </section>

        <!-- Lift Calculator Panel -->
        <section class="card calculator-card">
          <h2>Incremental Footfall Lift Calculator</h2>
          @if (selectedStore(); as s) {
            <div class="calc-panel">
              <div class="selected-header">
                <h3>Target: {{ s.storeName }}</h3>
                <p>Filter Highway Drive-Bys &lt; {{ s.dwellMinutes }} min dwell</p>
              </div>

              <div class="input-group">
                <label for="exp-aud">Ad-Exposed Mobile Device Cohort:</label>
                <input 
                  id="exp-aud" 
                  type="number" 
                  [(ngModel)]="exposedAudience" 
                  data-testid="exposed-audience-input" />
              </div>

              <button 
                type="button" 
                class="btn-primary" 
                (click)="calculateLift()"
                data-testid="calculate-lift-btn">
                Run Geofence Lift Estimation
              </button>

              @if (activeAnalysis(); as a) {
                <div class="lift-result" data-testid="lift-analysis-box">
                  <div class="lift-header">
                    <span>Analysis: {{ a.analysisId }}</span>
                    <span class="text-success">Statistically Significant (p &lt; 0.01)</span>
                  </div>
                  <div class="metric-row">
                    <span>Incremental Footfall Lift:</span>
                    <strong class="text-accent">+{{ a.liftPercentage }}%</strong>
                  </div>
                  <div class="metric-row">
                    <span>Exposed Verified Visits:</span>
                    <strong>{{ a.exposedVisits }}</strong>
                  </div>
                  <div class="metric-row">
                    <span>Control Baseline Visits:</span>
                    <strong>{{ a.controlVisits }}</strong>
                  </div>
                  <div class="metric-row" data-testid="cpiv-value">
                    <span>Cost Per Incremental Visit (CPIV):</span>
                    <strong>€{{ a.cpivEur }}</strong>
                  </div>
                </div>
              }
            </div>
          }
        </section>

        <!-- Footfall Attribution Metrics -->
        <section class="card metrics-card">
          <h2>Attribution Insights</h2>
          <div class="insights-list">
            <div class="insight-row">
              <span>Drive-By Passers Filtered:</span>
              <strong class="text-warning">78.4%</strong>
            </div>
            <div class="insight-row">
              <span>Average Basket Size:</span>
              <strong>€84.50</strong>
            </div>
            <div class="insight-row">
              <span>Synthetic Control Matching:</span>
              <strong class="text-success">Nearest-Neighbor Propensity</strong>
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
    .stores-list { display: flex; flex-direction: column; gap: 0.75rem; }
    .store-card {
      background: #0f172a;
      border: 1px solid #334155;
      border-radius: 0.5rem;
      padding: 0.75rem;
      cursor: pointer;
      transition: all 0.2s ease;
    }
    .store-card:hover, .store-card.selected {
      border-color: #38bdf8;
      background: #1e293b;
    }
    .store-top { display: flex; justify-content: space-between; align-items: center; }
    .store-top h3 { margin: 0; font-size: 1rem; color: #f8fafc; }
    .region-pill { font-size: 0.75rem; background: #0369a1; padding: 0.2rem 0.4rem; border-radius: 0.25rem; }
    .store-meta { display: flex; gap: 1rem; font-size: 0.8rem; color: #94a3b8; margin-top: 0.35rem; }
    .calc-panel { display: flex; flex-direction: column; gap: 0.75rem; }
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
    .lift-result {
      margin-top: 1rem;
      background: #0f172a;
      border: 1px solid #16a34a;
      border-radius: 0.5rem;
      padding: 0.75rem;
      font-size: 0.85rem;
    }
    .lift-header { display: flex; justify-content: space-between; margin-bottom: 0.5rem; }
    .metric-row { display: flex; justify-content: space-between; margin-bottom: 0.4rem; }
    .text-accent { color: #38bdf8; font-weight: bold; }
    .text-success { color: #4ade80; }
    .text-warning { color: #f59e0b; }
    .insights-list { display: flex; flex-direction: column; gap: 0.6rem; font-size: 0.85rem; }
    .insight-row { display: flex; justify-content: space-between; }
    .insight-row span { color: #94a3b8; }
  `]
})
export class StoreFootfallLiftCockpitComponent {
  stores = signal<RetailStore[]>([
    {
      storeId: 'STORE-NYC-5TH-AVE',
      storeName: 'Flagship 5th Avenue',
      region: 'Metro New York',
      dwellMinutes: 7,
      verticesCount: 4
    },
    {
      storeId: 'STORE-LON-OXFORD-ST',
      storeName: 'Oxford Circus Pavilion',
      region: 'Greater London',
      dwellMinutes: 8,
      verticesCount: 4
    }
  ]);

  selectedStore = signal<RetailStore | null>(this.stores()[0]);
  exposedAudience = 120000;
  activeAnalysis = signal<LiftAnalysis | null>(null);

  selectStore(s: RetailStore) {
    this.selectedStore.set(s);
    this.activeAnalysis.set(null);
  }

  calculateLift() {
    const s = this.selectedStore();
    if (!s) return;

    const expVisits = Math.round(this.exposedAudience * 0.0385);
    const ctrlVisits = Math.round(this.exposedAudience * 0.0282);
    const liftPct = +(((expVisits - ctrlVisits) / ctrlVisits) * 100).toFixed(1);
    const incremental = expVisits - ctrlVisits;
    const cpiv = +(12500 / incremental).toFixed(2);

    this.activeAnalysis.set({
      analysisId: 'LIFT-' + Math.floor(Math.random() * 9000 + 1000),
      storeId: s.storeId,
      campaignId: 'CAMP-OMNICHANNEL-LIFT',
      exposedVisits: expVisits,
      controlVisits: ctrlVisits,
      liftPercentage: liftPct,
      cpivEur: cpiv
    });
  }
}
