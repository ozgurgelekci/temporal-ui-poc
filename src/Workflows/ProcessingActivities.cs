using Microsoft.Extensions.Logging;
using Temporalio.Activities;

namespace Workflows;

public class ProcessingActivities
{
    private readonly ILogger<ProcessingActivities> _logger;

    public ProcessingActivities(ILogger<ProcessingActivities> logger)
    {
        _logger = logger;
    }

    [Activity]
    public async Task<ValidationResult> ValidateAsync(DocumentRequest request)
    {
        _logger.LogInformation("Validating document {DocumentId}", request.DocumentId);
        await Task.Delay(200);
        if (string.IsNullOrWhiteSpace(request.Payload))
        {
            return new ValidationResult(false, "Payload is empty");
        }
        return new ValidationResult(true, null);
    }

    [Activity]
    public async Task<ParsedDocument> ParseAsync(DocumentRequest request)
    {
        _logger.LogInformation("Parsing document {DocumentId}", request.DocumentId);
        await Task.Delay(300);
        var normalized = request.Payload.Trim().ReplaceLineEndings("\n");
        return new ParsedDocument(request.DocumentId, normalized, normalized.Length);
    }

    [Activity]
    public async Task<EnrichedDocument> EnrichAsync(ParsedDocument parsed)
    {
        _logger.LogInformation("Enriching document {DocumentId}", parsed.DocumentId);
        await Task.Delay(250);
        var metadata = new Dictionary<string, string>
        {
            ["ingestedAt"] = DateTime.UtcNow.ToString("O"),
            ["sizeBytes"] = parsed.SizeBytes.ToString(),
            ["source"] = "poc"
        };
        return new EnrichedDocument(parsed.DocumentId, parsed.NormalizedPayload, metadata);
    }
}
