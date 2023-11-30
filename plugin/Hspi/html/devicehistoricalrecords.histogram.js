let chart;

//https://www.patternfly.org/charts/colors-for-charts/
const colors = ['#8BC1F7','#BDE2B9','#A2D9D9','#B2B0EA','#F9E0A2','#F4B678','#C9190B','#519DE9', '#7CC674', '#73C5C5','#8481DD','#F6D173','#EF9234','#A30000'];
			  
let dynamicColors = function(label, index) {	 
	if (label) {
		switch (label.toLowerCase()) {
			case "on": return "#FFC154";
			case "off": return "#47B39C";
		}
	} else {
		return "#D2D2D2"; //others
	}
	return colors[index];
};

const data = {
  labels: [],
  datasets: [{
    data: [],
  }]
};

const totalDurationMs = moment.duration(moment(displayEndDate) - moment(displayStartDate)).asMilliseconds();
const tooltip = {
    enabled: true,
    callbacks: { 
      // To change label in tooltip
      label: (data) => { 
           return humanizeDuration(data.parsed / 1000);
      },	  
	  afterLabel: (data) => { 
		   const percentage = Math.round(100 * (data.parsed * 100)/totalDurationMs) /100;
           return percentage.toString() + ' %';
      },  
    }
};

function fetchPieChartData(chart) {
	 
	const formObject = {
	   refId : featureId,
	   min: displayStartDate,
	   max: displayEndDate,
	   count: 10,
	};
	
	ajaxPostPlugIn("histogramforrecords", formObject, function (result) {	  
		chart.data.datasets[0].data =  result.time;
		chart.data.datasets[0].backgroundColor =  Array.from(result.labels, (x,i) => dynamicColors(x,i));
		
		chart.data.labels = Array.from( result.labels, (x) => (x === null ? "Others" : x));
		chart.update('none');		

		let legandTableBody = $('#idPieLegand tbody');
		for(let i=0; i< chart.data.datasets[0].data.length; i++) {	
			var row = $("<tr>");			
			row.append($("<th class='scope'>").html('<div style="background-color:' + chart.data.datasets[0].backgroundColor[i] + ';width:20px;height:20px;"></div>'));
			row.append($("<td>").text(chart.data.labels[i]));	
			row.append($("<td>").text(humanizeDuration(chart.data.datasets[0].data[i] / 1000)));	
			row.appendTo(legandTableBody);    
		}
	});
}
 
function setupPieChart() { 
	Chart.defaults.font.family = "sans-serif";
	Chart.defaults.font.size = 16;
	
	let ctx = document.getElementById("myPieChart").getContext('2d'); 
	
	let myChart = new Chart(ctx, {
		type: 'pie',
		data: data,
		options: {
			plugins: {
				tooltip: tooltip,
				legend: {
					display: false,
				},
			},
			
			responsive: true,
			maintainAspectRatio: false,

		},
	});
	 
	chart = myChart;
	fetchPieChartData(chart);
}

$(document).ready(function () {
	setupPieChart();
	$('#loading').hide();
});