
function getJsonValue(value, nullSub) {
	
	if (value == null) {
		return nullSub;
	} else {
		return roundValue(value.toString()) + " " + deviceUnits;
	}
}

function setUpPersistanceTable() {
  $('#dt-persistence').dataTable({
	paging: false,
	responsive: true,
	bInfo : false, 
	searching: false,
	ajax: function (data, callback, settings) {
		const formObject = {
		   refId : featureId,
		   min: displayStartDate,
		   max: displayEndDate,   
		};
		
		ajaxPostPlugIn("statisticsforrecords", formObject, function (result) {	
			const data= result["data"];
			const jsonResult =
			{
				data: [
					[	
					 "Average (Step)",
					 getJsonValue(data[0], '-'),
					],
					[	
					 "Average (Linear)",
					 getJsonValue(data[1], '-'),
					],
					[	
					 "Minimum Value",
					 getJsonValue(data[2], 'No records found'),
					],
					[	
					 "Maximum Value",
					 getJsonValue(data[3], 'No records found'),
					],
				]
			};			
			callback(jsonResult);
		});
	},
  });
  
  $(".dataTables_length").addClass("bs-select"); 
}

$(document).ready(function () {
	setUpPersistanceTable();
	$('#loading').hide();
});


