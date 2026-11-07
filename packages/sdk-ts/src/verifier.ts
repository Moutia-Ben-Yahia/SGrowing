import * as crypto from 'node:crypto';
import type { WebhookVerificationOptions } from './models.js';

/**
 * Constant-time HMAC-SHA256 webhook signature verifier.
 * Protects against timing attacks and timestamp replay attacks.
 */
export class SGrowingWebhookVerifier {
  /**
   * Default clock skew tolerance in seconds (5 minutes).
   */
  public static readonly DEFAULT_TOLERANCE_SECONDS = 300;

  /**
   * Computes an HMAC-SHA256 signature for a payload.
   *
   * @param rawBody - Raw webhook request body as string or Buffer.
   * @param secret - Webhook secret key.
   * @param timestamp - Optional Unix timestamp in seconds to bind to the signature.
   * @returns Hex-encoded HMAC prefixed with 'sha256='.
   */
  public static computeSignature(
    rawBody: string | Buffer,
    secret: string,
    timestamp?: number | string
  ): string {
    const bodyString = typeof rawBody === 'string' ? rawBody : rawBody.toString('utf-8');
    const dataToSign = timestamp !== undefined && timestamp !== null && timestamp !== ''
      ? `${timestamp}.${bodyString}`
      : bodyString;

    const hmac = crypto.createHmac('sha256', secret);
    hmac.update(dataToSign, 'utf-8');
    const digest = hmac.digest('hex');
    return `sha256=${digest.toLowerCase()}`;
  }

  /**
   * Verifies an incoming webhook request signature in constant time.
   *
   * @param rawBody - Raw request body string or Buffer.
   * @param secret - Shared webhook secret key.
   * @param signatureHeader - Value of X-SG-Signature or X-SGrowing-Signature header.
   *                          Supports 'sha256=<hex>', 't=<timestamp>,v1=<hex>', or raw hex.
   * @param toleranceOrOptions - Tolerance in seconds or verification options object.
   * @param timestampHeader - Optional explicit timestamp header value (Unix seconds or ISO date).
   * @returns True if the signature is authentic and within clock tolerance; otherwise false.
   */
  public static verify(
    rawBody: string | Buffer,
    secret: string,
    signatureHeader: string,
    toleranceOrOptions?: number | WebhookVerificationOptions,
    timestampHeader?: string | number
  ): boolean {
    if (!rawBody || !secret || !signatureHeader) {
      return false;
    }

    let toleranceSeconds = SGrowingWebhookVerifier.DEFAULT_TOLERANCE_SECONDS;
    let explicitTimestamp: number | string | undefined = timestampHeader;

    if (typeof toleranceOrOptions === 'number') {
      toleranceSeconds = toleranceOrOptions;
    } else if (toleranceOrOptions && typeof toleranceOrOptions === 'object') {
      if (toleranceOrOptions.toleranceSeconds !== undefined) {
        toleranceSeconds = toleranceOrOptions.toleranceSeconds;
      }
      if (toleranceOrOptions.timestamp !== undefined) {
        explicitTimestamp = toleranceOrOptions.timestamp;
      }
    }

    const bodyString = typeof rawBody === 'string' ? rawBody : rawBody.toString('utf-8');

    // Parse signature header: could be "sha256=abcdef...", "t=1731000000,v1=abcdef...", or "v1=abcdef..."
    let parsedTimestamp: number | undefined;
    let expectedHex = signatureHeader.trim();

    if (signatureHeader.includes('t=') && (signatureHeader.includes('v1=') || signatureHeader.includes('sha256='))) {
      const parts = signatureHeader.split(',');
      for (const part of parts) {
        const [k, v] = part.split('=').map((s) => s.trim());
        if (k === 't') {
          const ts = Number(v);
          if (!Number.isNaN(ts)) parsedTimestamp = ts;
        } else if (k === 'v1' || k === 'sha256') {
          expectedHex = v;
        }
      }
    } else if (signatureHeader.toLowerCase().startsWith('sha256=')) {
      expectedHex = signatureHeader.slice(7).trim();
    } else if (signatureHeader.toLowerCase().startsWith('v1=')) {
      expectedHex = signatureHeader.slice(3).trim();
    }

    // If explicit timestamp header was provided, parse it
    if (explicitTimestamp !== undefined) {
      const parsed = typeof explicitTimestamp === 'number'
        ? explicitTimestamp
        : /^\d+$/.test(explicitTimestamp.trim())
          ? Number(explicitTimestamp.trim())
          : Math.floor(new Date(explicitTimestamp).getTime() / 1000);

      if (!Number.isNaN(parsed)) {
        parsedTimestamp = parsed;
      }
    }

    // If timestamp is present, validate replay window
    if (parsedTimestamp !== undefined) {
      const nowSeconds = Math.floor(Date.now() / 1000);
      if (Math.abs(nowSeconds - parsedTimestamp) > toleranceSeconds) {
        return false; // Expired / replay attack
      }
    }

    // Try verifying candidate signatures
    const candidates: string[] = [];
    if (parsedTimestamp !== undefined) {
      // 1. Timestamp-bound hash: `${timestamp}.${body}`
      candidates.push(
        crypto.createHmac('sha256', secret).update(`${parsedTimestamp}.${bodyString}`, 'utf-8').digest('hex')
      );
    }
    // 2. Direct body hash
    candidates.push(
      crypto.createHmac('sha256', secret).update(bodyString, 'utf-8').digest('hex')
    );

    const targetBuffer = Buffer.from(expectedHex.toLowerCase(), 'utf-8');

    for (const cand of candidates) {
      const candBuffer = Buffer.from(cand.toLowerCase(), 'utf-8');
      if (candBuffer.length === targetBuffer.length && crypto.timingSafeEqual(candBuffer, targetBuffer)) {
        return true;
      }
    }

    return false;
  }
}
