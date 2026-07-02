(function (global) {
    const EMPTY_MARKERS = [
        "Məlumat yoxdur",
        "seans qeydi yoxdur",
        "aktiv zolaq yoxdur",
        "avadanlıq əməliyyatı yoxdur"
    ];

    function isDataRow(tr) {
        if (!tr || tr.classList.contains("hesabat-sum-row") || tr.classList.contains("daily-total-row")) {
            return false;
        }
        if (tr.querySelector("td[colspan]")) {
            return false;
        }
        const text = (tr.textContent || "").trim();
        return !EMPTY_MARKERS.some(function (m) { return text.indexOf(m) >= 0; });
    }

    function parseNum(text) {
        const t = String(text || "").replace(/[^\d.,-]/g, "").replace(",", ".");
        const n = parseFloat(t);
        return Number.isFinite(n) ? n : 0;
    }

    function cellText(tr, col) {
        const cell = tr.cells[col];
        return cell ? cell.textContent.trim() : "";
    }

    function escapeHtml(s) {
        return String(s)
            .replace(/&/g, "&amp;")
            .replace(/</g, "&lt;")
            .replace(/"/g, "&quot;");
    }

    function ensureFilterRow(tbody) {
        const table = tbody.closest("table");
        const thead = table ? table.querySelector("thead") : null;
        const headerRow = thead ? thead.querySelector("tr.hesabat-header-labels, tr:first-child") : null;
        if (!thead || !headerRow) {
            return [];
        }

        let filterRow = thead.querySelector("tr.hesabat-filter-row");
        const colCount = headerRow.cells.length;

        if (!filterRow) {
            filterRow = document.createElement("tr");
            filterRow.className = "hesabat-filter-row";
            for (let c = 0; c < colCount; c++) {
                const th = document.createElement("th");
                const sel = document.createElement("select");
                sel.className = "hesabat-col-filter";
                sel.dataset.col = String(c);
                sel.title = "Filter";
                sel.innerHTML = '<option value="">Hamısı</option>';
                sel.addEventListener("change", function () { applyFilters(tbody); });
                th.appendChild(sel);
                filterRow.appendChild(th);
            }
            thead.appendChild(filterRow);
        }

        return Array.from(filterRow.querySelectorAll(".hesabat-col-filter"));
    }

    function parseSumCols(tbody) {
        return (tbody.dataset.sumCols || "")
            .split(",")
            .map(function (x) { return parseInt(x.trim(), 10); })
            .filter(function (n) { return Number.isFinite(n); });
    }

    function refreshTable(tbody) {
        if (!tbody) {
            return;
        }

        tbody._hesabatSumCols = parseSumCols(tbody);
        const rows = Array.from(tbody.querySelectorAll("tr")).filter(isDataRow);
        tbody._hesabatAllRows = rows;

        tbody.querySelectorAll(".hesabat-sum-row, .daily-total-row").forEach(function (r) { r.remove(); });

        const filterSelects = ensureFilterRow(tbody);
        filterSelects.forEach(function (sel) {
            const col = parseInt(sel.dataset.col, 10);
            const values = new Set();
            rows.forEach(function (tr) {
                const v = cellText(tr, col);
                values.add(v || "—");
            });
            const current = sel.value;
            const sorted = Array.from(values).sort(function (a, b) { return a.localeCompare(b, "az"); });
            sel.innerHTML = '<option value="">Hamısı</option>'
                + sorted.map(function (v) {
                    return '<option value="' + escapeHtml(v) + '">' + escapeHtml(v) + "</option>";
                }).join("");
            if (current && sorted.indexOf(current) >= 0) {
                sel.value = current;
            } else {
                sel.value = "";
            }
        });

        rows.forEach(function (r) { tbody.appendChild(r); });
        applyFilters(tbody);
    }

    function applyFilters(tbody) {
        const rows = tbody._hesabatAllRows || [];
        const table = tbody.closest("table");
        const filters = Array.from(table ? table.querySelectorAll(".hesabat-col-filter") : [])
            .map(function (sel) {
                return { col: parseInt(sel.dataset.col, 10), val: sel.value };
            });

        let visible = 0;
        rows.forEach(function (tr) {
            const show = filters.every(function (f) {
                return !f.val || cellText(tr, f.col) === f.val;
            });
            tr.style.display = show ? "" : "none";
            if (show) {
                visible++;
            }
        });

        tbody.querySelectorAll(".hesabat-sum-row, .daily-total-row").forEach(function (r) { r.remove(); });

        if (visible === 0 || rows.length === 0) {
            return;
        }

        const sumCols = tbody._hesabatSumCols || [];
        if (sumCols.length === 0) {
            return;
        }

        const colCount = rows[0].cells.length;
        const sumTr = document.createElement("tr");
        sumTr.className = "hesabat-sum-row daily-total-row";

        for (let c = 0; c < colCount; c++) {
            const td = document.createElement("td");
            if (c === 0) {
                td.textContent = "Cəmi";
            } else if (sumCols.indexOf(c) >= 0) {
                let total = 0;
                let isDecimal = false;
                rows.forEach(function (tr) {
                    if (tr.style.display === "none") {
                        return;
                    }
                    const raw = cellText(tr, c);
                    if (raw.indexOf(".") >= 0 || raw.indexOf(",") >= 0) {
                        isDecimal = true;
                    }
                    total += parseNum(raw);
                });
                td.textContent = isDecimal ? total.toFixed(2) : String(Math.round(total));
            }
            sumTr.appendChild(td);
        }

        tbody.appendChild(sumTr);
    }

    function mountTable(tbody) {
        if (!tbody || !tbody.hasAttribute("data-filter-table")) {
            return;
        }
        tbody.dataset.filterMounted = "1";
        refreshTable(tbody);
    }

    function initAll() {
        document.querySelectorAll("tbody[data-filter-table]").forEach(mountTable);
    }

    function refreshById(id) {
        const tbody = document.getElementById(id);
        if (!tbody) {
            return;
        }
        if (tbody.dataset.filterMounted !== "1") {
            mountTable(tbody);
            return;
        }
        refreshTable(tbody);
    }

    function getTableHeaders(tbody) {
        const table = tbody.closest("table");
        const headerRow = table
            ? table.querySelector("thead tr.hesabat-header-labels, thead tr:first-child")
            : null;
        if (!headerRow) {
            return [];
        }
        return Array.from(headerRow.cells).map(function (th) {
            return (th.textContent || "").trim();
        });
    }

    function rowToValues(tr, colCount) {
        const values = [];
        Array.from(tr.cells).forEach(function (cell) {
            const span = cell.colSpan || 1;
            values.push((cell.textContent || "").trim());
            for (let i = 1; i < span; i++) {
                values.push("");
            }
        });
        while (values.length < colCount) {
            values.push("");
        }
        return values.slice(0, colCount);
    }

    function getActiveFilterLabels(tbody) {
        const table = tbody.closest("table");
        const headers = getTableHeaders(tbody);
        return Array.from(table ? table.querySelectorAll(".hesabat-col-filter") : [])
            .filter(function (sel) { return sel.value; })
            .map(function (sel) {
                const col = parseInt(sel.dataset.col, 10);
                const name = headers[col] || ("Sütun " + (col + 1));
                return name + ": " + sel.value;
            });
    }

    function collectExportData(tbodyId) {
        const tbody = document.getElementById(tbodyId);
        if (!tbody) {
            return null;
        }

        const headers = getTableHeaders(tbody);
        const colCount = headers.length;
        const dataRows = tbody._hesabatAllRows
            || Array.from(tbody.querySelectorAll("tr")).filter(isDataRow);

        const rows = dataRows
            .filter(function (tr) { return tr.style.display !== "none"; })
            .map(function (tr) { return rowToValues(tr, colCount); });

        const sumRow = tbody.querySelector(".hesabat-sum-row, .daily-total-row");
        if (sumRow) {
            rows.push(rowToValues(sumRow, colCount));
        }

        return {
            headers: headers,
            rows: rows,
            filters: getActiveFilterLabels(tbody)
        };
    }

    async function exportFiltered(tbodyId, sheetName, rangeLabel) {
        const data = collectExportData(tbodyId);
        if (!data || !data.headers.length) {
            alert("Cədvəl məlumatı tapılmadı.");
            return;
        }

        let subtitle = rangeLabel || "";
        if (data.filters.length) {
            const filterPart = "Filter: " + data.filters.join("; ");
            subtitle = subtitle ? subtitle + " | " + filterPart : filterPart;
        }

        try {
            const resp = await fetch("/admin/analytics/export-grid.xlsx", {
                method: "POST",
                headers: { "Content-Type": "application/json" },
                body: JSON.stringify({
                    sheetName: sheetName || "Hesabat",
                    subtitle: subtitle || null,
                    headers: data.headers,
                    rows: data.rows
                })
            });
            if (!resp.ok) {
                alert("Excel-ə çıxartmaq mümkün olmadı.");
                return;
            }

            const blob = await resp.blob();
            const cd = resp.headers.get("Content-Disposition");
            let filename = "Hesabat.xlsx";
            if (cd) {
                const match = /filename[^;=\n]*=((['"]).*?\2|[^;\n]*)/.exec(cd);
                if (match && match[1]) {
                    filename = match[1].replace(/['"]/g, "");
                }
            }

            const url = URL.createObjectURL(blob);
            const a = document.createElement("a");
            a.href = url;
            a.download = filename;
            document.body.appendChild(a);
            a.click();
            a.remove();
            URL.revokeObjectURL(url);
        } catch (err) {
            alert("Excel-ə çıxartmaq mümkün olmadı.");
        }
    }

    global.HesabatTableFilters = {
        initAll: initAll,
        refreshById: refreshById,
        mountTable: mountTable,
        collectExportData: collectExportData,
        exportFiltered: exportFiltered
    };
})(window);
