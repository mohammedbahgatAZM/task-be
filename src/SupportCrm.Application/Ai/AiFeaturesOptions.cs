namespace SupportCrm.Application.Ai;

// Shared config for every AI Features story — one options class, not one per story.
public class AiFeaturesOptions
{
    public const string SectionName = "AiFeatures";
    public int SummaryThresholdMessageCount { get; set; } = 5;
    public int CategorizationConfidenceThresholdPercentage { get; set; } = 60;
}
