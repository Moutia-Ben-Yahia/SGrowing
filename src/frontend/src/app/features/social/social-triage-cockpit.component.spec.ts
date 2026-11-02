import { ComponentFixture, TestBed } from '@angular/core/testing';
import { describe, it, expect, beforeEach } from 'vitest';
import { SocialTriageCockpitComponent } from './social-triage-cockpit.component';

describe('SocialTriageCockpitComponent', () => {
  let component: SocialTriageCockpitComponent;
  let fixture: ComponentFixture<SocialTriageCockpitComponent>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [SocialTriageCockpitComponent]
    }).compileComponents();

    fixture = TestBed.createComponent(SocialTriageCockpitComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
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
});
