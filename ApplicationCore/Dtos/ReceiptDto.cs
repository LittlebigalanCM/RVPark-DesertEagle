using System;
using System.Collections.Generic;
using ApplicationCore.Models;   

namespace ApplicationCore.Dtos
{
    /// <summary>
    /// Represents a receipt for a reservation, including details about the customer, site, and financial transactions.
    /// </summary>
    public class ReceiptDto
    {
        public int ReceiptId { get; set; }
        public int ReservationId { get; set; }
        public string ReservationNumber { get; set; }
        public string CustomerName { get; set; }
        public string SiteName { get; set; }

        public DateTime IssuedDate { get; set; }
        public string Type { get; set; } 

        public decimal TotalCharged { get; set; }
        public decimal RefundedAmount { get; set; }
        public decimal AmountDue { get; set; }

        public List<ReceiptLineItemDto> LineItems { get; set; } = new();
        public List<Transaction> Transactions { get; set; }
        public decimal TotalPaid => Transactions?.Sum(t => t.Amount) ?? 0;
        public string CreatedBy { get; set; }
    }

    /// <summary>
    /// Represents a line item in a receipt, detailing the product or service, quantity, and pricing information.
    /// </summary>
    public class ReceiptLineItemDto
    {
        public string Label { get; set; }
        public int Units { get; set; }
        public decimal PerUnitAmount { get; set; }
        public decimal Total { get; set; }

        public string CalculationType { get; set; } 
    }

    /// <summary>
    /// Represents a detailed summary of a transaction, including label, amount, units, and additional rules.
    /// </summary>
    public class ExtendedTransactionSummaryDto
    {
        public string Label { get; set; } = "";
        public decimal Amount { get; set; }
        public int Units { get; set; }
        public decimal UnitAmount { get; set; }
        public string RuleJson { get; set; } = "";
    }


}
