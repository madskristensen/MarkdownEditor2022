# Managing Mermaid.js Dependencies with npm

This document explains how to manage the `mermaid.min.js` file using npm.

## Overview

The Markdown Editor 2022 uses [Mermaid](https://mermaid.js.org/) for rendering diagrams in Markdown preview. Previously, `mermaid.min.js` was manually updated. Now, npm manages this dependency automatically.

## Prerequisites

- **Node.js** 16.0.0 or later
- **npm** 8.0.0 or later

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

## Verifying the Installation

After running `npm install`, verify that the files are present in `src/Margin/`:

```bash
ls -la src/Margin/mermaid.min.js*
```

You should see:
- `mermaid.min.js` (the main library file)
- `mermaid.min.js.map` (source map for debugging)
- `mermaid.min.js.LICENSE.txt` (license information)

## How It Works

### Package Configuration

The `package.json` file defines:
- **Dependency**: `mermaid` - The npm package containing the diagram library
- **Scripts**: 
  - `postinstall`: Automatically runs after `npm install`
  - `copy-assets`: Copies files from node_modules to the project
  - `update-mermaid`: Convenience script to update and copy in one command

### Build Script

The `scripts/copy-mermaid.js` Node.js script:
1. Locates the installed `mermaid` package in `node_modules`
2. Copies the necessary files to `src/Margin/`
3. Copies the license file
4. Reports success or errors

## Integration in Visual Studio Build

The mermaid files in `src/Margin/` are part of your Visual Studio project and will be included in the compiled extension. The npm setup is separate from the C# build process.

**Workflow:**
1. Run `npm install` (or `npm run update-mermaid`) before or after pulling changes
2. Verify files are copied to `src/Margin/`
3. Build the solution normally in Visual Studio

## Troubleshooting

### Files not copied after `npm install`

If the postinstall script doesn't run automatically, try:
```bash
npm run copy-mermaid
```

### Script errors on Windows

If you encounter permission issues, try:
```bash
npm install --no-scripts
npm run copy-mermaid
```

### Node.js not found

Ensure Node.js is installed and accessible from your PATH. Download from https://nodejs.org/

### Different file locations expected?

Update the paths in `scripts/copy-mermaid.js` to match your project structure if needed.

## Dependencies

- **mermaid** - The diagram rendering library (installed via npm)

All dependencies are listed in `package.json` and installed to `node_modules/`.

## Best Practices

1. **Commit `package.json`** to version control (it should already be in your repo)
2. **Ignore `node_modules/`** (already in `.gitignore`)
3. **Run `npm install`** after pulling changes that might have updated versions
4. **Update periodically** - Check for updates: `npm outdated`
5. **Security** - Keep dependencies current: `npm audit` to check for vulnerabilities

## Version Management

The current version constraint in `package.json` is:
```json
"mermaid": "^10.6.1"
```

- **`^10.6.1`** means npm will install versions from 10.6.1 up to (but not including) 11.0.0
- This allows patch and minor updates while preventing major breaking changes
- To allow major version updates, change to `*` (not recommended without testing)
- To pin to a specific version, change to `"10.6.1"` (no caret)

## Additional Resources

- [Mermaid.js Documentation](https://mermaid.js.org/)
- [npm Documentation](https://docs.npmjs.com/)
- [Node.js Documentation](https://nodejs.org/docs/)
