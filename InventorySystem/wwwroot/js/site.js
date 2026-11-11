// CSRF header for cookie-authenticated JSON POSTs + redirect on expired session (401).
(function (window) {
    'use strict';

    var loginUrl = '/Auth/Login?expired=1';
    var csrfHeaderName = 'RequestVerificationToken';

    function getCsrfToken() {
        var meta = document.querySelector('meta[name="csrf-token"]');
        return meta ? meta.getAttribute('content') : '';
    }

    function isUnsafeMethod(method) {
        var normalized = String(method || 'GET').toUpperCase();
        return normalized === 'POST' || normalized === 'PUT' || normalized === 'PATCH' || normalized === 'DELETE';
    }

    function withCsrfHeaders(headers) {
        var token = getCsrfToken();
        if (!token) {
            return headers;
        }

        if (headers instanceof Headers) {
            if (!headers.has(csrfHeaderName)) {
                headers.set(csrfHeaderName, token);
            }
            return headers;
        }

        var next = headers ? Object.assign({}, headers) : {};
        if (!next[csrfHeaderName]) {
            next[csrfHeaderName] = token;
        }
        return next;
    }

    function isAuthPath(url) {
        if (!url) {
            return false;
        }
        try {
            var path = String(url);
            return path.indexOf('/Auth/') !== -1;
        } catch (e) {
            return false;
        }
    }

    function redirectToLogin() {
        if (window.location.pathname.indexOf('/Auth') === 0) {
            return;
        }
        window.location.href = loginUrl;
    }

    window.WimsCsrf = {
        headerName: csrfHeaderName,
        getToken: getCsrfToken
    };

    if (typeof window.fetch === 'function') {
        var originalFetch = window.fetch.bind(window);
        window.fetch = function (input, init) {
            init = init || {};
            var url = typeof input === 'string' ? input : (input && input.url);

            if (isUnsafeMethod(init.method)) {
                init.headers = withCsrfHeaders(init.headers);
            }

            return originalFetch(input, init).then(function (response) {
                if (response.status === 401 && !isAuthPath(url)) {
                    redirectToLogin();
                }
                return response;
            });
        };
    }

    if (window.jQuery) {
        window.jQuery.ajaxPrefilter(function (options) {
            if (!isUnsafeMethod(options.type || options.method)) {
                return;
            }

            var token = getCsrfToken();
            if (!token) {
                return;
            }

            options.headers = options.headers || {};
            if (!options.headers[csrfHeaderName]) {
                options.headers[csrfHeaderName] = token;
            }
        });

        window.jQuery(document).ajaxError(function (event, jqXHR) {
            if (jqXHR && jqXHR.status === 401) {
                redirectToLogin();
            }
        });
    }
})(window);

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

    // Select2 can misplace the dropdown on the first open when the select was
    // just injected (e.g. dynamic grid rows) before layout has settled.
    function fixDropdownPosition($el) {
        var inst = $el.data('select2');
        if (!inst || !inst.dropdown || typeof inst.dropdown._positionDropdown !== 'function') {
            return;
        }
        inst.dropdown._positionDropdown();
    }

    function attachDropdownOpenFix($el) {
        $el.off('select2:open.wimsPosFix').on('select2:open.wimsPosFix', function () {
            fixDropdownPosition($el);
            requestAnimationFrame(function () {
                fixDropdownPosition($el);
            });
        });
    }

    function initAfterLayout(el, options, done) {
        requestAnimationFrame(function () {
            requestAnimationFrame(function () {
                if (!document.body.contains(el)) {
                    if (typeof done === 'function') {
                        done();
                    }
                    return;
                }
                initOne(el, options);
                if (typeof done === 'function') {
                    done();
                }
            });
        });
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
        attachDropdownOpenFix($el);
        watchOptionMutations(el);

        // Keep unobtrusive validation in sync when Select2 changes value
        $el.off('change.wimsSelect2Validate').on('change.wimsSelect2Validate', function () {
            if ($el.valid && $el.closest('form').length) {
                try { $el.valid(); } catch (e) { /* validator may be absent */ }
            }
        });
    }

    function refresh(el, options) {
        if (!el || shouldSkip(el)) {
            return;
        }
        if (refreshing.has(el)) {
            return;
        }
        refreshing.add(el);

        var $el = $(el);
        var val = $el.val();
        var deferLayout = $el.closest('table').length > 0;
        destroy(el);

        function finishRefresh() {
            try {
                if (val !== undefined && val !== null) {
                    $el.val(val);
                    sync(el);
                }
            } finally {
                refreshing.delete(el);
            }
        }

        if (deferLayout) {
            initAfterLayout(el, options, finishRefresh);
            return;
        }

        initOne(el, options);
        finishRefresh();
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
                $(root).find('select').each(function () {
                    if (this.closest('table')) {
                        initAfterLayout(this);
                    } else {
                        initOne(this);
                    }
                });
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

    var toastDefaults = {
        success: { delay: 3000, live: 'polite', role: 'status', sticky: false },
        info: { delay: 4000, live: 'polite', role: 'status', sticky: false },
        warning: { delay: 5000, live: 'assertive', role: 'alert', sticky: true },
        danger: { delay: 6000, live: 'assertive', role: 'alert', sticky: true }
    };

    function normalizeToastType(type) {
        var value = String(type || 'danger').toLowerCase();
        if (value === 'error') {
            return 'danger';
        }
        return toastDefaults[value] ? value : 'danger';
    }

    window.WimsToast = {
        show: function (message, type, options) {
            options = options || {};
            type = normalizeToastType(type);

            var container = document.getElementById('wimsToastContainer');
            if (!container || !window.bootstrap) {
                if (window.console) {
                    console.warn('WimsToast could not render:', message);
                }
                return;
            }

            while (container.children.length >= 4) {
                container.removeChild(container.firstElementChild);
            }

            var defaults = toastDefaults[type];
            var lightToast = type === 'warning' || type === 'info' || type === 'light';
            var toastEl = document.createElement('div');
            toastEl.className = 'toast wims-toast align-items-center border-0 shadow bg-' + type +
                (lightToast ? ' text-dark' : ' text-white');
            toastEl.setAttribute('role', defaults.role);
            toastEl.setAttribute('aria-live', defaults.live);
            toastEl.setAttribute('aria-atomic', 'true');

            var flex = document.createElement('div');
            flex.className = 'd-flex';

            var body = document.createElement('div');
            body.className = 'toast-body';
            body.textContent = String(message || 'Something went wrong.');

            var close = document.createElement('button');
            close.type = 'button';
            close.className = 'btn-close me-2 m-auto' + (lightToast ? '' : ' btn-close-white');
            close.setAttribute('data-bs-dismiss', 'toast');
            close.setAttribute('aria-label', 'Close');

            flex.appendChild(body);
            flex.appendChild(close);
            toastEl.appendChild(flex);
            container.appendChild(toastEl);

            var sticky = options.sticky === true || (options.sticky !== false && defaults.sticky === true);
            var delay = typeof options.delay === 'number' ? options.delay : defaults.delay;
            var toast = bootstrap.Toast.getOrCreateInstance(toastEl, {
                animation: true,
                autohide: !sticky,
                delay: delay
            });
            toastEl.addEventListener('hidden.bs.toast', function () {
                toastEl.remove();
            });
            toast.show();
        }
    };

    function showNotification(level, message, options) {
        var type = level === 'error' ? 'danger' : level;
        window.WimsToast.show(message, type, options);
    }

    window.WimsNotify = {
        show: showNotification,
        success: function (message, options) { showNotification('success', message, options); },
        error: function (message, options) { showNotification('error', message, options); },
        warning: function (message, options) { showNotification('warning', message, options); },
        info: function (message, options) { showNotification('info', message, options); },

        // Stores a message that should survive a full page reload.
        flash: function (message, type) {
            try {
                window.sessionStorage.setItem('wims:flash', JSON.stringify({
                    message: String(message || ''),
                    type: normalizeToastType(type),
                    createdAt: Date.now()
                }));
            } catch (e) {
                // Session storage can be unavailable in private browsing; ignore.
            }
        },

        consumeFlash: function () {
            try {
                var raw = window.sessionStorage.getItem('wims:flash');
                if (!raw) {
                    return;
                }
                window.sessionStorage.removeItem('wims:flash');
                var data = JSON.parse(raw);
                if (!data || !data.message || (Date.now() - Number(data.createdAt || 0)) > 60000) {
                    return;
                }
                window.WimsToast.show(data.message, data.type, { delay: 4500 });
            } catch (e) {
                // Ignore malformed or unavailable session storage.
            }
        }
    };

    // Initialize toasts emitted by the server-side _FlashMessages partial.
    function initServerToasts() {
        document.querySelectorAll('[data-wims-toast]').forEach(function (el) {
            var message = el.getAttribute('data-wims-toast-message');
            var type = el.getAttribute('data-wims-toast-type') || 'danger';
            el.remove();
            if (message) {
                window.WimsToast.show(message, type);
            }
        });
    }

    // Promise-based Bootstrap confirmation dialog. The markup lives in _ConfirmModal.cshtml.
    (function initDialog() {
        var modalEl = document.getElementById('wimsConfirmModal');
        if (!modalEl || !window.bootstrap) {
            window.WimsDialog = {
                confirm: function () {
                    if (window.console) {
                        console.error('WimsDialog is unavailable because the confirmation modal is missing.');
                    }
                    return Promise.resolve(false);
                }
            };
            return;
        }

        var modal = bootstrap.Modal.getOrCreateInstance(modalEl);
        var titleEl = document.getElementById('wimsConfirmTitle');
        var headerEl = document.getElementById('wimsConfirmHeader');
        var messageEl = document.getElementById('wimsConfirmMessage');
        var detailsEl = document.getElementById('wimsConfirmDetails');
        var okButton = document.getElementById('wimsConfirmOk');
        var cancelButton = document.getElementById('wimsConfirmCancel');
        var pending = null;
        var trigger = null;

        function settle(result) {
            if (!pending) {
                return;
            }
            var resolve = pending;
            pending = null;
            resolve(result);
        }

        modalEl.addEventListener('hidden.bs.modal', function () {
            settle(false);
            if (trigger && document.body.contains(trigger)) {
                trigger.focus();
            }
            trigger = null;
        });

        okButton.addEventListener('click', function () {
            settle(true);
            modal.hide();
        });

        window.WimsDialog = {
            confirm: function (options) {
                options = options || {};
                if (pending) {
                    settle(false);
                }

                trigger = document.activeElement;
                titleEl.textContent = options.title || 'Confirm action';
                messageEl.textContent = options.message || 'Are you sure you want to continue?';
                cancelButton.textContent = options.cancelText || 'Cancel';
                okButton.textContent = options.confirmText || 'Confirm';

                detailsEl.innerHTML = '';
                var details = options.details || [];
                details.forEach(function (detail) {
                    var item = document.createElement('li');
                    item.textContent = String(detail);
                    detailsEl.appendChild(item);
                });
                detailsEl.classList.toggle('d-none', details.length === 0);

                var variant = options.variant || 'primary';
                headerEl.className = 'modal-header ' +
                    (variant === 'danger' ? 'bg-danger text-white' :
                        variant === 'warning' ? 'bg-warning text-dark' : 'bg-primary text-white');
                okButton.className = 'btn ' +
                    (variant === 'danger' ? 'btn-danger' :
                        variant === 'warning' ? 'btn-warning' : 'btn-primary');

                var promise = new Promise(function (resolve) {
                    pending = resolve;
                });
                modal.show();
                return promise;
            }
        };
    })();

    // Any form can opt into the shared confirmation dialog with data-wims-confirm="...".
    document.addEventListener('submit', function (event) {
        var form = event.target;
        if (!(form instanceof HTMLFormElement) || !form.dataset.wimsConfirm) {
            return;
        }
        if (form.dataset.wimsConfirmed === '1') {
            return;
        }

        event.preventDefault();
        window.WimsDialog.confirm({
            title: form.dataset.wimsConfirmTitle || 'Confirm action',
            message: form.dataset.wimsConfirm,
            confirmText: form.dataset.wimsConfirmText || 'Confirm',
            variant: form.dataset.wimsConfirmVariant || 'danger'
        }).then(function (confirmed) {
            if (!confirmed) {
                return;
            }
            form.dataset.wimsConfirmed = '1';
            if (typeof form.requestSubmit === 'function') {
                form.requestSubmit();
            } else {
                form.submit();
            }
        });
    });

    function responseJson(xhr) {
        if (!xhr) {
            return null;
        }
        if (xhr.responseJSON) {
            return xhr.responseJSON;
        }
        try {
            return xhr.responseText ? JSON.parse(xhr.responseText) : null;
        } catch (e) {
            return null;
        }
    }

    function firstError(data) {
        if (!data) {
            return null;
        }
        if (typeof data.message === 'string' && data.message) {
            return data.message;
        }
        if (Array.isArray(data.errors) && data.errors.length) {
            return data.errors.join(' ');
        }
        if (data.errors && typeof data.errors === 'object') {
            var values = Object.keys(data.errors).reduce(function (all, key) {
                var item = data.errors[key];
                return all.concat(Array.isArray(item) ? item : [item]);
            }, []);
            if (values.length) {
                return values.join(' ');
            }
        }
        return data.title || data.detail || null;
    }

    window.WimsAjax = {
        messageFrom: function (xhr, fallback) {
            var data = responseJson(xhr);
            return firstError(data) || fallback || 'Something went wrong. Please try again.';
        },

        handleError: function (xhr, fallback) {
            var status = xhr && xhr.status ? xhr.status : 0;
            var message = firstError(responseJson(xhr));

            if (status === 0) {
                message = 'Unable to reach the server. Check your connection and try again.';
            } else if (!message) {
                if (status === 401) {
                    message = 'Your session has expired. Please sign in again.';
                } else if (status === 403) {
                    message = 'You do not have permission to perform this action.';
                } else if (status === 404) {
                    message = 'The requested record was not found.';
                } else if (status >= 500) {
                    message = 'Something went wrong. Please try again.';
                } else {
                    message = fallback || 'Something went wrong. Please try again.';
                }
            }

            if (window.console) {
                console.error('AJAX request failed', status, xhr && xhr.responseText);
            }
            window.WimsNotify.error(message);
            return message;
        }
    };

    /**
     * Auto-dismiss success/info banners only. Errors and warnings stay until dismissed.
     */
    function autoDismissAlerts(delayMs) {
        delayMs = typeof delayMs === 'number' ? delayMs : 5000;
        if (!window.bootstrap || !bootstrap.Alert) {
            return;
        }

        document.querySelectorAll('.alert.alert-dismissible').forEach(function (el) {
            if (el.dataset.wimsAutoDismiss === '1' || el.dataset.wimsPersist === 'true') {
                return;
            }
            if (el.classList.contains('alert-danger') || el.classList.contains('alert-warning')) {
                return;
            }
            el.dataset.wimsAutoDismiss = '1';
            window.setTimeout(function () {
                if (!document.body.contains(el)) {
                    return;
                }
                var instance = bootstrap.Alert.getOrCreateInstance(el);
                instance.close();
            }, delayMs);
        });
    }

    $(function () {
        init(document);
        observeNewSelects();
        initServerToasts();
        window.WimsNotify.consumeFlash();
        autoDismissAlerts(5000);
    });
})(window, window.jQuery);
