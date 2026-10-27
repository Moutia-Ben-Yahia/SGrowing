import { Component, signal, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { ApiService } from '../../core/services/api.service';
import { BotSimulationSession, BotSimulationStepResult } from '../../core/models/pillars.models';

@Component({
  selector: 'app-bot-simulator-runner',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterLink],
  template: `
    <div class="flex h-screen w-screen overflow-hidden bg-slate-950 text-slate-100 font-sans">
      <main class="flex-1 flex flex-col overflow-y-auto">
        <!-- Topbar -->
        <header class="h-14 border-b border-slate-800 bg-slate-900/60 px-6 flex items-center justify-between shrink-0">
          <div class="flex items-center gap-3">
            <a routerLink="/chatbots" class="text-xs text-slate-400 hover:text-white flex items-center gap-1 transition">
              ← Bot Canvas
            </a>
            <span class="text-slate-700">|</span>
            <span class="text-base font-semibold text-white flex items-center gap-2">
              🤖 Interactive Bot Flow Simulator & Step Runner
            </span>
            <span class="bg-purple-500/20 text-purple-400 text-[10px] font-mono px-2 py-0.5 rounded-full border border-purple-500/30">
              In-Memory Runner • JSONPath • Carousels
            </span>
          </div>

          <button
            (click)="resetSimulation()"
            class="px-3.5 py-1.5 bg-slate-800 hover:bg-slate-700 text-white rounded-lg text-xs font-semibold transition">
            🔄 Restart Simulation
          </button>
        </header>

        <!-- Body -->
        <div class="p-6 space-y-6 flex-1 grid grid-cols-1 lg:grid-cols-3 gap-6">
          <!-- Left: Simulated Chat Device Window -->
          <div class="lg:col-span-2 flex flex-col h-[650px] rounded-2xl bg-slate-900/80 border border-slate-800 overflow-hidden shadow-2xl">
            <!-- Header bar of simulated chat -->
            <div class="p-3.5 bg-slate-950 border-b border-slate-800 flex items-center justify-between">
              <div class="flex items-center gap-2.5">
                <div class="w-8 h-8 rounded-full bg-purple-600/30 border border-purple-500/40 flex items-center justify-center text-sm">
                  🤖
                </div>
                <div>
                  <div class="text-xs font-bold text-white">SGrowing Intelligent Assistant</div>
                  <span class="text-[10px] text-emerald-400 flex items-center gap-1">
                    <span class="w-1.5 h-1.5 rounded-full bg-emerald-400 animate-pulse"></span> Flow Simulator Active
                  </span>
                </div>
              </div>

              @if (session()?.isHandedOverToHuman) {
                <span class="px-2.5 py-1 rounded bg-rose-500/20 text-rose-300 border border-rose-500/30 text-[10px] font-bold">
                  ⚠️ Handed Over to Live Human Agent
                </span>
              }
            </div>

            <!-- Messages Transcript -->
            <div class="flex-1 overflow-y-auto p-4 space-y-3 bg-slate-950/60">
              @for (msg of transcript(); track $index) {
                <div class="flex flex-col" [class.items-end]="msg.sender === 'User'" [class.items-start]="msg.sender === 'Bot'">
                  <div class="flex items-center gap-1.5 mb-0.5">
                    <span class="text-[10px] text-slate-500">{{ msg.sender }}</span>
                  </div>

                  @if (msg.isTypingIndicator) {
                    <div class="p-2.5 rounded-2xl bg-slate-800/80 border border-slate-700/50 text-slate-400 text-xs flex gap-1">
                      <span class="animate-bounce">●</span>
                      <span class="animate-bounce delay-100">●</span>
                      <span class="animate-bounce delay-200">●</span>
                    </div>
                  } @else {
                    <div
                      class="max-w-md p-3 rounded-2xl text-xs leading-relaxed"
                      [class.bg-purple-600]="msg.sender === 'User'"
                      [class.text-white]="msg.sender === 'User'"
                      [class.bg-slate-800]="msg.sender === 'Bot'"
                      [class.text-slate-100]="msg.sender === 'Bot'">
                      {{ msg.content }}
                    </div>
                  }

                  <!-- Carousel Cards Rendering if present -->
                  @if (msg.cards && msg.cards.length > 0) {
                    <div class="flex gap-3 overflow-x-auto py-2 max-w-full">
                      @for (card of msg.cards; track card.title) {
                        <div class="w-56 shrink-0 p-3 rounded-xl bg-slate-900 border border-slate-800 space-y-2">
                          @if (card.imageUrl) {
                            <div class="h-24 rounded bg-slate-800 flex items-center justify-center text-2xl text-slate-500">
                              🖼️
                            </div>
                          }
                          <div class="text-xs font-bold text-white">{{ card.title }}</div>
                          <div class="text-[11px] text-slate-400 line-clamp-2">{{ card.subtitle }}</div>
                          <div class="pt-1 space-y-1">
                            @for (btn of card.buttons; track btn.title) {
                              <button
                                (click)="sendCardChoice(btn.title)"
                                class="w-full py-1 text-[11px] bg-purple-600/30 hover:bg-purple-600 text-purple-200 hover:text-white rounded border border-purple-500/40 transition">
                                {{ btn.title }}
                              </button>
                            }
                          </div>
                        </div>
                      }
                    </div>
                  }
                </div>
              }
            </div>

            <!-- Input Bar -->
            <div class="p-3 bg-slate-950 border-t border-slate-800 flex gap-2">
              <input
                type="text"
                [(ngModel)]="userMessageInput"
                (keyup.enter)="sendUserMessage()"
                [disabled]="isStepping() || !!session()?.isHandedOverToHuman"
                placeholder="Type a response to the bot..."
                class="flex-1 bg-slate-900 border border-slate-800 rounded-xl px-4 py-2 text-xs text-white focus:outline-none focus:border-purple-500" />
              <button
                (click)="sendUserMessage()"
                [disabled]="isStepping() || !userMessageInput.trim()"
                class="px-4 py-2 bg-purple-600 hover:bg-purple-500 disabled:opacity-50 text-white rounded-xl text-xs font-semibold transition">
                Send
              </button>
            </div>
          </div>

          <!-- Right: Execution Engine Inspector (Variables & Logs) -->
          <div class="space-y-4">
            <!-- State Variables -->
            <div class="p-4 rounded-xl bg-slate-900/60 border border-slate-800 space-y-3">
              <h4 class="text-xs font-semibold text-white uppercase tracking-wider">Live State Variables</h4>
              <div class="p-3 bg-slate-950 rounded-lg border border-slate-800/80 font-mono text-[11px] text-purple-300 max-h-48 overflow-y-auto whitespace-pre-wrap">
                {{ formatJson(session()?.variables) }}
              </div>
            </div>

            <!-- Execution Logs -->
            <div class="p-4 rounded-xl bg-slate-900/60 border border-slate-800 space-y-3">
              <h4 class="text-xs font-semibold text-white uppercase tracking-wider">Engine Step Trace Logs</h4>
              <div class="p-3 bg-slate-950 rounded-lg border border-slate-800/80 font-mono text-[10px] text-slate-400 space-y-1 max-h-72 overflow-y-auto">
                @for (log of traceLogs(); track $index) {
                  <div>{{ log }}</div>
                }
              </div>
            </div>
          </div>
        </div>
      </main>
    </div>
  `
})
export class BotSimulatorRunnerComponent {
  private api = inject(ApiService);

  public nodesJson = JSON.stringify([
    {
      id: 'node_start',
      type: 'Trigger',
      name: 'User Enters Chat',
      next: 'node_typing'
    },
    {
      id: 'node_typing',
      type: 'Typing',
      name: 'Typing Bubble',
      config: { delaySeconds: 1 },
      next: 'node_welcome'
    },
    {
      id: 'node_welcome',
      type: 'SendMessage',
      name: 'Welcome Greeting',
      config: { message: 'Hello! I am your SGrowing AI Copilot. Which revenue engine are you looking to activate today?' },
      next: 'node_carousel'
    },
    {
      id: 'node_carousel',
      type: 'CarouselCard',
      name: 'Product Catalog',
      config: {
        title: 'Choose a pillar:',
        cards: [
          { title: 'Cold Outreach SDR', subtitle: 'Deterministic spintax & verified delivery', buttons: [{ title: 'Explore Outreach', value: 'outreach' }] },
          { title: 'COD Logistics & Anti-Fraud', subtitle: 'Doorstep carrier routing & fraud shield', buttons: [{ title: 'Explore COD', value: 'cod' }] }
        ]
      }
    }
  ]);

  public userMessageInput = '';
  public isStepping = signal(false);
  public session = signal<BotSimulationSession | null>(null);
  public transcript = signal<any[]>([]);
  public traceLogs = signal<string[]>([]);

  constructor() {
    this.resetSimulation();
  }

  public resetSimulation() {
    this.api.startBotSimulation({
      flowName: 'InteractiveDemoBot',
      nodesJson: this.nodesJson,
      initialVariables: { userRole: 'Growth Leader' }
    }).subscribe({
      next: s => {
        this.session.set(s);
        this.transcript.set([...s.transcript]);
        this.traceLogs.set(['[Simulator Initialized] Session ready.']);
      }
    });
  }

  public sendUserMessage() {
    if (!this.userMessageInput.trim() || !this.session()) return;
    const msg = this.userMessageInput.trim();
    this.userMessageInput = '';
    this.executeStep(msg);
  }

  public sendCardChoice(choice: string) {
    this.executeStep(choice);
  }

  private executeStep(userMsg: string) {
    const curSession = this.session();
    if (!curSession) return;

    this.isStepping.set(true);
    this.api.stepBotSimulation({
      nodesJson: this.nodesJson,
      session: curSession,
      userMessage: userMsg
    }).subscribe({
      next: (stepResult: BotSimulationStepResult) => {
        this.session.update(s => s ? {
          ...s,
          currentNodeId: stepResult.currentNodeId,
          variables: stepResult.updatedVariables,
          isWaitingForUserInput: stepResult.isWaitingForUserInput,
          isHandedOverToHuman: stepResult.isHandedOverToHuman,
          isCompleted: stepResult.isCompleted
        } : null);

        this.transcript.update(t => [...t, ...stepResult.newMessages]);
        this.traceLogs.update(l => [...l, ...stepResult.executionTraceLogs]);
        this.isStepping.set(false);
      },
      error: () => this.isStepping.set(false)
    });
  }

  public formatJson(obj: any): string {
    return JSON.stringify(obj || {}, null, 2);
  }
}
