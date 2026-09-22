// @ts-nocheck -- Standalone Node harness; no Node typings are installed in this VSIX solution.
// Dependency-free behavioral coverage of the native preview-content.js asset.
// Run from the repository root: node --test test\preview-content-script.test.cjs
const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
const vm = require('node:vm');
const { test } = require('node:test');
const script = fs.readFileSync(process.env.MARKDOWN_PREVIEW_SCRIPT ||
    path.resolve(__dirname, '..', 'src', 'Margin', 'preview-content.js'), 'utf8');

class Events {
    constructor() { this.listeners = new Map(); }
    addEventListener(name, fn, options) {
        const listeners = this.listeners.get(name) || [];
        listeners.push({ fn, once: options && options.once });
        this.listeners.set(name, listeners);
    }
    fire(name, target = this) {
        for (const listener of [...(this.listeners.get(name) || [])]) {
            listener.fn({ target });
            if (listener.once) this.listeners.set(name, this.listeners.get(name).filter(x => x !== listener));
        }
    }
}
class Node extends Events {
    constructor(tag, attributes = {}, text = '') {
        super();
        this.nodeType = tag === '#text' ? 3 : tag === '#comment' ? 8 : tag === '#fragment' ? 11 : 1;
        this.tagName = tag.toUpperCase();
        this.attributes = attributes;
        this.nodeValue = text;
        this.childNodes = [];
        this.parentNode = null;
    }
    get firstChild() { return this.childNodes[0] || null; }
    get nextSibling() { return this.parentNode?.childNodes[this.parentNode.childNodes.indexOf(this) + 1] || null; }
    appendChild(node) { return this.insertBefore(node, null); }
    insertBefore(node, cursor) {
        if (node === cursor) return node;
        if (node.parentNode) node.parentNode.removeChild(node);
        const index = cursor ? this.childNodes.indexOf(cursor) : this.childNodes.length;
        assert.ok(index >= 0);
        this.childNodes.splice(index, 0, node);
        node.parentNode = this;
        return node;
    }
    removeChild(node) {
        const index = this.childNodes.indexOf(node);
        assert.ok(index >= 0);
        this.childNodes.splice(index, 1);
        node.parentNode = null;
        return node;
    }
    setAttribute(name, value) { this.attributes[name] = value; }
    get outerHTML() {
        const attrs = Object.entries(this.attributes).map(([k, v]) => ` ${k}="${v}"`).join('');
        return `<${this.tagName.toLowerCase()}${attrs}>${this.innerHTML}</${this.tagName.toLowerCase()}>`;
    }
    get innerHTML() { return this.childNodes.map(x => x.nodeType === 1 ? x.outerHTML : x.nodeType === 8 ? `<!--${x.nodeValue}-->` : x.nodeValue).join(''); }
    set innerHTML(value) {
        for (const child of [...this.childNodes]) this.removeChild(child);
        for (const child of [...parse(value).childNodes]) this.appendChild(child);
    }
    matches(selector) {
        if (this.nodeType !== 1) return false;
        if (selector.includes(',')) return selector.split(',').some(x => this.matches(x.trim()));
        if (selector === '[class*="language-"]') return (this.attributes.class || '').includes('language-');
        if (selector.startsWith('.')) return (this.attributes.class || '').split(' ').includes(selector.slice(1));
        if (selector.endsWith('[href]')) return this.tagName === selector.slice(0, -6).toUpperCase() && 'href' in this.attributes;
        return this.tagName === selector.toUpperCase();
    }
    querySelectorAll(selector) {
        return this.childNodes.flatMap(x => [...(x.matches(selector) ? [x] : []), ...x.querySelectorAll(selector)]);
    }
    querySelector(selector) { return this.querySelectorAll(selector)[0] || null; }
    closest(selector) { return this.matches(selector) ? this : this.parentNode?.closest(selector); }
}
function parse(html) {
    const root = new Node('#fragment');
    const stack = [root];
    for (const token of html.match(/<!--[\s\S]*?-->|<[^>]+>|[^<]+/g) || []) {
        if (token.startsWith('<!--')) stack.at(-1).appendChild(new Node('#comment', {}, token.slice(4, -3)));
        else if (token.startsWith('</')) stack.pop();
        else if (token.startsWith('<')) {
            const tag = token.match(/^<(\w+)/)[1];
            const attrs = Object.fromEntries([...token.matchAll(/([\w-]+)="([^"]*)"/g)].map(x => [x[1], x[2]]));
            const node = new Node(tag, attrs);
            stack.at(-1).appendChild(node);
            if (!token.endsWith('/>')) stack.push(node);
        } else stack.at(-1).appendChild(new Node('#text', {}, token));
    }
    return root;
}
function environment(html = '', libraries = {}, readyState = 'complete') {
    const container = new Node('main');
    container.innerHTML = html;
    const document = new Events();
    document.readyState = readyState;
    document.getElementById = () => container;
    document.scripts = [];
    document.head = new Node('head');
    const append = document.head.appendChild.bind(document.head);
    document.head.appendChild = node => { document.scripts.push(node); return append(node); };
    document.createElement = tag => {
        const node = new Node(tag);
        if (tag === 'template') Object.defineProperty(node, 'innerHTML', { set(value) { this.content = parse(value); } });
        return node;
    };
    const errors = [];
    const messages = [];
    const window = Object.assign(new Events(), libraries, { chrome: { webview: { postMessage: x => messages.push(x) } } });
    vm.runInNewContext(script, { window, document, console: { error: (...args) => errors.push(args) }, setTimeout });
    return { window, document, container, errors, messages,
        update: (html, theme = 'dark', requestId) => window.__updateMarkdownPreview(html, theme, requestId),
        loaded(node, error = false) { document.fire(error ? 'error' : 'load', node); node.fire(error ? 'error' : 'load'); } };
}
const settle = async () => { for (let i = 0; i < 12; i++) await new Promise(resolve => setImmediate(resolve)); };
const deferred = () => { let resolve, reject; const promise = new Promise((a, b) => { resolve = a; reject = b; }); return { promise, resolve, reject }; };

test('original signatures preserve mutated nodes, duplicates, text, comments and reordering', async () => {
    const env = environment('<p>A</p><p>B</p><p>A</p> text<!--note-->');
    const [first, second, duplicate, text, comment] = env.container.childNodes;
    first.innerHTML = '<span>library mutation</span>';
    assert.equal(env.update('<p>A</p><p>A</p><!--note--> text<p>B</p>'), true);
    await settle();
    assert.deepEqual(env.container.childNodes, [first, duplicate, comment, text, second]);
    assert.match(first.innerHTML, /library mutation/);
    assert.deepEqual(env.errors, []);
});

test('tables get stable horizontal scroll containers', async () => {
    const env = environment('<p>before</p><table><tbody><tr><td>wide</td></tr></tbody></table><p>after</p>');
    env.window.__initializeMarkdownPreview('dark');
    await settle();
    const wrapper = env.container.childNodes[1];
    assert.equal(wrapper.attributes.class, 'markdown-table-wrapper');
    assert.equal(wrapper.firstChild.tagName, 'TABLE');
    env.update('<p>before</p><table><tbody><tr><td>wide</td></tr></tbody></table><p>after</p>');
    await settle();
    assert.equal(env.container.childNodes[1], wrapper);
    assert.equal(wrapper.querySelectorAll('.markdown-table-wrapper').length, 0);
    assert.deepEqual(env.errors, []);
});

test('only inserted roots are processed and removed MathJax state is cleared before removal', async () => {
    const highlighted = [], diagrams = [], typeset = [], cleared = [];
    const original = '<p class="math">old</p><pre><code class="language-js">old</code></pre><pre class="mermaid">old</pre>';
    const env = environment(original, {
        Prism: { highlightElement: node => highlighted.push(node) },
        mermaid: { initialize() {}, run: async ({ nodes }) => { diagrams.push(...nodes); } },
        MathJax: { startup: { promise: Promise.resolve() },
            typesetPromise: async roots => { typeset.push(...roots); },
            typesetClear: roots => { roots.forEach(x => assert.equal(x.parentNode, env.container)); cleared.push(...roots); } }
    });
    const old = [...env.container.childNodes];
    const added = '<section><p class="math">new</p><code class="language-js">new</code><pre class="mermaid">new</pre></section>';
    env.update(original + added);
    await settle();
    assert.equal(highlighted.length, 1);
    assert.equal(diagrams.length, 1);
    assert.deepEqual(typeset, [env.container.childNodes[3]]);
    assert.deepEqual(cleared, []);
    const inserted = env.container.childNodes[3];
    inserted.innerHTML = '<svg>mutated by libraries</svg>';
    env.update(original + added);
    await settle();
    assert.equal(env.container.childNodes[3], inserted);
    assert.equal(highlighted.length, 1);
    env.update('<p>no math remains</p>');
    await settle();
    assert.deepEqual(cleared, [...old, inserted]);
    assert.equal(typeset.length, 1);
    assert.deepEqual(env.errors, []);
});

test('updates serialize Mermaid and MathJax, coalesce pending changes, and never lose final content', async () => {
    const mermaidGate = deferred(), mathGate = deferred();
    const order = [];
    const env = environment('', {
        mermaid: { initialize() {}, run: async () => { order.push('mermaid'); await mermaidGate.promise; } },
        MathJax: { startup: { promise: Promise.resolve() }, typesetClear() {},
            typesetPromise: async () => { order.push('math'); await mathGate.promise; } }
    });
    env.update('<section><pre class="mermaid">A</pre><p class="math">A</p></section>');
    await settle();
    const active = env.container.firstChild;
    env.update('<p>discard this</p>');
    env.update('<p>final</p>');
    await settle();
    assert.equal(env.container.firstChild, active);
    mermaidGate.resolve();
    await settle();
    assert.deepEqual(order, ['mermaid', 'math']);
    assert.equal(env.container.firstChild, active);
    mathGate.resolve();
    await settle();
    assert.equal(env.container.innerHTML, '<p>final</p>');
    assert.deepEqual(env.errors, []);
});

test('lazy math config is not treated as ready; startup and typesetting are awaited with one load', async () => {
    const startup = deferred(), typesetting = deferred();
    let calls = 0;
    const env = environment('', { MathJax: {} });
    env.update('<p class="math">A</p>');
    await settle();
    assert.equal(env.document.scripts.length, 1);
    assert.equal(env.document.scripts[0].src, 'http://markdown-editor-host/margin/mathjax.js');
    assert.equal(env.window.MathJax.startup.typeset, false);
    assert.equal(env.window.MathJax.loader.paths.tex, 'http://markdown-editor-host/margin');
    env.window.MathJax = { startup: { promise: startup.promise }, typesetClear() {},
        typesetPromise: () => { calls++; return typesetting.promise; } };
    env.loaded(env.document.scripts[0]);
    env.update('<p>last</p>');
    await settle();
    assert.equal(calls, 0);
    startup.resolve();
    await settle();
    assert.equal(calls, 1);
    assert.match(env.container.innerHTML, /class="math"/);
    typesetting.resolve();
    await settle();
    assert.equal(env.container.innerHTML, '<p>last</p>');
    assert.equal(env.document.scripts.length, 1);
    assert.deepEqual(env.errors, []);
});

test('initial onload Mermaid and MathJax work blocks replacement, including MathJax startup', async () => {
    const mermaid = deferred(), math = deferred(), startup = deferred();
    const env = environment('<p>initial</p>', {}, 'loading');
    const asset = new Node('script');
    env.window.mermaid = { init: () => mermaid.promise, run: async () => {} };
    env.window.MathJax = { startup: { promise: startup.promise }, typesetClear() {}, typesetPromise: () => math.promise };
    env.loaded(asset);
    env.window.mermaid.init();
    env.window.MathJax.typesetPromise();
    env.update('<p>next</p>');
    env.window.fire('load');
    await new Promise(resolve => setTimeout(resolve, 5));
    await settle();
    assert.equal(env.container.innerHTML, '<p>initial</p>');
    startup.resolve();
    mermaid.resolve();
    await settle();
    assert.equal(env.container.innerHTML, '<p>initial</p>');
    math.resolve();
    await settle();
    assert.equal(env.container.innerHTML, '<p>next</p>');
});

test('lazy Prism disables automatic whole-document highlighting and loads only once', async () => {
    const highlighted = [];
    const env = environment('<code>plain</code>');
    env.update('<code>plain</code><code class="language-js">new</code>');
    await settle();
    const asset = env.document.scripts[0];
    assert.equal(asset.src, 'http://markdown-editor-host/margin/prism.js');
    assert.ok('data-manual' in asset.attributes);
    env.window.Prism = { highlightElement: node => highlighted.push(node) };
    env.loaded(asset);
    await settle();
    env.update('<code>plain</code><code class="language-js">newer</code>');
    await settle();
    assert.equal(highlighted.length, 2);
    assert.equal(env.document.scripts.length, 1);
    assert.deepEqual(env.errors, []);
});

test('render/load errors are reported and the queue continues to the latest update', async () => {
    const env = environment('', { mermaid: { initialize() {}, run: async () => { throw new Error('bad diagram'); } } });
    env.update('<pre class="mermaid">invalid</pre>');
    await settle();
    assert.ok(env.messages.some(x => x === 'previewError:bad diagram'));
    assert.ok(env.errors.length);
    env.update('<p>recovered</p>');
    await settle();
    assert.equal(env.container.innerHTML, '<p>recovered</p>');
    const failed = environment();
    failed.update('<code class="language-js">x</code>');
    await settle();
    failed.loaded(failed.document.scripts[0], true);
    await settle();
    assert.ok(failed.messages.some(x => x.includes('Could not load')));
    failed.update('<p>recovered</p>');
    await settle();
    assert.equal(failed.container.innerHTML, '<p>recovered</p>');
});

test('failed MathJax loading leaves no state to clear and does not block later plain content', async () => {
    const env = environment();
    env.update('<p class="math">x</p>');
    await settle();
    env.loaded(env.document.scripts[0], true);
    await settle();
    assert.ok(env.messages.some(x => x.includes('mathjax.js')));
    env.update('<p>recovered without math</p>');
    await settle();
    assert.equal(env.container.innerHTML, '<p>recovered without math</p>');
    assert.equal(env.document.scripts.length, 1);
});

test('existing in-flight feature tags are reused rather than loaded twice', async () => {
    const highlighted = [];
    const env = environment();
    const asset = env.document.createElement('script');
    asset.src = 'http://markdown-editor-host/margin/prism.js';
    env.document.head.appendChild(asset);
    env.update('<code class="language-js">x</code>');
    await settle();
    assert.equal(env.document.scripts.length, 1);
    env.window.Prism = { highlightElement: node => highlighted.push(node) };
    env.loaded(asset);
    await settle();
    assert.equal(highlighted.length, 1);
    assert.deepEqual(env.errors, []);
});

test('Mermaid automatic window-load rendering registered after the helper blocks queued updates', async () => {
    const gate = deferred();
    const env = environment('<pre class="mermaid">initial</pre>', {}, 'loading');
    env.window.mermaid = { initialize() {}, run: () => gate.promise };
    // Mermaid 11 registers contentLoaded during bundle evaluation, before its script load event.
    env.window.addEventListener('load', () => { env.window.mermaid.run().catch(() => {}); });
    env.loaded(new Node('script'));
    env.update('<p>next</p>');
    env.window.fire('load');
    await new Promise(resolve => setTimeout(resolve, 5));
    await settle();
    assert.equal(env.container.innerHTML, '<pre class="mermaid">initial</pre>');
    gate.resolve();
    await settle();
    assert.equal(env.container.innerHTML, '<p>next</p>');
    assert.deepEqual(env.errors, []);
});

test('explicit initialization lazily renders through the same serialized queue and coalesces edits', async () => {
    const gate = deferred();
    const settings = [], rendered = [];
    const env = environment('<pre class="mermaid">initial</pre>', {}, 'loading');
    const original = env.container.firstChild;
    assert.equal(env.window.__initializeMarkdownPreview('forest'), true);
    env.update('<p>discard intermediate</p>');
    env.update('<p>latest</p>');
    assert.equal(env.container.firstChild, original);
    env.window.fire('load');
    await new Promise(resolve => setTimeout(resolve, 5));
    await settle();
    assert.notEqual(env.container.firstChild, original);
    assert.equal(env.document.scripts.length, 1);
    assert.equal(env.document.scripts[0].src, 'http://markdown-editor-host/margin/mermaid.min.js');
    env.window.mermaid = {
        initialize: config => settings.push(config),
        run: async ({ nodes }) => { rendered.push(...nodes); await gate.promise; }
    };
    env.loaded(env.document.scripts[0]);
    await settle();
    assert.equal(settings[0].theme, 'forest');
    assert.equal(settings[0].flowchart.htmlLabels, true);
    assert.equal(settings[0].startOnLoad, false);
    assert.equal(rendered.length, 1);
    assert.equal(env.container.innerHTML, '<pre class="mermaid">initial</pre>');
    gate.resolve();
    await settle();
    assert.equal(env.container.innerHTML, '<p>latest</p>');
    assert.equal(env.document.scripts.length, 1);
    assert.deepEqual(env.errors, []);
});

test('many identical newline nodes are each reused once without array shifting', async () => {
    const env = environment();
    // Adjacent text nodes normally merge during HTML parsing; headings keep newline nodes separate.
    const html = Array.from({ length: 2000 }, (_, index) => `<p>${index}</p>\n`).join('');
    env.container.innerHTML = html;
    // Capture the nodes through the first update, then exercise duplicate reuse on a reorder.
    env.update(html);
    await settle();
    const old = [...env.container.childNodes];
    env.update(Array.from({ length: 2000 }, (_, index) => `<p>${1999 - index}</p>\n`).join(''));
    await settle();
    for (let index = 0; index < 2000; index++) {
        assert.equal(env.container.childNodes[index * 2], old[(1999 - index) * 2]);
        assert.equal(env.container.childNodes[index * 2 + 1], old[index * 2 + 1]);
    }
    assert.ok(!script.includes('.shift('));
    assert.deepEqual(env.errors, []);
});

test('completion acknowledgements wait for rendering and skip coalesced request IDs', async () => {
    const gate = deferred();
    const env = environment('<pre class="mermaid">initial</pre>', {
        mermaid: { initialize() {}, run: () => gate.promise }
    });
    env.window.__initializeMarkdownPreview('dark', 0);
    await settle();
    env.update('<p>discard</p>', 'dark', 1);
    env.update('<p>final</p>', 'dark', 2);
    await settle();
    assert.deepEqual(env.messages, []);
    gate.resolve();
    await settle();
    assert.deepEqual(env.messages, ['previewComplete:0', 'previewComplete:2']);
    assert.equal(env.container.innerHTML, '<p>final</p>');
    env.update('<p>without request ID</p>');
    await settle();
    assert.deepEqual(env.messages, ['previewComplete:0', 'previewComplete:2']);
});

test('failed requests report their ID without success and subsequent requests still complete', async () => {
    const env = environment('', {
        mermaid: { initialize() {}, run: async () => { throw new Error('diagram failed'); } }
    });
    env.update('<pre class="mermaid">bad</pre>', 'dark', 'failed-request');
    await settle();
    assert.ok(env.messages.includes('previewError:diagram failed'));
    assert.ok(env.messages.includes('previewFailed:failed-request'));
    assert.ok(!env.messages.includes('previewComplete:failed-request'));
    env.update('<p>recovered</p>', 'dark', 'good-request');
    await settle();
    assert.ok(env.messages.includes('previewComplete:good-request'));
    assert.ok(!env.messages.includes('previewFailed:good-request'));
    const unnumbered = environment('', {
        mermaid: { initialize() {}, run: async () => { throw new Error('unnumbered'); } }
    });
    unnumbered.update('<pre class="mermaid">bad</pre>');
    await settle();
    assert.ok(unnumbered.messages.every(x => x.startsWith('previewError:')));
});

test('MathJax script load can precede typesetting API creation during asynchronous startup', async () => {
    const startupGate = deferred(), typesettingGate = deferred();
    const typeset = [], cleared = [];
    const env = environment();
    env.update('<p class="math">initial equation</p>', 'dark', 'math-initial');
    await settle();
    assert.equal(env.document.scripts.length, 1);
    env.window.MathJax = {
        startup: { promise: startupGate.promise.then(() => {
            env.window.MathJax.typesetPromise = roots => { typeset.push(...roots); return typesettingGate.promise; };
            env.window.MathJax.typesetClear = roots => { cleared.push(...roots); };
        }) }
    };
    env.loaded(env.document.scripts[0]);
    env.update('<p>final without math</p>', 'dark', 'math-final');
    await settle();
    assert.deepEqual(env.errors, []);
    assert.deepEqual(env.messages, []);
    assert.equal(typeset.length, 0);
    startupGate.resolve();
    await settle();
    assert.equal(typeset.length, 1);
    assert.deepEqual(env.messages, []);
    const mathRoot = env.container.firstChild;
    typesettingGate.resolve();
    await settle();
    assert.deepEqual(env.errors, []);
    assert.deepEqual(env.messages, ['previewComplete:math-initial', 'previewComplete:math-final']);
    assert.deepEqual(cleared, [mathRoot]);
    assert.equal(env.container.innerHTML, '<p>final without math</p>');
    assert.equal(env.document.scripts.length, 1);
});
