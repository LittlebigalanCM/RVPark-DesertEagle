var currDataTable;
var pastDataTable;
var dataTable;
function renderStatus(status) {
    if (!status || typeof status !== 'string') {
        return `<span class="badge bg-secondary" style="font-size: 0.85rem;">Unknown</span>`;
    }

    let colorClass = 'bg-secondary';

    switch (status.toLowerCase()) {
        case 'confirmed':
            colorClass = 'bg-primary';
            break;
        case 'active':
            colorClass = 'bg-success';
            break;
        case 'pending':
            colorClass = 'bg-warning';
            break;
        case 'cancelled':
            colorClass = 'bg-danger';
            break;
        case 'completed':
            colorClass = 'bg-secondary';
            break;
    }



    return `<span class="badge ${colorClass}" style="font-size: 0.85rem;">${status}</span>`;
}

//Custom status sorting to show Confirmed first, then Pending, Active, Cancelled, and Completed
function getStatusOrder(status) {
    if (!status) return 99; // Unknowns at the end
    switch (status.toLowerCase()) {
        case 'confirmed': return 1;
        case 'pending': return 2;
        case 'active': return 3;
        case 'cancelled': return 4;
        case 'completed': return 5;
        default: return 99;
    }
}

$(document).ready(function () {
    // Current Reservations Table
    currDataTable = $('#DT_Current').DataTable({
        "ajax": {
            "url": "/api/Reservation/CurrentReservations",
            "type": "GET",
            "datatype": "json"
        },
        "responsive": true,
        "columns": [
            { "data": "reservationId", width: "10%" },
            { "data": "startDate", width: "20%" },
            { "data": "endDate", width: "20%" },
            { "data": "siteName", width: "25%" },
            {
                "data": "reservationStatus",
                "render": function (data) {
                    return renderStatus(data);
                },
                "width": "10%"
            },
            {
                "data": "reservationId",
                "render": function (data) {
                    return `<div class="text-center d-flex justify-content-between">
                        <a href="/Client/Reservations/Edit?id=${data}" class="btn btn-primary btn-sm me-1" style="font-size: 0.8rem;">
                            <i class="fas fa-edit"></i> Edit
                        </a>
                        <button onclick="cancelReservation(${data})" class="btn btn-danger btn-sm me-1" style="font-size: 0.8rem;">
                            <i class="fas fa-ban"></i> Cancel
                        </button>
                        <a href="/Client/Reservations/Receipt?reservationId=${data}" class="btn btn-secondary btn-sm me-1" style="font-size: 0.8rem;">
                            <i class="fas fa-info-circle"></i> Details
                        </a>
                    </div>`;
                },
                "width": "15%"
            }
        ],
        "pageLength": 10,
        "language": {
            "emptyTable": "No current reservations found."
        },
        "width": "100%"
    });

    // Past Reservations Table
    pastDataTable = $('#DT_Past').DataTable({
        "ajax": {
            "url": "/api/Reservation/PastReservations",
            "type": "GET",
            "datatype": "json"
        },
        "responsive": true,
        "columns": [
            { "data": "reservationId", width: "20%" },
            { "data": "startDate", width: "20%" },
            { "data": "endDate", width: "20%" },
            { "data": "siteName", width: "20%" },
            {
                "data": "reservationStatus",
                "render": function (data) {
                    return renderStatus(data);
                },
                "width": "20%"
            }
        ],
        "pageLength": 10,
        "language": {
            "emptyTable": "No past reservations found."
        },
        "width": "100%"
    });

    // Admin View Table
    dataTable = $('#reservationsTable').DataTable({
        "processing": true,
        "serverSide": true,
        "ajax": {
            "url": "/api/Reservation",
            "type": "GET",
            "datatype": "json",
            "data": function (d) {
                d.searchTerm = $('#searchInput').val();
                d.statusFilter = $('#statusDropdown').val();
            }
        },
        "responsive": true,
        "columns": [
            { "data": "siteNumber", "visible": false, "searchable": false, "type": "num" },
            { "data": "siteName", "width": "14%" },
            { "data": "reservationId", "width": "10%" },
            { "data": "customerName", "width": "20%" },
            { "data": "startDate", "width": "14%" },
            { "data": "endDate", "width": "14%" },
            // Hidden sort order column (hidden so it doesn't shift rendered columns)
            {
                "data": "reservationStatus",
                "render": function (data) {
                    return getStatusOrder(data);
                },
                "visible": false,
                "searchable": false,
                "type": "num"
            },
            {
                "data": "reservationStatus",
                "render": function (data) {
                    return renderStatus(data);
                },
                "width": "10%"
            },
            {
                "data": "reservationId",
                "render": function (data, type, row) {
                    let status = row.reservationStatus.toLowerCase();
                    let disabled = (status == "cancelled" || status == "completed")

                    return `<div class="text-center d-flex justify-content-between">
                        ${
                        disabled ?
                        `<a class="btn btn-outline-success btn-sm me-1 disabled" style="font-size: 0.8rem;" title="Cannot Edit.">
                            <i class="fas fa-edit"></i> Edit
                        </a>`
                        :
                        `<a href="/Admin/Reservations/Edit?id=${data}" class="btn btn-success btn-sm me-1" style="font-size: 0.8rem;">
                            <i class="fas fa-edit"></i> Edit
                        </a >`
                        }
                        <a href="/Admin/Transactions?reservationId=${data}" class="btn btn-primary btn-sm me-1" style="font-size: 0.8rem;">
                            View Transactions
                        </a>
                        ${
                        disabled ?
                        `<button class="btn btn-outline-danger btn-sm" style="font-size: 0.8rem;" disabled title="Cannot Edit.">
                            <i class="fas fa-ban"></i> Cancel
                        </button>`
                        :
                        `<button onclick="cancelReservation(${data})" class="btn btn-danger btn-sm" style="font-size: 0.8rem;">
                            <i class="fas fa-ban"></i> Cancel
                        </button>`
                        }
                    </div>`;
                },
                "width": "21%"
            }
        ],
        "ordering": true,
        "info": true, 
        "order": [
            [6, 'asc'],//Sort by the hidden status order column, see getStatusOrder() for sorting order
            [0, 'asc'] //and then sort by site number
        ], 
        "pageLength": 10,
        "searching": false,
        "language": {
            "emptyTable": "No reservations found."
        },
        "width": "100%",
        "columnDefs": [
            { "type": "status", "targets": 5 }
        ]
    });

    // Search Logic

    $('#searchButton').click(function () {
        dataTable.ajax.reload();
    });
    //$('#searchButton').click(function () {
    //    var searchTerm = $('#searchInput').val();
    //    var filter = $('#statusDropdown').val();

    //    if (searchTerm.trim() === '' && filter === '') {
    //        alert("Please enter a search term or apply a filter");
    //        return;
    //    }

    //    $.ajax({
    //        url: `/api/Reservation/Search?term=${searchTerm}&filter=${filter}`,
    //        type: 'GET',
    //        success: function (response) {
    //            dataTable.clear();
    //            dataTable.rows.add(response.data).draw();
    //            if (response.data.length === 0) {
    //                alert("No reservations found matching your search.");
    //            }
    //        },
    //        error: function () {
    //            alert("Error performing search. Please try again.");
    //        }
    //    });
    //});

    $('#resetSearch').click(function () {
        $('#searchInput').val('');
        $('#statusDropdown').val('');
        dataTable.ajax.url('/api/Reservation').load();
    });

    $('#searchInput').keypress(function (e) {
        if (e.which === 13) {
            $('#searchButton').click();
            return false;
        }
    });
});
function cancelReservation(reservationId) {
    if (confirm('Are you sure you want to cancel this reservation? This action cannot be undone.')) {
        $.ajax({
            url: '/api/Reservation/CancelReservation',
            type: 'POST',
            data: JSON.stringify({ reservationId: reservationId}),
            contentType: 'application/json',
            success: function (response) {
                alert(
                    `${response.message}\n` +
                    `Paid: $${response.amountPaid.toFixed(2)}\n` +
                    `Cancellation Fee: $${response.cancellationFee.toFixed(2)}\n` +
                    `Refund: $${response.refundAmount.toFixed(2)}\n` +
                    `Balance Due: $${response.balanceDue.toFixed(2)}`
                );

                if (response.receiptUrl) {
                    window.location.href = response.receiptUrl;
                }

                if (typeof currDataTable !== 'undefined') currDataTable.ajax.reload();
                if (typeof pastDataTable !== 'undefined') pastDataTable.ajax.reload();
                if (typeof dataTable !== 'undefined') dataTable.ajax.reload();
            },
            error: function (xhr) {
                alert('Error canceling reservation: ' + (xhr.responseText || 'Unknown error.'));
            }
        });
    }
}
