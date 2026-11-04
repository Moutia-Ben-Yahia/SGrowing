// @vitest-environment jsdom
import '@angular/compiler';
import { describe, it, expect, beforeEach } from 'vitest';
import { WebhookMeshCockpitComponent } from './webhook-mesh-cockpit.component';

describe('WebhookMeshCockpitComponent', () => {
  let component: WebhookMeshCockpitComponent;

  beforeEach(() => {
    component = new WebhookMeshCockpitComponent();
  });

  it('should initialize with endpoints, delivery logs, and computed DLQ count', () => {
    expect(component).toBeTruthy();
    expect(component.endpoints().length).toBe(3);
    expect(component.deliveryLogs().length).toBe(3);
    expect(component.dlqCount()).toBe(1);
  });

  it('should emit synthetic test event and prepend to delivery log', () => {
    const initialCount = component.deliveryLogs().length;
    component.emitTestEvent();

    expect(component.deliveryLogs().length).toBe(initialCount + 1);
    expect(component.deliveryLogs()[0].topic).toBe('lead.qualified');
    expect(component.deliveryLogs()[0].status).toBe('Delivered');
  });

  it('should replay failed dead-letter event and mark as Delivered', () => {
    expect(component.dlqCount()).toBe(1);

    component.replayEvent('del_103');

    const replayed = component.deliveryLogs().find(l => l.id === 'del_103');
    expect(replayed?.status).toBe('Delivered');
    expect(replayed?.httpStatus).toBe(200);
    expect(component.dlqCount()).toBe(0);
  });

  it('should ignore replayEvent when delivery log id is not found', () => {
    const initialDlq = component.dlqCount();
    component.replayEvent('non_existent_log');

    expect(component.dlqCount()).toBe(initialDlq);
  });
});
