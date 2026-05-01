module DG.XrmTypeScript.DataRetrieval

open System.Net.Http
open System.Xml

open Microsoft.OData.Edm.Csdl

open Utility

open CrmBaseHelper
open CrmDataHelper
open Microsoft.Xrm.Sdk.Metadata
open Microsoft.Xrm.Sdk
open Microsoft.Xrm.Tooling.Connector


/// Fetch the OData CSDL $metadata XML from the Web API
let fetchCsdlXml (proxy: IOrganizationService) =
  printf "Fetching OData $metadata..."
  match proxy with
  | :? CrmServiceClient as client ->
      let baseUri = client.CrmConnectOrgUriActual
      let url = $"{baseUri.Scheme}://{baseUri.Host}/api/data/v9.2/$metadata"
      use http = new HttpClient()
      http.DefaultRequestHeaders.Authorization <- Headers.AuthenticationHeaderValue("Bearer", client.CurrentAccessToken)
      let xml = http.GetStringAsync(url).Result
      use reader = XmlReader.Create(new System.IO.StringReader(xml))
      let model = CsdlReader.Parse reader
      printfn "Done!"
      Some model
  | _ ->
      printfn "Skipped (proxy is not CrmServiceClient)"
      None

/// Connect to CRM with the given authentication
let connectToCrm xrmAuth =
  printf "Connecting to CRM..."
  let proxy = proxyHelper xrmAuth ()
  printfn "Done!"
  proxy

// Retrieve CRM entity name map
let retrieveEntitiesInfo mainProxy =
  printf "Fetching entity names from CRM..."

  let arr =
    getAllEntityMetadataLight mainProxy
    |> Array.Parallel.map (fun m ->
      { LogicalName = m.LogicalName
        SchemaName = m.SchemaName
        EntitySetName = m.EntitySetName
        DisplayName = getLabel m.DisplayName })

  printfn "Done!"
  arr

// Retrieve CRM entity metadata
let retrieveEntityMetadata entities (mainProxy:IOrganizationService) =
  printf "Fetching specific entity metadata from CRM..."

  let rawEntityMetadata = 
    match entities with
    | None -> getAllEntityMetadata mainProxy
    | Some logicalNames -> 
      getSpecificEntitiesAndDependentMetadata mainProxy logicalNames

  printfn "Done!"
  rawEntityMetadata

/// Retrieve version from CRM
let retrieveCrmVersion mainProxy =
  printf "Retrieving CRM version..."

  let version = 
    CrmBaseHelper.retrieveVersion mainProxy

  printfn "Done!"
  printfn "Version: %A" (version)
  version

/// Retrieve all the necessary CRM data
let retrieveCrmData crmVersion entities (mainProxy: IOrganizationService) skipInactiveForms skipForms =
  let entitiesInfo =
    retrieveEntitiesInfo mainProxy

  let rawEntityMetadata = 
    retrieveEntityMetadata entities mainProxy
    |> Array.sortBy(fun md -> md.LogicalName)

  let bpfData = 
    match skipForms, crmVersion .>= (6, 0, 0, 0) with
    | false, true ->
      printf "Fetching BPF metadata from CRM..."
      let data = getBpfData mainProxy
      printfn "Done!"
      data
    | _ -> [||]

  let formData =
    match skipForms with
    | true -> Map.empty
    | false ->
      printf "Fetching FormXmls from CRM..."
      let data =
        rawEntityMetadata
        |> Array.filter (fun (em: EntityMetadata) -> em.IsCustomizable.Value)
        |> Array.map (fun (em: EntityMetadata) -> em.LogicalName)
        |> getEntityFormsBulk mainProxy skipInactiveForms
        |> Map.ofArray
      printfn "Done!"
      data

  { 
    RawState.metadata = rawEntityMetadata
    info = entitiesInfo
    bpfData = bpfData
    formData = formData 
    crmVersion = crmVersion
  }

/// Gets all the entities related to the given solutions and merges with the given entities
let getFullEntityList entities solutions (proxy:IOrganizationService) =
  printf "Figuring out which entities should be included in the context.."
  let solutionEntities = 
    match solutions with
    | Some sols -> 
      sols 
      |> Array.map (CrmBaseHelper.retrieveSolutionEntities proxy)
      |> Seq.concat |> Set.ofSeq
    | None -> Set.empty

  let finalEntities =
    match entities with
    | Some ents -> Set.union solutionEntities (Set.ofArray ents)
    | None -> solutionEntities

  printfn "Done!"
  match finalEntities.Count with
  | 0 -> 
    printfn "Creating context for all entities"
    None

  | _ -> 
    let entitySet = finalEntities |> Set.toArray 
    printfn "Creating context for the following entities:"
    entitySet |> Array.iter (printfn "\t- %s")
    Some entitySet
