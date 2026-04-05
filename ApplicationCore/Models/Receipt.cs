using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ApplicationCore.Models
{
    public class Receipt
    {
        [Key]
        public int ReceiptId { get; set; }

        [Required]
        public int ReservationId { get; set; }

        [ForeignKey("ReservationId")]
        public Reservation Reservation { get; set; }

        public DateTime IssuedDate { get; set; } = DateTime.UtcNow;

        [Required]
        public string Type { get; set; } 

        public decimal TotalCharged { get; set; }
        public decimal TotalPaid { get; set; }
        public decimal RefundedAmount { get; set; }
        public decimal AmountDue { get; set; }

        public string Summary { get; set; } 

        public string CreatedBy { get; set; } 

        public string PdfFilePath { get; set; } 
    }
}
