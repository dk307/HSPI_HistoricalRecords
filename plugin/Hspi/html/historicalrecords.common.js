

async function ajaxPostPlugIn(url, data, successCallback = null, failureCallback = null, contextValue = null) {
    const result = $.ajax({
        type: 'POST',
        async: 'true',
        url: '../HistoricalRecords/' + url,
        data:  JSON.stringify(data),
        timeout: 60000,
		context: contextValue,
        success: function (response) {
			
			let result;
			try {
				result = JSON.parse(response);
			}
			catch(e) {
				result = {
					error: "Failed with " + e.message,
				};
			}
			let errorMessage = result.error;

			if (errorMessage != null) {	
				if (failureCallback === null) {
					alert(errorMessage);
				} else {
					failureCallback(errorMessage, this);
				}
			} else {
				if (!(successCallback === null)) {
					successCallback(result.result, this); 
				};
			}
        },
        error: function (s) {
			const message = "Error in network call";
          	if (failureCallback === null) {
				alert(message);
			} else {
				failureCallback(message, this);
			}
        }
    });

    return result;
}

function humanizeTime(unix_timestamp) {	
	const ts = moment(unix_timestamp);	
	return ts.calendar({
		sameDay: '[Today], LTS',
		nextDay: '[Tomorrow], LTS',
		nextWeek: 'dddd, LTS',
		lastDay: '[Yesterday], LTS',
		lastWeek: '[Last] dddd, LTS',
		sameElse: 'll, LTS'
	});
}
