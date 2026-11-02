import { Component, inject, signal, computed, ChangeDetectorRef } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { TranslationService } from '../../core/services/translation.service';

export interface FieldItem {
  id: string;
  name: string;
  label: string;
  type: string;
  required: boolean;
}

export interface SchemaItem {
  id: string;
  name: string;
  plural: string;
  icon: string;
  fields: FieldItem[];
  recordCount: number;
}

export interface DuplicateCandidateItem {
  idA: string;
  idB: string;
  field: string;
  valueA: string;
  valueB: string;
  score: number;
  reason: string;
}

@Component({
  selector: 'app-custom-objects-studio',
  standalone: true,
  imports: [CommonModule, FormsModule],
  template: `
    <div dir="ltr" class="p-6 max-w-7xl mx-auto space-y-6 text-slate-100 font-sans">
      <!-- Header -->
      <div class="flex flex-col md:flex-row md:items-center justify-between gap-4 border-b border-slate-800 pb-5">
        <div>
          <div class="flex items-center gap-2">
            <span class="px-2.5 py-0.5 rounded-full text-xs font-semibold bg-indigo-500/20 text-indigo-400 border border-indigo-500/30">
              Horizon 17 • Domain B1 & B4
            </span>
            <span class="text-xs text-slate-400">Custom Metadata & Fuzzy Merge Engine</span>
          </div>
          <h1 class="text-2xl font-bold tracking-tight text-white mt-1">
            CRM Custom Objects Studio & Metadata Schema Engine
          </h1>
          <p class="text-sm text-slate-400">
            Define dynamic business entities, typed relation attributes, and automated Levenshtein duplicate record detection.
          </p>
        </div>

        <div class="flex items-center gap-3">
          <button (click)="createDemoSchema()"
                  class="px-3.5 py-2 rounded-xl bg-indigo-600 hover:bg-indigo-500 text-white text-xs font-semibold shadow-lg shadow-indigo-600/30 transition">
            + New Custom Entity Schema
          </button>
        </div>
      </div>

      <!-- Top Metrics -->
      <div class="grid grid-cols-1 md:grid-cols-4 gap-4">
        <div class="bg-slate-900/60 border border-slate-800 p-4 rounded-2xl">
          <div class="text-xs text-slate-400 font-medium">Custom Schemas</div>
          <div class="text-2xl font-bold text-white mt-1">{{ schemas().length }}</div>
          <div class="text-xs text-slate-500 mt-0.5">Active Entity Types</div>
        </div>

        <div class="bg-slate-900/60 border border-slate-800 p-4 rounded-2xl">
          <div class="text-xs text-slate-400 font-medium">Total Custom Records</div>
          <div class="text-2xl font-bold text-indigo-400 mt-1">{{ totalRecords() }}</div>
          <div class="text-xs text-slate-500 mt-0.5">Indexed in Relational Graph</div>
        </div>

        <div class="bg-slate-900/60 border border-slate-800 p-4 rounded-2xl">
          <div class="text-xs text-slate-400 font-medium">Active Schema Fields</div>
          <div class="text-2xl font-bold text-emerald-400 mt-1">{{ selectedSchema()?.fields?.length || 0 }}</div>
          <div class="text-xs text-slate-500 mt-0.5">In {{ selectedSchema()?.name }}</div>
        </div>

        <div class="bg-slate-900/60 border border-slate-800 p-4 rounded-2xl">
          <div class="text-xs text-slate-400 font-medium">Duplicate Candidates</div>
          <div class="text-2xl font-bold text-amber-400 mt-1">{{ duplicates().length }}</div>
          <div class="text-xs text-slate-500 mt-0.5">Levenshtein Similarity ≥ 0.75</div>
        </div>
      </div>

      <div class="grid grid-cols-1 lg:grid-cols-3 gap-6">
        <!-- Schemas Navigation & Fields (2 Cols) -->
        <div class="lg:col-span-2 space-y-4">
          <div class="flex items-center justify-between">
            <h2 class="text-sm font-semibold text-slate-300 uppercase tracking-wider">Entity Type Schemas</h2>
            <span class="text-xs text-slate-500">Select to inspect attributes</span>
          </div>

          <div class="grid grid-cols-1 md:grid-cols-2 gap-3">
            @for (s of schemas(); track s.id) {
              <div (click)="selectSchema(s.id)"
                   class="p-4 rounded-2xl border cursor-pointer transition"
                   [ngClass]="s.id === selectedSchemaId() ? 'bg-indigo-950/40 border-indigo-500/60 shadow-lg shadow-indigo-950/50' : 'bg-slate-900/80 border-slate-800 hover:border-slate-700'">
                <div class="flex items-center justify-between">
                  <div class="flex items-center gap-2.5">
                    <span class="text-lg">{{ s.icon }}</span>
                    <span class="font-bold text-sm text-white">{{ s.name }}</span>
                  </div>
                  <span class="text-xs px-2 py-0.5 rounded-full bg-slate-800 text-slate-300">
                    {{ s.recordCount }} records
                  </span>
                </div>
                <div class="text-xs text-slate-400 mt-2">
                  {{ s.fields.length }} typed attributes • Plural: {{ s.plural }}
                </div>
              </div>
            }
          </div>

          <!-- Fields Table for Selected Schema -->
          @if (selectedSchema()) {
            <div class="bg-slate-900/80 border border-slate-800 rounded-2xl p-4 space-y-3">
              <div class="flex items-center justify-between border-b border-slate-800 pb-3">
                <h3 class="text-sm font-bold text-white flex items-center gap-2">
                  <span>{{ selectedSchema()!.icon }}</span>
                  <span>{{ selectedSchema()!.name }} Attributes Schema</span>
                </h3>
                <button (click)="addFieldToSelected()"
                        class="text-xs px-2.5 py-1 rounded-lg bg-indigo-600/30 text-indigo-300 hover:bg-indigo-600/40 border border-indigo-500/30">
                  + Add Field
                </button>
              </div>

              <div class="overflow-x-auto">
                <table class="w-full text-xs text-left">
                  <thead class="text-slate-500 border-b border-slate-800">
                    <tr>
                      <th class="pb-2">Field Label</th>
                      <th class="pb-2">Attribute Key</th>
                      <th class="pb-2">Data Type</th>
                      <th class="pb-2">Required</th>
                    </tr>
                  </thead>
                  <tbody class="divide-y divide-slate-800/60">
                    @for (f of selectedSchema()!.fields; track f.id) {
                      <tr>
                        <td class="py-2.5 font-medium text-white">{{ f.label }}</td>
                        <td class="py-2.5 font-mono text-slate-400">{{ f.name }}</td>
                        <td class="py-2.5">
                          <span class="px-2 py-0.5 rounded bg-slate-800 text-slate-300">{{ f.type }}</span>
                        </td>
                        <td class="py-2.5">
                          <span class="px-2 py-0.5 rounded text-[10px] font-bold"
                                [ngClass]="f.required ? 'bg-amber-500/20 text-amber-300' : 'bg-slate-800 text-slate-500'">
                            {{ f.required ? 'REQUIRED' : 'OPTIONAL' }}
                          </span>
                        </td>
                      </tr>
                    }
                  </tbody>
                </table>
              </div>
            </div>
          }
        </div>

        <!-- Duplicates & Merge Queue (1 Col) -->
        <div class="space-y-4">
          <div class="flex items-center justify-between">
            <h2 class="text-sm font-semibold text-slate-300 uppercase tracking-wider">Duplicate Record Queue</h2>
            <span class="text-xs text-amber-400 font-semibold">{{ duplicates().length }} Pending</span>
          </div>

          <div class="space-y-3">
            @for (dup of duplicates(); track dup.idA + dup.idB) {
              <div class="bg-slate-900/80 border border-slate-800 rounded-2xl p-4 space-y-3">
                <div class="flex items-center justify-between text-xs">
                  <span class="font-semibold text-white">Matching on: {{ dup.field }}</span>
                  <span class="px-2 py-0.5 rounded-full text-[10px] font-bold bg-amber-500/20 text-amber-300 border border-amber-500/30">
                    {{ (dup.score * 100).toFixed(0) }}% Similarity
                  </span>
                </div>

                <div class="p-2.5 bg-slate-950/80 rounded-xl space-y-1.5 text-xs font-mono">
                  <div class="text-slate-300 truncate"><span class="text-slate-500">Record A:</span> {{ dup.valueA }}</div>
                  <div class="text-slate-300 truncate"><span class="text-slate-500">Record B:</span> {{ dup.valueB }}</div>
                </div>

                <button (click)="mergeCandidate(dup.idA)"
                        class="w-full py-2 bg-indigo-600 hover:bg-indigo-500 text-white text-xs font-semibold rounded-xl shadow-md shadow-indigo-600/30 transition">
                  Merge into Master Record 🔗
                </button>
              </div>
            }

            @if (duplicates().length === 0) {
              <div class="p-6 bg-slate-900/40 border border-slate-800 rounded-2xl text-center text-xs text-slate-500">
                No duplicate records detected. All custom records are unique.
              </div>
            }
          </div>
        </div>
      </div>
    </div>
  `
})
export class CustomObjectsStudioComponent {
  translation: TranslationService;
  private cdr?: ChangeDetectorRef;

  selectedSchemaId = signal<string>('sch_property');

  schemas = signal<SchemaItem[]>([
    {
      id: 'sch_property',
      name: 'Commercial Property',
      plural: 'Commercial Properties',
      icon: '🏢',
      recordCount: 148,
      fields: [
        { id: 'f1', name: 'propertyName', label: 'Property Name', type: 'Text', required: true },
        { id: 'f2', name: 'squareFeet', label: 'Square Feet', type: 'Number', required: true },
        { id: 'f3', name: 'monthlyLease', label: 'Monthly Lease', type: 'Currency', required: false },
        { id: 'f4', name: 'assignedBroker', label: 'Assigned Broker', type: 'Relation', required: false }
      ]
    },
    {
      id: 'sch_vehicle',
      name: 'Fleet Vehicle',
      plural: 'Fleet Vehicles',
      icon: '🚛',
      recordCount: 64,
      fields: [
        { id: 'f1', name: 'vin', label: 'VIN Number', type: 'Text', required: true },
        { id: 'f2', name: 'odometer', label: 'Odometer Miles', type: 'Number', required: true },
        { id: 'f3', name: 'lastInspection', label: 'Last Inspection', type: 'DateTime', required: false }
      ]
    }
  ]);

  duplicates = signal<DuplicateCandidateItem[]>([
    {
      idA: 'rec_01',
      idB: 'rec_02',
      field: 'propertyName',
      valueA: 'Skyline Tower Suite 400',
      valueB: 'Skyline Tower Ste 400',
      score: 0.91,
      reason: 'Fuzzy address match'
    }
  ]);

  selectedSchema = computed(() => {
    return this.schemas().find(s => s.id === this.selectedSchemaId());
  });

  totalRecords = computed(() => {
    return this.schemas().reduce((sum, s) => sum + s.recordCount, 0);
  });

  constructor(translationService?: TranslationService) {
    if (translationService) {
      this.translation = translationService;
    } else {
      try {
        this.translation = inject(TranslationService);
      } catch {
        this.translation = new TranslationService();
      }
    }
    try {
      this.cdr = inject(ChangeDetectorRef);
    } catch {
      // Direct unit test instantiation
    }
  }

  selectSchema(id: string): void {
    this.selectedSchemaId.set(id);
    this.cdr?.markForCheck();
  }

  createDemoSchema(): void {
    const newSchema: SchemaItem = {
      id: 'sch_' + Math.random().toString(36).substring(2, 7),
      name: 'Agency Partner',
      plural: 'Agency Partners',
      icon: '🤝',
      recordCount: 22,
      fields: [
        { id: 'f1', name: 'agencyName', label: 'Agency Name', type: 'Text', required: true },
        { id: 'f2', name: 'commissionTier', label: 'Commission Tier', type: 'Select', required: true }
      ]
    };

    this.schemas.update(list => [...list, newSchema]);
    this.selectedSchemaId.set(newSchema.id);
    this.cdr?.markForCheck();
  }

  addFieldToSelected(): void {
    if (!this.selectedSchema()) return;
    const newField: FieldItem = {
      id: 'f_' + Math.random().toString(36).substring(2, 6),
      name: 'customField_' + (this.selectedSchema()!.fields.length + 1),
      label: 'Custom Attribute',
      type: 'Text',
      required: false
    };

    const updated = this.schemas().map(s => {
      if (s.id === this.selectedSchemaId()) {
        return { ...s, fields: [...s.fields, newField] };
      }
      return s;
    });

    this.schemas.set(updated);
    this.cdr?.markForCheck();
  }

  mergeCandidate(idA: string): void {
    this.duplicates.update(list => list.filter(d => d.idA !== idA));
    this.cdr?.markForCheck();
  }
}
