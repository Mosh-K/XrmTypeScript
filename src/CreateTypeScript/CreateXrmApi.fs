module internal DG.XrmTypeScript.CreateXrmApi

open TsStringUtil
open Constants
open IntermediateRepresentation

// retrieveRecord<T = any>(entityLogicalName: string, id: string, options?: string): Async.PromiseLike<T>;
// retrieveMultipleRecords<T = any>(entityLogicalName: string, options?: string, maxPageSize?: number): Async.PromiseLike<RetrieveMultipleResult<T>>;

(** XrmApi definitions *)
let getRetrieveFuncs state =
  state.entities
  |> Array.toList
  |> List.filter (fun e -> not e.isIntersect)
  |> List.sortBy (fun e -> e.logicalName)
  |> List.map (fun e ->
    Function.Create(
      "retrieveRecord",
      [ Variable.Create("entityLogicalName", getConstantType e.logicalName)
        Variable.Create("id", TsType.String)
        Variable.Create("options", TsType.String, optional = true) ],
      TsType.Generic("Async.PromiseLike", $"{WEB_NS}.{e.schemaName}"),
      Comment.Create e.displayName
    ))

let getRetrieveMultipleFuncs state =
  state.entities
  |> Array.toList
  |> List.filter (fun e -> not e.isIntersect)
  |> List.sortBy (fun e -> e.logicalName)
  |> List.map (fun e ->
    Function.Create(
      "retrieveMultipleRecords",
      [ Variable.Create("entityLogicalName", getConstantType e.logicalName)
        Variable.Create("options", TsType.String, optional = true)
        Variable.Create("maxPageSize", TsType.Number, optional = true) ],
      TsType.Generic("Async.PromiseLike", $"RetrieveMultipleResult<{WEB_NS}.{e.schemaName}>"),
      Comment.Create e.displayName
    ))
