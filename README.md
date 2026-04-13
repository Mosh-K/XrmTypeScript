# XrmTypeScript

[![CI](https://github.com/Mosh-K/XrmTypeScript/actions/workflows/ci.yml/badge.svg)](https://github.com/Mosh-K/XrmTypeScript/actions/workflows/ci.yml)
[![Release](https://img.shields.io/github/v/release/Mosh-K/XrmTypeScript)](https://github.com/Mosh-K/XrmTypeScript/releases/latest)
[![Attestation](https://img.shields.io/badge/build-attested-brightgreen?logo=github)](https://github.com/Mosh-K/XrmTypeScript/attestations)

XrmTypeScript generates TypeScript declaration files from your Dynamics 365 / Power Apps solution,
giving you full intellisense and compile-time type safety for form scripting and Web API development.

Instead of guessing attribute names and types at runtime, you get autocomplete for your actual entities,
attributes, and forms — and catch errors before they reach your CRM instance.

The generated entity interfaces work anywhere you query the Dataverse API — on forms via `Xrm.WebApi`,
in PCF components, or in external TypeScript/JavaScript projects.

## Getting Started

1. Download and unzip the latest release from [GitHub Releases](https://github.com/Mosh-K/XrmTypeScript/releases)
2. Edit `XrmTypeScript.exe.config` with your environment details:

```xml
<appSettings>
  <add key="url" value="https://INSTANCE.crm4.dynamics.com" />
  <add key="method" value="ClientSecret" />
  <add key="appId" value="YOUR_APP_ID" />
  <add key="clientSecret" value="YOUR_CLIENT_SECRET" />

  <add key="out" value="../typings/XRM" />
  <add key="solutions" value="" />
  <add key="entities" value="account, contact" />
  <add key="web" value="true" />
</appSettings>
```

3. Run `XrmTypeScript.exe`


## Project Setup

If you haven't worked with TypeScript tooling before, you'll need a `tsconfig.json` in your project root — the same root that contains your scripts and the generated typings folder.

```json
{
  "compilerOptions": {
    "allowJs": true,
    // "checkJs": true,  // uncomment to get type errors in your JS files

    "noEmit": true,

    "moduleDetection": "force"
  }
}
```


## Documentation

- [Tool Arguments](docs/tool-arguments.md)
- [Form Scripting](docs/form-scripting.md)
- [Web API & Entities](docs/web-api.md)
- [Comparison with XrmDefinitelyTyped](docs/comparison.md)

## Acknowledgements

XrmTypeScript is a fork of [XrmDefinitelyTyped](https://github.com/delegateas/XrmDefinitelyTyped) by Delegateas,
which created the original project of generating TypeScript declaration files for Dynamics CRM development.
XrmTypeScript has taken a different direction, but the foundation that Delegateas built made this work possible.
