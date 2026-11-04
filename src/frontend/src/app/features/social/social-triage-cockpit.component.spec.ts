// @vitest-environment jsdom
import '@angular/compiler';
import { describe, it, expect, beforeEach } from 'vitest';
import { SocialTriageCockpitComponent } from './social-triage-cockpit.component';

describe('SocialTriageCockpitComponent', () => {
  let component: SocialTriageCockpitComponent;

  beforeEach(() => {
    component = new SocialTriageCockpitComponent();
  });

  it('should create component and render initial social triage queue', () => {
    expect(component).toBeTruthy();
    expect(component.queueItems().length).toBeGreaterThanOrEqual(2);
    expect(component.hazardsCount()).toBeGreaterThanOrEqual(1);
  });

  it('should simulate inbound escalation hazard and prepend to queue', () => {
    const initialCount = component.queueItems().length;
    component.simulateInboundHazard();

    expect(component.queueItems().length).toBe(initialCount + 1);
    expect(component.selectedItem()?.assessment.sentiment).toBe('SevereHazard');
    expect(component.selectedItem()?.assignedAgentRole).toBe('ExecutiveEscalationLead');
  });

  it('should approve and dispatch draft reply to channel', () => {
    const item = component.queueItems()[0];
    expect(item.status).toBe('PendingReview');

    component.approveAndSendReply(item);
    expect(component.queueItems().find(i => i.queueId === item.queueId)?.status).toBe('ApprovedAndSent');
  });

  it('should return correct badge classes for channels and sentiments and return 0 hazards when queue is empty', () => {
    expect(component.getChannelBadgeClass('WhatsApp')).toContain('emerald');
    expect(component.getChannelBadgeClass('LinkedIn')).toContain('blue');
    expect(component.getChannelBadgeClass('Instagram')).toContain('rose');
    expect(component.getSentimentBadgeClass('SevereHazard')).toContain('rose');
    expect(component.getSentimentBadgeClass('Positive')).toContain('emerald');

    component.queueItems.set([]);
    expect(component.hazardsCount()).toBe(0);
  });
});
