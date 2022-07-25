﻿import { ResizableTableColumns } from "./ResizableTableColumns/resizable-table-columns.js"
import { highlight } from "./sql-highlight/Index.js";
import { default as Tab } from "./bootstrap/tab.js";


export function initializeResizableTableColumns(tableName) {

    // https://github.com/validide/resizable-table-columns
    var tableElement = window.document.getElementById(tableName);

    var options = {
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

export function searchTable(input, table) {
    // Declare variables
    var input, filter, table, tr, i;
    input = document.getElementById(input);
    filter = input.value.toUpperCase();
    table = document.getElementById(table);
    tr = table.tBodies[0].getElementsByTagName("tr");

    if (filter == "" || filter === undefined || filter === null) {
        for (i = 0; i < tr.length; i++) {
            tr[i].style.display = "";
        }
    } else {
        // Loop through all table rows, and hide those who don't match the search query
        for (i = 0; i < tr.length; i++) {

            // define the row's cells
            var tds = tr[i].getElementsByTagName("td");

            if (tds.length > 0) {
                // hide the row
                tr[i].style.display = "none";

                // loop through row cells
                for (var cellI = 0; cellI < tds.length; cellI++) {

                    // if there's a match
                    if (tds[cellI].innerHTML.toUpperCase().indexOf(filter) > -1) {

                        // show the row
                        tr[i].style.display = "";

                        // skip to the next row
                        continue;

                    }
                }
            }
        }
    }
}

export function addSearchEventHandler(input, table) {
    var buttonElement = window.document.getElementById(input);
    buttonElement.addEventListener('search', () => { searchTable(input, table) });
}

export function showButtonsByAction(action) {
    DotNet.invokeMethodAsync("LightQueryProfiler.SharedWebUI", "ShowButtonsByAction", action)
}