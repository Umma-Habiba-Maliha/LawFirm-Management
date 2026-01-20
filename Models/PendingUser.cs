using System;
using System.ComponentModel.DataAnnotations;

namespace LawFirmManagement.Models
{
    public enum PendingRole { Client, Lawyer }

    public class PendingUser
    {
        [Key]
        public Guid Id { get; set; } = Guid.NewGuid();

        [Required, StringLength(250)]
        public string ?FullName { get; set; } = "";

        [Required, EmailAddress, StringLength(250)]
        public string Email { get; set; } = "";

        [StringLength(50)]
        public string? Phone { get; set; }

        public string? Address { get; set; }

        public PendingRole Role { get; set; }

        public string? AdditionalInfo { get; set; }

        public DateTime RequestedAt { get; set; } = DateTime.UtcNow;

        public bool IsProcessed { get; set; } = false;

        public string? AdminNote { get; set; }
    }
}
