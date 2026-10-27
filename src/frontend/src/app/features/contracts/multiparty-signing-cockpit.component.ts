import { Component, signal, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { ApiService } from '../../core/services/api.service';
import { AuthService } from '../../core/services/auth.service';
import { MultiPartyContractDto, VisualPdfFieldDto } from '../../core/models/pillars.models';

@Component({
  selector: 'app-multiparty-signing-cockpit',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterLink],
  template: `
    <div class="flex h-screen w-screen overflow-hidden bg-slate-950 text-slate-100 font-sans">
      <main class="flex-1 flex flex-col overflow-y-auto">
        <!-- Topbar -->
        <header class="h-14 border-b border-slate-800 bg-slate-900/60 px-6 flex items-center justify-between shrink-0">
          <div class="flex items-center gap-3">
            <a routerLink="/contracts" class="text-xs text-slate-400 hover:text-white flex items-center gap-1 transition">
              ← Contracts Studio
            </a>
            <span class="text-slate-700">|</span>
            <span class="text-base font-semibold text-white flex items-center gap-2">
              ✍️ Multi-Party Cryptographic E-Signature & OTP Orchestrator
            </span>
            <span class="bg-emerald-500/20 text-emerald-400 text-[10px] font-mono px-2 py-0.5 rounded-full border border-emerald-500/30">
              Sequential • Parallel • Coordinate Placement
            </span>
          </div>

          <div class="flex items-center gap-3">
            <button
              (click)="createContract()"
              [disabled]="isCreating()"
              class="px-3.5 py-1.5 bg-emerald-600 hover:bg-emerald-500 disabled:opacity-50 text-white rounded-lg text-xs font-semibold transition">
              {{ isCreating() ? 'Generating...' : 'New Multi-Signer Agreement' }}
            </button>
          </div>
        </header>

        <!-- Body -->
        <div class="p-6 space-y-6 flex-1">
          @if (contract()) {
            <!-- Contract Overview Banner -->
            <div class="p-5 rounded-2xl bg-slate-900/60 border border-slate-800 flex items-center justify-between">
              <div>
                <span class="text-xs font-mono text-emerald-400">#{{ contract()?.contractId }}</span>
                <h3 class="text-base font-bold text-white mt-1">{{ contract()?.title }}</h3>
                <span class="text-xs text-slate-400">
                  Mode: {{ contract()?.workflowType === 0 ? 'Sequential (Step-by-Step)' : 'Parallel (Concurrent)' }} • Status:
                  <strong class="text-emerald-400 uppercase font-mono">{{ contract()?.overallStatus }}</strong>
                </span>
              </div>

              @if (contract()?.overallStatus === 'Draft') {
                <button
                  (click)="startWorkflow()"
                  class="px-4 py-2 bg-indigo-600 hover:bg-indigo-500 text-white rounded-xl text-xs font-semibold transition">
                  Dispatch Invitations
                </button>
              }
            </div>

            <!-- Workflow Signers Pipeline -->
            <div class="space-y-3">
              <h4 class="text-xs font-semibold text-slate-400 uppercase tracking-wider">Multi-Party Signer Pipeline</h4>
              <div class="grid grid-cols-1 md:grid-cols-2 gap-4">
                @for (signer of contract()?.signers; track signer.signerId) {
                  <div class="p-4 rounded-xl bg-slate-900/80 border border-slate-800 space-y-3">
                    <div class="flex items-center justify-between">
                      <div class="flex items-center gap-2">
                        <span class="w-6 h-6 rounded-full bg-slate-800 flex items-center justify-center text-xs font-bold font-mono text-slate-300">
                          {{ signer.orderIndex }}
                        </span>
                        <div>
                          <div class="text-xs font-bold text-white">{{ signer.fullName }}</div>
                          <span class="text-[11px] text-slate-400">{{ signer.email }}</span>
                        </div>
                      </div>

                      <span [class]="getSignerStatusBadge(signer.status)" class="text-[10px] px-2 py-0.5 rounded font-mono font-bold uppercase">
                        {{ getSignerStatusName(signer.status) }}
                      </span>
                    </div>

                    <!-- Signer Action Sandbox -->
                    @if (signer.status === 1 || signer.status === 2 || signer.status === 3) {
                      <div class="p-3 bg-slate-950 rounded-lg border border-slate-800/80 space-y-2">
                        <div class="flex items-center justify-between text-xs">
                          <span class="text-slate-400 text-[11px]">Signer OTP Verification:</span>
                          @if (!signer.isOtpVerified) {
                            <button
                              (click)="sendOtp(signer.signingToken)"
                              class="text-[10px] px-2 py-0.5 bg-slate-800 hover:bg-slate-700 text-emerald-400 rounded">
                              Send 6-digit Code
                            </button>
                          } @else {
                            <span class="text-emerald-400 text-[10px] font-bold">✓ OTP Verified</span>
                          }
                        </div>

                        <div class="pt-1 flex gap-2">
                          <button
                            (click)="simulateSign(signer.signingToken, signer.fullName)"
                            class="flex-1 py-1.5 bg-emerald-600 hover:bg-emerald-500 text-white rounded text-xs font-semibold transition">
                            Execute Electronic Signature
                          </button>
                        </div>
                      </div>
                    }
                  </div>
                }
              </div>
            </div>

            <!-- Visual PDF Field Coordinate Placement Canvas Preview -->
            <div class="p-5 rounded-xl bg-slate-900/60 border border-slate-800 space-y-3">
              <h4 class="text-xs font-semibold text-slate-400 uppercase tracking-wider">Visual PDF Coordinate Field Overlay (A4 Page 1)</h4>
              <div class="relative w-full max-w-xl h-72 mx-auto bg-slate-950 border border-slate-700/80 rounded-lg shadow-inner overflow-hidden">
                <div class="absolute inset-0 flex items-center justify-center text-slate-800 text-4xl font-bold select-none">
                  PDF PAGE 1
                </div>

                @for (field of contract()?.visualFields; track field.fieldId) {
                  <div
                    class="absolute border-2 border-dashed border-emerald-400 bg-emerald-500/10 rounded flex items-center justify-center text-[10px] font-mono text-emerald-300 shadow"
                    [style.left.px]="field.xCoordinate"
                    [style.top.px]="field.yCoordinate"
                    [style.width.px]="field.width"
                    [style.height.px]="field.height">
                    ✍️ {{ field.placeholder || 'Signer Box' }}
                  </div>
                }
              </div>
            </div>
          }
        </div>
      </main>
    </div>
  `
})
export class MultipartySigningCockpitComponent {
  private api = inject(ApiService);
  private auth = inject(AuthService);

  public isCreating = signal(false);
  public contract = signal<MultiPartyContractDto | null>(null);

  constructor() {
    this.createContract();
  }

  public createContract() {
    this.isCreating.set(true);
    const tenantId = this.auth.currentUser()?.tenantId || '00000000-0000-0000-0000-000000000000';
    this.api.createMultiPartyContract(tenantId, {
      title: 'Enterprise Master Subscription Agreement & SLA Schedule',
      documentUrl: 'https://storage.sgrowing.io/contracts/msa-demo.pdf',
      workflowType: 0, // Sequential
      signers: [
        { orderIndex: 1, fullName: 'Chief Executive Officer', email: 'ceo@clientcorp.com', requiresOtp: true },
        { orderIndex: 2, fullName: 'VP Finance & Controller', email: 'cfo@clientcorp.com', requiresOtp: false }
      ],
      visualFields: [
        { fieldId: 'f1', pageNumber: 1, xCoordinate: 40, yCoordinate: 160, width: 180, height: 50, fieldType: 0, signerEmail: 'ceo@clientcorp.com', isRequired: true, placeholder: 'CEO Signature' },
        { fieldId: 'f2', pageNumber: 1, xCoordinate: 280, yCoordinate: 160, width: 180, height: 50, fieldType: 0, signerEmail: 'cfo@clientcorp.com', isRequired: true, placeholder: 'CFO Countersign' }
      ]
    }).subscribe({
      next: res => {
        this.contract.set(res);
        this.isCreating.set(false);
      },
      error: () => {
        this.contract.set({
          contractId: 'demo-contract-001',
          tenantId: '00000000-0000-0000-0000-000000000001',
          title: 'Enterprise Master Subscription Agreement & SLA Schedule',
          documentUrl: 'https://storage.sgrowing.io/contracts/msa-demo.pdf',
          workflowType: 0,
          overallStatus: 'Draft',
          currentSequentialOrderIndex: 1,
          signers: [
            { signerId: 's1', orderIndex: 1, fullName: 'Chief Executive Officer', email: 'ceo@clientcorp.com', role: 0, status: 1, signingToken: 'token_ceo_1', isOtpVerified: false, requiresOtp: true },
            { signerId: 's2', orderIndex: 2, fullName: 'VP Finance & Controller', email: 'cfo@clientcorp.com', role: 0, status: 0, signingToken: 'token_cfo_2', isOtpVerified: false, requiresOtp: false }
          ],
          visualFields: [
            { fieldId: 'f1', pageNumber: 1, xCoordinate: 40, yCoordinate: 160, width: 180, height: 50, fieldType: 0, signerEmail: 'ceo@clientcorp.com', isRequired: true, placeholder: 'CEO Signature' },
            { fieldId: 'f2', pageNumber: 1, xCoordinate: 280, yCoordinate: 160, width: 180, height: 50, fieldType: 0, signerEmail: 'cfo@clientcorp.com', isRequired: true, placeholder: 'CFO Countersign' }
          ]
        });
        this.isCreating.set(false);
      }
    });
  }

  public startWorkflow() {
    const cur = this.contract();
    if (!cur) return;
    const tenantId = this.auth.currentUser()?.tenantId || '00000000-0000-0000-0000-000000000000';
    this.api.startSigningWorkflow(cur.contractId, tenantId).subscribe({
      next: res => this.contract.set(res)
    });
  }

  public sendOtp(token: string) {
    this.api.sendSignerOtp(token).subscribe({
      next: res => alert(res.message)
    });
  }

  public simulateSign(token: string, name: string) {
    this.api.submitSignerSignature(token, {
      fullName: name,
      signatureImageBase64: 'data:image/png;base64,iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mNkYAAAAAYAAjCB0C8AAAAASUVORK5CYII=',
      completedFields: []
    }).subscribe({
      next: res => this.contract.set(res)
    });
  }

  public getSignerStatusName(status: number): string {
    switch (status) {
      case 0: return 'Pending';
      case 1: return 'Sent';
      case 2: return 'Viewed';
      case 3: return 'OtpVerified';
      case 4: return 'Signed';
      case 5: return 'Declined';
      default: return 'Unknown';
    }
  }

  public getSignerStatusBadge(status: number): string {
    switch (status) {
      case 4: return 'bg-emerald-500/20 text-emerald-400 border border-emerald-500/30';
      case 1:
      case 2:
      case 3: return 'bg-indigo-500/20 text-indigo-400 border border-indigo-500/30';
      default: return 'bg-slate-800 text-slate-400';
    }
  }
}
