import { Component, ChangeDetectionStrategy, signal, computed } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';

export interface AuctionResult {
  auctionId: string;
  slotType: string;
  targetKeyword: string;
  winningMerchantName: string;
  winningSku: string;
  winningBidAmount: number;
  clearingPrice: number;
  computedRankScore: number;
  executedAt: string;
}

@Component({
  selector: 'app-retail-media-auction-cockpit',
  standalone: true,
  imports: [CommonModule, FormsModule],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="p-6 bg-slate-900 text-slate-100 min-h-screen" dir="ltr" data-testid="retail-media-auction-cockpit">
      <header class="mb-6 flex flex-col md:flex-row md:items-center justify-between gap-4 border-b border-slate-800 pb-4">
        <div>
          <div class="flex items-center gap-3">
            <span class="px-2.5 py-1 text-xs font-semibold rounded bg-cyan-500/20 text-cyan-400 border border-cyan-500/30">Horizon 102</span>
            <span class="text-xs text-slate-400">Retail Media Network (RMN) & Real-Time Ad Auction</span>
          </div>
          <h1 class="text-2xl font-bold tracking-tight text-white mt-1">Autonomous Retail Media Network Sponsored Ad Placement & Auction</h1>
          <p class="text-sm text-slate-400">Generalized Second-Price (GSP) bidding, eCTR relevance score weighting, and automated sponsored search interleaving.</p>
        </div>
      </header>

      <!-- KPI Summary -->
      <section class="grid grid-cols-1 md:grid-cols-4 gap-4 mb-6">
        <div class="bg-slate-800/80 border border-slate-700/60 p-4 rounded-xl" data-testid="kpi-daily-auctions">
          <div class="text-xs font-medium text-slate-400 uppercase">24h Ad Auctions</div>
          <div class="text-2xl font-bold text-white mt-1">18,450 Auctions</div>
          <div class="text-xs text-slate-400 mt-1">Real-time GSP second-price matching</div>
        </div>
        <div class="bg-slate-800/80 border border-slate-700/60 p-4 rounded-xl" data-testid="kpi-ad-revenue">
          <div class="text-xs font-medium text-slate-400 uppercase">RMN Ad Revenue (24h)</div>
          <div class="text-2xl font-bold text-emerald-400 mt-1">\${{ totalRevenue() | number:'1.0-0' }}</div>
          <div class="text-xs text-slate-400 mt-1">Direct merchant monetization</div>
        </div>
        <div class="bg-slate-800/80 border border-slate-700/60 p-4 rounded-xl" data-testid="kpi-clearing-cpc">
          <div class="text-xs font-medium text-slate-400 uppercase">Avg Clearing CPC</div>
          <div class="text-2xl font-bold text-cyan-400 mt-1">\${{ avgClearingCpc() | number:'1.2-2' }}</div>
          <div class="text-xs text-slate-400 mt-1">Second-price clearing rate</div>
        </div>
        <div class="bg-slate-800/80 border border-slate-700/60 p-4 rounded-xl" data-testid="kpi-merchant-roas">
          <div class="text-xs font-medium text-slate-400 uppercase">Merchant ROAS</div>
          <div class="text-2xl font-bold text-amber-400 mt-1">5.4x</div>
          <div class="text-xs text-slate-400 mt-1">Average merchant advertising yield</div>
        </div>
      </section>

      <!-- Recent Auctions Table -->
      <section class="bg-slate-800/70 border border-slate-700/60 rounded-xl p-5 mb-6">
        <div class="flex items-center justify-between mb-4">
          <h2 class="text-lg font-semibold text-white">Recent Real-Time GSP Ad Auctions</h2>
          <button class="px-3 py-1.5 bg-cyan-600 hover:bg-cyan-500 text-white rounded text-xs font-semibold transition-colors"
                  (click)="simulateAuction()"
                  data-testid="btn-simulate-auction">
            Simulate Sponsored Auction
          </button>
        </div>

        <div class="overflow-x-auto">
          <table class="w-full text-left text-sm" data-testid="auctions-table">
            <thead class="text-xs uppercase bg-slate-800 text-slate-400 border-b border-slate-700">
              <tr>
                <th class="py-3 px-4">Auction ID / Slot</th>
                <th class="py-3 px-4">Target Keyword</th>
                <th class="py-3 px-4">Winner Merchant</th>
                <th class="py-3 px-4">Product SKU</th>
                <th class="py-3 px-4">Max Bid</th>
                <th class="py-3 px-4">Clearing CPC</th>
                <th class="py-3 px-4">Rank Score</th>
                <th class="py-3 px-4 text-right">Time</th>
              </tr>
            </thead>
            <tbody class="divide-y divide-slate-750 text-slate-300">
              @for (a of auctions(); track a.auctionId) {
                <tr class="hover:bg-slate-800/50 transition-colors" [attr.data-testid]="'auction-row-' + a.auctionId">
                  <td class="py-3 px-4">
                    <div class="font-medium text-white">{{ a.slotType }}</div>
                    <div class="text-xs text-slate-400 font-mono">{{ a.auctionId }}</div>
                  </td>
                  <td class="py-3 px-4 text-xs font-mono text-cyan-300">
                    "{{ a.targetKeyword }}"
                  </td>
                  <td class="py-3 px-4 font-semibold text-white">
                    {{ a.winningMerchantName }}
                  </td>
                  <td class="py-3 px-4 text-xs font-mono text-slate-400">
                    {{ a.winningSku }}
                  </td>
                  <td class="py-3 px-4 text-xs text-slate-400 line-through">
                    \${{ a.winningBidAmount | number:'1.2-2' }}
                  </td>
                  <td class="py-3 px-4 font-bold text-emerald-400 font-mono">
                    \${{ a.clearingPrice | number:'1.2-2' }}
                  </td>
                  <td class="py-3 px-4 text-xs font-mono text-purple-300">
                    {{ a.computedRankScore | number:'1.3-3' }}
                  </td>
                  <td class="py-3 px-4 text-right text-xs text-slate-400">
                    Just now
                  </td>
                </tr>
              }
            </tbody>
          </table>
        </div>
      </section>

      <!-- Action Toast -->
      @if (lastAuctionNotice()) {
        <div class="p-3 bg-cyan-950/60 border border-cyan-500/40 rounded-xl text-cyan-300 text-xs flex items-center justify-between"
             data-testid="auction-notice">
          <span>{{ lastAuctionNotice() }}</span>
          <button (click)="lastAuctionNotice.set(null)" class="text-slate-400 hover:text-white">&times;</button>
        </div>
      }
    </div>
  `
})
export class RetailMediaAuctionCockpitComponent {
  readonly auctions = signal<AuctionResult[]>([
    {
      auctionId: 'AUC-2026-8001',
      slotType: 'SearchTopBanner',
      targetKeyword: 'wireless noise cancelling headphones',
      winningMerchantName: 'AuraAudio Labs',
      winningSku: 'SKU-AURA-NC700',
      winningBidAmount: 3.50,
      clearingPrice: 2.85,
      computedRankScore: 0.882,
      executedAt: '2026-11-03T02:00:00Z'
    },
    {
      auctionId: 'AUC-2026-8002',
      slotType: 'ProductGridSponsoredListing',
      targetKeyword: 'ergonomic mechanical keyboard',
      winningMerchantName: 'KeyCraft Studio',
      winningSku: 'SKU-KEY-PRO-RGB',
      winningBidAmount: 2.20,
      clearingPrice: 1.75,
      computedRankScore: 0.748,
      executedAt: '2026-11-03T01:50:00Z'
    },
    {
      auctionId: 'AUC-2026-8003',
      slotType: 'CheckoutCrossSellWidget',
      targetKeyword: 'usb c fast charging cable',
      winningMerchantName: 'VoltPower Tech',
      winningSku: 'SKU-VOLT-USB100W',
      winningBidAmount: 1.80,
      clearingPrice: 1.35,
      computedRankScore: 0.690,
      executedAt: '2026-11-03T01:40:00Z'
    }
  ]);

  readonly lastAuctionNotice = signal<string | null>(null);

  readonly totalRevenue = computed(() =>
    this.auctions().reduce((sum, a) => sum + (a.clearingPrice * 1250), 0)
  );

  readonly avgClearingCpc = computed(() => {
    const list = this.auctions();
    return list.length > 0 ? list.reduce((sum, a) => sum + a.clearingPrice, 0) / list.length : 2.0;
  });

  simulateAuction(): void {
    const newAuction: AuctionResult = {
      auctionId: `AUC-2026-${Math.random().toString(36).substring(2, 7).toUpperCase()}`,
      slotType: 'SearchTopBanner',
      targetKeyword: 'smart fitness smartwatch',
      winningMerchantName: 'ApexWear Health',
      winningSku: 'SKU-APEX-FIT3',
      winningBidAmount: 4.10,
      clearingPrice: 3.25,
      computedRankScore: 0.915,
      executedAt: new Date().toISOString()
    };

    this.auctions.update(list => [newAuction, ...list]);
    this.lastAuctionNotice.set(`Simulated GSP Auction: ${newAuction.winningMerchantName} won '${newAuction.targetKeyword}' slot at \$${newAuction.clearingPrice.toFixed(2)} CPC.`);
  }
}
