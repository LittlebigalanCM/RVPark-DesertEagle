using System.ComponentModel.DataAnnotations;

namespace ApplicationCore.Models
{
    public class GSPayGrade
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public string Code { get; set; }   // e.g. "GS-01"

        public bool IsActive { get; set; }
    }
}
