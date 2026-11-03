import { Component, signal, ChangeDetectionStrategy } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';

export interface RoomProfileItem {
  profileId: string;
  callSessionId: string;
  roomType: string;
  rt60DecaySeconds: number;
  directToReverbRatioDb: number;
}

export interface FilterItem {
  filterEventId: string;
  callSessionId: string;
  inputSnrDb: number;
  outputSnrDb: number;
  suppressionDb: number;
  delayMs: number;
  filterActive: boolean;
}

@Component({
  selector: 'app-dereverberation-cockpit',
  standalone: true,
  imports: [CommonModule, FormsModule],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="cockpit-container" dir="ltr" data-testid="dereverberation-cockpit">
      <header class="cockpit-header">
        <div class="badge">Horizon 173 · Telephony Audio DSP & Speech Intelligence</div>
        <h1>Autonomous AI Voice Telephony Dynamic Acoustic Room Profiling & Dereverberation Mesh</h1>
        <p class="subtitle">Microsecond RT60 reverberation estimation, deep spectral neural inverse filtering, and zero hollow-room acoustic echo artifacts</p>
      </header>

      <section class="metrics-grid">
        <div class="metric-card" data-testid="metric-active-streams">
          <div class="metric-label">Active Audio Streams</div>
          <div class="metric-value positive">{{ activeStreamsCount() }}</div>
          <div class="metric-sub">Real-time spectral dereverberation</div>
        </div>
        <div class="metric-card" data-testid="metric-avg-rt60">
          <div class="metric-label">Avg RT60 Suppressed</div>
          <div class="metric-value positive">{{ avgRt60Sec() }}s</div>
          <div class="metric-sub">Room impulse decay attenuation</div>
        </div>
        <div class="metric-card" data-testid="metric-snr-gain">
          <div class="metric-label">SNR Clarity Gain</div>
          <div class="metric-value positive">+{{ snrGainDb() }} dB</div>
          <div class="metric-sub">Spectral signal-to-noise improvement</div>
        </div>
        <div class="metric-card" data-testid="metric-wer-reduction">
          <div class="metric-label">ASR WER Reduction</div>
          <div class="metric-value positive">-{{ werReductionPct() }}%</div>
          <div class="metric-sub">Transcription accuracy boost</div>
        </div>
      </section>

      <section class="profiles-section">
        <div class="section-header">
          <h2>Acoustic Room Profiles</h2>
          <button class="action-btn" (click)="profileNewRoom()" data-testid="profile-room-btn">
            + Profile Room Acoustics
          </button>
        </div>
        <div class="table-wrapper">
          <table class="data-table" data-testid="profiles-table">
            <thead>
              <tr>
                <th>Profile ID</th>
                <th>Call Session</th>
                <th>Room Type</th>
                <th>Estimated RT60</th>
                <th>D/R Ratio</th>
                <th>Status</th>
              </tr>
            </thead>
            <tbody>
              @for (p of profiles(); track p.profileId) {
                <tr [attr.data-testid]="'profile-row-' + p.profileId">
                  <td><code>{{ p.profileId }}</code></td>
                  <td class="primary-cell">
                    <strong>{{ p.callSessionId }}</strong>
                  </td>
                  <td><span class="room-badge">{{ p.roomType }}</span></td>
                  <td><strong>{{ p.rt60DecaySeconds }}s</strong></td>
                  <td>{{ p.directToReverbRatioDb }} dB</td>
                  <td><span class="status-pill status-filtered">CALIBRATED</span></td>
                </tr>
              }
            </tbody>
          </table>
        </div>
      </section>

      <section class="filters-section">
        <h2>Live Spectral Dereverberation Filters</h2>
        <div class="filters-grid" data-testid="filters-grid">
          @for (f of filters(); track f.filterEventId) {
            <div class="filter-card" [attr.data-testid]="'filter-card-' + f.filterEventId">
              <div class="f-header">
                <strong>{{ f.callSessionId }}</strong>
                <span class="suppr-badge">{{ f.suppressionDb }} dB</span>
              </div>
              <div class="f-body">
                <p><strong>Input SNR:</strong> {{ f.inputSnrDb }} dB → <strong>Output SNR:</strong> <span class="clean-snr">{{ f.outputSnrDb }} dB</span></p>
                <p><strong>Latency Overhead:</strong> {{ f.delayMs }}ms</p>
                <p><strong>Filter Status:</strong> <span class="active-badge">ACTIVE</span></p>
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
    .badge { display: inline-block; padding: 4px 10px; background: #ede9fe; color: #6d28d9; border-radius: 9999px; font-size: 12px; font-weight: 600; margin-bottom: 8px; }
    h1 { font-size: 26px; font-weight: 700; margin: 0 0 8px 0; color: #5b21b6; }
    .subtitle { color: #64748b; font-size: 14px; margin: 0; max-width: 900px; line-height: 1.5; }
    .metrics-grid { display: grid; grid-template-columns: repeat(auto-fit, minmax(220px, 1fr)); gap: 16px; margin-bottom: 28px; }
    .metric-card { background: #ffffff; border: 1px solid #e2e8f0; border-radius: 10px; padding: 18px; box-shadow: 0 1px 3px rgba(0,0,0,0.05); }
    .metric-label { font-size: 12px; font-weight: 600; text-transform: uppercase; color: #64748b; margin-bottom: 6px; }
    .metric-value { font-size: 28px; font-weight: 800; color: #0f172a; margin-bottom: 4px; }
    .metric-value.positive { color: #059669; }
    .metric-sub { font-size: 12px; color: #94a3b8; }
    .profiles-section, .filters-section { background: #ffffff; border: 1px solid #e2e8f0; border-radius: 12px; padding: 20px; margin-bottom: 24px; box-shadow: 0 1px 3px rgba(0,0,0,0.05); }
    .section-header { display: flex; justify-content: space-between; align-items: center; margin-bottom: 16px; }
    h2 { font-size: 18px; font-weight: 700; margin: 0; color: #1e293b; }
    .action-btn { background: #7c3aed; color: #ffffff; border: none; border-radius: 6px; padding: 8px 14px; font-size: 13px; font-weight: 600; cursor: pointer; transition: background 0.15s; }
    .action-btn:hover { background: #6d28d9; }
    .table-wrapper { overflow-x: auto; }
    .data-table { width: 100%; border-collapse: collapse; font-size: 13px; text-align: left; }
    .data-table th { background: #f8fafc; padding: 10px 14px; font-weight: 600; color: #475569; border-bottom: 1px solid #e2e8f0; }
    .data-table td { padding: 12px 14px; border-bottom: 1px solid #f1f5f9; vertical-align: middle; }
    .primary-cell strong { color: #0f172a; }
    .room-badge { background: #f5f3ff; color: #7c3aed; font-size: 11px; font-weight: 700; padding: 2px 6px; border-radius: 4px; }
    .status-pill { padding: 3px 8px; border-radius: 9999px; font-size: 11px; font-weight: 600; }
    .status-filtered { background: #dcfce7; color: #15803d; }
    .filters-grid { display: grid; grid-template-columns: repeat(auto-fit, minmax(280px, 1fr)); gap: 16px; margin-top: 14px; }
    .filter-card { background: #ffffff; border: 1px solid #e2e8f0; border-radius: 8px; padding: 16px; }
    .f-header { display: flex; justify-content: space-between; margin-bottom: 8px; }
    .suppr-badge { background: #ede9fe; color: #6d28d9; font-size: 11px; font-weight: 700; padding: 2px 6px; border-radius: 4px; }
    .f-body p { margin: 4px 0; font-size: 12px; }
    .clean-snr { color: #059669; font-weight: 700; }
    .active-badge { color: #15803d; font-weight: 700; font-size: 11px; }
  `]
})
export class DereverberationCockpitComponent {
  activeStreamsCount = signal(112);
  avgRt60Sec = signal(0.72);
  snrGainDb = signal(12.4);
  werReductionPct = signal(42.1);

  profiles = signal<RoomProfileItem[]>([
    {
      profileId: 'ROOM-PROF-001',
      callSessionId: 'CALL-RTC-8801',
      roomType: 'GlassConferenceRoom',
      rt60DecaySeconds: 0.85,
      directToReverbRatioDb: 4.2
    }
  ]);

  filters = signal<FilterItem[]>([
    {
      filterEventId: 'FLT-EVT-001',
      callSessionId: 'CALL-RTC-8801',
      inputSnrDb: 12.4,
      outputSnrDb: 24.8,
      suppressionDb: -18.5,
      delayMs: 2.1,
      filterActive: true
    }
  ]);

  profileNewRoom(): void {
    const newProfile: RoomProfileItem = {
      profileId: `ROOM-PROF-00${this.profiles().length + 1}`,
      callSessionId: 'CALL-RTC-9044',
      roomType: 'SmallTiledRoomOrKitchen',
      rt60DecaySeconds: 0.95,
      directToReverbRatioDb: 3.5
    };
    this.profiles.update(list => [...list, newProfile]);
  }
}
