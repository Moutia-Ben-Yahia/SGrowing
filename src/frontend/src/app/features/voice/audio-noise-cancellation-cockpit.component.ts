import { Component, ChangeDetectionStrategy, signal, computed } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';

export interface AudioChannelTelemetry {
  channelId: string;
  repName: string;
  callSessionId: string;
  noiseFloorDb: number;
  snrDb: number;
  meanOpinionScoreMos: number;
  detectedNoise: 'None' | 'AmbientHvac' | 'KeyboardTyping' | 'StreetTraffic' | 'BackgroundChatter';
  isFilterActive: boolean;
  dspLatencyMs: number;
}

@Component({
  selector: 'app-audio-noise-cancellation-cockpit',
  standalone: true,
  imports: [CommonModule, FormsModule],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="p-6 bg-slate-900 text-slate-100 min-h-screen" dir="ltr" data-testid="audio-noise-cancellation-cockpit">
      <header class="mb-6 flex flex-col md:flex-row md:items-center justify-between gap-4 border-b border-slate-800 pb-4">
        <div>
          <div class="flex items-center gap-3">
            <span class="px-2.5 py-1 text-xs font-semibold rounded bg-purple-500/20 text-purple-400 border border-purple-500/30">Horizon 103</span>
            <span class="text-xs text-slate-400">WebRTC Acoustic DSP & Audio Processing</span>
          </div>
          <h1 class="text-2xl font-bold tracking-tight text-white mt-1">Autonomous Real-Time Audio Noise Cancellation & Speech Isolation DSP</h1>
          <p class="text-sm text-slate-400">Sub-15ms spectral gating, human vocal formant isolation (300-3400 Hz), and live MOS audio telemetry.</p>
        </div>
      </header>

      <!-- KPI Summary -->
      <section class="grid grid-cols-1 md:grid-cols-4 gap-4 mb-6">
        <div class="bg-slate-800/80 border border-slate-700/60 p-4 rounded-xl" data-testid="kpi-active-streams">
          <div class="text-xs font-medium text-slate-400 uppercase">Active Audio Streams</div>
          <div class="text-2xl font-bold text-white mt-1">{{ channels().length }} Channels</div>
          <div class="text-xs text-slate-400 mt-1">Real-time WebRTC audio processing</div>
        </div>
        <div class="bg-slate-800/80 border border-slate-700/60 p-4 rounded-xl" data-testid="kpi-snr-gain">
          <div class="text-xs font-medium text-slate-400 uppercase">Avg SNR Gain</div>
          <div class="text-2xl font-bold text-emerald-400 mt-1">+16.8 dB</div>
          <div class="text-xs text-slate-400 mt-1">Spectral gating noise reduction</div>
        </div>
        <div class="bg-slate-800/80 border border-slate-700/60 p-4 rounded-xl" data-testid="kpi-avg-mos">
          <div class="text-xs font-medium text-slate-400 uppercase">Acoustic Clarity (MOS)</div>
          <div class="text-2xl font-bold text-purple-400 mt-1">{{ avgMos() | number:'1.2-2' }} / 5.0</div>
          <div class="text-xs text-slate-400 mt-1">Mean Opinion Score voice quality</div>
        </div>
        <div class="bg-slate-800/80 border border-slate-700/60 p-4 rounded-xl" data-testid="kpi-dsp-latency">
          <div class="text-xs font-medium text-slate-400 uppercase">Peak DSP Latency</div>
          <div class="text-2xl font-bold text-cyan-400 mt-1">&lt;11 ms</div>
          <div class="text-xs text-slate-400 mt-1">Zero perceivable speech delay</div>
        </div>
      </section>

      <!-- Active Audio Channels Grid -->
      <section class="space-y-4 mb-6">
        <h2 class="text-lg font-semibold text-white">Live Rep Audio Streams & Spectral Noise Telemetry</h2>

        <div class="grid grid-cols-1 md:grid-cols-3 gap-6">
          @for (c of channels(); track c.channelId) {
            <div class="bg-slate-800/70 border border-slate-700/60 rounded-xl p-5 flex flex-col justify-between"
                 [attr.data-testid]="'channel-card-' + c.channelId">
              <div>
                <div class="flex items-center justify-between mb-3">
                  <span class="text-xs font-mono text-purple-400">{{ c.channelId }}</span>
                  <span class="px-2 py-0.5 rounded text-xs font-semibold"
                        [ngClass]="c.isFilterActive ? 'bg-emerald-500/20 text-emerald-300' : 'bg-rose-500/20 text-rose-300'">
                    {{ c.isFilterActive ? 'DSP Active' : 'Filter Bypassed' }}
                  </span>
                </div>

                <div class="font-semibold text-white text-base">{{ c.repName }}</div>
                <div class="text-xs text-slate-400 mb-4">{{ c.callSessionId }}</div>

                <div class="space-y-2 bg-slate-900/60 p-3 rounded-lg mb-4 text-xs font-mono">
                  <div class="flex justify-between">
                    <span class="text-slate-400">Detected Noise:</span>
                    <span class="font-semibold text-amber-300">{{ c.detectedNoise }}</span>
                  </div>
                  <div class="flex justify-between">
                    <span class="text-slate-400">Noise Floor:</span>
                    <span class="text-slate-200">{{ c.noiseFloorDb }} dB</span>
                  </div>
                  <div class="flex justify-between">
                    <span class="text-slate-400">SNR Ratio:</span>
                    <span class="font-bold text-emerald-400">{{ c.snrDb }} dB</span>
                  </div>
                  <div class="flex justify-between border-t border-slate-800 pt-1">
                    <span class="text-slate-400">Clarity MOS:</span>
                    <span class="font-bold text-purple-300">{{ c.meanOpinionScoreMos | number:'1.2-2' }}</span>
                  </div>
                </div>
              </div>

              <button class="w-full py-2 rounded text-xs font-semibold transition-colors"
                      [ngClass]="c.isFilterActive ? 'bg-slate-700 hover:bg-slate-600 text-slate-200' : 'bg-purple-600 hover:bg-purple-500 text-white'"
                      (click)="toggle(c.channelId)"
                      [attr.data-testid]="'btn-toggle-' + c.channelId">
                {{ c.isFilterActive ? 'Bypass DSP Filter' : 'Engage Noise Cancellation' }}
              </button>
            </div>
          }
        </div>
      </section>

      <!-- Action Toast -->
      @if (lastDspNotice()) {
        <div class="p-3 bg-purple-950/60 border border-purple-500/40 rounded-xl text-purple-300 text-xs flex items-center justify-between"
             data-testid="dsp-notice">
          <span>{{ lastDspNotice() }}</span>
          <button (click)="lastDspNotice.set(null)" class="text-slate-400 hover:text-white">&times;</button>
        </div>
      }
    </div>
  `
})
export class AudioNoiseCancellationCockpitComponent {
  readonly channels = signal<AudioChannelTelemetry[]>([
    {
      channelId: 'VOX-DSP-01',
      repName: 'Victoria Sterling',
      callSessionId: 'CALL-SES-781',
      noiseFloorDb: -32.5,
      snrDb: 18.2,
      meanOpinionScoreMos: 4.6,
      detectedNoise: 'KeyboardTyping',
      isFilterActive: true,
      dspLatencyMs: 8
    },
    {
      channelId: 'VOX-DSP-02',
      repName: 'Marcus Vance',
      callSessionId: 'CALL-SES-782',
      noiseFloorDb: -26.0,
      snrDb: 12.4,
      meanOpinionScoreMos: 3.8,
      detectedNoise: 'StreetTraffic',
      isFilterActive: false,
      dspLatencyMs: 6
    },
    {
      channelId: 'VOX-DSP-03',
      repName: 'Dr. Evelyn Reed',
      callSessionId: 'CALL-SES-783',
      noiseFloorDb: -42.0,
      snrDb: 24.8,
      meanOpinionScoreMos: 4.85,
      detectedNoise: 'AmbientHvac',
      isFilterActive: true,
      dspLatencyMs: 9
    }
  ]);

  readonly lastDspNotice = signal<string | null>(null);

  readonly avgMos = computed(() => {
    const list = this.channels();
    return list.length > 0 ? list.reduce((sum, c) => sum + c.meanOpinionScoreMos, 0) / list.length : 4.4;
  });

  toggle(channelId: string): void {
    this.channels.update(list =>
      list.map(c => c.channelId === channelId ? {
        ...c,
        isFilterActive: !c.isFilterActive,
        meanOpinionScoreMos: !c.isFilterActive ? 4.7 : 3.6,
        snrDb: !c.isFilterActive ? c.snrDb + 8.5 : Math.max(10, c.snrDb - 8.5)
      } : c)
    );

    const updated = this.channels().find(c => c.channelId === channelId);
    this.lastDspNotice.set(`Channel ${channelId}: Noise filter is now ${updated?.isFilterActive ? 'Engaged (-24dB spectral suppression)' : 'Bypassed'}.`);
  }
}
