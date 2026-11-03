import { Component, signal, ChangeDetectionStrategy } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';

export interface BrandCampaignItem {
  campaignId: string;
  brandName: string;
  headline: string;
  slotType: string;
  maxCpcBidUsd: number;
  featuredSkusCount: number;
  isActive: boolean;
}

export interface BannerBidItem {
  auctionId: string;
  campaignId: string;
  searchKeyword: string;
  winningCpcUsd: number;
  effectiveRoas: number;
  inStockCount: number;
}

@Component({
  selector: 'app-sponsored-brand-ads-cockpit',
  standalone: true,
  imports: [CommonModule, FormsModule],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="cockpit-container" dir="ltr" data-testid="sponsored-brand-ads-cockpit">
      <header class="cockpit-header">
        <div class="badge">Horizon 177 · Retail Media Sponsored Brand Banners</div>
        <h1>Autonomous Retail Media Sponsored Brand Multi-Product Banner DSP Auction</h1>
        <p class="subtitle">Multi-SKU search header carousel banners, real-time inventory stock validation, and multi-attribute brand search lift attribution</p>
      </header>

      <section class="metrics-grid">
        <div class="metric-card" data-testid="metric-brand-campaigns">
          <div class="metric-label">Active Brand Campaigns</div>
          <div class="metric-value positive">{{ activeCampaignsCount() }}</div>
          <div class="metric-sub">Multi-product search headline campaigns</div>
        </div>
        <div class="metric-card" data-testid="metric-auctions-today">
          <div class="metric-label">Banner Auctions Today</div>
          <div class="metric-value">{{ auctionsToday() }}</div>
          <div class="metric-sub">Search result placements cleared</div>
        </div>
        <div class="metric-card" data-testid="metric-avg-cpc">
          <div class="metric-label">Avg Winning CPC</div>
          <div class="metric-value positive">\${{ avgCpcUsd().toFixed(2) }}</div>
          <div class="metric-sub">First-party search auction clearing price</div>
        </div>
        <div class="metric-card" data-testid="metric-blended-roas">
          <div class="metric-label">Blended Brand ROAS</div>
          <div class="metric-value positive">{{ blendedRoasPct() }}%</div>
          <div class="metric-sub">Multi-SKU conversion acceleration</div>
        </div>
      </section>

      <section class="campaigns-section">
        <div class="section-header">
          <h2>Active Sponsored Brand Campaigns</h2>
          <button class="action-btn" (click)="createBrandCampaign()" data-testid="create-campaign-btn">
            + Create Brand Campaign
          </button>
        </div>
        <div class="table-wrapper">
          <table class="data-table" data-testid="campaigns-table">
            <thead>
              <tr>
                <th>Campaign ID</th>
                <th>Brand Name</th>
                <th>Headline</th>
                <th>Slot Type</th>
                <th>Max CPC</th>
                <th>Featured SKUs</th>
                <th>Status</th>
              </tr>
            </thead>
            <tbody>
              @for (c of campaigns(); track c.campaignId) {
                <tr [attr.data-testid]="'campaign-row-' + c.campaignId">
                  <td><code>{{ c.campaignId }}</code></td>
                  <td class="primary-cell">
                    <strong>{{ c.brandName }}</strong>
                  </td>
                  <td><em>"{{ c.headline }}"</em></td>
                  <td><span class="slot-badge">{{ c.slotType }}</span></td>
                  <td><strong>\${{ c.maxCpcBidUsd.toFixed(2) }}</strong></td>
                  <td><span class="sku-badge">{{ c.featuredSkusCount }} In-Stock</span></td>
                  <td><span class="status-pill status-active">ACTIVE</span></td>
                </tr>
              }
            </tbody>
          </table>
        </div>
      </section>

      <section class="bids-section">
        <h2>Live Banner RTB Clearing Bids</h2>
        <div class="bids-grid" data-testid="bids-grid">
          @for (b of bids(); track b.auctionId) {
            <div class="bid-card" [attr.data-testid]="'bid-card-' + b.auctionId">
              <div class="b-header">
                <strong>{{ b.searchKeyword }}</strong>
                <span class="roas-badge">{{ b.effectiveRoas }}x ROAS</span>
              </div>
              <div class="b-body">
                <p><strong>Winning CPC:</strong> \${{ b.winningCpcUsd.toFixed(2) }}</p>
                <p><strong>SKUs Displayed:</strong> {{ b.inStockCount }} verified items</p>
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
    .badge { display: inline-block; padding: 4px 10px; background: #fdf4ff; color: #a21caf; border-radius: 9999px; font-size: 12px; font-weight: 600; margin-bottom: 8px; }
    h1 { font-size: 26px; font-weight: 700; margin: 0 0 8px 0; color: #86198f; }
    .subtitle { color: #64748b; font-size: 14px; margin: 0; max-width: 900px; line-height: 1.5; }
    .metrics-grid { display: grid; grid-template-columns: repeat(auto-fit, minmax(220px, 1fr)); gap: 16px; margin-bottom: 28px; }
    .metric-card { background: #ffffff; border: 1px solid #e2e8f0; border-radius: 10px; padding: 18px; box-shadow: 0 1px 3px rgba(0,0,0,0.05); }
    .metric-label { font-size: 12px; font-weight: 600; text-transform: uppercase; color: #64748b; margin-bottom: 6px; }
    .metric-value { font-size: 28px; font-weight: 800; color: #0f172a; margin-bottom: 4px; }
    .metric-value.positive { color: #059669; }
    .metric-sub { font-size: 12px; color: #94a3b8; }
    .campaigns-section, .bids-section { background: #ffffff; border: 1px solid #e2e8f0; border-radius: 12px; padding: 20px; margin-bottom: 24px; box-shadow: 0 1px 3px rgba(0,0,0,0.05); }
    .section-header { display: flex; justify-content: space-between; align-items: center; margin-bottom: 16px; }
    h2 { font-size: 18px; font-weight: 700; margin: 0; color: #1e293b; }
    .action-btn { background: #a21caf; color: #ffffff; border: none; border-radius: 6px; padding: 8px 14px; font-size: 13px; font-weight: 600; cursor: pointer; transition: background 0.15s; }
    .action-btn:hover { background: #86198f; }
    .table-wrapper { overflow-x: auto; }
    .data-table { width: 100%; border-collapse: collapse; font-size: 13px; text-align: left; }
    .data-table th { background: #f8fafc; padding: 10px 14px; font-weight: 600; color: #475569; border-bottom: 1px solid #e2e8f0; }
    .data-table td { padding: 12px 14px; border-bottom: 1px solid #f1f5f9; vertical-align: middle; }
    .primary-cell strong { color: #0f172a; }
    .slot-badge { background: #f5f3ff; color: #7c3aed; font-size: 11px; font-weight: 700; padding: 2px 6px; border-radius: 4px; }
    .sku-badge { background: #e0f2fe; color: #0369a1; font-size: 11px; font-weight: 700; padding: 2px 6px; border-radius: 4px; }
    .status-pill { padding: 3px 8px; border-radius: 9999px; font-size: 11px; font-weight: 600; }
    .status-active { background: #dcfce7; color: #15803d; }
    .bids-grid { display: grid; grid-template-columns: repeat(auto-fit, minmax(280px, 1fr)); gap: 16px; margin-top: 14px; }
    .bid-card { background: #ffffff; border: 1px solid #e2e8f0; border-radius: 8px; padding: 16px; }
    .b-header { display: flex; justify-content: space-between; margin-bottom: 8px; }
    .roas-badge { background: #ecfdf5; color: #047857; font-size: 11px; font-weight: 700; padding: 2px 6px; border-radius: 4px; }
    .b-body p { margin: 4px 0; font-size: 12px; }
  `]
})
export class SponsoredBrandAdsCockpitComponent {
  activeCampaignsCount = signal(1);
  auctionsToday = signal(8420);
  avgCpcUsd = signal(2.85);
  blendedRoasPct = signal(420.5);

  campaigns = signal<BrandCampaignItem[]>([
    {
      campaignId: 'CAMP-BRAND-001',
      brandName: 'AuraSound Acoustic Labs',
      headline: 'Experience Pure Studio Master Audio Anywhere',
      slotType: 'TopSearchHeaderBanner',
      maxCpcBidUsd: 3.25,
      featuredSkusCount: 3,
      isActive: true
    }
  ]);

  bids = signal<BannerBidItem[]>([
    {
      auctionId: 'AUC-BRAND-001',
      campaignId: 'CAMP-BRAND-001',
      searchKeyword: 'wireless noise cancelling headphones',
      winningCpcUsd: 3.10,
      effectiveRoas: 4.85,
      inStockCount: 3
    }
  ]);

  createBrandCampaign(): void {
    const newCamp: BrandCampaignItem = {
      campaignId: `CAMP-BRAND-00${this.campaigns().length + 1}`,
      brandName: 'LuxeGlow Skincare Organics',
      headline: 'Clinically Proven Botanical Hydration Formulas',
      slotType: 'CategoryAisleHeader',
      maxCpcBidUsd: 3.80,
      featuredSkusCount: 4,
      isActive: true
    };
    this.campaigns.update(list => [...list, newCamp]);
    this.activeCampaignsCount.update(v => v + 1);
  }
}
