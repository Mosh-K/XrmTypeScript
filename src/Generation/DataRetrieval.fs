module DG.XrmTypeScript.DataRetrieval

open System.Net.Http
open System.Xml

open Microsoft.OData.Edm
open Microsoft.OData.Edm.Csdl

open Utility

open CrmBaseHelper
open CrmDataHelper
open Microsoft.Xrm.Sdk.Metadata
open Microsoft.Xrm.Tooling.Connector


/// Fetch the OData CSDL $metadata XML from the Web API
let fetchCsdlXml (client: CrmServiceClient) =
  printf "Fetching OData $metadata..."
  let baseUri = client.CrmConnectOrgUriActual
  let url = $"{baseUri.Scheme}://{baseUri.Host}/api/data/v9.2/$metadata"
  use http = new HttpClient()
  http.DefaultRequestHeaders.Authorization <- Headers.AuthenticationHeaderValue("Bearer", client.CurrentAccessToken)
  let xml = http.GetStringAsync(url).Result
  use reader = XmlReader.Create(new System.IO.StringReader(xml))
  let model = CsdlReader.Parse reader
  printfn "Done!"
  model

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
let retrieveEntityMetadata entities (mainProxy: CrmServiceClient) =
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
let retrieveCrmData crmVersion entities (mainProxy: CrmServiceClient) skipInactiveForms skipForms =
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

  let csdlData =
    let nameSet = rawEntityMetadata |> Array.map (fun m -> m.LogicalName) |> Set.ofArray
    (fetchCsdlXml mainProxy).SchemaElements
    |> Seq.choose (function
      | :? IEdmEntityType as t when Set.contains t.Name nameSet ->
          Some {
            CsdlEntityInfo.Name = t.Name
            StructuralProperties = t.StructuralProperties() |> Seq.map (fun p -> p.Name) |> Array.ofSeq
            NavigationProperties = t.NavigationProperties() |> Seq.map (fun p -> p.Name) |> Array.ofSeq
          }
      | _ -> None)
    |> Array.ofSeq

  { 
    RawState.metadata = rawEntityMetadata
    info = entitiesInfo
    bpfData = bpfData
    formData = formData 
    crmVersion = crmVersion
    csdlData = csdlData
  }

/// Gets all the entities related to the given solutions and merges with the given entities
let getFullEntityList entities solutions (proxy: CrmServiceClient) =
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
