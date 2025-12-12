using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace LawFirmManagement.Models
{
    public class CaseDocument
    {
        [Key]
        public int Id { get; set; }

        // Links the document to a specific Case
        [Required]
        public Guid CaseId { get; set; }

        [ForeignKey("CaseId")]
        public virtual Case? Case { get; set; }

        // The original name of the file (e.g., "Contract.pdf")
        [Required]
        public string FileName { get; set; } = "";

        // The path where the file is stored on the server (e.g., "/documents/guid.pdf")
        [Required]
        public string FilePath { get; set; } = "";

        // Tracks who uploaded the file (Lawyer's Email or Admin's Email)
        public string? UploadedBy { get; set; }

        public DateTime UploadedAt { get; set; } = DateTime.UtcNow;
    }
}