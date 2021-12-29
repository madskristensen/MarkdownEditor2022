# Pkgdef Language for Visual Studio

[![Build](https://github.com/madskristensen/PkgdefLanguage/actions/workflows/build.yaml/badge.svg)](https://github.com/madskristensen/PkgdefLanguage/actions/workflows/build.yaml)

Download this extension from the [Visual Studio Marketplace](https://marketplace.visualstudio.com/items?itemName=MadsKristensen.PkgdefLanguage)
or get the [CI build](http://vsixgallery.com/extension/06278dd5-5d9d-4f27-a3e8-cd619b101a50/).

--------------------------------------

This extension provides basic language support for .pkgdef and .pkgundef files in Visual Studio.

## Syntax highlighting
Syntax highligting makes it easy to parse the document. Here's what it looks like:

![Colorization](art/colorization.png)

## IntelliSense
Full completion provided for variables and registry keys.

![Intellisense](art/intellisense.gif)

## Validation
There's validation for both syntax errors and unknown variables.

![Validation](art/validation.png)

## Outlining
Collapse sections for better overview of the document.

![Outlining](art/outlining.png)

Notice how only comments starting with a semicolon is correctly identified as a comment.

## Formatting
You can format the whole document `Ctrl+K,Ctrl+D` or the current selection `Ctrl+K,Ctrl+F`. It will add a line break between registry key entries, trim whitespace, and other clean-up formatting.

![Formatting](art/formatting.png)