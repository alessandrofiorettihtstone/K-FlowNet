// Please see documentation at https://learn.microsoft.com/aspnet/core/client-side/bundling-and-minification
// for details on configuring this project to bundle and minify static web assets.

// Write your JavaScript code.

(() => {
    const TRANSITION_DELAY_MS = 220;
    const TRANSITION_ROOT_SELECTOR = "[data-page-transition-root]";
    const nativeFormSubmit = HTMLFormElement.prototype.submit;
    let isLeaving = false;

    function getTransitionRoot() {
        return document.querySelector(TRANSITION_ROOT_SELECTOR) ?? document.getElementById("page-content");
    }

    function getAnimationDelay(delay) {
        return typeof delay === "number" ? delay : TRANSITION_DELAY_MS;
    }

    function isSameOriginUrl(url) {
        return url.origin === window.location.origin && /^(http|https):$/.test(url.protocol);
    }

    function shouldHandleLink(link, event) {
        if (!(link instanceof HTMLAnchorElement)) {
            return false;
        }

        if (link.dataset.pageTransition === "none") {
            return false;
        }

        if (
            event.defaultPrevented ||
            event.button !== 0 ||
            event.metaKey ||
            event.ctrlKey ||
            event.shiftKey ||
            event.altKey
        ) {
            return false;
        }

        const rawHref = link.getAttribute("href");
        if (!rawHref || rawHref.startsWith("javascript:")) {
            return false;
        }

        if (link.target && link.target.toLowerCase() === "_blank") {
            return false;
        }

        if (
            link.hasAttribute("download") ||
            link.hasAttribute("data-bs-toggle") ||
            link.hasAttribute("data-toggle")
        ) {
            return false;
        }

        const url = new URL(link.href, window.location.href);
        if (!isSameOriginUrl(url)) {
            return false;
        }

        return !(
            url.pathname === window.location.pathname &&
            url.search === window.location.search &&
            url.hash
        );
    }

    function shouldHandleForm(form) {
        if (!(form instanceof HTMLFormElement)) {
            return false;
        }

        if (form.dataset.pageTransition === "none") {
            return false;
        }

        return !(form.target && form.target.toLowerCase() === "_blank");
    }

    function isFormValid(form) {
        if (
            typeof form.checkValidity === "function" &&
            !form.noValidate &&
            !form.checkValidity()
        ) {
            if (typeof form.reportValidity === "function") {
                form.reportValidity();
            }

            return false;
        }

        if (
            window.jQuery &&
            typeof window.jQuery(form).valid === "function" &&
            !form.noValidate &&
            !window.jQuery(form).valid()
        ) {
            return false;
        }

        return true;
    }

    function playEntryTransition() {
        const root = getTransitionRoot();
        if (!(root instanceof HTMLElement)) {
            document.documentElement.classList.remove("is-page-transitioning");
            isLeaving = false;
            return;
        }

        document.documentElement.classList.remove("is-page-transitioning");
        root.classList.remove("page-transition-leaving");
        root.classList.remove("page-entering");

        window.requestAnimationFrame(() => {
            root.classList.add("page-entering");
            root.addEventListener(
                "animationend",
                () => root.classList.remove("page-entering"),
                { once: true }
            );
        });

        isLeaving = false;
    }

    function startTransition(callback, options = {}) {
        const root = getTransitionRoot();
        if (!(root instanceof HTMLElement)) {
            callback();
            return;
        }

        if (isLeaving) {
            return;
        }

        isLeaving = true;
        document.documentElement.classList.add("is-page-transitioning");
        root.classList.remove("page-entering");
        root.classList.add("page-transition-leaving");

        window.setTimeout(callback, getAnimationDelay(options.delay));
    }

    function restoreAttribute(element, attributeName, previousValue) {
        if (previousValue === null) {
            element.removeAttribute(attributeName);
            return;
        }

        element.setAttribute(attributeName, previousValue);
    }

    function applySubmitterContext(form, submitter) {
        if (!(submitter instanceof HTMLButtonElement || submitter instanceof HTMLInputElement)) {
            return () => { };
        }

        const cleanup = [];

        if (submitter.hasAttribute("formaction")) {
            const previousAction = form.getAttribute("action");
            form.setAttribute("action", submitter.formAction);
            cleanup.push(() => restoreAttribute(form, "action", previousAction));
        }

        if (submitter.hasAttribute("formmethod")) {
            const previousMethod = form.getAttribute("method");
            form.setAttribute("method", submitter.formMethod);
            cleanup.push(() => restoreAttribute(form, "method", previousMethod));
        }

        if (submitter.hasAttribute("formenctype")) {
            const previousEnctype = form.getAttribute("enctype");
            form.setAttribute("enctype", submitter.formEnctype);
            cleanup.push(() => restoreAttribute(form, "enctype", previousEnctype));
        }

        if (submitter.hasAttribute("formtarget")) {
            const previousTarget = form.getAttribute("target");
            form.setAttribute("target", submitter.formTarget);
            cleanup.push(() => restoreAttribute(form, "target", previousTarget));
        }

        if (submitter.hasAttribute("formnovalidate")) {
            const previousNoValidate = form.noValidate;
            form.noValidate = true;
            cleanup.push(() => {
                form.noValidate = previousNoValidate;
            });
        }

        if (submitter.name) {
            const hiddenField = document.createElement("input");
            hiddenField.type = "hidden";
            hiddenField.name = submitter.name;
            hiddenField.value = submitter.value;
            form.appendChild(hiddenField);

            cleanup.push(() => {
                hiddenField.remove();
            });
        }

        return () => {
            cleanup.reverse().forEach((step) => step());
        };
    }

    function commitFormSubmission(form, options = {}) {
        const restoreSubmitterContext = applySubmitterContext(form, options.submitter);

        try {
            nativeFormSubmit.call(form);
        } finally {
            restoreSubmitterContext();
        }
    }

    function submitForm(form, options = {}) {
        if (!shouldHandleForm(form)) {
            commitFormSubmission(form, options);
            return;
        }

        if (!options.skipValidation && !isFormValid(form)) {
            return;
        }

        startTransition(() => commitFormSubmission(form, options), options);
    }

    function navigate(url, options = {}) {
        if (!url || isLeaving) {
            return;
        }

        startTransition(() => {
            window.location.href = url;
        }, options);
    }

    document.addEventListener(
        "click",
        (event) => {
            if (!(event.target instanceof Element)) {
                return;
            }

            const link = event.target.closest("a");
            if (!shouldHandleLink(link, event)) {
                return;
            }

            event.preventDefault();
            navigate(link.href);
        },
        true
    );

    document.addEventListener(
        "submit",
        (event) => {
            const form = event.target;
            if (!(form instanceof HTMLFormElement)) {
                return;
            }

            if (!shouldHandleForm(form) || !isFormValid(form)) {
                return;
            }

            event.preventDefault();
            submitForm(form, { submitter: event.submitter });
        }
    );

    HTMLFormElement.prototype.submit = function () {
        submitForm(this, { skipValidation: true });
    };

    window.PageTransition = {
        navigate,
        start: startTransition,
        submitForm
    };

    if (document.readyState === "loading") {
        document.addEventListener("DOMContentLoaded", playEntryTransition, { once: true });
    } else {
        playEntryTransition();
    }

    window.addEventListener("pageshow", playEntryTransition);
})();

(() => {
    const showKFlowToasts = () => {
        if (typeof bootstrap === "undefined") {
            return;
        }

        document.querySelectorAll("[data-kflow-toast]").forEach(toastElement => {
            bootstrap.Toast.getOrCreateInstance(toastElement, {
                autohide: true,
                delay: Number.parseInt(toastElement.dataset.bsDelay || "3800", 10)
            }).show();
        });
    };

    if (document.readyState === "loading") {
        document.addEventListener("DOMContentLoaded", showKFlowToasts, { once: true });
    } else {
        showKFlowToasts();
    }
})();

window.setupKFlowScanner = function (modalId, readerId, inputName, buttonId) {
    const modalElement = document.getElementById(modalId);
    if (!modalElement || typeof Html5Qrcode === "undefined") {
        return;
    }

    let scanner;
    let audioContext;

    const playBeep = () => {
        try {
            audioContext ??= new (window.AudioContext || window.webkitAudioContext)();
            const oscillator = audioContext.createOscillator();
            oscillator.type = "sine";
            oscillator.frequency.setValueAtTime(1000, audioContext.currentTime);
            oscillator.connect(audioContext.destination);
            oscillator.start();
            oscillator.stop(audioContext.currentTime + 0.18);
            if (navigator.vibrate) {
                navigator.vibrate(120);
            }
        } catch {
        }
    };

    const stopScanner = () => {
        if (!scanner) {
            return;
        }

        scanner.stop().catch(() => { });
    };

    const startScanner = () => {
        const reader = document.getElementById(readerId);
        if (!reader) {
            return;
        }

        scanner ??= new Html5Qrcode(readerId);
        const size = Math.min(reader.offsetWidth || 300, 300);
        scanner.start(
            { facingMode: "environment" },
            { fps: 15, qrbox: { width: size, height: size } },
            decodedText => {
                const input = document.querySelector(`[name="${inputName}"]`);
                if (input) {
                    input.value = decodedText;
                }

                playBeep();
                stopScanner();
                bootstrap.Modal.getInstance(modalElement)?.hide();
                document.getElementById(buttonId)?.click();
            }
        ).catch(() => { });
    };

    modalElement.addEventListener("shown.bs.modal", () => window.setTimeout(startScanner, 150));
    modalElement.addEventListener("hidden.bs.modal", stopScanner);
};

window.setupKFlowAutocomplete = function (options) {
    const input = document.querySelector(options.inputSelector);
    if (!(input instanceof HTMLInputElement)) {
        return;
    }

    const wrapper = input.closest(".kflow-autocomplete-wrapper");
    if (!(wrapper instanceof HTMLElement)) {
        return;
    }

    const minLength = options.minLength ?? 2;
    const suggestionsId = options.suggestionsId || `${input.id || input.name}-suggestions`;
    let timer;

    input.setAttribute("autocomplete", "off");

    const removeSuggestions = () => {
        document.getElementById(suggestionsId)?.remove();
    };

    const getSelectedValue = item => {
        if (typeof options.getSelectedValue === "function") {
            return options.getSelectedValue(item);
        }

        return item;
    };

    const renderSuggestions = items => {
        removeSuggestions();

        const list = document.createElement("div");
        list.id = suggestionsId;
        list.className = "list-group shadow kflow-autocomplete-suggestions";

        if (items.length === 0) {
            const emptyItem = document.createElement("div");
            emptyItem.className = "list-group-item text-muted";
            emptyItem.textContent = options.emptyText || "Nessun risultato trovato";
            list.append(emptyItem);
            wrapper.append(list);
            return;
        }

        items.forEach(item => {
            const button = document.createElement("button");
            button.type = "button";
            button.className = "list-group-item list-group-item-action text-start";
            button.textContent = item;
            button.addEventListener("mousedown", event => {
                event.preventDefault();
                input.value = getSelectedValue(item);
                removeSuggestions();
                input.dispatchEvent(new Event("change", { bubbles: true }));

                if (typeof options.onSelect === "function") {
                    options.onSelect(item, input);
                }

                if (options.submitOnSelect) {
                    input.closest("form")?.requestSubmit();
                }
            });
            list.append(button);
        });

        wrapper.append(list);
    };

    input.addEventListener("input", () => {
        window.clearTimeout(timer);
        const term = input.value.trim();
        removeSuggestions();

        if (term.length < minLength) {
            return;
        }

        timer = window.setTimeout(async () => {
            const url = new URL(options.url, window.location.origin);
            url.searchParams.set("handler", options.handler || "SuggerimentiRicerca");
            url.searchParams.set("prefixText", term);
            url.searchParams.set("tipoRicerca", options.tipoRicerca || "");

            try {
                const response = await fetch(url);
                if (!response.ok) {
                    removeSuggestions();
                    return;
                }

                renderSuggestions(await response.json());
            } catch {
                removeSuggestions();
            }
        }, options.delay ?? 180);
    });

    input.addEventListener("keydown", event => {
        if (event.key === "Escape") {
            removeSuggestions();
        }
    });

    document.addEventListener("click", event => {
        if (!wrapper.contains(event.target)) {
            removeSuggestions();
        }
    });
};

window.setupKFlowFilterClear = function (scope) {
    const root = scope || document;

    root.querySelectorAll("[data-clear-target]").forEach(button => {
        if (button.dataset.kflowClearBound === "1") {
            return;
        }

        button.dataset.kflowClearBound = "1";
        button.addEventListener("click", () => {
            const selector = button.dataset.clearTarget || "";
            const targets = selector
                ? [...document.querySelectorAll(selector)]
                : [];

            targets.forEach(target => {
                if (target instanceof HTMLInputElement && (target.type === "checkbox" || target.type === "radio")) {
                    target.checked = false;
                    target.dispatchEvent(new Event("change", { bubbles: true }));
                    return;
                }

                if (target instanceof HTMLInputElement || target instanceof HTMLSelectElement || target instanceof HTMLTextAreaElement) {
                    target.value = "";
                    target.dispatchEvent(new Event("change", { bubbles: true }));
                }
            });

            if (button.dataset.clearSubmit !== "false") {
                button.closest("form")?.requestSubmit();
            }
        });
    });
};
