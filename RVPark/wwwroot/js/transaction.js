var dataTable;

$(document).ready(function () {
    loadList();
    loadSearch();

    // Initialize the filters after the DataTable is ready
    $('#Transaction_table').on('init.dt', function () {
        paymentFilters();
    });


});
function loadList() {
    resId = new URLSearchParams(window.location.search).get("reservationId");
    //console.log("RES ID " + resId);
    if (resId == null) {
        resId = 0;
    }
    dataTable = $('#Transaction_table').DataTable({
        "ajax": {
            "url": "/api/transaction",
            "type": "GET",
            "datatype": "json",
            "data": {
                "reservationId": resId,
            },
        },
        "order": [[6, 'desc']], //Most recent transactions first
        "columns": [
            { data: "transactionId", width: "5%" },
            {
                data: "reservationId", width: "5%"//, //Should there ever be a reservation Details page, include below
                //"render": function (data) {
                //    return `<a href="Admin/Reservations/Details?reservationId=${data}">
                //                ${data}
                //            <\a>`
                //}
            },
            { data: "fullName", width: "15%" },
            { data: "amount", render: $.fn.dataTable.render.number(',', '.', 2, "$"), width: "10%" },
            { data: "description", width: "20%" },
            { data: "paymentMethod", width: "10%" },
            { data: "transactionDateTime", width: "25%" },
            { data: "previouslyRefunded", width: "5%", "visible": false },
            {
                data: "transactionId", width: "15%",
                "render": function (data) {
                    return `<div class="text-center d-flex justify-content-between">
                        <a href="/Admin/Transactions/Details?transactionId=${data}" class="btn btn-primary btn-sm me-1" style="font-size: 0.8rem;">
                            <i class="fas fa-circle-info"></i> Details
                        </a>
                        <button onClick="Refund('/api/transaction/Refund?transactionId=${data}')" class="btn btn-danger btn-sm" style="font-size: 0.8rem;">
                            <i class="fas fa-rotate-left"></i> Refund
                        </button>
                    </div>`;
                }
            }
        ],
        "searching": true,
        "language": {
            "emptyTable": "no data found."
        },
        "width": "100%",
        "columnDefs": [{ "targets": [7], "visible": false }]
    });
}

function Refund(url) {
    console.log("REFUND TEST");
    var confirmeRefund = confirm("WARNING: Are you sure you want to refund this transaction?");

    if (confirmeRefund == true) {
        $.ajax({
            type: 'POST',
            url: url,
            success: function (data) {
                if (data.success) {
                    alert(data.message);
                    dataTable.ajax.reload();
                }
                else {
                    alert(data.message);
                }
            }
        })
    }
}

function loadSearch() {
    // Add a reset button next to search
    $('#searchTransactionsButton').click(function () {

        console.log('Transaction TEST');

        var searchTerm = $('#searchInput').val();

        if (searchTerm.trim() === '') {
            alert("Please enter a search term");
            return;
        }


        // Make a direct AJAX call
        $.ajax({
            url: `/api/transaction/SearchTransactions?transactionId=${encodeURIComponent(searchTerm)}`,
            type: 'GET',
            success: function (response) {
                dataTable.clear();

                dataTable.rows.add(response.data).draw();

                // Show message if no results
                if (response.data.length === 0) {
                    alert("No transactions found matching your search.");
                }
            },
            error: function (xhr, status, error) {
                alert("Error performing search. Please try again." + error);
            }
        });
    });


    // Custom search functionality
    $('#searchReservationsButton').click(function () {

        console.log('RESERVE TEST');

        var searchTerm = $('#searchInput').val();

        if (searchTerm.trim() === '') {
            alert("Please enter a search term");
            return;
        }


        // Make a direct AJAX call
        $.ajax({
            url: `/api/transaction/ReservationSearch?reservationId=${encodeURIComponent(searchTerm)}`,
            type: 'GET',
            success: function (response) {
                dataTable.clear();

                dataTable.rows.add(response.data).draw();

                // Show message if no results
                if (response.data.length === 0) {
                    alert("No transactions found matching your search.");
                }
            },
            error: function (xhr, status, error) {
                alert("Error performing search. Please try again.");
            }
        });
    });

    // Reset search
    $('#resetTransactionSearch').click(function () {
        $('#searchInput').val('');
        dataTable.ajax.url('/api/transaction').load();
    });

    // Allow search on Enter key press
    $('#searchInput').keypress(function (e) {
        if (e.which === 13) { // Enter key
            $('#searchButton').click();
            return false; // Prevent form submission
        }
    });
}

let listenerBinding = false;
function paymentFilters() {
    const checkboxes = document.querySelectorAll("input[name='PaymentMethod']");
    const allCheck = document.getElementById("allCheck");
    const table = $('#Transaction_table').DataTable();
    //console.log("paymentFilters Function!")

    function togglePayment() {
        console.log("TogglePayment Function!")
        // Collect all checked values
        const selected = Array.from(checkboxes)
            .filter(c => c.checked)
            .map(c => c.value);

        console.log("Selected filters:", selected);

        // If "All" is checked or none checked ? reset filters
        if (selected.includes("All") || selected.length === 0) {
            table.column(5).search('')
            table.column(7).search('').draw();
            $(document.getElementById("allCheck")).prop('checked', true) //recheck it if selected.length == 0
            return;
        }

        // Handle refunded case (can combine with other filters)
        const isRefunded = selected.includes("Refund");

        // Remove "Refunded" from list so we only search payment methods in column 5
        const paymentValues = selected.filter(v => v !== "Refund");

        console.log("filtering...")
        // Filter by payment method
        if (paymentValues.length > 0) {
            const regex = paymentValues.join('|'); // OR filter
            console.log("paymentValues.length>0")
            table.column(5).search(regex, true, false)
        } else {
            table.column(5).search('')
        }

        // Filter by refunded status
        if (isRefunded) {
            console.log("filtering by refunded")
            table.column(7).search('Yes', true, false)
        } else {
            table.column(7).search('')
        }
        table.draw(); //reload the data table
    } //End togglePayment

    //Bind the listeners once so that it doesn't run into an infinite loop hahaha
    if (!listenerBinding) {
        checkboxes.forEach(cb => {
            cb.addEventListener("change", function () {
                if (this.id === "allCheck" && this.checked) {
                    checkboxes.forEach(otherCb => {
                        if (otherCb !== this) otherCb.checked = false;
                    });
                    table.column(5).search('');
                    table.column(7).search('').draw();
                    return;
                }
                else {
                    allCheck.checked = false;
                }
                togglePayment();
            });
        });
        listenerBinding = true;
    }//end if

}