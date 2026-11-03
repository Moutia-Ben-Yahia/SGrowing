import { Component, signal, ChangeDetectionStrategy } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';

export interface WeatherRuleItem {
  ruleId: string;
  campaignId: string;
  conditionType: string;
  geoTargetMetro: string;
  targetedVariantCreativeId: string;
  bidMultiplier: number;
  isActive: boolean;
}

export interface WeatherObservationItem {
  observationId: string;
  geoTargetMetro: string;
  temperatureCelsius: number;
  precipitationProbabilityPct: number;
  airQualityIndexAqi: number;
  weatherConditionSummary: string;
}

@Component({
  selector: 'app-weather-triggers-cockpit',
  standalone: true,
  imports: [CommonModule, FormsModule],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="cockpit-container" dir="ltr" data-testid="weather-triggers-cockpit">
      <header class="cockpit-header">
        <div class="badge">Horizon 157 · Environmental Ad Tech</div>
        <h1>Autonomous Dynamic Product Ads (DPA) Weather Context Engine</h1>
        <p class="subtitle">Real-time localized NOAA weather telemetry, dynamic programmatic creative variant injection, and environmental bid multipliers</p>
      </header>

      <section class="metrics-grid">
        <div class="metric-card" data-testid="metric-active-rules">
          <div class="metric-label">Active Weather Rules</div>
          <div class="metric-value positive">{{ activeRulesCount() }}</div>
          <div class="metric-sub">Environmental triggers armed</div>
        </div>
        <div class="metric-card" data-testid="metric-monitored-metros">
          <div class="metric-label">Monitored Metros</div>
          <div class="metric-value">{{ monitoredMetrosCount() }}</div>
          <div class="metric-sub">Global micro-climate telemetry zones</div>
        </div>
        <div class="metric-card" data-testid="metric-triggered-events">
          <div class="metric-label">Triggered (24h)</div>
          <div class="metric-value">{{ triggeredEventsCount() }}</div>
          <div class="metric-sub">Automated ad creative activations</div>
        </div>
        <div class="metric-card" data-testid="metric-roas-lift">
          <div class="metric-label">Avg ROAS Lift</div>
          <div class="metric-value positive">+{{ roasLiftMultiplier() }}x</div>
          <div class="metric-sub">Contextual relevancy conversion premium</div>
        </div>
      </section>

      <section class="rules-section">
        <div class="section-header">
          <h2>Programmatic Weather Trigger Rules</h2>
          <button class="action-btn" (click)="triggerNewCampaign()" data-testid="trigger-campaign-btn">
            + Arm Weather Trigger
          </button>
        </div>
        <div class="table-wrapper">
          <table class="data-table" data-testid="rules-table">
            <thead>
              <tr>
                <th>Rule ID</th>
                <th>Campaign</th>
                <th>Condition Trigger</th>
                <th>Geo Metro</th>
                <th>Variant Creative</th>
                <th>Bid Multiplier</th>
                <th>Status</th>
              </tr>
            </thead>
            <tbody>
              @for (rule of rules(); track rule.ruleId) {
                <tr [attr.data-testid]="'rule-row-' + rule.ruleId">
                  <td><code>{{ rule.ruleId }}</code></td>
                  <td><strong>{{ rule.campaignId }}</strong></td>
                  <td><span class="trigger-badge">{{ rule.conditionType }}</span></td>
                  <td><span class="metro-pill">{{ rule.geoTargetMetro }}</span></td>
                  <td><code>{{ rule.targetedVariantCreativeId }}</code></td>
                  <td><strong>{{ rule.bidMultiplier }}x</strong></td>
                  <td><span class="status-pill status-active">ARMED</span></td>
                </tr>
              }
            </tbody>
          </table>
        </div>
      </section>

      <section class="observations-section">
        <h2>Live Meteorological Telemetry Feeds</h2>
        <div class="obs-grid" data-testid="observations-grid">
          @for (obs of observations(); track obs.observationId) {
            <div class="obs-card" [attr.data-testid]="'obs-card-' + obs.observationId">
              <div class="obs-header">
                <strong>{{ obs.geoTargetMetro }}</strong>
                <span class="temp-badge">{{ obs.temperatureCelsius }}°C</span>
              </div>
              <div class="obs-body">
                <p><strong>Condition:</strong> {{ obs.weatherConditionSummary }}</p>
                <p><strong>Rain Probability:</strong> {{ obs.precipitationProbabilityPct }}%</p>
                <p><strong>Air Quality (AQI):</strong> {{ obs.airQualityIndexAqi }}</p>
              </div>
            </div>
          }
        </div>
      </section>
    </div>
  `,
  styles: [`
    .cockpit-container { padding: 24px; max-width: 1200px; margin: 0 auto; font-family: system-ui, -apple-system, sans-serif; color: #1e293b; }
    .cockpit-header { margin-bottom: 24px; }
    .badge { display: inline-block; padding: 4px 10px; border-radius: 9999px; background: #e0f2fe; color: #0369a1; font-size: 12px; font-weight: 600; text-transform: uppercase; margin-bottom: 8px; }
    h1 { font-size: 26px; font-weight: 700; margin: 0 0 6px 0; color: #0f172a; }
    .subtitle { font-size: 14px; color: #64748b; margin: 0; }
    .metrics-grid { display: grid; grid-template-columns: repeat(auto-fit, minmax(240px, 1fr)); gap: 16px; margin-bottom: 28px; }
    .metric-card { background: #ffffff; border: 1px solid #e2e8f0; border-radius: 8px; padding: 18px; box-shadow: 0 1px 3px rgba(0,0,0,0.05); }
    .metric-label { font-size: 13px; font-weight: 500; color: #64748b; margin-bottom: 6px; }
    .metric-value { font-size: 28px; font-weight: 700; color: #0f172a; }
    .metric-value.positive { color: #16a34a; }
    .metric-sub { font-size: 12px; color: #94a3b8; margin-top: 4px; }
    .section-header { display: flex; justify-content: space-between; align-items: center; margin-bottom: 14px; }
    .action-btn { background: #0284c7; color: #ffffff; border: none; padding: 8px 14px; border-radius: 4px; font-size: 13px; font-weight: 600; cursor: pointer; }
    .table-wrapper { overflow-x: auto; background: #ffffff; border: 1px solid #e2e8f0; border-radius: 8px; }
    .data-table { width: 100%; border-collapse: collapse; text-align: left; font-size: 13px; }
    .data-table th, .data-table td { padding: 12px 16px; border-bottom: 1px solid #e2e8f0; }
    .data-table th { background: #f8fafc; color: #475569; font-weight: 600; }
    code { background: #f8fafc; padding: 2px 6px; border-radius: 4px; font-size: 11px; }
    .trigger-badge { background: #fef3c7; color: #b45309; font-size: 11px; font-weight: 600; padding: 2px 6px; border-radius: 4px; }
    .metro-pill { background: #f1f5f9; color: #334155; font-size: 11px; font-weight: 700; padding: 2px 6px; border-radius: 4px; }
    .status-pill { padding: 2px 8px; border-radius: 9999px; font-size: 11px; font-weight: 600; }
    .status-active { background: #dcfce7; color: #15803d; }
    .obs-grid { display: grid; grid-template-columns: repeat(auto-fit, minmax(320px, 1fr)); gap: 16px; margin-top: 14px; }
    .obs-card { background: #ffffff; border: 1px solid #e2e8f0; border-radius: 8px; padding: 16px; }
    .obs-header { display: flex; justify-content: space-between; margin-bottom: 8px; }
    .temp-badge { background: #fee2e2; color: #991b1b; font-size: 11px; font-weight: 700; padding: 2px 8px; border-radius: 4px; }
    .obs-body p { margin: 4px 0; font-size: 12px; }
  `]
})
export class WeatherTriggersCockpitComponent {
  activeRulesCount = signal(2);
  monitoredMetrosCount = signal(2);
  triggeredEventsCount = signal(1);
  roasLiftMultiplier = signal(1.42);

  rules = signal<WeatherRuleItem[]>([
    {
      ruleId: 'WTR-RULE-001',
      campaignId: 'CAMP-BEVERAGE-COLDBREW',
      conditionType: 'HEATWAVE_ABOVE_32C',
      geoTargetMetro: 'US_AUSTIN_TX',
      targetedVariantCreativeId: 'CREATIVE-COLD-BREW-ICE-HERO',
      bidMultiplier: 1.45,
      isActive: true
    },
    {
      ruleId: 'WTR-RULE-002',
      campaignId: 'CAMP-RIDEHAIL-SURGE',
      conditionType: 'RAIN_PRECIPITATION_ABOVE_80PCT',
      geoTargetMetro: 'UK_LONDON_METRO',
      targetedVariantCreativeId: 'CREATIVE-RAIN-CAB-DISPATCH',
      bidMultiplier: 1.60,
      isActive: true
    }
  ]);

  observations = signal<WeatherObservationItem[]>([
    {
      observationId: 'OBS-001',
      geoTargetMetro: 'US_AUSTIN_TX',
      temperatureCelsius: 34.8,
      precipitationProbabilityPct: 5.0,
      airQualityIndexAqi: 42,
      weatherConditionSummary: 'SUNNY_HEAT'
    },
    {
      observationId: 'OBS-002',
      geoTargetMetro: 'UK_LONDON_METRO',
      temperatureCelsius: 13.2,
      precipitationProbabilityPct: 92.0,
      airQualityIndexAqi: 24,
      weatherConditionSummary: 'HEAVY_RAIN'
    }
  ]);

  triggerNewCampaign(): void {
    const newRule: WeatherRuleItem = {
      ruleId: `WTR-RULE-00${this.rules().length + 1}`,
      campaignId: 'CAMP-BEVERAGE-HOTTEA',
      conditionType: 'FREEZE_BELOW_0C',
      geoTargetMetro: 'US_CHICAGO_IL',
      targetedVariantCreativeId: 'CREATIVE-HOT-HERBAL-TEA',
      bidMultiplier: 1.55,
      isActive: true
    };
    this.rules.update(list => [...list, newRule]);
    this.activeRulesCount.update(v => v + 1);
  }
}
