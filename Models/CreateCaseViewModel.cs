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

        // --- NEW PROPERTY (Fixes the Error) ---
        [Required]
        [Display(Name = "Total Fee (BDT)")]
        public decimal TotalFee { get; set; }
        // -------------------------------------

        // Dropdowns
        public IEnumerable<SelectListItem>? ClientList { get; set; }
        public IEnumerable<SelectListItem>? LawyerList { get; set; }
    }
}