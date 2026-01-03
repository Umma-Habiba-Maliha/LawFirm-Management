using System;
using System.ComponentModel.DataAnnotations;

namespace LawFirmManagement.Models
{
    public class EditUserViewModel
    {
        [Required]
        public string UserId { get; set; } = "";

        [Required, EmailAddress]
        public string Email { get; set; } = "";

        [Required]
        public string FullName { get; set; } = "";

        public string? Phone { get; set; }

        public string Role { get; set; } = "";

        // Lawyer Specific Fields (Nullable because Clients won't have them)
        public string? Specialization { get; set; }
        public DateTime? DateOfJoining { get; set; }
    }
}