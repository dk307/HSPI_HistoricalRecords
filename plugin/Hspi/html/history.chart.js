let chart;

const colors = {
    blue: {
        default: "#007bff",
        half: "rgba(00, 123, 233, 0.5)",
        quarter: "rgba(00, 123, 233, 0.25)",
        zero: "rgba(00, 123, 233, 0)"
    },
};

function setupLineChart() {
    let ctx = document.getElementById("myLineChart").getContext('2d');

    let gradient = ctx.createLinearGradient(0, 25, 0, 300);
    gradient.addColorStop(0.5, colors.blue.half);
    gradient.addColorStop(0.25, colors.blue.quarter);
    gradient.addColorStop(1, colors.blue.zero);

    chart = createLineChart(ctx, displayStartDate, displayEndDate);	
    addDatasetToChart(chart, gradient, true, colors.blue.default, featureId, deviceUnits, devicePrecision);
    
	for (let i = 0; i < chart.data.datasets.length; i++) {
        startFetchWithMinMax(chart, displayStartDate, displayEndDate, i);
    }
}

$(document).ready(function() {
    setupLineChart();
    $('#loading').hide();
});