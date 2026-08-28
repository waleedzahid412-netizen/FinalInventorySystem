(function () {
    function setRowChecks(row, checked) {
        row.querySelectorAll('.perm-view, .perm-add, .perm-edit, .perm-delete').forEach(function (cb) {
            cb.checked = checked;
        });
    }

    function setModuleChecks(table, checked) {
        table.querySelectorAll('tbody tr').forEach(function (row) {
            setRowChecks(row, checked);
            var rowSelect = row.querySelector('.select-all-row');
            if (rowSelect) rowSelect.checked = checked;
        });
    }

    document.querySelectorAll('.select-all-row').forEach(function (master) {
        master.addEventListener('change', function () {
            var row = master.closest('tr');
            if (row) setRowChecks(row, master.checked);
        });
    });

    document.querySelectorAll('.select-all-module').forEach(function (master) {
        master.addEventListener('change', function () {
            var table = master.closest('.permission-matrix');
            if (table) setModuleChecks(table, master.checked);
        });
    });

    document.querySelectorAll('.perm-view, .perm-add, .perm-edit, .perm-delete').forEach(function (cb) {
        cb.addEventListener('change', function () {
            var row = cb.closest('tr');
            if (!row) return;
            var boxes = row.querySelectorAll('.perm-view, .perm-add, .perm-edit, .perm-delete');
            var allChecked = Array.from(boxes).every(function (b) { return b.checked; });
            var rowSelect = row.querySelector('.select-all-row');
            if (rowSelect) rowSelect.checked = allChecked;
        });
    });
})();
