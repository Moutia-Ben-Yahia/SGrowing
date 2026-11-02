namespace SGrowing.Api.Modules.BioTech.NanoporeTranslocationBasecaller;

public interface INanoporeTranslocationEngine
{
    NanoporeTranslocationDecision EvaluateTranslocation(NanoporeEvaluationRequest request);
}

public class NanoporeTranslocationEngine : INanoporeTranslocationEngine
{
    public NanoporeTranslocationDecision EvaluateTranslocation(NanoporeEvaluationRequest request)
    {
        var tel = request.Telemetry;
        var thresh = request.Thresholds;
        var samples = tel.CurrentSignal ?? [];

        if (samples.Count == 0)
        {
            return new NanoporeTranslocationDecision(
                ChannelId: tel.ChannelId,
                TotalKmersTranslocated: 0,
                EstimatedPolyATailLengthNucleotides: 0,
                MethylatedCpGCount: 0,
                MeanTranslocationSpeedBps: 0.0,
                PoreStallEventDetected: false,
                ChannelState: "OPEN_PORE_AWAITING_CAPTURE",
                RecommendedPoreControlDirective: "IDLE_MONITOR: Channel open at nominal baseline current. Awaiting single-molecule capture event."
            );
        }

        int polyACount = 0;
        int methylatedCount = 0;
        double totalDwellMicro = 0.0;
        bool stallDetected = false;

        double openCurrent = Math.Max(50.0, tel.OpenPoreCurrentPicoAmperes);
        double targetPolyACurrent = openCurrent * thresh.PolyACurrentLevelFraction;

        foreach (var s in samples)
        {
            totalDwellMicro += s.DwellTimeMicroseconds;

            if (s.DwellTimeMicroseconds >= thresh.MaxStallDwellTimeMicroseconds)
            {
                stallDetected = true;
            }

            // Check if current matches poly-A homopolymer signature (+/- 6 pA)
            if (Math.Abs(s.CurrentPicoAmperes - targetPolyACurrent) <= 6.0)
            {
                polyACount++;
            }

            // Check if methylation current perturbation signature detected
            if (Math.Abs(s.CurrentPicoAmperes - (openCurrent * 0.35)) <= thresh.MethylationShiftPicoAmperes)
            {
                methylatedCount++;
            }
        }

        double meanDwellMicro = samples.Count > 0 ? (totalDwellMicro / samples.Count) : 2500.0;
        double speedBps = meanDwellMicro > 0 ? (1e6 / meanDwellMicro) : 400.0;
        speedBps = Math.Clamp(speedBps, 10.0, 1500.0);

        string channelState;
        string directive;

        if (stallDetected)
        {
            channelState = "MOLECULAR_PORE_STALL";
            directive = "TRIGGER_REVERSE_PULSE: Polymer translocation stalled exceeding 50ms dwell limit. Apply -250 mV reverse electrophoretic bias pulse for 10ms to clear pore constriction.";
        }
        else if (polyACount > 15)
        {
            channelState = "POLYA_HOMOPOLYMER_TRANSLOCATING";
            directive = $"SEQUENCE_POLYA: Poly-A tail translocation in progress ({polyACount} nt detected). Maintaining motor protein ratcheting voltage at {tel.AppliedVoltageBiasMilliVolts:F0} mV.";
        }
        else
        {
            channelState = "SEQUENCING_TRANSLOCATION_ACTIVE";
            directive = $"CONTINUE_BASECALLING: Translocation rate nominal ({speedBps:F0} bps). Epigenetic CpG methylation and standard 5-mer contexts passing neural basecaller.";
        }

        return new NanoporeTranslocationDecision(
            ChannelId: tel.ChannelId,
            TotalKmersTranslocated: samples.Count,
            EstimatedPolyATailLengthNucleotides: polyACount,
            MethylatedCpGCount: methylatedCount,
            MeanTranslocationSpeedBps: Math.Round(speedBps, 1),
            PoreStallEventDetected: stallDetected,
            ChannelState: channelState,
            RecommendedPoreControlDirective: directive
        );
    }
}
