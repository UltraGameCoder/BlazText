// JS interop module for BlazTextEditor. One module instance serves all editors;
// per-surface state lives in the `states` map keyed by the surface element.

const states = new WeakMap();

// CSS.highlights is a document-global registry, so highlight names must be unique per
// editor — otherwise a search in one editor overwrites another editor's highlights, and
// disposing one editor deletes the other's.
// Scoped to the document, not the module: if this module is ever evaluated twice in one page
// (two Blazor roots, or a cache-busting query string) two module-level counters would both
// start at zero and hand out colliding names again.
function nextHighlightSequence() {
    const key = "__blazTextHighlightSequence";
    document[key] = (document[key] ?? 0) + 1;
    return document[key];
}

export function init(el, dotnetRef) {
    const highlightName = `blaztext-search-${nextHighlightSequence()}`;
    const state = {
        dotnetRef,
        interceptKeys: new Set(),
        lastReported: "",
        searchRanges: [],
        selectionTimer: 0,
        onSelectionChange: null,
        highlightName,
        highlightActiveName: `${highlightName}-active`,
        highlightStyleId: `${highlightName}-styles`,
    };
    states.set(el, state);
    injectHighlightStyles(state);

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
        const html = e.clipboardData.getData("text/html");
        const insert = html
            ? sanitizeHtml(html)
            : escapeHtml(e.clipboardData.getData("text/plain")).replaceAll("\n", "<br>");
        insertHtmlAtCaret(el, insert);
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
    document.getElementById(state.highlightStyleId)?.remove();
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

// setContent deliberately preserves <style> blocks for e-mail templates, but their text is not
// document text. Including it made a search for "color" report a hit that nothing could
// highlight or scroll to. Both the text and the range walk use this filter so offsets agree.
const TEXT_NODE_FILTER = {
    acceptNode(node) {
        const tag = node.parentElement?.tagName;
        return tag === "STYLE" || tag === "SCRIPT" ? NodeFilter.FILTER_REJECT : NodeFilter.FILTER_ACCEPT;
    },
};

function textWalker(el) {
    return document.createTreeWalker(el, NodeFilter.SHOW_TEXT, TEXT_NODE_FILTER);
}

export function getPlainText(el) {
    // Concatenated text nodes, matching how highlightRanges indexes the text.
    let text = "";
    const walker = textWalker(el);
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

// Returns how many ranges actually resolved, so the caller can tell that the DOM moved on
// rather than displaying a match count nothing on screen corresponds to.
export function highlightRanges(el, ranges, activeIndex) {
    const state = states.get(el);
    if (!CSS.highlights || !state) return 0;
    clearHighlights(el);

    const domRanges = resolveRanges(el, ranges);
    state.searchRanges = domRanges;

    const resolved = domRanges.filter(r => r !== null);
    if (resolved.length === 0) return 0;

    // Built incrementally rather than spread into the constructor: a document with more than
    // ~65k matches would blow the argument limit and surface as an unhandled JSException.
    const highlight = new Highlight();
    for (const range of resolved) highlight.add(range);
    CSS.highlights.set(state.highlightName, highlight);

    const active = activeIndex >= 0 && activeIndex < domRanges.length ? domRanges[activeIndex] : null;
    if (active) {
        CSS.highlights.set(state.highlightActiveName, new Highlight(active));
    }

    return resolved.length;
}

export function clearHighlights(el) {
    const state = states.get(el);
    if (!CSS.highlights || !state) return;
    CSS.highlights.delete(state.highlightName);
    CSS.highlights.delete(state.highlightActiveName);
    state.searchRanges = [];
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
    el.focus();
    const sel = window.getSelection();
    let range = sel && sel.rangeCount > 0 && el.contains(sel.anchorNode) ? sel.getRangeAt(0) : null;
    if (!range) {
        range = document.createRange();
        range.selectNodeContents(el);
        range.collapse(false);
    }
    range.deleteContents();
    const fragment = range.createContextualFragment(html);
    const lastNode = fragment.lastChild;
    range.insertNode(fragment);
    if (lastNode && sel) {
        range.setStartAfter(lastNode);
        range.collapse(true);
        sel.removeAllRanges();
        sel.addRange(range);
    }
}

// One walk for all ranges. Resolving them one at a time re-walked the whole text tree per
// match, and this runs on every input event — O(matches x nodes) froze the UI on a large
// template with a one-character query.
//
// A range that does not resolve keeps its slot as null: activeIndex and scrollToHighlight
// index into the caller's list, so the array has to stay aligned with it.
function resolveRanges(el, ranges) {
    const resolved = new Array(ranges.length).fill(null);
    const ordered = ranges
        .map((r, index) => ({ index, start: r.start, end: r.start + r.length }))
        .sort((a, b) => a.start - b.start);

    const walker = textWalker(el);
    let position = 0;
    let next = 0;
    const open = [];

    while (walker.nextNode()) {
        const node = walker.currentNode;
        const nodeEnd = position + node.nodeValue.length;

        while (next < ordered.length && ordered[next].start < nodeEnd) {
            const item = ordered[next++];
            if (item.start >= position) {
                const range = document.createRange();
                range.setStart(node, item.start - position);
                open.push({ item, range });
            }
        }

        for (let i = open.length - 1; i >= 0; i--) {
            if (open[i].item.end <= nodeEnd) {
                open[i].range.setEnd(node, open[i].item.end - position);
                resolved[open[i].item.index] = open[i].range;
                open.splice(i, 1);
            }
        }

        position = nodeEnd;
    }

    return resolved;
}

function sanitizeHtml(html) {
    const doc = new DOMParser().parseFromString(html, "text/html");
    for (const node of doc.querySelectorAll("script, style, link, meta, iframe, object, embed, form, input, button, base")) {
        node.remove();
    }
    for (const node of doc.body.querySelectorAll("*")) {
        for (const attr of [...node.attributes]) {
            const name = attr.name.toLowerCase();
            if (name.startsWith("on") || ((name === "href" || name === "src") && attr.value.trim().toLowerCase().startsWith("javascript:"))) {
                node.removeAttribute(attr.name);
            }
        }
    }
    return doc.body.innerHTML;
}

function escapeHtml(text) {
    const div = document.createElement("div");
    div.textContent = text ?? "";
    return div.innerHTML;
}

function injectHighlightStyles(state) {
    // One style element per editor, removed again on dispose, so the highlight names and
    // their rules have exactly the same lifetime.
    const style = document.createElement("style");
    style.id = state.highlightStyleId;
    style.textContent = `
::highlight(${state.highlightName}) { background-color: var(--blaztext-highlight-bg, #ffe58f); }
::highlight(${state.highlightActiveName}) { background-color: var(--blaztext-highlight-active-bg, #ff9c6e); }`;
    document.head.appendChild(style);
}
