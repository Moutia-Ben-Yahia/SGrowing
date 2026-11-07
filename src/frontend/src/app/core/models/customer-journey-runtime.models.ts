export enum JourneyNodeType {
  TriggerNode = 'TriggerNode',
  ActionNode = 'ActionNode',
  DelayNode = 'DelayNode',
  ConditionNode = 'ConditionNode',
  SplitTestNode = 'SplitTestNode'
}

export enum JourneyExecutionStatus {
  Active = 'Active',
  PausedWaitingDelay = 'PausedWaitingDelay',
  WaitingEvent = 'WaitingEvent',
  Completed = 'Completed',
  Failed = 'Failed'
}

export interface JourneyStepExecutionLog {
  stepId: string;
  nodeId: string;
  nodeType: JourneyNodeType;
  executedAtUtc: string;
  outcome: string;
  details: string;
}

export interface CustomerJourneyProgress {
  progressId: string;
  tenantId: string;
  journeyId: string;
  contactId: string;
  contactEmail: string;
  currentNodeId: string;
  status: JourneyExecutionStatus;
  executionHistory: JourneyStepExecutionLog[];
  contextData: Record<string, string>;
  resumeAtUtc?: string | null;
  completedAtUtc?: string | null;
}

export interface TriggerJourneyEventRequest {
  tenantId: string;
  eventName: string;
  contactId: string;
  contactEmail: string;
  payloadData?: Record<string, string>;
}

export interface AdvanceJourneyRequest {
  tenantId: string;
  progressId: string;
  overrideNodeId?: string | null;
}

export interface ActiveJourneySummary {
  journeyId: string;
  title: string;
  description: string;
  triggerEvent: string;
  nodeCount: number;
  enrolledContacts: number;
  inDelayNodes: number;
  convertedContacts: number;
  conversionRatePercentage: number;
}
