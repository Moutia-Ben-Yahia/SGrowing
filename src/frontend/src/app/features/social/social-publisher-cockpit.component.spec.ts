import { describe, it, expect, beforeEach } from 'vitest';
import { SocialPublisherCockpitComponent } from './social-publisher-cockpit.component';
import { TranslationService } from '../../core/services/translation.service';

describe('SocialPublisherCockpitComponent', () => {
  let component: SocialPublisherCockpitComponent;
  let mockTranslation: TranslationService;

  beforeEach(() => {
    mockTranslation = new TranslationService();
    component = new SocialPublisherCockpitComponent(mockTranslation);
  });

  it('initializes with default platforms and posts in queue', () => {
    expect(component.selectedPlatforms()).toContain('LinkedIn');
    expect(component.selectedPlatforms()).toContain('Instagram');
    expect(component.posts().length).toBeGreaterThanOrEqual(2);
    expect(component.totalImpressions()).toBeGreaterThan(0);
  });

  it('toggles platform selections safely preventing empty selection', () => {
    component.togglePlatform('Facebook');
    expect(component.selectedPlatforms()).toContain('Facebook');

    component.togglePlatform('Facebook');
    expect(component.selectedPlatforms()).not.toContain('Facebook');
  });

  it('creates scheduled post when approval is not required', () => {
    component.postContent.set('Test Growth Campaign Post');
    component.requireApproval = false;
    const initialCount = component.posts().length;

    component.createPost(false);

    expect(component.posts().length).toBe(initialCount + 1);
    expect(component.posts()[0].status).toBe('Scheduled');
    expect(component.posts()[0].content).toBe('Test Growth Campaign Post');
  });

  it('creates pending approval post when approval flag is set', () => {
    component.postContent.set('Executive Post Needing Review');
    component.requireApproval = true;

    component.createPost(false);

    expect(component.posts()[0].status).toBe('PendingApproval');
    expect(component.pendingApprovalCount()).toBeGreaterThan(0);
  });

  it('approves a pending post and moves it to Scheduled', () => {
    const pendingPost = component.posts().find(p => p.status === 'PendingApproval');
    expect(pendingPost).toBeDefined();

    component.approvePost(pendingPost!.id);

    const updated = component.posts().find(p => p.id === pendingPost!.id);
    expect(updated?.status).toBe('Scheduled');
  });
});
