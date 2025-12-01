using System;
using System.ComponentModel.DataAnnotations;

namespace LawFirmManagement.Models
{
    public class CreateLawyerViewModel
    {
        [Required]
        [EmailAddress]
        public string Email { get; set; } = "";

        

        [Required]
        public string FullName { get; set; } = "";

        public string? Phone { get; set; }

        [Required]
        public string Specialization { get; set; } = "";

        [Required]
        public DateTime DateOfJoining { get; set; } = DateTime.UtcNow;
    }
}