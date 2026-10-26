using CoffeeMachine.Subscriber.Models;

namespace CoffeeMachine.Subscriber.State;

public class TwinStateStore
{
    private CoffeeMachineState _currentState = new();

    private readonly object _lock = new();

    public event Action<CoffeeMachineState>? StateChanged;

    public CoffeeMachineState CurrentState
    {
        get
        {
            lock (_lock)
            {
                return CopyState(_currentState);
            }
        }
    }

    public void Update(CoffeeMachineState newState)
    {
        lock (_lock)
        {
            newState.LastUpdated = DateTimeOffset.UtcNow;

            _currentState = CopyState(newState);
        }

        StateChanged?.Invoke(CurrentState);
    }

    private static CoffeeMachineState CopyState(CoffeeMachineState state)
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