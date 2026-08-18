// WIMS site scripts — Select2 searchable dropdowns + shared helpers.
(function (window, $) {
    'use strict';

    if (!$ || !$.fn) {
        return;
    }

    var refreshing = new WeakSet();

    function placeholderFor($el) {
        var dataPh = $el.data('placeholder');
        if (dataPh) {
            return dataPh;
        }
        var emptyOpt = $el.find('option[value=""]').first();
        if (emptyOpt.length) {
            return emptyOpt.text() || 'Select...';
        }
        var first = $el.find('option').first();
        return first.length ? first.text() : 'Select...';
    }

    function dropdownParentFor($el) {
        var $modal = $el.closest('.modal');
        if ($modal.length) {
            return $modal;
        }
        return $(document.body);
    }

    function shouldSkip(el) {
        var $el = $(el);
        if (!$el.is('select')) {
            return true;
        }
        if ($el.is('[data-no-select2]')) {
            return true;
        }
        if ($el.attr('multiple') !== undefined && $el.is('[data-no-select2-multi]')) {
            return true;
        }
        // Native listbox (size > 1) — leave alone
        var size = parseInt($el.attr('size'), 10);
        if (size > 1) {
            return true;
        }
        return false;
    }

    function destroy(el) {
        var $el = $(el);
        if ($el.hasClass('select2-hidden-accessible')) {
            try {
                $el.select2('destroy');
            } catch (e) {
                /* ignore */
            }
        }
    }

    /**
     * Clone a table row (or container) without Select2 chrome so new selects can be re-initialized.
     */
    function cloneClean($source) {
        var $clone = $source.clone(false, false);
        $clone.find('.select2-container').remove();
        $clone.find('select').each(function () {
            var $s = $(this);
            $s.removeClass('select2-hidden-accessible')
                .removeAttr('data-select2-id')
                .removeAttr('aria-hidden')
                .removeAttr('tabindex')
                .css('display', '');
            $s.find('option').removeAttr('data-select2-id');
            this._wimsSelect2OptionObserver = null;
        });
        return $clone;
    }

    function initOne(el, options) {
        if (!$.fn.select2 || shouldSkip(el)) {
            return;
        }

        var $el = $(el);
        if ($el.hasClass('select2-hidden-accessible')) {
            return;
        }

        var allowClear = $el.data('allow-clear');
        if (allowClear === undefined) {
            allowClear = $el.find('option[value=""]').length > 0;
        } else {
            allowClear = allowClear === true || allowClear === 'true';
        }

        var opts = $.extend({
            theme: 'bootstrap-5',
            width: '100%',
            placeholder: placeholderFor($el),
            allowClear: !!allowClear,
            dropdownParent: dropdownParentFor($el)
        }, options || {});

        $el.select2(opts);
        watchOptionMutations(el);

        // Keep unobtrusive validation in sync when Select2 changes value
        $el.off('change.wimsSelect2Validate').on('change.wimsSelect2Validate', function () {
            if ($el.valid && $el.closest('form').length) {
                try { $el.valid(); } catch (e) { /* validator may be absent */ }
            }
        });
    }

    function refresh(el) {
        if (!el || shouldSkip(el)) {
            return;
        }
        if (refreshing.has(el)) {
            return;
        }
        refreshing.add(el);
        try {
            var $el = $(el);
            var val = $el.val();
            destroy(el);
            initOne(el);
            if (val !== undefined && val !== null) {
                $el.val(val);
                sync(el);
            }
        } finally {
            refreshing.delete(el);
        }
    }

    function sync(el) {
        var $el = $(el);
        if ($el.hasClass('select2-hidden-accessible')) {
            $el.trigger('change.select2');
        }
    }

    function init(context) {
        if (!$.fn.select2) {
            return;
        }
        $(context || document).find('select').each(function () {
            initOne(this);
        });
    }

    function watchOptionMutations(select) {
        if (select._wimsSelect2OptionObserver) {
            return;
        }
        var timer = null;
        var obs = new MutationObserver(function () {
            if (refreshing.has(select)) {
                return;
            }
            clearTimeout(timer);
            timer = setTimeout(function () {
                if (document.body.contains(select)) {
                    refresh(select);
                }
            }, 40);
        });
        obs.observe(select, { childList: true });
        select._wimsSelect2OptionObserver = obs;
    }

    function observeNewSelects() {
        var root = document.getElementById('main-content') || document.body;
        if (!root || root._wimsSelect2DomObserver) {
            return;
        }
        var timer = null;
        var obs = new MutationObserver(function (mutations) {
            var found = false;
            mutations.forEach(function (m) {
                m.addedNodes.forEach(function (node) {
                    if (node.nodeType !== 1) {
                        return;
                    }
                    if (node.matches && node.matches('select')) {
                        found = true;
                    } else if (node.querySelectorAll && node.querySelectorAll('select').length) {
                        found = true;
                    }
                });
            });
            if (!found) {
                return;
            }
            clearTimeout(timer);
            timer = setTimeout(function () {
                init(root);
            }, 30);
        });
        obs.observe(root, { childList: true, subtree: true });
        root._wimsSelect2DomObserver = obs;
    }

    window.WimsSelect2 = {
        init: init,
        initOne: initOne,
        refresh: refresh,
        sync: sync,
        destroy: destroy,
        cloneClean: cloneClean
    };

    $(function () {
        init(document);
        observeNewSelects();
    });
})(window, window.jQuery);
