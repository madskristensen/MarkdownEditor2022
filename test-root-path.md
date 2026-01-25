# Testing Root Path Configuration

This document demonstrates the root path configuration feature for the Markdown Editor extension.

## How to Use

Add a `root_path` variable in the YAML front matter at the top of your markdown file:

```yaml
---
root_path: C:\Projects\blog
title: My Blog Post
---
```

## Example 1: Basic Usage

---
root_path: C:\Projects\MyWebsite
---

This image uses a root-relative path:
![Logo](/images/logo.png)

Without the `root_path` setting, the path `/images/logo.png` would not resolve correctly. With `root_path: C:\Projects\MyWebsite`, it resolves to `C:\Projects\MyWebsite\images\logo.png`.

## Example 2: Jekyll/GitHub Pages Workflow

For Jekyll sites, you typically have a structure like:

```
myblog/
├── _posts/
│   └── 2024-01-01-my-post.md
├── images/
│   └── header.jpg
└── assets/
    └── style.css
```

In your post file `_posts/2024-01-01-my-post.md`:

```yaml
---
root_path: C:\Users\Me\myblog
title: My Blog Post
---

![Header Image](/images/header.jpg)
```

The image path `/images/header.jpg` will correctly resolve to `C:\Users\Me\myblog\images\header.jpg` in the preview.

## Example 3: Multiple Root-Relative Paths

---
root_path: /home/user/website
---

You can use multiple root-relative paths in the same document:

- ![Image 1](/assets/images/photo1.jpg)
- ![Image 2](/assets/images/photo2.jpg)
- [Link to another page](/docs/readme.md)

All paths starting with `/` will be resolved relative to the `root_path`.

## Example 4: Mixed Paths

You can still use regular relative paths alongside root-relative paths:

---
root_path: C:\Projects\docs
---

- Regular relative path: ![Local](./local-image.png)
- Root-relative path: ![Global](/shared/global-image.png)
- Parent directory: ![Parent](../images/parent.png)

Regular relative paths (not starting with `/`) continue to work as before, resolving relative to the markdown file's location.

## Supported Values

The `root_path` can be:
- Windows path: `C:\Projects\blog`
- Unix path: `/home/user/website`
- Quoted: `"C:\My Projects\My Site"`
- With spaces: `C:\My Projects\Site`

## Case Insensitivity

The `root_path` key is case-insensitive:

```yaml
---
root_path: C:\Projects\blog
---
```

is the same as:

```yaml
---
ROOT_PATH: C:\Projects\blog
---
```

## Notes

- Only paths starting with `/` (and not `//`) are treated as root-relative
- Paths starting with `http://`, `https://`, `data:`, or `#` are left unchanged
- If `root_path` is not specified, root-relative paths are not modified
- The feature works with both images (`src` attributes) and links (`href` attributes)
