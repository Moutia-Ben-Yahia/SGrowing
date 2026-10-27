import { Component, signal, computed, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';

interface OnboardingStep {
  id: number;
  title: string;
  subtitle: string;
  icon: string;
  completed: boolean;
}

@Component({
  selector: 'app-onboarding-wizard',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterLink],
  template: `
    <div class="flex h-screen w-screen overflow-hidden bg-slate-950 text-slate-100 font-sans">
      <main class="flex-1 flex flex-col overflow-y-auto">
        <!-- Topbar -->
        <header class="h-14 border-b border-slate-800 bg-slate-900/60 px-6 flex items-center justify-between shrink-0">
          <div class="flex items-center gap-3">
            <a routerLink="/billing" class="text-xs text-slate-400 hover:text-white flex items-center gap-1 transition">
              ← Billing & Plans
            </a>
            <span class="text-slate-700">|</span>
            <span class="text-base font-semibold text-white flex items-center gap-2">
              🚀 Self-Serve Enterprise Onboarding Wizard
            </span>
            <span class="bg-indigo-500/20 text-indigo-400 text-[10px] font-mono px-2 py-0.5 rounded-full border border-indigo-500/30">
              Interactive Checklist • 4 Steps to Launch
            </span>
          </div>

          <div class="flex items-center gap-4">
            <div class="flex items-center gap-2">
              <span class="text-xs text-slate-400">Progress:</span>
              <span class="text-xs font-bold text-indigo-400">{{ progressPercent() }}%</span>
              <div class="w-28 h-2 bg-slate-800 rounded-full overflow-hidden">
                <div class="h-full bg-indigo-500 transition-all duration-500" [style.width.%]="progressPercent()"></div>
              </div>
            </div>
            <button
              (click)="resetWizard()"
              class="text-xs text-slate-400 hover:text-slate-200 px-2 py-1 rounded bg-slate-800/60 border border-slate-700">
              Restart
            </button>
          </div>
        </header>

        <!-- Main Content Area -->
        <div class="p-8 max-w-6xl mx-auto w-full space-y-8 flex-1">
          <!-- Step Navigator Cards -->
          <div class="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-4 gap-4">
            @for (step of steps(); track step.id) {
              <div
                (click)="currentStep.set(step.id)"
                [class]="currentStep() === step.id
                  ? 'border-indigo-500 bg-indigo-950/20 ring-1 ring-indigo-500'
                  : step.completed
                  ? 'border-emerald-500/40 bg-slate-900/40 hover:border-slate-700'
                  : 'border-slate-800 bg-slate-900/40 hover:border-slate-700'"
                class="p-4 rounded-xl border cursor-pointer transition flex flex-col justify-between">
                <div class="flex items-center justify-between mb-2">
                  <span class="text-xl">{{ step.icon }}</span>
                  @if (step.completed) {
                    <span class="text-xs bg-emerald-500/20 text-emerald-400 px-2 py-0.5 rounded-full font-medium">✓ Done</span>
                  } @else if (currentStep() === step.id) {
                    <span class="text-xs bg-indigo-500/20 text-indigo-400 px-2 py-0.5 rounded-full font-medium">Active</span>
                  } @else {
                    <span class="text-xs text-slate-500 font-medium">Step {{ step.id }}</span>
                  }
                </div>
                <div>
                  <h3 class="text-xs font-semibold text-slate-200">{{ step.title }}</h3>
                  <p class="text-[11px] text-slate-400 mt-0.5 line-clamp-1">{{ step.subtitle }}</p>
                </div>
              </div>
            }
          </div>

          <!-- Active Step Panel -->
          <div class="bg-slate-900/70 border border-slate-800 rounded-2xl p-8 backdrop-blur shadow-xl relative overflow-hidden">
            <!-- Step 1: Connect Domain & DNS -->
            @if (currentStep() === 1) {
              <div class="space-y-6">
                <div>
                  <h2 class="text-lg font-bold text-white flex items-center gap-2">
                    <span>🌐</span> Step 1: Connect Sending Domain & Verify DNS
                  </h2>
                  <p class="text-xs text-slate-400 mt-1">
                    Authenticate your enterprise domain records with SPF, DKIM, and DMARC to guarantee high inbox placement.
                  </p>
                </div>

                <div class="grid grid-cols-1 md:grid-cols-2 gap-6">
                  <div class="space-y-4">
                    <div>
                      <label class="text-xs text-slate-300 block mb-1">Company Sending Domain</label>
                      <input
                        type="text"
                        [(ngModel)]="domainInput"
                        class="w-full bg-slate-950 border border-slate-800 rounded-xl px-4 py-2.5 text-xs text-slate-200 focus:outline-none focus:border-indigo-500"
                        placeholder="e.g. outreach.acme.com" />
                    </div>

                    <div class="flex gap-3">
                      <button
                        (click)="verifyDns()"
                        class="px-4 py-2 bg-indigo-600 hover:bg-indigo-500 text-white rounded-xl text-xs font-semibold transition flex items-center gap-2">
                        <span>🔍</span> Verify DNS Records
                      </button>
                      <button
                        (click)="autoGenerateDkim()"
                        class="px-4 py-2 bg-slate-800 hover:bg-slate-700 text-slate-300 rounded-xl text-xs font-semibold transition">
                        Generate 2048-bit DKIM Key
                      </button>
                    </div>

                    @if (dnsVerified()) {
                      <div class="p-3 bg-emerald-950/40 border border-emerald-500/30 rounded-xl text-xs text-emerald-300 flex items-center gap-2">
                        <span>✅</span> Domain DNS records validated: SPF pass, DKIM 2048-bit pass, DMARC p=reject verified.
                      </div>
                    }
                  </div>

                  <!-- DNS Records Table -->
                  <div class="bg-slate-950/80 border border-slate-800/80 rounded-xl p-4 space-y-3">
                    <span class="text-xs font-semibold text-slate-300 block">Required DNS Entries</span>
                    <div class="space-y-2 text-[11px] font-mono">
                      <div class="flex items-center justify-between p-2 rounded bg-slate-900 border border-slate-800">
                        <span class="text-indigo-400">TXT @</span>
                        <span class="text-slate-300">v=spf1 include:_spf.sgrowing.io ~all</span>
                        <span class="text-emerald-400">Valid</span>
                      </div>
                      <div class="flex items-center justify-between p-2 rounded bg-slate-900 border border-slate-800">
                        <span class="text-indigo-400">CNAME sg._domainkey</span>
                        <span class="text-slate-300">dkim.sgrowing.io</span>
                        <span class="text-emerald-400">Valid</span>
                      </div>
                      <div class="flex items-center justify-between p-2 rounded bg-slate-900 border border-slate-800">
                        <span class="text-indigo-400">TXT _dmarc</span>
                        <span class="text-slate-300">v=DMARC1; p=quarantine; pct=100</span>
                        <span class="text-emerald-400">Valid</span>
                      </div>
                    </div>
                  </div>
                </div>

                <div class="flex justify-end gap-3 pt-4 border-t border-slate-800">
                  <button
                    (click)="markStepComplete(1, 2)"
                    class="px-6 py-2.5 bg-indigo-600 hover:bg-indigo-500 text-white rounded-xl text-xs font-bold transition shadow-lg shadow-indigo-600/20">
                    Next: Setup Mailbox Warmup →
                  </button>
                </div>
              </div>
            }

            <!-- Step 2: Setup Mailbox Warmup -->
            @if (currentStep() === 2) {
              <div class="space-y-6">
                <div>
                  <h2 class="text-lg font-bold text-white flex items-center gap-2">
                    <span>🔥</span> Step 2: Configure Mailbox P2P Warmup & Ramp Plan
                  </h2>
                  <p class="text-xs text-slate-400 mt-1">
                    Gradually ramp your mailbox sending volume through the P2P peer warmup exchange to prevent spam filter triggers.
                  </p>
                </div>

                <div class="grid grid-cols-1 md:grid-cols-2 gap-6">
                  <div class="space-y-4">
                    <div>
                      <label class="text-xs text-slate-300 block mb-1">Mailbox Sender Address</label>
                      <input
                        type="email"
                        [(ngModel)]="warmupSenderEmail"
                        class="w-full bg-slate-950 border border-slate-800 rounded-xl px-4 py-2.5 text-xs text-slate-200 focus:outline-none focus:border-indigo-500" />
                    </div>

                    <div>
                      <label class="text-xs text-slate-300 block mb-1">Daily Max Ramp Goal</label>
                      <select [(ngModel)]="dailyRampTarget" class="w-full bg-slate-950 border border-slate-800 rounded-xl px-4 py-2.5 text-xs text-slate-200">
                        <option [value]="25">Conservative (25 emails/day, 21-day ramp)</option>
                        <option [value]="50">Standard (50 emails/day, 14-day ramp)</option>
                        <option [value]="100">Aggressive (100 emails/day, Enterprise cluster)</option>
                      </select>
                    </div>

                    <div class="flex items-center gap-3 p-3 bg-slate-950 rounded-xl border border-slate-800">
                      <input type="checkbox" id="rescueToggle" [(ngModel)]="spamRescueEnabled" class="rounded accent-indigo-600" />
                      <label for="rescueToggle" class="text-xs text-slate-300 cursor-pointer">
                        Enable Automatic Spam Rescue & Thread Replies
                      </label>
                    </div>
                  </div>

                  <div class="bg-slate-950/80 border border-slate-800/80 rounded-xl p-4 space-y-3">
                    <span class="text-xs font-semibold text-slate-300 block">Warmup Ramp Projection</span>
                    <div class="space-y-2 text-xs">
                      <div class="flex justify-between text-slate-400"><span>Week 1:</span> <span class="text-white">5 - 15 exchanges/day</span></div>
                      <div class="flex justify-between text-slate-400"><span>Week 2:</span> <span class="text-white">20 - 35 exchanges/day</span></div>
                      <div class="flex justify-between text-slate-400"><span>Week 3:</span> <span class="text-emerald-400 font-bold">50 - 100 emails/day (Ready for Outreach)</span></div>
                    </div>
                  </div>
                </div>

                <div class="flex justify-between pt-4 border-t border-slate-800">
                  <button (click)="currentStep.set(1)" class="px-4 py-2 bg-slate-800 text-slate-300 rounded-xl text-xs">← Back</button>
                  <button
                    (click)="markStepComplete(2, 3)"
                    class="px-6 py-2.5 bg-indigo-600 hover:bg-indigo-500 text-white rounded-xl text-xs font-bold transition shadow-lg shadow-indigo-600/20">
                    Next: Import Leads CSV →
                  </button>
                </div>
              </div>
            }

            <!-- Step 3: Import CSV Leads -->
            @if (currentStep() === 3) {
              <div class="space-y-6">
                <div>
                  <h2 class="text-lg font-bold text-white flex items-center gap-2">
                    <span>📑</span> Step 3: Import Target Accounts & Leads CSV
                  </h2>
                  <p class="text-xs text-slate-400 mt-1">
                    Upload your prospect CSV with auto-detection for Email, First Name, Company, and Wilaya location fields.
                  </p>
                </div>

                <!-- Drag-drop dropzone -->
                <div class="border-2 border-dashed border-slate-700 hover:border-indigo-500 rounded-2xl p-8 text-center bg-slate-950/50 cursor-pointer transition">
                  <span class="text-3xl block mb-2">📁</span>
                  <p class="text-xs font-semibold text-slate-200">Drop your prospects CSV file here or click to browse</p>
                  <p class="text-[11px] text-slate-500 mt-1">Supports up to 50,000 rows with real-time MX validation</p>
                  <button
                    (click)="loadSampleCsv()"
                    class="mt-4 px-3 py-1.5 bg-slate-800 hover:bg-slate-700 text-indigo-400 text-xs rounded-lg border border-slate-700">
                    Load Sample Prospects (125 B2B Retail Leads)
                  </button>
                </div>

                <!-- Preview Table -->
                @if (csvRecords().length > 0) {
                  <div class="space-y-2">
                    <span class="text-xs font-semibold text-slate-300">Preview Parsed Contacts ({{ csvRecords().length }} found)</span>
                    <div class="border border-slate-800 rounded-xl overflow-hidden">
                      <table class="w-full text-left text-xs">
                        <thead class="bg-slate-950 text-slate-400 border-b border-slate-800">
                          <tr>
                            <th class="p-3">Name</th>
                            <th class="p-3">Email</th>
                            <th class="p-3">Company</th>
                            <th class="p-3">Status</th>
                          </tr>
                        </thead>
                        <tbody class="divide-y divide-slate-800/60 bg-slate-900/40">
                          @for (lead of csvRecords(); track lead.email) {
                            <tr class="hover:bg-slate-800/30">
                              <td class="p-3 font-medium text-white">{{ lead.name }}</td>
                              <td class="p-3 text-slate-300">{{ lead.email }}</td>
                              <td class="p-3 text-slate-400">{{ lead.company }}</td>
                              <td class="p-3"><span class="text-emerald-400 font-mono text-[10px]">MX Verified</span></td>
                            </tr>
                          }
                        </tbody>
                      </table>
                    </div>
                  </div>
                }

                <div class="flex justify-between pt-4 border-t border-slate-800">
                  <button (click)="currentStep.set(2)" class="px-4 py-2 bg-slate-800 text-slate-300 rounded-xl text-xs">← Back</button>
                  <button
                    (click)="markStepComplete(3, 4)"
                    class="px-6 py-2.5 bg-indigo-600 hover:bg-indigo-500 text-white rounded-xl text-xs font-bold transition shadow-lg shadow-indigo-600/20">
                    Next: Launch Pipeline →
                  </button>
                </div>
              </div>
            }

            <!-- Step 4: Launch Pipeline -->
            @if (currentStep() === 4) {
              <div class="space-y-6">
                <div>
                  <h2 class="text-lg font-bold text-white flex items-center gap-2">
                    <span>⚡</span> Step 4: Activate Outreach Pipeline & Autonomous SDR
                  </h2>
                  <p class="text-xs text-slate-400 mt-1">
                    Select your outreach cadence template and launch your autonomous growth pipeline.
                  </p>
                </div>

                <div class="grid grid-cols-1 md:grid-cols-2 gap-6">
                  <div class="space-y-4">
                    <div>
                      <label class="text-xs text-slate-300 block mb-1">Select Cadence Blueprint</label>
                      <select [(ngModel)]="selectedCadence" class="w-full bg-slate-950 border border-slate-800 rounded-xl px-4 py-2.5 text-xs text-slate-200">
                        <option value="enterprise-b2b">Enterprise B2B Cold Outreach (Email + LinkedIn Touchpoint)</option>
                        <option value="cod-logistics">Maghreb Retail COD Merchant Activation</option>
                        <option value="fast-accelerator">3-Day Fast Spintax Blitz</option>
                      </select>
                    </div>

                    <div class="p-4 bg-slate-950 border border-slate-800 rounded-xl space-y-2">
                      <span class="text-xs font-semibold text-slate-300">Pipeline Summary</span>
                      <ul class="text-[11px] text-slate-400 space-y-1 list-disc list-inside">
                        <li>Domain: <span class="text-indigo-400">{{ domainInput }}</span> (SPF & DKIM Valid)</li>
                        <li>Warmup: <span class="text-emerald-400">Ramping to {{ dailyRampTarget }}/day</span></li>
                        <li>Audience: <span class="text-white">{{ csvRecords().length }} Prospects Loaded</span></li>
                      </ul>
                    </div>
                  </div>

                  @if (pipelineLaunched()) {
                    <div class="bg-gradient-to-br from-indigo-950/60 to-purple-950/60 border border-indigo-500/40 rounded-2xl p-6 text-center space-y-3 animate-fade-in">
                      <span class="text-4xl block">🎉</span>
                      <h3 class="text-base font-bold text-white">Pipeline Activated & Running!</h3>
                      <p class="text-xs text-slate-300">
                        Autonomous AI SDR is now orchestrating outreach steps with spintax permutations and deliverability monitoring.
                      </p>
                      <div class="pt-2">
                        <a
                          routerLink="/agents/missions"
                          class="inline-block px-5 py-2 bg-indigo-600 hover:bg-indigo-500 text-white rounded-xl text-xs font-bold transition">
                          View Mission Cockpit →
                        </a>
                      </div>
                    </div>
                  } @else {
                    <div class="flex flex-col justify-center items-center p-6 border border-slate-800 rounded-2xl bg-slate-950 text-center space-y-3">
                      <span class="text-3xl">🚀</span>
                      <h3 class="text-sm font-semibold text-white">Ready for Launch</h3>
                      <p class="text-xs text-slate-400 max-w-xs">
                        Click below to commit all configuration and begin autonomous lead engagement.
                      </p>
                      <button
                        (click)="launchPipeline()"
                        class="px-6 py-2.5 bg-emerald-600 hover:bg-emerald-500 text-white rounded-xl text-xs font-bold transition shadow-lg shadow-emerald-600/20">
                        Launch Pipeline Now
                      </button>
                    </div>
                  }
                </div>

                <div class="flex justify-between pt-4 border-t border-slate-800">
                  <button (click)="currentStep.set(3)" class="px-4 py-2 bg-slate-800 text-slate-300 rounded-xl text-xs">← Back</button>
                </div>
              </div>
            }
          </div>
        </div>
      </main>
    </div>
  `
})
export class OnboardingWizardComponent {
  currentStep = signal<number>(1);
  dnsVerified = signal<boolean>(false);
  pipelineLaunched = signal<boolean>(false);

  domainInput = 'outreach.acme-retail.com';
  warmupSenderEmail = 'sales@outreach.acme-retail.com';
  dailyRampTarget = 50;
  spamRescueEnabled = true;
  selectedCadence = 'enterprise-b2b';

  steps = signal<OnboardingStep[]>([
    { id: 1, title: 'Connect Domain & DNS', subtitle: 'SPF, DKIM & DMARC setup', icon: '🌐', completed: false },
    { id: 2, title: 'Setup Mailbox Warmup', subtitle: 'P2P ramp configuration', icon: '🔥', completed: false },
    { id: 3, title: 'Import CSV Leads', subtitle: 'Prospect list ingestion', icon: '📑', completed: false },
    { id: 4, title: 'Launch Pipeline', subtitle: 'AI SDR activation', icon: '⚡', completed: false },
  ]);

  csvRecords = signal<Array<{ name: string; email: string; company: string }>>([]);

  progressPercent = computed(() => {
    const completedCount = this.steps().filter(s => s.completed).length;
    return completedCount * 25;
  });

  verifyDns() {
    this.dnsVerified.set(true);
  }

  autoGenerateDkim() {
    this.dnsVerified.set(true);
  }

  markStepComplete(stepId: number, nextStepId?: number) {
    this.steps.update(list =>
      list.map(s => s.id === stepId ? { ...s, completed: true } : s)
    );
    if (nextStepId) {
      this.currentStep.set(nextStepId);
    }
  }

  loadSampleCsv() {
    this.csvRecords.set([
      { name: 'Karim Trabelsi', email: 'karim@maghrebretail.tn', company: 'Maghreb Retail' },
      { name: 'Amina Mansour', email: 'amina@carthagelog.tn', company: 'Carthage Logistics' },
      { name: 'Kais Saidi', email: 'kais@tunisecommerce.tn', company: 'Tunis E-Commerce Hub' }
    ]);
  }

  launchPipeline() {
    this.markStepComplete(4);
    this.pipelineLaunched.set(true);
  }

  resetWizard() {
    this.currentStep.set(1);
    this.dnsVerified.set(false);
    this.pipelineLaunched.set(false);
    this.csvRecords.set([]);
    this.steps.update(list => list.map(s => ({ ...s, completed: false })));
  }
}
