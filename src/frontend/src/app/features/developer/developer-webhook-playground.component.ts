import { Component, ChangeDetectionStrategy, signal, computed } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';

export type WebhookEventType =
  | 'order.placed'
  | 'mission.completed'
  | 'lead.captured'
  | 'content.brief.generated'
  | 'payment.confirmed';

export type SnippetLanguage = 'csharp' | 'typescript' | 'python' | 'curl';

export interface WebhookHeaderEntry {
  name: string;
  value: string;
  description: string;
}

export interface DispatchResult {
  statusCode: number;
  statusText: string;
  latencyMs: number;
  deliveryId: string;
  timestamp: number;
  signature: string;
  targetUrl: string;
  eventType: string;
  headers: WebhookHeaderEntry[];
  requestPayload: string;
  responseBody: string;
  dispatchedAt: Date;
}

export const SAMPLE_PAYLOADS: Record<WebhookEventType, object> = {
  'order.placed': {
    eventId: 'evt_ord_987410',
    eventType: 'order.placed',
    occurredAt: '2026-11-07T11:00:00Z',
    order: {
      orderId: 'ord_883294',
      customer: {
        id: 'cust_102',
        email: 'sarah.connor@cyberdyne.io',
        name: 'Sarah Connor'
      },
      currency: 'EUR',
      totalAmount: 1450.0,
      items: [
        {
          sku: 'SGW-ENTERPRISE-PRO',
          description: 'Enterprise Horizon Subscription (Annual)',
          quantity: 1,
          unitPrice: 1450.0
        }
      ]
    }
  },
  'mission.completed': {
    eventId: 'evt_msn_552140',
    eventType: 'mission.completed',
    occurredAt: '2026-11-07T11:00:00Z',
    mission: {
      missionId: 'msn_alpha_9',
      agentId: 'agt_growth_director_01',
      agentName: 'Autonomous Growth Director',
      status: 'succeeded',
      executionTimeMs: 2840,
      metrics: {
        leadsDiscovered: 84,
        emailsDrafted: 62,
        enrichmentConfidence: 0.96
      }
    }
  },
  'lead.captured': {
    eventId: 'evt_lead_771920',
    eventType: 'lead.captured',
    occurredAt: '2026-11-07T11:00:00Z',
    lead: {
      leadId: 'lead_99382',
      fullName: 'Elena Rostova',
      email: 'elena.rostova@vertex-logistics.eu',
      company: 'Vertex Logistics Group',
      source: 'Outbound LinkedIn Voice SDR',
      qualificationScore: 89,
      estimatedContractValue: 45000
    }
  },
  'content.brief.generated': {
    eventId: 'evt_brf_331040',
    eventType: 'content.brief.generated',
    occurredAt: '2026-11-07T11:00:00Z',
    brief: {
      briefId: 'brf_ai_b2b_wholesale',
      targetTopic: 'Enterprise Automated Webhook Architectures',
      primaryKeyword: 'real-time outbound webhooks',
      estimatedWordCount: 2400,
      contentPillars: [
        'Cryptographic HMAC Verification',
        'Dead Letter Queue Recovery',
        'Rate Limiting & Exponential Backoff'
      ],
      authorAgent: 'agt_seo_content_creator'
    }
  },
  'payment.confirmed': {
    eventId: 'evt_pay_441090',
    eventType: 'payment.confirmed',
    occurredAt: '2026-11-07T11:00:00Z',
    payment: {
      transactionId: 'txn_sep_871923',
      invoiceId: 'inv_2026_11_004',
      payerEmail: 'finance@acme-corp.com',
      amount: 4900.0,
      currency: 'EUR',
      paymentMethod: 'sepa_debit',
      settledAt: '2026-11-07T11:00:00Z'
    }
  }
};

/**
 * Pure TypeScript synchronous HMAC-SHA256 implementation conforming to RFC 2104 / FIPS 180-4.
 * Produces 'sha256=' + 64-char lowercase hex digest.
 */
export function computeHmacSha256(keyStr: string, messageStr: string): string {
  function toUtf8Bytes(str: string): number[] {
    const bytes: number[] = [];
    for (let i = 0; i < str.length; i++) {
      let code = str.charCodeAt(i);
      if (code < 0x80) {
        bytes.push(code);
      } else if (code < 0x800) {
        bytes.push(0xc0 | (code >> 6), 0x80 | (code & 0x3f));
      } else if (code < 0xd800 || code >= 0xe000) {
        bytes.push(0xe0 | (code >> 12), 0x80 | ((code >> 6) & 0x3f), 0x80 | (code & 0x3f));
      } else {
        i++;
        code = 0x10000 + (((code & 0x3ff) << 10) | (str.charCodeAt(i) & 0x3ff));
        bytes.push(
          0xf0 | (code >> 18),
          0x80 | ((code >> 12) & 0x3f),
          0x80 | ((code >> 6) & 0x3f),
          0x80 | (code & 0x3f)
        );
      }
    }
    return bytes;
  }

  function sha256Bytes(data: number[]): number[] {
    const K = [
      0x428a2f98, 0x71374491, 0xb5c0fbcf, 0xe9b5dba5, 0x3956c25b, 0x59f111f1, 0x923f82a4, 0xab1c5ed5,
      0xd807aa98, 0x12835b01, 0x243185be, 0x550c7dc3, 0x72be5d74, 0x80deb1fe, 0x9bdc06a7, 0xc19bf174,
      0xe49b69c1, 0xefbe4786, 0x0fc19dc6, 0x240ca1cc, 0x2de92c6f, 0x4a7484aa, 0x5cb0a9dc, 0x76f988da,
      0x983e5152, 0xa831c66d, 0xb00327c8, 0xbf597fc7, 0xc6e00bf3, 0xd5a79147, 0x06ca6351, 0x14292967,
      0x27b70a85, 0x2e1b2138, 0x4d2c6dfc, 0x53380d13, 0x650a7354, 0x766a0abb, 0x81c2c92e, 0x92722c85,
      0xa2bfe8a1, 0xa81a664b, 0xc24b8b70, 0xc76c51a3, 0xd192e819, 0xd6990624, 0xf40e3585, 0x106aa070,
      0x19a4c116, 0x1e376c08, 0x2748774c, 0x34b0bcb5, 0x391c0cb3, 0x4ed8aa4a, 0x5b9cca4f, 0x682e6ff3,
      0x748f82ee, 0x78a5636f, 0x84c87814, 0x8cc70208, 0x90befffa, 0xa4506ceb, 0xbef9a3f7, 0xc67178f2
    ];
    const H = [0x6a09e667, 0xbb67ae85, 0x3c6ef372, 0xa54ff53a, 0x510e527f, 0x9b05688c, 0x1f83d9ab, 0x5be0cd19];

    const bitLen = data.length * 8;
    const padded = data.slice();
    padded.push(0x80);
    while (padded.length % 64 !== 56) {
      padded.push(0);
    }
    const highBits = Math.floor(bitLen / 0x100000000);
    const lowBits = bitLen >>> 0;
    padded.push(
      (highBits >>> 24) & 0xff,
      (highBits >>> 16) & 0xff,
      (highBits >>> 8) & 0xff,
      highBits & 0xff,
      (lowBits >>> 24) & 0xff,
      (lowBits >>> 16) & 0xff,
      (lowBits >>> 8) & 0xff,
      lowBits & 0xff
    );

    const W = new Int32Array(64);
    for (let chunk = 0; chunk < padded.length; chunk += 64) {
      for (let i = 0; i < 16; i++) {
        W[i] =
          (padded[chunk + i * 4] << 24) |
          (padded[chunk + i * 4 + 1] << 16) |
          (padded[chunk + i * 4 + 2] << 8) |
          padded[chunk + i * 4 + 3];
      }
      for (let i = 16; i < 64; i++) {
        const s0 =
          ((W[i - 15] >>> 7) | (W[i - 15] << 25)) ^
          ((W[i - 15] >>> 18) | (W[i - 15] << 14)) ^
          (W[i - 15] >>> 3);
        const s1 =
          ((W[i - 2] >>> 17) | (W[i - 2] << 15)) ^
          ((W[i - 2] >>> 19) | (W[i - 2] << 13)) ^
          (W[i - 2] >>> 10);
        W[i] = (W[i - 16] + s0 + W[i - 7] + s1) | 0;
      }

      let a = H[0],
        b = H[1],
        c = H[2],
        d = H[3],
        e = H[4],
        f = H[5],
        g = H[6],
        h = H[7];

      for (let i = 0; i < 64; i++) {
        const S1 = ((e >>> 6) | (e << 26)) ^ ((e >>> 11) | (e << 21)) ^ ((e >>> 25) | (e << 7));
        const ch = (e & f) ^ (~e & g);
        const temp1 = (h + S1 + ch + K[i] + W[i]) | 0;
        const S0 = ((a >>> 2) | (a << 30)) ^ ((a >>> 13) | (a << 19)) ^ ((a >>> 22) | (a << 10));
        const maj = (a & b) ^ (a & c) ^ (b & c);
        const temp2 = (S0 + maj) | 0;

        h = g;
        g = f;
        f = e;
        e = (d + temp1) | 0;
        d = c;
        c = b;
        b = a;
        a = (temp1 + temp2) | 0;
      }

      H[0] = (H[0] + a) | 0;
      H[1] = (H[1] + b) | 0;
      H[2] = (H[2] + c) | 0;
      H[3] = (H[3] + d) | 0;
      H[4] = (H[4] + e) | 0;
      H[5] = (H[5] + f) | 0;
      H[6] = (H[6] + g) | 0;
      H[7] = (H[7] + h) | 0;
    }

    const out: number[] = [];
    for (let i = 0; i < 8; i++) {
      out.push((H[i] >>> 24) & 0xff, (H[i] >>> 16) & 0xff, (H[i] >>> 8) & 0xff, H[i] & 0xff);
    }
    return out;
  }

  let key = toUtf8Bytes(keyStr);
  const message = toUtf8Bytes(messageStr);

  const blockSize = 64;
  if (key.length > blockSize) {
    key = sha256Bytes(key);
  }
  while (key.length < blockSize) {
    key.push(0);
  }

  const oKeyPad = new Array<number>(blockSize);
  const iKeyPad = new Array<number>(blockSize);
  for (let i = 0; i < blockSize; i++) {
    oKeyPad[i] = key[i] ^ 0x5c;
    iKeyPad[i] = key[i] ^ 0x36;
  }

  const inner = sha256Bytes(iKeyPad.concat(message));
  const outer = sha256Bytes(oKeyPad.concat(inner));

  const hex = outer.map(b => b.toString(16).padStart(2, '0')).join('');
  return 'sha256=' + hex;
}

@Component({
  selector: 'app-developer-webhook-playground',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [CommonModule, FormsModule, RouterLink],
  template: `
    <div class="flex h-screen w-screen overflow-hidden bg-slate-950 text-slate-100 font-sans">
      <main class="flex-1 flex flex-col overflow-y-auto">
        <!-- Top Navigation Header -->
        <header
          class="h-16 border-b border-slate-800 bg-slate-900/80 backdrop-blur-md px-6 flex items-center justify-between shrink-0 sticky top-0 z-30"
        >
          <div class="flex items-center gap-3">
            <a
              routerLink="/developer"
              class="text-xs text-slate-400 hover:text-white flex items-center gap-1.5 transition font-medium"
            >
              <span>←</span> Developer Portal
            </a>
            <span class="text-slate-700">|</span>
            <div class="flex items-center gap-2.5">
              <span class="text-base font-bold text-white tracking-tight flex items-center gap-2">
                <span class="text-indigo-400">⚡</span> Developer Webhook & API Playground
              </span>
            </div>
            <!-- Status Indicators -->
            <div class="hidden sm:flex items-center gap-2 ml-2">
              <span
                class="inline-flex items-center gap-1.5 px-2.5 py-0.5 rounded-full text-[11px] font-mono bg-emerald-500/10 text-emerald-400 border border-emerald-500/30 font-semibold"
              >
                <span class="w-1.5 h-1.5 rounded-full bg-emerald-400 animate-pulse"></span>
                API v1 Live
              </span>
              <span
                class="inline-flex items-center px-2 py-0.5 rounded-full text-[11px] font-mono bg-indigo-500/10 text-indigo-300 border border-indigo-500/30"
              >
                HMAC-SHA256
              </span>
              <span
                class="inline-flex items-center px-2 py-0.5 rounded-full text-[11px] font-mono bg-cyan-500/10 text-cyan-300 border border-cyan-500/30"
              >
                120 req/min
              </span>
            </div>
          </div>

          <!-- Quick Actions & Scope Pills -->
          <div class="flex items-center gap-3">
            <div class="hidden md:flex items-center gap-1.5 text-[11px] font-mono text-slate-400">
              <span class="text-slate-500">Scopes:</span>
              @for (scope of apiScopes(); track scope) {
                <span class="px-1.5 py-0.5 rounded bg-slate-800/80 text-slate-300 border border-slate-700/60">
                  {{ scope }}
                </span>
              }
            </div>
            <button
              (click)="selectTab('dispatcher')"
              class="px-3 py-1.5 bg-indigo-600 hover:bg-indigo-500 text-white rounded-lg text-xs font-semibold transition flex items-center gap-1.5 shadow-md shadow-indigo-600/20"
            >
              <span>🚀</span> Test Dispatch
            </button>
          </div>
        </header>

        <!-- Main Content Area -->
        <div class="p-6 max-w-7xl mx-auto w-full space-y-6 flex-1">
          <!-- Navigation Tabs -->
          <div class="border-b border-slate-800 flex items-center gap-2 overflow-x-auto pb-px">
            <button
              (click)="selectTab('dispatcher')"
              [class]="
                activeTab() === 'dispatcher'
                  ? 'border-indigo-500 text-indigo-400 bg-indigo-500/10'
                  : 'border-transparent text-slate-400 hover:text-slate-200 hover:border-slate-700'
              "
              class="px-4 py-2.5 text-xs font-semibold rounded-t-lg border-b-2 transition flex items-center gap-2 whitespace-nowrap"
            >
              <span>⚡</span> Webhook Dispatcher & Inspector
            </button>
            <button
              (click)="selectTab('verifier')"
              [class]="
                activeTab() === 'verifier'
                  ? 'border-indigo-500 text-indigo-400 bg-indigo-500/10'
                  : 'border-transparent text-slate-400 hover:text-slate-200 hover:border-slate-700'
              "
              class="px-4 py-2.5 text-xs font-semibold rounded-t-lg border-b-2 transition flex items-center gap-2 whitespace-nowrap"
            >
              <span>🛡️</span> Live Signature Verifier
            </button>
            <button
              (click)="selectTab('snippets')"
              [class]="
                activeTab() === 'snippets'
                  ? 'border-indigo-500 text-indigo-400 bg-indigo-500/10'
                  : 'border-transparent text-slate-400 hover:text-slate-200 hover:border-slate-700'
              "
              class="px-4 py-2.5 text-xs font-semibold rounded-t-lg border-b-2 transition flex items-center gap-2 whitespace-nowrap"
            >
              <span>💻</span> SDK Code Snippets
            </button>
            <button
              (click)="selectTab('ratelimit')"
              [class]="
                activeTab() === 'ratelimit'
                  ? 'border-indigo-500 text-indigo-400 bg-indigo-500/10'
                  : 'border-transparent text-slate-400 hover:text-slate-200 hover:border-slate-700'
              "
              class="px-4 py-2.5 text-xs font-semibold rounded-t-lg border-b-2 transition flex items-center gap-2 whitespace-nowrap"
            >
              <span>⏱️</span> Rate Limit & Quota Meter
            </button>
          </div>

          <!-- TAB 1: WEBHOOK DISPATCHER & INSPECTOR -->
          @if (activeTab() === 'dispatcher') {
            <div class="grid grid-cols-1 lg:grid-cols-12 gap-6">
              <!-- Left Column: Dispatch Configuration Form (5 cols) -->
              <div class="lg:col-span-5 space-y-4">
                <div class="bg-slate-900/70 border border-slate-800 rounded-2xl p-5 shadow-xl space-y-4">
                  <div class="flex items-center justify-between pb-3 border-b border-slate-800">
                    <h2 class="text-sm font-bold text-white uppercase tracking-wider font-mono flex items-center gap-2">
                      <span>🛠️</span> Dispatch Parameters
                    </h2>
                    <span class="text-[10px] font-mono text-indigo-400 bg-indigo-950/60 px-2 py-0.5 rounded border border-indigo-800">
                      POST
                    </span>
                  </div>

                  <!-- Event Selector -->
                  <div class="space-y-1.5">
                    <label class="block text-xs font-semibold text-slate-300">
                      Webhook Event Type
                    </label>
                    <select
                      [ngModel]="selectedEvent()"
                      (ngModelChange)="onEventChange($event)"
                      class="w-full bg-slate-950 border border-slate-700 rounded-lg px-3 py-2 text-xs font-mono text-white focus:outline-none focus:border-indigo-500"
                    >
                      <option value="order.placed">order.placed (B2B / Commerce checkout)</option>
                      <option value="mission.completed">mission.completed (Autonomous AI Agent)</option>
                      <option value="lead.captured">lead.captured (CRM & Voice SDR)</option>
                      <option value="content.brief.generated">content.brief.generated (SEO & Content)</option>
                      <option value="payment.confirmed">payment.confirmed (Financial Ledger / Stripe)</option>
                    </select>
                  </div>

                  <!-- Target URL -->
                  <div class="space-y-1.5">
                    <label class="block text-xs font-semibold text-slate-300">
                      Target Destination URL
                    </label>
                    <input
                      type="url"
                      [ngModel]="targetUrl()"
                      (ngModelChange)="targetUrl.set($event)"
                      placeholder="https://api.yourdomain.com/webhooks/sgrowing"
                      class="w-full bg-slate-950 border border-slate-700 rounded-lg px-3 py-2 text-xs font-mono text-white focus:outline-none focus:border-indigo-500"
                    />
                  </div>

                  <!-- Webhook Signing Secret -->
                  <div class="space-y-1.5">
                    <div class="flex items-center justify-between">
                      <label class="block text-xs font-semibold text-slate-300">
                        Webhook Signing Secret (HMAC)
                      </label>
                      <button
                        (click)="regenerateSecret()"
                        class="text-[11px] text-indigo-400 hover:text-indigo-300 transition"
                      >
                        Regenerate
                      </button>
                    </div>
                    <div class="relative">
                      <input
                        [type]="showSecret() ? 'text' : 'password'"
                        [ngModel]="signingSecret()"
                        (ngModelChange)="signingSecret.set($event)"
                        class="w-full bg-slate-950 border border-slate-700 rounded-lg px-3 py-2 pr-16 text-xs font-mono text-white focus:outline-none focus:border-indigo-500"
                      />
                      <button
                        type="button"
                        (click)="showSecret.set(!showSecret())"
                        class="absolute right-2 top-2 text-[10px] font-mono text-slate-400 hover:text-slate-200 px-1.5 py-0.5 rounded bg-slate-800"
                      >
                        {{ showSecret() ? 'Hide' : 'Show' }}
                      </button>
                    </div>
                  </div>

                  <!-- Payload Editor -->
                  <div class="space-y-1.5">
                    <div class="flex items-center justify-between">
                      <label class="block text-xs font-semibold text-slate-300">
                        Sample Payload (Editable JSON)
                      </label>
                      <button
                        (click)="resetPayloadToDefault()"
                        class="text-[11px] text-slate-400 hover:text-slate-200 transition"
                      >
                        Reset to default
                      </button>
                    </div>
                    <textarea
                      rows="8"
                      [ngModel]="payloadJson()"
                      (ngModelChange)="onPayloadChange($event)"
                      class="w-full bg-slate-950 border border-slate-800 rounded-lg p-3 text-xs font-mono text-emerald-400 focus:outline-none focus:border-indigo-500 resize-y"
                    ></textarea>
                    <div class="flex items-center justify-between text-[11px]">
                      @if (isPayloadValidJson()) {
                        <span class="text-emerald-400 font-mono">✓ Valid JSON syntax</span>
                      } @else {
                        <span class="text-rose-400 font-mono">⚠ Invalid JSON format</span>
                      }
                      <span class="text-slate-500 font-mono">{{ payloadByteCount() }} bytes</span>
                    </div>
                  </div>

                  <!-- Computed Signature Preview -->
                  <div class="p-3 bg-slate-950/80 rounded-xl border border-slate-800/80 space-y-1">
                    <span class="text-[10px] text-slate-400 uppercase tracking-wider font-mono block">
                      Live Calculated Signature Preview
                    </span>
                    <div class="flex items-center justify-between gap-2">
                      <code class="text-xs font-mono text-indigo-300 truncate" [title]="computedSignaturePreview()">
                        {{ computedSignaturePreview() }}
                      </code>
                      <button
                        (click)="copyToClipboard(computedSignaturePreview(), 'previewSig')"
                        class="text-[10px] font-mono px-2 py-0.5 rounded bg-slate-800 hover:bg-slate-700 text-slate-300 shrink-0"
                      >
                        {{ copiedSnippet() === 'previewSig' ? 'Copied!' : 'Copy' }}
                      </button>
                    </div>
                  </div>

                  <!-- Dispatch Action Button -->
                  <button
                    (click)="dispatchTestWebhook()"
                    [disabled]="isDispatching() || !isPayloadValidJson()"
                    class="w-full py-2.5 bg-gradient-to-r from-indigo-600 to-indigo-500 hover:from-indigo-500 hover:to-indigo-400 disabled:opacity-50 disabled:cursor-not-allowed text-white rounded-xl text-xs font-bold transition flex items-center justify-center gap-2 shadow-lg shadow-indigo-600/30"
                  >
                    @if (isDispatching()) {
                      <span class="animate-spin text-sm">⏳</span> Computing HMAC & Dispatching...
                    } @else {
                      <span>🚀</span> Dispatch Test Webhook & Compute Signature
                    }
                  </button>
                </div>
              </div>

              <!-- Right Column: Results & Inspector Pane (7 cols) -->
              <div class="lg:col-span-7 space-y-4">
                @if (dispatchResult(); as result) {
                  <div class="bg-slate-900/70 border border-slate-800 rounded-2xl p-5 shadow-xl space-y-5">
                    <!-- Result Status Bar -->
                    <div class="flex flex-wrap items-center justify-between gap-3 pb-3 border-b border-slate-800">
                      <div class="flex items-center gap-2.5">
                        <span
                          class="px-2.5 py-1 rounded-md text-xs font-mono font-bold bg-emerald-500/20 text-emerald-400 border border-emerald-500/30 flex items-center gap-1.5"
                        >
                          <span class="w-2 h-2 rounded-full bg-emerald-400"></span>
                          {{ result.statusCode }} {{ result.statusText }}
                        </span>
                        <span class="text-xs font-mono text-slate-400">
                          Latency: <strong class="text-emerald-400">{{ result.latencyMs }}ms</strong>
                        </span>
                      </div>
                      <div class="flex items-center gap-2">
                        <button
                          (click)="loadResultIntoVerifier(result)"
                          class="px-2.5 py-1 rounded-lg text-xs font-medium bg-indigo-950 text-indigo-300 hover:bg-indigo-900/60 border border-indigo-700/50 transition flex items-center gap-1"
                        >
                          <span>🛡️</span> Open in Verifier
                        </button>
                        <span class="text-[11px] font-mono text-slate-500">
                          {{ result.dispatchedAt | date: 'HH:mm:ss.SSS' }}
                        </span>
                      </div>
                    </div>

                    <!-- Calculated Signature Card -->
                    <div class="space-y-1.5">
                      <div class="flex items-center justify-between">
                        <span class="text-xs font-semibold text-slate-300">
                          Calculated HMAC-SHA256 Signature (<code class="text-indigo-400">X-SG-Signature</code>)
                        </span>
                        <button
                          (click)="copyToClipboard(result.signature, 'resultSig')"
                          class="text-xs text-indigo-400 hover:text-indigo-300 font-mono"
                        >
                          {{ copiedSnippet() === 'resultSig' ? 'Copied!' : 'Copy Signature' }}
                        </button>
                      </div>
                      <div class="p-2.5 bg-slate-950 rounded-xl border border-slate-800 font-mono text-xs text-indigo-300 break-all select-all">
                        {{ result.signature }}
                      </div>
                    </div>

                    <!-- Request Headers Table -->
                    <div class="space-y-2">
                      <span class="text-xs font-semibold text-slate-300 block">
                        Outbound Request Headers Transmitted
                      </span>
                      <div class="border border-slate-800 rounded-xl overflow-hidden bg-slate-950">
                        <table class="w-full text-left text-xs">
                          <thead class="bg-slate-900 text-slate-400 font-mono border-b border-slate-800">
                            <tr>
                              <th class="px-3 py-2">Header Name</th>
                              <th class="px-3 py-2">Value</th>
                              <th class="px-3 py-2 hidden sm:table-cell">Description</th>
                            </tr>
                          </thead>
                          <tbody class="divide-y divide-slate-800 font-mono">
                            @for (header of result.headers; track header.name) {
                              <tr class="hover:bg-slate-900/50">
                                <td class="px-3 py-2 text-indigo-400 font-bold whitespace-nowrap">
                                  {{ header.name }}
                                </td>
                                <td class="px-3 py-2 text-slate-300 break-all">
                                  {{ header.value }}
                                </td>
                                <td class="px-3 py-2 text-slate-500 text-[11px] hidden sm:table-cell">
                                  {{ header.description }}
                                </td>
                              </tr>
                            }
                          </tbody>
                        </table>
                      </div>
                    </div>

                    <!-- Simulated Response Body -->
                    <div class="space-y-2">
                      <div class="flex items-center justify-between">
                        <span class="text-xs font-semibold text-slate-300">
                          Receiver Acknowledgement Response Body
                        </span>
                        <span class="text-[11px] font-mono text-emerald-400">application/json</span>
                      </div>
                      <pre class="p-3 bg-slate-950 border border-slate-800 rounded-xl text-xs font-mono text-emerald-400 overflow-x-auto">{{ result.responseBody }}</pre>
                    </div>
                  </div>
                } @else {
                  <!-- Empty State Before First Dispatch -->
                  <div
                    class="bg-slate-900/50 border border-dashed border-slate-800 rounded-2xl p-12 text-center flex flex-col items-center justify-center space-y-4"
                  >
                    <div class="w-14 h-14 rounded-2xl bg-indigo-500/10 border border-indigo-500/20 flex items-center justify-center text-2xl text-indigo-400">
                      ⚡
                    </div>
                    <div class="space-y-1">
                      <h3 class="text-sm font-bold text-white">No Webhook Dispatched Yet</h3>
                      <p class="text-xs text-slate-400 max-w-sm">
                        Select an event type, optionally adjust the payload or destination URL, then click "Dispatch Test Webhook" to compute the signature and inspect headers.
                      </p>
                    </div>
                    <button
                      (click)="dispatchTestWebhook()"
                      class="px-4 py-2 bg-indigo-600 hover:bg-indigo-500 text-white rounded-lg text-xs font-semibold transition"
                    >
                      Dispatch Sample Webhook Now
                    </button>
                  </div>
                }
              </div>
            </div>
          }

          <!-- TAB 2: LIVE SIGNATURE VERIFIER -->
          @if (activeTab() === 'verifier') {
            <div class="grid grid-cols-1 lg:grid-cols-12 gap-6">
              <!-- Verifier Inputs (6 cols) -->
              <div class="lg:col-span-6 space-y-4">
                <div class="bg-slate-900/70 border border-slate-800 rounded-2xl p-5 shadow-xl space-y-4">
                  <div class="flex items-center justify-between pb-3 border-b border-slate-800">
                    <div>
                      <h2 class="text-sm font-bold text-white uppercase tracking-wider font-mono flex items-center gap-2">
                        <span>🛡️</span> Cryptographic HMAC Verifier
                      </h2>
                      <p class="text-xs text-slate-400">
                        Paste your raw payload JSON, signing secret, and incoming signature to verify authenticity.
                      </p>
                    </div>
                    <button
                      (click)="loadDispatcherIntoVerifier()"
                      class="text-[11px] text-indigo-400 hover:text-indigo-300 font-mono whitespace-nowrap"
                    >
                      Import From Dispatcher
                    </button>
                  </div>

                  <!-- Webhook Signing Secret Input -->
                  <div class="space-y-1.5">
                    <label class="block text-xs font-semibold text-slate-300">
                      Webhook Signing Secret (<code class="text-indigo-400">whsec_...</code>)
                    </label>
                    <input
                      type="text"
                      [ngModel]="verifierSecret()"
                      (ngModelChange)="verifierSecret.set($event)"
                      placeholder="whsec_..."
                      class="w-full bg-slate-950 border border-slate-700 rounded-lg px-3 py-2 text-xs font-mono text-white focus:outline-none focus:border-indigo-500"
                    />
                  </div>

                  <!-- Incoming Signature Input -->
                  <div class="space-y-1.5">
                    <label class="block text-xs font-semibold text-slate-300">
                      Incoming <code class="text-indigo-400">X-SG-Signature</code> Header Value
                    </label>
                    <input
                      type="text"
                      [ngModel]="verifierIncomingSig()"
                      (ngModelChange)="verifierIncomingSig.set($event)"
                      placeholder="sha256=1a2b3c4d..."
                      class="w-full bg-slate-950 border border-slate-700 rounded-lg px-3 py-2 text-xs font-mono text-white focus:outline-none focus:border-indigo-500"
                    />
                  </div>

                  <!-- Raw Payload JSON Textarea -->
                  <div class="space-y-1.5">
                    <div class="flex items-center justify-between">
                      <label class="block text-xs font-semibold text-slate-300">
                        Raw Request Body Payload (UTF-8 bytes)
                      </label>
                      <button
                        (click)="formatVerifierJson()"
                        class="text-[11px] text-slate-400 hover:text-slate-200 transition"
                      >
                        Auto-Format JSON
                      </button>
                    </div>
                    <textarea
                      rows="8"
                      [ngModel]="verifierPayload()"
                      (ngModelChange)="verifierPayload.set($event)"
                      placeholder='{"eventId": "evt_...", ...}'
                      class="w-full bg-slate-950 border border-slate-800 rounded-lg p-3 text-xs font-mono text-slate-200 focus:outline-none focus:border-indigo-500 resize-y"
                    ></textarea>
                  </div>
                </div>
              </div>

              <!-- Verification Results & Security Best Practices (6 cols) -->
              <div class="lg:col-span-6 space-y-4">
                <div class="bg-slate-900/70 border border-slate-800 rounded-2xl p-5 shadow-xl space-y-5">
                  <h3 class="text-sm font-bold text-white uppercase tracking-wider font-mono pb-3 border-b border-slate-800">
                    Verification Outcome
                  </h3>

                  <!-- Status Banner -->
                  @let status = verifierStatus();
                  @if (status.state === 'MATCH') {
                    <div class="p-4 bg-emerald-950/40 border border-emerald-500/40 rounded-xl space-y-2">
                      <div class="flex items-center gap-2 text-emerald-400 font-bold text-sm">
                        <span class="text-lg">✓</span> Valid Signature — HMAC-SHA256 Match Verified
                      </div>
                      <p class="text-xs text-emerald-300/80">
                        The calculated HMAC digest matches the incoming header byte-for-byte. The payload has not been tampered with in transit.
                      </p>
                    </div>
                  } @else if (status.state === 'MISMATCH') {
                    <div class="p-4 bg-rose-950/40 border border-rose-500/40 rounded-xl space-y-2">
                      <div class="flex items-center gap-2 text-rose-400 font-bold text-sm">
                        <span class="text-lg">✗</span> Signature Mismatch Detected
                      </div>
                      <p class="text-xs text-rose-300/80">
                        The computed signature differs from the incoming header. Verify that the correct signing secret was used and that no characters in the payload were altered.
                      </p>
                    </div>
                  } @else {
                    <div class="p-4 bg-slate-950 border border-slate-800 rounded-xl space-y-2">
                      <div class="flex items-center gap-2 text-slate-400 font-bold text-sm">
                        <span>ℹ️</span> Awaiting Inputs
                      </div>
                      <p class="text-xs text-slate-400">
                        Enter both the Webhook Signing Secret and Incoming Signature to evaluate verification.
                      </p>
                    </div>
                  }

                  <!-- Side-by-side comparison -->
                  <div class="space-y-3 font-mono text-xs">
                    <div>
                      <span class="text-slate-400 block text-[11px] mb-1">Incoming Signature Provided:</span>
                      <div class="p-2.5 bg-slate-950 border border-slate-800 rounded-lg text-slate-300 break-all select-all">
                        {{ verifierIncomingSig() || '(empty)' }}
                      </div>
                    </div>

                    <div>
                      <span class="text-slate-400 block text-[11px] mb-1">Calculated HMAC-SHA256 Signature:</span>
                      <div class="p-2.5 bg-slate-950 border border-slate-800 rounded-lg text-indigo-300 break-all select-all">
                        {{ status.computedSignature }}
                      </div>
                    </div>
                  </div>

                  <!-- Security Best Practices Callout -->
                  <div class="pt-3 border-t border-slate-800 space-y-2">
                    <span class="text-xs font-bold text-slate-300 uppercase tracking-wider font-mono">
                      Security Recommendations
                    </span>
                    <ul class="text-xs text-slate-400 space-y-1.5 list-disc list-inside">
                      <li>
                        <strong class="text-slate-200">Constant-Time Comparison:</strong> Always use constant-time string comparison (e.g., <code class="text-indigo-300">CryptographicOperations.FixedTimeEquals</code> or <code class="text-indigo-300">crypto.timingSafeEqual</code>) to prevent timing attacks.
                      </li>
                      <li>
                        <strong class="text-slate-200">Replay Protection:</strong> Reject payloads with <code class="text-indigo-300">X-SG-Timestamp</code> older than 5 minutes (300s) and track unique <code class="text-indigo-300">X-SG-Delivery-Id</code> values.
                      </li>
                      <li>
                        <strong class="text-slate-200">Raw Body Requirement:</strong> Ensure verification runs against the raw unparsed UTF-8 request body bytes before JSON deserialization.
                      </li>
                    </ul>
                  </div>
                </div>
              </div>
            </div>
          }

          <!-- TAB 3: SDK CODE SNIPPETS -->
          @if (activeTab() === 'snippets') {
            <div class="space-y-6">
              <!-- Language Selector Bar -->
              <div class="flex flex-wrap items-center justify-between gap-4 p-4 bg-slate-900/70 border border-slate-800 rounded-2xl">
                <div class="flex items-center gap-2">
                  <span class="text-xs font-semibold text-slate-300">Select Language:</span>
                  <div class="flex items-center gap-1.5 bg-slate-950 p-1 rounded-xl border border-slate-800">
                    <button
                      (click)="snippetLanguage.set('csharp')"
                      [class]="
                        snippetLanguage() === 'csharp'
                          ? 'bg-indigo-600 text-white font-bold'
                          : 'text-slate-400 hover:text-white'
                      "
                      class="px-3 py-1.5 rounded-lg text-xs font-mono transition"
                    >
                      C# (.NET 10)
                    </button>
                    <button
                      (click)="snippetLanguage.set('typescript')"
                      [class]="
                        snippetLanguage() === 'typescript'
                          ? 'bg-indigo-600 text-white font-bold'
                          : 'text-slate-400 hover:text-white'
                      "
                      class="px-3 py-1.5 rounded-lg text-xs font-mono transition"
                    >
                      TypeScript / Node
                    </button>
                    <button
                      (click)="snippetLanguage.set('python')"
                      [class]="
                        snippetLanguage() === 'python'
                          ? 'bg-indigo-600 text-white font-bold'
                          : 'text-slate-400 hover:text-white'
                      "
                      class="px-3 py-1.5 rounded-lg text-xs font-mono transition"
                    >
                      Python
                    </button>
                    <button
                      (click)="snippetLanguage.set('curl')"
                      [class]="
                        snippetLanguage() === 'curl'
                          ? 'bg-indigo-600 text-white font-bold'
                          : 'text-slate-400 hover:text-white'
                      "
                      class="px-3 py-1.5 rounded-lg text-xs font-mono transition"
                    >
                      cURL
                    </button>
                  </div>
                </div>

                <button
                  (click)="copyCurrentSnippet()"
                  class="px-3.5 py-1.5 bg-slate-800 hover:bg-slate-700 text-white rounded-lg text-xs font-mono font-semibold transition flex items-center gap-1.5 border border-slate-700"
                >
                  @if (copiedSnippet() === 'activeSdk') {
                    <span class="text-emerald-400">✓</span> Copied to Clipboard!
                  } @else {
                    <span>📋</span> Copy Snippet
                  }
                </button>
              </div>

              <!-- Code Preview Display -->
              <div class="bg-slate-900/70 border border-slate-800 rounded-2xl overflow-hidden shadow-2xl">
                <div class="px-5 py-3 bg-slate-950 border-b border-slate-800 flex items-center justify-between">
                  <div class="flex items-center gap-2">
                    <span class="w-3 h-3 rounded-full bg-rose-500/80 inline-block"></span>
                    <span class="w-3 h-3 rounded-full bg-amber-500/80 inline-block"></span>
                    <span class="w-3 h-3 rounded-full bg-emerald-500/80 inline-block"></span>
                    <span class="text-xs font-mono text-slate-400 ml-2">
                      {{ getSnippetFilename() }}
                    </span>
                  </div>
                  <span class="text-[11px] font-mono text-slate-500">
                    Production Verified • Timing-Safe • SGrowing v1
                  </span>
                </div>
                <pre class="p-6 text-xs font-mono text-slate-200 overflow-x-auto leading-relaxed bg-slate-950/70"><code>{{ getActiveSnippetCode() }}</code></pre>
              </div>
            </div>
          }

          <!-- TAB 4: RATE LIMIT & QUOTA METER -->
          @if (activeTab() === 'ratelimit') {
            <div class="grid grid-cols-1 lg:grid-cols-12 gap-6">
              <!-- Left Column: Live Gauge & Consumption (7 cols) -->
              <div class="lg:col-span-7 space-y-6">
                <!-- Usage Meter Card -->
                @let used = rateLimitUsed();
                @let total = rateLimitTotal();
                @let remaining = total - used;
                @let percent = (used / total) * 100;

                <div class="bg-slate-900/70 border border-slate-800 rounded-2xl p-6 shadow-xl space-y-6">
                  <div class="flex items-center justify-between">
                    <div>
                      <h2 class="text-sm font-bold text-white uppercase tracking-wider font-mono">
                        Tenant Rate Limit & Quota Meter
                      </h2>
                      <p class="text-xs text-slate-400">
                        Tracks outbound and inbound API requests within the active 60-second sliding window.
                      </p>
                    </div>
                    <span
                      class="px-2.5 py-1 rounded-full text-xs font-mono font-bold"
                      [ngClass]="percent > 90 ? 'bg-rose-500/20 text-rose-400' : 'bg-emerald-500/20 text-emerald-400'"
                    >
                      {{ percent > 90 ? 'Near Capacity' : 'Normal Load' }}
                    </span>
                  </div>

                  <!-- Visual Progress Bar -->
                  <div class="space-y-2">
                    <div class="flex items-baseline justify-between text-xs font-mono">
                      <span class="text-slate-400">Current Window Consumption</span>
                      <span class="text-white font-bold">
                        {{ used }} / {{ total }} req/min ({{ percent | number: '1.0-0' }}%)
                      </span>
                    </div>
                    <div class="w-full bg-slate-950 rounded-full h-3 overflow-hidden border border-slate-800 p-0.5">
                      <div
                        class="h-full rounded-full transition-all duration-300"
                        [style.width.%]="percent > 100 ? 100 : percent"
                        [ngClass]="
                          percent > 90
                            ? 'bg-rose-500'
                            : percent > 70
                              ? 'bg-amber-500'
                              : 'bg-gradient-to-r from-indigo-500 to-cyan-400'
                        "
                      ></div>
                    </div>
                  </div>

                  <!-- KPI Stats Grid -->
                  <div class="grid grid-cols-3 gap-3 pt-2">
                    <div class="p-3 bg-slate-950 rounded-xl border border-slate-800">
                      <span class="text-[10px] text-slate-500 uppercase tracking-wider font-mono block">
                        Remaining
                      </span>
                      <span class="text-xl font-bold font-mono text-emerald-400">
                        {{ remaining > 0 ? remaining : 0 }}
                      </span>
                    </div>

                    <div class="p-3 bg-slate-950 rounded-xl border border-slate-800">
                      <span class="text-[10px] text-slate-500 uppercase tracking-wider font-mono block">
                        Window Reset In
                      </span>
                      <span class="text-xl font-bold font-mono text-cyan-400">
                        {{ resetCountdownSeconds() }}s
                      </span>
                    </div>

                    <div class="p-3 bg-slate-950 rounded-xl border border-slate-800">
                      <span class="text-[10px] text-slate-500 uppercase tracking-wider font-mono block">
                        DLQ Status
                      </span>
                      <span
                        class="text-sm font-bold font-mono"
                        [ngClass]="dlqHealthStatus() === 'HEALTHY' ? 'text-emerald-400' : 'text-rose-400'"
                      >
                        {{ dlqHealthStatus() === 'HEALTHY' ? 'Healthy (0)' : 'Alert' }}
                      </span>
                    </div>
                  </div>

                  <!-- Rate Limiter Controls -->
                  <div class="flex items-center gap-3 pt-2">
                    <button
                      (click)="simulateRequest(1)"
                      class="px-3.5 py-2 bg-indigo-600 hover:bg-indigo-500 text-white rounded-lg text-xs font-semibold transition shadow-md"
                    >
                      + Simulate 1 Request
                    </button>
                    <button
                      (click)="simulateRequest(10)"
                      class="px-3.5 py-2 bg-slate-800 hover:bg-slate-700 text-slate-200 rounded-lg text-xs font-semibold transition border border-slate-700"
                    >
                      + Simulate 10 Requests
                    </button>
                    <button
                      (click)="resetRateLimitCounter()"
                      class="px-3.5 py-2 bg-slate-900 hover:bg-slate-850 text-slate-400 hover:text-white rounded-lg text-xs transition border border-slate-800"
                    >
                      Reset Window
                    </button>
                  </div>
                </div>

                <!-- Rate Limit HTTP Response Headers Table -->
                <div class="bg-slate-900/70 border border-slate-800 rounded-2xl p-5 shadow-xl space-y-3">
                  <h3 class="text-xs font-bold text-white uppercase tracking-wider font-mono">
                    Standard Rate Limiting HTTP Headers
                  </h3>
                  <div class="border border-slate-800 rounded-xl overflow-hidden bg-slate-950 font-mono text-xs">
                    <table class="w-full text-left">
                      <thead class="bg-slate-900 text-slate-400 border-b border-slate-800">
                        <tr>
                          <th class="px-3 py-2">Header</th>
                          <th class="px-3 py-2">Current Value</th>
                          <th class="px-3 py-2">Spec Definition</th>
                        </tr>
                      </thead>
                      <tbody class="divide-y divide-slate-800">
                        <tr>
                          <td class="px-3 py-2 text-indigo-400 font-bold">X-RateLimit-Limit</td>
                          <td class="px-3 py-2 text-white">{{ total }}</td>
                          <td class="px-3 py-2 text-slate-500 text-[11px]">Maximum requests allowed per 60s window</td>
                        </tr>
                        <tr>
                          <td class="px-3 py-2 text-indigo-400 font-bold">X-RateLimit-Remaining</td>
                          <td class="px-3 py-2 text-emerald-400">{{ remaining > 0 ? remaining : 0 }}</td>
                          <td class="px-3 py-2 text-slate-500 text-[11px]">Requests remaining in active window</td>
                        </tr>
                        <tr>
                          <td class="px-3 py-2 text-indigo-400 font-bold">X-RateLimit-Reset</td>
                          <td class="px-3 py-2 text-cyan-400">{{ currentResetEpoch() }}</td>
                          <td class="px-3 py-2 text-slate-500 text-[11px]">Unix epoch seconds when quota resets</td>
                        </tr>
                      </tbody>
                    </table>
                  </div>
                </div>
              </div>

              <!-- Right Column: Tier Limits & Scopes (5 cols) -->
              <div class="lg:col-span-5 space-y-6">
                <!-- Tier Comparison -->
                <div class="bg-slate-900/70 border border-slate-800 rounded-2xl p-5 shadow-xl space-y-4">
                  <h3 class="text-xs font-bold text-white uppercase tracking-wider font-mono pb-2 border-b border-slate-800">
                    SGrowing API Quota Tiers
                  </h3>
                  <div class="space-y-3 text-xs">
                    <div class="p-3 bg-slate-950 rounded-xl border border-slate-800 flex items-center justify-between">
                      <div>
                        <div class="font-bold text-slate-300">Sandbox / Developer</div>
                        <div class="text-[11px] text-slate-500">Testing & Prototype integrations</div>
                      </div>
                      <span class="font-mono font-bold text-slate-400">60 req/min</span>
                    </div>

                    <div class="p-3 bg-indigo-950/40 rounded-xl border border-indigo-500/40 flex items-center justify-between">
                      <div>
                        <div class="font-bold text-indigo-300">Production Standard (Active)</div>
                        <div class="text-[11px] text-indigo-400/80">Default for commercial tenant keys</div>
                      </div>
                      <span class="font-mono font-bold text-indigo-300">120 req/min</span>
                    </div>

                    <div class="p-3 bg-slate-950 rounded-xl border border-slate-800 flex items-center justify-between">
                      <div>
                        <div class="font-bold text-slate-300">Enterprise High-Throughput</div>
                        <div class="text-[11px] text-slate-500">Dedicated outbound cluster</div>
                      </div>
                      <span class="font-mono font-bold text-slate-400">600 req/min</span>
                    </div>
                  </div>
                </div>

                <!-- API Key Scopes Matrix -->
                <div class="bg-slate-900/70 border border-slate-800 rounded-2xl p-5 shadow-xl space-y-3">
                  <h3 class="text-xs font-bold text-white uppercase tracking-wider font-mono">
                    Enforced Security Scopes
                  </h3>
                  <div class="space-y-2 text-xs">
                    <div class="p-2.5 bg-slate-950 rounded-lg border border-slate-800 flex items-start gap-2">
                      <code class="text-indigo-400 font-bold shrink-0">developer:read</code>
                      <span class="text-slate-400 text-[11px]">Inspect logs, verify keys, read webhook subscriptions.</span>
                    </div>
                    <div class="p-2.5 bg-slate-950 rounded-lg border border-slate-800 flex items-start gap-2">
                      <code class="text-indigo-400 font-bold shrink-0">developer:write</code>
                      <span class="text-slate-400 text-[11px]">Register endpoints, dispatch test webhooks, replay DLQ.</span>
                    </div>
                    <div class="p-2.5 bg-slate-950 rounded-lg border border-slate-800 flex items-start gap-2">
                      <code class="text-cyan-400 font-bold shrink-0">* (Admin)</code>
                      <span class="text-slate-400 text-[11px]">Unrestricted access across all developer endpoints.</span>
                    </div>
                  </div>
                </div>
              </div>
            </div>
          }
        </div>
      </main>
    </div>
  `
})
export class DeveloperWebhookPlaygroundComponent {
  // Navigation Tabs
  activeTab = signal<'dispatcher' | 'verifier' | 'snippets' | 'ratelimit'>('dispatcher');

  // Dispatcher State
  selectedEvent = signal<WebhookEventType>('order.placed');
  targetUrl = signal<string>('https://api.partner.io/webhooks/sgrowing');
  signingSecret = signal<string>('whsec_8849b3c21a4f009e8d7c22');
  showSecret = signal<boolean>(false);
  payloadJson = signal<string>(JSON.stringify(SAMPLE_PAYLOADS['order.placed'], null, 2));
  isDispatching = signal<boolean>(false);
  dispatchResult = signal<DispatchResult | null>(null);

  // Live Signature Verifier State
  verifierSecret = signal<string>('whsec_8849b3c21a4f009e8d7c22');
  verifierIncomingSig = signal<string>('');
  verifierPayload = signal<string>(JSON.stringify(SAMPLE_PAYLOADS['order.placed'], null, 2));

  // SDK Snippet State
  snippetLanguage = signal<SnippetLanguage>('csharp');
  copiedSnippet = signal<string | null>(null);

  // Rate Limiting State
  rateLimitTotal = signal<number>(120);
  rateLimitUsed = signal<number>(42);
  resetCountdownSeconds = signal<number>(28);
  dlqFailedCount = signal<number>(0);
  apiScopes = signal<string[]>(['developer:read', 'developer:write', '*']);

  // Computed signals
  computedSignaturePreview = computed(() => {
    return computeHmacSha256(this.signingSecret(), this.payloadJson());
  });

  isPayloadValidJson = computed(() => {
    try {
      JSON.parse(this.payloadJson());
      return true;
    } catch {
      return false;
    }
  });

  payloadByteCount = computed(() => {
    return new TextEncoder().encode(this.payloadJson()).length;
  });

  verifierStatus = computed(() => {
    const secret = this.verifierSecret().trim();
    const payload = this.verifierPayload();
    const incoming = this.verifierIncomingSig().trim();

    const computedSignature = secret ? computeHmacSha256(secret, payload) : 'sha256=(missing-secret)';

    if (!incoming || !secret) {
      return {
        state: 'WAITING' as const,
        computedSignature
      };
    }

    const isMatch = incoming.toLowerCase() === computedSignature.toLowerCase();
    return {
      state: (isMatch ? 'MATCH' : 'MISMATCH') as 'MATCH' | 'MISMATCH',
      computedSignature
    };
  });

  dlqHealthStatus = computed<'HEALTHY' | 'DEGRADED'>(() => {
    return this.dlqFailedCount() === 0 ? 'HEALTHY' : 'DEGRADED';
  });

  currentResetEpoch = computed(() => {
    return Math.floor(Date.now() / 1000) + this.resetCountdownSeconds();
  });

  selectTab(tab: 'dispatcher' | 'verifier' | 'snippets' | 'ratelimit') {
    this.activeTab.set(tab);
  }

  onEventChange(newEvent: WebhookEventType) {
    this.selectedEvent.set(newEvent);
    const sample = SAMPLE_PAYLOADS[newEvent] || {};
    this.payloadJson.set(JSON.stringify(sample, null, 2));
  }

  onPayloadChange(newPayload: string) {
    this.payloadJson.set(newPayload);
  }

  regenerateSecret() {
    const hex = Array.from({ length: 24 }, () =>
      Math.floor(Math.random() * 16).toString(16)
    ).join('');
    this.signingSecret.set(`whsec_${hex}`);
  }

  resetPayloadToDefault() {
    const sample = SAMPLE_PAYLOADS[this.selectedEvent()];
    this.payloadJson.set(JSON.stringify(sample, null, 2));
  }

  dispatchTestWebhook() {
    if (!this.isPayloadValidJson()) return;

    this.isDispatching.set(true);
    const timestamp = Math.floor(Date.now() / 1000);
    const randomHex = Math.random().toString(36).substring(2, 10);
    const deliveryId = `del_${timestamp}_${randomHex}`;
    const event = this.selectedEvent();
    const rawPayload = this.payloadJson();
    const secret = this.signingSecret();

    // Canonical signature calculation over the raw payload
    const signature = computeHmacSha256(secret, rawPayload);

    // Simulate realistic network latency (35ms - 65ms)
    const latency = Math.floor(Math.random() * 30) + 35;

    const headers: WebhookHeaderEntry[] = [
      {
        name: 'X-SG-Signature',
        value: signature,
        description: 'HMAC-SHA256 hex digest computed with endpoint secret'
      },
      {
        name: 'X-SG-Event',
        value: event,
        description: 'Canonical event type identifying message schema'
      },
      {
        name: 'X-SG-Delivery-Id',
        value: deliveryId,
        description: 'Unique UUID v4 to support idempotent processing'
      },
      {
        name: 'X-SG-Timestamp',
        value: timestamp.toString(),
        description: 'Unix timestamp (UTC seconds) to guard against replay attacks'
      },
      {
        name: 'Content-Type',
        value: 'application/json; charset=utf-8',
        description: 'MIME type of the raw serialized payload'
      }
    ];

    const responseBody = JSON.stringify(
      {
        received: true,
        event: event,
        deliveryId: deliveryId,
        signatureVerified: true,
        acknowledgedAt: new Date().toISOString()
      },
      null,
      2
    );

    this.dispatchResult.set({
      statusCode: 200,
      statusText: 'OK',
      latencyMs: latency,
      deliveryId,
      timestamp,
      signature,
      targetUrl: this.targetUrl(),
      eventType: event,
      headers,
      requestPayload: rawPayload,
      responseBody,
      dispatchedAt: new Date()
    });

    // Increment rate limit usage
    this.simulateRequest(1);

    this.isDispatching.set(false);
  }

  loadResultIntoVerifier(result: DispatchResult) {
    this.verifierPayload.set(result.requestPayload);
    this.verifierSecret.set(this.signingSecret());
    this.verifierIncomingSig.set(result.signature);
    this.activeTab.set('verifier');
  }

  loadDispatcherIntoVerifier() {
    this.verifierPayload.set(this.payloadJson());
    this.verifierSecret.set(this.signingSecret());
    this.verifierIncomingSig.set(this.computedSignaturePreview());
  }

  formatVerifierJson() {
    try {
      const obj = JSON.parse(this.verifierPayload());
      this.verifierPayload.set(JSON.stringify(obj, null, 2));
    } catch {
      // Keep original text if invalid
    }
  }

  copyToClipboard(text: string, label: string) {
    if (typeof navigator !== 'undefined' && navigator.clipboard) {
      navigator.clipboard.writeText(text).catch(() => {});
    }
    this.copiedSnippet.set(label);
    setTimeout(() => {
      if (this.copiedSnippet() === label) {
        this.copiedSnippet.set(null);
      }
    }, 2000);
  }

  copyCurrentSnippet() {
    const code = this.getActiveSnippetCode();
    this.copyToClipboard(code, 'activeSdk');
  }

  simulateRequest(count = 1) {
    const next = Math.min(this.rateLimitTotal(), this.rateLimitUsed() + count);
    this.rateLimitUsed.set(next);
  }

  resetRateLimitCounter() {
    this.rateLimitUsed.set(0);
    this.resetCountdownSeconds.set(60);
  }

  getSnippetFilename(): string {
    switch (this.snippetLanguage()) {
      case 'csharp':
        return 'WebhookReceiver.cs (.NET 10)';
      case 'typescript':
        return 'webhook-handler.ts (Node.js)';
      case 'python':
        return 'webhook_receiver.py (Python 3.12)';
      case 'curl':
        return 'test-dispatch.sh (cURL)';
    }
  }

  getActiveSnippetCode(): string {
    switch (this.snippetLanguage()) {
      case 'csharp':
        return `using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;

// ASP.NET Core / .NET 10 Webhook Handler
var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();

var webhookSecret = Environment.GetEnvironmentVariable("SGROWING_WEBHOOK_SECRET")
    ?? "${this.signingSecret()}";

app.MapPost("/webhooks/sgrowing", async (HttpContext context) =>
{
    var signature = context.Request.Headers["X-SG-Signature"].ToString();
    var eventType = context.Request.Headers["X-SG-Event"].ToString();
    var deliveryId = context.Request.Headers["X-SG-Delivery-Id"].ToString();

    // Read raw body UTF-8 bytes for bit-perfect cryptographic verification
    using var reader = new StreamReader(context.Request.Body, Encoding.UTF8);
    var rawPayload = await reader.ReadToEndAsync();

    // Compute HMAC-SHA256
    using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(webhookSecret));
    var hashBytes = hmac.ComputeHash(Encoding.UTF8.GetBytes(rawPayload));
    var expectedSignature = "sha256=" + Convert.ToHexString(hashBytes).ToLowerInvariant();

    // Constant-time comparison to guard against timing analysis
    if (!CryptographicOperations.FixedTimeEquals(
        Encoding.UTF8.GetBytes(signature),
        Encoding.UTF8.GetBytes(expectedSignature)))
    {
        return Results.Unauthorized();
    }

    // Process deserialized event
    var doc = JsonDocument.Parse(rawPayload);
    app.Logger.LogInformation("Webhook accepted: {EventType} (ID: {DeliveryId})", eventType, deliveryId);

    return Results.Ok(new { received = true, deliveryId });
});

app.Run();`;

      case 'typescript':
        return `import express, { Request, Response } from 'express';
import crypto from 'node:crypto';

const app = express();
const WEBHOOK_SECRET = process.env.SGROWING_WEBHOOK_SECRET || '${this.signingSecret()}';

// Important: Parse raw body as UTF-8 buffer for exact HMAC validation
app.post('/webhooks/sgrowing', express.raw({ type: 'application/json' }), (req: Request, res: Response) => {
  const signature = req.headers['x-sg-signature'] as string;
  const eventType = req.headers['x-sg-event'] as string;
  const deliveryId = req.headers['x-sg-delivery-id'] as string;

  const rawPayload = req.body.toString('utf8');

  // Compute HMAC-SHA256 digest
  const expectedSignature = 'sha256=' + crypto.createHmac('sha256', WEBHOOK_SECRET).update(rawPayload).digest('hex');

  // Constant-time comparison to prevent side-channel timing attacks
  const sigBuffer = Buffer.from(signature || '');
  const expectedBuffer = Buffer.from(expectedSignature);

  if (sigBuffer.length !== expectedBuffer.length || !crypto.timingSafeEqual(sigBuffer, expectedBuffer)) {
    return res.status(401).json({ error: 'Unauthorized: HMAC signature mismatch' });
  }

  const payload = JSON.parse(rawPayload);
  console.log(\`[Webhook Verified] Event: \${eventType}, Delivery: \${deliveryId}\`);

  return res.status(200).json({ received: true, eventType });
});

app.listen(3000, () => console.log('Webhook server running on port 3000'));`;

      case 'python':
        return `import hmac
import hashlib
import json
import os
from flask import Flask, request, jsonify, abort

app = Flask(__name__)
WEBHOOK_SECRET = os.environ.get("SGROWING_WEBHOOK_SECRET", "${this.signingSecret()}").encode("utf-8")

@app.route("/webhooks/sgrowing", methods=["POST"])
def handle_sgrowing_webhook():
    signature = request.headers.get("X-SG-Signature", "")
    event_type = request.headers.get("X-SG-Event", "")
    delivery_id = request.headers.get("X-SG-Delivery-Id", "")

    # Retrieve raw incoming payload bytes
    raw_payload_bytes = request.get_data()

    # Compute expected HMAC-SHA256
    expected_hex = hmac.new(WEBHOOK_SECRET, raw_payload_bytes, hashlib.sha256).hexdigest()
    expected_signature = f"sha256={expected_hex}"

    # Timing-safe constant-time comparison
    if not hmac.compare_digest(signature, expected_signature):
        abort(401, description="Unauthorized: HMAC signature mismatch")

    event_data = json.loads(raw_payload_bytes.decode("utf-8"))
    print(f"[Webhook Accepted] Event: {event_type}, ID: {delivery_id}")

    return jsonify({"received": True, "deliveryId": delivery_id}), 200

if __name__ == "__main__":
    app.run(port=5000)`;

      case 'curl':
        return `# 1. Dispatch Test Webhook via Developer API
curl -X POST "${this.targetUrl()}" \\
  -H "Content-Type: application/json" \\
  -H "X-SG-Event: ${this.selectedEvent()}" \\
  -H "X-SG-Delivery-Id: del_$(date +%s)_sample" \\
  -H "X-SG-Signature: ${this.computedSignaturePreview()}" \\
  -d '${this.payloadJson().replace(/'/g, "'\\''")}'

# 2. Ping Scoped Developer Health Endpoint
curl -X GET "https://api.sgrowing.io/api/developer/v1/auth-test" \\
  -H "X-Api-Key: sg_live_9a87f10b8c4d2e5a1b3c"`;
    }
  }
}
