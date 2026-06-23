

$(document).ready(function () {
    $('.mb-2').filter(function () {
        return $(this).find('.ComputedExpression, .MinLength, .DecimalPlaces, .DropdownSourceTable, .DropdownValueColumn, .DropdownTextColumn, .DropdownWhere, .DropdownOrder').length > 0;
    }).hide();

    const saved = localStorage.getItem("ttype_last_selected");

    if (saved && $("#ttype option[value='" + saved + "']").length > 0) {
        $("#ttype").val(saved).trigger("change");
    }
});

$(document).on("change", "#ttype", function () {
    const value = $(this).val();
    localStorage.setItem("ttype_last_selected", value);
});


$(document).on('focusout', '.DisplayLabel', function () {
    var label = $(this).val();
    var cname = $(this).closest('.row').find('.ColumnName');
    label = (label || '').trim().toLowerCase();

    if (label.length < 20) {
        var formatted = label
            .replace(/[^a-zA-Z0-9\s]/g, '')
            .replace(/\b(enter|of|the|please|specify|select)\b/gi, '')
            .trim()
            .replace(/\s+(.)/g, (_, g1) => g1.toUpperCase())
            .replace(/^(.)/, (_, g1) => g1.toUpperCase());

        if (formatted && cname.val()=="") {
            cname.val(formatted);
        }
    }

    const $select = $(this).closest('.row').find('.InputType');
    if ($select.length) {
        const inputType = InputTypeFromLabel(label);
        if (inputType) {
            $select.val(inputType).trigger('change');
        }
    }



});

$(document).on('change', '.InputType', function () {
    var val = $(this).val();
    if (val === "select" || val === "checkbox" || val === "radio" || val==="grid") {
        var target = $(this).closest('.row').find('.IsForeignKey');
        target.val('true').trigger('change');
    }
    else if (val === "boolean") {
        var target = $(this).closest('.row').find('.SqlType');
        target.val('bit').trigger('change');
    }
    /*
    else if (val === "number") {
        var target = $(this).closest('.row').find('.SqlType');
        target.val('int').trigger('change');
    }
    else if (val === "text") {
        var target = $(this).closest('.row').find('.SqlType');
        target.val('varchar').trigger('change');
    }
    */
});

$(document).on("change", ".IsComputed", function () {
    var me = $(this).val();
    const row = $(this).closest(".row");

    const a = row.find(".ComputedExpression");

    if (me === "1" || me === "true") {
        showThese([a]);
    }
    else {
        hideThese([a]);
    }
});



$(document).on("change", ".SqlType", function () {
    var me = $(this).val();
    const row = $(this).closest(".row");

    const min = row.find(".MinLength");
    const max = row.find(".MaxLength");
    const dec = row.find(".DecimalPlaces");

    hideThese([min, max, dec]);
    if (me.includes("text") || me.includes("char")) {
        showThese([min, max]);
    }
     if (me.includes("float") || me.includes("double")) {
        showThese([dec]);
    }
     if (me.includes("decimal")) {
        showThese([max, dec]);
    }
     if (me === "int identity") {
        $(this).closest('.row').find('.IsReadonly').val('true');
        $(this).closest('.row').find('.AllowInsert').val('false');
        $(this).closest('.row').find('.AllowUpdate').val('false');
        $(this).closest('.row').find('.ShowInList').val('false');
    }
});

$(document).on("change", ".IsForeignKey", function () {
    var me = $(this).val();
    var normalized = (me || "").toString().trim().toLowerCase();

    const row = $(this).closest(".row");

    const tbl = row.find(".DropdownSourceTable");
    const val = row.find(".DropdownValueColumn");
    const txt = row.find(".DropdownTextColumn");
    const where = row.find(".DropdownWhere");
    const order = row.find(".DropdownOrderBy");


    if (normalized === "0" || normalized === "false") {
        hideThese([tbl, val, txt, where, order]);

        row.find(".DropdownSourceTable, .DropdownValueColumn, .DropdownTextColumn").empty();
        row.find(".DropdownWhere, .DropdownOrderBy").val("");

        return;
    }
    else {
        showThese([tbl, val, txt, where, order]);
    }

    if (tbl.html() == "") {
        tbl.html(generateOptions(inputConfig.FieldMetadata.DropdownSourceTable.options, ""));
    }


});

$(document).on("change", ".DropdownSourceTable", function () {

    const tableName = $(this).val();
    if (!tableName) return;

    // Scope to the current column card / row
    const row = $(this).closest(".column-row");

    const val = row.find(".DropdownValueColumn");
    const txt = row.find(".DropdownTextColumn");

    val.empty().append('<option value="">-- Select Value Column --</option>');
    txt.empty().append('<option value="">-- Select Text Column --</option>');

    $.ajax({
        url: "/api/metadata/columns",
        type: "GET",
        data: { table: tableName },
        success: function (response) {
            if (response.Success) {
                val.append(generateOptions(response.Data, val.attr("data-id")));
                txt.append(generateOptions(response.Data, txt.attr("data-id")));
                showThese([val, txt]);
            }
            else {
                alert(response.Message);
            }

        },
        error: function () {
            alert("Failed to load table columns");
        }
    });

});

$(document).on('focusout', '.SectionNumber', function () {

    var currentRow = $(this).closest('.column-row');
    var sectionNumber = parseInt($(this).val());

    if (isNaN(sectionNumber)) return;

    var highestSort = 0;
    var matchedSectionName = null;

    $('.column-row').each(function () {

        var row = $(this);

        // Skip current row
        if (row.is(currentRow)) return;

        var otherSectionNumber = parseInt(
            row.find('.SectionNumber').val()
        );

        if (otherSectionNumber === sectionNumber) {

            // Get SectionName from matched row
            matchedSectionName = row.find('.SectionName').val();

            var sortOrder = parseInt(
                row.find('.SortOrder').val()
            );

            if (!isNaN(sortOrder) && sortOrder > highestSort) {
                highestSort = sortOrder;
            }
        }
    });

    if (matchedSectionName !== null) {

        // Set SectionName
        currentRow.find('.SectionName').val(matchedSectionName);

        // Set SortOrder = highest + 1
        currentRow.find('.SortOrder').val(highestSort + 1);

    } else {

        // If no matching section exists
        currentRow.find('.SortOrder').val(1);
    }

});


function showThese(arr) {
    arr.forEach(function (item, index) {
        item.closest(".mb-2").show()
    });
}

function hideThese(arr) {
    arr.forEach(function (item, index) {
        item.closest(".mb-2").hide()
    });
}

function InputTypeFromLabel(label) {
    const lowerLabel = label.toLowerCase();
    for (const [type, keywords] of Object.entries(inputTypeMap)) {
        if (keywords.some(kw => lowerLabel.includes(kw))) {
            return type;
        }
    }
    return null; // fallback if nothing matches
}

function generateOptions(options, selectedValue) {

    options = Array.isArray(options) ? options : [];

    selectedValue = String(selectedValue ?? "").trim();

    return options.map(o => {
        const value = typeof o === "object" ? String(o.value).trim() : String(o).trim();
        const text = typeof o === "object" ? o.text : o;
        const selected = value === selectedValue ? "selected" : "";
        return `<option value="${value}" ${selected}>${text}</option>`;
    }).join('');
}

$("#add-column").click(function () {
    renderColumnRow2();
});

// Remove column
$(document).on("click", ".remove-column", function () {
    $(this).closest(".column-row").remove();
});

function applyDefaultSectionAndSort(data) {
    data = data || {};

    const hasSection =
        data.SectionNumber !== undefined &&
        data.SectionNumber !== null &&
        String(data.SectionNumber).trim() !== "";

    const hasSort =
        data.SortOrder !== undefined &&
        data.SortOrder !== null &&
        String(data.SortOrder).trim() !== "";

    if (hasSection && hasSort) {
        return data;
    }

    let highestSectionNumber = 1;
    let highestSortOrder = 0;

    $(".column-row").each(function () {

        const sectionVal = $(this).find(".SectionNumber").val();
        const sortVal = $(this).find(".SortOrder").val();

        const sectionNo = parseInt(sectionVal, 10);
        const sortOrder = parseInt(sortVal, 10);

        if (!isNaN(sectionNo) && sectionNo > 0 && sectionNo < 10) {

            if (sectionNo > highestSectionNumber) {
                highestSectionNumber = sectionNo;
            }

            if (!isNaN(sortOrder) && sortOrder > highestSortOrder) {
                highestSortOrder = sortOrder;
            }
        }
    });

    if (!hasSection) {
        data.SectionNumber = highestSectionNumber;
    }

    if (!hasSort) {
        data.SortOrder = highestSortOrder + 1;
    }

    return data;
}

function renderColumnRow2(data = {}, column) {
    data = applyDefaultSectionAndSort(data);
    const idx = $(".column-row").length + 1;
    const grouped = groupByGroup(inputConfig.FieldMetadata);

    let html = `
    <div class="card mb-3 column-row metadata-row-card" data-idx="${idx}">
        <div class="metadata-row-header">
            <div>
                <div class="metadata-row-title">
                    <i class="bi bi-columns-gap me-1"></i>
                    Metadata Field #${idx}
                </div>
            </div>
        </div>

        <div class="metadata-row-body">
            <div class="row g-3">`;

    const fixedGroups = ["Layout", "Database", "Options"];
    const mergedFields = [];

    fixedGroups.forEach(groupName => {

        const fields = grouped[groupName] || [];

        html += `
        <div class="col-lg-3 col-md-6 col-sm-12">
            <div class="metadata-section-box">
                <div class="metadata-section-title">
                    ${getMetadataGroupIcon(groupName)}
                    <span>${groupName}</span>
                </div>`;

        fields.forEach(({ key, config }) => {

            let extraClass = key;

            if (key === "DropdownValueColumn") {

                const label = key.replace(/([a-z])([A-Z])/g, '$1 $2');

                html += `
                <div class="mb-2 metadata-full-field">
                    <label class="metadata-label">${label}</label>
                    <select class="form-control ${key} select2 metadata-control"
                            name="${key}_${idx}"
                            data-id="${data[key] ?? ""}">
                    </select>
                </div>`;
            }
            else {
                html += renderField(key, config, idx, data, extraClass);
            }
        });

        html += `
            </div>
        </div>`;
    });

    Object.entries(grouped).forEach(([groupName, fields]) => {
        if (!fixedGroups.includes(groupName)) {
            fields.forEach(f => mergedFields.push(f));
        }
    });

    html += `
        <div class="col-lg-3 col-md-6 col-sm-12">
            <div class="metadata-section-box metadata-section-box-merged">
                <div class="metadata-section-title">
                    ${getMetadataGroupIcon("Controls")}
                    <span>Flags & Validation</span>
                </div>`;

    mergedFields.forEach(({ key, config }) => {

        let extraClass = key;

        html += renderField(key, config, idx, data, extraClass);
    });

    html += `
            </div>
        </div>`;

    var cls = "remove-column";
    var edit = "";
    const pageName = window.location.pathname.split('/').pop().split('?')[0].toLowerCase();

    if (pageName.includes("alter")) {
        cls = "drop-column";
        if (data != null && data.ID > 0) {
            edit = "<button type='button' class='btn btn-success alter-column' id='" + data.ColumnName + "'><i class='bi bi-pencil-square'></i></button>";
        }
        else {
            edit = "<button type='button' class='btn btn-success alter-column'><i class='bi bi-pencil-square'></i></button>";
        }
    }

    html += `
            </div>

            <div class="metadata-row-actions mt-3">
                <button type="button" class="btn btn-danger ${cls}">
                    <i class="bi bi-x-circle"></i>
                </button> 
                ${edit}
            </div>
        </div>
    </div>`;

    const $row = $(html);

    if (column != null) {
        $row.addClass(column);
    }

    $("#columns-container").append($row);

    if ($row.find(".InputType").length) {
        $row.find(".InputType").trigger("change");
    }
    if ($row.find(".SqlType").length) {
        $row.find(".SqlType").trigger("change");
    }
    if ($row.find(".IsForeignKey").length) {
        $row.find(".IsForeignKey").trigger("change");
    }
    if ($row.find(".DropdownSourceTable").length) {
        $row.find(".DropdownSourceTable").trigger("change");
    }
    if ($row.find(".IsComputed").length) {
        $row.find(".IsComputed").trigger("change");
    }

    $row.find(".DropdownSourceTable,.DropdownValueColumn").select2({
        width: "100%",
        placeholder: "Search table...",
        allowClear: true,
        dropdownParent: $row
    });
    return $row.prop("outerHTML");
}

function renderField(key, config, idx, data, extraClass = "") {

    let label = key.replace(/([a-z])([A-Z])/g, '$1 $2');


    const name = `${key}_${idx}`;
    const value = data[key] ?? "";
    const placeholder = config.Placeholder ?? "";

    const halfFields = ["AllowInsert","AllowUpdate","ShowInList","IsComputed","MinValue", "MaxValue", "SectionNumber","SortOrder"];
    const switchFields = [];

    const fieldClass = halfFields.includes(key)
        ? "metadata-field metadata-half-field"
        : switchFields.includes(key)
            ? "metadata-field metadata-switch-field"
            : "metadata-field metadata-full-field";

    if (switchFields.includes(key)) {

        const isTrue = String(value).toLowerCase() === "true";
        const isFalse = String(value).toLowerCase() === "false" || value === "";

        return `
        <div class="mb-2 ${fieldClass}">
            <label class="metadata-label">${label}</label>

            <div class="metadata-binary-switch">
                <input type="radio"
                       id="${name}_true"
                       name="${name}"
                       value="true"
                       class="${extraClass}"
                       ${isTrue ? "checked" : ""}>

                <label for="${name}_true" class="switch-true">
                    True
                </label>

                <input type="radio"
                       id="${name}_false"
                       name="${name}"
                       value="false"
                       class="${extraClass}"
                       ${isFalse ? "checked" : ""}>

                <label for="${name}_false" class="switch-false">
                    False
                </label>
            </div>
        </div>`;
    }

    if (config.Type === "select") {
        return `
        <div class="mb-2 ${fieldClass}">
            <label class="metadata-label">${label}</label>
            <select class="form-select ${extraClass} metadata-control" name="${name}">
                ${generateOptions(config.options || [], value)}
            </select>
        </div>`;
    }

    return `
    <div class="mb-2 ${fieldClass}">
        <label class="metadata-label">${label}</label>
        <input type="${config.Type}"
               class="form-control ${extraClass} metadata-control"
               name="${name}"
               value="${value}"
               placeholder="${placeholder}">
    </div>`;
}

function getMetadataGroupIcon(groupName) {

    const icons = {
        Layout: `<i class="bi bi-layout-text-window"></i>`,
        Database: `<i class="bi bi-database"></i>`,
        Options: `<i class="bi bi-list-check"></i>`,
        Flags: `<i class="bi bi-toggles"></i>`,
        Filter: `<i class="bi bi-funnel"></i>`,
        Controls: `<i class="bi bi-sliders"></i>`
    };

    return icons[groupName] || `<i class="bi bi-grid-3x3-gap"></i>`;
}
function groupByGroup(fieldMetadata) {
    const groups = {
        Layout: [],
        Database: [],
        Options: [],
        "Flags & Validation": []
    };

    Object.entries(fieldMetadata).forEach(([key, config]) => {

        const groupName = config.Group;

        if (groupName === "Layout") {
            groups.Layout.push({ key, config });
        }
        else if (groupName === "Database") {
            groups.Database.push({ key, config });
        }
        else if (groupName === "Options") {
            groups.Options.push({ key, config });
        }
        else if(groupName === "Flags" || groupName==="Filter") {
            groups["Flags & Validation"].push({ key, config });
        }
    });

    return groups;
}

function include() {
    const container = $('#checkbox-container');

    INCLUDE.forEach(item => {
        

        const labelText = item.label || item.lable || "No Label";

        const checkboxDiv = $(`
            <div class="form-check">
                <input class="form-check-input" type="checkbox" name="${item.name}" id="${item.name}">
                <label class="form-check-label" for="${item.name}">
                    ${labelText}
                </label>
            </div>
        `);

        container.append(checkboxDiv);
    });

    applyAllIncludes();
}


$(document).on('change', '.form-check-input', function () {

    const includeMap = INCLUDE.reduce((map, item) => {
        map[item.name] = item;
        return map;
    }, {});

    const name = this.id; // identity, remarks, deleted
    const includeItem = includeMap[name];

    if (!includeItem) return;

    const columnClass = `${name}-column`;

    if (this.checked) {
        // REMOVE alsohide class (show)
        $(`#columns-container .${columnClass}`).removeClass('alsohide');
    } else {
        // ADD alsohide class (hide)
        $(`#columns-container .${columnClass}`).addClass('alsohide');
    }

});

function applyAllIncludes() {

    // Build include map once
    const includeMap = INCLUDE.reduce((map, item) => {
        map[item.name] = item;
        return map;
    }, {});

    // Loop through all INCLUDE items
    Object.keys(includeMap).forEach(name => {

        const includeItem = includeMap[name];
        const columnClass = `${name}-column alsohide`;

        // Remove existing columns to prevent duplicates
        $(`#columns-container .${columnClass}`).remove();


        // Render fields
        includeItem.fields.forEach(field => {
            renderColumnRow2(field, columnClass);
        });

    });
}



function getDuplicateColumnNames(columns) {
    const seen = new Set();
    const duplicates = new Set();

    columns.forEach(col => {
        if (seen.has(col.ColumnName)) {
            duplicates.add(col.ColumnName);
        } else {
            seen.add(col.ColumnName);
        }
    });

    // Return duplicates as array if found, otherwise null
    return duplicates.size > 0 ? Array.from(duplicates) : null;
}



