import { Component, signal, ChangeDetectionStrategy } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';

export interface VocalSession {
  callId: string;
  callerId: string;
  framesAnalyzed: number;
  whisperFrames: number;
  peakStressScore: number;
  duressClassification: string;
  alertTriggered: boolean;
}

export interface AcousticAnalysis {
  callId: string;
  energyDbfs: number;
  tremorModulationHz: number;
  stressScore: number;
  duressLevel: string;
  alertTriggered: boolean;
  whisperBoostActive: boolean;
}

@Component({
  selector: 'app-vocal-tremor-cockpit',
  standalone: true,
  imports: [CommonModule, FormsModule],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="cockpit-container" dir="ltr" data-testid="vocal-tremor-cockpit">
      <header class="cockpit-header">
        <div class="header-titles">
          <h1>Horizon 183: Whisper Voice Isolation & Micro-Vocal Tremor Stress Detector</h1>
          <p class="subtitle">Acoustic Formant Gain Boosting & 8-14 Hz Physiological Tremor De-escalation Mesh</p>
        </div>
        <div class="header-badges">
          <span class="badge badge-accent" data-testid="audited-sessions-count">Audited Calls: {{ sessions().length }}</span>
          <span class="badge badge-warning">Physiological Tremor Band: 8-14 Hz</span>
        </div>
      </header>

      <div class="cockpit-grid">
        <!-- Audited Calls List -->
        <section class="card sessions-card">
          <h2>Monitored Voice Calls</h2>
          <div class="sessions-list">
            @for (sess of sessions(); track sess.callId) {
              <div 
                class="session-card" 
                [class.selected]="selectedSession()?.callId === sess.callId"
                [class.alert]="sess.alertTriggered"
                (click)="selectSession(sess)"
                [attr.data-testid]="'session-row-' + sess.callId">
                <div class="session-top">
                  <h3>{{ sess.callId }}</h3>
                  <span class="duress-pill" [class.danger]="sess.alertTriggered">{{ sess.duressClassification }}</span>
                </div>
                <div class="caller-id">{{ sess.callerId }}</div>
                <div class="session-meta">
                  <span>Peak Stress: <strong>{{ sess.peakStressScore }}/100</strong></span>
                  <span>Whisper Frames: <strong>{{ sess.whisperFrames }}</strong></span>
                </div>
              </div>
            }
          </div>
        </section>

        <!-- Live Acoustic Probe Simulator -->
        <section class="card probe-card">
          <h2>Live Acoustic Biomarker Probe</h2>
          <div class="probe-form">
            <label for="call-select">Active Call ID:</label>
            <input id="call-select" [(ngModel)]="activeCallId" />

            <label for="energy-slider">Energy Level (dBFS): {{ energyDbfs }} dBFS</label>
            <input id="energy-slider" type="range" min="-60" max="-10" [(ngModel)]="energyDbfs" />

            <label for="tremor-hz">Vocal Cord Micro-Tremor Modulation (Hz):</label>
            <input 
              id="tremor-hz" 
              type="number" 
              step="0.5" 
              [(ngModel)]="tremorModulationHz" 
              data-testid="tremor-hz-input" />

            <div class="toggle-group">
              <label>
                <input type="checkbox" [(ngModel)]="whisperBoostEnabled" data-testid="whisper-toggle" />
                Enable Adaptive Whisper Boost (+14.5dB Formant Gain)
              </label>
            </div>

            <button 
              type="button" 
              class="btn-primary" 
              (click)="analyzeAcoustics()"
              data-testid="analyze-acoustics-btn">
              Analyze Vocal Cord Biomarkers
            </button>
          </div>

          @if (analysisResult(); as res) {
            <div class="analysis-box" data-testid="stress-result-meter">
              <div class="meter-header">
                <span>Acute Stress Index: <strong>{{ res.stressScore }}/100</strong></span>
                <span class="level-pill" [class.danger]="res.alertTriggered">{{ res.duressLevel }}</span>
              </div>
              <div class="stress-bar">
                <div class="stress-fill" [style.width.%]="res.stressScore" [class.danger]="res.alertTriggered"></div>
              </div>
              @if (res.alertTriggered) {
                <div class="alert-banner" data-testid="supervisor-alert-banner">
                  ⚠️ <strong>SUPERVISOR INTERVENTION DISPATCHED:</strong> Acute physiological duress biomarker detected ({{ res.tremorModulationHz }} Hz). Empathetic de-escalation protocol activated.
                </div>
              }
            </div>
          }
        </section>

        <!-- Acoustic Spectral Telemetry -->
        <section class="card spectral-card">
          <h2>Acoustic Telemetry & Formant Filters</h2>
          <div class="spec-info">
            <div class="metric-row">
              <span class="metric-lbl">Whisper Threshold:</span>
              <span class="metric-val">-35.0 dBFS to -60.0 dBFS</span>
            </div>
            <div class="metric-row">
              <span class="metric-lbl">Formant Multiplier:</span>
              <span class="metric-val">2.4x Dynamic Q-Filter</span>
            </div>
            <div class="metric-row">
              <span class="metric-lbl">De-escalation State:</span>
              <span class="metric-val text-success">Autonomous Tone Pitch Shifter Armed</span>
            </div>
          </div>
        </section>
      </div>
    </div>
  `,
  styles: [`
    .cockpit-container {
      padding: 1.5rem;
      background: #0f172a;
      color: #f8fafc;
      min-height: 100vh;
      font-family: system-ui, -apple-system, sans-serif;
    }
    .cockpit-header {
      display: flex;
      justify-content: space-between;
      align-items: center;
      margin-bottom: 2rem;
      border-bottom: 1px solid #334155;
      padding-bottom: 1rem;
    }
    .cockpit-header h1 { margin: 0; font-size: 1.5rem; color: #38bdf8; }
    .subtitle { margin: 0.25rem 0 0 0; color: #94a3b8; font-size: 0.9rem; }
    .header-badges { display: flex; gap: 0.75rem; }
    .badge {
      padding: 0.35rem 0.75rem;
      border-radius: 9999px;
      font-size: 0.8rem;
      font-weight: 600;
    }
    .badge-accent { background: #0369a1; color: #e0f2fe; }
    .badge-warning { background: #854d0e; color: #fef08a; }
    .cockpit-grid {
      display: grid;
      grid-template-columns: repeat(auto-fit, minmax(340px, 1fr));
      gap: 1.5rem;
    }
    .card {
      background: #1e293b;
      border: 1px solid #334155;
      border-radius: 0.75rem;
      padding: 1.25rem;
    }
    .card h2 {
      margin-top: 0;
      font-size: 1.15rem;
      color: #e2e8f0;
      border-bottom: 1px solid #334155;
      padding-bottom: 0.5rem;
    }
    .sessions-list {
      display: flex;
      flex-direction: column;
      gap: 0.75rem;
    }
    .session-card {
      background: #0f172a;
      border: 1px solid #334155;
      border-radius: 0.5rem;
      padding: 0.75rem;
      cursor: pointer;
      transition: all 0.2s ease;
    }
    .session-card:hover, .session-card.selected {
      border-color: #38bdf8;
      background: #1e293b;
    }
    .session-card.alert { border-color: #ef4444; }
    .session-top {
      display: flex;
      justify-content: space-between;
      align-items: center;
    }
    .session-top h3 { margin: 0; font-size: 1rem; color: #f8fafc; }
    .duress-pill {
      font-size: 0.75rem;
      padding: 0.2rem 0.4rem;
      border-radius: 0.25rem;
      background: #0284c7;
      color: #e0f2fe;
    }
    .duress-pill.danger { background: #b91c1c; color: #fee2e2; }
    .caller-id { font-size: 0.8rem; color: #94a3b8; margin: 0.25rem 0; }
    .session-meta { display: flex; gap: 1rem; font-size: 0.8rem; color: #cbd5e1; }
    .probe-form {
      display: flex;
      flex-direction: column;
      gap: 0.6rem;
    }
    input[type="text"], input[type="number"] {
      background: #0f172a;
      border: 1px solid #334155;
      border-radius: 0.375rem;
      color: #f8fafc;
      padding: 0.45rem;
      font-family: inherit;
    }
    .btn-primary {
      background: #2563eb;
      color: #fff;
      border: none;
      border-radius: 0.375rem;
      padding: 0.5rem 1rem;
      font-weight: 600;
      cursor: pointer;
      margin-top: 0.5rem;
      transition: background 0.2s;
    }
    .btn-primary:hover { background: #1d4ed8; }
    .analysis-box {
      margin-top: 1rem;
      background: #0f172a;
      border: 1px solid #334155;
      border-radius: 0.5rem;
      padding: 0.75rem;
    }
    .meter-header {
      display: flex;
      justify-content: space-between;
      margin-bottom: 0.5rem;
      font-size: 0.85rem;
    }
    .stress-bar {
      background: #334155;
      height: 10px;
      border-radius: 5px;
      overflow: hidden;
      margin-bottom: 0.75rem;
    }
    .stress-fill { background: #38bdf8; height: 100%; }
    .stress-fill.danger { background: #ef4444; }
    .alert-banner {
      background: #7f1d1d;
      border: 1px solid #dc2626;
      border-radius: 0.375rem;
      padding: 0.5rem;
      color: #fecaca;
      font-size: 0.8rem;
    }
    .metric-row {
      display: flex;
      justify-content: space-between;
      margin-bottom: 0.5rem;
      font-size: 0.85rem;
    }
    .metric-lbl { color: #94a3b8; }
    .metric-val { color: #f8fafc; font-weight: 600; }
    .text-success { color: #4ade80; }
  `]
})
export class VocalTremorCockpitComponent {
  sessions = signal<VocalSession[]>([
    {
      callId: 'CALL-VOICE-881',
      callerId: '+1-415-555-0199',
      framesAnalyzed: 1840,
      whisperFrames: 24,
      peakStressScore: 42,
      duressClassification: 'MildStress',
      alertTriggered: false
    },
    {
      callId: 'CALL-VOICE-882',
      callerId: '+1-212-555-0144',
      framesAnalyzed: 950,
      whisperFrames: 410,
      peakStressScore: 89,
      duressClassification: 'SevereDuress',
      alertTriggered: true
    }
  ]);

  selectedSession = signal<VocalSession | null>(this.sessions()[1]);
  activeCallId = 'CALL-VOICE-882';
  energyDbfs = -42.0;
  tremorModulationHz = 11.0;
  whisperBoostEnabled = true;
  analysisResult = signal<AcousticAnalysis | null>(null);

  selectSession(sess: VocalSession) {
    this.selectedSession.set(sess);
    this.activeCallId = sess.callId;
  }

  analyzeAcoustics() {
    const isTremor = this.tremorModulationHz >= 8.0 && this.tremorModulationHz <= 14.0;
    let score = 20;
    if (isTremor) score += 45;
    if (this.energyDbfs <= -35.0) score += 20;
    score = Math.min(100, score);

    const duress = score >= 75 ? 'SevereDuress' : score >= 40 ? 'MildStress' : 'Normal';
    const alert = score >= 75;

    this.analysisResult.set({
      callId: this.activeCallId,
      energyDbfs: this.energyDbfs,
      tremorModulationHz: this.tremorModulationHz,
      stressScore: score,
      duressLevel: duress,
      alertTriggered: alert,
      whisperBoostActive: this.whisperBoostEnabled && this.energyDbfs <= -35.0
    });
  }
}
