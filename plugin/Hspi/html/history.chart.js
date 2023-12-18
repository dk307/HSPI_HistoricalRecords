
let chart;

const colors = {
  blue: {
	default: "#007bff",
	half: "rgba(00, 123, 233, 0.5)",
	quarter: "rgba(00, 123, 233, 0.25)",
	zero: "rgba(00, 123, 233, 0)"
  }, 
};

const defaultTension = 0.1;

function getRangeString(min, max) {
	const minDateString = humanizeTime(min);
	const maxDateString = humanizeTime(max);
	return minDateString + " - " + maxDateString;
}

function startFetchWithMinMax(chart, min, max) {
	// $('#loading').show();
	console.log('Fetching data between ' + min + ' and ' + max);
	const formObject = {
	   refId : featureId,
	   min: min,
	   max: max,
	   fill: chart.data.datasets[0].stepped ? 0 : 1,
	   points: Math.max(Math.abs((chart.chartArea.right - chart.chartArea.left)/5), 25),
	};
	
	ajaxPostPlugIn("graphrecords", formObject, function (result) {		
		console.log('Fetched data between ' + min + ' and ' + max);
		chart.data.datasets[0].data = result.data;
		chart.stop(); // make sure animations are not running
		chart.options.plugins.subtitle.text = getRangeString(min, max);
		
		if ((max - min) > (1000 * 60 * 60 * 24 * 2)) {
			chart.options.scales.x.time.displayFormats.hour = "MMM D";
		} else {
			chart.options.scales.x.time.displayFormats.hour = "hA";
		}
		
		// $('#loading').hide();
		
		chart.update();	
		
	});
}

function startFetch({chart}) {
  const {min, max} = chart.scales.x;
  startFetchWithMinMax(chart, Math.round(min), Math.round(max));
}
	
const zoomOptions = {
  limits: {
	x: {min: 'original', max: 'original', minRange: 60 * 1000},
  },
  pan: {
	enabled: true,
	mode: 'x',
	modifierKey: 'ctrl',
	onPanComplete: startFetch
  },
  zoom: {
	wheel: {
	  enabled: true,
	},
	drag: {
	  enabled: true,
	},
	pinch: {
	  enabled: true
	},
	limits: {
      x: {minRange: 60},        
    },
	mode: 'x',
	onZoomComplete: startFetch
  }
};

const scales = {
  x: {
	position: 'bottom',
	min: displayStartDate,
	max: displayEndDate,
	time: {
		displayFormats: {
		  hour: "hA"
		},
	},
	type: 'time',
	ticks: {
	  autoSkip: true,
	  autoSkipPadding: 50,
	  maxRotation: 0
	},	
  },
  y: {
	type: 'linear',
	position: 'left',
	ticks: {
		callback: function(value, index, ticks) {
			return Chart.Ticks.formatters.numeric.apply(this, [value, index, ticks]) + deviceUnits;
		}
	}
  },
};

const tooltip = {
    enabled: true,
    callbacks: { 
      label: (data) => { 
           return roundValue(data.formattedValue).toString() + deviceUnits;
      },	  
    }
};

function setupLineChart() { 
	Chart.defaults.font.family = "sans-serif";
	Chart.defaults.font.size = 16;
	
	let ctx = document.getElementById("myLineChart").getContext('2d'); 
	
	let gradient = ctx.createLinearGradient(0, 25, 0, 300);
	gradient.addColorStop(0, colors.blue.half);
	gradient.addColorStop(0.35, colors.blue.quarter);
	gradient.addColorStop(1, colors.blue.zero);

	let myChart = new Chart(ctx, {
	  type: "line",
	  data: {
			datasets: [{
				data: [],			 
				stepped : false,
				tension : defaultTension,
				pointStyle : false,
				fill: true,
				backgroundColor: gradient,
				pointBackgroundColor: colors.blue.default,
				borderColor: colors.blue.default,
				borderWidth: 2,
				pointRadius: 3
			}, 			 
		]
	  },
	  options: {
		scales: scales,
		responsive: true,
		maintainAspectRatio: false,
		plugins: {
			tooltip: tooltip,
			zoom: zoomOptions,
			legend: {
				display: false
			},	
			subtitle: {
				display: true,
				text: '',
			}				
		},
		transitions: {
		  zoom: {
			animation: {
			  duration: 100
			}
		  }
		}
	  },  
	});
	 
	chart = myChart;
	startFetchWithMinMax(myChart, displayStartDate, displayEndDate);
}
	
function chartFunction(reason) {
	switch(reason)
	{
		case 0:
		chart.resetZoom();
		startFetch({chart});
		break;
		case 1:
		chart.data.datasets[0].stepped = 'before';
		chart.data.datasets[0].tension = 0;
		chart.update();
		startFetch({chart});
		break;
		case 2:
		chart.data.datasets[0].stepped = false;
		chart.data.datasets[0].tension = defaultTension;
		chart.update();
		startFetch({chart});
		break;	
	}
}

$(document).ready(function () {
	setupLineChart();
	$('#loading').hide();
});
