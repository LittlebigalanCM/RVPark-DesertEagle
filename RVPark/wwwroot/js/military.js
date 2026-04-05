
// universal delete function for military branch/rank/status
// url links to military controller  
function Delete(url) {

    var confirmeDelete = confirm("WARNING: Are you sure you want to delete this?");
    if (confirmeDelete == true) {
        $.ajax({
            type: 'DELETE',
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