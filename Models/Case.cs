using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.AspNetCore.Identity;

namespace LawFirmManagement.Models
{
    public enum CaseStatus { Pending, Active, Closed }

    public class Case
    {
        [Key]
        public Guid Id { get; set; } = Guid.NewGuid();

        [Required]
        public string CaseTitle { get; set; } = "";

        [Required]
        public string CaseType { get; set; } = "";

        [Required]
        public string Description { get; set; } = "";

        [Required]
        public CaseStatus Status { get; set; } = CaseStatus.Pending;

        [Required]
        public DateTime StartDate { get; set; } = DateTime.UtcNow;

        public DateTime? EndDate { get; set; }

        // --- NEW PAYMENT FIELDS ---
        [Required]
        [Column(TypeName = "decimal(18,2)")]
        public decimal TotalFee { get; set; } = 0.00m;

        // Tracks progress: "Unpaid", "AdvancePaid", "FullyPaid"
        public string PaymentStatus { get; set; } = "Unpaid";
        // --------------------------

        // Relations
        [Required]
        public string ClientId { get; set; } = "";
        [ForeignKey("ClientId")]
        public virtual IdentityUser? Client { get; set; }

        [Required]
        public string LawyerId { get; set; } = "";
        [ForeignKey("LawyerId")]
        public virtual IdentityUser? Lawyer { get; set; }
    }
}