using Microsoft.AspNetCore.Identity;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ApplicationCore.Models
{
    public class UserAccount : IdentityUser
    {
        [Required]
        public string? FirstName { get; set; }
        [Required]
        public string? LastName { get; set; }

        public int? BranchId { get; set; }

        [ForeignKey("BranchId")]
        public virtual MilitaryBranch? MilitaryBranch { get; set; }

        public int? RankId { get; set; }

        [ForeignKey("RankId")]
        public virtual MilitaryRank? MilitaryRank { get; set; }

        [Required]
        public int StatusId { get; set; }

        [ForeignKey("StatusId")]
        public virtual MilitaryStatus? MilitaryStatus { get; set; }

        [ForeignKey("GSPayGrade")]
        public int? GSPayId { get; set; }
        public virtual GSPayGrade? GSPayGrade { get; set; }


        [NotMapped]
        public string FullName { get { return FirstName + " " + LastName; } }

    }
}
