import { Component, signal, ChangeDetectionStrategy } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';

export interface PlcReconItem {
  reconstructionId: string;
  callSessionId: string;
  sequenceNumber: number;
  codec: string;
  lostConsecutiveFramesCount: number;
  synthesisLatencyMs: number;
  predictedMosScore: number;
  neuralSuccess: boolean;
}

export interface JitterMetricItem {
  metricId: string;
  callSessionId: string;
  currentBufferDepthMs: number;
  targetBufferDepthMs: number;
  packetLossPct: number;
  jitterVarianceMs: number;
  bufferState: string;
}

@Component({
  selector: 'app-neural-plc-cockpit',
  standalone: true,
  imports: [CommonModule, FormsModule],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="cockpit-container" dir="ltr" data-testid="neural-plc-cockpit">
      <header class="cockpit-header">
        <div class="badge">Horizon 168 · Audio DSP & Telephony</div>
        <h1>Autonomous Deep Neural Packet Loss Concealment (PLC) & Jitter Arbiter</h1>
        <p class="subtitle">Deep neural autoregressive waveform synthesis across lost 20ms audio frames, dynamic jitter buffer sizing (<18ms latency), and zero acoustic clicking</p>
      </header>

      <section class="metrics-grid">
        <div class="metric-card" data-testid="metric-active-streams">
          <div class="metric-label">Active Audio Streams</div>
          <div class="metric-value positive">{{ activeStreamsCount() }}</div>
          <div class="metric-sub">Full-duplex Opus/G.722 channels</div>
        </div>
        <div class="metric-card" data-testid="metric-loss-concealed">
          <div class="metric-label">Loss Concealed Rate</div>
          <div class="metric-value positive">{{ lossConcealedPct() }}%</div>
          <div class="metric-sub">Neural synthesis recovery without dropout</div>
        </div>
        <div class="metric-card" data-testid="metric-mos-score">
          <div class="metric-label">Avg Waveform MOS</div>
          <div class="metric-value positive">{{ avgMosScore() }}</div>
          <div class="metric-sub">Perceptual audio fidelity score</div>
        </div>
        <div class="metric-card" data-testid="metric-buffer-latency">
          <div class="metric-label">Jitter Buffer Latency</div>
          <div class="metric-value">{{ bufferLatencyMs() }}ms</div>
          <div class="metric-sub">Ultra-low latency buffer depth</div>
        </div>
      </section>

      <section class="recons-section">
        <div class="section-header">
          <h2>Neural Frame Reconstructions Stream</h2>
          <button class="action-btn" (click)="synthesizeLostFrame()" data-testid="synthesize-frame-btn">
            + Synthesize Lost Frame
          </button>
        </div>
        <div class="table-wrapper">
          <table class="data-table" data-testid="recons-table">
            <thead>
              <tr>
                <th>Reconstruction ID</th>
                <th>Call Session</th>
                <th>Seq Num</th>
                <th>Codec</th>
                <th>Consecutive Lost</th>
                <th>Synthesis Latency</th>
                <th>MOS Score</th>
                <th>Status</th>
              </tr>
            </thead>
            <tbody>
              @for (r of recons(); track r.reconstructionId) {
                <tr [attr.data-testid]="'recon-row-' + r.reconstructionId">
                  <td><code>{{ r.reconstructionId }}</code></td>
                  <td class="primary-cell">
                    <strong>{{ r.callSessionId }}</strong>
                  </td>
                  <td>#{{ r.sequenceNumber }}</td>
                  <td><span class="codec-pill">{{ r.codec }}</span></td>
                  <td>{{ r.lostConsecutiveFramesCount }} frame(s)</td>
                  <td>{{ r.synthesisLatencyMs }}ms</td>
                  <td><span class="mos-pill">{{ r.predictedMosScore }}</span></td>
                  <td><span class="status-pill status-restored">RESTORED</span></td>
                </tr>
              }
            </tbody>
          </table>
        </div>
      </section>

      <section class="buffers-section">
        <h2>Active Jitter Buffer Telemetry</h2>
        <div class="buffers-grid" data-testid="buffers-grid">
          @for (b of buffers(); track b.metricId) {
            <div class="buffer-card" [attr.data-testid]="'buffer-card-' + b.metricId">
              <div class="b-header">
                <strong>{{ b.callSessionId }}</strong>
                <span class="state-badge">{{ b.bufferState }}</span>
              </div>
              <div class="b-body">
                <p><strong>Buffer Depth:</strong> {{ b.currentBufferDepthMs }}ms (Target: {{ b.targetBufferDepthMs }}ms)</p>
                <p><strong>Packet Loss:</strong> {{ b.packetLossPct }}%</p>
                <p><strong>Jitter Variance:</strong> {{ b.jitterVarianceMs }}ms</p>
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
    .badge { display: inline-block; padding: 4px 10px; background: #cffafe; color: #0e7490; border-radius: 9999px; font-size: 12px; font-weight: 600; margin-bottom: 8px; }
    h1 { font-size: 26px; font-weight: 700; margin: 0 0 8px 0; color: #164e63; }
    .subtitle { color: #64748b; font-size: 14px; margin: 0; max-width: 900px; line-height: 1.5; }
    .metrics-grid { display: grid; grid-template-columns: repeat(auto-fit, minmax(220px, 1fr)); gap: 16px; margin-bottom: 28px; }
    .metric-card { background: #ffffff; border: 1px solid #e2e8f0; border-radius: 10px; padding: 18px; box-shadow: 0 1px 3px rgba(0,0,0,0.05); }
    .metric-label { font-size: 12px; font-weight: 600; text-transform: uppercase; color: #64748b; margin-bottom: 6px; }
    .metric-value { font-size: 28px; font-weight: 800; color: #0f172a; margin-bottom: 4px; }
    .metric-value.positive { color: #059669; }
    .metric-sub { font-size: 12px; color: #94a3b8; }
    .recons-section, .buffers-section { background: #ffffff; border: 1px solid #e2e8f0; border-radius: 12px; padding: 20px; margin-bottom: 24px; box-shadow: 0 1px 3px rgba(0,0,0,0.05); }
    .section-header { display: flex; justify-content: space-between; align-items: center; margin-bottom: 16px; }
    h2 { font-size: 18px; font-weight: 700; margin: 0; color: #1e293b; }
    .action-btn { background: #0891b2; color: #ffffff; border: none; border-radius: 6px; padding: 8px 14px; font-size: 13px; font-weight: 600; cursor: pointer; transition: background 0.15s; }
    .action-btn:hover { background: #0e7490; }
    .table-wrapper { overflow-x: auto; }
    .data-table { width: 100%; border-collapse: collapse; font-size: 13px; text-align: left; }
    .data-table th { background: #f8fafc; padding: 10px 14px; font-weight: 600; color: #475569; border-bottom: 1px solid #e2e8f0; }
    .data-table td { padding: 12px 14px; border-bottom: 1px solid #f1f5f9; vertical-align: middle; }
    .primary-cell strong { color: #0f172a; }
    .codec-pill { background: #e0f2fe; color: #0369a1; font-size: 11px; font-weight: 700; padding: 2px 6px; border-radius: 4px; }
    .mos-pill { color: #059669; font-weight: 700; }
    .status-pill { padding: 3px 8px; border-radius: 9999px; font-size: 11px; font-weight: 600; }
    .status-restored { background: #dcfce7; color: #15803d; }
    .buffers-grid { display: grid; grid-template-columns: repeat(auto-fit, minmax(320px, 1fr)); gap: 16px; margin-top: 14px; }
    .buffer-card { background: #ffffff; border: 1px solid #e2e8f0; border-radius: 8px; padding: 16px; }
    .b-header { display: flex; justify-content: space-between; margin-bottom: 8px; }
    .state-badge { background: #ecfdf5; color: #047857; font-size: 11px; font-weight: 700; padding: 2px 8px; border-radius: 4px; }
    .b-body p { margin: 4px 0; font-size: 12px; }
  `]
})
export class NeuralPlcCockpitComponent {
  activeStreamsCount = signal(68);
  lossConcealedPct = signal(98.6);
  avgMosScore = signal(4.41);
  bufferLatencyMs = signal(18.5);

  recons = signal<PlcReconItem[]>([
    {
      reconstructionId: 'PLC-REC-001',
      callSessionId: 'CALL-SIP-8910',
      sequenceNumber: 412,
      codec: 'Opus48kHz',
      lostConsecutiveFramesCount: 1,
      synthesisLatencyMs: 1.42,
      predictedMosScore: 4.45,
      neuralSuccess: true
    }
  ]);

  buffers = signal<JitterMetricItem[]>([
    {
      metricId: 'JIT-MET-001',
      callSessionId: 'CALL-SIP-8910',
      currentBufferDepthMs: 20,
      targetBufferDepthMs: 18,
      packetLossPct: 4.2,
      jitterVarianceMs: 3.8,
      bufferState: 'Optimal'
    }
  ]);

  synthesizeLostFrame(): void {
    const newRecon: PlcReconItem = {
      reconstructionId: `PLC-REC-00${this.recons().length + 1}`,
      callSessionId: 'CALL-SIP-9044',
      sequenceNumber: 884,
      codec: 'Opus48kHz',
      lostConsecutiveFramesCount: 2,
      synthesisLatencyMs: 1.55,
      predictedMosScore: 4.38,
      neuralSuccess: true
    };
    this.recons.update(list => [...list, newRecon]);
  }
}
