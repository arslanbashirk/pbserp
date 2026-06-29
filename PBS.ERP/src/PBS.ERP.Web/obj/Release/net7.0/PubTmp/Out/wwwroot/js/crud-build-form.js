(function (window, $) {
    "use strict";

    window.PbsCrudTemplates = window.PbsCrudTemplates || {};

    window.PbsCrudTemplates.buildForm = function (fields, state) {

        const preFilledData = state?.preFilledData || window.PreFilledData || {};
        const $form = $("#dynamicForm");

        $form.empty();
        $form.append('<input type="hidden" id="UID" name="UID" />');

        let currentSection = null;

        fields.forEach(function (f) {

            if (!f || !f.ColumnName) return;

            const inputType = (f.InputType || "text").toLowerCase();
            const columnName = f.ColumnName;

            const hasPreFilled =
                Object.prototype.hasOwnProperty.call(preFilledData, columnName) &&
                preFilledData[columnName] !== null &&
                preFilledData[columnName] !== undefined &&
                preFilledData[columnName] !== "";

            const preFilledValue = hasPreFilled
                ? String(preFilledData[columnName])
                : "";

            if (f.IsReadonly && !hasPreFilled) {
                return;
            }

            if (f.SectionNumber != null && f.SectionNumber !== currentSection) {
                currentSection = f.SectionNumber;

                if (f.SectionName) {
                    $form.append(`
                        <div class="col-md-12 mt-3">
                            <h3>${escapeHtml(f.SectionName)}</h3>
                            <hr />
                        </div>
                    `);
                }
            }

            const required = f.IsRequired ? "required" : "";
            const readonly = f.IsReadonly || hasPreFilled ? "readonly" : "";
            const disabled = f.IsReadonly || hasPreFilled ? "disabled" : "";

            const label = escapeHtml(f.DisplayLabel || f.ColumnName);
            const placeholder = escapeAttr(f.Placeholder || "");
            const tooltip = f.Tooltip
                ? `<small class="text-muted">${escapeHtml(f.Tooltip)}</small>`
                : "";

            const hiddenValue = hasPreFilled
                ? `<input type="hidden" name="${escapeAttr(columnName)}" value="${escapeAttr(preFilledValue)}" />`
                : "";

            let inputHtml = "";

            if (inputType === "grid") {
                f.InputType = "select";
            }

            switch (f.InputType) {

                case "boolean": {
                    const boolValue = preFilledValue.toLowerCase();

                    const trueChecked =
                        !hasPreFilled ||
                            boolValue === "true" ||
                            boolValue === "1" ||
                            boolValue === "yes"
                            ? "checked"
                            : "";

                    const falseChecked =
                        hasPreFilled &&
                            (
                                boolValue === "false" ||
                                boolValue === "0" ||
                                boolValue === "no"
                            )
                            ? "checked"
                            : "";

                    inputHtml = `
                        ${hiddenValue}

                        <div class="form-check form-check-inline">
                            <input class="form-check-input"
                                   type="radio"
                                   name="${escapeAttr(columnName)}"
                                   id="${escapeAttr(columnName)}_true"
                                   value="true"
                                   ${required}
                                   ${disabled}
                                   ${trueChecked} />

                            <label class="form-check-label" for="${escapeAttr(columnName)}_true">
                                ${escapeHtml(f.TrueLabel || "True")}
                            </label>
                        </div>

                        <div class="form-check form-check-inline ms-3">
                            <input class="form-check-input"
                                   type="radio"
                                   name="${escapeAttr(columnName)}"
                                   id="${escapeAttr(columnName)}_false"
                                   value="false"
                                   ${disabled}
                                   ${falseChecked} />

                            <label class="form-check-label" for="${escapeAttr(columnName)}_false">
                                ${escapeHtml(f.FalseLabel || "False")}
                            </label>
                        </div>
                    `;
                    break;
                }

                case "select": {
                    inputHtml = `
                        ${hiddenValue}

                        <select class="form-select"
                                name="${escapeAttr(columnName)}"
                                id="${escapeAttr(columnName)}"
                                ${required}
                                ${disabled}>
                            <option value="">-- Select --</option>
                        </select>
                    `;
                    break;
                }

                case "radio": {
                    inputHtml = `
                        ${hiddenValue}
                        <div id="radioContainer_${escapeAttr(columnName)}"></div>
                    `;
                    break;
                }

                case "checkbox": {
                    inputHtml = `
                        ${hiddenValue}
                        <div id="checkboxContainer_${escapeAttr(columnName)}"></div>
                    `;
                    break;
                }

                case "textarea": {
                    inputHtml = `
                        <textarea class="form-control"
                                  name="${escapeAttr(columnName)}"
                                  placeholder="${placeholder}"
                                  ${required}
                                  ${readonly}>${escapeHtml(preFilledValue)}</textarea>
                    `;
                    break;
                }

                case "date": {
                    inputHtml = `
                        <input type="date"
                               class="form-control"
                               name="${escapeAttr(columnName)}"
                               value="${escapeAttr(preFilledValue)}"
                               ${required}
                               ${readonly} />
                    `;
                    break;
                }

                case "file": {
                    inputHtml = `
                        <input type="file"
                               class="form-control"
                               name="${escapeAttr(columnName)}"
                               ${required} />
                    `;
                    break;
                }

                default: {
                    const htmlType = getHtmlInputType(f.InputType);

                    inputHtml = `
                        <input type="${escapeAttr(htmlType)}"
                               class="form-control"
                               name="${escapeAttr(columnName)}"
                               placeholder="${placeholder}"
                               value="${escapeAttr(preFilledValue)}"
                               ${required}
                               ${readonly} />
                    `;
                    break;
                }
            }

            $form.append(`
                <div class="col-md-4 mb-3">
                    <label class="form-label">
                        ${label}
                        ${f.IsRequired ? '<span class="text-danger">*</span>' : ''}
                    </label>

                    ${inputHtml}
                    ${tooltip}
                </div>
            `);

            loadLookupOptions(f, preFilledValue, hasPreFilled, disabled);
        });

        if ($.fn.select2) {
            $("#dynamicForm select").select2({
                width: "100%",
                placeholder: "Select an option",
                allowClear: true
            });
        }
    };

    function loadLookupOptions(f, preFilledValue, hasPreFilled, disabled) {

        const inputType = (f.InputType || "").toLowerCase();

        if (
            inputType !== "select" &&
            inputType !== "radio" &&
            inputType !== "checkbox"
        ) {
            return;
        }

        if (!f.DropdownSourceTable) {
            return;
        }

        window.PbsErp.call("lookup.get", {
            table: f.DropdownSourceTable,
            valueCol: f.DropdownValueColumn,
            textCol: f.DropdownTextColumn,
            where: f.DropdownWhere || "",
            order: f.DropdownOrderBy || f.DropdownOrder || ""
        }, {
            success: function (res) {

                if (!res.Success) {
                    return;
                }

                const rows = res.Data || [];

                if (inputType === "select") {
                    const $select = $("#" + f.ColumnName);

                    rows.forEach(function (r) {
                        const value = r.value ?? r.Value ?? "";
                        const text = r.text ?? r.Text ?? "";

                        $select.append(`
                            <option value="${escapeAttr(value)}">
                                ${escapeHtml(text)}
                            </option>
                        `);
                    });

                    if (hasPreFilled) {
                        $select.val(preFilledValue).trigger("change");
                    }

                    return;
                }

                if (inputType === "radio") {
                    const $container = $("#radioContainer_" + f.ColumnName);

                    rows.forEach(function (r) {
                        const value = String(r.value ?? r.Value ?? "");
                        const text = String(r.text ?? r.Text ?? "");
                        const checked = hasPreFilled && value === preFilledValue ? "checked" : "";
                        const id = `${f.ColumnName}_${value}`;

                        $container.append(`
                            <div class="form-check">
                                <input class="form-check-input"
                                       type="radio"
                                       name="${escapeAttr(f.ColumnName)}"
                                       value="${escapeAttr(value)}"
                                       id="${escapeAttr(id)}"
                                       ${checked}
                                       ${disabled} />

                                <label class="form-check-label" for="${escapeAttr(id)}">
                                    ${escapeHtml(text)}
                                </label>
                            </div>
                        `);
                    });

                    return;
                }

                if (inputType === "checkbox") {
                    const $container = $("#checkboxContainer_" + f.ColumnName);

                    const selectedValues = hasPreFilled
                        ? preFilledValue.split(",").map(x => x.trim())
                        : [];

                    rows.forEach(function (r) {
                        const value = String(r.value ?? r.Value ?? "");
                        const text = String(r.text ?? r.Text ?? "");
                        const checked = selectedValues.includes(value) ? "checked" : "";
                        const id = `${f.ColumnName}_${value}`;

                        $container.append(`
                            <div class="form-check">
                                <input class="form-check-input"
                                       type="checkbox"
                                       name="${escapeAttr(f.ColumnName)}"
                                       value="${escapeAttr(value)}"
                                       id="${escapeAttr(id)}"
                                       ${checked}
                                       ${disabled} />

                                <label class="form-check-label" for="${escapeAttr(id)}">
                                    ${escapeHtml(text)}
                                </label>
                            </div>
                        `);
                    });
                }
            },

            error: function (err) {
                console.error("Lookup failed:", err);
            }
        });
    }

    function getHtmlInputType(inputType) {
        const allowed = [
            "text",
            "number",
            "email",
            "url",
            "tel",
            "password",
            "color",
            "range",
            "month",
            "week",
            "search",
            "time",
            "datetime-local"
        ];

        return allowed.includes(inputType) ? inputType : "text";
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

})(window, window.jQuery);