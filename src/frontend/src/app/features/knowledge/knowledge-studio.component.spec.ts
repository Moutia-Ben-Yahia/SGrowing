// @vitest-environment jsdom
import '@angular/compiler';
import { describe, it, expect, beforeEach, vi } from 'vitest';
import { Injector, runInInjectionContext } from '@angular/core';
import { of } from 'rxjs';
import { ApiService } from '../../core/services/api.service';
import { KnowledgeStudioComponent } from './knowledge-studio.component';

describe('KnowledgeStudioComponent', () => {
  let component: KnowledgeStudioComponent;
  let mockApi: any;

  const mockArticles = [
    {
      id: 'a-1',
      categoryId: 'cat-1',
      title: 'Connecting WhatsApp Meta Cloud API',
      slug: 'whatsapp-meta-setup',
      contentMarkdown: 'Guide for WhatsApp Meta Cloud API connection.',
      isPublished: true,
      viewCount: 142,
      createdAtUtc: '2026-11-03T10:00:00Z'
    }
  ];

  const mockRagResult = {
    deflected: true,
    answer: 'To connect Meta WhatsApp Cloud API: 1. Generate System User Token.',
    articleTitle: 'Connecting WhatsApp Meta Cloud API',
    articleSlug: 'whatsapp-meta-setup',
    confidenceScore: 92
  };

  beforeEach(() => {
    mockApi = {
      getArticles: vi.fn().mockReturnValue(of(mockArticles)),
      queryHelpCenter: vi.fn().mockReturnValue(of(mockRagResult))
    };

    const injector = Injector.create({
      providers: [
        { provide: ApiService, useValue: mockApi }
      ]
    });

    component = runInInjectionContext(injector, () => new KnowledgeStudioComponent());
    component.ngOnInit();
  });

  it('should initialize and load published help articles and execute initial AI query', () => {
    expect(component).toBeTruthy();
    expect(mockApi.getArticles).toHaveBeenCalled();
    expect(mockApi.queryHelpCenter).toHaveBeenCalled();
    expect(component.articles().length).toBe(1);
    expect(component.ragResult()?.deflected).toBe(true);
    expect(component.ragResult()?.confidenceScore).toBe(92);
  });

  it('should query AI RAG deflection engine with new customer question', () => {
    component.searchQuery = 'How to change delivery address?';
    component.askAi();

    expect(mockApi.queryHelpCenter).toHaveBeenCalledWith(
      expect.any(String),
      'How to change delivery address?'
    );
  });

  it('should ignore empty search queries and invoke fallback deflection on API failure', () => {
    mockApi.queryHelpCenter.mockClear();
    component.searchQuery = '';
    component.askAi();
    expect(mockApi.queryHelpCenter).not.toHaveBeenCalled();

    // Trigger error fallback path
    mockApi.queryHelpCenter.mockReturnValue(of());
    // Simulate error callback
    component.searchQuery = 'How do I connect WhatsApp Meta Cloud API?';
    mockApi.queryHelpCenter = vi.fn().mockImplementation(() => {
      const { throwError } = require('rxjs');
      return throwError(() => new Error('Service Unavailable'));
    });

    component.askAi();
    expect(component.ragResult()?.deflected).toBe(true);
    expect(component.ragResult()?.articleSlug).toBe('whatsapp-meta-setup');
    expect(component.ragResult()?.confidenceScore).toBe(92);
  });
});
