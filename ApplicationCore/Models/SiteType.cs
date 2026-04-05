using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ApplicationCore.Models
{
    public class SiteType
    {
        [Key]
        public int SiteTypeId { get; set; }
        [Required]
        [Display(Name = "Site Type Name")]
        public string? Name { get; set; }
        public bool? IsActive { get; set; }
    }
}
