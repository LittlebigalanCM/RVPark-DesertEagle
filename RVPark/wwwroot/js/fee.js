var dataTable;

$(document).ready(function () {
    loadList();

    // Search Button
    $('#searchFeeButton').on('click', function () {
        const value = $('#searchFeeInput').val();
        dataTable.search(value).draw();
    });

    // Reset Button
    $('#resetFeeSearch').on('click', function () {
        $('#searchFeeInput').val('');
        dataTable.search('').draw();
    });

    // Enter Key
    $('#searchFeeInput').on('keypress', function (e) {
        if (e.which === 13) {
            e.preventDefault();
            const value = $(this).val();
            dataTable.search(value).draw();
        }
    });
});

function loadList() {
    dataTable = $('#Fee_table').DataTable({
        "ajax": {
            "url": "/api/fee",
            "type": "GET",
            "datatype": "json"
        },
        "responsive": true,
        "columns": [
            { data: "name", width: "25%" },
            { data: "amountDisplay", width: "15%" },
            { data: "calculationTypeDisplay", width: "20%" },
            { data: "triggerTypeDisplay", width: "20%" },
            {
                data: "feeId",
                width: "20%",
                "render": function (data) {
                    var isEnabled = GetEnabledStatus(`/api/fee/GetEnabledStatus?feeId=${data}`);

                    return `
                        <div class="text-center">
                            <a href="/Admin/Fees/Upsert?feeId=${data}&returnUrl=/Admin/Fees/Index" 
                               class="btn btn-success btn-sm me-1" 
                               style="font-size: 0.8rem; padding: 0.25rem 0.5rem;">
                                <i class="fas fa-edit"></i> Edit
                            </a>
                            <a onClick="ToggleFeeStatus('/api/fee/ToggleFeeStatus?feeId=${data}', ${isEnabled})"
                               class="btn ${isEnabled ? 'btn-warning text-white' : 'btn-secondary'} btn-sm"
                               style="font-size: 0.8rem; padding: 0.25rem 0.5rem;">
                               <i class="fas ${isEnabled ? 'fa-toggle-on' : 'fa-toggle-off'}"></i>
                               ${isEnabled ? 'Disable' : 'Enable'}
                            </a>
                        </div>`;
                }
            }
        ],
        "searching": true,
        "language": {
            "emptyTable": "No fees found."
        },
        "width": "100%",
        "dom": "lrtip"
    });
}

function GetEnabledStatus(url) {
    var status = false;
    $.ajax({
        async: false,
        type: 'GET',
        url: url,
        success: function (data) {
            status = data.isEnabled;
        }
    });
    return status;
}

function ToggleFeeStatus(url, enabledState) {
    var action = enabledState ? "disable" : "enable";
    if (confirm(`Are you sure you want to ${action} this fee?`)) {
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
