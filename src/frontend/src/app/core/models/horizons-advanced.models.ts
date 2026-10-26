// Horizon 1: Courier Companion & Headless Checkout
export interface DriverMobileSessionDto {
  id: string;
  tenantId: string;
  driverId: string;
  driverName: string;
  sessionToken: string;
  deviceModel: string;
  lastGpsLatitude: number;
  lastGpsLongitude: number;
  batteryLevelPercent: number;
  isOnline: boolean;
  lastSyncAtUtc: string;
}

export interface DoorstepPodDto {
  id: string;
  tenantId: string;
  runSheetStopId: string;
  orderId: string;
  orderNumber: string;
  driverId: string;
  customerSignatureBase64: string;
  parcelPhotoUrl: string;
  cashCollected: number;
  paymentMethod: string;
  recipientName: string;
  recipientRelation: string;
  gpsLatitude: number;
  gpsLongitude: number;
  deliveredAtUtc: string;
}

export interface DriverCashSummaryDto {
  driverId: string;
  driverName: string;
  totalCashInHand: number;
  completedStopsCount: number;
  pendingStopsCount: number;
  lastUpdatedUtc: string;
}

export interface WilayaZoneDto {
  id: string;
  code: string;
  nameAr: string;
  nameFr: string;
  countryCode: string;
  standardShippingFee: number;
  estimatedDeliveryDays: number;
  delegationsJson: string;
}

export interface CodCheckoutSessionDto {
  id: string;
  tenantId: string;
  sessionKey: string;
  cartItemsJson: string;
  subtotal: number;
  shippingFee: number;
  discountAmount: number;
  totalAmount: number;
  customerName: string;
  customerPhone: string;
  wilayaCode: string;
  wilayaName: string;
  delegationName: string;
  streetAddress: string;
  isOtpVerified: boolean;
  status: number;
  placedOrderId?: string;
  upsellOfferAcceptedSku?: string;
  upsellAmount: number;
}

// Horizon 3: UGC Creative Scripts & Competitor Radar
export interface UgcScriptSceneDto {
  sceneNumber: number;
  visualCue: string;
  creatorAction: string;
  voiceoverText: string;
  onscreenText: string;
  durationSeconds: number;
}

export interface UgcVideoScriptDto {
  id: string;
  tenantId: string;
  productSku: string;
  productName: string;
  language: string;
  targetAudience: string;
  hookAngle: number;
  hookText: string;
  scenesJson: string;
  callToAction: string;
  estimatedDurationSeconds: number;
  createdAtUtc: string;
}

export interface CompetitorAdRecordDto {
  id: string;
  tenantId: string;
  competitorName: string;
  platform: string;
  hookSnippet: string;
  offerDetails: string;
  estimatedDailySpend: number;
  threatLevel: string;
  isActive: boolean;
  firstSeenAtUtc: string;
  lastSeenAtUtc: string;
}

export interface CompetitorPriceTrackerDto {
  id: string;
  tenantId: string;
  targetSku: string;
  competitorName: string;
  competitorUrl: string;
  competitorPrice: number;
  ourPrice: number;
  priceDifferencePercentage: number;
  recommendedCounterAction: string;
  lastCheckedAtUtc: string;
}

export interface CompetitorRadarSummaryDto {
  totalCompetitorAdsMonitored: number;
  highThreatCompetitorsCount: number;
  skusWithPriceDeficit: number;
  priceAlerts: CompetitorPriceTrackerDto[];
  activeCompetitorCampaigns: CompetitorAdRecordDto[];
}

// Horizon 4: B2B Credit, Factoring & Developer Gateway
export interface B2BCreditAccountDto {
  id: string;
  tenantId: string;
  companyId: string;
  companyName: string;
  creditLimit: number;
  outstandingBalance: number;
  availableCredit: number;
  paymentTermsDays: number;
  creditScoreRating: string;
  isFrozen: boolean;
  createdAtUtc: string;
}

export interface FactoringReceivableRecordDto {
  id: string;
  tenantId: string;
  courierProviderId: string;
  courierName: string;
  inTransitCashFloat: number;
  verifiedDeliveredCash: number;
  advanceDiscountRatePercentage: number;
  immediateCashAvailable: number;
  status: string;
  fundedAtUtc?: string;
  createdAtUtc: string;
}

export interface TenantApiKeyDto {
  id: string;
  tenantId: string;
  name: string;
  keyPrefix: string;
  keyMasked: string;
  allowedScopesJson: string;
  rateLimitPerMinute: number;
  isRevoked: boolean;
  lastUsedAtUtc?: string;
  createdAtUtc: string;
}

export interface WebhookSubscriptionRecordDto {
  id: string;
  tenantId: string;
  targetUrl: string;
  secretKey: string;
  subscribedEventsJson: string;
  isActive: boolean;
  failureCount: number;
  createdAtUtc: string;
}

export interface WebhookDeliveryAttemptLogDto {
  id: string;
  tenantId: string;
  subscriptionId: string;
  eventType: string;
  payloadJson: string;
  responseStatusCode: number;
  isSuccess: boolean;
  executionDurationMs: number;
  attemptedAtUtc: string;
}
