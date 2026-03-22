using System.ComponentModel.DataAnnotations.Schema;

namespace Wealthline.Functions.Functions.Services
{
    [Table("Agents", Schema = "Wealthline_LuckyDraw")]
    public class Agent
    {
        public Guid Id { get; set; }

        public string AgentCode { get; set; } = string.Empty;
        public string AgentName { get; set; } = string.Empty;

        public string MobileNumber { get; set; } = string.Empty;
        public string? Email { get; set; }

        public decimal? CommissionPercentage { get; set; }
        public bool IsActive { get; set; } = true;

        public DateTime CreatedAt { get; set; } = DateTime.Now;
    }
}