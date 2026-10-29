import { Component, OnInit, OnDestroy, signal, computed, inject, HostListener } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { RouterLink, ActivatedRoute, Router } from '@angular/router';
import { ApiService } from '../../core/services/api.service';
import { AuthService } from '../../core/services/auth.service';
import {
  VideoMeetingSessionDto,
  WebRtcAccessTokenResultDto,
  ParticipantMediaStateDto,
  SalesBattleCardDto,
  ObjectionEvaluationResultDto,
  MeetingTranscriptItemDto,
  MeetingSessionSummaryDto
} from '../../core/models/calendar.models';

@Component({
  selector: 'app-video-meeting-room',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterLink],
  template: `
    <div dir="ltr" class="flex flex-col h-screen w-screen bg-slate-950 text-slate-100 font-sans overflow-hidden select-none">
      
      <!-- 1. TOP HEADER -->
      <header class="h-14 border-b border-slate-800 bg-slate-900/80 backdrop-blur-md px-6 flex items-center justify-between shrink-0 z-20">
        <div class="flex items-center gap-3">
          <a routerLink="/calendar" class="text-xs text-slate-400 hover:text-white flex items-center gap-1.5 transition py-1 px-2 rounded-md hover:bg-slate-800">
            <svg class="w-4 h-4" fill="none" viewBox="0 0 24 24" stroke="currentColor">
              <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M10 19l-7-7m0 0l7-7m-7 7h18" />
            </svg>
            Calendar
          </a>
          <span class="text-slate-700">|</span>
          <div class="flex items-center gap-2">
            <span class="w-2.5 h-2.5 rounded-full bg-emerald-500 animate-pulse"></span>
            <h1 class="text-sm font-semibold text-white tracking-tight truncate max-w-xs md:max-w-md">
              {{ room()?.title || 'Client Strategy Consultation' }}
            </h1>
          </div>
          <span class="hidden sm:inline-flex bg-indigo-500/10 text-indigo-400 text-[11px] font-mono px-2 py-0.5 rounded-full border border-indigo-500/20">
            Room: {{ roomToken() }}
          </span>
        </div>

        <div class="flex items-center gap-3">
          <!-- Live Call Recording Badge -->
          <div class="flex items-center gap-1.5 px-2.5 py-1 rounded-full bg-rose-500/10 border border-rose-500/20 text-rose-400 text-xs font-mono font-medium">
            <span class="w-2 h-2 rounded-full bg-rose-500 animate-ping"></span>
            <span>REC</span>
          </div>

          <!-- Live Duration Timer -->
          <div class="flex items-center gap-1.5 px-3 py-1 rounded-lg bg-slate-800/80 border border-slate-700 text-xs font-mono text-slate-200">
            <svg class="w-3.5 h-3.5 text-slate-400" fill="none" viewBox="0 0 24 24" stroke="currentColor">
              <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M12 8v4l3 3m6-3a9 9 0 11-18 0 9 9 0 0118 0z" />
            </svg>
            <span>{{ callTimerFormatted() }}</span>
          </div>

          <!-- Network Quality Indicator -->
          <div class="hidden md:flex items-center gap-1.5 px-2.5 py-1 rounded-lg bg-slate-800/60 border border-slate-700/60 text-xs" title="Latency: {{ pingLatencyMs() }}ms | Packet Loss: 0%">
            <div class="flex gap-0.5 items-end h-3">
              <span class="w-1 h-1.5 rounded-sm bg-emerald-400"></span>
              <span class="w-1 h-2 rounded-sm bg-emerald-400"></span>
              <span class="w-1 h-2.5 rounded-sm bg-emerald-400"></span>
              <span class="w-1 h-3 rounded-sm" [class.bg-emerald-400]="networkQuality() === 'excellent'" [class.bg-amber-400]="networkQuality() === 'good'" [class.bg-slate-600]="networkQuality() === 'poor'"></span>
            </div>
            <span class="text-[11px] font-mono text-slate-300">{{ pingLatencyMs() }}ms</span>
          </div>

          <!-- In-Call Copilot Drawer Toggle Button -->
          <button
            (click)="toggleCopilot()"
            class="relative px-3 py-1.5 rounded-lg text-xs font-medium flex items-center gap-1.5 transition shadow-sm border"
            [ngClass]="isCopilotOpen() ? 'bg-indigo-600 text-white border-indigo-500' : 'bg-slate-800 text-slate-300 border-slate-700 hover:bg-slate-700'">
            <svg class="w-4 h-4 text-amber-300" fill="currentColor" viewBox="0 0 20 20">
              <path fill-rule="evenodd" d="M11.3 1.046A1 1 0 0112 2v5h4a1 1 0 01.82 1.573l-7 10A1 1 0 018 18v-5H4a1 1 0 01-.82-1.573l7-10a1 1 0 011.12-.38z" clip-rule="evenodd" />
            </svg>
            <span class="hidden sm:inline">AI Copilot</span>
            @if (activeBattleCards().length > 0) {
              <span class="w-4 h-4 rounded-full bg-amber-400 text-slate-950 text-[10px] font-bold flex items-center justify-center animate-bounce">
                {{ activeBattleCards().length }}
              </span>
            }
          </button>
        </div>
      </header>

      <!-- 2. MAIN STAGE & COPILOT DRAWER CONTAINER -->
      <div class="flex-1 flex overflow-hidden relative">

        <!-- A. VIDEO TILES STAGE (LEFT / CENTER) -->
        <main class="flex-1 flex flex-col p-4 md:p-6 bg-slate-950 relative overflow-hidden transition-all duration-300">
          
          <!-- Screen Share Active Banner (if enabled) -->
          @if (isScreenSharing()) {
            <div class="mb-3 px-4 py-2 rounded-lg bg-indigo-950/60 border border-indigo-500/40 text-xs flex items-center justify-between text-indigo-200">
              <div class="flex items-center gap-2">
                <svg class="w-4 h-4 text-indigo-400 animate-pulse" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                  <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M9.75 17L9 20l-1 1h8l-1-1-.75-3M3 13h18M5 17h14a2 2 0 002-2V5a2 2 0 00-2-2H5a2 2 0 00-2 2v10a2 2 0 002 2z" />
                </svg>
                <span>You are currently presenting your screen (Full Display 1080p SFU Stream)</span>
              </div>
              <button (click)="toggleScreenShare()" class="text-xs text-rose-400 hover:text-rose-300 underline font-medium">
                Stop Sharing [S]
              </button>
            </div>
          }

          <!-- Video Grid Container -->
          <div class="flex-1 grid grid-cols-1 md:grid-cols-12 gap-4 relative items-center justify-center min-h-0">
            
            <!-- Primary Tile: Active Speaker (Remote Prospect / Client) -->
            <div 
              class="relative rounded-2xl overflow-hidden bg-slate-900 border-2 transition-all duration-300 shadow-2xl flex flex-col justify-between"
              [ngClass]="[
                isScreenSharing() ? 'md:col-span-8 h-full' : 'md:col-span-12 h-full',
                isSpeakingRemote() ? 'border-emerald-500 ring-4 ring-emerald-500/20' : 'border-slate-800'
              ]">
              
              <!-- Remote Participant Video Canvas Mock / Avatar -->
              <div class="absolute inset-0 flex flex-col items-center justify-center bg-gradient-to-br from-slate-900 via-slate-900 to-indigo-950/40">
                <div class="relative">
                  <!-- Animated Speaking Wave Aura -->
                  @if (isSpeakingRemote()) {
                    <div class="absolute -inset-4 rounded-full bg-emerald-500/20 animate-ping"></div>
                  }
                  <div class="w-24 h-24 md:w-32 md:h-32 rounded-full bg-indigo-600/30 border-2 border-indigo-400/40 flex items-center justify-center text-3xl md:text-4xl font-bold text-indigo-200 shadow-xl relative z-10">
                    {{ getInitials(remoteParticipantName()) }}
                  </div>
                </div>

                <!-- Speaking Audio Equalizer Bars -->
                @if (isSpeakingRemote()) {
                  <div class="mt-4 flex items-end gap-1 h-5">
                    @for (bar of audioBars(); track $index) {
                      <span 
                        class="w-1 bg-emerald-400 rounded-full transition-all duration-75"
                        [style.height.%]="bar">
                      </span>
                    }
                  </div>
                }
              </div>

              <!-- Top Left: Speaker State & Role -->
              <div class="relative z-10 p-4 flex items-center justify-between">
                <div class="flex items-center gap-2">
                  <span class="px-2.5 py-1 rounded-md bg-slate-900/90 backdrop-blur border border-slate-700/80 text-xs font-medium text-slate-200 flex items-center gap-1.5 shadow-sm">
                    <span class="w-2 h-2 rounded-full" [class.bg-emerald-400]="isSpeakingRemote()" [class.bg-slate-500]="!isSpeakingRemote()"></span>
                    {{ remoteParticipantName() }}
                  </span>
                  <span class="px-2 py-0.5 rounded text-[10px] font-mono bg-indigo-500/20 text-indigo-300 border border-indigo-500/30">
                    Client Participant
                  </span>
                </div>

                <!-- Remote Connection Health -->
                <div class="px-2 py-0.5 rounded bg-slate-900/80 text-[10px] font-mono text-emerald-400 border border-emerald-500/20">
                  HD 1080p SFU
                </div>
              </div>

              <!-- Bottom Overlay: Active Objections Alert Bar (if detected) -->
              <div class="relative z-10 p-4">
                @if (activeBattleCards().length > 0) {
                  <div class="p-2.5 rounded-xl bg-amber-950/80 border border-amber-500/40 backdrop-blur-md flex items-center justify-between text-xs text-amber-200 animate-pulse">
                    <div class="flex items-center gap-2">
                      <span class="text-base">⚡</span>
                      <span class="font-semibold text-amber-300">Sales Copilot Alert:</span>
                      <span class="truncate max-w-sm">{{ activeBattleCards()[0].title }}</span>
                    </div>
                    <button (click)="openCopilotDrawer()" class="px-2 py-1 rounded bg-amber-500/20 hover:bg-amber-500/30 text-amber-300 text-[11px] font-medium border border-amber-500/40">
                      View Talking Points →
                    </button>
                  </div>
                }
              </div>
            </div>

            <!-- Picture-in-Picture / Self Video Tile (Local Representative) -->
            <div 
              class="relative rounded-xl overflow-hidden bg-slate-900 border transition-all duration-200 shadow-xl flex flex-col justify-between"
              [ngClass]="[
                isScreenSharing() ? 'md:col-span-4 h-full' : 'absolute bottom-6 right-6 w-52 md:w-64 h-36 md:h-44 z-20',
                isSpeakingLocal() ? 'border-emerald-500 ring-2 ring-emerald-500/30' : 'border-slate-700/80'
              ]">
              
              <!-- Video / Avatar Display -->
              <div class="absolute inset-0 flex flex-col items-center justify-center bg-slate-800">
                @if (isCameraOff()) {
                  <div class="flex flex-col items-center gap-1.5 text-slate-400">
                    <div class="w-12 h-12 rounded-full bg-slate-700 flex items-center justify-center text-lg font-bold text-slate-300">
                      {{ getInitials(participantName()) }}
                    </div>
                    <span class="text-[10px] font-mono text-slate-400">Camera Off</span>
                  </div>
                } @else {
                  <!-- Simulated Live Camera Canvas View -->
                  <div class="w-full h-full bg-gradient-to-br from-indigo-950 via-slate-800 to-slate-900 flex items-center justify-center relative">
                    <div class="w-14 h-14 rounded-full bg-emerald-600/30 border border-emerald-400/40 flex items-center justify-center text-xl font-bold text-emerald-200">
                      {{ getInitials(participantName()) }}
                    </div>
                    <span class="absolute bottom-2 left-2 text-[10px] font-mono bg-slate-900/80 px-1.5 py-0.5 rounded text-emerald-400 border border-emerald-500/20">
                      Webcam Active
                    </span>
                  </div>
                }
              </div>

              <!-- Top Controls: Mute status -->
              <div class="relative z-10 p-2 flex items-center justify-between">
                <span class="px-2 py-0.5 rounded bg-slate-900/90 text-[10px] font-medium text-slate-200 truncate max-w-[120px]">
                  You ({{ participantRole() }})
                </span>
                @if (isMicMuted()) {
                  <span class="w-5 h-5 rounded-full bg-rose-500/90 text-white flex items-center justify-center text-[10px]" title="Microphone Muted">
                    ✕
                  </span>
                }
              </div>

              <div class="relative z-10 p-2"></div>
            </div>

          </div>

          <!-- BOTTOM FLOATING CONTROL DOCK -->
          <div class="mt-4 flex items-center justify-center shrink-0 z-30">
            <div class="px-4 py-2.5 rounded-2xl bg-slate-900/90 backdrop-blur-xl border border-slate-700/80 shadow-2xl flex items-center gap-3">
              
              <!-- 1. Microphone Toggle -->
              <button
                (click)="toggleMic()"
                [class.bg-rose-600]="isMicMuted()"
                [class.bg-slate-800]="!isMicMuted()"
                [class.text-white]="isMicMuted()"
                [class.text-slate-200]="!isMicMuted()"
                class="w-11 h-11 rounded-xl flex items-center justify-center hover:bg-slate-700 transition border border-slate-700"
                title="Toggle Mic (M)">
                @if (isMicMuted()) {
                  <svg class="w-5 h-5 text-white" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                    <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M19 11a7 7 0 01-7 7m0 0a7 7 0 01-7-7m7 7v4m0 0H8m4 0h4m-4-8a3 3 0 01-3-3V5a3 3 0 116 0v6a3 3 0 01-3 3z" />
                    <line x1="3" y1="3" x2="21" y2="21" stroke="currentColor" stroke-width="2" stroke-linecap="round" />
                  </svg>
                } @else {
                  <svg class="w-5 h-5 text-emerald-400" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                    <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M19 11a7 7 0 01-7 7m0 0a7 7 0 01-7-7m7 7v4m0 0H8m4 0h4m-4-8a3 3 0 01-3-3V5a3 3 0 116 0v6a3 3 0 01-3 3z" />
                  </svg>
                }
              </button>

              <!-- 2. Camera Toggle -->
              <button
                (click)="toggleCamera()"
                [class.bg-rose-600]="isCameraOff()"
                [class.bg-slate-800]="!isCameraOff()"
                class="w-11 h-11 rounded-xl flex items-center justify-center hover:bg-slate-700 transition border border-slate-700 text-slate-200"
                title="Toggle Camera (V)">
                @if (isCameraOff()) {
                  <svg class="w-5 h-5 text-white" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                    <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M15 10l4.553-2.276A1 1 0 0121 8.618v6.764a1 1 0 01-1.447.894L15 14M5 18h8a2 2 0 002-2V8a2 2 0 00-2-2H5a2 2 0 00-2 2v8a2 2 0 002 2z" />
                    <line x1="3" y1="3" x2="21" y2="21" stroke="currentColor" stroke-width="2" stroke-linecap="round" />
                  </svg>
                } @else {
                  <svg class="w-5 h-5 text-slate-200" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                    <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M15 10l4.553-2.276A1 1 0 0121 8.618v6.764a1 1 0 01-1.447.894L15 14M5 18h8a2 2 0 002-2V8a2 2 0 00-2-2H5a2 2 0 00-2 2v8a2 2 0 002 2z" />
                  </svg>
                }
              </button>

              <!-- 3. Screen Share Toggle -->
              <button
                (click)="toggleScreenShare()"
                [class.bg-indigo-600]="isScreenSharing()"
                [class.bg-slate-800]="!isScreenSharing()"
                class="w-11 h-11 rounded-xl flex items-center justify-center hover:bg-slate-700 transition border border-slate-700 text-slate-200"
                title="Share Screen (S)">
                <svg class="w-5 h-5" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                  <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M9.75 17L9 20l-1 1h8l-1-1-.75-3M3 13h18M5 17h14a2 2 0 002-2V5a2 2 0 00-2-2H5a2 2 0 00-2 2v10a2 2 0 002 2z" />
                </svg>
              </button>

              <!-- Divider -->
              <span class="w-px h-6 bg-slate-700"></span>

              <!-- 4. Quick Battle Card Simulation Trigger (Demo Tool) -->
              <button
                (click)="simulateObjection('pricing')"
                class="hidden md:flex items-center gap-1.5 px-3 py-2 rounded-xl bg-slate-800/80 hover:bg-slate-800 border border-slate-700 text-xs text-amber-300 font-medium transition"
                title="Simulate enterprise pricing objection">
                <span>⚡ Test Objection</span>
              </button>

              <!-- Divider -->
              <span class="w-px h-6 bg-slate-700"></span>

              <!-- 5. Leave Call Button (Red) -->
              <button
                (click)="promptLeaveCall()"
                class="px-5 h-11 rounded-xl bg-rose-600 hover:bg-rose-700 text-white font-medium text-xs flex items-center gap-2 transition shadow-lg shadow-rose-900/30">
                <svg class="w-4 h-4" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                  <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M16 8l2-2m0 0l2-2m-2 2l-2-2m2 2l2 2M5 3a2 2 0 00-2 2v1c0 8.284 6.716 15 15 15h1a2 2 0 002-2v-3.28a1 1 0 00-.684-.948l-4.493-1.498a1 1 0 00-1.21.502l-1.13 2.257a11.042 11.042 0 01-5.516-5.517l2.257-1.128a1 1 0 00.502-1.21L9.228 3.683A1 1 0 008.279 3H5z" />
                </svg>
                <span>Leave Call</span>
              </button>

            </div>
          </div>

        </main>

        <!-- B. RIGHT-SIDE IN-CALL COPILOT DRAWER (TOGGLABLE) -->
        @if (isCopilotOpen()) {
          <aside class="w-80 md:w-96 border-l border-slate-800 bg-slate-900/95 flex flex-col h-full shrink-0 z-20 shadow-2xl transition-all duration-300">
            
            <!-- Drawer Header -->
            <div class="h-14 border-b border-slate-800 px-4 flex items-center justify-between shrink-0 bg-slate-900">
              <div class="flex items-center gap-2">
                <div class="w-7 h-7 rounded-lg bg-indigo-600/30 text-indigo-400 flex items-center justify-center text-sm border border-indigo-500/30">
                  ⚡
                </div>
                <div>
                  <h2 class="text-xs font-bold text-white uppercase tracking-wider">In-Call Sales Copilot</h2>
                  <p class="text-[10px] text-slate-400 font-mono">Real-Time Objection Radar</p>
                </div>
              </div>
              
              <!-- Sentiment Pill -->
              <div class="flex items-center gap-1.5 px-2 py-0.5 rounded-full border text-[11px] font-medium"
                [ngClass]="{
                  'bg-emerald-500/10 border-emerald-500/30 text-emerald-300': overallSentiment() === 'Positive',
                  'bg-slate-800 border-slate-700 text-slate-300': overallSentiment() === 'Neutral',
                  'bg-amber-500/10 border-amber-500/30 text-amber-300': overallSentiment() === 'Skeptical'
                }">
                <span>{{ overallSentiment() }}</span>
                <span class="text-[10px] font-mono opacity-80">{{ sentimentScore() > 0 ? '+' : '' }}{{ sentimentScore() | number:'1.2-2' }}</span>
              </div>
            </div>

            <!-- Drawer Tabs / Sections -->
            <div class="flex-1 flex flex-col overflow-y-auto p-4 space-y-4">
              
              <!-- ACTIVE BATTLE CARDS SECTION -->
              @if (activeBattleCards().length > 0) {
                <div class="space-y-3">
                  <div class="flex items-center justify-between">
                    <span class="text-[11px] font-bold text-amber-300 uppercase tracking-wider flex items-center gap-1.5">
                      <span class="w-2 h-2 rounded-full bg-amber-400 animate-pulse"></span>
                      Active Battle Cards ({{ activeBattleCards().length }})
                    </span>
                    <button (click)="dismissAllBattleCards()" class="text-[10px] text-slate-400 hover:text-white underline">
                      Dismiss
                    </button>
                  </div>

                  @for (card of activeBattleCards(); track card.objectionKey) {
                    <div class="p-3.5 rounded-xl bg-slate-800/90 border border-amber-500/40 shadow-lg space-y-2.5">
                      <div class="flex items-center justify-between">
                        <span class="text-[10px] font-mono px-2 py-0.5 rounded bg-amber-500/20 text-amber-300 border border-amber-500/30">
                          {{ card.category }}
                        </span>
                        <span class="text-[10px] font-mono text-slate-400">
                          {{ (card.confidenceScore * 100) | number:'1.0-0' }}% Match
                        </span>
                      </div>

                      <h3 class="text-xs font-bold text-white leading-snug">
                        {{ card.title }}
                      </h3>

                      <!-- Quick Pitch Callout -->
                      <div class="p-2.5 rounded-lg bg-indigo-950/60 border border-indigo-500/30 text-xs text-indigo-100">
                        <p class="font-medium italic leading-relaxed">
                          "{{ card.quickPitch }}"
                        </p>
                      </div>

                      <!-- Key Talking Points Bullets -->
                      <div class="space-y-1">
                        <span class="text-[10px] font-semibold text-slate-400 uppercase tracking-wider">Talking Points:</span>
                        <ul class="space-y-1 text-xs text-slate-300">
                          @for (point of card.keyTalkingPoints; track $index) {
                            <li class="flex items-start gap-1.5">
                              <span class="text-emerald-400 text-xs mt-0.5">✓</span>
                              <span class="leading-tight">{{ point }}</span>
                            </li>
                          }
                        </ul>
                      </div>

                      <!-- Follow-up Question -->
                      @if (card.recommendedFollowUpQuestion) {
                        <div class="p-2 rounded-lg bg-slate-900 border border-slate-700/80 text-[11px] text-slate-300">
                          <span class="text-amber-300 font-semibold">Ask: </span>
                          <span>{{ card.recommendedFollowUpQuestion }}</span>
                        </div>
                      }
                    </div>
                  }
                </div>
              }

              <!-- REAL-TIME TRANSCRIPT FEED -->
              <div class="space-y-2">
                <div class="flex items-center justify-between">
                  <span class="text-[11px] font-bold text-slate-400 uppercase tracking-wider flex items-center gap-1.5">
                    <svg class="w-3.5 h-3.5 text-indigo-400" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                      <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M19 11a7 7 0 01-7 7m0 0a7 7 0 01-7-7m7 7v4m0 0H8m4 0h4m-4-8a3 3 0 01-3-3V5a3 3 0 116 0v6a3 3 0 01-3 3z" />
                    </svg>
                    Live Speech Transcription
                  </span>
                  <span class="text-[10px] font-mono text-emerald-400">Deepgram Nova-2</span>
                </div>

                <div class="max-h-60 overflow-y-auto space-y-2 pr-1">
                  @for (t of transcripts(); track $index) {
                    <div class="p-2 rounded-lg bg-slate-950/70 border border-slate-800 text-xs space-y-1">
                      <div class="flex items-center justify-between text-[10px]">
                        <span class="font-semibold" [class.text-indigo-400]="t.speakerIdentity === 'Host'" [class.text-amber-400]="t.speakerIdentity !== 'Host'">
                          {{ t.speakerIdentity }}
                        </span>
                        <span class="font-mono text-slate-500">
                          {{ t.timestampUtc | date:'HH:mm:ss' }}
                        </span>
                      </div>
                      <p class="text-slate-200 leading-snug">
                        {{ t.transcript }}
                      </p>
                    </div>
                  }
                </div>
              </div>

              <!-- SIMULATE LIVE OBJECTION TOOLBAR (Testing & Demo Sandbox) -->
              <div class="p-3 rounded-xl bg-slate-950/80 border border-slate-800 space-y-2">
                <span class="text-[10px] font-bold text-slate-400 uppercase tracking-wider block">
                  Simulate Live Speech Triggers:
                </span>
                <div class="grid grid-cols-2 gap-1.5 text-[11px]">
                  <button 
                    (click)="simulateObjection('pricing')" 
                    class="p-1.5 rounded-lg bg-slate-800 hover:bg-slate-700 text-slate-200 border border-slate-700 text-left truncate transition">
                    💰 Pricing High
                  </button>
                  <button 
                    (click)="simulateObjection('migration')" 
                    class="p-1.5 rounded-lg bg-slate-800 hover:bg-slate-700 text-slate-200 border border-slate-700 text-left truncate transition">
                    🔄 Migration
                  </button>
                  <button 
                    (click)="simulateObjection('cod')" 
                    class="p-1.5 rounded-lg bg-slate-800 hover:bg-slate-700 text-slate-200 border border-slate-700 text-left truncate transition">
                    🇹🇳 TND / COD
                  </button>
                  <button 
                    (click)="simulateObjection('security')" 
                    class="p-1.5 rounded-lg bg-slate-800 hover:bg-slate-700 text-slate-200 border border-slate-700 text-left truncate transition">
                    🛡️ Security / GDPR
                  </button>
                </div>
              </div>

              <!-- QUICK IN-CALL NOTES PAD -->
              <div class="space-y-1.5">
                <div class="flex items-center justify-between">
                  <span class="text-[11px] font-bold text-slate-400 uppercase tracking-wider">
                    In-Call Notes
                  </span>
                  <span class="text-[10px] font-mono text-slate-500">Autosaved</span>
                </div>
                <textarea
                  [(ngModel)]="inCallNotes"
                  rows="3"
                  placeholder="Type notes, client requirements, action items..."
                  class="w-full bg-slate-950 border border-slate-800 rounded-lg p-2.5 text-xs text-slate-200 focus:outline-none focus:border-indigo-500 transition placeholder-slate-600 resize-none"></textarea>
              </div>

            </div>
          </aside>
        }
      </div>

      <!-- 3. POST-MEETING SUMMARY MODAL (Triggered upon Leaving Call) -->
      @if (isLeaveModalOpen()) {
        <div class="fixed inset-0 z-50 bg-slate-950/80 backdrop-blur-md flex items-center justify-center p-4">
          <div class="w-full max-w-lg bg-slate-900 border border-slate-800 rounded-2xl shadow-2xl p-6 space-y-5">
            
            <div class="flex items-center justify-between pb-3 border-b border-slate-800">
              <div class="flex items-center gap-2">
                <span class="w-3 h-3 rounded-full bg-emerald-500"></span>
                <h3 class="text-base font-bold text-white">Consultation Summary & Follow-Ups</h3>
              </div>
              <span class="text-xs font-mono text-slate-400">Duration: {{ callTimerFormatted() }}</span>
            </div>

            <!-- Call Performance Highlights -->
            <div class="grid grid-cols-3 gap-3 text-center">
              <div class="p-3 rounded-xl bg-slate-950 border border-slate-800">
                <div class="text-[10px] font-semibold text-slate-400 uppercase">Objections</div>
                <div class="text-lg font-bold text-amber-300 mt-0.5">{{ activeBattleCards().length }}</div>
              </div>
              <div class="p-3 rounded-xl bg-slate-950 border border-slate-800">
                <div class="text-[10px] font-semibold text-slate-400 uppercase">Sentiment</div>
                <div class="text-lg font-bold mt-0.5" [class.text-emerald-400]="sentimentScore() >= 0" [class.text-amber-400]="sentimentScore() < 0">
                  {{ overallSentiment() }}
                </div>
              </div>
              <div class="p-3 rounded-xl bg-slate-950 border border-slate-800">
                <div class="text-[10px] font-semibold text-slate-400 uppercase">SFU Health</div>
                <div class="text-lg font-bold text-emerald-400 mt-0.5">100%</div>
              </div>
            </div>

            <!-- Follow-up Action Items Generated by AI -->
            <div class="space-y-2">
              <span class="text-xs font-bold text-white uppercase tracking-wider">AI Recommended Next Steps:</span>
              <div class="p-3 rounded-xl bg-slate-950 border border-slate-800 space-y-1.5 text-xs text-slate-300">
                <div class="flex items-start gap-2">
                  <span class="text-indigo-400 font-bold">1.</span>
                  <span>Deliver custom ROI breakdown & bundle pricing proposal consolidating Cal.com + Zoom + CRM.</span>
                </div>
                <div class="flex items-start gap-2">
                  <span class="text-indigo-400 font-bold">2.</span>
                  <span>Send formal quote in Tunisian Dinars (TND) with Flouci/Konnect checkout and COD WhatsApp workflow setup.</span>
                </div>
                <div class="flex items-start gap-2">
                  <span class="text-indigo-400 font-bold">3.</span>
                  <span>Schedule 1-click CRM & calendar migration walkthrough with dedicated onboarding engineer.</span>
                </div>
              </div>
            </div>

            <!-- Action Buttons -->
            <div class="flex items-center justify-end gap-3 pt-2">
              <button
                (click)="cancelLeaveCall()"
                class="px-4 py-2 rounded-xl text-xs font-medium text-slate-400 hover:text-white bg-slate-800 hover:bg-slate-700 transition">
                Return to Call
              </button>
              <button
                (click)="confirmLeaveCall()"
                class="px-5 py-2 rounded-xl text-xs font-medium text-white bg-indigo-600 hover:bg-indigo-500 transition shadow-lg shadow-indigo-900/30">
                Conclude & Save Debrief
              </button>
            </div>

          </div>
        </div>
      }

    </div>
  `
})
export class VideoMeetingRoomComponent implements OnInit, OnDestroy {
  private api = inject(ApiService);
  private auth = inject(AuthService);
  private route = inject(ActivatedRoute);
  private router = inject(Router);

  // Core Room State Signals
  public roomToken = signal<string>('demo-consultation');
  public room = signal<VideoMeetingSessionDto | null>(null);
  public tokenResult = signal<WebRtcAccessTokenResultDto | null>(null);

  public participantIdentity = signal<string>('host_sales_rep');
  public participantRole = signal<'host' | 'attendee' | 'copilot'>('host');
  public participantName = signal<string>('Sarah Jenkins (SGrowing)');
  public remoteParticipantName = signal<string>('Dr. Tarek Ben Salem (Prospect)');

  // In-Call Media & Toggles
  public isMicMuted = signal<boolean>(false);
  public isCameraOff = signal<boolean>(false);
  public isScreenSharing = signal<boolean>(false);
  public isCopilotOpen = signal<boolean>(true);
  public isSpeakingLocal = signal<boolean>(false);
  public isSpeakingRemote = signal<boolean>(true);
  public isLeaveModalOpen = signal<boolean>(false);

  // Quality & Metrics
  public networkQuality = signal<'excellent' | 'good' | 'poor'>('excellent');
  public pingLatencyMs = signal<number>(24);
  public elapsedSeconds = signal<number>(872);
  public callTimerFormatted = signal<string>('00:14:32');

  // Copilot State
  public overallSentiment = signal<'Positive' | 'Neutral' | 'Skeptical'>('Skeptical');
  public sentimentScore = signal<number>(-0.35);
  public inCallNotes = signal<string>('Prospect is interested in the Enterprise plan but requested confirmation on Tunisian Dinar invoicing and COD WhatsApp notifications.');
  public activeBattleCards = signal<SalesBattleCardDto[]>([]);
  public transcripts = signal<MeetingTranscriptItemDto[]>([]);
  public audioBars = signal<number[]>([40, 75, 55, 90, 60, 85, 30, 95]);

  private timerInterval: any = null;
  private audioBarInterval: any = null;

  ngOnInit(): void {
    // 1. Resolve roomToken from route parameters
    this.route.paramMap.subscribe(params => {
      const token = params.get('roomToken') || 'demo-consultation';
      this.roomToken.set(token);
      this.initializeMeetingSession(token);
    });

    // 2. Start Call Timer
    this.timerInterval = setInterval(() => {
      this.elapsedSeconds.update(s => s + 1);
      const total = this.elapsedSeconds();
      const hrs = Math.floor(total / 3600);
      const mins = Math.floor((total % 3600) / 60);
      const secs = total % 60;
      const formatted = hrs > 0
        ? `${hrs.toString().padStart(2, '0')}:${mins.toString().padStart(2, '0')}:${secs.toString().padStart(2, '0')}`
        : `${mins.toString().padStart(2, '0')}:${secs.toString().padStart(2, '0')}`;
      this.callTimerFormatted.set(formatted);
    }, 1000);

    // 3. Audio visualizer wave simulation
    this.audioBarInterval = setInterval(() => {
      if (this.isSpeakingRemote()) {
        this.audioBars.set([
          Math.floor(Math.random() * 60) + 30,
          Math.floor(Math.random() * 80) + 20,
          Math.floor(Math.random() * 70) + 30,
          Math.floor(Math.random() * 95) + 15,
          Math.floor(Math.random() * 75) + 25,
          Math.floor(Math.random() * 85) + 20,
          Math.floor(Math.random() * 50) + 30,
          Math.floor(Math.random() * 90) + 20
        ]);
      }
    }, 150);

    // Populate initial seed transcript & battle card for high fidelity
    this.seedInitialSession();
  }

  ngOnDestroy(): void {
    if (this.timerInterval) clearInterval(this.timerInterval);
    if (this.audioBarInterval) clearInterval(this.audioBarInterval);
  }

  // KEYBOARD SHORTCUTS (M: Mic, V: Camera, S: Screen Share, C: Copilot)
  @HostListener('window:keydown', ['$event'])
  handleKeyboardEvent(event: KeyboardEvent): void {
    const target = event.target as HTMLElement;
    if (target && (target.tagName === 'INPUT' || target.tagName === 'TEXTAREA')) {
      return;
    }

    if (event.key === 'm' || event.key === 'M') {
      event.preventDefault();
      this.toggleMic();
    } else if (event.key === 'v' || event.key === 'V') {
      event.preventDefault();
      this.toggleCamera();
    } else if (event.key === 's' || event.key === 'S') {
      event.preventDefault();
      this.toggleScreenShare();
    } else if (event.key === 'c' || event.key === 'C') {
      event.preventDefault();
      this.toggleCopilot();
    } else if (event.key === 'Escape' && this.isLeaveModalOpen()) {
      this.cancelLeaveCall();
    }
  }

  private initializeMeetingSession(token: string): void {
    this.api.joinMeetingRoom(token, {
      participantIdentity: this.participantIdentity(),
      participantRole: this.participantRole(),
      displayName: this.participantName(),
      isPublisher: true
    }).subscribe({
      next: res => {
        this.tokenResult.set(res.token);
        this.room.set(res.room);
        if (res.room.hostName) this.participantName.set(res.room.hostName);
        if (res.room.attendeeName) this.remoteParticipantName.set(res.room.attendeeName);
      },
      error: _err => {
        // Fallback for standalone demo mode
        console.warn('Meeting room not found on server, proceeding with ephemeral studio session.');
      }
    });
  }

  private seedInitialSession(): void {
    this.transcripts.set([
      {
        speakerIdentity: 'Host',
        transcript: 'Welcome Dr. Ben Salem, thank you for joining our strategy demo today.',
        sentimentScore: 0.5,
        isFinal: true,
        timestampUtc: new Date(Date.now() - 300000).toISOString()
      },
      {
        speakerIdentity: 'Client',
        transcript: 'Thank you Sarah. We are reviewing software to consolidate our sales dialer, booking calendar, and CRM.',
        sentimentScore: 0.2,
        isFinal: true,
        timestampUtc: new Date(Date.now() - 200000).toISOString()
      },
      {
        speakerIdentity: 'Client',
        transcript: 'However, our finance team feels the pricing is too high, and we want to know if you support Tunisian Dinars and Cash on Delivery verification.',
        sentimentScore: -0.4,
        isFinal: true,
        timestampUtc: new Date(Date.now() - 60000).toISOString()
      }
    ]);

    this.activeBattleCards.set([
      {
        objectionKey: 'PricingHigh',
        category: 'Budget & ROI',
        title: 'All-in-One Tech Stack Consolidation (3.4x ROI)',
        detectedTrigger: 'Pricing is too high / budget constraints',
        quickPitch: 'Unlike buying separate tools for scheduling (Cal.com), video (Zoom), CRM (HubSpot), and call recording (Gong), SGrowing consolidates your entire stack, cutting monthly software bills by 40-60% with a proven 3.4x average ROI.',
        keyTalkingPoints: [
          'Consolidates 4 separate SaaS subscriptions into a single platform.',
          'Demonstrated 3.4x ROI in first 60 days via automated 30s lead qualification.',
          'Flexible monthly billing with volume discounts available.'
        ],
        recommendedFollowUpQuestion: 'What is your current total monthly spend across your existing CRM, video, and scheduling tools?',
        confidenceScore: 0.95,
        severity: 'high'
      },
      {
        objectionKey: 'LocalPaymentTunisianDinarsCOD',
        category: 'Localization & Payment',
        title: 'Native Tunisian Dinars (TND) & WhatsApp COD Verification',
        detectedTrigger: 'Tunisian Dinars, Flouci/Konnect, and COD confirmation',
        quickPitch: 'SGrowing natively supports invoicing in Tunisian Dinars (TND), integrates local gateways like Flouci and Konnect, and provides automated WhatsApp OTP delivery verification that cuts COD returns by 45%.',
        keyTalkingPoints: [
          'Native TND invoice generation with North African fiscal compliance.',
          'Pre-integrated Flouci, Konnect, and local bank transfers with zero FX fee.',
          'WhatsApp automated COD OTP address confirmation reduces failed deliveries by 45%.'
        ],
        recommendedFollowUpQuestion: 'Are you primarily selling to local consumers across Tunisia via COD, or international B2B clients?',
        confidenceScore: 0.96,
        severity: 'high'
      }
    ]);
  }

  // In-Call Controls
  public toggleMic(): void {
    const newState = !this.isMicMuted();
    this.isMicMuted.set(newState);
    this.syncMediaState();
  }

  public toggleCamera(): void {
    const newState = !this.isCameraOff();
    this.isCameraOff.set(newState);
    this.syncMediaState();
  }

  public toggleScreenShare(): void {
    const newState = !this.isScreenSharing();
    this.isScreenSharing.set(newState);
    this.syncMediaState();
  }

  public toggleCopilot(): void {
    this.isCopilotOpen.update(v => !v);
  }

  public openCopilotDrawer(): void {
    this.isCopilotOpen.set(true);
  }

  public dismissAllBattleCards(): void {
    this.activeBattleCards.set([]);
  }

  private syncMediaState(): void {
    const state: ParticipantMediaStateDto = {
      isMicEnabled: !this.isMicMuted(),
      isCameraEnabled: !this.isCameraOff(),
      isScreenSharing: this.isScreenSharing(),
      isSpeaking: this.isSpeakingLocal(),
      networkQuality: this.networkQuality(),
      latencyMs: this.pingLatencyMs(),
      packetLossRate: 0.0
    };

    this.api.updateMeetingMediaState(this.roomToken(), {
      participantIdentity: this.participantIdentity(),
      state
    }).subscribe({
      next: _res => {},
      error: _err => {}
    });
  }

  // Interactive Live Speech Trigger Simulator (For Testing and Demonstrations)
  public simulateObjection(type: 'pricing' | 'migration' | 'cod' | 'security'): void {
    let transcriptSegment = '';
    if (type === 'pricing') {
      transcriptSegment = 'Our board thinks your pricing is too high compared to basic tools. Do you have discounts?';
    } else if (type === 'migration') {
      transcriptSegment = 'How does migration work if we already have thousands of leads in HubSpot and Google Calendar?';
    } else if (type === 'cod') {
      transcriptSegment = 'Can we pay in Tunisian dinars, and does SGrowing support Cash on Delivery verification?';
    } else if (type === 'security') {
      transcriptSegment = 'What are your data privacy and gdpr guarantees, and where is customer data stored?';
    }

    // Call API evaluate
    this.api.evaluateMeetingObjections(this.roomToken(), transcriptSegment).subscribe({
      next: result => {
        this.transcripts.update(list => [
          ...list,
          {
            speakerIdentity: 'Client',
            transcript: transcriptSegment,
            sentimentScore: result.sentimentScore,
            isFinal: true,
            timestampUtc: new Date().toISOString()
          }
        ]);

        this.overallSentiment.set(result.sentiment as any);
        this.sentimentScore.set(result.sentimentScore);

        // Merge newly triggered battle cards
        const existingKeys = new Set(this.activeBattleCards().map(c => c.objectionKey));
        const newCards = result.matchedBattleCards.filter(c => !existingKeys.has(c.objectionKey));
        this.activeBattleCards.update(cards => [...newCards, ...cards]);
        this.isCopilotOpen.set(true);
      },
      error: _err => {
        // Fallback simulation in case API is offline
        this.transcripts.update(list => [
          ...list,
          {
            speakerIdentity: 'Client',
            transcript: transcriptSegment,
            sentimentScore: -0.3,
            isFinal: true,
            timestampUtc: new Date().toISOString()
          }
        ]);
        this.isCopilotOpen.set(true);
      }
    });
  }

  // Leave Call Lifecycle
  public promptLeaveCall(): void {
    this.isLeaveModalOpen.set(true);
  }

  public cancelLeaveCall(): void {
    this.isLeaveModalOpen.set(false);
  }

  public confirmLeaveCall(): void {
    this.api.endMeetingRoom(this.roomToken(), this.inCallNotes()).subscribe({
      next: _summary => {
        this.isLeaveModalOpen.set(false);
        this.router.navigate(['/calendar']);
      },
      error: _err => {
        this.isLeaveModalOpen.set(false);
        this.router.navigate(['/calendar']);
      }
    });
  }

  public getInitials(name: string): string {
    if (!name) return 'U';
    const parts = name.trim().split(' ');
    if (parts.length === 1) return parts[0][0].toUpperCase();
    return (parts[0][0] + parts[parts.length - 1][0]).toUpperCase();
  }
}
