using System.ComponentModel.DataAnnotations;

namespace LawFirmManagement.Models
{
    public class RegisterPendingViewModel
    {
        [Required(ErrorMessage = "Full Name is required")]
        public string ?FullName { get; set; }

        [Required(ErrorMessage = "Email is required")]
        [EmailAddress(ErrorMessage = "Invalid Email Address")]
        public string ?Email { get; set; }

        [Required(ErrorMessage = "Phone number is required")]
        public string? Phone { get; set; }

        [Required(ErrorMessage = "Address is required")]
        public string? Address { get; set; }

        [Required(ErrorMessage = "Please select a role")]
        public string ?Role { get; set; } // Stores "Client" or "Lawyer"

        public string? AdditionalInfo { get; set; } // Optional
    }
}