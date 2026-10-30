import { Component, OnInit, signal, computed, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { HttpClient } from '@angular/common/http';

export type SpeakerType = 'Agent' | 'Caller';
export type SentimentTrajectory = 'Improving' | 'Stable' | 'Deteriorating';
export type ChurnAlertSeverity = 'None' | 'Low' | 'Medium' | 'High' | 'Critical';

export interface WordConfidence {
  word: string;
  confidence: number;
  startMs: number;
  endMs: number;
}

export interface TranscribedUtterance {
  id: string;
  callId: string;
  speaker: SpeakerType;
  text: string;
  startOffsetMs: number;
  endOffsetMs: number;
  durationMs: number;
  confidenceScore: number;
  sentimentScore: number;
  sentimentVelocity: number;
  timestamp: string;
  words: WordConfidence[];
  triggeredFrustrationAlert?: boolean;
}

export interface FrustrationAlertInfo {
  isActive: boolean;
  severity: ChurnAlertSeverity;
  triggerReason: string;
  triggerKeywords: string[];
  empathyGuidanceTalkingPoints: string[];
  triggeredAt?: string;
}

export interface PostCallStructuredSummary {
  callId: string;
  generatedAt: string;
  keyDiscussionPoints: string[];
  objectionsRaised: string[];
  customerSentimentTrajectory: SentimentTrajectory;
  overallSentimentScore: number;
  agreedActionItems: string[];
  recommendedNextStep: string;
  crmDealStage: string;
  associatedDealId?: string;
  executiveBrief: string;
}

export interface DiarizedVoiceCall {
  callId: string;
  tenantId: string;
  callerName: string;
  callerPhoneNumber: string;
  agentName: string;
  callTopic: string;
  startedAtUtc: string;
  endedAtUtc?: string;
  totalDurationSeconds: number;
  status: 'Live' | 'Completed';
  overallSentimentScore: number;
  sentimentTrajectory: SentimentTrajectory;
  frustrationAlert: FrustrationAlertInfo;
  utterances: TranscribedUtterance[];
  summary?: PostCallStructuredSummary;
  associatedDealId?: string;
}

@Component({
  selector: 'app-voice-transcription-studio',
  standalone: true,
  imports: [CommonModule, FormsModule],
  template: `
    <div dir="ltr" class="flex flex-col h-screen bg-slate-950 text-slate-100 font-sans select-none overflow-hidden">
      <!-- Top Application Header -->
      <header class="h-16 border-b border-slate-800 bg-slate-900/80 backdrop-blur px-6 flex items-center justify-between shrink-0">
        <div class="flex items-center gap-3">
          <div class="w-10 h-10 rounded-xl bg-gradient-to-tr from-cyan-600 to-blue-600 flex items-center justify-center font-bold text-white shadow-lg shadow-cyan-500/20">
            🎙️
          </div>
          <div>
            <div class="flex items-center gap-2">
              <h1 class="text-base font-bold text-white tracking-tight">Real-Time Voice Transcription & Whisper Diarization</h1>
              <span class="text-[10px] uppercase font-semibold px-2 py-0.5 rounded bg-blue-500/20 text-blue-400 border border-blue-500/30">
                Horizon 39
              </span>
            </div>
            <p class="text-xs text-slate-400">Acoustic dual-speaker segmentation, sentiment velocity dS/dt, and automated churn deflection</p>
          </div>
        </div>

        <div class="flex items-center gap-4">
          <!-- Live Telemetry Status -->
          <div class="flex items-center gap-2 px-3 py-1.5 rounded-lg bg-slate-800/80 border border-slate-700/60 text-xs">
            <span class="w-2.5 h-2.5 rounded-full bg-emerald-400 animate-pulse"></span>
            <span class="text-slate-300 font-medium">Whisper Ingest Pipeline Active</span>
            <span class="text-slate-500 font-mono text-[11px]">44.1kHz • 16-bit</span>
          </div>

          <!-- Quick Refresh Button -->
          <button
            (click)="loadCalls()"
            [disabled]="isLoading()"
            class="px-3 py-1.5 rounded-lg bg-slate-800 hover:bg-slate-700 text-xs font-medium text-slate-200 border border-slate-700 transition flex items-center gap-1.5">
            <span [class.animate-spin]="isLoading()">🔄</span>
            <span>Refresh</span>
          </button>
        </div>
      </header>

      <!-- Subheader / Call Selector Ribbon -->
      <section class="h-20 border-b border-slate-800/80 bg-slate-900/40 px-6 py-2.5 flex items-center gap-3 overflow-x-auto shrink-0">
        <div class="text-[11px] font-semibold uppercase text-slate-500 tracking-wider pr-2 shrink-0 border-r border-slate-800">
          Enterprise Calls ({{ calls().length }})
        </div>

        @for (call of calls(); track call.callId) {
          <div
            (click)="selectCall(call.callId)"
            [class.ring-2]="selectedCallId() === call.callId"
            [class.ring-blue-500]="selectedCallId() === call.callId"
            [class.bg-slate-800]="selectedCallId() === call.callId"
            [class.bg-slate-900]="selectedCallId() !== call.callId"
            class="min-w-[260px] max-w-[280px] p-2 rounded-xl border border-slate-800 hover:border-slate-700 cursor-pointer transition shadow-sm flex flex-col justify-between shrink-0">
            <div class="flex items-center justify-between gap-1">
              <span class="text-xs font-bold text-white truncate">{{ call.callerName }}</span>
              <span
                [class.bg-emerald-500]="call.sentimentTrajectory === 'Improving'"
                [class.bg-blue-500]="call.sentimentTrajectory === 'Stable'"
                [class.bg-rose-500]="call.sentimentTrajectory === 'Deteriorating'"
                class="w-2 h-2 rounded-full"></span>
            </div>
            
            <div class="text-[11px] text-slate-400 truncate">{{ call.callTopic }}</div>

            <div class="flex items-center justify-between text-[10px] text-slate-400 mt-1">
              <span class="font-mono text-slate-300">{{ formatDuration(call.totalDurationSeconds) }}</span>
              <div class="flex items-center gap-1">
                @if (call.frustrationAlert && call.frustrationAlert.isActive) {
                  <span class="px-1.5 py-0.2 rounded bg-rose-500/20 text-rose-400 font-bold border border-rose-500/40 text-[9px] animate-pulse">
                    CHURN ALERT
                  </span>
                }
                <span
                  [class.text-emerald-400]="call.overallSentimentScore > 0.2"
                  [class.text-rose-400]="call.overallSentimentScore < -0.2"
                  [class.text-amber-400]="call.overallSentimentScore >= -0.2 && call.overallSentimentScore <= 0.2"
                  class="font-mono font-semibold">
                  {{ call.overallSentimentScore > 0 ? '+' : '' }}{{ call.overallSentimentScore.toFixed(2) }}
                </span>
              </div>
            </div>
          </div>
        }
      </section>

      <!-- Main Workspace (Dual Split-View) -->
      <div class="flex-1 flex overflow-hidden">
        
        <!-- Left Panel: Dual-Speaker Diarized Transcript Stream (60% width) -->
        <main class="flex-1 flex flex-col border-r border-slate-800 overflow-hidden bg-slate-950">
          
          <!-- Stream Control Bar -->
          <div class="h-12 border-b border-slate-800/80 bg-slate-900/50 px-6 flex items-center justify-between shrink-0">
            <div class="flex items-center gap-3">
              <span class="text-xs font-semibold text-slate-200">Acoustic Diarization Feed</span>
              @if (currentCall(); as call) {
                <span class="text-xs text-slate-400 font-mono">
                  Call: <strong class="text-white">{{ call.callId }}</strong> • Agent: <span class="text-blue-400">{{ call.agentName }}</span>
                </span>
              }
            </div>

            <div class="flex items-center gap-2">
              <span class="text-[11px] text-slate-400 flex items-center gap-1">
                <span class="w-2 h-2 rounded-full bg-blue-400"></span> Agent (Left)
              </span>
              <span class="text-[11px] text-slate-400 flex items-center gap-1 ml-2">
                <span class="w-2 h-2 rounded-full bg-purple-400"></span> Caller (Right)
              </span>
            </div>
          </div>

          <!-- Transcript Scroll Container -->
          <div class="flex-1 overflow-y-auto p-6 space-y-4">
            @if (currentCall(); as call) {
              @if (call.utterances.length === 0) {
                <div class="h-64 flex flex-col items-center justify-center text-slate-500">
                  <div class="text-4xl mb-2">💬</div>
                  <p class="text-sm">No speech utterances ingested yet for this call session.</p>
                  <p class="text-xs text-slate-600 mt-1">Use the simulator below to inject live speech frames.</p>
                </div>
              }

              @for (utterance of call.utterances; track utterance.id) {
                <div
                  [class.justify-start]="utterance.speaker === 'Agent'"
                  [class.justify-end]="utterance.speaker === 'Caller'"
                  class="flex w-full">
                  
                  <div
                    [class.border-blue-500/40]="utterance.speaker === 'Agent'"
                    [class.bg-slate-900/90]="utterance.speaker === 'Agent'"
                    [class.border-purple-500/40]="utterance.speaker === 'Caller' && !utterance.triggeredFrustrationAlert"
                    [class.bg-purple-950/20]="utterance.speaker === 'Caller' && !utterance.triggeredFrustrationAlert"
                    [class.border-rose-500/70]="utterance.triggeredFrustrationAlert"
                    [class.bg-rose-950/30]="utterance.triggeredFrustrationAlert"
                    class="max-w-[85%] rounded-2xl border p-4 shadow-lg space-y-2 transition duration-200">
                    
                    <!-- Utterance Header -->
                    <div class="flex items-center justify-between gap-4 text-[11px]">
                      <div class="flex items-center gap-2">
                        @if (utterance.speaker === 'Agent') {
                          <span class="w-5 h-5 rounded-full bg-blue-600 text-white flex items-center justify-center font-bold text-[10px]">A</span>
                          <span class="font-semibold text-blue-300">{{ call.agentName }}</span>
                        } @else {
                          <span class="w-5 h-5 rounded-full bg-purple-600 text-white flex items-center justify-center font-bold text-[10px]">C</span>
                          <span class="font-semibold text-purple-300">{{ call.callerName }}</span>
                        }
                        
                        <span class="text-slate-500 font-mono">
                          {{ formatMs(utterance.startOffsetMs) }} - {{ formatMs(utterance.endOffsetMs) }}
                        </span>
                      </div>

                      <!-- Confidence & Frustration Pill -->
                      <div class="flex items-center gap-1.5">
                        @if (utterance.triggeredFrustrationAlert) {
                          <span class="px-1.5 py-0.5 rounded bg-rose-500/30 text-rose-300 font-bold text-[10px] border border-rose-500/50 flex items-center gap-1">
                            ⚠️ DEFLECTION TRIGGER
                          </span>
                        }
                        <span class="text-[10px] font-mono text-slate-400 bg-slate-800/80 px-2 py-0.5 rounded border border-slate-700/50">
                          {{ (utterance.confidenceScore * 100).toFixed(0) }}% conf
                        </span>
                      </div>
                    </div>

                    <!-- Utterance Transcript Text -->
                    <p class="text-sm text-slate-100 leading-relaxed font-normal">
                      {{ utterance.text }}
                    </p>

                    <!-- Word-level confidence chips (Collapsible / inline) -->
                    @if (utterance.words && utterance.words.length > 0) {
                      <div class="pt-1 flex flex-wrap gap-1">
                        @for (w of utterance.words; track w.word + w.startMs) {
                          <span
                            [title]="'Confidence: ' + (w.confidence * 100).toFixed(1) + '% [' + w.startMs + '-' + w.endMs + 'ms]'"
                            [class.text-emerald-300]="w.confidence >= 0.90"
                            [class.text-amber-300]="w.confidence >= 0.75 && w.confidence < 0.90"
                            [class.text-rose-300]="w.confidence < 0.75"
                            class="text-[10px] bg-slate-950/70 border border-slate-800 px-1.5 py-0.2 rounded font-mono cursor-help">
                            {{ w.word }}
                          </span>
                        }
                      </div>
                    }

                    <!-- Utterance Footer: Sentiment & Velocity -->
                    <div class="pt-2 border-t border-slate-800/60 flex items-center justify-between text-[11px]">
                      <div class="flex items-center gap-2">
                        <span class="text-slate-400">Sentiment:</span>
                        <span
                          [class.text-emerald-400]="utterance.sentimentScore > 0.2"
                          [class.text-rose-400]="utterance.sentimentScore < -0.2"
                          [class.text-amber-400]="utterance.sentimentScore >= -0.2 && utterance.sentimentScore <= 0.2"
                          class="font-mono font-semibold">
                          {{ utterance.sentimentScore > 0 ? '+' : '' }}{{ utterance.sentimentScore.toFixed(2) }}
                        </span>
                      </div>

                      <div class="flex items-center gap-1 text-[10px] font-mono text-slate-400">
                        <span>Velocity (dS/dt):</span>
                        <span
                          [class.text-emerald-400]="utterance.sentimentVelocity > 0.05"
                          [class.text-rose-400]="utterance.sentimentVelocity < -0.05"
                          [class.text-slate-400]="utterance.sentimentVelocity >= -0.05 && utterance.sentimentVelocity <= 0.05"
                          class="font-semibold">
                          {{ utterance.sentimentVelocity > 0 ? '▲ +' : utterance.sentimentVelocity < 0 ? '▼ ' : '▬ ' }}{{ utterance.sentimentVelocity.toFixed(3) }}/s
                        </span>
                      </div>
                    </div>
                  </div>
                </div>
              }
            }
          </div>

          <!-- Bottom Utterance Simulator Bar -->
          <div class="border-t border-slate-800 bg-slate-900/90 p-4 shrink-0 space-y-3">
            <div class="flex items-center justify-between">
              <span class="text-xs font-semibold text-slate-300 flex items-center gap-1.5">
                <span>⚡</span> Simulate Live Inbound Utterance Stream
              </span>

              <!-- Preset Trigger Buttons -->
              <div class="flex items-center gap-2">
                <button
                  (click)="applyPreset('cancel')"
                  class="text-[10px] px-2 py-1 rounded bg-rose-950/80 hover:bg-rose-900 text-rose-300 border border-rose-800 transition">
                  🚨 Cancel Threat
                </button>
                <button
                  (click)="applyPreset('supervisor')"
                  class="text-[10px] px-2 py-1 rounded bg-amber-950/80 hover:bg-amber-900 text-amber-300 border border-amber-800 transition">
                  ⚠️ Supervisor Escalation
                </button>
                <button
                  (click)="applyPreset('happy')"
                  class="text-[10px] px-2 py-1 rounded bg-emerald-950/80 hover:bg-emerald-900 text-emerald-300 border border-emerald-800 transition">
                  ✨ Delighted Renewal
                </button>
              </div>
            </div>

            <div class="flex items-center gap-3">
              <select
                [(ngModel)]="simulatedSpeaker"
                class="bg-slate-950 border border-slate-700 rounded-lg px-2.5 py-1.5 text-xs text-slate-200 focus:outline-none focus:border-blue-500">
                <option value="Caller">Caller</option>
                <option value="Agent">Agent</option>
              </select>

              <input
                type="text"
                [(ngModel)]="simulatedText"
                (keydown.enter)="sendSimulatedUtterance()"
                placeholder="Type spoken sentence or click a preset above..."
                class="flex-1 bg-slate-950 border border-slate-700 rounded-lg px-3 py-1.5 text-xs text-white placeholder-slate-500 focus:outline-none focus:border-blue-500" />

              <div class="flex items-center gap-1.5 text-xs text-slate-400 font-mono">
                <span>Conf:</span>
                <input
                  type="number"
                  step="0.01"
                  min="0.5"
                  max="1.0"
                  [(ngModel)]="simulatedConfidence"
                  class="w-16 bg-slate-950 border border-slate-700 rounded px-1.5 py-1 text-xs text-center text-white" />
              </div>

              <button
                (click)="sendSimulatedUtterance()"
                [disabled]="!simulatedText().trim() || isSubmittingUtterance()"
                class="px-4 py-1.5 rounded-lg bg-blue-600 hover:bg-blue-500 text-white text-xs font-semibold shadow-md transition disabled:opacity-50 flex items-center gap-1.5">
                @if (isSubmittingUtterance()) {
                  <span class="animate-spin">⏳</span>
                } @else {
                  <span>🎙️</span>
                }
                <span>Inject Turn</span>
              </button>
            </div>
          </div>
        </main>

        <!-- Right Panel: Sentiment Velocity, Empathy Deflector & Summary (40% width) -->
        <aside class="w-[480px] flex flex-col overflow-y-auto bg-slate-900/30 p-6 space-y-6 shrink-0">
          
          <!-- Card 1: Real-Time Sentiment Momentum & Trajectory -->
          @if (currentCall(); as call) {
            <div class="bg-slate-900 border border-slate-800 rounded-2xl p-5 shadow-xl space-y-4">
              <div class="flex items-center justify-between">
                <div>
                  <h2 class="text-sm font-bold text-white tracking-tight">Sentiment Momentum Gauge</h2>
                  <p class="text-[11px] text-slate-400">Dynamic utterance trajectory & aggregate polarity</p>
                </div>

                <span
                  [class.bg-emerald-500/20]="call.sentimentTrajectory === 'Improving'"
                  [class.text-emerald-400]="call.sentimentTrajectory === 'Improving'"
                  [class.border-emerald-500/30]="call.sentimentTrajectory === 'Improving'"
                  [class.bg-blue-500/20]="call.sentimentTrajectory === 'Stable'"
                  [class.text-blue-400]="call.sentimentTrajectory === 'Stable'"
                  [class.border-blue-500/30]="call.sentimentTrajectory === 'Stable'"
                  [class.bg-rose-500/20]="call.sentimentTrajectory === 'Deteriorating'"
                  [class.text-rose-400]="call.sentimentTrajectory === 'Deteriorating'"
                  [class.border-rose-500/30]="call.sentimentTrajectory === 'Deteriorating'"
                  class="px-2.5 py-1 rounded-full text-xs font-bold border uppercase tracking-wider">
                  {{ call.sentimentTrajectory }}
                </span>
              </div>

              <!-- Sentiment Gauge Number & Bar -->
              <div class="space-y-2">
                <div class="flex items-baseline justify-between">
                  <span class="text-2xl font-black font-mono text-white">
                    {{ call.overallSentimentScore > 0 ? '+' : '' }}{{ call.overallSentimentScore.toFixed(2) }}
                  </span>
                  <span class="text-xs text-slate-400 font-mono">
                    Scale: [-1.00 .. +1.00]
                  </span>
                </div>

                <!-- Bipolar Sentiment Progress Bar -->
                <div class="h-3 w-full bg-slate-950 rounded-full overflow-hidden flex border border-slate-800">
                  <div
                    class="h-full bg-gradient-to-r from-rose-600 to-amber-500 transition-all duration-300"
                    [style.width.%]="calcNegativeBarPercent(call.overallSentimentScore)"></div>
                  <div
                    class="h-full bg-gradient-to-r from-amber-500 to-emerald-500 transition-all duration-300"
                    [style.width.%]="calcPositiveBarPercent(call.overallSentimentScore)"></div>
                </div>
              </div>

              <!-- Utterance Polarity Sparkline Track -->
              <div class="pt-2 border-t border-slate-800/80 space-y-2">
                <div class="flex items-center justify-between text-[11px] text-slate-400">
                  <span>Turn-by-Turn Sentiment Velocity</span>
                  <span class="font-mono text-slate-500">{{ call.utterances.length }} utterances</span>
                </div>

                <div class="h-12 bg-slate-950 rounded-xl border border-slate-800 p-2 flex items-center gap-1.5 overflow-x-auto">
                  @for (u of call.utterances; track u.id) {
                    <div
                      [title]="u.speaker + ': ' + u.sentimentScore.toFixed(2) + ' (Velocity: ' + u.sentimentVelocity.toFixed(3) + ')'"
                      [style.height.%]="Math.max(25, Math.abs(u.sentimentScore) * 100)"
                      [class.bg-emerald-500]="u.sentimentScore > 0.2"
                      [class.bg-amber-400]="u.sentimentScore >= -0.2 && u.sentimentScore <= 0.2"
                      [class.bg-rose-500]="u.sentimentScore < -0.2"
                      class="w-3 rounded-sm transition-all duration-200 cursor-pointer hover:opacity-80"></div>
                  }
                </div>
              </div>
            </div>

            <!-- Card 2: Frustration & Churn Alert De-escalation Empathy Guidance -->
            @if (call.frustrationAlert && call.frustrationAlert.isActive) {
              <div class="bg-rose-950/40 border-2 border-rose-500/70 rounded-2xl p-5 shadow-2xl space-y-4 animate-pulse">
                <div class="flex items-center justify-between">
                  <div class="flex items-center gap-2">
                    <span class="text-xl">🚨</span>
                    <div>
                      <h3 class="text-sm font-black text-rose-300 uppercase tracking-wider">
                        Active Churn & Frustration Deflector
                      </h3>
                      <p class="text-[11px] text-rose-200/80">Severity: <strong class="text-white">{{ call.frustrationAlert.severity }}</strong></p>
                    </div>
                  </div>
                </div>

                <!-- Trigger Reason & Keywords -->
                <div class="bg-rose-950/60 border border-rose-800/80 rounded-xl p-3 text-xs text-rose-200 space-y-2">
                  <p class="font-medium">{{ call.frustrationAlert.triggerReason }}</p>

                  @if (call.frustrationAlert.triggerKeywords && call.frustrationAlert.triggerKeywords.length > 0) {
                    <div class="flex items-center gap-1.5 flex-wrap">
                      <span class="text-[10px] text-rose-400 font-semibold uppercase">Flagged Keywords:</span>
                      @for (kw of call.frustrationAlert.triggerKeywords; track kw) {
                        <span class="px-2 py-0.5 rounded bg-rose-500/30 text-rose-100 font-mono text-[10px] border border-rose-500/40 font-bold">
                          "{{ kw }}"
                        </span>
                      }
                    </div>
                  }
                </div>

                <!-- Empathy Guidance Talking Points -->
                <div class="space-y-2">
                  <div class="text-[11px] font-bold text-amber-300 uppercase tracking-wider flex items-center gap-1">
                    <span>💡</span> Co-Pilot Real-Time Talking Points (De-escalation)
                  </div>

                  <div class="space-y-2">
                    @for (tp of call.frustrationAlert.empathyGuidanceTalkingPoints; track tp) {
                      <div class="bg-slate-900/90 border border-slate-700/80 rounded-xl p-3 text-xs text-slate-100 flex items-start justify-between gap-3 shadow-md">
                        <p class="italic leading-relaxed">"{{ tp }}"</p>
                        <button
                          (click)="copyTalkingPoint(tp)"
                          title="Copy talking point"
                          class="px-2 py-1 rounded bg-slate-800 hover:bg-slate-700 text-[10px] font-semibold text-slate-300 border border-slate-600 shrink-0">
                          Copy
                        </button>
                      </div>
                    }
                  </div>
                </div>
              </div>
            }

            <!-- Card 3: Post-Call Executive Structured Summary -->
            <div class="bg-slate-900 border border-slate-800 rounded-2xl p-5 shadow-xl space-y-4">
              <div class="flex items-center justify-between">
                <div>
                  <h3 class="text-sm font-bold text-white tracking-tight">Post-Call Structured Deal Summary</h3>
                  <p class="text-[11px] text-slate-400">Synthesized CRM action items and deal trajectory</p>
                </div>

                <button
                  (click)="generateSummary(call.callId)"
                  [disabled]="isGeneratingSummary()"
                  class="px-2.5 py-1 rounded-lg bg-blue-600/30 hover:bg-blue-600 text-blue-300 hover:text-white text-xs font-semibold border border-blue-500/40 transition flex items-center gap-1">
                  @if (isGeneratingSummary()) {
                    <span class="animate-spin">⏳</span>
                  } @else {
                    <span>✨</span>
                  }
                  <span>Synthesize</span>
                </button>
              </div>

              @if (call.summary; as s) {
                <div class="space-y-3 text-xs">
                  <!-- CRM Stage Pill -->
                  <div class="flex items-center justify-between bg-slate-950/80 p-2.5 rounded-xl border border-slate-800">
                    <span class="text-slate-400">CRM Deal Association:</span>
                    <div class="flex items-center gap-2">
                      <span class="font-mono text-blue-400 font-semibold">{{ s.associatedDealId || 'DEAL-AUTONOMOUS' }}</span>
                      <span
                        [class.bg-emerald-500/20]="s.crmDealStage === 'Closed Won'"
                        [class.text-emerald-400]="s.crmDealStage === 'Closed Won'"
                        [class.bg-rose-500/20]="s.crmDealStage === 'At Risk'"
                        [class.text-rose-400]="s.crmDealStage === 'At Risk'"
                        [class.bg-blue-500/20]="s.crmDealStage === 'Negotiation'"
                        [class.text-blue-400]="s.crmDealStage === 'Negotiation'"
                        class="px-2 py-0.5 rounded text-[10px] font-bold border">
                        {{ s.crmDealStage }}
                      </span>
                    </div>
                  </div>

                  <!-- Executive Brief -->
                  <div class="p-3 bg-slate-950/50 rounded-xl border border-slate-800/80 text-slate-300 italic">
                    {{ s.executiveBrief }}
                  </div>

                  <!-- Key Discussion Points -->
                  <div class="space-y-1">
                    <span class="text-[11px] font-semibold text-slate-400 uppercase tracking-wider">Key Discussion Points</span>
                    <ul class="list-disc list-inside space-y-1 text-slate-200">
                      @for (point of s.keyDiscussionPoints; track point) {
                        <li class="leading-relaxed">{{ point }}</li>
                      }
                    </ul>
                  </div>

                  <!-- Objections Raised -->
                  @if (s.objectionsRaised && s.objectionsRaised.length > 0) {
                    <div class="space-y-1">
                      <span class="text-[11px] font-semibold text-rose-400 uppercase tracking-wider">Objections Raised</span>
                      <ul class="list-disc list-inside space-y-1 text-rose-200">
                        @for (obj of s.objectionsRaised; track obj) {
                          <li class="leading-relaxed">{{ obj }}</li>
                        }
                      </ul>
                    </div>
                  }

                  <!-- Agreed Action Items -->
                  <div class="space-y-1">
                    <span class="text-[11px] font-semibold text-emerald-400 uppercase tracking-wider">Agreed Action Items</span>
                    <ul class="space-y-1">
                      @for (item of s.agreedActionItems; track item) {
                        <li class="flex items-start gap-2 text-slate-200">
                          <span class="text-emerald-400 mt-0.5">✓</span>
                          <span>{{ item }}</span>
                        </li>
                      }
                    </ul>
                  </div>

                  <!-- Recommended Next Step -->
                  <div class="bg-blue-950/40 border border-blue-800/80 p-3 rounded-xl space-y-1">
                    <span class="text-[10px] font-bold text-blue-300 uppercase tracking-wider">Recommended Next Step</span>
                    <p class="text-slate-100 font-medium">{{ s.recommendedNextStep }}</p>
                  </div>
                </div>
              } @else {
                <div class="p-4 bg-slate-950/50 rounded-xl border border-slate-800/60 text-center text-xs text-slate-500">
                  Click "Synthesize" above to generate a structured post-call summary.
                </div>
              }
            </div>
          }
        </aside>
      </div>
    </div>
  `
})
export class VoiceTranscriptionStudioComponent implements OnInit {
  private http = inject(HttpClient);

  readonly Math = Math;

  calls = signal<DiarizedVoiceCall[]>([]);
  selectedCallId = signal<string>('CALL-5120-OUT');
  isLoading = signal<boolean>(false);
  isSubmittingUtterance = signal<boolean>(false);
  isGeneratingSummary = signal<boolean>(false);

  simulatedSpeaker = signal<'Agent' | 'Caller'>('Caller');
  simulatedText = signal<string>('');
  simulatedConfidence = signal<number>(0.96);

  currentCall = computed(() => {
    return this.calls().find(c => c.callId === this.selectedCallId()) || null;
  });

  ngOnInit(): void {
    this.loadCalls();
  }

  loadCalls(): void {
    this.isLoading.set(true);
    this.http.get<DiarizedVoiceCall[]>('/api/v1/voice/diarization/calls')
      .subscribe({
        next: (res) => {
          this.isLoading.set(false);
          if (res && res.length > 0) {
            this.calls.set(res);
            // Default select first or existing
            if (!this.calls().some(c => c.callId === this.selectedCallId())) {
              this.selectedCallId.set(res[0].callId);
            }
          }
        },
        error: (err) => {
          this.isLoading.set(false);
          console.warn('API call failed; using fallback demo data.', err);
          this.loadFallbackDemoCalls();
        }
      });
  }

  selectCall(callId: string): void {
    this.selectedCallId.set(callId);
  }

  applyPreset(preset: 'cancel' | 'supervisor' | 'happy'): void {
    this.simulatedSpeaker.set('Caller');
    if (preset === 'cancel') {
      this.simulatedText.set('This latency is completely unacceptable! I want to cancel our enterprise account and get a refund immediately.');
      this.simulatedConfidence.set(0.97);
    } else if (preset === 'supervisor') {
      this.simulatedText.set('Transfer me to your infrastructure supervisor right now. This is a complete disaster for our dispatch fleet.');
      this.simulatedConfidence.set(0.94);
    } else if (preset === 'happy') {
      this.simulatedText.set('Thank you Marcus, the new API patch works fantastic and we are delighted to renew our annual tier.');
      this.simulatedConfidence.set(0.98);
    }
  }

  sendSimulatedUtterance(): void {
    const text = this.simulatedText().trim();
    const callId = this.selectedCallId();
    if (!text || !callId) return;

    this.isSubmittingUtterance.set(true);

    const payload = {
      callId: callId,
      speaker: this.simulatedSpeaker() === 'Agent' ? 0 : 1, // 0 = Agent, 1 = Caller
      text: text,
      confidenceScore: this.simulatedConfidence()
    };

    this.http.post<any>('/api/v1/voice/diarization/stream-utterance', payload)
      .subscribe({
        next: (res) => {
          this.isSubmittingUtterance.set(false);
          this.simulatedText.set('');
          // Refresh calls from backend to get updated sentiment and alerts
          this.loadCalls();
        },
        error: (err) => {
          this.isSubmittingUtterance.set(false);
          console.warn('Stream utterance API call failed; applying locally.', err);
          this.appendLocalUtterance(callId, this.simulatedSpeaker(), text, this.simulatedConfidence());
          this.simulatedText.set('');
        }
      });
  }

  generateSummary(callId: string): void {
    if (!callId) return;
    this.isGeneratingSummary.set(true);

    this.http.post<PostCallStructuredSummary>('/api/v1/voice/diarization/generate-summary', { callId })
      .subscribe({
        next: (summary) => {
          this.isGeneratingSummary.set(false);
          this.calls.update(calls => calls.map(c => {
            if (c.callId === callId) {
              return { ...c, summary };
            }
            return c;
          }));
        },
        error: (err) => {
          this.isGeneratingSummary.set(false);
          console.warn('Generate summary API failed; simulating locally.', err);
          this.generateLocalSummary(callId);
        }
      });
  }

  copyTalkingPoint(text: string): void {
    if (navigator.clipboard) {
      navigator.clipboard.writeText(text);
    }
  }

  formatDuration(seconds: number): string {
    const mins = Math.floor(seconds / 60);
    const secs = seconds % 60;
    return `${mins.toString().padStart(2, '0')}:${secs.toString().padStart(2, '0')}`;
  }

  formatMs(ms: number): string {
    const totalSecs = Math.floor(ms / 1000);
    const mins = Math.floor(totalSecs / 60);
    const secs = totalSecs % 60;
    return `${mins.toString().padStart(2, '0')}:${secs.toString().padStart(2, '0')}`;
  }

  calcNegativeBarPercent(score: number): number {
    if (score >= 0) return 50;
    // Map -1.0..0 to 0%..50%
    return Math.max(0, 50 - Math.abs(score) * 50);
  }

  calcPositiveBarPercent(score: number): number {
    if (score <= 0) return 0;
    // Map 0..+1.0 to 0%..50%
    return Math.min(50, score * 50);
  }

  private appendLocalUtterance(callId: string, speaker: SpeakerType, text: string, conf: number): void {
    this.calls.update(calls => calls.map(c => {
      if (c.callId === callId) {
        const lastMs = c.utterances.length > 0 ? c.utterances[c.utterances.length - 1].endOffsetMs : 0;
        const newUtterance: TranscribedUtterance = {
          id: 'u_' + Math.random().toString(36).substring(2, 9),
          callId: c.callId,
          speaker: speaker,
          text: text,
          startOffsetMs: lastMs + 600,
          endOffsetMs: lastMs + 4500,
          durationMs: 3900,
          confidenceScore: conf,
          sentimentScore: text.includes('unacceptable') || text.includes('cancel') ? -0.85 : 0.60,
          sentimentVelocity: -0.15,
          timestamp: new Date().toISOString(),
          words: text.split(' ').map((w, idx) => ({
            word: w,
            confidence: Math.max(0.75, conf - 0.02 * (idx % 3)),
            startMs: lastMs + 600 + idx * 300,
            endMs: lastMs + 900 + idx * 300
          })),
          triggeredFrustrationAlert: text.includes('cancel') || text.includes('unacceptable')
        };

        const updatedUtterances = [...c.utterances, newUtterance];
        return {
          ...c,
          utterances: updatedUtterances,
          overallSentimentScore: Number((updatedUtterances.reduce((acc, u) => acc + u.sentimentScore, 0) / updatedUtterances.length).toFixed(2))
        };
      }
      return c;
    }));
  }

  private generateLocalSummary(callId: string): void {
    this.calls.update(calls => calls.map(c => {
      if (c.callId === callId) {
        return {
          ...c,
          summary: {
            callId: c.callId,
            generatedAt: new Date().toISOString(),
            keyDiscussionPoints: [
              'Review of warehouse dispatch latency and webhook delivery stability.',
              'Detailed analysis of peak transaction throughput.'
            ],
            objectionsRaised: [
              'Latency spikes observed during peak batch order updates.'
            ],
            customerSentimentTrajectory: c.sentimentTrajectory,
            overallSentimentScore: c.overallSentimentScore,
            agreedActionItems: [
              'Deploy edge caching tier for outbound webhooks.',
              'Schedule engineering sync on Friday.'
            ],
            recommendedNextStep: 'Send technical recap email to client VP of Engineering.',
            crmDealStage: c.sentimentTrajectory === 'Improving' ? 'Closed Won' : 'At Risk',
            associatedDealId: 'DEAL-' + c.callId,
            executiveBrief: `Call concluded with ${c.sentimentTrajectory} sentiment score (${c.overallSentimentScore}).`
          }
        };
      }
      return c;
    }));
  }

  private loadFallbackDemoCalls(): void {
    this.calls.set([
      {
        callId: 'CALL-5120-OUT',
        tenantId: '11111111-1111-1111-1111-111111111111',
        callerName: 'Elena Rostova',
        callerPhoneNumber: '+44 20 7946 0912',
        agentName: "Liam O'Connor",
        callTopic: 'Critical Webhook Latency & Ingest Outage Incident',
        startedAtUtc: new Date(Date.now() - 15 * 60000).toISOString(),
        totalDurationSeconds: 620,
        status: 'Live',
        overallSentimentScore: -0.74,
        sentimentTrajectory: 'Deteriorating',
        associatedDealId: 'DEAL-EUROLOG-RISK-2026',
        frustrationAlert: {
          isActive: true,
          severity: 'Critical',
          triggerReason: 'Critical Churn Threat: Caller sentiment dropped below -0.60 for consecutive turns and trigger words detected: [unacceptable, supervisor, fraud].',
          triggerKeywords: ['unacceptable', 'supervisor', 'fraud'],
          empathyGuidanceTalkingPoints: [
            'I hear how disruptive this webhook outage is to your dispatch fleet. I am personally escalating this ticket directly to our VP of Engineering.',
            'I will stay on this line with you and guarantee you will not be transferred to another queue.'
          ]
        },
        utterances: [
          {
            id: 'u1',
            callId: 'CALL-5120-OUT',
            speaker: 'Caller',
            text: 'Liam, our warehouse dispatchers are dead in the water. Webhooks are dropping 30% of incoming deliveries.',
            startOffsetMs: 0,
            endOffsetMs: 7000,
            durationMs: 7000,
            confidenceScore: 0.95,
            sentimentScore: -0.45,
            sentimentVelocity: 0.0,
            timestamp: new Date().toISOString(),
            words: [
              { word: 'Liam,', confidence: 0.98, startMs: 0, endMs: 800 },
              { word: 'our', confidence: 0.96, startMs: 850, endMs: 1200 },
              { word: 'dispatchers', confidence: 0.94, startMs: 1250, endMs: 2400 },
              { word: 'are', confidence: 0.97, startMs: 2450, endMs: 2800 },
              { word: 'dead', confidence: 0.92, startMs: 2850, endMs: 3400 },
              { word: 'in', confidence: 0.98, startMs: 3450, endMs: 3700 },
              { word: 'water.', confidence: 0.95, startMs: 3750, endMs: 4400 }
            ]
          },
          {
            id: 'u2',
            callId: 'CALL-5120-OUT',
            speaker: 'Agent',
            text: 'Elena, I am checking our EU-West gateway telemetry right now to identify the ingress bottleneck.',
            startOffsetMs: 7500,
            endOffsetMs: 14000,
            durationMs: 6500,
            confidenceScore: 0.98,
            sentimentScore: 0.20,
            sentimentVelocity: 0.08,
            timestamp: new Date().toISOString(),
            words: []
          },
          {
            id: 'u3',
            callId: 'CALL-5120-OUT',
            speaker: 'Caller',
            text: 'This has been going on for two hours. It is completely unacceptable and feels like fraud given your 99.99% SLA claim!',
            startOffsetMs: 14800,
            endOffsetMs: 23000,
            durationMs: 8200,
            confidenceScore: 0.96,
            sentimentScore: -0.88,
            sentimentVelocity: -0.12,
            timestamp: new Date().toISOString(),
            triggeredFrustrationAlert: true,
            words: []
          },
          {
            id: 'u4',
            callId: 'CALL-5120-OUT',
            speaker: 'Caller',
            text: 'Get me a supervisor or your VP of Infrastructure immediately. We are losing thousands of euros every minute!',
            startOffsetMs: 24000,
            endOffsetMs: 31500,
            durationMs: 7500,
            confidenceScore: 0.94,
            sentimentScore: -0.82,
            sentimentVelocity: 0.01,
            timestamp: new Date().toISOString(),
            triggeredFrustrationAlert: true,
            words: []
          }
        ]
      },
      {
        callId: 'CALL-8921-EXP',
        tenantId: '11111111-1111-1111-1111-111111111111',
        callerName: 'Sarah Chen',
        callerPhoneNumber: '+1 (415) 892-1002',
        agentName: 'Marcus Vance',
        callTopic: 'Warehouse Automation API Renewal & Volume Expansion',
        startedAtUtc: new Date(Date.now() - 40 * 60000).toISOString(),
        totalDurationSeconds: 885,
        status: 'Completed',
        overallSentimentScore: 0.58,
        sentimentTrajectory: 'Improving',
        associatedDealId: 'DEAL-APEX-EXP-2026',
        frustrationAlert: { isActive: false, severity: 'None', triggerReason: '', triggerKeywords: [], empathyGuidanceTalkingPoints: [] },
        utterances: []
      }
    ]);
  }
}
