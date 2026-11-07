import type {
  AgentMissionPlan,
  MissionObjectiveRequest,
  RecordSyntheticMetricRequest,
  SGrowingClientOptions,
  SreHealthRadarReport,
  TestDispatchWebhookResponse,
  TransformPayloadResult,
  WebhookVerificationOptions
} from './models.js';
import { SGrowingWebhookVerifier } from './verifier.js';

export class SGrowingApiError extends Error {
  public readonly status: number;
  public readonly responseBody: unknown;

  constructor(message: string, status: number, responseBody?: unknown) {
    super(message);
    this.name = 'SGrowingApiError';
    this.status = status;
    this.responseBody = responseBody;
  }
}

/**
 * Missions API client module.
 */
export class MissionsClient {
  constructor(private readonly client: SGrowingClient) {}

  /**
   * Plans an autonomous AI agent mission decomposing objectives into a task DAG.
   */
  public async plan(req: MissionObjectiveRequest): Promise<AgentMissionPlan> {
    return this.client.request<AgentMissionPlan>('/api/v1/ai/missions/plan', {
      method: 'POST',
      body: JSON.stringify(req)
    });
  }

  /**
   * Advances execution of an active mission, optionally approving a human-in-the-loop task.
   */
  public async step(missionId: string, approveTaskId?: string): Promise<AgentMissionPlan> {
    const path = approveTaskId
      ? `/api/v1/ai/missions/${encodeURIComponent(missionId)}/approve/${encodeURIComponent(approveTaskId)}`
      : `/api/v1/ai/missions/${encodeURIComponent(missionId)}/step`;

    return this.client.request<AgentMissionPlan>(path, {
      method: 'POST'
    });
  }

  /**
   * Retrieves an autonomous mission plan and task DAG by ID.
   */
  public async get(missionId: string): Promise<AgentMissionPlan> {
    return this.client.request<AgentMissionPlan>(`/api/v1/ai/missions/${encodeURIComponent(missionId)}`, {
      method: 'GET'
    });
  }
}

/**
 * Connectors & Marketplace API client module.
 */
export class ConnectorsClient {
  constructor(private readonly client: SGrowingClient) {}

  /**
   * Transforms external commerce provider payload into canonical SGrowing format.
   */
  public async transformOrder(
    connectorId: string,
    rawPayload: string | object,
    mappings?: Record<string, string>
  ): Promise<TransformPayloadResult> {
    const rawJsonPayload = typeof rawPayload === 'string' ? rawPayload : JSON.stringify(rawPayload);
    const body = {
      rawJsonPayload,
      fieldMappings: mappings ?? {}
    };

    return this.client.request<TransformPayloadResult>(
      `/api/v1/connectors/marketplace/${encodeURIComponent(connectorId)}/transform-order`,
      {
        method: 'POST',
        body: JSON.stringify(body)
      }
    );
  }
}

/**
 * Developer Webhooks API client module.
 */
export class WebhooksClient {
  constructor(private readonly client: SGrowingClient) {}

  /**
   * Dispatches a synthetic test webhook event to all subscribed endpoints for the tenant.
   */
  public async dispatchTest(
    eventType: string,
    payload: unknown
  ): Promise<TestDispatchWebhookResponse> {
    const tenantId = this.client.tenantId;
    if (!tenantId) {
      throw new Error('TenantId is required to dispatch test webhooks.');
    }

    return this.client.request<TestDispatchWebhookResponse>('/api/developer/webhooks/test-dispatch', {
      method: 'POST',
      body: JSON.stringify({
        tenantId,
        eventType,
        payload
      })
    });
  }

  /**
   * Verifies an incoming webhook request signature in constant time.
   */
  public verifySignature(
    rawBody: string | Buffer,
    secret: string,
    signatureHeader: string,
    toleranceOrOptions?: number | WebhookVerificationOptions,
    timestampHeader?: string | number
  ): boolean {
    return SGrowingWebhookVerifier.verify(
      rawBody,
      secret,
      signatureHeader,
      toleranceOrOptions,
      timestampHeader
    );
  }
}

/**
 * SRE Telemetry & Observability API client module.
 */
export class TelemetryClient {
  constructor(private readonly client: SGrowingClient) {}

  /**
   * Retrieves SRE Health Radar report with SLO error budget and system vitals.
   */
  public async getRadar(dlqDepth = 0, activeMissions = 0): Promise<SreHealthRadarReport> {
    const params = new URLSearchParams({
      dlqDepth: dlqDepth.toString(),
      activeMissions: activeMissions.toString()
    });

    return this.client.request<SreHealthRadarReport>(`/api/v1/telemetry/sre-radar?${params.toString()}`, {
      method: 'GET'
    });
  }

  /**
   * Records a synthetic metric into the OpenTelemetry telemetry pipeline.
   */
  public async recordSyntheticMetric(metric: RecordSyntheticMetricRequest): Promise<{ message: string }> {
    return this.client.request<{ message: string }>('/api/v1/telemetry/record-synthetic-metric', {
      method: 'POST',
      body: JSON.stringify(metric)
    });
  }
}

/**
 * Official SGrowing Growth OS SDK Client.
 */
export class SGrowingClient {
  public readonly baseUrl: string;
  public readonly apiKey?: string;
  public readonly tenantId?: string;
  public readonly timeoutMs: number;
  private readonly customFetch: typeof globalThis.fetch;
  private readonly defaultHeaders: Record<string, string>;

  public readonly missions: MissionsClient;
  public readonly connectors: ConnectorsClient;
  public readonly webhooks: WebhooksClient;
  public readonly telemetry: TelemetryClient;

  constructor(options: SGrowingClientOptions = {}) {
    this.baseUrl = (options.baseUrl ?? 'https://api.sgrowing.io').replace(/\/$/, '');
    this.apiKey = options.apiKey;
    this.tenantId = options.tenantId;
    this.timeoutMs = options.timeoutMs ?? 30000;
    this.customFetch = options.fetch ?? globalThis.fetch;
    this.defaultHeaders = options.headers ?? {};

    this.missions = new MissionsClient(this);
    this.connectors = new ConnectorsClient(this);
    this.webhooks = new WebhooksClient(this);
    this.telemetry = new TelemetryClient(this);
  }

  /**
   * Executes an authenticated HTTP request against the SGrowing API.
   */
  public async request<T>(path: string, options: RequestInit = {}): Promise<T> {
    const url = `${this.baseUrl}${path.startsWith('/') ? '' : '/'}${path}`;
    const headers: Record<string, string> = {
      'Content-Type': 'application/json',
      'Accept': 'application/json',
      ...this.defaultHeaders,
      ...(options.headers as Record<string, string> | undefined)
    };

    if (this.apiKey) {
      headers['Authorization'] = `Bearer ${this.apiKey}`;
      headers['X-Api-Key'] = this.apiKey;
    }

    if (this.tenantId) {
      headers['X-Tenant-Id'] = this.tenantId;
    }

    const controller = new AbortController();
    const timer = setTimeout(() => controller.abort(), this.timeoutMs);

    try {
      const response = await this.customFetch(url, {
        ...options,
        headers,
        signal: controller.signal
      });

      const responseText = await response.text();
      let responseData: any = null;

      if (responseText && responseText.length > 0) {
        try {
          responseData = JSON.parse(responseText);
        } catch {
          responseData = responseText;
        }
      }

      if (!response.ok) {
        const errorMsg =
          responseData && typeof responseData === 'object' && responseData.message
            ? responseData.message
            : responseData && typeof responseData === 'object' && responseData.error
              ? responseData.error
              : `HTTP request failed with status ${response.status} ${response.statusText}`;

        throw new SGrowingApiError(errorMsg, response.status, responseData);
      }

      return responseData as T;
    } catch (err: any) {
      if (err.name === 'AbortError') {
        throw new SGrowingApiError(`Request timed out after ${this.timeoutMs}ms`, 408);
      }
      throw err;
    } finally {
      clearTimeout(timer);
    }
  }
}
