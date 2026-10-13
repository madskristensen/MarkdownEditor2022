
(function () {
    'use strict';
    var container = document.getElementById('___markdown-content___');
    if (!container || window.__updateMarkdownPreview) return;

    var assetRoot = 'http://markdown-editor-host/margin/';
    var loads = new Map();
    var completedScripts = new WeakSet();
    var failedScripts = new WeakSet();
    var rendering = new Set();
    var wrapped = new WeakSet();
    var pending = null;
    var running = false;

    function report(error) {
        console.error('Markdown preview:', error);
        if (window.chrome && window.chrome.webview) {
            window.chrome.webview.postMessage('previewError:' + (error && error.message || String(error)));
        }
    }
    function acknowledge(request, success) {
        if (request.id !== undefined && window.chrome && window.chrome.webview) {
            window.chrome.webview.postMessage((success ? 'previewComplete:' : 'previewFailed:') + request.id);
        }
    }

    // Remember source markup, not the SVG, MathJax or Prism markup generated later.
    function entry(node) {
        return { node: node, signature: node.nodeType + ':' +
            (node.nodeType === 1 ? node.outerHTML : node.nodeValue), ready: true };
    }
    var entries = Array.from(container.childNodes, entry);

    // Initial feature tags run after this script. Track their asynchronous work too.
    function track(object, name) {
        if (!object || typeof object[name] !== 'function' || wrapped.has(object[name])) return;
        var original = object[name];
        var replacement = function () {
            var result = original.apply(this, arguments);
            if (result && typeof result.then === 'function') {
                rendering.add(result);
                result.then(function () { rendering.delete(result); }, function (error) {
                    rendering.delete(result);
                    report(error);
                });
            }
            return result;
        };
        wrapped.add(replacement);
        object[name] = replacement;
    }
    function trackLibraries() {
        track(window.mermaid, 'init');
        track(window.mermaid, 'run');
        track(window.MathJax, 'typesetPromise');
    }
    document.addEventListener('load', function (event) {
        if (event.target.tagName === 'SCRIPT') {
            completedScripts.add(event.target);
            trackLibraries();
        }
    }, true);
    document.addEventListener('error', function (event) {
        if (event.target.tagName === 'SCRIPT') failedScripts.add(event.target);
    }, true);
    trackLibraries();

    // Let initial scripts and their window-load handlers start before changing the DOM.
    var initialLoad = document.readyState === 'complete' ? Promise.resolve() :
        new Promise(function (resolve) {
            window.addEventListener('load', function () { setTimeout(resolve, 0); }, { once: true });
        });

    async function waitForRendering() {
        if (window.MathJax && window.MathJax.startup && window.MathJax.startup.promise) {
            await window.MathJax.startup.promise;
        }
        while (rendering.size) await Promise.all(Array.from(rendering));
    }

    function load(name, isReady) {
        if (isReady()) {
            trackLibraries();
            return Promise.resolve();
        }
        if (loads.has(name)) return loads.get(name);
        var promise = new Promise(function (resolve, reject) {
            var url = assetRoot + name;
            var script = Array.from(document.scripts).find(function (item) { return item.src === url; });
            if (script && failedScripts.has(script)) {
                reject(new Error('Could not load ' + url));
                return;
            }
            function loaded() {
                trackLibraries();
                if (isReady()) resolve();
                else reject(new Error(name + ' loaded without its expected API.'));
            }
            if (script && completedScripts.has(script)) {
                loaded();
                return;
            }
            var isNew = !script;
            if (isNew) {
                script = document.createElement('script');
                script.src = url;
                if (name === 'prism.js') script.setAttribute('data-manual', '');
            }
            script.addEventListener('load', loaded, { once: true });
            script.addEventListener('error', function () {
                reject(new Error('Could not load ' + url));
            }, { once: true });
            if (isNew) document.head.appendChild(script);
        });
        loads.set(name, promise);
        return promise;
    }

    async function mathJax() {
        // A configuration object is not a loaded MathJax instance.
        if (!window.MathJax || typeof window.MathJax.typesetPromise !== 'function') {
            var config = window.MathJax = window.MathJax || {};
            config.startup = config.startup || {};
            config.startup.typeset = false;
            config.tex = config.tex || {};
            config.tex.packages = config.tex.packages || { '[+]': ['color'] };
            config.loader = config.loader || {};
            config.loader.load = config.loader.load || ['[tex]/color'];
            config.loader.paths = config.loader.paths || {};
            config.loader.paths.tex = assetRoot.slice(0, -1);
            await load('mathjax.js', function () {
                return window.MathJax && window.MathJax.startup &&
                    window.MathJax.startup.promise && typeof window.MathJax.startup.promise.then === 'function';
            });
        }
        await waitForRendering();
        if (typeof window.MathJax.typesetPromise !== 'function' || typeof window.MathJax.typesetClear !== 'function') {
            throw new Error('MathJax startup completed without its expected typesetting APIs.');
        }
        trackLibraries();
        return window.MathJax;
    }

    function collect(roots, selector) {
        var result = [];
        roots.forEach(function (root) {
            if (root.matches(selector)) result.push(root);
            result.push.apply(result, root.querySelectorAll(selector));
        });
        return result;
    }

    function wrapTables(root) {
        Array.from(root.querySelectorAll('table')).forEach(function (table) {
            if (table.parentNode && table.parentNode.matches('.markdown-table-wrapper')) return;
            var wrapper = document.createElement('div');
            wrapper.setAttribute('class', 'markdown-table-wrapper');
            table.parentNode.insertBefore(wrapper, table);
            wrapper.appendChild(table);
        });
    }

    function adjustAnchors(roots) {
        collect(roots, 'a[href], area[href]').forEach(function (anchor) {
            if (anchor.protocol !== 'file:') return;
            var pathName = null;
            var hash = anchor.hash;
            if (hash) {
                pathName = anchor.pathname;
                anchor.hash = '';
                anchor.pathname = '';
            }
            anchor.protocol = 'about:';
            if (hash) {
                if (pathName === null || pathName.endsWith('/')) pathName = 'blank';
                anchor.pathname = pathName;
                anchor.hash = hash;
            }
        });
    }

    async function update(request) {
        await initialLoad;
        await waitForRendering();
        var template = document.createElement('template');
        template.innerHTML = request.html;
        wrapTables(template.content);
        var desired = Array.from(template.content.childNodes, entry);
        var available = new Map();
        entries.forEach(function (item) {
            if (!item.ready || item.node.parentNode !== container) return;
            if (!available.has(item.signature)) available.set(item.signature, { items: [], cursor: 0 });
            available.get(item.signature).items.push(item);
        });
        var inserted = [];
        var next = desired.map(function (item) {
            var matches = available.get(item.signature);
            if (matches && matches.cursor < matches.items.length) return matches.items[matches.cursor++];
            item.ready = false;
            inserted.push(item);
            return item;
        });
        var retained = new Set(next.map(function (item) { return item.node; }));
        var removed = Array.from(container.childNodes).filter(function (node) { return !retained.has(node); });
        var removedElements = removed.filter(function (node) { return node.nodeType === 1; });

        // Clear even when the new document contains no math, and always before detaching.
        if (removedElements.length && window.MathJax && typeof window.MathJax.typesetPromise === 'function') {
            var math = await mathJax();
            if (typeof math.typesetClear !== 'function') throw new Error('MathJax.typesetClear is unavailable.');
            math.typesetClear(removedElements);
        }
        removed.forEach(function (node) { container.removeChild(node); });
        var cursor = container.firstChild;
        next.forEach(function (item) {
            if (item.node !== cursor) container.insertBefore(item.node, cursor);
            cursor = item.node.nextSibling;
        });
        entries = next;
        var roots = inserted.map(function (item) { return item.node; })
            .filter(function (node) { return node.nodeType === 1; });
        adjustAnchors(roots);

        // Each feature gets only newly inserted subtrees; retained nodes stay untouched.
        var code = collect(roots, 'code').filter(function (node) {
            return node.closest('[class*="language-"]') && !node.closest('.mermaid');
        });
        var diagrams = collect(roots, '.mermaid');
        var mathRoots = roots.filter(function (root) { return root.matches('.math') || root.querySelector('.math'); });
        if (code.length) {
            await load('prism.js', function () { return window.Prism && typeof window.Prism.highlightElement === 'function'; });
            code.forEach(function (node) { window.Prism.highlightElement(node); });
        }
        if (diagrams.length) {
            await load('mermaid.min.js', function () { return window.mermaid && typeof window.mermaid.run === 'function'; });
            window.mermaid.initialize({
                startOnLoad: false, securityLevel: 'loose', theme: request.theme,
                flowchart: { htmlLabels: true }, sequence: { useMaxWidth: true }
            });
            await window.mermaid.run({ nodes: diagrams, suppressErrors: false });
        }
        if (mathRoots.length) {
            var math = await mathJax();
            await math.typesetPromise(mathRoots);
        }
        inserted.forEach(function (item) { item.ready = true; });
    }

    async function drain() {
        running = true;
        try {
            while (pending) {
                var request = pending;
                pending = null;
                try {
                    await update(request);
                    acknowledge(request, true);
                } catch (error) {
                    report(error);
                    acknowledge(request, false);
                }
            }
        } finally {
            running = false;
        }
    }

    // ExecuteScriptAsync receives an immediate acknowledgement, not a rendering promise.
    window.__updateMarkdownPreview = function (html, mermaidTheme, requestId) {
        pending = { html: html, theme: mermaidTheme, id: requestId };
        if (!running) void drain();
        return true;
    };
    window.__initializeMarkdownPreview = function (mermaidTheme, requestId) {
        entries.forEach(function (item) { item.ready = false; });
        return window.__updateMarkdownPreview(container.innerHTML, mermaidTheme, requestId);
    };
})();
