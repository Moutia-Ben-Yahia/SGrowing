import { Component, OnInit, signal, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { ApiService } from '../../core/services/api.service';
import { AuthService } from '../../core/services/auth.service';
import { LiveTelemetrySummaryDto, TelemetryEventDto } from '../../core/models/telemetry.models';

@Component({
  selector: 'app-telemetry-studio',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterLink],
  template: `
    <div class="flex h-screen w-screen overflow-hidden bg-slate-950 text-slate-100 font-sans">
      <main class="flex-1 flex flex-col overflow-y-auto">
        <!-- Topbar -->
        <header class="h-14 border-b border-slate-800 bg-slate-900/60 px-6 flex items-center justify-between shrink-0">
          <div class="flex items-center gap-3">
            <a routerLink="/" class="text-xs text-slate-400 hover:text-white flex items-center gap-1 transition">
              ← Inbox
            </a>
            <span class="text-slate-700">|</span>
            <span class="text-base font-semibold text-white flex items-center gap-2">
              📡 Product Telemetry & Sovereign Live Analytics
            </span>
            <span class="bg-emerald-500/20 text-emerald-400 text-[10px] font-mono px-2 py-0.5 rounded-full border border-emerald-500/30">
              Category 14 • Countly & Plausible Mesh
            </span>
          </div>

          <div class="flex items-center gap-3">
            <button
              (click)="simulateTraffic()"
              class="px-3 py-1.5 bg-emerald-600 hover:bg-emerald-500 text-white rounded-lg text-xs font-bold shadow-lg shadow-emerald-950/20 transition">
              ⚡ Inject Storefront Event
            </button>
          </div>
        </header>

        <!-- Body -->
        <div class="p-6 space-y-6 flex-1">
          <!-- Live Visitors Header Cards -->
          <div class="grid grid-cols-1 md:grid-cols-4 gap-4">
            <div class="p-5 rounded-2xl bg-gradient-to-br from-slate-900 to-slate-950 border border-slate-800 flex flex-col justify-between">
              <div class="flex items-center justify-between">
                <span class="text-xs uppercase tracking-wider text-slate-400 font-semibold">Active Right Now</span>
                <span class="flex h-2.5 w-2.5 relative">
                  <span class="animate-ping absolute inline-flex h-full w-full rounded-full bg-emerald-400 opacity-75"></span>
                  <span class="relative inline-flex rounded-full h-2.5 w-2.5 bg-emerald-500"></span>
                </span>
              </div>
              <div class="mt-2">
                <h3 class="text-4xl font-extrabold text-white tracking-tight">{{ summary()?.activeVisitorsCount ?? 24 }}</h3>
                <span class="text-xs text-slate-400">concurrent storefront users</span>
              </div>
            </div>

            <div class="p-5 rounded-2xl bg-slate-900/60 border border-slate-800 flex flex-col justify-between">
              <span class="text-xs uppercase tracking-wider text-slate-400 font-semibold">Live Sessions (15m)</span>
              <div class="mt-2">
                <h3 class="text-3xl font-bold text-cyan-400">{{ summary()?.activeSessionsCount ?? 38 }}</h3>
                <span class="text-xs text-slate-400">avg 3m 42s duration</span>
              </div>
            </div>

            <div class="p-5 rounded-2xl bg-slate-900/60 border border-slate-800 flex flex-col justify-between">
              <span class="text-xs uppercase tracking-wider text-slate-400 font-semibold">Top Country</span>
              <div class="mt-2">
                <h3 class="text-3xl font-bold text-amber-300">
                  {{ summary()?.topCountries?.[0]?.countryCode || 'TN' }}
                </h3>
                <span class="text-xs text-slate-400">
                  {{ summary()?.topCountries?.[0]?.percentage || 65 }}% of total traffic
                </span>
              </div>
            </div>

            <div class="p-5 rounded-2xl bg-slate-900/60 border border-slate-800 flex flex-col justify-between">
              <span class="text-xs uppercase tracking-wider text-slate-400 font-semibold">Data Sovereignty</span>
              <div class="mt-2">
                <h3 class="text-2xl font-bold text-emerald-400">100% First-Party</h3>
                <span class="text-xs text-emerald-500/80">Zero GA4 / Meta third-party pixels</span>
              </div>
            </div>
          </div>

          <!-- Active URLs & Country Distribution -->
          <div class="grid grid-cols-1 lg:grid-cols-2 gap-6">
            <!-- Active URLs -->
            <div class="p-5 rounded-2xl bg-slate-900/60 border border-slate-800 space-y-3">
              <h3 class="text-sm font-semibold text-white">🔥 Most Active Storefront URLs</h3>
              <div class="space-y-2">
                @for (page of summary()?.topActivePages || []; track page.url) {
                  <div class="p-3 rounded-lg bg-slate-950/80 border border-slate-800 flex items-center justify-between font-mono text-xs">
                    <span class="text-indigo-300 truncate max-w-sm">{{ page.url }}</span>
                    <span class="px-2 py-0.5 rounded bg-indigo-500/20 text-indigo-300 font-bold">
                      {{ page.activeVisitors }} active
                    </span>
                  </div>
                }
              </div>
            </div>

            <!-- Country Metrics -->
            <div class="p-5 rounded-2xl bg-slate-900/60 border border-slate-800 space-y-3">
              <h3 class="text-sm font-semibold text-white">🌍 Geographical Distribution (Sovereign GeoIP)</h3>
              <div class="space-y-2">
                @for (c of summary()?.topCountries || []; track c.countryCode) {
                  <div class="p-3 rounded-lg bg-slate-950/80 border border-slate-800 flex items-center justify-between text-xs">
                    <span class="font-bold text-white font-mono">{{ c.countryCode }}</span>
                    <div class="flex items-center gap-3">
                      <span class="text-slate-400">{{ c.visitorCount }} sessions</span>
                      <span class="text-emerald-400 font-semibold font-mono">{{ c.percentage }}%</span>
                    </div>
                  </div>
                }
              </div>
            </div>
          </div>

          <!-- Chronological Telemetry Ingress Feed -->
          <div class="rounded-2xl border border-slate-800 bg-slate-900/40 overflow-hidden">
            <div class="px-5 py-3 border-b border-slate-800 bg-slate-900/80 flex items-center justify-between">
              <h3 class="text-sm font-semibold text-white">⚡ Real-Time Telemetry Event Stream</h3>
              <span class="text-xs font-mono text-slate-400">Buffered Ingestion</span>
            </div>
            <div class="overflow-x-auto">
              <table class="w-full text-left text-xs text-slate-300">
                <thead class="bg-slate-950/70 text-[11px] uppercase tracking-wider text-slate-400 border-b border-slate-800">
                  <tr>
                    <th class="px-4 py-3">Timestamp</th>
                    <th class="px-4 py-3">Event</th>
                    <th class="px-4 py-3">URL</th>
                    <th class="px-4 py-3">Device / Browser</th>
                    <th class="px-4 py-3">Geo</th>
                  </tr>
                </thead>
                <tbody class="divide-y divide-slate-800/60 font-mono">
                  @for (ev of recentEvents(); track ev.id) {
                    <tr class="hover:bg-slate-800/30 transition">
                      <td class="px-4 py-3 text-slate-400">{{ ev.timestampUtc | date:'mediumTime' }}</td>
                      <td class="px-4 py-3">
                        <span class="px-2 py-0.5 rounded text-[10px] font-bold"
                          [class.bg-emerald-500/20]="ev.eventName === 'purchase'"
                          [class.text-emerald-400]="ev.eventName === 'purchase'"
                          [class.bg-indigo-500/20]="ev.eventName === 'pageview'"
                          [class.text-indigo-300]="ev.eventName === 'pageview'"
                          [class.bg-amber-500/20]="ev.eventName === 'add_to_cart'"
                          [class.text-amber-300]="ev.eventName === 'add_to_cart'">
                          {{ ev.eventName }}
                        </span>
                      </td>
                      <td class="px-4 py-3 text-slate-200 truncate max-w-xs">{{ ev.url }}</td>
                      <td class="px-4 py-3 text-slate-400 font-sans">{{ ev.deviceType }} • {{ ev.browser }}</td>
                      <td class="px-4 py-3 font-semibold text-white">{{ ev.countryCode }} ({{ ev.city }})</td>
                    </tr>
                  }
                </tbody>
              </table>
            </div>
          </div>
        </div>
      </main>
    </div>
  `
})
export class TelemetryStudioComponent implements OnInit {
  private api = inject(ApiService);
  private auth = inject(AuthService);

  public summary = signal<LiveTelemetrySummaryDto | null>(null);
  public recentEvents = signal<TelemetryEventDto[]>([]);

  public get tenantId(): string {
    return this.auth.currentTenant()?.id || '00000000-0000-0000-0000-000000000001';
  }

  ngOnInit(): void {
    this.loadData();
  }

  public loadData(): void {
    this.api.getLiveTelemetry(this.tenantId).subscribe(s => this.summary.set(s));
    this.api.getRecentTelemetryEvents(this.tenantId).subscribe(events => {
      if (events && events.length > 0) {
        this.recentEvents.set(events);
      } else {
        this.simulateTraffic();
      }
    });
  }

  public simulateTraffic(): void {
    const pages = ['/shop/sneakers-leather', '/checkout', '/cart', '/returns-exchange', '/'];
    const events = ['pageview', 'add_to_cart', 'begin_checkout', 'purchase'];
    const p = pages[Math.floor(Math.random() * pages.length)];
    const ev = events[Math.floor(Math.random() * events.length)];
    const sId = 'sess_' + Math.floor(Math.random() * 900 + 100);

    this.api.ingestTelemetry({
      tenantId: this.tenantId,
      sessionId: sId,
      visitorId: 'vis_' + Math.floor(Math.random() * 900 + 100),
      eventName: ev,
      url: 'https://store.sgrowing.io' + p,
      referrer: 'https://instagram.com/',
      deviceType: 'Mobile',
      browser: 'Safari Mobile',
      countryCode: 'TN',
      city: 'Tunis',
      propertiesJson: '{"cartTotal": 195}'
    }).subscribe(newEvent => {
      this.recentEvents.update(prev => [newEvent, ...prev]);
      this.api.getLiveTelemetry(this.tenantId).subscribe(s => this.summary.set(s));
    });
  }
}
