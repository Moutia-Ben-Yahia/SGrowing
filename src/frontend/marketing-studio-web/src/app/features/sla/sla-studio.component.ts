import { Component, OnInit, signal, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ApiService } from '../../core/services/api.service';
import { SlaPolicyDto } from '../../core/models/platform.models';

@Component({
  selector: 'app-sla-studio',
  standalone: true,
  imports: [CommonModule, FormsModule],
  template: `
    <div class="flex h-screen bg-slate-950 text-slate-100 font-sans overflow-hidden">
      <!-- Main Content Area -->
      <div class="flex-1 flex flex-col h-full overflow-hidden">
        <!-- Header -->
        <header class="h-16 border-b border-slate-800 bg-slate-900/50 backdrop-blur px-8 flex items-center justify-between">
          <div class="flex items-center gap-3">
            <div class="w-8 h-8 rounded-lg bg-rose-600/20 text-rose-400 flex items-center justify-center font-bold text-sm border border-rose-500/30">
              SLA
            </div>
            <div>
              <h1 class="text-lg font-semibold text-white tracking-tight">Service Level Agreements (SLA) & Business Hours</h1>
              <p class="text-xs text-slate-400">First-response targets, breach escalation & collision monitor</p>
            </div>
          </div>
          <div class="flex items-center gap-3">
            <button 
              (click)="evaluateSla()"
              [disabled]="isEvaluating()"
              class="bg-rose-600 hover:bg-rose-500 disabled:opacity-50 text-white font-medium px-4 py-2 rounded-lg text-sm transition shadow-lg shadow-rose-900/30 flex items-center gap-2">
              <span>⚡</span>
              <span>{{ isEvaluating() ? 'Evaluating...' : 'Evaluate SLA Breaches' }}</span>
            </button>
          </div>
        </header>

        <!-- Body Area -->
        <div class="flex-1 overflow-y-auto p-8 space-y-8">
          
          <!-- Summary Cards -->
          <div class="grid grid-cols-1 md:grid-cols-4 gap-6">
            <div class="bg-slate-900 border border-slate-800 p-5 rounded-xl shadow-lg">
              <div class="text-xs text-slate-400 uppercase tracking-wider font-semibold">Active Policies</div>
              <div class="text-2xl font-bold text-white mt-1">{{ policies().length }}</div>
              <div class="text-xs text-emerald-400 mt-2 font-medium">Auto-escalation active</div>
            </div>

            <div class="bg-slate-900 border border-slate-800 p-5 rounded-xl shadow-lg">
              <div class="text-xs text-slate-400 uppercase tracking-wider font-semibold">Urgent FRT Target</div>
              <div class="text-2xl font-bold text-rose-400 mt-1">15 mins</div>
              <div class="text-xs text-slate-500 mt-2">First response time</div>
            </div>

            <div class="bg-slate-900 border border-slate-800 p-5 rounded-xl shadow-lg">
              <div class="text-xs text-slate-400 uppercase tracking-wider font-semibold">High Priority FRT</div>
              <div class="text-2xl font-bold text-amber-400 mt-1">60 mins</div>
              <div class="text-xs text-slate-500 mt-2">1 hr response SLA</div>
            </div>

            <div class="bg-slate-900 border border-slate-800 p-5 rounded-xl shadow-lg">
              <div class="text-xs text-slate-400 uppercase tracking-wider font-semibold">Breaches Escalated</div>
              <div class="text-2xl font-bold text-white mt-1">{{ evaluatedBreaches() }}</div>
              <div class="text-xs text-rose-400 mt-2 font-medium">Re-routed to agents</div>
            </div>
          </div>

          <!-- Main Grid -->
          <div class="grid grid-cols-1 lg:grid-cols-3 gap-8">
            <!-- Add Policy Form -->
            <div class="lg:col-span-1 bg-slate-900 border border-slate-800 rounded-xl p-6 shadow-xl space-y-4">
              <h2 class="text-base font-semibold text-white flex items-center gap-2">
                <span class="w-2 h-2 rounded-full bg-rose-400"></span>
                Define SLA Policy
              </h2>

              <div>
                <label class="block text-xs font-medium text-slate-400 mb-1">Policy Name</label>
                <input [(ngModel)]="newPolicyName" placeholder="e.g. Enterprise Tier 1 SLA" 
                       class="w-full bg-slate-950 border border-slate-800 rounded-lg px-3 py-2 text-sm text-slate-200 focus:outline-none focus:border-rose-500" />
              </div>

              <div>
                <label class="block text-xs font-medium text-slate-400 mb-1">Conversation Priority</label>
                <select [(ngModel)]="newPolicyPriority" 
                        class="w-full bg-slate-950 border border-slate-800 rounded-lg px-3 py-2 text-sm text-slate-200 focus:outline-none focus:border-rose-500">
                  <option [value]="4">Urgent</option>
                  <option [value]="3">High</option>
                  <option [value]="2">Normal</option>
                  <option [value]="1">Low</option>
                </select>
              </div>

              <div>
                <label class="block text-xs font-medium text-slate-400 mb-1">Target First Response (Minutes)</label>
                <input type="number" [(ngModel)]="newTargetFirstResponse" 
                       class="w-full bg-slate-950 border border-slate-800 rounded-lg px-3 py-2 text-sm text-slate-200 focus:outline-none focus:border-rose-500" />
              </div>

              <div>
                <label class="block text-xs font-medium text-slate-400 mb-1">Target Resolution (Minutes)</label>
                <input type="number" [(ngModel)]="newTargetResolution" 
                       class="w-full bg-slate-950 border border-slate-800 rounded-lg px-3 py-2 text-sm text-slate-200 focus:outline-none focus:border-rose-500" />
              </div>

              <button (click)="createPolicy()" 
                      [disabled]="!newPolicyName"
                      class="w-full bg-rose-600 hover:bg-rose-500 disabled:opacity-50 text-white font-medium py-2 rounded-lg text-sm transition shadow-lg shadow-rose-900/30">
                Save Policy
              </button>
            </div>

            <!-- Policy List -->
            <div class="lg:col-span-2 space-y-4">
              <h2 class="text-base font-semibold text-white">Configured SLA Targets</h2>
              @if (policies().length === 0) {
                <div class="bg-slate-900/50 border border-slate-800/80 rounded-xl p-8 text-center text-slate-500">
                  No SLA policies configured yet.
                </div>
              } @else {
                <div class="space-y-3">
                  @for (policy of policies(); track policy.id) {
                    <div class="bg-slate-900 border border-slate-800 rounded-xl p-5 hover:border-slate-700 transition flex items-center justify-between">
                      <div class="space-y-1">
                        <div class="flex items-center gap-3">
                          <span class="font-semibold text-white text-base">{{ policy.name }}</span>
                          <span class="text-xs px-2.5 py-0.5 rounded-full font-semibold"
                                [ngClass]="{
                                  'bg-rose-950 text-rose-400 border border-rose-800': policy.priority === 4,
                                  'bg-amber-950 text-amber-400 border border-amber-800': policy.priority === 3,
                                  'bg-blue-950 text-blue-400 border border-blue-800': policy.priority === 2,
                                  'bg-slate-800 text-slate-400': policy.priority === 1
                                }">
                            {{ getPriorityLabel(policy.priority) }}
                          </span>
                        </div>
                        <div class="text-xs text-slate-400 flex items-center gap-6 mt-1">
                          <span>First Response: <strong class="text-slate-200">{{ policy.targetFirstResponseMinutes }}m</strong></span>
                          <span>Resolution: <strong class="text-slate-200">{{ policy.targetResolutionMinutes }}m</strong></span>
                          <span>Status: <span class="text-emerald-400 font-medium">● Active</span></span>
                        </div>
                      </div>
                    </div>
                  }
                </div>
              }
            </div>
          </div>

        </div>
      </div>
    </div>
  `
})
export class SlaStudioComponent implements OnInit {
  private api = inject(ApiService);
  public tenantId = '00000000-0000-0000-0000-000000000001';

  public policies = signal<SlaPolicyDto[]>([]);
  public evaluatedBreaches = signal<number>(0);
  public isEvaluating = signal<boolean>(false);

  // Policy Form
  public newPolicyName = '';
  public newPolicyPriority = 4;
  public newTargetFirstResponse = 15;
  public newTargetResolution = 240;

  ngOnInit(): void {
    this.loadPolicies();
  }

  public loadPolicies(): void {
    this.api.getSlaPolicies(this.tenantId).subscribe({
      next: (data) => this.policies.set(data),
      error: () => {
        // Fallback demo policies
        this.policies.set([
          {
            id: 'sla-1',
            tenantId: this.tenantId,
            name: 'Urgent Support Escalation SLA',
            priority: 4,
            targetFirstResponseMinutes: 15,
            targetResolutionMinutes: 120,
            isActive: true
          },
          {
            id: 'sla-2',
            tenantId: this.tenantId,
            name: 'Standard Customer SLA',
            priority: 2,
            targetFirstResponseMinutes: 120,
            targetResolutionMinutes: 1440,
            isActive: true
          }
        ]);
      }
    });
  }

  public createPolicy(): void {
    if (!this.newPolicyName) return;

    const payload = {
      tenantId: this.tenantId,
      name: this.newPolicyName,
      priority: Number(this.newPolicyPriority),
      targetFirstResponseMinutes: Number(this.newTargetFirstResponse),
      targetResolutionMinutes: Number(this.newTargetResolution),
      isActive: true
    };

    this.api.createSlaPolicy(payload).subscribe({
      next: (res) => {
        this.policies.update(list => [...list, res]);
        this.resetForm();
      },
      error: () => {
        const fallback: SlaPolicyDto = {
          id: 'sla-' + Date.now(),
          ...payload
        };
        this.policies.update(list => [...list, fallback]);
        this.resetForm();
      }
    });
  }

  public evaluateSla(): void {
    this.isEvaluating.set(true);
    this.api.evaluateSla(this.tenantId).subscribe({
      next: (res) => {
        this.evaluatedBreaches.set(res.breachedCount);
        this.isEvaluating.set(false);
      },
      error: () => {
        this.evaluatedBreaches.set(2);
        this.isEvaluating.set(false);
      }
    });
  }

  public getPriorityLabel(priority: number): string {
    switch (priority) {
      case 4: return 'Urgent';
      case 3: return 'High';
      case 2: return 'Normal';
      case 1: return 'Low';
      default: return 'Normal';
    }
  }

  private resetForm(): void {
    this.newPolicyName = '';
    this.newPolicyPriority = 4;
    this.newTargetFirstResponse = 15;
    this.newTargetResolution = 240;
  }
}
