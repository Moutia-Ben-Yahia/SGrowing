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
