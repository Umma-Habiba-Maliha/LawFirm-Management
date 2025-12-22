using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace LawFirmManagement.Models
{
    public class Payment
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public Guid CaseId { get; set; }

        [ForeignKey("CaseId")]
        public virtual Case? Case { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal TotalAmount { get; set; } // The amount paid in this specific transaction

        [Column(TypeName = "decimal(18,2)")]
        public decimal AdminShare { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal LawyerShare { get; set; }

        public string PaymentMethod { get; set; } = ""; // "bKash", "Visa", "MasterCard"

        // NEW: Tracks if this is the "Advance" (20%) or "Final" (80%) payment
        public string PaymentType { get; set; } = "Full";

        public DateTime PaymentDate { get; set; } = DateTime.UtcNow;
    }
}