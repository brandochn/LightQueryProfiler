import { ResizableTableColumns } from "./ResizableTableColumns/resizable-table-columns.js"
import { highlight } from "./sql-highlight/Index.js";
import { default as Tab } from "./bootstrap/tab.js";


export function initializeResizableTableColumns(tableName) {

    // https://github.com/validide/resizable-table-columns
    let tableElement = window.document.getElementById(tableName);

    let options = {
        // boolean - The resize handle will span the entire height of the table
        resizeFromBody: true,

        // null or number - The minimum width any column in the table should have
        minWidth: 80,

        // null or number - The maximum width any column in the table should have
        maxWidth: null,

        // number - The maximum number off milliseconds between to pointer down events to consider the action a 'double click'
        doubleClickDelay: 500,

        // data store provider (ex: https://github.com/marcuswestin/store.js)
        store: null,

        // null or number - The suggestion for how wide (in pixels) a cell might be in case the content is really wide.
        maxInitialWidthHint: null
    }

    new ResizableTableColumns(tableElement, options);
}

export function syntaxHighlight(sqlString) {

    let highlighted = highlight(sqlString, {
        html: true
    });

    return highlighted;
}

export function initializeNavTab(tabName) {
    const triggerTabList = document.querySelectorAll(`#${tabName} button`);
    triggerTabList.forEach(triggerEl => {
        const tabTrigger = new Tab(triggerEl)

        triggerEl.addEventListener('click', event => {
            event.preventDefault()
            // Fix tab details resize issue, this is temporally.
            window.dispatchEvent(new Event('resize'));
            tabTrigger.show()
        })
    });
}