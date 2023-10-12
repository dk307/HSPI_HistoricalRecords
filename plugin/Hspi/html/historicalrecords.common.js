

async function ajaxPostPlugIn(url, data, successCallback) {
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
				alert(errorMessage);
			} else {
				if (!(successCallback === null)) {
					successCallback(result.result); 
				};
			}
        },
        error: function (s) {
          	alert("Error in operation");
        }
    });

    return result;
}