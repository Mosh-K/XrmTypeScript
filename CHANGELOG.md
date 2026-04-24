# Changelog

## [Unreleased]
### Changed
- WebEntities internal interfaces reorganized into sub-namespaces under `_`: `Scalars`, `Read`, `Write`, `Binds`, and `Lookup`, replacing the previous flat layout
- Lookup value properties (`_*_value`) now include the lookup field's logical name in their JSDoc comment
- WebEntities relationship variables are now split into typed `ManyToOne`, `OneToMany`, and `ManyToMany` sub-interfaces, directly mirroring the SDK metadata structure
- Intersect (many-to-many junction) entities now generate full interfaces identical to regular entities, replacing the previous simplified read-only path
- Intersect entity JSDoc now includes `Logical Name:`, an `Intersect Table` marker, and the two participating entities
- Relationship JSDoc comments now include a `Partner:` line with the counterpart navigation property name
- Intersect entities not in the user's entity selection are fetched from CRM automatically, restoring upstream behavior
- `activityparty` is fetched automatically when any selected entity has a PartyList attribute, restoring upstream behavior
- `Comment` refactored into named factory methods (`Basic`, `Attribute`, `Relationship`, `Entity`, `Other`) that return `string list` directly, replacing the single `Comment.Create` / `ToCommentStrings()` approach
- `Create` and `Update` interfaces restructured: bind variables are derived from raw SDK relationship metadata rather than the intermediate representation
- Relationship variables for entities not included in generation now emit `any` instead of being silently omitted
- Entity primary ID attribute promoted to a full `XrmAttribute`, carrying display name and column type metadata into generated JSDoc comments
### Fixed
- Updated `xrm.d.ts` from the original XRM library
- Fixed ManyToMany relationship navigation property names being swapped when the current entity is the second entity in the relationship

## [1.3.0] - 2026-04-15
### Changed
- Release zip reorganized: all DLLs (except `FSharp.Core.dll`) moved into a `lib/` subdirectory, keeping only user-facing files at the zip root
- Config file (`XrmTypeScript.exe.config`) is now embedded as a resource and restored from it when missing, ensuring the `lib/` probing path is always preserved

## [1.2.0] - 2026-04-14
### Changed
- `TabUnion` and `QuickFormUnion` type aliases replaced with `TabMap` and `QuickFormMap` interfaces, mapping names to their typed counterparts (consistent with `AttributeMap` and `ControlMap`)
- Renamed `appId` config key and parameter to `clientId` for clarity

## [1.1.0] - 2026-04-13
### Changed
- Lookup JSDoc "Table:" lines now truncate to 5 target entities, appending `+N more` when there are additional targets
- Refactored `Comment` type to use `XrmAttributeType` DU (24 SDK attribute types) instead of strings for type-safe formatting
- Extracted `XrmAttributeType` and `RelType` DUs to `Domain.fs` for better organization and constraint satisfaction (Set operations in form intersection)
- Moved formatting logic into `Comment.ToCommentStrings()` and `XrmAttributeType.fromDisplayName()` for cleaner separation of concerns
- Lookup logical name variables are now derived from attributes rather than relationships, using a union type of all target entity names; duplicate variable names are merged into union types instead of being renamed
- Section control interfaces now include a JSDoc comment with the tab display name and a `{@link}` reference to the containing tab section interface
- Added `{@link}` comments to `ui.tabs.get()`, `tab.sections.get()`, and `ui.quickForms.get()` overloads
- Lookup field JSDoc comments now include the target table's display name
- `SaveEventContext` is now generic (`<T extends FormContext = FormContext>`), consistent with all other event context interfaces
### Fixed
- Fixed `ui.getControl()` JSDoc enum `{@link}` rendering
- Fixed `ControlMap` generating invalid TypeScript for controls with non-identifier names (e.g. GUID-based control IDs)

## [1.0.0] - 2026-04-09
### Added
- Initial public release

[1.3.0]: https://github.com/Mosh-K/XrmTypeScript/releases/tag/v1.3.0
[1.2.0]: https://github.com/Mosh-K/XrmTypeScript/releases/tag/v1.2.0
[1.1.0]: https://github.com/Mosh-K/XrmTypeScript/releases/tag/v1.1.0
[1.0.0]: https://github.com/Mosh-K/XrmTypeScript/releases/tag/v1.0.0
