//$(document).ready(function () {
//    //loadPage();
//    //loadList();
//    //loadSearch();
//});

//function loadPage() {
//    var datatable = $.ajax(
//        {
//            url: '/api/clientsite',
//            type: 'GET',
//            data: {
//                pageNumber: null,
//                pageSize: null,
//                siteTypeId: null
//            },
//            success: 

//        }
//    )
//}


//function loadList() {

//    $('#pageButton').click(function () {
//        var pageNum = $('#pageSelection').val()
//        if (parseInt(pageNum, 10) != NaN) {
//            pageNum = parseInt(pageNum);
//            $.ajax(
//                {
//                    url: '/api/clientsite',
//                    type: 'GET',
//                    data: {
//                        pageNumber: pageNum
//                    },

//                }
//            )
//        }
//    }
//    )

//}