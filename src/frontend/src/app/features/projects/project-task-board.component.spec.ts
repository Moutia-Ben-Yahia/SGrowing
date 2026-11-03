// @vitest-environment jsdom
import '@angular/compiler';
import { describe, it, expect, beforeEach, vi } from 'vitest';
import { Injector, runInInjectionContext } from '@angular/core';
import { ProjectTaskBoardComponent, KanbanTask } from './project-task-board.component';
import { TranslationService } from '../../core/services/translation.service';

describe('ProjectTaskBoardComponent', () => {
  let component: ProjectTaskBoardComponent;
  let mockTranslation: any;

  beforeEach(() => {
    mockTranslation = {
      t: vi.fn((key: string) => key),
      currentLocale: vi.fn().mockReturnValue('en-US')
    };

    const injector = Injector.create({
      providers: [
        { provide: TranslationService, useValue: mockTranslation }
      ]
    });

    component = runInInjectionContext(injector, () => new ProjectTaskBoardComponent());
  });

  it('should initialize with active project and default kanban tasks', () => {
    expect(component).toBeTruthy();
    expect(component.selectedProjectId()).toBe('proj-onboarding-1');
    expect(component.activeProject()?.name).toContain('Enterprise Retail Corp');
    expect(component.tasks().length).toBeGreaterThan(0);
    expect(component.completionRate()).toBeGreaterThanOrEqual(0);
  });

  it('should filter tasks by column status, search query, and priority', () => {
    const todoTasks = component.getColumnTasks('Todo');
    expect(todoTasks.every(t => t.status === 'Todo')).toBe(true);

    component.searchQuery = 'Infrastructure';
    const searchedTasks = component.getColumnTasks('InProgress');
    expect(searchedTasks.length).toBe(1);
    expect(searchedTasks[0].title).toContain('Technical Infrastructure');

    component.searchQuery = '';
    component.selectedPriorityFilter = 'Urgent';
    const urgentDone = component.getColumnTasks('Done');
    expect(urgentDone.length).toBe(1);
    expect(urgentDone[0].priority).toBe('Urgent');
  });

  it('should move tasks left and right across workflow statuses', () => {
    let task = component.tasks().find(t => t.id === 'task-2')!; // InProgress
    expect(task.status).toBe('InProgress');

    component.moveTaskLeft(task);
    expect(component.tasks().find(t => t.id === 'task-2')?.status).toBe('Todo');

    task = component.tasks().find(t => t.id === 'task-2')!;
    component.moveTaskRight(task);
    expect(component.tasks().find(t => t.id === 'task-2')?.status).toBe('InProgress');

    task = component.tasks().find(t => t.id === 'task-2')!;
    component.moveTaskRight(task);
    expect(component.tasks().find(t => t.id === 'task-2')?.status).toBe('InReview');

    task = component.tasks().find(t => t.id === 'task-2')!;
    component.moveTaskRight(task);
    expect(component.tasks().find(t => t.id === 'task-2')?.status).toBe('Done');
  });

  it('should create a new kanban task and update completion rate', () => {
    component.newTaskTitle = 'Configure Cloudflare Tunnel & DNS CNAME';
    component.newTaskDescription = 'Set up reverse proxy ingress for custom domains';
    component.newTaskStatus = 'Todo';
    component.newTaskPriority = 'High';
    component.newTaskAssignee = 'Sarah Jenkins';
    component.newTaskSubtasksTotal = 4;
    component.newTaskSubtasksCompleted = 0;

    const countBefore = component.tasks().length;
    component.submitCreateTask();

    expect(component.tasks().length).toBe(countBefore + 1);
    const created = component.tasks().find(t => t.title.includes('Cloudflare Tunnel'));
    expect(created).toBeDefined();
    expect(created?.priority).toBe('High');
    expect(created?.assigneeAvatar).toBe('SJ');
    expect(component.showCreateModal()).toBe(false);
  });

  it('should convert a won deal into an onboarding sprint with milestone templates', () => {
    const projectsBefore = component.projects().length;
    const tasksBefore = component.tasks().length;

    component.convertWonDealDemo();

    expect(component.projects().length).toBe(projectsBefore + 1);
    expect(component.tasks().length).toBe(tasksBefore + 3);
    expect(component.activeProject()?.name).toContain('Client Onboarding: Global FMCG Won Deal');
  });

  it('should generate secure client onboarding portal link', () => {
    component.generatePortalLink();
    expect(component.portalGeneratedToken()).toContain('spt_live_');
    expect(component.portalLinkGenerated()).toBe(true);

    component.copyPortalUrl();
    expect(component.toastMessage()).toContain('/portal/onboarding/');
  });
});
