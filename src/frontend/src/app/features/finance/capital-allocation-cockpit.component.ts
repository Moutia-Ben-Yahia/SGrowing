import { Component, signal, ChangeDetectionStrategy } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';

export interface AllocationItem {
  allocationId: string;
  assetClass: string;
  currencyIso: string;
  allocatedAmount: number;
  annualYieldPercentage: number;
  counterpartyInstitution: string;
  status: string;
}

export interface OptimizationItem {
  optimizationId: string;
  totalLiquidTreasuryDollars: number;
  currentMonthlyBurnRateDollars: number;
  runwayMonths: number;
  projectedInterestIncomeAnnualDollars: number;
}

@Component({
  selector: 'app-capital-allocation-cockpit',
  standalone: true,
  imports: [CommonModule, FormsModule],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="cockpit-container" dir="ltr" data-testid="capital-allocation-cockpit">
      <header class="cockpit-header">
        <div class="badge">Horizon 164 · Corporate Treasury & Liquidity</div>
        <h1>Autonomous Sovereign Capital Allocation & Working Capital Optimizer</h1>
        <p class="subtitle">Multi-currency treasury yield optimization, sovereign AAA T-bill ladder management, automated working capital runway extension, and covenant buffers</p>
      </header>

      <section class="metrics-grid">
        <div class="metric-card" data-testid="metric-portfolio-value">
          <div class="metric-label">Total Liquid Portfolio</div>
          <div class="metric-value positive">\${{ (totalPortfolioValue() / 1000000).toFixed(2) }}M</div>
          <div class="metric-sub">Multi-currency sovereign cash & T-bills</div>
        </div>
        <div class="metric-card" data-testid="metric-weighted-yield">
          <div class="metric-label">Weighted Average Yield</div>
          <div class="metric-value positive">{{ weightedYieldPct() }}%</div>
          <div class="metric-sub">Risk-adjusted sovereign annualized yield</div>
        </div>
        <div class="metric-card" data-testid="metric-runway-months">
          <div class="metric-label">Net Cash Runway</div>
          <div class="metric-value">{{ cashRunwayMonths() }} Mos</div>
          <div class="metric-sub">Self-sustaining operational runway</div>
        </div>
        <div class="metric-card" data-testid="metric-active-positions">
          <div class="metric-label">Active Treasury Positions</div>
          <div class="metric-value">{{ activePositionsCount() }}</div>
          <div class="metric-sub">Automated laddered maturities</div>
        </div>
      </section>

      <section class="allocations-section">
        <div class="section-header">
          <h2>Sovereign & Treasury Asset Allocations</h2>
          <button class="action-btn" (click)="allocateNewCapital()" data-testid="allocate-capital-btn">
            + Allocate Treasury Capital
          </button>
        </div>
        <div class="table-wrapper">
          <table class="data-table" data-testid="allocations-table">
            <thead>
              <tr>
                <th>Allocation ID</th>
                <th>Asset Class</th>
                <th>Currency</th>
                <th>Allocated Amount</th>
                <th>Yield</th>
                <th>Counterparty</th>
                <th>Status</th>
              </tr>
            </thead>
            <tbody>
              @for (a of allocations(); track a.allocationId) {
                <tr [attr.data-testid]="'allocation-row-' + a.allocationId">
                  <td><code>{{ a.allocationId }}</code></td>
                  <td><strong>{{ a.assetClass }}</strong></td>
                  <td><span class="curr-badge">{{ a.currencyIso }}</span></td>
                  <td><strong>\${{ (a.allocatedAmount / 1000000).toFixed(2) }}M</strong></td>
                  <td><span class="yield-pill">{{ a.annualYieldPercentage }}%</span></td>
                  <td>{{ a.counterpartyInstitution }}</td>
                  <td><span class="status-pill status-yielding">{{ a.status }}</span></td>
                </tr>
              }
            </tbody>
          </table>
        </div>
      </section>

      <section class="optimizations-section">
        <h2>Working Capital Runway & Burn Scenarios</h2>
        <div class="opts-grid" data-testid="optimizations-grid">
          @for (o of optimizations(); track o.optimizationId) {
            <div class="opt-card" [attr.data-testid]="'opt-card-' + o.optimizationId">
              <div class="o-header">
                <strong>{{ o.optimizationId }}</strong>
                <span class="runway-badge">{{ o.runwayMonths }} Months</span>
              </div>
              <div class="o-body">
                <p><strong>Monthly Burn:</strong> \${{ (o.currentMonthlyBurnRateDollars / 1000).toFixed(0) }}k/mo</p>
                <p><strong>Annual Interest Income:</strong> +\${{ (o.projectedInterestIncomeAnnualDollars / 1000).toFixed(0) }}k</p>
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
    .badge { display: inline-block; padding: 4px 10px; border-radius: 9999px; background: #fef3c7; color: #b45309; font-size: 12px; font-weight: 600; text-transform: uppercase; margin-bottom: 8px; }
    h1 { font-size: 26px; font-weight: 700; margin: 0 0 6px 0; color: #0f172a; }
    .subtitle { font-size: 14px; color: #64748b; margin: 0; }
    .metrics-grid { display: grid; grid-template-columns: repeat(auto-fit, minmax(240px, 1fr)); gap: 16px; margin-bottom: 28px; }
    .metric-card { background: #ffffff; border: 1px solid #e2e8f0; border-radius: 8px; padding: 18px; box-shadow: 0 1px 3px rgba(0,0,0,0.05); }
    .metric-label { font-size: 13px; font-weight: 500; color: #64748b; margin-bottom: 6px; }
    .metric-value { font-size: 28px; font-weight: 700; color: #0f172a; }
    .metric-value.positive { color: #16a34a; }
    .metric-sub { font-size: 12px; color: #94a3b8; margin-top: 4px; }
    .section-header { display: flex; justify-content: space-between; align-items: center; margin-bottom: 14px; }
    .action-btn { background: #059669; color: #ffffff; border: none; padding: 8px 14px; border-radius: 4px; font-size: 13px; font-weight: 600; cursor: pointer; }
    .table-wrapper { overflow-x: auto; background: #ffffff; border: 1px solid #e2e8f0; border-radius: 8px; }
    .data-table { width: 100%; border-collapse: collapse; text-align: left; font-size: 13px; }
    .data-table th, .data-table td { padding: 12px 16px; border-bottom: 1px solid #e2e8f0; }
    .data-table th { background: #f8fafc; color: #475569; font-weight: 600; }
    code { background: #f8fafc; padding: 2px 6px; border-radius: 4px; font-size: 11px; }
    .curr-badge { background: #e0f2fe; color: #0369a1; font-size: 11px; font-weight: 700; padding: 2px 6px; border-radius: 4px; }
    .yield-pill { color: #047857; font-weight: 700; }
    .status-pill { padding: 2px 8px; border-radius: 9999px; font-size: 11px; font-weight: 600; }
    .status-yielding { background: #dcfce7; color: #15803d; }
    .opts-grid { display: grid; grid-template-columns: repeat(auto-fit, minmax(320px, 1fr)); gap: 16px; margin-top: 14px; }
    .opt-card { background: #ffffff; border: 1px solid #e2e8f0; border-radius: 8px; padding: 16px; }
    .o-header { display: flex; justify-content: space-between; margin-bottom: 8px; }
    .runway-badge { background: #dcfce7; color: #15803d; font-size: 11px; font-weight: 700; padding: 2px 8px; border-radius: 4px; }
    .o-body p { margin: 4px 0; font-size: 12px; }
  `]
})
export class CapitalAllocationCockpitComponent {
  totalPortfolioValue = signal(23500000);
  weightedYieldPct = signal(4.71);
  cashRunwayMonths = signal(28.5);
  activePositionsCount = signal(2);

  allocations = signal<AllocationItem[]>([
    {
      allocationId: 'ALLOC-001',
      assetClass: 'SOVEREIGN_TBILLS_USD',
      currencyIso: 'USD',
      allocatedAmount: 15000000,
      annualYieldPercentage: 5.20,
      counterpartyInstitution: 'US Federal Reserve Treasury Direct',
      status: 'ACTIVE_YIELDING'
    },
    {
      allocationId: 'ALLOC-002',
      assetClass: 'OVERNIGHT_REPO_EUR',
      currencyIso: 'EUR',
      allocatedAmount: 8500000,
      annualYieldPercentage: 3.85,
      counterpartyInstitution: 'European Central Bank Facility',
      status: 'ACTIVE_YIELDING'
    }
  ]);

  optimizations = signal<OptimizationItem[]>([
    {
      optimizationId: 'OPT-001',
      totalLiquidTreasuryDollars: 24250000,
      currentMonthlyBurnRateDollars: 850000,
      runwayMonths: 28.5,
      projectedInterestIncomeAnnualDollars: 1107500
    }
  ]);

  allocateNewCapital(): void {
    const newAlloc: AllocationItem = {
      allocationId: `ALLOC-00${this.allocations().length + 1}`,
      assetClass: 'SOVEREIGN_TBILLS_USD',
      currencyIso: 'USD',
      allocatedAmount: 5000000,
      annualYieldPercentage: 5.10,
      counterpartyInstitution: 'US Federal Reserve Direct',
      status: 'ACTIVE_YIELDING'
    };
    this.allocations.update(list => [...list, newAlloc]);
    this.totalPortfolioValue.update(v => v + 5000000);
    this.activePositionsCount.update(v => v + 1);
  }
}
