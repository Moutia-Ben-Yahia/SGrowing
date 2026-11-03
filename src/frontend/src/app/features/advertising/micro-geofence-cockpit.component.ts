import { Component, signal, ChangeDetectionStrategy } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';

export interface PolygonItem {
  polygonId: string;
  venueName: string;
  venueType: string;
  radiusMeters: number;
  bidMultiplier: number;
  isActive: boolean;
}

export interface FootfallItem {
  eventId: string;
  deviceAnonymousId: string;
  venueName: string;
  dwellTimeMinutes: number;
  attributedStoreVisit: boolean;
  incrementalValueGeneratedUsd: number;
}

@Component({
  selector: 'app-micro-geofence-cockpit',
  standalone: true,
  imports: [CommonModule, FormsModule],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="cockpit-container" dir="ltr" data-testid="micro-geofence-cockpit">
      <header class="cockpit-header">
        <div class="badge">Horizon 162 · Spatial DSP Ad Tech</div>
        <h1>Autonomous Real-Time Micro-Geofencing & Physical Footfall DSP Mesh</h1>
        <p class="subtitle">High-precision GPS polygon perimeters, sub-second RTB bid multipliers for venues, and deterministic footfall dwell store-visit attribution</p>
      </header>

      <section class="metrics-grid">
        <div class="metric-card" data-testid="metric-active-polygons">
          <div class="metric-label">Active Polygons</div>
          <div class="metric-value positive">{{ activePolygonsCount() }}</div>
          <div class="metric-sub">Armed high-intent physical venues</div>
        </div>
        <div class="metric-card" data-testid="metric-daily-dwells">
          <div class="metric-label">Daily Dwell Detections</div>
          <div class="metric-value">{{ dailyAudienceDwells() }}</div>
          <div class="metric-sub">Audience members inside target zones</div>
        </div>
        <div class="metric-card" data-testid="metric-conversion-lift">
          <div class="metric-label">Footfall Conversion Lift</div>
          <div class="metric-value positive">+{{ footfallLiftPct() }}%</div>
          <div class="metric-sub">Proximity programmatic ad impact</div>
        </div>
        <div class="metric-card" data-testid="metric-attributed-revenue">
          <div class="metric-label">Attributed Revenue</div>
          <div class="metric-value">\${{ (totalAttributedRevenue() / 1000).toFixed(1) }}k</div>
          <div class="metric-sub">Verified in-store & venue purchases</div>
        </div>
      </section>

      <section class="polygons-section">
        <div class="section-header">
          <h2>Active Micro-Geofencing Polygons</h2>
          <button class="action-btn" (click)="defineNewPolygon()" data-testid="define-polygon-btn">
            + Define Geofence Polygon
          </button>
        </div>
        <div class="table-wrapper">
          <table class="data-table" data-testid="polygons-table">
            <thead>
              <tr>
                <th>Polygon ID</th>
                <th>Venue Name</th>
                <th>Type</th>
                <th>Radius</th>
                <th>Bid Multiplier</th>
                <th>Status</th>
              </tr>
            </thead>
            <tbody>
              @for (p of polygons(); track p.polygonId) {
                <tr [attr.data-testid]="'polygon-row-' + p.polygonId">
                  <td><code>{{ p.polygonId }}</code></td>
                  <td class="primary-cell">
                    <strong>{{ p.venueName }}</strong>
                  </td>
                  <td><span class="venue-badge">{{ p.venueType }}</span></td>
                  <td>{{ p.radiusMeters }}m</td>
                  <td><strong>{{ p.bidMultiplier }}x</strong></td>
                  <td><span class="status-pill status-armed">ACTIVE</span></td>
                </tr>
              }
            </tbody>
          </table>
        </div>
      </section>

      <section class="footfall-section">
        <h2>Attributed Footfall Dwell Events</h2>
        <div class="events-grid" data-testid="events-grid">
          @for (e of events(); track e.eventId) {
            <div class="event-card" [attr.data-testid]="'event-card-' + e.eventId">
              <div class="e-header">
                <strong>{{ e.venueName }}</strong>
                <span class="dwell-badge">{{ e.dwellTimeMinutes }} mins</span>
              </div>
              <div class="e-body">
                <p><strong>Device:</strong> <code>{{ e.deviceAnonymousId }}</code></p>
                <p><strong>Store Visit:</strong> {{ e.attributedStoreVisit ? 'VERIFIED' : 'PASSING_BY' }}</p>
                <p><strong>Incremental Value:</strong> \${{ e.incrementalValueGeneratedUsd }}</p>
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
    .badge { display: inline-block; padding: 4px 10px; border-radius: 9999px; background: #e0f2fe; color: #0369a1; font-size: 12px; font-weight: 600; text-transform: uppercase; margin-bottom: 8px; }
    h1 { font-size: 26px; font-weight: 700; margin: 0 0 6px 0; color: #0f172a; }
    .subtitle { font-size: 14px; color: #64748b; margin: 0; }
    .metrics-grid { display: grid; grid-template-columns: repeat(auto-fit, minmax(240px, 1fr)); gap: 16px; margin-bottom: 28px; }
    .metric-card { background: #ffffff; border: 1px solid #e2e8f0; border-radius: 8px; padding: 18px; box-shadow: 0 1px 3px rgba(0,0,0,0.05); }
    .metric-label { font-size: 13px; font-weight: 500; color: #64748b; margin-bottom: 6px; }
    .metric-value { font-size: 28px; font-weight: 700; color: #0f172a; }
    .metric-value.positive { color: #16a34a; }
    .metric-sub { font-size: 12px; color: #94a3b8; margin-top: 4px; }
    .section-header { display: flex; justify-content: space-between; align-items: center; margin-bottom: 14px; }
    .action-btn { background: #0284c7; color: #ffffff; border: none; padding: 8px 14px; border-radius: 4px; font-size: 13px; font-weight: 600; cursor: pointer; }
    .table-wrapper { overflow-x: auto; background: #ffffff; border: 1px solid #e2e8f0; border-radius: 8px; }
    .data-table { width: 100%; border-collapse: collapse; text-align: left; font-size: 13px; }
    .data-table th, .data-table td { padding: 12px 16px; border-bottom: 1px solid #e2e8f0; }
    .data-table th { background: #f8fafc; color: #475569; font-weight: 600; }
    .primary-cell strong { display: block; color: #0f172a; }
    code { background: #f8fafc; padding: 2px 6px; border-radius: 4px; font-size: 11px; }
    .venue-badge { background: #f1f5f9; color: #334155; font-size: 11px; font-weight: 700; padding: 2px 6px; border-radius: 4px; }
    .status-pill { padding: 2px 8px; border-radius: 9999px; font-size: 11px; font-weight: 600; }
    .status-armed { background: #dcfce7; color: #15803d; }
    .events-grid { display: grid; grid-template-columns: repeat(auto-fit, minmax(320px, 1fr)); gap: 16px; margin-top: 14px; }
    .event-card { background: #ffffff; border: 1px solid #e2e8f0; border-radius: 8px; padding: 16px; }
    .e-header { display: flex; justify-content: space-between; margin-bottom: 8px; }
    .dwell-badge { background: #fef3c7; color: #b45309; font-size: 11px; font-weight: 700; padding: 2px 8px; border-radius: 4px; }
    .e-body p { margin: 4px 0; font-size: 12px; }
  `]
})
export class MicroGeofenceCockpitComponent {
  activePolygonsCount = signal(2);
  dailyAudienceDwells = signal(1481);
  footfallLiftPct = signal(48.2);
  totalAttributedRevenue = signal(85750);

  polygons = signal<PolygonItem[]>([
    {
      polygonId: 'GEO-POLY-001',
      venueName: 'Moscone Convention Center',
      venueType: 'CONVENTION_CENTER',
      radiusMeters: 250,
      bidMultiplier: 1.85,
      isActive: true
    },
    {
      polygonId: 'GEO-POLY-002',
      venueName: 'Silicon Valley Tech Park',
      venueType: 'COMPETITOR_HEADQUARTERS',
      radiusMeters: 500,
      bidMultiplier: 1.50,
      isActive: true
    }
  ]);

  events = signal<FootfallItem[]>([
    {
      eventId: 'FT-EVT-001',
      deviceAnonymousId: 'DEV-SHA256-48BA19FF',
      venueName: 'Moscone Convention Center',
      dwellTimeMinutes: 45,
      attributedStoreVisit: true,
      incrementalValueGeneratedUsd: 1250
    }
  ]);

  defineNewPolygon(): void {
    const newPoly: PolygonItem = {
      polygonId: `GEO-POLY-00${this.polygons().length + 1}`,
      venueName: 'Javits Center NYC',
      venueType: 'CONVENTION_CENTER',
      radiusMeters: 300,
      bidMultiplier: 1.95,
      isActive: true
    };
    this.polygons.update(list => [...list, newPoly]);
    this.activePolygonsCount.update(v => v + 1);
  }
}
