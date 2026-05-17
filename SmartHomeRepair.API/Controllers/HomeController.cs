using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.VectorData;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Agents;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using Microsoft.SemanticKernel.Embeddings;
using Microsoft.SemanticKernel.Text;
using SmartHomeRepair.API.Memory;
using SmartHomeRepair.API.Models;
using System.Text;
using UglyToad.PdfPig;

namespace SmartHomeRepair.API.Controllers
{
    [Route("[controller]")]
    [ApiController]
    public class HomeController : ControllerBase
    {
        private IConfiguration configuration;
        private Kernel kernel;
        private SessionStore sessions;

        public HomeController(IConfiguration _configuration, Kernel _kernel, HomeRepairPlugin repairPlugin,
            SessionStore _sessions, RepairSubAgents subAgents)
        {
            configuration = _configuration;
            kernel = _kernel;
            sessions = _sessions;

            kernel.Plugins.AddFromObject(repairPlugin);
            kernel.Plugins.AddFromObject(subAgents, "SubAgentsPlugin");
        }

        [HttpPost("analyze")]
        public async Task<IActionResult> AnalyzeIssue([FromForm] string prompt, [FromForm] string? imageUrl = null)
        {
            var chat = kernel.GetRequiredService<IChatCompletionService>();

            var items = new ChatMessageContentItemCollection
            {
                new TextContent(prompt)
            };

            if (!string.IsNullOrEmpty(imageUrl) && Uri.TryCreate(imageUrl, UriKind.Absolute, out Uri validUri))
            {
                items.Add(new ImageContent(validUri));
            }
            var history = new ChatHistory();
            history.AddUserMessage(items);

            var setting = new OpenAIPromptExecutionSettings
            {
                Temperature = 0,
                ResponseFormat = "json_object",
                ToolCallBehavior = ToolCallBehavior.AutoInvokeKernelFunctions,

                ChatSystemPrompt = @"You are a Smart Home Repair Assistant.
                CRITICAL RULES:
                1. Identify the issue type based on the image and description (Plumbing, Electricity, Carpentry).
                2. Never guess tools or steps manually. ALWAYS call tools if needed.
                3. Make a clear decision: 'DIY' (for simple/low risk) or 'Professional' (for dangerous/complex like Electricity).
                4. If DIY: Provide tools and steps.
                5. If Professional OR if the user explicitly asks to book: You MUST call the make_appointment tool.
                6. ALWAYS return ONLY a structured JSON response. Do not include any text explanations.

                JSON STRUCTURE:
                {
                    ""issue_type"": ""..."",
                    ""decision"": ""DIY / Professional"",
                    ""tools_needed"": ""..."",
                    ""repair_steps"": ""..."",
                    ""appointment_details"": ""...""
                }"
            };

            var response = await chat.GetChatMessageContentAsync(history, setting, kernel);

            history.AddAssistantMessage(response.Content);

            return Content(response.Content, "application/json");
        }

        [HttpPost("analyze-reduced")]
        public async Task<IActionResult> AnalyzeIssueReduced([FromForm] string prompt, [FromForm] string? imageUrl = null)
        {
            var chat = kernel.GetRequiredService<IChatCompletionService>();

            var items = new ChatMessageContentItemCollection
            {
                new TextContent(prompt)
            };

            if (!string.IsNullOrEmpty(imageUrl) && Uri.TryCreate(imageUrl, UriKind.Absolute, out Uri validUri))
            {
                items.Add(new ImageContent(validUri));
            }
            var history = new ChatHistory();
            history.AddUserMessage(items);

            var reducer = new ChatHistoryTruncationReducer(targetCount: 10, thresholdCount: 20);
            var reducedHistory = await reducer.ReduceAsync(history);

            if (reducedHistory != null)
            {
                Console.WriteLine("Reduced History:");
                history.Clear();
                history.AddRange(reducedHistory);
            }

            var setting = new OpenAIPromptExecutionSettings
            {
                Temperature = 0,
                ResponseFormat = "json_object",
                ToolCallBehavior = ToolCallBehavior.AutoInvokeKernelFunctions,

                ChatSystemPrompt = @"You are a Smart Home Repair Assistant.
                CRITICAL RULES:
                1. Identify the issue type based on the image and description (Plumbing, Electricity, Carpentry).
                2. Never guess tools or steps manually. ALWAYS call tools if needed.
                3. Make a clear decision: 'DIY' (for simple/low risk) or 'Professional' (for dangerous/complex like Electricity).
                4. If DIY: Provide tools and steps.
                5. If Professional OR if the user explicitly asks to book: You MUST call the make_appointment tool.
                6. ALWAYS return ONLY a structured JSON response. Do not include any text explanations.

                JSON STRUCTURE:
                {
                    ""issue_type"": ""..."",
                    ""decision"": ""DIY / Professional"",
                    ""tools_needed"": ""..."",
                    ""repair_steps"": ""..."",
                    ""appointment_details"": ""...""
                }"
            };

            var response = await chat.GetChatMessageContentAsync(history, setting, kernel);

            history.AddAssistantMessage(response.Content);

            return Content(response.Content, "application/json");
        }

        [HttpPost("upload-pdf")]
        public async Task<IActionResult> UploadPdf(List<IFormFile> files)
        {
            if (files == null || files.Count == 0) return BadRequest("No files uploaded");

            var embeddingService = kernel.GetRequiredService<ITextEmbeddingGenerationService>();
            var qdrantStore = kernel.GetRequiredService<VectorStore>();

            var collection = qdrantStore.GetCollection<ulong, DocumentChunk>("pdfchunks");
            await collection.EnsureCollectionExistsAsync();

            ulong chunkId = (ulong)DateTime.Now.Ticks;

            foreach (var file in files)
            {
                using var stream = file.OpenReadStream();
                using var pdf = PdfDocument.Open(stream);
                var rawText = new StringBuilder();
                foreach (var page in pdf.GetPages()) { rawText.Append(page.Text).Append(" "); }

                var lines = TextChunker.SplitPlainTextLines(rawText.ToString(), maxTokensPerLine: 80);
                var chunks = TextChunker.SplitPlainTextParagraphs(lines, 300, 50);

                foreach (var chunk in chunks)
                {
                    var vector = await embeddingService.GenerateEmbeddingAsync(chunk);
                    var documentChunk = new DocumentChunk
                    {
                        ChunkId = chunkId++,
                        Source = file.FileName,
                        Text = chunk,
                        Embedding = vector
                    };
                    await collection.UpsertAsync(documentChunk);
                }
            }
            return Ok(new { message = "Documents processed and stored in Qdrant successfully." });
        }

        [HttpPost("ask-pdf")]
        public async Task<IActionResult> AskPdf([FromForm] string prompt)
        {
            if (string.IsNullOrWhiteSpace(prompt)) return BadRequest("Prompt is empty");

            var embeddingservice = kernel.GetRequiredService<ITextEmbeddingGenerationService>();
            var qdrantstore = kernel.GetRequiredService<VectorStore>();
            var chatservice = kernel.GetRequiredService<IChatCompletionService>();

            var collection = qdrantstore.GetCollection<ulong, DocumentChunk>("pdfchunks");
            var promptembedding = await embeddingservice.GenerateEmbeddingAsync(prompt);

            var retrievedChunks = await collection.SearchAsync<ReadOnlyMemory<float>>(promptembedding, top: 3).ToListAsync();

            if (retrievedChunks == null || !retrievedChunks.Any())
                return NotFound("No context found in the uploaded documents.");

            var contextBuilder = new StringBuilder();
            foreach (var r in retrievedChunks)
            {
                contextBuilder.AppendLine($"[Source: {r.Record.Source}] {r.Record.Text}");
            }
            var fullPrompt = $@"Answer the user question based ONLY on the provided context.
            Context: {contextBuilder}
            Question: {prompt}";

            var response = await chatservice.GetChatMessageContentAsync(fullPrompt);

            return Ok(new { Answer = response.Content });
        }

        [HttpPost("analyze-multi")]
        public async Task<IActionResult> AnalyzeIssueMulti([FromForm] string prompt, [FromForm] string thread_id, [FromForm] string? imageUrl = null)
        {
            if (string.IsNullOrWhiteSpace(thread_id)) return BadRequest("Thread ID is required.");

            ChatHistoryAgentThread thread = sessions.GetOrCreate(thread_id).Thread;
            var items = new ChatMessageContentItemCollection { new TextContent(prompt) };
            if (!string.IsNullOrEmpty(imageUrl) && Uri.TryCreate(imageUrl, UriKind.Absolute, out Uri validUri))
            {
                items.Add(new ImageContent(validUri));
            }
            thread.ChatHistory.AddUserMessage(items);
            var executionSettings = new OpenAIPromptExecutionSettings
            {
                FunctionChoiceBehavior = FunctionChoiceBehavior.Auto(),
                ResponseFormat = "json_object",
                Temperature = 0
            };

            var orchestratorAgent = new ChatCompletionAgent
            {
                Name = "RepairOrchestratorAgent",
                Instructions = @"You are the Orchestrator Agent for a Smart Home Repair System.
                CRITICAL RULES:
                1. Always analyze the image first to determine the issue type (Plumbing, Electricity, Carpentry). Do not use tools for this step.
                2. Never generate tools or steps manually. ALWAYS call tools.
                3. Return ONLY valid JSON format.

                STRICT WORKFLOW:
                Step 1: Analyze image -> get issue_type (by LLM, not tool).
                Step 2: Get required tools -> call get_suitable_tool.
                Step 3: If simple issue -> call get_steps.
                Step 4: If complex/dangerous (like Electricity) -> call schedule_repair AND estimate_cost.
        
                DECISION LOGIC:
                - If DIY (Simple): decision = 'DIY'. Return issue_type, tools, steps, decision.
                - If Professional (Complex): decision = 'Professional'. Return issue_type, tools, decision, appointment, cost.

                JSON STRUCTURE EXAMPLES:
                DIY: { ""issue_type"": """", ""decision"": ""DIY"", ""tools"": """", ""steps"": """" }
                Professional: { ""issue_type"": """", ""decision"": ""Professional"", ""tools"": """", ""appointment"": """", ""cost"": """" }",
                Kernel = kernel,
                Arguments = new KernelArguments(executionSettings)
            };

            var response = await orchestratorAgent.InvokeAsync(thread).LastOrDefaultAsync();

            if (response == null) return StatusCode(500, "Orchestrator failed to respond.");

            return Content(response.Message.Content, "application/json");
        }

        [HttpPost("clear-session")]
        public IActionResult Clear([FromForm] string thread_id)
        {
            sessions.Clear(thread_id);
            return Ok(new { message = $"Session for thread {thread_id} cleared." });
        }
    }
}