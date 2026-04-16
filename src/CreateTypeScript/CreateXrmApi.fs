module internal DG.XrmTypeScript.CreateXrmApi

open Microsoft.Xrm.Sdk.Metadata
open TsStringUtil
open Constants
open Utility

// retrieveRecord<T = any>(entityLogicalName: string, id: string, options?: string): Async.PromiseLike<T>;
// retrieveMultipleRecords<T = any>(entityLogicalName: string, options?: string, maxPageSize?: number): Async.PromiseLike<RetrieveMultipleResult<T>>;

(** XrmApi definitions *)
let getRetrieveFuncs (rawEntities: EntityMetadata array) =
  rawEntities
  |> Array.toList
  |> List.filter (fun e -> not (e.IsIntersect.GetValueOrDefault true))
  |> List.sortBy (fun e -> e.LogicalName)
  |> List.map (fun e ->
    Function.Create(
      "retrieveRecord",
      [ Variable.Create("entityLogicalName", getConstantType e.LogicalName)
        Variable.Create("id", TsType.String)
        Variable.Create("options", TsType.String, optional = true) ],
      TsType.Generic("Async.PromiseLike", $"{WEB_NS}.{e.SchemaName}"),
      Comment.Create (getLabel e.DisplayName)
    ))

let getRetrieveMultipleFuncs (rawEntities: EntityMetadata array) =
  rawEntities
  |> Array.toList
  |> List.filter (fun e -> not (e.IsIntersect.GetValueOrDefault true))
  |> List.sortBy (fun e -> e.LogicalName)
  |> List.map (fun e ->
    Function.Create(
      "retrieveMultipleRecords",
      [ Variable.Create("entityLogicalName", getConstantType e.LogicalName)
        Variable.Create("options", TsType.String, optional = true)
        Variable.Create("maxPageSize", TsType.Number, optional = true) ],
      TsType.Generic("Async.PromiseLike", $"RetrieveMultipleResult<{WEB_NS}.{e.SchemaName}>"),
      Comment.Create (getLabel e.DisplayName)
    ))
