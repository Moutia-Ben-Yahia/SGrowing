// Smart Courier Routing & Rate Cards
export interface OptimalCarrierRecommendationDto {
  courierProviderId: string;
  courierName: string;
  estimatedShippingCost: number;
  historicalDeliveryRate: number;
  expectedNetMargin: number;
  reasoning: string;
}

export interface CarrierRateCardDto {
  id: string;
  tenantId: string;
  courierProviderId: string;
  courierName: string;
  baseWeightKg: number;
  baseFee: number;
  extraKgFee: number;
  remoteZoneSurcharge: number;
  isActive: boolean;
}

// Post-Purchase Retention & Replenishment
export interface ReplenishmentScheduleDto {
  id: string;
  tenantId: string;
  orderId: string;
  customerPhone: string;
  customerName: string;
  sku: string;
  productName: string;
  deliveredAtUtc: string;
  scheduledReminderUtc: string;
  isTriggered: boolean;
  discountCode?: string;
}

export interface ReplenishmentTriggerResultDto {
  customerPhone: string;
  customerName: string;
  productName: string;
  messageText: string;
  discountCode: string;
  whatsAppCardJson: string;
}

// Multi-Warehouse Hubs & Transfers
export interface ProximityAllocationResultDto {
  warehouseId: string;
  warehouseName: string;
  availableStock: number;
  matchReason: string;
}

export interface InterHubStockTransferDto {
  id: string;
  tenantId: string;
  transferNumber: string;
  sourceWarehouseId: string;
  sourceWarehouseName: string;
  destinationWarehouseId: string;
  destinationWarehouseName: string;
  status: number; // 1=Draft, 2=InTransit, 3=Received, 4=Cancelled
  itemsJson: string;
  createdAtUtc: string;
  dispatchedAtUtc?: string;
  receivedAtUtc?: string;
}

// Live Social Commerce
export interface LiveOverlayStockDto {
  productName: string;
  sku: string;
  flashPrice: number;
  remainingUnits: number;
  triggerKeyword: string;
  isUrgentAlert: boolean;
}

export interface LiveCommentOrderResultDto {
  success: boolean;
  orderId?: string;
  customerUsername: string;
  sku: string;
  price: number;
  message: string;
  checkoutCardJson: string;
}

// Courier Cash Float & Treasury
export interface CourierFloatValuationDto {
  id: string;
  tenantId: string;
  inTransitParcelCount: number;
  inTransitCashValue: number;
  deliveredUnremittedParcelCount: number;
  deliveredUnremittedCashValue: number;
  estimatedCourierFees: number;
  netReceivableFloatValue: number;
  averageDaysSalesOutstanding: number;
  calculatedAtUtc: string;
}

export interface CashFlowForecastDayDto {
  forecastDate: string;
  courierName: string;
  expectedParcelCount: number;
  expectedGrossCash: number;
  expectedNetRemittance: number;
  confidence: string;
}

// 1. Creator & Influencer Affiliate Escrow
export interface CreatorProfileDto {
  id: string;
  tenantId: string;
  fullName: string;
  handle: string;
  email: string;
  phoneNumber: string;
  platform: number; // 0=Instagram, 1=TikTok, 2=YouTube
  commissionType: number; // 0=Percentage, 1=FixedPerDeliveredOrder
  commissionRate: number;
  minimumPayoutThreshold: number;
  paymentMethod: string;
  payoutDetails: string;
  totalEarned: number;
  pendingEscrowBalance: number;
  totalPaidOut: number;
  isActive: boolean;
  createdAtUtc: string;
}

export interface CreatorPerformanceSummaryDto {
  creatorId: string;
  fullName: string;
  handle: string;
  totalReferredGrossGmv: number;
  deliveredNetGmv: number;
  totalReferredOrders: number;
  deliveredOrders: number;
  returnedOrCanceledOrders: number;
  deliverySuccessRate: number;
  totalEarnedCommission: number;
  pendingEscrowCommission: number;
  voidedCommission: number;
  paidOutCommission: number;
  availableForPayoutBalance: number;
}

export interface CommissionEscrowLedgerDto {
  id: string;
  tenantId: string;
  creatorId: string;
  orderId: string;
  referralCode: string;
  orderTrackingNumber: string;
  orderAmount: number;
  commissionAmount: number;
  status: number; // 0=EscrowHold, 1=ClearedForPayout, 2=VoidedReturn, 3=PaidOut
  holdReason: string;
  voidReason?: string;
  createdAtUtc: string;
  clearedAtUtc?: string;
}

// 2. Automated Multi-Courier Reverse Logistics & Drop-and-Swap
export interface OrderExchangeRequestDto {
  id: string;
  tenantId: string;
  originalOrderId: string;
  originalTrackingNumber: string;
  customerPhone: string;
  customerName: string;
  reason: string;
  originalSku: string;
  replacementSku: string;
  replacementVariantDescription: string;
  additionalCashAdjustment: number;
  status: number; // 0=Requested, 1=Approved, 2=DispatchedForSwap, 3=SwapCompletedAtDoorstep, 4=ReturnInspectedAndRestocked
  swapManifestId?: string;
  createdAtUtc: string;
  completedAtUtc?: string;
}

export interface ExchangeCourierSwapManifestDto {
  id: string;
  tenantId: string;
  exchangeRequestId: string;
  courierProviderId: string;
  courierName: string;
  swapTrackingBarcode: string;
  customerAddress: string;
  governorate: string;
  bundledSwapDeliveryFee: number;
  cashToCollectOnDelivery: number;
  handoverNewParcelConfirmed: boolean;
  returnParcelCollectedConfirmed: boolean;
  dispatchedAtUtc: string;
  deliveredAtUtc?: string;
}

// 3. Multi-Currency & Cross-Border Maghreb / GCC Trade Hub
export interface CountryTradeConfigDto {
  id: string;
  tenantId: string;
  countryCode: string;
  countryName: string;
  currencyCode: string;
  phonePrefix: string;
  nationalPhoneNumberLength: number;
  defaultVatPercentage: number;
  standardShippingFee: number;
  supportsCod: boolean;
  isActive: boolean;
}

export interface CustomsCommercialInvoiceDto {
  id: string;
  tenantId: string;
  orderId: string;
  invoiceNumber: string;
  exporterName: string;
  exporterCountry: string;
  destinationCountry: string;
  consigneeCustomerName: string;
  consigneePhoneE164: string;
  consigneeAddress: string;
  currency: string;
  totalDeclaredCustomsValue: number;
  totalWeightKg: number;
  incoterm: string;
  itemsJson: string;
  clearanceIssuedAtUtc: string;
}

// 4. Predictive Demand Forecasting & Supplier Restock Matrix
export interface StockoutProjectionAlertDto {
  id: string;
  tenantId: string;
  productId: string;
  sku: string;
  currentPhysicalStock: number;
  currentReservedStock: number;
  availableStock: number;
  projectedDailyDemand: number;
  estimatedDaysOfSupplyRemaining: number;
  projectedStockoutDateUtc: string;
  recommendedReorderQuantity: number;
  priority: number; // 0=Normal, 1=Elevated, 2=High, 3=CriticalStockoutImminent
  alertDismissed: boolean;
  generatedAtUtc: string;
}

export interface SupplierScorecardDto {
  id: string;
  tenantId: string;
  supplierName: string;
  contactEmail: string;
  contactPhone: string;
  country: string;
  promisedLeadTimeDays: number;
  actualAvgLeadTimeDays: number;
  defectRatePercentage: number;
  qualityScoreOutOf100: number;
  totalPurchaseOrdersCompleted: number;
  isActive: boolean;
}

export interface AutomatedRestockOrderDto {
  id: string;
  tenantId: string;
  supplierId: string;
  supplierName: string;
  productId: string;
  sku: string;
  productName: string;
  quantityToOrder: number;
  unitPurchaseCost: number;
  totalCost: number;
  status: number; // 0=Suggested, 1=Approved, 2=PlacedWithSupplier, 3=PartiallyReceived, 4=ReceivedInWarehouse
  placedAtUtc?: string;
  expectedDeliveryDateUtc?: string;
  createdAtUtc: string;
}

// 5. RBAC & Security Audit Trail
export interface SecurityAuditLogEntryDto {
  id: string;
  tenantId: string;
  userId: string;
  userEmail: string;
  userRole: string;
  action: string;
  resource: string;
  resourceId: string;
  details: string;
  ipAddress: string;
  timestampUtc: string;
}

export interface MaskedCustomerOrderViewDto {
  orderId: string;
  orderNumber: string;
  customerName: string;
  displayPhoneNumber: string;
  displayAddress: string;
  clickToDialToken: string;
  totalAmount: number;
  status: string;
}

// 6. Closed-Loop Real Delivered ROAS & Ad Optimizer
export interface AdSetDeliveredRoasMetricDto {
  id: string;
  tenantId: string;
  platform: number; // 0=Meta, 1=TikTok, 2=Google, 3=Snapchat
  adSetId: string;
  adSetName: string;
  dateUtc: string;
  totalAdSpend: number;
  totalOrdersAttributed: number;
  deliveredOrdersCount: number;
  returnedOrdersCount: number;
  deliveryRatePercentage: number;
  grossAttributedGmv: number;
  deliveredNetCashRemitted: number;
  totalCogs: number;
  totalCourierShippingFees: number;
  netContributionMargin: number;
  realDeliveredRoas: number;
  standardPixelRoas: number;
  roasLeakagePercentage: number;
}

export interface AdPlatformExecutionLogDto {
  id: string;
  tenantId: string;
  platform: number;
  adSetId: string;
  action: number; // 0=NoAction, 1=PauseAdSet, 2=ScaleBudget, 3=AlertMediaBuyer
  reason: string;
  currentDailyBudget: number;
  newDailyBudget?: number;
  executedAtUtc: string;
}

// 7. In-House Fleet & Driver Run-Sheets
export interface DeliveryDriverDto {
  id: string;
  tenantId: string;
  fullName: string;
  phoneNumber: string;
  vehicleType: string;
  licensePlate: string;
  hubWarehouseId: string;
  status: number; // 0=Available, 1=OnRoute, 2=OffDuty
  cashInHandBalance: number;
  totalDeliveriesCompleted: number;
  isActive: boolean;
}

export interface DriverRunSheetDto {
  id: string;
  tenantId: string;
  driverId: string;
  sheetNumber: string;
  dateUtc: string;
  status: number; // 0=Draft, 1=Dispatched, 2=Completed
  totalParcels: number;
  deliveredParcels: number;
  refusedParcels: number;
  totalCashToCollect: number;
  totalCashActuallyCollected: number;
  startedAtUtc?: string;
  completedAtUtc?: string;
}

export interface RunSheetStopDto {
  id: string;
  tenantId: string;
  runSheetId: string;
  orderId: string;
  orderNumber: string;
  stopSequence: number;
  customerName: string;
  customerPhone: string;
  shippingAddress: string;
  amountToCollect: number;
  paymentMethodAtDoorstep: string;
  status: number; // 0=Scheduled, 1=Arrived, 2=DeliveredPaidCash, 3=DeliveredPaidOnline, 4=Rescheduled, 5=Refused
}

// 8. Visual WhatsApp Flow Engine
export interface FlowDefinitionDto {
  id: string;
  tenantId: string;
  name: string;
  triggerKeyword: string;
  description: string;
  isActive: boolean;
}

export interface FlowTurnResultDto {
  nextNodeKey: string;
  responseMessage: string;
  buttons: string[];
  isEscalated: boolean;
  detectedLanguage: string;
  executedAction?: string;
}

// 9. Intelligent Aging Inventory Liquidation
export interface StockAgingLedgerDto {
  id: string;
  tenantId: string;
  sku: string;
  productName: string;
  quantity: number;
  unitCostPrice: number;
  originalRetailPrice: number;
  daysInWarehouse: number;
  tier: number; // 0=Fresh, 1=Mature, 2=Stagnant, 3=DeadStock
  currentEffectivePrice: number;
  currentMarkdownPercentage: number;
}

export interface LiquidationBundleOfferDto {
  id: string;
  tenantId: string;
  bundleName: string;
  winningProductSku: string;
  agingProductSku: string;
  originalCombinedPrice: number;
  discountedBundlePrice: number;
  incentiveDiscountPercentage: number;
  activeInChatCpq: boolean;
}
