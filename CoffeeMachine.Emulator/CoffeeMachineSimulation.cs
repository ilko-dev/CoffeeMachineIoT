using CoffeeMachine.Emulator.Enums;

namespace CoffeeMachine.Emulator;

public class CoffeeMachineSimulation
{
    private double _modeTime;

    public MachineMode Mode { get; private set; } = MachineMode.Idle;

    public double Temperature { get; private set; } = 25;
    public double Pressure { get; private set; }
    public double FlowRate { get; private set; }
    public double WaterLevel { get; private set; } = 100;
    public double HeaterPower { get; private set; }
    public double PumpVibration { get; private set; }

    public AnomalyType Anomaly { get; set; }

    public void Update(double deltaSeconds)
    {
        _modeTime += deltaSeconds;

        switch (Mode)
        {
            case MachineMode.Idle:
                HeaterPower = 0;
                Pressure = Math.Max(0, Noise(0, 0.02));
                FlowRate = 0;
                PumpVibration = Math.Max(0, Noise(0.02, 0.01));

                if (_modeTime >= 5)
                    ChangeMode(MachineMode.Heating);

                break;

            case MachineMode.Heating:
                HeaterPower = 1400 + Noise(0, 20);

                if (Anomaly == AnomalyType.HeaterFailure)
                {
                    Temperature += 0.05 * deltaSeconds;
                }
                else
                {
                    Temperature += 2.5 * deltaSeconds;
                }

                Pressure = Math.Max(0, Noise(0, 0.02));
                FlowRate = 0;
                PumpVibration = Math.Max(0, Noise(0.02, 0.01));

                if (Temperature >= 93)
                {
                    Temperature = 93;
                    ChangeMode(MachineMode.Brewing);
                }

                break;

            case MachineMode.Brewing:
                HeaterPower = Temperature < 92.5 ? 500 : 0;

                if (Anomaly == AnomalyType.Clog)
                {
                    Pressure = Noise(11.5, 0.2);
                    FlowRate = Noise(0.7, 0.1);
                    PumpVibration = Noise(1.5, 0.05);
                }
                else if (Anomaly == AnomalyType.PumpFailure)
                {
                    Pressure = Noise(1.0, 0.1);
                    FlowRate = Noise(0.2, 0.05);
                    PumpVibration = Noise(2.5, 0.2);
                }
                else
                {
                    Pressure = Noise(9.0, 0.15);
                    FlowRate = Noise(3.0, 0.1);
                    PumpVibration = Noise(1.2, 0.05);
                }

                Temperature += (93 - Temperature) * 0.05;

                WaterLevel -= FlowRate * deltaSeconds / 1500.0 * 100;

                if (_modeTime >= 15)
                    ChangeMode(MachineMode.Idle);

                break;
        }
    }

    private void ChangeMode(MachineMode mode)
    {
        Mode = mode;
        _modeTime = 0;

        Console.WriteLine($"MODE -> {mode}");
    }

    private static double Noise(double center, double amplitude)
    {
        return center + (Random.Shared.NextDouble() * 2 - 1) * amplitude;
    }
}