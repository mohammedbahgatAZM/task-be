namespace SupportCrm.Application.Ai;

// A simple Arabic-Unicode-range heuristic, not a language-ID model. Shared by this story and
// Story 34's chatbot — do not duplicate this character-range check anywhere else.
public static class AiLanguageDetector
{
    public static string Detect(string text) =>
        text.Any(c => c >= '؀' && c <= 'ۿ') ? "ar" : "en";
}
