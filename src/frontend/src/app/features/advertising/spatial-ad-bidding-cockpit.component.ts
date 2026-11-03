import { Component, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';

export interface SpatialPlacementZone {
  zoneId: string;
  virtualWorldEngine: string;
  placementType: string;
  textureResolution: string;
  floorCpmUsd: number;
}

export interface SpatialBidRecord {
  bidId: string;
  zoneId: string;
  advertiserName: string;
  winningBidCpmUsd: number;
  cameraDistanceUnits: number;
  gazeDwellTimeSeconds: number;
  renderingStatus: string;
}

@Component({
  selector: 'app-spatial-ad-bidding-cockpit',
  standalone: true,
  imports: [CommonModule, FormsModule],
  template: `
    <div dir="ltr" data-testid="spatial-ad-bidding-cockpit" class="p-6 bg-slate-900 text-slate-100 min-h-screen">
      <header class="mb-8 border-b border-slate-800 pb-4 flex justify-between items-center">
        <div>
          <h1 class="text-2xl font-bold tracking-tight text-white flex items-center gap-2">
            <span class="inline-block w-3 h-3 rounded-full bg-violet-500 animate-pulse"></span>
            In-Game & Metaverse 3D Spatial Ad Bidding Mesh
          </h1>
          <p class="text-slate-400 text-sm mt-1">
            Real-Time 3D Camera Frustum Bidding, PBR Lighting Adaptation & Dwell Gaze Verification Engine
          </p>
        </div>
        <div class="flex gap-3">
          <button (click)="simulateSpatialBid()" class="px-4 py-2 bg-violet-600 hover:bg-violet-500 text-white font-medium rounded-lg shadow-sm transition">
            Simulate Spatial RTB Bid
          </button>
        </div>
      </header>

      <!-- KPI Metrics Grid -->
      <section class="grid grid-cols-1 md:grid-cols-4 gap-4 mb-8">
        <div class="bg-slate-800/60 border border-slate-700/50 p-4 rounded-xl">
          <span class="text-xs font-semibold text-slate-400 uppercase tracking-wider">Spatial Placements</span>
          <p class="text-2xl font-extrabold text-white mt-1">{{ zones().length }}</p>
          <span class="text-xs text-violet-400">UE5 Nanite & Unity 6</span>
        </div>
        <div class="bg-slate-800/60 border border-slate-700/50 p-4 rounded-xl">
          <span class="text-xs font-semibold text-slate-400 uppercase tracking-wider">Daily RTB Volume</span>
          <p class="text-2xl font-extrabold text-indigo-400 mt-1">184K</p>
          <span class="text-xs text-slate-400">Sub-30ms Evaluations</span>
        </div>
        <div class="bg-slate-800/60 border border-slate-700/50 p-4 rounded-xl">
          <span class="text-xs font-semibold text-slate-400 uppercase tracking-wider">Bid Win Rate</span>
          <p class="text-2xl font-extrabold text-emerald-400 mt-1">{{ bidWinRatePct() }}%</p>
          <span class="text-xs text-slate-400">Dynamic Gaze Pricing</span>
        </div>
        <div class="bg-slate-800/60 border border-slate-700/50 p-4 rounded-xl">
          <span class="text-xs font-semibold text-slate-400 uppercase tracking-wider">P99 Spatial Latency</span>
          <p class="text-2xl font-extrabold text-cyan-400 mt-1">18.5ms</p>
          <span class="text-xs text-emerald-400">0 Frame Drops in Engine</span>
        </div>
      </section>

      <!-- Main Panels -->
      <div class="grid grid-cols-1 lg:grid-cols-3 gap-6">
        <!-- Zones -->
        <section class="bg-slate-800/40 border border-slate-700/50 rounded-xl p-5">
          <h2 class="text-lg font-semibold text-white mb-4">Virtual 3D Inventory Zones</h2>
          <div class="space-y-3">
            <div *ngFor="let z of zones()" class="p-3 bg-slate-900/60 border border-slate-700/40 rounded-lg">
              <div class="flex justify-between items-center mb-1">
                <span class="font-medium text-slate-200 text-sm">{{ z.placementType }}</span>
                <span class="px-2 py-0.5 text-xs bg-violet-500/20 text-violet-300 rounded font-semibold font-mono">\${{ z.floorCpmUsd }} CPM</span>
              </div>
              <p class="text-xs text-slate-400 mb-1">Engine: {{ z.virtualWorldEngine }}</p>
              <div class="text-[10px] font-mono text-cyan-400 bg-slate-950 p-1.5 rounded">
                Res: {{ z.textureResolution }}
              </div>
            </div>
          </div>
        </section>

        <!-- Bids Table -->
        <section class="lg:col-span-2 bg-slate-800/40 border border-slate-700/50 rounded-xl p-5">
          <h2 class="text-lg font-semibold text-white mb-4">Real-Time Spatial RTB Ledger</h2>
          <div class="overflow-x-auto">
            <table class="w-full text-left border-collapse text-sm">
              <thead>
                <tr class="border-b border-slate-700 text-slate-400 text-xs">
                  <th class="pb-3 font-medium">Bid ID</th>
                  <th class="pb-3 font-medium">Advertiser</th>
                  <th class="pb-3 font-medium">Winning CPM</th>
                  <th class="pb-3 font-medium">Distance</th>
                  <th class="pb-3 font-medium">Gaze Dwell</th>
                  <th class="pb-3 font-medium">Render Status</th>
                </tr>
              </thead>
              <tbody class="divide-y divide-slate-800">
                <tr *ngFor="let b of bids()" class="hover:bg-slate-800/50 transition">
                  <td class="py-3 font-mono text-xs text-violet-300 font-semibold">{{ b.bidId }}</td>
                  <td class="py-3 font-medium text-slate-200">{{ b.advertiserName }}</td>
                  <td class="py-3 text-emerald-400 font-mono font-bold">\${{ b.winningBidCpmUsd }}</td>
                  <td class="py-3 text-slate-400 font-mono text-xs">{{ b.cameraDistanceUnits }}u</td>
                  <td class="py-3 text-cyan-300 font-mono text-xs">{{ b.gazeDwellTimeSeconds }}s</td>
                  <td class="py-3">
                    <span class="px-2 py-0.5 text-[10px] rounded font-semibold font-mono bg-emerald-500/20 text-emerald-300">
                      {{ b.renderingStatus }}
                    </span>
                  </td>
                </tr>
              </tbody>
            </table>
          </div>
        </section>
      </div>
    </div>
  `
})
export class SpatialAdBiddingCockpitComponent {
  zones = signal<SpatialPlacementZone[]>([
    { zoneId: 'ZONE-UE5-CYBERPUNK-01', virtualWorldEngine: 'Unreal Engine 5 (Nanite/Lumen)', placementType: 'Dynamic Cyberpunk Neon Hologram', textureResolution: '4096x2048 PBR Albedo/Normal/Emissive', floorCpmUsd: 12.50 },
    { zoneId: 'ZONE-UNITY-STADIUM-02', virtualWorldEngine: 'Unity 6 (URP)', placementType: 'Esports Stadium Perimeter LED Ribbon', textureResolution: '8192x1024 HDR Uncompressed', floorCpmUsd: 18.00 }
  ]);

  bids = signal<SpatialBidRecord[]>([
    { bidId: 'BID-SPAT-881', zoneId: 'ZONE-UE5-CYBERPUNK-01', advertiserName: 'Apex Cybernetics', winningBidCpmUsd: 15.80, cameraDistanceUnits: 24.5, gazeDwellTimeSeconds: 3.8, renderingStatus: 'PROJECTION_PBR_APPLIED' },
    { bidId: 'BID-SPAT-882', zoneId: 'ZONE-UNITY-STADIUM-02', advertiserName: 'Quantum Cloud Gaming', winningBidCpmUsd: 21.20, cameraDistanceUnits: 12.0, gazeDwellTimeSeconds: 5.2, renderingStatus: 'PROJECTION_PBR_APPLIED' }
  ]);

  bidWinRatePct = signal(64.2);

  simulateSpatialBid() {
    const newBid: SpatialBidRecord = {
      bidId: `BID-SPAT-${Date.now().toString().slice(-4)}`,
      zoneId: 'ZONE-UE5-CYBERPUNK-01',
      advertiserName: 'Hyperion Neural Tech',
      winningBidCpmUsd: 24.50,
      cameraDistanceUnits: 18.2,
      gazeDwellTimeSeconds: 4.1,
      renderingStatus: 'PROJECTION_PBR_APPLIED'
    };
    this.bids.update(list => [newBid, ...list]);
  }
}
