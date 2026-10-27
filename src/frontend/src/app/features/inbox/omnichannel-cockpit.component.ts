import { Component, OnInit, signal, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { ApiService } from '../../core/services/api.service';
import { AuthService } from '../../core/services/auth.service';
import { 
  ParsedMimeEmail, 
  CannedMacroDto, 
  CannedMacroResolutionDto, 
  CsatSurveyDispatchResult, 
  SlaEscalationEvaluationResult 
} from '../../core/models/pillars.models';

@Component({
  selector: 'app-omnichannel-cockpit',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterLink],
  template: `
    <div class="flex h-screen w-screen overflow-hidden bg-slate-950 text-slate-100 font-sans">
      <main class="flex-1 flex flex-col overflow-y-auto">
        <!-- Topbar -->
        <header class="h-14 border-b border-slate-800 bg-slate-900/60 px-6 flex items-center justify-between shrink-0">
          <div class="flex items-center gap-3">
            <a routerLink="/" class="text-xs text-slate-400 hover:text-white flex items-center gap-1 transition">
              ← Main Inbox
            </a>
            <span class="text-slate-700">|</span>
            <span class="text-base font-semibold text-white flex items-center gap-2">
              📥 Omnichannel Power Tools: MIME, Macros & SLA
            </span>
            <span class="bg-teal-500/20 text-teal-400 text-[10px] font-mono px-2 py-0.5 rounded-full border border-teal-500/30">
              Clean Quoting • Hotkey Macros • SLA Engine
            </span>
          </div>

          <div class="flex items-center gap-3">
            <button
              (click)="activeTab.set('macros')"
              [class]="activeTab() === 'macros' ? 'bg-teal-600 text-white' : 'bg-slate-800 text-slate-300 hover:bg-slate-700'"
              class="px-3 py-1.5 rounded-lg text-xs font-semibold transition">
              Canned Macros
            </button>
            <button
              (click)="activeTab.set('mime')"
              [class]="activeTab() === 'mime' ? 'bg-teal-600 text-white' : 'bg-slate-800 text-slate-300 hover:bg-slate-700'"
              class="px-3 py-1.5 rounded-lg text-xs font-semibold transition">
              RFC 822 MIME Parser
            </button>
            <button
              (click)="activeTab.set('sla')"
              [class]="activeTab() === 'sla' ? 'bg-teal-600 text-white' : 'bg-slate-800 text-slate-300 hover:bg-slate-700'"
              class="px-3 py-1.5 rounded-lg text-xs font-semibold transition">
              CSAT & SLA Escalations
            </button>
          </div>
        </header>

        <!-- Body -->
        <div class="p-6 space-y-6 flex-1">
          <!-- TAB 1: CANNED MACROS -->
          @if (activeTab() === 'macros') {
            <div class="grid grid-cols-1 lg:grid-cols-2 gap-6">
              <!-- Left: Macro List & Search -->
              <div class="p-5 rounded-xl bg-slate-900/60 border border-slate-800 space-y-4">
                <div class="flex items-center justify-between">
                  <h3 class="text-sm font-semibold text-white flex items-center gap-2">
                    <span>⚡</span> Hotkey Trigger Macros
                  </h3>
                  <span class="text-xs text-slate-400 font-mono">Type / to trigger in chat</span>
                </div>

                <div class="space-y-2">
                  @for (macro of macros(); track macro.id) {
                    <div
                      (click)="selectMacro(macro)"
                      class="p-3 rounded-lg border border-slate-800 hover:border-teal-500/50 bg-slate-950/60 cursor-pointer transition flex items-center justify-between">
                      <div>
                        <div class="flex items-center gap-2">
                          <span class="font-mono text-teal-400 text-xs font-bold">{{ macro.triggerKey }}</span>
                          <span class="text-xs text-white font-medium">{{ macro.title }}</span>
                        </div>
                        <span class="text-[10px] text-slate-400 block mt-0.5">{{ macro.category }}</span>
                      </div>
                      <span class="text-[10px] bg-slate-800 text-slate-300 px-2 py-0.5 rounded font-mono">
                        {{ macro.requiredVariables.length }} vars
                      </span>
                    </div>
                  }
                </div>
              </div>

              <!-- Right: Macro Variable Resolver Sandbox -->
              <div class="p-5 rounded-xl bg-slate-900/60 border border-slate-800 space-y-4">
                <h3 class="text-sm font-semibold text-white">Live Variable Substitution Simulator</h3>

                <div class="grid grid-cols-2 gap-2 text-xs">
                  <div>
                    <label class="text-[10px] text-slate-400 block mb-0.5">contact.name</label>
                    <input type="text" [(ngModel)]="contextName" class="w-full bg-slate-950 border border-slate-800 rounded p-1.5 text-xs text-slate-200" />
                  </div>
                  <div>
                    <label class="text-[10px] text-slate-400 block mb-0.5">agent.name</label>
                    <input type="text" [(ngModel)]="contextAgent" class="w-full bg-slate-950 border border-slate-800 rounded p-1.5 text-xs text-slate-200" />
                  </div>
                  <div>
                    <label class="text-[10px] text-slate-400 block mb-0.5">order.number</label>
                    <input type="text" [(ngModel)]="contextOrderNumber" class="w-full bg-slate-950 border border-slate-800 rounded p-1.5 text-xs text-slate-200" />
                  </div>
                  <div>
                    <label class="text-[10px] text-slate-400 block mb-0.5">order.amount</label>
                    <input type="number" [(ngModel)]="contextOrderAmount" class="w-full bg-slate-950 border border-slate-800 rounded p-1.5 text-xs text-slate-200" />
                  </div>
                </div>

                <button
                  (click)="resolveMacro()"
                  [disabled]="isResolving()"
                  class="w-full py-2 bg-teal-600 hover:bg-teal-500 disabled:opacity-50 text-white rounded-lg text-xs font-semibold transition">
                  {{ isResolving() ? 'Resolving...' : 'Substitute Macro Variables' }}
                </button>

                @if (resolvedMacro()) {
                  <div class="p-4 rounded-xl bg-slate-950 border border-slate-800 space-y-2">
                    <span class="text-xs text-teal-400 font-bold block">{{ resolvedMacro()?.title }} ({{ resolvedMacro()?.triggerKey }})</span>
                    <p class="text-xs text-slate-200 whitespace-pre-line leading-relaxed font-sans">{{ resolvedMacro()?.resolvedContent }}</p>
                  </div>
                }
              </div>
            </div>
          }

          <!-- TAB 2: MIME PARSER -->
          @if (activeTab() === 'mime') {
            <div class="grid grid-cols-1 lg:grid-cols-2 gap-6">
              <div class="p-5 rounded-xl bg-slate-900/60 border border-slate-800 space-y-3">
                <h3 class="text-sm font-semibold text-white">Raw RFC 822 MIME Message Stream</h3>
                <textarea
                  [(ngModel)]="rawMimeInput"
                  rows="12"
                  class="w-full bg-slate-950 border border-slate-800 rounded-lg p-3 text-[11px] font-mono text-slate-300 focus:outline-none focus:border-teal-500"></textarea>

                <button
                  (click)="parseMime()"
                  [disabled]="isParsingMime()"
                  class="w-full py-2 bg-teal-600 hover:bg-teal-500 disabled:opacity-50 text-white rounded-lg text-xs font-semibold transition">
                  {{ isParsingMime() ? 'Parsing...' : 'Clean Quoted History & Parse MIME' }}
                </button>
              </div>

              <div class="p-5 rounded-xl bg-slate-900/60 border border-slate-800 space-y-4">
                <h3 class="text-sm font-semibold text-white">Cleaned Body & Extracted Attachments</h3>

                @if (parsedMime()) {
                  <div class="space-y-3">
                    <div class="p-3 bg-slate-950 rounded border border-slate-800 text-xs space-y-1">
                      <div><strong class="text-slate-400">Subject:</strong> {{ parsedMime()?.subject }}</div>
                      <div><strong class="text-slate-400">From:</strong> {{ parsedMime()?.from }}</div>
                      <div><strong class="text-slate-400">Message-ID:</strong> {{ parsedMime()?.messageId }}</div>
                    </div>

                    <div class="p-3 bg-slate-950 rounded border border-slate-800 space-y-1">
                      <span class="text-[10px] text-teal-400 uppercase tracking-wider font-semibold block">Cleaned Text Body (Quotes Stripped)</span>
                      <p class="text-xs text-slate-200 whitespace-pre-line">{{ parsedMime()?.cleanTextBody }}</p>
                    </div>

                    @if (parsedMime()?.attachments?.length) {
                      <div class="p-3 bg-slate-950 rounded border border-slate-800 space-y-2">
                        <span class="text-[10px] text-slate-400 uppercase tracking-wider block font-semibold">Extracted Attachments ({{ parsedMime()?.attachments?.length }})</span>
                        @for (att of parsedMime()?.attachments; track att.contentId) {
                          <div class="text-xs text-slate-300 flex items-center justify-between">
                            <span>📎 {{ att.fileName }} ({{ att.contentType }})</span>
                            <span class="text-slate-500 text-[10px]">{{ att.sizeBytes | number }} bytes</span>
                          </div>
                        }
                      </div>
                    }
                  </div>
                } @else {
                  <div class="h-64 flex flex-col items-center justify-center text-slate-500 text-xs gap-2">
                    <span>💡</span> Click Clean to strip quoted threads, remove disclaimers, and isolate attachments.
                  </div>
                }
              </div>
            </div>
          }

          <!-- TAB 3: SLA & CSAT -->
          @if (activeTab() === 'sla') {
            <div class="space-y-6">
              <div class="p-5 rounded-xl bg-slate-900/60 border border-slate-800 flex items-center justify-between">
                <div>
                  <h3 class="text-sm font-semibold text-white">SLA Policy Escalation Engine</h3>
                  <p class="text-xs text-slate-400 mt-1">Multi-tier response monitoring: Warning (75%), Breached (100%), Critical (150%).</p>
                </div>
                <button
                  (click)="evaluateSla()"
                  [disabled]="isEvaluatingSla()"
                  class="px-4 py-2 bg-rose-600 hover:bg-rose-500 disabled:opacity-50 text-white rounded-lg text-xs font-semibold transition">
                  {{ isEvaluatingSla() ? 'Evaluating...' : 'Run SLA Escalation Scan' }}
                </button>
              </div>

              @if (slaReport()) {
                <div class="grid grid-cols-4 gap-4">
                  <div class="p-4 rounded-xl bg-slate-900/60 border border-slate-800">
                    <span class="text-xs text-slate-400 block uppercase">Evaluated</span>
                    <span class="text-2xl font-bold font-mono text-white mt-1 block">{{ slaReport()?.totalActiveEvaluated }}</span>
                  </div>
                  <div class="p-4 rounded-xl bg-slate-900/60 border border-slate-800">
                    <span class="text-xs text-slate-400 block uppercase">Warning (75%)</span>
                    <span class="text-2xl font-bold font-mono text-amber-400 mt-1 block">{{ slaReport()?.totalWarning }}</span>
                  </div>
                  <div class="p-4 rounded-xl bg-slate-900/60 border border-slate-800">
                    <span class="text-xs text-slate-400 block uppercase">Breached (100%)</span>
                    <span class="text-2xl font-bold font-mono text-rose-400 mt-1 block">{{ slaReport()?.totalBreached }}</span>
                  </div>
                  <div class="p-4 rounded-xl bg-slate-900/60 border border-slate-800">
                    <span class="text-xs text-slate-400 block uppercase">Critical (150%+)</span>
                    <span class="text-2xl font-bold font-mono text-purple-400 mt-1 block">{{ slaReport()?.totalCritical }}</span>
                  </div>
                </div>

                @if (slaReport()?.escalations?.length) {
                  <div class="p-5 rounded-xl bg-slate-900/60 border border-slate-800 space-y-3">
                    <h4 class="text-xs font-semibold uppercase text-slate-400">Escalated Conversations Requiring Action</h4>
                    <div class="space-y-2">
                      @for (esc of slaReport()?.escalations; track esc.conversationId) {
                        <div class="p-3 bg-slate-950 rounded-lg border border-slate-800 flex items-center justify-between text-xs">
                          <div>
                            <span class="font-mono text-slate-300">#{{ esc.conversationId }}</span>
                            <span class="text-slate-400 ml-2">({{ esc.elapsedMinutes }}m / {{ esc.targetMinutes }}m target)</span>
                            <p class="text-[11px] text-rose-300 mt-0.5">{{ esc.recommendedAction }}</p>
                          </div>
                          <span class="px-2.5 py-0.5 rounded text-[10px] font-bold bg-rose-500/20 text-rose-300 border border-rose-500/30">
                            {{ esc.escalationTier }}
                          </span>
                        </div>
                      }
                    </div>
                  </div>
                }
              }
            </div>
          }
        </div>
      </main>
    </div>
  `
})
export class OmnichannelCockpitComponent implements OnInit {
  private api = inject(ApiService);
  private auth = inject(AuthService);

  public activeTab = signal<'macros' | 'mime' | 'sla'>('macros');

  // Macros
  public macros = signal<CannedMacroDto[]>([]);
  public selectedTrigger = '/refund';
  public contextName = 'Karim Ben Ali';
  public contextAgent = 'Sonia (Tier 2 Support)';
  public contextOrderNumber = 'ORD-2026-9921';
  public contextOrderAmount = 145.50;
  public isResolving = signal(false);
  public resolvedMacro = signal<CannedMacroResolutionDto | null>(null);

  // MIME
  public rawMimeInput = `From: "Customer" <client@outlook.com>\nTo: <support@sgrowing.io>\nSubject: Replacement inquiry\nDate: Tue, 27 Oct 2026 12:00:00 +0000\nContent-Type: text/plain; charset=utf-8\n\nHello, I received a wrong size. Can I exchange it?\n\nOn Mon, Oct 26, 2026 at 9:00 AM Support <support@sgrowing.io> wrote:\n> Thank you for choosing SGrowing.\n\n--\nBest,\nClient User`;
  public isParsingMime = signal(false);
  public parsedMime = signal<ParsedMimeEmail | null>(null);

  // SLA
  public isEvaluatingSla = signal(false);
  public slaReport = signal<SlaEscalationEvaluationResult | null>(null);

  ngOnInit() {
    this.loadMacros();
  }

  private loadMacros() {
    const tenantId = this.auth.currentUser()?.tenantId || '00000000-0000-0000-0000-000000000000';
    this.api.getCannedMacros(tenantId).subscribe({
      next: list => {
        this.macros.set(list);
        if (list.length > 0) this.selectMacro(list[0]);
      }
    });
  }

  public selectMacro(macro: CannedMacroDto) {
    this.selectedTrigger = macro.triggerKey;
    this.resolveMacro();
  }

  public resolveMacro() {
    this.isResolving.set(true);
    const tenantId = this.auth.currentUser()?.tenantId || '00000000-0000-0000-0000-000000000000';
    this.api.resolveCannedMacro(tenantId, this.selectedTrigger, {
      contactName: this.contextName,
      agentName: this.contextAgent,
      orderNumber: this.contextOrderNumber,
      orderAmount: this.contextOrderAmount,
      currency: 'TND'
    }).subscribe({
      next: res => {
        this.resolvedMacro.set(res);
        this.isResolving.set(false);
      },
      error: () => this.isResolving.set(false)
    });
  }

  public parseMime() {
    this.isParsingMime.set(true);
    this.api.parseMimeEmail(this.rawMimeInput).subscribe({
      next: res => {
        this.parsedMime.set(res);
        this.isParsingMime.set(false);
      },
      error: () => this.isParsingMime.set(false)
    });
  }

  public evaluateSla() {
    this.isEvaluatingSla.set(true);
    const tenantId = this.auth.currentUser()?.tenantId || '00000000-0000-0000-0000-000000000000';
    this.api.evaluateSlaEscalations(tenantId).subscribe({
      next: res => {
        this.slaReport.set(res);
        this.isEvaluatingSla.set(false);
      },
      error: () => this.isEvaluatingSla.set(false)
    });
  }
}
