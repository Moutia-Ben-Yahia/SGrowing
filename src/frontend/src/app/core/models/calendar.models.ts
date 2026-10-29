export interface BookingEventTypeDto {
  id: string;
  tenantId: string;
  title: string;
  slug: string;
  description: string;
  durationMinutes: number;
  bufferBeforeMinutes: number;
  bufferAfterMinutes: number;
  minimumNoticeHours: number;
  isRoundRobin: boolean;
  assignedHostIdsJson: string;
  hostWeightsJson?: string;
  colorHex: string;
  isActive: boolean;
  createdAtUtc: string;
}

export interface DayAvailabilityDto {
  dayOfWeek: string;
  startTime: string;
  endTime: string;
}

export interface HostAvailabilityScheduleDto {
  id: string;
  tenantId: string;
  hostUserId: string;
  hostName: string;
  timeZone: string;
  weeklyHoursJson: string;
  blackoutDatesJson: string;
  createdAtUtc: string;
}

export interface TimeSlotDto {
  startTimeUtc: string;
  endTimeUtc: string;
  hostUserId: string;
  hostName: string;
}

export interface CalendarBookingRecordDto {
  id: string;
  tenantId: string;
  eventTypeId: string;
  hostUserId: string;
  hostName: string;
  attendeeName: string;
  attendeeEmail: string;
  attendeePhone: string;
  startTimeUtc: string;
  endTimeUtc: string;
  timeZone: string;
  status: number; // 1: Scheduled, 2: Completed, 3: Cancelled, 4: NoShow
  meetingUrl: string;
  icsUid: string;
  notes: string;
  createdAtUtc: string;
}

export interface HostWeightDto {
  hostUserId: string;
  hostName: string;
  weight: number;
  assignedMeetingsCount: number;
  score: number;
}

export interface ParticipantMediaStateDto {
  isMicEnabled: boolean;
  isCameraEnabled: boolean;
  isScreenSharing: boolean;
  isSpeaking: boolean;
  networkQuality: 'excellent' | 'good' | 'poor';
  latencyMs: number;
  packetLossRate: number;
}

export interface ParticipantStateDto {
  participantIdentity: string;
  displayName: string;
  participantRole: 'host' | 'attendee' | 'copilot' | 'viewer';
  joinedAtUtc: string;
  leftAtUtc?: string | null;
  lastHeartbeatUtc: string;
  mediaState: ParticipantMediaStateDto;
  totalSpeakingSeconds: number;
}

export interface WebRtcAccessTokenResultDto {
  accessToken: string;
  roomToken: string;
  participantIdentity: string;
  participantRole: string;
  canPublish: boolean;
  canSubscribe: boolean;
  canPublishData: boolean;
  expiresAtUtc: string;
  sfuServerUrl: string;
}

export interface SalesBattleCardDto {
  objectionKey: string;
  category: string;
  title: string;
  detectedTrigger: string;
  quickPitch: string;
  keyTalkingPoints: string[];
  recommendedFollowUpQuestion: string;
  confidenceScore: number;
  severity: 'low' | 'medium' | 'high';
}

export interface ObjectionEvaluationResultDto {
  roomToken: string;
  transcriptSegment: string;
  sentiment: 'Positive' | 'Neutral' | 'Skeptical' | 'Negative';
  sentimentScore: number;
  matchedBattleCards: SalesBattleCardDto[];
  evaluatedAtUtc: string;
}

export interface MeetingTranscriptItemDto {
  speakerIdentity: string;
  transcript: string;
  sentimentScore: number;
  isFinal: boolean;
  timestampUtc: string;
}

export interface VideoMeetingSessionDto {
  roomToken: string;
  tenantId: string;
  title: string;
  hostName: string;
  attendeeName: string;
  status: number; // 1: Active, 2: Ended, 3: Cancelled, 4: Expired
  createdAtUtc: string;
  startedAtUtc: string;
  endedAtUtc?: string | null;
  durationSeconds: number;
  durationFormatted: string;
  activeParticipantCount: number;
  participants: ParticipantStateDto[];
  recentTranscripts: MeetingTranscriptItemDto[];
  activeBattleCards: SalesBattleCardDto[];
  meetingUrl: string;
}

export interface MeetingParticipantAttendanceDto {
  participantIdentity: string;
  displayName: string;
  role: string;
  joinedAtUtc: string;
  leftAtUtc: string;
  durationSeconds: number;
  speakingSeconds: number;
  usedCamera: boolean;
  usedScreenShare: boolean;
}

export interface MeetingSessionSummaryDto {
  roomToken: string;
  tenantId: string;
  title: string;
  hostName: string;
  attendeeName: string;
  startedAtUtc: string;
  endedAtUtc: string;
  totalDurationSeconds: number;
  totalDurationFormatted: string;
  attendanceHistory: MeetingParticipantAttendanceDto[];
  objectionsEncountered: string[];
  buyingSignalsDetected: string[];
  followUpTasks: string[];
  summaryNotes: string;
}

