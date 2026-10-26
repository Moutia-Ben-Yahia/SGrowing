export enum FunnelStepType {
  LandingPage = 1,
  Checkout = 2,
  OrderBump = 3,
  OneClickUpsell = 4,
  Downsell = 5,
  ThankYouPage = 6
}

export interface SalesFunnelDto {
  id: string;
  tenantId: string;
  name: string;
  slug: string;
  description: string;
  isActive: boolean;
  totalVisitors: number;
  totalOrders: number;
  totalRevenue: number;
  currency: string;
  createdAtUtc: string;
}

export interface FunnelStepDto {
  id: string;
  tenantId: string;
  funnelId: string;
  stepName: string;
  stepType: FunnelStepType;
  stepOrder: number;
  targetUrlOrSlug: string;
  pageTemplateJson: string;
  visitorsCount: number;
  conversionsCount: number;
  createdAtUtc: string;
}

export interface OrderBumpOfferDto {
  id: string;
  tenantId: string;
  funnelId: string;
  stepId: string;
  title: string;
  headline: string;
  description: string;
  productSku: string;
  originalPrice: number;
  discountedPrice: number;
  badgeText: string;
  isActive: boolean;
  createdAtUtc: string;
}

export interface OneClickUpsellOfferDto {
  id: string;
  tenantId: string;
  funnelId: string;
  stepId: string;
  offerTitle: string;
  productSku: string;
  headline: string;
  imageUrl: string;
  originalPrice: number;
  upsellPrice: number;
  savingsAmount: number;
  isActive: boolean;
  createdAtUtc: string;
}

export interface StepPerformanceMetricDto {
  stepId: string;
  stepName: string;
  stepType: FunnelStepType;
  stepOrder: number;
  visitors: number;
  conversions: number;
  dropoffRatePercentage: number;
}

export interface FunnelPerformanceSummaryDto {
  funnelId: string;
  funnelName: string;
  totalVisitors: number;
  totalOrders: number;
  totalRevenue: number;
  conversionRatePercentage: number;
  bumpOrdersCount: number;
  bumpRevenue: number;
  upsellOrdersCount: number;
  upsellRevenue: number;
  stepMetrics: StepPerformanceMetricDto[];
}
