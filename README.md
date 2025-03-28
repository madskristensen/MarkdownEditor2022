[marketplace]: https://marketplace.visualstudio.com/items?itemName=MadsKristensen.MarkdownEditor2
[vsixgallery]: http://vsixgallery.com/extension/MarkdownEditor2022.2347dc70-1875-4775-bf48-f2b9fdfee8d4/
[repo]:https://github.com/madskristensen/MarkdownEditor2022

# Markdown Editor v2 for Visual Studio

[![Build](https://github.com/madskristensen/MarkdownEditor2022/actions/workflows/build.yaml/badge.svg)](https://github.com/madskristensen/MarkdownEditor2022/actions/workflows/build.yaml)
![GitHub Sponsors](https://img.shields.io/github/sponsors/madskristensen)

Download this extension from the [Visual Studio Marketplace][marketplace]
or get the [CI build][vsixgallery].

--------------------------------------

A full featured Markdown editor with live preview and syntax highlighting. Supports GitHub flavored Markdown.

> This is a complete rewrite of the original [Markdown Editor](https://marketplace.visualstudio.com/items?itemName=MadsKristensen.MarkdownEditor64) with tons of fixes, tweeks, and performance improvements. A few little-used features are not being ported to this new extension, so if you rely on them you should keep using the original.

## Features

- Powered by [Markdig](https://github.com/lunet-io/markdig) - the best markdown parser
- Syntax highlighting of code blocks (using Prism.js)
- Live preview window with scroll sync and dark theme support
    - Both vertical and horizontal layout supported
- CommonMark and GitHub flavored Markdown + FrontMatter YAML
- Mermaid and math notation rendering supported
- Jump between Markdown Headings from the NavigationBar
- Drag 'n drop of images supported
- Paste image from clipboard directly onto document
- Table of contents (TOC) generation
- Outlining/folding of code blocks
- Keyboard shortcuts
- Support for custom CSS and HTML templates in preview window
- Brace completion with type-through
- Validation of relative URLs
- Lightning fast

### Live Preview Window
The preview window opens up on the right side of the document when it opens. Use `F7` to toggle the preview window on and off.

![Preview window](art/preview-window.png)

![Preview window](art/preview-window-dark.png)

Every time the markdown document is modified, the preview window will update.

The preview window is automatically scrolled to match the scroll position of the document. As the document is scrolled up and down, the preview window will follow.

Live preview can be managed in the [settings](#settings).

### Syntax highlighting
All fonts can be changed in **Tools -> Options -> Environment -> Fonts and Colors** dialog.

![Font Options](art/font-options.png)

#### GitHub and other flavors
Advanced markdown extensions are supported to give more features to the syntax. This includes pipe tables, emoji, mathematics and a lot
more.

### IntelliSense
You get full IntelliSense for over 1,600 emoji and smiley characters.

![Markdown Emoji Intellisense](art/emoji.gif)

### Heading-based navigation
The NavigationBar shows all of the document headings, like a table of contents. Select a heading to jump to that section of the document.

![Navigator Bar](art/navigator-bar.png)

### Drag 'n drop images
Drag an image directly from Solution Explorer onto the document to insert the appropriate markdown that will render the image.

### Paste images
This is really helpful for copying images from a browser or for inserting screen shots. Simply copy an image into the clipboard and paste it directly into the document. This will prompt you for a file name relative to the document and then it inserts the appropriate markdown.

It will even parse the file name and make a friendly name to use for the alt text.

### Outlining
Any fenced code and HTML blocks can be collapsed, so that this:

![Outlining Expanded](art/outlining-expanded.png)

...can be collapsed into this:

![Outlining Collapsed](art/outlining-collapsed.png)

### Keyboard shortcuts
**Ctrl+Alt+R** Refresh the preview window.

**Ctrl+Alt+K** Inserts a new link.

**Ctrl+B** makes the selected text bold by wrapping it with `**`.

**Ctrl+I** makes the selected text italic by wrapping it with `_`.

<!--**Ctrl+Shift+C** wraps the selected text in a code block.-->
**Ctrl+Space** checks and unchecks task list items.

```markdown
- [x] task list item
```

**Tab** increases indentation of list items.

**Shift+Tab** decreases indentation of list items.

**Ctrl+K,C** wraps the selection with HTML comments.

**Ctrl+K,U** removes HTML comments surrounding the selection/caret.

<!--**Ctrl+PgUp** moves caret to previous heading

**Ctrl+PgDown** moves caret to next heading-->

## Validation
Relative URLs are validated and will show a red squiggly when they can't be resolved.

![Error](art/error.png)

The errors will also be listed in the Error List.

## Custom styles and template
You can provide your own .CSS and HTML templates used to render the preview window. The extension will look for the files **md-styles.css** and **md-template.html** in the same folder and any parent folder. If one or both of these files are found, they are being applied in the preview. 

The HTML template must contain the string **[content]** which is where the rendered markdown will be injected.

You have to refresh the preview window after making changes to the custom CSS and HTML file. You can do that from the markdown editor context menu or by hitting **Ctrl+Alt+R**.

## Context menu
Right-click anywhere in the markdown document to get easy access to common tasks, such as toggling the preview window scroll sync, see Markdown references, and getting to the settings dialog.

![Context menu](art/context-menu.png
)

### Settings
Control the settings for this extension under
**Tools -> Options -> Text Editor -> Markdown**

![Options](art/options.png)

### How can I help?
If you enjoy using the extension, please give it a ★★★★★ rating on the [Visual Studio Marketplace][marketplace].

Should you encounter bugs or if you have feature requests, head on over to the [GitHub repo][repo] to open an issue if one doesn't already exist.

Pull requests are also very welcome, since I can't always get around to fixing all bugs myself. This is a personal passion project, so my time is limited.

Another way to help out is to [sponsor me on GitHub](https://github.com/sponsors/madskristensen).
