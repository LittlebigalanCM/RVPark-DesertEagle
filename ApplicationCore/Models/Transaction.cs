using ApplicationCore.Enums;
using ApplicationCore.Models;
using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;

namespace ApplicationCore.Models
{
    public class Transaction
    {
        [Key]
        public int TransactionId { get; set; }

        [Required]
        public int ReservationId { get; set; }

        [Required]
        public int FeeId { get; set; }

        [Required]
        public string? PaymentMethod { get; set; } //Cash, check, credit

        [ForeignKey("FeeId")]
        public virtual Fee? Fee { get; set; }

        [ForeignKey("ReservationId")]
        public virtual Reservation? Reservation { get; set; }

        public decimal Amount { get; set; }

        public string? Description { get; set; }
        public bool IsPaid { get; set; }

        public int? TransactionApprovalId { get; set; } //for walk in credit card transactions. For reference only, doesn't point to anything

        public DateTime TransactionDateTime { get; set; }

        public bool PreviouslyRefunded { get; set; }

        public TriggerType TriggerType { get; set; }

        public CalculationType CalculationType { get; set; }

        public string? TriggerRuleSnapshotJson { get; set; }
    }

}