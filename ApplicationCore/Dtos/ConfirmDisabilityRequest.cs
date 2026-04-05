using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ApplicationCore.Dtos
{
    /// <summary>
    /// Network request to confirm disability for a reservation.
    /// </summary>
    public class ConfirmDisabilityRequest
    {
        public int ReservationId { get; set; }

    }

}
