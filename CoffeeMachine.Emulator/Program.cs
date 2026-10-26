using System.Text.Json;
using CoffeeMachine.Contracts;
using CoffeeMachine.Emulator;
using CoffeeMachine.Emulator.Enums;
using MQTTnet;
using MQTTnet.Protocol;

const string machineId = "cm-001";

var qos = MqttQualityOfServiceLevel.AtLeastOnce;

var mqttFactory = new MqttClientFactory();

using var mqttClient = mqttFactory.CreateMqttClient();

var options = new MqttClientOptionsBuilder()
    .WithClientId("coffee-machine-emulator")
    .WithTcpServer("localhost", 1883)

    .WithWillTopic($"coffee-machine/{machineId}/status")
    .WithWillPayload("offline")
    .WithWillQualityOfServiceLevel(
        MqttQualityOfServiceLevel.AtLeastOnce)

    .WithWillRetain(true)
    .Build();

await mqttClient.ConnectAsync(options, CancellationToken.None);

Console.WriteLine("Connected to MQTT broker.");

var onlineMessage = new MqttApplicationMessageBuilder()
    .WithTopic($"coffee-machine/{machineId}/status")
    .WithPayload("online")
    .WithQualityOfServiceLevel(
        MqttQualityOfServiceLevel.AtLeastOnce)
    .WithRetainFlag()
    .Build();

await mqttClient.PublishAsync(
    onlineMessage,
    CancellationToken.None);

Console.WriteLine("STATUS -> online");

var simulation = new CoffeeMachineSimulation();

long sequence = 0;
long tick = 0;

var nextAnomalyAt = DateTimeOffset.UtcNow.AddSeconds(30);
var anomalyEndsAt = DateTimeOffset.MaxValue;


using var timer = new PeriodicTimer(
    TimeSpan.FromMilliseconds(100));

while (await timer.WaitForNextTickAsync())
{
    var now = DateTimeOffset.UtcNow;

    if (simulation.Anomaly == AnomalyType.None &&
        now >= nextAnomalyAt)
    {
        var values = new[]
        {
        AnomalyType.Clog,
        AnomalyType.HeaterFailure,
        AnomalyType.PumpFailure
    };

        simulation.Anomaly =
            values[Random.Shared.Next(values.Length)];

        anomalyEndsAt = now.AddSeconds(10);

        Console.WriteLine(
            $"ANOMALY START -> {simulation.Anomaly}");
    }

    if (simulation.Anomaly != AnomalyType.None &&
        now >= anomalyEndsAt)
    {
        Console.WriteLine(
            $"ANOMALY END -> {simulation.Anomaly}");

        simulation.Anomaly = AnomalyType.None;

        nextAnomalyAt =
            now.AddSeconds(30);
    }

    simulation.Update(0.1);

    var publishingTasks = new List<Task>
    {
        Publish("pressure", simulation.Pressure, "bar"),
        Publish("flow", simulation.FlowRate, "ml/s"),
        Publish("pump-vibration", simulation.PumpVibration, "m/s2")
    };

    if (tick % 10 == 0)
    {
        publishingTasks.Add(
            Publish("temperature", simulation.Temperature, "C"));

        publishingTasks.Add(
            Publish("heater-power", simulation.HeaterPower, "W"));

        publishingTasks.Add(
            Publish(
                "brewing-active",
                simulation.Mode == MachineMode.Brewing ? 1 : 0,
                "bool"));
    }

    if (tick % 100 == 0)
    {
        publishingTasks.Add(
            Publish("water-level", simulation.WaterLevel, "%"));

        publishingTasks.Add(
            Publish(
                "water-tank-present",
                1,
                "bool"));
    }

    await Task.WhenAll(publishingTasks);

    tick++;
}

async Task Publish(string sensorId, double value, string unit)
{
    var telemetry = new TelemetryMessage(
        machineId,
        sensorId,
        Math.Round(value, 2),
        unit,
        DateTimeOffset.UtcNow,
        ++sequence);

    var json = JsonSerializer.Serialize(telemetry);

    var topic =
        $"coffee-machine/{machineId}/sensor/{sensorId}";

    var mqttMessage = new MqttApplicationMessageBuilder()
        .WithTopic(topic)
        .WithPayload(json)
        .WithQualityOfServiceLevel(qos)
        .Build();

    await mqttClient.PublishAsync(
        mqttMessage,
        CancellationToken.None);

    Console.WriteLine(
        $"PUB [{topic}] {telemetry.Value} {unit}");
}