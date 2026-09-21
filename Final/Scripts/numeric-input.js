// Blocks non-numeric characters from inputs marked with class="numeric-only"
// (used for pure number fields: money amounts, counts, quantities).
// class="digits-only" is the stricter variant -- no decimal point allowed --
// for fields like Phone where a decimal point never makes sense.
// class="no-digits" is the inverse -- strips digits, for name fields (e.g. Building Name).
// Safety net: scrolling the mouse wheel over a focused <input type="number"> silently changes
// its value in browsers. Numeric fields are type="text" inputmode="decimal" now, but if a
// numeric-only field is ever type="number" again, blurring it on wheel stops the change.
document.addEventListener('wheel', function () {
    var el = document.activeElement;
    if (el && el.classList && el.classList.contains('numeric-only') && el.type === 'number') {
        el.blur();
    }
}, { passive: true });

document.addEventListener('input', function (e) {
    if (e.target.classList.contains('numeric-only')) {
        e.target.value = e.target.value.replace(/[^0-9.]/g, '');
    } else if (e.target.classList.contains('digits-only')) {
        e.target.value = e.target.value.replace(/[^0-9]/g, '');
    } else if (e.target.classList.contains('no-digits')) {
        e.target.value = e.target.value.replace(/[0-9]/g, '');
    }
});
