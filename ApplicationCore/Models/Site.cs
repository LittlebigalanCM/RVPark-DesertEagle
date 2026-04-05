using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ApplicationCore.Models
{
    public class Site
    {
        [Key]
        public int SiteId { get; set; }
        [Required]
        public string? Name { get; set; }
        [Required]
        [Display(Name = "Description")]
        public string? Description { get; set; }

        [Required]
        [Display(Name = "Site Locked")]
        public bool IsLocked { get; set; }

        [Required]
        public int? SiteTypeId { get; set; }

        [Display(Name = "Trailer Max Size (ft)")]
        public double? TrailerMaxSize { get; set; }

        [ForeignKey("SiteTypeId")]
        public virtual SiteType? SiteType { get; set; }
        public bool IsHandicappedAccessible { get; set; }
    }
}