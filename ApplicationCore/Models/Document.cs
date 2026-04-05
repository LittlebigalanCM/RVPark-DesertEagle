using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ApplicationCore.Models
{
    public class Document
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public string FileName { get; set; } = "";

        [Required]
        public string ContentType { get; set; } = "";

        // stored in DB (varbinary(max))
        public byte[]? FileData { get; set; }

        [Required]
        public string? Filepath { get; set; }

        [Required]
        [Display(Name = "Document Type")]
        public string? DocType { get; set; } //PCS orders or Disability documentation

        [Required]
        public bool IsApproved { get; set; } = false;

        [Required]
        public int ReservationId { get; set; }
        
        [ForeignKey("ReservationId")]
        public virtual Reservation? Reservation { get; set; }
    }
}
