import { Component, OnInit, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ApiService } from '../../core/services/api.service';
import { AuthService } from '../../core/services/auth.service';
import { DynamicFormDefinitionDto, DynamicFormSubmissionDto, CryptographicPdfRecordDto } from '../../core/models/dynamic-forms.models';

@Component({
  selector: 'app-forms-studio',
  standalone: true,
  imports: [CommonModule, FormsModule],
  template: `
    <div class="h-full flex flex-col bg-slate-950 text-slate-100 overflow-y-auto">
      <!-- Header -->
      <div class="p-6 border-b border-slate-800 bg-slate-900/40 backdrop-blur flex flex-col md:flex-row md:items-center justify-between gap-4">
        <div>
          <div class="flex items-center gap-2">
            <span class="text-2xl">📋</span>
            <h1 class="text-xl font-bold tracking-tight text-white">Dynamic Forms & Cryptographic PDF Studio</h1>
            <span class="px-2.5 py-0.5 rounded-full text-xs font-mono bg-teal-500/10 text-teal-400 border border-teal-500/20">
              Formbricks & DocuSeal Architecture
            </span>
          </div>
          <p class="text-xs text-slate-400 mt-1">
            Build conversational multi-step surveys, capture verified customer data, and seal tamper-evident PDF contracts with SHA-256 audit chains.
          </p>
        </div>

        <!-- Navigation Tabs -->
        <div class="flex items-center gap-1 bg-slate-900 p-1 rounded-xl border border-slate-800 text-xs">
          <button
            (click)="activeTab.set('builder')"
            [class.bg-teal-600]="activeTab() === 'builder'"
            [class.text-white]="activeTab() === 'builder'"
            class="px-3 py-1.5 rounded-lg text-slate-400 hover:text-white transition">
            🎨 Form Builder
          </button>
          <button
            (click)="activeTab.set('submissions')"
            [class.bg-teal-600]="activeTab() === 'submissions'"
            [class.text-white]="activeTab() === 'submissions'"
            class="px-3 py-1.5 rounded-lg text-slate-400 hover:text-white transition">
            📥 Responses ({{ submissions().length }})
          </button>
          <button
            (click)="activeTab.set('documents')"
            [class.bg-teal-600]="activeTab() === 'documents'"
            [class.text-white]="activeTab() === 'documents'"
            class="px-3 py-1.5 rounded-lg text-slate-400 hover:text-white transition">
            🔒 Cryptographic PDF Vault
          </button>
        </div>
      </div>

      <!-- Main Content -->
      <div class="p-6 space-y-6 flex-1">
        <!-- 1. Form Builder & Live Test -->
        @if (activeTab() === 'builder') {
          <div class="grid grid-cols-1 lg:grid-cols-2 gap-6">
            <!-- Active Forms List -->
            <div class="space-y-4">
              <h3 class="text-sm font-bold text-slate-200">Published Forms ({{ forms().length }})</h3>
              <div class="space-y-3">
                @for (f of forms(); track f.id) {
                  <div
                    (click)="selectedForm.set(f); loadSubmissions(f.id)"
                    [class.border-teal-500]="selectedForm()?.id === f.id"
                    class="p-4 rounded-xl bg-slate-900/60 border border-slate-800 cursor-pointer hover:border-teal-500/50 transition">
                    <div class="flex items-center justify-between">
                      <h4 class="text-sm font-bold text-white">{{ f.title }}</h4>
                      <span class="text-xs font-mono px-2 py-0.5 rounded bg-teal-500/10 text-teal-300">
                        {{ f.submissionCount }} responses
                      </span>
                    </div>
                    <p class="text-xs text-slate-400 mt-1">{{ f.description }}</p>
                    <span class="text-[11px] font-mono text-slate-500 mt-2 block">Slug: /forms/{{ f.slug }}</span>
                  </div>
                }
              </div>
            </div>

            <!-- Interactive Form Simulation -->
            <div class="p-5 rounded-2xl bg-slate-900/80 border border-slate-800 space-y-4">
              <div class="flex items-center justify-between border-b border-slate-800 pb-3">
                <div>
                  <h4 class="text-sm font-bold text-teal-400">⚡ Live Form Experience</h4>
                  <span class="text-xs text-slate-400">{{ selectedForm()?.title || 'Interactive Preview' }}</span>
                </div>
                <span class="px-2 py-0.5 rounded text-[10px] font-mono bg-emerald-500/20 text-emerald-300">Active Form</span>
              </div>

              <div class="space-y-3 text-xs">
                <div>
                  <label class="text-slate-400 block mb-1">Company / Merchant Name</label>
                  <input [(ngModel)]="sampleMerchantName" class="w-full px-3 py-2 bg-slate-950 border border-slate-800 rounded-lg text-slate-200" />
                </div>
                <div>
                  <label class="text-slate-400 block mb-1">Monthly Parcel Volume</label>
                  <select [(ngModel)]="sampleVolume" class="w-full px-3 py-2 bg-slate-950 border border-slate-800 rounded-lg text-slate-200">
                    <option>500 - 2,000 parcels</option>
                    <option>2,000 - 10,000 parcels</option>
                    <option>10,000+ parcels</option>
                  </select>
                </div>
                <div>
                  <label class="text-slate-400 block mb-1">Preferred Courier Priority</label>
                  <select [(ngModel)]="samplePriority" class="w-full px-3 py-2 bg-slate-950 border border-slate-800 rounded-lg text-slate-200">
                    <option>Cheapest Tariff First</option>
                    <option>Fastest Delivery Rate (Under 24h)</option>
                    <option>Automated Drop-and-Swap Enabled</option>
                  </select>
                </div>

                <button
                  (click)="submitSampleAnswers()"
                  [disabled]="isSubmitting()"
                  class="w-full py-2.5 bg-teal-600 hover:bg-teal-500 disabled:opacity-50 text-white rounded-lg font-bold transition shadow-lg shadow-teal-600/20">
                  {{ isSubmitting() ? 'Submitting Answers...' : 'Submit Answers' }}
                </button>

                @if (submitSuccess()) {
                  <div class="p-3 bg-emerald-500/10 border border-emerald-500/20 rounded-lg text-emerald-400 text-center font-medium">
                    {{ submitSuccess() }}
                  </div>
                }
              </div>
            </div>
          </div>
        }

        <!-- 2. Submissions Tab -->
        @if (activeTab() === 'submissions') {
          <div class="rounded-2xl border border-slate-800 overflow-hidden bg-slate-900/40">
            <div class="p-4 border-b border-slate-800 flex justify-between items-center">
              <span class="text-xs font-bold text-slate-200">Responses for {{ selectedForm()?.title }}</span>
              <span class="text-xs font-mono text-slate-400">{{ submissions().length }} submissions recorded</span>
            </div>
            <table class="w-full text-left text-xs">
              <thead class="bg-slate-900/80 text-slate-400 uppercase font-mono border-b border-slate-800">
                <tr>
                  <th class="px-4 py-3">Submission ID</th>
                  <th class="px-4 py-3">Answers Data</th>
                  <th class="px-4 py-3">Duration</th>
                  <th class="px-4 py-3">Timestamp</th>
                </tr>
              </thead>
              <tbody class="divide-y divide-slate-800/60 font-mono">
                @for (s of submissions(); track s.id) {
                  <tr class="hover:bg-slate-900/40">
                    <td class="px-4 py-3 text-slate-400">{{ s.id.substring(0, 8) }}...</td>
                    <td class="px-4 py-3 text-teal-300 font-sans">{{ s.answersJson }}</td>
                    <td class="px-4 py-3 text-slate-400">{{ s.completedInSeconds }}s</td>
                    <td class="px-4 py-3 text-slate-400">{{ s.submittedAtUtc | date:'medium' }}</td>
                  </tr>
                }
              </tbody>
            </table>
          </div>
        }

        <!-- 3. Documents & Cryptographic Seals -->
        @if (activeTab() === 'documents') {
          <div class="space-y-6">
            <div class="p-5 rounded-2xl bg-slate-900/60 border border-slate-800 flex flex-col md:flex-row md:items-center justify-between gap-4">
              <div>
                <h3 class="text-sm font-bold text-white">Cryptographic PDF Seal Registry</h3>
                <p class="text-xs text-slate-400 mt-1">Tamper-evident document signing with SHA-256 hash chaining and audit trail verification.</p>
              </div>
              <div class="flex items-center gap-2">
                <button
                  (click)="createSampleContract()"
                  class="px-3 py-1.5 bg-slate-800 hover:bg-slate-700 text-slate-200 rounded-lg text-xs font-mono border border-slate-700 transition">
                  📄 Create Contract
                </button>
                <button
                  (click)="sealDocumentDemo()"
                  class="px-3 py-1.5 bg-teal-600 hover:bg-teal-500 text-white rounded-lg text-xs font-bold transition">
                  🔒 Apply SHA-256 Seal
                </button>
              </div>
            </div>

            @if (activeDoc()) {
              <div class="p-5 rounded-2xl bg-slate-900/40 border border-slate-800 space-y-4">
                <div class="flex items-center justify-between">
                  <h4 class="text-base font-bold text-white">{{ activeDoc()?.documentTitle }}</h4>
                  <span class="px-2.5 py-1 rounded-full text-xs font-mono"
                    [class.bg-emerald-500/20]="activeDoc()?.status === 'Sealed'"
                    [class.text-emerald-300]="activeDoc()?.status === 'Sealed'"
                    [class.bg-amber-500/20]="activeDoc()?.status !== 'Sealed'"
                    [class.text-amber-300]="activeDoc()?.status !== 'Sealed'">
                    Status: {{ activeDoc()?.status }}
                  </span>
                </div>

                <div class="grid grid-cols-1 md:grid-cols-2 gap-4 font-mono text-xs">
                  <div class="p-3 bg-slate-950 rounded-lg border border-slate-800">
                    <span class="text-slate-500 block mb-1">Original SHA-256 Hash</span>
                    <span class="text-slate-300 break-all">{{ activeDoc()?.originalSha256 }}</span>
                  </div>
                  <div class="p-3 bg-slate-950 rounded-lg border border-slate-800">
                    <span class="text-slate-500 block mb-1">Cryptographic Seal Hash</span>
                    <span class="text-teal-400 break-all">{{ activeDoc()?.sealedSha256 }}</span>
                  </div>
                </div>

                <div class="p-3 bg-slate-950 rounded-lg border border-slate-800 text-xs font-mono text-slate-400">
                  <span class="text-slate-500 block mb-1">Audit Trail Hash Chain</span>
                  <p class="text-slate-300">{{ activeDoc()?.auditTrailHashChainJson }}</p>
                </div>

                <div class="flex items-center gap-3">
                  <button
                    (click)="verifyAuditChain()"
                    class="px-4 py-2 bg-slate-800 hover:bg-slate-700 text-teal-300 rounded-lg text-xs font-mono border border-slate-700 transition">
                    🛡️ Verify Tamper-Evidence
                  </button>
                  @if (verificationResult()) {
                    <span class="text-xs font-mono text-emerald-400 font-bold">
                      ✓ {{ verificationResult() }}
                    </span>
                  }
                </div>
              </div>
            }
          </div>
        }
      </div>
    </div>
  `
})
export class FormsStudioComponent implements OnInit {
  private api = inject(ApiService);
  private auth = inject(AuthService);

  public tenantId = this.auth.currentTenant()?.id || 'a0000000-0000-0000-0000-000000000001';
  public activeTab = signal<'builder' | 'submissions' | 'documents'>('builder');

  public forms = signal<DynamicFormDefinitionDto[]>([]);
  public selectedForm = signal<DynamicFormDefinitionDto | null>(null);
  public submissions = signal<DynamicFormSubmissionDto[]>([]);
  public activeDoc = signal<CryptographicPdfRecordDto | null>(null);

  public sampleMerchantName = 'E-Store Carthage';
  public sampleVolume = '2,000 - 10,000 parcels';
  public samplePriority = 'Fastest Delivery Rate (Under 24h)';

  public isSubmitting = signal(false);
  public submitSuccess = signal<string | null>(null);
  public verificationResult = signal<string | null>(null);

  ngOnInit(): void {
    this.loadForms();
    this.createSampleContract();
  }

  public loadForms(): void {
    this.api.getForms(this.tenantId).subscribe(forms => {
      if (forms.length === 0) {
        this.api.createForm({
          tenantId: this.tenantId,
          title: 'High-Volume COD Merchant Intake',
          slug: 'merchant-intake',
          description: 'Surveying logistics requirements, daily pickup schedules, and delivery speeds.',
          fields: [
            { fieldKey: 'companyName', label: 'Company Name', fieldType: 'text', isRequired: true, options: [] },
            { fieldKey: 'volume', label: 'Monthly Parcel Volume', fieldType: 'select', isRequired: true, options: ['500-2000', '2000-10000'] }
          ]
        }).subscribe(f => {
          this.forms.set([f]);
          this.selectedForm.set(f);
        });
      } else {
        this.forms.set(forms);
        this.selectedForm.set(forms[0]);
        this.loadSubmissions(forms[0].id);
      }
    });
  }

  public loadSubmissions(formId: string): void {
    this.api.getSubmissions(this.tenantId, formId).subscribe(subs => {
      this.submissions.set(subs);
    });
  }

  public submitSampleAnswers(): void {
    if (!this.selectedForm()) return;
    this.isSubmitting.set(true);
    this.submitSuccess.set(null);

    this.api.submitFormAnswers(this.selectedForm()!.slug, {
      tenantId: this.tenantId,
      formId: this.selectedForm()!.id,
      answers: {
        companyName: this.sampleMerchantName,
        parcelVolume: this.sampleVolume,
        courierPriority: this.samplePriority
      },
      completedInSeconds: 35
    }).subscribe({
      next: (sub) => {
        this.submissions.update(prev => [sub, ...prev]);
        this.isSubmitting.set(false);
        this.submitSuccess.set('🎉 Form response recorded and linked to customer identity graph!');
        this.selectedForm.update(f => f ? { ...f, submissionCount: f.submissionCount + 1 } : null);
      },
      error: () => this.isSubmitting.set(false)
    });
  }

  public createSampleContract(): void {
    this.api.createDocument({
      tenantId: this.tenantId,
      title: 'Merchant SLA & Courier Remittance Guarantee',
      content: 'AGREEMENT: SGrowing agrees to process and remit COD collections within 48 hours with guaranteed rate protection.',
      signaturePlacementJson: '[{"signer":"director@merchant.tn","x":120,"y":300}]'
    }).subscribe(doc => {
      this.activeDoc.set(doc);
    });
  }

  public sealDocumentDemo(): void {
    if (!this.activeDoc()) return;
    this.api.sealDocument({
      tenantId: this.tenantId,
      documentId: this.activeDoc()!.id,
      signerEmail: 'director@merchant.tn',
      signatureCoordinates: 'X:120,Y:300,Page:1'
    }).subscribe(sealed => {
      this.activeDoc.set(sealed);
    });
  }

  public verifyAuditChain(): void {
    if (!this.activeDoc()) return;
    this.api.verifyDocumentAuditChain(this.tenantId, this.activeDoc()!.id).subscribe(res => {
      this.verificationResult.set(res.summary);
    });
  }
}
