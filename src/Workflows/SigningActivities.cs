using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Logging;
using Temporalio.Activities;

namespace Workflows;

public class SigningActivities
{
    private readonly ILogger<SigningActivities> _logger;

    public SigningActivities(ILogger<SigningActivities> logger)
    {
        _logger = logger;
    }

    [Activity]
    public async Task<string> HashPayloadAsync(EnrichedDocument document)
    {
        _logger.LogInformation("Hashing payload for {DocumentId}", document.DocumentId);
        await Task.Delay(100);
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(document.NormalizedPayload));
        return Convert.ToHexString(bytes);
    }

    [Activity]
    public async Task<SignedDocument> ApplySignatureAsync(SignatureRequest request)
    {
        _logger.LogInformation("Applying signature for {DocumentId}", request.DocumentId);
        await Task.Delay(400);
        var seed = $"{request.PayloadHash}|{request.Owner}";
        var sig = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(seed)));
        return new SignedDocument(request.DocumentId, request.PayloadHash, sig, DateTime.UtcNow);
    }

    [Activity]
    public async Task<VerificationResult> VerifySignatureAsync(SignedDocument signed)
    {
        _logger.LogInformation("Verifying signature for {DocumentId}", signed.DocumentId);
        await Task.Delay(150);
        var thumbprint = signed.SignatureValue[..16];
        return new VerificationResult(true, thumbprint);
    }

    [Activity]
    public async Task PersistSignedDocumentAsync(SignedDocument signed)
    {
        _logger.LogInformation("Persisting signed document {DocumentId} signedAt={SignedAt}", signed.DocumentId, signed.SignedAt);
        await Task.Delay(200);
    }
}
