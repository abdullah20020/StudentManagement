// Forms with data-confirm="..." ask before submitting (text comes translated from the server).
document.addEventListener('submit', function (e) {
    const form = e.target;
    if (form.dataset && form.dataset.confirm && !window.confirm(form.dataset.confirm)) {
        e.preventDefault();
    }
});
