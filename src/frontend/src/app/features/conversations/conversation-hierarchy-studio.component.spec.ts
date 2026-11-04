// @vitest-environment jsdom
import '@angular/compiler';
import { describe, it, expect, beforeEach, vi } from 'vitest';
import { ConversationHierarchyStudioComponent } from './conversation-hierarchy-studio.component';

describe('ConversationHierarchyStudioComponent', () => {
  let component: ConversationHierarchyStudioComponent;

  beforeEach(() => {
    component = new ConversationHierarchyStudioComponent();
  });

  it('should instantiate successfully', () => {
    expect(component).toBeTruthy();
  });

  it('should trigger openSplitModal without error', () => {
    const splitSpy = vi.spyOn(component, 'openSplitModal');
    component.openSplitModal();
    expect(splitSpy).toHaveBeenCalled();
  });

  it('should trigger openMergeModal without error', () => {
    const mergeSpy = vi.spyOn(component, 'openMergeModal');
    component.openMergeModal();
    expect(mergeSpy).toHaveBeenCalled();
  });

  it('should maintain stable method signatures for modal actions', () => {
    expect(typeof component.openSplitModal).toBe('function');
    expect(typeof component.openMergeModal).toBe('function');
    expect(() => {
      component.openSplitModal();
      component.openMergeModal();
    }).not.toThrow();
  });
});
