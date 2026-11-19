using Temporalio.Common;
using Temporalio.Workflows;

namespace Workflows;

[Workflow]
public class DocumentSigningWorkflow
{
    private static readonly ActivityOptions ActivityOpts = new()
    {
        StartToCloseTimeout = TimeSpan.FromSeconds(30),
        RetryPolicy = new RetryPolicy
        {
            MaximumAttempts = 3,
            InitialInterval = TimeSpan.FromSeconds(1),
            BackoffCoefficient = 2
        }
    };

    [WorkflowRun]
    public async Task<SignedDocument> RunAsync(EnrichedDocument document)
    {
        var hash = await Workflow.ExecuteActivityAsync(
            (SigningActivities a) => a.HashPayloadAsync(document),
            ActivityOpts);

        var signed = await Workflow.ExecuteActivityAsync(
            (SigningActivities a) => a.ApplySignatureAsync(new SignatureRequest(document.DocumentId, hash, document.Metadata.GetValueOrDefault("owner", "unknown"))),
            ActivityOpts);

        var verification = await Workflow.ExecuteActivityAsync(
            (SigningActivities a) => a.VerifySignatureAsync(signed),
            ActivityOpts);

        if (!verification.IsValid)
        {
            throw new Temporalio.Exceptions.ApplicationFailureException("Signature verification failed", "SignatureInvalid");
        }

        await Workflow.ExecuteActivityAsync(
            (SigningActivities a) => a.PersistSignedDocumentAsync(signed),
            ActivityOpts);

        return signed;
    }
}
