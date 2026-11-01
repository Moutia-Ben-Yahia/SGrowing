// @vitest-environment jsdom
import '@angular/compiler';
import { describe, it, expect, beforeEach } from 'vitest';
import { Injector, runInInjectionContext } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { of } from 'rxjs';
import { HeadlessFormBuilderComponent } from './headless-form-builder.component';

describe('HeadlessFormBuilderComponent (Headless Forms & In-App Micro-Surveys)', () => {
  let component: HeadlessFormBuilderComponent;

  const mockForms = [
    {
      id: 'form-enterprise-lead',
      tenantId: 'tenant-1',
      title: 'Enterprise High-Intent Lead Form',
      slug: 'enterprise-lead',
      description: 'Capture B2B leads with progressive profiling',
      targetAction: 'CreateLead' as const,
      isPublished: true,
      isArchived: false,
      requireTurnstile: true,
      rateLimitPerMinutePerIp: 10,
      velocityThrottlePerMinute: 60,
      createdAtUtc: '2026-11-01T08:00:00Z',
      metrics: {
        impressions: 1250,
        submissions: 240,
        spamRejections: 18,
        averageCompletionTimeSeconds: 42,
        conversionRatePercentage: 19.2
      },
      fields: [
        {
          id: 'field-1',
          name: 'fullName',
          label: 'Full Name',
          type: 'Text' as const,
          isRequired: true,
          options: [],
          step: 1
        },
        {
          id: 'field-2',
          name: 'workEmail',
          label: 'Work Email',
          type: 'Email' as const,
          isRequired: true,
          options: [],
          step: 1
        },
        {
          id: 'field-3',
          name: 'companySize',
          label: 'Company Size',
          type: 'Select' as const,
          isRequired: false,
          options: ['1-10', '11-50', '51-200', '201+'],
          step: 2
        }
      ]
    }
  ];

  const mockSubmissions = [
    {
      id: 'sub-1',
      formId: 'form-enterprise-lead',
      tenantId: 'tenant-1',
      answers: { fullName: 'Jane Doe', workEmail: 'jane@enterprise.com' },
      isSpam: false,
      spamScore: 5,
      clientIp: '192.168.1.1',
      completionTimeSeconds: 38,
      webhookDeliveryStatus: 'Dispatched',
      submittedAtUtc: '2026-11-01T09:30:00Z'
    },
    {
      id: 'sub-2',
      formId: 'form-enterprise-lead',
      tenantId: 'tenant-1',
      answers: { fullName: 'Spam Bot', workEmail: 'bot@spam.ru' },
      isSpam: true,
      spamScore: 95,
      clientIp: '10.0.0.99',
      completionTimeSeconds: 1,
      webhookDeliveryStatus: 'Skipped',
      submittedAtUtc: '2026-11-01T09:32:00Z'
    }
  ];

  const mockHttp = {
    get: (url: string) => {
      if (url.includes('/headless/form-enterprise-lead/submissions')) return of(mockSubmissions);
      if (url.includes('/headless')) return of(mockForms);
      return of([]);
    },
    post: () => of({ success: true })
  };

  beforeEach(() => {
    const injector = Injector.create({
      providers: [
        { provide: HttpClient, useValue: mockHttp }
      ]
    });

    component = runInInjectionContext(injector, () => new HeadlessFormBuilderComponent());
    component.ngOnInit();
  });

  it('should initialize with forms list and select first form by default', () => {
    expect(component.forms().length).toBeGreaterThanOrEqual(1);
    expect(component.selectedFormId()).toBe('form-enterprise-lead');
    expect(component.currentForm()).not.toBeNull();
    expect(component.currentForm()?.title).toBe('Enterprise High-Intent Lead Form');
  });

  it('should compute form conversion metrics correctly', () => {
    expect(component.totalViews()).toBe(1250);
    expect(component.totalSubmissions()).toBe(240);
    expect(component.conversionRate()).toBe(19.2);
    expect(component.spamBlocked()).toBe(18);
    expect(component.avgCompletionTime()).toBe(42);
  });

  it('should calculate maxFormStep and filter fields for multi-step preview', () => {
    expect(component.maxFormStep()).toBe(2);
    expect(component.currentPreviewStep()).toBe(1);

    // Step 1 fields (Name and Email)
    const step1Fields = component.currentStepFields();
    expect(step1Fields.length).toBe(2);
    expect(step1Fields.map(f => f.name)).toEqual(['fullName', 'workEmail']);
    expect(component.isFinalPreviewStep()).toBe(false);

    // Advance to Step 2
    component.currentPreviewStep.set(2);
    const step2Fields = component.currentStepFields();
    expect(step2Fields.length).toBe(1);
    expect(step2Fields[0].name).toBe('companySize');
    expect(component.isFinalPreviewStep()).toBe(true);
  });

  it('should filter submissions by clean and spam correctly', () => {
    component.submissions.set(mockSubmissions as any);

    component.submissionFilter.set('all');
    expect(component.filteredSubmissions().length).toBe(2);

    component.submissionFilter.set('clean');
    expect(component.filteredSubmissions().length).toBe(1);
    expect(component.filteredSubmissions()[0].id).toBe('sub-1');

    component.submissionFilter.set('spam');
    expect(component.filteredSubmissions().length).toBe(1);
    expect(component.filteredSubmissions()[0].id).toBe('sub-2');
  });

  it('should generate embed snippets for HTML, iframe, and direct link', () => {
    component.embedSnippetTab.set('html');
    const htmlSnippet = component.activeEmbedSnippet();
    expect(htmlSnippet).toContain('<form action=');
    expect(htmlSnippet).toContain('name="fullName"');

    component.embedSnippetTab.set('iframe');
    const iframeSnippet = component.activeEmbedSnippet();
    expect(iframeSnippet).toContain('<iframe');
    expect(iframeSnippet).toContain('app.sgrowing.io/f/enterprise-lead');

    component.embedSnippetTab.set('link');
    const linkSnippet = component.activeEmbedSnippet();
    expect(linkSnippet).toBe('https://app.sgrowing.io/f/enterprise-lead');
  });
});
