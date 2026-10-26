export interface ActivePageMetricDto {
  url: string;
  activeVisitors: number;
}

export interface CountryMetricDto {
  countryCode: string;
  visitorCount: number;
  percentage: number;
}

export interface DeviceMetricDto {
  deviceType: string;
  sessionCount: number;
  percentage: number;
}

export interface LiveTelemetrySummaryDto {
  activeVisitorsCount: number;
  activeSessionsCount: number;
  topActivePages: ActivePageMetricDto[];
  topCountries: CountryMetricDto[];
  deviceBreakdown: DeviceMetricDto[];
}

export interface TelemetryEventDto {
  id: string;
  tenantId: string;
  sessionId: string;
  visitorId: string;
  eventName: string;
  url: string;
  referrer: string;
  deviceType: string;
  browser: string;
  countryCode: string;
  city: string;
  propertiesJson: string;
  timestampUtc: string;
}
