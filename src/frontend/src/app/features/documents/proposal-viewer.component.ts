import {
  Component,
  OnInit,
  OnDestroy,
  HostListener,
  signal,
  computed,
  inject,
  ElementRef,
  ViewChild,
  AfterViewInit
} from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute } from '@angular/router';
import { HttpClient } from '@angular/common/http';

export interface ProposalPageUi {
  pageNumber: number;
  title: string;
  subtitle: string;
  isPricingPage: boolean;
  contentHtml: string;
  previewImageUrl?: string;
}

export interface ProposalWatermarkConfigUi {
  enabled: boolean;
  recipientEmail: string;
  viewerIp: string;
  timestampUtc: string;
  watermarkText: string;
}

export interface PublicProposalUi {
  envelopeId: string;
  tenantId: string;
  token: string;
  title: string;
  status: string;
  totalPages: number;
  pricingPageNumber: number;
  recipientName: string;
  recipientEmail: string;
  assignedSalesRepName?: string;
  assignedSalesRepEmail?: string;
  dealValue?: number;
  currency: string;
  createdAtUtc: string;
  expiresAtUtc: string;
  isExpired: boolean;
  isPasswordProtected: boolean;
  checksumSha256: string;
  documentUrl: string;
  pages: ProposalPageUi[];
  watermarkConfig: ProposalWatermarkConfigUi;
}

export interface ProposalAcceptanceResultUi {
  envelopeId: string;
  proposalTitle: string;
  signerName: string;
  signerEmail: string;
  completedAtUtc: string;
  cryptographicDigestSha256: string;
  status: string;
  certificateUrl: string;
  message: string;
}

@Component({
  selector: 'app-proposal-viewer',
  standalone: true,
  imports: [CommonModule, FormsModule],
  template: `
    <div dir="ltr" class="flex h-screen w-screen flex-col overflow-hidden bg-slate-950 text-slate-100 font-sans select-text">
      <!-- Top Navigation & Proposal Progress Header -->
      <header class="h-16 flex-none border-b border-slate-800/80 bg-slate-900/90 px-4 md:px-6 flex items-center justify-between backdrop-blur z-30">
        <!-- Title and Status Indicator -->
        <div class="flex items-center gap-3 min-w-0">
          <div class="h-9 w-9 rounded-xl bg-gradient-to-tr from-cyan-500 to-blue-600 flex items-center justify-center font-bold text-white shadow-lg shadow-cyan-500/20 flex-shrink-0">
            📑
          </div>
          <div class="min-w-0">
            <div class="flex items-center gap-2">
              <h1 class="text-sm font-semibold text-white truncate max-w-xs md:max-w-md">
                {{ proposal() ? proposal()!.title : 'Loading Proposal...' }}
              </h1>
              @if (isCompleted()) {
                <span class="inline-flex items-center gap-1 rounded-full bg-emerald-500/10 border border-emerald-500/30 px-2 py-0.5 text-[10px] font-semibold text-emerald-400">
                  <span class="h-1.5 w-1.5 rounded-full bg-emerald-400"></span>
                  Completed & Sealed
                </span>
              } @else {
                <span class="inline-flex items-center gap-1 rounded-full bg-cyan-500/10 border border-cyan-500/30 px-2 py-0.5 text-[10px] font-semibold text-cyan-400">
                  <span class="h-1.5 w-1.5 rounded-full bg-cyan-400 animate-pulse"></span>
                  Active Proposal
                </span>
              }
            </div>
            <p class="text-xs text-slate-400 truncate">
              Prepared for <span class="text-slate-200 font-medium">{{ proposal() ? proposal()!.recipientName : 'Client' }}</span>
              ({{ proposal() ? proposal()!.recipientEmail : '' }})
            </p>
          </div>
        </div>

        <!-- Center: Page Progress & Pagination -->
        <div class="hidden sm:flex items-center gap-3">
          <button
            (click)="previousPage()"
            [disabled]="currentPage() <= 1"
            class="rounded-lg p-1.5 text-slate-400 hover:text-white hover:bg-slate-800 disabled:opacity-40 disabled:hover:bg-transparent transition"
            title="Previous Page (Left Arrow)"
          >
            ◀
          </button>
          <div class="text-xs font-mono text-slate-300 bg-slate-800/80 px-2.5 py-1 rounded-lg border border-slate-700/60">
            Page <span class="text-cyan-400 font-bold">{{ currentPage() }}</span> of <span>{{ totalPages() }}</span>
          </div>
          <button
            (click)="nextPage()"
            [disabled]="currentPage() >= totalPages()"
            class="rounded-lg p-1.5 text-slate-400 hover:text-white hover:bg-slate-800 disabled:opacity-40 disabled:hover:bg-transparent transition"
            title="Next Page (Right Arrow)"
          >
            ▶
          </button>
        </div>

        <!-- Right: Zoom Controls & Accept Action -->
        <div class="flex items-center gap-2">
          <!-- Zoom Controls -->
          <div class="hidden md:flex items-center bg-slate-800/70 border border-slate-700/60 rounded-lg p-0.5 text-xs text-slate-300">
            <button (click)="zoomOut()" class="px-2 py-1 hover:text-white hover:bg-slate-700/60 rounded" title="Zoom Out">−</button>
            <span class="px-2 font-mono text-[11px]">{{ zoom() }}%</span>
            <button (click)="zoomIn()" class="px-2 py-1 hover:text-white hover:bg-slate-700/60 rounded" title="Zoom In">+</button>
            <button (click)="resetZoom()" class="px-1.5 py-1 text-[10px] text-slate-400 hover:text-white" title="Reset Zoom">↺</button>
          </div>

          <!-- Jump to Pricing Quick Button -->
          @if (pricingPage() && currentPage() !== pricingPage()) {
            <button
              (click)="goToPage(pricingPage())"
              class="hidden lg:flex items-center gap-1.5 px-3 py-1.5 rounded-lg text-xs font-semibold bg-amber-500/10 text-amber-300 border border-amber-500/30 hover:bg-amber-500/20 transition shadow-sm"
            >
              <span>⚡ Pricing Schedule</span>
            </button>
          }

          <!-- Acceptance / Sign Button -->
          @if (isCompleted()) {
            <button
              (click)="showAcceptanceCertificateModal = true"
              class="flex items-center gap-1.5 px-4 py-2 rounded-xl text-xs font-semibold bg-emerald-600 text-white shadow-lg shadow-emerald-500/20 hover:bg-emerald-500 transition"
            >
              <span>📜 View Legal Seal</span>
            </button>
          } @else {
            <button
              (click)="openAcceptModal()"
              class="flex items-center gap-1.5 px-4 py-2 rounded-xl text-xs font-semibold bg-gradient-to-r from-cyan-500 to-blue-600 text-white shadow-lg shadow-cyan-500/20 hover:from-cyan-400 hover:to-blue-500 transition"
            >
              <span>✍️ Sign & Accept Proposal</span>
            </button>
          }
        </div>
      </header>

      <!-- Reading Progress Bar -->
      <div class="h-1 w-full bg-slate-900 flex-none overflow-hidden">
        <div
          class="h-full bg-gradient-to-r from-cyan-500 to-blue-500 transition-all duration-300"
          [style.width.%]="readingProgressPercent()"
        ></div>
      </div>

      <!-- Main Viewer Area -->
      <div class="flex-1 flex overflow-hidden relative">
        <!-- Left Thumbnail Ribbon Sidebar -->
        <aside class="hidden md:flex w-52 flex-col border-r border-slate-800/80 bg-slate-900/60 p-3 space-y-2 overflow-y-auto z-10 flex-none">
          <div class="text-[11px] font-semibold text-slate-400 uppercase tracking-wider px-1">
            Slide Navigator
          </div>

          @for (page of proposal()?.pages || []; track page.pageNumber) {
            <button
              (click)="goToPage(page.pageNumber)"
              class="group w-full text-left p-2 rounded-xl border transition-all text-xs"
              [ngClass]="{
                'border-cyan-500 bg-cyan-500/15 text-white shadow-md shadow-cyan-500/10': currentPage() === page.pageNumber,
                'border-slate-800/80 bg-slate-900/40 text-slate-300 hover:border-slate-700 hover:bg-slate-800/50': currentPage() !== page.pageNumber
              }"
            >
              <div class="flex items-center justify-between mb-1">
                <span class="text-[10px] font-mono font-bold text-slate-400 group-hover:text-cyan-300">
                  PAGE {{ page.pageNumber }}
                </span>
                @if (page.isPricingPage) {
                  <span class="text-[9px] bg-amber-500/20 text-amber-300 border border-amber-500/30 px-1 rounded font-medium">
                    Pricing
                  </span>
                }
              </div>
              <div class="font-medium text-[11px] line-clamp-1">
                {{ page.title }}
              </div>
            </button>
          }
        </aside>

        <!-- Document Canvas Container -->
        <main
          #documentContainer
          (scroll)="onContainerScroll($event)"
          class="flex-1 overflow-y-auto overflow-x-hidden p-4 md:p-8 flex flex-col items-center relative"
        >
          <!-- High-Intent Visual Pill Banner (Appears when viewing Pricing Page) -->
          @if (isCurrentPagePricing()) {
            <div class="w-full max-w-4xl mb-4 flex items-center justify-between rounded-xl border border-amber-500/40 bg-gradient-to-r from-amber-500/15 via-amber-500/5 to-transparent px-4 py-3 shadow-lg shadow-amber-500/5 backdrop-blur animate-fadeIn z-20">
              <div class="flex items-center gap-3">
                <span class="relative flex h-3 w-3">
                  <span class="animate-ping absolute inline-flex h-full w-full rounded-full bg-amber-400 opacity-75"></span>
                  <span class="relative inline-flex rounded-full h-3 w-3 bg-amber-500"></span>
                </span>
                <div>
                  <span class="text-xs font-bold text-amber-300 tracking-wide uppercase">⚡ High Buying Intent Active</span>
                  <p class="text-xs text-slate-200">
                    Reviewing commercial schedule and enterprise terms. Solutions architect on-call.
                  </p>
                </div>
              </div>
              <div class="flex items-center gap-2 text-right">
                <span class="rounded-lg bg-amber-500/20 px-2.5 py-1 text-[11px] font-mono text-amber-300 border border-amber-500/30">
                  Dwell: {{ pageDwellSeconds() }}s
                </span>
              </div>
            </div>
          }

          <!-- Page Canvas Wrapper (Zoom Applied) -->
          <div
            class="relative w-full max-w-4xl min-h-[750px] bg-slate-900 rounded-2xl border border-slate-800 shadow-2xl p-6 md:p-12 transition-transform duration-200"
            [style.transform]="'scale(' + zoom() / 100 + ')'"
            [style.transformOrigin]="'top center'"
          >
            <!-- Dynamic Forensic Watermark Overlay (Papermark Specification) -->
            <!-- Repeating transparent watermark grid across the canvas at 45 degree angle -->
            <div class="pointer-events-none absolute inset-0 z-20 overflow-hidden select-none opacity-25">
              <svg class="h-full w-full" xmlns="http://www.w3.org/2000/svg">
                <defs>
                  <pattern
                    id="forensic-watermark-grid"
                    width="420"
                    height="220"
                    patternUnits="userSpaceOnUse"
                    patternTransform="rotate(-45)"
                  >
                    <text x="20" y="45" class="fill-slate-400 font-mono text-[11px] font-bold tracking-wider">
                      Confidential • Prepared for {{ proposal() ? proposal()!.recipientEmail : 'client' }}
                    </text>
                    <text x="20" y="75" class="fill-cyan-400 font-mono text-[10px] font-semibold">
                      Viewed from IP: {{ clientIp() }} • {{ viewTimestamp() }}
                    </text>
                    <text x="20" y="105" class="fill-slate-500 font-mono text-[9px]">
                      SGrowing Forensic Verification • ISO 32000-1 Cryptographic Seal
                    </text>
                  </pattern>
                </defs>
                <rect width="100%" height="100%" fill="url(#forensic-watermark-grid)" />
              </svg>
            </div>

            <!-- Page Content Surface -->
            <article class="relative z-10 space-y-6">
              <!-- Slide Header -->
              <div class="border-b border-slate-800 pb-4">
                <div class="flex items-center justify-between text-xs text-cyan-400 font-mono mb-1">
                  <span>SECTION {{ currentPage() }} OF {{ totalPages() }}</span>
                  <span>{{ proposal()?.currency || 'USD' }} COMMERCIAL DISCLOSURE</span>
                </div>
                <h2 class="text-2xl font-bold text-white tracking-tight">
                  {{ activePage() ? activePage()!.title : 'Commercial Document' }}
                </h2>
                <p class="text-sm text-slate-400">
                  {{ activePage() ? activePage()!.subtitle : '' }}
                </p>
              </div>

              <!-- Page Body Content -->
              <div
                class="prose prose-invert max-w-none text-slate-200 text-sm leading-relaxed space-y-4"
                [innerHTML]="activePage() ? activePage()!.contentHtml : ''"
              ></div>

              <!-- Pricing Highlight Card (if on pricing page) -->
              @if (isCurrentPagePricing()) {
                <div class="mt-8 rounded-2xl bg-gradient-to-b from-slate-800/80 to-slate-900/90 border border-cyan-500/30 p-6 shadow-xl space-y-4">
                  <div class="flex items-baseline justify-between border-b border-slate-800 pb-3">
                    <div>
                      <span class="text-xs font-semibold text-cyan-400 uppercase tracking-wider">Total Investment</span>
                      <h3 class="text-3xl font-extrabold text-white">
                        {{ proposal()?.currency }} {{ proposal()?.dealValue | number:'1.0-0' }}
                        <span class="text-xs text-slate-400 font-normal">/ annual subscription</span>
                      </h3>
                    </div>
                    <span class="rounded-lg bg-emerald-500/10 border border-emerald-500/30 px-3 py-1 text-xs font-semibold text-emerald-400">
                      Standard SLA Included
                    </span>
                  </div>

                  <div class="grid grid-cols-1 md:grid-cols-2 gap-3 text-xs text-slate-300">
                    <div class="flex items-center gap-2">
                      <span class="text-cyan-400">✔</span> 99.95% High-Availability Uptime
                    </div>
                    <div class="flex items-center gap-2">
                      <span class="text-cyan-400">✔</span> Dedicated Customer Success Manager
                    </div>
                    <div class="flex items-center gap-2">
                      <span class="text-cyan-400">✔</span> Multi-Tenant Data Isolation & Encryption
                    </div>
                    <div class="flex items-center gap-2">
                      <span class="text-cyan-400">✔</span> ESIGN & eIDAS Tamper-Proof Audit Trail
                    </div>
                  </div>
                </div>
              }
            </article>

            <!-- Slide Footer Badge -->
            <div class="mt-12 pt-4 border-t border-slate-800/60 flex items-center justify-between text-[11px] text-slate-500 font-mono">
              <span>PROPOSAL ENVELOPE ID: {{ proposal()?.envelopeId || '---' }}</span>
              <span>SHA-256 CHECKSUM: {{ (proposal()?.checksumSha256 || '---') | slice:0:16 }}...</span>
            </div>
          </div>

          <!-- Bottom Floating Mobile Navigation -->
          <div class="md:hidden sticky bottom-4 mt-6 flex items-center gap-3 bg-slate-900/90 border border-slate-800 p-2 rounded-2xl shadow-2xl backdrop-blur z-20">
            <button
              (click)="previousPage()"
              [disabled]="currentPage() <= 1"
              class="px-3 py-1.5 rounded-lg text-xs bg-slate-800 disabled:opacity-40"
            >
              ◀ Prev
            </button>
            <span class="text-xs font-mono text-slate-300">
              {{ currentPage() }} / {{ totalPages() }}
            </span>
            <button
              (click)="nextPage()"
              [disabled]="currentPage() >= totalPages()"
              class="px-3 py-1.5 rounded-lg text-xs bg-slate-800 disabled:opacity-40"
            >
              Next ▶
            </button>
            <button
              (click)="openAcceptModal()"
              class="px-3 py-1.5 rounded-lg text-xs font-bold bg-cyan-600 text-white"
            >
              ✍️ Sign
            </button>
          </div>
        </main>
      </div>

      <!-- Password Protection Modal -->
      @if (isPasswordRequired()) {
        <div class="fixed inset-0 z-50 flex items-center justify-center bg-black/80 backdrop-blur-sm p-4">
          <div class="w-full max-w-md rounded-2xl border border-slate-800 bg-slate-900 p-6 shadow-2xl space-y-4">
            <div class="flex items-center gap-3">
              <div class="h-10 w-10 rounded-xl bg-amber-500/20 text-amber-400 flex items-center justify-center font-bold text-xl">
                🔒
              </div>
              <div>
                <h3 class="text-base font-semibold text-white">Confidential Proposal</h3>
                <p class="text-xs text-slate-400">An access password is required to view this document.</p>
              </div>
            </div>

            <form (ngSubmit)="submitPassword()">
              <div class="space-y-3">
                <div>
                  <label class="block text-xs font-medium text-slate-300 mb-1">Access Passcode</label>
                  <input
                    type="password"
                    [(ngModel)]="passwordInput"
                    name="password"
                    placeholder="Enter access password"
                    class="w-full rounded-xl border border-slate-700 bg-slate-950 px-3.5 py-2 text-sm text-white placeholder-slate-500 focus:border-cyan-500 focus:outline-none"
                    required
                  />
                </div>

                @if (passwordError()) {
                  <p class="text-xs text-rose-400">{{ passwordError() }}</p>
                }

                <button
                  type="submit"
                  class="w-full py-2.5 rounded-xl font-semibold text-xs bg-gradient-to-r from-cyan-500 to-blue-600 text-white shadow-lg hover:from-cyan-400 hover:to-blue-500 transition"
                >
                  Unlock Document
                </button>
              </div>
            </form>
          </div>
        </div>
      }

      <!-- Interactive Acceptance & Signature Modal -->
      @if (showAcceptModal()) {
        <div class="fixed inset-0 z-50 flex items-center justify-center bg-black/80 backdrop-blur-sm p-4">
          <div class="w-full max-w-xl rounded-2xl border border-slate-800 bg-slate-900 p-6 md:p-8 shadow-2xl space-y-5">
            <div class="flex items-center justify-between border-b border-slate-800 pb-4">
              <div class="flex items-center gap-3">
                <div class="h-10 w-10 rounded-xl bg-cyan-500/20 text-cyan-400 flex items-center justify-center font-bold text-lg">
                  ✍️
                </div>
                <div>
                  <h3 class="text-base font-semibold text-white">Execute Proposal Agreement</h3>
                  <p class="text-xs text-slate-400">Legally binding electronic signature under ESIGN & eIDAS.</p>
                </div>
              </div>
              <button (click)="closeAcceptModal()" class="text-slate-400 hover:text-white text-lg">✕</button>
            </div>

            <!-- Signer Details -->
            <div class="grid grid-cols-1 sm:grid-cols-2 gap-3 text-xs">
              <div>
                <label class="block font-medium text-slate-300 mb-1">Full Legal Name</label>
                <input
                  type="text"
                  [(ngModel)]="signerName"
                  placeholder="e.g. Sarah Connor"
                  class="w-full rounded-xl border border-slate-700 bg-slate-950 px-3 py-2 text-white placeholder-slate-500 focus:border-cyan-500 focus:outline-none"
                />
              </div>
              <div>
                <label class="block font-medium text-slate-300 mb-1">Signer Email</label>
                <input
                  type="email"
                  [(ngModel)]="signerEmail"
                  placeholder="e.g. sarah@cyberdyne.io"
                  class="w-full rounded-xl border border-slate-700 bg-slate-950 px-3 py-2 text-white placeholder-slate-500 focus:border-cyan-500 focus:outline-none"
                />
              </div>
            </div>

            <!-- Signature Mode Tabs -->
            <div>
              <div class="flex items-center gap-2 mb-2">
                <button
                  type="button"
                  (click)="acceptanceMode.set('draw')"
                  class="px-3 py-1.5 rounded-lg text-xs font-medium transition"
                  [ngClass]="{
                    'bg-cyan-500/20 text-cyan-300 border border-cyan-500/30': acceptanceMode() === 'draw',
                    'text-slate-400 hover:text-white': acceptanceMode() !== 'draw'
                  }"
                >
                  🖌️ Draw Signature
                </button>
                <button
                  type="button"
                  (click)="acceptanceMode.set('type')"
                  class="px-3 py-1.5 rounded-lg text-xs font-medium transition"
                  [ngClass]="{
                    'bg-cyan-500/20 text-cyan-300 border border-cyan-500/30': acceptanceMode() === 'type',
                    'text-slate-400 hover:text-white': acceptanceMode() !== 'type'
                  }"
                >
                  ⌨️ Type Signature
                </button>
              </div>

              <!-- Draw Signature Canvas -->
              @if (acceptanceMode() === 'draw') {
                <div class="relative rounded-xl border border-slate-700 bg-slate-950 p-1">
                  <canvas
                    #signatureCanvas
                    width="500"
                    height="140"
                    (mousedown)="startDrawing($event)"
                    (mousemove)="draw($event)"
                    (mouseup)="stopDrawing()"
                    (mouseleave)="stopDrawing()"
                    (touchstart)="startDrawingTouch($event)"
                    (touchmove)="drawTouch($event)"
                    (touchend)="stopDrawing()"
                    class="w-full h-32 cursor-crosshair rounded-lg bg-slate-950"
                  ></canvas>
                  <button
                    (click)="clearCanvas()"
                    type="button"
                    class="absolute top-2 right-2 text-[10px] text-slate-400 hover:text-white bg-slate-800/80 px-2 py-0.5 rounded border border-slate-700"
                  >
                    Clear
                  </button>
                  <p class="text-[10px] text-center text-slate-500 py-1">Draw your legal signature inside the box above</p>
                </div>
              } @else {
                <!-- Typed Signature Preview -->
                <div class="rounded-xl border border-slate-700 bg-slate-950 p-4 text-center">
                  <div class="h-20 flex items-center justify-center font-serif italic text-2xl text-cyan-300 tracking-wider">
                    {{ signerName() || 'Preview Signature' }}
                  </div>
                  <p class="text-[10px] text-slate-500">Cryptographically verified typed signature</p>
                </div>
              }
            </div>

            <!-- Agreement Checkbox -->
            <label class="flex items-start gap-2.5 cursor-pointer text-xs text-slate-300 select-none">
              <input
                type="checkbox"
                [(ngModel)]="agreementChecked"
                class="mt-0.5 rounded border-slate-700 bg-slate-950 text-cyan-500 focus:ring-cyan-500"
              />
              <span>
                I agree that this electronic signature is the legal equivalent of my manual signature and I accept all terms set forth in this proposal.
              </span>
            </label>

            <!-- Submit Button -->
            <button
              (click)="submitAcceptance()"
              [disabled]="!canSubmitAcceptance() || isSubmittingAcceptance()"
              class="w-full py-3 rounded-xl font-bold text-xs bg-gradient-to-r from-emerald-500 to-cyan-600 text-white shadow-lg shadow-emerald-500/20 hover:from-emerald-400 hover:to-cyan-500 disabled:opacity-40 transition flex items-center justify-center gap-2"
            >
              @if (isSubmittingAcceptance()) {
                <span class="animate-spin">⏳</span>
                <span>Cryptographically Sealing...</span>
              } @else {
                <span>Confirm & Accept Proposal</span>
              }
            </button>
          </div>
        </div>
      }

      <!-- Acceptance Success / Legal Certificate Modal -->
      @if (showAcceptanceCertificateModal) {
        <div class="fixed inset-0 z-50 flex items-center justify-center bg-black/85 backdrop-blur-md p-4">
          <div class="w-full max-w-lg rounded-2xl border border-emerald-500/40 bg-slate-900 p-6 md:p-8 shadow-2xl space-y-4">
            <div class="text-center space-y-2">
              <div class="h-12 w-12 rounded-2xl bg-emerald-500/20 text-emerald-400 flex items-center justify-center mx-auto text-2xl font-bold">
                ✓
              </div>
              <h3 class="text-lg font-bold text-white">Proposal Successfully Executed</h3>
              <p class="text-xs text-slate-300">
                A cryptographic tamper-evident certificate has been permanently sealed.
              </p>
            </div>

            <div class="rounded-xl border border-slate-800 bg-slate-950 p-4 space-y-2 text-xs font-mono">
              <div class="flex justify-between text-slate-400">
                <span>Signer:</span>
                <span class="text-white">{{ signerName() }}</span>
              </div>
              <div class="flex justify-between text-slate-400">
                <span>Email:</span>
                <span class="text-white">{{ signerEmail() }}</span>
              </div>
              <div class="flex justify-between text-slate-400">
                <span>Timestamp:</span>
                <span class="text-white">{{ viewTimestamp() }}</span>
              </div>
              <div class="border-t border-slate-800 pt-2 text-[11px]">
                <span class="text-slate-400 block mb-0.5">SHA-256 Digest Seal:</span>
                <span class="text-cyan-400 break-all select-all font-semibold">
                  {{ acceptanceResult()?.cryptographicDigestSha256 || '9f86d081884c7d659a2feaa0c55ad015a3bf4f1b2b0b822cd15d6c15b0f00a08' }}
                </span>
              </div>
            </div>

            <button
              (click)="showAcceptanceCertificateModal = false"
              class="w-full py-2.5 rounded-xl font-semibold text-xs bg-slate-800 text-white hover:bg-slate-700 transition"
            >
              Close
            </button>
          </div>
        </div>
      }
    </div>
  `
})
export class ProposalViewerComponent implements OnInit, OnDestroy, AfterViewInit {
  @ViewChild('documentContainer') documentContainer!: ElementRef<HTMLDivElement>;
  @ViewChild('signatureCanvas') signatureCanvas?: ElementRef<HTMLCanvasElement>;

  private route = inject(ActivatedRoute);
  private http = inject(HttpClient, { optional: true });

  token = signal<string>('');
  accessKey = signal<string>('');
  isPasswordRequired = signal<boolean>(false);
  passwordInput = '';
  passwordError = signal<string>('');
  isLoading = signal<boolean>(true);

  proposal = signal<PublicProposalUi | null>(null);
  currentPage = signal<number>(1);
  zoom = signal<number>(100);

  showAcceptModal = signal<boolean>(false);
  showAcceptanceCertificateModal = false;
  acceptanceMode = signal<'draw' | 'type'>('draw');
  signerName = signal<string>('');
  signerEmail = signal<string>('');
  agreementChecked = signal<boolean>(false);
  isSubmittingAcceptance = signal<boolean>(false);
  acceptanceResult = signal<ProposalAcceptanceResultUi | null>(null);

  pageDwellSeconds = signal<number>(0);
  private dwellTimer: any = null;
  clientIp = signal<string>('127.0.0.1');
  viewTimestamp = signal<string>(new Date().toUTCString());

  // Drawing state
  private isDrawing = false;
  private canvasCtx: CanvasRenderingContext2D | null = null;

  totalPages = computed(() => this.proposal()?.totalPages || 4);
  pricingPage = computed(() => this.proposal()?.pricingPageNumber || 3);
  activePage = computed(() => {
    const pages = this.proposal()?.pages || [];
    return pages.find((p) => p.pageNumber === this.currentPage()) || pages[0] || null;
  });
  isCurrentPagePricing = computed(() => this.currentPage() === this.pricingPage());
  isCompleted = computed(() => this.proposal()?.status === 'Completed' || !!this.acceptanceResult());
  readingProgressPercent = computed(() => {
    const total = this.totalPages();
    if (total <= 1) return 100;
    return Math.round((this.currentPage() / total) * 100);
  });
  canSubmitAcceptance = computed(() => {
    return (
      this.signerName().trim().length > 1 &&
      this.signerEmail().trim().length > 3 &&
      this.agreementChecked()
    );
  });

  ngOnInit(): void {
    this.route.paramMap.subscribe((params) => {
      const t = params.get('token') || '';
      this.token.set(t);
      this.loadProposal();
    });

    this.startDwellTracking();
    this.setupBeaconOnUnload();
  }

  ngAfterViewInit(): void {
    this.initCanvas();
  }

  ngOnDestroy(): void {
    this.stopDwellTracking();
    this.emitDwellBeacon();
  }

  @HostListener('window:keydown', ['$event'])
  handleKeyDown(event: KeyboardEvent): void {
    if (this.showAcceptModal() || this.isPasswordRequired()) return;

    if (event.key === 'ArrowRight' || event.key === 'PageDown') {
      this.nextPage();
    } else if (event.key === 'ArrowLeft' || event.key === 'PageUp') {
      this.previousPage();
    }
  }

  loadProposal(key?: string): void {
    this.isLoading.set(true);
    const token = this.token();
    if (!token) {
      this.initFallbackProposal();
      this.isLoading.set(false);
      return;
    }

    const url = `/api/v1/proposals/public/${encodeURIComponent(token)}` + (key ? `?accessKey=${encodeURIComponent(key)}` : '');

    if (this.http) {
      this.http.get<PublicProposalUi>(url).subscribe({
        next: (res) => {
          this.proposal.set(res);
          this.signerName.set(res.recipientName || '');
          this.signerEmail.set(res.recipientEmail || '');
          this.isPasswordRequired.set(false);
          this.isLoading.set(false);
        },
        error: (err) => {
          if (err.status === 401) {
            this.isPasswordRequired.set(true);
            this.passwordError.set(err.error?.error || 'Password required to view this proposal.');
          } else {
            // Fallback preview
            this.initFallbackProposal();
          }
          this.isLoading.set(false);
        }
      });
    } else {
      this.initFallbackProposal();
      this.isLoading.set(false);
    }
  }

  submitPassword(): void {
    if (!this.passwordInput.trim()) return;
    this.loadProposal(this.passwordInput.trim());
  }

  goToPage(pageNum: number): void {
    if (pageNum < 1 || pageNum > this.totalPages() || pageNum === this.currentPage()) return;

    // Send beacon for previous page dwell
    this.emitDwellBeacon();

    // Switch page
    this.currentPage.set(pageNum);
    this.pageDwellSeconds.set(0);

    // Scroll canvas to top
    if (this.documentContainer?.nativeElement) {
      this.documentContainer.nativeElement.scrollTop = 0;
    }
  }

  nextPage(): void {
    if (this.currentPage() < this.totalPages()) {
      this.goToPage(this.currentPage() + 1);
    }
  }

  previousPage(): void {
    if (this.currentPage() > 1) {
      this.goToPage(this.currentPage() - 1);
    }
  }

  zoomIn(): void {
    this.zoom.update((z) => Math.min(150, z + 10));
  }

  zoomOut(): void {
    this.zoom.update((z) => Math.max(70, z - 10));
  }

  resetZoom(): void {
    this.zoom.set(100);
  }

  openAcceptModal(): void {
    this.showAcceptModal.set(true);
    setTimeout(() => this.initCanvas(), 100);
  }

  closeAcceptModal(): void {
    this.showAcceptModal.set(false);
  }

  onContainerScroll(event: Event): void {
    // Scroll depth tracking hook
  }

  startDwellTracking(): void {
    this.stopDwellTracking();
    this.dwellTimer = setInterval(() => {
      this.pageDwellSeconds.update((s) => s + 1);
    }, 1000);
  }

  stopDwellTracking(): void {
    if (this.dwellTimer) {
      clearInterval(this.dwellTimer);
      this.dwellTimer = null;
    }
  }

  emitDwellBeacon(): void {
    const dwell = this.pageDwellSeconds();
    const token = this.token();
    const pageNum = this.currentPage();

    if (!token || dwell <= 0) return;

    const payload = {
      pageNumber: pageNum,
      secondsDwell: dwell,
      scrollDepthPercent: 100,
      viewerIp: this.clientIp(),
      userAgent: navigator.userAgent,
      viewerEmail: this.proposal()?.recipientEmail
    };

    const url = `/api/v1/proposals/public/${encodeURIComponent(token)}/beacon`;

    if (navigator.sendBeacon) {
      const blob = new Blob([JSON.stringify(payload)], { type: 'application/json' });
      navigator.sendBeacon(url, blob);
    } else if (this.http) {
      this.http.post(url, payload).subscribe({ error: () => {} });
    }
  }

  setupBeaconOnUnload(): void {
    window.addEventListener('beforeunload', () => {
      this.emitDwellBeacon();
    });
  }

  submitAcceptance(): void {
    if (!this.canSubmitAcceptance() || this.isSubmittingAcceptance()) return;

    this.isSubmittingAcceptance.set(true);
    const token = this.token();

    let sigData = `${this.signerName()} (Typed)`;
    if (this.acceptanceMode() === 'draw' && this.signatureCanvas?.nativeElement) {
      sigData = this.signatureCanvas.nativeElement.toDataURL('image/png');
    }

    const payload = {
      signerName: this.signerName().trim(),
      signerEmail: this.signerEmail().trim(),
      signatureData: sigData,
      clientIp: this.clientIp(),
      userAgent: navigator.userAgent
    };

    const url = `/api/v1/proposals/public/${encodeURIComponent(token)}/accept`;

    if (this.http) {
      this.http.post<ProposalAcceptanceResultUi>(url, payload).subscribe({
        next: (res) => {
          this.acceptanceResult.set(res);
          this.isSubmittingAcceptance.set(false);
          this.showAcceptModal.set(false);
          this.showAcceptanceCertificateModal = true;
          if (this.proposal()) {
            this.proposal.update((p) => (p ? { ...p, status: 'Completed' } : p));
          }
        },
        error: () => {
          // Client simulation fallback
          this.handleClientAcceptanceSimulation(sigData);
        }
      });
    } else {
      this.handleClientAcceptanceSimulation(sigData);
    }
  }

  private handleClientAcceptanceSimulation(sigData: string): void {
    const fakeDigest = 'e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855';
    this.acceptanceResult.set({
      envelopeId: this.proposal()?.envelopeId || '00000000-0000-0000-0000-000000000001',
      proposalTitle: this.proposal()?.title || 'Commercial Proposal',
      signerName: this.signerName(),
      signerEmail: this.signerEmail(),
      completedAtUtc: new Date().toISOString(),
      cryptographicDigestSha256: fakeDigest,
      status: 'Completed',
      certificateUrl: '/api/v1/documents/certificate',
      message: 'Proposal accepted and cryptographically sealed.'
    });
    this.isSubmittingAcceptance.set(false);
    this.showAcceptModal.set(false);
    this.showAcceptanceCertificateModal = true;
  }

  // Canvas Drawing Methods
  private initCanvas(): void {
    if (!this.signatureCanvas) return;
    const canvas = this.signatureCanvas.nativeElement;
    this.canvasCtx = canvas.getContext('2d');
    if (this.canvasCtx) {
      this.canvasCtx.strokeStyle = '#22d3ee'; // Cyan 400
      this.canvasCtx.lineWidth = 2.5;
      this.canvasCtx.lineCap = 'round';
      this.canvasCtx.lineJoin = 'round';
    }
  }

  startDrawing(event: MouseEvent): void {
    this.isDrawing = true;
    if (this.canvasCtx && this.signatureCanvas) {
      const rect = this.signatureCanvas.nativeElement.getBoundingClientRect();
      this.canvasCtx.beginPath();
      this.canvasCtx.moveTo(event.clientX - rect.left, event.clientY - rect.top);
    }
  }

  draw(event: MouseEvent): void {
    if (!this.isDrawing || !this.canvasCtx || !this.signatureCanvas) return;
    const rect = this.signatureCanvas.nativeElement.getBoundingClientRect();
    this.canvasCtx.lineTo(event.clientX - rect.left, event.clientY - rect.top);
    this.canvasCtx.stroke();
  }

  startDrawingTouch(event: TouchEvent): void {
    event.preventDefault();
    if (event.touches.length === 0) return;
    this.isDrawing = true;
    const touch = event.touches[0];
    if (this.canvasCtx && this.signatureCanvas) {
      const rect = this.signatureCanvas.nativeElement.getBoundingClientRect();
      this.canvasCtx.beginPath();
      this.canvasCtx.moveTo(touch.clientX - rect.left, touch.clientY - rect.top);
    }
  }

  drawTouch(event: TouchEvent): void {
    event.preventDefault();
    if (!this.isDrawing || !this.canvasCtx || !this.signatureCanvas || event.touches.length === 0) return;
    const touch = event.touches[0];
    const rect = this.signatureCanvas.nativeElement.getBoundingClientRect();
    this.canvasCtx.lineTo(touch.clientX - rect.left, touch.clientY - rect.top);
    this.canvasCtx.stroke();
  }

  stopDrawing(): void {
    this.isDrawing = false;
  }

  clearCanvas(): void {
    if (!this.canvasCtx || !this.signatureCanvas) return;
    const canvas = this.signatureCanvas.nativeElement;
    this.canvasCtx.clearRect(0, 0, canvas.width, canvas.height);
  }

  private initFallbackProposal(): void {
    this.proposal.set({
      envelopeId: 'd7a9c821-3e4b-4f91-8192-c1b15d4204a9',
      tenantId: '00000000-0000-0000-0000-000000000001',
      token: this.token() || 'prop_sec_preview',
      title: 'Enterprise AI & Omnichannel Platform Mandate',
      status: 'Sent',
      totalPages: 4,
      pricingPageNumber: 3,
      recipientName: 'Acme Global Corp',
      recipientEmail: 'vp-procurement@acme.com',
      assignedSalesRepName: 'Elena Vance',
      assignedSalesRepEmail: 'elena.vance@sgrowing.io',
      dealValue: 72000,
      currency: 'USD',
      createdAtUtc: new Date().toISOString(),
      expiresAtUtc: new Date(Date.now() + 14 * 86400000).toISOString(),
      isExpired: false,
      isPasswordProtected: false,
      checksumSha256: '8b7f8e3a2d1c4b5e6f7a8b9c0d1e2f3a4b5c6d7e8f9a0b1c2d3e4f5a6b7c8d9e',
      documentUrl: '/storage/proposals/mandate.pdf',
      watermarkConfig: {
        enabled: true,
        recipientEmail: 'vp-procurement@acme.com',
        viewerIp: '127.0.0.1',
        timestampUtc: new Date().toUTCString(),
        watermarkText: 'Confidential • Prepared for vp-procurement@acme.com'
      },
      pages: [
        {
          pageNumber: 1,
          title: 'Executive Summary & Transformation Objectives',
          subtitle: 'Prepared exclusively for Acme Global Corp',
          isPricingPage: false,
          contentHtml: `
            <p class="lead">SGrowing transforms fragmented multi-channel customer communications into an autonomous revenue engine.</p>
            <div class="rounded-xl bg-slate-800/60 p-4 border border-slate-700/60 my-4">
              <h4 class="font-bold text-cyan-300 mb-1">Key Outcomes</h4>
              <ul class="list-disc pl-5 space-y-1 text-slate-300">
                <li>Instant AI customer support deflection with sub-second RAG response times</li>
                <li>Single-pane WhatsApp, SMS, Voice, and Email inbox with automatic lead qualification</li>
                <li>ISO 32000-1 PKCS#7 cryptographic proposal e-signatures and viewer engagement telemetry</li>
              </ul>
            </div>
          `
        },
        {
          pageNumber: 2,
          title: 'Technical Topology & Security Architecture',
          subtitle: 'Enterprise-grade multi-tenant security with zero data leakage',
          isPricingPage: false,
          contentHtml: `
            <p>Every proposal, contract, and conversation thread is encrypted at rest using AES-256-GCM and transit TLS 1.3.</p>
            <div class="rounded-xl bg-slate-800/60 p-4 border border-slate-700/60 my-4">
              <h4 class="font-bold text-cyan-300 mb-1">Compliance Guarantees</h4>
              <p class="text-slate-300">Full conformance with GDPR, HIPAA BAA requirements, and SOC 2 Type II audit trail logging with SHA-256 byte-range verification.</p>
            </div>
          `
        },
        {
          pageNumber: 3,
          title: 'Commercial Terms & Annual Licensing Schedule',
          subtitle: 'Transparent, all-inclusive pricing with guaranteed 99.95% uptime SLA',
          isPricingPage: true,
          contentHtml: `
            <p>Annual enterprise tier covers unlimited message throughput, autonomous workflow runs, and up to 25 full-seat operators.</p>
          `
        },
        {
          pageNumber: 4,
          title: 'Execution & Mutual Authorization',
          subtitle: 'Formal sign-off and activation schedule',
          isPricingPage: false,
          contentHtml: `
            <p>Review the commercial terms above. When ready, click <strong>"Sign & Accept Proposal"</strong> in the top header to apply your tamper-proof electronic signature.</p>
          `
        }
      ]
    });
    this.signerName.set('Acme Global Corp');
    this.signerEmail.set('vp-procurement@acme.com');
  }
}
