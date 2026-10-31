import { Component, OnInit, OnDestroy, signal, computed, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { HttpClient } from '@angular/common/http';

export type StreamingPipelineState =
  | 'Idle'
  | 'Listening'
  | 'UserSpeaking'
  | 'Transcribing'
  | 'Thinking'
  | 'AgentSpeaking'
  | 'BargeInFlush';

export type TelephonyCallLifecycle =
  | 'Initiated'
  | 'Ringing'
  | 'Connected'
  | 'ActiveAudio'
  | 'TransferredToHuman'
  | 'CallCompleted';

export type SpeakerRole = 'User' | 'Agent' | 'System';

export interface VoiceAgentProfile {
  id: string;
  name: string;
  voiceModel: string;
  persona: string;
  baseLatencyMs: number;
  speakingRate: number;
  languageCode: string;
  bargeInEnabled: boolean;
}

export interface VoiceTranscriptSegment {
  id: string;
  sessionId: string;
  speaker: SpeakerRole;
  text: string;
  timestampUtc: string;
  confidence: number;
  isInterrupted: boolean;
  sentimentScore: number;
}

export interface TurnaroundLatencyMetric {
  turnId: string;
  userSpeechEndedUtc: string;
  vadSilenceDetectedUtc: string;
  transcriptionCompletedUtc: string;
  llmThoughtGeneratedUtc: string;
  ttsFirstChunkEmittedUtc: string;
  vadLatencyMs: number;
  asrLatencyMs: number;
  llmLatencyMs: number;
  ttsLatencyMs: number;
  totalTurnaroundMs: number;
  isSub600Ms: boolean;
  latencyGrade: string;
}

export interface ExtractedIntentAndEntities {
  intent: string;
  confidence: number;
  prospectBudget?: number;
  timeframe?: string;
  sentimentScore: number;
  sentimentLabel: string;
  crmDealValue?: number;
  entities: Record<string, string>;
  actionItems: string[];
}

export interface StreamingSessionMetrics {
  sessionId: string;
  tenantId: string;
  callSid: string;
  lifecycle: TelephonyCallLifecycle;
  pipelineState: StreamingPipelineState;
  voiceProfile: VoiceAgentProfile;
  callerNumber: string;
  calleeNumber: string;
  direction: 'Inbound' | 'Outbound';
  currentInputDb: number;
  currentOutputDb: number;
  totalFramesReceived: number;
  totalBargeInCount: number;
  totalBufferFlushes: number;
  averageTurnaroundLatencyMs: number;
  minTurnaroundLatencyMs: number;
  maxTurnaroundLatencyMs: number;
  sub600msCompliance: boolean;
  recentTurnLatencies: TurnaroundLatencyMetric[];
  transcript: VoiceTranscriptSegment[];
  extractedInsights?: ExtractedIntentAndEntities;
  startedAtUtc: string;
  endedAtUtc?: string;
  transferredAtUtc?: string;
  transferDestination?: string;
  transferReason?: string;
}

@Component({
  selector: 'app-voice-streaming-cockpit',
  standalone: true,
  imports: [CommonModule, FormsModule],
  template: `
    <div dir="ltr" class="flex flex-col h-screen bg-slate-950 text-slate-100 font-sans select-none overflow-hidden">
      <!-- Top Bar / Header -->
      <header class="h-16 border-b border-slate-800 bg-slate-900/80 px-6 flex items-center justify-between backdrop-blur-md shrink-0">
        <div class="flex items-center gap-3">
          <div class="h-10 w-10 rounded-xl bg-gradient-to-tr from-cyan-500 to-blue-600 flex items-center justify-center shadow-lg shadow-cyan-500/20 border border-cyan-400/30">
            <svg class="w-5 h-5 text-white" fill="none" viewBox="0 0 24 24" stroke="currentColor">
              <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M19 11a7 7 0 01-7 7m0 0a7 7 0 01-7-7m7 7v4m0 0H8m4 0h4m-4-8a3 3 0 100-6 3 3 0 000 6z" />
            </svg>
          </div>
          <div>
            <div class="flex items-center gap-2">
              <h1 class="text-base font-bold text-white tracking-wide">Real-Time Full-Duplex Voice AI Cockpit</h1>
              <span class="px-2 py-0.5 rounded text-[10px] font-mono font-semibold tracking-wider bg-cyan-950/80 text-cyan-400 border border-cyan-800/60 uppercase">
                Horizon 50 • Inbound IVR Audio Bridge
              </span>
            </div>
            <p class="text-xs text-slate-400">16kHz 16-bit Mono Linear PCM • Neural VAD &lt;100ms • Target Turnaround &lt;600ms</p>
          </div>
        </div>

        <!-- Telephony Status Indicator & Header Metrics -->
        <div class="flex items-center gap-4">
          <!-- Session Selector Dropdown -->
          <div class="flex items-center gap-2">
            <label class="text-xs text-slate-400 font-medium">Active Call:</label>
            <select
              [ngModel]="selectedSessionId()"
              (ngModelChange)="onSessionSelected($event)"
              class="bg-slate-800 border border-slate-700 rounded-lg px-3 py-1.5 text-xs text-slate-200 focus:outline-none focus:border-cyan-500">
              @for (s of sessions(); track s.sessionId) {
                <option [value]="s.sessionId">
                  {{ s.callerNumber }} ({{ s.lifecycle }}) - {{ s.voiceProfile.name }}
                </option>
              }
            </select>
          </div>

          <!-- Turnaround Target Badge -->
          <div class="flex items-center gap-2 px-3 py-1.5 rounded-lg border bg-slate-900"
            [ngClass]="activeSession()?.sub600msCompliance ? 'border-emerald-500/30 text-emerald-400' : 'border-amber-500/30 text-amber-400'">
            <div class="w-2 h-2 rounded-full"
              [ngClass]="activeSession()?.sub600msCompliance ? 'bg-emerald-400 animate-pulse' : 'bg-amber-400'"></div>
            <span class="text-xs font-mono font-bold">
              Latency: {{ activeSession()?.averageTurnaroundLatencyMs ?? 412 }}ms
            </span>
            <span class="text-[10px] uppercase font-semibold px-1 rounded bg-slate-800 text-slate-300">
              Target &lt;600ms
            </span>
          </div>

          <!-- Pipeline State Pill -->
          <div class="px-3 py-1.5 rounded-lg border text-xs font-mono font-bold flex items-center gap-2"
            [ngClass]="getPipelineStateBadgeClass(activeSession()?.pipelineState)">
            <span class="w-2 h-2 rounded-full" [ngClass]="getPipelineStateDotClass(activeSession()?.pipelineState)"></span>
            <span>{{ activeSession()?.pipelineState ?? 'Idle' }}</span>
          </div>

          <!-- New Call Button -->
          <button
            (click)="initiateNewCallModal = true"
            class="px-3 py-1.5 rounded-lg bg-cyan-600 hover:bg-cyan-500 text-white text-xs font-semibold shadow-md transition flex items-center gap-1.5">
            <svg class="w-3.5 h-3.5" fill="none" viewBox="0 0 24 24" stroke="currentColor">
              <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M12 4v16m8-8H4" />
            </svg>
            New IVR Session
          </button>
        </div>
      </header>

      <!-- Main Layout Body -->
      <div class="flex-1 flex overflow-hidden">
        <!-- LEFT COLUMN: Live Transcript & Extracted Insights -->
        <section class="flex-1 flex flex-col border-r border-slate-800 bg-slate-950 min-w-0">
          <!-- Call Information Bar -->
          <div class="h-12 border-b border-slate-800/80 px-6 flex items-center justify-between bg-slate-900/50 text-xs">
            <div class="flex items-center gap-6 text-slate-300 font-mono">
              <div><span class="text-slate-500">Call SID:</span> {{ activeSession()?.callSid }}</div>
              <div><span class="text-slate-500">From:</span> {{ activeSession()?.callerNumber }}</div>
              <div><span class="text-slate-500">Agent:</span> {{ activeSession()?.voiceProfile?.name }}</div>
              <div><span class="text-slate-500">Barge-Ins:</span> <span class="text-cyan-400 font-bold">{{ activeSession()?.totalBargeInCount ?? 0 }}</span></div>
            </div>

            <!-- CRM Deal Value Tag if detected -->
            @if (activeSession()?.extractedInsights?.crmDealValue; as dealVal) {
              <div class="flex items-center gap-1.5 px-2.5 py-1 rounded bg-indigo-950/70 border border-indigo-700/60 text-indigo-300 font-medium">
                <span>💰 Estimated Deal:</span>
                <span class="font-bold text-white">\${{ dealVal | number:'1.0-0' }}</span>
              </div>
            }
          </div>

          <!-- Live Transcript Stream Area -->
          <div class="flex-1 p-6 overflow-y-auto space-y-4">
            @for (segment of activeSession()?.transcript; track segment.id) {
              @if (segment.speaker === 'System') {
                <!-- System / Barge-In Event Pill -->
                <div class="flex items-center justify-center my-2">
                  <div class="px-4 py-1.5 rounded-full text-xs font-mono border flex items-center gap-2 shadow-sm"
                    [ngClass]="segment.text.includes('Barge-In') ? 'bg-amber-950/40 border-amber-500/40 text-amber-300' : 'bg-slate-900 border-slate-700 text-slate-400'">
                    @if (segment.text.includes('Barge-In')) {
                      <svg class="w-3.5 h-3.5 text-amber-400 animate-spin" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                        <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M13 10V3L4 14h7v7l9-11h-7z" />
                      </svg>
                    }
                    <span>{{ segment.text }}</span>
                  </div>
                </div>
              } @else if (segment.speaker === 'User') {
                <!-- User Utterance Bubble (Left / Blue) -->
                <div class="flex items-start gap-3 max-w-xl">
                  <div class="h-8 w-8 rounded-full bg-cyan-700/40 border border-cyan-500/50 flex items-center justify-center shrink-0 text-cyan-200 text-xs font-bold">
                    User
                  </div>
                  <div class="bg-cyan-950/30 border border-cyan-800/40 rounded-2xl rounded-tl-sm px-4 py-3 shadow-md">
                    <div class="flex items-center justify-between gap-4 mb-1">
                      <span class="text-xs font-semibold text-cyan-300">Prospect / Inbound Caller</span>
                      <div class="flex items-center gap-2 text-[10px] text-slate-400 font-mono">
                        <span>Confidence: {{ (segment.confidence * 100) | number:'1.0-0' }}%</span>
                        <span>•</span>
                        <span [ngClass]="segment.sentimentScore >= 0.3 ? 'text-emerald-400' : 'text-slate-400'">
                          Sentiment: {{ segment.sentimentScore > 0 ? '+' : '' }}{{ segment.sentimentScore | number:'1.2-2' }}
                        </span>
                      </div>
                    </div>
                    <p class="text-sm text-slate-100 leading-relaxed">{{ segment.text }}</p>
                  </div>
                </div>
              } @else {
                <!-- Agent Utterance Bubble (Right / Emerald) -->
                <div class="flex items-start justify-end gap-3 max-w-xl ml-auto">
                  <div class="bg-emerald-950/30 border border-emerald-800/40 rounded-2xl rounded-tr-sm px-4 py-3 shadow-md text-right relative">
                    <!-- Interrupted Mark if barged-in -->
                    @if (segment.isInterrupted) {
                      <div class="absolute -top-2.5 right-4 px-2 py-0.5 rounded text-[10px] font-mono font-bold bg-amber-500 text-slate-950 shadow">
                        INTERRUPTED VIA BARGE-IN
                      </div>
                    }
                    <div class="flex items-center justify-end gap-4 mb-1">
                      <div class="text-[10px] text-slate-400 font-mono">
                        {{ activeSession()?.voiceProfile?.voiceModel }}
                      </div>
                      <span class="text-xs font-semibold text-emerald-300">Voice AI Agent</span>
                    </div>
                    <p class="text-sm text-slate-100 leading-relaxed">{{ segment.text }}</p>
                  </div>
                  <div class="h-8 w-8 rounded-full bg-emerald-700/40 border border-emerald-500/50 flex items-center justify-center shrink-0 text-emerald-200 text-xs font-bold">
                    AI
                  </div>
                </div>
              }
            }
          </div>

          <!-- Extracted Intent & Entities Footer Panel -->
          @if (activeSession()?.extractedInsights; as insights) {
            <div class="border-t border-slate-800 bg-slate-900/60 p-4 shrink-0">
              <div class="flex items-center justify-between mb-3">
                <span class="text-xs font-bold text-slate-300 uppercase tracking-wider flex items-center gap-1.5">
                  <svg class="w-4 h-4 text-cyan-400" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                    <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M9.663 17h4.673M12 3v1m6.364 1.636l-.707.707M21 12h-1M4 12H3m3.343-5.657l-.707-.707m2.828 9.9a5 5 0 117.072 0l-.548.547A3.374 3.374 0 0014 18.469V19a2 2 0 11-4 0v-.531c0-.895-.356-1.754-.988-2.386l-.548-.547z" />
                  </svg>
                  Prospect Intent &amp; Structured CRM Intelligence
                </span>
                <span class="text-xs font-mono text-cyan-400">Confidence: {{ (insights.confidence * 100) | number:'1.0-0' }}%</span>
              </div>
              <div class="grid grid-cols-4 gap-3 text-xs mb-3">
                <div class="bg-slate-950 border border-slate-800 rounded-lg p-2.5">
                  <span class="text-slate-500 block mb-0.5">Detected Intent</span>
                  <span class="font-bold text-white">{{ insights.intent }}</span>
                </div>
                <div class="bg-slate-950 border border-slate-800 rounded-lg p-2.5">
                  <span class="text-slate-500 block mb-0.5">Prospect Budget</span>
                  <span class="font-bold text-emerald-400">{{ insights.prospectBudget ? ('$' + (insights.prospectBudget | number:'1.0-0')) : 'Pending' }}</span>
                </div>
                <div class="bg-slate-950 border border-slate-800 rounded-lg p-2.5">
                  <span class="text-slate-500 block mb-0.5">Target Timeframe</span>
                  <span class="font-bold text-cyan-300">{{ insights.timeframe ?? 'Flexible' }}</span>
                </div>
                <div class="bg-slate-950 border border-slate-800 rounded-lg p-2.5">
                  <span class="text-slate-500 block mb-0.5">Sentiment Stance</span>
                  <span class="font-bold" [ngClass]="insights.sentimentScore >= 0.2 ? 'text-emerald-400' : (insights.sentimentScore <= -0.2 ? 'text-rose-400' : 'text-slate-300')">
                    {{ insights.sentimentLabel }} ({{ insights.sentimentScore > 0 ? '+' : '' }}{{ insights.sentimentScore | number:'1.2-2' }})
                  </span>
                </div>
              </div>
              <!-- Action Items -->
              <div class="flex items-center gap-2">
                <span class="text-[11px] text-slate-400 font-medium">Recommended Next Steps:</span>
                <div class="flex flex-wrap gap-1.5">
                  @for (action of insights.actionItems; track action) {
                    <span class="px-2 py-0.5 rounded text-[10px] bg-slate-800 border border-slate-700 text-slate-200">
                      ✓ {{ action }}
                    </span>
                  }
                </div>
              </div>
            </div>
          }
        </section>

        <!-- RIGHT COLUMN: Oscilloscope Meters, Turnaround Latency & Softphone Controller -->
        <aside class="w-96 flex flex-col bg-slate-900/60 p-6 overflow-y-auto space-y-6">
          <!-- 1. Real-time Audio Oscilloscope & dB Meters -->
          <div class="bg-slate-950 border border-slate-800 rounded-xl p-4 shadow-lg">
            <div class="flex items-center justify-between mb-3">
              <h2 class="text-xs font-bold text-slate-200 uppercase tracking-wider flex items-center gap-2">
                <span class="w-2 h-2 rounded-full bg-cyan-400 animate-ping"></span>
                Full-Duplex PCM Audio Stream
              </h2>
              <span class="text-[10px] font-mono text-slate-400">16kHz 16-bit Mono</span>
            </div>

            <!-- Oscilloscope Waveform Graphic -->
            <div class="h-20 bg-slate-900 border border-slate-800/80 rounded-lg p-2 flex items-center justify-center relative overflow-hidden mb-4">
              <div class="absolute inset-0 bg-gradient-to-r from-transparent via-cyan-500/5 to-transparent pointer-events-none"></div>
              <!-- Simulated Waveform Bars -->
              <div class="w-full flex items-center justify-between h-12 gap-1 px-1">
                @for (bar of waveformBars(); track $index) {
                  <div
                    class="w-1.5 rounded-full transition-all duration-75"
                    [style.height.px]="bar.height"
                    [ngClass]="bar.isAgent ? 'bg-emerald-400' : 'bg-cyan-400'">
                  </div>
                }
              </div>
            </div>

            <!-- Input dB Meter (Caller Mic) -->
            <div class="space-y-1 mb-3">
              <div class="flex items-center justify-between text-xs font-mono">
                <span class="text-cyan-400 flex items-center gap-1.5">
                  <svg class="w-3.5 h-3.5" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                    <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M19 11a7 7 0 01-7 7m0 0a7 7 0 01-7-7m7 7v4m0 0H8m4 0h4m-4-8a3 3 0 100-6 3 3 0 000 6z" />
                  </svg>
                  Input Audio Level (Mic)
                </span>
                <span class="text-slate-300 font-bold">{{ activeSession()?.currentInputDb ?? -90 }} dB</span>
              </div>
              <div class="h-2 bg-slate-800 rounded-full overflow-hidden">
                <div
                  class="h-full bg-gradient-to-r from-cyan-600 via-cyan-400 to-emerald-400 transition-all duration-100"
                  [style.width.%]="calcDbPercentage(activeSession()?.currentInputDb ?? -90)">
                </div>
              </div>
            </div>

            <!-- Output dB Meter (AI Agent TTS) -->
            <div class="space-y-1">
              <div class="flex items-center justify-between text-xs font-mono">
                <span class="text-emerald-400 flex items-center gap-1.5">
                  <svg class="w-3.5 h-3.5" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                    <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M15.536 8.464a5 5 0 010 7.072m2.828-9.9a9 9 0 010 12.728M5.586 15H4a1 1 0 01-1-1v-4a1 1 0 011-1h1.586l4.707-4.707C10.923 3.663 12 4.109 12 5v14c0 .891-1.077 1.337-1.707.707L5.586 15z" />
                  </svg>
                  Output Audio Level (Agent)
                </span>
                <span class="text-slate-300 font-bold">{{ activeSession()?.currentOutputDb ?? -90 }} dB</span>
              </div>
              <div class="h-2 bg-slate-800 rounded-full overflow-hidden">
                <div
                  class="h-full bg-gradient-to-r from-emerald-600 via-emerald-400 to-amber-400 transition-all duration-100"
                  [style.width.%]="calcDbPercentage(activeSession()?.currentOutputDb ?? -90)">
                </div>
              </div>
            </div>
          </div>

          <!-- 2. Turnaround Latency Tracker Card (<600ms target) -->
          <div class="bg-slate-950 border border-slate-800 rounded-xl p-4 shadow-lg space-y-3">
            <div class="flex items-center justify-between">
              <h2 class="text-xs font-bold text-slate-200 uppercase tracking-wider flex items-center gap-1.5">
                <svg class="w-4 h-4 text-emerald-400" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                  <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M12 8v4l3 3m6-3a9 9 0 11-18 0 9 9 0 0118 0z" />
                </svg>
                Turnaround Latency Breakdown
              </h2>
              <span class="px-2 py-0.5 rounded text-[10px] font-mono font-bold bg-emerald-950 text-emerald-400 border border-emerald-800/60">
                SLA: &lt;600ms
              </span>
            </div>

            <!-- Average and Range -->
            <div class="grid grid-cols-3 gap-2 text-center text-xs font-mono">
              <div class="bg-slate-900 border border-slate-800/80 rounded p-2">
                <span class="text-[10px] text-slate-500 block">Average</span>
                <span class="text-sm font-bold text-emerald-400">{{ activeSession()?.averageTurnaroundLatencyMs ?? 412 }}ms</span>
              </div>
              <div class="bg-slate-900 border border-slate-800/80 rounded p-2">
                <span class="text-[10px] text-slate-500 block">Min</span>
                <span class="text-sm font-bold text-cyan-400">{{ activeSession()?.minTurnaroundLatencyMs ?? 380 }}ms</span>
              </div>
              <div class="bg-slate-900 border border-slate-800/80 rounded p-2">
                <span class="text-[10px] text-slate-500 block">Max</span>
                <span class="text-sm font-bold text-slate-300">{{ activeSession()?.maxTurnaroundLatencyMs ?? 485 }}ms</span>
              </div>
            </div>

            <!-- Phase Timing Breakdown -->
            @if (latestTurnaround(); as m) {
              <div class="space-y-1.5 text-xs font-mono pt-1">
                <div class="flex justify-between text-[11px] text-slate-400">
                  <span>VAD Speech Silence Onset</span>
                  <span class="text-slate-200">{{ m.vadLatencyMs }}ms</span>
                </div>
                <div class="flex justify-between text-[11px] text-slate-400">
                  <span>Fast ASR Audio-to-Text</span>
                  <span class="text-slate-200">{{ m.asrLatencyMs }}ms</span>
                </div>
                <div class="flex justify-between text-[11px] text-slate-400">
                  <span>LLM First-Token Generation</span>
                  <span class="text-slate-200">{{ m.llmLatencyMs }}ms</span>
                </div>
                <div class="flex justify-between text-[11px] text-slate-400">
                  <span>Neural TTS Audio Chunk Synthesis</span>
                  <span class="text-slate-200">{{ m.ttsLatencyMs }}ms</span>
                </div>
                <div class="h-1.5 w-full bg-slate-800 rounded-full flex overflow-hidden mt-1">
                  <div class="bg-blue-500" [style.width.%]="(m.vadLatencyMs / m.totalTurnaroundMs) * 100"></div>
                  <div class="bg-cyan-500" [style.width.%]="(m.asrLatencyMs / m.totalTurnaroundMs) * 100"></div>
                  <div class="bg-indigo-500" [style.width.%]="(m.llmLatencyMs / m.totalTurnaroundMs) * 100"></div>
                  <div class="bg-emerald-500" [style.width.%]="(m.ttsLatencyMs / m.totalTurnaroundMs) * 100"></div>
                </div>
              </div>
            }
          </div>

          <!-- 3. Softphone & Telephony Controller -->
          <div class="bg-slate-950 border border-slate-800 rounded-xl p-4 shadow-lg space-y-3">
            <h2 class="text-xs font-bold text-slate-200 uppercase tracking-wider flex items-center gap-1.5">
              <svg class="w-4 h-4 text-cyan-400" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M3 5a2 2 0 012-2h3.28a1 1 0 01.948.684l1.498 4.493a1 1 0 01-.502 1.21l-2.257 1.13a11.042 11.042 0 005.516 5.516l1.13-2.257a1 1 0 011.21-.502l4.493 1.498a1 1 0 01.684.949V19a2 2 0 01-2 2h-1C9.716 21 3 14.284 3 6V5z" />
              </svg>
              Telephony Softphone Controls
            </h2>

            <div class="grid grid-cols-2 gap-2 text-xs">
              <!-- Mute / Unmute Button -->
              <button
                (click)="toggleMute()"
                class="px-3 py-2.5 rounded-lg border font-semibold flex items-center justify-center gap-1.5 transition"
                [ngClass]="isMuted() ? 'bg-amber-950/60 border-amber-600/70 text-amber-300' : 'bg-slate-900 border-slate-700 text-slate-200 hover:bg-slate-800'">
                <svg class="w-4 h-4" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                  <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M19 11a7 7 0 01-7 7m0 0a7 7 0 01-7-7m7 7v4m0 0H8m4 0h4m-4-8a3 3 0 100-6 3 3 0 000 6z" />
                </svg>
                {{ isMuted() ? 'Muted' : 'Mute Mic' }}
              </button>

              <!-- Interrupt (Barge-In) Button -->
              <button
                (click)="triggerManualBargeIn()"
                class="px-3 py-2.5 rounded-lg bg-amber-600 hover:bg-amber-500 text-slate-950 font-bold flex items-center justify-center gap-1.5 transition shadow">
                <svg class="w-4 h-4" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                  <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M13 10V3L4 14h7v7l9-11h-7z" />
                </svg>
                Barge-In / Flush
              </button>

              <!-- Transfer Call Button -->
              <button
                (click)="showTransferModal = true"
                class="px-3 py-2.5 rounded-lg bg-indigo-600 hover:bg-indigo-500 text-white font-semibold flex items-center justify-center gap-1.5 transition shadow">
                <svg class="w-4 h-4" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                  <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M8 7h12m0 0l-4-4m4 4l-4 4m0 6H4m0 0l4 4m-4-4l4-4" />
                </svg>
                Transfer Human
              </button>

              <!-- End Call / Hangup Button -->
              <button
                (click)="hangupCall()"
                class="px-3 py-2.5 rounded-lg bg-rose-600 hover:bg-rose-500 text-white font-semibold flex items-center justify-center gap-1.5 transition shadow">
                <svg class="w-4 h-4" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                  <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M16 8l2-2m0 0l2-2m-2 2l-2-2m2 2l2 2M5 3a2 2 0 00-2 2v1c0 8.284 6.716 15 15 15h1a2 2 0 002-2v-3.28a1 1 0 00-.684-.948l-4.493-1.498a1 1 0 00-1.21.502l-1.13 2.257a11.042 11.042 0 01-5.516-5.516l2.257-1.13a1 1 0 00.502-1.21L8.228 3.684A1 1 0 007.28 3H5z" />
                </svg>
                End Call
              </button>
            </div>
          </div>

          <!-- 4. Interactive Simulation & Quick Ingestion Triggers -->
          <div class="bg-slate-950 border border-slate-800 rounded-xl p-4 shadow-lg space-y-3">
            <div class="flex items-center justify-between">
              <h2 class="text-xs font-bold text-slate-200 uppercase tracking-wider flex items-center gap-1.5">
                <svg class="w-4 h-4 text-cyan-400" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                  <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M14.752 11.168l-3.197-2.132A1 1 0 0010 9.87v4.263a1 1 0 001.555.832l3.197-2.132a1 1 0 000-1.664z" />
                  <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M21 12a9 9 0 11-18 0 9 9 0 0118 0z" />
                </svg>
                Simulation &amp; Testing Console
              </h2>
              <button
                (click)="toggleContinuousStreaming()"
                class="px-2 py-0.5 rounded text-[10px] font-mono font-bold transition"
                [ngClass]="isSimulatingStream() ? 'bg-cyan-600 text-white' : 'bg-slate-800 text-slate-400 border border-slate-700'">
                {{ isSimulatingStream() ? 'Streaming (20ms) ACTIVE' : 'Start 20ms Stream' }}
              </button>
            </div>

            <p class="text-[11px] text-slate-400">Inject synthetic caller speech chunks to test neural VAD onset (&lt;100ms) and automatic turnaround synthesis (&lt;600ms):</p>

            <div class="space-y-1.5 text-xs">
              <button
                (click)="simulateSpeechChunk('Hi Sophia, we have an urgent requirement to support 80 concurrent IVR lines for $60k budget.')"
                class="w-full text-left px-3 py-2 rounded bg-slate-900 hover:bg-slate-800 border border-slate-800 text-slate-200 transition text-[11px]">
                💬 Inquire about 80 IVR lines ($60,000 budget)
              </button>
              <button
                (click)="simulateSpeechChunk('Wait, let me interrupt you right there, what about WebRTC SIP latency?')"
                class="w-full text-left px-3 py-2 rounded bg-slate-900 hover:bg-slate-800 border border-slate-800 text-slate-200 transition text-[11px]">
                ⚡ Barge-In Interruption ("Wait, let me interrupt...")
              </button>
              <button
                (click)="simulateSpeechChunk('Can you connect me with your principal cloud systems architect?')"
                class="w-full text-left px-3 py-2 rounded bg-slate-900 hover:bg-slate-800 border border-slate-800 text-slate-200 transition text-[11px]">
                📞 Request Technical Escalation Desk
              </button>
            </div>
          </div>
        </aside>
      </div>

      <!-- MODAL: Initiate New Session -->
      @if (initiateNewCallModal) {
        <div class="fixed inset-0 bg-slate-950/80 backdrop-blur-sm z-50 flex items-center justify-center p-4">
          <div class="bg-slate-900 border border-slate-800 rounded-2xl w-full max-w-md p-6 shadow-2xl space-y-4">
            <h2 class="text-base font-bold text-white flex items-center gap-2">
              <span>🎙️</span> Initiate Full-Duplex Voice Session
            </h2>
            <div class="space-y-3 text-xs">
              <div>
                <label class="block text-slate-400 mb-1">Caller Phone Number</label>
                <input
                  type="text"
                  [(ngModel)]="newCallCallerNumber"
                  class="w-full bg-slate-950 border border-slate-700 rounded-lg p-2.5 text-slate-100 focus:outline-none focus:border-cyan-500" />
              </div>
              <div>
                <label class="block text-slate-400 mb-1">Agent Voice Persona Profile</label>
                <select
                  [(ngModel)]="newCallProfileId"
                  class="w-full bg-slate-950 border border-slate-700 rounded-lg p-2.5 text-slate-100 focus:outline-none focus:border-cyan-500">
                  @for (p of profiles(); track p.id) {
                    <option [value]="p.id">{{ p.name }} ({{ p.voiceModel }})</option>
                  }
                </select>
              </div>
              <div>
                <label class="block text-slate-400 mb-1">Initial Context Brief</label>
                <input
                  type="text"
                  [(ngModel)]="newCallContextPrompt"
                  placeholder="e.g. Inbound Enterprise Discovery"
                  class="w-full bg-slate-950 border border-slate-700 rounded-lg p-2.5 text-slate-100 focus:outline-none focus:border-cyan-500" />
              </div>
            </div>
            <div class="flex items-center justify-end gap-3 pt-2">
              <button
                (click)="initiateNewCallModal = false"
                class="px-4 py-2 rounded-lg bg-slate-800 text-slate-300 text-xs font-semibold hover:bg-slate-700">
                Cancel
              </button>
              <button
                (click)="initiateSession()"
                class="px-4 py-2 rounded-lg bg-cyan-600 text-white text-xs font-semibold hover:bg-cyan-500 shadow">
                Start Session
              </button>
            </div>
          </div>
        </div>
      }

      <!-- MODAL: Warm Call Transfer -->
      @if (showTransferModal) {
        <div class="fixed inset-0 bg-slate-950/80 backdrop-blur-sm z-50 flex items-center justify-center p-4">
          <div class="bg-slate-900 border border-slate-800 rounded-2xl w-full max-w-md p-6 shadow-2xl space-y-4">
            <h2 class="text-base font-bold text-white flex items-center gap-2">
              <span>🔄</span> Warm SIP / WebRTC Call Transfer
            </h2>
            <div class="space-y-3 text-xs">
              <div>
                <label class="block text-slate-400 mb-1">Escalation Destination Desk</label>
                <select
                  [(ngModel)]="transferTargetDesk"
                  class="w-full bg-slate-950 border border-slate-700 rounded-lg p-2.5 text-slate-100 focus:outline-none focus:border-indigo-500">
                  <option value="Tier-1-Enterprise-Sales-Desk">Tier 1 Enterprise Sales Desk</option>
                  <option value="Lead-Systems-Architect-Desk">Lead Cloud Systems Architect Desk</option>
                  <option value="Customer-Success-Executive-Desk">Customer Success Executive Desk</option>
                  <option value="Finance-Contracts-Queue">Finance &amp; Contracts Queue</option>
                </select>
              </div>
              <div>
                <label class="block text-slate-400 mb-1">Transfer Reason</label>
                <input
                  type="text"
                  [(ngModel)]="transferReason"
                  class="w-full bg-slate-950 border border-slate-700 rounded-lg p-2.5 text-slate-100 focus:outline-none focus:border-indigo-500" />
              </div>
            </div>
            <div class="flex items-center justify-end gap-3 pt-2">
              <button
                (click)="showTransferModal = false"
                class="px-4 py-2 rounded-lg bg-slate-800 text-slate-300 text-xs font-semibold hover:bg-slate-700">
                Cancel
              </button>
              <button
                (click)="executeTransfer()"
                class="px-4 py-2 rounded-lg bg-indigo-600 text-white text-xs font-semibold hover:bg-indigo-500 shadow">
                Execute Warm Transfer
              </button>
            </div>
          </div>
        </div>
      }
    </div>
  `
})
export class VoiceStreamingCockpitComponent implements OnInit, OnDestroy {
  private http = inject(HttpClient);

  // Signals
  sessions = signal<StreamingSessionMetrics[]>([]);
  selectedSessionId = signal<string>('');
  profiles = signal<VoiceAgentProfile[]>([]);
  isMuted = signal<boolean>(false);
  isSimulatingStream = signal<boolean>(false);

  // Modals state
  initiateNewCallModal = false;
  showTransferModal = false;
  newCallCallerNumber = '+1 (415) 890-2341';
  newCallProfileId = 'sophia-enterprise';
  newCallContextPrompt = 'Inbound Enterprise Discovery Call';
  transferTargetDesk = 'Tier-1-Enterprise-Sales-Desk';
  transferReason = 'Prospect requested senior executive discussion';

  // Oscilloscope waveform bars
  waveformBars = signal<{ height: number; isAgent: boolean }[]>(
    Array.from({ length: 36 }, (_, i) => ({
      height: 6 + Math.floor(Math.sin(i * 0.4) * 8 + 8),
      isAgent: i % 2 === 0
    }))
  );

  // Active Session Computed
  activeSession = computed(() => {
    const list = this.sessions();
    const id = this.selectedSessionId();
    return list.find(s => s.sessionId === id) || (list.length > 0 ? list[0] : null);
  });

  latestTurnaround = computed(() => {
    const s = this.activeSession();
    if (!s || !s.recentTurnLatencies || s.recentTurnLatencies.length === 0) {
      return null;
    }
    return s.recentTurnLatencies[s.recentTurnLatencies.length - 1];
  });

  private streamInterval: any = null;
  private animInterval: any = null;
  private seqCounter = 100;

  ngOnInit() {
    this.loadProfiles();
    this.loadSessions();

    // Start oscilloscope waveform jitter animation
    this.animInterval = setInterval(() => {
      const isAgent = this.activeSession()?.pipelineState === 'AgentSpeaking';
      const isUser = this.activeSession()?.pipelineState === 'UserSpeaking';
      this.waveformBars.set(
        Array.from({ length: 36 }, (_, i) => {
          let h = 6;
          if (isAgent) {
            h = 10 + Math.floor(Math.random() * 32);
          } else if (isUser) {
            h = 12 + Math.floor(Math.random() * 30);
          } else {
            h = 6 + Math.floor(Math.random() * 4);
          }
          return { height: h, isAgent };
        })
      );
    }, 120);
  }

  ngOnDestroy() {
    if (this.streamInterval) {
      clearInterval(this.streamInterval);
    }
    if (this.animInterval) {
      clearInterval(this.animInterval);
    }
  }

  loadProfiles() {
    this.http.get<VoiceAgentProfile[]>('/api/v1/voice/streaming/profiles').subscribe({
      next: res => this.profiles.set(res),
      error: () => {
        // Fallback demo profiles if backend not running in test harness
        this.profiles.set([
          {
            id: 'sophia-enterprise',
            name: 'Sophia (Enterprise AE)',
            voiceModel: 'azure-neural-sophia',
            persona: 'Executive B2B AE',
            baseLatencyMs: 145,
            speakingRate: 1.05,
            languageCode: 'en-US',
            bargeInEnabled: true
          },
          {
            id: 'alex-technical',
            name: 'Alex (Solutions Architect)',
            voiceModel: 'openai-realtime-tts',
            persona: 'Cloud Systems Specialist',
            baseLatencyMs: 130,
            speakingRate: 1.0,
            languageCode: 'en-US',
            bargeInEnabled: true
          }
        ]);
      }
    });
  }

  loadSessions() {
    this.http.get<StreamingSessionMetrics[]>('/api/v1/voice/streaming/sessions').subscribe({
      next: res => {
        this.sessions.set(res);
        if (res.length > 0 && !this.selectedSessionId()) {
          this.selectedSessionId.set(res[0].sessionId);
        }
      },
      error: () => {
        // Fallback demo state
        const fallbackSession: StreamingSessionMetrics = {
          sessionId: '00000000-0000-0000-0000-000000000050',
          tenantId: '00000000-0000-0000-0000-000000000001',
          callSid: 'CA_STREAM_ENTERPRISE_50',
          lifecycle: 'ActiveAudio',
          pipelineState: 'AgentSpeaking',
          voiceProfile: {
            id: 'sophia-enterprise',
            name: 'Sophia (Enterprise AE)',
            voiceModel: 'azure-neural-sophia',
            persona: 'Executive B2B AE',
            baseLatencyMs: 145,
            speakingRate: 1.05,
            languageCode: 'en-US',
            bargeInEnabled: true
          },
          callerNumber: '+1 (415) 890-2341',
          calleeNumber: '+1 (800) 555-0199',
          direction: 'Inbound',
          currentInputDb: -42.0,
          currentOutputDb: -16.5,
          totalFramesReceived: 1240,
          totalBargeInCount: 1,
          totalBufferFlushes: 1,
          averageTurnaroundLatencyMs: 412.0,
          minTurnaroundLatencyMs: 388.0,
          maxTurnaroundLatencyMs: 435.0,
          sub600msCompliance: true,
          recentTurnLatencies: [
            {
              turnId: '1',
              userSpeechEndedUtc: new Date().toISOString(),
              vadSilenceDetectedUtc: new Date().toISOString(),
              transcriptionCompletedUtc: new Date().toISOString(),
              llmThoughtGeneratedUtc: new Date().toISOString(),
              ttsFirstChunkEmittedUtc: new Date().toISOString(),
              vadLatencyMs: 40,
              asrLatencyMs: 98,
              llmLatencyMs: 192,
              ttsLatencyMs: 82,
              totalTurnaroundMs: 412,
              isSub600Ms: true,
              latencyGrade: 'Target (<600ms)'
            }
          ],
          transcript: [
            {
              id: '1',
              sessionId: '00000000-0000-0000-0000-000000000050',
              speaker: 'User',
              text: 'Hi Sophia, we have an urgent requirement to support 80 concurrent IVR lines for $60,000 budget.',
              timestampUtc: new Date().toISOString(),
              confidence: 0.98,
              isInterrupted: false,
              sentimentScore: 0.75
            },
            {
              id: '2',
              sessionId: '00000000-0000-0000-0000-000000000050',
              speaker: 'Agent',
              text: 'Hello! Our full-duplex PCM bridge directly cuts turnaround latency to under 450 milliseconds while eliminating conversational overlap.',
              timestampUtc: new Date().toISOString(),
              confidence: 0.99,
              isInterrupted: false,
              sentimentScore: 0.85
            }
          ],
          extractedInsights: {
            intent: 'PricingEvaluation',
            confidence: 0.95,
            prospectBudget: 60000,
            timeframe: 'Immediate',
            sentimentScore: 0.8,
            sentimentLabel: 'Positive',
            crmDealValue: 69000,
            entities: {
              PlatformType: 'Full-Duplex Voice AI',
              IVRProtocol: 'SIP/WebRTC PCM Linear 16kHz',
              TargetLatency: '<600ms Turnaround'
            },
            actionItems: [
              'Generate custom commercial proposal for $60,000',
              'Set target deployment milestone for Immediate',
              'Send softphone WebRTC SDK documentation to technical team'
            ]
          },
          startedAtUtc: new Date().toISOString()
        };
        this.sessions.set([fallbackSession]);
        this.selectedSessionId.set(fallbackSession.sessionId);
      }
    });
  }

  onSessionSelected(sessionId: string) {
    this.selectedSessionId.set(sessionId);
    this.refreshCurrentSessionMetrics();
  }

  refreshCurrentSessionMetrics() {
    const id = this.selectedSessionId();
    if (!id) return;
    this.http.get<StreamingSessionMetrics>(`/api/v1/voice/streaming/session/${id}/metrics`).subscribe({
      next: metrics => {
        this.sessions.update(list => list.map(s => s.sessionId === id ? metrics : s));
      }
    });
  }

  calcDbPercentage(db: number): number {
    // Maps -90dB to 0% and 0dB to 100%
    const clamped = Math.max(-90, Math.min(0, db));
    return Math.round(((clamped + 90) / 90) * 100);
  }

  getPipelineStateBadgeClass(state?: StreamingPipelineState): string {
    switch (state) {
      case 'Listening':
        return 'bg-blue-950/60 border-blue-500/40 text-blue-300';
      case 'UserSpeaking':
        return 'bg-cyan-950/60 border-cyan-500/40 text-cyan-300';
      case 'Transcribing':
      case 'Thinking':
        return 'bg-indigo-950/60 border-indigo-500/40 text-indigo-300';
      case 'AgentSpeaking':
        return 'bg-emerald-950/60 border-emerald-500/40 text-emerald-300';
      case 'BargeInFlush':
        return 'bg-amber-950/60 border-amber-500/40 text-amber-300';
      default:
        return 'bg-slate-900 border-slate-700 text-slate-400';
    }
  }

  getPipelineStateDotClass(state?: StreamingPipelineState): string {
    switch (state) {
      case 'Listening':
        return 'bg-blue-400 animate-pulse';
      case 'UserSpeaking':
        return 'bg-cyan-400 animate-pulse';
      case 'AgentSpeaking':
        return 'bg-emerald-400 animate-ping';
      case 'BargeInFlush':
        return 'bg-amber-400 animate-bounce';
      default:
        return 'bg-slate-500';
    }
  }

  toggleMute() {
    this.isMuted.update(m => !m);
  }

  triggerManualBargeIn() {
    const id = this.selectedSessionId();
    if (!id) return;
    this.http.post(`/api/v1/voice/streaming/session/${id}/barge-in`, {
      reason: 'ManualOperatorBargeIn',
      interruptionOffsetMs: 50.0
    }).subscribe({
      next: () => this.refreshCurrentSessionMetrics()
    });
  }

  executeTransfer() {
    const id = this.selectedSessionId();
    if (!id) return;
    this.http.post(`/api/v1/voice/streaming/session/${id}/transfer`, {
      targetDesk: this.transferTargetDesk,
      reason: this.transferReason
    }).subscribe({
      next: () => {
        this.showTransferModal = false;
        this.refreshCurrentSessionMetrics();
      }
    });
  }

  hangupCall() {
    const id = this.selectedSessionId();
    if (!id) return;
    this.http.post(`/api/v1/voice/streaming/session/${id}/hangup`, {
      reason: 'UserHangup'
    }).subscribe({
      next: () => this.refreshCurrentSessionMetrics()
    });
  }

  initiateSession() {
    this.http.post<StreamingSessionMetrics>('/api/v1/voice/streaming/session/initiate', {
      tenantId: '00000000-0000-0000-0000-000000000001',
      callerNumber: this.newCallCallerNumber,
      voiceProfileId: this.newCallProfileId,
      initialContextPrompt: this.newCallContextPrompt,
      direction: 1
    }).subscribe({
      next: newSession => {
        this.sessions.update(list => [newSession, ...list]);
        this.selectedSessionId.set(newSession.sessionId);
        this.initiateNewCallModal = false;
      }
    });
  }

  simulateSpeechChunk(textChunk: string) {
    const id = this.selectedSessionId();
    if (!id) return;

    this.seqCounter++;
    // Frame 1: high speech energy onset
    this.http.post(`/api/v1/voice/streaming/session/${id}/audio-frame`, {
      sequenceNumber: this.seqCounter++,
      simulatedRmsEnergy: 1800.0,
      simulatedTextChunk: textChunk
    }).subscribe(() => {
      // Frame 2: speech onset detected
      this.http.post(`/api/v1/voice/streaming/session/${id}/audio-frame`, {
        sequenceNumber: this.seqCounter++,
        simulatedRmsEnergy: 1950.0,
        simulatedTextChunk: textChunk
      }).subscribe(() => {
        // Frame 3: silence frame
        setTimeout(() => {
          this.http.post(`/api/v1/voice/streaming/session/${id}/audio-frame`, {
            sequenceNumber: this.seqCounter++,
            simulatedRmsEnergy: 80.0
          }).subscribe(() => {
            // Frame 4: second silence frame triggers turnaround
            setTimeout(() => {
              this.http.post(`/api/v1/voice/streaming/session/${id}/audio-frame`, {
                sequenceNumber: this.seqCounter++,
                simulatedRmsEnergy: 50.0
              }).subscribe(() => {
                this.refreshCurrentSessionMetrics();
              });
            }, 50);
          });
        }, 50);
      });
    });
  }

  toggleContinuousStreaming() {
    if (this.isSimulatingStream()) {
      if (this.streamInterval) {
        clearInterval(this.streamInterval);
        this.streamInterval = null;
      }
      this.isSimulatingStream.set(false);
    } else {
      this.isSimulatingStream.set(true);
      const id = this.selectedSessionId();
      this.streamInterval = setInterval(() => {
        if (!id) return;
        const isSpeaking = Math.random() > 0.7;
        const energy = isSpeaking ? (800 + Math.random() * 1500) : (40 + Math.random() * 80);
        this.http.post(`/api/v1/voice/streaming/session/${id}/audio-frame`, {
          sequenceNumber: this.seqCounter++,
          simulatedRmsEnergy: energy
        }).subscribe({
          next: () => this.refreshCurrentSessionMetrics()
        });
      }, 500);
    }
  }
}
