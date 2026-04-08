# XrmTypeScript vs XrmDefinitelyTyped

XrmTypeScript is a fork of [XrmDefinitelyTyped](https://github.com/delegateas/XrmDefinitelyTyped) by Delegateas.
The following is a list of the key differences.

## What's Different

- **Modern type foundation** — Xrm types are based on the modern [`@types/xrm`](https://www.npmjs.com/package/@types/xrm) library.
- **Non-English support** — Support for non-English environments.
- **Improved generation performance** — Type generation is significantly faster for solutions with large numbers of entities.
- **No bundled JavaScript library** — XrmQuery and its runtime dependencies have been removed. XrmTypeScript generates types only, with no runtime overhead.
- **OData-aligned entity interfaces** — The generated entity interfaces reflect the actual structure of data returned by the Dataverse OData endpoint, making them directly usable with `Xrm.WebApi` or any HTTP client.
- **Xrm.WebApi overloads** — XrmTypeScript generates typed overloads for the native `Xrm.WebApi` methods.
- **Richer generated output** — Types include comments with display names, column types, and enum links, making the generated code more informative in your IDE.
- **`getAttribute` and `getControl` work with arrays of field names** — Iterating over a list of known field names correctly resolves each return type, instead of falling through to `undefined`.
- **Attribute and control mappings are always generated** — The `generateMappings` argument has been removed. Mappings are now included in the generated output by default.
- **`-web` argument** — Accepts a boolean instead of a namespace string. The generated entity interfaces are placed in the `WebApi` namespace by default.
- **View generation removed** — The `-views` argument and generation have been removed.
