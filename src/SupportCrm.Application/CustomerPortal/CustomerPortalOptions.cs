namespace SupportCrm.Application.CustomerPortal;

// Shared config for the whole Customer Portal feature — one options class, not one per story.
public class CustomerPortalOptions
{
    public const string SectionName = "CustomerPortal";
    public int ReopenWindowDays { get; set; } = 7;
    public int LowRatingThreshold { get; set; } = 2;
}
