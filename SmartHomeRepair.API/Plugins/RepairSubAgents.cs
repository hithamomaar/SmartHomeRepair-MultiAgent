using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Agents;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using System.ComponentModel;

namespace SmartHomeRepair.API
{
    public class RepairSubAgents
    {
        private readonly Kernel _kernel;
        private readonly PromptExecutionSettings _executionSettings;

        public RepairSubAgents(Kernel kernel)
        {
            _kernel = kernel;
            _executionSettings = new OpenAIPromptExecutionSettings
            {
                FunctionChoiceBehavior = FunctionChoiceBehavior.Auto()
            };
        }

        [KernelFunction("get_suitable_tool")]
        [Description("Gets required tools based on issue type. (e.g., Pipe leak -> wrench)")]
        public async Task<string> GetSuitableTool([Description("The category of the issue")] string issueType)
        {
            var agent = new ChatCompletionAgent
            {
                Name = "ToolRecommendationAgent",
                Instructions = "You are a tool recommendation expert. Use your tools to find the required materials for the given issue. Return ONLY the tools, do not add conversational text.",
                Kernel = _kernel,
                Arguments = new KernelArguments(_executionSettings)
            };

            var history = new ChatHistory();
            history.AddUserMessage($"Issue: {issueType}");

            var response = await agent.InvokeAsync(history).LastOrDefaultAsync();
            return response?.Message.Content ?? "No tools found.";
        }

        [KernelFunction("get_steps")]
        [Description("Provides step-by-step repair instructions based on issue type.")]
        public async Task<string> GetSteps([Description("The category of the issue")] string issueType)
        {
            var agent = new ChatCompletionAgent
            {
                Name = "RepairStepsAgent",
                Instructions = "You are a step-by-step repair instructor. Use your tools to get the repair steps for the issue. Return ONLY the steps.",
                Kernel = _kernel,
                Arguments = new KernelArguments(_executionSettings)
            };

            var history = new ChatHistory();
            history.AddUserMessage($"Issue: {issueType}");

            var response = await agent.InvokeAsync(history).LastOrDefaultAsync();
            return response?.Message.Content ?? "No steps found.";
        }

        [KernelFunction("schedule_repair")]
        [Description("Schedules a professional repair and returns date/time.")]
        public async Task<string> ScheduleRepair([Description("The category of the issue")] string issueType)
        {
            var agent = new ChatCompletionAgent
            {
                Name = "SchedulingAgent",
                Instructions = "You are a professional scheduler. Use your tools to schedule a repair. Return ONLY the date and time.",
                Kernel = _kernel,
                Arguments = new KernelArguments(_executionSettings)
            };

            var history = new ChatHistory();
            history.AddUserMessage($"Schedule repair for: {issueType}");

            var response = await agent.InvokeAsync(history).LastOrDefaultAsync();
            return response?.Message.Content ?? "Scheduling failed.";
        }

        [KernelFunction("estimate_cost")]
        [Description("Estimates the repair cost based on issue type.")]
        public async Task<string> EstimateCost([Description("The category of the issue")] string issueType)
        {
            var agent = new ChatCompletionAgent
            {
                Name = "CostEstimationAgent",
                Instructions = "You are a cost estimator. Use your tools to estimate the cost for the issue. Return ONLY the cost.",
                Kernel = _kernel,
                Arguments = new KernelArguments(_executionSettings)
            };

            var history = new ChatHistory();
            history.AddUserMessage($"Estimate cost for: {issueType}");

            var response = await agent.InvokeAsync(history).LastOrDefaultAsync();
            return response?.Message.Content ?? "Cost estimation failed.";
        }
    }
}