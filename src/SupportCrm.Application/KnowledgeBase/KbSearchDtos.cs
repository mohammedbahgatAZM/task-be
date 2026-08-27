namespace SupportCrm.Application.KnowledgeBase;

public record KbSearchResultDto(string ContentType, Guid ContentId, string Title, string Snippet, double Score);
public record KbSearchResponseDto(IReadOnlyList<KbSearchResultDto> Results, int TotalCount);
