import { Component, signal, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { ApiService } from '../../core/services/api.service';
import { AuthService } from '../../core/services/auth.service';
import { SpintaxPreviewResponse, EmailVerificationResult, EnrichedContactDto } from '../../core/models/pillars.models';

@Component({
  selector: 'app-spintax-engine',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterLink],
  template: `
    <div class="flex h-screen w-screen overflow-hidden bg-slate-950 text-slate-100 font-sans">
      <main class="flex-1 flex flex-col overflow-y-auto">
        <!-- Topbar -->
        <header class="h-14 border-b border-slate-800 bg-slate-900/60 px-6 flex items-center justify-between shrink-0">
          <div class="flex items-center gap-3">
            <a routerLink="/outbound" class="text-xs text-slate-400 hover:text-white flex items-center gap-1 transition">
              ← Outbound Studio
            </a>
            <span class="text-slate-700">|</span>
            <span class="text-base font-semibold text-white flex items-center gap-2">
              ⚡ Cold Outreach AI SDR & Spintax Engine
            </span>
            <span class="bg-indigo-500/20 text-indigo-400 text-[10px] font-mono px-2 py-0.5 rounded-full border border-indigo-500/30">
              Deterministic Seed • MX Shield
            </span>
          </div>

          <div class="flex items-center gap-3">
            <button
              (click)="activeTab.set('spintax')"
              [class]="activeTab() === 'spintax' ? 'bg-indigo-600 text-white' : 'bg-slate-800 text-slate-300 hover:bg-slate-700'"
              class="px-3 py-1.5 rounded-lg text-xs font-semibold transition">
              Nested Spintax
            </button>
            <button
              (click)="activeTab.set('verifier')"
              [class]="activeTab() === 'verifier' ? 'bg-indigo-600 text-white' : 'bg-slate-800 text-slate-300 hover:bg-slate-700'"
              class="px-3 py-1.5 rounded-lg text-xs font-semibold transition">
              Real-time MX Verifier
            </button>
            <button
              (click)="activeTab.set('enrichment')"
              [class]="activeTab() === 'enrichment' ? 'bg-indigo-600 text-white' : 'bg-slate-800 text-slate-300 hover:bg-slate-700'"
              class="px-3 py-1.5 rounded-lg text-xs font-semibold transition">
              B2B Contact Enrichment
            </button>
          </div>
        </header>

        <!-- Body -->
        <div class="p-6 space-y-6 flex-1">
          <!-- TAB 1: NESTED SPINTAX -->
          @if (activeTab() === 'spintax') {
            <div class="grid grid-cols-1 lg:grid-cols-2 gap-6">
              <div class="p-5 rounded-xl bg-slate-900/60 border border-slate-800 space-y-4">
                <div class="flex items-center justify-between">
                  <h3 class="text-sm font-semibold text-white flex items-center gap-2">
                    <span>🔀</span> Spintax Template Editor
                  </h3>
                  <span class="text-xs text-indigo-400 font-mono">Arbitrary Depth</span>
                </div>
                <p class="text-xs text-slate-400">
                  Use nested curly braces <code class="text-indigo-300">&#123;A|B&#125;</code> for variations and tags like <code class="text-indigo-300">&#123;FirstName&#125;</code>.
                </p>

                <textarea
                  [(ngModel)]="spintaxTemplate"
                  rows="6"
                  class="w-full bg-slate-950 border border-slate-800 rounded-lg p-3 text-xs font-mono text-slate-200 focus:outline-none focus:border-indigo-500"></textarea>

                <div class="grid grid-cols-2 gap-3">
                  <div>
                    <label class="text-[11px] text-slate-400 block mb-1">Prospect Email (Seed)</label>
                    <input
                      type="text"
                      [(ngModel)]="prospectSeedEmail"
                      placeholder="e.g. john.doe@enterprise.com"
                      class="w-full bg-slate-950 border border-slate-800 rounded-lg px-3 py-1.5 text-xs text-slate-200" />
                  </div>
                  <div>
                    <label class="text-[11px] text-slate-400 block mb-1">FirstName Tag</label>
                    <input
                      type="text"
                      [(ngModel)]="firstNameVar"
                      placeholder="Sarah"
                      class="w-full bg-slate-950 border border-slate-800 rounded-lg px-3 py-1.5 text-xs text-slate-200" />
                  </div>
                </div>

                <button
                  (click)="generateSpintaxPreview()"
                  [disabled]="isLoadingSpintax()"
                  class="w-full py-2 bg-indigo-600 hover:bg-indigo-500 disabled:opacity-50 text-white rounded-lg text-xs font-semibold transition flex items-center justify-center gap-2">
                  <span>✨</span>
                  <span>{{ isLoadingSpintax() ? 'Evaluating...' : 'Compile Deterministic Variants' }}</span>
                </button>
              </div>

              <!-- Output Box -->
              <div class="p-5 rounded-xl bg-slate-900/60 border border-slate-800 space-y-4">
                <div class="flex items-center justify-between">
                  <h3 class="text-sm font-semibold text-white">Variant Generation Matrix</h3>
                  @if (spintaxResult()) {
                    <span class="text-xs bg-indigo-500/10 text-indigo-400 px-2.5 py-0.5 rounded-full border border-indigo-500/20 font-mono">
                      {{ spintaxResult()?.totalPermutations | number }} total permutations
                    </span>
                  }
                </div>

                @if (spintaxResult()) {
                  <div class="space-y-3">
                    <div class="p-3.5 rounded-lg bg-slate-950/80 border border-slate-800/80">
                      <div class="text-[10px] uppercase tracking-wider text-emerald-400 font-semibold mb-1">Deterministic Prospect Copy</div>
                      <p class="text-xs text-slate-200 leading-relaxed font-sans">{{ spintaxResult()?.deterministicVariant }}</p>
                    </div>

                    <div class="text-[11px] text-slate-400 font-medium">Sample Permutation Space:</div>
                    <div class="space-y-2">
                      @for (variant of spintaxResult()?.sampleVariants; track $index) {
                        <div class="p-2.5 rounded-lg bg-slate-950/50 border border-slate-800 text-xs text-slate-300 font-mono">
                          #{{ $index + 1 }}: {{ variant }}
                        </div>
                      }
                    </div>
                  </div>
                } @else {
                  <div class="h-64 flex flex-col items-center justify-center text-slate-500 text-xs gap-2">
                    <span>💡</span> Click Compile to evaluate variant permutations and deterministic seed.
                  </div>
                }
              </div>
            </div>
          }

          <!-- TAB 2: EMAIL VERIFIER -->
          @if (activeTab() === 'verifier') {
            <div class="max-w-2xl mx-auto p-6 rounded-xl bg-slate-900/60 border border-slate-800 space-y-4">
              <h3 class="text-sm font-semibold text-white flex items-center gap-2">
                <span>🛡️</span> Real-time MX & SMTP Handshake Verifier
              </h3>
              <p class="text-xs text-slate-400">
                Pre-flight validation to protect sender reputation before enrolling leads into cadences.
              </p>

              <div class="flex gap-2">
                <input
                  type="email"
                  [(ngModel)]="verificationEmail"
                  placeholder="prospect@company.com"
                  class="flex-1 bg-slate-950 border border-slate-800 rounded-lg px-3 py-2 text-xs text-slate-200" />
                <button
                  (click)="runVerification()"
                  [disabled]="isVerifying()"
                  class="px-4 py-2 bg-emerald-600 hover:bg-emerald-500 disabled:opacity-50 text-white rounded-lg text-xs font-semibold transition">
                  {{ isVerifying() ? 'Pinging...' : 'Verify Mailbox' }}
                </button>
              </div>

              @if (verificationResult()) {
                <div class="p-4 rounded-xl bg-slate-950 border border-slate-800 space-y-3">
                  <div class="flex items-center justify-between">
                    <span class="text-xs font-mono text-slate-300">{{ verificationResult()?.email }}</span>
                    <span [class]="getBadgeClass(verificationResult()?.status)" class="text-xs px-2.5 py-0.5 rounded-full font-semibold">
                      {{ verificationResult()?.status }}
                    </span>
                  </div>

                  <div class="grid grid-cols-2 md:grid-cols-4 gap-2 text-[11px] pt-2 border-t border-slate-800">
                    <div>
                      <span class="text-slate-500 block">RFC Syntax</span>
                      <span class="text-white font-mono">{{ verificationResult()?.syntaxValid ? 'VALID' : 'INVALID' }}</span>
                    </div>
                    <div>
                      <span class="text-slate-500 block">Disposable</span>
                      <span class="text-white font-mono">{{ verificationResult()?.isDisposable ? 'YES (Risk)' : 'NO' }}</span>
                    </div>
                    <div>
                      <span class="text-slate-500 block">MX Records</span>
                      <span class="text-white font-mono">{{ verificationResult()?.hasMxRecords ? 'FOUND' : 'MISSING' }}</span>
                    </div>
                    <div>
                      <span class="text-slate-500 block">Bounce Risk Score</span>
                      <span class="text-white font-mono font-bold">{{ verificationResult()?.riskScore }}/100</span>
                    </div>
                  </div>

                  <div class="text-xs text-slate-400 bg-slate-900/70 p-2.5 rounded border border-slate-800">
                    <strong>Verdict:</strong> {{ verificationResult()?.reason }}
                  </div>
                </div>
              }
            </div>
          }

          <!-- TAB 3: CONTACT ENRICHMENT -->
          @if (activeTab() === 'enrichment') {
            <div class="max-w-3xl mx-auto p-6 rounded-xl bg-slate-900/60 border border-slate-800 space-y-5">
              <h3 class="text-sm font-semibold text-white flex items-center gap-2">
                <span>🔍</span> B2B Contact Enrichment & Verification Pipeline
              </h3>

              <div class="grid grid-cols-2 gap-3">
                <input type="text" [(ngModel)]="enrichFirstName" placeholder="First Name (e.g. Marc)" class="bg-slate-950 border border-slate-800 rounded-lg p-2 text-xs text-slate-200" />
                <input type="text" [(ngModel)]="enrichLastName" placeholder="Last Name (e.g. Andreessen)" class="bg-slate-950 border border-slate-800 rounded-lg p-2 text-xs text-slate-200" />
                <input type="email" [(ngModel)]="enrichEmail" placeholder="Work Email" class="bg-slate-950 border border-slate-800 rounded-lg p-2 text-xs text-slate-200" />
                <input type="text" [(ngModel)]="enrichTitle" placeholder="Job Title (e.g. General Partner & Co-Founder)" class="bg-slate-950 border border-slate-800 rounded-lg p-2 text-xs text-slate-200" />
              </div>

              <button
                (click)="runEnrichment()"
                [disabled]="isEnriching()"
                class="w-full py-2 bg-indigo-600 hover:bg-indigo-500 disabled:opacity-50 text-white rounded-lg text-xs font-semibold transition">
                {{ isEnriching() ? 'Enriching...' : 'Run Autonomous Data Pipeline' }}
              </button>

              @if (enrichmentResult()) {
                <div class="p-5 rounded-xl bg-slate-950 border border-slate-800 space-y-4">
                  <div class="flex items-center justify-between">
                    <div>
                      <h4 class="text-base font-semibold text-white">{{ enrichmentResult()?.firstName }} {{ enrichmentResult()?.lastName }}</h4>
                      <span class="text-xs text-indigo-400">{{ enrichmentResult()?.jobTitle }} • {{ enrichmentResult()?.seniorityLevel }}</span>
                    </div>
                    <span class="bg-emerald-500/10 text-emerald-400 border border-emerald-500/20 text-xs px-3 py-1 rounded-full font-mono">
                      {{ enrichmentResult()?.overallDataConfidenceScore }}% Confidence
                    </span>
                  </div>

                  <div class="grid grid-cols-3 gap-3 text-xs pt-3 border-t border-slate-800">
                    <div>
                      <span class="text-slate-500 block text-[10px]">Company</span>
                      <span class="text-slate-200 font-semibold">{{ enrichmentResult()?.companyName }}</span>
                    </div>
                    <div>
                      <span class="text-slate-500 block text-[10px]">Industry</span>
                      <span class="text-slate-200">{{ enrichmentResult()?.industry }}</span>
                    </div>
                    <div>
                      <span class="text-slate-500 block text-[10px]">Headcount</span>
                      <span class="text-slate-200">{{ enrichmentResult()?.employeeRange }} employees</span>
                    </div>
                  </div>

                  <div class="p-3 bg-slate-900/60 rounded border border-slate-800 text-xs space-y-1">
                    <span class="text-slate-500 text-[10px] block uppercase font-mono">Identified Tech Stack</span>
                    <span class="text-slate-300 font-mono text-[11px]">{{ enrichmentResult()?.techStack }}</span>
                  </div>
                </div>
              }
            </div>
          }
        </div>
      </main>
    </div>
  `
})
export class SpintaxEngineComponent {
  private api = inject(ApiService);
  private auth = inject(AuthService);

  public activeTab = signal<'spintax' | 'verifier' | 'enrichment'>('spintax');

  // Spintax
  public spintaxTemplate = '{Hi|Hello {there|friend}}, {FirstName}! {Hope you are having a {great|productive} week.|Hope all is well with you.} We recently helped similar companies scale outreach by 3x.';
  public prospectSeedEmail = 'ceo@targetcorp.io';
  public firstNameVar = 'Sarah';
  public isLoadingSpintax = signal(false);
  public spintaxResult = signal<SpintaxPreviewResponse | null>(null);

  // Verifier
  public verificationEmail = 'alex.founder@growth-scale.io';
  public isVerifying = signal(false);
  public verificationResult = signal<EmailVerificationResult | null>(null);

  // Enrichment
  public enrichFirstName = 'Alex';
  public enrichLastName = 'Vance';
  public enrichEmail = 'alex@fintech-ventures.com';
  public enrichTitle = 'VP Engineering & Infrastructure';
  public isEnriching = signal(false);
  public enrichmentResult = signal<EnrichedContactDto | null>(null);

  public generateSpintaxPreview() {
    this.isLoadingSpintax.set(true);
    this.api.previewSpintax(
      this.spintaxTemplate,
      this.prospectSeedEmail,
      { FirstName: this.firstNameVar },
      3
    ).subscribe({
      next: res => {
        this.spintaxResult.set(res);
        this.isLoadingSpintax.set(false);
      },
      error: () => this.isLoadingSpintax.set(false)
    });
  }

  public runVerification() {
    this.isVerifying.set(true);
    this.api.verifyEmailDeliverability(this.verificationEmail).subscribe({
      next: res => {
        this.verificationResult.set(res);
        this.isVerifying.set(false);
      },
      error: () => this.isVerifying.set(false)
    });
  }

  public runEnrichment() {
    this.isEnriching.set(true);
    const tenantId = this.auth.currentUser()?.tenantId || '00000000-0000-0000-0000-000000000000';
    this.api.enrichAndVerifyContact(tenantId, {
      firstName: this.enrichFirstName,
      lastName: this.enrichLastName,
      email: this.enrichEmail,
      jobTitle: this.enrichTitle
    }).subscribe({
      next: res => {
        this.enrichmentResult.set(res);
        this.isEnriching.set(false);
      },
      error: () => this.isEnriching.set(false)
    });
  }

  public getBadgeClass(status?: string): string {
    switch (status) {
      case 'Deliverable': return 'bg-emerald-500/20 text-emerald-400 border border-emerald-500/30';
      case 'CatchAll': return 'bg-amber-500/20 text-amber-400 border border-amber-500/30';
      case 'Disposable': return 'bg-rose-500/20 text-rose-400 border border-rose-500/30';
      default: return 'bg-rose-500/20 text-rose-400 border border-rose-500/30';
    }
  }
}
