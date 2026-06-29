var inputConfig = {

    FieldMetadata: {

        DisplayLabel: {
            Group:"Layout",
            MaxLength: 500,
            Type: "text",
            Placeholder: "Label / Heading"
        },
        InputType: {
            Group: "Layout",
            Type: "select",
            options: [
                "text", "number", "select", "boolean", "checkbox", "radio", "textarea", "password",
                "date", "datetime-local",
                "time", 
                "file", "hidden", "email", "url", "tel", "color",
                "range", "month", "week", "search",
                "submit", "reset", "button","auto-text","auto-number","auto-time"
            ]
        },
        Placeholder: {
            Group: "Layout",
            MaxLength: 100,
            Type: "text",
            Placeholder: "Placeholder"
        },

        Tooltip: {
            Group: "Layout",
            MaxLength: 500,
            Type: "text",
            Placeholder: "Tooltip Text"
        },
        SectionNumber: {
            Group: "Layout",
            Type: "number",
            Placeholder: "Section Number"
        },
        SectionName: {
            Group: "Layout",
            MaxLength: 100,
            Type: "text",
            Placeholder: "Section Name"
        },
        SortOrder: {
            Group: "Layout",
            Type: "number",
            Placeholder: "Order within Section"
        },

        //database level
        ColumnName: {
            Group: "Database",
            MaxLength: 100,
            Type: "text",
            Placeholder: "Column Name"
        },

        SqlType: {
            Group: "Database",
            Type: "select",
            options: [
                "int", "int identity", "smallint", "bigint", "bit",
                "decimal", "float", "double", "date", "datetime",
                "char", "varchar", "nchar", "nvarchar",
                "varchar(max)", "nvarchar(max)",
                "text", "ntext", "uniqueidentifier",
                "varbinary", "image", "xml"
            ]
        },
        DecimalPlaces: {
            Group: "Database",
            Type: "number",
            Placeholder: "Decimal Places"
        },
        MaxLength: {
            Group: "Database",
            Type: "number",
            Placeholder: "Max Length"
        },
        IsRequired: {
            Group: "Database",
            Type: "select",
            options: [
                { value: false, text: "Optional" },
                { value: true, text: "Mandatory" }
            ]
        },
        DefaultValue: {
            Group: "Database",
            MaxLength: 100,
            Type: "text",
            Placeholder: "Default Value"
        },
        DefaultExpression: {
            Group: "Database",
            MaxLength: 100,
            Type: "text",
            Placeholder: "Default Expression"
        },
        TableType: {
            Type: "select",
            options: []
        },
        

        //forieng key and options fields
        IsForeignKey: {
            Group: "Options",
            Type: "select",
            options: [
                { value: false, text: "No" },
                { value: true, text: "Yes" }
            ]
        },

        DropdownSourceTable: {
            Group: "Options",
            Type: "select",
            options: []
        },

        DropdownValueColumn: {
            Group: "Options",
            Type: "select"
        },

        DropdownTextColumn: {
            Group: "Options",
            Type: "text"
        },

        DropdownWhere: {
            Group: "Options",
            Type: "text",
            Placeholder: "Where Clause / Filter"
        },

        DropdownOrderBy: {
            Group: "Options",
            Type: "text",
            Placeholder: "Order by Column"
        },

        //flags
        IsReadonly: {
            Group: "Flags",
            Type: "select",
            options: [
                { value: false, text: "No" },
                { value: true, text: "Yes" }
            ]
        },
        AllowInsert: {
            Group: "Flags",
            Type: "select",
            options: [
                { value: true, text: "Yes" },
                { value: false, text: "No" }
            ]
        },

        AllowUpdate: {
            Group: "Flags",
            Type: "select",
            options: [
                { value: true, text: "Yes" },
                { value: false, text: "No" }
            ]
        },

        ShowInList: {
            Group: "Flags",
            Type: "select",
            options: [
                { value: true, text: "Yes" },
                { value: false, text: "No" }
            ]
        },


        IsComputed: {
            Group: "Filter",
            Type: "select",
            options: [
                { value: false, text: "No" },
                { value: true, text: "Yes" }
            ]
        },
        ComputedExpression: {
            Group: "Filter",
            Type: "text",
            Placeholder: "Computed Expression"
        },
        MinLength: {
            Group: "Filter",
            Type: "number",
            Placeholder: "Min Length"
        },
        MinValue: {
            Group: "Filter",
            Type: "number",
            Placeholder: "Min Value"
        },
        MaxValue: {
            Group: "Filter",
            Type: "number",
            Placeholder: "Max Value"
        },
        RegexPattern: {
            Group: "Filter",
            Type: "text",
            Placeholder: "Regex Pattern"
        },
        

        

        

        
        IsSearchable: {
            Type: "select",
            options: [
                { value: true, text: "Yes" },
                { value: false, text: "No" }
            ]
        },

        IsSortable: {
            Type: "select",
            options: [
                { value: true, text: "Yes" },
                { value: false, text: "No" }
            ]
        },

        Exportable: {
            Type: "select",
            options: [
                { value: true, text: "Yes" },
                { value: false, text: "No" }
            ]
        },

        Importable: {
            Type: "select",
            options: [
                { value: true, text: "Yes" },
                { value: false, text: "No" }
            ]
        },

        IsDeleted: {
            Type: "select",
            options: [
                { value: false, text: "No" },
                { value: true, text: "Yes" }
            ]
        },

        IsMultiSelect: {
            Type: "select",
            options: [
                { value: false, text: "No" },
                { value: true, text: "Yes" }
            ]
        },

        

        

        
    }
};

const inputTypeMap = {
    number: ['amount', 'value', 'price', 'quantity', 'number'],
    text: ['name', 'description', 'designation', 'specify'],
    date: ['date', 'time'],
    // Add more as needed
};


