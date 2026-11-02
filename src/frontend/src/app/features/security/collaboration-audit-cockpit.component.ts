import { Component, signal, computed } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';

export interface ActiveCollaboratorUi {
  id: string;
  name: string;
  resource: string;
  activeField: string;
  cursor: { x: number; y: number };
  lastActive: string;
}

export interface SecurityAuditEntryUi {
  id: string;
  user: string;
  action: string;
  target: string;
  country: string;
  severity: 'Info' | 'Warning' | 'Critical';
  timestamp: string;
}

@Component({
  selector: 'app-collaboration-audit-cockpit',
  standalone: true,
  imports: [CommonModule, FormsModule],
  template: `
    <div class="collaboration-audit-cockpit p-6 max-w-7xl mx-auto space-y-6" dir="ltr">
      <!-- Header -->
      <div class="flex flex-col md:flex-row md:items-center md:justify-between gap-4 border-b pb-6">
        <div>
          <div class="flex items-center gap-3">
            <h1 class="text-2xl font-bold tracking-tight text-slate-900">Global Real-Time Collaboration & Security Audit Heatmap</h1>
            <span class="inline-flex items-center px-2.5 py-0.5 rounded-full text-xs font-semibold bg-emerald-100 text-emerald-800">
              CRDT Multi-Cursor Active
            </span>
          </div>
          <p class="text-sm text-slate-500 mt-1">
            Real-time conflict-free replicated data presence across funnels & journeys, paired with zero-trust geofence security logs.
          </p>
        </div>

        <div class="flex items-center gap-3">
          <button 
            (click)="simulateHeartbeat()"
            class="px-4 py-2 bg-emerald-600 hover:bg-emerald-700 text-white rounded-lg font-medium text-xs transition-colors shadow-sm"
            data-testid="btn-send-heartbeat">
            + Emit Cursor Heartbeat
          </button>
        </div>
      </div>

      <!-- Collaboration & Security KPIs -->
      <div class="grid grid-cols-1 sm:grid-cols-4 gap-4" data-testid="collab-audit-kpis">
        <div class="bg-white border rounded-xl p-4 shadow-sm">
          <div class="text-xs text-slate-500 font-medium">Active Collaborators</div>
          <div class="text-2xl font-bold text-slate-900 mt-1 font-mono" data-testid="kpi-collaborators-count">
            {{ collaborators().length }}
          </div>
          <div class="text-[11px] text-emerald-600 mt-1 font-semibold">Live CRDT Multi-Cursor</div>
        </div>

        <div class="bg-white border rounded-xl p-4 shadow-sm">
          <div class="text-xs text-slate-500 font-medium">Critical Security Events</div>
          <div class="text-2xl font-bold text-rose-600 mt-1 font-mono" data-testid="kpi-critical-count">
            {{ criticalCount() }}
          </div>
          <div class="text-[11px] text-slate-400 mt-1">Immediate Alerting Active</div>
        </div>

        <div class="bg-white border rounded-xl p-4 shadow-sm">
          <div class="text-xs text-slate-500 font-medium">Geofence Violations</div>
          <div class="text-2xl font-bold text-amber-600 mt-1 font-mono" data-testid="kpi-geofence-count">
            {{ geofenceAlerts() }}
          </div>
          <div class="text-[11px] text-slate-400 mt-1">Non-Permitted Territories</div>
        </div>

        <div class="bg-white border rounded-xl p-4 shadow-sm">
          <div class="text-xs text-slate-500 font-medium">Sync Telemetry Latency</div>
          <div class="text-2xl font-bold text-slate-900 mt-1 font-mono" data-testid="kpi-latency">8.4 ms</div>
          <div class="text-[11px] text-indigo-600 mt-1 font-semibold">WebSocket Peer Mesh</div>
        </div>
      </div>

      <!-- Main Columns -->
      <div class="grid grid-cols-1 lg:grid-cols-3 gap-6">
        <!-- Live Collaborators Presence -->
        <div class="bg-white border rounded-xl p-5 shadow-sm space-y-4">
          <div class="flex items-center justify-between">
            <h3 class="font-bold text-slate-900 text-sm">Active Canvas Collaborators</h3>
            <span class="text-xs text-slate-400 font-mono">{{ collaborators().length }} online</span>
          </div>

          <div class="space-y-3" data-testid="collaborators-list">
            @for (collab of collaborators(); track collab.id) {
              <div class="p-3 border rounded-lg hover:border-slate-300 transition-colors space-y-1.5">
                <div class="flex justify-between items-center text-xs">
                  <span class="font-bold text-slate-800">{{ collab.name }}</span>
                  <span class="px-2 py-0.5 rounded text-[10px] font-mono font-bold bg-emerald-100 text-emerald-800">
                    Live
                  </span>
                </div>
                <div class="text-[11px] text-slate-500 font-mono">
                  Resource: <strong>{{ collab.resource }}</strong>
                </div>
                <div class="flex justify-between items-center text-[10px] text-slate-400 pt-1 border-t">
                  <span>Focus: {{ collab.activeField }}</span>
                  <span class="font-mono">({{ collab.cursor.x | number:'1.0-0' }}px, {{ collab.cursor.y | number:'1.0-0' }}px)</span>
                </div>
              </div>
            }
          </div>

          <!-- Emergency Session Revocation -->
          <div class="pt-3 border-t">
            <button 
              (click)="revokeCompromisedSession()"
              class="w-full py-2 bg-rose-50 hover:bg-rose-100 text-rose-700 border border-rose-200 rounded-lg text-xs font-semibold transition-colors"
              data-testid="btn-revoke-sessions">
              &times; Emergency Revoke Suspicious Sessions
            </button>
          </div>
        </div>

        <!-- Security Audit Trail -->
        <div class="lg:col-span-2 bg-white border rounded-xl p-5 shadow-sm space-y-4">
          <div class="flex items-center justify-between">
            <h3 class="font-bold text-slate-900 text-sm">Operational Security & Geofence Audit Trail</h3>
            <span class="text-xs text-slate-400 font-mono">Immutable Append Log</span>
          </div>

          <div class="overflow-x-auto" data-testid="audit-logs-table">
            <table class="w-full text-left text-xs">
              <thead class="bg-slate-50 border-b text-slate-500">
                <tr>
                  <th class="p-3">Event / User</th>
                  <th class="p-3">Action</th>
                  <th class="p-3">Target</th>
                  <th class="p-3">Severity</th>
                  <th class="p-3">Time</th>
                </tr>
              </thead>
              <tbody class="divide-y">
                @for (log of auditLogs(); track log.id) {
                  <tr class="hover:bg-slate-50">
                    <td class="p-3">
                      <div class="font-mono font-bold text-slate-900">{{ log.id }}</div>
                      <div class="text-[10px] text-slate-500">{{ log.user }}</div>
                    </td>
                    <td class="p-3 font-semibold text-slate-800">{{ log.action }}</td>
                    <td class="p-3 font-mono text-slate-600">{{ log.target }}</td>
                    <td class="p-3">
                      <span 
                        [class.bg-emerald-100]="log.severity === 'Info'"
                        [class.text-emerald-800]="log.severity === 'Info'"
                        [class.bg-amber-100]="log.severity === 'Warning'"
                        [class.text-amber-800]="log.severity === 'Warning'"
                        [class.bg-rose-100]="log.severity === 'Critical'"
                        [class.text-rose-800]="log.severity === 'Critical'"
                        class="px-2 py-0.5 rounded text-[10px] font-bold font-mono">
                        {{ log.severity }}
                      </span>
                    </td>
                    <td class="p-3 font-mono text-slate-400">{{ log.timestamp }}</td>
                  </tr>
                }
              </tbody>
            </table>
          </div>
        </div>
      </div>
    </div>
  `
})
export class CollaborationAuditCockpitComponent {
  geofenceAlerts = signal(1);

  collaborators = signal<ActiveCollaboratorUi[]>([
    { id: 'c1', name: 'Alice Chen (Lead Designer)', resource: 'FunnelCanvas-Enterprise', activeField: 'HeroBlock', cursor: { x: 420, y: 185 }, lastActive: 'Just now' },
    { id: 'c2', name: 'Bob Miller (RevOps)', resource: 'FunnelCanvas-Enterprise', activeField: 'PricingCards', cursor: { x: 780, y: 310 }, lastActive: '2s ago' }
  ]);

  auditLogs = signal<SecurityAuditEntryUi[]>([
    { id: 'evt_01', user: 'admin@sgrowing.io', action: 'RoleElevated', target: 'SuperAdminPolicy', country: 'US', severity: 'Warning', timestamp: '40m ago' },
    { id: 'evt_02', user: 'eve@contractor.ext', action: 'GeofenceViolation', target: 'ApiTokenEndpoint', country: 'RU', severity: 'Critical', timestamp: '15m ago' }
  ]);

  criticalCount = computed(() => {
    return this.auditLogs().filter(l => l.severity === 'Critical').length;
  });

  simulateHeartbeat() {
    const newCollab: ActiveCollaboratorUi = {
      id: 'c_' + Date.now(),
      name: 'Elena Rostova (Growth Engineer)',
      resource: 'FunnelCanvas-Enterprise',
      activeField: 'LeadCaptureForm',
      cursor: { x: 550, y: 440 },
      lastActive: 'Just now'
    };

    this.collaborators.update(list => [newCollab, ...list]);
  }

  revokeCompromisedSession() {
    this.collaborators.update(list => list.filter(c => !c.name.includes('eve')));

    const newLog: SecurityAuditEntryUi = {
      id: 'evt_' + Math.floor(100 + Math.random() * 900),
      user: 'security-bot@sgrowing.io',
      action: 'UserSessionsTerminated',
      target: 'AuthSessionManager',
      country: 'US',
      severity: 'Critical',
      timestamp: 'Just now'
    };

    this.auditLogs.update(logs => [newLog, ...logs]);
  }
}
