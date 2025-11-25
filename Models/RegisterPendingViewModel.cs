using System.ComponentModel.DataAnnotations;

namespace LawFirmManagement.Models
{
    public class RegisterPendingViewModel
    {
        [Required] public string FullName { get; set; } = "";
        [Required, EmailAddress] public string Email { get; set; } = "";
        public string? Phone { get; set; }
        public string? Address { get; set; }
        [Required] public string? Role { get; set; }
        public string? AdditionalInfo { get; set; }
    }
}
