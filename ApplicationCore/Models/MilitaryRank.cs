using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ApplicationCore.Models
{
    public class MilitaryRank
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public string? Rank { get; set; }

        public bool IsActive { get; set; }
    }
}
