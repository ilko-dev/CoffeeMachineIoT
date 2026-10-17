using System.Text.Json;
using CoffeeMachine.Contracts;
using MQTTnet;
using MQTTnet.Protocol;

var mqttFactory = new MqttClientFactory();

using var mqttClient = mqttFactory.CreateMqttClient();

var receivedSequences = new HashSet<long>();
var latencies = new List<double>();

var duplicates = 0;
var lastReportedCount = 0;

var qos = MqttQualityOfServiceLevel.AtLeastOnce;

mqttClient.ApplicationMessageReceivedAsync += async e =>
{
    var topic = e.ApplicationMessage.Topic;
    var payload = e.ApplicationMessage.ConvertPayloadToString();

    if (topic.EndsWith("/status"))
    {
        Console.WriteLine($"STATUS -> {payload}");
        return;
    }

    try
    {
        var telemetry =
            JsonSerializer.Deserialize<TelemetryMessage>(payload);

        if (telemetry is null || !IsValid(telemetry))
        {
            Console.WriteLine($"INVALID [{topic}]");
            return;
        }

        var latency =
            DateTimeOffset.UtcNow - telemetry.Timestamp;

        if (!receivedSequences.Add(telemetry.Sequence))
        {
            duplicates++;
        }

        latencies.Add(latency.TotalMilliseconds);

        Console.WriteLine(
            $"RECV [{telemetry.SensorId}] " +
            $"{telemetry.Value} {telemetry.Unit} | " +
            $"seq={telemetry.Sequence} | " +
            $"latency={latency.TotalMilliseconds:F1} ms");

        var logLine =
            $"{DateTimeOffset.Now:O};" +
            $"{telemetry.MachineId};" +
            $"{telemetry.SensorId};" +
            $"{telemetry.Value};" +
            $"{telemetry.Unit};" +
            $"{telemetry.Sequence};" +
            $"{latency.TotalMilliseconds:F1}";

        if (receivedSequences.Count >= lastReportedCount + 500)
        {
            lastReportedCount = receivedSequences.Count;

            var minSequence = receivedSequences.Min();
            var maxSequence = receivedSequences.Max();

            var expected = maxSequence - minSequence + 1;
            var received = receivedSequences.Count;
            var lost = expected - received;

            var stats =
                $"QoS={qos}; " +
                $"Received={received}; " +
                $"Lost={lost}; " +
                $"Duplicates={duplicates}; " +
                $"AvgLatency={latencies.Average():F2}ms; " +
                $"MaxLatency={latencies.Max():F2}ms";

            Console.WriteLine();
            Console.WriteLine($"STATS -> {stats}");

            await File.AppendAllTextAsync(
                "qos-results.log",
                $"{DateTimeOffset.Now:O}; {stats}{Environment.NewLine}");
        }

        await File.AppendAllTextAsync(
            "telemetry.log",
            logLine + Environment.NewLine);
    }
    catch (JsonException)
    {
        Console.WriteLine(
            $"INVALID JSON [{topic}]: {payload}");
    }

    return;
};

var options = new MqttClientOptionsBuilder()
    .WithClientId("coffee-machine-subscriber")
    .WithTcpServer("localhost", 1883)
    .Build();

await mqttClient.ConnectAsync(
    options,
    CancellationToken.None);

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
Console.WriteLine("Waiting for telemetry...");

await Task.Delay(Timeout.Infinite);

static bool IsValid(TelemetryMessage message)
{
    if (string.IsNullOrWhiteSpace(message.MachineId))
        return false;

    if (string.IsNullOrWhiteSpace(message.SensorId))
        return false;

    if (string.IsNullOrWhiteSpace(message.Unit))
        return false;

    if (message.Timestamp == default)
        return false;

    if (double.IsNaN(message.Value) ||
        double.IsInfinity(message.Value))
        return false;

    return true;
}