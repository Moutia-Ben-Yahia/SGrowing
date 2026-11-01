namespace SGrowing.Api.Modules.Logistics.Fleet;

/// <summary>
/// Production implementation of <see cref="IObd2TelemetryStreamerEngine"/> utilizing modern C# 14 / .NET 10
/// native <see cref="System.Threading.Lock"/> primitives for thread-safe high-frequency CAN bus telemetry processing.
/// </summary>
public sealed class Obd2TelemetryStreamerEngine : IObd2TelemetryStreamerEngine
{
    private readonly Lock _lock = new();

    private readonly Dictionary<string, VehicleState> _vehicleStates = [];
    private readonly Dictionary<string, Obd2TelemetryPacket> _lastPackets = [];
    private readonly Dictionary<string, Dictionary<string, DiagnosticTroubleCode>> _activeDtcs = [];
    private readonly Dictionary<string, List<Obd2TelemetryPacket>> _telemetryHistory = [];
    private readonly Dictionary<string, long> _packetCounters = [];

    // Deadband threshold constants
    private const double SpeedDeadbandKmh = 0.5;
    private const double IdleRpmDeadband = 20.0;
    private const double ThrottleDeadbandPct = 0.5;
    private const double CoolantTempDeadbandCelsius = 0.5;
    private const double BatteryVoltageDeadbandVolts = 0.10;
    private const double CruisingRpmDeadband = 30.0;

    /// <inheritdoc />
    public IngestTelemetryResponse IngestTelemetry(Obd2TelemetryPacket packet)
    {
        ArgumentNullException.ThrowIfNull(packet);

        if (string.IsNullOrWhiteSpace(packet.VehicleId))
        {
            throw new ArgumentException("Vehicle ID cannot be null or whitespace.", nameof(packet));
        }

        if (string.IsNullOrWhiteSpace(packet.DriverId))
        {
            throw new ArgumentException("Driver ID cannot be null or whitespace.", nameof(packet));
        }

        lock (_lock)
        {
            var vehicleId = packet.VehicleId;
            var totalPackets = _packetCounters.TryGetValue(vehicleId, out var count) ? count + 1 : 1;
            _packetCounters[vehicleId] = totalPackets;

            // Merge active DTCs if packet contains codes
            EnsureDtcCollection(vehicleId);
            if (packet.DiagnosticTroubleCodes.Count > 0)
            {
                foreach (var dtc in packet.DiagnosticTroubleCodes)
                {
                    if (!string.IsNullOrWhiteSpace(dtc.Code))
                    {
                        _activeDtcs[vehicleId][dtc.Code] = dtc;
                    }
                }
            }

            var currentActiveDtcs = _activeDtcs[vehicleId].Values.ToList();
            var currentHealth = EvaluateHealthStatus(packet, currentActiveDtcs);

            // Check deadband filtering condition
            if (_lastPackets.TryGetValue(vehicleId, out var lastPacket))
            {
                var isDeadband = IsWithinDeadband(lastPacket, packet);
                if (isDeadband && packet.DiagnosticTroubleCodes.Count == 0)
                {
                    // Update state timestamp and packet counter without overwriting telemetry metrics
                    if (_vehicleStates.TryGetValue(vehicleId, out var existingState))
                    {
                        _vehicleStates[vehicleId] = existingState with
                        {
                            LastUpdatedUtc = packet.TimestampUtc,
                            TotalPacketsProcessed = totalPackets,
                            HealthStatus = currentHealth,
                            ActiveTroubleCodes = currentActiveDtcs
                        };
                    }

                    return new IngestTelemetryResponse(
                        Success: true,
                        VehicleId: vehicleId,
                        HealthStatus: currentHealth,
                        ActiveDtcCount: currentActiveDtcs.Count,
                        Message: "Telemetry packet filtered by deadband (micro-fluctuations ignored).",
                        FilteredByDeadband: true);
                }
            }

            // Accept and commit new telemetry state
            _lastPackets[vehicleId] = packet;

            if (!_telemetryHistory.TryGetValue(vehicleId, out var history))
            {
                history = [];
                _telemetryHistory[vehicleId] = history;
            }

            history.Add(packet);
            if (history.Count > 5000)
            {
                history.RemoveRange(0, 1000);
            }

            var newState = new VehicleState(
                VehicleId: vehicleId,
                DriverId: packet.DriverId,
                LastUpdatedUtc: packet.TimestampUtc,
                SpeedKmh: packet.SpeedKmh,
                EngineRpm: packet.EngineRpm,
                InstantaneousFuelRateLitersPerHour: packet.InstantaneousFuelRateLitersPerHour,
                CoolantTempCelsius: packet.CoolantTempCelsius,
                BatteryVoltage: packet.BatteryVoltage,
                ThrottlePositionPct: packet.ThrottlePositionPct,
                Latitude: packet.Latitude,
                Longitude: packet.Longitude,
                HealthStatus: currentHealth,
                ActiveTroubleCodes: currentActiveDtcs,
                TotalPacketsProcessed: totalPackets);

            _vehicleStates[vehicleId] = newState;

            return new IngestTelemetryResponse(
                Success: true,
                VehicleId: vehicleId,
                HealthStatus: currentHealth,
                ActiveDtcCount: currentActiveDtcs.Count,
                Message: "Telemetry packet ingested successfully.",
                FilteredByDeadband: false);
        }
    }

    /// <inheritdoc />
    public IReadOnlyList<IngestTelemetryResponse> IngestBatch(IEnumerable<Obd2TelemetryPacket> packets)
    {
        ArgumentNullException.ThrowIfNull(packets);

        var packetList = packets.OrderBy(p => p.TimestampUtc).ToList();
        var responses = new List<IngestTelemetryResponse>(packetList.Count);

        foreach (var packet in packetList)
        {
            responses.Add(IngestTelemetry(packet));
        }

        return responses;
    }

    /// <inheritdoc />
    public VehicleState? GetLatestVehicleState(string vehicleId)
    {
        if (string.IsNullOrWhiteSpace(vehicleId))
        {
            return null;
        }

        lock (_lock)
        {
            return _vehicleStates.GetValueOrDefault(vehicleId);
        }
    }

    /// <inheritdoc />
    public IReadOnlyList<DiagnosticTroubleCode> GetActiveTroubleCodes(string vehicleId)
    {
        if (string.IsNullOrWhiteSpace(vehicleId))
        {
            return [];
        }

        lock (_lock)
        {
            if (_activeDtcs.TryGetValue(vehicleId, out var dict))
            {
                return dict.Values.ToList();
            }

            return [];
        }
    }

    /// <inheritdoc />
    public IReadOnlyList<Obd2TelemetryPacket> GetTelemetryHistory(string vehicleId, int limit = 1000)
    {
        if (string.IsNullOrWhiteSpace(vehicleId))
        {
            return [];
        }

        lock (_lock)
        {
            if (_telemetryHistory.TryGetValue(vehicleId, out var history))
            {
                return history.TakeLast(Math.Max(1, limit)).ToList();
            }

            return [];
        }
    }

    /// <inheritdoc />
    public void ClearTroubleCodes(string vehicleId)
    {
        if (string.IsNullOrWhiteSpace(vehicleId))
        {
            return;
        }

        lock (_lock)
        {
            if (_activeDtcs.TryGetValue(vehicleId, out var dict))
            {
                dict.Clear();
            }

            if (_vehicleStates.TryGetValue(vehicleId, out var state))
            {
                _vehicleStates[vehicleId] = state with
                {
                    HealthStatus = VehicleHealthStatus.Optimal,
                    ActiveTroubleCodes = []
                };
            }
        }
    }

    private void EnsureDtcCollection(string vehicleId)
    {
        if (!_activeDtcs.ContainsKey(vehicleId))
        {
            _activeDtcs[vehicleId] = [];
        }
    }

    private static bool IsWithinDeadband(Obd2TelemetryPacket previous, Obd2TelemetryPacket current)
    {
        // If there are DTCs, never filter by deadband
        if (current.DiagnosticTroubleCodes.Count > 0)
        {
            return false;
        }

        var speedDelta = Math.Abs(current.SpeedKmh - previous.SpeedKmh);
        var rpmDelta = Math.Abs(current.EngineRpm - previous.EngineRpm);

        // Case 1: Vehicle is idling (speed < 1 km/h)
        if (previous.SpeedKmh < 1.0 && current.SpeedKmh < 1.0)
        {
            return speedDelta < SpeedDeadbandKmh && rpmDelta < IdleRpmDeadband;
        }

        // Case 2: Vehicle cruising / driving at relatively steady state
        var coolantDelta = Math.Abs(current.CoolantTempCelsius - previous.CoolantTempCelsius);
        var voltageDelta = Math.Abs(current.BatteryVoltage - previous.BatteryVoltage);
        var throttleDelta = Math.Abs(current.ThrottlePositionPct - previous.ThrottlePositionPct);

        return speedDelta < SpeedDeadbandKmh &&
               rpmDelta < CruisingRpmDeadband &&
               coolantDelta < CoolantTempDeadbandCelsius &&
               voltageDelta < BatteryVoltageDeadbandVolts &&
               throttleDelta < ThrottleDeadbandPct;
    }

    private static VehicleHealthStatus EvaluateHealthStatus(
        Obd2TelemetryPacket packet,
        IReadOnlyList<DiagnosticTroubleCode> activeDtcs)
    {
        // 1. Critical DTC Check: Immediate Action Required
        if (activeDtcs.Any(dtc => dtc.Severity == DtcSeverity.Critical))
        {
            return VehicleHealthStatus.CriticalMaintenanceRequired;
        }

        // 2. Critical telemetry thresholds: Engine Overheating (e.g. >= 115°C)
        if (packet.CoolantTempCelsius >= 115.0)
        {
            return VehicleHealthStatus.CriticalMaintenanceRequired;
        }

        // 3. Degraded DTC Check: Warning level trouble codes (e.g. lean fuel trim, O2 sensor warning)
        if (activeDtcs.Any(dtc => dtc.Severity == DtcSeverity.Warning))
        {
            return VehicleHealthStatus.Degraded;
        }

        // 4. Degraded telemetry thresholds: Running hot (105°C-114.9°C) or electrical subsystem abnormality
        if (packet.CoolantTempCelsius >= 105.0)
        {
            return VehicleHealthStatus.Degraded;
        }

        if (packet.BatteryVoltage < 11.5 || packet.BatteryVoltage > 15.5)
        {
            return VehicleHealthStatus.Degraded;
        }

        return VehicleHealthStatus.Optimal;
    }
}
