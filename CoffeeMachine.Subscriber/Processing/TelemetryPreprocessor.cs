using CoffeeMachine.Contracts;
using CoffeeMachine.Subscriber.Models;

namespace CoffeeMachine.Subscriber.Processing;

public class TelemetryPreprocessor
{
    private readonly Dictionary<string, double> _lastValues = new();

    private readonly Queue<double> _pressureWindow = new();
    private readonly Queue<double> _flowWindow = new();

    private const int AverageWindowSize = 10;

    public bool TryProcess(
        TelemetryMessage message,
        out string? error)
    {
        error = null;

        if (!IsValueValid(message))
        {
            error =
                $"Outlier rejected: {message.SensorId} = {message.Value}";

            return false;
        }

        _lastValues[message.SensorId] = message.Value;

        if (message.SensorId == "pressure")
        {
            AddToWindow(
                _pressureWindow,
                message.Value);
        }

        if (message.SensorId == "flow")
        {
            AddToWindow(
                _flowWindow,
                message.Value);
        }

        return true;
    }

    public CoffeeMachineState CreateSnapshot()
    {
        var state = new CoffeeMachineState
        {
            Temperature =
                GetLastValue("temperature"),

            Pressure =
                GetLastValue("pressure"),

            FlowRate =
                GetLastValue("flow"),

            WaterLevel =
                GetLastValue("water-level"),

            HeaterPower =
                GetLastValue("heater-power"),

            PumpVibration =
                GetLastValue("pump-vibration"),

            AveragePressure =
                CalculateAverage(_pressureWindow),

            AverageFlowRate =
                CalculateAverage(_flowWindow),

            BrewingActive =
                GetLastValue("brewing-active") >= 0.5,

            WaterTankPresent =
                GetLastValue("water-tank-present") >= 0.5,
        };

        state.UpdateDerivedValues();

        return state;
    }

    private static bool IsValueValid(
        TelemetryMessage message)
    {
        return message.SensorId switch
        {
            "temperature" =>
                message.Value is >= 0 and <= 120,

            "pressure" =>
                message.Value is >= 0 and <= 15,

            "flow" =>
                message.Value is >= 0 and <= 10,

            "water-level" =>
                message.Value is >= 0 and <= 100,

            "heater-power" =>
                message.Value is >= 0 and <= 2000,

            "pump-vibration" =>
                message.Value is >= 0 and <= 10,

            "brewing-active" =>
                message.Value is 0 or 1,

            "water-tank-present" =>
                message.Value is 0 or 1,

            _ => false
        };
    }

    private double GetLastValue(string sensorId)
    {
        return _lastValues.TryGetValue(
            sensorId,
            out var value)
            ? value
            : 0;
    }

    private static void AddToWindow(
        Queue<double> window,
        double value)
    {
        window.Enqueue(value);

        while (window.Count > AverageWindowSize)
        {
            window.Dequeue();
        }
    }

    private static double CalculateAverage(
        Queue<double> window)
    {
        if (window.Count == 0)
            return 0;

        return window.Average();
    }
}