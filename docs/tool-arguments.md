# Tool Arguments

Arguments can be passed via the `XrmTypeScript.exe.config` file or directly from the command line.

## Arguments

### Connection

| Argument         | Short-hand | Description                                  |
| :--------------- | :--------- | :------------------------------------------- |
| url              |            | URL to the organization                      |
| method           | m          | OAuth, ClientSecret or ConnectionString      |
| appId            | id         | Azure Application Id                         |
| clientSecret     | cs         | Client secret for the Azure Application      |
| returnUrl        |            | Return URL of the Azure Application |
| connectionString |            | Connection String used for authentication    |
| username         | u, usr     | Username for the CRM system                  |
| password         | p, pwd     | Password for the CRM system                  |
| domain           | d, dmn     | Domain for the user                          |
| ap               |            | Authentication Provider Type                 |

### Generation

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

## Configuration File

If no arguments are given to the executable, it will look for a configuration file in the same folder and use its settings.

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

## Command Line

```bash
XrmTypeScript.exe /url:https://INSTANCE.crm4.dynamics.com /method:ClientSecret /id:<APP_ID> /cs:<CLIENT_SECRET>
```

If you want to use a mix of the arguments from the configuration file and arguments passed to the executable,
you can use the `/useconfig` argument from the command-line.

```bash
XrmTypeScript.exe /useconfig /entities:account,contact
```
