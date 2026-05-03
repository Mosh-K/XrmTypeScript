module internal DG.XrmTypeScript.CrmDataHelper

open Microsoft.Xrm.Sdk.Messages
open Microsoft.Xrm.Sdk.Query

open CrmBaseHelper
open Microsoft.Xrm.Sdk
open Microsoft.Xrm.Tooling.Connector


// Retrieve entity form xml
let getEntityForms skipInactiveForms (lname:string) =
  let query = new QueryExpression("systemform")
  query.ColumnSet <- new ColumnSet([| "name"; "type"; "objecttypecode"; "formxml" |])

  query.Criteria.AddCondition(new ConditionExpression("objecttypecode", ConditionOperator.Equal, lname))

  if skipInactiveForms then query.Criteria.AddCondition(new ConditionExpression("formactivationstate", ConditionOperator.Equal, 1))
    
  let request = RetrieveMultipleRequest()
  request.Query <- query

  request

let getEntityFormsBulk proxy skipInactiveForms lnames =
  let requests =
    lnames 
    |> Array.map (fun lname -> getEntityForms skipInactiveForms lname :> OrganizationRequest)

  let handleResponse (resp:ExecuteMultipleResponseItem) = 
    let ec = (resp.Response :?> RetrieveMultipleResponse).EntityCollection
    ec.Entities |> Array.ofSeq

  performAsBulk proxy requests handleResponse
  |> Array.zip lnames

// Retrieve all entity form xmls
let getAllEntityForms (proxy:CrmServiceClient) skipInactiveForms =
  let query = new QueryExpression("systemform")
  query.ColumnSet <- new ColumnSet([| "name"; "type"; "objecttypecode"; "formxml" |])

  if skipInactiveForms then query.Criteria.AddCondition(new ConditionExpression("formactivationstate", ConditionOperator.Equal, 1))
    
  let request = RetrieveMultipleRequest()
  request.Query <- query
    
  let resp = proxy.Execute(request) :?> RetrieveMultipleResponse
  resp.EntityCollection.Entities 
  |> Array.ofSeq

// Retrieve fields for bpf
let getBpfData (proxy:CrmServiceClient) =
  let query = new QueryExpression("workflow")
  query.ColumnSet <- new ColumnSet([| "name"; "clientdata"; "category"; "primaryentity" |])

  query.Criteria.AddCondition(new ConditionExpression("category", ConditionOperator.Equal, 4)) // BPF
  query.Criteria.AddCondition(new ConditionExpression("clientdata", ConditionOperator.NotNull))
  let request = RetrieveMultipleRequest()
  request.Query <- query
    
  let resp = proxy.Execute(request) :?> RetrieveMultipleResponse
  resp.EntityCollection.Entities 
  |> Array.ofSeq
