using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ApplicationCore.Dtos
{
    /// <summary>
    /// Represents a reservation with details about the site, status, financials, and associated transactions.
    /// </summary>
    public class ReservationDto
    {
        public int ReservationId { get; set; }
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }

        public string SiteName { get; set; } = string.Empty;

        // Status fields 
        public string ReservationStatus { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;

        // Financials
        public decimal BaseAmount { get; set; }        
        public decimal ExtraFees { get; set; }      
        public decimal TotalAmount => BaseAmount + ExtraFees;

        public decimal TotalPaid { get; set; }   
        public decimal BalanceDue => TotalAmount - TotalPaid;

        public int? TransactionId { get; set; }

        public List<TransactionSummaryDto> TransactionSummaries { get; set; } = new();
    }
}
