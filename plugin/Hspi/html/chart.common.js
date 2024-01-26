const defaultTension = 0.1;

function getRangeString(min, max) {
    const minDateString = humanizeTime(min);
    const maxDateString = humanizeTime(max);
    return minDateString + " - " + maxDateString;
}

const zoomOptions = {
    limits: {
        x: {
            min: 'original',
            max: 'original',
            minRange: 60 * 1000
        },
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
            x: {
                minRange: 60
            },
        },
        mode: 'x',
        onZoomComplete: startFetch
    }
};

const scales = {
    x: {
        position: 'bottom',
        min: 0,
        max: 0,
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
};

const tooltip = {
    enabled: true,
    callbacks: {
        label: (data) => {			
            return roundValueWithPrecision(data.raw.y, data.chart.data.datasets[data.datasetIndex].precision).toString() + 
			       data.chart.data.datasets[data.datasetIndex].deviceUnits;
        },
    }
};

function startFetchWithMinMax(chart, min, max, datasetIndex) {
    featureRefId = chart.data.datasets[datasetIndex].featureRefId;
    console.log('Fetching data between ' + min + ' and ' + max + ' for refId ' + featureRefId);
    const formObject = {
        refId: featureRefId,
        min: min,
        max: max,
        fill: chart.data.datasets[datasetIndex].stepped ? 0 : 1,
        points: Math.max(Math.abs((chart.chartArea.right - chart.chartArea.left) / 5), 25),
    };

    ajaxPostPlugIn("graphrecords", formObject, function(result) {
        console.log('Fetched data between ' + min + ' and ' + max + ' for refId ' + featureRefId);
        chart.data.datasets[datasetIndex].data = result.data;
        chart.stop(); // make sure animations are not running
        chart.options.plugins.subtitle.text = getRangeString(min, max);

        if ((max - min) > (1000 * 60 * 60 * 24 * 2)) {
            chart.options.scales.x.time.displayFormats.hour = "MMM D";
        } else {
            chart.options.scales.x.time.displayFormats.hour = "hA";
        }

        chart.update();
    });
}

function addDatasetToChart(chart, backgroundColor, fill, borderColor, featureRefId, deviceUnits, precision, label) {
	// generate a unique y axis based on units
	const yAxisID = "y" + deviceUnits.replace(/\s/g, '');
    const dataset = {
        data: [],
        stepped: false,
        tension: defaultTension,
        pointStyle: false,
        fill: fill,
		backgroundColor: backgroundColor,
        borderColor: borderColor,
        borderWidth: 2,
        pointRadius: 3,
        featureRefId: featureRefId,
		deviceUnits: deviceUnits,
		yAxisID: yAxisID,
		precision: precision,
		label: label,
    };
	
	// add y axis if not added
	if (!chart.options.scales[yAxisID]) {
		chart.options.scales[yAxisID] = {
			type: 'linear',
			position: chart.options.nextYAxisIsLeft ? 'left' : 'right',
			display: true,
			ticks : {},
		};
		
		chart.options.nextYAxisIsLeft = !chart.options.nextYAxisIsLeft;
		
		// set the axis unit
		chart.options.scales[yAxisID].ticks.callback = function(value, index, ticks) {
			return Chart.Ticks.formatters.numeric.apply(this, [value, index, ticks]) + deviceUnits;
		};	
	}
		
    chart.data.datasets.push(dataset);
	return dataset;
}

function startFetch({chart}) {
    const {min,max} = chart.scales.x;
	
    for (let i = 0; i < chart.data.datasets.length; i++) {
        startFetchWithMinMax(chart, Math.round(min), Math.round(max), 0);
    }
}

function createLineChart(ctx, min, max) {
    Chart.defaults.font.family = "sans-serif";
    Chart.defaults.font.size = 16;
		
	scales.x.min = min;
	scales.x.max = max;

    let myChart = new Chart(ctx, {
        type: "line",
        data: {
            datasets: [],
        },
        options: {
			nextYAxisIsLeft : true,
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

    return myChart;
}

function chartFunction(reason) {
    switch (reason) {
        case 0:
            chart.resetZoom();
            break;
        case 1:
			for (let i = 0; i < chart.data.datasets.length; i++) {
				chart.data.datasets[i].stepped = 'before';
				chart.data.datasets[i].tension = 0;
			}
            chart.update();
            break;
        case 2:
			for (let i = 0; i < chart.data.datasets.length; i++) {
				chart.data.datasets[i].stepped = false;
				chart.data.datasets[i].tension = defaultTension;
			}
            chart.update();
            break;
    }
    startFetch({chart});
}
