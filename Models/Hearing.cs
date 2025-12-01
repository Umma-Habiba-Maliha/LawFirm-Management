using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace LawFirmManagement.Models
{
    public class Hearing
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public Guid CaseId { get; set; }

        [ForeignKey("CaseId")]
        public virtual Case? Case { get; set; }

        [Required]
        [Display(Name = "Hearing Date & Time")]
        public DateTime HearingDate { get; set; }

        [Required]
        [StringLength(150)]
        public string CourtName { get; set; } = ""; // New Field

        public string Notes { get; set; } = ""; // New Field

        public bool ReminderSent { get; set; } = false; // New Field
    }
}