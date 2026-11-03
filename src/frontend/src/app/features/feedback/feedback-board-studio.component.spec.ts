// @vitest-environment jsdom
import '@angular/compiler';
import { describe, it, expect, beforeEach, vi } from 'vitest';
import { Injector, runInInjectionContext } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { of } from 'rxjs';
import { FeedbackBoardStudioComponent, FeedbackPostUi } from './feedback-board-studio.component';

describe('FeedbackBoardStudioComponent', () => {
  let component: FeedbackBoardStudioComponent;
  let mockHttp: any;

  beforeEach(() => {
    mockHttp = {
      get: vi.fn().mockReturnValue(of([])),
      post: vi.fn().mockReturnValue(of({})),
      patch: vi.fn().mockReturnValue(of({}))
    };

    const injector = Injector.create({
      providers: [
        { provide: HttpClient, useValue: mockHttp }
      ]
    });

    component = runInInjectionContext(injector, () => new FeedbackBoardStudioComponent());
    component.ngOnInit();
  });

  it('should initialize with pre-seeded feedback posts and computed metrics', () => {
    expect(component).toBeTruthy();
    const posts = component.posts();
    expect(posts.length).toBeGreaterThan(0);
    expect(component.totalIdeas()).toBe(posts.length);
    expect(component.viewMode()).toBe('board');
  });

  it('should filter posts by search query and category', () => {
    component.posts.set([
      {
        id: 'p-1',
        tenantId: 't-1',
        title: 'HubSpot CRM Webhook Sync',
        description: 'Auto-sync deals to CRM',
        category: 'Integration',
        status: 'Submitted',
        authorEmail: 'alice@fincorp.com',
        authorName: 'Alice',
        accountId: 'acc-1',
        accountARR: 50000,
        upvoteCount: 10,
        impactScore: 47,
        createdAt: '2026-10-01T00:00:00Z',
        updatedAt: '2026-10-01T00:00:00Z',
        estimatedQuarter: 'Q1 2027'
      },
      {
        id: 'p-2',
        tenantId: 't-1',
        title: 'Dark Mode Theme Polish',
        description: 'Fix contrast on high-res displays',
        category: 'UxImprovement',
        status: 'Planned',
        authorEmail: 'bob@retail.com',
        authorName: 'Bob',
        accountId: 'acc-2',
        accountARR: 12000,
        upvoteCount: 4,
        impactScore: 16,
        createdAt: '2026-10-02T00:00:00Z',
        updatedAt: '2026-10-02T00:00:00Z',
        estimatedQuarter: 'Q2 2027'
      }
    ]);

    component.searchQuery.set('HubSpot');
    expect(component.filteredPosts().length).toBe(1);
    expect(component.filteredPosts()[0].id).toBe('p-1');

    component.searchQuery.set('');
    component.selectedCategory.set('UxImprovement');
    expect(component.filteredPosts().length).toBe(1);
    expect(component.filteredPosts()[0].id).toBe('p-2');
  });

  it('should handle optimistic upvoting and impact score re-calculation', () => {
    const initialPost = component.posts()[0];
    const initialVotes = initialPost.upvoteCount;
    const initialVoted = initialPost.hasVoted ?? false;

    component.toggleUpvote(initialPost);

    const updatedPost = component.posts().find(p => p.id === initialPost.id);
    expect(updatedPost).toBeDefined();
    if (initialVoted) {
      expect(updatedPost!.upvoteCount).toBe(Math.max(0, initialVotes - 1));
      expect(updatedPost!.hasVoted).toBe(false);
    } else {
      expect(updatedPost!.upvoteCount).toBe(initialVotes + 1);
      expect(updatedPost!.hasVoted).toBe(true);
    }
    expect(mockHttp.post).toHaveBeenCalled();
  });

  it('should submit a new idea optimistically and prepend to list', () => {
    component.openSubmitModal();
    expect(component.isSubmitModalOpen()).toBe(true);

    component.ideaForm.title = 'Native QuickBooks B2B Integration';
    component.ideaForm.description = 'Direct settlement sync for accounts receivable';
    component.ideaForm.category = 'Integration';
    component.ideaForm.accountARR = 85000;
    component.ideaForm.estimatedQuarter = 'Q2 2027';

    const countBefore = component.posts().length;
    component.submitNewIdea();

    expect(component.isSubmitModalOpen()).toBe(false);
    expect(component.posts().length).toBe(countBefore + 1);
    const newest = component.posts()[0];
    expect(newest.title).toBe('Native QuickBooks B2B Integration');
    expect(newest.accountARR).toBe(85000);
    expect(mockHttp.post).toHaveBeenCalled();
  });

  it('should support admin status changes and official response notes', () => {
    const targetPost = component.posts()[0];
    component.openDetailModal(targetPost);
    expect(component.selectedPost()).toBeDefined();

    component.adminStatusEdit = 'Planned';
    component.adminNoteEdit = 'Approved for Q2 engineering roadmap cycle.';
    component.adminQuarterEdit = 'Q2 2027';

    component.saveAdminStatusChange(targetPost);

    const postAfter = component.posts().find(p => p.id === targetPost.id);
    expect(postAfter?.status).toBe('Planned');
    expect(postAfter?.responseNotes).toBe('Approved for Q2 engineering roadmap cycle.');
    expect(mockHttp.patch).toHaveBeenCalled();
  });

  it('should format numbers and category badge classes gracefully', () => {
    expect(component.formatNumber(1500000)).toBe('1.5M');
    expect(component.formatNumber(45000)).toBe('45k');
    expect(component.formatNumber(500)).toBe('500');

    expect(component.getCategoryLabel('FeatureRequest')).toBe('Feature');
    expect(component.getCategoryLabel('Integration')).toBe('Integration');
    expect(component.getCategoryBadgeClass('BugReport')).toContain('rose');
    expect(component.getStatusBadgeClass('Completed')).toContain('emerald');
  });
});
