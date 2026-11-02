import { Component, ChangeDetectionStrategy, signal, computed } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';

export interface TopicClusterView {
  clusterId: string;
  pillarTopic: string;
  targetKeyword: string;
  authorityScore: number;
  subArticlesCount: number;
  missingTopicsCount: number;
  coveredEntities: string[];
  expansions: string[];
}

export interface InternalLinkOppView {
  opportunityId: string;
  sourceUrl: string;
  sourceTitle: string;
  targetUrl: string;
  anchorText: string;
  contextSnippet: string;
  relevanceScore: number;
  isInjected: boolean;
}

@Component({
  selector: 'app-topical-authority-cockpit',
  standalone: true,
  imports: [CommonModule, FormsModule],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="p-6 max-w-7xl mx-auto space-y-6" dir="ltr" data-testid="topical-authority-cockpit">
      <!-- Header -->
      <div class="flex flex-col md:flex-row md:items-center justify-between gap-4 border-b border-slate-700/60 pb-6">
        <div>
          <div class="flex items-center gap-2 text-xs font-semibold uppercase tracking-wider text-emerald-400">
            <span class="inline-block w-2 h-2 rounded-full bg-emerald-400 animate-pulse"></span>
            Semantic Knowledge Graph & SEO Mesh • Horizon 59
          </div>
          <h1 class="text-3xl font-extrabold text-white tracking-tight mt-1">
            Autonomous SEO Dynamic Topical Authority Mesh
          </h1>
          <p class="text-slate-400 text-sm mt-1">
            Pillar-cluster entity coverage evaluation, semantic topical gap detection & PageRank internal anchor optimization.
          </p>
        </div>

        <div class="flex items-center gap-3">
          <button
            (click)="auditTopicalClusters()"
            data-testid="analyze-cluster-btn"
            class="px-4 py-2 bg-emerald-600 hover:bg-emerald-500 text-white rounded-lg text-sm font-semibold transition-colors flex items-center gap-2 shadow-lg shadow-emerald-600/20"
          >
            <span>🕸️</span> Audit Topical Authority Mesh
          </button>
        </div>
      </div>

      <!-- KPI Overview Stats -->
      <div class="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-4 gap-4">
        <div class="bg-slate-800/80 border border-slate-700/60 rounded-xl p-4">
          <div class="text-slate-400 text-xs font-medium uppercase">Overall Topical Authority</div>
          <div class="text-2xl font-bold text-white mt-1" data-testid="stat-overall-authority">
            {{ overallAuthority() }}%
          </div>
          <div class="text-emerald-400 text-xs mt-1">Tier-1 Entity Dominance</div>
        </div>
        <div class="bg-slate-800/80 border border-slate-700/60 rounded-xl p-4">
          <div class="text-slate-400 text-xs font-medium uppercase">Audited Clusters</div>
          <div class="text-2xl font-bold text-emerald-400 mt-1" data-testid="stat-total-clusters">
            {{ clusters().length }}
          </div>
          <div class="text-slate-400 text-xs mt-1">Pillar & Sub-Topic Hierarchies</div>
        </div>
        <div class="bg-slate-800/80 border border-slate-700/60 rounded-xl p-4">
          <div class="text-slate-400 text-xs font-medium uppercase">Link Opportunities</div>
          <div class="text-2xl font-bold text-sky-400 mt-1" data-testid="stat-link-opps">
            {{ linkOpportunities().length }}
          </div>
          <div class="text-emerald-400 text-xs mt-1">Cosine Semantic Relevance >= 0.90</div>
        </div>
        <div class="bg-slate-800/80 border border-slate-700/60 rounded-xl p-4">
          <div class="text-slate-400 text-xs font-medium uppercase">Identified Content Gaps</div>
          <div class="text-2xl font-bold text-amber-400 mt-1">
            {{ totalMissingTopics() }}
          </div>
          <div class="text-slate-400 text-xs mt-1">Recommended Cluster Expansions</div>
        </div>
      </div>

      <!-- Main Stage: Topic Clusters & Internal Link Injections -->
      <div class="grid grid-cols-1 lg:grid-cols-12 gap-6">
        <!-- Clusters (7 cols) -->
        <div class="lg:col-span-7 bg-slate-800/80 border border-slate-700/60 rounded-xl overflow-hidden">
          <div class="p-4 border-b border-slate-700/60 flex items-center justify-between">
            <h2 class="text-lg font-bold text-white">Semantic Topic Clusters</h2>
            <span class="text-xs text-slate-400 font-mono">{{ clusters().length }} Clusters</span>
          </div>

          <div class="divide-y divide-slate-700/40">
            @for (cluster of clusters(); track cluster.clusterId) {
              <div
                (click)="selectCluster(cluster)"
                [class.bg-slate-700/40]="selectedCluster()?.clusterId === cluster.clusterId"
                class="p-4 hover:bg-slate-700/30 transition-colors cursor-pointer flex flex-col md:flex-row md:items-center justify-between gap-3"
                [attr.data-testid]="'cluster-row-' + cluster.clusterId"
              >
                <div>
                  <div class="flex items-center gap-2">
                    <span class="font-bold text-white text-sm">{{ cluster.pillarTopic }}</span>
                    <span class="bg-emerald-900/60 text-emerald-300 border border-emerald-700/60 px-2 py-0.5 rounded text-[10px] font-semibold">
                      {{ cluster.subArticlesCount }} Articles
                    </span>
                  </div>
                  <div class="text-xs text-slate-400 mt-1">
                    Primary Keyword: <span class="font-mono text-slate-300">{{ cluster.targetKeyword }}</span>
                  </div>
                  <div class="text-xs text-slate-400 mt-0.5">
                    Covered Entities: <span class="text-slate-300">{{ cluster.coveredEntities.join(' • ') }}</span>
                  </div>
                </div>

                <div class="text-right self-end md:self-auto">
                  <div class="text-xs text-slate-400">Authority Score</div>
                  <div class="text-lg font-extrabold font-mono text-emerald-400">
                    {{ cluster.authorityScore }}%
                  </div>
                </div>
              </div>
            }
          </div>
        </div>

        <!-- Internal Link Opportunity Injections (5 cols) -->
        <div class="lg:col-span-5 bg-slate-800/80 border border-slate-700/60 rounded-xl p-5 space-y-4">
          <div class="flex items-center justify-between border-b border-slate-700/60 pb-3">
            <div>
              <h3 class="font-bold text-white text-base">Internal PageRank Link Mesh</h3>
              <p class="text-xs text-slate-400">Contextual Link Injections</p>
            </div>
            <span class="text-xs px-2.5 py-1 rounded bg-emerald-950 text-emerald-300 border border-emerald-800">
              High Relevance
            </span>
          </div>

          <div class="space-y-3">
            @for (opp of linkOpportunities(); track opp.opportunityId) {
              <div class="p-3 bg-slate-900 rounded-xl border border-slate-800 space-y-2 text-xs" [attr.data-testid]="'opp-card-' + opp.opportunityId">
                <div class="flex items-center justify-between">
                  <span class="font-bold text-white">{{ opp.sourceTitle }}</span>
                  <span
                    [class]="opp.isInjected ? 'bg-emerald-900/60 text-emerald-300 border-emerald-700/60' : 'bg-slate-800 text-slate-400 border-slate-700'"
                    class="px-2 py-0.5 rounded text-[10px] font-semibold border"
                    data-testid="injection-status-badge"
                  >
                    {{ opp.isInjected ? 'Injected' : 'Pending' }}
                  </span>
                </div>

                <div class="p-2 bg-slate-950 rounded text-slate-300 text-[11px] leading-relaxed border border-slate-800/80">
                  {{ opp.contextSnippet }}
                </div>

                <div class="flex items-center justify-between pt-1 text-[11px]">
                  <span class="text-slate-400 font-mono">Anchor: <strong class="text-emerald-400">"{{ opp.anchorText }}"</strong></span>

                  @if (!opp.isInjected) {
                    <button
                      (click)="injectInternalLink(opp)"
                      data-testid="inject-link-btn"
                      class="px-3 py-1 bg-emerald-600 hover:bg-emerald-500 text-white rounded text-xs font-semibold shadow transition-colors"
                    >
                      Inject Anchor
                    </button>
                  } @else {
                    <span class="text-emerald-400 font-semibold text-xs">✓ Injected</span>
                  }
                </div>
              </div>
            }
          </div>
        </div>
      </div>
    </div>
  `
})
export class TopicalAuthorityCockpitComponent {
  readonly clusters = signal<TopicClusterView[]>([
    {
      clusterId: 'cluster_growth_marketing',
      pillarTopic: 'Autonomous B2B Growth Engine',
      targetKeyword: 'b2b growth automation',
      authorityScore: 88.5,
      subArticlesCount: 14,
      missingTopicsCount: 2,
      coveredEntities: ['CDP Identity Resolution', 'Dynamic Funnels', 'Multi-Touch Attribution', 'Reverse IP De-Anonymization'],
      expansions: ['Account-Based Marketing orchestration metrics', 'Zero-party data capture playbooks']
    },
    {
      clusterId: 'cluster_voice_ai',
      pillarTopic: 'Enterprise Conversational Voice AI',
      targetKeyword: 'voice ai sdr',
      authorityScore: 74.0,
      subArticlesCount: 9,
      missingTopicsCount: 4,
      coveredEntities: ['WebRTC Audio Streaming', 'Objection Classification', 'Turn-taking Latency Optimization'],
      expansions: ['Enterprise telephony SIP trunking benchmarks', 'SOC2 HIPAA compliant voice recording']
    }
  ]);

  readonly linkOpportunities = signal<InternalLinkOppView[]>([
    {
      opportunityId: 'opp_link_001',
      sourceUrl: '/blog/how-to-scale-b2b-outbound',
      sourceTitle: 'How to Scale B2B Outbound Without Burning Reps',
      targetUrl: '/solutions/b2b-growth-automation',
      anchorText: 'autonomous B2B growth automation',
      contextSnippet: 'Companies implementing ...autonomous B2B growth automation... typically see a 4x surge in qualified pipeline meetings.',
      relevanceScore: 0.96,
      isInjected: false
    },
    {
      opportunityId: 'opp_link_002',
      sourceUrl: '/blog/what-is-reverse-ip-lookup',
      sourceTitle: 'The Comprehensive Guide to Reverse IP De-Anonymization',
      targetUrl: '/platform/cdp-identity-graph',
      anchorText: 'enterprise CDP identity resolution graph',
      contextSnippet: 'When paired with an ...enterprise CDP identity resolution graph..., anonymous web sessions resolve into high-intent accounts.',
      relevanceScore: 0.93,
      isInjected: false
    }
  ]);

  readonly selectedCluster = signal<TopicClusterView | null>(this.clusters()[0]);

  readonly overallAuthority = computed(() => {
    const list = this.clusters();
    if (!list.length) return 0;
    return Math.round(list.reduce((acc, c) => acc + c.authorityScore, 0) / list.length);
  });

  readonly totalMissingTopics = computed(() =>
    this.clusters().reduce((acc, c) => acc + c.missingTopicsCount, 0)
  );

  selectCluster(cluster: TopicClusterView): void {
    this.selectedCluster.set(cluster);
  }

  auditTopicalClusters(): void {
    const newCluster: TopicClusterView = {
      clusterId: `cluster_cleanroom_${Date.now()}`,
      pillarTopic: 'Privacy-Preserving Data Clean Rooms',
      targetKeyword: 'zero knowledge ad cleanroom',
      authorityScore: 92.0,
      subArticlesCount: 6,
      missingTopicsCount: 1,
      coveredEntities: ['Private Set Intersection', 'Differential Privacy Laplace Noise', 'Multi-Party Computation'],
      expansions: ['GDPR Article 28 data processor verification']
    };

    this.clusters.update(list => [...list, newCluster]);
  }

  injectInternalLink(opp: InternalLinkOppView): void {
    this.linkOpportunities.update(list =>
      list.map(item =>
        item.opportunityId === opp.opportunityId ? { ...item, isInjected: true } : item
      )
    );
  }
}
