namespace CoffeeMachine.Contracts
{
    public record TelemetryMessage(
        string MachineId,
        string SensorId,
        double Value,
        string Unit,
        DateTimeOffset Timestamp,
        long Sequence
    );
}
