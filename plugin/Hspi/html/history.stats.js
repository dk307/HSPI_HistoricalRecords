
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
					 "Minimum value",
					 getJsonValue(data[2], 'No records found'),					 
					],
					[	
					 "Maximum value",
					 getJsonValue(data[3], 'No records found'),
					],
					[	
					 "Distance between minimum and maximum value",
					 getJsonValue(data[4], 'No records found'),
					],
					[	
					 "Records count",
					 data[5],
					],
					[	
					 "Number of time value changed",
					 data[6],
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


