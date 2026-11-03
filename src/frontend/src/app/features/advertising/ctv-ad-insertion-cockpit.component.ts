import { Component, signal, computed } from '@angular/core';
import { CommonModule } from '@angular/common';

export interface CtvPodItem {
  podId: string;
  channelName: string;
  showTitle: string;
  adDurationSeconds: number;
  floorCpm: number;
  activeBiddersCount: number;
  qrCodeSessionId: string;
  status: string;
}

export interface QrAttributionItem {
  eventId: string;
  qrCodeSessionId: string;
  broadcastSessionId: string;
  mobileDeviceType: string;
  checkoutAmount: number;
  conversionStatus: string;
}

@Component({
  selector: 'app-ctv-ad-insertion-cockpit',
  standalone: true,
  imports: [CommonModule],
  template: `
    <div class="p-6 max-w-7xl mx-auto space-y-6" dir="ltr" data-testid="ctv-ad-insertion-cockpit">
      <!-- Header -->
      <div class="flex flex-col md:flex-row justify-between items-start md:items-center gap-4 bg-slate-900 border border-slate-800 p-6 rounded-2xl shadow-xl">
        <div>
          <div class="flex items-center gap-3">
            <span class="px-3 py-1 text-xs font-semibold rounded-full bg-indigo-500/20 text-indigo-400 border border-indigo-500/30">
              Horizon 107 • Domain D19 & C15
            </span>
            <span class="text-xs text-slate-400">VAST/VMAP Dynamic Ad Pods &amp; Dual-Screen Attribution</span>
          </div>
          <h1 class="text-2xl font-bold text-white mt-2">Connected TV (CTV) Ad Insertion &amp; QR Attribution</h1>
          <p class="text-slate-400 text-sm mt-1">
            Programmatic video ad insertion, frame-synchronized QR watermark generation, and cross-device checkout matching.
          </p>
        </div>

        <div class="flex items-center gap-3">
          <button
            (click)="simulateBidClearing()"
            data-testid="simulate-bid-btn"
            class="px-4 py-2 bg-indigo-600 hover:bg-indigo-500 text-white text-sm font-medium rounded-xl transition shadow-lg shadow-indigo-600/30 flex items-center gap-2">
            Simulate RTB Bid Clearing
          </button>
          <button
            (click)="simulateQrScan()"
            data-testid="simulate-qr-scan-btn"
            class="px-4 py-2 bg-emerald-600 hover:bg-emerald-500 text-white text-sm font-medium rounded-xl transition shadow-lg shadow-emerald-600/30 flex items-center gap-2">
            Ingest Mobile QR Scan
          </button>
        </div>
      </div>

      <!-- Metrics -->
      <div class="grid grid-cols-1 md:grid-cols-4 gap-4">
        <div class="bg-slate-900/80 border border-slate-800 p-5 rounded-2xl">
          <span class="text-xs font-medium text-slate-400">Active CTV Ad Pods</span>
          <p class="text-2xl font-bold text-white mt-1" data-testid="metric-active-pods">{{ activePods() }}</p>
          <span class="text-xs text-emerald-400 mt-1 block">Roku, AppleTV, FireTV, Tizen</span>
        </div>

        <div class="bg-slate-900/80 border border-slate-800 p-5 rounded-2xl">
          <span class="text-xs font-medium text-slate-400">Average Clearing CPM</span>
          <p class="text-2xl font-bold text-indigo-400 mt-1" data-testid="metric-clearing-cpm">\${{ averageCpm() | number:'1.2-2' }}</p>
          <span class="text-xs text-indigo-300 mt-1 block">VAST 4.2 Sealed-Bid Auction</span>
        </div>

        <div class="bg-slate-900/80 border border-slate-800 p-5 rounded-2xl">
          <span class="text-xs font-medium text-slate-400">Impressions Today</span>
          <p class="text-2xl font-bold text-white mt-1" data-testid="metric-impressions">{{ totalImpressions() | number }}</p>
          <span class="text-xs text-emerald-400 mt-1 block">+18.4% WoW Velocity</span>
        </div>

        <div class="bg-slate-900/80 border border-slate-800 p-5 rounded-2xl">
          <span class="text-xs font-medium text-slate-400">QR Scan Conversion Rate</span>
          <p class="text-2xl font-bold text-emerald-400 mt-1" data-testid="metric-qr-conversion">{{ qrConversionRate() }}%</p>
          <span class="text-xs text-slate-400 mt-1 block">Dual-Screen Attribution Latency &lt; 1s</span>
        </div>
      </div>

      <!-- Active Pods Table -->
      <div class="bg-slate-900/80 border border-slate-800 rounded-2xl p-6">
        <h2 class="text-lg font-bold text-white mb-4">Live Broadcast Ad Pod Inventory</h2>
        <div class="overflow-x-auto">
          <table class="w-full text-left text-sm text-slate-300">
            <thead class="text-xs uppercase bg-slate-800/60 text-slate-400 border-b border-slate-700">
              <tr>
                <th class="py-3 px-4">Channel / Stream</th>
                <th class="py-3 px-4">Show Title</th>
                <th class="py-3 px-4">Duration</th>
                <th class="py-3 px-4">Floor CPM</th>
                <th class="py-3 px-4">Active Bidders</th>
                <th class="py-3 px-4">QR Session</th>
                <th class="py-3 px-4 text-right">Status</th>
              </tr>
            </thead>
            <tbody class="divide-y divide-slate-800">
              @for (pod of pods(); track pod.podId) {
                <tr class="hover:bg-slate-800/30 transition">
                  <td class="py-3 px-4 font-semibold text-white">{{ pod.channelName }}</td>
                  <td class="py-3 px-4 text-slate-300">{{ pod.showTitle }}</td>
                  <td class="py-3 px-4 text-slate-400">{{ pod.adDurationSeconds }}s</td>
                  <td class="py-3 px-4 text-indigo-300 font-semibold">\${{ pod.floorCpm | number:'1.2-2' }}</td>
                  <td class="py-3 px-4 text-slate-200">{{ pod.activeBiddersCount }} DSPs</td>
                  <td class="py-3 px-4 font-mono text-xs text-amber-300">{{ pod.qrCodeSessionId }}</td>
                  <td class="py-3 px-4 text-right">
                    <span [class]="pod.status === 'Streaming' ? 'px-2 py-1 text-xs rounded-full bg-emerald-500/20 text-emerald-400 border border-emerald-500/30 font-medium' : 'px-2 py-1 text-xs rounded-full bg-indigo-500/20 text-indigo-400 border border-indigo-500/30 font-medium'">
                      {{ pod.status }}
                    </span>
                  </td>
                </tr>
              }
            </tbody>
          </table>
        </div>
      </div>

      <!-- Attributions Table -->
      <div class="bg-slate-900/80 border border-slate-800 rounded-2xl p-6">
        <h2 class="text-lg font-bold text-white mb-4">Recent Dual-Screen QR Attributions</h2>
        <div class="overflow-x-auto">
          <table class="w-full text-left text-sm text-slate-300">
            <thead class="text-xs uppercase bg-slate-800/60 text-slate-400 border-b border-slate-700">
              <tr>
                <th class="py-3 px-4">Event ID</th>
                <th class="py-3 px-4">QR Session</th>
                <th class="py-3 px-4">Broadcast Fingerprint</th>
                <th class="py-3 px-4">Device</th>
                <th class="py-3 px-4 text-right">Attributed Revenue</th>
                <th class="py-3 px-4 text-right">Conversion State</th>
              </tr>
            </thead>
            <tbody class="divide-y divide-slate-800">
              @for (attr of attributions(); track attr.eventId) {
                <tr class="hover:bg-slate-800/30 transition">
                  <td class="py-3 px-4 font-mono text-xs text-slate-400">{{ attr.eventId }}</td>
                  <td class="py-3 px-4 font-mono text-xs text-indigo-300">{{ attr.qrCodeSessionId }}</td>
                  <td class="py-3 px-4 text-slate-300">{{ attr.broadcastSessionId }}</td>
                  <td class="py-3 px-4 text-slate-400">{{ attr.mobileDeviceType }}</td>
                  <td class="py-3 px-4 text-right font-bold text-emerald-400">\${{ attr.checkoutAmount | number:'1.2-2' }}</td>
                  <td class="py-3 px-4 text-right">
                    <span class="px-2 py-0.5 text-xs rounded bg-emerald-500/20 text-emerald-300 font-medium">
                      {{ attr.conversionStatus }}
                    </span>
                  </td>
                </tr>
              }
            </tbody>
          </table>
        </div>
      </div>
    </div>
  `
})
export class CtvAdInsertionCockpitComponent {
  activePods = signal<number>(4);
  averageCpm = signal<number>(24.00);
  totalImpressions = signal<number>(425000);
  qrConversionRate = signal<number>(4.85);

  pods = signal<CtvPodItem[]>([
    { podId: 'POD-ROKU-881', channelName: 'Bloomberg TV+', showTitle: 'Market Open Live', adDurationSeconds: 30, floorCpm: 24.50, activeBiddersCount: 6, qrCodeSessionId: 'QR-SESS-9921', status: 'Active' },
    { podId: 'POD-APPLE-402', channelName: 'Paramount+ Sports', showTitle: 'Champions League Pre-Show', adDurationSeconds: 15, floorCpm: 32.00, activeBiddersCount: 8, qrCodeSessionId: 'QR-SESS-8814', status: 'Streaming' },
    { podId: 'POD-FIRE-519', channelName: 'Peacock News', showTitle: 'Global Tech Hour', adDurationSeconds: 30, floorCpm: 21.00, activeBiddersCount: 5, qrCodeSessionId: 'QR-SESS-7731', status: 'Active' },
    { podId: 'POD-TIZEN-109', channelName: 'Samsung TV Plus', showTitle: 'Automotive Weekly', adDurationSeconds: 15, floorCpm: 18.50, activeBiddersCount: 4, qrCodeSessionId: 'QR-SESS-6612', status: 'Filled' }
  ]);

  attributions = signal<QrAttributionItem[]>([
    { eventId: 'ATTR-901', qrCodeSessionId: 'QR-SESS-9921', broadcastSessionId: 'BCST-NYC-2026', mobileDeviceType: 'iOS', checkoutAmount: 480.00, conversionStatus: 'AttributedPurchase' },
    { eventId: 'ATTR-902', qrCodeSessionId: 'QR-SESS-8814', broadcastSessionId: 'BCST-LON-2026', mobileDeviceType: 'Android', checkoutAmount: 1250.00, conversionStatus: 'AttributedPurchase' },
    { eventId: 'ATTR-903', qrCodeSessionId: 'QR-SESS-7731', broadcastSessionId: 'BCST-SFO-2026', mobileDeviceType: 'iOS', checkoutAmount: 180.00, conversionStatus: 'PendingCheckout' }
  ]);

  simulateBidClearing(): void {
    this.averageCpm.set(27.50);
    this.totalImpressions.update(v => v + 15000);
  }

  simulateQrScan(): void {
    const newAttr: QrAttributionItem = {
      eventId: `ATTR-${Math.floor(Math.random() * 900 + 100)}`,
      qrCodeSessionId: 'QR-SESS-9921',
      broadcastSessionId: 'BCST-NYC-2026',
      mobileDeviceType: 'iOS',
      checkoutAmount: 640.00,
      conversionStatus: 'AttributedPurchase'
    };
    this.attributions.update(list => [newAttr, ...list]);
  }
}
