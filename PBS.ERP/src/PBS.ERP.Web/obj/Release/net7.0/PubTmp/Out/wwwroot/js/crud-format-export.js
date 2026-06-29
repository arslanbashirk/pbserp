(function (window, $) {
    "use strict";

    if (!$) {
        throw new Error("PbsFormatExport requires jQuery.");
    }

    if (!window.PbsErp) {
        throw new Error("PbsFormatExport requires window.PbsErp.");
    }

    window.PbsFormatExport = window.PbsFormatExport || {};

    function init(options) {

        const cfg = $.extend(true, {
            formatTableSelector: "#FormatTable",
            formatColumnsSelector: "#FormatColumns",
            formatFilterSelector: "#FormatFilter",
            forColumnsSelector: "#ForColumns",

            preFilledData: window.PreFilledData || {},

            downloadButtonSelector: "#btnDownloadFormat",
            hiddenTableSelector: "#hiddenTable",
            fileNameSelector: "#table-title",

            autoBindDownload: true
        }, options || {});

        const state = {
            formatData: [],
            formatColumns: []
        };

        function loadFormat() {

            const table = $(cfg.formatTableSelector).val();
            const columns = $(cfg.formatColumnsSelector).val();
            let filter = $(cfg.formatFilterSelector).val() || "";

            if (!table) {
                $(cfg.downloadButtonSelector).hide();
                state.formatData = [];
                state.formatColumns = [];
                return;
            }

            filter = applyFilterReplacements(
                filter,
                cfg.preFilledData || window.PreFilledData || {}
            );

            console.log("Format table:", table);
            console.log("Format columns:", columns);
            console.log("Final format filter:", filter);
            console.log("PreFilledData:", cfg.preFilledData || window.PreFilledData || {});

            window.PbsErp.call("crud.list", {
                table: table,
                filter: filter || ""
            }, {
                success: function (res) {

                    if (!res.Success) {
                        $(cfg.downloadButtonSelector).hide();
                        state.formatData = [];
                        state.formatColumns = [];
                        alert(res.Message || "Failed to load format data.");
                        return;
                    }

                    const rows = res.Data || [];

                    state.formatData = rows;
                    state.formatColumns = parseColumns(columns);

                    if (rows.length > 0) {
                        $(cfg.downloadButtonSelector).show();
                    } else {
                        $(cfg.downloadButtonSelector).hide();
                    }
                },

                error: function (err) {
                    $(cfg.downloadButtonSelector).hide();

                    state.formatData = [];
                    state.formatColumns = [];

                    let msg = err.Message || "Failed to load format data.";

                    if (err.Errors) {
                        msg += "\nErrors:\n" + JSON.stringify(err.Errors);
                    }

                    alert(msg);
                    console.error("Format list error:", err);
                }
            });
        }

        function downloadExcel() {

            if (!state.formatData || state.formatData.length === 0) {
                alert("No data available.");
                return;
            }

            const baseColumns = state.formatColumns;
            const extraColumns = parseColumns($(cfg.forColumnsSelector).val());

            const allColumns = mergeColumns(baseColumns, extraColumns);

            const filteredData = state.formatData.map(function (row) {

                const obj = {};

                baseColumns.forEach(function (column) {
                    obj[column] = row[column] ?? "";
                });

                extraColumns.forEach(function (column) {
                    if (!Object.prototype.hasOwnProperty.call(obj, column)) {
                        obj[column] = "";
                    }
                });

                return obj;
            });

            buildAndDownloadExcel(filteredData, allColumns);
        }

        function buildAndDownloadExcel(data, columns) {

            const $hiddenTable = $(cfg.hiddenTableSelector);

            if (!$hiddenTable.length) {
                $("body").append(
                    '<table id="' + cfg.hiddenTableSelector.replace("#", "") + '" style="display:none;"></table>'
                );
            }

            const $table = $(cfg.hiddenTableSelector);

            if ($.fn.DataTable.isDataTable($table)) {
                $table.DataTable().clear().destroy();
                $table.empty();
            }

            const dtColumns = columns.map(function (column) {
                return {
                    title: column,
                    data: column
                };
            });

            const filename =
                $(cfg.fileNameSelector).text().trim()
                || $(cfg.fileNameSelector).html()
                || "Export";

            const dt = $table.DataTable({
                destroy: true,
                data: data,
                columns: dtColumns,
                paging: false,
                searching: false,
                info: false,
                ordering: false,
                dom: "B",
                buttons: [
                    {
                        extend: "excelHtml5",
                        title: null,
                        filename: filename,
                        header: true
                    }
                ]
            });

            dt.button(".buttons-excel").trigger();
        }

        function bindEvents() {
            if (!cfg.autoBindDownload) {
                return;
            }

            $(document)
                .off("click.pbsFormatExport", cfg.downloadButtonSelector)
                .on("click.pbsFormatExport", cfg.downloadButtonSelector, function () {
                    downloadExcel();
                });
        }

        bindEvents();

        return {
            state: state,
            loadFormat: loadFormat,
            downloadExcel: downloadExcel,
            buildAndDownloadExcel: buildAndDownloadExcel
        };
    }

    function applyFilterReplacements(filter, preFilledData) {

        if (!filter) {
            return "";
        }

        preFilledData = preFilledData || {};

        const blockedKeys = [
            "__RequestVerificationToken",
            "UID",
            "ID",
            "Id"
        ];

        Object.keys(preFilledData).forEach(function (key) {

            if (!key || blockedKeys.includes(key)) {
                return;
            }

            const value = preFilledData[key];

            if (value === null || value === undefined || value === "") {
                return;
            }

            const safeValue = String(value).replace(/'/g, "''");
            const quotedValue = "'" + safeValue + "'";

            // Replace @@CityMarket
            filter = filter.replaceAll("@@" + key, quotedValue);

            // Replace @CityMarket
            // Important: use regex so @CityMarketId does not accidentally match @CityMarket
            const singleAtRegex = new RegExp("@" + escapeRegex(key) + "\\b", "g");
            filter = filter.replace(singleAtRegex, quotedValue);
        });

        return filter;
    }

    function escapeRegex(value) {
        return String(value).replace(/[.*+?^${}()|[\]\\]/g, "\\$&");
    }
    function parseColumns(value) {

        return (value || "")
            .split(",")
            .map(function (x) {
                return x.trim();
            })
            .filter(function (x) {
                return x !== "";
            });
    }

    function mergeColumns(baseColumns, extraColumns) {

        const result = [];

        baseColumns.forEach(function (column) {
            if (!result.includes(column)) {
                result.push(column);
            }
        });

        extraColumns.forEach(function (column) {
            if (!result.includes(column)) {
                result.push(column);
            }
        });

        return result;
    }

    window.PbsFormatExport.init = init;

})(window, window.jQuery);