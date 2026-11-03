import { Component, signal, ChangeDetectionStrategy } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';

export interface TelephonyCallSession {
  callSessionId: string;
  customerPhoneNumber: string;
  agentName: string;
  supervisorName: string;
  activeSupervisionMode: string;
  roundTripLatencyMs: number;
  complianceStatus: string;
  injectedWhispers: string[];
}

export interface WhisperPromptRecord {
  whisperId: string;
  callSessionId: string;
  promptText: string;
  contextCategory: string;
  supervisorName: string;
  agentAcknowledged: boolean;
}

@Component({
  selector: 'app-telephony-whisper-cockpit',
  standalone: true,
  imports: [CommonModule, FormsModule],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="cockpit-container" dir="ltr" data-testid="telephony-whisper-cockpit">
      <header class="cockpit-header">
        <div class="badge">Horizon 143 · Conversational Telephony AI</div>
        <h1>Dual-Channel Telephony Whisper Coach & Supervisory Takeover</h1>
        <p class="subtitle">Sub-100ms real-time audio whisper injection to agent earpieces, live compliance supervision, and hot seamless call barge-in/takeover</p>
      </header>

      <section class="metrics-grid">
        <div class="metric-card" data-testid="metric-active-calls">
          <div class="metric-label">Active Supervised Calls</div>
          <div class="metric-value">{{ activeCallsCount() }}</div>
          <div class="metric-sub">Dual-channel RTP audio streams monitored</div>
        </div>
        <div class="metric-card" data-testid="metric-stream-latency">
          <div class="metric-label">Dual-Channel Latency</div>
          <div class="metric-value positive">{{ avgLatency() }}ms</div>
          <div class="metric-sub">Ultra-low audio injection latency</div>
        </div>
        <div class="metric-card" data-testid="metric-whispers-injected">
          <div class="metric-label">Total Whispers Delivered</div>
          <div class="metric-value">{{ totalWhispers() }}</div>
          <div class="metric-sub">Contextual prompts delivered to reps</div>
        </div>
        <div class="metric-card" data-testid="metric-takeover-escalations">
          <div class="metric-label">Supervisory Takeovers</div>
          <div class="metric-value warning">{{ takeoverCount() }}</div>
          <div class="metric-sub">Barge-in / full voice stream transfers</div>
        </div>
      </section>

      <section class="calls-section">
        <h2>Live Supervised Telephony Channels</h2>
        <div class="table-wrapper">
          <table class="data-table" data-testid="sessions-table">
            <thead>
              <tr>
                <th>Call ID & Customer</th>
                <th>Rep & Supervisor</th>
                <th>Mode</th>
                <th>Latency</th>
                <th>Compliance</th>
                <th>Actions</th>
              </tr>
            </thead>
            <tbody>
              @for (session of sessions(); track session.callSessionId) {
                <tr [attr.data-testid]="'session-row-' + session.callSessionId">
                  <td class="primary-cell">
                    <strong>{{ session.callSessionId }}</strong>
                    <span class="sub-text">{{ session.customerPhoneNumber }}</span>
                  </td>
                  <td>
                    <div><strong>Rep:</strong> {{ session.agentName }}</div>
                    <div class="sub-text"><strong>Sup:</strong> {{ session.supervisorName }}</div>
                  </td>
                  <td>
                    <span class="mode-pill" [class.mode-whisper]="session.activeSupervisionMode === 'WHISPERING'"
                          [class.mode-takeover]="session.activeSupervisionMode === 'FULL_TAKEOVER'">
                      {{ session.activeSupervisionMode }}
                    </span>
                  </td>
                  <td>{{ session.roundTripLatencyMs }}ms</td>
                  <td>
                    <span class="status-pill status-clean">{{ session.complianceStatus }}</span>
                  </td>
                  <td class="actions-cell">
                    <button class="whisper-btn" (click)="injectWhisper(session)" data-testid="inject-whisper-btn">
                      Whisper
                    </button>
                    <button class="takeover-btn" (click)="takeoverCall(session)" data-testid="takeover-btn">
                      Takeover
                    </button>
                  </td>
                </tr>
              }
            </tbody>
          </table>
        </div>
      </section>

      <section class="whispers-history-section">
        <h2>Recent Whisper Prompts Delivered</h2>
        <div class="whispers-list" data-testid="whispers-list">
          @for (w of whispers(); track w.whisperId) {
            <div class="whisper-item" [attr.data-testid]="'whisper-item-' + w.whisperId">
              <div class="w-header">
                <span class="category-badge">{{ w.contextCategory }}</span>
                <span class="call-ref">Ref: {{ w.callSessionId }}</span>
              </div>
              <div class="w-body">
                "{{ w.promptText }}"
              </div>
              <div class="w-footer">
                <span>By: {{ w.supervisorName }}</span>
                <span class="ack-badge" [class.acked]="w.agentAcknowledged">
                  {{ w.agentAcknowledged ? 'Acknowledged' : 'Delivered' }}
                </span>
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
    .metric-value.warning { color: #dc2626; }
    .metric-sub { font-size: 12px; color: #94a3b8; margin-top: 4px; }
    .table-wrapper { overflow-x: auto; background: #ffffff; border: 1px solid #e2e8f0; border-radius: 8px; }
    .data-table { width: 100%; border-collapse: collapse; text-align: left; font-size: 13px; }
    .data-table th, .data-table td { padding: 12px 16px; border-bottom: 1px solid #e2e8f0; }
    .data-table th { background: #f8fafc; color: #475569; font-weight: 600; }
    .primary-cell strong { display: block; color: #0f172a; }
    .sub-text { font-size: 11px; color: #64748b; }
    .mode-pill { padding: 2px 8px; border-radius: 4px; font-size: 11px; font-weight: 600; }
    .mode-whisper { background: #dbeafe; color: #1d4ed8; }
    .mode-takeover { background: #fee2e2; color: #b91c1c; }
    .status-pill { padding: 2px 8px; border-radius: 9999px; font-size: 11px; font-weight: 600; }
    .status-clean { background: #dcfce7; color: #15803d; }
    .actions-cell { display: flex; gap: 6px; }
    .whisper-btn { background: #2563eb; color: #ffffff; border: none; padding: 6px 10px; border-radius: 4px; font-size: 12px; font-weight: 500; cursor: pointer; }
    .takeover-btn { background: #dc2626; color: #ffffff; border: none; padding: 6px 10px; border-radius: 4px; font-size: 12px; font-weight: 500; cursor: pointer; }
    .whispers-list { display: flex; flex-direction: column; gap: 10px; margin-top: 14px; }
    .whisper-item { background: #ffffff; border: 1px solid #e2e8f0; border-radius: 8px; padding: 12px 16px; }
    .w-header { display: flex; justify-content: space-between; margin-bottom: 6px; }
    .category-badge { background: #f1f5f9; color: #334155; font-size: 11px; font-weight: 600; padding: 2px 6px; border-radius: 4px; }
    .call-ref { font-size: 11px; color: #64748b; }
    .w-body { font-size: 13px; font-style: italic; color: #0f172a; margin-bottom: 6px; }
    .w-footer { display: flex; justify-content: space-between; font-size: 11px; color: #64748b; }
    .ack-badge { padding: 1px 6px; border-radius: 4px; background: #f1f5f9; color: #64748b; }
    .ack-badge.acked { background: #dcfce7; color: #15803d; }
  `]
})
export class TelephonyWhisperCockpitComponent {
  activeCallsCount = signal(2);
  avgLatency = signal(73.2);
  totalWhispers = signal(2);
  takeoverCount = signal(0);

  sessions = signal<TelephonyCallSession[]>([
    {
      callSessionId: 'CALL-WHISP-001',
      customerPhoneNumber: '+1-415-555-8910',
      agentName: 'Jordan Reed (Mid-Market AE)',
      supervisorName: 'Elena Rostova (Sales Director)',
      activeSupervisionMode: 'WHISPERING',
      roundTripLatencyMs: 82.4,
      complianceStatus: 'CLEAN',
      injectedWhispers: ['Highlight ISO27001 cert', 'Stand firm on multi-year 15% discount cap']
    },
    {
      callSessionId: 'CALL-WHISP-002',
      customerPhoneNumber: '+44-20-7946-0922',
      agentName: 'David Sterling (Enterprise Rep)',
      supervisorName: 'Elena Rostova (Sales Director)',
      activeSupervisionMode: 'MONITORING',
      roundTripLatencyMs: 64.1,
      complianceStatus: 'CLEAN',
      injectedWhispers: ['Mention UK sovereign cloud hosting option in London']
    }
  ]);

  whispers = signal<WhisperPromptRecord[]>([
    {
      whisperId: 'WSP-001',
      callSessionId: 'CALL-WHISP-001',
      promptText: 'Highlight ISO27001 and SOC2 Type II audit attestations',
      contextCategory: 'TECH_SPEC',
      supervisorName: 'Elena Rostova (Sales Director)',
      agentAcknowledged: true
    },
    {
      whisperId: 'WSP-002',
      callSessionId: 'CALL-WHISP-001',
      promptText: 'Stand firm on multi-year 15% discount cap',
      contextCategory: 'PRICING_CONCESSION',
      supervisorName: 'Elena Rostova (Sales Director)',
      agentAcknowledged: true
    }
  ]);

  injectWhisper(session: TelephonyCallSession): void {
    const newWhisper: WhisperPromptRecord = {
      whisperId: `WSP-${Math.floor(Math.random() * 900 + 100)}`,
      callSessionId: session.callSessionId,
      promptText: 'Offer customer quarterly billing cycle instead of monthly with 5% fee waiver',
      contextCategory: 'PRICING_CONCESSION',
      supervisorName: 'Elena Rostova (Sales Director)',
      agentAcknowledged: false
    };
    this.whispers.update(list => [newWhisper, ...list]);
    this.totalWhispers.update(v => v + 1);
  }

  takeoverCall(session: TelephonyCallSession): void {
    this.sessions.update(list =>
      list.map(s => s.callSessionId === session.callSessionId ? { ...s, activeSupervisionMode: 'FULL_TAKEOVER' } : s)
    );
    this.takeoverCount.update(v => v + 1);
  }
}
