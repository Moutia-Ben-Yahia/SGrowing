import { Component, signal, computed, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { TranslationService } from '../../core/services/translation.service';
import { LanguageSwitcherComponent } from '../../core/components/language-switcher.component';

export type TaskStatus = 'Backlog' | 'Todo' | 'InProgress' | 'InReview' | 'Done';
export type TaskPriority = 'Urgent' | 'High' | 'Medium' | 'Low';

export interface KanbanTask {
  id: string;
  projectId: string;
  title: string;
  description: string;
  status: TaskStatus;
  priority: TaskPriority;
  assigneeName: string;
  assigneeAvatar: string;
  position: number;
  subtasksCompleted: number;
  subtasksTotal: number;
  dueDate: string;
  commentCount: number;
}

export interface ProjectHeader {
  id: string;
  name: string;
  description: string;
  status: string;
  dealId?: string;
  contractId?: string;
  targetLaunchDate: string;
}

@Component({
  selector: 'app-project-task-board',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterLink, LanguageSwitcherComponent],
  template: `
    <div class="flex h-screen w-screen overflow-hidden bg-slate-950 text-slate-100 font-sans" dir="ltr">
      <main class="flex-1 flex flex-col overflow-hidden">
        <!-- Top Navigation Header -->
        <header class="h-14 border-b border-slate-800 bg-slate-900/80 backdrop-blur-md px-6 flex items-center justify-between shrink-0 sticky top-0 z-20">
          <div class="flex items-center gap-3">
            <a routerLink="/" class="text-xs text-slate-400 hover:text-white flex items-center gap-1 transition">
              ← {{ translation.t('nav.inbox') }}
            </a>
            <span class="text-slate-700">|</span>
            <span class="text-base font-bold text-white flex items-center gap-2">
              📋 Sprint Task Engine & Post-Close Onboarding
            </span>
            <span class="bg-indigo-500/20 text-indigo-300 text-[10px] font-mono px-2 py-0.5 rounded-full border border-indigo-500/30">
              Inspired by Ever Teams • Linear-Style Kanban
            </span>
          </div>

          <div class="flex items-center gap-3">
            <app-language-switcher></app-language-switcher>
            <button
              data-testid="convert-deal-btn"
              (click)="convertWonDealDemo()"
              class="px-3 py-1.5 rounded-lg bg-emerald-600 hover:bg-emerald-500 text-white text-xs font-semibold flex items-center gap-1.5 transition shadow-sm">
              ✨ Convert Won Deal to Onboarding
            </button>
            <button
              data-testid="add-task-btn"
              (click)="showCreateModal.set(true)"
              class="px-3 py-1.5 rounded-lg bg-indigo-600 hover:bg-indigo-500 text-white text-xs font-semibold flex items-center gap-1.5 transition shadow-sm">
              + New Task
            </button>
          </div>
        </header>

        <!-- Project Subheader Banner -->
        <div class="px-6 py-3 border-b border-slate-800 bg-slate-900/40 flex flex-wrap items-center justify-between gap-4 shrink-0">
          <div class="flex items-center gap-4">
            <div class="flex items-center gap-2">
              <span class="text-xs text-slate-400 font-medium">Active Project:</span>
              <select
                [ngModel]="selectedProjectId()"
                (ngModelChange)="switchProject($event)"
                class="bg-slate-950 border border-slate-700 rounded-md px-2.5 py-1 text-xs text-white focus:outline-none focus:border-indigo-500">
                @for (p of projects(); track p.id) {
                  <option [value]="p.id">{{ p.name }} ({{ p.status }})</option>
                }
              </select>
            </div>
            <div class="h-4 w-px bg-slate-800"></div>
            <div class="text-xs text-slate-400">
              Target Launch: <span class="text-slate-200 font-mono">{{ activeProject()?.targetLaunchDate }}</span>
            </div>
            <div class="h-4 w-px bg-slate-800"></div>
            <div class="text-xs text-slate-400">
              Completion:
              <span class="text-emerald-400 font-semibold font-mono">{{ completionRate() }}%</span>
              <span class="text-slate-500 ml-1">({{ doneCount() }}/{{ tasks().length }} tasks)</span>
            </div>
          </div>

          <div class="flex items-center gap-3">
            <!-- Search Filter -->
            <input
              type="text"
              data-testid="task-search-input"
              [(ngModel)]="searchQuery"
              placeholder="Filter tasks..."
              class="bg-slate-950 border border-slate-800 rounded-md px-3 py-1 text-xs text-slate-200 placeholder-slate-500 focus:outline-none focus:border-indigo-500 w-44" />

            <!-- Priority Filter -->
            <select
              [(ngModel)]="selectedPriorityFilter"
              class="bg-slate-950 border border-slate-800 rounded-md px-2 py-1 text-xs text-slate-300 focus:outline-none focus:border-indigo-500">
              <option value="ALL">All Priorities</option>
              <option value="Urgent">Urgent</option>
              <option value="High">High</option>
              <option value="Medium">Medium</option>
              <option value="Low">Low</option>
            </select>
          </div>
        </div>

        <!-- Success Toast Notification -->
        @if (toastMessage()) {
          <div class="mx-6 mt-3 px-4 py-2 bg-emerald-500/20 border border-emerald-500/40 text-emerald-300 text-xs rounded-lg flex items-center justify-between animate-fadeIn">
            <span>{{ toastMessage() }}</span>
            <button (click)="toastMessage.set('')" class="text-emerald-400 hover:text-white">✕</button>
          </div>
        }

        <!-- Kanban Board Columns -->
        <div class="flex-1 overflow-x-auto p-6 flex gap-4 items-start select-none">
          @for (column of columns; track column.status) {
            <div class="w-80 shrink-0 bg-slate-900/60 border border-slate-800/80 rounded-xl flex flex-col max-h-full">
              <!-- Column Header -->
              <div class="p-3.5 border-b border-slate-800/80 flex items-center justify-between">
                <div class="flex items-center gap-2">
                  <span class="w-2.5 h-2.5 rounded-full" [ngClass]="column.dotColor"></span>
                  <span class="text-xs font-semibold text-slate-200 tracking-wide">{{ column.label }}</span>
                  <span class="text-[10px] font-mono px-1.5 py-0.5 rounded-full bg-slate-800 text-slate-400">
                    {{ getColumnTasks(column.status).length }}
                  </span>
                </div>
                <button
                  (click)="openCreateInColumn(column.status)"
                  class="text-slate-500 hover:text-indigo-400 text-xs transition"
                  title="Add task to column">
                  +
                </button>
              </div>

              <!-- Column Task List -->
              <div class="p-2.5 overflow-y-auto space-y-2.5 flex-1 min-h-[300px]">
                @for (task of getColumnTasks(column.status); track task.id) {
                  <div
                    [attr.data-testid]="'task-card-' + task.id"
                    class="bg-slate-950/80 hover:bg-slate-900 border border-slate-800 hover:border-slate-700 p-3.5 rounded-lg shadow-sm transition group cursor-pointer">
                    <!-- Title & Priority -->
                    <div class="flex items-start justify-between gap-2 mb-1.5">
                      <h4 class="text-xs font-semibold text-slate-100 group-hover:text-indigo-300 leading-snug">
                        {{ task.title }}
                      </h4>
                      <span
                        class="text-[9px] font-semibold uppercase px-1.5 py-0.5 rounded shrink-0 border"
                        [ngClass]="getPriorityBadge(task.priority)">
                        {{ task.priority }}
                      </span>
                    </div>

                    <!-- Description Snippet -->
                    <p class="text-[11px] text-slate-400 line-clamp-2 mb-3 leading-relaxed">
                      {{ task.description }}
                    </p>

                    <!-- Subtask Progress -->
                    @if (task.subtasksTotal > 0) {
                      <div class="mb-3">
                        <div class="flex items-center justify-between text-[10px] text-slate-400 mb-1 font-mono">
                          <span>Subtasks</span>
                          <span>{{ task.subtasksCompleted }}/{{ task.subtasksTotal }}</span>
                        </div>
                        <div class="w-full bg-slate-800 h-1.5 rounded-full overflow-hidden">
                          <div
                            class="bg-indigo-500 h-full rounded-full transition-all duration-300"
                            [style.width.%]="(task.subtasksCompleted / task.subtasksTotal) * 100"></div>
                        </div>
                      </div>
                    }

                    <!-- Footer: Assignee, Due Date & Quick Move -->
                    <div class="flex items-center justify-between pt-2 border-t border-slate-800/80 text-[10px] text-slate-400">
                      <div class="flex items-center gap-1.5">
                        <span class="w-5 h-5 rounded-full bg-indigo-600/40 border border-indigo-500/40 text-indigo-200 flex items-center justify-center font-bold text-[9px]">
                          {{ task.assigneeAvatar }}
                        </span>
                        <span class="truncate max-w-[90px] text-slate-300">{{ task.assigneeName }}</span>
                      </div>

                      <!-- Optimistic Move Actions -->
                      <div class="flex items-center gap-1">
                        @if (column.status !== 'Backlog') {
                          <button
                            (click)="moveTaskLeft(task); $event.stopPropagation()"
                            class="px-1.5 py-0.5 rounded bg-slate-800 hover:bg-slate-700 text-slate-300 hover:text-white transition"
                            title="Move Back">
                            ←
                          </button>
                        }
                        @if (column.status !== 'Done') {
                          <button
                            (click)="moveTaskRight(task); $event.stopPropagation()"
                            class="px-1.5 py-0.5 rounded bg-indigo-600/30 hover:bg-indigo-600 text-indigo-300 hover:text-white transition"
                            title="Move Forward">
                            →
                          </button>
                        }
                      </div>
                    </div>
                  </div>
                } @empty {
                  <div class="py-8 text-center text-xs text-slate-600 italic">
                    No tasks in this lane
                  </div>
                }
              </div>
            </div>
          }
        </div>

        <!-- Create Task Modal -->
        @if (showCreateModal()) {
          <div class="fixed inset-0 bg-black/60 backdrop-blur-sm z-50 flex items-center justify-center p-4">
            <div class="bg-slate-900 border border-slate-800 rounded-xl max-w-lg w-full p-6 shadow-2xl">
              <div class="flex items-center justify-between pb-3 mb-4 border-b border-slate-800">
                <h3 class="text-sm font-bold text-white flex items-center gap-2">
                  ✨ Create Sprint Task
                </h3>
                <button (click)="showCreateModal.set(false)" class="text-slate-400 hover:text-white text-sm">✕</button>
              </div>

              <div class="space-y-4">
                <div>
                  <label class="block text-xs text-slate-300 mb-1">Task Title *</label>
                  <input
                    type="text"
                    [(ngModel)]="newTaskTitle"
                    placeholder="e.g. Ingest Customer Data Catalog"
                    class="w-full bg-slate-950 border border-slate-700 rounded-lg px-3 py-2 text-xs text-white placeholder-slate-500 focus:outline-none focus:border-indigo-500" />
                </div>

                <div>
                  <label class="block text-xs text-slate-300 mb-1">Description</label>
                  <textarea
                    [(ngModel)]="newTaskDescription"
                    rows="3"
                    placeholder="Provide acceptance criteria and details..."
                    class="w-full bg-slate-950 border border-slate-700 rounded-lg px-3 py-2 text-xs text-white placeholder-slate-500 focus:outline-none focus:border-indigo-500"></textarea>
                </div>

                <div class="grid grid-cols-2 gap-4">
                  <div>
                    <label class="block text-xs text-slate-300 mb-1">Initial Status</label>
                    <select
                      [(ngModel)]="newTaskStatus"
                      class="w-full bg-slate-950 border border-slate-700 rounded-lg px-3 py-2 text-xs text-white focus:outline-none focus:border-indigo-500">
                      <option value="Backlog">Backlog</option>
                      <option value="Todo">Todo</option>
                      <option value="InProgress">In Progress</option>
                      <option value="InReview">In Review</option>
                      <option value="Done">Done</option>
                    </select>
                  </div>

                  <div>
                    <label class="block text-xs text-slate-300 mb-1">Priority</label>
                    <select
                      [(ngModel)]="newTaskPriority"
                      class="w-full bg-slate-950 border border-slate-700 rounded-lg px-3 py-2 text-xs text-white focus:outline-none focus:border-indigo-500">
                      <option value="Urgent">Urgent</option>
                      <option value="High">High</option>
                      <option value="Medium">Medium</option>
                      <option value="Low">Low</option>
                    </select>
                  </div>
                </div>

                <div class="grid grid-cols-2 gap-4">
                  <div>
                    <label class="block text-xs text-slate-300 mb-1">Assignee</label>
                    <input
                      type="text"
                      [(ngModel)]="newTaskAssignee"
                      placeholder="e.g. Sarah Jenkins"
                      class="w-full bg-slate-950 border border-slate-700 rounded-lg px-3 py-2 text-xs text-white placeholder-slate-500 focus:outline-none focus:border-indigo-500" />
                  </div>

                  <div>
                    <label class="block text-xs text-slate-300 mb-1">Subtasks (Completed / Total)</label>
                    <div class="flex items-center gap-2">
                      <input
                        type="number"
                        [(ngModel)]="newTaskSubtasksCompleted"
                        min="0"
                        class="w-1/2 bg-slate-950 border border-slate-700 rounded-lg px-3 py-2 text-xs text-white focus:outline-none focus:border-indigo-500" />
                      <span class="text-slate-500">/</span>
                      <input
                        type="number"
                        [(ngModel)]="newTaskSubtasksTotal"
                        min="0"
                        class="w-1/2 bg-slate-950 border border-slate-700 rounded-lg px-3 py-2 text-xs text-white focus:outline-none focus:border-indigo-500" />
                    </div>
                  </div>
                </div>
              </div>

              <div class="mt-6 flex items-center justify-end gap-3 pt-4 border-t border-slate-800">
                <button
                  (click)="showCreateModal.set(false)"
                  class="px-4 py-2 rounded-lg bg-slate-800 hover:bg-slate-700 text-slate-300 text-xs font-medium transition">
                  Cancel
                </button>
                <button
                  (click)="submitCreateTask()"
                  class="px-4 py-2 rounded-lg bg-indigo-600 hover:bg-indigo-500 text-white text-xs font-semibold transition">
                  Create Task
                </button>
              </div>
            </div>
          </div>
        }
      </main>
    </div>
  `
})
export class ProjectTaskBoardComponent {
  translation = inject(TranslationService);

  readonly columns: { status: TaskStatus; label: string; dotColor: string }[] = [
    { status: 'Backlog', label: 'Backlog', dotColor: 'bg-slate-500' },
    { status: 'Todo', label: 'To Do', dotColor: 'bg-amber-400' },
    { status: 'InProgress', label: 'In Progress', dotColor: 'bg-indigo-500' },
    { status: 'InReview', label: 'In Review', dotColor: 'bg-purple-500' },
    { status: 'Done', label: 'Done', dotColor: 'bg-emerald-500' }
  ];

  projects = signal<ProjectHeader[]>([
    {
      id: 'proj-onboarding-1',
      name: 'Client Onboarding: Enterprise Retail Corp',
      description: 'Post-close onboarding sprint with milestone task templates.',
      status: 'Active',
      dealId: 'deal-won-101',
      targetLaunchDate: 'Nov 28, 2026'
    },
    {
      id: 'proj-deployment-2',
      name: 'Deployment Sprint: MENA Logistics MSA',
      description: 'Enterprise contract delivery & multi-carrier gateway deployment.',
      status: 'Active',
      contractId: 'contract-sealed-202',
      targetLaunchDate: 'Dec 15, 2026'
    }
  ]);

  selectedProjectId = signal<string>('proj-onboarding-1');

  tasks = signal<KanbanTask[]>([
    {
      id: 'task-1',
      projectId: 'proj-onboarding-1',
      title: 'Kickoff & Stakeholder Requirements Discovery',
      description: 'Align key stakeholders, establish delivery milestones, and sign off on project roadmap.',
      status: 'Done',
      priority: 'Urgent',
      assigneeName: 'Sarah Jenkins',
      assigneeAvatar: 'SJ',
      position: 1,
      subtasksCompleted: 3,
      subtasksTotal: 3,
      dueDate: 'Nov 02',
      commentCount: 4
    },
    {
      id: 'task-2',
      projectId: 'proj-onboarding-1',
      title: 'Technical Infrastructure & API Keys Provisioning',
      description: 'Configure tenant sub-account, Webhook HMAC secrets, and WhatsApp Business API gateways.',
      status: 'InProgress',
      priority: 'High',
      assigneeName: 'Alex Rivera',
      assigneeAvatar: 'AR',
      position: 2,
      subtasksCompleted: 2,
      subtasksTotal: 4,
      dueDate: 'Nov 08',
      commentCount: 2
    },
    {
      id: 'task-3',
      projectId: 'proj-onboarding-1',
      title: 'Catalog Data Ingestion & Warehouse Stock Seeding',
      description: 'Import SKUs, multi-warehouse stock allocations, and carrier rate cards.',
      status: 'Todo',
      priority: 'Medium',
      assigneeName: 'Liam Chen',
      assigneeAvatar: 'LC',
      position: 3,
      subtasksCompleted: 1,
      subtasksTotal: 5,
      dueDate: 'Nov 14',
      commentCount: 0
    },
    {
      id: 'task-4',
      projectId: 'proj-onboarding-1',
      title: 'Operator Training & Multi-Carrier Simulation',
      description: 'Conduct training workshops for SDRs, warehouse packers, and dispatch drivers.',
      status: 'Backlog',
      priority: 'High',
      assigneeName: 'Emma Watson',
      assigneeAvatar: 'EW',
      position: 4,
      subtasksCompleted: 0,
      subtasksTotal: 3,
      dueDate: 'Nov 20',
      commentCount: 1
    },
    {
      id: 'task-5',
      projectId: 'proj-onboarding-1',
      title: 'Production Go-Live & Post-Launch Sign-Off',
      description: 'Execute production traffic cutover, verify live COD reconciliation, and issue sign-off.',
      status: 'Backlog',
      priority: 'Urgent',
      assigneeName: 'Sarah Jenkins',
      assigneeAvatar: 'SJ',
      position: 5,
      subtasksCompleted: 0,
      subtasksTotal: 2,
      dueDate: 'Nov 28',
      commentCount: 0
    }
  ]);

  searchQuery = '';
  selectedPriorityFilter = 'ALL';
  toastMessage = signal<string>('');
  showCreateModal = signal<boolean>(false);

  // Form inputs
  newTaskTitle = '';
  newTaskDescription = '';
  newTaskStatus: TaskStatus = 'Todo';
  newTaskPriority: TaskPriority = 'Medium';
  newTaskAssignee = 'Sarah Jenkins';
  newTaskSubtasksCompleted = 0;
  newTaskSubtasksTotal = 3;

  activeProject = computed(() => {
    return this.projects().find(p => p.id === this.selectedProjectId());
  });

  doneCount = computed(() => {
    return this.tasks().filter(t => t.status === 'Done').length;
  });

  completionRate = computed(() => {
    const total = this.tasks().length;
    if (total === 0) return 0;
    return Math.round((this.doneCount() / total) * 100);
  });

  switchProject(projectId: string): void {
    this.selectedProjectId.set(projectId);
  }

  getColumnTasks(status: TaskStatus): KanbanTask[] {
    return this.tasks().filter(t => {
      const matchesStatus = t.status === status;
      const matchesProject = t.projectId === this.selectedProjectId();
      const matchesSearch = !this.searchQuery ||
        t.title.toLowerCase().includes(this.searchQuery.toLowerCase()) ||
        t.assigneeName.toLowerCase().includes(this.searchQuery.toLowerCase());
      const matchesPriority = this.selectedPriorityFilter === 'ALL' || t.priority === this.selectedPriorityFilter;

      return matchesStatus && matchesProject && matchesSearch && matchesPriority;
    });
  }

  getPriorityBadge(priority: TaskPriority): string {
    switch (priority) {
      case 'Urgent':
        return 'bg-red-500/20 text-red-300 border-red-500/30';
      case 'High':
        return 'bg-orange-500/20 text-orange-300 border-orange-500/30';
      case 'Medium':
        return 'bg-blue-500/20 text-blue-300 border-blue-500/30';
      case 'Low':
        return 'bg-slate-500/20 text-slate-300 border-slate-500/30';
    }
  }

  moveTaskLeft(task: KanbanTask): void {
    const statusOrder: TaskStatus[] = ['Backlog', 'Todo', 'InProgress', 'InReview', 'Done'];
    const currentIndex = statusOrder.indexOf(task.status);
    if (currentIndex > 0) {
      const prevStatus = statusOrder[currentIndex - 1];
      this.updateTaskStatusOptimistic(task.id, prevStatus);
    }
  }

  moveTaskRight(task: KanbanTask): void {
    const statusOrder: TaskStatus[] = ['Backlog', 'Todo', 'InProgress', 'InReview', 'Done'];
    const currentIndex = statusOrder.indexOf(task.status);
    if (currentIndex < statusOrder.length - 1) {
      const nextStatus = statusOrder[currentIndex + 1];
      this.updateTaskStatusOptimistic(task.id, nextStatus);
    }
  }

  updateTaskStatusOptimistic(taskId: string, newStatus: TaskStatus): void {
    this.tasks.update(current =>
      current.map(t => (t.id === taskId ? { ...t, status: newStatus } : t))
    );
    this.toastMessage.set(`Task moved to ${newStatus}`);
  }

  openCreateInColumn(status: TaskStatus): void {
    this.newTaskStatus = status;
    this.showCreateModal.set(true);
  }

  submitCreateTask(): void {
    if (!this.newTaskTitle.trim()) return;

    const initials = this.newTaskAssignee
      .split(' ')
      .map(part => part[0])
      .join('')
      .toUpperCase()
      .slice(0, 2) || 'OP';

    const newTask: KanbanTask = {
      id: `task-${Date.now()}`,
      projectId: this.selectedProjectId(),
      title: this.newTaskTitle.trim(),
      description: this.newTaskDescription.trim(),
      status: this.newTaskStatus,
      priority: this.newTaskPriority,
      assigneeName: this.newTaskAssignee.trim() || 'Unassigned',
      assigneeAvatar: initials,
      position: this.tasks().length + 1,
      subtasksCompleted: Number(this.newTaskSubtasksCompleted),
      subtasksTotal: Number(this.newTaskSubtasksTotal),
      dueDate: 'Dec 05',
      commentCount: 0
    };

    this.tasks.update(current => [...current, newTask]);
    this.showCreateModal.set(false);
    this.toastMessage.set(`Task "${newTask.title}" created successfully!`);

    // Reset inputs
    this.newTaskTitle = '';
    this.newTaskDescription = '';
  }

  convertWonDealDemo(): void {
    const newProjId = `proj-deal-${Date.now()}`;
    const newProject: ProjectHeader = {
      id: newProjId,
      name: `Client Onboarding: Global FMCG Won Deal #${Math.floor(1000 + Math.random() * 9000)}`,
      description: 'Automated post-close client onboarding sprint generated from Won Deal.',
      status: 'Active',
      dealId: `deal-${Date.now()}`,
      targetLaunchDate: 'Dec 25, 2026'
    };

    this.projects.update(current => [newProject, ...current]);
    this.selectedProjectId.set(newProjId);

    const generatedTasks: KanbanTask[] = [
      {
        id: `task-gen-1-${Date.now()}`,
        projectId: newProjId,
        title: 'Milestone 1: Client Kickoff & Scope Validation',
        description: 'Review contract SLA terms and introduce implementation lead.',
        status: 'Todo',
        priority: 'Urgent',
        assigneeName: 'Sarah Jenkins',
        assigneeAvatar: 'SJ',
        position: 1,
        subtasksCompleted: 0,
        subtasksTotal: 3,
        dueDate: 'Dec 02',
        commentCount: 0
      },
      {
        id: `task-gen-2-${Date.now()}`,
        projectId: newProjId,
        title: 'Milestone 2: Multi-Tenant Workspace & Secrets Setup',
        description: 'Generate tenant schema, API keys, and secure vault entries.',
        status: 'Backlog',
        priority: 'High',
        assigneeName: 'Alex Rivera',
        assigneeAvatar: 'AR',
        position: 2,
        subtasksCompleted: 0,
        subtasksTotal: 4,
        dueDate: 'Dec 08',
        commentCount: 0
      },
      {
        id: `task-gen-3-${Date.now()}`,
        projectId: newProjId,
        title: 'Milestone 3: Warehouse & SKU Inventory Migration',
        description: 'Seed catalog data and configure regional fulfillment hubs.',
        status: 'Backlog',
        priority: 'Medium',
        assigneeName: 'Liam Chen',
        assigneeAvatar: 'LC',
        position: 3,
        subtasksCompleted: 0,
        subtasksTotal: 5,
        dueDate: 'Dec 16',
        commentCount: 0
      }
    ];

    this.tasks.update(current => [...current, ...generatedTasks]);
    this.toastMessage.set(`Won Deal converted into "${newProject.name}" with milestone templates!`);
  }
}
