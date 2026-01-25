# Root Path Configuration Feature - Implementation Summary

## Overview
This feature allows users to configure a root path via YAML front matter to properly resolve root-relative paths (paths starting with `/`) in the markdown preview. This is particularly useful for Jekyll and GitHub Pages workflows.

## Problem Statement
When editing markdown files for static site generators like Jekyll, users often use root-relative paths (e.g., `/images/logo.png`) that work on the deployed site but don't resolve correctly in the Visual Studio preview because the preview doesn't know what the "root" directory should be.

## Solution
Users can now specify a `root_path` variable in YAML front matter at the top of their markdown files. The preview will use this root path to resolve any paths starting with `/`.

## Technical Implementation

### Files Modified
1. **src/Margin/Browser.cs**
   - Added `GetRootPathFromFrontMatter()` method to extract root_path from YAML front matter
   - Enhanced `ResolveRelativePathsToAbsoluteUrls()` to accept an optional rootPath parameter
   - Added `_rootRelativePathRegex` to match paths starting with `/` (but not `//`)
   - Updated three call sites to extract and pass root path from front matter

### Files Added
2. **test/MarkdownEditor2022.UnitTests/FrontMatterTests.cs**
   - Comprehensive unit tests for front matter parsing
   - Tests for quote handling, edge cases, and error conditions

3. **test/MarkdownEditor2022.UnitTests/PathResolutionTests.cs** (modified)
   - Added tests for root-relative path resolution

4. **test-root-path.md**
   - Comprehensive testing guide with examples

5. **README.md** (modified)
   - Added documentation section for the feature

## Usage Example

```yaml
---
root_path: C:\Projects\myblog
title: My Blog Post
---

![Header Image](/images/header.jpg)
[About Page](/about.md)
```

With this configuration:
- `/images/header.jpg` resolves to `C:\Projects\myblog\images\header.jpg`
- `/about.md` resolves to `C:\Projects\myblog\about.md`

## Key Features
- **Backward compatible**: Regular relative paths continue to work as before
- **Case-insensitive**: `root_path` or `ROOT_PATH` both work
- **Quote support**: Handles both single and double quoted values
- **Cross-platform**: Supports both Windows and Unix path formats
- **Works with images and links**: Applies to both `src` and `href` attributes

## Testing
- ✅ Unit tests for FrontMatter parsing
- ✅ Unit tests for root-relative path resolution
- ✅ Code review completed and feedback addressed
- ✅ Security scan passed (0 alerts)
- ⏳ Manual testing pending (requires Visual Studio environment)

## Security Considerations
- Path resolution uses `Path.GetFullPath()` which handles path traversal correctly
- All paths are validated and resolved before being used
- Invalid paths gracefully fall back to original values
- CodeQL security scan found no vulnerabilities

## Backward Compatibility
- ✅ Feature is opt-in via front matter variable
- ✅ Existing markdown files without `root_path` work exactly as before
- ✅ Regular relative paths (not starting with `/`) are unaffected
- ✅ No breaking changes to existing functionality

## Future Enhancements (Not in Scope)
- Configuration file support (e.g., `.markdownrc`)
- Workspace-level root path settings
- Auto-detection of project root
- Multiple root path definitions

## References
- Original Issue: User request for configurable root path for Jekyll/GitHub Pages workflow
- Maintainer Suggestion: Use FrontMatter variables (implemented in this PR)
