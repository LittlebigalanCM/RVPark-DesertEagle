var dataTable;

$(document).ready(function () {
    loadList();

    $('#searchFeeTypeButton').on('click', function () {
        const value = $('#searchFeeTypeInput').val();
        dataTable.search(value).draw();
    });
    //adding for enter key.
    $('#searchFeeTypeInput').on('keypress', function (e) {
        if (e.which === 13) {
            e.preventDefault();
            const value = $(this).val();
            dataTable.search(value).draw();
        }
    });
    // Reset button click
    $('#resetFeeTypeSearch').on('click', function () {
        $('#searchFeeTypeInput').val('');
        dataTable.search('').draw();
    });

});
function loadList() {
    dataTable = $('#Transaction_Type_table').DataTable({
        "ajax": {
            "url": "/api/transactionType",
            "type": "GET",
            "datatype": "json"
        },
        "responsive": true,
        "columns": [
            { data: "name", width: "20%" },
            { data: "amountDisplay", width: "15%" },
            { data: "calculation", width: "15%" },
            { data: "triggerType", width: "15%" },
            {
                data: "transactionTypeId",
                width: "35%",
                "render": function (data) {
                    var isLocked = GetLockedStatus(`/api/transactionType/GetLockedStatus?typeId=${data}`);

                    return `<div class="text-center">
                                <a href="/Admin/Fees/Upsert?id=${data}" 
                                   class="btn btn-success btn-sm me-1" 
                                   style="font-size: 0.8rem; padding: 0.25rem 0.5rem;">
                                    <i class="fas fa-edit"></i> Edit
                                </a>
                                <a onClick="LockUnlock('/api/transactionType/LockUnlockTransactionType?typeId=${data}', ${isLocked})"
                                   class="btn btn-warning text-white btn-sm"
                                   style="font-size: 0.8rem; padding: 0.25rem 0.5rem;">
                                   <i class="fas fa-lock"></i>
                                   ${isLocked === true ? 'Unlock' : 'Lock'}
                                </a>
                            </div>`;
                }
            }
        ],
        "searching": true,
        "language": {
            "emptyTable": "No data found."
        },
        "width": "100%",
        "dom": 'lrtip' // Hides the default search box"
        
    });
}

function GetLockedStatus(url) {
    var status = false;
    $.ajax({
        async: false,
        type: 'GET',
        url: url,
        success: function (data) {
            status = data.isLocked;
        }
    });
    return status;
}

function LockUnlock(url, lockState) {
    var action = lockState ? "unlock" : "lock";
    if (confirm(`Are you sure you want to ${action} this transaction type?`)) {
        $.post(url, function (data) {
            if (data.success) {
                alert(data.message);
                dataTable.ajax.reload();
            } else {
                alert(data.message);
            }
        });
    }
}
