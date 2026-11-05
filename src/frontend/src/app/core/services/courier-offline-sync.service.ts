import { Injectable, inject, signal, computed, OnDestroy } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { firstValueFrom } from 'rxjs';

export interface CourierDeliveryItem {
  parcelId: string;
  trackingNumber: string;
  courierDriverId: string;
  wilayaCode: string;
  status: 'Delivered' | 'FailedAttempt' | 'Returned';
  cashCollected: number;
  customerSignatureHash?: string | null;
  recordedAtUtc: string;
}

export interface CourierDeliverySyncBatchRequest {
  tenantId: string;
  deliveries: CourierDeliveryItem[];
}

export interface CourierDeliverySyncBatchResult {
  totalReceived: number;
  successfullySynced: number;
  duplicatesIgnored: number;
  totalCashReconciled: number;
  syncedParcelIds: string[];
}

@Injectable({
  providedIn: 'root'
})
export class CourierOfflineSyncService implements OnDestroy {
  private readonly http = inject(HttpClient);
  private readonly storageKey = 'sgrowing_courier_offline_queue';
  private readonly apiEndpoint = '/api/v1/courier/deliveries/sync';

  public readonly isOnline = signal<boolean>(
    typeof navigator !== 'undefined' ? navigator.onLine : true
  );
  public readonly pendingDeliveries = signal<CourierDeliveryItem[]>(this.loadQueueFromStorage());
  public readonly pendingCount = computed(() => this.pendingDeliveries().length);
  public readonly isSyncing = signal<boolean>(false);
  public readonly lastSyncResult = signal<CourierDeliverySyncBatchResult | null>(null);

  private readonly onlineHandler = () => this.handleNetworkChange(true);
  private readonly offlineHandler = () => this.handleNetworkChange(false);

  constructor() {
    if (typeof window !== 'undefined') {
      window.addEventListener('online', this.onlineHandler);
      window.addEventListener('offline', this.offlineHandler);
    }
  }

  ngOnDestroy(): void {
    if (typeof window !== 'undefined') {
      window.removeEventListener('online', this.onlineHandler);
      window.removeEventListener('offline', this.offlineHandler);
    }
  }

  public handleNetworkChange(online: boolean): void {
    this.isOnline.set(online);
  }

  public queueDelivery(item: CourierDeliveryItem): void {
    const current = this.loadQueueFromStorage();
    const updated = [...current, item];
    this.persistQueue(updated);
    this.pendingDeliveries.set(updated);
  }

  public async syncPendingDeliveries(tenantId: string): Promise<CourierDeliverySyncBatchResult | null> {
    const queue = this.pendingDeliveries();
    if (!this.isOnline() || queue.length === 0 || this.isSyncing()) {
      return null;
    }

    this.isSyncing.set(true);

    try {
      const request: CourierDeliverySyncBatchRequest = {
        tenantId,
        deliveries: queue
      };

      const result = await firstValueFrom(
        this.http.post<CourierDeliverySyncBatchResult>(this.apiEndpoint, request)
      );

      this.lastSyncResult.set(result);

      // Remove successfully synced or duplicate parcels from queue
      const syncedSet = new Set(result.syncedParcelIds);
      const remaining = queue.filter(item => !syncedSet.has(item.parcelId));

      this.persistQueue(remaining);
      this.pendingDeliveries.set(remaining);

      return result;
    } catch (err) {
      // Retain queue on network or server error to avoid data loss
      return null;
    } finally {
      this.isSyncing.set(false);
    }
  }

  public clearQueue(): void {
    this.persistQueue([]);
    this.pendingDeliveries.set([]);
  }

  private loadQueueFromStorage(): CourierDeliveryItem[] {
    try {
      if (typeof localStorage !== 'undefined') {
        const raw = localStorage.getItem(this.storageKey);
        if (raw) {
          return JSON.parse(raw);
        }
      }
    } catch {
      // Fallback if localStorage corrupted
    }
    return [];
  }

  private persistQueue(items: CourierDeliveryItem[]): void {
    try {
      if (typeof localStorage !== 'undefined') {
        localStorage.setItem(this.storageKey, JSON.stringify(items));
      }
    } catch {
      // Ignore storage quota errors
    }
  }
}
