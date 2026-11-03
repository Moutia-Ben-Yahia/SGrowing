import { Component, signal, ChangeDetectionStrategy } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';

export interface ToneEventItem {
  eventId: string;
  callSessionId: string;
  detectedEmotion: string;
  confidenceScore: number;
  pitchShiftSemitones: number;
  tempoAdjustmentRatio: number;
  timbreFilter: string;
}

export interface ProfileItem {
  profileId: string;
  voiceActorId: string;
  pitchF0Modulation: number;
  speechRateMultiplier: number;
  warmthBoostDb: number;
  targetEmotion: string;
}

@Component({
  selector: 'app-prosody-empathy-cockpit',
  standalone: true,
  imports: [CommonModule, FormsModule],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="cockpit-container" dir="ltr" data-testid="prosody-empathy-cockpit">
      <header class="cockpit-header">
        <div class="badge">Horizon 178 · Real-Time Emotional Prosody & Speech Synthesis</div>
        <h1>Autonomous Speech-to-Speech Prosody & Emotional Empathy Tone Shifter Engine</h1>
        <p class="subtitle">Sub-50ms acoustic emotion classification, dynamic pitch F0 modulation, speaking rate deceleration, and empathetic warmth timbre filtering</p>
      </header>

      <section class="metrics-grid">
        <div class="metric-card" data-testid="metric-active-calls">
          <div class="metric-label">Tone-Shifted Calls</div>
          <div class="metric-value positive">{{ activeCallsCount() }}</div>
          <div class="metric-sub">Active calls with dynamic empathy</div>
        </div>
        <div class="metric-card" data-testid="metric-deescalation-rate">
          <div class="metric-label">De-Escalation Success</div>
          <div class="metric-value positive">{{ deescalationRatePct() }}%</div>
          <div class="metric-sub">Frustrated callers calmed without human transfer</div>
        </div>
        <div class="metric-card" data-testid="metric-avg-latency">
          <div class="metric-label">Prosody Shift Latency</div>
          <div class="metric-value">{{ avgLatencyMs() }}ms</div>
          <div class="metric-sub">Sub-50ms streaming neural adaptation</div>
        </div>
        <div class="metric-card" data-testid="metric-profiles-count">
          <div class="metric-label">Empathy Profiles</div>
          <div class="metric-value">{{ profilesCount() }}</div>
          <div class="metric-sub">Calibrated emotional timbre curves</div>
        </div>
      </section>

      <section class="events-section">
        <div class="section-header">
          <h2>Recent Prosody Modulation & Tone Shifts</h2>
          <button class="action-btn" (click)="triggerToneShift()" data-testid="shift-tone-btn">
            + Shift Call Tone
          </button>
        </div>
        <div class="table-wrapper">
          <table class="data-table" data-testid="events-table">
            <thead>
              <tr>
                <th>Event ID</th>
                <th>Call Session</th>
                <th>Detected Emotion</th>
                <th>Confidence</th>
                <th>Pitch Shift</th>
                <th>Tempo Ratio</th>
                <th>Applied Filter</th>
              </tr>
            </thead>
            <tbody>
              @for (e of events(); track e.eventId) {
                <tr [attr.data-testid]="'event-row-' + e.eventId">
                  <td><code>{{ e.eventId }}</code></td>
                  <td class="primary-cell">
                    <strong>{{ e.callSessionId }}</strong>
                  </td>
                  <td><span class="emotion-badge">{{ e.detectedEmotion }}</span></td>
                  <td>{{ (e.confidenceScore * 100).toFixed(0) }}%</td>
                  <td><strong>{{ e.pitchShiftSemitones }} st</strong></td>
                  <td>{{ e.tempoAdjustmentRatio }}x</td>
                  <td><span class="filter-badge">{{ e.timbreFilter }}</span></td>
                </tr>
              }
            </tbody>
          </table>
        </div>
      </section>

      <section class="profiles-section">
        <h2>Active Prosody Modulation Profiles</h2>
        <div class="profiles-grid" data-testid="profiles-grid">
          @for (p of profiles(); track p.profileId) {
            <div class="profile-card" [attr.data-testid]="'profile-card-' + p.profileId">
              <div class="p-header">
                <strong>{{ p.profileId }}</strong>
                <span class="target-badge">{{ p.targetEmotion }}</span>
              </div>
              <div class="p-body">
                <p><strong>Actor Voice:</strong> {{ p.voiceActorId }}</p>
                <p><strong>F0 Modulation:</strong> {{ p.pitchF0Modulation }} st</p>
                <p><strong>Tempo Multiplier:</strong> {{ p.speechRateMultiplier }}x</p>
                <p><strong>Warmth Harmonic Boost:</strong> +{{ p.warmthBoostDb }} dB</p>
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
    .badge { display: inline-block; padding: 4px 10px; background: #ecfeff; color: #0891b2; border-radius: 9999px; font-size: 12px; font-weight: 600; margin-bottom: 8px; }
    h1 { font-size: 26px; font-weight: 700; margin: 0 0 8px 0; color: #155e75; }
    .subtitle { color: #64748b; font-size: 14px; margin: 0; max-width: 900px; line-height: 1.5; }
    .metrics-grid { display: grid; grid-template-columns: repeat(auto-fit, minmax(220px, 1fr)); gap: 16px; margin-bottom: 28px; }
    .metric-card { background: #ffffff; border: 1px solid #e2e8f0; border-radius: 10px; padding: 18px; box-shadow: 0 1px 3px rgba(0,0,0,0.05); }
    .metric-label { font-size: 12px; font-weight: 600; text-transform: uppercase; color: #64748b; margin-bottom: 6px; }
    .metric-value { font-size: 28px; font-weight: 800; color: #0f172a; margin-bottom: 4px; }
    .metric-value.positive { color: #059669; }
    .metric-sub { font-size: 12px; color: #94a3b8; }
    .events-section, .profiles-section { background: #ffffff; border: 1px solid #e2e8f0; border-radius: 12px; padding: 20px; margin-bottom: 24px; box-shadow: 0 1px 3px rgba(0,0,0,0.05); }
    .section-header { display: flex; justify-content: space-between; align-items: center; margin-bottom: 16px; }
    h2 { font-size: 18px; font-weight: 700; margin: 0; color: #1e293b; }
    .action-btn { background: #0891b2; color: #ffffff; border: none; border-radius: 6px; padding: 8px 14px; font-size: 13px; font-weight: 600; cursor: pointer; transition: background 0.15s; }
    .action-btn:hover { background: #0e7490; }
    .table-wrapper { overflow-x: auto; }
    .data-table { width: 100%; border-collapse: collapse; font-size: 13px; text-align: left; }
    .data-table th { background: #f8fafc; padding: 10px 14px; font-weight: 600; color: #475569; border-bottom: 1px solid #e2e8f0; }
    .data-table td { padding: 12px 14px; border-bottom: 1px solid #f1f5f9; vertical-align: middle; }
    .primary-cell strong { color: #0f172a; }
    .emotion-badge { background: #fee2e2; color: #991b1b; font-size: 11px; font-weight: 700; padding: 2px 6px; border-radius: 4px; }
    .filter-badge { background: #e0f2fe; color: #0369a1; font-size: 11px; font-weight: 700; padding: 2px 6px; border-radius: 4px; }
    .profiles-grid { display: grid; grid-template-columns: repeat(auto-fit, minmax(280px, 1fr)); gap: 16px; margin-top: 14px; }
    .profile-card { background: #ffffff; border: 1px solid #e2e8f0; border-radius: 8px; padding: 16px; }
    .p-header { display: flex; justify-content: space-between; margin-bottom: 8px; }
    .target-badge { background: #ecfdf5; color: #047857; font-size: 11px; font-weight: 700; padding: 2px 6px; border-radius: 4px; }
    .p-body p { margin: 4px 0; font-size: 12px; }
  `]
})
export class ProsodyEmpathyCockpitComponent {
  activeCallsCount = signal(28);
  deescalationRatePct = signal(88.4);
  avgLatencyMs = signal(34.2);
  profilesCount = signal(2);

  events = signal<ToneEventItem[]>([
    {
      eventId: 'TONE-EVT-001',
      callSessionId: 'CALL-SIP-7740',
      detectedEmotion: 'FrustratedAngry',
      confidenceScore: 0.96,
      pitchShiftSemitones: -1.5,
      tempoAdjustmentRatio: 0.88,
      timbreFilter: 'NeuralCalmingWarmthFilter'
    }
  ]);

  profiles = signal<ProfileItem[]>([
    {
      profileId: 'PROF-CALMING-01',
      voiceActorId: 'VOICE-NEURAL-EMILY',
      pitchF0Modulation: -1.5,
      speechRateMultiplier: 0.88,
      warmthBoostDb: 3.5,
      targetEmotion: 'FrustratedAngry'
    },
    {
      profileId: 'PROF-REASSURING-01',
      voiceActorId: 'VOICE-NEURAL-MARCUS',
      pitchF0Modulation: -0.8,
      speechRateMultiplier: 0.92,
      warmthBoostDb: 2.8,
      targetEmotion: 'AnxiousDistressed'
    }
  ]);

  triggerToneShift(): void {
    const newEvt: ToneEventItem = {
      eventId: `TONE-EVT-00${this.events().length + 1}`,
      callSessionId: 'CALL-SIP-8891',
      detectedEmotion: 'AnxiousDistressed',
      confidenceScore: 0.92,
      pitchShiftSemitones: -0.8,
      tempoAdjustmentRatio: 0.92,
      timbreFilter: 'GentleReassuranceFilter'
    };
    this.events.update(list => [...list, newEvt]);
  }
}
