export type TourPlacement = 'Top' | 'Bottom' | 'Left' | 'Right';

export type AnnouncementModalType = 'CenteredModal' | 'FloatingBanner' | 'SlideInToast';

export interface TourStepDto {
  id?: string;
  tourId?: string;
  stepOrder: number;
  targetSelector: string;
  title: string;
  content: string;
  placement: TourPlacement;
  backdropHighlight: boolean;
  requireInteraction: boolean;
}

export interface ProductTourDto {
  id: string;
  tenantId: string;
  title: string;
  description: string;
  targetRoute: string;
  targetRole: string;
  isActive: boolean;
  createdAtUtc: string;
  steps: TourStepDto[];
}

export interface AnnouncementModalDto {
  id: string;
  tenantId: string;
  title: string;
  bodyText: string;
  ctaText: string;
  ctaUrl: string;
  modalType: AnnouncementModalType;
  targetRole: string;
  targetRouteRegex: string;
  minSessionCount: number;
  priority: number;
  startDateUtc?: string | null;
  endDateUtc?: string | null;
  isActive: boolean;
  createdAtUtc: string;
}

export interface StepDropoffMetricDto {
  stepOrder: number;
  stepTitle: string;
  usersReached: number;
  retentionRatePercent: number;
  dropoffRatePercent: number;
}

export interface TourFunnelMetricDto {
  tourId: string;
  tourTitle: string;
  totalStarted: number;
  totalCompleted: number;
  totalSkipped: number;
  completionRatePercent: number;
  stepDropoffs: StepDropoffMetricDto[];
}

export interface AnnouncementMetricDto {
  announcementId: string;
  title: string;
  modalType: AnnouncementModalType;
  impressions: number;
  dismissals: number;
  ctaClicks: number;
  ctrPercent: number;
}

export interface OnboardingMetricsSummaryDto {
  activeToursCount: number;
  activeAnnouncementsCount: number;
  totalTourStarts: number;
  totalTourCompletions: number;
  overallTourCompletionRate: number;
  tourFunnels: TourFunnelMetricDto[];
  announcementMetrics: AnnouncementMetricDto[];
}

export interface CreateProductTourRequest {
  title: string;
  description: string;
  targetRoute: string;
  targetRole: string;
  isActive: boolean;
  steps: {
    stepOrder: number;
    targetSelector: string;
    title: string;
    content: string;
    placement: TourPlacement;
    backdropHighlight: boolean;
    requireInteraction: boolean;
  }[];
}

export interface CreateAnnouncementRequest {
  title: string;
  bodyText: string;
  ctaText: string;
  ctaUrl: string;
  modalType: AnnouncementModalType;
  targetRole: string;
  targetRouteRegex: string;
  minSessionCount: number;
  priority: number;
  isActive: boolean;
}
