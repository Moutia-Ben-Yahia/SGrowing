import { Component, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';

export interface ActiveS2sSession {
  sessionId: string;
  clientAudioCodec: string;
  voicePersona: string;
  currentRoundTripLatencyMs: number;
  framesProcessed: number;
  streamStatus: string;
}

export interface BargeInEventRecord {
  eventId: string;
  sessionId: string;
  userSpeechOnsetLatencyMs: number;
  cutoffTtsPhrase: string;
}

@Component({
  selector: 'app-speech-streaming-mesh-cockpit',
  standalone: true,
  imports: [CommonModule, FormsModule],
  template: `
    <div dir="ltr" data-testid="speech-streaming-mesh-cockpit" class="p-6 bg-slate-900 text-slate-100 min-h-screen">
      <header class="mb-8 border-b border-slate-800 pb-4 flex justify-between items-center">
        <div>
          <h1 class="text-2xl font-bold tracking-tight text-white flex items-center gap-2">
            <span class="inline-block w-3 h-3 rounded-full bg-emerald-500 animate-pulse"></span>
            AI Voice Telephony Speech-to-Speech (S2S) Audio Streaming Mesh
          </h1>
          <p class="text-slate-400 text-sm mt-1">
            Sub-80ms Full-Duplex Audio Pipeline, Zero-Latency Acoustic Barge-In & Continuous Neural Prosody Modulation
          </p>
        </div>
        <div class="flex gap-3">
          <button (click)="triggerBargeIn()" class="px-4 py-2 bg-rose-600 hover:bg-rose-500 text-white font-medium rounded-lg shadow-sm transition">
            Trigger Instant Barge-In Cutoff
          </button>
        </div>
      </header>

      <!-- KPI Metrics Grid -->
      <section class="grid grid-cols-1 md:grid-cols-4 gap-4 mb-8">
        <div class="bg-slate-800/60 border border-slate-700/50 p-4 rounded-xl">
          <span class="text-xs font-semibold text-slate-400 uppercase tracking-wider">Active S2S Streams</span>
          <p class="text-2xl font-extrabold text-white mt-1">{{ sessions().length }}</p>
          <span class="text-xs text-emerald-400">Opus 48kHz Full-Duplex</span>
        </div>
        <div class="bg-slate-800/60 border border-slate-700/50 p-4 rounded-xl">
          <span class="text-xs font-semibold text-slate-400 uppercase tracking-wider">P99 Audio Latency</span>
          <p class="text-2xl font-extrabold text-emerald-400 mt-1">68.5ms</p>
          <span class="text-xs text-slate-400">No Intermediate Text Step</span>
        </div>
        <div class="bg-slate-800/60 border border-slate-700/50 p-4 rounded-xl">
          <span class="text-xs font-semibold text-slate-400 uppercase tracking-wider">Barge-In Cutoff</span>
          <p class="text-2xl font-extrabold text-cyan-400 mt-1">38.5ms</p>
          <span class="text-xs text-slate-400">Sub-50ms TTS Dampening</span>
        </div>
        <div class="bg-slate-800/60 border border-slate-700/50 p-4 rounded-xl">
          <span class="text-xs font-semibold text-slate-400 uppercase tracking-wider">Turn Confidence</span>
          <p class="text-2xl font-extrabold text-indigo-400 mt-1">{{ turnTakingConfidencePct() }}%</p>
          <span class="text-xs text-emerald-400">Natural Human Overlap</span>
        </div>
      </section>

      <!-- Main Panels -->
      <div class="grid grid-cols-1 lg:grid-cols-3 gap-6">
        <!-- Sessions Table -->
        <section class="lg:col-span-2 bg-slate-800/40 border border-slate-700/50 rounded-xl p-5">
          <h2 class="text-lg font-semibold text-white mb-4">Active Neural Audio Streams</h2>
          <div class="overflow-x-auto">
            <table class="w-full text-left border-collapse text-sm">
              <thead>
                <tr class="border-b border-slate-700 text-slate-400 text-xs">
                  <th class="pb-3 font-medium">Session ID</th>
                  <th class="pb-3 font-medium">Codec Profile</th>
                  <th class="pb-3 font-medium">Persona</th>
                  <th class="pb-3 font-medium">Round-Trip Latency</th>
                  <th class="pb-3 font-medium">Frames</th>
                </tr>
              </thead>
              <tbody class="divide-y divide-slate-800">
                <tr *ngFor="let s of sessions()" class="hover:bg-slate-800/50 transition">
                  <td class="py-3 font-mono text-xs text-emerald-400 font-semibold">{{ s.sessionId }}</td>
                  <td class="py-3 text-slate-300 text-xs">{{ s.clientAudioCodec }}</td>
                  <td class="py-3 font-medium text-slate-200">{{ s.voicePersona }}</td>
                  <td class="py-3 font-mono font-bold text-cyan-400">{{ s.currentRoundTripLatencyMs }}ms</td>
                  <td class="py-3 font-mono text-slate-400 text-xs">{{ s.framesProcessed.toLocaleString() }}</td>
                </tr>
              </tbody>
            </table>
          </div>
        </section>

        <!-- Barge-In Events -->
        <section class="bg-slate-800/40 border border-slate-700/50 rounded-xl p-5">
          <h2 class="text-lg font-semibold text-white mb-4">Acoustic Barge-In Cutoffs</h2>
          <div class="space-y-3">
            <div *ngFor="let b of bargeIns()" class="p-3 bg-slate-900/60 border border-slate-700/40 rounded-lg">
              <div class="flex justify-between items-center mb-1">
                <span class="font-mono text-xs text-rose-400 font-bold">{{ b.eventId }}</span>
                <span class="px-2 py-0.5 text-xs bg-rose-500/20 text-rose-300 rounded font-semibold font-mono">{{ b.userSpeechOnsetLatencyMs }}ms</span>
              </div>
              <p class="text-xs text-slate-300 italic mb-1">"{{ b.cutoffTtsPhrase }}"</p>
              <div class="text-[10px] font-mono text-slate-400">
                Session: {{ b.sessionId }}
              </div>
            </div>
          </div>
        </section>
      </div>
    </div>
  `
})
export class SpeechStreamingMeshCockpitComponent {
  sessions = signal<ActiveS2sSession[]>([
    { sessionId: 'S2S-STREAM-901', clientAudioCodec: 'Opus 48kHz (Fullband stereo)', voicePersona: 'Executive Sovereign British Female (Neural S2S)', currentRoundTripLatencyMs: 68.5, framesProcessed: 12450, streamStatus: 'STREAMING_ACTIVE' },
    { sessionId: 'S2S-STREAM-902', clientAudioCodec: 'PCM16 24kHz', voicePersona: 'Conversational Silicon Valley Male (Low-Jitter)', currentRoundTripLatencyMs: 74.2, framesProcessed: 8320, streamStatus: 'STREAMING_ACTIVE' }
  ]);

  bargeIns = signal<BargeInEventRecord[]>([
    { eventId: 'EVT-BARGE-441', sessionId: 'S2S-STREAM-901', userSpeechOnsetLatencyMs: 42.0, cutoffTtsPhrase: 'Certainly, our SLA provides 99.999% up...' }
  ]);

  turnTakingConfidencePct = signal(97.6);

  triggerBargeIn() {
    const newBarge: BargeInEventRecord = {
      eventId: `EVT-BARGE-${Date.now().toString().slice(-4)}`,
      sessionId: 'S2S-STREAM-902',
      userSpeechOnsetLatencyMs: 38.5,
      cutoffTtsPhrase: 'Yes, and we can also support multi-cloud deployment...'
    };
    this.bargeIns.update(list => [newBarge, ...list]);
  }
}
