// @vitest-environment jsdom
import '@angular/compiler';
import { describe, it, expect, beforeEach, vi } from 'vitest';
import { Injector, runInInjectionContext } from '@angular/core';
import { of, throwError } from 'rxjs';
import { FeedbackStudioComponent } from './feedback-studio.component';
import { ApiService } from '../../core/services/api.service';
import { SurveyResponseDto } from '../../core/models/platform.models';

describe('FeedbackStudioComponent', () => {
  let component: FeedbackStudioComponent;
  let mockApi: any;

  beforeEach(() => {
    vi.stubGlobal('alert', vi.fn());

    mockApi = {
      getFeedbackResponses: vi.fn().mockReturnValue(of([
        {
          id: 'resp-1',
          surveyFormId: 'form-1',
          contactId: 'c-101',
          score: 5,
          feedbackText: 'Fastest delivery resolution ever seen!',
          sentiment: 1,
          createdAtUtc: '2026-10-25T12:00:00Z'
        },
        {
          id: 'resp-2',
          surveyFormId: 'form-1',
          contactId: 'c-102',
          score: 1,
          feedbackText: 'Driver did not arrive on time.',
          sentiment: 3,
          createdAtUtc: '2026-10-25T13:00:00Z'
        }
      ])),
      submitFeedback: vi.fn().mockReturnValue(of({
        id: 'resp-new',
        surveyFormId: 'f-1',
        contactId: 'c-1',
        score: 5,
        feedbackText: 'Flawless WhatsApp bot experience',
        sentiment: 1,
        createdAtUtc: '2026-10-25T14:00:00Z'
      }))
    };

    const injector = Injector.create({
      providers: [
        { provide: ApiService, useValue: mockApi }
      ]
    });

    component = runInInjectionContext(injector, () => new FeedbackStudioComponent());
    component.ngOnInit();
  });

  it('should initialize and load feedback responses from ApiService', () => {
    expect(component).toBeTruthy();
    expect(mockApi.getFeedbackResponses).toHaveBeenCalled();
    const responses = component.responses();
    expect(responses.length).toBe(2);
    expect(responses[0].score).toBe(5);
    expect(responses[1].sentiment).toBe(3);
  });

  it('should fall back to pre-seeded mock responses when ApiService returns empty or throws', () => {
    mockApi.getFeedbackResponses.mockReturnValue(throwError(() => new Error('Network error')));
    const injector = Injector.create({
      providers: [{ provide: ApiService, useValue: mockApi }]
    });

    const fallbackComp = runInInjectionContext(injector, () => new FeedbackStudioComponent());
    fallbackComp.ngOnInit();

    expect(fallbackComp.responses().length).toBe(2);
    expect(fallbackComp.responses()[0].feedbackText).toContain('AI Copilot');
  });

  it('should submit sample feedback successfully and prepend response to list', () => {
    component.selectedScore.set(5);
    component.feedbackComment = 'Flawless WhatsApp bot experience';

    component.submitSampleFeedback();

    expect(mockApi.submitFeedback).toHaveBeenCalledWith(
      expect.any(String),
      expect.any(String),
      expect.any(String),
      5,
      'Flawless WhatsApp bot experience'
    );
    expect(component.feedbackComment).toBe('');
    expect(component.responses()[0].id).toBe('resp-new');
    expect(globalThis.alert).toHaveBeenCalled();
  });

  it('should handle API submit failure by locally synthesizing feedback and alert', () => {
    mockApi.submitFeedback.mockReturnValue(throwError(() => new Error('Offline')));

    component.selectedScore.set(2);
    component.feedbackComment = 'Failed to download receipt PDF';

    component.submitSampleFeedback();

    expect(component.feedbackComment).toBe('');
    const newest = component.responses()[0];
    expect(newest.score).toBe(2);
    expect(newest.sentiment).toBe(3); // Score <= 3 maps to 3 (Negative / At-Risk)
    expect(newest.feedbackText).toBe('Failed to download receipt PDF');
    expect(globalThis.alert).toHaveBeenCalled();
  });
});
