$(document).ready(function () {
    renderVisualizationUI();
});

function renderVisualizationUI() {

    const html = `
        <div class="viz-card">

            <div class="viz-card-header" id="visualizationHeader">
                <div class="viz-title">
                    <span class="viz-title-icon">📊</span>
                    <div>
                        <div>Basic Visualization</div>
                        <div class="viz-subtitle">Create quick summaries and charts from selected table data</div>
                    </div>
                </div>

                <button type="button" class="viz-toggle-btn" id="toggleVisualizationPanel">
                    Show
                </button>
            </div>

            <div class="viz-card-body" id="visualizationBody">

                <div class="viz-filter-box">
                    <div class="row">

                        <div class="col-md-3 mb-3">
                            <label class="form-label">Aggregate Function <span class="text-danger">*</span></label>
                            <select class="form-select" id="funcList">
                                <option>COUNT</option>
                                <option>COUNT_DISTINCT</option>
                                <option>SUM</option>
                                <option>AVG</option>
                                <option>MIN</option>
                                <option>MAX</option>
                            </select>
                        </div>

                        <div class="col-md-3 mb-3">
                            <label class="form-label">Column <span class="text-danger">*</span></label>
                            <select class="form-select" id="colList">
                                <option value="*">-- Select Column --</option>
                            </select>
                        </div>

                        <div class="col-md-3 mb-3">
                            <label class="form-label">Group By Column <span class="text-danger">*</span></label>
                            <select class="form-select" id="grpList"></select>
                        </div>

                        <div class="col-md-3 mb-3">
                            <label class="form-label">Where Clause</label>
                            <input type="text" class="form-control" id="whereClause" placeholder="{alias}.IsActive = 1" />
                        </div>

                        <div class="col-md-3 mb-3">
                            <label class="form-label">Having Clause</label>
                            <input type="text" class="form-control" id="havingClause" placeholder="COUNT(*) > 0" />
                        </div>

                        <div class="col-md-3 mb-3">
                            <label class="form-label">Order By</label>
                            <input type="text" class="form-control" id="orderBy" placeholder="Value DESC" />
                        </div>

                        <div class="col-md-3 mb-3">
                            <label class="form-label">&nbsp;</label>
                            <div class="viz-action-bar">
                                <button type="button" class="btn btn-primary" id="displayInfoGraphic">
                                    Display Chart
                                </button>
                            </div>
                        </div>

                    </div>
                </div>

                <div class="viz-result-area">

                    <div class="viz-chart-panel">
                        <div id="chartContainer">
                            <div class="viz-empty-state">
                                Select options and click Display Chart.
                            </div>
                        </div>
                    </div>

                    <div class="viz-table-panel">
                        <table id="summaryTable">
                            <thead>
                                <tr id="tableHead"></tr>
                            </thead>
                            <tbody id="tableBody">
                                <tr>
                                    <td class="viz-empty-state">No summary data yet.</td>
                                </tr>
                            </tbody>
                        </table>
                    </div>

                </div>

            </div>
        </div>
    `;

    $("#visualizationRoot").html(html);

    bindVisualizationEvents();
}

function bindVisualizationEvents() {

    $(document).off("click", "#visualizationHeader").on("click", "#visualizationHeader", function (e) {
        if ($(e.target).closest("#toggleVisualizationPanel").length) {
            return;
        }

        toggleVisualizationPanel();
    });

    $(document).off("click", "#toggleVisualizationPanel").on("click", "#toggleVisualizationPanel", function (e) {
        e.stopPropagation();
        toggleVisualizationPanel();
    });

    $(document).off("click", "#displayInfoGraphic").on("click", "#displayInfoGraphic", function () {
        var column = $("#colList").val();
        var func = $("#funcList").val();
        var grp = $("#grpList").val();

        getAggregate(column, grp, func);
    });
}
function toggleVisualizationPanel() {
    const $body = $("#visualizationBody");
    const $btn = $("#toggleVisualizationPanel");

    $body.slideToggle(180, function () {
        const isVisible = $body.is(":visible");
        $btn.text(isVisible ? "Hide" : "Show");
    });
}
function getAggregate(column, group, func) {
    var where = ($("#whereClause").val() || "").trim();

    const preFilledWhere = buildPreFilledWhere();

    if (preFilledWhere) {
        where = where
            ? `(${where}) AND (${preFilledWhere})`
            : preFilledWhere;
    }
    var table = $("#uid").val();
    var having=$("#havingClause").val();
    var order=$("#orderBy").val();
    $.ajax({
        url: `/api/crud/aggregate`,
        type: 'GET',
        data: {
            table: table,
            column: column,
            function: func,
            group: group,
            where: where,
            having: having,
            order: order,
            checkRights: true
        },
        success: function (response) {

            if (!response.Success) {
                alert(response.Message);
                return;
            }

            // Example response:
            // [{ DepartmentId: 1, Value: 5000 }, { DepartmentId: 2, Value: 8000 }]

            const data = response.Data;

            if (!data.length) {
                $('#chartContainer').html(`
                    <div class="viz-empty-state">
                        No data found for selected criteria.
                    </div>
                `);

                $('#tableHead').empty();
                $('#tableBody').html(`
                    <tr>
                        <td class="viz-empty-state">No summary data found.</td>
                    </tr>
                `);

                return;
            }

            const groupField = group;   // e.g. DepartmentId

            // =========================
            // Prepare Chart Data
            // =========================
            const categories = [];
            const values = [];

            if (order == null || order == "") {
                data.sort((a, b) => b.Value - a.Value); // Descending
            }
            data.forEach(row => {
                categories.push(row[groupField]);
                values.push(row.Value);
            });

            // =========================
            // Render Highcharts
            // =========================
            Highcharts.chart('chartContainer', {
                chart: {
                    type: 'column'
                },
                title: {
                    text: func + ' of ' + column
                },
                xAxis: {
                    categories: categories,
                    title: { text: group }
                },
                yAxis: {
                    title: {
                        text: func
                    }
                },
                series: [{
                    name: func,
                    data: values
                }]
            });

            // =========================
            // Render Table
            // =========================
            const $head = $('#tableHead');
            const $body = $('#tableBody');

            $head.empty();
            $body.empty();

            // Header
            $head.append(`
                        <th>${group}</th>
                        <th>${func}(${column})</th>
                    `);

            // Rows
            data.forEach(row => {
                $body.append(`
                            <tr>
                                <td>${row[groupField]}</td>
                                <td>${row.Value}</td>
                            </tr>
                        `);
            });

        },
        error: function (xhr) {
            console.error('Error:', xhr.responseText);
        }
    });
}

function buildPreFilledWhere() {
    const preFilledData =
        window.PreFilledData ||
        window.preFilledData ||
        window.extraUploadData ||
        {};

    let filter = "";

    if (preFilledData && Object.keys(preFilledData).length > 0) {
        filter = Object.entries(preFilledData)
            .filter(([key, value]) =>
                value !== null &&
                value !== undefined &&
                value !== ""
            )
            .map(([key, value]) => {
                const safeValue = String(value).replace(/'/g, "''");
                return `{alias}.${key}='${safeValue}'`;
            })
            .join(" AND ");
    }

    return filter;
}

function showColumns(data, includeBlank = true) {

    let $columns = $("#colList");
    let $groups = $("#grpList");

    $columns.empty();
    $groups.empty();
    let sortedData = [...data];

    sortedData.sort((a, b) => (a.SortOrder ?? 0) - (b.SortOrder ?? 0));

    sortedData.forEach(col => {

        const value = col.ColumnName;
        const text = col.DisplayLabel || col.ColumnName;

        $columns.append(`<option value="${value}">${text}</option>`);
        $groups.append(`<option value="${value}">${text}</option>`);
    });

    $("#colList").val("CreatedBy");
    $("#funcList").val("COUNT");
    $("#grpList").val("CreatedBy");

    setTimeout(() => {$("#displayInfoGraphic").trigger("click");}, 0);
}
