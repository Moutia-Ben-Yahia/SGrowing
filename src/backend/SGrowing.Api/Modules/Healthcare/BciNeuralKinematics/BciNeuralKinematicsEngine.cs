namespace SGrowing.Api.Modules.Healthcare.BciNeuralKinematics;

public interface IBciNeuralKinematicsEngine
{
    BciKinematicsDecodeResult DecodeKinematics(BciKinematicsDecodeRequest request);
}

public class BciNeuralKinematicsEngine : IBciNeuralKinematicsEngine
{
    private const double VelocityDampingFactor = 0.82; // Autoregressive damping to cancel drift
    private const double MinSpikesThreshold = 4;        // Minimum population spikes required for active motor decoding

    public BciKinematicsDecodeResult DecodeKinematics(BciKinematicsDecodeRequest request)
    {
        var packets = request.ChannelPackets ?? [];
        int totalSpikes = packets.Sum(p => p.SpikeCountInBin);
        int activeChannels = packets.Count(p => p.SpikeCountInBin > 0 && p.SignalToNoiseRatio >= 2.5);

        if (totalSpikes < MinSpikesThreshold || activeChannels == 0)
        {
            // Decaying velocity to rest when motor cortex is quiescent
            double decayedVx = request.PreviousVx * 0.4;
            double decayedVy = request.PreviousVy * 0.4;
            double decayedVz = request.PreviousVz * 0.4;
            double speed = Math.Sqrt(decayedVx * decayedVx + decayedVy * decayedVy + decayedVz * decayedVz);

            return new BciKinematicsDecodeResult(
                SubjectId: request.SubjectId,
                TotalSpikesCount: totalSpikes,
                ActiveChannelsCount: activeChannels,
                Kinematics: new DecodedKinematicVector(
                    VelocityX: Math.Round(decayedVx, 2),
                    VelocityY: Math.Round(decayedVy, 2),
                    VelocityZ: Math.Round(decayedVz, 2),
                    SpeedMagnitudeMmPerSec: Math.Round(speed, 2),
                    DominantDirection: "IDLE_REST"
                ),
                DecodingConfidence: 0.95,
                MotorIntentState: "QUIESCENT_REST: No active motor reach intention detected."
            );
        }

        // Kalman Filter measurement projection:
        // Each channel has a preferred cosine tuning direction:
        // theta_ch = (ChannelId * 2 * PI) / 96, phi_ch = (ChannelId * PI) / 96
        double sumVx = 0.0;
        double sumVy = 0.0;
        double sumVz = 0.0;
        double totalWeight = 0.0;

        foreach (var p in packets.Where(p => p.SpikeCountInBin > 0))
        {
            double angleAzimuth = (p.ChannelId * 2.0 * Math.PI) / 96.0;
            double angleElevation = (p.ChannelId * Math.PI) / 96.0;

            // Preferred tuning vector for this cortical unit
            double preferredX = Math.Cos(angleAzimuth) * Math.Cos(angleElevation);
            double preferredY = Math.Sin(angleAzimuth) * Math.Cos(angleElevation);
            double preferredZ = Math.Sin(angleElevation);

            // Weight by spike rate and signal-to-noise ratio
            double weight = p.SpikeCountInBin * Math.Clamp(p.SignalToNoiseRatio / 3.0, 0.5, 3.0);

            sumVx += preferredX * weight;
            sumVy += preferredY * weight;
            sumVz += preferredZ * weight;
            totalWeight += weight;
        }

        double rawVx = totalWeight > 0 ? (sumVx / totalWeight) * 120.0 : 0.0; // Scale to mm/s
        double rawVy = totalWeight > 0 ? (sumVy / totalWeight) * 120.0 : 0.0;
        double rawVz = totalWeight > 0 ? (sumVz / totalWeight) * 120.0 : 0.0;

        // Kalman State Update: x_t = A * x_{t-1} + K * (z_t - H * A * x_{t-1})
        // Kalman Gain K approx 0.35 with damping A = 0.82
        double kGain = 0.38;
        double newVx = (VelocityDampingFactor * request.PreviousVx) + (kGain * rawVx);
        double newVy = (VelocityDampingFactor * request.PreviousVy) + (kGain * rawVy);
        double newVz = (VelocityDampingFactor * request.PreviousVz) + (kGain * rawVz);

        double speedMmS = Math.Sqrt(newVx * newVx + newVy * newVy + newVz * newVz);

        // Identify dominant directional intent
        string dominantDir;
        if (Math.Abs(newVx) >= Math.Abs(newVy) && Math.Abs(newVx) >= Math.Abs(newVz))
        {
            dominantDir = newVx > 0 ? "+X (Lateral Right Reach)" : "-X (Medial Left Reach)";
        }
        else if (Math.Abs(newVy) >= Math.Abs(newVx) && Math.Abs(newVy) >= Math.Abs(newVz))
        {
            dominantDir = newVy > 0 ? "+Y (Anterior Reach Forward)" : "-Y (Posterior Retraction)";
        }
        else
        {
            dominantDir = newVz > 0 ? "+Z (Superior Elevation)" : "-Z (Inferior Depression)";
        }

        double confidence = Math.Clamp(0.50 + (activeChannels * 0.03) + (totalSpikes * 0.005), 0.50, 0.99);

        return new BciKinematicsDecodeResult(
            SubjectId: request.SubjectId,
            TotalSpikesCount: totalSpikes,
            ActiveChannelsCount: activeChannels,
            Kinematics: new DecodedKinematicVector(
                VelocityX: Math.Round(newVx, 2),
                VelocityY: Math.Round(newVy, 2),
                VelocityZ: Math.Round(newVz, 2),
                SpeedMagnitudeMmPerSec: Math.Round(speedMmS, 2),
                DominantDirection: dominantDir
            ),
            DecodingConfidence: Math.Round(confidence, 2),
            MotorIntentState: $"ACTIVE_REACH: Continuous decoded velocity {speedMmS:F1} mm/s towards {dominantDir}."
        );
    }
}
