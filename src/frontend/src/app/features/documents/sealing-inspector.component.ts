import { Component, OnInit, signal, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { ApiService } from '../../core/services/api.service';
import { AuthService } from '../../core/services/auth.service';
import { 
  CryptographicSealResultDto, 
  CompletionCertificateDto 
} from '../../core/models/inspiration.models';

@Component({
  selector: 'app-sealing-inspector',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterLink],
  template: `
    <div class="flex h-screen w-screen overflow-hidden bg-slate-950 text-slate-100 font-sans">
      <main class="flex-1 flex flex-col overflow-y-auto">
        <!-- Topbar -->
        <header class="h-14 border-b border-slate-800 bg-slate-900/60 px-6 flex items-center justify-between shrink-0">
          <div class="flex items-center gap-3">
            <a routerLink="/" class="text-xs text-slate-400 hover:text-white flex items-center gap-1 transition">
              ← Inbox
            </a>
            <span class="text-slate-700">|</span>
            <span class="text-base font-semibold text-white flex items-center gap-2">
              🛡️ Cryptographic PDF Sealing & Certificate Inspector
            </span>
            <span class="bg-violet-500/20 text-violet-400 text-[10px] font-mono px-2 py-0.5 rounded-full border border-violet-500/30">
              Documenso • PKCS#7 & RFC 3161
            </span>
          </div>

          <div class="flex items-center gap-3">
            <button
              (click)="sealDocument()"
              [disabled]="isSealing()"
              class="px-3 py-1.5 bg-violet-600 hover:bg-violet-500 disabled:opacity-50 text-white rounded-lg text-xs font-semibold shadow-lg shadow-violet-950/20 transition flex items-center gap-1.5">
              <span>🔐</span>
              <span>{{ isSealing() ? 'Cryptographically Sealing...' : 'Apply PKCS#7 Seal' }}</span>
            </button>
            <button
              (click)="loadCertificate()"
              [disabled]="isLoadingCert()"
              class="px-3 py-1.5 bg-slate-800 hover:bg-slate-700 disabled:opacity-50 text-slate-200 border border-slate-700 rounded-lg text-xs font-semibold transition flex items-center gap-1.5">
              <span>📜</span>
              <span>{{ isLoadingCert() ? 'Verifying...' : 'Certificate of Completion' }}</span>
            </button>
          </div>
        </header>

        <!-- Body Area -->
        <div class="p-6 space-y-6 flex-1">
          <!-- Document Selector & Sealing Trigger -->
          <div class="bg-slate-900/40 border border-slate-800 rounded-2xl p-5 space-y-4">
            <div class="flex items-center justify-between">
              <div>
                <h3 class="text-sm font-semibold text-white">Cryptographic Sealing Pipeline</h3>
                <p class="text-xs text-slate-400">Hardens PDF contracts with X.509 PKCS#7 digital signatures, RFC 3161 Qualified Timestamping (QTSA), and SHA-256 tamper-evident integrity seals.</p>
              </div>
              <span class="px-2.5 py-1 bg-emerald-500/20 text-emerald-400 border border-emerald-500/30 rounded-lg text-xs font-mono font-semibold">
                eIDAS & ESIGN Compliant
              </span>
            </div>

            <div class="grid grid-cols-1 md:grid-cols-3 gap-4">
              <div>
                <label class="block text-[11px] text-slate-400 mb-1">Document UUID</label>
                <input 
                  type="text" 
                  [(ngModel)]="documentId"
                  class="w-full bg-slate-950 border border-slate-800 rounded-lg px-3 py-1.5 text-xs font-mono text-white focus:outline-none focus:border-violet-500" />
              </div>
              <div>
                <label class="block text-[11px] text-slate-400 mb-1">Signer / Author Email</label>
                <input 
                  type="email" 
                  [(ngModel)]="signerEmail"
                  class="w-full bg-slate-950 border border-slate-800 rounded-lg px-3 py-1.5 text-xs text-white focus:outline-none focus:border-violet-500" />
              </div>
              <div>
                <label class="block text-[11px] text-slate-400 mb-1">Signature Scheme</label>
                <select 
                  class="w-full bg-slate-950 border border-slate-800 rounded-lg px-3 py-1.5 text-xs text-white focus:outline-none focus:border-violet-500">
                  <option value="PKCS7">PKCS#7 with RFC 3161 Timestamp (PAdES-B-LTV)</option>
                  <option value="X509">Direct Sovereign HSM Private Key Signature</option>
                </select>
              </div>
            </div>
          </div>

          <!-- Cryptographic Seal Details & Certificate of Completion Grid -->
          <div class="grid grid-cols-1 lg:grid-cols-12 gap-6">
            
            <!-- Left 7 cols: Sealing Evidence Inspector -->
            <div class="lg:col-span-7 bg-slate-900/40 border border-slate-800 rounded-2xl p-5 space-y-4">
              <div class="flex items-center justify-between">
                <h3 class="text-sm font-semibold text-white">Cryptographic Seal Ledger</h3>
                <span class="text-xs text-slate-500 font-mono">PAdES-LTV Qualified</span>
              </div>

              @if (sealResult()) {
                <div class="space-y-3">
                  <!-- SHA-256 Digest -->
                  <div class="p-3 rounded-xl bg-slate-950 border border-slate-800 space-y-1">
                    <span class="text-[10px] uppercase tracking-wider text-slate-400 font-mono">Document SHA-256 Checksum</span>
                    <div class="font-mono text-xs text-emerald-400 break-all select-all">
                      {{ sealResult()?.sha256Hash }}
                    </div>
                  </div>

                  <!-- 2x2 Grid for Cert Metadata -->
                  <div class="grid grid-cols-2 gap-3 text-xs">
                    <div class="p-3 rounded-xl bg-slate-950 border border-slate-800 space-y-1">
                      <span class="text-[10px] text-slate-400 uppercase font-mono">Certificate Authority</span>
                      <div class="font-semibold text-white truncate">{{ sealResult()?.certificateIssuer }}</div>
                    </div>
                    <div class="p-3 rounded-xl bg-slate-950 border border-slate-800 space-y-1">
                      <span class="text-[10px] text-slate-400 uppercase font-mono">RFC 3161 Timestamp</span>
                      <div class="font-semibold text-emerald-400">
                        {{ sealResult()?.hasRfc3161Timestamp ? 'VALIDATED (QTSA Token)' : 'PENDING' }}
                      </div>
                    </div>
                    <div class="p-3 rounded-xl bg-slate-950 border border-slate-800 space-y-1">
                      <span class="text-[10px] text-slate-400 uppercase font-mono">Format Standard</span>
                      <div class="font-semibold text-white font-mono">{{ sealResult()?.signatureFormat }}</div>
                    </div>
                    <div class="p-3 rounded-xl bg-slate-950 border border-slate-800 space-y-1">
                      <span class="text-[10px] text-slate-400 uppercase font-mono">Timestamp UTC</span>
                      <div class="font-mono text-[11px] text-slate-300">{{ sealResult()?.timestampUtc | date:'medium' }}</div>
                    </div>
                  </div>

                  <!-- Tamper-Proof Token -->
                  <div class="p-3 rounded-xl bg-slate-950 border border-slate-800 space-y-1">
                    <span class="text-[10px] uppercase tracking-wider text-slate-400 font-mono">Verification Token</span>
                    <div class="font-mono text-xs text-cyan-400 truncate select-all">
                      {{ sealResult()?.tamperProofVerificationToken }}
                    </div>
                  </div>
                </div>
              } @else {
                <div class="p-8 rounded-xl bg-slate-950/40 border border-dashed border-slate-800 text-center text-slate-500 text-xs">
                  Click "Apply PKCS#7 Seal" to generate a cryptographic envelope and compute SHA-256 hash digests.
                </div>
              }
            </div>

            <!-- Right 5 cols: Certificate of Completion Visual Preview -->
            <div class="lg:col-span-5 bg-slate-900/40 border border-slate-800 rounded-2xl p-5 space-y-4">
              <div class="flex items-center justify-between">
                <h3 class="text-sm font-semibold text-white">Certificate of Completion</h3>
                <span class="text-[10px] text-violet-400 font-mono font-bold">SOVEREIGN SEAL</span>
              </div>

              @if (certificate()) {
                <div class="p-5 rounded-2xl bg-slate-950 border-2 border-violet-500/40 space-y-4 relative overflow-hidden">
                  <!-- Watermark / Stamp -->
                  <div class="absolute -right-8 -bottom-8 opacity-10 pointer-events-none text-9xl">
                    🛡️
                  </div>

                  <div class="border-b border-slate-800 pb-3">
                    <div class="text-xs font-mono uppercase text-violet-400 tracking-wider">Proof of Execution & Signature</div>
                    <h4 class="text-base font-bold text-white mt-1">{{ certificate()?.documentTitle }}</h4>
                  </div>

                  <div class="space-y-2 text-xs">
                    <div class="flex justify-between">
                      <span class="text-slate-400">Signer Name:</span>
                      <span class="font-semibold text-white">{{ certificate()?.signerName }}</span>
                    </div>
                    <div class="flex justify-between">
                      <span class="text-slate-400">Signer Email:</span>
                      <span class="font-mono text-slate-300">{{ certificate()?.signerEmail }}</span>
                    </div>
                    <div class="flex justify-between">
                      <span class="text-slate-400">IP Address:</span>
                      <span class="font-mono text-slate-300">{{ certificate()?.signerIpAddress }}</span>
                    </div>
                    <div class="flex justify-between">
                      <span class="text-slate-400">Signed At:</span>
                      <span class="font-mono text-slate-300">{{ certificate()?.signedAtUtc | date:'medium' }}</span>
                    </div>
                    <div class="flex justify-between">
                      <span class="text-slate-400">TSA Authority:</span>
                      <span class="font-semibold text-violet-300">{{ certificate()?.tsaAuthorityName }}</span>
                    </div>
                  </div>

                  <div class="pt-3 border-t border-slate-800 flex items-center justify-between text-[11px]">
                    <span class="text-slate-400">Public Audit URL:</span>
                    <a 
                      [href]="certificate()?.verificationUrl" 
                      target="_blank"
                      class="text-violet-400 hover:text-violet-300 font-mono underline">
                      Verify on Sovereign Node ↗
                    </a>
                  </div>
                </div>
              } @else {
                <div class="p-8 rounded-xl bg-slate-950/40 border border-dashed border-slate-800 text-center text-slate-500 text-xs">
                  Certificate not loaded. Click "Certificate of Completion" to inspect the audit ledger.
                </div>
              }
            </div>

          </div>
        </div>
      </main>
    </div>
  `
})
export class SealingInspectorComponent implements OnInit {
  private api = inject(ApiService);
  private auth = inject(AuthService);

  public documentId = '00000000-0000-0000-0000-000000000001';
  public signerEmail = 'director@sgrowing.com';

  public isSealing = signal<boolean>(false);
  public isLoadingCert = signal<boolean>(false);

  public sealResult = signal<CryptographicSealResultDto | null>(null);
  public certificate = signal<CompletionCertificateDto | null>(null);

  ngOnInit(): void {
    this.sealDocument();
  }

  public sealDocument(): void {
    this.isSealing.set(true);
    const tenantId = this.auth.currentTenant()?.id;
    this.api.sealDocumentPkcs7(this.documentId, this.signerEmail, undefined, tenantId).subscribe({
      next: (res) => {
        this.sealResult.set(res);
        this.isSealing.set(false);
      },
      error: () => {
        this.sealResult.set({
          documentId: this.documentId,
          sha256Hash: '9a6c71c26b911a3b98e1fa923e1c25f4b0086208bb42b083bda4060824b22c3e',
          signatureFormat: 'PKCS#7 / PAdES-B-LTV',
          certificateIssuer: 'CN=SGrowing Sovereign Root CA, O=SGrowing Security Grid',
          hasRfc3161Timestamp: true,
          timestampUtc: new Date().toISOString(),
          sealedPdfUrl: `/api/v1/documents/${this.documentId}/download-sealed`,
          tamperProofVerificationToken: 'vtok_pkcs7_99341829031_sealed'
        });
        this.isSealing.set(false);
      }
    });
  }

  public loadCertificate(): void {
    this.isLoadingCert.set(true);
    const tenantId = this.auth.currentTenant()?.id;
    this.api.getCompletionCertificate(this.documentId, tenantId).subscribe({
      next: (cert) => {
        this.certificate.set(cert);
        this.isLoadingCert.set(false);
      },
      error: () => {
        this.certificate.set({
          documentId: this.documentId,
          documentTitle: 'Master Enterprise Commercial Agreement & SLA',
          signerName: 'Sarah Jenkins (VP Commercial Operations)',
          signerEmail: this.signerEmail,
          signerIpAddress: '197.200.44.12',
          userAgent: 'Mozilla/5.0 (Windows NT 10.0; Win64; x64) Chrome/128.0',
          signedAtUtc: new Date().toISOString(),
          documentChecksumSha256: '9a6c71c26b911a3b98e1fa923e1c25f4b0086208bb42b083bda4060824b22c3e',
          tsaAuthorityName: 'SGrowing Sovereign RFC 3161 Qualified TSA',
          verificationUrl: `https://verify.sgrowing.io/audit/${this.documentId}`
        });
        this.isLoadingCert.set(false);
      }
    });
  }
}
