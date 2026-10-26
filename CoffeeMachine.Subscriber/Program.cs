using System.Text.Json;
using CoffeeMachine.Contracts;
using CoffeeMachine.Subscriber.Metrics;
using CoffeeMachine.Subscriber.Persistence;
using CoffeeMachine.Subscriber.Processing;
using CoffeeMachine.Subscriber.State;
using MQTTnet;
using MQTTnet.Protocol;

var mqttFactory = new MqttClientFactory();

using var mqttClient = mqttFactory.CreateMqttClient();

var telemetryBuffer = new TelemetryBuffer();
var preprocessor = new TelemetryPreprocessor();
var synchronizationEngine = new SynchronizationEngine();
var stateStore = new TwinStateStore();

var metrics = new PipelineMetrics();

using var influxRepository =
    new InfluxTimeSeriesRepository(
        "http://localhost:8086",
        "coffee-machine-local-token-2026",
        "coffee-machine-org",
        "coffee-machine");

var qos = MqttQualityOfServiceLevel.AtLeastOnce;

var hasTelemetry = false;
var isMachineOnline = false;

stateStore.StateChanged += state =>
{
    Console.WriteLine();
    Console.WriteLine("========== TWIN UPDATED ==========");
    Console.WriteLine($"Mode:              {state.Mode}");
    Console.WriteLine($"Temperature:       {state.Temperature:F1} C");
    Console.WriteLine($"Pressure:          {state.Pressure:F2} bar");
    Console.WriteLine($"Average pressure:  {state.AveragePressure:F2} bar");
    Console.WriteLine($"Flow rate:         {state.FlowRate:F2} ml/s");
    Console.WriteLine($"Average flow:      {state.AverageFlowRate:F2} ml/s");
    Console.WriteLine($"Water level:       {state.WaterLevel:F1} %");
    Console.WriteLine($"Heater power:      {state.HeaterPower:F0} W");
    Console.WriteLine($"Vibration:         {state.PumpVibration:F2} m/s2");
    Console.WriteLine($"Brewing:           {state.BrewingActive}");
    Console.WriteLine($"Tank present:      {state.WaterTankPresent}");
    Console.WriteLine($"Water low:         {state.IsWaterLow}");
    Console.WriteLine($"Updated:           {state.LastUpdated:HH:mm:ss.fff}");
    Console.WriteLine("==================================");
    Console.WriteLine();
};


// MQTT callback
mqttClient.ApplicationMessageReceivedAsync += e =>
{
    var topic = e.ApplicationMessage.Topic;
    var payload = e.ApplicationMessage.ConvertPayloadToString();

    if (topic.EndsWith("/status"))
    {
        isMachineOnline = payload.Equals(
            "online",
             StringComparison.OrdinalIgnoreCase);

        Console.WriteLine($"STATUS -> {payload}");

        return Task.CompletedTask;
    }

    try
    {
        var telemetry =
            JsonSerializer.Deserialize<TelemetryMessage>(payload);

        if (telemetry is null ||
            !IsStructurallyValid(telemetry))
        {
            Console.WriteLine($"INVALID [{topic}]");

            return Task.CompletedTask;
        }

        telemetryBuffer.Add(telemetry);
    }
    catch (JsonException)
    {
        Console.WriteLine(
            $"INVALID JSON [{topic}]: {payload}");
    }

    return Task.CompletedTask;
};


// MQTT connection
var options = new MqttClientOptionsBuilder()
    .WithClientId("coffee-machine-subscriber")
    .WithTcpServer("localhost", 1883)
    .Build();

await mqttClient.ConnectAsync(
    options,
    CancellationToken.None);


// Subscribe
var subscribeOptions = mqttFactory
    .CreateSubscribeOptionsBuilder()
    .WithTopicFilter(
        "coffee-machine/+/sensor/+",
        qos)
    .WithTopicFilter(
        "coffee-machine/+/status",
        qos)
    .Build();

await mqttClient.SubscribeAsync(
    subscribeOptions,
    CancellationToken.None);

Console.WriteLine("Subscriber connected.");
Console.WriteLine("Digital twin pipeline started.");
Console.WriteLine();


using var processingTimer =
    new PeriodicTimer(
        TimeSpan.FromMilliseconds(100));

var lastMetricsOutput =
    DateTimeOffset.UtcNow;

while (await processingTimer.WaitForNextTickAsync())
{
    var readyBefore =
        DateTimeOffset.UtcNow.AddMilliseconds(-200);

    var messages =
        telemetryBuffer.GetReadyMessages(readyBefore);

    var processedAny = false;

    DateTimeOffset? latestTelemetryTimestamp = null;

    // Preprocessing
    foreach (var telemetry in messages)
    {
        if (!preprocessor.TryProcess(
                telemetry,
                out var error))
        {
            Console.WriteLine(
                $"REJECTED: {error}");

            continue;
        }

        metrics.RecordProcessedMessage();

        processedAny = true;

        latestTelemetryTimestamp =
            telemetry.Timestamp;


        await influxRepository.WriteTelemetryAsync(telemetry);

        hasTelemetry = true;
    }

    if (!hasTelemetry || !isMachineOnline || !processedAny)
        continue;

    var candidateState =
        preprocessor.CreateSnapshot();

    if (!synchronizationEngine.ShouldSynchronize(
            candidateState,
            out var reason))
    {
        continue;
    }


    stateStore.Update(candidateState);

    var currentState =
        stateStore.CurrentState;

    synchronizationEngine.MarkSynchronized(
        currentState);

    metrics.RecordSynchronization(
        latestTelemetryTimestamp!.Value,
        currentState.LastUpdated);

    await influxRepository
        .WriteTwinStateAsync(
            currentState,
            reason);

    Console.WriteLine(
        $"Synchronization reason: {reason}");

    if (DateTimeOffset.UtcNow - lastMetricsOutput >= TimeSpan.FromSeconds(5))
    {
        Console.WriteLine();
        Console.WriteLine("========== METRICS ==========");
        Console.WriteLine(
            $"Processed:      {metrics.ProcessedMessages}");

        Console.WriteLine(
            $"Synchronizations: {metrics.Synchronizations}");

        Console.WriteLine(
            $"Avg latency:    {metrics.AverageLatencyMs:F2} ms");

        Console.WriteLine(
            $"Max latency:    {metrics.MaxLatencyMs:F2} ms");

        Console.WriteLine(
            $"Throughput:     {metrics.Throughput:F2} msg/s");

        Console.WriteLine("=============================");
        Console.WriteLine();

        lastMetricsOutput =
            DateTimeOffset.UtcNow;
    }
}


static bool IsStructurallyValid(
    TelemetryMessage message)
{
    if (string.IsNullOrWhiteSpace(
            message.MachineId))
        return false;

    if (string.IsNullOrWhiteSpace(
            message.SensorId))
        return false;

    if (message.Timestamp == default)
        return false;

    if (double.IsNaN(message.Value) ||
        double.IsInfinity(message.Value))
        return false;

    return true;
}