module internal DG.XrmTypeScript.CrmBaseHelper

open System
open System.Threading.Tasks

open Utility
open Microsoft.Xrm.Sdk
open Microsoft.Xrm.Tooling.Connector
open Microsoft.Xrm.Sdk.Messages
open Microsoft.Xrm.Sdk.Query
open Microsoft.Xrm.Sdk.Metadata
open Microsoft.Crm.Sdk.Messages

// Retrieve version
let retrieveVersion (proxy: CrmServiceClient) =
  let req = RetrieveVersionRequest()
  let resp = proxy.Execute req :?> RetrieveVersionResponse
  parseVersion resp.Version

// Retrieve data
let internal retrieveMultiple (proxy:CrmServiceClient) (query:QueryExpression) = 
  query.PageInfo <- PagingInfo()

  let rec retrieveMultiple' 
    (proxy:CrmServiceClient) (query:QueryExpression) page cookie =
    seq {
        query.PageInfo.PageNumber <- page
        query.PageInfo.PagingCookie <- cookie
        let resp = proxy.RetrieveMultiple(query)
        yield! resp.Entities

        match resp.MoreRecords with
        | true -> yield! retrieveMultiple' proxy query (page + 1) resp.PagingCookie
        | false -> ()
    }
  retrieveMultiple' proxy query 1 null

// Perform requests as bulk
let performAsBulk (proxy:CrmServiceClient) requests handleResponse =
  requests
  |> Array.chunkBySize 1000
  |> Array.collect (fun chunk ->
      let request = ExecuteMultipleRequest()
      request.Requests <- OrganizationRequestCollection()
      request.Requests.AddRange(chunk)
      request.Settings <- ExecuteMultipleSettings()
      request.Settings.ContinueOnError <- false
      request.Settings.ReturnResponses <- true

      let bulkResp = proxy.Execute(request) :?> ExecuteMultipleResponse
      bulkResp.Responses
      |> Seq.map (fun resp -> 
        if isNull resp.Fault then handleResponse resp
        else failwithf "Error while retrieving entity metadata: %s" resp.Fault.Message)
      |> Seq.toArray)

// Get all entities
let internal getEntities 
  proxy (logicalName:string) (cols:string list) =

  let q = QueryExpression(logicalName)
  if cols.Length = 0 then q.ColumnSet <- ColumnSet(true)
  else q.ColumnSet <- ColumnSet(Array.ofList cols)

  retrieveMultiple proxy q

// Get all entities with a filter
let internal getEntitiesFilter 
  (proxy:CrmServiceClient) (logicalName:string)
  (cols:string list) (filter:Map<string,obj>) =
    
  let f = FilterExpression()
  filter |> Map.iter(fun k v -> f.AddCondition(k, ConditionOperator.Equal, v))

  let q = QueryExpression(logicalName)
  if cols.Length = 0 then q.ColumnSet <- ColumnSet(true)
  else q.ColumnSet <- ColumnSet(Array.ofList cols)
  q.Criteria <- f
    
  retrieveMultiple proxy q

// Retrieve entity metadata for all entities
let getAllEntityMetadataLight (proxy:CrmServiceClient) =
  let request = RetrieveAllEntitiesRequest()
  request.EntityFilters <- Microsoft.Xrm.Sdk.Metadata.EntityFilters.Entity
  let resp = proxy.Execute(request) :?> RetrieveAllEntitiesResponse
  resp.EntityMetadata

// Retrieve all metadata for all entities
let getAllEntityMetadata (proxy:CrmServiceClient) =
  let request = RetrieveAllEntitiesRequest()
  request.EntityFilters <- Microsoft.Xrm.Sdk.Metadata.EntityFilters.All
  request.RetrieveAsIfPublished <- false

  let resp = proxy.Execute(request) :?> RetrieveAllEntitiesResponse
  resp.EntityMetadata

// Make retrieve request
let getEntityMetadataRequest lname = 
  let request = RetrieveEntityRequest()
  request.LogicalName <- lname
  request.EntityFilters <- Microsoft.Xrm.Sdk.Metadata.EntityFilters.All
  request.RetrieveAsIfPublished <- false
  request

// Retrieve single entity metadata
let getEntityMetadata (proxy:CrmServiceClient) lname =
  let resp = proxy.Execute(getEntityMetadataRequest lname) :?> RetrieveEntityResponse
  resp.EntityMetadata
    

// Retrieve entity metadata with a bulk request
let getEntityMetadataBulk proxy lnames =
  let requests = 
    lnames 
    |> Array.map (getEntityMetadataRequest >> fun x -> x :> OrganizationRequest)

  let handleRespnose (resp: ExecuteMultipleResponseItem) = (resp.Response :?> RetrieveEntityResponse).EntityMetadata
  
  performAsBulk proxy requests handleRespnose


// Make a task of a function
let makeAsyncTask  (f : unit->'a) = 
  async { return! Task<'a>.Factory.StartNew( new Func<'a>(f) ) |> Async.AwaitTask }


// Retrieve all optionset metadata
let getAllOptionSetMetadata (proxy:CrmServiceClient) =
  let request = RetrieveAllOptionSetsRequest()
  request.RetrieveAsIfPublished <- true

  let resp = proxy.Execute(request) :?> RetrieveAllOptionSetsResponse
  resp.OptionSetMetadata

// Find relationship intersect entities
let findRelationEntities allLogicalNames (metadata:EntityMetadata[]) =
  metadata
  |> Array.Parallel.map (fun md ->
    md.ManyToManyRelationships
    |> Array.filter (fun m2m ->
      not (Set.contains m2m.IntersectEntityName allLogicalNames)
      && Set.contains m2m.Entity1LogicalName allLogicalNames
      && Set.contains m2m.Entity2LogicalName allLogicalNames)
    |> Array.map (fun m2m -> m2m.IntersectEntityName))
  |> Array.concat
  |> Array.distinct

// Retrieve specific entity metadata along with any intersect
let getSpecificEntitiesAndDependentMetadata proxy logicalNames =
  // TODO: either figure out the best degree of parallelism through code, or add it as a setting
  let entities = getEntityMetadataBulk proxy logicalNames

  let set = logicalNames |> Set.ofArray
  let needActivityParty =
    not (set.Contains "activityparty") &&
    entities 
    |> Array.exists (fun m -> 
      m.Attributes 
      |> Array.exists (fun a -> 
        a.AttributeTypeName = AttributeTypeDisplayName.PartyListType))

  let additionalEntities = 
    findRelationEntities set entities
    |> if needActivityParty then Array.append [|"activityparty"|] else id
    |> getEntityMetadataBulk proxy

  Array.append entities additionalEntities
  |> Array.distinctBy (fun e -> e.LogicalName)
  |> Array.sortBy (fun e -> e.LogicalName)


// Retrieves all the logical names of entities in a solution
let retrieveSolutionEntities (proxy:CrmServiceClient) solutionName =
  let solutionFilter = [("uniquename", solutionName)] |> Map.ofList
  let solutions = 
    getEntitiesFilter proxy "solution" 
      ["solutionid"; "uniquename"] solutionFilter

  let metadataById =
    getAllEntityMetadataLight proxy
    |> Array.map (fun em -> em.MetadataId, em.LogicalName)
    |> dict

  solutions
  |> Seq.map (fun sol ->
    let solutionComponentFilter = 
      [ "solutionid", sol.GetAttributeValue<obj> "solutionid" 
        "componenttype", 1 :> obj // 1 = Entity
      ] |> Map.ofList

    getEntitiesFilter proxy "solutioncomponent" 
      ["solutionid"; "objectid"; "componenttype"] solutionComponentFilter
    |> Seq.choose (fun sc -> 
      match metadataById.TryGetValue (sc.GetAttributeValue<Guid> "objectid") with
      | true, logicalName -> Some logicalName
      | _ -> None
    )
  )
  |> Seq.concat

// Proxy helper that makes it easy to get a new proxy instance
let proxyHelper xrmAuth () =
  let clientId = defaultArg xrmAuth.clientId  ""
  let clientSecret = defaultArg xrmAuth.clientSecret  ""

  match xrmAuth.method with
  | ClientSecret -> CrmAuth.getCrmServiceClientClientSecret xrmAuth.url clientId clientSecret
  | ConnectionString -> CrmAuth.getCrmServiceClientConnectionString xrmAuth.connectionString