$(document).ready(function () {
    const uid = $("#uid").val();
    const table = $("#table").text();
    let metadata = [];
    loadMetadata()
    function loadMetadata() {
        $.ajax({
            url: '/Api/Metadata/Get',
            type: 'GET',
            data: { table: uid },
            success: function (response) {
                if (response.error) {
                    alert(response.message || 'Failed to load metadata.');
                    return;
                }

                metadata = response.data;
                buildForm(metadata);
                loadGrid();

            },
            error: function () {
                alert('Server error while loading metadata.');
            }
        });
    }

    function buildForm(fields) {
        const $form = $('#dynamicForm');
        $form.empty();
        $form.append('<input type="hidden" id="UID" name="UID"/>');

        let currentSection = null;

        fields.forEach(f => {
            if (!f.AllowInsert) return;

            // ---------- SECTION HEADER ----------
            if (f.SectionNumber != null && f.SectionNumber !== currentSection) {
                currentSection = f.SectionNumber;
                if (f.SectionName) {
                    $form.append(`<div class="col-md-12 mt-3"><h3>${f.SectionName}</h3><hr/></div>`);
                } else {
                    $form.append(`<div class="col-md-12 mt-3"></div>`);
                }
            }

            // ---------- INPUT HTML ----------
            let inputHtml = '';
            const required = f.IsRequired ? 'required' : '';
            const readonly = f.IsReadonly ? 'readonly' : '';
            const placeholder = f.Placeholder ?? '';
            // --- HANDLE BOOLEAN INPUTS ---
            if (f.InputType === 'boolean') {
                inputHtml = `
                        <div class="form-check">
                            <input class="form-check-input"
                                   type="checkbox"
                                   name="${f.ColumnName}"
                                   id="${f.ColumnName}"
                                   value="true"
                                   ${required}
                                   ${readonly}>
                            <label class="form-check-label" for="${f.ColumnName}">
                                ${f.Label || 'Yes'}
                            </label>
                        </div>
                    `;
            }
            else {
                switch (f.InputType) {
                    case 'text':
                    case 'number':
                    case 'email':
                    case 'url':
                    case 'tel':
                    case 'password':
                    case 'color':
                    case 'range':
                    case 'month':
                    case 'week':
                    case 'search':
                    case 'time':
                    case 'datetime-local':
                        inputHtml = `<input type="${f.InputType}" class="form-control" name="${f.ColumnName}" placeholder="${placeholder}" ${required} ${readonly} />`;
                        break;

                    case 'date':
                        inputHtml = `<input type="date" class="form-control" name="${f.ColumnName}" ${required} ${readonly} />`;
                        break;

                    case 'textarea':
                        inputHtml = `<textarea class="form-control" name="${f.ColumnName}" placeholder="${placeholder}" ${required} ${readonly}></textarea>`;
                        break;

                    case 'checkbox':
                        inputHtml = `<div class="form-check">
                                        <input class="form-check-input" type="checkbox" name="${f.ColumnName}" value="1" ${readonly} />
                                        <label class="form-check-label">${f.DisplayLabel ?? f.ColumnName}</label>
                                        </div>`;
                        break;

                    case 'radio':
                        // placeholder container; will populate options later
                        inputHtml = `<div id="radioContainer_${f.ColumnName}"></div>`;
                        break;

                    case 'select':
                        inputHtml = `<select class="form-control" name="${f.ColumnName}" id="${f.ColumnName}" ${required} ${readonly}>
                                        <option value="">-- Select --</option>
                                        </select>`;
                        break;

                    case 'file':
                        inputHtml = `<input type="file" class="form-control" name="${f.ColumnName}" ${required} ${readonly} />`;
                        break;

                    default:
                        // fallback to text input
                        inputHtml = `<input type="text" class="form-control" name="${f.ColumnName}" placeholder="${placeholder}" ${required} ${readonly} />`;
                        break;
                }
            }


            // ---------- APPEND FIELD ----------
            if (f.InputType !== 'checkbox' && f.InputType !== 'radio') {
                // Label outside for non-checkbox/radio
                $form.append(`
                    <div class="col-md-4 mb-3">
                        <label class="form-label">${f.DisplayLabel ?? f.ColumnName} ${f.IsRequired ? '<span class="text-danger">*</span>' : ''}</label>
                        ${inputHtml}
                        ${f.Tooltip ? `<small class="text-muted">${f.Tooltip}</small>` : ''}
                    </div>
                `);
            } else {
                // Checkbox or radio container
                $form.append(`
                    <div class="col-md-4 mb-3">
                        <label class="form-label">${f.InputType === 'radio' ? f.DisplayLabel ?? f.ColumnName : ''} ${f.IsRequired ? '<span class="text-danger">*</span>' : ''}</label>
                        ${inputHtml}
                        ${f.Tooltip ? `<small class="text-muted">${f.Tooltip}</small>` : ''}
                    </div>
                `);
            }

            // ---------- LOAD DROPDOWN / RADIO DATA ----------
            if ((f.InputType === 'select' || f.InputType === 'radio') && f.DropdownSourceTable) {
                $.get('/Api/Lookup/Get', {
                    table: f.DropdownSourceTable,
                    valueCol: f.DropdownValueColumn,
                    textCol: f.DropdownTextColumn,
                    where: f.DropdownWhere,
                    order: f.DropdownOrderBy
                }, function (rows) {
                    if (f.InputType === 'select') {
                        rows.forEach(r => {
                            $('#' + f.ColumnName).append(`<option value="${r.value}">${r.text}</option>`);
                        });
                    } else if (f.InputType === 'radio') {
                        const $container = $(`#radioContainer_${f.ColumnName}`);
                        rows.forEach(r => {
                            const radioHtml = `
                                <div class="form-check">
                                    <input class="form-check-input" type="radio" name="${f.ColumnName}" value="${r.value}" id="${f.ColumnName}_${r.value}" />
                                    <label class="form-check-label" for="${f.ColumnName}_${r.value}">${r.text}</label>
                                </div>
                            `;
                            $container.append(radioHtml);
                        });
                    }
                });
            }
        });
    }

    window.save = function () {

        var id = $('#UID').val();
        var data = {};
        const isUpdate = id && id != '';


        $('#dynamicForm').find('input, select, textarea').each(function () {
            if (this.type === 'checkbox') {
                data[this.name] = this.checked ? 1 : 0;

            }
            else if (this.type === 'radio') {
                // Only set value if this radio button is checked
                if (this.checked) {
                    data[this.name] = this.value;
                }
            } else if ($(this).val() != "") {
                data[this.name] = $(this).val();
            }
        });

        const url = isUpdate ? '/Crud/Update' : '/Crud/Insert';
        $.ajax({
            url: url,
            method: 'POST',
            data: { table: uid, ...data },
            success: function (res) {
                if (res.error) {
                    alert(res.errors?.join('\n') || 'Error');
                }
                else {
                    alert(isUpdate ? 'Updated successfully' : 'Saved successfully');
                    resetForm();
                    loadGrid();
                }
            }
        });
    }


    window.resetForm = function () {
        $('#dynamicForm')[0].reset();
        $('#UID').val('');
    }

    function loadGrid() {
        $.get('/Crud/List', { table: uid }, function (rows) {
            const $grid = $('#grid');

            // Destroy DataTable first if it exists
            if ($.fn.DataTable.isDataTable($grid)) {
                $grid.DataTable().destroy();
            }

            // Clear previous content
            $grid.find('thead').empty();
            $grid.find('tbody').empty();

            if (!rows || rows.length === 0) {
                $grid.find('thead').html('<tr><th>No data</th></tr>');
                return;
            }

            const convertedRows = rows; // already an array of objects


            // Build header
            let headerHtml = '<tr>';
            metadata.filter(f => f.ShowInList).forEach(f => {
                headerHtml += `<th>${f.DisplayName ?? f.ColumnName}</th>`;
            });
            headerHtml += '<th>Actions</th></tr>';
            $grid.find('thead').html(headerHtml);

            // Build body
            let bodyHtml = '';
            convertedRows.forEach(r => {
                bodyHtml += '<tr>';
                metadata.filter(f => f.ShowInList).forEach(f => {
                    let val = r[f.ColumnName] ?? '';
                    // Parse .NET date format
                    if (typeof val === 'string' && val.startsWith('/Date(')) {
                        val = formatDotNetDate(val);
                    } else if (typeof val === 'object') {
                        val = JSON.stringify(val);
                    }
                    bodyHtml += `<td>${val}</td>`;
                });

                bodyHtml += `<td>`;
                bodyHtml += `<button class="btn btn-sm btn-info edit-btn" data-id="${r.UID}" title="Edit">
                                    <i class="bi bi-pencil"></i>
                                </button>`;
                bodyHtml += `
                            <button class="btn btn-sm btn-danger delete-btn" data-id="${r.UID}" title="Delete">
                                <i class="bi bi-trash"></i>
                            </button>`;

                bodyHtml += '</td>';
                bodyHtml += '</tr>';
            });
            $grid.find('tbody').html(bodyHtml);
            var button = [
                { extend: 'excelHtml5', title: table },
                { extend: 'csvHtml5', title: table },
                { extend: 'pdfHtml5', title: table },
                {
                    text: 'JSON',
                    action: function (e, dt, node, config) {
                        const data = dt.rows({ search: 'applied' }).data().toArray();
                        const jsonStr = JSON.stringify(data, null, 2);
                        const blob = new Blob([jsonStr], { type: "application/json" });
                        const url = URL.createObjectURL(blob);
                        const a = document.createElement('a');
                        a.href = url;
                        a.download = 'data.json';
                        a.click();
                        URL.revokeObjectURL(url);
                    }
                }
            ];
            $grid.DataTable({
                dom: button ? 'Blfrtip' : 'flrtip',
                buttons: button || [],
                order: [[0, "asc"]],
                pageLength: 20,
                lengthMenu: [5, 10, 20, 50, 100]
            });
        });
    }



    // Event delegation for dynamically generated buttons
    $('#grid tbody').on('click', '.edit-btn', function () {
        editRecord($(this).data('id'));
    });

    $('#grid tbody').on('click', '.delete-btn', function () {
        deleteRecord($(this).data('id'));
    });


    function setDateInput(name, val) {
        if (!val) {
            $(`[name='${name}']`).val('');
            return;
        }

        let dateStr = '';

        // Handle /Date(1532400000000)/ format
        if (val.startsWith('/Date(')) {
            const timestamp = parseInt(val.replace(/\/Date\((\d+)\)\//, '$1'));
            const dateObj = new Date(timestamp);
            const yyyy = dateObj.getFullYear();
            const mm = String(dateObj.getMonth() + 1).padStart(2, '0');
            const dd = String(dateObj.getDate()).padStart(2, '0');
            dateStr = `${yyyy}-${mm}-${dd}`;
        }
        // Handle yyyy-MM-dd or ISO string
        else {
            const dateObj = new Date(val);
            if (isNaN(dateObj)) {
                dateStr = '';
            } else {
                const yyyy = dateObj.getFullYear();
                const mm = String(dateObj.getMonth() + 1).padStart(2, '0');
                const dd = String(dateObj.getDate()).padStart(2, '0');
                dateStr = `${yyyy}-${mm}-${dd}`;
            }
        }

        $(`[name='${name}']`).val(dateStr);
    }


    function parseDotNetDate(dotNetDateStr) {
        // Example input: "/Date(1532458800000)/"
        const match = /\/Date\((\d+)\)\//.exec(dotNetDateStr);
        if (match) {
            const timestamp = parseInt(match[1], 10);
            return new Date(timestamp);
        }
        return null;
    }

    function formatDotNetDate(dotNetDateStr) {
        // Example input: "/Date(1532458800000)/"
        const timestamp = parseInt(dotNetDateStr.replace(/\/Date\((\d+)\)\//, '$1'), 10);
        const date = new Date(timestamp);

        const mm = String(date.getMonth() + 1).padStart(2, '0');
        const dd = String(date.getDate()).padStart(2, '0');
        const yyyy = date.getFullYear();

        return `${mm}/${dd}/${yyyy}`;
    }


    // ----------------- EDIT RECORD -----------------
    function editRecord(id) {
        $.get('/Crud/Get', { table: uid, UID: id }, function (recordArr) {
            const record = {};

            Object.keys(recordArr).forEach(key => {
                record[key] = recordArr[key];
            })
            $('#UID').val(record.UID);

            metadata.forEach(f => {
                let val = record[f.ColumnName];
                let $el = $(`[name='${f.ColumnName}']`);


                if (!$el.length) return; // safety check

                if (f.InputType === 'checkbox') {
                    $el.prop('checked', val == 1 || val === true);
                }
                else if (f.InputType === 'boolean') {
                    // Treat boolean as single checkbox:
                    // checked = true, unchecked = false
                    $el.prop('checked',
                        val === true ||
                        val === 1 ||
                        val === '1' ||
                        val === 'true' ||
                        val === 'True'
                    );
                }
                else if (f.InputType === 'date') {
                    setDateInput(f.ColumnName, val);
                }
                else if (f.InputType === 'select') {
                    if (typeof val === 'boolean') {
                        val = val ? 'True' : 'False';
                    }
                    $el.val(val).trigger('change');
                }
                else {
                    $el.val(val);
                }
            });

        });
    }



    // ----------------- DELETE -----------------
    function deleteRecord(id) {
        if (!confirm('Are you sure?')) return;
        $.post('/Crud/Delete', { table: uid, uid: id }, function (res) {
            if (res.error) alert(res.message || 'Error');
            else {
                alert('Deleted successfully');
                loadGrid();
            }
        });
    }

    window.uploadFile = function () {

        const fileInput = document.getElementById('importFile');
        if (fileInput.files.length === 0) {
            alert("Please select a file.");
            return;
        }

        const formData = new FormData();
        formData.append("file", fileInput.files[0]);
        formData.append("table", $("#uid").val());

        $.ajax({
            url: '/Crud/Import',
            type: 'POST',
            data: formData,
            processData: false,
            contentType: false,
            success: function (res) {
                if (res.error) {
                    alert(res.message || "Import failed");
                } else {
                    alert("Import successful");
                    loadGrid();
                }
            },
            error: function () {
                alert("Upload error");
            }
        });
    }

});