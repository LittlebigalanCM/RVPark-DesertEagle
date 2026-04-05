using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace ApplicationCore.Models
{
    //Class used for keeping track of the checks that are used for payments
    //For walk in reservations
    public class Check
    {
        [Key]
        public int CheckId { get; set; }
       
        [Required]
        public int TransactionId { get; set; }

        [ForeignKey("TransactionId")]
        public virtual Transaction? Transaction { get; set; }


        //Check number
        [Required]
        [Display(Name = "Check Number")]

        public int CheckNumber { get; set; }

        //date
        [Required]
        [Display(Name = "Check Date")]
        public DateTime CheckDateTime { get; set; }

        //Dollar Amount
        [Required]
        [Range(0.01, 9999.99, ErrorMessage = "Amount must be between $0.01 and $9,999.99")]
        [DisplayFormat(DataFormatString = "{0:C}", ApplyFormatInEditMode = false)]
        [Display(Name = "Dollar Amount")]
        public decimal Amount { get; set; }


    }
}
