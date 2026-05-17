using Microsoft.SemanticKernel;
using System.ComponentModel;

namespace SmartHomeRepair.API
{
    public enum IssueType
    {
        Plumbing,
        Electricity,
        Carpentry
    }

    public class HomeRepairPlugin
    {
        [KernelFunction("get_suitable_tool")]
        [Description("Gets suitable repair materials and tools for a specific home issue type.")]
        public string GetSuitableTool([Description("The category of the issue")] IssueType issueType)
        {
            return issueType switch
            {
                IssueType.Plumbing => "Wrench, Pipe sealant",
                IssueType.Electricity => "Insulated gloves, Screwdriver, Voltage tester",
                IssueType.Carpentry => "Hammer, Wood glue, Measuring tape",
                _ => "Basic multi-tool kit"
            };
        }

        [KernelFunction("get_steps")]
        [Description("Gets step-by-step repair instructions for a specific home issue.")]
        public string GetSteps([Description("The category of the issue")] IssueType issueType)
        {
            return issueType switch
            {
                IssueType.Plumbing => "1. Shut off water supply. 2. Drain water. 3. Apply sealant.",
                IssueType.Carpentry => "1. Measure materials. 2. Cut and assemble. 3. Secure with glue.",
                IssueType.Electricity => "⚠️ DANGER: Electrical repairs require a professional. DIY is STRICTLY PROHIBITED.",
                _ => "1. Inspect damage. 2. Execute repair."
            };
        }

        [KernelFunction("schedule_repair")]
        [Description("Schedules a professional repair appointment and returns the date and time.")]
        public string ScheduleRepair([Description("The category of the issue")] IssueType issueType)
        {
            var appointmentDate = DateTime.Now.AddDays(1).ToString("yyyy-MM-dd hh:mm tt");
            return $"Technician scheduled to arrive on {appointmentDate}.";
        }

        [KernelFunction("estimate_cost")]
        [Description("Estimates the repair cost based on the issue type.")]
        public string EstimateCost([Description("The category of the issue")] IssueType issueType)
        {
            var cost = issueType switch
            {
                IssueType.Electricity => 250,
                IssueType.Plumbing => 150,
                IssueType.Carpentry => 80,
                _ => 100
            };
            return $"${cost}";
        }
    }
}