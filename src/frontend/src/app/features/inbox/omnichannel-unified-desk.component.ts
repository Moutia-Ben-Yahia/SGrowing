import { Component, OnInit, signal, computed, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { ApiService } from '../../core/services/api.service';
import {
  UnifiedConversationSummaryDto,
  UnifiedConversationDetailDto,
  UnifiedDeskMessageDto,
  CopilotSuggestedReplyDto,
  Customer360OverviewDto,
  AgentPresenceReportDto,
  SlaOverviewReportDto,
  AutopilotResolutionDto
} from '../../core/models/unified-desk.models';

@Component({
  selector: 'app-omnichannel-unified-desk',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterLink],
  template: `
    <div dir="ltr" class="flex h-screen w-screen overflow-hidden bg-slate-950 text-slate-100 font-sans antialiased selection:bg-blue-600 selection:text-white">
      
      <!-- ========================================== -->
      <!-- COLUMN 1: OMNICHANNEL CONVERSATION LIST    -->
      <!-- ========================================== -->
      <aside class="w-80 lg:w-96 border-r border-slate-800 bg-slate-900/70 flex flex-col shrink-0">
        <!-- Top App Bar -->
        <div class="p-4 border-b border-slate-800 flex items-center justify-between">
          <div class="flex items-center gap-2.5">
            <div class="w-8 h-8 rounded-lg bg-gradient-to-tr from-blue-600 to-indigo-500 flex items-center justify-center font-bold text-white shadow-md shadow-blue-500/20 text-sm">
              SG
            </div>
            <div>
              <h1 class="text-sm font-semibold text-white tracking-tight flex items-center gap-1.5">
                Unified Desk
                <span class="px-1.5 py-0.5 text-[10px] font-medium bg-emerald-500/20 text-emerald-400 border border-emerald-500/30 rounded">H20</span>
              </h1>
              <p class="text-[11px] text-slate-400">Omnichannel & AI Autopilot</p>
            </div>
          </div>

          <div class="flex items-center gap-1.5">
            <button 
              (click)="loadConversations()" 
              title="Refresh"
              class="p-1.5 text-slate-400 hover:text-white hover:bg-slate-800 rounded-lg transition text-xs">
              🔄
            </button>
            <a routerLink="/" class="text-[11px] text-slate-400 hover:text-white px-2 py-1 bg-slate-800/80 hover:bg-slate-800 rounded border border-slate-700/60 transition">
              Exit
            </a>
          </div>
        </div>

        <!-- SLA Overview Metrics Bar -->
        @if (slaOverview()) {
          <div class="px-3 py-2 bg-slate-950/60 border-b border-slate-800/80 flex items-center justify-between text-[11px]">
            <div class="flex items-center gap-1.5 text-slate-400">
              <span class="w-2 h-2 rounded-full bg-blue-500"></span>
              <span>Active: <strong class="text-white font-mono">{{ slaOverview()?.totalActiveConversations }}</strong></span>
            </div>
            <div class="flex items-center gap-1.5 text-amber-400">
              <span class="w-2 h-2 rounded-full bg-amber-500 animate-pulse"></span>
              <span>On Hold: <strong class="font-mono">{{ slaOverview()?.pausedOnHoldCount }}</strong></span>
            </div>
            <div class="flex items-center gap-1.5 text-rose-400">
              <span class="w-2 h-2 rounded-full bg-rose-500"></span>
              <span>Breached: <strong class="font-mono">{{ slaOverview()?.breachedCount }}</strong></span>
            </div>
          </div>
        }

        <!-- Omnichannel Filter Tabs -->
        <div class="px-3 pt-3 pb-2 flex items-center gap-1 overflow-x-auto no-scrollbar border-b border-slate-800/60 text-xs">
          <button 
            (click)="selectChannelFilter('All')"
            [class]="selectedChannel() === 'All' ? 'bg-blue-600 text-white font-semibold' : 'bg-slate-800/70 text-slate-400 hover:text-slate-200'"
            class="px-2.5 py-1 rounded-md text-[11px] transition whitespace-nowrap">
            All Channels
          </button>
          <button 
            (click)="selectChannelFilter('WhatsApp')"
            [class]="selectedChannel() === 'WhatsApp' ? 'bg-emerald-600 text-white font-semibold' : 'bg-slate-800/70 text-slate-400 hover:text-slate-200'"
            class="px-2.5 py-1 rounded-md text-[11px] transition flex items-center gap-1 whitespace-nowrap">
            <span>💬</span> WhatsApp
          </button>
          <button 
            (click)="selectChannelFilter('Instagram')"
            [class]="selectedChannel() === 'Instagram' ? 'bg-pink-600 text-white font-semibold' : 'bg-slate-800/70 text-slate-400 hover:text-slate-200'"
            class="px-2.5 py-1 rounded-md text-[11px] transition flex items-center gap-1 whitespace-nowrap">
            <span>📸</span> Instagram
          </button>
          <button 
            (click)="selectChannelFilter('Email')"
            [class]="selectedChannel() === 'Email' ? 'bg-amber-600 text-white font-semibold' : 'bg-slate-800/70 text-slate-400 hover:text-slate-200'"
            class="px-2.5 py-1 rounded-md text-[11px] transition flex items-center gap-1 whitespace-nowrap">
            <span>✉️</span> Email
          </button>
          <button 
            (click)="selectChannelFilter('WebChat')"
            [class]="selectedChannel() === 'WebChat' ? 'bg-cyan-600 text-white font-semibold' : 'bg-slate-800/70 text-slate-400 hover:text-slate-200'"
            class="px-2.5 py-1 rounded-md text-[11px] transition flex items-center gap-1 whitespace-nowrap">
            <span>🌐</span> WebChat
          </button>
        </div>

        <!-- Search Bar -->
        <div class="p-3 border-b border-slate-800/60">
          <div class="relative">
            <span class="absolute inset-y-0 left-0 pl-2.5 flex items-center pointer-events-none text-slate-500 text-xs">🔍</span>
            <input 
              type="text" 
              [(ngModel)]="searchQuery"
              placeholder="Search conversations, tickets, clients..." 
              class="w-full pl-8 pr-3 py-1.5 bg-slate-950/80 border border-slate-800 rounded-lg text-xs text-slate-200 placeholder-slate-500 focus:outline-none focus:border-blue-500 focus:ring-1 focus:ring-blue-500 transition"
            />
          </div>
        </div>

        <!-- Thread List -->
        <div class="flex-1 overflow-y-auto divide-y divide-slate-800/40">
          @if (filteredConversations().length === 0) {
            <div class="p-6 text-center text-slate-500 text-xs">
              No conversations found.
            </div>
          }

          @for (conv of filteredConversations(); track conv.conversationId) {
            <div 
              (click)="selectConversation(conv)"
              [class]="selectedConv()?.conversationId === conv.conversationId ? 'bg-blue-600/10 border-l-4 border-l-blue-500' : 'hover:bg-slate-800/40 border-l-4 border-l-transparent'"
              class="p-3 cursor-pointer transition">
              
              <div class="flex items-start justify-between gap-2 mb-1">
                <div class="flex items-center gap-2">
                  <!-- Channel Icon Badge -->
                  <span [class]="getChannelBadgeClass(conv.channel)" class="px-1.5 py-0.5 rounded text-[10px] font-mono border font-semibold flex items-center gap-1">
                    <span>{{ getChannelIcon(conv.channel) }}</span>
                    <span>{{ conv.channel }}</span>
                  </span>

                  <!-- Customer Name -->
                  <span class="text-xs font-semibold text-white truncate max-w-[130px]">
                    {{ conv.customerName }}
                  </span>
                </div>

                <!-- Priority Badge -->
                <span [class]="getPriorityClass(conv.priority)" class="px-1.5 py-0.5 rounded text-[9px] uppercase font-mono font-bold">
                  {{ getPriorityLabel(conv.priority) }}
                </span>
              </div>

              <!-- Message Snippet -->
              <p class="text-[11px] text-slate-400 truncate mb-2">
                {{ conv.snippet }}
              </p>

              <!-- Footer with SLA Pill and Collision Warning -->
              <div class="flex items-center justify-between text-[10px]">
                <!-- SLA Pill -->
                <div class="flex items-center gap-1">
                  @if (conv.resolutionSla.isPaused) {
                    <span class="px-1.5 py-0.5 rounded-full bg-amber-500/20 text-amber-300 border border-amber-500/30 font-medium flex items-center gap-1">
                      <span>⏸️</span> Hold
                    </span>
                  } @else if (conv.resolutionSla.isBreached) {
                    <span class="px-1.5 py-0.5 rounded-full bg-rose-500/20 text-rose-300 border border-rose-500/30 font-medium flex items-center gap-1">
                      <span>⚠️</span> Breached
                    </span>
                  } @else {
                    <span class="px-1.5 py-0.5 rounded-full bg-emerald-500/20 text-emerald-300 border border-emerald-500/30 font-medium flex items-center gap-1">
                      <span>⏱️</span> {{ conv.resolutionSla.minutesRemaining }}m left
                    </span>
                  }
                </div>

                <!-- Agent Collision Badge if applicable -->
                @if (conv.collisionWarning?.hasCollision) {
                  <span class="px-1.5 py-0.5 rounded bg-amber-500/20 text-amber-300 border border-amber-500/30 font-semibold text-[9px] flex items-center gap-1" title="Multiple agents viewing">
                    <span>👥</span> Multi-view
                  </span>
                }
              </div>
            </div>
          }
        </div>
      </aside>

      <!-- ========================================== -->
      <!-- COLUMN 2: CENTER THREAD & COLLISION PANE   -->
      <!-- ========================================== -->
      <main class="flex-1 flex flex-col bg-slate-950 border-r border-slate-800 min-w-0">
        @if (selectedConvDetail()) {
          <!-- Chat Header -->
          <header class="h-16 px-6 border-b border-slate-800 bg-slate-900/60 flex items-center justify-between shrink-0">
            <div class="flex items-center gap-3">
              <div class="w-10 h-10 rounded-full bg-gradient-to-tr from-slate-800 to-slate-700 border border-slate-700 flex items-center justify-center font-bold text-slate-200 text-sm">
                {{ getInitials(selectedConvDetail()?.summary?.customerName || 'Customer') }}
              </div>
              <div>
                <div class="flex items-center gap-2">
                  <h2 class="text-sm font-semibold text-white">
                    {{ selectedConvDetail()?.summary?.customerName }}
                  </h2>
                  <span [class]="getChannelBadgeClass(selectedConvDetail()?.summary?.channel || '')" class="px-1.5 py-0.5 rounded text-[10px] font-mono border font-semibold">
                    {{ selectedConvDetail()?.summary?.channel }}
                  </span>
                </div>
                <p class="text-[11px] text-slate-400">
                  {{ selectedConvDetail()?.summary?.customerContactInfo }}
                </p>
              </div>
            </div>

            <!-- Status & Presence Controls -->
            <div class="flex items-center gap-3">
              <!-- Active Presence Pill -->
              @if (selectedConvDetail()?.presence?.activeAgents?.length) {
                <div class="hidden sm:flex items-center gap-1.5 bg-slate-800/80 px-2.5 py-1 rounded-full border border-slate-700 text-xs text-slate-300">
                  <span class="w-2 h-2 rounded-full bg-emerald-500 animate-pulse"></span>
                  <span class="text-[11px]">
                    {{ selectedConvDetail()?.presence?.activeAgents?.length }} Active
                  </span>
                </div>
              }

              <!-- Ticket Status Switcher (Pause-on-Hold) -->
              <div class="flex items-center gap-1.5 text-xs">
                <span class="text-slate-400 text-[11px]">Status:</span>
                <select 
                  [ngModel]="selectedConvDetail()?.summary?.status"
                  (ngModelChange)="onStatusChange($event)"
                  class="bg-slate-900 border border-slate-700 rounded-lg px-2.5 py-1 text-xs text-slate-200 focus:outline-none focus:border-blue-500">
                  <option [value]="1">🟢 Open</option>
                  <option [value]="4">⏸️ Waiting on Customer (Hold)</option>
                  <option [value]="3">✅ Resolved</option>
                </select>
              </div>
            </div>
          </header>

          <!-- COLLISION WARNING BANNER -->
          @if (selectedConvDetail()?.presence?.hasCollision) {
            <div class="bg-amber-950/70 border-b border-amber-600/40 px-6 py-2.5 flex items-center justify-between text-amber-200 text-xs">
              <div class="flex items-center gap-2">
                <span class="text-base">⚠️</span>
                <span>
                  <strong>Collision Warning:</strong> Multiple operators are actively viewing this ticket. Please coordinate before sending replies.
                </span>
              </div>
              <span class="bg-amber-500/20 border border-amber-500/30 px-2 py-0.5 rounded text-[10px] font-mono text-amber-300">
                Live Guard Active
              </span>
            </div>
          }

          <!-- SOFT-LOCK WARNING BANNER (When another agent replied < 60s ago) -->
          @if (softLockAlert()) {
            <div class="bg-rose-950/80 border-b border-rose-600/40 px-6 py-3 flex items-center justify-between text-rose-200 text-xs">
              <div class="flex items-center gap-2.5">
                <span class="text-lg">🛑</span>
                <div>
                  <p class="font-semibold">{{ softLockAlert() }}</p>
                  <p class="text-[11px] text-rose-300">Sending duplicate replies to the same customer causes confusion and drops CSAT.</p>
                </div>
              </div>
              <div class="flex items-center gap-2">
                <button 
                  (click)="cancelSoftLock()"
                  class="px-2.5 py-1 bg-slate-800 hover:bg-slate-700 text-slate-300 rounded text-xs transition">
                  Cancel
                </button>
                <button 
                  (click)="bypassAndSend()"
                  class="px-3 py-1 bg-rose-600 hover:bg-rose-500 text-white font-semibold rounded text-xs transition">
                  Confirm & Force Send
                </button>
              </div>
            </div>
          }

          <!-- Message Thread List -->
          <div class="flex-1 overflow-y-auto p-6 space-y-4">
            @for (msg of selectedConvDetail()?.messages; track msg.id) {
              <!-- Customer Message (Left) -->
              @if (msg.senderType === 1) {
                <div class="flex flex-col items-start max-w-[80%]">
                  <span class="text-[10px] text-slate-400 mb-1 ml-1 font-medium">
                    {{ selectedConvDetail()?.summary?.customerName }} • {{ formatTime(msg.createdAtUtc) }}
                  </span>
                  <div class="bg-slate-800 text-slate-100 px-4 py-2.5 rounded-2xl rounded-tl-sm border border-slate-700/60 shadow-sm text-xs leading-relaxed">
                    {{ msg.content }}
                  </div>
                </div>
              }

              <!-- Agent Message (Right) -->
              @else if (msg.senderType === 2 && !msg.isPrivateNote) {
                <div class="flex flex-col items-end max-w-[80%] ml-auto">
                  <span class="text-[10px] text-slate-400 mb-1 mr-1 font-medium">
                    {{ msg.senderName || 'Support Agent' }} • {{ formatTime(msg.createdAtUtc) }}
                  </span>
                  <div class="bg-blue-600 text-white px-4 py-2.5 rounded-2xl rounded-tr-sm shadow-sm text-xs leading-relaxed">
                    {{ msg.content }}
                  </div>
                </div>
              }

              <!-- AI Autopilot Resolution (Left/Center) -->
              @else if (msg.senderType === 3) {
                <div class="flex flex-col items-start max-w-[85%] mx-auto w-full my-2">
                  <div class="w-full bg-emerald-950/40 border border-emerald-500/40 rounded-xl p-3 shadow-md">
                    <div class="flex items-center justify-between text-[11px] text-emerald-400 mb-1.5 font-semibold">
                      <span class="flex items-center gap-1.5">
                        <span>🤖</span> AI Autopilot Resolution
                      </span>
                      <span class="bg-emerald-500/20 text-emerald-300 text-[10px] font-mono px-2 py-0.5 rounded border border-emerald-500/30">
                        Autonomous • 98% Match
                      </span>
                    </div>
                    <p class="text-xs text-emerald-100 leading-relaxed font-sans">
                      {{ msg.content }}
                    </p>
                    <div class="mt-2 text-[10px] text-emerald-400/80 flex items-center gap-1">
                      <span>⚡</span> Ticket placed on Pause-on-Hold awaiting customer response.
                    </div>
                  </div>
                </div>
              }

              <!-- Private Internal Note (Yellow/Amber Padlock) -->
              @else if (msg.isPrivateNote || msg.senderType === 5) {
                <div class="flex flex-col items-center max-w-[90%] mx-auto w-full my-1">
                  <div class="w-full bg-amber-950/30 border border-amber-600/40 rounded-xl p-3 shadow-sm">
                    <div class="flex items-center justify-between text-[11px] text-amber-400 mb-1 font-semibold">
                      <span class="flex items-center gap-1.5">
                        <span>🔒</span> Private Internal Note (Visible to Team Only)
                      </span>
                      <span class="text-[10px] text-slate-400">
                        {{ formatTime(msg.createdAtUtc) }}
                      </span>
                    </div>
                    <p class="text-xs text-amber-200 leading-relaxed font-mono">
                      {{ msg.content }}
                    </p>
                  </div>
                </div>
              }
            }
          </div>

          <!-- Typing Indicator -->
          @if (isOtherAgentTyping()) {
            <div class="px-6 py-1.5 text-xs text-amber-300/90 italic flex items-center gap-2">
              <span class="w-1.5 h-1.5 rounded-full bg-amber-400 animate-ping"></span>
              <span>Another agent is currently typing...</span>
            </div>
          }

          <!-- Message Composer Area -->
          <footer class="p-4 border-t border-slate-800 bg-slate-900/60">
            <!-- Composer Mode Toggle & Quick Action Chips -->
            <div class="flex items-center justify-between mb-2">
              <div class="flex items-center gap-2">
                <button 
                  (click)="isPrivateNote.set(false)"
                  [class]="!isPrivateNote() ? 'bg-blue-600 text-white font-semibold' : 'bg-slate-800 text-slate-400 hover:text-slate-200'"
                  class="px-2.5 py-1 rounded text-xs transition flex items-center gap-1">
                  <span>💬</span> Customer Reply
                </button>
                <button 
                  (click)="isPrivateNote.set(true)"
                  [class]="isPrivateNote() ? 'bg-amber-600 text-white font-semibold' : 'bg-slate-800 text-slate-400 hover:text-slate-200'"
                  class="px-2.5 py-1 rounded text-xs transition flex items-center gap-1">
                  <span>🔒</span> Internal Note
                </button>
              </div>

              <!-- Quick Canned Chips -->
              <div class="hidden md:flex items-center gap-1.5 text-[11px]">
                <button (click)="applyCanned('track')" class="px-2 py-0.5 bg-slate-800 hover:bg-slate-700 text-slate-300 rounded border border-slate-700/60 transition">
                  Tracking
                </button>
                <button (click)="applyCanned('return')" class="px-2 py-0.5 bg-slate-800 hover:bg-slate-700 text-slate-300 rounded border border-slate-700/60 transition">
                  Return Policy
                </button>
                <button (click)="applyCanned('hold')" class="px-2 py-0.5 bg-slate-800 hover:bg-slate-700 text-slate-300 rounded border border-slate-700/60 transition">
                  Hold Address
                </button>
              </div>
            </div>

            <!-- Text Input Area -->
            <div class="flex items-end gap-2">
              <textarea 
                [(ngModel)]="composerText"
                (input)="onTyping()"
                (keydown.control.enter)="sendMessage()"
                [placeholder]="isPrivateNote() ? 'Write private internal note (not visible to customer)...' : 'Type your reply (Ctrl+Enter to send)...'"
                rows="2"
                [class]="isPrivateNote() ? 'border-amber-500/50 focus:border-amber-500' : 'border-slate-700 focus:border-blue-500'"
                class="flex-1 bg-slate-950 border rounded-xl px-3.5 py-2 text-xs text-slate-100 placeholder-slate-500 focus:outline-none focus:ring-1 transition resize-none"></textarea>

              <button 
                (click)="sendMessage()"
                [disabled]="!composerText.trim()"
                [class]="isPrivateNote() ? 'bg-amber-600 hover:bg-amber-500' : 'bg-blue-600 hover:bg-blue-500'"
                class="px-4 py-2 rounded-xl text-xs font-semibold text-white transition disabled:opacity-50 disabled:cursor-not-allowed shadow-md h-10 flex items-center gap-1.5">
                <span>{{ isPrivateNote() ? 'Save Note' : 'Send' }}</span>
                <span>➤</span>
              </button>
            </div>
          </footer>
        } @else {
          <!-- Empty State -->
          <div class="flex-1 flex flex-col items-center justify-center p-8 text-center text-slate-500">
            <span class="text-4xl mb-3">📬</span>
            <h3 class="text-sm font-semibold text-slate-300 mb-1">Select a Conversation</h3>
            <p class="text-xs max-w-sm">
              Choose an omnichannel thread from the left to start responding, prevent collision, or view AI suggestions.
            </p>
          </div>
        }
      </main>

      <!-- ========================================== -->
      <!-- COLUMN 3: CUSTOMER 360 & AI COPILOT PANE   -->
      <!-- ========================================== -->
      <aside class="w-80 lg:w-96 bg-slate-900/60 flex flex-col shrink-0 overflow-y-auto divide-y divide-slate-800/80">
        @if (selectedConvDetail()) {
          <!-- 1. Customer 360 Overview -->
          <section class="p-4 space-y-3">
            <div class="flex items-center justify-between">
              <h3 class="text-xs font-semibold text-white uppercase tracking-wider flex items-center gap-1.5">
                <span>👤</span> Customer 360
              </h3>
              <span class="px-2 py-0.5 rounded-full text-[10px] font-semibold bg-indigo-500/20 text-indigo-300 border border-indigo-500/30">
                {{ selectedConvDetail()?.customer360?.lifecycleStage }}
              </span>
            </div>

            <div class="grid grid-cols-3 gap-2 text-center">
              <div class="p-2 rounded-lg bg-slate-950 border border-slate-800">
                <span class="block text-[10px] text-slate-400">Total Orders</span>
                <strong class="text-sm font-bold text-white font-mono">{{ selectedConvDetail()?.customer360?.totalOrders }}</strong>
              </div>
              <div class="p-2 rounded-lg bg-slate-950 border border-slate-800">
                <span class="block text-[10px] text-slate-400">Total Spent</span>
                <strong class="text-sm font-bold text-emerald-400 font-mono">{{ selectedConvDetail()?.customer360?.totalSpent }} DT</strong>
              </div>
              <div class="p-2 rounded-lg bg-slate-950 border border-slate-800">
                <span class="block text-[10px] text-slate-400">Delivery Rate</span>
                <strong class="text-sm font-bold text-blue-400 font-mono">{{ selectedConvDetail()?.customer360?.deliverySuccessRate }}%</strong>
              </div>
            </div>

            <!-- Recent Commercial Orders -->
            <div class="space-y-1.5">
              <span class="text-[11px] font-semibold text-slate-400 block">Recent Commercial Orders</span>
              @for (order of selectedConvDetail()?.customer360?.recentOrders; track order.orderNumber) {
                <div class="p-2 rounded-lg bg-slate-950 border border-slate-800/90 text-xs flex items-center justify-between">
                  <div>
                    <div class="flex items-center gap-1.5">
                      <strong class="text-white">{{ order.orderNumber }}</strong>
                      <span class="text-[10px] px-1.5 py-0.2 rounded bg-slate-800 text-slate-300 border border-slate-700">
                        {{ order.status }}
                      </span>
                    </div>
                    <span class="text-[10px] text-slate-400">
                      {{ order.courierName }} • Waybill: <code class="text-blue-400">{{ order.trackingNumber }}</code>
                    </span>
                  </div>
                  <strong class="text-xs font-mono text-emerald-400">
                    {{ order.totalAmount }} {{ order.currency }}
                  </strong>
                </div>
              }
            </div>
          </section>

          <!-- 2. AI Copilot Suggestions -->
          <section class="p-4 space-y-3">
            <div class="flex items-center justify-between">
              <h3 class="text-xs font-semibold text-white uppercase tracking-wider flex items-center gap-1.5">
                <span>⚡</span> AI Copilot Recommendations
              </h3>
              <span class="px-1.5 py-0.5 rounded text-[10px] font-mono bg-blue-500/20 text-blue-300 border border-blue-500/30">
                3 Suggested
              </span>
            </div>

            <div class="space-y-2.5">
              @for (sug of selectedConvDetail()?.copilotSuggestions; track sug.id) {
                <div class="p-3 rounded-xl bg-slate-950 border border-slate-800 hover:border-blue-500/40 transition text-xs space-y-2">
                  <div class="flex items-center justify-between">
                    <strong class="text-slate-200 text-[11px]">{{ sug.title }}</strong>
                    <span class="text-[10px] font-mono text-emerald-400 bg-emerald-500/10 px-1.5 py-0.2 rounded">
                      {{ (sug.confidenceScore * 100).toFixed(0) }}% match
                    </span>
                  </div>
                  <p class="text-[11px] text-slate-400 leading-relaxed">
                    {{ sug.suggestedText }}
                  </p>
                  <div class="flex items-center justify-between pt-1 border-t border-slate-900 text-[10px]">
                    <span class="text-slate-500 truncate max-w-[150px]" [title]="sug.groundingSource">
                      📖 {{ sug.groundingSource }}
                    </span>
                    <button 
                      (click)="useCopilotSuggestion(sug.suggestedText)"
                      class="px-2 py-0.5 bg-blue-600/30 hover:bg-blue-600 text-blue-300 hover:text-white rounded border border-blue-500/40 transition font-medium">
                      Use Reply
                    </button>
                  </div>
                </div>
              }
            </div>
          </section>

          <!-- 3. Transactional Autopilot Simulator -->
          <section class="p-4 space-y-3 bg-gradient-to-b from-transparent to-slate-950">
            <div class="flex items-center justify-between">
              <h3 class="text-xs font-semibold text-white uppercase tracking-wider flex items-center gap-1.5">
                <span>🤖</span> Autopilot Simulator
              </h3>
              <label class="relative inline-flex items-center cursor-pointer">
                <input type="checkbox" [(ngModel)]="autopilotEnabled" class="sr-only peer">
                <div class="w-8 h-4 bg-slate-800 peer-focus:outline-none rounded-full peer peer-checked:after:translate-x-full peer-checked:after:border-white after:content-[''] after:absolute after:top-[2px] after:left-[2px] after:bg-white after:border-slate-300 after:border after:rounded-full after:h-3 after:w-3 after:transition-all peer-checked:bg-emerald-600"></div>
              </label>
            </div>

            <p class="text-[11px] text-slate-400 leading-relaxed">
              Trigger high-frequency transactional inquiries to test automatic intent recognition and SLA Pause-on-Hold resolution.
            </p>

            <div class="grid grid-cols-2 gap-2 text-xs">
              <button 
                (click)="simulateAutopilot('Where is my shipment for order #ORD-7721?')"
                class="p-2 rounded-lg bg-slate-950 border border-slate-800 hover:border-emerald-500/40 hover:bg-emerald-950/20 text-left transition">
                <span class="block font-medium text-emerald-400">📦 Shipment Status</span>
                <span class="text-[10px] text-slate-500">Live courier tracking</span>
              </button>

              <button 
                (click)="simulateAutopilot('What is my order tracking number?')"
                class="p-2 rounded-lg bg-slate-950 border border-slate-800 hover:border-emerald-500/40 hover:bg-emerald-950/20 text-left transition">
                <span class="block font-medium text-blue-400">🏷️ Tracking Code</span>
                <span class="text-[10px] text-slate-500">Waybill lookup</span>
              </button>

              <button 
                (click)="simulateAutopilot('Update delivery address before courier pickup')"
                class="p-2 rounded-lg bg-slate-950 border border-slate-800 hover:border-emerald-500/40 hover:bg-emerald-950/20 text-left transition">
                <span class="block font-medium text-amber-400">📍 Address Update</span>
                <span class="text-[10px] text-slate-500">Pre-pickup lock</span>
              </button>

              <button 
                (click)="simulateAutopilot('What is your return policy and warranty?')"
                class="p-2 rounded-lg bg-slate-950 border border-slate-800 hover:border-emerald-500/40 hover:bg-emerald-950/20 text-left transition">
                <span class="block font-medium text-pink-400">🔄 Return Policy</span>
                <span class="text-[10px] text-slate-500">14-day warranty</span>
              </button>
            </div>

            @if (lastAutopilotResolution()) {
              <div class="p-2.5 rounded-lg bg-emerald-950/40 border border-emerald-600/30 text-[11px] space-y-1">
                <div class="flex items-center justify-between text-emerald-300 font-semibold">
                  <span>Intent: {{ lastAutopilotResolution()?.intent }}</span>
                  <span class="font-mono">{{ ((lastAutopilotResolution()?.confidenceScore || 0) * 100).toFixed(0) }}%</span>
                </div>
                <p class="text-slate-300 text-[10px]">
                  {{ lastAutopilotResolution()?.actionTaken }}
                </p>
              </div>
            }
          </section>
        } @else {
          <div class="p-6 text-center text-slate-500 text-xs">
            No active conversation selected.
          </div>
        }
      </aside>

    </div>
  `
})
export class OmnichannelUnifiedDeskComponent implements OnInit {
  private api = inject(ApiService);

  // Signals
  public conversations = signal<UnifiedConversationSummaryDto[]>([]);
  public selectedConv = signal<UnifiedConversationSummaryDto | null>(null);
  public selectedConvDetail = signal<UnifiedConversationDetailDto | null>(null);
  public slaOverview = signal<SlaOverviewReportDto | null>(null);

  public selectedChannel = signal<string>('All');
  public searchQuery = '';
  public isPrivateNote = signal<boolean>(false);
  public composerText = '';
  public autopilotEnabled = true;

  public softLockAlert = signal<string | null>(null);
  public lastAutopilotResolution = signal<AutopilotResolutionDto | null>(null);

  // Computed
  public filteredConversations = computed(() => {
    let list = this.conversations();
    const query = this.searchQuery.trim().toLowerCase();

    if (query) {
      list = list.filter(c =>
        c.customerName.toLowerCase().includes(query) ||
        c.snippet.toLowerCase().includes(query) ||
        (c.customerContactInfo && c.customerContactInfo.toLowerCase().includes(query))
      );
    }

    return list;
  });

  public isOtherAgentTyping = computed(() => {
    const detail = this.selectedConvDetail();
    if (!detail?.presence?.activeAgents) return false;
    return detail.presence.activeAgents.some(a => a.isTyping && a.agentId !== 'agent-current');
  });

  public ngOnInit(): void {
    this.loadConversations();
    this.loadSlaOverview();
  }

  public loadConversations(): void {
    const channel = this.selectedChannel() === 'All' ? undefined : this.selectedChannel();
    this.api.getUnifiedDeskConversations(channel).subscribe({
      next: (data) => {
        this.conversations.set(data);
        if (data.length > 0 && !this.selectedConv()) {
          this.selectConversation(data[0]);
        }
      },
      error: (err) => console.error('Failed to load unified conversations:', err)
    });
  }

  public loadSlaOverview(): void {
    this.api.getUnifiedDeskSlaOverview().subscribe({
      next: (data) => this.slaOverview.set(data),
      error: (err) => console.error('Failed to load SLA overview:', err)
    });
  }

  public selectChannelFilter(channel: string): void {
    this.selectedChannel.set(channel);
    this.loadConversations();
  }

  public selectConversation(conv: UnifiedConversationSummaryDto): void {
    this.selectedConv.set(conv);
    this.softLockAlert.set(null);

    this.api.getUnifiedDeskConversationDetail(conv.conversationId).subscribe({
      next: (detail) => {
        this.selectedConvDetail.set(detail);

        // Heartbeat presence for this agent
        this.api.updateUnifiedDeskPresence(conv.conversationId, {
          agentId: 'agent-current',
          agentName: 'Agent Sarah',
          isTyping: false
        }).subscribe({
          next: (presence) => {
            if (this.selectedConvDetail()) {
              this.selectedConvDetail.update(d => d ? { ...d, presence } : null);
            }
          }
        });
      },
      error: (err) => console.error('Failed to load conversation detail:', err)
    });
  }

  public onTyping(): void {
    const convId = this.selectedConv()?.conversationId;
    if (!convId) return;

    this.api.updateUnifiedDeskPresence(convId, {
      agentId: 'agent-current',
      agentName: 'Agent Sarah',
      isTyping: this.composerText.length > 0
    }).subscribe();
  }

  public sendMessage(bypassSoftLock: boolean = false): void {
    const text = this.composerText.trim();
    const conv = this.selectedConv();
    if (!text || !conv) return;

    this.api.sendUnifiedDeskMessage(conv.conversationId, {
      content: text,
      isPrivateNote: this.isPrivateNote(),
      senderName: 'Agent Sarah',
      senderId: 'agent-current',
      bypassSoftLock
    }).subscribe({
      next: (resp) => {
        if (resp.softLockTriggered) {
          this.softLockAlert.set(resp.softLockWarning || 'Soft-lock alert triggered.');
          return;
        }

        this.composerText = '';
        this.softLockAlert.set(null);

        // Refresh detail
        this.selectConversation(conv);
        this.loadConversations();
      },
      error: (err) => {
        if (err.status === 409 && err.error?.softLockTriggered) {
          this.softLockAlert.set(err.error.softLockWarning);
        } else {
          console.error('Failed to send message:', err);
        }
      }
    });
  }

  public bypassAndSend(): void {
    this.sendMessage(true);
  }

  public cancelSoftLock(): void {
    this.softLockAlert.set(null);
  }

  public useCopilotSuggestion(suggestedText: string): void {
    this.composerText = suggestedText;
    this.isPrivateNote.set(false);
  }

  public applyCanned(type: string): void {
    if (type === 'track') {
      this.composerText = 'Your shipment is in transit with Express Courier. Tracking code: TN-99218. Delivery is expected tomorrow.';
    } else if (type === 'return') {
      this.composerText = 'We provide a 14-day return and size exchange policy. Would you like a courier pickup arranged?';
    } else if (type === 'hold') {
      this.composerText = 'Address modification lock applied. Please share your updated delivery address.';
    }
  }

  public onStatusChange(newStatus: number): void {
    const conv = this.selectedConv();
    if (!conv) return;

    this.api.updateUnifiedDeskStatus(conv.conversationId, newStatus).subscribe({
      next: () => {
        this.selectConversation(conv);
        this.loadConversations();
        this.loadSlaOverview();
      },
      error: (err) => console.error('Failed to update status:', err)
    });
  }

  public simulateAutopilot(customerInquiry: string): void {
    const conv = this.selectedConv();
    if (!conv) return;

    // 1. Send simulated customer message
    this.api.sendUnifiedDeskMessage(conv.conversationId, {
      content: customerInquiry,
      senderType: 1 // Contact
    }).subscribe({
      next: () => {
        // 2. Trigger AI Autopilot resolution
        this.api.resolveUnifiedDeskAutopilot(conv.conversationId, customerInquiry, true).subscribe({
          next: (resolution) => {
            this.lastAutopilotResolution.set(resolution);
            this.selectConversation(conv);
            this.loadConversations();
            this.loadSlaOverview();
          },
          error: (err) => console.error('Failed to resolve autopilot:', err)
        });
      }
    });
  }

  public getChannelIcon(channel: string): string {
    switch (channel?.toLowerCase()) {
      case 'whatsapp': return '💬';
      case 'instagram': return '📸';
      case 'messenger': return '🔵';
      case 'email': return '✉️';
      case 'webchat': return '🌐';
      default: return '💬';
    }
  }

  public getChannelBadgeClass(channel: string): string {
    switch (channel?.toLowerCase()) {
      case 'whatsapp': return 'bg-emerald-500/20 text-emerald-400 border-emerald-500/30';
      case 'instagram': return 'bg-pink-500/20 text-pink-400 border-pink-500/30';
      case 'messenger': return 'bg-blue-500/20 text-blue-400 border-blue-500/30';
      case 'email': return 'bg-amber-500/20 text-amber-400 border-amber-500/30';
      case 'webchat': return 'bg-cyan-500/20 text-cyan-400 border-cyan-500/30';
      default: return 'bg-slate-500/20 text-slate-400 border-slate-500/30';
    }
  }

  public getPriorityLabel(priority: number): string {
    switch (priority) {
      case 4: return 'Urgent';
      case 3: return 'High';
      case 2: return 'Normal';
      default: return 'Low';
    }
  }

  public getPriorityClass(priority: number): string {
    switch (priority) {
      case 4: return 'bg-rose-500/20 text-rose-400 border border-rose-500/30';
      case 3: return 'bg-orange-500/20 text-orange-400 border border-orange-500/30';
      case 2: return 'bg-slate-800 text-slate-300 border border-slate-700';
      default: return 'bg-zinc-800 text-zinc-400 border border-zinc-700';
    }
  }

  public getInitials(name: string): string {
    return name
      .split(' ')
      .map(n => n[0])
      .join('')
      .toUpperCase()
      .substring(0, 2);
  }

  public formatTime(utcString?: string): string {
    if (!utcString) return '';
    const date = new Date(utcString);
    return date.toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' });
  }
}
