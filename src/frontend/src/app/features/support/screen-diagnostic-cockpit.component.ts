import { Component, signal, computed } from '@angular/core';
import { CommonModule } from '@angular/common';

export interface ScreenSessionItem {
  sessionId: string;
  customerId: string;
  activeUrl: string;
  issueCategory: string;
  detectedStickingPoint: string;
  resolutionProgressPct: number;
  status: string;
}

export interface HighlightedElementItem {
  elementId: string;
  elementType: string;
  confidenceScore: number;
  labelText: string;
}

@Component({
  selector: 'app-screen-diagnostic-cockpit',
  standalone: true,
  imports: [CommonModule],
  template: `
    <div class="p-6 max-w-7xl mx-auto space-y-6" dir="ltr" data-testid="screen-diagnostic-cockpit">
      <!-- Header -->
      <div class="flex flex-col md:flex-row justify-between items-start md:items-center gap-4 bg-slate-900 border border-slate-800 p-6 rounded-2xl shadow-xl">
        <div>
          <div class="flex items-center gap-3">
            <span class="px-3 py-1 text-xs font-semibold rounded-full bg-cyan-500/20 text-cyan-400 border border-cyan-500/30">
              Horizon 108 • Domain E16 & F13
            </span>
            <span class="text-xs text-slate-400">Multimodal Screen Share &amp; Synthetic Cursor Guidance</span>
          </div>
          <h1 class="text-2xl font-bold text-white mt-2">AI Agent Screen Diagnostic &amp; Cursor Co-Pilot</h1>
          <p class="text-slate-400 text-sm mt-1">
            Frame differential perceptual hashing, UI bounding box classification, and real-time Bezier cursor guidance.
          </p>
        </div>

        <div class="flex items-center gap-3">
          <button
            (click)="analyzeActiveFrame()"
            data-testid="analyze-frame-btn"
            class="px-4 py-2 bg-cyan-600 hover:bg-cyan-500 text-white text-sm font-medium rounded-xl transition shadow-lg shadow-cyan-600/30 flex items-center gap-2">
            Analyze Screen Frame
          </button>
          <button
            (click)="generateCursorTrajectory()"
            data-testid="generate-trajectory-btn"
            class="px-4 py-2 bg-indigo-600 hover:bg-indigo-500 text-white text-sm font-medium rounded-xl transition shadow-lg shadow-indigo-600/30 flex items-center gap-2">
            Dispatch Cursor Trajectory
          </button>
        </div>
      </div>

      <!-- Metrics -->
      <div class="grid grid-cols-1 md:grid-cols-4 gap-4">
        <div class="bg-slate-900/80 border border-slate-800 p-5 rounded-2xl">
          <span class="text-xs font-medium text-slate-400">Active Screen Sessions</span>
          <p class="text-2xl font-bold text-white mt-1" data-testid="metric-active-sessions">{{ activeSessions() }}</p>
          <span class="text-xs text-cyan-400 mt-1 block">Live WebRTC Video Streams</span>
        </div>

        <div class="bg-slate-900/80 border border-slate-800 p-5 rounded-2xl">
          <span class="text-xs font-medium text-slate-400">Avg Resolution Time</span>
          <p class="text-2xl font-bold text-emerald-400 mt-1" data-testid="metric-avg-resolution">{{ avgResolutionSeconds() }}s</p>
          <span class="text-xs text-slate-400 mt-1 block">-62% vs Human Screen Sharing</span>
        </div>

        <div class="bg-slate-900/80 border border-slate-800 p-5 rounded-2xl">
          <span class="text-xs font-medium text-slate-400">Element Detection Accuracy</span>
          <p class="text-2xl font-bold text-indigo-400 mt-1" data-testid="metric-accuracy">{{ detectionAccuracy() }}%</p>
          <span class="text-xs text-indigo-300 mt-1 block">YOLO-UI Vision Backbone</span>
        </div>

        <div class="bg-slate-900/80 border border-slate-800 p-5 rounded-2xl">
          <span class="text-xs font-medium text-slate-400">Autonomous Guidance Success</span>
          <p class="text-2xl font-bold text-white mt-1" data-testid="metric-guidance-success">{{ guidanceSuccess() }}%</p>
          <span class="text-xs text-emerald-400 mt-1 block">Zero-Human Escalations</span>
        </div>
      </div>

      <!-- Live Sessions Table -->
      <div class="bg-slate-900/80 border border-slate-800 rounded-2xl p-6">
        <h2 class="text-lg font-bold text-white mb-4">Active Support Co-Pilot Sessions</h2>
        <div class="overflow-x-auto">
          <table class="w-full text-left text-sm text-slate-300">
            <thead class="text-xs uppercase bg-slate-800/60 text-slate-400 border-b border-slate-700">
              <tr>
                <th class="py-3 px-4">Session ID</th>
                <th class="py-3 px-4">Customer</th>
                <th class="py-3 px-4">Category</th>
                <th class="py-3 px-4">Detected Sticking Point</th>
                <th class="py-3 px-4">Progress</th>
                <th class="py-3 px-4 text-right">State</th>
              </tr>
            </thead>
            <tbody class="divide-y divide-slate-800">
              @for (sess of sessions(); track sess.sessionId) {
                <tr class="hover:bg-slate-800/30 transition">
                  <td class="py-3 px-4 font-mono text-xs text-slate-400">{{ sess.sessionId }}</td>
                  <td class="py-3 px-4 font-semibold text-white">{{ sess.customerId }}</td>
                  <td class="py-3 px-4 text-slate-300">{{ sess.issueCategory }}</td>
                  <td class="py-3 px-4 text-amber-300 font-medium">{{ sess.detectedStickingPoint }}</td>
                  <td class="py-3 px-4">
                    <div class="w-32 bg-slate-800 rounded-full h-2">
                      <div class="bg-cyan-500 h-2 rounded-full" [style.width.%]="sess.resolutionProgressPct"></div>
                    </div>
                  </td>
                  <td class="py-3 px-4 text-right">
                    <span [class]="sess.status === 'GuidingCursor' ? 'px-2 py-1 text-xs rounded-full bg-cyan-500/20 text-cyan-400 border border-cyan-500/30 font-medium' : 'px-2 py-1 text-xs rounded-full bg-slate-800 text-slate-400 font-medium'">
                      {{ sess.status }}
                    </span>
                  </td>
                </tr>
              }
            </tbody>
          </table>
        </div>
      </div>

      <!-- Detected UI Elements Canvas Preview -->
      <div class="bg-slate-900/80 border border-slate-800 rounded-2xl p-6">
        <h2 class="text-lg font-bold text-white mb-4">Detected Interactive UI Elements</h2>
        <div class="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-4 gap-4">
          @for (elem of highlightedElements(); track elem.elementId) {
            <div class="bg-slate-800/60 border border-slate-700/60 p-4 rounded-xl flex flex-col justify-between">
              <div>
                <div class="flex justify-between items-center mb-2">
                  <span class="text-xs font-mono text-cyan-400">{{ elem.elementId }}</span>
                  <span class="px-2 py-0.5 text-xs rounded bg-slate-700 text-slate-300">{{ elem.elementType }}</span>
                </div>
                <p class="text-sm font-semibold text-white">{{ elem.labelText }}</p>
              </div>
              <div class="mt-3 pt-2 border-t border-slate-700/50 flex justify-between items-center text-xs">
                <span class="text-slate-400">Confidence:</span>
                <span class="font-bold text-emerald-400">{{ (elem.confidenceScore * 100) | number:'1.1-1' }}%</span>
              </div>
            </div>
          }
        </div>
      </div>
    </div>
  `
})
export class ScreenDiagnosticCockpitComponent {
  activeSessions = signal<number>(3);
  avgResolutionSeconds = signal<number>(95);
  detectionAccuracy = signal<number>(97.4);
  guidanceSuccess = signal<number>(92.8);

  sessions = signal<ScreenSessionItem[]>([
    { sessionId: 'SESS-SCR-401', customerId: 'CUST-ACME-88', activeUrl: 'https://app.sgrowing.io/settings/integrations', issueCategory: 'OAuthConfiguration', detectedStickingPoint: 'Missing Redirect URI Callback', resolutionProgressPct: 65, status: 'GuidingCursor' },
    { sessionId: 'SESS-SCR-402', customerId: 'CUST-STRIPE-12', activeUrl: 'https://app.sgrowing.io/settings/billing', issueCategory: 'BillingCardUpdate', detectedStickingPoint: 'Zip Code Verification Mismatch', resolutionProgressPct: 80, status: 'GuidingCursor' },
    { sessionId: 'SESS-SCR-403', customerId: 'CUST-NEXUS-99', activeUrl: 'https://app.sgrowing.io/security/api-keys', issueCategory: 'WebhookSecrets', detectedStickingPoint: 'Unchecked Webhook Signing Secret', resolutionProgressPct: 35, status: 'Diagnosing' }
  ]);

  highlightedElements = signal<HighlightedElementItem[]>([
    { elementId: 'UI-EL-01', elementType: 'TextInput', confidenceScore: 0.98, labelText: 'OAuth Redirect URI Field' },
    { elementId: 'UI-EL-02', elementType: 'Button', confidenceScore: 0.96, labelText: 'Save Callback URL' },
    { elementId: 'UI-EL-03', elementType: 'Dropdown', confidenceScore: 0.94, labelText: 'Grant Type Scopes' },
    { elementId: 'UI-EL-04', elementType: 'Toast', confidenceScore: 0.99, labelText: 'Invalid Client Secret Warning' }
  ]);

  analyzeActiveFrame(): void {
    this.detectionAccuracy.set(98.6);
  }

  generateCursorTrajectory(): void {
    this.sessions.update(list =>
      list.map(s => s.sessionId === 'SESS-SCR-401' ? { ...s, resolutionProgressPct: 100, status: 'Resolved' } : s)
    );
  }
}
