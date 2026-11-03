// @vitest-environment jsdom
import '@angular/compiler';
import { describe, it, expect, beforeEach, vi } from 'vitest';
import { Injector, runInInjectionContext } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { of } from 'rxjs';
import { HybridRagAutopilotStudioComponent, RagQueryResult, KnowledgeDocument } from './hybrid-rag-autopilot-studio.component';

describe('HybridRagAutopilotStudioComponent', () => {
  let component: HybridRagAutopilotStudioComponent;
  let mockHttp: any;

  const mockQueryResult: RagQueryResult = {
    answer: 'For damaged items, instant drop and swap is available within 48 hours.',
    confidenceScore: 0.94,
    isAutopilotEligible: true,
    citations: [
      {
        documentId: 'doc-3',
        documentTitle: 'Damaged Item Drop-and-Swap Replacement',
        chunkIndex: 0,
        excerptSnippet: 'For damaged, defective items received...',
        relevanceScore: 32.8
      }
    ],
    denseScore: 0.78,
    sparseScore: 5.2,
    rrfScore: 0.0328,
    latencyMs: 18.5
  };

  const mockNewDoc: KnowledgeDocument = {
    id: 'doc-101',
    tenantId: 'tenant-123',
    title: 'VIP Courier White-Glove Protocol',
    category: 'ShippingPolicies',
    content: 'All VIP parcels require security pin confirmation before handover.',
    tags: ['custom', 'shippingpolicies'],
    updatedAt: '2026-11-03T10:00:00Z',
    chunkCount: 2
  };

  beforeEach(() => {
    mockHttp = {
      get: vi.fn().mockImplementation((url: string) => {
        if (url.includes('/documents')) return of([mockNewDoc]);
        return of({});
      }),
      post: vi.fn().mockImplementation((url: string) => {
        if (url.includes('/query')) return of(mockQueryResult);
        if (url.includes('/documents')) return of(mockNewDoc);
        return of({});
      })
    };

    const injector = Injector.create({
      providers: [
        { provide: HttpClient, useValue: mockHttp }
      ]
    });

    component = runInInjectionContext(injector, () => new HybridRagAutopilotStudioComponent());
    component.ngOnInit();
  });

  it('should initialize with seed knowledge documents, tickets, and execute initial RAG search', () => {
    expect(component).toBeTruthy();
    expect(mockHttp.post).toHaveBeenCalledWith(
      '/api/v1/knowledge/rag-autopilot/query',
      expect.objectContaining({ customerQuestion: expect.any(String) })
    );
    expect(component.currentResult()?.confidenceScore).toBe(0.94);
    expect(component.documents().length).toBeGreaterThanOrEqual(1);
  });

  it('should update confidence threshold and recalculate autopilot eligibility', () => {
    component.onThresholdChange(0.96);
    expect(component.threshold()).toBe(0.96);
    expect(component.currentResult()?.isAutopilotEligible).toBe(false);

    component.onThresholdChange(0.90);
    expect(component.currentResult()?.isAutopilotEligible).toBe(true);
  });

  it('should switch example queries and execute hybrid search', () => {
    component.setQuery('What is your return policy?');
    expect(component.questionInput()).toBe('What is your return policy?');
    expect(mockHttp.post).toHaveBeenCalledWith(
      '/api/v1/knowledge/rag-autopilot/query',
      expect.objectContaining({ customerQuestion: 'What is your return policy?' })
    );
  });

  it('should ingest new knowledge document and append to document index', () => {
    component.newDocTitle.set('VIP Courier White-Glove Protocol');
    component.newDocCategory.set('ShippingPolicies');
    component.newDocContent.set('All VIP parcels require security pin confirmation before handover.');
    component.submitIngestDocument();

    expect(mockHttp.post).toHaveBeenCalledWith(
      '/api/v1/knowledge/rag-autopilot/documents',
      expect.objectContaining({ title: 'VIP Courier White-Glove Protocol' })
    );
    expect(component.documents().some(d => d.title === 'VIP Courier White-Glove Protocol')).toBe(true);
    expect(component.showIngestModal()).toBe(false);
  });

  it('should approve ticket resolution and mark as AutoResolved', () => {
    const ticket = component.tickets()[0];
    if (ticket) {
      component.approveResolution(ticket);
      const updated = component.tickets().find(t => t.ticketId === ticket.ticketId);
      expect(updated?.actionTaken).toBe('AutoResolved');
    }
  });
});
