// @vitest-environment jsdom
import '@angular/compiler';
import { describe, it, expect, beforeEach, vi } from 'vitest';
import { Injector, runInInjectionContext } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { of } from 'rxjs';
import { ComplianceAuditLedgerComponent } from './compliance-audit-ledger.component';

describe('ComplianceAuditLedgerComponent (Immutable Merkle Audit Ledger)', () => {
  let component: ComplianceAuditLedgerComponent;
  let mockHttp: any;

  beforeEach(() => {
    mockHttp = {
      get: vi.fn().mockImplementation((url: string) => {
        if (url.includes('/proof')) {
          return of({
            eventId: '2a11b8ef-1011-4541-bc7a-554433221101',
            leafHash: '0c7625a698be6f6153b6fa0c20ab0e326b2191c7849e8a0f8bf3c299c8bfbe11',
            leafIndex: 0,
            totalLeaves: 4,
            merkleRoot: 'd484e5684742a0bdf51e9bf4349479b37c0fc4410113c19ceb649d21c3fa32b0',
            epochId: '10000000-0000-0000-0000-000000000029',
            epochNumber: 29,
            isSealed: true,
            proofPath: []
          });
        }
        if (url.includes('/events')) {
          return of({ events: [], totalCount: 0 });
        }
        if (url.includes('/epochs')) {
          return of([]);
        }
        return of({});
      }),
      post: vi.fn().mockImplementation((url: string, body: any) => {
        if (url.includes('/simulate-tamper')) {
          return of({
            eventId: body?.eventId || 'test-ev',
            originalPayloadHash: 'orig-payload-hash',
            tamperedPayloadHash: 'tampered-payload-hash',
            originalLeafHash: 'orig-leaf-hash',
            tamperedLeafHash: 'tampered-leaf-hash',
            originalMerkleRoot: 'orig-root',
            tamperedMerkleRoot: 'tampered-root',
            rootMismatchDetected: true,
            proofVerificationFailed: true,
            integrityBroken: true,
            tamperAuditVerdict: 'TAMPER_DETECTED',
            originalProofPath: []
          });
        }
        if (url.includes('/seal-epoch')) {
          return of({
            id: 'epoch-new-1',
            tenantId: 'tenant-test',
            epochNumber: 30,
            epochLabel: 'Hourly Epoch #30',
            startTimeUtc: new Date().toISOString(),
            endTimeUtc: new Date().toISOString(),
            sealedAtUtc: new Date().toISOString(),
            sealedBy: body?.sealedBy || 'Security Operator',
            eventCount: 4,
            merkleRoot: 'd484e5684742a0bdf51e9bf4349479b37c0fc4410113c19ceb649d21c3fa32b0',
            epochSignature: 'sig-test-30',
            previousEpochChainedHash: 'chain-29',
            chainedHash: 'chain-30',
            eventIds: [],
            isSealed: true
          });
        }
        if (url.includes('/verify-proof')) {
          return of({ isValid: true });
        }
        return of({});
      })
    };

    const injector = Injector.create({
      providers: [
        { provide: HttpClient, useValue: mockHttp }
      ]
    });

    component = runInInjectionContext(injector, () => new ComplianceAuditLedgerComponent());
    component.ngOnInit();
  });

  it('should initialize with seed events, epochs, and computed counts', () => {
    expect(component).toBeTruthy();
    expect(component.events().length).toBeGreaterThan(0);
    expect(component.epochs().length).toBeGreaterThan(0);
    expect(component.sealedEventsCount()).toBeGreaterThan(0);
    expect(component.unsealedEventsCount()).toBeGreaterThan(0);
    expect(component.lastEpochLabel()).toBeTruthy();
  });

  it('should filter events by searchFilter signal', () => {
    const initialCount = component.filteredEvents().length;
    component.searchFilter.set('RoleEscalation');
    expect(component.filteredEvents().length).toBeLessThanOrEqual(initialCount);
    expect(component.filteredEvents().every(e => 
      e.action.includes('RoleEscalation') || e.actorEmail.includes('RoleEscalation') || e.resource.includes('RoleEscalation')
    )).toBe(true);
  });

  it('should inspect proof and update selectedProof signal', () => {
    const event = component.events()[0];
    component.inspectProof(event);

    const proof = component.selectedProof();
    expect(proof).not.toBeNull();
    expect(proof?.eventId).toBeTruthy();
    expect(proof?.leafHash).toBeTruthy();
  });

  it('should toggle modals and run tamper simulation detecting integrity breach', () => {
    component.openTamperModal();
    expect(component.showTamperModal()).toBe(true);

    const targetEvent = component.events()[0];
    component.tamperTargetEventId.set(targetEvent.id);
    component.runTamperSimulation();

    const result = component.tamperResult();
    expect(result).not.toBeNull();
    expect(result?.integrityBroken).toBe(true);
    expect(result?.rootMismatchDetected).toBe(true);

    component.showTamperModal.set(false);
    expect(component.showTamperModal()).toBe(false);
  });

  it('should seal active epoch and update unsealed events', () => {
    component.sealEpoch();

    expect(mockHttp.post).toHaveBeenCalled();
    expect(component.isSealing()).toBe(false);
    expect(component.epochs().length).toBeGreaterThan(0);
  });

  it('should format action badges correctly', () => {
    expect(component.getActionBadgeClass('UserLogin')).toContain('text-cyan-400');
    expect(component.getActionBadgeClass('RoleEscalation')).toContain('text-purple-400');
    expect(component.getActionBadgeClass('ApiKeyGenerated')).toContain('text-amber-400');
    expect(component.getActionBadgeClass('UnknownAction')).toContain('text-slate-300');
  });
});
