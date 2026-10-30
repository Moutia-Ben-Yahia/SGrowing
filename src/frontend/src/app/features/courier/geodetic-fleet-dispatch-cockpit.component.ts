import { Component, OnInit, signal, computed, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { HttpClient } from '@angular/common/http';

export interface LogisticsHubDto {
  hubId: string;
  code: string;
  name: string;
  address: string;
  city: string;
  country: string;
  latitude: number;
  longitude: number;
  currency: string;
  isActive: boolean;
}

export interface CourierDriverDto {
  driverId: string;
  driverCode: string;
  driverName: string;
  phoneNumber: string;
  vehicleType: string;
  maxParcelCapacity: number;
  maxWeightCapacityKg: number;
  baseLatitude: number;
  baseLongitude: number;
  assignedHubId: string;
  isActive: boolean;
}

export interface HubWithDriversDto {
  hub: LogisticsHubDto;
  activeDrivers: CourierDriverDto[];
}

export interface DeliveryStopDto {
  stopId: string;
  hubId: string;
  trackingNumber: string;
  customerName: string;
  customerPhone: string;
  deliveryAddress: string;
  city: string;
  latitude: number;
  longitude: number;
  parcelWeightKg: number;
  codAmount: number;
  currency: string;
  timeWindowStart: string;
  timeWindowEnd: string;
  windowLabel: string;
  priority: string;
  status: string;
}

export interface RouteWaypointDto {
  sequenceOrder: number;
  stopId: string;
  trackingNumber: string;
  customerName: string;
  deliveryAddress: string;
  latitude: number;
  longitude: number;
  parcelWeightKg: number;
  codAmount: number;
  currency: string;
  timeWindowStart: string;
  timeWindowEnd: string;
  windowLabel: string;
  estimatedArrival: string;
  estimatedDeparture: string;
  waitMinutes: number;
  lateMinutes: number;
  isLate: boolean;
  latePenaltyScore: number;
  legDistanceKm: number;
  cumulativeDistanceKm: number;
  status: string;
}

export interface DriverClusterRouteDto {
  clusterId: string;
  driver: CourierDriverDto;
  stopCount: number;
  totalWeightKg: number;
  capacityUtilizationPct: number;
  weightUtilizationPct: number;
  totalCodAmount: number;
  currency: string;
  totalRouteDistanceKm: number;
  estimatedStartTime: string;
  estimatedEndTime: string;
  totalDurationMinutes: number;
  totalLateMinutes: number;
  totalLatePenalty: number;
  waypoints: RouteWaypointDto[];
  centroid?: { latitude: number; longitude: number };
}

export interface FleetOptimizationResultDto {
  optimizationId: string;
  executedAtUtc: string;
  hub: LogisticsHubDto;
  totalStopsAssigned: number;
  totalUnassignedStops: number;
  totalDriversUsed: number;
  totalFleetDistanceKm: number;
  totalFleetDurationMinutes: number;
  totalFleetCodAmount: number;
  currency: string;
  totalFleetLatePenalty: number;
  routes: DriverClusterRouteDto[];
  unassignedStops: DeliveryStopDto[];
}

export interface DriverRunsheetManifestDto {
  runsheetId: string;
  runsheetCode: string;
  hubId: string;
  hubName: string;
  driverId: string;
  driverCode: string;
  driverName: string;
  vehicleType: string;
  dispatchedDateUtc: string;
  totalStops: number;
  totalDistanceKm: number;
  estimatedDurationMinutes: number;
  departureTime: string;
  returnTime: string;
  totalCodToCollect: number;
  currency: string;
  status: string;
  manifestStops: RouteWaypointDto[];
}

@Component({
  selector: 'app-geodetic-fleet-dispatch-cockpit',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterLink],
  template: `
    <div dir="ltr" class="flex h-screen w-screen overflow-hidden bg-slate-950 text-slate-100 font-sans">
      <main class="flex-1 flex flex-col overflow-y-auto">
        <!-- Top Navigation Header -->
        <header class="h-16 border-b border-slate-800 bg-slate-900/80 backdrop-blur px-6 flex items-center justify-between shrink-0 sticky top-0 z-20">
          <div class="flex items-center gap-4">
            <a routerLink="/courier" class="text-xs text-slate-400 hover:text-white flex items-center gap-1.5 transition">
              <span>←</span> Courier Hub
            </a>
            <span class="text-slate-700">|</span>
            <div class="flex items-center gap-2">
              <span class="text-xl">🛰️</span>
              <div>
                <h1 class="text-sm font-bold text-white tracking-wide">
                  Geodetic Fleet Dispatch & Spatial Clustering Engine
                </h1>
                <p class="text-[11px] text-slate-400">
                  PostGIS Spherical Haversine • Capacitated Geodesic K-Means • 2-Opt TSP Optimizer
                </p>
              </div>
            </div>
            <span class="bg-cyan-500/10 text-cyan-400 border border-cyan-500/30 text-[10px] font-mono px-2.5 py-0.5 rounded-full">
              Horizon 40
            </span>
          </div>

          <!-- Controls & Actions -->
          <div class="flex items-center gap-3">
            <!-- Hub Selector -->
            <div class="flex items-center gap-2 bg-slate-900 border border-slate-800 rounded-lg px-2.5 py-1">
              <span class="text-xs text-slate-400">Hub:</span>
              <select
                [ngModel]="selectedHubId()"
                (ngModelChange)="onHubChanged($event)"
                class="bg-transparent text-xs font-semibold text-cyan-400 focus:outline-none cursor-pointer">
                @for (item of hubsData(); track item.hub.hubId) {
                  <option [value]="item.hub.hubId" class="bg-slate-900 text-slate-200">
                    {{ item.hub.name }} ({{ item.hub.city }})
                  </option>
                }
              </select>
            </div>

            <!-- Optimization Action Button -->
            <button
              (click)="runOptimization()"
              [disabled]="isOptimizing() || !selectedHubId()"
              class="flex items-center gap-2 px-3.5 py-1.5 rounded-lg text-xs font-bold transition shadow-lg
                     bg-cyan-600 hover:bg-cyan-500 text-white disabled:opacity-50 disabled:cursor-not-allowed">
              @if (isOptimizing()) {
                <span class="inline-block animate-spin">⏳</span>
                <span>Optimizing Routes...</span>
              } @else {
                <span>⚡</span>
                <span>Run Geodetic 2-Opt TSP</span>
              }
            </button>

            <!-- Dispatch Runsheets Button -->
            <button
              (click)="dispatchRunsheets()"
              [disabled]="isDispatching() || !optimizationResult()"
              class="flex items-center gap-2 px-3.5 py-1.5 rounded-lg text-xs font-bold transition shadow-lg
                     bg-emerald-600 hover:bg-emerald-500 text-white disabled:opacity-50 disabled:cursor-not-allowed">
              @if (isDispatching()) {
                <span class="inline-block animate-spin">📄</span>
                <span>Dispatching...</span>
              } @else {
                <span>🚀</span>
                <span>Dispatch Runsheets</span>
              }
            </button>

            <!-- Reset Seed Button -->
            <button
              (click)="resetSeedData()"
              title="Reset in-memory fleet state to initial seed"
              class="px-2.5 py-1.5 rounded-lg text-xs bg-slate-800 text-slate-400 hover:text-slate-200 hover:bg-slate-700 transition">
              ↺ Reset
            </button>
          </div>
        </header>

        <!-- Notification Banner -->
        @if (statusMessage()) {
          <div class="mx-6 mt-4 p-3 rounded-lg bg-cyan-950/60 border border-cyan-800/60 text-xs text-cyan-200 flex items-center justify-between">
            <div class="flex items-center gap-2">
              <span>✨</span>
              <span>{{ statusMessage() }}</span>
            </div>
            <button (click)="statusMessage.set(null)" class="text-cyan-400 hover:text-white">✕</button>
          </div>
        }

        <!-- Top KPI Cards -->
        <section class="p-6 grid grid-cols-1 md:grid-cols-4 gap-4">
          <!-- KPI 1: Delivery Stops -->
          <div class="p-4 rounded-xl bg-slate-900/70 border border-slate-800 shadow-sm relative overflow-hidden">
            <div class="flex items-center justify-between">
              <span class="text-xs font-medium text-slate-400 uppercase tracking-wider">Total Stops</span>
              <span class="p-1.5 rounded-md bg-blue-500/10 text-blue-400 text-sm">📍</span>
            </div>
            <div class="mt-2 flex items-baseline gap-2">
              <span class="text-2xl font-black text-white">{{ totalStopsCount() }}</span>
              <span class="text-[11px] text-slate-400">pending deliveries</span>
            </div>
            <div class="mt-2 text-[10px] text-slate-500">
              Assigned to {{ selectedHub()?.hub?.name || 'Hub' }}
            </div>
          </div>

          <!-- KPI 2: Active Drivers -->
          <div class="p-4 rounded-xl bg-slate-900/70 border border-slate-800 shadow-sm relative overflow-hidden">
            <div class="flex items-center justify-between">
              <span class="text-xs font-medium text-slate-400 uppercase tracking-wider">Active Drivers</span>
              <span class="p-1.5 rounded-md bg-emerald-500/10 text-emerald-400 text-sm">🛵</span>
            </div>
            <div class="mt-2 flex items-baseline gap-2">
              <span class="text-2xl font-black text-emerald-400">{{ activeDrivers().length }}</span>
              <span class="text-[11px] text-slate-400">vehicles available</span>
            </div>
            <div class="mt-2 text-[10px] text-slate-500">
              Capacity: {{ totalDriverCapacity() }} parcels max
            </div>
          </div>

          <!-- KPI 3: Fleet Distance -->
          <div class="p-4 rounded-xl bg-slate-900/70 border border-slate-800 shadow-sm relative overflow-hidden">
            <div class="flex items-center justify-between">
              <span class="text-xs font-medium text-slate-400 uppercase tracking-wider">Total Fleet Distance</span>
              <span class="p-1.5 rounded-md bg-cyan-500/10 text-cyan-400 text-sm">🌐</span>
            </div>
            <div class="mt-2 flex items-baseline gap-2">
              <span class="text-2xl font-black text-cyan-400">{{ fleetTotalDistance() | number:'1.1-2' }}</span>
              <span class="text-xs font-semibold text-slate-400">km</span>
            </div>
            <div class="mt-2 text-[10px] text-slate-500">
              Est. Duration: {{ fleetTotalDuration() }} min (2-Opt optimized)
            </div>
          </div>

          <!-- KPI 4: COD Cash To Collect -->
          <div class="p-4 rounded-xl bg-slate-900/70 border border-slate-800 shadow-sm relative overflow-hidden">
            <div class="flex items-center justify-between">
              <span class="text-xs font-medium text-slate-400 uppercase tracking-wider">COD Cash To Collect</span>
              <span class="p-1.5 rounded-md bg-amber-500/10 text-amber-400 text-sm">💵</span>
            </div>
            <div class="mt-2 flex items-baseline gap-2">
              <span class="text-2xl font-black text-amber-400">{{ totalCodAmount() | number:'1.2-2' }}</span>
              <span class="text-xs font-semibold text-slate-400">{{ currentCurrency() }}</span>
            </div>
            <div class="mt-2 text-[10px] text-slate-500">
              Penalty Score: {{ fleetLatePenalty() | number:'1.1-2' }} pts
            </div>
          </div>
        </section>

        <!-- Main Content Grid -->
        <div class="px-6 pb-6 space-y-6 flex-1">
          <!-- Views Switcher Tab Bar -->
          <div class="flex items-center justify-between border-b border-slate-800 pb-3">
            <div class="flex items-center gap-2">
              <button
                (click)="activeTab.set('cockpit')"
                [class]="activeTab() === 'cockpit' ? 'bg-cyan-600 text-white font-bold' : 'bg-slate-900 text-slate-400 hover:bg-slate-800 hover:text-slate-200'"
                class="px-3.5 py-1.5 rounded-lg text-xs transition flex items-center gap-1.5">
                <span>🗺️</span> Spatial Radar & Clusters
              </button>

              <button
                (click)="activeTab.set('runsheets')"
                [class]="activeTab() === 'runsheets' ? 'bg-cyan-600 text-white font-bold' : 'bg-slate-900 text-slate-400 hover:bg-slate-800 hover:text-slate-200'"
                class="px-3.5 py-1.5 rounded-lg text-xs transition flex items-center gap-1.5">
                <span>📋</span> Driver Runsheets ({{ runsheets().length }})
              </button>

              <button
                (click)="activeTab.set('stops')"
                [class]="activeTab() === 'stops' ? 'bg-cyan-600 text-white font-bold' : 'bg-slate-900 text-slate-400 hover:bg-slate-800 hover:text-slate-200'"
                class="px-3.5 py-1.5 rounded-lg text-xs transition flex items-center gap-1.5">
                <span>📦</span> Pending Delivery Stops ({{ stops().length }})
              </button>
            </div>

            <div class="text-[11px] text-slate-500 font-mono">
              Coordinates: Lat {{ selectedHub()?.hub?.latitude }}, Lon {{ selectedHub()?.hub?.longitude }}
            </div>
          </div>

          <!-- TAB 1: COCKPIT (Interactive Map + Driver Cluster Cards) -->
          @if (activeTab() === 'cockpit') {
            <div class="grid grid-cols-1 lg:grid-cols-12 gap-6">
              <!-- Left / Main: Interactive Delivery Map Visualizer (7 cols) -->
              <div class="lg:col-span-7 bg-slate-900/70 border border-slate-800 rounded-2xl p-5 flex flex-col">
                <div class="flex items-center justify-between mb-4">
                  <div>
                    <h2 class="text-sm font-bold text-white flex items-center gap-2">
                      <span>🎯</span> Geodesic Dispatch Visualizer & TSP Route Tours
                    </h2>
                    <p class="text-[11px] text-slate-400">
                      Real-time Great-Circle Haversine projection with dynamic 2-Opt TSP loops.
                    </p>
                  </div>
                  <div class="flex items-center gap-2 text-[10px]">
                    <span class="flex items-center gap-1 text-amber-400"><span class="w-2 h-2 rounded-full bg-amber-400"></span> Warehouse Hub</span>
                    <span class="flex items-center gap-1 text-cyan-400"><span class="w-2 h-2 rounded-full bg-cyan-400"></span> Driver DRV-01</span>
                    <span class="flex items-center gap-1 text-emerald-400"><span class="w-2 h-2 rounded-full bg-emerald-400"></span> Driver DRV-02</span>
                  </div>
                </div>

                <!-- SVG Geodetic Projection Canvas -->
                <div class="relative w-full aspect-[4/3] bg-slate-950 rounded-xl border border-slate-800/80 overflow-hidden flex items-center justify-center">
                  <!-- Concentric Radar Rings -->
                  <div class="absolute inset-0 flex items-center justify-center pointer-events-none opacity-20">
                    <div class="w-[85%] h-[85%] border border-cyan-500/40 rounded-full animate-pulse"></div>
                    <div class="w-[60%] h-[60%] border border-cyan-500/30 rounded-full absolute"></div>
                    <div class="w-[35%] h-[35%] border border-cyan-500/20 rounded-full absolute"></div>
                  </div>

                  <svg class="w-full h-full" viewBox="0 0 600 450">
                    <!-- Grid background coordinates lines -->
                    <defs>
                      <pattern id="radarGrid" width="40" height="40" patternUnits="userSpaceOnUse">
                        <path d="M 40 0 L 0 0 0 40" fill="none" stroke="#1e293b" stroke-width="0.5"/>
                      </pattern>
                      <linearGradient id="hubGlow" x1="0%" y1="0%" x2="100%" y2="100%">
                        <stop offset="0%" stop-color="#f59e0b" stop-opacity="0.8"/>
                        <stop offset="100%" stop-color="#d97706" stop-opacity="0.2"/>
                      </linearGradient>
                    </defs>

                    <rect width="100%" height="100%" fill="url(#radarGrid)" />

                    <!-- Route Paths connecting Hub to Waypoints and back -->
                    @if (optimizationResult(); as opt) {
                      @for (route of opt.routes; track route.clusterId; let rIdx = $index) {
                        @if (route.waypoints.length > 0) {
                          <path
                            [attr.d]="computeRouteSvgPath(route)"
                            [attr.stroke]="getClusterColor(rIdx)"
                            stroke-width="2.5"
                            stroke-dasharray="4,3"
                            fill="none"
                            class="opacity-80 transition-all duration-500" />
                        }
                      }
                    }

                    <!-- Delivery Stop Markers -->
                    @for (stop of mapDisplayStops(); track stop.stopId; let sIdx = $index) {
                      <g
                        (click)="selectStop(stop)"
                        class="cursor-pointer group transition transform"
                        [attr.transform]="'translate(' + getSvgX(stop.longitude) + ',' + getSvgY(stop.latitude) + ')'">
                        <!-- Stop Circle Pin -->
                        <circle
                          r="9"
                          [attr.fill]="getStopMarkerColor(stop)"
                          stroke="#ffffff"
                          stroke-width="1.5"
                          class="group-hover:scale-125 transition-transform origin-center" />
                        <text
                          y="3"
                          text-anchor="middle"
                          fill="#ffffff"
                          font-size="8"
                          font-weight="bold"
                          font-family="sans-serif">
                          {{ sIdx + 1 }}
                        </text>

                        <!-- Label Tooltip -->
                        <g class="opacity-0 group-hover:opacity-100 transition-opacity pointer-events-none">
                          <rect x="-45" y="-32" width="90" height="18" rx="4" fill="#0f172a" stroke="#334155" stroke-width="1" />
                          <text x="0" y="-20" text-anchor="middle" fill="#f8fafc" font-size="8" font-weight="bold">
                            {{ stop.trackingNumber }}
                          </text>
                        </g>
                      </g>
                    }

                    <!-- Hub Warehouse Central Marker -->
                    @if (selectedHub(); as h) {
                      <g
                        [attr.transform]="'translate(' + getSvgX(h.hub.longitude) + ',' + getSvgY(h.hub.latitude) + ')'">
                        <circle r="22" fill="url(#hubGlow)" class="animate-ping opacity-30 origin-center" />
                        <circle r="14" fill="#f59e0b" stroke="#ffffff" stroke-width="2" />
                        <text y="4" text-anchor="middle" fill="#000000" font-size="10" font-weight="black">
                          HUB
                        </text>
                        <text y="25" text-anchor="middle" fill="#fbbf24" font-size="9" font-weight="bold">
                          {{ h.hub.code }}
                        </text>
                      </g>
                    }
                  </svg>

                  <!-- Map Info Overlay -->
                  <div class="absolute bottom-3 left-3 bg-slate-900/80 backdrop-blur border border-slate-800 rounded px-2.5 py-1 text-[10px] text-slate-400">
                    Projection: Geodetic WGS84 • Haversine Mean Earth Radius 6,371 km
                  </div>
                </div>

                <!-- Selected Stop Inspection Card -->
                @if (selectedStop(); as sel) {
                  <div class="mt-4 p-3.5 bg-slate-950 border border-slate-800 rounded-xl flex items-center justify-between text-xs">
                    <div class="space-y-1">
                      <div class="flex items-center gap-2">
                        <span class="font-bold text-white">{{ sel.trackingNumber }}</span>
                        <span class="text-slate-400">({{ sel.customerName }})</span>
                        <span class="bg-cyan-500/20 text-cyan-300 text-[10px] px-1.5 py-0.5 rounded">
                          {{ sel.windowLabel }} Window
                        </span>
                      </div>
                      <div class="text-[11px] text-slate-400">{{ sel.deliveryAddress }}</div>
                    </div>

                    <div class="flex items-center gap-6 text-right">
                      <div>
                        <div class="text-[10px] text-slate-500">COD Due</div>
                        <div class="font-bold text-amber-400">{{ sel.codAmount | number:'1.2-2' }} {{ sel.currency }}</div>
                      </div>
                      <div>
                        <div class="text-[10px] text-slate-500">Weight</div>
                        <div class="font-semibold text-slate-300">{{ sel.parcelWeightKg }} kg</div>
                      </div>
                      <button (click)="selectedStop.set(null)" class="text-slate-500 hover:text-white px-2">✕</button>
                    </div>
                  </div>
                }
              </div>

              <!-- Right: Driver Cluster Cards & Optimization Preview (5 cols) -->
              <div class="lg:col-span-5 space-y-4">
                <div class="flex items-center justify-between">
                  <h2 class="text-sm font-bold text-white flex items-center gap-2">
                    <span>🚚</span> Driver Routes & Capacity Allocation
                  </h2>
                  <span class="text-xs text-slate-400 font-mono">
                    {{ activeDrivers().length }} Drivers Online
                  </span>
                </div>

                @if (optimizationResult(); as opt) {
                  <!-- Routes Cards -->
                  @for (route of opt.routes; track route.clusterId; let rIdx = $index) {
                    <div class="p-4 rounded-xl bg-slate-900/70 border border-slate-800 space-y-3 relative overflow-hidden">
                      <div class="flex items-center justify-between">
                        <div class="flex items-center gap-2.5">
                          <span
                            [style.background-color]="getClusterColor(rIdx)"
                            class="w-3 h-3 rounded-full shrink-0 shadow-md">
                          </span>
                          <div>
                            <div class="text-xs font-bold text-white flex items-center gap-1.5">
                              <span>{{ route.driver.driverName }}</span>
                              <span class="text-[10px] font-mono text-cyan-400">({{ route.driver.driverCode }})</span>
                            </div>
                            <div class="text-[10px] text-slate-400">{{ route.driver.vehicleType }}</div>
                          </div>
                        </div>

                        <!-- Lateness Badge -->
                        @if (route.totalLateMinutes > 0) {
                          <span class="bg-rose-500/10 text-rose-400 border border-rose-500/30 text-[10px] px-2 py-0.5 rounded-full font-mono">
                            ⚠️ {{ route.totalLateMinutes }}m Late ({{ route.totalLatePenalty }} pts)
                          </span>
                        } @else {
                          <span class="bg-emerald-500/10 text-emerald-400 border border-emerald-500/30 text-[10px] px-2 py-0.5 rounded-full font-mono">
                            ✓ On Time Schedule
                          </span>
                        }
                      </div>

                      <!-- Capacity & Weight Progress Bars -->
                      <div class="grid grid-cols-2 gap-3 text-[11px]">
                        <!-- Stops / Capacity -->
                        <div>
                          <div class="flex justify-between text-slate-400 mb-1">
                            <span>Stops: {{ route.stopCount }} / {{ route.driver.maxParcelCapacity }}</span>
                            <span class="font-bold text-white">{{ route.capacityUtilizationPct }}%</span>
                          </div>
                          <div class="w-full bg-slate-800 rounded-full h-1.5 overflow-hidden">
                            <div
                              class="bg-cyan-500 h-1.5 rounded-full transition-all duration-500"
                              [style.width.%]="route.capacityUtilizationPct">
                            </div>
                          </div>
                        </div>

                        <!-- Weight / Capacity -->
                        <div>
                          <div class="flex justify-between text-slate-400 mb-1">
                            <span>Weight: {{ route.totalWeightKg }} / {{ route.driver.maxWeightCapacityKg }} kg</span>
                            <span class="font-bold text-white">{{ route.weightUtilizationPct }}%</span>
                          </div>
                          <div class="w-full bg-slate-800 rounded-full h-1.5 overflow-hidden">
                            <div
                              class="bg-amber-500 h-1.5 rounded-full transition-all duration-500"
                              [style.width.%]="route.weightUtilizationPct">
                            </div>
                          </div>
                        </div>
                      </div>

                      <!-- Key Route Metrics -->
                      <div class="grid grid-cols-3 gap-2 p-2 bg-slate-950/60 rounded-lg text-center text-xs">
                        <div>
                          <div class="text-[10px] text-slate-500">Route Dist</div>
                          <div class="font-bold text-cyan-400">{{ route.totalRouteDistanceKm | number:'1.1-2' }} km</div>
                        </div>
                        <div>
                          <div class="text-[10px] text-slate-500">Duration</div>
                          <div class="font-bold text-slate-200">{{ route.totalDurationMinutes }} min</div>
                        </div>
                        <div>
                          <div class="text-[10px] text-slate-500">COD Tally</div>
                          <div class="font-bold text-amber-400">{{ route.totalCodAmount | number:'1.2-2' }} {{ route.currency }}</div>
                        </div>
                      </div>

                      <!-- Route Sequence Preview -->
                      <div class="space-y-1">
                        <div class="text-[10px] font-semibold text-slate-400 uppercase tracking-wider">
                          2-Opt Optimized Visiting Sequence:
                        </div>
                        <div class="flex items-center gap-1.5 overflow-x-auto py-1 text-[10px] font-mono">
                          <span class="px-2 py-0.5 rounded bg-amber-500/20 text-amber-300 border border-amber-500/30">
                            HUB
                          </span>
                          @for (wp of route.waypoints; track wp.stopId) {
                            <span class="text-slate-600">→</span>
                            <span
                              (click)="selectStop(wp)"
                              title="{{ wp.customerName }} ({{ wp.deliveryAddress }})"
                              class="px-2 py-0.5 rounded bg-slate-800 text-slate-200 hover:bg-slate-700 cursor-pointer border border-slate-700">
                              #{{ wp.sequenceOrder }} {{ wp.trackingNumber }}
                            </span>
                          }
                          <span class="text-slate-600">→</span>
                          <span class="px-2 py-0.5 rounded bg-amber-500/20 text-amber-300 border border-amber-500/30">
                            RETURN
                          </span>
                        </div>
                      </div>
                    </div>
                  }

                  <!-- Unassigned Overflow Stops -->
                  @if (opt.unassignedStops.length > 0) {
                    <div class="p-3.5 rounded-xl bg-rose-950/30 border border-rose-900/60 text-xs space-y-2">
                      <div class="flex items-center justify-between text-rose-300 font-bold">
                        <span>⚠️ Unassigned Overflow Stops ({{ opt.unassignedStops.length }})</span>
                        <span class="text-[10px] font-normal text-rose-400">Driver capacity exceeded</span>
                      </div>
                      <div class="space-y-1 max-h-32 overflow-y-auto pr-1">
                        @for (s of opt.unassignedStops; track s.stopId) {
                          <div class="flex items-center justify-between text-[11px] text-slate-300 bg-slate-900/60 p-1.5 rounded">
                            <span>{{ s.trackingNumber }} ({{ s.customerName }})</span>
                            <span class="font-bold text-amber-400">{{ s.parcelWeightKg }} kg • {{ s.codAmount }} {{ s.currency }}</span>
                          </div>
                        }
                      </div>
                    </div>
                  }
                } @else {
                  <!-- Empty State -->
                  <div class="p-8 rounded-2xl bg-slate-900/40 border border-dashed border-slate-800 text-center space-y-3">
                    <span class="text-4xl block">🛰️</span>
                    <h3 class="text-sm font-bold text-white">No Route Calculated Yet</h3>
                    <p class="text-xs text-slate-400 max-w-sm mx-auto">
                      Click the <strong class="text-cyan-400">"Run Geodetic 2-Opt TSP"</strong> button above to partition delivery stops into driver clusters and optimize travel sequences.
                    </p>
                  </div>
                }
              </div>
            </div>
          }

          <!-- TAB 2: DRIVER RUNSHEET MANIFESTS -->
          @if (activeTab() === 'runsheets') {
            <div class="space-y-6">
              <div class="flex items-center justify-between">
                <div>
                  <h2 class="text-sm font-bold text-white flex items-center gap-2">
                    <span>📋</span> Driver Runsheet Manifests
                  </h2>
                  <p class="text-[11px] text-slate-400">
                    Official dispatch runsheet manifests with verified COD cash collection tallies.
                  </p>
                </div>
                <div class="text-xs text-slate-400">
                  Total Dispatched: <strong class="text-white">{{ runsheets().length }}</strong>
                </div>
              </div>

              @if (runsheets().length === 0) {
                <div class="p-12 rounded-2xl bg-slate-900/40 border border-dashed border-slate-800 text-center space-y-3">
                  <span class="text-4xl block">📑</span>
                  <h3 class="text-sm font-bold text-white">No Runsheets Dispatched</h3>
                  <p class="text-xs text-slate-400 max-w-sm mx-auto">
                    Execute geodetic optimization first, then click "Dispatch Runsheets" to generate driver road manifests.
                  </p>
                </div>
              } @else {
                <div class="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-4">
                  @for (manifest of runsheets(); track manifest.runsheetId) {
                    <div
                      (click)="selectedRunsheet.set(manifest)"
                      class="p-4 rounded-xl bg-slate-900/70 border transition cursor-pointer hover:border-cyan-500/50 space-y-3"
                      [class.border-cyan-500]="selectedRunsheet()?.runsheetId === manifest.runsheetId"
                      [class.border-slate-800]="selectedRunsheet()?.runsheetId !== manifest.runsheetId">
                      <div class="flex items-center justify-between">
                        <span class="font-mono text-xs font-bold text-cyan-400">{{ manifest.runsheetCode }}</span>
                        <span class="bg-emerald-500/10 text-emerald-400 border border-emerald-500/30 text-[10px] px-2 py-0.5 rounded-full font-mono">
                          {{ manifest.status }}
                        </span>
                      </div>

                      <div class="space-y-1">
                        <div class="text-xs font-bold text-white">{{ manifest.driverName }}</div>
                        <div class="text-[11px] text-slate-400">{{ manifest.vehicleType }}</div>
                        <div class="text-[10px] text-slate-500 font-mono">{{ manifest.hubName }}</div>
                      </div>

                      <div class="grid grid-cols-3 gap-2 p-2 bg-slate-950/60 rounded text-center text-xs">
                        <div>
                          <div class="text-[10px] text-slate-500">Stops</div>
                          <div class="font-bold text-white">{{ manifest.totalStops }}</div>
                        </div>
                        <div>
                          <div class="text-[10px] text-slate-500">Distance</div>
                          <div class="font-bold text-cyan-400">{{ manifest.totalDistanceKm | number:'1.1-1' }} km</div>
                        </div>
                        <div>
                          <div class="text-[10px] text-slate-500">COD Total</div>
                          <div class="font-bold text-amber-400">{{ manifest.totalCodToCollect | number:'1.2-2' }} {{ manifest.currency }}</div>
                        </div>
                      </div>

                      <div class="text-[10px] text-slate-400 flex items-center justify-between">
                        <span>Depart: {{ manifest.departureTime }}</span>
                        <span>Return: {{ manifest.returnTime }}</span>
                      </div>
                    </div>
                  }
                </div>

                <!-- Detailed Manifest Table Modal / Inspector -->
                @if (selectedRunsheet(); as m) {
                  <div class="mt-8 p-6 rounded-2xl bg-slate-900 border border-slate-800 space-y-4">
                    <div class="flex items-center justify-between border-b border-slate-800 pb-4">
                      <div>
                        <div class="flex items-center gap-3">
                          <h3 class="text-base font-black text-white font-mono">{{ m.runsheetCode }}</h3>
                          <span class="bg-cyan-500/20 text-cyan-300 text-xs px-2.5 py-0.5 rounded font-mono">
                            {{ m.driverName }} ({{ m.driverCode }})
                          </span>
                        </div>
                        <p class="text-xs text-slate-400 mt-1">
                          Vehicle: {{ m.vehicleType }} • Departure Hub: {{ m.hubName }}
                        </p>
                      </div>

                      <div class="flex items-center gap-3">
                        <div class="text-right">
                          <div class="text-xs text-slate-400">Total COD Cash Manifest</div>
                          <div class="text-lg font-black text-amber-400">{{ m.totalCodToCollect | number:'1.2-2' }} {{ m.currency }}</div>
                        </div>
                        <button
                          (click)="selectedRunsheet.set(null)"
                          class="px-3 py-1.5 rounded-lg bg-slate-800 text-slate-300 hover:text-white text-xs">
                          Close
                        </button>
                      </div>
                    </div>

                    <!-- Stops Table -->
                    <div class="overflow-x-auto">
                      <table class="w-full text-left text-xs">
                        <thead class="bg-slate-950 text-slate-400 uppercase text-[10px] font-mono">
                          <tr>
                            <th class="p-2.5">Seq</th>
                            <th class="p-2.5">Tracking #</th>
                            <th class="p-2.5">Customer & Phone</th>
                            <th class="p-2.5">Address</th>
                            <th class="p-2.5">Weight</th>
                            <th class="p-2.5">Time Window</th>
                            <th class="p-2.5">Est. Arrival</th>
                            <th class="p-2.5 text-right">COD Cash</th>
                          </tr>
                        </thead>
                        <tbody class="divide-y divide-slate-800 font-sans">
                          @for (wp of m.manifestStops; track wp.stopId) {
                            <tr class="hover:bg-slate-800/40 transition">
                              <td class="p-2.5 font-bold font-mono text-cyan-400">#{{ wp.sequenceOrder }}</td>
                              <td class="p-2.5 font-mono text-white font-semibold">{{ wp.trackingNumber }}</td>
                              <td class="p-2.5">
                                <div class="font-semibold text-slate-200">{{ wp.customerName }}</div>
                              </td>
                              <td class="p-2.5 text-slate-300 max-w-xs truncate">{{ wp.deliveryAddress }}</td>
                              <td class="p-2.5 text-slate-400">{{ wp.parcelWeightKg }} kg</td>
                              <td class="p-2.5">
                                <span class="px-2 py-0.5 rounded text-[10px] bg-slate-800 text-slate-300">
                                  {{ wp.timeWindowStart }} - {{ wp.timeWindowEnd }}
                                </span>
                              </td>
                              <td class="p-2.5 font-mono" [class.text-rose-400]="wp.isLate" [class.text-emerald-400]="!wp.isLate">
                                {{ wp.estimatedArrival }}
                                @if (wp.isLate) { <span class="text-[9px] text-rose-400 block font-sans">⚠️ late</span> }
                              </td>
                              <td class="p-2.5 text-right font-mono font-bold text-amber-400">
                                {{ wp.codAmount | number:'1.2-2' }} {{ wp.currency }}
                              </td>
                            </tr>
                          }
                        </tbody>
                      </table>
                    </div>
                  </div>
                }
              }
            </div>
          }

          <!-- TAB 3: PENDING DELIVERY STOPS -->
          @if (activeTab() === 'stops') {
            <div class="space-y-4">
              <div class="flex items-center justify-between">
                <div>
                  <h2 class="text-sm font-bold text-white flex items-center gap-2">
                    <span>📦</span> Pending Delivery Stops
                  </h2>
                  <p class="text-[11px] text-slate-400">
                    Geolocated delivery orders ready for capacitated clustering and dispatch.
                  </p>
                </div>
                <div class="text-xs text-slate-400">
                  Total In Hub: <strong class="text-white">{{ stops().length }}</strong>
                </div>
              </div>

              <div class="overflow-x-auto rounded-xl border border-slate-800 bg-slate-900/70">
                <table class="w-full text-left text-xs">
                  <thead class="bg-slate-950 text-slate-400 uppercase text-[10px] font-mono">
                    <tr>
                      <th class="p-3">Tracking #</th>
                      <th class="p-3">Customer</th>
                      <th class="p-3">Address & City</th>
                      <th class="p-3">GPS Coordinates</th>
                      <th class="p-3">Weight</th>
                      <th class="p-3">Time Window</th>
                      <th class="p-3">COD Amount</th>
                      <th class="p-3">Status</th>
                    </tr>
                  </thead>
                  <tbody class="divide-y divide-slate-800">
                    @for (s of stops(); track s.stopId) {
                      <tr class="hover:bg-slate-800/40 transition">
                        <td class="p-3 font-mono font-bold text-cyan-400">{{ s.trackingNumber }}</td>
                        <td class="p-3">
                          <div class="font-semibold text-white">{{ s.customerName }}</div>
                          <div class="text-[10px] text-slate-500 font-mono">{{ s.customerPhone }}</div>
                        </td>
                        <td class="p-3 text-slate-300">
                          <div>{{ s.deliveryAddress }}</div>
                          <div class="text-[10px] text-slate-500">{{ s.city }}</div>
                        </td>
                        <td class="p-3 font-mono text-[11px] text-slate-400">
                          {{ s.latitude | number:'1.4-4' }}, {{ s.longitude | number:'1.4-4' }}
                        </td>
                        <td class="p-3 font-semibold text-slate-200">{{ s.parcelWeightKg }} kg</td>
                        <td class="p-3">
                          <span class="px-2 py-0.5 rounded text-[10px] bg-slate-800 text-slate-300">
                            {{ s.timeWindowStart }} - {{ s.timeWindowEnd }} ({{ s.windowLabel }})
                          </span>
                        </td>
                        <td class="p-3 font-mono font-bold text-amber-400">
                          {{ s.codAmount | number:'1.2-2' }} {{ s.currency }}
                        </td>
                        <td class="p-3">
                          <span
                            class="px-2 py-0.5 rounded-full text-[10px] font-mono"
                            [class.bg-emerald-500-10]="s.status === 'Dispatched'"
                            [class.text-emerald-400]="s.status === 'Dispatched'"
                            [class.bg-slate-800]="s.status !== 'Dispatched'"
                            [class.text-slate-400]="s.status !== 'Dispatched'">
                            {{ s.status }}
                          </span>
                        </td>
                      </tr>
                    }
                  </tbody>
                </table>
              </div>
            </div>
          }
        </div>
      </main>
    </div>
  `
})
export class GeodeticFleetDispatchCockpitComponent implements OnInit {
  private http = inject(HttpClient);
  private apiBase = '/api/v1/logistics/fleet-dispatch';

  // Signals
  hubsData = signal<HubWithDriversDto[]>([]);
  selectedHubId = signal<string>('');
  stops = signal<DeliveryStopDto[]>([]);
  optimizationResult = signal<FleetOptimizationResultDto | null>(null);
  runsheets = signal<DriverRunsheetManifestDto[]>([]);
  selectedRunsheet = signal<DriverRunsheetManifestDto | null>(null);
  selectedStop = signal<DeliveryStopDto | RouteWaypointDto | null>(null);

  isOptimizing = signal<boolean>(false);
  isDispatching = signal<boolean>(false);
  statusMessage = signal<string | null>(null);
  activeTab = signal<'cockpit' | 'runsheets' | 'stops'>('cockpit');

  // Computed state
  selectedHub = computed(() => {
    const id = this.selectedHubId();
    return this.hubsData().find(h => h.hub.hubId === id) || null;
  });

  activeDrivers = computed(() => {
    return this.selectedHub()?.activeDrivers || [];
  });

  totalStopsCount = computed(() => {
    return this.stops().length;
  });

  totalDriverCapacity = computed(() => {
    return this.activeDrivers().reduce((sum, d) => sum + d.maxParcelCapacity, 0);
  });

  totalCodAmount = computed(() => {
    return this.stops().reduce((sum, s) => sum + s.codAmount, 0);
  });

  currentCurrency = computed(() => {
    return this.selectedHub()?.hub.currency || 'TND';
  });

  fleetTotalDistance = computed(() => {
    return this.optimizationResult()?.totalFleetDistanceKm || 0;
  });

  fleetTotalDuration = computed(() => {
    return this.optimizationResult()?.totalFleetDurationMinutes || 0;
  });

  fleetLatePenalty = computed(() => {
    return this.optimizationResult()?.totalFleetLatePenalty || 0;
  });

  mapDisplayStops = computed(() => {
    // If optimization is available, return sequenced waypoints, otherwise raw stops
    const opt = this.optimizationResult();
    if (opt && opt.routes.length > 0) {
      return opt.routes.flatMap(r => r.waypoints);
    }
    return this.stops();
  });

  // Coordinate Bounds for SVG projection
  geoBounds = computed(() => {
    const hub = this.selectedHub();
    const stopsList = this.stops();

    let minLat = hub ? hub.hub.latitude : 0;
    let maxLat = hub ? hub.hub.latitude : 0;
    let minLon = hub ? hub.hub.longitude : 0;
    let maxLon = hub ? hub.hub.longitude : 0;

    for (const s of stopsList) {
      if (s.latitude < minLat) minLat = s.latitude;
      if (s.latitude > maxLat) maxLat = s.latitude;
      if (s.longitude < minLon) minLon = s.longitude;
      if (s.longitude > maxLon) maxLon = s.longitude;
    }

    // Safety padding
    const latSpan = Math.max(maxLat - minLat, 0.05);
    const lonSpan = Math.max(maxLon - minLon, 0.05);

    return {
      minLat: minLat - latSpan * 0.15,
      maxLat: maxLat + latSpan * 0.15,
      minLon: minLon - lonSpan * 0.15,
      maxLon: maxLon + lonSpan * 0.15
    };
  });

  ngOnInit(): void {
    this.loadHubs();
  }

  loadHubs(): void {
    this.http.get<HubWithDriversDto[]>(`${this.apiBase}/hubs`).subscribe({
      next: (data) => {
        this.hubsData.set(data);
        if (data.length > 0 && !this.selectedHubId()) {
          this.selectedHubId.set(data[0].hub.hubId);
          this.loadStopsForHub(data[0].hub.hubId);
          this.loadRunsheetsForHub(data[0].hub.hubId);
        }
      },
      error: (err) => {
        console.error('Failed to load hubs:', err);
      }
    });
  }

  onHubChanged(newHubId: string): void {
    this.selectedHubId.set(newHubId);
    this.optimizationResult.set(null);
    this.selectedRunsheet.set(null);
    this.selectedStop.set(null);
    this.loadStopsForHub(newHubId);
    this.loadRunsheetsForHub(newHubId);
  }

  loadStopsForHub(hubId: string): void {
    this.http.get<DeliveryStopDto[]>(`${this.apiBase}/stops?hubId=${hubId}`).subscribe({
      next: (stops) => {
        this.stops.set(stops);
      },
      error: (err) => console.error('Failed to load stops:', err)
    });
  }

  loadRunsheetsForHub(hubId: string): void {
    this.http.get<DriverRunsheetManifestDto[]>(`${this.apiBase}/runsheets?hubId=${hubId}`).subscribe({
      next: (runsheets) => {
        this.runsheets.set(runsheets);
      },
      error: (err) => console.error('Failed to load runsheets:', err)
    });
  }

  runOptimization(): void {
    const hubId = this.selectedHubId();
    if (!hubId) return;

    this.isOptimizing.set(true);
    this.statusMessage.set(null);

    this.http.post<FleetOptimizationResultDto>(`${this.apiBase}/optimize`, {
      hubId,
      departureTime: '08:30:00',
      averageSpeedKmh: 35.0,
      serviceTimePerStopMinutes: 12,
      latePenaltyPerMinute: 0.5,
      maxIterations2Opt: 250
    }).subscribe({
      next: (result) => {
        this.optimizationResult.set(result);
        this.isOptimizing.set(false);
        this.statusMessage.set(
          `Routes optimized! ${result.totalStopsAssigned} stops allocated to ${result.totalDriversUsed} drivers (${result.totalFleetDistanceKm.toFixed(1)} km total).`
        );
      },
      error: (err) => {
        this.isOptimizing.set(false);
        console.error('Optimization failed:', err);
        this.statusMessage.set('Optimization failed: ' + (err.error?.error || err.message));
      }
    });
  }

  dispatchRunsheets(): void {
    const hubId = this.selectedHubId();
    const optResult = this.optimizationResult();
    if (!hubId || !optResult) return;

    this.isDispatching.set(true);

    this.http.post<DriverRunsheetManifestDto[]>(`${this.apiBase}/dispatch`, {
      hubId,
      optimizationResult: optResult
    }).subscribe({
      next: (manifests) => {
        this.isDispatching.set(false);
        this.runsheets.set(manifests);
        this.activeTab.set('runsheets');
        this.statusMessage.set(
          `Dispatched ${manifests.length} Driver Manifests (${manifests.map(m => m.runsheetCode).join(', ')}).`
        );
        this.loadStopsForHub(hubId);
      },
      error: (err) => {
        this.isDispatching.set(false);
        console.error('Dispatch failed:', err);
      }
    });
  }

  resetSeedData(): void {
    this.http.post(`${this.apiBase}/reset`, {}).subscribe({
      next: () => {
        this.optimizationResult.set(null);
        this.selectedRunsheet.set(null);
        this.statusMessage.set('Factory seed data restored.');
        this.loadHubs();
      },
      error: (err) => console.error('Reset failed:', err)
    });
  }

  selectStop(stop: DeliveryStopDto | RouteWaypointDto): void {
    this.selectedStop.set(stop);
  }

  // SVG coordinate transformation helpers
  getSvgX(lon: number): number {
    const bounds = this.geoBounds();
    const width = 600;
    const padding = 50;
    const lonRange = bounds.maxLon - bounds.minLon || 1;
    return padding + ((lon - bounds.minLon) / lonRange) * (width - 2 * padding);
  }

  getSvgY(lat: number): number {
    const bounds = this.geoBounds();
    const height = 450;
    const padding = 45;
    const latRange = bounds.maxLat - bounds.minLat || 1;
    // Invert Y axis for screen space
    return height - (padding + ((lat - bounds.minLat) / latRange) * (height - 2 * padding));
  }

  computeRouteSvgPath(route: DriverClusterRouteDto): string {
    const hub = this.selectedHub();
    if (!hub || route.waypoints.length === 0) return '';

    const hubX = this.getSvgX(hub.hub.longitude);
    const hubY = this.getSvgY(hub.hub.latitude);

    let path = `M ${hubX} ${hubY}`;
    for (const wp of route.waypoints) {
      const x = this.getSvgX(wp.longitude);
      const y = this.getSvgY(wp.latitude);
      path += ` L ${x} ${y}`;
    }
    // Return leg to Hub
    path += ` L ${hubX} ${hubY}`;
    return path;
  }

  getClusterColor(index: number): string {
    const colors = ['#06b6d4', '#10b981', '#f59e0b', '#a855f7', '#ec4899'];
    return colors[index % colors.length];
  }

  getStopMarkerColor(stop: any): string {
    const opt = this.optimizationResult();
    if (opt) {
      for (let i = 0; i < opt.routes.length; i++) {
        const found = opt.routes[i].waypoints.some(w => w.stopId === stop.stopId);
        if (found) {
          return this.getClusterColor(i);
        }
      }
    }
    return '#64748b'; // default slate color
  }
}
