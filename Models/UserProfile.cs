using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace LawFirmManagement.Models
{
    public class UserProfile
    {
        [Key]
        public Guid Id { get; set; } = Guid.NewGuid();

        // FK to AspNetUsers.Id
        [Required]
        public string UserId { get; set; } = "";

        [ForeignKey("UserId")]
        public virtual Microsoft.AspNetCore.Identity.IdentityUser? User { get; set; }

        [Required, StringLength(250)]
        public string FullName { get; set; } = "";

        [StringLength(50)]
        public string? Phone { get; set; }

        public string? Address { get; set; }

        public string? AdditionalInfo { get; set; }

        // "Client" or "Lawyer"
        [Required]
        public string Role { get; set; } = "";

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        // ---------------------------------------------------------
        // NEW FIELDS FOR LAWYERS
        // ---------------------------------------------------------
        public string? Specialization { get; set; } // Civil, Criminal, etc.
        public DateTime? DateOfJoining { get; set; }
    }
}