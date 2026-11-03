import { Component, signal, ChangeDetectionStrategy } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';

export interface DialectProfileRecord {
  dialectCode: string;
  regionDescription: string;
  acousticFeatureCount: number;
  fastPathThresholdCutoff: number;
  averageInferenceLatencyMs: number;
}

export interface IntentPredictionRecord {
  predictionId: string;
  dialectCode: string;
  detectedIntent: string;
  confidenceScore: number;
  inferenceLatencyMs: number;
  fastPathRouted: boolean;
  timestamp: string;
}

@Component({
  selector: 'app-voice-intent-cockpit',
  standalone: true,
  imports: [CommonModule, FormsModule],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="cockpit-container" dir="ltr" data-testid="voice-intent-cockpit">
      <header class="cockpit-header">
        <div class="badge">Horizon 153 · Voice AI & Acoustic Telephony</div>
        <h1>Autonomous Voice Telephony Acoustic Normalization & Intent Fast-Path</h1>
        <p class="subtitle">Sub-20ms speech-to-intent routing, regional dialect acoustic normalization, Mel-spectrogram featurization, and direct agent dispatch</p>
      </header>

      <section class="metrics-grid">
        <div class="metric-card" data-testid="metric-hit-rate">
          <div class="metric-label">Fast-Path Hit Rate</div>
          <div class="metric-value positive">{{ fastPathHitRate() }}%</div>
          <div class="metric-sub">Bypasses full LLM generation pipeline</div>
        </div>
        <div class="metric-card" data-testid="metric-p99-latency">
          <div class="metric-label">P99 Inference Latency</div>
          <div class="metric-value positive">{{ p99Latency() }}ms</div>
          <div class="metric-sub">Acoustic featurization + neural classification</div>
        </div>
        <div class="metric-card" data-testid="metric-total-classifications">
          <div class="metric-label">Classifications (24h)</div>
          <div class="metric-value">{{ totalClassifications() }}</div>
          <div class="metric-sub">Inbound customer calls triaged</div>
        </div>
        <div class="metric-card" data-testid="metric-acoustic-confidence">
          <div class="metric-label">Avg Acoustic Confidence</div>
          <div class="metric-value">{{ acousticConfidence() }}</div>
          <div class="metric-sub">Dialect phoneme normalization score</div>
        </div>
      </section>

      <section class="predictions-section">
        <div class="section-header">
          <h2>Real-Time Intent Classifications</h2>
          <button class="action-btn" (click)="classifyNewIntent()" data-testid="classify-intent-btn">
            + Classify Speech Utterance
          </button>
        </div>
        <div class="table-wrapper">
          <table class="data-table" data-testid="predictions-table">
            <thead>
              <tr>
                <th>Prediction ID</th>
                <th>Dialect Code</th>
                <th>Detected Intent</th>
                <th>Confidence</th>
                <th>Latency</th>
                <th>Fast-Path</th>
              </tr>
            </thead>
            <tbody>
              @for (pred of predictions(); track pred.predictionId) {
                <tr [attr.data-testid]="'prediction-row-' + pred.predictionId">
                  <td><code>{{ pred.predictionId }}</code></td>
                  <td><span class="dialect-badge">{{ pred.dialectCode }}</span></td>
                  <td class="primary-cell">
                    <strong>{{ pred.detectedIntent }}</strong>
                    <span class="sub-text">{{ pred.timestamp }}</span>
                  </td>
                  <td><strong>{{ (pred.confidenceScore * 100).toFixed(1) }}%</strong></td>
                  <td><span class="latency-pill">{{ pred.inferenceLatencyMs }}ms</span></td>
                  <td>
                    @if (pred.fastPathRouted) {
                      <span class="fast-routed">ROUTED</span>
                    } @else {
                      <span class="fallback-routed">FALLBACK</span>
                    }
                  </td>
                </tr>
              }
            </tbody>
          </table>
        </div>
      </section>

      <section class="dialects-section">
        <h2>Regional Dialect Acoustic Profiles</h2>
        <div class="dialects-grid" data-testid="dialects-grid">
          @for (d of dialects(); track d.dialectCode) {
            <div class="dialect-card" [attr.data-testid]="'dialect-card-' + d.dialectCode">
              <div class="dia-header">
                <strong>{{ d.dialectCode }}</strong>
                <span class="lat-badge">{{ d.averageInferenceLatencyMs }}ms</span>
              </div>
              <div class="dia-body">
                <p><strong>Region:</strong> {{ d.regionDescription }}</p>
                <p><strong>Acoustic Features:</strong> {{ d.acousticFeatureCount }} MFCC/Pitch</p>
                <p><strong>Threshold Cutoff:</strong> {{ d.fastPathThresholdCutoff }}</p>
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
    .badge { display: inline-block; padding: 4px 10px; border-radius: 9999px; background: #fae8ff; color: #86198f; font-size: 12px; font-weight: 600; text-transform: uppercase; margin-bottom: 8px; }
    h1 { font-size: 26px; font-weight: 700; margin: 0 0 6px 0; color: #0f172a; }
    .subtitle { font-size: 14px; color: #64748b; margin: 0; }
    .metrics-grid { display: grid; grid-template-columns: repeat(auto-fit, minmax(240px, 1fr)); gap: 16px; margin-bottom: 28px; }
    .metric-card { background: #ffffff; border: 1px solid #e2e8f0; border-radius: 8px; padding: 18px; box-shadow: 0 1px 3px rgba(0,0,0,0.05); }
    .metric-label { font-size: 13px; font-weight: 500; color: #64748b; margin-bottom: 6px; }
    .metric-value { font-size: 28px; font-weight: 700; color: #0f172a; }
    .metric-value.positive { color: #16a34a; }
    .metric-sub { font-size: 12px; color: #94a3b8; margin-top: 4px; }
    .section-header { display: flex; justify-content: space-between; align-items: center; margin-bottom: 14px; }
    .action-btn { background: #9333ea; color: #ffffff; border: none; padding: 8px 14px; border-radius: 4px; font-size: 13px; font-weight: 600; cursor: pointer; }
    .table-wrapper { overflow-x: auto; background: #ffffff; border: 1px solid #e2e8f0; border-radius: 8px; }
    .data-table { width: 100%; border-collapse: collapse; text-align: left; font-size: 13px; }
    .data-table th, .data-table td { padding: 12px 16px; border-bottom: 1px solid #e2e8f0; }
    .data-table th { background: #f8fafc; color: #475569; font-weight: 600; }
    .primary-cell strong { display: block; color: #0f172a; }
    .sub-text { font-size: 11px; color: #64748b; display: block; margin-top: 2px; }
    code { background: #f8fafc; padding: 2px 6px; border-radius: 4px; font-size: 11px; }
    .dialect-badge { background: #f3e8ff; color: #6b21a8; font-size: 11px; font-weight: 600; padding: 2px 6px; border-radius: 4px; }
    .latency-pill { font-weight: 700; color: #047857; }
    .fast-routed { background: #dcfce7; color: #15803d; font-size: 11px; font-weight: 700; padding: 2px 6px; border-radius: 4px; }
    .fallback-routed { background: #fef3c7; color: #b45309; font-size: 11px; font-weight: 700; padding: 2px 6px; border-radius: 4px; }
    .dialects-grid { display: grid; grid-template-columns: repeat(auto-fit, minmax(320px, 1fr)); gap: 16px; margin-top: 14px; }
    .dialect-card { background: #ffffff; border: 1px solid #e2e8f0; border-radius: 8px; padding: 16px; }
    .dia-header { display: flex; justify-content: space-between; margin-bottom: 8px; }
    .lat-badge { background: #e0f2fe; color: #0369a1; font-size: 11px; font-weight: 700; padding: 2px 8px; border-radius: 4px; }
    .dia-body p { margin: 4px 0; font-size: 12px; }
  `]
})
export class VoiceIntentCockpitComponent {
  fastPathHitRate = signal(93.6);
  p99Latency = signal(18.4);
  totalClassifications = signal(12850);
  acousticConfidence = signal(0.96);

  predictions = signal<IntentPredictionRecord[]>([
    {
      predictionId: 'PRED-001',
      dialectCode: 'EN-TX-AUSTIN',
      detectedIntent: 'PAYMENT_DISPUTE_EXPEDITE',
      confidenceScore: 0.96,
      inferenceLatencyMs: 16.2,
      fastPathRouted: true,
      timestamp: 'Just now'
    },
    {
      predictionId: 'PRED-002',
      dialectCode: 'EN-UK-GLASGOW',
      detectedIntent: 'CONTRACT_UPGRADE_RENEWAL',
      confidenceScore: 0.94,
      inferenceLatencyMs: 19.5,
      fastPathRouted: true,
      timestamp: '2 mins ago'
    }
  ]);

  dialects = signal<DialectProfileRecord[]>([
    {
      dialectCode: 'EN-TX-AUSTIN',
      regionDescription: 'North America - Southern/Central Texas',
      acousticFeatureCount: 128,
      fastPathThresholdCutoff: 0.85,
      averageInferenceLatencyMs: 16.5
    },
    {
      dialectCode: 'EN-UK-GLASGOW',
      regionDescription: 'UK - Scottish Lowland Glasgow',
      acousticFeatureCount: 128,
      fastPathThresholdCutoff: 0.88,
      averageInferenceLatencyMs: 18.8
    }
  ]);

  classifyNewIntent(): void {
    const newPred: IntentPredictionRecord = {
      predictionId: `PRED-00${this.predictions().length + 1}`,
      dialectCode: 'EN-AU-SYDNEY',
      detectedIntent: 'SHIPPING_REROUTE_CONFIRMATION',
      confidenceScore: 0.95,
      inferenceLatencyMs: 17.8,
      fastPathRouted: true,
      timestamp: 'Just now'
    };
    this.predictions.update(list => [...list, newPred]);
    this.totalClassifications.update(v => v + 1);
  }
}
