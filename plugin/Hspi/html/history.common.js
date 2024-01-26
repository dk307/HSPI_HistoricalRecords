async function ajaxPostPlugIn(url, data, successCallback = null, failureCallback = null, contextValue = null) {
    const result = $.ajax({
        type: 'POST',
        async: 'true',
        url: '../History/' + url,
        data: JSON.stringify(data),
        timeout: 60000,
        context: contextValue,
        success: function (response) {
            let result;
            try {
                result = JSON.parse(response);
            }
            catch (e) {
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

function roundValueWithPrecision(num, precision) {
    return Math.round(num * 10 ** precision) / 10 ** precision;
}

// needs moment.js
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

function humanizeSize(bytes, decimals) {
    if (bytes == 0) return '0 Bytes';
    var k = 1024,
        dm = decimals || 2,
        sizes = ['Bytes', 'KB', 'MB', 'GB', 'TB', 'PB', 'EB', 'ZB', 'YB'],
        i = Math.floor(Math.log(bytes) / Math.log(k));
    return parseFloat((bytes / Math.pow(k, i)).toFixed(dm)) + ' ' + sizes[i];
}

function humanizeDuration(periodSeconds) {
    let parts = [];
    const duration = moment.duration(periodSeconds, 'seconds');

    // return nothing when the duration is falsy or not correctly parsed (P0D)
    if (!duration || duration.toISOString() === "P0D") return;

    if (duration.years() >= 1) {
        const years = Math.floor(duration.years());
        parts.push(years + " " + (years > 1 ? "years" : "year"));
    }

    if (duration.months() >= 1) {
        const months = Math.floor(duration.months());
        parts.push(months + " " + (months > 1 ? "months" : "month"));
    }

    if (duration.days() >= 1) {
        const days = Math.floor(duration.days());
        parts.push(days + " " + (days > 1 ? "days" : "day"));
    }

    if (duration.hours() >= 1) {
        const hours = Math.floor(duration.hours());
        parts.push(hours + " " + (hours > 1 ? "hours" : "hour"));
    }

    if (duration.minutes() >= 1) {
        const minutes = Math.floor(duration.minutes());
        parts.push(minutes + " " + (minutes > 1 ? "minutes" : "minute"));
    }

    if (duration.seconds() >= 1) {
        const seconds = Math.floor(duration.seconds());
        parts.push(seconds + " " + (seconds > 1 ? "seconds" : "second"));
    }

    return parts.join(", ");
}

function iFrameSrcHelper() {
    $(document).ready(function () {
        window.addEventListener("blur", function (event) {
            // this closes any open dropdown
            $("body").trigger("click");
        }, false);
    });
}