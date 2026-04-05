using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ApplicationCore.Models
{
    public class Photo
    {
        [Key]
        public int Id { get; set; }
        [Required]
        public string? Name { get; set; }
            
        [Required]
        public int SiteId { get; set; }
        [ForeignKey("SiteId")]
        public virtual Site? Site { get; set; }
    }
}
