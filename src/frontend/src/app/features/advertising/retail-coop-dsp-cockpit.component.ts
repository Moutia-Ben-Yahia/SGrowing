import { Component, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';

export interface RetailerCleanRoomPartnerRecord {
  cleanRoomPartnerId: string;
  retailNetworkName: string;
  cleanRoomProvider: string;
  verifiedShopperIdentitiesCount: number;
  overlapAccuracyPct: number;
}

export interface OffSiteCoopCampaignRecord {
  campaignId: string;
  brandName: string;
  retailPartnerId: string;
  totalCoopBudgetUsd: number;
  brandContributionUsd: number;
  retailerCoopCreditUsd: number;
  targetAudienceSegment: string;
  channels: string;
  projectedRoasMultiplier: number;
  status: string;
}

@Component({
  selector: 'app-retail-coop-dsp-cockpit',
  standalone: true,
  imports: [CommonModule, FormsModule],
  template: `
    <div dir="ltr" data-testid="retail-coop-dsp-cockpit" class="p-6 bg-slate-900 text-slate-100 min-h-screen">
      <header class="mb-8 border-b border-slate-800 pb-4 flex justify-between items-center">
        <div>
          <h1 class="text-2xl font-bold tracking-tight text-white flex items-center gap-2">
            <span class="inline-block w-3 h-3 rounded-full bg-blue-500 animate-pulse"></span>
            Retail Media Network Off-Site Co-Op DSP & Clean Room Mesh
          </h1>
          <p class="text-slate-400 text-sm mt-1">
            Closed-Loop Off-Site Programmatic Buying, Cryptographic Retail Clean Room Identity Joins & In-Store Attribution
          </p>
        </div>
        <div class="flex gap-3">
          <button (click)="launchCoopCampaign()" class="px-4 py-2 bg-blue-600 hover:bg-blue-500 text-white font-medium rounded-lg shadow-sm transition">
            Launch Off-Site Co-Op Flight
          </button>
        </div>
      </header>

      <!-- KPI Metrics Grid -->
      <section class="grid grid-cols-1 md:grid-cols-4 gap-4 mb-8">
        <div class="bg-slate-800/60 border border-slate-700/50 p-4 rounded-xl">
          <span class="text-xs font-semibold text-slate-400 uppercase tracking-wider">Clean Rooms</span>
          <p class="text-2xl font-extrabold text-white mt-1">{{ cleanRooms().length }}</p>
          <span class="text-xs text-blue-400">Snowflake & AWS Clean Rooms</span>
        </div>
        <div class="bg-slate-800/60 border border-slate-700/50 p-4 rounded-xl">
          <span class="text-xs font-semibold text-slate-400 uppercase tracking-wider">Active Co-Op Spend</span>
          <p class="text-2xl font-extrabold text-white mt-1">$195K</p>
          <span class="text-xs text-emerald-400">50% Retailer Matched</span>
        </div>
        <div class="bg-slate-800/60 border border-slate-700/50 p-4 rounded-xl">
          <span class="text-xs font-semibold text-slate-400 uppercase tracking-wider">Blended ROAS</span>
          <p class="text-2xl font-extrabold text-white mt-1">{{ blendedRoas() }}x</p>
          <span class="text-xs text-cyan-400">Closed-Loop Verified</span>
        </div>
        <div class="bg-slate-800/60 border border-slate-700/50 p-4 rounded-xl">
          <span class="text-xs font-semibold text-slate-400 uppercase tracking-wider">Shopper Reach</span>
          <p class="text-2xl font-extrabold text-white mt-1">355M</p>
          <span class="text-xs text-indigo-400">First-Party Verified</span>
        </div>
      </section>

      <!-- Main Grid -->
      <div class="grid grid-cols-1 lg:grid-cols-2 gap-8">
        <!-- Clean Room Connections -->
        <section class="bg-slate-800/40 border border-slate-700/60 rounded-xl p-6">
          <h2 class="text-lg font-semibold text-white mb-4">Retail Clean Room Enclaves</h2>
          <div class="space-y-4">
            <div *ngFor="let cr of cleanRooms()" class="bg-slate-900/60 border border-slate-700/40 p-4 rounded-lg">
              <div class="flex justify-between items-center mb-1">
                <span class="font-bold text-white">{{ cr.retailNetworkName }}</span>
                <span class="text-xs bg-blue-900/40 text-blue-300 border border-blue-700/60 px-2 py-0.5 rounded font-mono">
                  {{ cr.cleanRoomProvider }}
                </span>
              </div>
              <p class="text-xs text-slate-400 mb-2">Partner ID: {{ cr.cleanRoomPartnerId }}</p>
              <div class="flex justify-between text-xs text-slate-300">
                <span>Verified Shoppers: {{ cr.verifiedShopperIdentitiesCount | number }}</span>
                <span class="text-emerald-400 font-mono font-bold">{{ cr.overlapAccuracyPct }}% Overlap Accuracy</span>
              </div>
            </div>
          </div>
        </section>

        <!-- Co-Op Campaigns -->
        <section class="bg-slate-800/40 border border-slate-700/60 rounded-xl p-6">
          <h2 class="text-lg font-semibold text-white mb-4">Active Off-Site Co-Op Flights</h2>
          <div class="space-y-4">
            <div *ngFor="let c of campaigns()" class="bg-slate-900/60 border border-slate-700/40 p-4 rounded-lg">
              <div class="flex justify-between items-center mb-1">
                <span class="font-bold text-blue-400">{{ c.brandName }}</span>
                <span class="text-xs text-emerald-400 font-bold font-mono">{{ c.projectedRoasMultiplier }}x ROAS</span>
              </div>
              <p class="text-xs text-slate-300 mb-1">Segment: {{ c.targetAudienceSegment }}</p>
              <p class="text-xs text-slate-400 mb-2">Channels: {{ c.channels }}</p>
              <div class="flex justify-between text-xs font-mono bg-slate-950 p-2 rounded text-slate-300">
                <span>Brand: {{ c.brandContributionUsd | currency }}</span>
                <span class="text-blue-300">Co-Op Credit: {{ c.retailerCoopCreditUsd | currency }}</span>
                <span class="text-emerald-300 font-bold">Total: {{ c.totalCoopBudgetUsd | currency }}</span>
              </div>
            </div>
          </div>
        </section>
      </div>
    </div>
  `
})
export class RetailCoopDspCockpitComponent {
  cleanRooms = signal<RetailerCleanRoomPartnerRecord[]>([
    { cleanRoomPartnerId: 'CR-WALMART-01', retailNetworkName: 'Walmart Luminate / Connect Clean Room', cleanRoomProvider: 'Snowflake Clean Rooms', verifiedShopperIdentitiesCount: 145000000, overlapAccuracyPct: 98.8 },
    { cleanRoomPartnerId: 'CR-AMAZON-02', retailNetworkName: 'Amazon Marketing Cloud (AMC) Hub', cleanRoomProvider: 'AWS Clean Rooms', verifiedShopperIdentitiesCount: 210000000, overlapAccuracyPct: 99.2 }
  ]);

  campaigns = signal<OffSiteCoopCampaignRecord[]>([
    { campaignId: 'COOP-CMP-801', brandName: 'BioActive Organic Nutrients', retailPartnerId: 'CR-WALMART-01', totalCoopBudgetUsd: 120000, brandContributionUsd: 80000, retailerCoopCreditUsd: 40000, targetAudienceSegment: 'Verified Past 90-Day Organic Buyers', channels: 'CTV & High-Impact Display', projectedRoasMultiplier: 4.65, status: 'PACING_OPTIMAL' },
    { campaignId: 'COOP-CMP-802', brandName: 'HyperHydrate Electrolytes', retailPartnerId: 'CR-AMAZON-02', totalCoopBudgetUsd: 75000, brandContributionUsd: 50000, retailerCoopCreditUsd: 25000, targetAudienceSegment: 'Prime Sports & Fitness Shoppers', channels: 'Open Web Video & Audio', projectedRoasMultiplier: 5.20, status: 'PACING_OPTIMAL' }
  ]);

  blendedRoas = signal(4.92);

  launchCoopCampaign() {
    const newCamp: OffSiteCoopCampaignRecord = {
      campaignId: `COOP-${Date.now().toString().slice(-4)}`,
      brandName: 'Quantum Power Snacks',
      retailPartnerId: 'CR-WALMART-01',
      totalCoopBudgetUsd: 90000,
      brandContributionUsd: 60000,
      retailerCoopCreditUsd: 30000,
      targetAudienceSegment: 'Verified Health Snack Reorder Shoppers',
      channels: 'Programmatic CTV & DOOH',
      projectedRoasMultiplier: 5.10,
      status: 'ACTIVATED_COOP_BIDDING'
    };
    this.campaigns.update(list => [newCamp, ...list]);
  }
}
