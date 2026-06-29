(function (window, $) {
    "use strict";

    if (!$) {
        throw new Error("PbsCrudPage requires jQuery.");
    }

    if (!window.PbsErp) {
        throw new Error("PbsCrudPage requires pbs-erp-auth.js / window.PbsErp.");
    }

    window.PbsCrudPage = window.PbsCrudPage || {};

    function init(options) {

        const cfg = $.extend(true, {
            uidSelector: "#uid",
            pathSelector: "#path",
            tableSelector: "#table",
            descSelector:"#table-description",

            formSelector: "#dynamicForm",
            gridSelector: "#grid",
            fileInputSelector: "#importFile",
            uploadOverlaySelector: "#uploadOverlay",
            uploadProgressSelector: "#uploadProgressBar",

            dynamicFieldSelector: ".dynamic-field",
            includeDynamicFieldsInUpload: true,

            preFilledData: window.PreFilledData || {},

            autoLoadMetadata: true,
            exposeGlobals: true,

            buildForm: null,
            loadGrid: null,
            showColumns: null,

            gridMode: "showInList",
            gridOptions: {},

            afterMetadataLoaded: null,
            afterSave: null,
            afterDelete: null,
            afterUpload: null
        }, options || {});

        const state = {
            uid: $(cfg.uidSelector).val(),
            path: $(cfg.pathSelector).val(),
            table: $(cfg.tableSelector).text(),
            desc: $(cfg.descSelector).text(),
            metadata: [],
            preFilledData: cfg.preFilledData || {},
            config: cfg
        };

        function reloadGrid(overrideGridOptions) {

            const finalGridOptions = $.extend(
                true,
                {},
                cfg.gridOptions || {},
                overrideGridOptions || {}
            );

            // Page-specific custom grid function, if provided
            if (typeof cfg.loadGrid === "function") {
                cfg.loadGrid(state, finalGridOptions);
                return;
            }

            // Preferred reusable grid template
            if (
                window.PbsCrudTemplates &&
                typeof window.PbsCrudTemplates.loadGrid === "function"
            ) {
                window.PbsCrudTemplates.loadGrid($.extend(true, {
                    table: state.uid,
                    metadata: state.metadata,
                    preFilledData: state.preFilledData,
                    mode: cfg.gridMode || "showInList",
                    gridSelector: cfg.gridSelector || "#grid",
                    desc: state.desc
                }, finalGridOptions));

                return;
            }

            // Old fallback support
            if (typeof window.loadGrid === "function") {
                window.loadGrid(state.uid, state.metadata, state.preFilledData);
                return;
            }

            console.error("No grid renderer found. Provide cfg.loadGrid or PbsCrudTemplates.loadGrid.");
        }

        function loadMetadata() {
            window.PbsErp.call("metadata.get", {
                table: state.uid
            }, {
                success: function (res) {
                    if (!res.Success) {
                        alert(res.Message || "Failed to load metadata.");
                        return;
                    }

                    state.metadata = res.Data || [];

                    if (typeof cfg.buildForm === "function") {
                        cfg.buildForm(state.metadata, state);
                    }

                    reloadGrid();

                    if (typeof cfg.showColumns === "function") {
                        cfg.showColumns(state.metadata, state);
                    }
                    else if (typeof window.showColumns === "function") {
                        window.showColumns(state.metadata);
                    }

                    if (typeof cfg.afterMetadataLoaded === "function") {
                        cfg.afterMetadataLoaded(state);
                    }
                },

                error: function (err) {
                    alert(err.Message || "Server error while loading metadata.");
                    console.error("Metadata error:", err);
                }
            });
        }

        function save() {
            const id = $("#UID").val();
            const isUpdate = id && id !== "";

            const data = window.PbsErp.collectFormFields(cfg.formSelector);

            window.PbsErp.call("crud.save", {
                table: state.uid,
                id: isUpdate ? id : null,
                fields: data
            }, {
                success: function (res) {
                    if (res.Success) {
                        alert(isUpdate ? "Updated successfully" : "Saved successfully");
                        resetForm();
                        reloadGrid();

                        if (typeof cfg.afterSave === "function") {
                            cfg.afterSave(res, state);
                        }

                        return;
                    }

                    showError(res, "Save failed.");
                },

                error: function (err) {
                    showError(err, "Save failed.");
                }
            });
        }

        function resetForm() {

            const preFilledData = state.preFilledData || {};
            const preFilledKeys = Object.keys(preFilledData);

            // Clear UID because reset means insert/new mode
            $("#UID").val("");

            $(cfg.formSelector)
                .find("input, select, textarea")
                .each(function () {

                    const el = this;
                    const $el = $(el);
                    const name = el.name;

                    if (!name) {
                        return;
                    }

                    // Do not reset fields that are part of preFilledData
                    if (preFilledKeys.includes(name)) {
                        return;
                    }

                    // Do not touch anti-forgery token if present
                    if (name === "__RequestVerificationToken") {
                        return;
                    }

                    if (el.type === "checkbox" || el.type === "radio") {
                        el.checked = false;
                        return;
                    }

                    if (el.type === "file") {
                        $el.val("");
                        return;
                    }

                    if ($el.is("select")) {
                        $el.val("").trigger("change");
                        return;
                    }

                    $el.val("");
                });

        }

        function editRecord(id) {
            $("#dynamicForm").closest("#form-container").show();
            window.PbsErp.call("crud.record", {
                table: state.uid,
                uid: id
            }, {
                success: function (res) {
                    if (!res.Success) {
                        showError(res, "Failed to load record.");
                        return;
                    }

                    const record = res.Data || {};
                    fillForm(record);
                },

                error: function (err) {
                    showError(err, "Server error while loading record.");
                }
            });
        }

        function fillForm(record) {
            $("#UID").val(record.UID || record.uid || "");

            state.metadata.forEach(function (f) {
                let val = record[f.ColumnName];

                const $el = $(`[name='${f.ColumnName}']`);

                if (!$el.length) {
                    return;
                }

                if (f.InputType === "grid") {
                    f.InputType = "select";
                }

                if (f.InputType === "checkbox") {
                    $(`input[name='${f.ColumnName}']`).prop("checked", false);

                    if (val !== null && val !== undefined && val !== "") {
                        String(val)
                            .split(",")
                            .map(x => x.trim())
                            .filter(x => x !== "")
                            .forEach(function (v) {
                                $(`input[name='${f.ColumnName}'][value='${v}']`).prop("checked", true);
                            });
                    }
                }

                else if (f.InputType === "boolean") {
                    let boolValue = null;

                    if (val === true || val === "true" || val === "True" || val === 1 || val === "1") {
                        boolValue = "true";
                    }
                    else if (val === false || val === "false" || val === "False" || val === 0 || val === "0") {
                        boolValue = "false";
                    }

                    $(`input[name='${f.ColumnName}']`).prop("checked", false);

                    if (boolValue !== null) {
                        $(`input[name='${f.ColumnName}'][value='${boolValue}']`).prop("checked", true);
                    }
                }

                else if (f.InputType === "date") {
                    setDateInput(f.ColumnName, val);
                }

                else if (f.InputType === "select") {
                    if (typeof val === "boolean") {
                        val = val ? "True" : "False";
                    }

                    $el.val(val).trigger("change");
                }

                else {
                    $el.val(val);
                }
            });

            $(cfg.formSelector)
                .find("input,select,textarea")
                .filter(":visible:not([readonly]):not([disabled])")
                .first()
                .focus();
        }

        function deleteRecord(id) {
            if (!confirm("Are you sure, deleting this record?")) {
                return;
            }

            window.PbsErp.call("crud.delete", {
                table: state.uid,
                uid: id
            }, {
                success: function (res) {
                    if (!res.Success) {
                        showError(res, "Delete failed.");
                        return;
                    }

                    alert(res.Message || "Deleted successfully.");
                    reloadGrid();

                    if (typeof cfg.afterDelete === "function") {
                        cfg.afterDelete(res, state);
                    }
                },

                error: function (err) {
                    showError(err, "Server error while deleting record.");
                }
            });
        }

        function uploadFile() {
            const fileInput = document.querySelector(cfg.fileInputSelector);

            if (!fileInput || fileInput.files.length === 0) {
                alert("Please select a file.");
                return;
            }

            const tableUid = state.uid || $(cfg.uidSelector).val();

            if (!tableUid) {
                alert("Table UID is missing.");
                return;
            }

            const formData = new FormData();

            formData.append("file", fileInput.files[0]);

            if (cfg.includeDynamicFieldsInUpload === true) {
                appendPreFilledData(
                    formData,
                    cfg.preFilledData
                );
            }

            $(cfg.uploadOverlaySelector).css("display", "flex");

            $(cfg.uploadProgressSelector)
                .css("width", "0%")
                .text("0%");

            window.PbsErp.call("crud.upload", {
                table: tableUid,
                formData: formData
            }, {
                progress: function (percent) {
                    $(cfg.uploadProgressSelector)
                        .css("width", percent + "%")
                        .text(percent + "%");
                },

                success: function (res) {
                    $(cfg.uploadOverlaySelector).hide();

                    if (res.Success) {
                        $(cfg.fileInputSelector).val("");
                        alert(res.Message || "Uploaded successfully.");

                        reloadGrid();

                        if (typeof cfg.afterUpload === "function") {
                            cfg.afterUpload(res, state);
                        }

                        return;
                    }

                    alert(buildUploadMessage(res.Message || "Upload failed.", res.Errors));
                },

                error: function (err) {
                    $(cfg.uploadOverlaySelector).hide();

                    alert(buildUploadMessage(err.Message || "Upload error.", err.Errors));
                    console.error("Upload error:", err);
                }
            });
        }

        function bindEvents() {
            $(document)
                .off("click.pbsCrudEdit", cfg.gridSelector + " tbody .edit-btn")
                .on("click.pbsCrudEdit", cfg.gridSelector + " tbody .edit-btn", function () {
                    editRecord($(this).data("id"));
                });

            $(document)
                .off("click.pbsCrudDelete", cfg.gridSelector + " tbody .delete-btn")
                .on("click.pbsCrudDelete", cfg.gridSelector + " tbody .delete-btn", function () {
                    deleteRecord($(this).data("id"));
                });
        }

        function exposeGlobals() {
            window.save = save;
            window.resetForm = resetForm;
            window.editRecord = editRecord;
            window.deleteRecord = deleteRecord;
            window.uploadFile = uploadFile;
            window.reloadGrid = reloadGrid;
        }

        bindEvents();

        if (cfg.exposeGlobals) {
            exposeGlobals();
        }

        if (cfg.autoLoadMetadata) {
            loadMetadata();
        }

        return {
            state: state,
            loadMetadata: loadMetadata,
            reloadGrid: reloadGrid,
            save: save,
            resetForm: resetForm,
            editRecord: editRecord,
            deleteRecord: deleteRecord,
            uploadFile: uploadFile,
            fillForm: fillForm
        };
    }

    function showError(res, fallback) {
        let message = res.Message || fallback || "Request failed.";

        if (res.Errors) {
            message += "\nErrors:\n" + JSON.stringify(res.Errors);
        }

        alert(message);
        console.error(message, res);
    }

    function setDateInput(name, val) {
        if (!val) {
            $(`[name='${name}']`).val("");
            return;
        }

        let dateStr = "";

        if (typeof val === "string" && val.startsWith("/Date(")) {
            const timestamp = parseInt(val.replace(/\/Date\((\d+)\)\//, "$1"));
            const dateObj = new Date(timestamp);

            const yyyy = dateObj.getFullYear();
            const mm = String(dateObj.getMonth() + 1).padStart(2, "0");
            const dd = String(dateObj.getDate()).padStart(2, "0");

            dateStr = `${yyyy}-${mm}-${dd}`;
        }
        else {
            const dateObj = new Date(val);

            if (!isNaN(dateObj)) {
                const yyyy = dateObj.getFullYear();
                const mm = String(dateObj.getMonth() + 1).padStart(2, "0");
                const dd = String(dateObj.getDate()).padStart(2, "0");

                dateStr = `${yyyy}-${mm}-${dd}`;
            }
        }

        $(`[name='${name}']`).val(dateStr);
    }

    function appendPreFilledData(formData, data) {
        if (!data) return;

        const skipKeys = new Set([
            "__RequestVerificationToken",
            "UID",
            "ID",
            "Id"
        ]);

        function appendField(key, value) {
            if (!key || skipKeys.has(key)) {
                return;
            }

            // skip only empty values, but keep 0 and false
            if (value === null || value === undefined || value === "") {
                return;
            }

            // FormData values should be simple strings/numbers/bool
            if (typeof value === "object") {
                value = JSON.stringify(value);
            }

            formData.append(`fields[${key}]`, value);
        }

        // Case 1: normal object like window.PreFilledData = { HQ: 0, CityMarket: "..." }
        if (!Array.isArray(data) && typeof data === "object" && !(data instanceof jQuery)) {
            Object.entries(data).forEach(function ([key, value]) {
                appendField(key, value);
            });

            return;
        }

        /*
        $(data).each(function () {
            const key = this.name;
            const value = $(this).val();

            appendField(key, value);
        });
        */
    }
    function buildUploadMessage(message, errors) {

        let msg = message || "Upload failed.";

        if (!errors) {
            return msg;
        }

        msg += "\nErrors:\n";

        if (Array.isArray(errors)) {
            errors.forEach(function (e, i) {
                msg += `\nError ${i + 1}: `;

                if (typeof e === "string") {
                    msg += e;
                }
                else if (e.Errors) {
                    msg += e.Errors;
                }
                else if (e.Message) {
                    msg += e.Message;
                }
                else {
                    msg += JSON.stringify(e);
                }

                if (e.Row) {
                    msg += `\nRow: ${JSON.stringify(e.Row)}`;
                }

                msg += "\n";
            });

            return msg;
        }

        if (typeof errors === "object") {
            msg += JSON.stringify(errors, null, 2);
            return msg;
        }

        msg += String(errors);
        return msg;
    }

    window.PbsCrudPage.init = init;

})(window, window.jQuery);