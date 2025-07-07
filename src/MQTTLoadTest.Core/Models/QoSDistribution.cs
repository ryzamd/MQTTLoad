namespace MQTTLoadTest.Core.Models;

public class QoSDistribution
{
    public int QoS0Percentage { get; set; } = 40; // Fire and forget
    public int QoS1Percentage { get; set; } = 30; // At least once
    public int QoS2Percentage { get; set; } = 30; // Exactly once

    public void Validate()
    {
        var total = QoS0Percentage + QoS1Percentage + QoS2Percentage;
        if (total != 100)
            throw new ArgumentException($"QoS percentages must sum to 100, got {total}");
    }
}