import { Component, OnInit, signal, computed, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { HttpClient } from '@angular/common/http';

export type FormFieldType =
  | 'Text'
  | 'Email'
  | 'Tel'
  | 'Number'
  | 'Select'
  | 'MultiSelect'
  | 'Checkbox'
  | 'Textarea'
  | 'Rating'
  | 'FileUpload';

export type FormTargetAction = 'CreateLead' | 'CreateDeal' | 'CreateSupportTicket';

export interface FormFieldDefinition {
  id: string;
  name: string;
  label: string;
  type: FormFieldType;
  placeholder?: string;
  isRequired: boolean;
  validationRegex?: string;
  options: string[];
  min?: number;
  max?: number;
  step: number;
  defaultValue?: string;
  helpText?: string;
}

export interface FormMetrics {
  impressions: number;
  submissions: number;
  spamRejections: number;
  averageCompletionTimeSeconds: number;
  conversionRatePercentage: number;
}

export interface HeadlessFormDefinition {
  id: string;
  tenantId: string;
  title: string;
  slug: string;
  description?: string;
  fields: FormFieldDefinition[];
  targetAction: FormTargetAction;
  webhookUrl?: string;
  webhookSecret?: string;
  isPublished: boolean;
  isArchived: boolean;
  requireTurnstile: boolean;
  rateLimitPerMinutePerIp: number;
  velocityThrottlePerMinute: number;
  createdAtUtc: string;
  metrics: FormMetrics;
}

export interface CrmContactMapping {
  email?: string;
  phone?: string;
  firstName?: string;
  lastName?: string;
  company?: string;
  customProperties?: Record<string, any>;
}

export interface CrmPipelineRecord {
  recordId: string;
  action: FormTargetAction;
  title: string;
  status: string;
  contact: CrmContactMapping;
  createdAtUtc: string;
  details?: Record<string, any>;
}

export interface HeadlessFormSubmission {
  id: string;
  formId: string;
  tenantId: string;
  submittedAtUtc: string;
  clientIp: string;
  userAgent?: string;
  answers: Record<string, any>;
  crmRecord?: CrmPipelineRecord;
  isSpam: boolean;
  spamScore: number;
  spamReason?: string;
  completionTimeSeconds: number;
  webhookDeliveryStatus?: string;
  webhookSignature?: string;
  webhookPayloadJson?: string;
}

@Component({
  selector: 'app-headless-form-builder',
  standalone: true,
  imports: [CommonModule, FormsModule],
  template: `
    <div class="h-full flex flex-col bg-slate-950 text-slate-100 overflow-y-auto" dir="ltr">
      <!-- Top Bar / Header -->
      <header class="p-6 border-b border-slate-800 bg-slate-900/40 backdrop-blur flex flex-col lg:flex-row lg:items-center justify-between gap-4">
        <div>
          <div class="flex items-center gap-3">
            <div class="p-2.5 rounded-xl bg-gradient-to-tr from-amber-600 to-indigo-600 text-white shadow-lg shadow-indigo-900/30">
              <svg class="w-6 h-6" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M9 12h6m-6 4h6m2 5H7a2 2 0 01-2-2V5a2 2 0 012-2h5.586a1 1 0 01.707.293l5.414 5.414a1 1 0 01.293.707V19a2 2 0 01-2 2z" />
              </svg>
            </div>
            <div>
              <div class="flex items-center gap-2">
                <h1 class="text-xl font-bold tracking-tight text-white">Horizon 54: Headless Form Engine</h1>
                <span class="px-2.5 py-0.5 rounded-full text-xs font-mono bg-indigo-500/10 text-indigo-400 border border-indigo-500/20">
                  Turnstile Anti-Spam Pipeline
                </span>
                <span class="px-2 py-0.5 rounded text-[11px] font-semibold bg-emerald-500/10 text-emerald-400 border border-emerald-500/20">
                  CRM Bridge Active
                </span>
              </div>
              <p class="text-xs text-slate-400 mt-1">
                Schema-driven headless forms with Cloudflare Turnstile token verification, disposable email blocking, multi-step navigation, and outbound HMAC webhooks.
              </p>
            </div>
          </div>
        </div>

        <!-- Controls: Form Selector, Embed Modal Button & Navigation -->
        <div class="flex flex-wrap items-center gap-2">
          <!-- Active Form Selector -->
          <select
            [ngModel]="selectedFormId()"
            (ngModelChange)="selectForm($event)"
            class="bg-slate-900 border border-slate-700 text-slate-200 text-xs rounded-lg px-3 py-2 font-medium focus:outline-none focus:ring-2 focus:ring-indigo-500">
            @for (f of forms(); track f.id) {
              <option [value]="f.id">{{ f.title }} ({{ f.fields.length }} fields)</option>
            }
          </select>

          <!-- Embed Code Button -->
          <button
            (click)="showEmbedModal.set(true)"
            class="px-3.5 py-2 rounded-lg bg-slate-800 hover:bg-slate-700 text-slate-200 text-xs font-semibold flex items-center gap-1.5 border border-slate-700 transition">
            <svg class="w-4 h-4 text-amber-400" fill="none" viewBox="0 0 24 24" stroke="currentColor">
              <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M10 20l4-16m4 4l4 4-4 4M6 16l-4-4 4-4" />
            </svg>
            Embed Snippets
          </button>

          <!-- Save / New Form -->
          <button
            (click)="createNewForm()"
            class="px-3.5 py-2 rounded-lg bg-indigo-600 hover:bg-indigo-500 text-white text-xs font-semibold flex items-center gap-1.5 shadow-md shadow-indigo-600/20 transition">
            <svg class="w-4 h-4" fill="none" viewBox="0 0 24 24" stroke="currentColor">
              <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M12 4v16m8-8H4" />
            </svg>
            New Form
          </button>
        </div>
      </header>

      <!-- View Tabs -->
      <div class="px-6 py-2.5 bg-slate-900/60 border-b border-slate-800/80 flex items-center justify-between">
        <div class="flex items-center gap-2">
          <button
            (click)="activeView.set('builder')"
            [class.bg-indigo-600]="activeView() === 'builder'"
            [class.text-white]="activeView() === 'builder'"
            [class.text-slate-400]="activeView() !== 'builder'"
            class="px-3.5 py-1.5 rounded-lg text-xs font-semibold hover:text-white transition flex items-center gap-1.5">
            <svg class="w-3.5 h-3.5" fill="none" viewBox="0 0 24 24" stroke="currentColor">
              <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M11 5H6a2 2 0 00-2 2v11a2 2 0 002 2h11a2 2 0 002-2v-5m-1.414-9.414a2 2 0 112.828 2.828L11.828 15H9v-2.828l8.586-8.586z" />
            </svg>
            Visual Form Canvas
          </button>

          <button
            (click)="activeView.set('preview')"
            [class.bg-indigo-600]="activeView() === 'preview'"
            [class.text-white]="activeView() === 'preview'"
            [class.text-slate-400]="activeView() !== 'preview'"
            class="px-3.5 py-1.5 rounded-lg text-xs font-semibold hover:text-white transition flex items-center gap-1.5">
            <svg class="w-3.5 h-3.5 text-amber-400" fill="none" viewBox="0 0 24 24" stroke="currentColor">
              <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M15 12a3 3 0 11-6 0 3 3 0 016 0z" />
              <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M2.458 12C3.732 7.943 7.523 5 12 5c4.478 0 8.268 2.943 9.542 7-1.274 4.057-5.064 7-9.542 7-4.477 0-8.268-2.943-9.542-7z" />
            </svg>
            Live Preview & Turnstile Test
          </button>

          <button
            (click)="activeView.set('submissions')"
            [class.bg-indigo-600]="activeView() === 'submissions'"
            [class.text-white]="activeView() === 'submissions'"
            [class.text-slate-400]="activeView() !== 'submissions'"
            class="px-3.5 py-1.5 rounded-lg text-xs font-semibold hover:text-white transition flex items-center gap-1.5">
            <svg class="w-3.5 h-3.5 text-emerald-400" fill="none" viewBox="0 0 24 24" stroke="currentColor">
              <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M9 5H7a2 2 0 00-2 2v12a2 2 0 002 2h10a2 2 0 002-2V7a2 2 0 00-2-2h-2M9 5a2 2 0 002 2h2a2 2 0 002-2M9 5a2 2 0 012-2h2a2 2 0 012 2" />
            </svg>
            Submissions & CRM Leads ({{ submissions().length }})
          </button>
        </div>

        <!-- Target Action Tag -->
        @if (currentForm(); as form) {
          <div class="hidden md:flex items-center gap-2 text-xs text-slate-400">
            <span>CRM Action:</span>
            <span class="px-2 py-0.5 rounded bg-slate-800 text-indigo-300 font-mono text-[11px] border border-slate-700">
              {{ form.targetAction }}
            </span>
            <span class="text-slate-600">•</span>
            <span>Slug:</span>
            <span class="text-slate-300 font-mono text-[11px]">/f/{{ form.slug }}</span>
          </div>
        }
      </div>

      <!-- Performance Metrics Cards -->
      <div class="p-6 grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-5 gap-4">
        <!-- Views -->
        <div class="p-4 rounded-xl bg-slate-900/70 border border-slate-800 flex items-center gap-4">
          <div class="p-3 rounded-lg bg-blue-500/10 text-blue-400 border border-blue-500/20">
            <svg class="w-5 h-5" fill="none" viewBox="0 0 24 24" stroke="currentColor">
              <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M15 12a3 3 0 11-6 0 3 3 0 016 0z" />
              <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M2.458 12C3.732 7.943 7.523 5 12 5c4.478 0 8.268 2.943 9.542 7-1.274 4.057-5.064 7-9.542 7-4.477 0-8.268-2.943-9.542-7z" />
            </svg>
          </div>
          <div>
            <div class="text-xs text-slate-400 font-medium">Total Views</div>
            <div class="text-xl font-bold text-white mt-0.5">{{ totalViews() | number }}</div>
          </div>
        </div>

        <!-- Submissions -->
        <div class="p-4 rounded-xl bg-slate-900/70 border border-slate-800 flex items-center gap-4">
          <div class="p-3 rounded-lg bg-emerald-500/10 text-emerald-400 border border-emerald-500/20">
            <svg class="w-5 h-5" fill="none" viewBox="0 0 24 24" stroke="currentColor">
              <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M9 12l2 2 4-4m6 2a9 9 0 11-18 0 9 9 0 0118 0z" />
            </svg>
          </div>
          <div>
            <div class="text-xs text-slate-400 font-medium">Submissions</div>
            <div class="text-xl font-bold text-emerald-400 mt-0.5">{{ totalSubmissions() | number }}</div>
          </div>
        </div>

        <!-- Conversion Rate -->
        <div class="p-4 rounded-xl bg-slate-900/70 border border-slate-800 flex items-center gap-4">
          <div class="p-3 rounded-lg bg-indigo-500/10 text-indigo-400 border border-indigo-500/20">
            <svg class="w-5 h-5" fill="none" viewBox="0 0 24 24" stroke="currentColor">
              <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M13 7h8m0 0v8m0-8l-8 8-4-4-6 6" />
            </svg>
          </div>
          <div>
            <div class="text-xs text-slate-400 font-medium">Conversion Rate</div>
            <div class="text-xl font-bold text-indigo-300 mt-0.5">{{ conversionRate() }}%</div>
          </div>
        </div>

        <!-- Spam Blocked -->
        <div class="p-4 rounded-xl bg-slate-900/70 border border-slate-800 flex items-center gap-4">
          <div class="p-3 rounded-lg bg-rose-500/10 text-rose-400 border border-rose-500/20">
            <svg class="w-5 h-5" fill="none" viewBox="0 0 24 24" stroke="currentColor">
              <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M12 9v2m0 4h.01m-6.938 4h13.856c1.54 0 2.502-1.667 1.732-3L13.732 4c-.77-1.333-2.694-1.333-3.464 0L3.34 16c-.77 1.333.192 3 1.732 3z" />
            </svg>
          </div>
          <div>
            <div class="text-xs text-slate-400 font-medium">Spam Blocked</div>
            <div class="text-xl font-bold text-rose-400 mt-0.5">{{ spamBlocked() | number }}</div>
          </div>
        </div>

        <!-- Avg Completion Time -->
        <div class="p-4 rounded-xl bg-slate-900/70 border border-slate-800 flex items-center gap-4">
          <div class="p-3 rounded-lg bg-amber-500/10 text-amber-400 border border-amber-500/20">
            <svg class="w-5 h-5" fill="none" viewBox="0 0 24 24" stroke="currentColor">
              <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M12 8v4l3 3m6-3a9 9 0 11-18 0 9 9 0 0118 0z" />
            </svg>
          </div>
          <div>
            <div class="text-xs text-slate-400 font-medium">Avg Completion</div>
            <div class="text-xl font-bold text-amber-300 mt-0.5">{{ avgCompletionTime() }}s</div>
          </div>
        </div>
      </div>

      <!-- Main Body per View -->
      <main class="flex-1 px-6 pb-12">
        <!-- 1. Visual Form Canvas View -->
        @if (activeView() === 'builder') {
          @if (currentForm(); as form) {
            <div class="grid grid-cols-1 lg:grid-cols-3 gap-6">
              <!-- Left 2 Cols: Form Canvas / Field List -->
              <div class="lg:col-span-2 space-y-4">
                <!-- Form Settings Bar -->
                <div class="p-4 rounded-xl bg-slate-900/60 border border-slate-800 flex flex-wrap items-center justify-between gap-4">
                  <div class="flex-1 min-w-[200px]">
                    <label class="text-[11px] font-semibold text-slate-400 uppercase tracking-wider block mb-1">Form Title</label>
                    <input
                      type="text"
                      [ngModel]="form.title"
                      (ngModelChange)="updateFormTitle($event)"
                      class="w-full bg-slate-950 border border-slate-700 rounded-lg px-3 py-1.5 text-sm text-white focus:outline-none focus:ring-2 focus:ring-indigo-500" />
                  </div>

                  <div class="w-44">
                    <label class="text-[11px] font-semibold text-slate-400 uppercase tracking-wider block mb-1">CRM Target Action</label>
                    <select
                      [ngModel]="form.targetAction"
                      (ngModelChange)="updateFormTargetAction($event)"
                      class="w-full bg-slate-950 border border-slate-700 rounded-lg px-3 py-1.5 text-xs text-slate-200 focus:outline-none focus:ring-2 focus:ring-indigo-500">
                      <option value="CreateLead">Create Lead</option>
                      <option value="CreateDeal">Create Deal</option>
                      <option value="CreateSupportTicket">Create Support Ticket</option>
                    </select>
                  </div>

                  <div class="flex items-center gap-4 pt-4 sm:pt-0">
                    <label class="flex items-center gap-2 cursor-pointer text-xs text-slate-300">
                      <input
                        type="checkbox"
                        [ngModel]="form.requireTurnstile"
                        (ngModelChange)="updateRequireTurnstile($event)"
                        class="rounded bg-slate-950 border-slate-700 text-indigo-600 focus:ring-indigo-500" />
                      <span>Turnstile Guard</span>
                    </label>
                  </div>
                </div>

                <!-- Field Cards Canvas -->
                <div class="space-y-3">
                  <div class="flex items-center justify-between text-xs text-slate-400 font-semibold uppercase tracking-wider px-1">
                    <span>Fields Canvas ({{ form.fields.length }})</span>
                    <button
                      (click)="addNewField()"
                      class="px-2.5 py-1 rounded bg-indigo-600/20 text-indigo-400 hover:bg-indigo-600/30 border border-indigo-500/30 transition flex items-center gap-1 font-normal capitalize">
                      <svg class="w-3.5 h-3.5" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                        <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M12 4v16m8-8H4" />
                      </svg>
                      Add Field
                    </button>
                  </div>

                  @for (field of form.fields; track field.id; let i = $index) {
                    <div
                      [class.border-indigo-500]="selectedFieldId() === field.id"
                      [class.border-slate-800]="selectedFieldId() !== field.id"
                      [class.bg-slate-900]="selectedFieldId() === field.id"
                      [class.bg-slate-900/60]="selectedFieldId() !== field.id"
                      (click)="selectedFieldId.set(field.id)"
                      class="p-4 rounded-xl border transition cursor-pointer hover:border-slate-700 flex items-start gap-4">
                      <!-- Reorder Handles -->
                      <div class="flex flex-col gap-1 pt-0.5 text-slate-500">
                        <button
                          (click)="moveFieldUp(i, $event)"
                          [disabled]="i === 0"
                          class="p-1 rounded hover:bg-slate-800 hover:text-white disabled:opacity-30 disabled:hover:bg-transparent">
                          <svg class="w-3.5 h-3.5" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                            <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M5 15l7-7 7 7" />
                          </svg>
                        </button>
                        <button
                          (click)="moveFieldDown(i, $event)"
                          [disabled]="i === form.fields.length - 1"
                          class="p-1 rounded hover:bg-slate-800 hover:text-white disabled:opacity-30 disabled:hover:bg-transparent">
                          <svg class="w-3.5 h-3.5" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                            <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M19 9l-7 7-7-7" />
                          </svg>
                        </button>
                      </div>

                      <!-- Field Info -->
                      <div class="flex-1 min-w-0">
                        <div class="flex items-center gap-2">
                          <span class="text-sm font-semibold text-white truncate">{{ field.label || 'Untitled Field' }}</span>
                          <span class="px-2 py-0.5 rounded text-[10px] font-mono bg-slate-800 text-slate-300 border border-slate-700">
                            {{ field.type }}
                          </span>
                          @if (field.isRequired) {
                            <span class="px-1.5 py-0.2 rounded text-[10px] font-semibold bg-rose-500/10 text-rose-400 border border-rose-500/20">
                              Required
                            </span>
                          }
                          <span class="text-[11px] font-mono text-slate-500">Step {{ field.step }}</span>
                        </div>
                        <div class="text-xs text-slate-400 mt-1 flex items-center gap-4">
                          <span>Key: <code class="text-indigo-300 font-mono">{{ field.name }}</code></span>
                          @if (field.placeholder) {
                            <span class="text-slate-500 truncate">Placeholder: "{{ field.placeholder }}"</span>
                          }
                        </div>
                      </div>

                      <!-- Delete Button -->
                      <button
                        (click)="deleteField(i, $event)"
                        class="text-slate-500 hover:text-rose-400 p-1.5 rounded-lg hover:bg-rose-500/10 transition">
                        <svg class="w-4 h-4" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                          <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M19 7l-.867 12.142A2 2 0 0116.138 21H7.862a2 2 0 01-1.995-1.858L5 7m5 4v6m4-6v6m1-10V4a1 1 0 00-1-1h-4a1 1 0 00-1 1v3M4 7h16" />
                        </svg>
                      </button>
                    </div>
                  }
                </div>
              </div>

              <!-- Right Col: Field Property Inspector -->
              <div class="space-y-4">
                <div class="p-5 rounded-xl bg-slate-900 border border-slate-800 sticky top-6">
                  <div class="flex items-center justify-between pb-3 border-b border-slate-800 mb-4">
                    <h3 class="text-xs font-bold uppercase tracking-wider text-slate-300 flex items-center gap-1.5">
                      <svg class="w-4 h-4 text-indigo-400" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                        <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M12 6V4m0 2a2 2 0 100 4m0-4a2 2 0 110 4m-6 8a2 2 0 100-4m0 4a2 2 0 110-4m0 4v2m0-6V4m6 6v10m6-2a2 2 0 100-4m0 4a2 2 0 110-4m0 4v2m0-6V4" />
                      </svg>
                      Field Inspector
                    </h3>
                    @if (selectedField(); as selField) {
                      <span class="text-[11px] font-mono text-indigo-400">{{ selField.name }}</span>
                    }
                  </div>

                  @if (selectedField(); as field) {
                    <div class="space-y-4 text-xs">
                      <div>
                        <label class="text-slate-400 font-semibold block mb-1">Field Label</label>
                        <input
                          type="text"
                          [(ngModel)]="field.label"
                          class="w-full bg-slate-950 border border-slate-700 rounded-lg px-3 py-2 text-white focus:outline-none focus:ring-2 focus:ring-indigo-500" />
                      </div>

                      <div>
                        <label class="text-slate-400 font-semibold block mb-1">Field Key (Name)</label>
                        <input
                          type="text"
                          [(ngModel)]="field.name"
                          class="w-full bg-slate-950 border border-slate-700 rounded-lg px-3 py-2 text-white font-mono focus:outline-none focus:ring-2 focus:ring-indigo-500" />
                      </div>

                      <div class="grid grid-cols-2 gap-3">
                        <div>
                          <label class="text-slate-400 font-semibold block mb-1">Field Type</label>
                          <select
                            [(ngModel)]="field.type"
                            class="w-full bg-slate-950 border border-slate-700 rounded-lg px-2.5 py-2 text-slate-200 focus:outline-none focus:ring-2 focus:ring-indigo-500">
                            <option value="Text">Text</option>
                            <option value="Email">Email</option>
                            <option value="Tel">Phone (Tel)</option>
                            <option value="Number">Number</option>
                            <option value="Select">Dropdown (Select)</option>
                            <option value="MultiSelect">MultiSelect</option>
                            <option value="Checkbox">Checkbox</option>
                            <option value="Textarea">Textarea</option>
                            <option value="Rating">Rating</option>
                            <option value="FileUpload">File Upload</option>
                          </select>
                        </div>

                        <div>
                          <label class="text-slate-400 font-semibold block mb-1">Form Step</label>
                          <input
                            type="number"
                            min="1"
                            [(ngModel)]="field.step"
                            class="w-full bg-slate-950 border border-slate-700 rounded-lg px-3 py-2 text-white font-mono focus:outline-none focus:ring-2 focus:ring-indigo-500" />
                        </div>
                      </div>

                      <div>
                        <label class="text-slate-400 font-semibold block mb-1">Placeholder Text</label>
                        <input
                          type="text"
                          [(ngModel)]="field.placeholder"
                          class="w-full bg-slate-950 border border-slate-700 rounded-lg px-3 py-2 text-white focus:outline-none focus:ring-2 focus:ring-indigo-500" />
                      </div>

                      <div class="flex items-center gap-2 pt-1">
                        <input
                          type="checkbox"
                          [(ngModel)]="field.isRequired"
                          id="chk_required"
                          class="rounded bg-slate-950 border-slate-700 text-indigo-600 focus:ring-indigo-500" />
                        <label for="chk_required" class="text-slate-300 font-semibold cursor-pointer">
                          Mark field as required
                        </label>
                      </div>

                      <!-- Min & Max limits if Number/Rating/Text -->
                      @if (field.type === 'Number' || field.type === 'Rating') {
                        <div class="grid grid-cols-2 gap-3 pt-2">
                          <div>
                            <label class="text-slate-400 font-semibold block mb-1">Min Value</label>
                            <input
                              type="number"
                              [(ngModel)]="field.min"
                              class="w-full bg-slate-950 border border-slate-700 rounded-lg px-3 py-2 text-white font-mono focus:outline-none focus:ring-2 focus:ring-indigo-500" />
                          </div>
                          <div>
                            <label class="text-slate-400 font-semibold block mb-1">Max Value</label>
                            <input
                              type="number"
                              [(ngModel)]="field.max"
                              class="w-full bg-slate-950 border border-slate-700 rounded-lg px-3 py-2 text-white font-mono focus:outline-none focus:ring-2 focus:ring-indigo-500" />
                          </div>
                        </div>
                      }

                      <!-- Options for Select / MultiSelect -->
                      @if (field.type === 'Select' || field.type === 'MultiSelect') {
                        <div class="pt-2">
                          <label class="text-slate-400 font-semibold block mb-1">Options (comma-separated)</label>
                          <input
                            type="text"
                            [ngModel]="field.options.join(', ')"
                            (ngModelChange)="updateFieldOptions(field, $event)"
                            placeholder="Option 1, Option 2, Option 3"
                            class="w-full bg-slate-950 border border-slate-700 rounded-lg px-3 py-2 text-white focus:outline-none focus:ring-2 focus:ring-indigo-500" />
                        </div>
                      }

                      <!-- Validation Regex -->
                      <div class="pt-2">
                        <label class="text-slate-400 font-semibold block mb-1">Validation Regex (Optional)</label>
                        <input
                          type="text"
                          [(ngModel)]="field.validationRegex"
                          placeholder="^[A-Za-z0-9_-]{3,15}$"
                          class="w-full bg-slate-950 border border-slate-700 rounded-lg px-3 py-2 text-white font-mono text-[11px] focus:outline-none focus:ring-2 focus:ring-indigo-500" />
                      </div>
                    </div>
                  } @else {
                    <div class="text-center py-10 text-slate-500 text-xs">
                      Select a field on the canvas to configure its properties.
                    </div>
                  }
                </div>
              </div>
            </div>
          }
        }

        <!-- 2. Live Preview & Turnstile Test View -->
        @if (activeView() === 'preview') {
          @if (currentForm(); as form) {
            <div class="max-w-2xl mx-auto space-y-6">
              <!-- Simulated Browser / Viewport Container -->
              <div class="rounded-2xl border border-slate-800 bg-slate-900/90 shadow-2xl overflow-hidden">
                <!-- Window header -->
                <div class="px-4 py-3 bg-slate-950 border-b border-slate-800 flex items-center justify-between">
                  <div class="flex items-center gap-2">
                    <div class="w-3 h-3 rounded-full bg-rose-500/80"></div>
                    <div class="w-3 h-3 rounded-full bg-amber-500/80"></div>
                    <div class="w-3 h-3 rounded-full bg-emerald-500/80"></div>
                    <span class="ml-2 text-xs font-mono text-slate-400 truncate">
                      https://app.sgrowing.io/f/{{ form.slug }}
                    </span>
                  </div>
                  <span class="text-[11px] font-mono text-indigo-400 bg-indigo-500/10 px-2 py-0.5 rounded border border-indigo-500/20">
                    Interactive Simulator
                  </span>
                </div>

                <!-- Form Body -->
                <div class="p-8 space-y-6">
                  <!-- Title & Description -->
                  <div>
                    <h2 class="text-xl font-bold text-white">{{ form.title }}</h2>
                    @if (form.description) {
                      <p class="text-xs text-slate-400 mt-1">{{ form.description }}</p>
                    }
                  </div>

                  <!-- Multi-Step Progress Indicator if > 1 step -->
                  @if (maxFormStep() > 1) {
                    <div class="space-y-2 pb-2">
                      <div class="flex items-center justify-between text-xs font-semibold text-slate-400">
                        <span>Step {{ currentPreviewStep() }} of {{ maxFormStep() }}</span>
                        <span class="text-indigo-400">{{ Math.round((currentPreviewStep() / maxFormStep()) * 100) }}% Complete</span>
                      </div>
                      <div class="w-full h-1.5 bg-slate-800 rounded-full overflow-hidden">
                        <div
                          class="h-full bg-indigo-500 transition-all duration-300"
                          [style.width.%]="(currentPreviewStep() / maxFormStep()) * 100">
                        </div>
                      </div>
                    </div>
                  }

                  <!-- Fields for Current Step -->
                  <div class="space-y-4">
                    @for (field of currentStepFields(); track field.id) {
                      <div class="space-y-1.5">
                        <label class="text-xs font-semibold text-slate-300 block">
                          {{ field.label }}
                          @if (field.isRequired) {
                            <span class="text-rose-400">*</span>
                          }
                        </label>

                        <!-- Switch field types -->
                        @switch (field.type) {
                          @case ('Text') {
                            <input
                              type="text"
                              [placeholder]="field.placeholder || ''"
                              [(ngModel)]="simulatedAnswers[field.name]"
                              class="w-full bg-slate-950 border border-slate-700 rounded-lg px-3.5 py-2 text-sm text-white focus:outline-none focus:ring-2 focus:ring-indigo-500" />
                          }
                          @case ('Email') {
                            <input
                              type="email"
                              [placeholder]="field.placeholder || 'user@company.com'"
                              [(ngModel)]="simulatedAnswers[field.name]"
                              class="w-full bg-slate-950 border border-slate-700 rounded-lg px-3.5 py-2 text-sm text-white focus:outline-none focus:ring-2 focus:ring-indigo-500" />
                          }
                          @case ('Tel') {
                            <input
                              type="tel"
                              [placeholder]="field.placeholder || '+1 555-000-0000'"
                              [(ngModel)]="simulatedAnswers[field.name]"
                              class="w-full bg-slate-950 border border-slate-700 rounded-lg px-3.5 py-2 text-sm text-white focus:outline-none focus:ring-2 focus:ring-indigo-500" />
                          }
                          @case ('Number') {
                            <input
                              type="number"
                              [min]="field.min ?? null"
                              [max]="field.max ?? null"
                              [(ngModel)]="simulatedAnswers[field.name]"
                              class="w-full bg-slate-950 border border-slate-700 rounded-lg px-3.5 py-2 text-sm text-white focus:outline-none focus:ring-2 focus:ring-indigo-500" />
                          }
                          @case ('Textarea') {
                            <textarea
                              rows="3"
                              [placeholder]="field.placeholder || ''"
                              [(ngModel)]="simulatedAnswers[field.name]"
                              class="w-full bg-slate-950 border border-slate-700 rounded-lg px-3.5 py-2 text-sm text-white focus:outline-none focus:ring-2 focus:ring-indigo-500"></textarea>
                          }
                          @case ('Select') {
                            <select
                              [(ngModel)]="simulatedAnswers[field.name]"
                              class="w-full bg-slate-950 border border-slate-700 rounded-lg px-3.5 py-2 text-sm text-slate-200 focus:outline-none focus:ring-2 focus:ring-indigo-500">
                              <option value="">Select an option...</option>
                              @for (opt of field.options; track opt) {
                                <option [value]="opt">{{ opt }}</option>
                              }
                            </select>
                          }
                          @case ('MultiSelect') {
                            <div class="flex flex-wrap gap-2">
                              @for (opt of field.options; track opt) {
                                <button
                                  type="button"
                                  (click)="toggleMultiSelect(field.name, opt)"
                                  [class.bg-indigo-600]="isOptionSelected(field.name, opt)"
                                  [class.text-white]="isOptionSelected(field.name, opt)"
                                  [class.border-indigo-500]="isOptionSelected(field.name, opt)"
                                  [class.bg-slate-950]="!isOptionSelected(field.name, opt)"
                                  [class.text-slate-300]="!isOptionSelected(field.name, opt)"
                                  class="px-3 py-1.5 rounded-lg text-xs border border-slate-700 transition">
                                  {{ opt }}
                                </button>
                              }
                            </div>
                          }
                          @case ('Rating') {
                            <div class="flex items-center gap-1.5">
                              @for (star of getRatingStars(field.max || 5); track star) {
                                <button
                                  type="button"
                                  (click)="simulatedAnswers[field.name] = star"
                                  [class.text-amber-400]="(simulatedAnswers[field.name] || 0) >= star"
                                  [class.text-slate-700]="(simulatedAnswers[field.name] || 0) < star"
                                  class="p-1 hover:scale-110 transition">
                                  <svg class="w-6 h-6 fill-current" viewBox="0 0 20 20">
                                    <path d="M9.049 2.927c.3-.921 1.603-.921 1.902 0l1.07 3.292a1 1 0 00.95.69h3.462c.969 0 1.371 1.24.588 1.81l-2.8 2.034a1 1 0 00-.364 1.118l1.07 3.292c.3.921-.755 1.688-1.54 1.118l-2.8-2.034a1 1 0 00-1.175 0l-2.8 2.034c-.784.57-1.838-.197-1.539-1.118l1.07-3.292a1 1 0 00-.364-1.118L2.98 8.72c-.783-.57-.38-1.81.588-1.81h3.461a1 1 0 00.951-.69l1.07-3.292z" />
                                  </svg>
                                </button>
                              }
                              <span class="text-xs text-slate-400 ml-2 font-mono">
                                {{ simulatedAnswers[field.name] || 0 }} / {{ field.max || 5 }}
                              </span>
                            </div>
                          }
                          @case ('Checkbox') {
                            <label class="flex items-center gap-2 cursor-pointer pt-1">
                              <input
                                type="checkbox"
                                [(ngModel)]="simulatedAnswers[field.name]"
                                class="rounded bg-slate-950 border-slate-700 text-indigo-600 focus:ring-indigo-500" />
                              <span class="text-xs text-slate-300">{{ field.placeholder || 'I acknowledge and agree' }}</span>
                            </label>
                          }
                          @case ('FileUpload') {
                            <div class="border-2 border-dashed border-slate-700 rounded-xl p-4 text-center bg-slate-950/60 hover:border-indigo-500 transition">
                              <svg class="w-8 h-8 mx-auto text-slate-500 mb-2" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                                <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M7 16a4 4 0 01-.88-7.903A5 5 0 1115.9 6L16 6a5 5 0 011 9.9M15 13l-3-3m0 0l-3 3m3-3v12" />
                              </svg>
                              <span class="text-xs text-slate-400">Click to upload document or drag and drop</span>
                              <input
                                type="text"
                                [(ngModel)]="simulatedAnswers[field.name]"
                                placeholder="Simulated file token or URL"
                                class="mt-2 w-full text-center bg-slate-900 border border-slate-800 rounded px-2 py-1 text-xs text-slate-300 font-mono" />
                            </div>
                          }
                        }
                      </div>
                    }
                  </div>

                  <!-- Simulated Cloudflare Turnstile Challenge Widget -->
                  @if (form.requireTurnstile && isFinalPreviewStep()) {
                    <div class="p-4 rounded-xl bg-slate-950 border border-slate-800 flex items-center justify-between">
                      <div class="flex items-center gap-3">
                        <button
                          type="button"
                          (click)="toggleTurnstileSim()"
                          [class.bg-emerald-600]="turnstileSimPassed()"
                          [class.border-emerald-500]="turnstileSimPassed()"
                          [class.bg-slate-900]="!turnstileSimPassed()"
                          class="w-6 h-6 rounded-md border border-slate-700 flex items-center justify-center transition">
                          @if (turnstileSimPassed()) {
                            <svg class="w-4 h-4 text-white" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                              <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M5 13l4 4L19 7" />
                            </svg>
                          }
                        </button>
                        <div>
                          <div class="text-xs font-semibold text-slate-200">
                            {{ turnstileSimPassed() ? 'Verification Successful' : 'Verify you are human' }}
                          </div>
                          <div class="text-[10px] text-slate-500 font-mono">
                            Token: {{ turnstileToken() }}
                          </div>
                        </div>
                      </div>

                      <!-- Cloudflare Turnstile Brand Logo Badge -->
                      <div class="flex flex-col items-end">
                        <div class="flex items-center gap-1 text-[11px] font-semibold text-slate-400">
                          <svg class="w-4 h-4 text-amber-500" viewBox="0 0 24 24" fill="currentColor">
                            <path d="M19.35 10.04C18.67 6.59 15.64 4 12 4 9.11 4 6.6 5.64 5.35 8.04 2.34 8.36 0 10.91 0 14c0 3.31 2.69 6 6 6h13c2.76 0 5-2.24 5-5 0-2.64-2.05-4.78-4.65-4.96z"/>
                          </svg>
                          Cloudflare
                        </div>
                        <span class="text-[9px] text-slate-600 font-mono">Turnstile Privacy Guard</span>
                      </div>
                    </div>
                  }

                  <!-- Submission Alerts / Feedback -->
                  @if (submissionMessage()) {
                    <div
                      [class.bg-emerald-500/10]="submissionSuccess()"
                      [class.border-emerald-500/30]="submissionSuccess()"
                      [class.text-emerald-300]="submissionSuccess()"
                      [class.bg-rose-500/10]="!submissionSuccess()"
                      [class.border-rose-500/30]="!submissionSuccess()"
                      [class.text-rose-300]="!submissionSuccess()"
                      class="p-4 rounded-xl border text-xs flex items-start gap-2">
                      <span>{{ submissionSuccess() ? '✓' : '⚠' }}</span>
                      <div>
                        <div class="font-semibold">{{ submissionMessage() }}</div>
                        @if (lastSubmissionResult(); as lastRes) {
                          @if (lastRes.crmRecord; as crm) {
                            <div class="text-[11px] text-slate-400 mt-1">
                              CRM Record: <code class="text-indigo-400">{{ crm.title }}</code> ({{ crm.action }} - {{ crm.status }})
                            </div>
                          }
                        }
                      </div>
                    </div>
                  }

                  <!-- Step Navigation & Submit Buttons -->
                  <div class="flex items-center justify-between pt-4 border-t border-slate-800">
                    @if (currentPreviewStep() > 1) {
                      <button
                        type="button"
                        (click)="previousPreviewStep()"
                        class="px-4 py-2 rounded-lg bg-slate-800 hover:bg-slate-700 text-slate-200 text-xs font-semibold transition">
                        Back
                      </button>
                    } @else {
                      <div></div>
                    }

                    @if (!isFinalPreviewStep()) {
                      <button
                        type="button"
                        (click)="nextPreviewStep()"
                        class="px-5 py-2 rounded-lg bg-indigo-600 hover:bg-indigo-500 text-white text-xs font-semibold shadow-md shadow-indigo-600/30 transition">
                        Next Step →
                      </button>
                    } @else {
                      <button
                        type="button"
                        [disabled]="isSubmitting()"
                        (click)="submitSimulatedForm()"
                        class="px-6 py-2.5 rounded-lg bg-emerald-600 hover:bg-emerald-500 disabled:opacity-50 text-white text-xs font-semibold shadow-md shadow-emerald-600/30 transition flex items-center gap-1.5">
                        @if (isSubmitting()) {
                          <span class="animate-spin text-sm">↻</span>
                          Submitting...
                        } @else {
                          Submit Form & Ingest
                        }
                      </button>
                    }
                  </div>
                </div>
              </div>
            </div>
          }
        }

        <!-- 3. Submissions Table & Lead Inspector View -->
        @if (activeView() === 'submissions') {
          <div class="space-y-4">
            <!-- Filter Bar -->
            <div class="p-4 rounded-xl bg-slate-900 border border-slate-800 flex items-center justify-between gap-4">
              <div class="flex items-center gap-2">
                <span class="text-xs font-semibold text-slate-400">Filter Submissions:</span>
                <button
                  (click)="submissionFilter.set('all')"
                  [class.bg-indigo-600]="submissionFilter() === 'all'"
                  [class.text-white]="submissionFilter() === 'all'"
                  [class.bg-slate-800]="submissionFilter() !== 'all'"
                  class="px-3 py-1 rounded text-xs transition">
                  All ({{ submissions().length }})
                </button>
                <button
                  (click)="submissionFilter.set('clean')"
                  [class.bg-emerald-600]="submissionFilter() === 'clean'"
                  [class.text-white]="submissionFilter() === 'clean'"
                  [class.bg-slate-800]="submissionFilter() !== 'clean'"
                  class="px-3 py-1 rounded text-xs transition">
                  Verified Clean
                </button>
                <button
                  (click)="submissionFilter.set('spam')"
                  [class.bg-rose-600]="submissionFilter() === 'spam'"
                  [class.text-white]="submissionFilter() === 'spam'"
                  [class.bg-slate-800]="submissionFilter() !== 'spam'"
                  class="px-3 py-1 rounded text-xs transition">
                  Spam / Rejected
                </button>
              </div>

              <button
                (click)="loadSubmissions()"
                class="px-3 py-1.5 rounded-lg bg-slate-800 hover:bg-slate-700 text-xs text-slate-300 border border-slate-700 flex items-center gap-1 transition">
                <svg class="w-3.5 h-3.5" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                  <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M4 4v5h.582m15.356 2A8.001 8.001 0 004.582 9m0 0H9m11 11v-5h-.581m0 0a8.003 8.003 0 01-15.357-2m15.357 2H15" />
                </svg>
                Refresh
              </button>
            </div>

            <!-- Table -->
            <div class="rounded-xl border border-slate-800 bg-slate-900/60 overflow-hidden">
              <table class="w-full text-left border-collapse text-xs">
                <thead>
                  <tr class="border-b border-slate-800 bg-slate-950/70 text-slate-400 font-semibold uppercase tracking-wider">
                    <th class="py-3 px-4">Contact / Title</th>
                    <th class="py-3 px-4">Target CRM Action</th>
                    <th class="py-3 px-4">Spam Verification</th>
                    <th class="py-3 px-4">IP Address</th>
                    <th class="py-3 px-4">Webhook Status</th>
                    <th class="py-3 px-4">Submitted At</th>
                    <th class="py-3 px-4 text-right">Actions</th>
                  </tr>
                </thead>
                <tbody class="divide-y divide-slate-800/60">
                  @for (sub of filteredSubmissions(); track sub.id) {
                    <tr class="hover:bg-slate-800/40 transition">
                      <td class="py-3 px-4">
                        <div class="font-semibold text-white">
                          {{ sub.crmRecord?.title || 'Form Submission' }}
                        </div>
                        <div class="text-[11px] text-slate-400 font-mono">
                          {{ sub.crmRecord?.contact?.email || sub.clientIp }}
                        </div>
                      </td>

                      <td class="py-3 px-4">
                        <span class="px-2 py-0.5 rounded bg-indigo-500/10 text-indigo-300 font-mono text-[11px] border border-indigo-500/20">
                          {{ sub.crmRecord?.action || 'CreateLead' }}
                        </span>
                      </td>

                      <td class="py-3 px-4">
                        @if (sub.isSpam) {
                          <span class="px-2 py-0.5 rounded bg-rose-500/10 text-rose-400 font-mono text-[11px] border border-rose-500/20">
                            Spam Blocked (Score {{ sub.spamScore }})
                          </span>
                        } @else {
                          <span class="px-2 py-0.5 rounded bg-emerald-500/10 text-emerald-400 font-mono text-[11px] border border-emerald-500/20">
                            Turnstile Verified
                          </span>
                        }
                      </td>

                      <td class="py-3 px-4 text-slate-400 font-mono">{{ sub.clientIp }}</td>

                      <td class="py-3 px-4">
                        @if (sub.webhookDeliveryStatus === 'Dispatched') {
                          <span class="px-2 py-0.5 rounded bg-blue-500/10 text-blue-400 font-mono text-[11px] border border-blue-500/20">
                            Dispatched (HMAC Signed)
                          </span>
                        } @else {
                          <span class="text-slate-500 font-mono text-[11px]">Skipped</span>
                        }
                      </td>

                      <td class="py-3 px-4 text-slate-400">
                        {{ sub.submittedAtUtc | date:'short' }}
                      </td>

                      <td class="py-3 px-4 text-right">
                        <button
                          (click)="inspectSubmission(sub)"
                          class="px-2.5 py-1 rounded bg-slate-800 hover:bg-slate-700 text-slate-200 text-xs font-medium border border-slate-700 transition">
                          Inspect
                        </button>
                      </td>
                    </tr>
                  } @empty {
                    <tr>
                      <td colspan="7" class="py-8 text-center text-slate-500 text-xs">
                        No submissions recorded for this form yet.
                      </td>
                    </tr>
                  }
                </tbody>
              </table>
            </div>
          </div>
        }
      </main>

      <!-- Embed Code Modal -->
      @if (showEmbedModal()) {
        <div class="fixed inset-0 z-50 bg-black/70 backdrop-blur-sm flex items-center justify-center p-4">
          <div class="w-full max-w-2xl bg-slate-900 border border-slate-800 rounded-2xl shadow-2xl p-6 space-y-4">
            <div class="flex items-center justify-between border-b border-slate-800 pb-3">
              <h3 class="text-base font-bold text-white flex items-center gap-2">
                <svg class="w-5 h-5 text-amber-400" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                  <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M10 20l4-16m4 4l4 4-4 4M6 16l-4-4 4-4" />
                </svg>
                Form Integration & Embed Snippets
              </h3>
              <button
                (click)="showEmbedModal.set(false)"
                class="p-1 rounded-lg text-slate-400 hover:text-white hover:bg-slate-800">
                ✕
              </button>
            </div>

            <!-- Snippet Selection Tabs -->
            <div class="flex items-center gap-2 border-b border-slate-800/60 pb-2 text-xs">
              <button
                (click)="embedSnippetTab.set('html')"
                [class.text-indigo-400]="embedSnippetTab() === 'html'"
                [class.border-b-2]="embedSnippetTab() === 'html'"
                [class.border-indigo-500]="embedSnippetTab() === 'html'"
                class="pb-2 font-semibold transition">
                HTML &lt;form&gt; Snippet
              </button>
              <button
                (click)="embedSnippetTab.set('iframe')"
                [class.text-indigo-400]="embedSnippetTab() === 'iframe'"
                [class.border-b-2]="embedSnippetTab() === 'iframe'"
                [class.border-indigo-500]="embedSnippetTab() === 'iframe'"
                class="pb-2 font-semibold transition">
                iFrame Embed
              </button>
              <button
                (click)="embedSnippetTab.set('link')"
                [class.text-indigo-400]="embedSnippetTab() === 'link'"
                [class.border-b-2]="embedSnippetTab() === 'link'"
                [class.border-indigo-500]="embedSnippetTab() === 'link'"
                class="pb-2 font-semibold transition">
                Direct Public Link
              </button>
            </div>

            <!-- Code display -->
            <div class="relative">
              <pre class="p-4 rounded-xl bg-slate-950 border border-slate-800 text-xs font-mono text-indigo-300 overflow-x-auto max-h-60">{{ activeEmbedSnippet() }}</pre>
              <button
                (click)="copyToClipboard(activeEmbedSnippet())"
                class="absolute top-3 right-3 px-3 py-1 rounded bg-slate-800 hover:bg-slate-700 text-xs text-white border border-slate-700 flex items-center gap-1 shadow">
                {{ copyToast() ? 'Copied!' : 'Copy Code' }}
              </button>
            </div>

            <p class="text-xs text-slate-400">
              Paste this snippet into your React, Next.js, Webflow, or WordPress site. Out-of-the-box support for Cloudflare Turnstile anti-bot verification and instant CRM lead generation.
            </p>
          </div>
        </div>
      }

      <!-- Submission Inspector Drawer Modal -->
      @if (inspectedSubmission(); as sub) {
        <div class="fixed inset-0 z-50 bg-black/70 backdrop-blur-sm flex items-center justify-center p-4">
          <div class="w-full max-w-xl bg-slate-900 border border-slate-800 rounded-2xl shadow-2xl p-6 space-y-4">
            <div class="flex items-center justify-between border-b border-slate-800 pb-3">
              <h3 class="text-base font-bold text-white flex items-center gap-2">
                <span>🔍</span> Submission Lead Inspector
              </h3>
              <button
                (click)="inspectedSubmission.set(null)"
                class="p-1 rounded-lg text-slate-400 hover:text-white hover:bg-slate-800">
                ✕
              </button>
            </div>

            <!-- Metadata Cards -->
            <div class="grid grid-cols-2 gap-3 text-xs">
              <div class="p-3 rounded-lg bg-slate-950 border border-slate-800">
                <span class="text-slate-500 block">Target Action</span>
                <span class="font-semibold text-indigo-400">{{ sub.crmRecord?.action }}</span>
              </div>
              <div class="p-3 rounded-lg bg-slate-950 border border-slate-800">
                <span class="text-slate-500 block">IP & Duration</span>
                <span class="font-semibold text-slate-200">{{ sub.clientIp }} • {{ sub.completionTimeSeconds }}s</span>
              </div>
            </div>

            <!-- Answers JSON -->
            <div>
              <label class="text-xs font-semibold text-slate-400 block mb-1">Parsed Form Answers</label>
              <pre class="p-3 rounded-xl bg-slate-950 border border-slate-800 text-[11px] font-mono text-emerald-400 overflow-x-auto max-h-48">{{ sub.answers | json }}</pre>
            </div>

            <!-- Outbound Webhook Signature -->
            @if (sub.webhookSignature) {
              <div>
                <label class="text-xs font-semibold text-slate-400 block mb-1">HMAC-SHA256 Webhook Signature</label>
                <div class="p-2.5 rounded-lg bg-slate-950 border border-slate-800 text-[11px] font-mono text-amber-300 break-all">
                  X-SGrowing-Signature: {{ sub.webhookSignature }}
                </div>
              </div>
            }
          </div>
        </div>
      }
    </div>
  `
})
export class HeadlessFormBuilderComponent implements OnInit {
  private http = inject(HttpClient);
  protected Math = Math;

  // State signals
  public forms = signal<HeadlessFormDefinition[]>([]);
  public selectedFormId = signal<string>('');
  public activeView = signal<'builder' | 'preview' | 'submissions'>('builder');
  public selectedFieldId = signal<string | null>(null);

  // Submissions & Filter
  public submissions = signal<HeadlessFormSubmission[]>([]);
  public submissionFilter = signal<'all' | 'clean' | 'spam'>('all');
  public inspectedSubmission = signal<HeadlessFormSubmission | null>(null);

  // Live Simulator state
  public currentPreviewStep = signal<number>(1);
  public simulatedAnswers: Record<string, any> = {};
  public turnstileSimPassed = signal<boolean>(true);
  public turnstileToken = signal<string>('1x00000000000000000000AA');
  public isSubmitting = signal<boolean>(false);
  public submissionMessage = signal<string | null>(null);
  public submissionSuccess = signal<boolean>(false);
  public lastSubmissionResult = signal<any>(null);

  // Embed Modal state
  public showEmbedModal = signal<boolean>(false);
  public embedSnippetTab = signal<'html' | 'iframe' | 'link'>('html');
  public copyToast = signal<boolean>(false);

  // Computed properties
  public currentForm = computed(() => {
    const list = this.forms();
    const id = this.selectedFormId();
    return list.find(f => f.id === id) || list[0] || null;
  });

  public currentMetrics = computed(() => {
    return this.currentForm()?.metrics || null;
  });

  public totalViews = computed(() => {
    return this.currentMetrics()?.impressions || 0;
  });

  public totalSubmissions = computed(() => {
    return this.currentMetrics()?.submissions || 0;
  });

  public conversionRate = computed(() => {
    return this.currentMetrics()?.conversionRatePercentage || 0;
  });

  public spamBlocked = computed(() => {
    return this.currentMetrics()?.spamRejections || 0;
  });

  public avgCompletionTime = computed(() => {
    return this.currentMetrics()?.averageCompletionTimeSeconds || 0;
  });

  public selectedField = computed(() => {
    const form = this.currentForm();
    const fieldId = this.selectedFieldId();
    if (!form || !fieldId) return null;
    return form.fields.find(f => f.id === fieldId) || null;
  });

  public maxFormStep = computed(() => {
    const form = this.currentForm();
    const fields = form ? form.fields : [];
    if (fields.length === 0) return 1;
    return Math.max(...fields.map(f => f.step || 1), 1);
  });

  public currentStepFields = computed(() => {
    const form = this.currentForm();
    if (!form) return [];
    const step = this.currentPreviewStep();
    return form.fields.filter(f => (f.step || 1) === step);
  });

  public isFinalPreviewStep = computed(() => {
    return this.currentPreviewStep() >= this.maxFormStep();
  });

  public filteredSubmissions = computed(() => {
    const list = this.submissions();
    const filter = this.submissionFilter();
    if (filter === 'clean') return list.filter(s => !s.isSpam);
    if (filter === 'spam') return list.filter(s => s.isSpam);
    return list;
  });

  public activeEmbedSnippet = computed(() => {
    const form = this.currentForm();
    if (!form) return '';
    const tab = this.embedSnippetTab();

    if (tab === 'iframe') {
      return `<iframe
  src="https://app.sgrowing.io/f/${form.slug}"
  width="100%"
  height="650"
  frameborder="0"
  allow="camera; microphone; autoplay">
</iframe>`;
    }

    if (tab === 'link') {
      return `https://app.sgrowing.io/f/${form.slug}`;
    }

    // HTML Form Snippet
    return `<!-- SGrowing Horizon 54 Headless Form Embed -->
<form action="https://api.sgrowing.io/api/v1/forms/headless/${form.id}/submit" method="POST">
  <!-- Turnstile Anti-Spam Widget -->
  <div class="cf-turnstile" data-sitekey="0x4AAAAAAATestKey"></div>

  ${form.fields.map(f => `<div>
    <label>${f.label}</label>
    <input type="${f.type === 'Tel' ? 'tel' : f.type === 'Email' ? 'email' : 'text'}" name="${f.name}" ${f.isRequired ? 'required' : ''} />
  </div>`).join('\n  ')}

  <button type="submit">Submit</button>
</form>
<script src="https://challenges.cloudflare.com/turnstile/v0/api.js" async defer></script>`;
  });

  ngOnInit(): void {
    this.loadForms();
  }

  public loadForms(): void {
    this.http.get<HeadlessFormDefinition[]>('/api/v1/forms/headless').subscribe({
      next: (data) => {
        if (data && data.length > 0) {
          this.forms.set(data);
          if (!this.selectedFormId()) {
            this.selectedFormId.set(data[0].id);
          }
          this.loadSubmissions();
        } else {
          this.fallbackSeedForms();
        }
      },
      error: () => {
        this.fallbackSeedForms();
      }
    });
  }

  private fallbackSeedForms(): void {
    const seeds: HeadlessFormDefinition[] = [
      {
        id: 'a0000000-0000-0000-0000-000000000001',
        tenantId: '00000000-0000-0000-0000-000000000001',
        title: 'Enterprise Demo Request (Multi-Step)',
        slug: 'enterprise-demo-request',
        description: 'Multi-step enterprise demo and technical architecture consultation request form.',
        targetAction: 'CreateLead',
        webhookUrl: 'https://api.sgrowing.io/webhooks/enterprise-leads',
        webhookSecret: 'whsec_demo_request_key_99',
        isPublished: true,
        isArchived: false,
        requireTurnstile: true,
        rateLimitPerMinutePerIp: 5,
        velocityThrottlePerMinute: 60,
        createdAtUtc: new Date().toISOString(),
        metrics: {
          impressions: 320,
          submissions: 74,
          spamRejections: 12,
          averageCompletionTimeSeconds: 45,
          conversionRatePercentage: 23.13
        },
        fields: [
          { id: 'f1', name: 'first_name', label: 'First Name', type: 'Text', placeholder: 'Jane', isRequired: true, options: [], step: 1 },
          { id: 'f2', name: 'last_name', label: 'Last Name', type: 'Text', placeholder: 'Doe', isRequired: true, options: [], step: 1 },
          { id: 'f3', name: 'email', label: 'Work Email', type: 'Email', placeholder: 'jane@company.com', isRequired: true, options: [], step: 1 },
          { id: 'f4', name: 'phone', label: 'Phone Number', type: 'Tel', placeholder: '+1 (555) 000-0000', isRequired: true, options: [], step: 1 },
          { id: 'f5', name: 'company', label: 'Company Name', type: 'Text', placeholder: 'Acme Corp', isRequired: true, options: [], step: 2 },
          { id: 'f6', name: 'company_size', label: 'Company Size', type: 'Select', isRequired: true, options: ['1-10', '11-50', '51-200', '201-1000', '1000+'], step: 2 },
          { id: 'f7', name: 'industry', label: 'Industry', type: 'Select', isRequired: true, options: ['Software / SaaS', 'E-Commerce', 'Finance', 'Healthcare', 'Other'], step: 2 },
          { id: 'f8', name: 'interest_areas', label: 'Interest Areas', type: 'MultiSelect', isRequired: false, options: ['Omnichannel Messaging', 'Headless Forms', 'AI Copilots', 'Attribution & Analytics'], step: 3 },
          { id: 'f9', name: 'notes', label: 'Additional Notes', type: 'Textarea', placeholder: 'Tell us about your requirements...', isRequired: false, options: [], step: 3 }
        ]
      },
      {
        id: 'a0000000-0000-0000-0000-000000000002',
        tenantId: '00000000-0000-0000-0000-000000000001',
        title: 'B2B Wholesale Ingestion Form',
        slug: 'b2b-wholesale-ingestion',
        description: 'Onboarding form for qualified commercial wholesalers, distributors, and volume procurement.',
        targetAction: 'CreateDeal',
        webhookUrl: 'https://api.sgrowing.io/webhooks/b2b-wholesale',
        webhookSecret: 'whsec_wholesale_vault_88',
        isPublished: true,
        isArchived: false,
        requireTurnstile: true,
        rateLimitPerMinutePerIp: 5,
        velocityThrottlePerMinute: 60,
        createdAtUtc: new Date().toISOString(),
        metrics: {
          impressions: 185,
          submissions: 42,
          spamRejections: 8,
          averageCompletionTimeSeconds: 68,
          conversionRatePercentage: 22.7
        },
        fields: [
          { id: 'w1', name: 'company', label: 'Business Legal Name', type: 'Text', placeholder: 'Apex Global LLC', isRequired: true, options: [], step: 1 },
          { id: 'w2', name: 'tax_id', label: 'Tax ID / VAT Number', type: 'Text', placeholder: 'US-123456789', validationRegex: '^[A-Za-z0-9\\-_]{6,20}$', isRequired: true, options: [], step: 1 },
          { id: 'w3', name: 'email', label: 'Buyer Email', type: 'Email', placeholder: 'purchasing@apex.com', isRequired: true, options: [], step: 1 },
          { id: 'w4', name: 'annual_order_volume', label: 'Annual Order Volume', type: 'Select', isRequired: true, options: ['$50k - $250k', '$250k - $1M', '$1M - $5M', '$5M+'], step: 1 },
          { id: 'w5', name: 'product_lines', label: 'Product Lines', type: 'MultiSelect', isRequired: true, options: ['Raw Materials', 'Finished Goods', 'Packaging', 'Bulk Pallets'], step: 1 },
          { id: 'w6', name: 'delivery_address', label: 'Warehouse Delivery Address', type: 'Textarea', placeholder: 'Dock 4, Industrial Blvd...', isRequired: true, options: [], step: 1 }
        ]
      },
      {
        id: 'a0000000-0000-0000-0000-000000000003',
        tenantId: '00000000-0000-0000-0000-000000000001',
        title: 'Customer Satisfaction & NPS Survey',
        slug: 'customer-satisfaction-nps',
        description: 'Quarterly CSAT rating and Net Promoter Score survey for active clients.',
        targetAction: 'CreateSupportTicket',
        webhookUrl: 'https://api.sgrowing.io/webhooks/csat-nps',
        webhookSecret: 'whsec_csat_feedback_77',
        isPublished: true,
        isArchived: false,
        requireTurnstile: true,
        rateLimitPerMinutePerIp: 10,
        velocityThrottlePerMinute: 120,
        createdAtUtc: new Date().toISOString(),
        metrics: {
          impressions: 510,
          submissions: 215,
          spamRejections: 19,
          averageCompletionTimeSeconds: 24,
          conversionRatePercentage: 42.16
        },
        fields: [
          { id: 'c1', name: 'overall_satisfaction', label: 'Overall Satisfaction', type: 'Rating', min: 1, max: 5, isRequired: true, options: [], step: 1 },
          { id: 'c2', name: 'nps_score', label: 'Net Promoter Score (NPS)', type: 'Rating', min: 1, max: 10, isRequired: true, options: [], step: 1 },
          { id: 'c3', name: 'liked_most', label: 'What did you like most?', type: 'Textarea', placeholder: 'Speed, ease of integration...', isRequired: false, options: [], step: 1 },
          { id: 'c4', name: 'would_recommend', label: 'I confirm my feedback may be used anonymously in research', type: 'Checkbox', isRequired: true, options: [], step: 1 },
          { id: 'c5', name: 'email', label: 'Follow-up Email (Optional)', type: 'Email', placeholder: 'you@example.com', isRequired: false, options: [], step: 1 }
        ]
      }
    ];

    this.forms.set(seeds);
    this.selectedFormId.set(seeds[0].id);
    this.fallbackSeedSubmissions();
  }

  private fallbackSeedSubmissions(): void {
    const dummySubs: HeadlessFormSubmission[] = [
      {
        id: 'sub-001',
        formId: this.selectedFormId(),
        tenantId: '00000000-0000-0000-0000-000000000001',
        submittedAtUtc: new Date(Date.now() - 3600000).toISOString(),
        clientIp: '192.168.1.15',
        answers: {
          first_name: 'Alice',
          last_name: 'Smith',
          email: 'alice.smith@cloudnexus.io',
          phone: '+1 555-345-6789',
          company: 'CloudNexus Systems',
          company_size: '51-200'
        },
        crmRecord: {
          recordId: 'rec-001',
          action: 'CreateLead',
          title: 'Lead: Alice Smith (CloudNexus Systems)',
          status: 'New',
          contact: {
            firstName: 'Alice',
            lastName: 'Smith',
            email: 'alice.smith@cloudnexus.io',
            company: 'CloudNexus Systems'
          },
          createdAtUtc: new Date().toISOString()
        },
        isSpam: false,
        spamScore: 0.02,
        completionTimeSeconds: 38,
        webhookDeliveryStatus: 'Dispatched',
        webhookSignature: 'sha256=d3b07384d113edec49eaa6238ad5ff00'
      },
      {
        id: 'sub-002',
        formId: this.selectedFormId(),
        tenantId: '00000000-0000-0000-0000-000000000001',
        submittedAtUtc: new Date(Date.now() - 7200000).toISOString(),
        clientIp: '45.12.34.89',
        answers: {
          first_name: 'Bob',
          last_name: 'Attacker',
          email: 'spammer@tempmail.com',
          company: 'Fake Corp'
        },
        isSpam: true,
        spamScore: 0.98,
        spamReason: 'Disposable email domain tempmail.com rejected',
        completionTimeSeconds: 3,
        webhookDeliveryStatus: 'Skipped'
      }
    ];

    this.submissions.set(dummySubs);
  }

  public selectForm(id: string): void {
    this.selectedFormId.set(id);
    this.currentPreviewStep.set(1);
    this.simulatedAnswers = {};
    this.submissionMessage.set(null);
    this.loadSubmissions();
  }

  public loadSubmissions(): void {
    const id = this.selectedFormId();
    if (!id) return;
    this.http.get<HeadlessFormSubmission[]>(`/api/v1/forms/headless/${id}/submissions`).subscribe({
      next: (subs) => {
        if (subs) {
          this.submissions.set(subs);
        }
      },
      error: () => {
        // keep existing state
      }
    });
  }

  public updateFormTitle(newTitle: string): void {
    const form = this.currentForm();
    if (form) {
      form.title = newTitle;
    }
  }

  public updateFormTargetAction(action: FormTargetAction): void {
    const form = this.currentForm();
    if (form) {
      form.targetAction = action;
    }
  }

  public updateRequireTurnstile(required: boolean): void {
    const form = this.currentForm();
    if (form) {
      form.requireTurnstile = required;
    }
  }

  public moveFieldUp(index: number, e: Event): void {
    e.stopPropagation();
    const form = this.currentForm();
    if (!form || index <= 0) return;
    const temp = form.fields[index];
    form.fields[index] = form.fields[index - 1];
    form.fields[index - 1] = temp;
  }

  public moveFieldDown(index: number, e: Event): void {
    e.stopPropagation();
    const form = this.currentForm();
    if (!form || index >= form.fields.length - 1) return;
    const temp = form.fields[index];
    form.fields[index] = form.fields[index + 1];
    form.fields[index + 1] = temp;
  }

  public deleteField(index: number, e: Event): void {
    e.stopPropagation();
    const form = this.currentForm();
    if (!form) return;
    form.fields.splice(index, 1);
    this.selectedFieldId.set(null);
  }

  public addNewField(): void {
    const form = this.currentForm();
    if (!form) return;
    const count = form.fields.length + 1;
    const newF: FormFieldDefinition = {
      id: 'f_' + Date.now().toString(36),
      name: 'field_' + count,
      label: 'New Field ' + count,
      type: 'Text',
      placeholder: '',
      isRequired: false,
      options: [],
      step: 1
    };
    form.fields.push(newF);
    this.selectedFieldId.set(newF.id);
  }

  public createNewForm(): void {
    const newFormId = 'form_' + Date.now().toString(36);
    const newForm: HeadlessFormDefinition = {
      id: newFormId,
      tenantId: '00000000-0000-0000-0000-000000000001',
      title: 'Custom Headless Intake Form',
      slug: 'custom-headless-intake-' + Math.floor(Math.random() * 1000),
      description: 'Quickly capture prospect details and dispatch webhook payload.',
      targetAction: 'CreateLead',
      isPublished: true,
      isArchived: false,
      requireTurnstile: true,
      rateLimitPerMinutePerIp: 5,
      velocityThrottlePerMinute: 60,
      createdAtUtc: new Date().toISOString(),
      metrics: {
        impressions: 1,
        submissions: 0,
        spamRejections: 0,
        averageCompletionTimeSeconds: 0,
        conversionRatePercentage: 0
      },
      fields: [
        { id: 'nf_1', name: 'full_name', label: 'Full Name', type: 'Text', placeholder: 'Alex Mercer', isRequired: true, options: [], step: 1 },
        { id: 'nf_2', name: 'email', label: 'Email Address', type: 'Email', placeholder: 'alex@work.com', isRequired: true, options: [], step: 1 }
      ]
    };

    this.forms.update(f => [newForm, ...f]);
    this.selectedFormId.set(newForm.id);
    this.selectedFieldId.set(newForm.fields[0].id);
  }

  public updateFieldOptions(field: FormFieldDefinition, raw: string): void {
    field.options = raw.split(',').map(s => s.trim()).filter(s => s.length > 0);
  }

  public getRatingStars(max: number): number[] {
    const count = Math.min(Math.max(max || 5, 1), 10);
    return Array.from({ length: count }, (_, i) => i + 1);
  }

  public toggleMultiSelect(fieldName: string, option: string): void {
    if (!this.simulatedAnswers[fieldName]) {
      this.simulatedAnswers[fieldName] = [option];
      return;
    }
    const arr = [...this.simulatedAnswers[fieldName]];
    const idx = arr.indexOf(option);
    if (idx >= 0) {
      arr.splice(idx, 1);
    } else {
      arr.push(option);
    }
    this.simulatedAnswers[fieldName] = arr;
  }

  public isOptionSelected(fieldName: string, option: string): boolean {
    const val = this.simulatedAnswers[fieldName];
    return Array.isArray(val) && val.includes(option);
  }

  public toggleTurnstileSim(): void {
    const passed = !this.turnstileSimPassed();
    this.turnstileSimPassed.set(passed);
    this.turnstileToken.set(passed ? '1x00000000000000000000AA' : '2x00000000000000000000BB');
  }

  public nextPreviewStep(): void {
    if (this.currentPreviewStep() < this.maxFormStep()) {
      this.currentPreviewStep.update(s => s + 1);
    }
  }

  public previousPreviewStep(): void {
    if (this.currentPreviewStep() > 1) {
      this.currentPreviewStep.update(s => s - 1);
    }
  }

  public submitSimulatedForm(): void {
    const form = this.currentForm();
    if (!form) return;

    this.isSubmitting.set(true);
    this.submissionMessage.set(null);

    const payload = {
      answers: this.simulatedAnswers,
      turnstileToken: form.requireTurnstile ? this.turnstileToken() : 'turnstile-valid-token',
      completionTimeSeconds: 35
    };

    this.http.post<any>(`/api/v1/forms/headless/${form.id}/submit`, payload).subscribe({
      next: (res) => {
        this.isSubmitting.set(false);
        this.submissionSuccess.set(true);
        this.submissionMessage.set(res.message || 'Form submitted successfully! Contact mapped to CRM.');
        this.lastSubmissionResult.set(res);

        if (res.submission) {
          this.submissions.update(subs => [res.submission, ...subs]);
          if (form.metrics) {
            form.metrics.submissions++;
            form.metrics.conversionRatePercentage = Number(((form.metrics.submissions / form.metrics.impressions) * 100).toFixed(2));
          }
        }
      },
      error: (err) => {
        this.isSubmitting.set(false);
        this.submissionSuccess.set(false);
        const errObj = err.error || {};
        this.submissionMessage.set(errObj.message || errObj.spamReason || 'Submission rejected by anti-spam verification.');
      }
    });
  }

  public inspectSubmission(sub: HeadlessFormSubmission): void {
    this.inspectedSubmission.set(sub);
  }

  public copyToClipboard(text: string): void {
    navigator.clipboard.writeText(text).then(() => {
      this.copyToast.set(true);
      setTimeout(() => this.copyToast.set(false), 2000);
    });
  }
}
