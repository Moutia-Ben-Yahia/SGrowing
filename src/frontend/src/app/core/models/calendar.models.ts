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
