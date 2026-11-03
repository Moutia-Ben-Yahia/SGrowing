import { Component, signal, ChangeDetectionStrategy } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';

export interface DisfluencyEventItem {
  eventId: string;
  callSessionId: string;
  disfluencyType: string;
  durationMs: number;
  confidenceScore: number;
  recommendedPacingAdjustment: string;
}

export interface PacingProfileItem {
  profileId: string;
  callerSegment: string;
  baselineSpeakingRateWpm: number;
  dynamicPauseBudgetMs: number;
}

@Component({
  selector: 'app-telephony-disfluency-cockpit',
  standalone: true,
  imports: [CommonModule, FormsModule],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="cockpit-container" dir="ltr" data-testid="telephony-disfluency-cockpit">
      <header class="cockpit-header">
        <div class="badge">Horizon 158 · Conversational Speech Acoustics</div>
        <h1>Autonomous Telephony Silence & Disfluency Diagnostic Neural Mesh</h1>
        <p class="subtitle">Microsecond-level acoustic cadence diagnostic, caller cognitive pause detection, dynamic speaking rate adaptation, and turn-taking latency optimization</p>
      </header>

      <section class="metrics-grid">
        <div class="metric-card" data-testid="metric-analyzed-sessions">
          <div class="metric-label">Analyzed Calls (24h)</div>
          <div class="metric-value positive">{{ analyzedSessionsCount() }}</div>
          <div class="metric-sub">Real-time bi-directional calls parsed</div>
        </div>
        <div class="metric-card" data-testid="metric-avg-silence">
          <div class="metric-label">Avg Caller Silence</div>
          <div class="metric-value">{{ avgSilenceDurationMs() }}ms</div>
          <div class="metric-sub">Micro-hesitation & pause window</div>
        </div>
        <div class="metric-card" data-testid="metric-disfluencies">
          <div class="metric-label">Detected Disfluencies</div>
          <div class="metric-value">{{ disfluenciesCount() }}</div>
          <div class="metric-sub">Um/Uh fillers & false start repairs</div>
        </div>
        <div class="metric-card" data-testid="metric-turn-taking">
          <div class="metric-label">Turn-Taking Smoothness</div>
          <div class="metric-value positive">{{ turnTakingIndex() }}%</div>
          <div class="metric-sub">Collision-free conversation score</div>
        </div>
      </section>

      <section class="events-section">
        <div class="section-header">
          <h2>Detected Disfluency Events & Real-Time Adjustments</h2>
          <button class="action-btn" (click)="analyzeNewCallCadence()" data-testid="analyze-cadence-btn">
            + Analyze Call Cadence
          </button>
        </div>
        <div class="table-wrapper">
          <table class="data-table" data-testid="events-table">
            <thead>
              <tr>
                <th>Event ID</th>
                <th>Call Session</th>
                <th>Disfluency Type</th>
                <th>Duration</th>
                <th>Confidence</th>
                <th>Pacing Adaptation</th>
              </tr>
            </thead>
            <tbody>
              @for (evt of events(); track evt.eventId) {
                <tr [attr.data-testid]="'event-row-' + evt.eventId">
                  <td><code>{{ evt.eventId }}</code></td>
                  <td><strong>{{ evt.callSessionId }}</strong></td>
                  <td><span class="type-badge">{{ evt.disfluencyType }}</span></td>
                  <td><strong>{{ evt.durationMs }}ms</strong></td>
                  <td>{{ (evt.confidenceScore * 100).toFixed(0) }}%</td>
                  <td><span class="pacing-pill">{{ evt.recommendedPacingAdjustment }}</span></td>
                </tr>
              }
            </tbody>
          </table>
        </div>
      </section>

      <section class="profiles-section">
        <h2>Caller Cognitive Load Pacing Profiles</h2>
        <div class="profiles-grid" data-testid="profiles-grid">
          @for (p of profiles(); track p.profileId) {
            <div class="profile-card" [attr.data-testid]="'profile-card-' + p.profileId">
              <div class="p-header">
                <strong>{{ p.callerSegment }}</strong>
                <span class="wpm-badge">{{ p.baselineSpeakingRateWpm }} WPM</span>
              </div>
              <div class="p-body">
                <p><strong>Profile ID:</strong> {{ p.profileId }}</p>
                <p><strong>Pause Budget:</strong> {{ p.dynamicPauseBudgetMs }}ms</p>
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
    code { background: #f8fafc; padding: 2px 6px; border-radius: 4px; font-size: 11px; }
    .type-badge { background: #f3e8ff; color: #6b21a8; font-size: 11px; font-weight: 600; padding: 2px 6px; border-radius: 4px; }
    .pacing-pill { background: #e0f2fe; color: #0369a1; font-weight: 700; padding: 2px 8px; border-radius: 9999px; font-size: 11px; }
    .profiles-grid { display: grid; grid-template-columns: repeat(auto-fit, minmax(320px, 1fr)); gap: 16px; margin-top: 14px; }
    .profile-card { background: #ffffff; border: 1px solid #e2e8f0; border-radius: 8px; padding: 16px; }
    .p-header { display: flex; justify-content: space-between; margin-bottom: 8px; }
    .wpm-badge { background: #dcfce7; color: #15803d; font-size: 11px; font-weight: 700; padding: 2px 8px; border-radius: 4px; }
    .p-body p { margin: 4px 0; font-size: 12px; }
  `]
})
export class TelephonyDisfluencyCockpitComponent {
  analyzedSessionsCount = signal(142);
  avgSilenceDurationMs = signal(1035.0);
  disfluenciesCount = signal(87);
  turnTakingIndex = signal(94.6);

  events = signal<DisfluencyEventItem[]>([
    {
      eventId: 'DISF-001',
      callSessionId: 'CALL-DISF-901',
      disfluencyType: 'HESITATION_PAUSE',
      durationMs: 1450,
      confidenceScore: 0.95,
      recommendedPacingAdjustment: 'SLOW_DOWN_SPEECH_15PCT'
    },
    {
      eventId: 'DISF-002',
      callSessionId: 'CALL-DISF-902',
      disfluencyType: 'FILLER_SOUND_UM_UH',
      durationMs: 620,
      confidenceScore: 0.88,
      recommendedPacingAdjustment: 'INSERT_EMPATHY_BUFFER'
    }
  ]);

  profiles = signal<PacingProfileItem[]>([
    {
      profileId: 'PACE-PROF-001',
      callerSegment: 'HIGH_STRESS_DISPUTE',
      baselineSpeakingRateWpm: 128.0,
      dynamicPauseBudgetMs: 850
    },
    {
      profileId: 'PACE-PROF-002',
      callerSegment: 'CASUAL_RENEWAL',
      baselineSpeakingRateWpm: 155.0,
      dynamicPauseBudgetMs: 450
    }
  ]);

  analyzeNewCallCadence(): void {
    const newEvent: DisfluencyEventItem = {
      eventId: `DISF-00${this.events().length + 1}`,
      callSessionId: 'CALL-DISF-903',
      disfluencyType: 'PROLONGATION',
      durationMs: 880,
      confidenceScore: 0.91,
      recommendedPacingAdjustment: 'INSERT_EMPATHY_BUFFER'
    };
    this.events.update(list => [...list, newEvent]);
    this.disfluenciesCount.update(v => v + 1);
  }
}
