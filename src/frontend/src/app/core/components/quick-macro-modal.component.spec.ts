import '@angular/compiler';
import { describe, it, expect, beforeEach, vi } from 'vitest';
import { Injector, runInInjectionContext } from '@angular/core';
import { Router } from '@angular/router';
import { QuickMacroModalComponent } from './quick-macro-modal.component';
import { KeyboardShortcutsService } from '../services/keyboard-shortcuts.service';

describe('QuickMacroModalComponent', () => {
  let component: QuickMacroModalComponent;
  let mockRouter: any;
  let shortcutsService: KeyboardShortcutsService;

  beforeEach(() => {
    mockRouter = {
      navigate: vi.fn().mockResolvedValue(true)
    };

    shortcutsService = new KeyboardShortcutsService(mockRouter as any);

    const injector = Injector.create({
      providers: [
        { provide: Router, useValue: mockRouter },
        { provide: KeyboardShortcutsService, useValue: shortcutsService }
      ]
    });

    component = runInInjectionContext(injector, () => new QuickMacroModalComponent());
  });

  it('should initialize with empty search query and return all 8 default macro actions', () => {
    expect(component.searchQuery).toBe('');
    const actions = component.filteredActions();
    expect(actions.length).toBe(8);
    expect(actions.map(a => a.id)).toContain('softphone');
    expect(actions.map(a => a.id)).toContain('missions');
    expect(actions.map(a => a.id)).toContain('cpq');
    expect(actions.map(a => a.id)).toContain('webhooks');
  });

  it('should filter actions by title keyword', () => {
    component.searchQuery = 'dialer';
    const filtered = component.filteredActions();
    expect(filtered.length).toBe(1);
    expect(filtered[0].id).toBe('softphone');
    expect(filtered[0].title).toContain('Softphone & Auto Dialer');
  });

  it('should filter actions by category', () => {
    component.searchQuery = 'commerce';
    const filtered = component.filteredActions();
    expect(filtered.length).toBe(2);
    expect(filtered.map(a => a.id)).toEqual(expect.arrayContaining(['commerce_connectors', 'cpq']));
  });

  it('should filter actions by badge label', () => {
    component.searchQuery = 'outbox';
    const filtered = component.filteredActions();
    expect(filtered.length).toBe(1);
    expect(filtered[0].id).toBe('webhooks');
    expect(filtered[0].badge).toBe('Outbox');
  });

  it('should return empty list when no actions match query', () => {
    component.searchQuery = 'non_existent_macro_xyz';
    const filtered = component.filteredActions();
    expect(filtered.length).toBe(0);
  });

  it('should execute action, closing search modal and navigating to route', () => {
    shortcutsService.openMacroSearch();
    expect(shortcutsService.isMacroSearchOpen()).toBe(true);

    const action = {
      id: 'cpq',
      title: 'Interactive CPQ Quote & Proposal Builder',
      category: 'Commerce',
      route: '/cpq',
      badge: 'Pricing',
      icon: '💼'
    };

    component.execute(action);

    expect(shortcutsService.isMacroSearchOpen()).toBe(false);
    expect(mockRouter.navigate).toHaveBeenCalledWith(['/cpq']);
  });
});
