import { Injectable, signal } from '@angular/core';

export interface IdentityStitchedEvent {
  tenantId: string;
  canonicalProfileId: string;
  wasMerged: boolean;
  auditMessage: string;
  timestamp: string;
}

export interface CapiConversionEvent {
  tenantId: string;
  platform: string;
  eventId: string;
  value: number;
  isDeduplicated: boolean;
  timestamp: string;
}

export interface WorkflowTelemetryStepEvent {
  tenantId: string;
  workflowId: string;
  nodeId: string;
  nodeName: string;
  status: string;
  latencyMs: number;
  timestamp: string;
}

export interface RoasOptimizedEvent {
  tenantId: string;
  blendedRoas: number;
  scaledCampaignsCount: number;
  timestamp: string;
}

@Injectable({
  providedIn: 'root'
})
export class GrowthTelemetrySignalrService {
  public isConnected = signal<boolean>(true);
  public activeTenantId = signal<string>('00000000-0000-0000-0000-000000000001');

  public lastIdentityStitched = signal<IdentityStitchedEvent | null>(null);
  public lastCapiConversion = signal<CapiConversionEvent | null>(null);
  public lastWorkflowStep = signal<WorkflowTelemetryStepEvent | null>(null);
  public lastRoasOptimized = signal<RoasOptimizedEvent | null>(null);

  public emitIdentityStitched(canonicalProfileId: string, wasMerged: boolean, auditMessage: string): void {
    const event: IdentityStitchedEvent = {
      tenantId: this.activeTenantId(),
      canonicalProfileId,
      wasMerged,
      auditMessage,
      timestamp: new Date().toISOString()
    };
    this.lastIdentityStitched.set(event);
  }

  public emitCapiConversion(platform: string, eventId: string, value: number, isDeduplicated: boolean): void {
    const event: CapiConversionEvent = {
      tenantId: this.activeTenantId(),
      platform,
      eventId,
      value,
      isDeduplicated,
      timestamp: new Date().toISOString()
    };
    this.lastCapiConversion.set(event);
  }

  public emitWorkflowStep(workflowId: string, nodeId: string, nodeName: string, status: string, latencyMs: number): void {
    const event: WorkflowTelemetryStepEvent = {
      tenantId: this.activeTenantId(),
      workflowId,
      nodeId,
      nodeName,
      status,
      latencyMs,
      timestamp: new Date().toISOString()
    };
    this.lastWorkflowStep.set(event);
  }

  public emitRoasOptimized(blendedRoas: number, scaledCampaignsCount: number): void {
    const event: RoasOptimizedEvent = {
      tenantId: this.activeTenantId(),
      blendedRoas,
      scaledCampaignsCount,
      timestamp: new Date().toISOString()
    };
    this.lastRoasOptimized.set(event);
  }
}
