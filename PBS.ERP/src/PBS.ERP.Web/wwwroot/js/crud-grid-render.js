(function (window, $) {
    "use strict";

    window.PbsCrudTemplates = window.PbsCrudTemplates || {};

    window.PbsCrudTemplates.loadGrid = function (options) {

        const cfg = $.extend(true, {
            table: "",
            desc: "",
            metadata: [],
            preFilledData: {},
            gridSelector: "#grid",

            /*
                mode:
                - "showInList"       => only ShowInList columns visible/exported
                - "limitedColumns"   => all columns loaded, first 5 AllowInsert visible, all exported
                - "all"              => all metadata columns visible/exported
            */
            mode: "showInList",

            maxVisibleColumns: 5,

            showActions: true,
            showEdit: true,
            showDelete: true,

            useAliasFilter: true,
            aliasName: "{alias}",

            exportEnabled: true,
            exportButtons: ["excel", "csv", "pdf", "json"],
            exportFileName: null,

            onEditClass: "edit-btn",
            onDeleteClass: "delete-btn",

            emptyMessage: "No data",
            errorMessage: "Failed to load grid."
        }, options || {});

        const filter = buildFilter(
            cfg.preFilledData,
            cfg.useAliasFilter,
            cfg.aliasName
        );

        window.PbsErp.call("crud.list", {
            table: cfg.table,
            filter: filter
        }, {
            success: function (res) {

                if (!res.Success) {
                    alert(res.Message || "Failed to load records.");
                    return;
                }

                renderGrid(cfg, res.Data || []);
            },

            error: function (err) {
                renderError(cfg, err.Message || cfg.errorMessage);
                console.error("Grid load error:", err);
            }
        });
    };


    function renderGrid(cfg, rows) {

        const $grid = $(cfg.gridSelector);

        destroyGrid($grid);

        $grid.find("thead").empty();
        $grid.find("tbody").empty();

        if (!rows || rows.length === 0) {
            $grid.find("thead").html(`<tr><th>${escapeHtml(cfg.emptyMessage)}</th></tr>`);
            return;
        }

        const allFields = getAllFields(cfg.metadata);
        let visibleFields = getVisibleFields(allFields, cfg);

        if (!allFields.length) {
            $grid.find("thead").html("<tr><th>No metadata columns found</th></tr>");
            return;
        }

        // Safe fallback:
        // if ShowInList is empty, show first few insert/list fields instead of rendering blank grid.
        if (!visibleFields.length && cfg.mode !== "limitedColumns") {
            visibleFields = allFields
                .filter(f => f.ShowInList || f.AllowInsert)
                .slice(0, cfg.maxVisibleColumns || 5);

            if (!visibleFields.length) {
                visibleFields = allFields.slice(0, cfg.maxVisibleColumns || 5);
            }
        }

        const visibleColumnNames = new Set(
            visibleFields.map(f => f.ColumnName)
        );

        const renderFields =
            cfg.mode === "limitedColumns"
                ? allFields
                : visibleFields;

        let headerHtml = "<tr>";

        renderFields.forEach(function (f) {
            headerHtml += `<th>${escapeHtml(getFieldLabel(f))}</th>`;
        });

        if (cfg.showActions) {
            headerHtml += `<th class="no-export" style="min-width:80px;">Actions</th>`;
        }

        headerHtml += "</tr>";

        $grid.find("thead").html(headerHtml);

        let bodyHtml = "";

        rows.forEach(function (row) {
            bodyHtml += "<tr>";

            renderFields.forEach(function (f) {
                const val = formatCellValue(row[f.ColumnName]);
                bodyHtml += `<td>${escapeHtml(val)}</td>`;
            });

            if (cfg.showActions) {
                bodyHtml += renderActions(row, cfg);
            }

            bodyHtml += "</tr>";
        });

        $grid.find("tbody").html(bodyHtml);

        const actionColumnIndex = renderFields.length;

        const hiddenColumnIndexes =
            cfg.mode === "limitedColumns"
                ? renderFields
                    .map((f, index) => ({
                        index: index,
                        visible: visibleColumnNames.has(f.ColumnName)
                    }))
                    .filter(x => !x.visible)
                    .map(x => x.index)
                : [];

        const exportOptions = {
            columns: function (idx) {
                if (!cfg.showActions) {
                    return true;
                }

                return idx !== actionColumnIndex;
            }
        };

        const buttons = cfg.exportEnabled === true
            ? buildExportButtons(cfg, renderFields, exportOptions)
            : [];

        const columnDefs = [];

        if (hiddenColumnIndexes.length > 0) {
            columnDefs.push({
                targets: hiddenColumnIndexes,
                visible: false,
                searchable: true
            });
        }

        if (cfg.showActions) {
            columnDefs.push({
                targets: actionColumnIndex,
                orderable: false,
                searchable: false
            });
        }

        const dataTableOptions = {
            dom: cfg.exportEnabled === true ? "Blfrtip" : "flrtip",
            buttons: buttons,
            pageLength: 20,
            lengthMenu: [5, 10, 20, 50, 100],
            autoWidth: false,
            columnDefs: columnDefs
        };

        if (renderFields.length > 0) {
            dataTableOptions.order = [[0, "asc"]];
        }

        $grid.DataTable(dataTableOptions);
    }

    function renderActions(row, cfg) {

        const uid = escapeAttr(row.UID ?? row.uid ?? "");

        let html = "<td>";

        if (cfg.showEdit) {
            html += `
                <button class="btn btn-sm btn-info ${escapeAttr(cfg.onEditClass)}"
                        data-id="${uid}"
                        title="Edit">
                    <i class="bi bi-pencil"></i>
                </button>
            `;
        }

        if (cfg.showDelete) {
            html += `
                <button class="btn btn-sm btn-danger ${escapeAttr(cfg.onDeleteClass)}"
                        data-id="${uid}"
                        title="Delete">
                    <i class="bi bi-trash"></i>
                </button>
            `;
        }

        html += "</td>";

        return html;
    }

    function buildExportButtons(cfg, renderFields, exportOptions) {

        const rawTitle =
            cfg.exportFileName ||
            cfg.desc ||
            cfg.table ||
            "data";

        const title = safeDownloadFileName(rawTitle);

        const enabledButtons = cfg.exportButtons || ["excel", "csv", "pdf", "json"];

        const buttons = [];

        if (enabledButtons.includes("excel")) {
            buttons.push({
                extend: "excelHtml5",
                title: rawTitle,
                filename: title,
                exportOptions: exportOptions
            });
        }

        if (enabledButtons.includes("csv")) {
            buttons.push({
                extend: "csvHtml5",
                title: rawTitle,
                filename: title,
                exportOptions: exportOptions
            });
        }

        if (enabledButtons.includes("pdf")) {
            buttons.push({
                extend: "pdfHtml5",
                title: rawTitle,
                filename: title,
                exportOptions: exportOptions
            });
        }

        if (enabledButtons.includes("json")) {
            buttons.push({
                text: "JSON",
                action: function (e, dt) {

                    const exportedData = dt
                        .rows({ search: "applied" })
                        .data()
                        .toArray()
                        .map(function (row) {
                            const obj = {};

                            renderFields.forEach(function (f, index) {
                                obj[f.ColumnName] = row[index];
                            });

                            return obj;
                        });

                    const jsonStr = JSON.stringify(exportedData, null, 2);
                    const blob = new Blob([jsonStr], { type: "application/json" });
                    const url = URL.createObjectURL(blob);

                    const a = document.createElement("a");
                    a.href = url;
                    a.download = `${title}.json`;
                    a.click();

                    URL.revokeObjectURL(url);
                }
            });
        }

        return buttons;
    }

    function safeDownloadFileName(value) {
        return String(value || "data")
            .trim()
            .replace(/[\\/:*?"<>|]/g, "-")
            .replace(/\s+/g, " ")
            .substring(0, 120);
    }
    function getAllFields(metadata) {
        return (metadata || [])
            .filter(f => f && f.ColumnName);
    }

    function getVisibleFields(allFields, cfg) {

        if (cfg.mode === "all") {
            return allFields;
        }

        if (cfg.mode === "limitedColumns") {
            return allFields
                .filter(f => f.AllowInsert)
                .slice(0, cfg.maxVisibleColumns);
        }


        return allFields.filter(f => f.ShowInList);
    }

    function getFieldLabel(f) {
        return f.ColumnName
            || f.DisplayLabel;
    }

    function buildFilter(preFilledData, useAliasFilter, aliasName) {

        if (!preFilledData || Object.keys(preFilledData).length === 0) {
            return "";
        }

        const blockedKeys = [
            "__RequestVerificationToken",
            "UID",
            "ID",
            "Id"
        ];

        return Object.entries(preFilledData)
            .filter(function ([key, value]) {

                if (!key || blockedKeys.includes(key)) {
                    return false;
                }

                if (value === null || value === undefined || value === "") {
                    return false;
                }

                // Only allow normal SQL column-style names
                // Example: SurveyPhase, AreaCode, CreatedBy
                if (!/^[A-Za-z_][A-Za-z0-9_]*$/.test(key)) {
                    console.warn("Skipped unsafe filter key:", key);
                    return false;
                }

                return true;
            })
            .map(function ([key, value]) {
                const safeValue = String(value).replace(/'/g, "''");

                if (useAliasFilter) {
                    return `${aliasName}.[${key}]='${safeValue}'`;
                }

                return `T1.[${key}]='${safeValue}'`;
            })
            .join(" AND ");
    }
    function destroyGrid($grid) {
        if ($.fn.DataTable && $.fn.DataTable.isDataTable($grid)) {
            $grid.DataTable().clear().destroy();
        }
    }

    function renderError(cfg, message) {

        const $grid = $(cfg.gridSelector);

        destroyGrid($grid);

        $grid.find("thead").html("<tr><th>Error</th></tr>");
        $grid.find("tbody").html(`
            <tr>
                <td>${escapeHtml(message)}</td>
            </tr>
        `);
    }

    function formatCellValue(val) {

        if (val === null || val === undefined) {
            return "";
        }

        if (typeof val === "string" && val.startsWith("/Date(")) {
            return formatDotNetDate(val);
        }

        if (typeof val === "object") {
            return JSON.stringify(val);
        }

        return val;
    }

    function formatDotNetDate(dotNetDateStr) {
        const match = /\/Date\((\d+)\)\//.exec(dotNetDateStr);

        if (!match) {
            return dotNetDateStr;
        }

        const timestamp = parseInt(match[1], 10);
        const date = new Date(timestamp);

        if (isNaN(date)) {
            return dotNetDateStr;
        }

        const mm = String(date.getMonth() + 1).padStart(2, "0");
        const dd = String(date.getDate()).padStart(2, "0");
        const yyyy = date.getFullYear();

        return `${mm}/${dd}/${yyyy}`;
    }

    function escapeHtml(value) {
        return String(value ?? "")
            .replace(/&/g, "&amp;")
            .replace(/</g, "&lt;")
            .replace(/>/g, "&gt;")
            .replace(/"/g, "&quot;")
            .replace(/'/g, "&#039;");
    }

    function escapeAttr(value) {
        return escapeHtml(value);
    }

    /*
        Backward-compatible wrappers.
        These allow your old calls to keep working.
    */

    window.loadGrid = function (table, metadata, preFilledData) {
        window.PbsCrudTemplates.loadGrid({
            table: table,
            metadata: metadata,
            preFilledData: preFilledData,
            mode: "showInList",
            gridSelector: "#grid"
        });
    };

    window.loadGridLimitedColumns = function (table, metadata, preFilledData) {
        window.PbsCrudTemplates.loadGrid({
            table: table,
            metadata: metadata,
            preFilledData: preFilledData,
            mode: "limitedColumns",
            gridSelector: "#grid"
        });
    };

})(window, window.jQuery);