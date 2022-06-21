import { ResizableTableColumns } from "./ResizableTableColumns/resizable-table-columns.js"
import { highlight } from "./sql-highlight/Index.js";


export function initializeResizableTableColumns(tableName) {

    let tableElement = window.document.getElementById(tableName);

    new ResizableTableColumns(tableElement, null);
}

export function syntaxHighlight(sqlString) {

    let highlighted = highlight(sqlString, {
        html: true
    });

    return highlighted;
}