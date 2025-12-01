using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace LawFirmManagement.Models
{
    public class CreateCaseViewModel
    {
        [Required]
        public string CaseTitle { get; set; } = "";

        [Required]
        public string CaseType { get; set; } = ""; // Civil, Criminal, etc.

        [Required]
        public string Description { get; set; } = "";

        [Required]
        [Display(Name = "Select Client")]
        public string ClientId { get; set; } = "";

        [Required]
        [Display(Name = "Select Lawyer")]
        public string LawyerId { get; set; } = "";

        // These lists will fill the Dropdowns in the UI
        public IEnumerable<SelectListItem>? ClientList { get; set; }
        public IEnumerable<SelectListItem>? LawyerList { get; set; }
    }
}