namespace Workflows;

public record DocumentRequest(
    string DocumentId,
    string Payload,
    string Owner);

public record ValidationResult(bool IsValid, string? Reason);

public record ParsedDocument(string DocumentId, string NormalizedPayload, int SizeBytes);

public record EnrichedDocument(string DocumentId, string NormalizedPayload, IReadOnlyDictionary<string, string> Metadata);

public record SignatureRequest(string DocumentId, string PayloadHash, string Owner);

public record SignedDocument(string DocumentId, string PayloadHash, string SignatureValue, DateTime SignedAt);

public record VerificationResult(bool IsValid, string Thumbprint);

public record ProcessingResult(string DocumentId, string Status, SignedDocument? Signature);
