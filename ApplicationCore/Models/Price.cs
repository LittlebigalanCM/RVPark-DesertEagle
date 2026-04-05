using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ApplicationCore.Models
{
    public class Price
    {
        [Key]
        public int PriceId { get; set; }

        [Required]
        public DateTime StartDate { get; set; }

        public DateTime? EndDate { get; set; }

        [Required]
        public int SiteTypeId { get; set; }

        [ForeignKey("SiteTypeId")]
        public virtual SiteType? SiteType { get; set; }

        [Required]
        [Range(0.01, 9999.99, ErrorMessage = "Price must be between $0.01 and $9,999.99")]
        [DisplayFormat(DataFormatString = "{0:C}", ApplyFormatInEditMode = false)]
        [Display(Name = "Price Per Day")]
        public decimal PricePerDay { get; set; }
    }
}
