import { Component, signal, ChangeDetectionStrategy } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';

export interface AudioAdSlot {
  slotId: string;
  showName: string;
  episodeTitle: string;
  positionType: string;
  durationSeconds: number;
  acousticWatermarkHash: string;
  cpmBidUsd: number;
  targetDma: string;
  status: string;
}

export interface AudioAttributionEvent {
  attributionId: string;
  slotId: string;
  promoCode: string;
  deviceType: string;
  conversionChannel: string;
  attributedRevenueUsd: number;
}

@Component({
  selector: 'app-audio-dai-cockpit',
  standalone: true,
  imports: [CommonModule, FormsModule],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="cockpit-container" dir="ltr" data-testid="audio-dai-cockpit">
      <header class="cockpit-header">
        <div class="badge">Horizon 142 · Programmatic Audio & Podcasting</div>
        <h1>Dynamic Audio Ad Insertion (DAI) & Attribution Mesh</h1>
        <p class="subtitle">Real-time programmatic audio slot stitching, acoustic watermark stamping, and cross-device podcast conversion attribution</p>
      </header>

      <section class="metrics-grid">
        <div class="metric-card" data-testid="metric-impressions">
          <div class="metric-label">Audio Impressions Served</div>
          <div class="metric-value">{{ totalImpressions().toLocaleString() }}</div>
          <div class="metric-sub">Dynamic pre-roll, mid-roll, post-roll breaks</div>
        </div>
        <div class="metric-card" data-testid="metric-completion-rate">
          <div class="metric-label">Audio Completion Rate (ACR)</div>
          <div class="metric-value positive">{{ completionRate() }}%</div>
          <div class="metric-sub">Full listen-through rate without skips</div>
        </div>
        <div class="metric-card" data-testid="metric-attributed-revenue">
          <div class="metric-label">Attributed Revenue</div>
          <div class="metric-value">\${{ attributedRevenue().toLocaleString() }}</div>
          <div class="metric-sub">Promo codes & acoustic watermark matches</div>
        </div>
        <div class="metric-card" data-testid="metric-roas">
          <div class="metric-label">Audio ROAS</div>
          <div class="metric-value positive">{{ roas() }}x</div>
          <div class="metric-sub">Effective return on programmatic audio spend</div>
        </div>
      </section>

      <section class="slots-section">
        <div class="section-header">
          <h2>Active Dynamic Audio Ad Slots</h2>
          <button class="insert-btn" (click)="insertNewSlot()" data-testid="insert-slot-btn">
            + Insert Audio Slot
          </button>
        </div>
        <div class="table-wrapper">
          <table class="data-table" data-testid="audio-slots-table">
            <thead>
              <tr>
                <th>Podcast & Episode</th>
                <th>Position</th>
                <th>Duration</th>
                <th>Acoustic Watermark</th>
                <th>CPM Bid</th>
                <th>Target DMA</th>
                <th>Status</th>
              </tr>
            </thead>
            <tbody>
              @for (slot of slots(); track slot.slotId) {
                <tr [attr.data-testid]="'slot-row-' + slot.slotId">
                  <td class="primary-cell">
                    <strong>{{ slot.showName }}</strong>
                    <span class="sub-text">{{ slot.episodeTitle }}</span>
                  </td>
                  <td><span class="pos-badge">{{ slot.positionType }}</span></td>
                  <td>{{ slot.durationSeconds }}s</td>
                  <td><code>{{ slot.acousticWatermarkHash }}</code></td>
                  <td>\${{ slot.cpmBidUsd.toFixed(2) }}</td>
                  <td>{{ slot.targetDma }}</td>
                  <td><span class="status-pill status-active">{{ slot.status }}</span></td>
                </tr>
              }
            </tbody>
          </table>
        </div>
      </section>

      <section class="attribution-section">
        <h2>Recent Cross-Device Audio Attributions</h2>
        <div class="attribution-grid" data-testid="attributions-grid">
          @for (attr of attributions(); track attr.attributionId) {
            <div class="attr-card" [attr.data-testid]="'attr-card-' + attr.attributionId">
              <div class="attr-header">
                <span class="promo-badge">{{ attr.promoCode }}</span>
                <span class="device-badge">{{ attr.deviceType }}</span>
              </div>
              <div class="attr-body">
                <p><strong>Channel:</strong> {{ attr.conversionChannel }}</p>
                <p class="revenue"><strong>Revenue:</strong> \${{ attr.attributedRevenueUsd.toLocaleString() }}</p>
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
    .badge { display: inline-block; padding: 4px 10px; border-radius: 9999px; background: #fef3c7; color: #92400e; font-size: 12px; font-weight: 600; text-transform: uppercase; margin-bottom: 8px; }
    h1 { font-size: 26px; font-weight: 700; margin: 0 0 6px 0; color: #0f172a; }
    .subtitle { font-size: 14px; color: #64748b; margin: 0; }
    .metrics-grid { display: grid; grid-template-columns: repeat(auto-fit, minmax(240px, 1fr)); gap: 16px; margin-bottom: 28px; }
    .metric-card { background: #ffffff; border: 1px solid #e2e8f0; border-radius: 8px; padding: 18px; box-shadow: 0 1px 3px rgba(0,0,0,0.05); }
    .metric-label { font-size: 13px; font-weight: 500; color: #64748b; margin-bottom: 6px; }
    .metric-value { font-size: 28px; font-weight: 700; color: #0f172a; }
    .metric-value.positive { color: #16a34a; }
    .metric-sub { font-size: 12px; color: #94a3b8; margin-top: 4px; }
    .section-header { display: flex; justify-content: space-between; align-items: center; margin-bottom: 14px; }
    .insert-btn { background: #ea580c; color: #ffffff; border: none; padding: 8px 14px; border-radius: 4px; font-size: 13px; font-weight: 600; cursor: pointer; }
    .table-wrapper { overflow-x: auto; background: #ffffff; border: 1px solid #e2e8f0; border-radius: 8px; }
    .data-table { width: 100%; border-collapse: collapse; text-align: left; font-size: 13px; }
    .data-table th, .data-table td { padding: 12px 16px; border-bottom: 1px solid #e2e8f0; }
    .data-table th { background: #f8fafc; color: #475569; font-weight: 600; }
    .primary-cell strong { display: block; color: #0f172a; }
    .sub-text { font-size: 11px; color: #64748b; }
    .pos-badge { background: #f1f5f9; padding: 2px 6px; border-radius: 4px; font-size: 11px; font-weight: 600; color: #334155; }
    code { background: #f1f5f9; padding: 2px 6px; border-radius: 4px; font-size: 11px; color: #0f172a; }
    .status-pill { padding: 2px 8px; border-radius: 9999px; font-size: 11px; font-weight: 600; }
    .status-active { background: #dcfce7; color: #15803d; }
    .attribution-grid { display: grid; grid-template-columns: repeat(auto-fit, minmax(260px, 1fr)); gap: 16px; margin-top: 14px; }
    .attr-card { background: #ffffff; border: 1px solid #e2e8f0; border-radius: 8px; padding: 14px; }
    .attr-header { display: flex; justify-content: space-between; margin-bottom: 8px; }
    .promo-badge { background: #fef08a; color: #854d0e; font-weight: 700; font-size: 11px; padding: 2px 8px; border-radius: 4px; }
    .device-badge { background: #e0e7ff; color: #3730a3; font-weight: 600; font-size: 11px; padding: 2px 8px; border-radius: 4px; }
    .attr-body p { margin: 4px 0; font-size: 12px; }
    .revenue { color: #16a34a; font-weight: 600; }
  `]
})
export class AudioDaiCockpitComponent {
  totalImpressions = signal(345000);
  completionRate = signal(96.8);
  attributedRevenue = signal(27700);
  roas = signal(9.55);

  slots = signal<AudioAdSlot[]>([
    {
      slotId: 'SLOT-POD-001',
      showName: 'The SaaS Growth Syndicate',
      episodeTitle: 'Ep. 214: Zero-Trust Telephony at Scale',
      positionType: 'MID_ROLL',
      durationSeconds: 30,
      acousticWatermarkHash: 'AWT-98B2F3A10C',
      cpmBidUsd: 42.50,
      targetDma: 'US-SFO',
      status: 'SERVING'
    },
    {
      slotId: 'SLOT-POD-002',
      showName: 'Enterprise Architecture Daily',
      episodeTitle: 'Ep. 88: High-Throughput Stream Processing',
      positionType: 'PRE_ROLL',
      durationSeconds: 15,
      acousticWatermarkHash: 'AWT-47C1E9D02A',
      cpmBidUsd: 36.00,
      targetDma: 'US-NYC',
      status: 'SERVING'
    }
  ]);

  attributions = signal<AudioAttributionEvent[]>([
    {
      attributionId: 'ATTR-001',
      slotId: 'SLOT-POD-001',
      promoCode: 'GROWTH2026',
      deviceType: 'CARPLAY',
      conversionChannel: 'DEMO_BOOKED',
      attributedRevenueUsd: 18500
    },
    {
      attributionId: 'ATTR-002',
      slotId: 'SLOT-POD-002',
      promoCode: 'ENTERPRISE30',
      deviceType: 'MOBILE_HEADPHONES',
      conversionChannel: 'DIRECT_SIGNUP',
      attributedRevenueUsd: 9200
    }
  ]);

  insertNewSlot(): void {
    const newSlot: AudioAdSlot = {
      slotId: `SLOT-${Math.floor(Math.random() * 900 + 100)}`,
      showName: 'FinTech Leaders Podcast',
      episodeTitle: 'Global Sovereign Clearance',
      positionType: 'MID_ROLL',
      durationSeconds: 30,
      acousticWatermarkHash: 'AWT-66A1F9B310',
      cpmBidUsd: 45.00,
      targetDma: 'UK-LON',
      status: 'INSERTED'
    };
    this.slots.update(list => [...list, newSlot]);
  }
}
