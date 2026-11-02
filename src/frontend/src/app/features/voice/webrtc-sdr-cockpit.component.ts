import { Component, signal, computed } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';

export interface TranscriptSnippet {
  speaker: 'AI_SDR' | 'Lead';
  text: string;
  time: string;
}

@Component({
  selector: 'app-webrtc-sdr-cockpit',
  standalone: true,
  imports: [CommonModule, FormsModule],
  template: `
    <div class="webrtc-sdr-cockpit p-6 max-w-7xl mx-auto space-y-6" dir="ltr">
      <!-- Header -->
      <div class="flex flex-col md:flex-row md:items-center md:justify-between gap-4 border-b pb-6">
        <div>
          <div class="flex items-center gap-3">
            <h1 class="text-2xl font-bold tracking-tight text-slate-900">Real-Time WebRTC Voice SDR</h1>
            <span class="inline-flex items-center px-2.5 py-0.5 rounded-full text-xs font-semibold"
              [ngClass]="callStatus() === 'Active' ? 'bg-emerald-100 text-emerald-800' : 'bg-slate-100 text-slate-700'">
              {{ callStatus() }}
            </span>
          </div>
          <p class="text-sm text-slate-500 mt-1">
            Ultra-low latency duplex audio streaming with realtime barge-in voice activity detection (VAD).
          </p>
        </div>

        <div class="flex items-center gap-3">
          <div class="text-right hidden sm:block">
            <div class="text-xs text-slate-400">Audio Transport</div>
            <div class="text-xs font-mono font-semibold text-slate-700">WebRTC Opus &bull; 125ms RTT</div>
          </div>
          @if (callStatus() === 'Idle') {
            <button 
              (click)="startCall()"
              class="px-4 py-2 bg-emerald-600 hover:bg-emerald-700 text-white rounded-lg font-medium text-sm transition-colors shadow-sm"
              data-testid="btn-start-call">
              📞 Dial Lead
            </button>
          } @else {
            <button 
              (click)="endCall()"
              class="px-4 py-2 bg-rose-600 hover:bg-rose-700 text-white rounded-lg font-medium text-sm transition-colors shadow-sm"
              data-testid="btn-end-call">
              📴 End Call
            </button>
          }
        </div>
      </div>

      <!-- Live Call Monitor Panel -->
      <div class="grid grid-cols-1 lg:grid-cols-3 gap-6">
        <!-- Call Status & Telemetry Card -->
        <div class="bg-white border rounded-xl p-5 shadow-sm space-y-4">
          <h3 class="font-semibold text-slate-900 text-sm">Connection Telemetry</h3>
          
          <div class="space-y-3">
            <div class="flex items-center justify-between text-xs">
              <span class="text-slate-500">Target Lead:</span>
              <span class="font-medium text-slate-900">{{ leadName }} ({{ phoneNumber }})</span>
            </div>
            <div class="flex items-center justify-between text-xs">
              <span class="text-slate-500">Agent Persona:</span>
              <span class="font-medium text-indigo-600">{{ agentPersona }}</span>
            </div>
            <div class="flex items-center justify-between text-xs">
              <span class="text-slate-500">VAD State:</span>
              <span class="font-semibold px-2 py-0.5 rounded text-[11px]"
                [ngClass]="vadClass()">
                {{ vadState() }}
              </span>
            </div>
            <div class="flex items-center justify-between text-xs">
              <span class="text-slate-500">Packet Jitter:</span>
              <span class="font-mono text-slate-700">1.8 ms</span>
            </div>
            <div class="flex items-center justify-between text-xs">
              <span class="text-slate-500">Simulated Turn Count:</span>
              <span class="font-mono font-bold text-slate-900">{{ turnCount() }}</span>
            </div>
          </div>

          <!-- Waveform Visualizer Simulation -->
          <div class="pt-4 border-t">
            <div class="text-xs font-medium text-slate-500 mb-2">Duplex Audio Stream</div>
            <div class="h-16 bg-slate-900 rounded-lg p-2 flex items-center justify-center gap-1.5 overflow-hidden">
              @for (bar of waveformBars(); track $index) {
                <div 
                  class="w-1.5 bg-emerald-400 rounded-full transition-all duration-150"
                  [style.height.px]="callStatus() === 'Active' ? bar : 4">
                </div>
              }
            </div>
          </div>

          <!-- Interactive Test Action -->
          @if (callStatus() === 'Active') {
            <div class="pt-3 border-t space-y-2">
              <div class="text-xs font-semibold text-slate-700">Simulate Lead Interruption</div>
              <button 
                (click)="simulateLeadBargeIn()"
                class="w-full py-2 bg-indigo-50 hover:bg-indigo-100 text-indigo-700 rounded-lg text-xs font-medium transition-colors border border-indigo-200"
                data-testid="btn-simulate-bargein">
                ⚡ Simulate Barge-In: "Wait, tell me about pricing"
              </button>
            </div>
          }
        </div>

        <!-- Live Transcript Rolling Feed -->
        <div class="lg:col-span-2 bg-white border rounded-xl p-5 shadow-sm flex flex-col justify-between">
          <div>
            <div class="flex items-center justify-between pb-3 border-b mb-4">
              <h3 class="font-semibold text-slate-900 text-sm">Live Audio Transcript</h3>
              <span class="text-xs text-slate-400">Whisper Large-v3 Turbo &bull; 98.4% Confidence</span>
            </div>

            <div class="space-y-4 max-h-[380px] overflow-y-auto pr-2" data-testid="transcript-container">
              @if (transcript().length === 0) {
                <div class="p-8 text-center text-slate-400 text-xs">
                  Voice SDR session not yet active. Click "Dial Lead" to initiate outbound stream.
                </div>
              }
              @for (item of transcript(); track $index) {
                <div class="flex flex-col space-y-1"
                  [ngClass]="item.speaker === 'AI_SDR' ? 'items-start' : 'items-end'">
                  <div class="flex items-center gap-2 text-[11px] text-slate-400">
                    <span class="font-semibold" [ngClass]="item.speaker === 'AI_SDR' ? 'text-indigo-600' : 'text-emerald-700'">
                      {{ item.speaker === 'AI_SDR' ? '🤖 AI SDR (' + agentPersona + ')' : '👤 Lead' }}
                    </span>
                    <span>&bull;</span>
                    <span>{{ item.time }}</span>
                  </div>
                  <div 
                    class="p-3 rounded-xl text-xs max-w-md"
                    [ngClass]="item.speaker === 'AI_SDR' ? 'bg-slate-100 text-slate-800' : 'bg-indigo-600 text-white'">
                    {{ item.text }}
                  </div>
                </div>
              }
            </div>
          </div>

          <!-- Quick Objections / Hints -->
          <div class="mt-4 pt-3 border-t flex flex-wrap items-center justify-between gap-2 text-xs text-slate-400">
            <span>Sub-second voice pipeline active: STT &rarr; LLM Agent &rarr; ElevenLabs Neural TTS</span>
            <span class="font-semibold text-emerald-600">Zero packet drops</span>
          </div>
        </div>
      </div>
    </div>
  `
})
export class WebRtcSdrCockpitComponent {
  callStatus = signal<'Idle' | 'Active' | 'Completed'>('Idle');
  vadState = signal<'Silence' | 'UserSpeaking' | 'AgentSpeaking' | 'BargeInInterrupted'>('Silence');
  turnCount = signal<number>(0);

  leadName = 'David Miller (VP Marketing)';
  phoneNumber = '+1 (415) 883-9201';
  agentPersona = 'Sarah Sales SDR';

  waveformBars = signal<number[]>([12, 28, 40, 22, 14, 32, 48, 20, 16, 36, 44, 25, 15]);

  transcript = signal<TranscriptSnippet[]>([]);

  vadClass = computed(() => {
    switch (this.vadState()) {
      case 'Silence': return 'bg-slate-100 text-slate-700';
      case 'UserSpeaking': return 'bg-amber-100 text-amber-800 animate-pulse';
      case 'AgentSpeaking': return 'bg-indigo-100 text-indigo-800';
      case 'BargeInInterrupted': return 'bg-rose-100 text-rose-800 font-bold';
    }
  });

  startCall(): void {
    this.callStatus.set('Active');
    this.vadState.set('AgentSpeaking');
    this.turnCount.set(1);
    this.transcript.set([
      {
        speaker: 'AI_SDR',
        text: 'Hi David! This is Sarah from SGrowing. I noticed your team recently scaled advertising spend across Meta. Do you have 2 minutes to discuss automated ROAS attribution?',
        time: 'Just now'
      }
    ]);
  }

  simulateLeadBargeIn(): void {
    if (this.callStatus() !== 'Active') return;

    this.vadState.set('BargeInInterrupted');
    this.turnCount.update(c => c + 1);

    this.transcript.update(list => [
      ...list,
      {
        speaker: 'Lead',
        text: 'Wait Sarah, before we dive into attribution, what is your standard onboarding timeline and CRM integration support?',
        time: 'Just now'
      }
    ]);

    // Simulate AI sub-second reaction
    setTimeout(() => {
      this.vadState.set('AgentSpeaking');
      this.turnCount.update(c => c + 1);
      this.transcript.update(list => [
        ...list,
        {
          speaker: 'AI_SDR',
          text: 'Great question, David! We offer native one-click integrations for HubSpot and Salesforce, and our typical setup takes under 14 days with full team onboarding.',
          time: 'Just now'
        }
      ]);
    }, 400);
  }

  endCall(): void {
    this.callStatus.set('Completed');
    this.vadState.set('Silence');
  }
}
