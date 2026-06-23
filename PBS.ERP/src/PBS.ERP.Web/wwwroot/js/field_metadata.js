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
                "submit", "reset", "button","auto-text","auto-number","auto-time", "grid"
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
        SortOrder: {
            Group: "Layout",
            Type: "number",
            Placeholder: "Order within Section"
        },
        SectionName: {
            Group: "Layout",
            MaxLength: 100,
            Type: "text",
            Placeholder: "Section Name"
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
            Type: "text",
            Placeholder: "Enter Column Name"
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


var INCLUDE = [
    {
        name:"identity",
        label: "Key Fields (ID & UID)?",
        fields: [
            {
                ColumnName: "ID",
                InputType: "auto-number",
                SqlType: "int identity",
                DisplayLabel: "ID",
                IsRequired: true,
                IsReadonly: true,
                AllowInsert: false,
                AllowUpdate: false,
                ShowInList: false,
                IsComputed: false,
                SectionNumber: 0,
                SectionName: "Table Key",
                SortOrder: 0
            },
            {
                ColumnName: "UID",
                InputType: "auto-text",
                SqlType: "varchar",
                MaxLength:100,
                DisplayLabel: "UID",
                IsRequired: true,
                IsReadonly: true,
                IsComputed: true,
                AllowInsert: false,
                AllowUpdate: false,
                ShowInList: false,
                SectionNumber: 0,
                SectionName: "Table Key",
                SortOrder: 1
            }
        ]
    },
    {
        name: "remarks",
        label: "Remarks Column (500) ?",
        fields: [
            {
                ColumnName: "Remarks",
                SqlType: "varchar",
                MaxLength: 500,
                InputType: "text",
                DisplayLabel: "Remarks",
                IsRequired: false,
                IsReadonly: false,
                IsComputed: false,
                SectionNumber: 10,
                SectionName: "System Generated Fields",
                SortOrder: 1
            }
        ]
    },
    {
        name: "deleted",
        label: "Column IsDeleted (true/false)?",
        fields: [
            {
                ColumnName: "IsDeleted",
                SqlType: "bit",
                MaxLength: null,
                InputType: "boolean",
                IsForeignKey: false,
                DisplayLabel: "IsDeleted?",
                IsRequired: true,
                IsReadonly: true,
                AllowInsert: false,
                AllowUpdate: false,
                ShowInList:false,
                IsComputed: false,
                SectionNumber: 10,
                SectionName: "System Generated Fields",
                SortOrder: 10
            }
        ]
    },
    {
        name:"timestamp",
        lable: "Timestamp (Created, Modified, Deleted)?",
        fields: [
            {
                SqlType: "datetime",
                InputType: "auto-time",
                ColumnName: "CreatedTime",
                DisplayLabel: "Created Time",
                IsRequired: false,
                IsReadonly: true,
                IsComputed: false,
                AllowInsert: false,
                AllowUpdate: false,
                SectionNumber: 10,
                SectionName: "System Generated Fields",
                SortOrder: 3
            },
            {
                SqlType: "datetime",
                InputType: "auto-time",
                ColumnName: "ModifiedTime",
                DisplayLabel: "Modified Time",
                IsRequired: false,
                IsReadonly: true,
                IsComputed: false,
                AllowInsert: false,
                AllowUpdate: false,
                SectionNumber: 10,
                SectionName: "System Generated Fields",
                SortOrder: 4
            },
            {
                SqlType: "datetime",
                InputType: "auto-time",
                ColumnName: "DeletedTime",
                DisplayLabel: "Deleted Time",
                IsRequired: false,
                IsReadonly: true,
                IsComputed: false,
                AllowInsert: false,
                AllowUpdate: false,
                ShowInList:false,
                SectionNumber: 10,
                SectionName: "System Generated Fields",
                SortOrder: 5
            }
        ]
    },
    {
        name:"doneby",
        lable: "Action Done By (CreatedBy, ModifiedBy, DeletedBy)?",
        fields: [
            {
                InputType: "select",
                SqlType: "varchar",
                MaxLength: 100,
                ColumnName: "CreatedBy",
                DisplayLabel: "Created By",
                IsRequired: true,
                IsReadonly: true,
                IsComputed: true,
                AllowInsert: false,
                AllowUpdate: false,
                IsForeignKey: true,
                ShowInList: false,
                IsComputed:false,
                DropdownSourceTable: "Users",
                DropdownValueColumn: "UserName",
                DropdownTextColumn: "FullName",
                SectionNumber: 10,
                SectionName: "System Generated Fields",
                SortOrder: 6
            },
            {
                InputType: "select",
                SqlType: "varchar",
                MaxLength: 100,
                ColumnName: "ModifiedBy",
                DisplayLabel: "Modified By",
                IsRequired: false,
                IsReadonly: true,
                AllowInsert: false,
                AllowUpdate: false,
                ShowInList: false,
                IsComputed: false,
                DropdownSourceTable: "Users",
                DropdownValueColumn: "UserName",
                DropdownTextColumn: "FullName",
                SectionNumber: 10,
                SectionName: "System Generated Fields",
                SortOrder: 7
            },
            {
                InputType: "select",
                SqlType: "varchar",
                MaxLength: 100, 
                ColumnName: "DeletedBy",
                DisplayLabel: "Deleted By",
                IsRequired: false,
                IsReadonly: true,
                AllowInsert: false,
                AllowUpdate: false,
                IsComputed: true,
                ShowInList: false,
                DropdownSourceTable: "Users",
                DropdownValueColumn: "UserName",
                DropdownTextColumn: "FullName",
                SectionNumber: 10,
                SectionName: "System Generated Fields",
                SortOrder: 8
            },
        ]
    },
    /*
    {
        name:"lookup",
        label: "Include Lookup Column ID, Text, Value?",
        fields: [
            {
                ColumnName: "ID",
                SqlType: "int",
                DisplayLabel: "ID",
                InputType: "number",
                IsRequired: true,
                IsComputed: false,
                SectionNumber: 0,
                SortOrder: 0
            },
            {
                ColumnName: "Value",
                SqlType: "varchar",
                MaxLength: 100,
                DisplayLabel: "Value",
                InputType: "text",
                IsRequired: true,
                IsComputed: false,
                SectionNumber: 0,
                SortOrder: 0
            },
            {
                ColumnName: "Text",
                SqlType: "varchar",
                MaxLength: 100,
                DisplayLabel: "Text",
                InputType: "text",
                IsRequired: true,
                IsComputed: false,
                SectionNumber: 0,
                SortOrder: 0
            }
        ]
    }
    */
];