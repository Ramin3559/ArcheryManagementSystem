/**
 * Bakı vaxtı (UTC+4) — TV/planşet brauzerinin öz timezone-u nə olursa olsun eyni saat göstərilir.
 */
(function (global) {
    "use strict";

    var BAKU_OFFSET_MIN = 240;

    function pad2(n) {
        n = n | 0;
        return n < 10 ? "0" + n : String(n);
    }

    function formatBakuFromUtcMs(utcMs, withSeconds) {
        var t = new Date(utcMs + BAKU_OFFSET_MIN * 60000);
        var h = pad2(t.getUTCHours());
        var m = pad2(t.getUTCMinutes());
        if (!withSeconds) {
            return h + ":" + m;
        }
        return h + ":" + m + ":" + pad2(t.getUTCSeconds());
    }

    function formatWallClock() {
        return formatBakuFromUtcMs(Date.now(), true);
    }

    function parseApiDate(value) {
        if (!value) {
            return new Date(0);
        }
        var needsUtcSuffix = typeof value === "string" && !/[zZ]|[+-]\d{2}:\d{2}$/.test(value);
        return new Date(needsUtcSuffix ? value + "Z" : value);
    }

    function formatTimeFromApi(iso, withSeconds) {
        if (!iso) {
            return "—";
        }
        var d = parseApiDate(iso);
        if (Number.isNaN(d.getTime())) {
            return "—";
        }
        return formatBakuFromUtcMs(d.getTime(), !!withSeconds);
    }

    function getBakuDateParts(utcMs) {
        var t = new Date((utcMs == null ? Date.now() : utcMs) + BAKU_OFFSET_MIN * 60000);
        return {
            year: t.getUTCFullYear(),
            month: t.getUTCMonth(),
            date: t.getUTCDate()
        };
    }

    /** Bakı yerli saatını (HH:mm) UTC epoch-a çevirir. */
    function bakuLocalTimeToUtcMs(hour, minute, second) {
        var sec = second == null ? 0 : (second | 0);
        var p = getBakuDateParts(Date.now());
        return Date.UTC(p.year, p.month, p.date, hour | 0, minute | 0, sec) - BAKU_OFFSET_MIN * 60000;
    }

    function bakuLocalTimeToUtcIso(hour, minute, second) {
        return new Date(bakuLocalTimeToUtcMs(hour, minute, second)).toISOString();
    }

    global.BakuTime = {
        OFFSET_MIN: BAKU_OFFSET_MIN,
        pad2: pad2,
        formatBakuFromUtcMs: formatBakuFromUtcMs,
        formatWallClock: formatWallClock,
        parseApiDate: parseApiDate,
        formatTimeFromApi: formatTimeFromApi,
        getBakuDateParts: getBakuDateParts,
        bakuLocalTimeToUtcMs: bakuLocalTimeToUtcMs,
        bakuLocalTimeToUtcIso: bakuLocalTimeToUtcIso
    };
})(window);
