namespace SGrowing.Api.Modules.AI.SpikingRetinaGazeArbiter;

public interface ISpikingRetinaGazeArbiterEngine
{
    SaccadicGazeDecision EvaluateSpikeStream(SpikingRetinaEvaluationRequest request);
}

public class SpikingRetinaGazeArbiterEngine : ISpikingRetinaGazeArbiterEngine
{
    public SaccadicGazeDecision EvaluateSpikeStream(SpikingRetinaEvaluationRequest request)
    {
        var sensor = request.SensorState;
        var config = request.GazeConfig;
        var spikes = sensor.Spikes ?? [];

        int totalSpikes = spikes.Count;
        int onCount = 0;
        int offCount = 0;

        double sumX = 0;
        double sumY = 0;

        double sumVx = 0;
        double sumVy = 0;
        int flowPairs = 0;

        for (int i = 0; i < totalSpikes; i++)
        {
            var s = spikes[i];
            if (s.Polarity > 0) onCount++;
            else offCount++;

            sumX += s.X;
            sumY += s.Y;

            if (i > 0)
            {
                var prev = spikes[i - 1];
                long dtMicro = s.TimestampMicroseconds - prev.TimestampMicroseconds;
                if (dtMicro > 0 && dtMicro < 50000) // Within 50ms temporal coincidence window
                {
                    double vx = (double)(s.X - prev.X) / dtMicro;
                    double vy = (double)(s.Y - prev.Y) / dtMicro;
                    sumVx += vx;
                    sumVy += vy;
                    flowPairs++;
                }
            }
        }

        double meanVx = flowPairs > 0 ? (sumVx / flowPairs) : 0.0;
        double meanVy = flowPairs > 0 ? (sumVy / flowPairs) : 0.0;
        double flowVelocity = Math.Sqrt(meanVx * meanVx + meanVy * meanVy);
        double flowAngle = Math.Atan2(meanVy, meanVx);

        int targetX = totalSpikes > 0 ? (int)Math.Round(sumX / totalSpikes) : (sensor.ResolutionWidth / 2);
        int targetY = totalSpikes > 0 ? (int)Math.Round(sumY / totalSpikes) : (sensor.ResolutionHeight / 2);

        // Clamp to resolution bounds
        targetX = Math.Clamp(targetX, 0, sensor.ResolutionWidth);
        targetY = Math.Clamp(targetY, 0, sensor.ResolutionHeight);

        bool saccadeTriggered = totalSpikes >= config.MinSpikesToTriggerSaccade;
        string gazeState;
        string directive;

        if (saccadeTriggered)
        {
            gazeState = "BALLISTIC_SACCADE_ENGAGED";
            directive = $"EXECUTE_SACCADE: Salience cluster detected with {totalSpikes} asynchronous spikes. Reposition optical fovea to ({targetX}, {targetY}) at {config.MaxSaccadeVelocityDegPerSec:F0} deg/s to lock high-resolution tracking.";
        }
        else if (totalSpikes > 0)
        {
            gazeState = "SMOOTH_PURSUIT_MICROSACCADE";
            directive = $"MICRO_CORRECTION: Low-density temporal contrast flow. Apply smooth pursuit tracking along vector ({meanVx:F4}, {meanVy:F4}) px/us.";
        }
        else
        {
            gazeState = "FIXATION_IDLE";
            directive = "IDLE_FOVEA: Zero temporal contrast spikes recorded. Retain central foveation in quiescent energy-saving state.";
        }

        return new SaccadicGazeDecision(
            SensorId: sensor.SensorId,
            TotalProcessedEvents: totalSpikes,
            OnEventsCount: onCount,
            OffEventsCount: offCount,
            MeanOpticalFlowAngleRadians: Math.Round(flowAngle, 3),
            MeanOpticalFlowVelocityPixelsPerMicrosec: Math.Round(flowVelocity, 5),
            SaccadeCommandIssued: saccadeTriggered,
            TargetFoveationX: targetX,
            TargetFoveationY: targetY,
            GazeState: gazeState,
            RecommendedGazeDirective: directive
        );
    }
}
