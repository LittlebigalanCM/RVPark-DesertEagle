using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ApplicationCore.Dtos
{
    /// <summary>
    /// Network request to cancel a reservation.
    /// </summary>
    public class CancelReservationRequest
    {
        public int ReservationId { get; set; }
    }

}
