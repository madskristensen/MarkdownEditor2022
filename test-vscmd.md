# VS CMD URL Scheme Test

This document demonstrates the new VS CMD URL scheme support for executing Visual Studio commands from markdown links.

## Test Commands

- [Open Code Search (Go To All)](vscmd://Edit.GoToAll)
- [Open Solution Explorer](vscmd://View.SolutionExplorer)
- [Open Error List](vscmd://View.ErrorList)
- [Open Output Window](vscmd://View.Output)
- [Open Find and Replace](vscmd://Edit.Replace)
- [Build Solution](vscmd://Build.BuildSolution)
- [Show Quick Launch](vscmd://View.QuickLaunch)

## How it works

When you click on any of the links above in the preview window, the markdown editor will:

1. Parse the `vscmd://` URL scheme
2. Extract the command name (e.g., "Edit.GoToAll")
3. Execute the command using `VS.Commands.ExecuteAsync()`
4. Show a status message indicating success or failure

## Example Usage

```markdown
[Code Search](vscmd://Edit.GoToAll)
[Solution Explorer](vscmd://View.SolutionExplorer)
```

The URL format is: `vscmd://CommandName` where `CommandName` is any valid Visual Studio command.