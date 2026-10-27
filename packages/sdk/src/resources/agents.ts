import { AgentMission, SGrowingClientConfig } from '../types.js';

export class AgentsResource {
  constructor(private readonly config: SGrowingClientConfig) {}

  async listMissions(): Promise<AgentMission[]> {
    const res = await fetch(`${this.config.baseUrl}/api/agents/missions?tenantId=${this.config.tenantId}`, {
      headers: {
        Authorization: `Bearer ${this.config.apiKey}`,
        'Content-Type': 'application/json'
      }
    });
    if (!res.ok) throw new Error(`Failed to list missions: ${res.statusText}`);
    return res.json() as Promise<AgentMission[]>;
  }

  async createMission(params: {
    title: string;
    targetGoal: string;
    autonomyMode?: 'Auto-Approve' | 'Human-in-the-Loop' | 'Draft Only';
    budgetUsd?: number;
  }): Promise<AgentMission> {
    const res = await fetch(`${this.config.baseUrl}/api/agents/missions`, {
      method: 'POST',
      headers: {
        Authorization: `Bearer ${this.config.apiKey}`,
        'Content-Type': 'application/json'
      },
      body: JSON.stringify({
        tenantId: this.config.tenantId,
        title: params.title,
        targetGoal: params.targetGoal,
        autonomyMode: params.autonomyMode ?? 'Human-in-the-Loop',
        budgetUsd: params.budgetUsd ?? 50.0
      })
    });
    if (!res.ok) throw new Error(`Failed to create agent mission: ${res.statusText}`);
    return res.json() as Promise<AgentMission>;
  }

  async approveStep(missionId: string, stepId: string, approvedBy: string = 'User'): Promise<AgentMission> {
    const res = await fetch(`${this.config.baseUrl}/api/agents/missions/${missionId}/steps/${stepId}/approve`, {
      method: 'POST',
      headers: {
        Authorization: `Bearer ${this.config.apiKey}`,
        'Content-Type': 'application/json'
      },
      body: JSON.stringify({ approvedBy })
    });
    if (!res.ok) throw new Error(`Failed to approve step: ${res.statusText}`);
    return res.json() as Promise<AgentMission>;
  }
}
