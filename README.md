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

   > You can verify the build attestation with the [GitHub CLI](https://cli.github.com/):  
   > `gh attestation verify --repo Mosh-K/XrmTypeScript XrmTypeScript-vX.X.X-bin.zip`

&nbsp;

2. Edit `XrmTypeScript.exe.config` with your environment details:

```xml
<?xml version="1.0" encoding="utf-8"?>
<configuration>
  <appSettings>
    <add key="url" value="https://INSTANCE.crm4.dynamics.com" />
    <add key="method" value="ClientSecret" />
    <add key="clientId" value="YOUR_CLIENT_ID" />
    <add key="clientSecret" value="YOUR_CLIENT_SECRET" />
  
    <add key="out" value="../typings/XRM" />
    <add key="solutions" value="" />
    <add key="entities" value="account, contact" />
    <add key="web" value="true" />
  </appSettings>
</configuration>
```

3. Run `XrmTypeScript.exe`


## Project Setup

If you haven't worked with TypeScript tooling before, you'll need a `tsconfig.json` in your project root — the same root that contains your scripts and the generated typings folder.

```json
{
  "compilerOptions": {
    "target": "ESNext",
    "allowJs": true,
    // "checkJs": true,  // uncomment to get type errors in your JS files
    "moduleDetection": "force",
    "noEmit": true
  }
}
```


## Form Scripting

XrmTypeScript generates form-specific TypeScript interfaces for your Dynamics 365 forms,
giving you full intellisense and type safety.

### Specific Account Form Example

In this example, we want to add code to the standard Account form called
'Information', which is of the 'Main' form type. This can be found at
`Form/account/Main/Information.d.ts`. Simply by adding this to your TypeScript context,
you get access to the specific Xrm object model API for that form.

#### JavaScript

Use JSDoc comments to get intellisense and type support:

```javascript
/**
 * @param {Xrm.Events.LoadEventContext} executionContext
 */
function onLoad(executionContext) {
  /** @type {Form.account.Main.Information} */
  let form = executionContext.getFormContext();
  // Code here..
  form.getAttribute("accountnumber").addOnChange(checkAccount);
}

/**
 * @param {Xrm.ChangeEventContext<Form.account.Main.Information>} executionContext
 */
function checkAccount(executionContext) {
  let form = executionContext.getFormContext();
  // Code here..
  let accountNumber = form.getAttribute("accountnumber").getValue();
}
```

#### TypeScript

```typescript
function onLoad(executionContext: Xrm.LoadEventContext) {
  let form: Form.account.Main.Information = executionContext.getFormContext();
  // Code here..
  form.getAttribute("accountnumber").addOnChange(checkAccount);
}

function checkAccount(executionContext: Xrm.ChangeEventContext<Form.account.Main.Information>) {
  let form = executionContext.getFormContext();
  // Code here..
  let accountNumber = form.getAttribute("accountnumber").getValue();
}
```

> Remember to tick **Pass execution context as first parameter** in CRM when adding your function to the form.

### Type Safety

In your desired IDE, you will now get intellisense for that specific
account form, with all of its attributes, controls, tabs and sections.

Each attribute and control on the form has the correct type, giving you type safety on values and functions.
If an invalid string is entered as an argument to one of the string-specific functions, the compiler will
show an error — making it easy to catch incorrect attribute names or attempts to access elements that are
not present on the form.


## Web API & Entities

XrmTypeScript generates TypeScript interfaces that precisely reflect the structure of data returned
by the Dataverse OData endpoint. Every entity in your solution gets its own set of interfaces —
one for retrieval, one for create, and one for update — each typed to the exact shape the API
expects or returns.

This means you get full compile-time safety across your entire data layer: the correct property
names and types on retrieved records, typed nested objects for deep insert, the `@odata.bind`
syntax for associating existing records, formatted values and lookup metadata available as typed
properties, and relationship traversal with full intellisense on related records. Mistakes that
would previously only surface at runtime are caught by the compiler before they reach your CRM instance.

The generated interfaces work anywhere you query the Dataverse API — on forms via `Xrm.WebApi`,
in PCF components, or in external TypeScript projects using `fetch` or any HTTP client.

### Retrieve

```typescript
// Using Xrm.WebApi
const contact: WebApi.Contact = await Xrm.WebApi.retrieveRecord("contact", id, options);

// Using fetch
const response = await fetch(`/api/data/v9.2/contacts(${id})${options}`);
const contact: WebApi.Contact = await response.json();

// TypeScript knows birthdate is Date | null | undefined
const birthdate = contact.birthdate;
// Formatted values are typed as string | undefined
const formattedBirthdate = contact["birthdate@OData.Community.Display.V1.FormattedValue"];

// Lookup — intellisense shows all SystemUser properties
const createdBy = contact.createdby;
const createdByName = createdBy?.nickname;

// Related collection — each account is fully typed as WebApi.Account
const accounts = contact.account_primary_contact;
accounts?.forEach((account) => {
  const name = account.name; // string | null | undefined
  const shippingMethodFormatted = account["address2_shippingmethodcode@OData.Community.Display.V1.FormattedValue"];
});
```

### Create

```typescript
const newContact: WebApi.Contact.Create = {
  firstname: "John",
  lastname: "Doe",
  emailaddress1: "john.doe@example.com",
  birthdate: new Date("1990-01-01"),
  // Deep insert — compiler enforces the shape of the nested Account
  parentcustomerid_account: {
    creditlimit: 10000,
    name: "Contoso",
  }
};

// Using Xrm.WebApi
await Xrm.WebApi.createRecord("contact", newContact);

// Using fetch
await fetch("/api/data/v9.2/contacts", {
  method: "POST",
  headers: { "Content-Type": "application/json" },
  body: JSON.stringify(newContact),
});
```

### Update

```typescript
const updatedContact: WebApi.Contact.Update = {
  firstname: "Jane",
  lastname: "Smith",
  emailaddress1: "jane.smith@example.com",
  // @odata.bind — typed as a template literal to enforce the correct URL format
  "parentcustomerid_account@odata.bind": "/accounts(00000000-0000-0000-0000-000000001234)",
};

// Using Xrm.WebApi
await Xrm.WebApi.updateRecord("contact", "00000000-0000-0000-0000-000000000001", updatedContact);

// Using fetch
await fetch("/api/data/v9.2/contacts(00000000-0000-0000-0000-000000000001)", {
  method: "PATCH",
  headers: { "Content-Type": "application/json" },
  body: JSON.stringify(updatedContact),
});
```


## Tool Arguments

Arguments can be passed via the `XrmTypeScript.exe.config` file or directly from the command line.

### Arguments

#### Connection

| Argument         | Short-hand | Description                                  |
| :--------------- | :--------- | :------------------------------------------- |
| url              |            | URL to the organization                      |
| method           | m          | OAuth, ClientSecret or ConnectionString      |
| clientId         | id         | Azure Application Id                         |
| clientSecret     | cs         | Client secret for the Azure Application      |
| returnUrl        |            | Return URL of the Azure Application          |
| connectionString |            | Connection String used for authentication    |
| username         | u, usr     | Username for the CRM system                  |
| password         | p, pwd     | Password for the CRM system                  |
| domain           | d, dmn     | Domain for the user                          |
| ap               |            | Authentication Provider Type                 |

#### Generation

| Argument          | Short-hand | Description                                                                                                      |
| :---------------- | :--------- | :--------------------------------------------------------------------------------------------------------------- |
| out               | o          | Output directory for the generated declaration files                                                             |
| solutions         | ss         | Comma-separated list of solution names. Generates code for the entities found in these solutions.                |
| entities          | es         | Comma-separated list of entity logical names. Additive with the entities retrieved via the `solutions` argument. |
| web               | w          | Set to true to generate declaration files for Web entities                                                       |
| skipXrmApi        | sxa        | Set to true to skip generation of Xrm.WebApi overloads                                                           |
| crmVersion        | cv         | Version of CRM to generate declaration files for                                                                 |
| oneFile           | of         | Set to true to put all dynamic parts of the generated declarations into a single file                            |
| skipForms         | sf         | Set to true to skip generation of form declaration files                                                         |
| skipInactiveForms | sif        | Set to true to avoid generating types for inactive forms                                                         |
| useDeprecated     | ud         | Set to true to include typings for deprecated functionality                                                      |
| useconfig         | uc         | Also applies the arguments found in the `.config` file                                                           |

You can also view this list of arguments using the `/help` argument.

### Command Line

```bash
XrmTypeScript.exe /url:https://INSTANCE.crm4.dynamics.com /method:ClientSecret /id:<APP_ID> /cs:<CLIENT_SECRET>
```

If you want to use a mix of the arguments from the configuration file and arguments passed to the executable,
you can use the `/useconfig` argument from the command-line.

```bash
XrmTypeScript.exe /useconfig /entities:account,contact
```


## XrmTypeScript vs XrmDefinitelyTyped

XrmTypeScript is a fork of [XrmDefinitelyTyped](https://github.com/delegateas/XrmDefinitelyTyped) by Delegateas.
The following is a list of the key differences.

### What's Different

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


## Acknowledgements

XrmTypeScript is a fork of [XrmDefinitelyTyped](https://github.com/delegateas/XrmDefinitelyTyped) by Delegateas,
which created the original project of generating TypeScript declaration files for Dynamics CRM development.
XrmTypeScript has taken a different direction, but the foundation that Delegateas built made this work possible.
