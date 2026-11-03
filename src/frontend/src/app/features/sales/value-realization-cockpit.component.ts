import { Component, signal, ChangeDetectionStrategy } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';

export interface ValueMetricItem {
  metricId: string;
  customerAccountName: string;
  objectiveCategory: string;
  annualizedRoiDollars: number;
  realizationMultiplier: number;
  verificationSource: string;
}

export interface QbrDeckItem {
  deckId: string;
  customerAccountName: string;
  fiscalQuarter: string;
  totalRealizedRoiDollars: number;
  overallAccountHealthScore: number;
  proposedExpansionArrDollars: number;
}

@Component({
  selector: 'app-value-realization-cockpit',
  standalone: true,
  imports: [CommonModule, FormsModule],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="cockpit-container" dir="ltr" data-testid="value-realization-cockpit">
      <header class="cockpit-header">
        <div class="badge">Horizon 156 · Customer Success & Value Realization</div>
        <h1>Autonomous B2B Value Realization & Customer Success QBR Deck Generator</h1>
        <p class="subtitle">Automated realized ROI telemetry attestation, executive slide compiler, peer benchmarking, and expansion ARR proposal generation</p>
      </header>

      <section class="metrics-grid">
        <div class="metric-card" data-testid="metric-monitored-accounts">
          <div class="metric-label">Monitored Accounts</div>
          <div class="metric-value positive">{{ monitoredAccountsCount() }}</div>
          <div class="metric-sub">Enterprise tier-1 active contracts</div>
        </div>
        <div class="metric-card" data-testid="metric-aggregate-roi">
          <div class="metric-label">Aggregate Realized ROI</div>
          <div class="metric-value positive">\${{ (aggregateRoi() / 1000000).toFixed(2) }}M</div>
          <div class="metric-sub">Verified customer financial gains</div>
        </div>
        <div class="metric-card" data-testid="metric-avg-health">
          <div class="metric-label">Avg Account Health</div>
          <div class="metric-value">{{ avgHealthScore() }}%</div>
          <div class="metric-sub">Composite retention & adoption NPS</div>
        </div>
        <div class="metric-card" data-testid="metric-qbr-decks">
          <div class="metric-label">Generated QBR Decks</div>
          <div class="metric-value">{{ qbrDecksCount() }}</div>
          <div class="metric-sub">Board-ready executive presentations</div>
        </div>
      </section>

      <section class="decks-section">
        <div class="section-header">
          <h2>Executive QBR Presentation Decks</h2>
          <button class="action-btn" (click)="generateNewQbrDeck()" data-testid="generate-qbr-btn">
            + Generate QBR Deck
          </button>
        </div>
        <div class="table-wrapper">
          <table class="data-table" data-testid="decks-table">
            <thead>
              <tr>
                <th>Deck ID</th>
                <th>Enterprise Account</th>
                <th>Quarter</th>
                <th>Realized ROI</th>
                <th>Health Score</th>
                <th>Proposed Expansion</th>
              </tr>
            </thead>
            <tbody>
              @for (deck of decks(); track deck.deckId) {
                <tr [attr.data-testid]="'deck-row-' + deck.deckId">
                  <td><code>{{ deck.deckId }}</code></td>
                  <td class="primary-cell">
                    <strong>{{ deck.customerAccountName }}</strong>
                  </td>
                  <td><span class="quarter-badge">{{ deck.fiscalQuarter }}</span></td>
                  <td><strong>\${{ (deck.totalRealizedRoiDollars / 1000).toFixed(0) }}k</strong></td>
                  <td><span class="health-pill">{{ deck.overallAccountHealthScore }}%</span></td>
                  <td><strong>+\${{ (deck.proposedExpansionArrDollars / 1000).toFixed(0) }}k ARR</strong></td>
                </tr>
              }
            </tbody>
          </table>
        </div>
      </section>

      <section class="metrics-section">
        <h2>Verified Realized Value Benchmarks</h2>
        <div class="metrics-list" data-testid="metrics-grid">
          @for (m of metrics(); track m.metricId) {
            <div class="metric-item-card" [attr.data-testid]="'metric-card-' + m.metricId">
              <div class="m-header">
                <strong>{{ m.customerAccountName }}</strong>
                <span class="mult-badge">{{ m.realizationMultiplier }}x ROI</span>
              </div>
              <div class="m-body">
                <p><strong>Category:</strong> {{ m.objectiveCategory }}</p>
                <p><strong>Realized Savings:</strong> \${{ (m.annualizedRoiDollars / 1000).toFixed(0) }}k</p>
                <p><strong>Verification:</strong> <code>{{ m.verificationSource }}</code></p>
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
    code { background: #f8fafc; padding: 2px 6px; border-radius: 4px; font-size: 11px; }
    .quarter-badge { background: #f1f5f9; color: #334155; font-size: 11px; font-weight: 700; padding: 2px 6px; border-radius: 4px; }
    .health-pill { background: #dcfce7; color: #15803d; font-weight: 700; padding: 2px 8px; border-radius: 9999px; font-size: 11px; }
    .metrics-list { display: grid; grid-template-columns: repeat(auto-fit, minmax(320px, 1fr)); gap: 16px; margin-top: 14px; }
    .metric-item-card { background: #ffffff; border: 1px solid #e2e8f0; border-radius: 8px; padding: 16px; }
    .m-header { display: flex; justify-content: space-between; margin-bottom: 8px; }
    .mult-badge { background: #fef3c7; color: #b45309; font-size: 11px; font-weight: 700; padding: 2px 8px; border-radius: 4px; }
    .m-body p { margin: 4px 0; font-size: 12px; }
  `]
})
export class ValueRealizationCockpitComponent {
  monitoredAccountsCount = signal(2);
  aggregateRoi = signal(2280000);
  avgHealthScore = signal(96.5);
  qbrDecksCount = signal(1);

  decks = signal<QbrDeckItem[]>([
    {
      deckId: 'QBR-DECK-001',
      customerAccountName: 'Apex Global Financial',
      fiscalQuarter: '2026-Q2',
      totalRealizedRoiDollars: 2280000,
      overallAccountHealthScore: 96.5,
      proposedExpansionArrDollars: 420000
    }
  ]);

  metrics = signal<ValueMetricItem[]>([
    {
      metricId: 'VAL-MET-001',
      customerAccountName: 'Apex Global Financial',
      objectiveCategory: 'COST_REDUCTION',
      annualizedRoiDollars: 830000,
      realizationMultiplier: 3.2,
      verificationSource: 'AUTOMATED_LEDGER_AUDIT'
    },
    {
      metricId: 'VAL-MET-002',
      customerAccountName: 'Vanguard Logistics Cloud',
      objectiveCategory: 'CYCLE_TIME_SPEEDUP',
      annualizedRoiDollars: 1450000,
      realizationMultiplier: 4.8,
      verificationSource: 'EDI_FULFILLMENT_TIMESTAMPS'
    }
  ]);

  generateNewQbrDeck(): void {
    const newDeck: QbrDeckItem = {
      deckId: `QBR-DECK-00${this.decks().length + 1}`,
      customerAccountName: 'Starlight Financial Network',
      fiscalQuarter: '2026-Q3',
      totalRealizedRoiDollars: 1850000,
      overallAccountHealthScore: 98.0,
      proposedExpansionArrDollars: 350000
    };
    this.decks.update(list => [...list, newDeck]);
    this.qbrDecksCount.update(v => v + 1);
  }
}
