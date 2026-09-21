// Prevents picking the same product in more than one Items row of a requisition
// form -- each item row's product dropdown carries class="product-select".
// Scoped per <form> so independent forms (e.g. one Edit-&-Resubmit modal per
// rejected requisition on the same page) never disable each other's options.
function refreshProductSelectGuard(form) {
    var selects = form.querySelectorAll('select.product-select');
    if (selects.length < 2) return;

    var chosen = {};
    selects.forEach(function (s) {
        if (s.value && s.value !== '0') {
            chosen[s.value] = (chosen[s.value] || 0) + 1;
        }
    });

    selects.forEach(function (currentSelect) {
        Array.prototype.forEach.call(currentSelect.options, function (opt) {
            if (!opt.value || opt.value === '0') {
                opt.disabled = false;
                return;
            }
            opt.disabled = !!chosen[opt.value] && currentSelect.value !== opt.value;
        });
    });
}

// Quantity is only required on a row once that row's product has actually been
// chosen -- otherwise every still-empty row (rows are pre-padded up to 5) would
// block submit via native HTML5 validation even though the server already
// ignores rows with no product selected.
function refreshQuantityRequired(row) {
    var select = row.querySelector('select.product-select');
    var qty = row.querySelector('.qty-input');
    if (!select || !qty) return;
    qty.required = !!select.value && select.value !== '0';
}

document.addEventListener('change', function (e) {
    if (e.target.classList && e.target.classList.contains('product-select')) {
        var form = e.target.closest('form');
        if (form) refreshProductSelectGuard(form);

        var row = e.target.closest('.item-row');
        if (row) refreshQuantityRequired(row);
    }
});

document.addEventListener('DOMContentLoaded', function () {
    document.querySelectorAll('form').forEach(function (form) {
        refreshProductSelectGuard(form);
    });
    document.querySelectorAll('.item-row').forEach(function (row) {
        refreshQuantityRequired(row);
    });
});
