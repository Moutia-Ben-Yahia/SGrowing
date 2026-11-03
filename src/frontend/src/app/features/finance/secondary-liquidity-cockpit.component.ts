import { Component, signal, ChangeDetectionStrategy } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';

export interface TenderOfferItem {
  offerId: string;
  programName: string;
  maxVolumeUsd: number;
  sharePriceUsd: number;
  status: string;
  committedVolumeUsd: number;
}

export interface ShareLotItem {
  lotId: string;
  shareholderName: string;
  sharesOffered: number;
  aggregateValueUsd: number;
  rofrCleared: boolean;
  estimatedTaxUsd: number;
  status: string;
}

@Component({
  selector: 'app-secondary-liquidity-cockpit',
  standalone: true,
  imports: [CommonModule, FormsModule],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="cockpit-container" dir="ltr" data-testid="secondary-liquidity-cockpit">
      <header class="cockpit-header">
        <div class="badge">Horizon 169 · Corporate Equity & Secondary Markets</div>
        <h1>Autonomous Sovereign Secondary Share Liquidity & Tender Offer Ledger</h1>
        <p class="subtitle">Company-sponsored tender offer windows, automated Right of First Refusal (ROFR) clearance tokens, cap table anti-dilution simulation, and tax withholding</p>
      </header>

      <section class="metrics-grid">
        <div class="metric-card" data-testid="metric-active-offers">
          <div class="metric-label">Active Tender Offers</div>
          <div class="metric-value positive">{{ activeOffersCount() }}</div>
          <div class="metric-sub">Open liquidity tender windows</div>
        </div>
        <div class="metric-card" data-testid="metric-settled-volume">
          <div class="metric-label">Total Secondary Volume</div>
          <div class="metric-value positive">\${{ (settledVolumeUsd() / 1000000).toFixed(1) }}M</div>
          <div class="metric-sub">Settled liquidity transactions</div>
        </div>
        <div class="metric-card" data-testid="metric-holders-count">
          <div class="metric-label">Participating Holders</div>
          <div class="metric-value">{{ totalHoldersCount() }}</div>
          <div class="metric-sub">Verified employee & investor lots</div>
        </div>
        <div class="metric-card" data-testid="metric-avg-share-price">
          <div class="metric-label">Share Price (Tender)</div>
          <div class="metric-value">\${{ avgSharePriceUsd().toFixed(2) }}</div>
          <div class="metric-sub">Approved 409A safe harbor valuation</div>
        </div>
      </section>

      <section class="offers-section">
        <div class="section-header">
          <h2>Active Company Tender Offers</h2>
          <button class="action-btn" (click)="launchNewTenderOffer()" data-testid="launch-tender-btn">
            + Launch Tender Offer Window
          </button>
        </div>
        <div class="table-wrapper">
          <table class="data-table" data-testid="offers-table">
            <thead>
              <tr>
                <th>Offer ID</th>
                <th>Program Name</th>
                <th>Share Price</th>
                <th>Max Volume</th>
                <th>Committed Volume</th>
                <th>Status</th>
              </tr>
            </thead>
            <tbody>
              @for (o of offers(); track o.offerId) {
                <tr [attr.data-testid]="'offer-row-' + o.offerId">
                  <td><code>{{ o.offerId }}</code></td>
                  <td class="primary-cell">
                    <strong>{{ o.programName }}</strong>
                  </td>
                  <td><strong>\${{ o.sharePriceUsd.toFixed(2) }}</strong></td>
                  <td>\${{ (o.maxVolumeUsd / 1000000).toFixed(1) }}M</td>
                  <td><span class="commit-pill">\${{ (o.committedVolumeUsd / 1000000).toFixed(2) }}M</span></td>
                  <td><span class="status-pill status-active">{{ o.status }}</span></td>
                </tr>
              }
            </tbody>
          </table>
        </div>
      </section>

      <section class="lots-section">
        <h2>Secondary Share Lots & ROFR Status</h2>
        <div class="lots-grid" data-testid="lots-grid">
          @for (l of lots(); track l.lotId) {
            <div class="lot-card" [attr.data-testid]="'lot-card-' + l.lotId">
              <div class="l-header">
                <strong>{{ l.shareholderName }}</strong>
                <span class="rofr-badge">{{ l.status }}</span>
              </div>
              <div class="l-body">
                <p><strong>Shares Offered:</strong> {{ l.sharesOffered.toLocaleString() }} shares</p>
                <p><strong>Gross Proceeds:</strong> \${{ l.aggregateValueUsd.toLocaleString() }}</p>
                <p><strong>Est. Tax Withholding:</strong> \${{ l.estimatedTaxUsd.toLocaleString() }} (37%)</p>
                <p><strong>ROFR Cleared:</strong> <span class="cleared-badge">{{ l.rofrCleared ? 'YES' : 'PENDING' }}</span></p>
              </div>
            </div>
          }
        </div>
      </section>
    </div>
  `,
  styles: [`
    .cockpit-container { padding: 24px; font-family: -apple-system, BlinkMacSystemFont, "Segoe UI", Roboto, sans-serif; background: #f8fafc; min-height: 100vh; color: #0f172a; }
    .cockpit-header { margin-bottom: 24px; }
    .badge { display: inline-block; padding: 4px 10px; background: #fef3c7; color: #92400e; border-radius: 9999px; font-size: 12px; font-weight: 600; margin-bottom: 8px; }
    h1 { font-size: 26px; font-weight: 700; margin: 0 0 8px 0; color: #78350f; }
    .subtitle { color: #64748b; font-size: 14px; margin: 0; max-width: 900px; line-height: 1.5; }
    .metrics-grid { display: grid; grid-template-columns: repeat(auto-fit, minmax(220px, 1fr)); gap: 16px; margin-bottom: 28px; }
    .metric-card { background: #ffffff; border: 1px solid #e2e8f0; border-radius: 10px; padding: 18px; box-shadow: 0 1px 3px rgba(0,0,0,0.05); }
    .metric-label { font-size: 12px; font-weight: 600; text-transform: uppercase; color: #64748b; margin-bottom: 6px; }
    .metric-value { font-size: 28px; font-weight: 800; color: #0f172a; margin-bottom: 4px; }
    .metric-value.positive { color: #059669; }
    .metric-sub { font-size: 12px; color: #94a3b8; }
    .offers-section, .lots-section { background: #ffffff; border: 1px solid #e2e8f0; border-radius: 12px; padding: 20px; margin-bottom: 24px; box-shadow: 0 1px 3px rgba(0,0,0,0.05); }
    .section-header { display: flex; justify-content: space-between; align-items: center; margin-bottom: 16px; }
    h2 { font-size: 18px; font-weight: 700; margin: 0; color: #1e293b; }
    .action-btn { background: #d97706; color: #ffffff; border: none; border-radius: 6px; padding: 8px 14px; font-size: 13px; font-weight: 600; cursor: pointer; transition: background 0.15s; }
    .action-btn:hover { background: #b45309; }
    .table-wrapper { overflow-x: auto; }
    .data-table { width: 100%; border-collapse: collapse; font-size: 13px; text-align: left; }
    .data-table th { background: #f8fafc; padding: 10px 14px; font-weight: 600; color: #475569; border-bottom: 1px solid #e2e8f0; }
    .data-table td { padding: 12px 14px; border-bottom: 1px solid #f1f5f9; vertical-align: middle; }
    .primary-cell strong { color: #0f172a; }
    .commit-pill { color: #059669; font-weight: 700; }
    .status-pill { padding: 3px 8px; border-radius: 9999px; font-size: 11px; font-weight: 600; }
    .status-active { background: #dcfce7; color: #15803d; }
    .lots-grid { display: grid; grid-template-columns: repeat(auto-fit, minmax(320px, 1fr)); gap: 16px; margin-top: 14px; }
    .lot-card { background: #ffffff; border: 1px solid #e2e8f0; border-radius: 8px; padding: 16px; }
    .l-header { display: flex; justify-content: space-between; margin-bottom: 8px; }
    .rofr-badge { background: #ecfdf5; color: #047857; font-size: 11px; font-weight: 700; padding: 2px 8px; border-radius: 4px; }
    .cleared-badge { background: #dbeafe; color: #1d4ed8; font-size: 10px; font-weight: 700; padding: 2px 6px; border-radius: 4px; }
    .l-body p { margin: 4px 0; font-size: 12px; }
  `]
})
export class SecondaryLiquidityCockpitComponent {
  activeOffersCount = signal(1);
  settledVolumeUsd = signal(48200000);
  totalHoldersCount = signal(146);
  avgSharePriceUsd = signal(42.50);

  offers = signal<TenderOfferItem[]>([
    {
      offerId: 'TENDER-2026-Q4',
      programName: 'Series D Employee Liquidity Program',
      maxVolumeUsd: 25000000,
      sharePriceUsd: 42.50,
      status: 'ActiveOpenWindow',
      committedVolumeUsd: 18250000
    }
  ]);

  lots = signal<ShareLotItem[]>([
    {
      lotId: 'LOT-SEC-001',
      shareholderName: 'Julian Vance (VP Engineering)',
      sharesOffered: 20000,
      aggregateValueUsd: 850000,
      rofrCleared: true,
      estimatedTaxUsd: 314500,
      status: 'ROFR_CLEARED'
    }
  ]);

  launchNewTenderOffer(): void {
    const newOffer: TenderOfferItem = {
      offerId: `TENDER-2027-Q1-00${this.offers().length + 1}`,
      programName: 'Strategic Secondary Partner Liquidity Window',
      maxVolumeUsd: 15000000,
      sharePriceUsd: 48.00,
      status: 'ActiveOpenWindow',
      committedVolumeUsd: 0
    };
    this.offers.update(list => [...list, newOffer]);
    this.activeOffersCount.update(v => v + 1);
  }
}
