public class QoSDistribution
{
    public int QoS0Percentage { get; set; } = 40;
    public int QoS1Percentage { get; set; } = 30;
    public int QoS2Percentage { get; set; } = 30;

    public void Validate()
    {
        if (QoS0Percentage + QoS1Percentage + QoS2Percentage != 100)
        {
            throw new ArgumentException("QoS distribution percentages must sum to 100");
        }
    }
}