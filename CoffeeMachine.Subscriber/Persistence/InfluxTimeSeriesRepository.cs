using CoffeeMachine.Contracts;
using CoffeeMachine.Subscriber.Models;
using CoffeeMachine.Subscriber.Processing;
using InfluxDB.Client;
using InfluxDB.Client.Api.Domain;
using InfluxDB.Client.Writes;

namespace CoffeeMachine.Subscriber.Persistence;

public class InfluxTimeSeriesRepository : IDisposable
{
    private readonly InfluxDBClient _client;
    private readonly WriteApiAsync _writeApi;

    private readonly string _bucket;
    private readonly string _organization;

    public InfluxTimeSeriesRepository(
        string url,
        string token,
        string organization,
        string bucket)
    {
        _client = new InfluxDBClient(
            url,
            token);

        _writeApi = _client.GetWriteApiAsync();

        _organization = organization;
        _bucket = bucket;
    }

    public async Task WriteTelemetryAsync(
        TelemetryMessage message)
    {
        var point = PointData
            .Measurement("telemetry")
            .Tag("machineId", message.MachineId)
            .Tag("sensorId", message.SensorId)
            .Tag("unit", message.Unit)
            .Field("value", message.Value)
            .Field("sequence", message.Sequence)
            .Timestamp(
                message.Timestamp,
                WritePrecision.Ms);

        await _writeApi.WritePointAsync(
            point,
            _bucket,
            _organization);
    }

    public async Task WriteTwinStateAsync(
        CoffeeMachineState state,
        SynchronizationReason reason)
    {
        var point = PointData
            .Measurement("twin_state")

            .Tag(
                "mode",
                state.Mode.ToString())

            .Tag(
                "syncReason",
                reason.ToString())

            .Field(
                "temperature",
                state.Temperature)

            .Field(
                "pressure",
                state.Pressure)

            .Field(
                "flowRate",
                state.FlowRate)

            .Field(
                "waterLevel",
                state.WaterLevel)

            .Field(
                "heaterPower",
                state.HeaterPower)

            .Field(
                "pumpVibration",
                state.PumpVibration)

            .Field(
                "brewingActive",
                state.BrewingActive)

            .Field(
                "waterTankPresent",
                state.WaterTankPresent)

            .Field(
                "averagePressure",
                state.AveragePressure)

            .Field(
                "averageFlowRate",
                state.AverageFlowRate)

            .Field(
                "isWaterLow",
                state.IsWaterLow)

            .Timestamp(
                state.LastUpdated,
                WritePrecision.Ms);

        await _writeApi.WritePointAsync(
            point,
            _bucket,
            _organization);
    }

    public void Dispose()
    {
        _client.Dispose();
    }
}