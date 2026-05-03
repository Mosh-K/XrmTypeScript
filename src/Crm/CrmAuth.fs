module DG.XrmTypeScript.CrmAuth

open System
open Microsoft.Xrm.Tooling.Connector
open System.IO

// Get Organization Service Proxy using MFA
let ensureClientIsReady (client: CrmServiceClient) =
  match client.IsReady with
  | false ->
    let s = sprintf "Client could not authenticate. If the application user was just created, it might take a while before it is available.\n%s" client.LastCrmError 
    in failwith s
  | true -> client

let internal getCrmServiceClientClientSecret (org: Uri) appId clientSecret =
  new CrmServiceClient(org, appId, CrmServiceClient.MakeSecureString(clientSecret), true, Path.Combine(Path.GetTempPath(), appId, "oauth-cache.txt"))
  |> ensureClientIsReady

let internal getCrmServiceClientConnectionString (connectionString: string option) =
  if connectionString.IsNone then failwith "Ensure connectionString is set when using ConnectionString method" else
  new CrmServiceClient(connectionString.Value)
  |> ensureClientIsReady
