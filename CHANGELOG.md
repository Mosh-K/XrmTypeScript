# Changelog

## [Unreleased]
### Changed
- Section control interfaces now include a JSDoc comment with the tab display name and a `{@link}` reference to the containing tab section interface
- Added `{@link}` comments to `ui.tabs.get()`, `tab.sections.get()`, and `ui.quickForms.get()` overloads
- Lookup field JSDoc comments now include the target table's display name
### Fixed
- Fixed `ui.getControl()` JSDoc enum `{@link}` rendering
- Fixed `ControlMap` generating invalid TypeScript for controls with non-identifier names (e.g. GUID-based control IDs)

## [1.0.0] - 2026-04-09
### Added
- Initial public release

[1.0.0]: https://github.com/Mosh-K/XrmTypeScript/releases/tag/v1.0.0
