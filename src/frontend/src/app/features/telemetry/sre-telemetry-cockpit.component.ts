import { Component, signal, computed } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';

export type AlertSeverity = 'Critical' | 'Warning' | 'Info';

export interface SreAlert {
  id: string;
  severity: AlertSeverity;
  service: string;
  summary: string;
  firedAt: string;
  burnRate?: string;
  acknowledged: boolean;
}

export interface TraceparentComponents {
  version: string;
  traceId: string;
  parentId: string;
  traceFlags: string;
  isSampled: boolean;
}

@Component({
  selector: 'app-sre-telemetry-cockpit',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterLink],
  template: `
    <div class="min-h-screen bg-slate-950 text-slate-100 flex flex-col font-sans" dir="ltr">
      <!-- Header -->
      <header class="border-b border-slate-800 bg-slate-900/80 backdrop-blur px-6 py-4 flex items-center justify-between sticky top-0 z-40">
        <div class="flex items-center gap-4">
          <a routerLink="/" class="text-xl font-black bg-gradient-to-r from-emerald-400 via-teal-300 to-cyan-400 bg-clip-text text-transparent hover:opacity-90">
            SGrowing
          </a>
          <span class="text-slate-600">/</span>
          <div class="flex items-center gap-2">
            <span class="text-sm font-semibold text-white">SRE Telemetry & Resilience Radar</span>
            <span class="text-xs px-2.5 py-0.5 rounded-full bg-cyan-500/10 text-cyan-400 border border-cyan-500/20 font-medium">
              OpenTelemetry & W3C Trace Context
            </span>
          </div>
        </div>

        <div class="flex items-center gap-3">
          <!-- Overall Status Badge -->
          <div class="flex items-center gap-2 px-3 py-1.5 rounded-lg border" [class]="statusContainerClass()">
            <span class="w-2.5 h-2.5 rounded-full" [class]="statusDotClass()"></span>
            <span class="text-xs font-mono font-bold tracking-wider">{{ overallStatus() }}</span>
            <span class="text-xs font-mono text-slate-400">({{ healthScore() }}%)</span>
          </div>

          <!-- Synthetic Latency Toggle Button -->
          <button
            type="button"
            (click)="toggleSyntheticDegradation()"
            [class]="isDegraded()
              ? 'px-3.5 py-2 rounded-lg bg-emerald-600 hover:bg-emerald-500 text-xs font-bold text-white shadow-lg transition'
              : 'px-3.5 py-2 rounded-lg bg-rose-950/80 hover:bg-rose-900 border border-rose-700/80 text-xs font-bold text-rose-300 shadow-lg shadow-rose-950/30 transition flex items-center gap-1.5'"
          >
            @if (isDegraded()) {
              <span>🔄 Recover Normal State</span>
            } @else {
              <span>⚠️ Inject Synthetic Latency / Degraded State</span>
            }
          </button>
        </div>
      </header>

      <!-- Main Layout -->
      <main class="flex-1 p-6 max-w-7xl w-full mx-auto space-y-6">
        <!-- 4 Key SRE KPI Cards -->
        <section class="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-4 gap-5">
          <!-- KPI 1: SLO Error Budget -->
          <div class="bg-slate-900/60 border border-slate-800 rounded-2xl p-5 shadow-xl flex flex-col justify-between relative overflow-hidden">
            <div>
              <div class="flex items-center justify-between text-xs text-slate-400 mb-1">
                <span class="font-bold uppercase tracking-wider">99.9% SLO Error Budget</span>
                <span class="text-lg">🛡️</span>
              </div>
              <div class="text-2xl font-black font-mono tracking-tight text-white mt-1">
                {{ errorBudgetPercent() }}%
              </div>
              <p class="text-[11px] text-slate-400 mt-1">30-day rolling error budget remaining</p>
            </div>
            <!-- Progress Bar -->
            <div class="mt-4 pt-3 border-t border-slate-800/80">
              <div class="w-full bg-slate-800 rounded-full h-2 overflow-hidden">
                <div
                  class="h-2 rounded-full transition-all duration-500"
                  [style.width.%]="errorBudgetPercent()"
                  [class]="errorBudgetPercent() < 50 ? 'bg-rose-500' : 'bg-emerald-400'"
                ></div>
              </div>
            </div>
          </div>

          <!-- KPI 2: P99 Webhook Latency -->
          <div class="bg-slate-900/60 border border-slate-800 rounded-2xl p-5 shadow-xl flex flex-col justify-between">
            <div>
              <div class="flex items-center justify-between text-xs text-slate-400 mb-1">
                <span class="font-bold uppercase tracking-wider">P99 Webhook Latency</span>
                <span class="text-lg">⚡</span>
              </div>
              <div class="text-2xl font-black font-mono tracking-tight mt-1" [class]="p99LatencyMs() > 100 ? 'text-rose-400' : 'text-cyan-400'">
                {{ p99LatencyMs() }} <span class="text-sm font-normal text-slate-400">ms</span>
              </div>
              <p class="text-[11px] text-slate-400 mt-1">Target SLA: &lt; 50ms ingestion latency</p>
            </div>
            <div class="mt-4 pt-3 border-t border-slate-800/80 flex items-center justify-between text-[11px]">
              <span class="text-slate-500">P50: 12.3ms</span>
              <span class="text-slate-500">P95: 24.1ms</span>
              <span class="font-mono" [class]="p99LatencyMs() > 100 ? 'text-rose-400 font-bold' : 'text-emerald-400'">
                {{ p99LatencyMs() > 100 ? 'SLA Breached' : 'Within SLA' }}
              </span>
            </div>
          </div>

          <!-- KPI 3: Ingestion Rate -->
          <div class="bg-slate-900/60 border border-slate-800 rounded-2xl p-5 shadow-xl flex flex-col justify-between">
            <div>
              <div class="flex items-center justify-between text-xs text-slate-400 mb-1">
                <span class="font-bold uppercase tracking-wider">Clickstream Ingestion</span>
                <span class="text-lg">📈</span>
              </div>
              <div class="text-2xl font-black font-mono tracking-tight text-white mt-1">
                {{ eventsPerSec().toLocaleString() }} <span class="text-sm font-normal text-slate-400">EPS</span>
              </div>
              <p class="text-[11px] text-slate-400 mt-1">High-throughput Kafka / EventBridge stream</p>
            </div>
            <div class="mt-4 pt-3 border-t border-slate-800/80 flex items-center justify-between text-[11px]">
              <span class="text-slate-500">Peak Capacity: 50,000 EPS</span>
              <span class="text-emerald-400 font-mono font-semibold">Active Stream</span>
            </div>
          </div>

          <!-- KPI 4: DLQ Depth -->
          <div class="bg-slate-900/60 border border-slate-800 rounded-2xl p-5 shadow-xl flex flex-col justify-between">
            <div>
              <div class="flex items-center justify-between text-xs text-slate-400 mb-1">
                <span class="font-bold uppercase tracking-wider">Dead Letter Queue (DLQ)</span>
                <span class="text-lg">📮</span>
              </div>
              <div class="text-2xl font-black font-mono tracking-tight mt-1" [class]="dlqDepth() > 0 ? 'text-amber-400' : 'text-emerald-400'">
                {{ dlqDepth() }} <span class="text-sm font-normal text-slate-400">messages</span>
              </div>
              <p class="text-[11px] text-slate-400 mt-1">Poison pills & unrecoverable order payloads</p>
            </div>
            <div class="mt-4 pt-3 border-t border-slate-800/80 flex items-center justify-between text-[11px]">
              @if (dlqDepth() > 0) {
                <button
                  type="button"
                  (click)="drainDlq()"
                  class="text-amber-300 hover:text-amber-200 font-bold underline"
                >
                  Drain & Replay DLQ
                </button>
              } @else {
                <span class="text-emerald-400 font-mono">Zero Poison Queue</span>
              }
              <span class="text-slate-500">Auto-Retry x3</span>
            </div>
          </div>
        </section>

        <!-- Active SRE Alerts Table -->
        <section class="bg-slate-900/60 border border-slate-800 rounded-2xl p-6 shadow-xl space-y-4">
          <div class="flex items-center justify-between">
            <div class="flex items-center gap-2">
              <span class="text-lg">🚨</span>
              <h2 class="text-sm font-bold uppercase tracking-wider text-slate-300">
                Active SRE & Synthetic Incident Alerts ({{ activeAlerts().length }})
              </h2>
            </div>
            @if (activeAlerts().length > 0) {
              <button
                type="button"
                (click)="acknowledgeAllAlerts()"
                class="px-2.5 py-1 rounded-lg bg-slate-800 hover:bg-slate-700 text-slate-300 text-xs font-semibold transition"
              >
                Acknowledge All
              </button>
            }
          </div>

          @if (activeAlerts().length === 0) {
            <div class="p-8 text-center bg-slate-950/40 rounded-xl border border-slate-800/80">
              <div class="text-emerald-400 text-2xl mb-1">✅</div>
              <h3 class="text-xs font-bold text-white">All Systems Operational</h3>
              <p class="text-xs text-slate-400 mt-1">No active incidents or error budget threshold alerts detected across the sovereign mesh.</p>
            </div>
          } @else {
            <div class="overflow-x-auto">
              <table class="w-full text-left text-xs">
                <thead>
                  <tr class="border-b border-slate-800 text-[11px] uppercase tracking-wider text-slate-500">
                    <th class="py-2.5 px-3">Severity</th>
                    <th class="py-2.5 px-3">Service / Subsystem</th>
                    <th class="py-2.5 px-3">Summary & Symptoms</th>
                    <th class="py-2.5 px-3">Burn Rate</th>
                    <th class="py-2.5 px-3">Fired At</th>
                    <th class="py-2.5 px-3 text-right">Action</th>
                  </tr>
                </thead>
                <tbody class="divide-y divide-slate-800/60">
                  @for (alert of activeAlerts(); track alert.id) {
                    <tr class="hover:bg-slate-900/40 transition">
                      <td class="py-3 px-3">
                        <span [class]="getAlertSeverityBadgeClass(alert.severity)">
                          {{ alert.severity }}
                        </span>
                      </td>
                      <td class="py-3 px-3 font-mono font-medium text-slate-200">
                        {{ alert.service }}
                      </td>
                      <td class="py-3 px-3 text-slate-300 max-w-md">
                        {{ alert.summary }}
                      </td>
                      <td class="py-3 px-3 font-mono text-amber-400">
                        {{ alert.burnRate ?? '1.0x' }}
                      </td>
                      <td class="py-3 px-3 font-mono text-slate-400 text-[11px]">
                        {{ alert.firedAt }}
                      </td>
                      <td class="py-3 px-3 text-right">
                        @if (alert.acknowledged) {
                          <span class="text-slate-500 font-mono text-[10px]">Acked</span>
                        } @else {
                          <button
                            type="button"
                            (click)="acknowledgeAlert(alert.id)"
                            class="px-2 py-1 rounded bg-slate-800 hover:bg-slate-700 text-cyan-300 font-bold text-[10px] transition"
                          >
                            Ack
                          </button>
                        }
                      </td>
                    </tr>
                  }
                </tbody>
              </table>
            </div>
          }
        </section>

        <!-- W3C Distributed Traceparent Inspector -->
        <section class="bg-slate-900/60 border border-slate-800 rounded-2xl p-6 shadow-xl space-y-5">
          <div class="flex items-center justify-between pb-3 border-b border-slate-800">
            <div class="flex items-center gap-2">
              <span class="text-lg">🌐</span>
              <div>
                <h2 class="text-sm font-bold text-white">W3C Distributed Traceparent Context Inspector</h2>
                <p class="text-xs text-slate-400 mt-0.5">Distributed cross-service trace propagation header compliant with W3C Recommendation</p>
              </div>
            </div>

            <div class="flex items-center gap-2">
              <button
                type="button"
                (click)="generateNewSyntheticTrace()"
                class="px-3 py-1.5 rounded-lg bg-slate-800 hover:bg-slate-700 text-xs font-semibold text-slate-200 border border-slate-700 transition"
              >
                🔄 New Synthetic Trace
              </button>
              <button
                type="button"
                (click)="copyTraceparentToClipboard()"
                class="px-3 py-1.5 rounded-lg bg-cyan-600 hover:bg-cyan-500 text-xs font-bold text-white shadow transition flex items-center gap-1.5"
              >
                @if (copiedTrace()) {
                  <span>✅ Copied!</span>
                } @else {
                  <span>📋 Copy Header</span>
                }
              </button>
            </div>
          </div>

          <!-- Traceparent Full String Field -->
          <div class="bg-slate-950 p-3.5 rounded-xl border border-slate-800 flex items-center justify-between gap-3">
            <div class="flex-1 font-mono text-xs text-emerald-400 select-all overflow-x-auto whitespace-nowrap">
              <span class="text-slate-500">traceparent: </span>{{ traceParentHeader() }}
            </div>
          </div>

          <!-- Trace Breakdown Grid -->
          <div class="grid grid-cols-1 md:grid-cols-4 gap-4">
            <div class="bg-slate-950/80 p-3 rounded-xl border border-slate-800/80">
              <span class="text-[10px] uppercase font-bold text-slate-500 block">Version (2 Hex)</span>
              <span class="text-xs font-mono font-bold text-cyan-300 mt-1 block">{{ parsedTrace().version }}</span>
              <span class="text-[10px] text-slate-500 mt-1 block">W3C Specification Standard</span>
            </div>

            <div class="bg-slate-950/80 p-3 rounded-xl border border-slate-800/80 md:col-span-2">
              <span class="text-[10px] uppercase font-bold text-slate-500 block">Trace ID (32 Hex)</span>
              <span class="text-xs font-mono font-bold text-indigo-300 mt-1 block truncate">{{ parsedTrace().traceId }}</span>
              <span class="text-[10px] text-slate-500 mt-1 block">Global end-to-end mission correlation identifier</span>
            </div>

            <div class="bg-slate-950/80 p-3 rounded-xl border border-slate-800/80">
              <span class="text-[10px] uppercase font-bold text-slate-500 block">Parent / Span ID (16 Hex)</span>
              <span class="text-xs font-mono font-bold text-teal-300 mt-1 block">{{ parsedTrace().parentId }}</span>
              <span class="text-[10px] text-slate-500 mt-1 block">Ingress boundary span</span>
            </div>
          </div>

          <div class="p-3 bg-slate-950/60 rounded-xl border border-slate-800/80 flex items-center justify-between text-xs">
            <span class="text-slate-400">Trace Flags: <strong class="text-white font-mono">{{ parsedTrace().traceFlags }}</strong></span>
            <span class="font-mono text-[11px] px-2 py-0.5 rounded" [class]="parsedTrace().isSampled ? 'bg-emerald-500/10 text-emerald-300 border border-emerald-500/30' : 'bg-slate-800 text-slate-400'">
              {{ parsedTrace().isSampled ? 'Sampled (Recorded for Jaeger/Zipkin/OTel)' : 'Not Sampled' }}
            </span>
          </div>
        </section>
      </main>
    </div>
  `
})
export class SreTelemetryCockpitComponent {
  // Required signals matching spec
  readonly healthScore = signal<number>(99.4);
  readonly overallStatus = signal<string>('HEALTHY');
  readonly errorBudgetPercent = signal<number>(98.7);
  readonly p99LatencyMs = signal<number>(38.4);
  readonly eventsPerSec = signal<number>(10450);
  readonly dlqDepth = signal<number>(0);
  readonly activeAlerts = signal<SreAlert[]>([]);
  readonly traceParentHeader = signal<string>('00-4bf92f3577b34da6a3ce929d0e0e4736-00f067aa0ba902b7-01');

  // UI state signals
  readonly copiedTrace = signal<boolean>(false);

  // Computed signals
  readonly isDegraded = computed<boolean>(() => {
    return this.overallStatus() !== 'HEALTHY' || this.healthScore() < 90;
  });

  readonly parsedTrace = computed<TraceparentComponents>(() => {
    const raw = this.traceParentHeader().trim();
    const parts = raw.split('-');
    if (parts.length === 4) {
      return {
        version: parts[0],
        traceId: parts[1],
        parentId: parts[2],
        traceFlags: parts[3],
        isSampled: parts[3] === '01'
      };
    }
    return {
      version: '00',
      traceId: '00000000000000000000000000000000',
      parentId: '0000000000000000',
      traceFlags: '00',
      isSampled: false
    };
  });

  toggleSyntheticDegradation(): void {
    if (this.isDegraded()) {
      // Recover to healthy
      this.healthScore.set(99.4);
      this.overallStatus.set('HEALTHY');
      this.errorBudgetPercent.set(98.7);
      this.p99LatencyMs.set(38.4);
      this.eventsPerSec.set(10450);
      this.dlqDepth.set(0);
      this.activeAlerts.set([]);
    } else {
      // Degrade system
      this.healthScore.set(78.2);
      this.overallStatus.set('DEGRADED');
      this.errorBudgetPercent.set(41.3);
      this.p99LatencyMs.set(412.8);
      this.eventsPerSec.set(14200);
      this.dlqDepth.set(234);

      const alerts: SreAlert[] = [
        {
          id: 'alt-001',
          severity: 'Critical',
          service: 'Storefront-Webhook-Ingress',
          summary: 'Webhook Ingestion Latency Breach: P99 exceeded 250ms SLA (current: 412.8ms)',
          firedAt: new Date().toLocaleTimeString(),
          burnRate: '14.2x',
          acknowledged: false
        },
        {
          id: 'alt-002',
          severity: 'Warning',
          service: 'Dead-Letter-Queue-Worker',
          summary: 'Dead Letter Queue (DLQ) Backlog: 234 unparseable orders awaiting retry',
          firedAt: new Date().toLocaleTimeString(),
          burnRate: '2.5x',
          acknowledged: false
        },
        {
          id: 'alt-003',
          severity: 'Warning',
          service: 'Error-Budget-Governor',
          summary: 'SLO Burn Rate: 14x 1-hour error budget depletion rate detected',
          firedAt: new Date().toLocaleTimeString(),
          burnRate: '14.0x',
          acknowledged: false
        }
      ];
      this.activeAlerts.set(alerts);
    }
  }

  acknowledgeAlert(alertId: string): void {
    const updated = this.activeAlerts().map(a => {
      if (a.id === alertId) {
        return { ...a, acknowledged: true };
      }
      return a;
    });
    this.activeAlerts.set(updated);
  }

  acknowledgeAllAlerts(): void {
    const updated = this.activeAlerts().map(a => ({ ...a, acknowledged: true }));
    this.activeAlerts.set(updated);
  }

  drainDlq(): void {
    this.dlqDepth.set(0);
    const updated = this.activeAlerts().filter(a => a.service !== 'Dead-Letter-Queue-Worker');
    this.activeAlerts.set(updated);
  }

  generateNewSyntheticTrace(): void {
    const randomHex = (len: number) => {
      const chars = '0123456789abcdef';
      let result = '';
      for (let i = 0; i < len; i++) {
        result += chars[Math.floor(Math.random() * chars.length)];
      }
      return result;
    };

    const newTraceId = randomHex(32);
    const newSpanId = randomHex(16);
    this.traceParentHeader.set(`00-${newTraceId}-${newSpanId}-01`);
  }

  copyTraceparentToClipboard(): void {
    if (typeof navigator !== 'undefined' && navigator.clipboard) {
      navigator.clipboard.writeText(this.traceParentHeader());
    }
    this.copiedTrace.set(true);
    setTimeout(() => {
      this.copiedTrace.set(false);
    }, 2000);
  }

  statusContainerClass(): string {
    return this.isDegraded()
      ? 'bg-rose-500/10 border-rose-500/30 text-rose-300'
      : 'bg-emerald-500/10 border-emerald-500/30 text-emerald-300';
  }

  statusDotClass(): string {
    return this.isDegraded() ? 'bg-rose-500 animate-pulse' : 'bg-emerald-400';
  }

  getAlertSeverityBadgeClass(severity: AlertSeverity): string {
    switch (severity) {
      case 'Critical':
        return 'text-[10px] font-mono font-bold px-2 py-0.5 rounded-full bg-rose-500/20 text-rose-300 border border-rose-500/30';
      case 'Warning':
        return 'text-[10px] font-mono font-bold px-2 py-0.5 rounded-full bg-amber-500/20 text-amber-300 border border-amber-500/30';
      case 'Info':
        return 'text-[10px] font-mono font-bold px-2 py-0.5 rounded-full bg-cyan-500/20 text-cyan-300 border border-cyan-500/30';
      default:
        return 'text-[10px] font-mono px-2 py-0.5 rounded-full bg-slate-800 text-slate-300';
    }
  }
}
