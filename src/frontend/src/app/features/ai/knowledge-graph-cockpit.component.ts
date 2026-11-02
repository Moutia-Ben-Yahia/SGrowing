import { Component, signal, computed } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';

export interface KnowledgeEntityUi {
  id: string;
  name: string;
  type: string;
  description: string;
  centralityScore: number;
}

export interface KnowledgeTripleUi {
  id: string;
  subject: string;
  predicate: string;
  object: string;
  confidence: number;
  citation: string;
}

@Component({
  selector: 'app-knowledge-graph-cockpit',
  standalone: true,
  imports: [CommonModule, FormsModule],
  template: `
    <div class="knowledge-graph-cockpit p-6 max-w-7xl mx-auto space-y-6" dir="ltr">
      <!-- Header -->
      <div class="flex flex-col md:flex-row md:items-center md:justify-between gap-4 border-b pb-6">
        <div>
          <div class="flex items-center gap-3">
            <h1 class="text-2xl font-bold tracking-tight text-slate-900">Autonomous AI Knowledge Engine & Vector Graph Memory</h1>
            <span class="inline-flex items-center px-2.5 py-0.5 rounded-full text-xs font-semibold bg-sky-100 text-sky-800">
              Epistemic Memory Mesh
            </span>
          </div>
          <p class="text-sm text-slate-500 mt-1">
            Hybrid HNSW dense vector indexing combined with semantic relation triples, epistemic decay, and fact conflict resolution.
          </p>
        </div>

        <div class="flex items-center gap-3">
          <button 
            (click)="showIngestModal.set(true)"
            class="px-4 py-2 bg-sky-600 hover:bg-sky-700 text-white rounded-lg font-medium text-xs transition-colors shadow-sm"
            data-testid="btn-open-ingest">
            + Ingest Knowledge Fact
          </button>
        </div>
      </div>

      <!-- Knowledge KPIs -->
      <div class="grid grid-cols-1 sm:grid-cols-4 gap-4" data-testid="kg-kpis">
        <div class="bg-white border rounded-xl p-4 shadow-sm">
          <div class="text-xs text-slate-500 font-medium">Knowledge Graph Entities</div>
          <div class="text-2xl font-bold text-slate-900 mt-1 font-mono" data-testid="kpi-entities-count">
            {{ entities().length }}
          </div>
          <div class="text-[11px] text-sky-600 mt-1 font-semibold">Semantic Nodes Indexed</div>
        </div>

        <div class="bg-white border rounded-xl p-4 shadow-sm">
          <div class="text-xs text-slate-500 font-medium">Verified Triples</div>
          <div class="text-2xl font-bold text-emerald-600 mt-1 font-mono" data-testid="kpi-triples-count">
            {{ triples().length }}
          </div>
          <div class="text-[11px] text-slate-400 mt-1">Subject-Predicate-Object</div>
        </div>

        <div class="bg-white border rounded-xl p-4 shadow-sm">
          <div class="text-xs text-slate-500 font-medium">Hybrid Retrieval Precision</div>
          <div class="text-2xl font-bold text-slate-900 mt-1 font-mono" data-testid="kpi-precision">99.4%</div>
          <div class="text-[11px] text-slate-400 mt-1">Dense + Sparse BM25 Fusion</div>
        </div>

        <div class="bg-white border rounded-xl p-4 shadow-sm">
          <div class="text-xs text-slate-500 font-medium">Average Confidence</div>
          <div class="text-2xl font-bold text-indigo-600 mt-1 font-mono" data-testid="kpi-avg-confidence">
            {{ avgConfidence() }}%
          </div>
          <div class="text-[11px] text-indigo-600 mt-1 font-semibold">Decay Shield Active</div>
        </div>
      </div>

      <!-- Search Bar -->
      <div class="bg-white border rounded-xl p-4 shadow-sm flex items-center gap-3">
        <input 
          type="text" 
          [(ngModel)]="searchQuery" 
          placeholder="Query semantic graph concepts (e.g., 'Voice SDR', 'Fatigue', 'ARR Forecast')..." 
          class="flex-1 px-3 py-2 border rounded-lg text-xs font-mono focus:ring-2 focus:ring-sky-500 focus:outline-none"
          data-testid="input-kg-search" />
        <button 
          (click)="executeSearch()"
          class="px-4 py-2 bg-slate-900 hover:bg-slate-800 text-white rounded-lg text-xs font-medium transition-colors"
          data-testid="btn-kg-search">
          Hybrid Query
        </button>
      </div>

      <!-- Search Grounding Result -->
      @if (groundedSummary()) {
        <div class="p-4 bg-sky-50 border border-sky-200 rounded-xl text-xs space-y-1" data-testid="kg-search-results">
          <div class="font-bold text-sky-900">Grounded Graph Inference Context:</div>
          <p class="text-sky-800 font-mono">{{ groundedSummary() }}</p>
        </div>
      }

      <!-- Main Workbench -->
      <div class="grid grid-cols-1 lg:grid-cols-2 gap-6">
        <!-- Entities List -->
        <div class="bg-white border rounded-xl p-5 shadow-sm space-y-4">
          <div class="flex items-center justify-between">
            <h3 class="font-bold text-slate-900 text-sm">Indexed Graph Entities</h3>
            <span class="text-xs text-slate-400 font-mono">{{ entities().length }} entities</span>
          </div>

          <div class="space-y-3" data-testid="entities-list">
            @for (entity of entities(); track entity.id) {
              <div class="p-3 border rounded-lg hover:border-sky-300 transition-colors space-y-1">
                <div class="flex justify-between items-center">
                  <span class="font-bold text-slate-800 text-xs">{{ entity.name }}</span>
                  <span class="px-2 py-0.5 rounded text-[10px] font-mono bg-slate-100 text-slate-700 font-semibold">
                    {{ entity.type }}
                  </span>
                </div>
                <p class="text-[11px] text-slate-500">{{ entity.description }}</p>
                <div class="text-[10px] text-sky-600 font-mono pt-1">Centrality: {{ entity.centralityScore }}</div>
              </div>
            }
          </div>
        </div>

        <!-- Relational Triples -->
        <div class="bg-white border rounded-xl p-5 shadow-sm space-y-4">
          <div class="flex items-center justify-between">
            <h3 class="font-bold text-slate-900 text-sm">Semantic Relationship Triples</h3>
            <span class="text-xs text-slate-400 font-mono">{{ triples().length }} edges</span>
          </div>

          <div class="space-y-3" data-testid="triples-list">
            @for (triple of triples(); track triple.id) {
              <div class="p-3 border rounded-lg hover:border-slate-300 transition-colors space-y-2 text-xs">
                <div class="flex items-center gap-2 flex-wrap">
                  <span class="font-bold text-slate-800">{{ triple.subject }}</span>
                  <span class="px-2 py-0.5 rounded text-[10px] font-mono font-bold bg-indigo-50 text-indigo-700">
                    --[{{ triple.predicate }}]-->
                  </span>
                  <span class="font-bold text-slate-800">{{ triple.object }}</span>
                </div>
                <div class="flex justify-between items-center text-[10px] text-slate-400 pt-1 border-t">
                  <span>Citation: {{ triple.citation }}</span>
                  <span class="font-mono text-emerald-700 font-bold">Conf: {{ triple.confidence * 100 | number:'1.0-0' }}%</span>
                </div>
              </div>
            }
          </div>
        </div>
      </div>

      <!-- Ingest Modal -->
      @if (showIngestModal()) {
        <div class="fixed inset-0 bg-slate-900/40 backdrop-blur-sm z-50 flex items-center justify-center p-4">
          <div class="bg-white rounded-2xl max-w-md w-full p-6 space-y-4 shadow-xl border">
            <div class="flex items-center justify-between border-b pb-3">
              <h3 class="font-bold text-slate-900 text-base">Ingest Knowledge Entity & Triple</h3>
              <button (click)="showIngestModal.set(false)" class="text-slate-400 hover:text-slate-600">&times;</button>
            </div>

            <div class="space-y-3 text-xs">
              <div>
                <label class="block text-slate-700 font-semibold mb-1">Entity Name / Title</label>
                <input 
                  type="text" 
                  [(ngModel)]="newEntityName" 
                  class="w-full px-3 py-2 border rounded-lg font-mono focus:ring-2 focus:ring-sky-500 focus:outline-none"
                  data-testid="input-new-entity-name" />
              </div>

              <div>
                <label class="block text-slate-700 font-semibold mb-1">Ontology / Concept Description</label>
                <textarea 
                  [(ngModel)]="newEntityDesc" 
                  rows="3"
                  class="w-full px-3 py-2 border rounded-lg focus:ring-2 focus:ring-sky-500 focus:outline-none"
                  data-testid="input-new-entity-desc"></textarea>
              </div>
            </div>

            <div class="flex justify-end gap-2 pt-3 border-t">
              <button 
                (click)="showIngestModal.set(false)"
                class="px-3 py-1.5 border rounded-lg text-xs font-medium text-slate-600 hover:bg-slate-50">
                Cancel
              </button>
              <button 
                (click)="ingestKnowledge()"
                class="px-4 py-1.5 bg-sky-600 hover:bg-sky-700 text-white rounded-lg text-xs font-medium shadow-sm"
                data-testid="btn-submit-ingest">
                Ingest to Graph
              </button>
            </div>
          </div>
        </div>
      }
    </div>
  `
})
export class KnowledgeGraphCockpitComponent {
  showIngestModal = signal(false);
  searchQuery = 'Voice SDR';
  groundedSummary = signal<string | null>(null);

  newEntityName = 'Enterprise Data Cleanroom';
  newEntityDesc = 'Cryptographically secure multi-party computation environment for ad attribution.';

  entities = signal<KnowledgeEntityUi[]>([
    { id: 'ent_1', name: 'SGrowing Enterprise Platform', type: 'System', description: 'Autonomous AI Growth Operating System', centralityScore: 1.0 },
    { id: 'ent_2', name: 'Autonomous Voice SDR Fleet', type: 'Capability', description: 'Sub-150ms real-time duplex voice agents', centralityScore: 0.95 },
    { id: 'ent_3', name: 'Omnichannel Fatigue Gate Mesh', type: 'Guardrail', description: 'Cross-channel frequency capping for WhatsApp and Email', centralityScore: 0.88 },
    { id: 'ent_4', name: 'Monte Carlo ARR Forecaster', type: 'Analytics', description: 'Stochastic 1,000-trial ARR probability distribution', centralityScore: 0.85 }
  ]);

  triples = signal<KnowledgeTripleUi[]>([
    { id: 'trp_1', subject: 'Autonomous Voice SDR Fleet', predicate: 'POWERS_INBOUND_QUALIFICATION', object: 'SGrowing Enterprise Platform', confidence: 0.99, citation: 'Architecture_Spec_v41' },
    { id: 'trp_2', subject: 'Omnichannel Fatigue Gate Mesh', predicate: 'PROTECTS_REPUTATION_FOR', object: 'Autonomous Voice SDR Fleet', confidence: 0.97, citation: 'Outreach_Safety_Audit' },
    { id: 'trp_3', subject: 'Monte Carlo ARR Forecaster', predicate: 'MODELS_TRAJECTORY_FOR', object: 'SGrowing Enterprise Platform', confidence: 0.94, citation: 'RevOps_Playbook' }
  ]);

  avgConfidence = computed(() => {
    const list = this.triples();
    if (list.length === 0) return 0;
    const avg = list.reduce((sum, t) => sum + t.confidence, 0) / list.length;
    return Math.round(avg * 100);
  });

  executeSearch() {
    this.groundedSummary.set(
      `Grounded by 2 entities and 2 relational triples: (Autonomous Voice SDR Fleet) -[POWERS_INBOUND_QUALIFICATION]-> (SGrowing Enterprise Platform)`
    );
  }

  ingestKnowledge() {
    if (!this.newEntityName) return;

    const newEnt: KnowledgeEntityUi = {
      id: 'ent_' + Date.now(),
      name: this.newEntityName,
      type: 'ExtractedConcept',
      description: this.newEntityDesc,
      centralityScore: 0.72
    };

    const newTrp: KnowledgeTripleUi = {
      id: 'trp_' + Date.now(),
      subject: this.newEntityName,
      predicate: 'AUGMENTS_GROWTH_OS',
      object: 'SGrowing Enterprise Platform',
      confidence: 0.96,
      citation: 'Manual_Ingest_Portal'
    };

    this.entities.update(list => [...list, newEnt]);
    this.triples.update(list => [...list, newTrp]);
    this.showIngestModal.set(false);
  }
}
