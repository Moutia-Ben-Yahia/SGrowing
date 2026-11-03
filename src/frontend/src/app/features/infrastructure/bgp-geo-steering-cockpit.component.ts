import { Component, signal, computed } from '@angular/core';
import { CommonModule } from '@angular/common';

export interface EdgePopItem {
  popId: string;
  city: string;
  country: string;
  region: string;
  bgpAsn: number;
  anycastIp: string;
  latencyMs: number;
  jitterMs: number;
  packetLossPct: number;
  healthStatus: string;
}

export interface GeoSteeringPolicyItem {
  policyId: string;
  domainName: string;
  recordType: string;
  routingStrategy: string;
  activeEndpointPopId: string;
  status: string;
}

@Component({
  selector: 'app-bgp-geo-steering-cockpit',
  standalone: true,
  imports: [CommonModule],
  template: `
    <div class="p-6 max-w-7xl mx-auto space-y-6" dir="ltr" data-testid="bgp-geo-steering-cockpit">
      <!-- Header -->
      <div class="flex flex-col md:flex-row justify-between items-start md:items-center gap-4 bg-slate-900 border border-slate-800 p-6 rounded-2xl shadow-xl">
        <div>
          <div class="flex items-center gap-3">
            <span class="px-3 py-1 text-xs font-semibold rounded-full bg-rose-500/20 text-rose-400 border border-rose-500/30">
              Horizon 110 • Domain K17 & A20
            </span>
            <span class="text-xs text-slate-400">Global BGP Anycast Health &amp; DNS Geo-Steering</span>
          </div>
          <h1 class="text-2xl font-bold text-white mt-2">BGP Anycast Edge Probing &amp; Geo-Steering DNS Mesh</h1>
          <p class="text-slate-400 text-sm mt-1">
            Active synthetic POP latency/loss probing, automated DNS routing overrides, and route flap dampening.
          </p>
        </div>

        <div class="flex items-center gap-3">
          <button
            (click)="triggerGlobalProbing()"
            data-testid="probe-pops-btn"
            class="px-4 py-2 bg-indigo-600 hover:bg-indigo-500 text-white text-sm font-medium rounded-xl transition shadow-lg shadow-indigo-600/30 flex items-center gap-2">
            Execute Synthetic Probes
          </button>
          <button
            (click)="overrideSteeringPolicy()"
            data-testid="override-steering-btn"
            class="px-4 py-2 bg-rose-600 hover:bg-rose-500 text-white text-sm font-medium rounded-xl transition shadow-lg shadow-rose-600/30 flex items-center gap-2">
            Trigger DNS Failover
          </button>
        </div>
      </div>

      <!-- Metrics -->
      <div class="grid grid-cols-1 md:grid-cols-4 gap-4">
        <div class="bg-slate-900/80 border border-slate-800 p-5 rounded-2xl">
          <span class="text-xs font-medium text-slate-400">Global Anycast POPs</span>
          <p class="text-2xl font-bold text-white mt-1" data-testid="metric-total-pops">{{ totalPops() }}</p>
          <span class="text-xs text-emerald-400 mt-1 block">Tier-1 BGP Anycast Mesh</span>
        </div>

        <div class="bg-slate-900/80 border border-slate-800 p-5 rounded-2xl">
          <span class="text-xs font-medium text-slate-400">Healthy POP Nodes</span>
          <p class="text-2xl font-bold text-emerald-400 mt-1" data-testid="metric-healthy-pops">{{ healthyPops() }} / {{ totalPops() }}</p>
          <span class="text-xs text-slate-400 mt-1 block">99.999% Transit Availability</span>
        </div>

        <div class="bg-slate-900/80 border border-slate-800 p-5 rounded-2xl">
          <span class="text-xs font-medium text-slate-400">Global Average Latency</span>
          <p class="text-2xl font-bold text-indigo-400 mt-1" data-testid="metric-avg-latency">{{ avgLatencyMs() }} ms</p>
          <span class="text-xs text-indigo-300 mt-1 block">Sub-30ms Global Median</span>
        </div>

        <div class="bg-slate-900/80 border border-slate-800 p-5 rounded-2xl">
          <span class="text-xs font-medium text-slate-400">Active Steering Overrides</span>
          <p class="text-2xl font-bold text-amber-400 mt-1" data-testid="metric-overrides">{{ activeOverrides() }}</p>
          <span class="text-xs text-slate-400 mt-1 block">Flap Dampening Backoff: Active</span>
        </div>
      </div>

      <!-- Edge POP Nodes Table -->
      <div class="bg-slate-900/80 border border-slate-800 rounded-2xl p-6">
        <h2 class="text-lg font-bold text-white mb-4">Edge POP Health &amp; Telemetry Matrix</h2>
        <div class="overflow-x-auto">
          <table class="w-full text-left text-sm text-slate-300">
            <thead class="text-xs uppercase bg-slate-800/60 text-slate-400 border-b border-slate-700">
              <tr>
                <th class="py-3 px-4">POP Code</th>
                <th class="py-3 px-4">Location</th>
                <th class="py-3 px-4">BGP ASN</th>
                <th class="py-3 px-4">Anycast IP</th>
                <th class="py-3 px-4 text-right">RTT Latency</th>
                <th class="py-3 px-4 text-right">Packet Loss</th>
                <th class="py-3 px-4 text-right">Health Status</th>
              </tr>
            </thead>
            <tbody class="divide-y divide-slate-800">
              @for (pop of pops(); track pop.popId) {
                <tr class="hover:bg-slate-800/30 transition">
                  <td class="py-3 px-4 font-mono text-xs text-indigo-300 font-semibold">{{ pop.popId }}</td>
                  <td class="py-3 px-4 text-white font-medium">{{ pop.city }}, {{ pop.country }}</td>
                  <td class="py-3 px-4 text-slate-400">AS{{ pop.bgpAsn }}</td>
                  <td class="py-3 px-4 font-mono text-xs text-slate-300">{{ pop.anycastIp }}</td>
                  <td class="py-3 px-4 text-right font-semibold text-white">{{ pop.latencyMs }} ms</td>
                  <td class="py-3 px-4 text-right" [class.text-rose-400]="pop.packetLossPct > 0">{{ pop.packetLossPct }}%</td>
                  <td class="py-3 px-4 text-right">
                    <span [class]="pop.healthStatus === 'Healthy' ? 'px-2 py-0.5 text-xs rounded-full bg-emerald-500/20 text-emerald-400 border border-emerald-500/30 font-medium' : 'px-2 py-0.5 text-xs rounded-full bg-rose-500/20 text-rose-400 border border-rose-500/30 font-medium'">
                      {{ pop.healthStatus }}
                    </span>
                  </td>
                </tr>
              }
            </tbody>
          </table>
        </div>
      </div>

      <!-- Geo-Steering Policies Table -->
      <div class="bg-slate-900/80 border border-slate-800 rounded-2xl p-6">
        <h2 class="text-lg font-bold text-white mb-4">DNS Geo-Steering &amp; Anycast Failover Policies</h2>
        <div class="overflow-x-auto">
          <table class="w-full text-left text-sm text-slate-300">
            <thead class="text-xs uppercase bg-slate-800/60 text-slate-400 border-b border-slate-700">
              <tr>
                <th class="py-3 px-4">Policy ID</th>
                <th class="py-3 px-4">Domain Endpoint</th>
                <th class="py-3 px-4">Record</th>
                <th class="py-3 px-4">Strategy</th>
                <th class="py-3 px-4">Active POP Endpoint</th>
                <th class="py-3 px-4 text-right">Routing State</th>
              </tr>
            </thead>
            <tbody class="divide-y divide-slate-800">
              @for (pol of policies(); track pol.policyId) {
                <tr class="hover:bg-slate-800/30 transition">
                  <td class="py-3 px-4 font-mono text-xs text-slate-400">{{ pol.policyId }}</td>
                  <td class="py-3 px-4 font-semibold text-white">{{ pol.domainName }}</td>
                  <td class="py-3 px-4 text-xs font-mono text-indigo-300">{{ pol.recordType }}</td>
                  <td class="py-3 px-4 text-slate-300">{{ pol.routingStrategy }}</td>
                  <td class="py-3 px-4 font-mono text-xs text-emerald-300">{{ pol.activeEndpointPopId }}</td>
                  <td class="py-3 px-4 text-right">
                    <span [class]="pol.status === 'Active' ? 'px-2 py-0.5 text-xs rounded bg-emerald-500/20 text-emerald-300 font-medium' : 'px-2 py-0.5 text-xs rounded bg-rose-500/20 text-rose-300 font-medium'">
                      {{ pol.status }}
                    </span>
                  </td>
                </tr>
              }
            </tbody>
          </table>
        </div>
      </div>
    </div>
  `
})
export class BgpGeoSteeringCockpitComponent {
  totalPops = signal<number>(4);
  healthyPops = signal<number>(3);
  avgLatencyMs = signal<number>(34);
  activeOverrides = signal<number>(0);

  pops = signal<EdgePopItem[]>([
    { popId: 'POP-IAD-01', city: 'Ashburn', country: 'United States', region: 'NorthAmerica', bgpAsn: 13335, anycastIp: '198.51.100.1', latencyMs: 14, jitterMs: 1.2, packetLossPct: 0.0, healthStatus: 'Healthy' },
    { popId: 'POP-FRA-01', city: 'Frankfurt', country: 'Germany', region: 'Europe', bgpAsn: 13335, anycastIp: '198.51.100.2', latencyMs: 18, jitterMs: 1.5, packetLossPct: 0.0, healthStatus: 'Healthy' },
    { popId: 'POP-NRT-01', city: 'Tokyo', country: 'Japan', region: 'AsiaPacific', bgpAsn: 13335, anycastIp: '198.51.100.3', latencyMs: 26, jitterMs: 2.1, packetLossPct: 0.0, healthStatus: 'Healthy' },
    { popId: 'POP-GRU-01', city: 'Sao Paulo', country: 'Brazil', region: 'LatinAmerica', bgpAsn: 13335, anycastIp: '198.51.100.4', latencyMs: 78, jitterMs: 8.4, packetLossPct: 0.8, healthStatus: 'Degraded' }
  ]);

  policies = signal<GeoSteeringPolicyItem[]>([
    { policyId: 'POL-DNS-01', domainName: 'api.sgrowing.io', recordType: 'A', routingStrategy: 'LatencyOptimized', activeEndpointPopId: 'POP-IAD-01', status: 'Active' },
    { policyId: 'POL-DNS-02', domainName: 'voice.sgrowing.io', recordType: 'A', routingStrategy: 'GeoProximity', activeEndpointPopId: 'POP-FRA-01', status: 'Active' },
    { policyId: 'POL-DNS-03', domainName: 'edge.sgrowing.io', recordType: 'ALIAS', routingStrategy: 'FailoverSurge', activeEndpointPopId: 'POP-NRT-01', status: 'Active' }
  ]);

  triggerGlobalProbing(): void {
    this.avgLatencyMs.set(29);
  }

  overrideSteeringPolicy(): void {
    this.policies.update(list =>
      list.map(p => p.policyId === 'POL-DNS-01' ? { ...p, activeEndpointPopId: 'POP-FRA-01', status: 'Overridden' } : p)
    );
    this.activeOverrides.set(1);
  }
}
