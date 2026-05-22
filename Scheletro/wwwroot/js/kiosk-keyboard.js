window.KioskKeyboard = (function () {
    let currentInput = null;
    let currentType = "text";
    let currentValue = "";
    let bsKeyboard = null;
    let isShiftActive = false;
    let cursorPosition = null;

    const alphaRows = [
        ["q", "w", "e", "r", "t", "y", "u", "i", "o", "p"],
        ["a", "s", "d", "f", "g", "h", "j", "k", "l"]
    ];

    const lastAlphaRow = ["z", "x", "c", "v", "b", "n", "m"];

    const numberOnlyRows = [
        ["1", "2", "3"],
        ["4", "5", "6"],
        ["7", "8", "9"],
        ["0", "⌫"]
    ];

    const fullNumbers = ["1", "2", "3", "4", "5", "6", "7", "8", "9", "0", "⌫"];
    const emailQuickKeys = ["@", ".", "_", "-", ".com", ".it", "gmail.com"];

    function getOffcanvasElement() {
        return document.getElementById("kioskKeyboard");
    }

    function getPreviewElement() {
        return document.getElementById("keyboardPreview");
    }

    function getKeysContainer() {
        return document.getElementById("keyboardKeys");
    }

    function getSafeCursorPosition() {
        if (!currentValue) return 0;
        if (typeof cursorPosition !== "number") return currentValue.length;
        return Math.max(0, Math.min(cursorPosition, currentValue.length));
    }

    function syncCursorFromInput() {
        if (!currentInput) {
            cursorPosition = currentValue.length;
            return;
        }

        if (typeof currentInput.selectionStart === "number") {
            cursorPosition = currentInput.selectionStart;
        } else {
            cursorPosition = currentValue.length;
        }
    }

    function syncInputSelection() {
        if (!currentInput) return;

        try {
            const pos = getSafeCursorPosition();
            if (typeof currentInput.setSelectionRange === "function") {
                currentInput.setSelectionRange(pos, pos);
            }
        } catch (e) {
            // alcuni input readonly/tipi particolari possono lanciare eccezioni
        }
    }

    function updatePreview() {
        const preview = getPreviewElement();
        if (!preview) return;

        const pos = getSafeCursorPosition();
        const before = currentValue.slice(0, pos);
        const after = currentValue.slice(pos);

        preview.innerHTML = "";

        const beforeNode = document.createTextNode(before);
        const cursorNode = document.createElement("span");
        cursorNode.className = "keyboard-cursor";
        const afterNode = document.createTextNode(after);

        preview.appendChild(beforeNode);
        preview.appendChild(cursorNode);
        preview.appendChild(afterNode);
    }

    function syncInput() {
        if (!currentInput) return;

        currentInput.value = currentValue;
        currentInput.dispatchEvent(new Event("input", { bubbles: true }));
        currentInput.dispatchEvent(new Event("change", { bubbles: true }));

        syncInputSelection();
    }

    function getKeyboardInputs() {
        return Array.from(document.querySelectorAll("[data-keyboard]"))
            .filter(function (el) {
                if (el.disabled) return false;
                const visible = (el.offsetParent !== null) || (el.getClientRects && el.getClientRects().length > 0);
                return visible;
            });
    }

    function focusNextInput() {
        if (!currentInput) return false;

        const inputs = getKeyboardInputs();
        const currentIndex = inputs.indexOf(currentInput);

        if (currentIndex === -1) return false;

        const nextInput = inputs[currentIndex + 1];
        if (!nextInput) return false;

        open(nextInput, nextInput.dataset.keyboard || "text");
        return true;
    }

    function addTouchFeedback(button) {
        button.classList.add("active-touch");
        setTimeout(function () {
            button.classList.remove("active-touch");
        }, 140);
    }

    function transformAlphaKey(key) {
        if (key.length !== 1) return key;
        if (!/[a-z]/i.test(key)) return key;
        return isShiftActive ? key.toUpperCase() : key.toLowerCase();
    }

    function insertAtCursor(text) {
        const pos = getSafeCursorPosition();
        currentValue = currentValue.slice(0, pos) + text + currentValue.slice(pos);
        cursorPosition = pos + text.length;
        updatePreview();
        syncInput();
    }

    function moveCursorLeft() {
        cursorPosition = Math.max(0, getSafeCursorPosition() - 1);
        updatePreview();
        syncInputSelection();
    }

    function moveCursorRight() {
        cursorPosition = Math.min(currentValue.length, getSafeCursorPosition() + 1);
        updatePreview();
        syncInputSelection();
    }

    function toggleShift() {
        isShiftActive = !isShiftActive;
        renderKeyboard();
    }

    function createButton(text, onClick, extraStyle, extraClassName) {
        const button = document.createElement("button");
        button.type = "button";
        button.className = "keyboard-key btn";

        if (text === "⌫") button.classList.add("backspace-key");
        if (text === "⇧") button.classList.add("shift-key");
        if (text === "←" || text === "→") button.classList.add("arrow-key");
        if (text === "⇧" && isShiftActive) button.classList.add("shift-active");

        if (extraClassName) {
            button.className += " " + extraClassName;
        }

        if (text === "spazio") {
            button.textContent = "Spazio";
            button.dataset.keyValue = " ";
        } else {
            button.textContent = text;
        }

        if (extraStyle) {
            Object.assign(button.style, extraStyle);
        }

        button.addEventListener("pointerdown", function (event) {
            event.stopPropagation();
        });

        button.addEventListener("click", function (event) {
            event.stopPropagation();
            addTouchFeedback(button);
            const keyToSend = button.dataset.keyValue !== undefined ? button.dataset.keyValue : text;
            onClick(keyToSend);
        });

        button.addEventListener("touchstart", function () {
            button.classList.add("active-touch");
        }, { passive: true });

        button.addEventListener("touchend", function () {
            setTimeout(function () {
                button.classList.remove("active-touch");
            }, 120);
        }, { passive: true });

        button.addEventListener("touchcancel", function () {
            button.classList.remove("active-touch");
        }, { passive: true });

        return button;
    }

    function createRow(keys, clickHandler, buttonStyle) {
        const row = document.createElement("div");
        row.className = "keyboard-row";

        keys.forEach(function (key) {
            row.appendChild(
                createButton(
                    key,
                    function (k) {
                        if (key === "⌫") {
                            backspace();
                        } else if (key === "⇧") {
                            toggleShift();
                        } else if (key === "←") {
                            moveCursorLeft();
                        } else if (key === "→") {
                            moveCursorRight();
                        } else {
                            clickHandler(k);
                        }
                    },
                    buttonStyle || null
                )
            );
        });

        return row;
    }

    function renderNumberKeyboard() {
        const container = getKeysContainer();
        if (!container) return;

        container.innerHTML = "";

        numberOnlyRows.forEach(function (row) {
            container.appendChild(
                createRow(row, appendKey, {
                    minWidth: "60px",
                    height: "52px",
                    fontSize: "1rem"
                })
            );
        });

        container.appendChild(
            createRow(["←", "→"], function () { }, {
                minWidth: "60px",
                height: "48px",
                fontSize: "1rem"
            })
        );
    }

    function renderTextKeyboard() {
        const container = getKeysContainer();
        if (!container) return;

        container.innerHTML = "";

        container.appendChild(
            createRow(fullNumbers, appendKey, {
                minWidth: "50px",
                height: "48px",
                fontSize: "0.98rem"
            })
        );

        alphaRows.forEach(function (row) {
            container.appendChild(
                createRow(
                    row.map(transformAlphaKey),
                    appendKey,
                    {
                        minWidth: "50px",
                        height: "48px",
                        fontSize: "0.98rem"
                    }
                )
            );
        });

        container.appendChild(
            createRow(
                ["⇧"].concat(lastAlphaRow.map(transformAlphaKey)).concat(["←", "→"]),
                appendKey,
                {
                    minWidth: "50px",
                    height: "48px",
                    fontSize: "0.98rem"
                }
            )
        );

        const extraRow = document.createElement("div");
        extraRow.className = "keyboard-row";

        if (currentType === "email") {
            emailQuickKeys.forEach(function (key) {
                extraRow.appendChild(
                    createButton(
                        key,
                        function (k) {
                            insertAtCursor(k);
                        },
                        {
                            minWidth: key.length > 4 ? "92px" : "64px",
                            height: "46px",
                            fontSize: "0.92rem"
                        }
                    )
                );
            });
        } else {
            extraRow.appendChild(
                createButton(
                    "spazio",
                    function (k) {
                        insertAtCursor(k);
                    },
                    {
                        minWidth: "210px",
                        height: "46px",
                        fontSize: "0.98rem"
                    }
                )
            );
        }

        container.appendChild(extraRow);
    }

    function renderKeyboard() {
        if (currentType === "number") {
            renderNumberKeyboard();
        } else {
            renderTextKeyboard();
        }

        updatePreview();
    }

    function ensureOffcanvasInstance() {
        const el = getOffcanvasElement();
        if (!el) return null;

        bsKeyboard = bootstrap.Offcanvas.getOrCreateInstance(el);

        if (!el.dataset.keyboardHideBound) {
            el.addEventListener("hide.bs.offcanvas", function () {
                const active = document.activeElement;
                if (active && el.contains(active)) {
                    active.blur();
                }
            });

            el.addEventListener("hidden.bs.offcanvas", function () {
                currentInput = null;
                currentType = "text";
                currentValue = "";
                cursorPosition = null;
                isShiftActive = false;
            });

            el.dataset.keyboardHideBound = "true";
        }

        if (!el.dataset.keyboardOutsideBound) {
            document.addEventListener("pointerdown", function (event) {
                const keyboardEl = getOffcanvasElement();
                if (!keyboardEl || !keyboardEl.classList.contains("show")) return;

                const clickedInsideKeyboard = keyboardEl.contains(event.target);
                const clickedInput = event.target.closest("[data-keyboard]");

                if (!clickedInsideKeyboard && !clickedInput) {
                    close();
                }
            });

            el.dataset.keyboardOutsideBound = "true";
        }

        return bsKeyboard;
    }

    function open(input, type) {
        if (!input) return;

        currentInput = input;
        currentType = type || input.dataset.keyboard || "text";
        currentValue = input.value || "";
        isShiftActive = false;

        if (typeof input.selectionStart === "number") {
            cursorPosition = input.selectionStart;
        } else {
            cursorPosition = currentValue.length;
        }

        renderKeyboard();

        const instance = ensureOffcanvasInstance();
        if (instance) {
            instance.show();
        }

        setTimeout(function () {
            syncInputSelection();
        }, 50);
    }

    function close() {
        const instance = ensureOffcanvasInstance();
        if (!instance) return;

        const keyboardEl = getOffcanvasElement();
        const active = document.activeElement;

        if (active && keyboardEl && keyboardEl.contains(active)) {
            active.blur();
        }

        instance.hide();
    }

    function next() {
        syncInput();
        focusNextInput();
    }

    function confirm() {
        if (!currentInput) {
            const active = document.activeElement;
            if (active && active.dataset && active.dataset.keyboard) {
                currentInput = active;
                currentType = active.dataset.keyboard || "text";
                currentValue = active.value || "";
                cursorPosition = currentValue.length;
            }
        }

        syncInput();

        const form = currentInput ? (currentInput.form || currentInput.closest("form")) : null;

        close();

        setTimeout(function () {
            if (form) {
                const submitButton = form.querySelector('button[type="submit"], input[type="submit"]');
                if (submitButton) {
                    submitButton.click();
                } else if (typeof form.requestSubmit === "function") {
                    form.requestSubmit();
                } else {
                    form.submit();
                }
            }
        }, 120);
    }

    function appendKey(key) {
        insertAtCursor(key);
    }

    function backspace() {
        const pos = getSafeCursorPosition();

        if (pos === 0) return;

        currentValue = currentValue.slice(0, pos - 1) + currentValue.slice(pos);
        cursorPosition = pos - 1;
        updatePreview();
        syncInput();
    }

    function clear() {
        currentValue = "";
        cursorPosition = 0;
        updatePreview();
        syncInput();
    }

    function bindInputs(root) {
        const scope = root || document;
        const inputs = scope.querySelectorAll("[data-keyboard]");

        inputs.forEach(function (input) {
            if (input.dataset.keyboardBound === "true") return;

            if (!input.hasAttribute("tabindex")) {
                input.setAttribute("tabindex", "0");
            }

            input.addEventListener("click", function () {
                open(input, input.dataset.keyboard);
            });

            input.addEventListener("focus", function () {
                currentInput = input;
                currentType = input.dataset.keyboard || "text";
                currentValue = input.value || "";
                syncCursorFromInput();
                updatePreview();
            });

            input.addEventListener("input", function () {
                if (currentInput !== input) {
                    currentInput = input;
                }

                currentType = input.dataset.keyboard || "text";
                currentValue = input.value || "";
                syncCursorFromInput();
                updatePreview();
            });

            input.addEventListener("keyup", function () {
                if (currentInput !== input) {
                    currentInput = input;
                }

                currentType = input.dataset.keyboard || "text";
                currentValue = input.value || "";
                syncCursorFromInput();
                updatePreview();
            });

            input.addEventListener("mouseup", function () {
                if (currentInput !== input) {
                    currentInput = input;
                }

                currentType = input.dataset.keyboard || "text";
                currentValue = input.value || "";
                syncCursorFromInput();
                updatePreview();
            });

            input.dataset.keyboardBound = "true";
        });
    }
    document.addEventListener("DOMContentLoaded", function () {
        bindInputs();
    });

    return {
        open: open,
        close: close,
        next: next,
        confirm: confirm,
        appendKey: appendKey,
        backspace: backspace,
        clear: clear,
        bindInputs: bindInputs
    };
})();