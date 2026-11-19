using Temporalio.Common;
using Temporalio.Workflows;

namespace Workflows;

[Workflow]
public class DocumentProcessingWorkflow
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
    public async Task<ProcessingResult> RunAsync(DocumentRequest request)
    {
        var validation = await Workflow.ExecuteActivityAsync(
            (ProcessingActivities a) => a.ValidateAsync(request),
            ActivityOpts);

        if (!validation.IsValid)
        {
            return new ProcessingResult(request.DocumentId, $"Rejected: {validation.Reason}", null);
        }

        var parsed = await Workflow.ExecuteActivityAsync(
            (ProcessingActivities a) => a.ParseAsync(request),
            ActivityOpts);

        var enriched = await Workflow.ExecuteActivityAsync(
            (ProcessingActivities a) => a.EnrichAsync(parsed),
            ActivityOpts);

        var withOwner = enriched with
        {
            Metadata = new Dictionary<string, string>(enriched.Metadata) { ["owner"] = request.Owner }
        };

        var signed = await Workflow.ExecuteChildWorkflowAsync(
            (DocumentSigningWorkflow w) => w.RunAsync(withOwner),
            new ChildWorkflowOptions
            {
                Id = $"signing-{request.DocumentId}"
            });

        return new ProcessingResult(request.DocumentId, "Completed", signed);
    }
}
