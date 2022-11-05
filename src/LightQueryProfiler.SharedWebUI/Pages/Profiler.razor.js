export function highlightRow(table, _this) {

    var trows = document.getElementById(table).rows;
    for (var t = 1; t < trows.length; ++t) {
        var trow = trows[t];
        if (trow != this) { trow.className = "table-light" }
    }

    if (_this != undefined && _this != null) {
        _this.className = (this.className == "table-active") ? "table-light" : "table-active";
    }
}

export function tableKeydownHandler(table) {
    //derived from: https://stackoverflow.com/questions/17847618/adding-functionality-for-using-the-up-and-down-arrow-keys-to-select-a-table-row

    //From: http://forrst.com/posts/JavaScript_Cross_Browser_Event_Binding-yMd
    var addEvent = (function (window, document) {
        if (document.addEventListener) {
            return function (elem, type, cb) {
                if ((elem && !elem.length) || elem === window) {
                    elem.addEventListener(type, cb, false);
                }
                else if (elem && elem.length) {
                    var len = elem.length;
                    for (var i = 0; i < len; i++) {
                        addEvent(elem[i], type, cb);
                    }
                }
            };
        }
        else if (document.attachEvent) {
            return function (elem, type, cb) {
                if ((elem && !elem.length) || elem === window) {
                    elem.attachEvent('on' + type, function () { return cb.call(elem, window.event) });
                }
                else if (elem.length) {
                    var len = elem.length;
                    for (var i = 0; i < len; i++) {
                        addEvent(elem[i], type, cb);
                    }
                }
            };
        }
    })(this, document);

    //derived from: http://stackoverflow.com/a/10924150/402706
    function getpreviousSibling(element) {
        var p = element;
        do p = p.previousSibling;
        while (p && p.nodeType != 1);
        return p;
    }

    //derived from: http://stackoverflow.com/a/10924150/402706
    function getnextSibling(element) {
        var p = element;
        do p = p.nextSibling;
        while (p && p.nodeType != 1);
        return p;
    }

    ; (function () {

        addEvent(document.getElementById(table), 'keydown', function (e) {
            var key = e.keyCode || e.which;

            if ((key === 38 || key === 40) && !e.shiftKey && !e.metaKey && !e.ctrlKey && !e.altKey) {

                var highlightedRows = document.querySelectorAll('.table-active');

                if (highlightedRows.length > 0) {

                    var highlightedRow = highlightedRows[0];
                    // Ignore hidden rows
                    var prev = getpreviousSibling(highlightedRow);
                    while (prev && window.getComputedStyle(prev).display === "none") {
                        prev = getpreviousSibling(prev);
                    }

                    // Ignore hidden rows
                    var next = getnextSibling(highlightedRow);
                    while (next && window.getComputedStyle(next).display === "none") {
                        next = getnextSibling(next);
                    }

                    var event = new MouseEvent('dblclick', {
                        'view': window,
                        'bubbles': true,
                        'cancelable': true
                    });

                    if (key === 38 && prev && prev.nodeName === highlightedRow.nodeName) {//up
                        highlightedRow.className = 'table-light';
                        prev.className = 'table-active';
                        prev.dispatchEvent(event);
                    } else if (key === 40 && next && next.nodeName === highlightedRow.nodeName) { //down
                        highlightedRow.className = 'table-light';
                        next.className = 'table-active';
                        next.dispatchEvent(event);
                    }

                }
            }

        });


    })();//end script
}