// @vitest-environment jsdom
import '@angular/compiler';
import { describe, it, expect, beforeEach, vi } from 'vitest';
import { Injector, runInInjectionContext } from '@angular/core';
import { of } from 'rxjs';
import { ApiService } from '../../core/services/api.service';
import { InAppTourStudioComponent } from './in-app-tour-studio.component';

describe('InAppTourStudioComponent', () => {
  let component: InAppTourStudioComponent;
  let mockApi: any;

  const mockTours = [
    {
      id: 'tour-1',
      tenantId: 'tenant-1',
      title: 'Merchant Onboarding Tour',
      slug: 'merchant-onboarding',
      description: 'Quick product walkthrough',
      targetRouteRegex: '/dashboard',
      targetRole: 'Admin',
      isActive: true,
      priority: 10,
      totalStarts: 120,
      totalCompletions: 95,
      steps: [
        {
          id: 'step-1',
          tourId: 'tour-1',
          stepOrder: 1,
          title: 'Welcome to SGrowing',
          content: 'Here is your unified command center.',
          targetCssSelector: '#overview-card',
          placement: 'bottom',
          backdropDismissEnabled: false
        }
      ]
    }
  ];

  const mockAnnouncements = [
    {
      id: 'ann-1',
      tenantId: 'tenant-1',
      title: 'WhatsApp Automation Live',
      bodyText: 'Now you can pair WhatsApp phones via 8-digit OTP code.',
      modalType: 'CenteredModal',
      ctaText: 'Explore Gateway',
      ctaUrl: '/channels/whatsapp-pairing',
      targetRole: 'All',
      targetRouteRegex: '.*',
      minSessionCount: 1,
      priority: 100,
      isActive: true,
      impressionsCount: 450,
      dismissalsCount: 40,
      ctaClicksCount: 180
    }
  ];

  const mockMetrics = {
    activeToursCount: 2,
    activeAnnouncementsCount: 1,
    totalTourStarts: 240,
    totalTourCompletions: 198,
    overallTourCompletionRate: 82.5,
    tourFunnels: [
      {
        tourId: 'tour-1',
        tourTitle: 'Merchant Onboarding Tour',
        totalStarted: 120,
        totalCompleted: 95,
        completionRatePercent: 79.2,
        stepDropoffs: [
          {
            stepOrder: 1,
            stepTitle: 'Welcome to SGrowing',
            usersReached: 120,
            retentionRatePercent: 100,
            dropoffRatePercent: 0
          }
        ]
      }
    ],
    announcementMetrics: [
      {
        announcementId: 'ann-1',
        title: 'WhatsApp Automation Live',
        modalType: 'CenteredModal',
        impressions: 450,
        dismissals: 40,
        ctaClicks: 180,
        ctrPercent: 40.0
      }
    ]
  };

  beforeEach(() => {
    mockApi = {
      getOnboardingTours: vi.fn().mockReturnValue(of(mockTours)),
      getOnboardingAnnouncements: vi.fn().mockReturnValue(of(mockAnnouncements)),
      getOnboardingMetrics: vi.fn().mockReturnValue(of(mockMetrics)),
      createOnboardingTour: vi.fn().mockReturnValue(of(mockTours[0])),
      createOnboardingAnnouncement: vi.fn().mockReturnValue(of(mockAnnouncements[0]))
    };

    const injector = Injector.create({
      providers: [
        { provide: ApiService, useValue: mockApi }
      ]
    });

    component = runInInjectionContext(injector, () => new InAppTourStudioComponent());
    component.ngOnInit();
  });

  it('should initialize and load active onboarding tours, announcements, and funnel metrics', () => {
    expect(component).toBeTruthy();
    expect(mockApi.getOnboardingTours).toHaveBeenCalled();
    expect(mockApi.getOnboardingAnnouncements).toHaveBeenCalled();
    expect(mockApi.getOnboardingMetrics).toHaveBeenCalled();
    expect(component.tours().length).toBe(1);
    expect(component.selectedTour()?.id).toBe('tour-1');
    expect(component.announcements().length).toBe(1);
    expect(component.metrics()?.overallTourCompletionRate).toBe(82.5);
  });

  it('should switch studio navigation tabs between builder, preview, announcements, and analytics', () => {
    component.activeTab.set('preview');
    expect(component.activeTab()).toBe('preview');

    component.activeTab.set('announcements');
    expect(component.activeTab()).toBe('announcements');

    component.activeTab.set('analytics');
    expect(component.activeTab()).toBe('analytics');
  });

  it('should compute current preview spotlight step correctly', () => {
    expect(component.currentPreviewSteps().length).toBe(1);
    expect(component.currentPreviewStep()?.title).toBe('Welcome to SGrowing');
  });

  it('should add a step and recalculate preview step order', () => {
    const initialStepsCount = component.currentPreviewSteps().length;
    component.addStepToCurrentTour();
    expect(component.selectedTour()?.steps.length).toBe(initialStepsCount + 1);
    expect(component.currentPreviewSteps().length).toBe(initialStepsCount + 1);
    const added = component.selectedTour()?.steps[initialStepsCount];
    expect(added?.stepOrder).toBe(initialStepsCount + 1);
  });
});
