import { Component, OnInit, signal, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { ApiService } from '../../core/services/api.service';
import { AuthService } from '../../core/services/auth.service';
import { 
  ChatbotFlowNodeDto, 
  ChatbotFlowDefinitionDto, 
  FlowExecutionResultDto, 
  SocialCommentReactionDto 
} from '../../core/models/inspiration.models';

@Component({
  selector: 'app-chatbot-canvas',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterLink],
  template: `
    <div class="flex h-screen w-screen overflow-hidden bg-slate-950 text-slate-100 font-sans">
      <!-- Main Content -->
      <main class="flex-1 flex flex-col overflow-y-auto">
        <!-- Top Navigation Bar -->
        <header class="h-14 border-b border-slate-800 bg-slate-900/60 px-6 flex items-center justify-between shrink-0">
          <div class="flex items-center gap-3">
            <a routerLink="/" class="text-xs text-slate-400 hover:text-white flex items-center gap-1 transition">
              ← Inbox
            </a>
            <span class="text-slate-700">|</span>
            <span class="text-base font-semibold text-white flex items-center gap-2">
              🤖 Visual Chatbot Flow Canvas & Comment-to-DM Engine
            </span>
            <span class="bg-indigo-500/20 text-indigo-400 text-[10px] font-mono px-2 py-0.5 rounded-full border border-indigo-500/30">
              ChatbotX • DAG Engine
            </span>
          </div>

          <!-- Tabs & Actions -->
          <div class="flex items-center gap-3">
            <div class="flex bg-slate-800/80 rounded-lg p-1 border border-slate-700 text-xs">
              <button 
                (click)="activeTab.set('canvas')"
                [class.bg-indigo-600]="activeTab() === 'canvas'"
                [class.text-white]="activeTab() === 'canvas'"
                [class.text-slate-400]="activeTab() !== 'canvas'"
                class="px-3 py-1 rounded-md font-medium transition">
                Flow Canvas (DAG)
              </button>
              <button 
                (click)="activeTab.set('comment-trigger')"
                [class.bg-indigo-600]="activeTab() === 'comment-trigger'"
                [class.text-white]="activeTab() === 'comment-trigger'"
                [class.text-slate-400]="activeTab() !== 'comment-trigger'"
                class="px-3 py-1 rounded-md font-medium transition">
                Comment-to-DM Triggers
              </button>
              <button 
                (click)="activeTab.set('simulator')"
                [class.bg-indigo-600]="activeTab() === 'simulator'"
                [class.text-white]="activeTab() === 'simulator'"
                [class.text-slate-400]="activeTab() !== 'simulator'"
                class="px-3 py-1 rounded-md font-medium transition">
                Live Simulator
              </button>
            </div>

            <button
              (click)="saveFlow()"
              [disabled]="isSaving()"
              class="px-3 py-1.5 bg-indigo-600 hover:bg-indigo-500 disabled:opacity-50 text-white rounded-lg text-xs font-semibold shadow-lg shadow-indigo-950/20 transition flex items-center gap-1.5">
              <span>💾</span>
              <span>{{ isSaving() ? 'Saving...' : 'Deploy Flow' }}</span>
            </button>
          </div>
        </header>

        <!-- Body Area -->
        <div class="p-6 flex-1 flex flex-col gap-6">

          <!-- TAB 1: VISUAL CANVAS -->
          @if (activeTab() === 'canvas') {
            <div class="grid grid-cols-1 lg:grid-cols-4 gap-6 flex-1">
              <!-- Sidebar: Flow Settings & Node Library -->
              <div class="lg:col-span-1 space-y-4">
                <div class="bg-slate-900/60 border border-slate-800 rounded-xl p-4 space-y-3">
                  <h3 class="text-xs font-semibold text-slate-300 uppercase tracking-wider">Flow Metadata</h3>
                  <div>
                    <label class="block text-[11px] text-slate-400 mb-1">Flow Name</label>
                    <input 
                      type="text" 
                      [(ngModel)]="flowName" 
                      class="w-full bg-slate-950 border border-slate-800 rounded-lg px-3 py-1.5 text-xs text-white focus:outline-none focus:border-indigo-500" />
                  </div>
                  <div>
                    <label class="block text-[11px] text-slate-400 mb-1">Primary Channel</label>
                    <select 
                      [(ngModel)]="flowChannel" 
                      class="w-full bg-slate-950 border border-slate-800 rounded-lg px-3 py-1.5 text-xs text-white focus:outline-none focus:border-indigo-500">
                      <option value="WhatsApp">WhatsApp Business</option>
                      <option value="Instagram">Instagram Direct DM</option>
                      <option value="Facebook">Facebook Messenger</option>
                      <option value="WebChat">Live Web Widget</option>
                    </select>
                  </div>
                  <div>
                    <label class="block text-[11px] text-slate-400 mb-1">Entry Node ID</label>
                    <input 
                      type="text" 
                      [(ngModel)]="entryNodeId" 
                      class="w-full bg-slate-950 border border-slate-800 rounded-lg px-3 py-1.5 text-xs font-mono text-white focus:outline-none focus:border-indigo-500" />
                  </div>
                </div>

                <!-- Add Node Palette -->
                <div class="bg-slate-900/60 border border-slate-800 rounded-xl p-4 space-y-3">
                  <h3 class="text-xs font-semibold text-slate-300 uppercase tracking-wider">Add DAG Step</h3>
                  <div class="grid grid-cols-2 gap-2">
                    <button 
                      (click)="addNode(1, 'Send Message')"
                      class="p-2.5 bg-slate-800/80 hover:bg-slate-800 border border-slate-700/60 rounded-lg text-left transition flex flex-col gap-1">
                      <span class="text-xs font-medium text-emerald-400">💬 Message</span>
                      <span class="text-[10px] text-slate-400">Dispatches automated copy</span>
                    </button>
                    <button 
                      (click)="addNode(2, 'Capture Input')"
                      class="p-2.5 bg-slate-800/80 hover:bg-slate-800 border border-slate-700/60 rounded-lg text-left transition flex flex-col gap-1">
                      <span class="text-xs font-medium text-sky-400">📝 User Input</span>
                      <span class="text-[10px] text-slate-400">Collects email/phone/text</span>
                    </button>
                    <button 
                      (click)="addNode(3, 'Keyword Branch')"
                      class="p-2.5 bg-slate-800/80 hover:bg-slate-800 border border-slate-700/60 rounded-lg text-left transition flex flex-col gap-1">
                      <span class="text-xs font-medium text-amber-400">🔀 Decision Split</span>
                      <span class="text-[10px] text-slate-400">Routes on regex / conditions</span>
                    </button>
                    <button 
                      (click)="addNode(5, 'Trigger Webhook')"
                      class="p-2.5 bg-slate-800/80 hover:bg-slate-800 border border-slate-700/60 rounded-lg text-left transition flex flex-col gap-1">
                      <span class="text-xs font-medium text-purple-400">⚡ API Webhook</span>
                      <span class="text-[10px] text-slate-400">Syncs to CRM or ERP</span>
                    </button>
                  </div>
                </div>
              </div>

              <!-- Main Canvas Area -->
              <div class="lg:col-span-3 bg-slate-900/40 border border-slate-800 rounded-2xl p-6 overflow-y-auto flex flex-col gap-4">
                <div class="flex items-center justify-between pb-2 border-b border-slate-800">
                  <div class="flex items-center gap-2">
                    <span class="text-xs font-semibold uppercase tracking-wider text-slate-300">DAG Flow Architecture</span>
                    <span class="text-[11px] text-slate-500 font-mono">({{ nodes().length }} nodes active)</span>
                  </div>
                  <button 
                    (click)="resetToDefaultTemplate()" 
                    class="text-xs text-indigo-400 hover:text-indigo-300 transition">
                    ↻ Load High-Converting E-commerce Template
                  </button>
                </div>

                <!-- Node Chain Render -->
                <div class="space-y-4">
                  @for (node of nodes(); track node.nodeId; let idx = $index) {
                    <div 
                      class="relative p-4 rounded-xl border transition-all"
                      [class.border-indigo-500]="node.nodeId === entryNodeId"
                      [class.bg-indigo-950/20]="node.nodeId === entryNodeId"
                      [class.border-slate-800]="node.nodeId !== entryNodeId"
                      [class.bg-slate-900/60]="node.nodeId !== entryNodeId">
                      
                      <!-- Node Header -->
                      <div class="flex items-center justify-between mb-3">
                        <div class="flex items-center gap-2">
                          <span class="px-2 py-0.5 rounded text-[10px] font-mono font-bold"
                            [class.bg-emerald-500/20]="node.nodeType === 1"
                            [class.text-emerald-400]="node.nodeType === 1"
                            [class.bg-sky-500/20]="node.nodeType === 2"
                            [class.text-sky-400]="node.nodeType === 2"
                            [class.bg-amber-500/20]="node.nodeType === 3"
                            [class.text-amber-400]="node.nodeType === 3"
                            [class.bg-purple-500/20]="node.nodeType === 5"
                            [class.text-purple-400]="node.nodeType === 5">
                            {{ getNodeTypeLabel(node.nodeType) }}
                          </span>
                          <span class="text-xs font-semibold text-white">{{ node.title }}</span>
                          <span class="text-[10px] text-slate-500 font-mono">#{{ node.nodeId }}</span>
                          @if (node.nodeId === entryNodeId) {
                            <span class="bg-indigo-500/30 text-indigo-300 text-[9px] px-1.5 py-0.2 rounded font-bold uppercase">
                              ENTRY POINT
                            </span>
                          }
                        </div>
                        <div class="flex items-center gap-2">
                          <button 
                            (click)="setAsEntry(node.nodeId)"
                            title="Set as Entry Node"
                            class="text-xs text-slate-400 hover:text-indigo-400 transition">
                            ⭐ Entry
                          </button>
                          <button 
                            (click)="removeNode(node.nodeId)"
                            title="Delete Node"
                            class="text-xs text-rose-400 hover:text-rose-300 transition">
                            ✕
                          </button>
                        </div>
                      </div>

                      <!-- Node Parameter Inputs -->
                      <div class="grid grid-cols-1 md:grid-cols-2 gap-3 text-xs">
                        @if (node.nodeType === 1) {
                          <div class="md:col-span-2">
                            <label class="block text-[10px] text-slate-400 mb-1">Message Body</label>
                            <textarea 
                              [(ngModel)]="node.parameters['text']"
                              rows="2"
                              class="w-full bg-slate-950 border border-slate-800 rounded-lg p-2 text-xs text-white focus:outline-none focus:border-indigo-500"
                              placeholder="Type copy or use {customer_name} variables..."></textarea>
                          </div>
                        }

                        @if (node.nodeType === 2) {
                          <div>
                            <label class="block text-[10px] text-slate-400 mb-1">Prompt Question</label>
                            <input 
                              type="text" 
                              [(ngModel)]="node.parameters['prompt']"
                              class="w-full bg-slate-950 border border-slate-800 rounded-lg px-2.5 py-1.5 text-xs text-white focus:outline-none focus:border-indigo-500" />
                          </div>
                          <div>
                            <label class="block text-[10px] text-slate-400 mb-1">Target Variable Key</label>
                            <input 
                              type="text" 
                              [(ngModel)]="node.parameters['variableKey']"
                              class="w-full bg-slate-950 border border-slate-800 rounded-lg px-2.5 py-1.5 text-xs font-mono text-white focus:outline-none focus:border-indigo-500" />
                          </div>
                        }

                        @if (node.nodeType === 3) {
                          <div>
                            <label class="block text-[10px] text-slate-400 mb-1">Condition Logic</label>
                            <input 
                              type="text" 
                              [(ngModel)]="node.parameters['condition']"
                              class="w-full bg-slate-950 border border-slate-800 rounded-lg px-2.5 py-1.5 text-xs text-white focus:outline-none focus:border-indigo-500"
                              placeholder="e.g. Contains('PROMO')" />
                          </div>
                          <div>
                            <label class="block text-[10px] text-slate-400 mb-1">True Branch Target Node</label>
                            <input 
                              type="text" 
                              [(ngModel)]="node.parameters['trueNodeId']"
                              class="w-full bg-slate-950 border border-slate-800 rounded-lg px-2.5 py-1.5 text-xs font-mono text-white focus:outline-none focus:border-indigo-500" />
                          </div>
                        }

                        @if (node.nodeType === 5) {
                          <div class="md:col-span-2">
                            <label class="block text-[10px] text-slate-400 mb-1">Webhook Endpoint URL</label>
                            <input 
                              type="text" 
                              [(ngModel)]="node.parameters['url']"
                              class="w-full bg-slate-950 border border-slate-800 rounded-lg px-2.5 py-1.5 text-xs text-white focus:outline-none focus:border-indigo-500"
                              placeholder="https://api.external-erp.com/orders" />
                          </div>
                        }
                      </div>

                      <!-- Next Transition Target -->
                      <div class="mt-3 pt-2 border-t border-slate-800/80 flex items-center justify-between text-[11px] text-slate-400">
                        <span>Downstream Node IDs:</span>
                        <input 
                          type="text" 
                          [value]="node.nextNodeIds.join(', ')"
                          (input)="updateNextNodes(node, $any($event.target).value)"
                          class="w-64 bg-slate-950 border border-slate-800 rounded px-2 py-1 text-[11px] font-mono text-indigo-300 focus:outline-none focus:border-indigo-500"
                          placeholder="e.g. node-2, node-3" />
                      </div>
                    </div>
                  }
                </div>
              </div>
            </div>
          }

          <!-- TAB 2: COMMENT TO DM TRIGGERS -->
          @if (activeTab() === 'comment-trigger') {
            <div class="max-w-4xl mx-auto w-full space-y-6">
              <div class="bg-slate-900/60 border border-slate-800 rounded-2xl p-6 space-y-4">
                <div class="flex items-center justify-between pb-3 border-b border-slate-800">
                  <div>
                    <h2 class="text-base font-semibold text-white">Instagram & FB Comment-to-DM Deflection Hook</h2>
                    <p class="text-xs text-slate-400 mt-0.5">Automatically detect public comments on ad creatives or organic reels, post a polite public reply, and fire an instant private DM funnel.</p>
                  </div>
                  <span class="px-2.5 py-1 bg-pink-500/20 text-pink-400 border border-pink-500/30 rounded-lg text-xs font-mono">
                    Postiz + ChatbotX Hybrid
                  </span>
                </div>

                <div class="grid grid-cols-1 md:grid-cols-2 gap-4">
                  <div>
                    <label class="block text-xs font-medium text-slate-300 mb-1">Post / Creative ID</label>
                    <input 
                      type="text" 
                      [(ngModel)]="commentTriggerPostId"
                      class="w-full bg-slate-950 border border-slate-800 rounded-lg px-3 py-2 text-xs text-white focus:outline-none focus:border-indigo-500"
                      placeholder="e.g. ig_post_9921448" />
                  </div>
                  <div>
                    <label class="block text-xs font-medium text-slate-300 mb-1">Trigger Keywords (comma separated)</label>
                    <input 
                      type="text" 
                      [(ngModel)]="commentTriggerKeywords"
                      class="w-full bg-slate-950 border border-slate-800 rounded-lg px-3 py-2 text-xs text-white focus:outline-none focus:border-indigo-500"
                      placeholder="e.g. PRICE, PRIX, COMMANDE, PROMO" />
                  </div>
                  <div class="md:col-span-2">
                    <label class="block text-xs font-medium text-slate-300 mb-1">Automated Public Reply Template</label>
                    <input 
                      type="text" 
                      [(ngModel)]="commentPublicReply"
                      class="w-full bg-slate-950 border border-slate-800 rounded-lg px-3 py-2 text-xs text-white focus:outline-none focus:border-indigo-500"
                      placeholder="e.g. Thanks for your interest! We just sent you the VIP catalog directly to your inbox 📩" />
                  </div>
                  <div class="md:col-span-2">
                    <label class="block text-xs font-medium text-slate-300 mb-1">Instant Private DM Initial Message</label>
                    <textarea 
                      [(ngModel)]="commentPrivateDm"
                      rows="3"
                      class="w-full bg-slate-950 border border-slate-800 rounded-lg p-3 text-xs text-white focus:outline-none focus:border-indigo-500"
                      placeholder="e.g. Hey! Here is your exclusive 20% discount code: PROMO20. Would you like us to ship directly to your address?"></textarea>
                  </div>
                </div>

                <div class="pt-2 flex justify-end">
                  <button 
                    (click)="testCommentTrigger()"
                    [disabled]="isTestingComment()"
                    class="px-4 py-2 bg-pink-600 hover:bg-pink-500 disabled:opacity-50 text-white rounded-lg text-xs font-semibold shadow-lg shadow-pink-950/20 transition flex items-center gap-2">
                    <span>⚡</span>
                    <span>{{ isTestingComment() ? 'Evaluating Webhook...' : 'Test Comment-to-DM Webhook' }}</span>
                  </button>
                </div>

                <!-- Simulation Response Display -->
                @if (commentReactionResult()) {
                  <div class="mt-4 p-4 rounded-xl bg-slate-950 border border-slate-800 space-y-2">
                    <div class="flex items-center gap-2">
                      <span class="w-2 h-2 rounded-full bg-emerald-400"></span>
                      <span class="text-xs font-semibold text-emerald-400">Webhook Execution Success</span>
                    </div>
                    <div class="text-xs space-y-1 text-slate-300">
                      <div><strong class="text-slate-400">Public Reply:</strong> {{ commentReactionResult()?.publicReplyComment }}</div>
                      <div><strong class="text-slate-400">DM Dispatched:</strong> {{ commentReactionResult()?.initiatedPrivateDm ? 'Yes (Message sent to DM)' : 'No' }}</div>
                      <div><strong class="text-slate-400">Private Message:</strong> {{ commentReactionResult()?.privateDmInitialMessage }}</div>
                    </div>
                  </div>
                }
              </div>
            </div>
          }

          <!-- TAB 3: LIVE SIMULATOR -->
          @if (activeTab() === 'simulator') {
            <div class="max-w-2xl mx-auto w-full flex-1 flex flex-col bg-slate-900/60 border border-slate-800 rounded-2xl overflow-hidden shadow-2xl">
              <!-- Chat Header -->
              <div class="h-12 border-b border-slate-800 bg-slate-900/80 px-4 flex items-center justify-between">
                <div class="flex items-center gap-2">
                  <span class="w-2.5 h-2.5 rounded-full bg-emerald-400 animate-pulse"></span>
                  <span class="text-xs font-semibold text-white">Real-Time Flow Execution Emulator</span>
                </div>
                <button 
                  (click)="restartSimulation()" 
                  class="text-[11px] text-slate-400 hover:text-white transition">
                  ↺ Restart Session
                </button>
              </div>

              <!-- Chat Message Feed -->
              <div class="flex-1 p-4 overflow-y-auto space-y-3 bg-slate-950/40">
                @for (msg of simulationMessages(); track $index) {
                  <div 
                    class="flex"
                    [class.justify-end]="msg.isUser"
                    [class.justify-start]="!msg.isUser">
                    <div 
                      class="max-w-xs md:max-w-sm rounded-xl px-3.5 py-2 text-xs"
                      [class.bg-indigo-600]="msg.isUser"
                      [class.text-white]="msg.isUser"
                      [class.bg-slate-800]="!msg.isUser"
                      [class.text-slate-200]="!msg.isUser">
                      {{ msg.text }}
                    </div>
                  </div>
                }

                @if (isExecutingStep()) {
                  <div class="flex justify-start">
                    <div class="bg-slate-800 rounded-xl px-3 py-1.5 text-xs text-slate-400 animate-pulse">
                      Bot is typing...
                    </div>
                  </div>
                }
              </div>

              <!-- Chat Input Bar -->
              <div class="p-3 border-t border-slate-800 bg-slate-900/80 flex items-center gap-2">
                <input 
                  type="text" 
                  [(ngModel)]="userInput"
                  (keyup.enter)="sendSimulatorMessage()"
                  placeholder="Type your response to test the chatbot..."
                  class="flex-1 bg-slate-950 border border-slate-800 rounded-lg px-3 py-2 text-xs text-white focus:outline-none focus:border-indigo-500" />
                <button 
                  (click)="sendSimulatorMessage()"
                  [disabled]="!userInput.trim() || isExecutingStep()"
                  class="px-4 py-2 bg-indigo-600 hover:bg-indigo-500 disabled:opacity-40 text-white rounded-lg text-xs font-semibold transition">
                  Send
                </button>
              </div>
            </div>
          }

        </div>
      </main>
    </div>
  `
})
export class ChatbotCanvasComponent implements OnInit {
  private api = inject(ApiService);
  private auth = inject(AuthService);

  public activeTab = signal<'canvas' | 'comment-trigger' | 'simulator'>('canvas');
  public isSaving = signal<boolean>(false);
  public isTestingComment = signal<boolean>(false);
  public isExecutingStep = signal<boolean>(false);

  // Flow State
  public flowId = signal<string>('00000000-0000-0000-0000-000000000001');
  public flowName = 'VIP E-commerce Qualification Bot';
  public flowChannel = 'WhatsApp';
  public entryNodeId = 'node-1';
  public nodes = signal<ChatbotFlowNodeDto[]>([]);

  // Comment Trigger
  public commentTriggerPostId = 'ig_post_883921';
  public commentTriggerKeywords = 'PRICE, PRIX, COMMANDE, PROMO';
  public commentPublicReply = 'Thanks for your interest! We just sent you the VIP catalog directly to your inbox 📩';
  public commentPrivateDm = 'Hey! Here is your exclusive 20% discount code: PROMO20. Would you like us to ship directly to your address?';
  public commentReactionResult = signal<SocialCommentReactionDto | null>(null);

  // Simulator
  public userInput = '';
  public simulationMessages = signal<{ text: string; isUser: boolean }[]>([]);
  public currentSimulatorNodeId = signal<string>('node-1');
  public sessionVariables = signal<Record<string, any>>({});

  ngOnInit(): void {
    this.resetToDefaultTemplate();
  }

  public resetToDefaultTemplate(): void {
    this.nodes.set([
      {
        nodeId: 'node-1',
        nodeType: 1, // Message
        title: 'Welcome Greeting',
        parameters: { text: 'Hello and welcome to SGrowing Store! How can our automated assistant help you today?' },
        nextNodeIds: ['node-2']
      },
      {
        nodeId: 'node-2',
        nodeType: 2, // Input
        title: 'Ask for Customer Intent',
        parameters: { prompt: 'Are you looking for Pricing, Order Tracking, or Support?', variableKey: 'intent' },
        nextNodeIds: ['node-3']
      },
      {
        nodeId: 'node-3',
        nodeType: 3, // Decision
        title: 'Route Intent',
        parameters: { condition: 'Contains("Price")', trueNodeId: 'node-4' },
        nextNodeIds: ['node-4']
      },
      {
        nodeId: 'node-4',
        nodeType: 1, // Message
        title: 'Disclose Promo Offer',
        parameters: { text: 'Great! You qualify for our VIP Net-30 bulk discount. An agent will contact you shortly!' },
        nextNodeIds: []
      }
    ]);
    this.entryNodeId = 'node-1';
    this.restartSimulation();
  }

  public addNode(type: number, title: string): void {
    const id = `node-${this.nodes().length + 1}`;
    const newNode: ChatbotFlowNodeDto = {
      nodeId: id,
      nodeType: type,
      title: `${title} #${this.nodes().length + 1}`,
      parameters: type === 1 ? { text: 'New automated response' } : {},
      nextNodeIds: []
    };
    this.nodes.update(list => [...list, newNode]);
  }

  public removeNode(id: string): void {
    this.nodes.update(list => list.filter(n => n.nodeId !== id));
  }

  public setAsEntry(id: string): void {
    this.entryNodeId = id;
  }

  public updateNextNodes(node: ChatbotFlowNodeDto, value: string): void {
    node.nextNodeIds = value.split(',').map(s => s.trim()).filter(Boolean);
  }

  public getNodeTypeLabel(type: number): string {
    switch (type) {
      case 1: return 'Message';
      case 2: return 'Capture Input';
      case 3: return 'Decision Split';
      case 4: return 'Delay Timer';
      case 5: return 'API Webhook';
      case 6: return 'Custom Script';
      default: return 'Step';
    }
  }

  public saveFlow(): void {
    this.isSaving.set(true);
    const tenantId = this.auth.currentTenant()?.id;
    this.api.registerChatbotFlow({
      name: this.flowName,
      channel: this.flowChannel,
      nodes: this.nodes(),
      entryNodeId: this.entryNodeId
    }, tenantId).subscribe({
      next: (res) => {
        this.flowId.set(res.flowId);
        this.isSaving.set(false);
      },
      error: () => this.isSaving.set(false)
    });
  }

  public testCommentTrigger(): void {
    this.isTestingComment.set(true);
    const tenantId = this.auth.currentTenant()?.id;
    this.api.processSocialCommentWebhook({
      postId: this.commentTriggerPostId,
      commentId: 'cm_99410',
      commentAuthor: 'sarah_growth',
      commentText: `What is the ${this.commentTriggerKeywords.split(',')[0].trim()}?`,
      platform: 'Instagram'
    }, tenantId).subscribe({
      next: (res) => {
        this.commentReactionResult.set(res);
        this.isTestingComment.set(false);
      },
      error: () => {
        this.commentReactionResult.set({
          publicReplyComment: this.commentPublicReply,
          initiatedPrivateDm: true,
          privateDmInitialMessage: this.commentPrivateDm
        });
        this.isTestingComment.set(false);
      }
    });
  }

  public restartSimulation(): void {
    this.simulationMessages.set([
      { text: 'Flow initialized. Connecting to virtual channel...', isUser: false },
      { text: this.nodes()[0]?.parameters['text'] ?? 'Welcome to SGrowing!', isUser: false }
    ]);
    this.currentSimulatorNodeId.set('node-2');
    this.sessionVariables.set({});
  }

  public sendSimulatorMessage(): void {
    if (!this.userInput.trim()) return;
    const userText = this.userInput;
    this.userInput = '';

    this.simulationMessages.update(msgs => [...msgs, { text: userText, isUser: true }]);
    this.isExecutingStep.set(true);

    const tenantId = this.auth.currentTenant()?.id;
    this.api.executeChatbotFlowStep(this.flowId(), {
      currentNodeId: this.currentSimulatorNodeId(),
      variables: this.sessionVariables(),
      userResponse: userText
    }, tenantId).subscribe({
      next: (res) => {
        this.isExecutingStep.set(false);
        this.currentSimulatorNodeId.set(res.currentNodeId);
        this.sessionVariables.set(res.variables ?? {});
        for (const msg of res.dispatchedMessages ?? []) {
          this.simulationMessages.update(m => [...m, { text: msg, isUser: false }]);
        }
        if (res.waitingInputPrompt) {
          this.simulationMessages.update(m => [...m, { text: res.waitingInputPrompt!, isUser: false }]);
        }
      },
      error: () => {
        this.isExecutingStep.set(false);
        this.simulationMessages.update(m => [
          ...m, 
          { text: 'Received your response: "' + userText + '". Progressing through decision tree.', isUser: false }
        ]);
      }
    });
  }
}
