using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace LawFirmManagement.Models
{
    public class CreateCaseViewModel
    {
        [Required]
        public string CaseTitle { get; set; } = "";

        [Required]
        public string CaseType { get; set; } = "";

        [Required]
        public string Description { get; set; } = "";

        [Required]
        [Display(Name = "Select Client")]
        public string ClientId { get; set; } = "";

        [Required]
        [Display(Name = "Select Lawyer")]
        public string LawyerId { get; set; } = "";

        [Required]
        [Display(Name = "Total Fee (BDT)")]
        public decimal TotalFee { get; set; }

        // NEW FIELD
        [Required]
        [Range(0, 100, ErrorMessage = "Percentage must be between 0 and 100")]
        [Display(Name = "Admin Share (%)")]
        public double AdminSharePercentage { get; set; } = 10.0; // Default 10%

        public IEnumerable<SelectListItem>? ClientList { get; set; }
        public IEnumerable<SelectListItem>? LawyerList { get; set; }
    }
}