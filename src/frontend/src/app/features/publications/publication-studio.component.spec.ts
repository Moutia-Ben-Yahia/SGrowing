// @vitest-environment jsdom
import '@angular/compiler';
import { describe, it, expect, beforeEach } from 'vitest';
import { PublicationStudioComponent } from './publication-studio.component';

describe('PublicationStudioComponent', () => {
  let component: PublicationStudioComponent;

  beforeEach(() => {
    component = new PublicationStudioComponent();
    component.ngOnInit();
  });

  it('should initialize with pre-seeded publication articles and selected article', () => {
    expect(component).toBeTruthy();
    expect(component.articles().length).toBe(3);
    expect(component.selectedArticle()?.slug).toBe('saas-retention-playbook');
    expect(component.publishedCount()).toBe(3);
  });

  it('should evaluate paywall reader access gating correctly', () => {
    const paidArticle = component.articles().find(a => a.visibility === 'PaidTier')!;
    const membersArticle = component.articles().find(a => a.visibility === 'MembersOnly')!;
    const publicArticle = component.articles().find(a => a.visibility === 'Public')!;

    component.readerTier.set('Anonymous');
    expect(component.isReaderGated(paidArticle)).toBe(true);
    expect(component.isReaderGated(membersArticle)).toBe(true);
    expect(component.isReaderGated(publicArticle)).toBe(false);

    component.readerTier.set('FreeMember');
    expect(component.isReaderGated(paidArticle)).toBe(true);
    expect(component.isReaderGated(membersArticle)).toBe(false);

    component.readerTier.set('PaidMember');
    expect(component.isReaderGated(paidArticle)).toBe(false);
  });

  it('should save a new publication article as draft', () => {
    const initialCount = component.articles().length;
    component.newTitle = 'AI SDR Scaling Strategies';
    component.newExcerpt = 'Operational lessons from scaling multi-channel outbound.';
    component.newContent = 'Full article breakdown of SDR playbooks...';
    component.saveArticle();

    expect(component.articles().length).toBe(initialCount + 1);
    expect(component.articles()[0].title).toBe('AI SDR Scaling Strategies');
    expect(component.articles()[0].status).toBe('Draft');
    expect(component.selectedArticle()?.title).toBe('AI SDR Scaling Strategies');
  });

  it('should publish draft article and increment published count', () => {
    const draft = {
      id: 'draft-1',
      title: 'Draft Piece',
      slug: 'draft-piece',
      excerpt: 'Summary',
      authorName: 'Author',
      visibility: 'Public' as const,
      status: 'Draft' as const,
      wordCount: 120,
      estimatedReadMinutes: 1,
      viewCount: 0,
      htmlContent: 'Draft text'
    };

    component.publishArticle(draft);
    expect(draft.status).toBe('Published');
    expect(component.publishedCount()).toBe(4);
  });

  it('should ignore saveArticle when newTitle is empty and toggle broadcast modal', () => {
    const countBefore = component.articles().length;
    component.newTitle = '';
    component.saveArticle();
    expect(component.articles().length).toBe(countBefore);

    component.showBroadcastModal.set(true);
    expect(component.showBroadcastModal()).toBe(true);
    component.sendBroadcast();
    expect(component.showBroadcastModal()).toBe(false);
  });
});
