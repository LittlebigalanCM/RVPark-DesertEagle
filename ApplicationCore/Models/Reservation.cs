using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ApplicationCore.Models
{
    public class Reservation
    {
        [Key]
        public int ReservationId { get; set; }
        [Required]
        public string UserId { get; set; }
        [Required]
        public int SiteId { get; set; }
        [Required]
        public DateTime StartDate { get; set; }
        [Required]
        public DateTime EndDate { get; set; }
        public double? TrailerLength { get; set; }
        public int NumberOfVehicles { get; set; } = 1;

        public string VehiclePlates { get; set; } = "";


        public bool? RequiresPCS { get; set; }
        public bool? RequiresDisability { get; set; }

        [Required]
        public string? ReservationStatus { get; set; }

        [ForeignKey("UserId")]
        [ValidateNever]
        public virtual UserAccount UserAccount { get; set; }
        [ForeignKey("SiteId")]
        public virtual Site? Site { get; set; }
        [NotMapped]
        public Dictionary<string, object> DynamicData { get; set; } = new();
        public List<CustomDynamicField> FieldDefinitions { get; set; } = new();

        [ValidateNever]
        public virtual ICollection<Transaction> Transactions { get; set; }
        public decimal CalculateTotalPrice(int duration, decimal pricePerDay)
        {
            // Calculate the total price of the reservation
            // based on the length of the trailer and the price of the site

            return pricePerDay * duration;
        }
    }
}
