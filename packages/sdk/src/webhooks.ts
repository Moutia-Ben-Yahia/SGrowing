import * as crypto from 'crypto';

export interface WebhookHeaderVerificationOptions {
  payload: string | Buffer;
  signature: string;
  secret: string;
  timestamp: number | string;
  toleranceSeconds?: number;
}

export function computeWebhookSignature(payload: string, secret: string, timestamp: number | string): string {
  const stringToSign = `${timestamp}.${payload}`;
  const hmac = crypto.createHmac('sha256', secret);
  hmac.update(stringToSign);
  return 'sha256=' + hmac.digest('hex');
}

export function verifyWebhookSignature(options: WebhookHeaderVerificationOptions): boolean {
  const { payload, signature, secret, timestamp, toleranceSeconds = 300 } = options;
  if (!signature || !secret || payload === undefined || payload === null) {
    return false;
  }

  const payloadStr = typeof payload === 'string' ? payload : payload.toString('utf-8');
  const tsNum = typeof timestamp === 'number' ? timestamp : parseInt(String(timestamp), 10);

  if (isNaN(tsNum)) {
    return false;
  }

  // Verify timestamp within tolerance window (default 5 minutes)
  const now = Math.floor(Date.now() / 1000);
  if (Math.abs(now - tsNum) > toleranceSeconds) {
    return false;
  }

  const expected = computeWebhookSignature(payloadStr, secret, tsNum);

  const expectedBuf = Buffer.from(expected, 'utf-8');
  const sigBuf = Buffer.from(signature, 'utf-8');

  if (expectedBuf.length !== sigBuf.length) {
    return false;
  }

  try {
    return crypto.timingSafeEqual(expectedBuf, sigBuf);
  } catch {
    return false;
  }
}
