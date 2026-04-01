# Managing Mermaid.js Dependencies with npm

## Overview

The Markdown Editor 2022 uses [Mermaid](https://mermaid.js.org/) for rendering diagrams in Markdown preview. Previously, `mermaid.min.js` was manually updated. Now, npm manages this dependency automatically.

## Prerequisites

- **Node.js** 20.10.0 or later
- **npm** 10.2.3 or later

You can check your versions with:
```bash
node --version
npm --version
```

## Initial Setup

After cloning the repository, run:

```bash
npm install
```

This will:
1. Install the `mermaid` package from npm
2. Automatically copy the necessary files to `src/Margin/`:
   - `mermaid.min.js`
   - `mermaid.min.js.map`
   - `mermaid.min.js.LICENSE.txt`

## Updating Mermaid

To update to the latest stable version of Mermaid:

```bash
npm run update-mermaid
```

Or manually:
```bash
npm update mermaid
npm run copy-assets
```

## Manual File Copying

If you need to copy the assets manually (after changing versions):

```bash
npm run copy-mermaid
```
