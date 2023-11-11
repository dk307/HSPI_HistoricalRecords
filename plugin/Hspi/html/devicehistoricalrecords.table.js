function setUpPersistanceTable() {
  $('#dt-persistence').DataTable({
	bLengthChange : true,
    bFilter : false,
	searching : false,
	order: [[0, 'desc']],
	paging: true,
	processing: true,
	responsive: true,
	stateSave: false,
    serverSide: true,
	  ajax: {
        url: '../HistoricalRecords/historyrecords',
        type: 'POST',
		data: function (d) {
            d.refId = featureId;	 
			d.min = displayStartDate;
			d.max = displayEndDate;	 
        }
    },
	columnDefs: [
		{
			targets: 0,
			render: function (data, type, full, meta) {
				return humanizeTime(data);
			}
		},
		{
			targets: 1,
			render: function (data, type, full, meta) {
				return roundValue(data);
			}
		},
		{
			targets: 3,
			render: function (data, type, full, meta) {
				if (data === null) {
					return "";
				} else {
					return humanizeDuration(data * 1000);
				}
			}
		},
	],
	initComplete: function () {
	},
  });
  
  $(".dataTables_length").addClass("bs-select"); 
}

$(document).ready(function () {
	setUpPersistanceTable();
	$('#loading').hide();
});	