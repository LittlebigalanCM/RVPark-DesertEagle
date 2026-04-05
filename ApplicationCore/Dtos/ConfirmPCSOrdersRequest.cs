using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ApplicationCore.Dtos
{
    /// <summary>
    /// Network request to confirm PCS orders for a reservation.
    /// </summary>
    public class ConfirmPCSOrdersRequest
    {
        public int ReservationId { get; set; }
        //public int DocumentId { get; set; }

    }

}
