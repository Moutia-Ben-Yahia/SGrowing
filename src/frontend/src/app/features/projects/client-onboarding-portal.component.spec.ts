// @vitest-environment jsdom
import '@angular/compiler';
import { describe, it, expect, beforeEach, vi } from 'vitest';
import { Injector, runInInjectionContext } from '@angular/core';
import { ActivatedRoute } from '@angular/router';
import { ClientOnboardingPortalComponent, ClientPortalMilestone } from './client-onboarding-portal.component';

describe('ClientOnboardingPortalComponent', () => {
  let component: ClientOnboardingPortalComponent;
  let mockRoute: any;

  beforeEach(() => {
    mockRoute = {
      snapshot: {
        paramMap: {
          get: vi.fn((key: string) => key === 'token' ? 'spt_live_demo123456789' : null)
        }
      }
    };

    const injector = Injector.create({
      providers: [
        { provide: ActivatedRoute, useValue: mockRoute }
      ]
    });

    component = runInInjectionContext(injector, () => new ClientOnboardingPortalComponent());
    component.ngOnInit();
  });

  it('should initialize and extract portal token from route parameters', () => {
    expect(component).toBeTruthy();
    expect(component.token()).toBe('spt_live_demo123456789');
    expect(component.projectName()).toContain('Enterprise ERP Data Migration');
    expect(component.milestones().length).toBeGreaterThanOrEqual(5);
    expect(component.overallProgress()).toBeGreaterThanOrEqual(0);
  });

  it('should allow selecting milestone for stakeholder review', () => {
    const target = component.milestones()[3]; // Phase 4
    component.selectMilestoneForReview(target);
    expect(component.selectedMilestoneId).toBe(target.id);
  });

  it('should record milestone stakeholder approval and update completion progress', async () => {
    vi.useFakeTimers();

    const targetMilestone = component.milestones().find(m => m.id === 'm-3')!;
    component.selectedMilestoneId = 'm-3';
    component.clientFeedbackName = 'Kamel Riahi';
    component.clientFeedbackEmail = 'kamel@hyper-logistics.tn';
    component.clientStatusVote = 'Approved';
    component.clientFeedbackNotes = 'Approved following live SAP simulation.';

    component.submitFeedback();
    expect(component.isSubmittingFeedback()).toBe(true);

    vi.advanceTimersByTime(350);

    expect(component.isSubmittingFeedback()).toBe(false);
    expect(component.feedbackSuccessMessage()).toContain('Phase 3');
    expect(component.feedbackLog()[0].statusVote).toBe('Approved');

    const updated = component.milestones().find(m => m.id === 'm-3');
    expect(updated?.status).toBe('Done');
    expect(updated?.progressPercent).toBe(100);
    expect(component.overallProgress()).toBeGreaterThanOrEqual(60);

    vi.useRealTimers();
  });

  it('should handle change requested vote and keep milestone in progress', async () => {
    vi.useFakeTimers();

    component.selectedMilestoneId = 'm-4';
    component.clientFeedbackName = 'Alexandre Meyer';
    component.clientFeedbackEmail = 'alex@hyper-logistics.tn';
    component.clientStatusVote = 'ChangeRequested';
    component.clientFeedbackNotes = 'Please adjust timeout TTL on WhatsApp confirmation message.';

    component.submitFeedback();
    vi.advanceTimersByTime(350);

    const updated = component.milestones().find(m => m.id === 'm-4');
    expect(updated?.status).toBe('InProgress');
    expect(updated?.latestClientVote).toBe('ChangeRequested');

    vi.useRealTimers();
  });

  it('should not submit feedback if name or email are empty', () => {
    component.clientFeedbackName = '';
    component.clientFeedbackEmail = '';
    const logBefore = component.feedbackLog().length;

    component.submitFeedback();

    expect(component.feedbackLog().length).toBe(logBefore);
    expect(component.isSubmittingFeedback()).toBe(false);
  });
});
