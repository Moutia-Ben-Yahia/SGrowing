namespace SGrowing.Api.Modules.Telecom.SubseaDwdmRamanCompensator;

public interface ISubseaDwdmRamanCompensatorEngine
{
    KerrNonlinearCompensationPlan EvaluateAndCompensate(RamanCompensationRequest request);
}

public class SubseaDwdmRamanCompensatorEngine : ISubseaDwdmRamanCompensatorEngine
{
    private const double NonlinearRefractiveIndexN2 = 2.4e-20; // m^2 / W

    public KerrNonlinearCompensationPlan EvaluateAndCompensate(RamanCompensationRequest request)
    {
        var span = request.Span;
        var channels = request.Channels ?? [];

        if (channels.Count == 0)
        {
            return new KerrNonlinearCompensationPlan(
                SpanId: span.SpanId,
                SpectralPowerTiltDb: 0.0,
                NonlinearPhaseShiftRadians: 0.0,
                MeanOsnrDb: 25.0,
                NonlinearityThresholdExceeded: false,
                OpticalEqualizationStrategy: "NO_ACTIVE_CHANNELS: Standby mode.",
                PumpAdjustments: []
            );
        }

        // Sort channels by wavelength
        var sorted = channels.OrderBy(c => c.CenterWavelengthNm).ToList();
        double shortWaveOsnr = sorted.First().ReceivedOsnrDb;
        double longWaveOsnr = sorted.Last().ReceivedOsnrDb;

        // Stimulated Raman Scattering (SRS) transfers energy from short to long wavelengths:
        // Spectral tilt = longWavePower - shortWavePower
        double powerTiltDb = Math.Max(0.0, longWaveOsnr - shortWaveOsnr);
        double meanOsnr = channels.Average(c => c.ReceivedOsnrDb);

        // Nonlinear Kerr Phase Shift: Phi_NL = gamma * P_total * L_eff
        // gamma = (2 * pi * n2) / (lambda * A_eff)
        double lambdaMeters = 1550e-9;
        double aEffMeters2 = span.EffectiveAreaUm2 * 1e-12;
        double gamma = (2.0 * Math.PI * NonlinearRefractiveIndexN2) / (lambdaMeters * aEffMeters2);

        // Effective length L_eff = (1 - exp(-alpha * L)) / alpha
        double alphaPerMeter = (span.AttenuationDbPerKm / 4.343) / 1000.0;
        double lMeters = span.SpanLengthKm * 1000.0;
        double lEffMeters = (1.0 - Math.Exp(-alphaPerMeter * lMeters)) / alphaPerMeter;

        double totalPowerWatts = Math.Pow(10.0, (request.TotalInputPowerDbm - 30.0) / 10.0);
        double nonlinearPhaseShiftRad = gamma * totalPowerWatts * lEffMeters;

        // Threshold: Nonlinear phase shift > 1.2 radians causes severe XPM/SPM constellation penalty in 64-QAM
        bool nonlinearityExcess = nonlinearPhaseShiftRad >= 1.2;

        // Synthesize Counter-Propagating Raman Pump Adjustments
        // Pump 1: 1420 nm (boosts short-wavelength C-band)
        // Pump 2: 1450 nm (mid C-band)
        // Pump 3: 1480 nm (L-band)
        double basePumpPowerMw = 250.0;
        double tiltCompensationDeltaMw = powerTiltDb * 45.0; // Boost short pump more to counter SRS tilt

        var pumpAdjustments = new List<RamanPumpAdjustment>
        {
            new(1, 1420.0, Math.Round(basePumpPowerMw + tiltCompensationDeltaMw, 1), "SRS_TILT_CORRECTION: Boost short-wave gain to compensate energy depletion"),
            new(2, 1450.0, Math.Round(basePumpPowerMw, 1), "MID_BAND_FLATNESS: Balance central DWDM spectrum"),
            new(3, 1480.0, Math.Round(Math.Max(100.0, basePumpPowerMw - (tiltCompensationDeltaMw * 0.5)), 1), "GAIN_SUPPRESSION: Trim long-wave pump to prevent nonlinear saturation")
        };

        string strategy;
        if (nonlinearityExcess && powerTiltDb >= 3.0)
        {
            strategy = $"KERR_NONLINEAR_OVERDRIVE & SRS_SPECTRAL_TILT_ACTIVE: Total phase shift ({nonlinearPhaseShiftRad:F2} rad) exceeds 1.2 rad limit and Raman tilt is {powerTiltDb:F1} dB. Lower launch power by 1.5 dB, enable DBP and apply asymmetric pump profile.";
        }
        else if (nonlinearityExcess)
        {
            strategy = $"KERR_NONLINEAR_OVERDRIVE: Total phase shift ({nonlinearPhaseShiftRad:F2} rad) exceeds 1.2 rad limit. Lower launch power by 1.5 dB and enable 2-step Digital Back-Propagation (DBP).";
        }
        else if (powerTiltDb >= 3.0)
        {
            strategy = $"SRS_SPECTRAL_TILT_ACTIVE: Raman tilt ({powerTiltDb:F1} dB) compensated via asymmetric pump profile.";
        }
        else
        {
            strategy = "OPTICAL_CHANNEL_NOMINAL: OSNR flatness and nonlinear phase margins optimal for coherent 64-QAM.";
        }

        return new KerrNonlinearCompensationPlan(
            SpanId: span.SpanId,
            SpectralPowerTiltDb: Math.Round(powerTiltDb, 2),
            NonlinearPhaseShiftRadians: Math.Round(nonlinearPhaseShiftRad, 3),
            MeanOsnrDb: Math.Round(meanOsnr, 1),
            NonlinearityThresholdExceeded: nonlinearityExcess,
            OpticalEqualizationStrategy: strategy,
            PumpAdjustments: pumpAdjustments
        );
    }
}
