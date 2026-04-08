# Contributing

## Reporting a Bug

Be sure to reproduce the issue on the latest master branch before filing a report.
If you can still reproduce it, open a detailed issue with the steps needed to reproduce the bug.
The goal of your description is to make it easy to fix the bug, not just find it.

## Feature Requests

We love new ideas. Open an issue explaining the feature in detail so we can understand exactly what you need.

## Building the Project

The build script is `build.fsx`, run with `dotnet fsi`.

```
dotnet fsi build.fsx
```


## Code Structure

XrmTypeScript works by querying CRM for metadata and generating TypeScript declaration files from it.
The entry point is `XrmTypeScript.fs`, which calls two functions: `retrieveRawState`, which fetches
metadata from CRM, and `generateFromRaw`, which generates the declaration files from that metadata.

The following describes the contents of each folder in the project.

### Types
Contains the static TypeScript declaration files that are copied directly to the output.
The main file is `xrm.d.ts`, which provides the base `Xrm` namespace declarations based on `@types/xrm`.

### TypeScript
Contains helper methods and declarations used during the generation of TypeScript files.

### Crm
Contains helper methods for connecting to and querying CRM.
`CrmAuth` handles authentication, `CrmBaseHelper` handles querying, and `CrmDataHelper` extends `CrmBaseHelper`.

### Interpretation
Contains interpreters that translate raw CRM metadata into an intermediate representation used by the rest of the program.

### CreateTypeScript
Contains functions that translate the intermediate representation into TypeScript declaration file content.

### Generation
Orchestrates the overall generation process — retrieving metadata, interpreting it, and writing the output files.

### CommandLine
Contains the functions necessary to handle command-line arguments.
