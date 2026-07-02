(function () {
    "use strict";

    var PREFIX = "es-admin-filter:";

    function cleanUrl() {
        if (window.location.search) {
            history.replaceState(null, "", window.location.pathname);
        }
    }

    function absorbLegacyQuery() {
        if (!window.location.search) return {};
        return Object.fromEntries(new URLSearchParams(window.location.search));
    }

    function save(key, form) {
        if (!(form instanceof HTMLFormElement)) return;
        try {
            sessionStorage.setItem(PREFIX + key, JSON.stringify(Object.fromEntries(new FormData(form))));
        } catch (e) { /* ignore */ }
    }

    function load(key) {
        try {
            var raw = sessionStorage.getItem(PREFIX + key);
            return raw ? JSON.parse(raw) : null;
        } catch (e) {
            return null;
        }
    }

    function applyToForm(form, data) {
        if (!(form instanceof HTMLFormElement) || !data) return;
        Object.keys(data).forEach(function (name) {
            var el = form.elements.namedItem(name);
            if (!el) return;
            var value = data[name] ?? "";
            if (el instanceof RadioNodeList) {
                Array.prototype.forEach.call(el, function (node) {
                    if (node instanceof HTMLInputElement) {
                        node.checked = node.value === String(value);
                    }
                });
                return;
            }
            if ("value" in el) {
                el.value = value;
            }
        });
    }

    function initForm(key, form, options) {
        if (!(form instanceof HTMLFormElement)) return;
        var legacy = absorbLegacyQuery();
        var stored = load(key);
        var data = Object.keys(legacy).length > 0 ? legacy : stored;
        if (data) {
            applyToForm(form, data);
        } else if (options && typeof options.applyDefaults === "function") {
            options.applyDefaults(form);
        }
        cleanUrl();
    }

    function escapeHtml(s) {
        return String(s ?? "").replace(/&/g, "&amp;").replace(/</g, "&lt;").replace(/>/g, "&gt;");
    }

    window.AdminFilterState = {
        cleanUrl: cleanUrl,
        absorbLegacyQuery: absorbLegacyQuery,
        save: save,
        load: load,
        applyToForm: applyToForm,
        initForm: initForm,
        escapeHtml: escapeHtml,
        todayIso: function () {
            var d = new Date();
            var m = String(d.getMonth() + 1).padStart(2, "0");
            var day = String(d.getDate()).padStart(2, "0");
            return d.getFullYear() + "-" + m + "-" + day;
        }
    };
})();
