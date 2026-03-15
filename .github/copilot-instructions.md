# Copilot Instructions

## Project Guidelines

- When a user says a fix did not work, immediately verify in-workspace behavior and adjust instead of only applying the first likely change.
- When applying external guidance, adapt registry/settings snippets to this extension's own editor identifiers instead of copying them verbatim. Ensure that external registration advice aligns with the compile-time generated editor registration via ProvideEditorExtension/ProvideEditorFactory attributes.
- When editing a .csproj file, unload the project from the solution in the IDE, make the necessary changes, and then reload the project to ensure the changes take effect properly.
