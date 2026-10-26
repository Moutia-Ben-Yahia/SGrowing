// Autonomous Voice AI Confirmation Dialer
export interface OutboundCallAttemptDto {
  id: string;
  tenantId: string;
  campaignId?: string;
  orderId: string;
  customerPhone: string;
  customerName: string;
  attemptNumber: number;
  state: number; // 1=Queued, 2=Ringing, 3=InCallSpeaking, 4=AwaitingIntent, 5=Confirmed, 6=Postponed, 7=Cancelled, 8=NoAnswer, 9=Failed
  spokenIntent: number; // 0=None, 1=Confirmed, 2=Postponed, 3=Cancelled, 4=Unrecognized
  speechTranscript?: string;
  dtmfDigit?: string;
  callDurationSeconds: number;
  scheduledRetryUtc?: string;
  createdAtUtc: string;
  completedAtUtc?: string;
}

// Mobile Warehouse Barcode Fulfillment Desk
export interface WarehousePickSlipDto {
  id: string;
  tenantId: string;
  pickSlipNumber: string;
  orderId: string;
  orderNumber: string;
  warehouseId: string;
  assignedPickerId?: string;
  status: number; // 1=Pending, 2=Picking, 3=Picked, 4=Packed, 5=Dispatched
  createdAtUtc: string;
  completedAtUtc?: string;
  items: PickSlipItemDto[];
}

export interface PickSlipItemDto {
  id: string;
  pickSlipId: string;
  sku: string;
  productName: string;
  barcode: string;
  quantityRequested: number;
  quantityPicked: number;
  isVerified: boolean;
}

export interface CourierManifestRunSheetDto {
  id: string;
  tenantId: string;
  courierName: string;
  manifestReference: string;
  driverName?: string;
  driverPhone?: string;
  driverSignatureData?: string;
  totalParcels: number;
  totalCodValue: number;
  status: number; // 1=Draft, 2=Closed, 3=HandedOverToDriver
  createdAtUtc: string;
  handedOverAtUtc?: string;
  parcels: ManifestParcelItemDto[];
}

export interface ManifestParcelItemDto {
  id: string;
  manifestId: string;
  orderId: string;
  trackingNumber: string;
  customerName: string;
  governorate: string;
  codAmount: number;
  scannedAtUtc: string;
}

export interface WarehouseRmaInspectionDto {
  id: string;
  tenantId: string;
  trackingNumber: string;
  orderId: string;
  inspectorId: string;
  condition: number; // 1=IntactRestock, 2=DamagedWriteoff, 3=WrongItemReturned
  inspectorNotes?: string;
  restockedToWarehouseId?: string;
  inspectedAtUtc: string;
}

// Meta Catalog & WhatsApp MPM
export interface MetaCatalogItemDto {
  id: string;
  tenantId: string;
  retailerId: string;
  title: string;
  description: string;
  availability: string;
  condition: string;
  price: number;
  currency: string;
  link: string;
  imageLink: string;
  brand: string;
  googleProductCategory: string;
  lastUpdatedUtc: string;
}

export interface MetaCatalogSyncFeedDto {
  id: string;
  tenantId: string;
  catalogId: string;
  feedFormat: string;
  totalProducts: number;
  lastSyncedAtUtc: string;
  syncStatus: string;
}

// White-Label Custom Domains & Brand Theming
export interface StorefrontDomainDto {
  id: string;
  tenantId: string;
  hostname: string;
  isPrimary: boolean;
  targetCname: string;
  validationStatus: number; // 1=PendingDns, 2=Active, 3=Failed
  sslStatus: number; // 1=Provisioning, 2=Issued, 3=Expired
  verifiedAtUtc?: string;
  createdAtUtc: string;
}

export interface BrandThemeTokensDto {
  id: string;
  tenantId: string;
  storefrontSlug: string;
  brandName: string;
  primaryColorHex: string;
  secondaryColorHex: string;
  fontFamily: string;
  logoUrl: string;
  faviconUrl: string;
  supportEmail: string;
  supportPhone: string;
  smsSenderId: string;
  whatsAppSenderPhone: string;
}

// Anti-Fraud Customer Reputation Scorecard
export interface CustomerReputationScorecardDto {
  id: string;
  tenantId: string;
  customerPhone: string;
  customerEmail?: string;
  reputationScore: number;
  tier: number; // 1=Trusted, 2=Standard, 3=HighRisk, 4=Blacklisted
  totalOrdersCount: number;
  deliveredOrdersCount: number;
  refusedReturnsCount: number;
  cancelledOrdersCount: number;
  totalSpentAmount: number;
  deliverySuccessRatePercentage: number;
  requiresMicroDeposit: boolean;
  lastEvaluatedAtUtc: string;
}

export interface MicroDepositPaymentLinkDto {
  id: string;
  tenantId: string;
  orderId: string;
  customerPhone: string;
  depositAmount: number;
  currency: string;
  paymentUrl: string;
  paymentProvider: string;
  status: number; // 1=Pending, 2=Paid, 3=Refunded, 4=Forfeited
  paidAtUtc?: string;
  expiresAtUtc: string;
  createdAtUtc: string;
}
