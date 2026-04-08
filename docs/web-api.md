# Web API & Entities

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

## Usage

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
