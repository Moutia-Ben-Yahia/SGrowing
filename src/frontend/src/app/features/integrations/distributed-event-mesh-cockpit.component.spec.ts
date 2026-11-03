// @vitest-environment jsdom
import '@angular/compiler';
import { describe, it, expect, beforeEach, vi } from 'vitest';
import { Injector, runInInjectionContext } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { of, throwError } from 'rxjs';
import {
  DistributedEventMeshCockpitComponent,
  EventBrokerMode
} from './distributed-event-mesh-cockpit.component';

describe('DistributedEventMeshCockpitComponent (Distributed Event Mesh & Real-Time Streaming)', () => {
  let component: DistributedEventMeshCockpitComponent;
  let mockHttp: any;

  beforeEach(() => {
    mockHttp = {
      get: vi.fn().mockReturnValue(of({
        totalPublished: 2500000,
        totalConsumed: 2498200,
        totalDeduplicated: 1800,
        dlqSize: 2,
        partitionCount: 16,
        partitionLags: [],
        activeBrokerMode: 'InMemoryRingBuffer',
        throughputPerSecond: 1850.5,
        lastEventPublishedAtUtc: new Date().toISOString(),
        isHealthy: true,
        brokerMetadata: {}
      })),
      post: vi.fn().mockReturnValue(of({ success: true }))
    };

    const injector = Injector.create({
      providers: [
        { provide: HttpClient, useValue: mockHttp }
      ]
    });

    component = runInInjectionContext(injector, () => new DistributedEventMeshCockpitComponent());
    component.ngOnInit();
  });

  it('should initialize with broker mode, metrics, and DLQ items', () => {
    expect(component).toBeTruthy();
    expect(component.activeBroker()).toBe('InMemoryRingBuffer');
    expect(component.metrics().partitionCount).toBe(16);
    expect(component.dlqItems().length).toBe(2);
  });

  it('should switch broker mode between InMemoryRingBuffer, RedisStreams, and NatsJetStream', () => {
    component.setBrokerMode('RedisStreams');
    expect(component.activeBroker()).toBe('RedisStreams');
    expect(component.metrics().activeBrokerMode).toBe('RedisStreams');

    component.setBrokerMode('NatsJetStream');
    expect(component.activeBroker()).toBe('NatsJetStream');
  });

  it('should load presets correctly for order, payment, inventory, and fleet', () => {
    component.loadPreset('payment');
    expect(component.formTopic).toBe('payments.stripe.captured');
    expect(component.formEventType).toBe('PaymentCaptured');
    expect(component.formPayload).toContain('ch_3M00941');

    component.loadPreset('inventory');
    expect(component.formTopic).toBe('inventory.stock.allocated');
    expect(component.formEventType).toBe('StockAllocated');

    component.loadPreset('fleet');
    expect(component.formTopic).toBe('fleet.telemetry.gps');
  });

  it('should filter DLQ items by search text and status', () => {
    component.dlqSearchText = 'crm';
    component.dlqItems.update(items => [...items]);
    expect(component.filteredDlqItems().length).toBe(1);
    expect(component.filteredDlqItems()[0].topic).toBe('crm.contact.sync');

    component.dlqSearchText = '';
    component.dlqStatusFilter = 'Quarantined';
    component.dlqItems.update(items => [...items]);
    expect(component.filteredDlqItems().length).toBe(2);

    component.dlqStatusFilter = 'Replayed';
    component.dlqItems.update(items => [...items]);
    expect(component.filteredDlqItems().length).toBe(0);
  });

  it('should publish single event and calculate predicted partition and fingerprint', () => {
    const partition = component.predictedPartition();
    expect(partition).toBeGreaterThanOrEqual(0);
    expect(partition).toBeLessThan(16);

    const fingerprint = component.previewFingerprint();
    expect(fingerprint).toBeTruthy();
  });
});
