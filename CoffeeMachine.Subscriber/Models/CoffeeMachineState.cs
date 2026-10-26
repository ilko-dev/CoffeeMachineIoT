namespace CoffeeMachine.Subscriber.Models;

public class CoffeeMachineState
{

    public double Temperature { get; set; }

    public double Pressure { get; set; }

    public double FlowRate { get; set; }

    public double WaterLevel { get; set; }

    public double HeaterPower { get; set; }

    public double PumpVibration { get; set; }

    public bool BrewingActive { get; set; }

    public bool WaterTankPresent { get; set; }


    public CoffeeMachineMode Mode { get; set; }

    public double AveragePressure { get; set; }

    public double AverageFlowRate { get; set; }

    public bool IsWaterLow { get; set; }




    public DateTimeOffset LastUpdated { get; set; }


    public void UpdateDerivedValues()
    {
        if (BrewingActive)
        {
            Mode = CoffeeMachineMode.Brewing;
        }
        else if (HeaterPower > 0)
        {
            Mode = CoffeeMachineMode.Heating;
        }
        else
        {
            Mode = CoffeeMachineMode.Idle;
        }

        IsWaterLow = WaterLevel < 20;
    }
}