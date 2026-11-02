import { Component, ChangeDetectionStrategy, signal, computed } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';

export interface AvatarPersona {
  id: string;
  name: string;
  title: string;
  voiceTone: string;
  expertise: string[];
}

export interface VisemeFrame {
  blendshapeName: string;
  weight: number;
  offsetMs: number;
}

@Component({
  selector: 'app-video-avatar-cockpit',
  standalone: true,
  imports: [CommonModule, FormsModule],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="p-6 max-w-7xl mx-auto space-y-6" dir="ltr" data-testid="video-avatar-cockpit">
      <!-- Header -->
      <div class="flex flex-col md:flex-row md:items-center justify-between gap-4 border-b border-slate-700/60 pb-6">
        <div>
          <div class="flex items-center gap-2 text-xs font-semibold uppercase tracking-wider text-rose-400">
            <span class="inline-block w-2 h-2 rounded-full bg-rose-500 animate-ping"></span>
            Conversational AI & Synthetic Media • Horizon 47
          </div>
          <h1 class="text-3xl font-extrabold text-white tracking-tight mt-1">
            Real-Time AI Video Avatar & Stream Synthesis
          </h1>
          <p class="text-slate-400 text-sm mt-1">
            Sub-120ms WebRTC low-latency stream pipeline, audio-to-viseme lip-sync & synchronized deck projection.
          </p>
        </div>

        <div class="flex items-center gap-3">
          <button
            (click)="toggleStreamSession()"
            data-testid="toggle-stream-btn"
            [class]="isStreaming() ? 'bg-rose-600 hover:bg-rose-500' : 'bg-emerald-600 hover:bg-emerald-500'"
            class="px-4 py-2 text-white rounded-lg text-sm font-semibold transition-colors flex items-center gap-2 shadow-lg"
          >
            <span>{{ isStreaming() ? '■ End Avatar Session' : '▶ Start WebRTC Session' }}</span>
          </button>
        </div>
      </div>

      <!-- Telemetry Stats -->
      <div class="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-4 gap-4">
        <div class="bg-slate-800/80 border border-slate-700/60 rounded-xl p-4">
          <div class="text-slate-400 text-xs font-medium uppercase">Stream Pipeline</div>
          <div class="text-2xl font-bold text-white mt-1" data-testid="stat-stream-status">
            {{ isStreaming() ? 'WebRTC 60 FPS' : 'Idle / Standby' }}
          </div>
          <div class="text-emerald-400 text-xs mt-1">H.264 / Opus Stereo</div>
        </div>
        <div class="bg-slate-800/80 border border-slate-700/60 rounded-xl p-4">
          <div class="text-slate-400 text-xs font-medium uppercase">Round-Trip Latency</div>
          <div class="text-2xl font-bold text-emerald-400 mt-1" data-testid="stat-latency">{{ rttLatencyMs() }} ms</div>
          <div class="text-slate-400 text-xs mt-1">Sub-150ms Enterprise SLA</div>
        </div>
        <div class="bg-slate-800/80 border border-slate-700/60 rounded-xl p-4">
          <div class="text-slate-400 text-xs font-medium uppercase">Active Blendshapes</div>
          <div class="text-2xl font-bold text-rose-400 mt-1">{{ activeVisemes().length }} Active</div>
          <div class="text-slate-400 text-xs mt-1">JawOpen, MouthPucker, Smile</div>
        </div>
        <div class="bg-slate-800/80 border border-slate-700/60 rounded-xl p-4">
          <div class="text-slate-400 text-xs font-medium uppercase">Projected Slide</div>
          <div class="text-2xl font-bold text-indigo-400 mt-1" data-testid="stat-active-slide">Slide {{ activeSlideIndex() }}</div>
          <div class="text-slate-400 text-xs mt-1">Synchronized Pitch Deck</div>
        </div>
      </div>

      <!-- Main Stage: Avatar Canvas & Controls -->
      <div class="grid grid-cols-1 lg:grid-cols-12 gap-6">
        <!-- Persona Selector & Avatar Stream Viewport (7 cols) -->
        <div class="lg:col-span-7 space-y-4">
          <!-- Persona Selector Tabs -->
          <div class="flex gap-2 p-1 bg-slate-900/80 rounded-xl border border-slate-800">
            @for (persona of personas(); track persona.id) {
              <button
                (click)="selectPersona(persona)"
                [class.bg-slate-800]="selectedPersona().id === persona.id"
                [class.text-white]="selectedPersona().id === persona.id"
                [class.text-slate-400]="selectedPersona().id !== persona.id"
                class="flex-1 py-2 px-3 rounded-lg text-xs font-semibold transition-all text-center"
                [attr.data-testid]="'persona-tab-' + persona.id"
              >
                {{ persona.name }}
              </button>
            }
          </div>

          <!-- Video Stream Simulation Canvas -->
          <div
            class="relative aspect-video bg-gradient-to-br from-slate-950 via-slate-900 to-indigo-950 rounded-2xl border border-slate-700/80 overflow-hidden shadow-2xl flex flex-col justify-between p-6"
            data-testid="avatar-viewport-canvas"
          >
            <!-- Watermark / Stream Header -->
            <div class="flex items-center justify-between z-10">
              <div class="flex items-center gap-2 bg-slate-900/80 backdrop-blur-md px-3 py-1.5 rounded-full border border-slate-700 text-xs">
                <span class="w-2.5 h-2.5 rounded-full" [class]="isStreaming() ? 'bg-rose-500 animate-pulse' : 'bg-slate-500'"></span>
                <span class="font-bold text-white">{{ selectedPersona().name }}</span>
                <span class="text-slate-400">• {{ selectedPersona().title }}</span>
              </div>
              <div class="text-[11px] font-mono text-emerald-400 bg-slate-900/80 px-2 py-1 rounded border border-slate-800">
                1080p • 60 FPS
              </div>
            </div>

            <!-- Simulated Avatar Visual Representation -->
            <div class="flex flex-col items-center justify-center my-auto z-10 space-y-3">
              <div class="relative w-36 h-36 rounded-full bg-slate-800/80 border-2 border-rose-500/60 flex items-center justify-center shadow-xl">
                <!-- Viseme Lip Sync Dynamic Aura -->
                <div
                  class="absolute inset-0 rounded-full transition-transform duration-100 opacity-60"
                  [style.transform]="'scale(' + (1 + (currentVisemeIntensity() * 0.25)) + ')'"
                  [style.background]="isSpeaking() ? 'radial-gradient(circle, rgba(244,63,94,0.4) 0%, transparent 70%)' : 'none'"
                ></div>

                <div class="text-5xl font-extrabold text-white tracking-widest">
                  {{ selectedPersona().name.substring(0, 2) }}
                </div>
              </div>

              <div class="text-center">
                <div class="text-xs text-rose-300 font-medium">Tone: {{ selectedPersona().voiceTone }}</div>
                <div class="text-[11px] text-slate-400 max-w-sm mt-1">
                  {{ lastSpokenUtterance() }}
                </div>
              </div>
            </div>

            <!-- Blendshape Meter Overlay -->
            <div class="flex items-center justify-between z-10 bg-slate-900/80 backdrop-blur-md px-4 py-2 rounded-xl border border-slate-800 text-xs">
              <div class="text-slate-400">Viseme Blendshape:</div>
              <div class="flex gap-2">
                @for (v of activeVisemes(); track v.blendshapeName) {
                  <span class="bg-rose-950 text-rose-300 px-2 py-0.5 rounded text-[10px] font-mono border border-rose-800">
                    {{ v.blendshapeName }}: {{ v.weight }}
                  </span>
                }
              </div>
            </div>
          </div>
        </div>

        <!-- Speech Synthesis & Interactive Deck Projection (5 cols) -->
        <div class="lg:col-span-5 space-y-4">
          <!-- Speech Synthesis Controller -->
          <div class="bg-slate-800/80 border border-slate-700/60 rounded-xl p-5 space-y-3">
            <h3 class="font-bold text-white text-base">Speech & Gesture Synthesis</h3>
            <p class="text-xs text-slate-400">
              Prompt the avatar with speech input to generate phoneme-synchronized blendshapes.
            </p>

            <textarea
              [(ngModel)]="speechPromptInput"
              data-testid="input-speech-prompt"
              rows="3"
              class="w-full bg-slate-900 border border-slate-700 rounded-lg p-3 text-xs text-white placeholder-slate-500 focus:outline-none focus:border-rose-500 font-sans"
              placeholder="Enter script for avatar to deliver..."
            ></textarea>

            <div class="flex items-center justify-between gap-3">
              <select
                [(ngModel)]="selectedEmotion"
                class="bg-slate-900 border border-slate-700 text-slate-200 text-xs rounded-lg px-2.5 py-1.5 focus:outline-none"
              >
                <option value="Confident">Confident</option>
                <option value="Enthusiastic">Enthusiastic</option>
                <option value="Empathetic">Empathetic</option>
                <option value="Serious">Serious</option>
              </select>

              <button
                (click)="synthesizeSpeech()"
                data-testid="synthesize-speech-btn"
                class="px-4 py-2 bg-rose-600 hover:bg-rose-500 text-white rounded-lg text-xs font-semibold shadow transition-colors"
              >
                Synthesize & Speak
              </button>
            </div>
          </div>

          <!-- Synchronized Presentation Deck Projection -->
          <div class="bg-slate-800/80 border border-slate-700/60 rounded-xl p-5 space-y-3">
            <div class="flex items-center justify-between border-b border-slate-700/60 pb-3">
              <h3 class="font-bold text-white text-base">Interactive Deck Projection</h3>
              <span class="text-xs text-slate-400">WebRTC Canvas Sync</span>
            </div>

            <div class="space-y-2">
              @for (slide of presentationSlides; track slide.index) {
                <div
                  (click)="projectSlide(slide.index)"
                  [class.border-indigo-500]="activeSlideIndex() === slide.index"
                  [class.bg-slate-700/40]="activeSlideIndex() === slide.index"
                  class="p-3 bg-slate-900/60 border border-slate-800 rounded-lg hover:border-slate-700 cursor-pointer transition-all flex items-center justify-between"
                  [attr.data-testid]="'slide-item-' + slide.index"
                >
                  <div>
                    <div class="text-xs font-bold text-white">{{ slide.title }}</div>
                    <div class="text-[11px] text-slate-400 mt-0.5">{{ slide.caption }}</div>
                  </div>
                  <button
                    data-testid="project-slide-btn"
                    class="px-2.5 py-1 bg-indigo-600/80 hover:bg-indigo-600 text-white text-[11px] font-semibold rounded"
                  >
                    {{ activeSlideIndex() === slide.index ? 'Live' : 'Project' }}
                  </button>
                </div>
              }
            </div>
          </div>
        </div>
      </div>
    </div>
  `
})
export class VideoAvatarCockpitComponent {
  readonly personas = signal<AvatarPersona[]>([
    {
      id: 'persona_elena_sdr',
      name: 'Elena Vance',
      title: 'Lead Enterprise SDR',
      voiceTone: 'Enthusiastic & Confident',
      expertise: ['SaaS ROI Analysis', 'B2B Objection Handling']
    },
    {
      id: 'persona_marcus_architect',
      name: 'Marcus Sterling',
      title: 'Principal Solutions Architect',
      voiceTone: 'Authoritative & Empathetic',
      expertise: ['Zero-Trust Security', 'Multi-Tenant Scalability']
    },
    {
      id: 'persona_sarah_cs',
      name: 'Sarah Chen',
      title: 'VP Customer Success',
      voiceTone: 'Warm & Solution-Oriented',
      expertise: ['Onboarding Acceleration', 'Churn Mitigation']
    }
  ]);

  readonly selectedPersona = signal<AvatarPersona>(this.personas()[0]);
  readonly isStreaming = signal(true);
  readonly rttLatencyMs = signal(95);
  readonly isSpeaking = signal(false);
  readonly currentVisemeIntensity = signal(0.75);

  speechPromptInput = 'Welcome to Growth OS! Let me show you how our multi-tenant cloud accelerates your revenue.';
  selectedEmotion = 'Confident';
  readonly lastSpokenUtterance = signal('Avatar stream connected and awaiting client interaction.');

  readonly activeVisemes = signal<VisemeFrame[]>([
    { blendshapeName: 'JawOpen', weight: 0.85, offsetMs: 0 },
    { blendshapeName: 'MouthPucker', weight: 0.62, offsetMs: 160 },
    { blendshapeName: 'MouthSmileLeft', weight: 0.45, offsetMs: 320 }
  ]);

  readonly activeSlideIndex = signal(1);

  readonly presentationSlides = [
    { index: 1, title: 'Growth OS Architecture Overview', caption: 'High-throughput event mesh & distributed DB partitioning' },
    { index: 2, title: 'Enterprise ROI & Payback Velocity', caption: '340% average net retention enhancement' },
    { index: 3, title: 'Zero-Trust Merkle Compliance', caption: 'Verifiable cryptographic tamper-proof ledger' }
  ];

  selectPersona(persona: AvatarPersona): void {
    this.selectedPersona.set(persona);
    this.lastSpokenUtterance.set(`Switched avatar persona to ${persona.name}.`);
  }

  toggleStreamSession(): void {
    this.isStreaming.update(v => !v);
  }

  synthesizeSpeech(): void {
    if (!this.speechPromptInput.trim()) return;

    this.isSpeaking.set(true);
    this.lastSpokenUtterance.set(this.speechPromptInput.trim());

    // Generate dynamic visemes
    this.activeVisemes.set([
      { blendshapeName: 'JawOpen', weight: 0.92, offsetMs: 0 },
      { blendshapeName: 'MouthSmileRight', weight: 0.55, offsetMs: 140 },
      { blendshapeName: 'BrowInnerUp', weight: 0.38, offsetMs: 280 }
    ]);

    setTimeout(() => {
      this.isSpeaking.set(false);
    }, 1200);
  }

  projectSlide(index: number): void {
    this.activeSlideIndex.set(index);
  }
}
