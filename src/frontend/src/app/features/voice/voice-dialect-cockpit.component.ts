import { Component, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';

export interface RegionalDialectProfileRecord {
  dialectCode: string;
  regionName: string;
  linguisticCadenceDescription: string;
  pitchAdjustmentSemiTones: number;
  speechRateMultiplier: number;
}

export interface AdaptedPhraseRecord {
  adaptationId: string;
  dialectCode: string;
  standardPhrase: string;
  culturallyAdaptedPhrase: string;
  naturalnessScore: number;
}

@Component({
  selector: 'app-voice-dialect-cockpit',
  standalone: true,
  imports: [CommonModule, FormsModule],
  template: `
    <div dir="ltr" data-testid="voice-dialect-cockpit" class="p-6 bg-slate-900 text-slate-100 min-h-screen">
      <header class="mb-8 border-b border-slate-800 pb-4 flex justify-between items-center">
        <div>
          <h1 class="text-2xl font-bold tracking-tight text-white flex items-center gap-2">
            <span class="inline-block w-3 h-3 rounded-full bg-emerald-500 animate-pulse"></span>
            Voice Agent Real-Time Dialect Adaptation & Cultural Idiom Mesh
          </h1>
          <p class="text-slate-400 text-sm mt-1">
            Dynamic Phonetic Inflection, Regional Colloquialism Mapping & Culturally Nuanced B2B Acoustic Delivery
          </p>
        </div>
        <div class="flex gap-3">
          <button (click)="adaptTestPhrase()" class="px-4 py-2 bg-emerald-600 hover:bg-emerald-500 text-white font-medium rounded-lg shadow-sm transition">
            Adapt Cultural Phrase
          </button>
        </div>
      </header>

      <!-- KPI Metrics Grid -->
      <section class="grid grid-cols-1 md:grid-cols-4 gap-4 mb-8">
        <div class="bg-slate-800/60 border border-slate-700/50 p-4 rounded-xl">
          <span class="text-xs font-semibold text-slate-400 uppercase tracking-wider">Dialects Active</span>
          <p class="text-2xl font-extrabold text-white mt-1">{{ profiles().length }}</p>
          <span class="text-xs text-emerald-400">Phonetic Accents</span>
        </div>
        <div class="bg-slate-800/60 border border-slate-700/50 p-4 rounded-xl">
          <span class="text-xs font-semibold text-slate-400 uppercase tracking-wider">Adapted Phrases</span>
          <p class="text-2xl font-extrabold text-white mt-1">{{ adaptations().length }}</p>
          <span class="text-xs text-cyan-400">Contextual Vernacular</span>
        </div>
        <div class="bg-slate-800/60 border border-slate-700/50 p-4 rounded-xl">
          <span class="text-xs font-semibold text-slate-400 uppercase tracking-wider">Naturalness Score</span>
          <p class="text-2xl font-extrabold text-white mt-1">98.2%</p>
          <span class="text-xs text-amber-400">Human MOS Equivalence</span>
        </div>
        <div class="bg-slate-800/60 border border-slate-700/50 p-4 rounded-xl">
          <span class="text-xs font-semibold text-slate-400 uppercase tracking-wider">Sub-Turn Latency</span>
          <p class="text-2xl font-extrabold text-white mt-1">12ms</p>
          <span class="text-xs text-indigo-400">Zero Acoustic Delay</span>
        </div>
      </section>

      <!-- Main Panels -->
      <div class="grid grid-cols-1 lg:grid-cols-2 gap-8">
        <!-- Regional Profiles -->
        <section class="bg-slate-800/40 border border-slate-700/60 rounded-xl p-6">
          <h2 class="text-lg font-semibold text-white mb-4">Calibrated Regional Dialects</h2>
          <div class="space-y-4">
            <div *ngFor="let p of profiles()" class="bg-slate-900/60 border border-slate-700/40 p-4 rounded-lg">
              <div class="flex justify-between items-center mb-1">
                <span class="font-bold text-white">{{ p.regionName }}</span>
                <span class="text-xs px-2 py-0.5 rounded font-mono bg-slate-800 text-emerald-300 border border-slate-700">
                  {{ p.dialectCode }}
                </span>
              </div>
              <p class="text-xs text-slate-400 mb-2">{{ p.linguisticCadenceDescription }}</p>
              <div class="flex justify-between text-xs text-slate-300 font-mono">
                <span>Pitch: {{ p.pitchAdjustmentSemiTones }} semitones</span>
                <span>Rate: {{ p.speechRateMultiplier }}x</span>
              </div>
            </div>
          </div>
        </section>

        <!-- Adapted Phrases -->
        <section class="bg-slate-800/40 border border-slate-700/60 rounded-xl p-6">
          <h2 class="text-lg font-semibold text-white mb-4">Real-Time Idiom Adaptations</h2>
          <div class="space-y-4">
            <div *ngFor="let a of adaptations()" class="bg-slate-900/60 border border-slate-700/40 p-4 rounded-lg">
              <div class="flex justify-between items-center mb-1">
                <span class="text-xs font-bold text-emerald-400">{{ a.dialectCode }}</span>
                <span class="text-[10px] font-mono text-slate-400">{{ (a.naturalnessScore * 100).toFixed(1) }}% Natural</span>
              </div>
              <p class="text-xs text-slate-400 italic mb-2">Original: "{{ a.standardPhrase }}"</p>
              <div class="p-2.5 bg-emerald-950/30 border border-emerald-800/40 rounded text-xs text-emerald-200 font-medium">
                Adapted: "{{ a.culturallyAdaptedPhrase }}"
              </div>
            </div>
          </div>
        </section>
      </div>
    </div>
  `
})
export class VoiceDialectCockpitComponent {
  profiles = signal<RegionalDialectProfileRecord[]>([
    { dialectCode: 'en-US-TEXAS', regionName: 'US Southern & Texas Enterprise', linguisticCadenceDescription: 'Warm drawl, unhurried cadence, cordial respectful address', pitchAdjustmentSemiTones: -0.8, speechRateMultiplier: 0.94 },
    { dialectCode: 'en-GB-SCOTTISH', regionName: 'UK Scotland & Northern Enterprise', linguisticCadenceDescription: 'Crisp rhotic cadence, direct concise phrasing, high transparency', pitchAdjustmentSemiTones: 0.5, speechRateMultiplier: 1.02 }
  ]);

  adaptations = signal<AdaptedPhraseRecord[]>([
    { adaptationId: 'ADP-001', dialectCode: 'en-US-TEXAS', standardPhrase: 'We are ready to proceed with the contract.', culturallyAdaptedPhrase: "We're ready to get this across the finish line for y'all, much obliged.", naturalnessScore: 0.982 }
  ]);

  adaptTestPhrase() {
    const newAdaptation: AdaptedPhraseRecord = {
      adaptationId: `ADP-${Date.now().toString().slice(-4)}`,
      dialectCode: 'en-GB-SCOTTISH',
      standardPhrase: 'Let us finalize the contract terms today.',
      culturallyAdaptedPhrase: "Right then, let's get these contract terms sorted straight away.",
      naturalnessScore: 0.985
    };
    this.adaptations.update(list => [newAdaptation, ...list]);
  }
}
