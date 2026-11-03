import { Component, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';

export interface LiveTranslationSession {
  sessionId: string;
  sourceLanguageCode: string;
  targetLanguageCode: string;
  speakerName: string;
  currentLatencyMs: number;
  chunksTranslated: number;
  streamStatus: string;
}

export interface VoiceCloneProfile {
  profileId: string;
  speakerName: string;
  pitchBaseHz: number;
  timbreResonanceScore: number;
}

@Component({
  selector: 'app-multilingual-voice-mesh-cockpit',
  standalone: true,
  imports: [CommonModule, FormsModule],
  template: `
    <div dir="ltr" data-testid="multilingual-voice-mesh-cockpit" class="p-6 bg-slate-900 text-slate-100 min-h-screen">
      <header class="mb-8 border-b border-slate-800 pb-4 flex justify-between items-center">
        <div>
          <h1 class="text-2xl font-bold tracking-tight text-white flex items-center gap-2">
            <span class="inline-block w-3 h-3 rounded-full bg-violet-500 animate-pulse"></span>
            Simultaneous Multilingual Speech Translation Mesh
          </h1>
          <p class="text-slate-400 text-sm mt-1">
            Real-Time Cross-Lingual Audio Translation with Zero-Shot Vocal Pitch, Timbre & Cadence Preservation
          </p>
        </div>
        <div class="flex gap-3">
          <button (click)="registerVoiceClone()" class="px-4 py-2 bg-violet-600 hover:bg-violet-500 text-white font-medium rounded-lg shadow-sm transition">
            Extract Zero-Shot Timbre Embedding
          </button>
        </div>
      </header>

      <!-- KPI Metrics Grid -->
      <section class="grid grid-cols-1 md:grid-cols-4 gap-4 mb-8">
        <div class="bg-slate-800/60 border border-slate-700/50 p-4 rounded-xl">
          <span class="text-xs font-semibold text-slate-400 uppercase tracking-wider">Live Translations</span>
          <p class="text-2xl font-extrabold text-white mt-1">{{ sessions().length }}</p>
          <span class="text-xs text-violet-400">Full-Duplex Telephony</span>
        </div>
        <div class="bg-slate-800/60 border border-slate-700/50 p-4 rounded-xl">
          <span class="text-xs font-semibold text-slate-400 uppercase tracking-wider">Language Pairs</span>
          <p class="text-2xl font-extrabold text-indigo-400 mt-1">42 Pairs</p>
          <span class="text-xs text-slate-400">EU, APAC & LATAM Coverage</span>
        </div>
        <div class="bg-slate-800/60 border border-slate-700/50 p-4 rounded-xl">
          <span class="text-xs font-semibold text-slate-400 uppercase tracking-wider">P99 Stream Latency</span>
          <p class="text-2xl font-extrabold text-emerald-400 mt-1">168.0ms</p>
          <span class="text-xs text-slate-400">Sub-200ms Acoustic Pipeline</span>
        </div>
        <div class="bg-slate-800/60 border border-slate-700/50 p-4 rounded-xl">
          <span class="text-xs font-semibold text-slate-400 uppercase tracking-wider">Timbre Fidelity</span>
          <p class="text-2xl font-extrabold text-cyan-400 mt-1">{{ (timbreSimilarityScore() * 100).toFixed(1) }}%</p>
          <span class="text-xs text-emerald-400">Exact Vocal Resonance</span>
        </div>
      </section>

      <!-- Main Panels -->
      <div class="grid grid-cols-1 lg:grid-cols-3 gap-6">
        <!-- Live Sessions -->
        <section class="lg:col-span-2 bg-slate-800/40 border border-slate-700/50 rounded-xl p-5">
          <h2 class="text-lg font-semibold text-white mb-4">Active Cross-Lingual Audio Streams</h2>
          <div class="overflow-x-auto">
            <table class="w-full text-left border-collapse text-sm">
              <thead>
                <tr class="border-b border-slate-700 text-slate-400 text-xs">
                  <th class="pb-3 font-medium">Session ID</th>
                  <th class="pb-3 font-medium">Speaker</th>
                  <th class="pb-3 font-medium">Language Route</th>
                  <th class="pb-3 font-medium">Latency</th>
                  <th class="pb-3 font-medium">Translated Chunks</th>
                </tr>
              </thead>
              <tbody class="divide-y divide-slate-800">
                <tr *ngFor="let s of sessions()" class="hover:bg-slate-800/50 transition">
                  <td class="py-3 font-mono text-xs text-violet-300 font-semibold">{{ s.sessionId }}</td>
                  <td class="py-3 font-medium text-slate-200">{{ s.speakerName }}</td>
                  <td class="py-3">
                    <span class="px-2 py-0.5 text-xs rounded font-mono bg-indigo-500/20 text-indigo-300">
                      {{ s.sourceLanguageCode }} → {{ s.targetLanguageCode }}
                    </span>
                  </td>
                  <td class="py-3 font-mono font-bold text-cyan-400">{{ s.currentLatencyMs }}ms</td>
                  <td class="py-3 font-mono text-slate-400 text-xs">{{ s.chunksTranslated }}</td>
                </tr>
              </tbody>
            </table>
          </div>
        </section>

        <!-- Voice Clones -->
        <section class="bg-slate-800/40 border border-slate-700/50 rounded-xl p-5">
          <h2 class="text-lg font-semibold text-white mb-4">Zero-Shot Voice Clones</h2>
          <div class="space-y-3">
            <div *ngFor="let p of profiles()" class="p-3 bg-slate-900/60 border border-slate-700/40 rounded-lg">
              <div class="flex justify-between items-center mb-1">
                <span class="font-medium text-slate-200 text-sm">{{ p.speakerName }}</span>
                <span class="px-2 py-0.5 text-xs bg-emerald-500/20 text-emerald-300 rounded font-semibold font-mono">
                  {{ (p.timbreResonanceScore * 100).toFixed(1) }}%
                </span>
              </div>
              <p class="text-xs text-slate-400">Base Pitch: {{ p.pitchBaseHz }} Hz</p>
              <div class="mt-2 text-[10px] font-mono text-slate-400 bg-slate-950 p-1.5 rounded truncate">
                Profile Ref: {{ p.profileId }}
              </div>
            </div>
          </div>
        </section>
      </div>
    </div>
  `
})
export class MultilingualVoiceMeshCockpitComponent {
  sessions = signal<LiveTranslationSession[]>([
    { sessionId: 'TRN-SES-401', sourceLanguageCode: 'fr-FR', targetLanguageCode: 'en-US', speakerName: 'Alexandre Dubois (VP Sales EMEA)', currentLatencyMs: 168.0, chunksTranslated: 450, streamStatus: 'STREAMING_ACTIVE' },
    { sessionId: 'TRN-SES-402', sourceLanguageCode: 'ja-JP', targetLanguageCode: 'en-US', speakerName: 'Yuki Tanaka (Head of Partnerships)', currentLatencyMs: 185.0, chunksTranslated: 320, streamStatus: 'STREAMING_ACTIVE' }
  ]);

  profiles = signal<VoiceCloneProfile[]>([
    { profileId: 'VCL-ALEX-01', speakerName: 'Alexandre Dubois (VP Sales EMEA)', pitchBaseHz: 124.0, timbreResonanceScore: 0.982 },
    { profileId: 'VCL-YUKI-02', speakerName: 'Yuki Tanaka (Head of Partnerships)', pitchBaseHz: 195.0, timbreResonanceScore: 0.978 }
  ]);

  timbreSimilarityScore = signal(0.98);

  registerVoiceClone() {
    const newProfile: VoiceCloneProfile = {
      profileId: `VCL-${Date.now().toString().slice(-4)}`,
      speakerName: 'Chiara Rossi (VP Sales Southern Europe)',
      pitchBaseHz: 140.0,
      timbreResonanceScore: 0.985
    };
    this.profiles.update(list => [newProfile, ...list]);
  }
}
