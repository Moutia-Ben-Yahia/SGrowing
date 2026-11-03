import { Component, signal, ChangeDetectionStrategy } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';

export interface FrameItem {
  frameId: string;
  callSessionId: string;
  residualEchoDecibels: number;
  nearEndSpeechEnergyDb: number;
  farEndReferenceEnergyDb: number;
  doubleTalkDetected: boolean;
  filterState: string;
}

export interface ProfileItem {
  profileId: string;
  codecType: string;
  filterLengthTaps: number;
  stepSizeMu: number;
  maxEchoReturnLossDb: number;
}

@Component({
  selector: 'app-echo-cancellation-cockpit',
  standalone: true,
  imports: [CommonModule, FormsModule],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="cockpit-container" dir="ltr" data-testid="echo-cancellation-cockpit">
      <header class="cockpit-header">
        <div class="badge">Horizon 163 · WebRTC DSP Audio Engine</div>
        <h1>Autonomous Neural Acoustic Echo Cancellation & Double-Talk Arbiter</h1>
        <p class="subtitle">Real-time sub-50dB acoustic echo suppression, neural double-talk preservation (zero clipping during human interruption), and 48kHz audio clarity</p>
      </header>

      <section class="metrics-grid">
        <div class="metric-card" data-testid="metric-active-sessions">
          <div class="metric-label">Active WebRTC Calls</div>
          <div class="metric-value positive">{{ activeSessionsCount() }}</div>
          <div class="metric-sub">Full-duplex real-time audio streams</div>
        </div>
        <div class="metric-card" data-testid="metric-echo-suppression">
          <div class="metric-label">Avg Echo Suppression</div>
          <div class="metric-value positive">{{ avgSuppressionDb() }} dB</div>
          <div class="metric-sub">Deep residual acoustic attenuation</div>
        </div>
        <div class="metric-card" data-testid="metric-double-talk">
          <div class="metric-label">Double-Talk Accuracy</div>
          <div class="metric-value">{{ doubleTalkAccuracy() }}%</div>
          <div class="metric-sub">Preserves natural human interruptions</div>
        </div>
        <div class="metric-card" data-testid="metric-clarity-score">
          <div class="metric-label">Duplex Clarity Score</div>
          <div class="metric-value positive">{{ clarityScore() }}%</div>
          <div class="metric-sub">Opus 48kHz perceptual PESQ score</div>
        </div>
      </section>

      <section class="frames-section">
        <div class="section-header">
          <h2>Processed Audio DSP Frame Stream</h2>
          <button class="action-btn" (click)="processNewFrame()" data-testid="process-frame-btn">
            + Process DSP Audio Frame
          </button>
        </div>
        <div class="table-wrapper">
          <table class="data-table" data-testid="frames-table">
            <thead>
              <tr>
                <th>Frame ID</th>
                <th>Call Session</th>
                <th>Near-End Energy</th>
                <th>Far-End Energy</th>
                <th>Residual Echo</th>
                <th>Double-Talk</th>
                <th>State</th>
              </tr>
            </thead>
            <tbody>
              @for (f of frames(); track f.frameId) {
                <tr [attr.data-testid]="'frame-row-' + f.frameId">
                  <td><code>{{ f.frameId }}</code></td>
                  <td><strong>{{ f.callSessionId }}</strong></td>
                  <td>{{ f.nearEndSpeechEnergyDb }} dB</td>
                  <td>{{ f.farEndReferenceEnergyDb }} dB</td>
                  <td><strong>{{ f.residualEchoDecibels }} dB</strong></td>
                  <td>
                    @if (f.doubleTalkDetected) {
                      <span class="dt-detected">DOUBLE-TALK</span>
                    } @else {
                      <span class="dt-none">SINGLE</span>
                    }
                  </td>
                  <td><span class="status-pill status-converged">{{ f.filterState }}</span></td>
                </tr>
              }
            </tbody>
          </table>
        </div>
      </section>

      <section class="profiles-section">
        <h2>Acoustic Filter Configuration Profiles</h2>
        <div class="profiles-grid" data-testid="profiles-grid">
          @for (p of profiles(); track p.profileId) {
            <div class="profile-card" [attr.data-testid]="'profile-card-' + p.profileId">
              <div class="p-header">
                <strong>{{ p.profileId }}</strong>
                <span class="codec-badge">{{ p.codecType }}</span>
              </div>
              <div class="p-body">
                <p><strong>Filter Taps:</strong> {{ p.filterLengthTaps }} Taps</p>
                <p><strong>Step Size:</strong> {{ p.stepSizeMu }} &mu;</p>
                <p><strong>Max ERLE:</strong> {{ p.maxEchoReturnLossDb }} dB</p>
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
    .dt-detected { background: #fef3c7; color: #b45309; font-size: 11px; font-weight: 700; padding: 2px 6px; border-radius: 4px; }
    .dt-none { background: #f1f5f9; color: #475569; font-size: 11px; padding: 2px 6px; border-radius: 4px; }
    .status-pill { padding: 2px 8px; border-radius: 9999px; font-size: 11px; font-weight: 600; }
    .status-converged { background: #dcfce7; color: #15803d; }
    .profiles-grid { display: grid; grid-template-columns: repeat(auto-fit, minmax(320px, 1fr)); gap: 16px; margin-top: 14px; }
    .profile-card { background: #ffffff; border: 1px solid #e2e8f0; border-radius: 8px; padding: 16px; }
    .p-header { display: flex; justify-content: space-between; margin-bottom: 8px; }
    .codec-badge { background: #e0f2fe; color: #0369a1; font-size: 11px; font-weight: 700; padding: 2px 8px; border-radius: 4px; }
    .p-body p { margin: 4px 0; font-size: 12px; }
  `]
})
export class EchoCancellationCockpitComponent {
  activeSessionsCount = signal(87);
  avgSuppressionDb = signal(-55.5);
  doubleTalkAccuracy = signal(99.2);
  clarityScore = signal(98.5);

  frames = signal<FrameItem[]>([
    {
      frameId: 'FRAME-001',
      callSessionId: 'CALL-RTC-7701',
      residualEchoDecibels: -54.2,
      nearEndSpeechEnergyDb: -18.4,
      farEndReferenceEnergyDb: -22.0,
      doubleTalkDetected: true,
      filterState: 'CONVERGED'
    },
    {
      frameId: 'FRAME-002',
      callSessionId: 'CALL-RTC-7702',
      residualEchoDecibels: -56.8,
      nearEndSpeechEnergyDb: -45.0,
      farEndReferenceEnergyDb: -16.2,
      doubleTalkDetected: false,
      filterState: 'CONVERGED'
    }
  ]);

  profiles = signal<ProfileItem[]>([
    {
      profileId: 'ECHO-PROF-001',
      codecType: 'OPUS_48KHZ',
      filterLengthTaps: 2048,
      stepSizeMu: 0.025,
      maxEchoReturnLossDb: -58.4
    },
    {
      profileId: 'ECHO-PROF-002',
      codecType: 'G722_16KHZ',
      filterLengthTaps: 1024,
      stepSizeMu: 0.040,
      maxEchoReturnLossDb: -52.0
    }
  ]);

  processNewFrame(): void {
    const newFrame: FrameItem = {
      frameId: `FRAME-00${this.frames().length + 1}`,
      callSessionId: 'CALL-RTC-7703',
      residualEchoDecibels: -57.4,
      nearEndSpeechEnergyDb: -24.0,
      farEndReferenceEnergyDb: -26.5,
      doubleTalkDetected: true,
      filterState: 'CONVERGED'
    };
    this.frames.update(list => [...list, newFrame]);
  }
}
