namespace SupportCrm.Application.Tickets;

/// <summary>
/// Splits an outbound SMS body into ordered, non-data-losing segments using the standard
/// GSM-7 160-character-per-segment limit (single segment) — multi-segment messages use a
/// smaller 153-character-per-segment budget to leave room for concatenation headers, matching
/// how real carriers segment long SMS (this codebase doesn't need to send the real UDH bytes
/// since no real gateway exists, but the character-budget behavior itself is real).
/// </summary>
public static class SmsSegmenter
{
    private const int SingleSegmentLimit = 160;
    private const int MultiSegmentLimit = 153;

    public static IReadOnlyList<string> Split(string body)
    {
        if (body.Length <= SingleSegmentLimit)
            return new[] { body };

        var segments = new List<string>();
        for (var offset = 0; offset < body.Length; offset += MultiSegmentLimit)
            segments.Add(body.Substring(offset, Math.Min(MultiSegmentLimit, body.Length - offset)));
        return segments;
    }
}
