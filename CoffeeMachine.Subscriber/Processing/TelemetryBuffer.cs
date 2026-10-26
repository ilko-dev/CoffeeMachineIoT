using CoffeeMachine.Contracts;

namespace CoffeeMachine.Subscriber.Processing;

public class TelemetryBuffer
{
    private readonly PriorityQueue<TelemetryMessage, long> _queue = new();
    private readonly object _lock = new();

    public void Add(TelemetryMessage message)
    {
        lock (_lock)
        {
            var priority = message.Timestamp.UtcDateTime.Ticks;

            _queue.Enqueue(message, priority);
        }
    }

    public List<TelemetryMessage> GetReadyMessages(
        DateTimeOffset readyBefore)
    {
        var result = new List<TelemetryMessage>();

        lock (_lock)
        {
            while (_queue.TryPeek(out var message, out _))
            {
                if (message.Timestamp > readyBefore)
                    break;

                result.Add(_queue.Dequeue());
            }
        }

        return result;
    }

    public int Count
    {
        get
        {
            lock (_lock)
            {
                return _queue.Count;
            }
        }
    }
}