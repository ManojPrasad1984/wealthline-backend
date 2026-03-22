using System.ComponentModel.DataAnnotations;
using Wealthline.Functions.Functions.Services;
namespace Wealthline.Functions.Models
{
    public class LuckyDrawEntry
    {
        public int Id { get; set; }

        [Required]
        public string FullName { get; set; }

        public string Address { get; set; }

        [Required]
        public string MobileNumber { get; set; }

        public string AadhaarNumber { get; set; }

        public string PrizeChoice { get; set; }

        public decimal? EntryAmount { get; set; }

        public string RazorpayOrderId { get; set; }

        public string PaymentId { get; set; }

        public bool? PaymentStatus { get; set; }

        public string CardNumber { get; set; }

        public DateTime? EntryDate { get; set; }
        // NEW FIELD
        public Guid? AgentId { get; set; }

        // Navigation
        public Agent? Agent { get; set; }
    }
}