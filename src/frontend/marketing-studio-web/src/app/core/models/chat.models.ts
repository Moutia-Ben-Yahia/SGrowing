export interface ContactDto {
  id: string;
  tenantId: string;
  firstName: string;
  lastName: string;
  email?: string;
  phone?: string;
  lifecycleStage: number;
  customFields: Record<string, any>;
  tags: string[];
  createdAtUtc: string;
}

export interface ConversationDto {
  id: string;
  tenantId: string;
  inboxId: string;
  contactId: string;
  assigneeId?: string;
  status: number; // 1 = Open, 2 = Snoozed, 3 = Resolved
  priority: number; // 1 = Low, 2 = Normal, 3 = High, 4 = Urgent
  aiMode: number; // 0 = Disabled, 1 = Copilot, 2 = Autonomous
  lastMessageAtUtc?: string;
  createdAtUtc: string;
  contact?: ContactDto;
}

export interface MessageDto {
  id: string;
  conversationId: string;
  senderType: number; // 1 = Contact, 2 = Agent, 3 = Bot, 4 = System
  senderId?: string;
  contentType: number; // 1 = Text, 2 = Image, etc.
  content: string;
  mediaUrl?: string;
  deliveryStatus: number;
  createdAtUtc: string;
}

export interface AiDraftDto {
  id: string;
  conversationId: string;
  suggestedReply: string;
  confidenceScore: number;
  status: string;
  createdAtUtc: string;
}
