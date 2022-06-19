import { highlight } from "./sql-highlight/Index.js";

export function syntaxHighlight(sqlString) {

    let highlighted = highlight(sqlString, {
        html: true
    });

    return highlighted;
}