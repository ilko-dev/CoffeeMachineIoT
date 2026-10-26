using System.Diagnostics;

namespace CoffeeMachine.Subscriber.Metrics;

public class PipelineMetrics
{
    private readonly Stopwatch _stopwatch =
        Stopwatch.StartNew();

    private long _processedMessages;
    private long _synchronizations;

    private double _totalLatencyMs;
    private double _maxLatencyMs;

    public void RecordProcessedMessage()
    {
        _processedMessages++;
    }

    public void RecordSynchronization(
        DateTimeOffset measurementTimestamp,
        DateTimeOffset twinUpdatedTimestamp)
    {
        var latency =
            (twinUpdatedTimestamp - measurementTimestamp)
            .TotalMilliseconds;

        _synchronizations++;
        _totalLatencyMs += latency;

        if (latency > _maxLatencyMs)
        {
            _maxLatencyMs = latency;
        }
    }

    public double AverageLatencyMs =>
        _synchronizations == 0
            ? 0
            : _totalLatencyMs / _synchronizations;

    public double MaxLatencyMs =>
        _maxLatencyMs;

    public double Throughput =>
        _stopwatch.Elapsed.TotalSeconds == 0
            ? 0
            : _processedMessages /
              _stopwatch.Elapsed.TotalSeconds;

    public long ProcessedMessages =>
        _processedMessages;

    public long Synchronizations =>
        _synchronizations;
}