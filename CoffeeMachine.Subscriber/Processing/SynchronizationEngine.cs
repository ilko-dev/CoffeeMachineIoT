using CoffeeMachine.Subscriber.Models;

namespace CoffeeMachine.Subscriber.Processing;

public enum SynchronizationReason
{
    Initial,
    Periodic,
    Event,
    Threshold
}

public class SynchronizationEngine
{
    private CoffeeMachineState? _lastSynchronizedState;

    private DateTimeOffset _lastSynchronization = DateTimeOffset.MinValue;

    private readonly TimeSpan _periodicInterval = TimeSpan.FromSeconds(1);

    public bool ShouldSynchronize(
        CoffeeMachineState newState,
        out SynchronizationReason reason)
    {
        if (_lastSynchronizedState is null)
        {
            reason = SynchronizationReason.Initial;
            return true;
        }

        if (HasImportantEvent(newState))
        {
            reason = SynchronizationReason.Event;
            return true;
        }

        if (HasSignificantChange(newState))
        {
            reason = SynchronizationReason.Threshold;
            return true;
        }

        if (DateTimeOffset.UtcNow - _lastSynchronization
            >= _periodicInterval)
        {
            reason = SynchronizationReason.Periodic;
            return true;
        }

        reason = default;
        return false;
    }

    public void MarkSynchronized(
        CoffeeMachineState state)
    {
        _lastSynchronizedState = CopyState(state);

        _lastSynchronization =
            DateTimeOffset.UtcNow;
    }

    private bool HasImportantEvent(
        CoffeeMachineState state)
    {
        return
            state.BrewingActive
                != _lastSynchronizedState!.BrewingActive
            ||
            state.WaterTankPresent
                != _lastSynchronizedState.WaterTankPresent;
    }

    private bool HasSignificantChange(CoffeeMachineState state)
    {
        var previous = _lastSynchronizedState!;

        if (Math.Abs(
                state.Temperature -
                previous.Temperature) >= 0.5)
        {
            return true;
        }

        if (Math.Abs(
                state.Pressure -
                previous.Pressure) >= 0.2)
        {
            return true;
        }

        if (Math.Abs(
                state.FlowRate -
                previous.FlowRate) >= 0.2)
        {
            return true;
        }

        if (Math.Abs(
                state.WaterLevel -
                previous.WaterLevel) >= 1)
        {
            return true;
        }

        return false;
    }

    private static CoffeeMachineState CopyState(
        CoffeeMachineState state)
    {
        return new CoffeeMachineState
        {
            Temperature = state.Temperature,
            Pressure = state.Pressure,
            FlowRate = state.FlowRate,
            WaterLevel = state.WaterLevel,
            HeaterPower = state.HeaterPower,
            PumpVibration = state.PumpVibration,

            BrewingActive = state.BrewingActive,
            WaterTankPresent = state.WaterTankPresent,

            Mode = state.Mode,

            AveragePressure = state.AveragePressure,
            AverageFlowRate = state.AverageFlowRate,
            IsWaterLow = state.IsWaterLow,

            LastUpdated = state.LastUpdated
        };
    }
}