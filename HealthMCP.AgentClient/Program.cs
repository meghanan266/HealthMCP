using HealthMCP.AgentClient;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.AzureOpenAI;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;

DotEnvLoader.Load();

var endpoint = RequireEnv("AZURE_OPENAI_ENDPOINT");
var apiKey = RequireEnv("AZURE_OPENAI_KEY");
var deployment = RequireEnv("AZURE_OPENAI_DEPLOYMENT");

var loggerFactory = NullLoggerFactory.Instance;

var transport = new HttpClientTransport(
    new HttpClientTransportOptions
    {
        Name = "HealthMCP Server",
        Endpoint = new Uri("http://localhost:5100/mcp"),
        TransportMode = HttpTransportMode.Sse,
    },
    loggerFactory);

await using var mcpClient = await McpClient.CreateAsync(
    transport,
    new McpClientOptions
    {
        ClientInfo = new Implementation { Name = "healthmcp", Version = "1.0.0" },
    },
    loggerFactory);

var listed = await mcpClient.ListToolsAsync();
Console.WriteLine("MCP tools available:");
foreach (var tool in listed)
    Console.WriteLine($"  - {tool.Name}: {tool.Description}");

var kernelBuilder = Kernel.CreateBuilder();
kernelBuilder.AddAzureOpenAIChatCompletion(deployment, endpoint, apiKey);
var kernel = kernelBuilder.Build();

kernel.Plugins.AddFromFunctions(
    "clinical_tools",
    listed.Select(t => t.AsKernelFunction()));

var chatService = kernel.GetRequiredService<IChatCompletionService>();

var history = new ChatHistory();
history.AddSystemMessage(
    "You are a clinical data assistant with access to a healthcare MCP server. " +
    "You have tools to look up patient records, query clinical data, fetch and validate FHIR resources, check drug interactions, flag abnormal vitals, and evaluate readmission risk. " +
    "Always use your tools to answer questions — never guess or fabricate clinical data. " +
    "When a user asks about a patient, start by calling get_patient to establish context before calling other tools.");

var settings = new AzureOpenAIPromptExecutionSettings
{
    FunctionChoiceBehavior = FunctionChoiceBehavior.Auto()
};

Console.WriteLine();
Console.WriteLine("HealthMCP Agent — type a question (empty line or 'exit' to quit).");
Console.WriteLine();

while (true)
{
    Console.Write("You: ");
    var line = Console.ReadLine();
    if (line is null)
        break;
    if (string.IsNullOrWhiteSpace(line))
        break;
    if (string.Equals(line.Trim(), "exit", StringComparison.OrdinalIgnoreCase))
        break;

    history.AddUserMessage(line);

    var response = await chatService.GetChatMessageContentAsync(history, settings, kernel);
    if (response.Content is not null)
        history.AddAssistantMessage(response.Content);

    Console.WriteLine($"Assistant: {response.Content}");
    Console.WriteLine();
}

static string RequireEnv(string name)
{
    var value = Environment.GetEnvironmentVariable(name);
    if (string.IsNullOrWhiteSpace(value))
        throw new InvalidOperationException($"Missing environment variable '{name}'. Set it in the solution .env file.");
    return value;
}
