import { Component, signal, ChangeDetectionStrategy } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';

export interface VoiceBiometricProfile {
  profileId: string;
  callerIdentifier: string;
  executiveName: string;
  baselinePitchJitterPct: number;
  spectralCentroidMeanHz: number;
  cryptographicVoiceprintHash: string;
  status: string;
}

export interface CallSecurityAuditRecord {
  auditId: string;
  callSessionId: string;
  callerIdentifier: string;
  biometricMatchScore: number;
  syntheticDeepfakeProbability: number;
  vocoderArtifactSignature: string;
  defenseActionTaken: string;
}

@Component({
  selector: 'app-voice-biometric-cockpit',
  standalone: true,
  imports: [CommonModule, FormsModule],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="cockpit-container" dir="ltr" data-testid="voice-biometric-cockpit">
      <header class="cockpit-header">
        <div class="badge">Horizon 148 · AI Telephony Biometric Defense</div>
        <h1>Voice Biometric Speaker Verification & Deepfake Audio Defense</h1>
        <p class="subtitle">In-flight acoustic voiceprint feature extraction, spectral centroid analysis, vocoder diffusion artifact detection, and automated synthetic audio quarantine</p>
      </header>

      <section class="metrics-grid">
        <div class="metric-card" data-testid="metric-enrolled-profiles">
          <div class="metric-label">Enrolled Voiceprints</div>
          <div class="metric-value positive">{{ totalProfiles() }}</div>
          <div class="metric-sub">Verified executive & client voice signatures</div>
        </div>
        <div class="metric-card" data-testid="metric-scanned-calls">
          <div class="metric-label">Calls Scanned</div>
          <div class="metric-value">{{ scannedCalls() }}</div>
          <div class="metric-sub">Real-time spectral analysis executed</div>
        </div>
        <div class="metric-card" data-testid="metric-blocked-deepfakes">
          <div class="metric-label">Deepfakes Blocked</div>
          <div class="metric-value warning">{{ blockedDeepfakes() }}</div>
          <div class="metric-sub">Synthetic clones quarantined (>0.80 prob)</div>
        </div>
        <div class="metric-card" data-testid="metric-defense-accuracy">
          <div class="metric-label">Detection Accuracy</div>
          <div class="metric-value positive">{{ defenseAccuracy() }}%</div>
          <div class="metric-sub">Zero false positive quarantine rate</div>
        </div>
      </section>

      <section class="profiles-section">
        <div class="section-header">
          <h2>Active Voice Biometric Profiles</h2>
          <button class="verify-btn" (click)="simulateDeepfakeAttack()" data-testid="simulate-attack-btn">
            + Simulate Deepfake Influx
          </button>
        </div>
        <div class="table-wrapper">
          <table class="data-table" data-testid="profiles-table">
            <thead>
              <tr>
                <th>Executive & Caller ID</th>
                <th>Baseline Jitter</th>
                <th>Spectral Centroid</th>
                <th>Voiceprint Hash</th>
                <th>Status</th>
              </tr>
            </thead>
            <tbody>
              @for (p of profiles(); track p.profileId) {
                <tr [attr.data-testid]="'profile-row-' + p.profileId">
                  <td class="primary-cell">
                    <strong>{{ p.executiveName }}</strong>
                    <span class="sub-text">{{ p.callerIdentifier }}</span>
                  </td>
                  <td>{{ p.baselinePitchJitterPct }}%</td>
                  <td>{{ p.spectralCentroidMeanHz }} Hz</td>
                  <td><code>{{ p.cryptographicVoiceprintHash }}</code></td>
                  <td><span class="status-pill status-verified">{{ p.status }}</span></td>
                </tr>
              }
            </tbody>
          </table>
        </div>
      </section>

      <section class="audits-section">
        <h2>Real-Time Security & Deepfake Audits</h2>
        <div class="audits-grid" data-testid="audits-grid">
          @for (audit of audits(); track audit.auditId) {
            <div class="audit-card" [attr.data-testid]="'audit-card-' + audit.auditId">
              <div class="audit-header">
                <span class="action-pill" [class.action-allowed]="audit.defenseActionTaken === 'VERIFIED_ALLOWED'"
                      [class.action-blocked]="audit.defenseActionTaken === 'TERMINATED_QUARANTINED'">
                  {{ audit.defenseActionTaken }}
                </span>
                <span class="df-score">Deepfake: {{ (audit.syntheticDeepfakeProbability * 100).toFixed(0) }}%</span>
              </div>
              <div class="audit-body">
                <p><strong>Session:</strong> {{ audit.callSessionId }} ({{ audit.callerIdentifier }})</p>
                <p><strong>Biometric Match:</strong> {{ (audit.biometricMatchScore * 100).toFixed(0) }}%</p>
                <p class="signature"><code>{{ audit.vocoderArtifactSignature }}</code></p>
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
    .badge { display: inline-block; padding: 4px 10px; border-radius: 9999px; background: #fee2e2; color: #991b1b; font-size: 12px; font-weight: 600; text-transform: uppercase; margin-bottom: 8px; }
    h1 { font-size: 26px; font-weight: 700; margin: 0 0 6px 0; color: #0f172a; }
    .subtitle { font-size: 14px; color: #64748b; margin: 0; }
    .metrics-grid { display: grid; grid-template-columns: repeat(auto-fit, minmax(240px, 1fr)); gap: 16px; margin-bottom: 28px; }
    .metric-card { background: #ffffff; border: 1px solid #e2e8f0; border-radius: 8px; padding: 18px; box-shadow: 0 1px 3px rgba(0,0,0,0.05); }
    .metric-label { font-size: 13px; font-weight: 500; color: #64748b; margin-bottom: 6px; }
    .metric-value { font-size: 28px; font-weight: 700; color: #0f172a; }
    .metric-value.positive { color: #16a34a; }
    .metric-value.warning { color: #dc2626; }
    .metric-sub { font-size: 12px; color: #94a3b8; margin-top: 4px; }
    .section-header { display: flex; justify-content: space-between; align-items: center; margin-bottom: 14px; }
    .verify-btn { background: #dc2626; color: #ffffff; border: none; padding: 8px 14px; border-radius: 4px; font-size: 13px; font-weight: 600; cursor: pointer; }
    .table-wrapper { overflow-x: auto; background: #ffffff; border: 1px solid #e2e8f0; border-radius: 8px; }
    .data-table { width: 100%; border-collapse: collapse; text-align: left; font-size: 13px; }
    .data-table th, .data-table td { padding: 12px 16px; border-bottom: 1px solid #e2e8f0; }
    .data-table th { background: #f8fafc; color: #475569; font-weight: 600; }
    .primary-cell strong { display: block; color: #0f172a; }
    .sub-text { font-size: 11px; color: #64748b; }
    code { background: #f8fafc; padding: 2px 6px; border-radius: 4px; font-size: 11px; }
    .status-pill { padding: 2px 8px; border-radius: 9999px; font-size: 11px; font-weight: 600; }
    .status-verified { background: #dcfce7; color: #15803d; }
    .audits-grid { display: grid; grid-template-columns: repeat(auto-fit, minmax(320px, 1fr)); gap: 16px; margin-top: 14px; }
    .audit-card { background: #ffffff; border: 1px solid #e2e8f0; border-radius: 8px; padding: 16px; }
    .audit-header { display: flex; justify-content: space-between; margin-bottom: 8px; }
    .action-pill { padding: 2px 8px; border-radius: 4px; font-size: 11px; font-weight: 700; }
    .action-allowed { background: #dcfce7; color: #15803d; }
    .action-blocked { background: #fee2e2; color: #b91c1c; }
    .df-score { font-size: 12px; font-weight: 600; color: #475569; }
    .audit-body p { margin: 4px 0; font-size: 12px; }
    .signature code { color: #dc2626; font-size: 10px; }
  `]
})
export class VoiceBiometricCockpitComponent {
  totalProfiles = signal(2);
  scannedCalls = signal(2);
  blockedDeepfakes = signal(1);
  defenseAccuracy = signal(99.4);

  profiles = signal<VoiceBiometricProfile[]>([
    {
      profileId: 'BIO-PROF-001',
      callerIdentifier: '+1-415-555-8901',
      executiveName: 'Alexander Vance (CEO)',
      baselinePitchJitterPct: 0.62,
      spectralCentroidMeanHz: 1420.5,
      cryptographicVoiceprintHash: 'VP-SHA256-48BA192F01C',
      status: 'ACTIVE_VERIFIED'
    },
    {
      profileId: 'BIO-PROF-002',
      callerIdentifier: '+1-212-555-4422',
      executiveName: 'Beatrice Sterling (CFO)',
      baselinePitchJitterPct: 0.58,
      spectralCentroidMeanHz: 1680.2,
      cryptographicVoiceprintHash: 'VP-SHA256-99DF021C88B',
      status: 'ACTIVE_VERIFIED'
    }
  ]);

  audits = signal<CallSecurityAuditRecord[]>([
    {
      auditId: 'AUD-SEC-001',
      callSessionId: 'CALL-SESSION-890',
      callerIdentifier: '+1-415-555-8901',
      biometricMatchScore: 0.94,
      syntheticDeepfakeProbability: 0.02,
      vocoderArtifactSignature: 'CLEAN_ORGANIC_VOCAL_TRACT',
      defenseActionTaken: 'VERIFIED_ALLOWED'
    },
    {
      auditId: 'AUD-SEC-002',
      callSessionId: 'CALL-SESSION-912',
      callerIdentifier: '+1-800-SUSPICIOUS',
      biometricMatchScore: 0.42,
      syntheticDeepfakeProbability: 0.96,
      vocoderArtifactSignature: 'VOCODER_DIFFUSION_RESIDUAL_ARTIFACT',
      defenseActionTaken: 'TERMINATED_QUARANTINED'
    }
  ]);

  simulateDeepfakeAttack(): void {
    const newAudit: CallSecurityAuditRecord = {
      auditId: `AUD-SEC-${Math.floor(Math.random() * 900 + 100)}`,
      callSessionId: `CALL-ATTACK-${Math.floor(Math.random() * 9000 + 1000)}`,
      callerIdentifier: '+1-888-VOICE-CLONE',
      biometricMatchScore: 0.35,
      syntheticDeepfakeProbability: 0.98,
      vocoderArtifactSignature: 'VOCODER_DIFFUSION_RESIDUAL_ARTIFACT',
      defenseActionTaken: 'TERMINATED_QUARANTINED'
    };
    this.audits.update(list => [newAudit, ...list]);
    this.blockedDeepfakes.update(v => v + 1);
    this.scannedCalls.update(v => v + 1);
  }
}
