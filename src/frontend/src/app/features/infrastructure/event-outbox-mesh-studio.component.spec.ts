// @vitest-environment jsdom
import '@angular/compiler';
import { describe, it, expect, beforeEach } from 'vitest';
import { EventOutboxMeshStudioComponent } from './event-outbox-mesh-studio.component';
import { TranslationService } from '../../core/services/translation.service';

describe('EventOutboxMeshStudioComponent (Domain K1 & PRD 8.2/8.3)', () => {
  let component: EventOutboxMeshStudioComponent;

  beforeEach(() => {
    component = new EventOutboxMeshStudioComponent();
  });

  it('should initialize with default outbox stream, DLQ, and KPI metrics', () => {
    expect(component).toBeTruthy();
    expect(component.stats().totalEnqueued).toBe(4);
    expect(component.stats().totalPublished).toBe(2);
    expect(component.stats().deadLetterDepth).toBe(1);
    expect(component.stats().currentPending).toBe(1);
    expect(component.events().length).toBe(4);
    expect(component.ledger().length).toBe(4);
    expect(component.selectedTab()).toBe('mesh');
  });

  it('should switch navigation tabs correctly', () => {
    component.setTab('dlq');
    expect(component.selectedTab()).toBe('dlq');

    component.setTab('dedup');
    expect(component.selectedTab()).toBe('dedup');

    component.setTab('simulate');
    expect(component.selectedTab()).toBe('simulate');

    component.setTab('mesh');
    expect(component.selectedTab()).toBe('mesh');
  });

  it('should filter events by eventType', () => {
    component.setFilter('OrderPaid');
    expect(component.filterEventType()).toBe('OrderPaid');
    const filtered = component.filteredEvents();
    expect(filtered.length).toBe(1);
    expect(filtered[0].eventType).toBe('OrderPaid');

    component.setFilter('all');
    expect(component.filteredEvents().length).toBe(4);
  });

  it('should filter events by search term matching aggregate ID or payload', () => {
    component.searchTerm.set('DEAL-9904');
    expect(component.filteredEvents().length).toBe(1);
    expect(component.filteredEvents()[0].aggregateId).toBe('DEAL-9904');

    component.searchTerm.set('cyberdyne');
    expect(component.filteredEvents().length).toBe(1);
    expect(component.filteredEvents()[0].eventType).toBe('OrderPaid');

    component.searchTerm.set('');
    expect(component.filteredEvents().length).toBe(4);
  });

  it('should dispatch pending batch and transition pending events to published', () => {
    expect(component.stats().currentPending).toBe(1);
    expect(component.stats().totalPublished).toBe(2);

    component.dispatchPendingBatch();

    expect(component.stats().currentPending).toBe(0);
    expect(component.stats().totalPublished).toBe(3);
    const leadEvt = component.events().find(e => e.id === 'evt-lead-003');
    expect(leadEvt?.status).toBe('Published');
    expect(leadEvt?.processedAtUtc).toBeDefined();
    expect(component.statusToast()?.type).toBe('success');
  });

  it('should simulate OrderPaid domain event intake and update pending count', () => {
    const initialEnqueued = component.stats().totalEnqueued;
    const initialPending = component.stats().currentPending;

    component.simulateEvent('OrderPaid');

    expect(component.stats().totalEnqueued).toBe(initialEnqueued + 1);
    expect(component.stats().currentPending).toBe(initialPending + 1);
    const firstEvt = component.events()[0];
    expect(firstEvt.eventType).toBe('OrderPaid');
    expect(firstEvt.status).toBe('Pending');
    expect(firstEvt.deduplicationHash.length).toBe(64);
  });

  it('should simulate DealWon, LeadQualified, and TicketEscalated domain events', () => {
    component.simulateEvent('DealWon');
    expect(component.events()[0].eventType).toBe('DealWon');

    component.simulateEvent('LeadQualified');
    expect(component.events()[0].eventType).toBe('LeadQualified');

    component.simulateEvent('TicketEscalated');
    expect(component.events()[0].eventType).toBe('TicketEscalated');
  });

  it('should detect duplicate hash collision and suppress double write', () => {
    const initialHits = component.stats().totalDeduplicationHits;
    const initialCount = component.events().length;

    component.simulateDuplicateCollision();

    expect(component.stats().totalDeduplicationHits).toBe(initialHits + 1);
    expect(component.events().length).toBe(initialCount); // no duplicate added
    expect(component.statusToast()?.type).toBe('info');
    expect(component.statusToast()?.message).toContain('Duplicate event detected');
  });

  it('should simulate poison event and isolate it in DLQ quarantine after retry exhaustion', () => {
    const initialDlq = component.stats().deadLetterDepth;

    component.simulatePoisonEvent();

    expect(component.stats().deadLetterDepth).toBe(initialDlq + 1);
    const poison = component.events()[0];
    expect(poison.status).toBe('QuarantinedDeadLetter');
    expect(poison.retryCount).toBe(5);
    expect(poison.deadLetterReason).toContain('Exceeded max retry threshold');
    expect(component.dlqItems().some(i => i.id === poison.id)).toBe(true);
    expect(component.statusToast()?.type).toBe('error');
  });

  it('should toggle, select all, and deselect all DLQ items', () => {
    const dlqId = 'evt-tck-004';
    expect(component.isDlqSelected(dlqId)).toBe(false);

    component.toggleSelectDlq(dlqId);
    expect(component.isDlqSelected(dlqId)).toBe(true);
    expect(component.hasSelectedDlq()).toBe(true);

    component.toggleSelectDlq(dlqId);
    expect(component.isDlqSelected(dlqId)).toBe(false);

    component.selectAllDlq();
    expect(component.selectedDlqIds().length).toBe(component.dlqCount());

    component.deselectAllDlq();
    expect(component.selectedDlqIds().length).toBe(0);
    expect(component.hasSelectedDlq()).toBe(false);
  });

  it('should replay selected DLQ items back to active pending stream and reset retries', () => {
    component.selectAllDlq();
    expect(component.selectedDlqIds().length).toBe(1);

    component.replaySelectedDlq();

    expect(component.stats().deadLetterDepth).toBe(0);
    expect(component.stats().currentPending).toBe(2);
    const replayed = component.events().find(e => e.id === 'evt-tck-004');
    expect(replayed?.status).toBe('Pending');
    expect(replayed?.retryCount).toBe(0);
    expect(replayed?.lastError).toBeUndefined();
    expect(component.selectedDlqIds().length).toBe(0);
    expect(component.statusToast()?.type).toBe('success');
  });

  it('should replay single DLQ item', () => {
    component.replaySingleDlq('evt-tck-004');
    const evt = component.events().find(e => e.id === 'evt-tck-004');
    expect(evt?.status).toBe('Pending');
    expect(evt?.retryCount).toBe(0);
  });

  it('should purge DLQ item permanently from outbox stream and deduplication ledger', () => {
    expect(component.events().some(e => e.id === 'evt-tck-004')).toBe(true);
    expect(component.ledger().some(l => l.eventId === 'evt-tck-004')).toBe(true);

    component.purgeDlq('evt-tck-004');

    expect(component.events().some(e => e.id === 'evt-tck-004')).toBe(false);
    expect(component.ledger().some(l => l.eventId === 'evt-tck-004')).toBe(false);
    expect(component.stats().deadLetterDepth).toBe(0);
    expect(component.statusToast()?.type).toBe('info');
  });

  it('should open and close inspect modal', () => {
    const sample = component.events()[0];
    component.inspectEvent(sample);
    expect(component.selectedDetailEvent()).toEqual(sample);

    component.closeInspectModal();
    expect(component.selectedDetailEvent()).toBeNull();
  });

  it('should reset mesh demo state', () => {
    component.simulateEvent('OrderPaid');
    component.simulateEvent('DealWon');
    expect(component.stats().totalEnqueued).toBe(6);

    component.resetMeshDemo();
    expect(component.stats().totalEnqueued).toBe(4);
    expect(component.stats().deadLetterDepth).toBe(1);
    expect(component.statusToast()?.message).toContain('reset to initial demo');
  });

  it('should compute isRtl as false when translationService is undefined or non-Arabic', () => {
    expect(component.isRtl()).toBe(false);
  });

  it('should compute isRtl as true when Arabic locale is set', () => {
    const mockTrans = new TranslationService();
    mockTrans.currentLocale.set('ar-AR');
    const compWithRtl = new EventOutboxMeshStudioComponent(mockTrans);
    expect(compWithRtl.isRtl()).toBe(true);
  });
});
