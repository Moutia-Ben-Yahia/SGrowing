// @vitest-environment jsdom
import '@angular/compiler';
import { describe, it, expect, beforeEach, vi } from 'vitest';
import { Injector, runInInjectionContext } from '@angular/core';
import { of, throwError } from 'rxjs';
import { JourneyCanvasStudioComponent } from './journey-canvas-studio.component';
import { ApiService } from '../../core/services/api.service';
import { JourneyNodeType } from '../../core/models/platform.models';

describe('JourneyCanvasStudioComponent', () => {
  let component: JourneyCanvasStudioComponent;
  let mockApi: any;

  beforeEach(() => {
    mockApi = {
      getJourneyDags: vi.fn().mockReturnValue(of([])),
      getJourneyDagMetrics: vi.fn().mockReturnValue(of({
        totalInstances: 142,
        activeInstances: 38,
        completedInstances: 94,
        droppedInstances: 10,
        nodeMetrics: []
      })),
      saveJourneyDag: vi.fn().mockReturnValue(of({})),
      sweepJourneyTimeouts: vi.fn().mockReturnValue(of({ advancedCount: 5, unenrolledCount: 1 })),
      enrollContactInDag: vi.fn().mockReturnValue(of({ instanceId: 'inst-1' })),
      ingestJourneyDagEvent: vi.fn().mockReturnValue(of({ advancedCount: 2, unenrolledCount: 0, message: 'Event ingested' }))
    };

    const injector = Injector.create({
      providers: [
        { provide: ApiService, useValue: mockApi }
      ]
    });

    component = runInInjectionContext(injector, () => new JourneyCanvasStudioComponent());
    component.ngOnInit();
  });

  it('should initialize and load sample DAG journey when API returns empty', () => {
    expect(component).toBeTruthy();
    expect(mockApi.getJourneyDags).toHaveBeenCalled();
    const journeys = component.journeys();
    expect(journeys.length).toBeGreaterThan(0);
    expect(component.selectedJourney()).toBeDefined();
    expect(component.selectedJourney()?.nodes.length).toBeGreaterThanOrEqual(4);
  });

  it('should validate acyclic DAG structure correctly with Kahn algorithm', () => {
    const validation = component.dagValidation();
    expect(validation.isValid).toBe(true);

    // Introduce an artificial cycle to verify Kahn DFS failure detection
    const current = component.selectedJourney()!;
    const cloned = JSON.parse(JSON.stringify(current));
    // Point the exit node back to trigger
    const exitNode = cloned.nodes.find((n: any) => n.type === JourneyNodeType.Exit);
    if (exitNode) {
      exitNode.nextNodeId = cloned.nodes[0].id;
    }
    const invalidRes = component['validateDagLocally'](cloned.nodes);
    expect(invalidRes.isValid).toBe(false);
    expect(invalidRes.errorMessage).toContain('Cycle detected');
  });

  it('should add new nodes to the active journey DAG', () => {
    const journey = component.selectedJourney()!;
    const nodeCountBefore = journey.nodes.length;

    component.addNode(JourneyNodeType.ConditionBranch);

    expect(journey.nodes.length).toBe(nodeCountBefore + 1);
    const added = component.selectedNode();
    expect(added?.type).toBe(JourneyNodeType.ConditionBranch);
    expect(added?.name).toContain('Condition Check');
  });

  it('should simulate event ingestion and record execution logs', () => {
    component.simulateEvent('checkout_completed');

    expect(mockApi.ingestJourneyDagEvent).toHaveBeenCalledWith(
      component.simContactId,
      'checkout_completed',
      expect.any(Object)
    );
    expect(component.isSimulating()).toBe(false);
    expect(component.simulationLogs().length).toBeGreaterThan(0);
    expect(component.simulationLogs()[0].nodeName).toContain('checkout_completed');
  });

  it('should format TTL and map node icons appropriately', () => {
    expect(component.formatTtl(86400)).toBe('1d (86400s)');
    expect(component.formatTtl(3600)).toBe('1h (3600s)');
    expect(component.formatTtl(60)).toBe('1m (60s)');
    expect(component.formatTtl(0)).toBe('0s');

    expect(component.getNodeIcon(JourneyNodeType.Trigger)).toBe('⚡');
    expect(component.getNodeIcon(JourneyNodeType.WaitUntilEvent)).toBe('⏳');
    expect(component.getNodeIcon(JourneyNodeType.Exit)).toBe('🏁');
  });
});
