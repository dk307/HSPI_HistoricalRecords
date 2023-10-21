

async function ajaxPostPlugIn(url, data, successCallback = null, failureCallback = null) {
    const result = $.ajax({
        type: 'POST',
        async: 'true',
        url: '../HistoricalRecords/' + url,
        data:  JSON.stringify(data),
        timeout: 60000,
        success: function (response) {
			let result = JSON.parse(response);
			let errorMessage = result.error;

			if (errorMessage != null) {	
				if (failureCallback === null) {
					alert(errorMessage);
				} else {
					failureCallback(errorMessage);
				}
			} else {
				if (!(successCallback === null)) {
					successCallback(result.result); 
				};
			}
        },
        error: function (s) {
			const message = "Error in network call";
          	if (failureCallback === null) {
				alert(message);
			} else {
				failureCallback(message);
			}
        }
    });

    return result;
}