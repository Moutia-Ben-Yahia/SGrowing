// @vitest-environment jsdom
import '@angular/compiler';
import { describe, it, expect, beforeEach } from 'vitest';
import { RoadmapBoardComponent, FeedbackPostUi } from './roadmap-board.component';

describe('RoadmapBoardComponent', () => {
  let component: RoadmapBoardComponent;

  beforeEach(() => {
    component = new RoadmapBoardComponent();
    component.ngOnInit();
  });

  it('should initialize with pre-seeded public roadmap suggestions', () => {
    expect(component).toBeTruthy();
    const posts = component.posts();
    expect(posts.length).toBeGreaterThanOrEqual(4);
    expect(component.showCreateModal()).toBe(false);
  });

  it('should correctly partition posts across the 4 kanban stages', () => {
    const openPosts = component.getByStatus('Open');
    const plannedPosts = component.getByStatus('Planned');
    const inProgressPosts = component.getByStatus('InProgress');
    const completedPosts = component.getByStatus('Completed');

    expect(openPosts.length).toBeGreaterThan(0);
    expect(plannedPosts.length).toBeGreaterThan(0);
    expect(inProgressPosts.length).toBeGreaterThan(0);
    expect(completedPosts.length).toBeGreaterThan(0);

    expect(openPosts.every(p => p.status === 'Open')).toBe(true);
    expect(completedPosts.every(p => p.status === 'Completed')).toBe(true);
  });

  it('should toggle upvotes idempotently and update vote count', () => {
    const target = component.posts()[0];
    const initialVotes = target.upvoteCount;
    const initialVoted = target.hasVoted;

    component.toggleVote(target);

    if (initialVoted) {
      expect(target.hasVoted).toBe(false);
      expect(target.upvoteCount).toBe(initialVotes - 1);
    } else {
      expect(target.hasVoted).toBe(true);
      expect(target.upvoteCount).toBe(initialVotes + 1);
    }

    // Toggle back
    component.toggleVote(target);
    expect(target.hasVoted).toBe(initialVoted);
    expect(target.upvoteCount).toBe(initialVotes);
  });

  it('should move post status across columns', () => {
    const target = component.posts()[0];
    expect(target.status).not.toBe('Completed');

    component.moveStatus(target, 'Completed');
    expect(target.status).toBe('Completed');
    expect(component.getByStatus('Completed')).toContain(target);
  });

  it('should create and submit a new feature suggestion', () => {
    component.showCreateModal.set(true);
    component.newTitle = 'Autonomous WhatsApp Voice Note Transcriptions';
    component.newCategory = 'Developer';
    component.newDescription = 'Automatically transcribe Arabic & English audio voicemails into text.';

    const countBefore = component.posts().length;
    component.savePost();

    expect(component.showCreateModal()).toBe(false);
    expect(component.posts().length).toBe(countBefore + 1);

    const newest = component.posts()[0];
    expect(newest.title).toBe('Autonomous WhatsApp Voice Note Transcriptions');
    expect(newest.status).toBe('Open');
    expect(newest.upvoteCount).toBe(1);
    expect(newest.hasVoted).toBe(true);
    expect(component.newTitle).toBe('');
    expect(component.newDescription).toBe('');
  });

  it('should not create a post if title is empty', () => {
    component.newTitle = '';
    const countBefore = component.posts().length;
    component.savePost();
    expect(component.posts().length).toBe(countBefore);
  });
});
