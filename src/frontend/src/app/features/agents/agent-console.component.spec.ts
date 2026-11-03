// @vitest-environment jsdom
import '@angular/compiler';
import { describe, it, expect, beforeEach, vi } from 'vitest';
import { createEnvironmentInjector } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { AgentConsoleComponent } from './agent-console.component';
import { of } from 'rxjs';

describe('AgentConsoleComponent', () => {
  let component: AgentConsoleComponent;
  let httpMock: any;

  beforeEach(() => {
    httpMock = {
      get: vi.fn().mockImplementation((url: string) => {
        if (url.includes('/autonomy')) return of({ autonomyMode: 'Auto-Approve' });
        if (url.includes('/audit-logs')) return of([
          {
            id: 'log-101',
            timestampUtc: '2026-11-03T18:00:00Z',
            agentId: 'Agent-Test',
            toolName: 'crm_search_leads',
            autonomyMode: 'Auto-Approve',
            actionSummary: 'Lead search',
            status: 'Approved' as const,
            executionTimeMs: 42.0
          }
        ]);
        return of([]);
      }),
      post: vi.fn().mockImplementation((url: string) => {
        if (url.includes('/autonomy')) return of({ status: 'Updated' });
        if (url.includes('/invoke')) return of({ success: true, count: 5 });
        return of({});
      })
    };

    const injector = createEnvironmentInjector([
      { provide: HttpClient, useValue: httpMock }
    ]);

    injector.runInContext(() => {
      component = new AgentConsoleComponent();
    });

    component.ngOnInit();
  });

  it('should initialize with registered MCP tools and active autonomy mode', () => {
    expect(component).toBeTruthy();
    expect(component.registeredTools.length).toBeGreaterThan(0);
    expect(component.activeAutonomy()).toBe('Auto-Approve');
    expect(component.auditLogs().length).toBe(1);
    expect(component.selectedTool()).toBeTruthy();
  });

  it('should update autonomy mode and dispatch governance change', () => {
    component.setAutonomy('Draft Only');

    expect(component.activeAutonomy()).toBe('Draft Only');
    expect(httpMock.post).toHaveBeenCalledWith('/api/v1/mcp/autonomy', { mode: 'Draft Only' });
  });

  it('should select an MCP tool and format sample arguments into JSON template', () => {
    const targetTool = component.registeredTools[1]; // outbound_send_spintax_campaign
    component.selectTool(targetTool);

    expect(component.selectedTool().name).toBe(targetTool.name);
    expect(component.toolArgsJson).toContain(targetTool.name === 'outbound_send_spintax_campaign' ? 'CMP-2026-Q4' : '');
  });

  it('should execute selected MCP tool and capture execution duration and result', () => {
    component.executeSelectedTool();

    expect(httpMock.post).toHaveBeenCalledWith(
      `/api/v1/mcp/tools/${component.selectedTool().name}/invoke`,
      expect.any(Object)
    );
    expect(component.isExecuting()).toBe(false);
    expect(component.executionResult()).toContain('"success": true');
    expect(component.executionDuration()).toBeGreaterThanOrEqual(0);
  });

  it('should reset sample arguments to default tool definition', () => {
    component.toolArgsJson = '{"corrupted": true}';
    component.resetSampleArgs();

    expect(component.toolArgsJson).toContain('query');
  });
});
