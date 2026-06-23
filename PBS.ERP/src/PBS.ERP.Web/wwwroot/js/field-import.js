(function () {

    function resetImportState() {

        $("#importProgress").hide();

        $("#btnImportColumns")
            .prop("disabled", false);
    }

    function showImportMessage(type, message) {

        $("#importAlert")
            .removeClass()
            .addClass("alert alert-" + type)
            .html(message)
            .fadeIn();
    }

    function performImport(tableUid, columns) {

        $.ajax({

            url: "/api/super/column/import",

            type: "POST",

            contentType: "application/json",

            data: JSON.stringify({
                Table: tableUid,
                Columns: columns
            }),

            success: function (response) {
                resetImportState();
                if (response.Success) {
                    showImportMessage("success", response.Message || "Import completed successfully.");
                    setTimeout(function () { location.reload(); }, 1200);
                }
                else {
                    showImportMessage("danger", response.Message || "Import completed successfully.");
                }
                
            },

            error: function (xhr) {
                resetImportState();
                showImportMessage("danger", xhr.responseText || "Import failed.");
            }
        });
    }

    $(document).ready(function () {

        $("#btnDownloadTemplate").on("click", function () {

            const data = [
                {
                    ColumnName: "CustomerName",
                    SqlType: "varchar(200)",
                    IsRequired: 1   // optional but explicitly shown
                },
                {
                    ColumnName: "Email",
                    SqlType: "varchar(250)"
                    // IsRequired omitted → optional
                },
                {
                    ColumnName: "DateOfBirth",
                    SqlType: "date",
                    DisplayLabel: "Date of Birth"
                    // IsRequired omitted → optional
                },
                {
                    ColumnName: "Salary",
                    SqlType: "decimal(18,2)",
                    DisplayLabel: "Salary"
                    // IsRequired omitted → optional
                }
            ];

            const ws = XLSX.utils.json_to_sheet(data);

            const wb = XLSX.utils.book_new();

            XLSX.utils.book_append_sheet(wb, ws, "Columns");

            XLSX.writeFile(wb, "ColumnImportTemplate.xlsx");
        });

        $("#btnImportColumns").on("click", function () {

            const file =
                $("#importFile")[0].files[0];

            const tableUid =
                $("#uid").val();

            if (!file) {

                showImportMessage(
                    "danger",
                    "Please select a file.");

                return;
            }

            $("#importProgress").show();

            $("#btnImportColumns")
                .prop("disabled", true);

            const reader =
                new FileReader();

            reader.onload = function (e) {

                const workbook =
                    XLSX.read(
                        new Uint8Array(e.target.result),
                        { type: "array" });

                const sheet =
                    workbook.Sheets[
                    workbook.SheetNames[0]
                    ];

                const rows =
                    XLSX.utils.sheet_to_json(sheet);

                const columns =
                    rows.map(r => ({
                        ColumnName: r.ColumnName,
                        SqlType: r.SqlType
                    }));

                performImport(
                    tableUid,
                    columns);
            };

            reader.readAsArrayBuffer(file);
        });

    });

})();