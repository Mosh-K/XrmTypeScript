module internal DG.XrmTypeScript.CreateWebEntities

open Utility
open TsStringUtil
open Constants
open InterpretCommon
open IntermediateRepresentation
open Microsoft.Xrm.Sdk.Metadata


let INTERNAL_NS = "_"

(** Interface name helper functions *)
let withNamespace (ns: string) (str: string) = $"{ns}.{str}"

let currencyId = {
  XrmAttribute.logicalName = "transactioncurrencyid"
  schemaName = "TransactionCurrencyId"
  specialType = SpecialType.EntityReference
  varType = TsType.String
  typeName = AttributeTypeDisplayName.LookupType.Value
  targetEntitySets = Some [| "transactioncurrency", "transactioncurrencies", "Currency" |]
  readable = true
  createable = true
  updateable = true
  displayName = "Currency"
}

let entityTag = 
  Variable.Create("\"@odata.etag\"", TsType.String)

let entityId (e: XrmEntity, optional ) =
  Variable.Create(e.idAttribute, TsType.String, optional = optional)

let logicalName (a: XrmAttribute) = a.logicalName
let valueInfix (s: string) = $"_{s}_value"
let guidName (a: XrmAttribute) = valueInfix a.logicalName

let formattedName (a: XrmAttribute) =
  let format = "@OData.Community.Display.V1.FormattedValue"

  match a.specialType with
  | SpecialType.EntityReference -> $"\"{valueInfix a.logicalName}{format}\""
  | _ -> $"\"{a.logicalName}{format}\""

let lookupName (a: XrmAttribute) =
  $"\"{valueInfix a.logicalName}@Microsoft.Dynamics.CRM.lookuplogicalname\""

let bindType (r: XrmRelationship) =
  TsType.Custom $"`/{r.relatedSetName}(${{string}})`"

(** Various type helper functions *)
let arrayOf = TsType.Custom >> TsType.Array
let varsToType =
  varsToInlineInterfaceString >> TsType.Custom

let sortByName = List.sortBy (fun (x: Variable) -> x.name)
let assignUniqueNames =
  List.groupBy (fun (var: Variable) -> var.name)
  >> List.map (fun (_, var) -> 
         var
//         |> Array.sortBy (fun var -> var.guid)
         |> List.mapi (fun i var -> 
                    if i = 0 then var
                    else
                        match var.name.StartsWith "\"" with
                        | true  -> { var with name = $"{var.name.TrimEnd '"'}{i}\"" }
                        | false -> { var with name = $"{var.name}{i}" }))
  >> List.concat
//  >> sortByName

let flattenUnion = function
  | TsType.Union x -> x
  | t -> [ t ]

let groupByName (vars: Variable list) =
  vars
  |> List.groupBy (fun v -> v.name)
  |> List.map (fun (_, vs) ->
    if vs.Length = 1 then vs.Head else
    let types =
      vs
      |> List.choose (fun v -> v.varType)
      |> List.collect flattenUnion
      |> List.distinct
    { vs.Head with varType = Some (TsType.Union types) })

let concatDistinctSort = 
  List.concat >> List.distinctBy (fun (x: Variable) -> x.name) >> sortByName

let hasFormattedValue a = 
  match a.specialType, a.varType with
  | SpecialType.EntityReference, _
  | SpecialType.Money, _
  | SpecialType.OptionSet, _
  | SpecialType.MultiSelectOptionSet, _
  | _, TsType.Date -> true
  | _ -> false

(** Definition functions *)
let defToBaseVars (a, comment, ty, nameTransform) =
  Variable.Create(nameTransform ?| logicalName <| a, TsType.Union [ ty; TsType.Null ], comment, optional = true) 

let defToResVars (a, comment, ty, nameTransform) =
  Variable.Create(nameTransform ?| logicalName <| a, ty, comment, optional = true) 

let defToFormattedVars (a, comment, _, _) =
  Variable.Create(formattedName a, TsType.String, comment, optional = true  ) 

let getEntityRefDef nameFormat (a: XrmAttribute) =
  nameFormat a, [ a, Comment.Create (a.displayName, colType = a.typeName), a.varType, Some guidName ]

let getResultDef (ent: XrmEntity) (attr: XrmAttribute) = 
  let vType = attr.varType
  let name = attr.logicalName
  let comment = Comment.Create(attr.displayName, colType = attr.typeName, link = getLink ent attr)

  match attr.specialType with
  | SpecialType.EntityReference -> getEntityRefDef guidName attr
  | SpecialType.Money -> name, [ attr, comment, vType, None; currencyId, comment, TsType.String, Some guidName ]
  | SpecialType.Decimal -> name, [ attr, comment, TsType.Number, None ]
  | SpecialType.MultiSelectOptionSet -> name, [ attr, comment, TsType.String, None ]
  | _ -> name, [ attr, comment, vType, None ]

(** Variable functions *)
let getBindVariables isCreate isUpdate attrMap (r: XrmRelationship) =
  Map.tryFind r.attributeName attrMap
  ?>> fun attr ->
    match r.referencing && isCreate = attr.createable && isUpdate = attr.updateable with
    | false -> None
    | true  -> Some $"\"{r.navProp}@odata.bind\""
  ?|> fun name -> Variable.Create(name, bindType r, Comment.Create r.displayName, optional = true)

let getResultVariable (a: XrmAttribute) = 
  match a.specialType with
  | SpecialType.EntityReference -> getEntityRefDef guidName a |> snd |> List.map defToResVars
  | _ -> []

let getRelationVars (forCreate: bool) (r: XrmRelationship) = 
  let interfaceName = if forCreate then $"{r.relatedSchemaName}.{CREATE_INTERFACE}" else r.relatedSchemaName

  TsType.Custom interfaceName
  |> 
    match r.referencing with
    | true  -> id
    | false -> TsType.Array
  |> fun ty -> Variable.Create(r.navProp, TsType.Union [ ty; TsType.Null ], Comment.Create (r.displayName, relType = r.relType), optional = true)

let getFormattedResultVariable  (ent: XrmEntity) (attr: XrmAttribute) = 
  match hasFormattedValue attr with
  | true  -> getResultDef ent attr |> snd |> List.map defToFormattedVars
  | false -> []

let getLookupNameVariable (a: XrmAttribute) =
  match a.targetEntitySets with
  | None
  | Some [||] -> None
  | Some tes ->
    let unionType =
      tes
      |> Array.map (fun (n, _, _) -> TsType.Custom $"\"{n}\"")
      |> Array.toList
      |> TsType.Union

    Some(
      Variable.Create(
        lookupName a,
        unionType,
        Comment.Create(a.displayName, colType = a.typeName),
        optional = true
      )
    )

let getBaseVariable (ent: XrmEntity) (attr: XrmAttribute) = 
  match attr.specialType with
  | SpecialType.EntityReference -> []
  | _ -> getResultDef ent attr |> snd |> List.map defToBaseVars

(** Code creation methods *)
type EntityInterfaces = {
  _base: Interface
  resultRelationships: Interface
  createRelationships: Interface
  formattedResult: Interface
  lookupResult: Interface
  createAndUpdate: Interface
  create: Interface
  update: Interface
  result: Interface
}

let getBlankEntityInterfaces (e: XrmEntity) =
  let comment = Comment.Create e.displayName

  let bn = "Base"
  let rrn = "Relationships"
  let crn = "CreateRelationships"
  let frn = "FormattedResult"
  let lrn = "LookupResult"
  let cu = "CreateAndUpdate"

  { _base = Interface.Create bn
    resultRelationships = Interface.Create rrn
    createRelationships = Interface.Create crn
    formattedResult = Interface.Create frn
    lookupResult = Interface.Create lrn
    createAndUpdate = Interface.Create(cu, extends = ([ bn; crn ] |> List.map (withNamespace INTERNAL_NS)))
    create = Interface.Create(CREATE_INTERFACE, comment, [ $"{INTERNAL_NS}.{cu}" ])
    update = Interface.Create(UPDATE_INTERFACE, comment, [ $"{INTERNAL_NS}.{cu}" ])
    result =
      Interface.Create(
        e.schemaName,
        comment,
        [ bn; rrn; frn; lrn ] |> List.map (withNamespace $"{e.schemaName}.{INTERNAL_NS}")
      ) }
        
/// Create entity interfaces
let getEntityInterfaceLines (e: XrmEntity) =
  let entityInterfaces = getBlankEntityInterfaces e

  let attrMap = e.attributes |> List.map (fun a -> a.logicalName, a) |> Map.ofList

  let createAndUpdate =
    [ { entityInterfaces.create with
          vars =
            e.allRelationships
            |> List.choose (getBindVariables true false attrMap)
            |> groupByName
            |> sortByName }
      { entityInterfaces.update with
          vars =
            e.allRelationships
            |> List.choose (getBindVariables false true attrMap)
            |> groupByName
            |> sortByName } ]

  let result =
    [ { entityInterfaces.result with
          vars =
            entityTag
            :: entityId (e, false)
            :: (List.map getResultVariable e.attributes |> concatDistinctSort) } ]

  let internalInterfaces =
    [ { entityInterfaces._base with
          vars = e.attributes |> List.map (getBaseVariable e) |> concatDistinctSort }
      { entityInterfaces.resultRelationships with
          vars =
            e.allRelationships
            |> List.map (getRelationVars false)
            |> groupByName
            |> sortByName }
      { entityInterfaces.createRelationships with
          vars =
            e.allRelationships
            |> List.map (getRelationVars true)
            |> groupByName
            |> sortByName }
      { entityInterfaces.createAndUpdate with
          vars =
            entityId (e, true)
            :: (e.allRelationships
                |> List.choose (getBindVariables true true attrMap)
                |> groupByName
                |> sortByName) }
      { entityInterfaces.formattedResult with
          vars = e.attributes |> List.map (getFormattedResultVariable e) |> concatDistinctSort }
      { entityInterfaces.lookupResult with
          vars =
            e.attributes
            |> List.choose getLookupNameVariable
            |> sortByName } ]

  Namespace.Create(
    WEB_NS,
    declare = true,
    interfaces = result,
    namespaces =
      [ Namespace.Create(
          e.schemaName,
          interfaces = createAndUpdate,
          namespaces = [ Namespace.Create(INTERNAL_NS, interfaces = internalInterfaces) ]
        ) ]
  )
  |> CreateCommon.skipNsIfEmpty
