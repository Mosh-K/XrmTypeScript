# Form Scripting

XrmTypeScript generates form-specific TypeScript interfaces for your Dynamics 365 forms,
giving you full intellisense and type safety.

## Specific Account Form Example

In this example, we want to add code to the standard Account form called
'Information', which is of the 'Main' form type. This can be found at
`Form/account/Main/Information.d.ts`. Simply by adding this to your TypeScript context,
you get access to the specific Xrm object model API for that form.

### JavaScript

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

### TypeScript

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

## Type Safety

In your desired IDE, you will now get intellisense for that specific
account form, with all of its attributes, controls, tabs and sections.

Each attribute and control on the form has the correct type, giving you type safety on values and functions.
If an invalid string is entered as an argument to one of the string-specific functions, the compiler will
show an error — making it easy to catch incorrect attribute names or attempts to access elements that are
not present on the form.
