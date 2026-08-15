// JS interop module for BlazTextEditor. One module instance serves all editors;
// per-surface state lives in the `states` map keyed by the surface element.

const states = new WeakMap();

const HIGHLIGHT_NAME = "blaztext-search";
const HIGHLIGHT_ACTIVE_NAME = "blaztext-search-active";

injectHighlightStyles();

export function init(el, dotnetRef) {
    const state = {
        dotnetRef,
        interceptKeys: new Set(),
        lastReported: "",
        searchRanges: [],
        selectionTimer: 0,
        onSelectionChange: null,
    };
    states.set(el, state);

    el.addEventListener("input", () => report(el));

    el.addEventListener("keydown", e => {
        if (state.interceptKeys.has(e.key)) {
            e.preventDefault();
            e.stopPropagation();
            state.dotnetRef.invokeMethodAsync("NotifyKeyInterceptedAsync", e.key);
        }
    });

    el.addEventListener("paste", e => {
        e.preventDefault();
        const clipboard = e.clipboardData;
        if (!clipboard) return;
        const html = clipboard.getData("text/html");
        if (html) {
            // Insert the sanitized *nodes*, never a re-serialized string — see sanitizeToFragment.
            insertFragmentAtCaret(el, sanitizeToFragment(html));
        } else {
            insertHtmlAtCaret(el, escapeHtml(clipboard.getData("text/plain")).replaceAll("\n", "<br>"));
        }
        report(el);
    });

    state.onSelectionChange = () => {
        clearTimeout(state.selectionTimer);
        state.selectionTimer = setTimeout(() => {
            const sel = window.getSelection();
            if (!sel || sel.rangeCount === 0 || !el.contains(sel.anchorNode)) return;
            state.dotnetRef.invokeMethodAsync(
                "NotifySelectionChangedAsync",
                textBeforeCaret(el),
                caretRect(el),
                sel.isCollapsed);
        }, 80);
    };
    document.addEventListener("selectionchange", state.onSelectionChange);
}

export function dispose(el) {
    const state = states.get(el);
    if (!state) return;
    document.removeEventListener("selectionchange", state.onSelectionChange);
    clearTimeout(state.selectionTimer);
    clearHighlights(el);
    states.delete(el);
}

// ---- content ----

// The document stores embedded images as src="blaztext:{id}"; the visible DOM uses
// data: URIs (carrying data-blaztext-id) so the browser can show them. getContent and
// setContent translate between the two representations.

export function getContent(el) {
    const clone = el.cloneNode(true);
    for (const img of clone.querySelectorAll("img[data-blaztext-id]")) {
        img.setAttribute("src", "blaztext:" + img.getAttribute("data-blaztext-id"));
    }
    return clone.innerHTML;
}

export function setContent(el, html, imageMap) {
    // Not sanitized: this is developer/document-supplied content (may contain <style>
    // blocks for email templates). Untrusted input paths (paste) sanitize separately.
    el.innerHTML = html ?? "";
    for (const img of el.querySelectorAll("img")) {
        const src = img.getAttribute("src") ?? "";
        if (src.startsWith("blaztext:")) {
            const id = src.substring("blaztext:".length);
            if (imageMap && imageMap[id]) {
                img.setAttribute("data-blaztext-id", id);
                img.setAttribute("src", imageMap[id]);
            }
        }
    }
    const state = states.get(el);
    if (state) state.lastReported = getContent(el);
}

export function getPlainText(el) {
    // Concatenated text nodes, matching how highlightRanges indexes the text.
    let text = "";
    const walker = document.createTreeWalker(el, NodeFilter.SHOW_TEXT);
    while (walker.nextNode()) text += walker.currentNode.nodeValue;
    return text;
}

export function insertHtml(el, html) {
    insertHtmlAtCaret(el, html);
    report(el);
}

export function replaceTextBeforeCaret(el, count, text) {
    el.focus();
    const sel = window.getSelection();
    if (!sel) return;
    for (let i = 0; i < count; i++) sel.modify("extend", "backward", "character");
    insertHtmlAtCaret(el, escapeHtml(text));
    report(el);
}

export function applyFormat(el, command, value) {
    el.focus();
    try { document.execCommand("styleWithCSS", false, "true"); } catch { /* not supported everywhere */ }
    document.execCommand(command, false, value ?? undefined);
    report(el);
}

export function focusEditor(el) {
    el.focus();
}

export function setInterceptKeys(el, keys) {
    const state = states.get(el);
    if (state) state.interceptKeys = new Set(keys);
}

// ---- search highlighting (CSS Custom Highlight API; no-op on unsupported browsers) ----

export function highlightRanges(el, ranges, activeIndex) {
    if (!CSS.highlights) return;
    clearHighlights(el);

    const state = states.get(el);
    const domRanges = [];
    for (const r of ranges) {
        const domRange = rangeFromTextOffsets(el, r.start, r.length);
        if (domRange) domRanges.push(domRange);
    }
    if (state) state.searchRanges = domRanges;
    if (domRanges.length === 0) return;

    CSS.highlights.set(HIGHLIGHT_NAME, new Highlight(...domRanges));
    if (activeIndex >= 0 && activeIndex < domRanges.length) {
        CSS.highlights.set(HIGHLIGHT_ACTIVE_NAME, new Highlight(domRanges[activeIndex]));
    }
}

export function clearHighlights(el) {
    if (!CSS.highlights) return;
    CSS.highlights.delete(HIGHLIGHT_NAME);
    CSS.highlights.delete(HIGHLIGHT_ACTIVE_NAME);
    const state = states.get(el);
    if (state) state.searchRanges = [];
}

export function scrollToHighlight(el, index) {
    const state = states.get(el);
    const range = state?.searchRanges[index];
    range?.startContainer?.parentElement?.scrollIntoView({ block: "nearest" });
}

// ---- internals ----

function report(el) {
    const state = states.get(el);
    if (!state) return;
    const html = getContent(el);
    if (html === state.lastReported) return;
    state.lastReported = html;
    state.dotnetRef.invokeMethodAsync("NotifyContentChangedAsync", html, textBeforeCaret(el), caretRect(el));
}

function textBeforeCaret(el) {
    const sel = window.getSelection();
    if (!sel || sel.rangeCount === 0 || !el.contains(sel.focusNode)) return "";
    const range = document.createRange();
    range.selectNodeContents(el);
    try {
        range.setEnd(sel.focusNode, sel.focusOffset);
    } catch {
        return "";
    }
    const text = range.toString();
    return text.length > 400 ? text.slice(-400) : text;
}

function caretRect(el) {
    const sel = window.getSelection();
    if (!sel || sel.rangeCount === 0 || !el.contains(sel.focusNode)) return null;
    const range = sel.getRangeAt(0).cloneRange();
    range.collapse(false);
    let rect = range.getBoundingClientRect();
    if (rect.top === 0 && rect.left === 0) {
        // Collapsed caret in an empty element has no rect; fall back to the container.
        rect = (sel.focusNode instanceof Element ? sel.focusNode : el).getBoundingClientRect();
    }
    return { top: rect.top, left: rect.left, bottom: rect.bottom };
}

function insertHtmlAtCaret(el, html) {
    insertAtCaret(el, range => range.createContextualFragment(html));
}

function insertFragmentAtCaret(el, fragment) {
    insertAtCaret(el, () => fragment);
}

function insertAtCaret(el, makeFragment) {
    el.focus();
    const sel = window.getSelection();
    let range = sel && sel.rangeCount > 0 && el.contains(sel.anchorNode) ? sel.getRangeAt(0) : null;
    if (!range) {
        range = document.createRange();
        range.selectNodeContents(el);
        range.collapse(false);
    }
    range.deleteContents();
    const fragment = makeFragment(range);
    const lastNode = fragment.lastChild;
    range.insertNode(fragment);
    if (lastNode && sel) {
        range.setStartAfter(lastNode);
        range.collapse(true);
        sel.removeAllRanges();
        sel.addRange(range);
    }
}

function rangeFromTextOffsets(el, start, length) {
    const walker = document.createTreeWalker(el, NodeFilter.SHOW_TEXT);
    let position = 0;
    let range = null;
    const end = start + length;
    while (walker.nextNode()) {
        const node = walker.currentNode;
        const nodeEnd = position + node.nodeValue.length;
        if (!range && start >= position && start < nodeEnd) {
            range = document.createRange();
            range.setStart(node, start - position);
        }
        if (range && end <= nodeEnd) {
            range.setEnd(node, end - position);
            return range;
        }
        position = nodeEnd;
    }
    return null;
}

// Attributes whose value is a URL. A browser strips whitespace, control characters and
// zero-width characters out of a URL before resolving its scheme, so the value has to be
// normalized the same way first — otherwise "java&#9;script:" walks past a startsWith check.
const URL_ATTRIBUTES = new Set([
    "href", "src", "xlink:href", "action", "formaction", "data", "poster", "background", "srcset", "ping",
]);

const DANGEROUS_SCHEMES = ["javascript:", "vbscript:", "data:text/html", "data:application/xhtml"];

function isDangerousUrl(value) {
    let normalized = "";
    for (const ch of value ?? "") {
        const code = ch.codePointAt(0);
        const isNoise = code <= 0x20 || code === 0x7f || (code >= 0x200b && code <= 0x200d) || code === 0xfeff;
        if (!isNoise) normalized += ch;
    }
    normalized = normalized.toLowerCase();
    return DANGEROUS_SCHEMES.some(scheme => normalized.startsWith(scheme));
}

// Attribute values that are URL *lists* rather than a single URL, with their separator.
// Checking one of these as a single string only ever inspects the first entry.
const URL_LIST_SEPARATORS = { srcset: ",", ping: /\s+/ };

function hasDangerousUrl(name, value) {
    const separator = URL_LIST_SEPARATORS[name];
    // isDangerousUrl only looks at the prefix, so a srcset descriptor needs no trimming.
    return separator
        ? value.split(separator).some(isDangerousUrl)
        : isDangerousUrl(value);
}

// `style` is not a URL, it is CSS that can carry URLs through url(). Scheme-checking those is
// defense in depth — modern browsers do not execute javascript: from CSS. Whether a sanitized
// document may fetch *remote* url() at all is a policy question, not something this closes.
function hasDangerousCss(value) {
    for (const match of value.matchAll(/url\(\s*(['"]?)([^'")]*)\1\s*\)/gi)) {
        if (isDangerousUrl(match[2])) return true;
    }
    return false;
}

// Returns sanitized nodes belonging to the live document — deliberately not a string.
// DOMParser parses with the scripting flag OFF; the live document has it ON, and the two
// disagree about <noscript>. Serializing back to a string and re-parsing it (as
// createContextualFragment would) lets markup that was inert here become live there, so the
// round trip is removed entirely rather than guarded. noscript/template are dropped anyway:
// template content is a separate fragment that querySelectorAll never descends into.
function sanitizeToFragment(html) {
    const doc = new DOMParser().parseFromString(html, "text/html");
    for (const node of doc.querySelectorAll(
        "script, style, link, meta, iframe, object, embed, form, input, button, base, noscript, template")) {
        node.remove();
    }
    for (const node of doc.body.querySelectorAll("*")) {
        for (const attr of [...node.attributes]) {
            const name = attr.name.toLowerCase();
            const dangerous = name.startsWith("on")
                || name === "srcdoc"
                || (name === "style" && hasDangerousCss(attr.value))
                || (URL_ATTRIBUTES.has(name) && hasDangerousUrl(name, attr.value));
            if (dangerous) {
                node.removeAttribute(attr.name);
            }
        }
    }

    const fragment = document.createDocumentFragment();
    for (const node of [...doc.body.childNodes]) {
        fragment.appendChild(document.importNode(node, true));
    }
    return fragment;
}

function escapeHtml(text) {
    const div = document.createElement("div");
    div.textContent = text ?? "";
    return div.innerHTML;
}

function injectHighlightStyles() {
    if (document.getElementById("blaztext-highlight-styles")) return;
    const style = document.createElement("style");
    style.id = "blaztext-highlight-styles";
    style.textContent = `
::highlight(${HIGHLIGHT_NAME}) { background-color: var(--blaztext-highlight-bg, #ffe58f); }
::highlight(${HIGHLIGHT_ACTIVE_NAME}) { background-color: var(--blaztext-highlight-active-bg, #ff9c6e); }`;
    document.head.appendChild(style);
}
