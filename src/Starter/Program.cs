using Temporalio.Client;
using Workflows;

const string TaskQueue = "document-pipeline";

var address = Environment.GetEnvironmentVariable("TEMPORAL_ADDRESS") ?? "localhost:7233";
var client = await TemporalClient.ConnectAsync(new TemporalClientConnectOptions(address)
{
    Namespace = "default"
});

var request = new DocumentRequest(
    DocumentId: $"doc-{Guid.NewGuid():N}"[..12],
    Payload: "Hello temporal, this is a sample document payload to be signed.",
    Owner: "alice");

Console.WriteLine($"Starting workflow for {request.DocumentId}");

var handle = await client.StartWorkflowAsync(
    (DocumentProcessingWorkflow w) => w.RunAsync(request),
    new WorkflowOptions
    {
        Id = $"processing-{request.DocumentId}",
        TaskQueue = TaskQueue
    });

Console.WriteLine($"WorkflowId: {handle.Id}");
Console.WriteLine($"RunId:      {handle.ResultRunId}");
Console.WriteLine("Open UI at http://localhost:8080 to inspect the run.");

var result = await handle.GetResultAsync();
Console.WriteLine();
Console.WriteLine($"Status:    {result.Status}");
if (result.Signature is { } sig)
{
    Console.WriteLine($"Signature: {sig.SignatureValue[..32]}... at {sig.SignedAt:O}");
}
