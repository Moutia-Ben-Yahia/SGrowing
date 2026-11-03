// @vitest-environment jsdom
import '@angular/compiler';
import { describe, it, expect, beforeEach, vi } from 'vitest';
import { createEnvironmentInjector } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { AgentWorkforceCockpitComponent, AgentRoleUi } from './agent-workforce-cockpit.component';
import { of } from 'rxjs';

describe('AgentWorkforceCockpitComponent', () => {
  let component: AgentWorkforceCockpitComponent;
  let httpMock: any;

  const mockRoles: AgentRoleUi[] = [
    {
      roleCode: 'growth_architect',
      displayName: 'Growth Architect',
      echelon: 'Business',
      category: 'Strategy',
      description: 'Designs go-to-market funnels',
      competencies: ['Funnels', 'CAC Optimization'],
      defaultModelTier: 'ReasoningTier',
      icon: '📈',
      defaultStatus: 'Active',
      systemObjective: 'Maximize ARR'
    },
    {
      roleCode: 'frontend_engineer',
      displayName: 'Lead Frontend Engineer',
      echelon: 'Engineering',
      category: 'UI/UX',
      description: 'Builds modern Angular web apps',
      competencies: ['Angular 22', 'Tailwind', 'Vitest'],
      defaultModelTier: 'StandardTier',
      icon: '💻',
      defaultStatus: 'Active',
      systemObjective: 'High speed, 100% test pass rate'
    }
  ];

  beforeEach(() => {
    httpMock = {
      get: vi.fn().mockImplementation((url: string) => {
        if (url.includes('/roles')) return of({ businessRoles: [mockRoles[0]], engineeringRoles: [mockRoles[1]] });
        if (url.includes('/trees')) return of([]);
        if (url.includes('/activity')) return of([]);
        return of([]);
      }),
      post: vi.fn().mockReturnValue(of({ success: true }))
    };

    const injector = createEnvironmentInjector([
      { provide: HttpClient, useValue: httpMock }
    ]);

    injector.runInContext(() => {
      component = new AgentWorkforceCockpitComponent();
    });

    component.allRoles.set(mockRoles);
  });

  it('should initialize with roles catalog and compute counts by echelon', () => {
    expect(component).toBeTruthy();
    expect(component.totalRolesCount()).toBe(2);
    expect(component.businessRolesCount()).toBe(1);
    expect(component.engineeringRolesCount()).toBe(1);
    expect(component.activeTab()).toBe('roles');
  });

  it('should switch between cockpit tabs', () => {
    component.activeTab.set('decomposer');
    expect(component.activeTab()).toBe('decomposer');

    component.activeTab.set('audit');
    expect(component.activeTab()).toBe('audit');

    component.activeTab.set('roles');
    expect(component.activeTab()).toBe('roles');
  });

  it('should filter roles by echelon', () => {
    component.roleFilter.set('business');
    expect(component.roleFilter()).toBe('business');

    component.roleFilter.set('engineering');
    expect(component.roleFilter()).toBe('engineering');
  });

  it('should inspect selected agent role detail', () => {
    component.inspectedRole.set(mockRoles[0]);

    expect(component.inspectedRole()).toEqual(mockRoles[0]);
    expect(component.inspectedRole()?.displayName).toBe('Growth Architect');
  });
});
