// @vitest-environment jsdom
import '@angular/compiler';
import { describe, it, expect, beforeEach } from 'vitest';
import { UniversalSearchCockpitComponent } from './universal-search-cockpit.component';

describe('UniversalSearchCockpitComponent', () => {
  let component: UniversalSearchCockpitComponent;

  beforeEach(() => {
    component = new UniversalSearchCockpitComponent();
  });

  it('should initialize with pre-seeded ranked search results', () => {
    expect(component).toBeTruthy();
    expect(component.results().length).toBe(4);
    expect(component.activeFilter()).toBe('All');
    expect(component.filteredResults().length).toBe(4);
  });

  it('should filter search results by entity type', () => {
    component.toggleFilter('TicketMessage');
    expect(component.activeFilter()).toBe('TicketMessage');
    expect(component.filteredResults().length).toBe(1);
    expect(component.filteredResults()[0].entityType).toBe('TicketMessage');

    component.toggleFilter('Customer');
    expect(component.filteredResults().length).toBe(0);

    component.toggleFilter('All');
    expect(component.filteredResults().length).toBe(4);
  });

  it('should filter search results by text query', () => {
    component.searchQuery = 'refund';
    component.results.update(r => [...r]);
    expect(component.filteredResults().length).toBeGreaterThan(0);

    component.searchQuery = 'non-matching-term-xyz';
    component.results.update(r => [...r]);
    expect(component.filteredResults().length).toBe(0);
  });

  it('should return styled CSS badge classes for entity types', () => {
    expect(component.getEntityBadgeClass('Customer')).toContain('text-purple-300');
    expect(component.getEntityBadgeClass('TicketMessage')).toContain('text-blue-300');
    expect(component.getEntityBadgeClass('OrderFulfillment')).toContain('text-cyan-300');
    expect(component.getEntityBadgeClass('KnowledgeArticle')).toContain('text-emerald-300');
  });
});
