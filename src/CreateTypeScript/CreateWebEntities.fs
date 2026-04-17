module internal DG.XrmTypeScript.CreateWebEntities

open Microsoft.Xrm.Sdk.Metadata
open Utility
open Constants
open InterpretCommon
open IntermediateRepresentation
open InterpretEntityMetadata


let sanitizeNavProp (s: string) =
  if s = null then "navigationPropertyNameNotDefined" else s

let INTERNAL_NS = "_"

(** Interface name helper functions *)
let withNamespace (ns: string) (str: string) = $"{ns}.{str}"

let currencyId = {
  XrmAttribute.logicalName = "transactioncurrencyid"
  schemaName = "TransactionCurrencyId"
  specialType = SpecialType.EntityReference
  varType = TsType.String
  colType = XrmAttributeType.Lookup
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

(** Various type helper functions *)
let sortByName = List.sortBy (fun (x: Variable) -> x.name)

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
  nameFormat a, [ a, Comment.Create (a.displayName, colType = a.colType, ?tes = a.targetEntitySets), a.varType, Some guidName ]

let getResultDef (options: OptionSet list) (attr: XrmAttribute) = 
  let vType = attr.varType
  let name = attr.logicalName
  let comment = Comment.Create(attr.displayName, colType = attr.colType, ?tes = attr.targetEntitySets, link = getLink options attr)

  match attr.specialType with
  | SpecialType.EntityReference -> getEntityRefDef guidName attr
  | SpecialType.Money -> name, [ attr, comment, vType, None; currencyId, comment, TsType.String, Some guidName ]
  | SpecialType.Decimal -> name, [ attr, comment, TsType.Number, None ]
  | SpecialType.MultiSelectOptionSet -> name, [ attr, comment, TsType.String, None ]
  | _ -> name, [ attr, comment, vType, None ]

(** Variable functions *)
let getBindVariables (nameMap: Map<string, EntityInfo>) isUpdate attrMap (rel: OneToManyRelationshipMetadata) =
  Map.tryFind rel.ReferencingAttribute attrMap
  ?>> fun attr ->
    (match attr.createable && isUpdate = attr.updateable with
     | false -> None
     | true ->
       Some $"\"{rel.ReferencingEntityNavigationPropertyName |> sanitizeNavProp}@odata.bind\"")
    ?>> fun name ->
      let relatedInfo = resolveRelatedEntities nameMap rel.ReferencedEntity
      match relatedInfo with
      | [] -> None
      | _  ->
        let bindType =
          relatedInfo
          |> List.map (fun e -> TsType.Custom $"`/{e.EntitySetName}(${{string}})`")
          |> TsType.Union
        Some(Variable.Create(
          name,
          bindType,
          Comment.Create(relatedInfo |> List.map (fun e -> e.DisplayName) |> String.concat " | "),
          optional = true
        ))

let getResultVariable (a: XrmAttribute) = 
  match a.specialType with
  | SpecialType.EntityReference -> getEntityRefDef guidName a |> snd |> List.map defToResVars
  | _ -> []

let private toInterfaceName forCreate schemaName =
  if forCreate then $"{schemaName}.{CREATE_INTERFACE}" else schemaName

let getManyToOneVar (nameMap: Map<string, EntityInfo>) (forCreate: bool) (rel: OneToManyRelationshipMetadata) =
  let relatedInfo = resolveRelatedEntities nameMap rel.ReferencedEntity
  match relatedInfo with
  | [] -> None
  | _  ->
    let varType =
      relatedInfo
      |> List.map (fun e -> TsType.Custom(toInterfaceName forCreate e.SchemaName))
      |> fun tys -> TsType.Union(tys @ [ TsType.Null ])
    Some(Variable.Create(
      rel.ReferencingEntityNavigationPropertyName |> sanitizeNavProp,
      varType,
      Comment.Create(relatedInfo |> List.map (fun e -> e.DisplayName) |> String.concat " | ", relType = RelType.ManyToOne),
      optional = true
    ))

let getOneToManyVar (nameMap: Map<string, EntityInfo>) (forCreate: bool) (rel: OneToManyRelationshipMetadata) =
  Map.tryFind rel.ReferencingEntity nameMap
  |> Option.map (fun eInfo ->
    let varType =
      TsType.Union
        [ TsType.Array(TsType.Custom(toInterfaceName forCreate eInfo.SchemaName))
          TsType.Null ]
    Variable.Create(
      rel.ReferencedEntityNavigationPropertyName |> sanitizeNavProp,
      varType,
      Comment.Create(eInfo.DisplayName, relType = RelType.OneToMany),
      optional = true
    ))

let getManyToManyVar (nameMap: Map<string, EntityInfo>) (forCreate: bool) logicalName (rel: ManyToManyRelationshipMetadata) =
  let navProp =
    if logicalName = rel.Entity2LogicalName then rel.Entity1NavigationPropertyName
    else rel.Entity2NavigationPropertyName
  let otherLogical =
    if logicalName = rel.Entity2LogicalName then rel.Entity1LogicalName
    else rel.Entity2LogicalName
  Map.tryFind otherLogical nameMap
  |> Option.map (fun eInfo ->
    let varType =
      TsType.Union
        [ TsType.Array(TsType.Custom(toInterfaceName forCreate eInfo.SchemaName))
          TsType.Null ]
    Variable.Create(
      navProp |> sanitizeNavProp,
      varType,
      Comment.Create(eInfo.DisplayName, relType = RelType.ManyToMany),
      optional = true
    ))

let getFormattedResultVariable  (options: OptionSet list) (attr: XrmAttribute) = 
  match hasFormattedValue attr with
  | true  -> getResultDef options attr |> snd |> List.map defToFormattedVars
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
        $"\"{valueInfix a.logicalName}@Microsoft.Dynamics.CRM.lookuplogicalname\"",
        unionType,
        Comment.Create(a.displayName, colType = a.colType, ?tes = a.targetEntitySets),
        optional = true
      )
    )

let getBaseVariable (options: OptionSet list) (attr: XrmAttribute) = 
  match attr.specialType with
  | SpecialType.EntityReference -> []
  | _ -> getResultDef options attr |> snd |> List.map defToBaseVars

(** Code creation methods *)
type EntityInterfaces = {
  _base: Interface
  resultOneToMany: Interface
  resultManyToMany: Interface
  resultRelationships: Interface
  createOneToMany: Interface
  createManyToMany: Interface
  createRelationships: Interface
  formattedResult: Interface
  lookupResult: Interface
  create: Interface
  update: Interface
  result: Interface
}

let getBlankEntityInterfaces (e: XrmEntity) =
  let comment = Comment.Create e.displayName

  let bn = "Base"
  let ro2m = "ResultOneToMany"
  let rm2m = "ResultManyToMany"
  let rrn = "ResultRelationships"
  let co2m = "CreateOneToMany"
  let cm2m = "CreateManyToMany"
  let crn = "CreateRelationships"
  let frn = "FormattedResult"
  let lrn = "LookupResult"

  { _base = Interface.Create bn
    resultOneToMany = Interface.Create ro2m
    resultManyToMany = Interface.Create rm2m
    resultRelationships = Interface.Create (rrn, extends = [ ro2m; rm2m ])
    createOneToMany = Interface.Create co2m
    createManyToMany = Interface.Create cm2m
    createRelationships = Interface.Create (crn, extends = [ co2m; cm2m ])
    formattedResult = Interface.Create frn
    lookupResult = Interface.Create lrn
    update = Interface.Create(UPDATE_INTERFACE, comment, [ bn; crn ] |> List.map (withNamespace INTERNAL_NS))
    create = Interface.Create(CREATE_INTERFACE, comment, [ UPDATE_INTERFACE ])
    result =
      Interface.Create(
        e.schemaName,
        comment,
        [ bn; rrn; frn; lrn ] |> List.map (withNamespace $"{e.schemaName}.{INTERNAL_NS}")
      ) }
        
/// Create entity interfaces
let getEntityInterfaceLines (nameMap: Map<string, EntityInfo>) (e: XrmEntity) =
  let entityInterfaces = getBlankEntityInterfaces e

  let attrMap = e.attributes |> List.map (fun a -> a.logicalName, a) |> Map.ofList

  let update =
    { entityInterfaces.update with
        vars =
          entityId (e, true)
          :: (e.manyToOneRelationships
              |> List.choose (getBindVariables nameMap true attrMap)
              |> sortByName) }

  let create =
    { entityInterfaces.create with
        vars =
          e.manyToOneRelationships
          |> List.choose (getBindVariables nameMap false attrMap)
          |> sortByName }

  let result =
    { entityInterfaces.result with
        vars =
          entityTag
          :: entityId (e, false)
          :: (List.map getResultVariable e.attributes |> concatDistinctSort) }

  let internalInterfaces =
    [ { entityInterfaces._base with vars = e.attributes |> List.map (getBaseVariable e.optionSets) |> concatDistinctSort }
      { entityInterfaces.resultOneToMany with vars = e.oneToManyRelationships |> List.choose (getOneToManyVar nameMap false) |> sortByName }
      { entityInterfaces.resultManyToMany with vars = e.manyToManyRelationships |> List.choose (getManyToManyVar nameMap false e.logicalName) |> sortByName }
      { entityInterfaces.resultRelationships with vars = e.manyToOneRelationships |> List.choose (getManyToOneVar nameMap false) |> sortByName}
      { entityInterfaces.createOneToMany with vars = e.oneToManyRelationships |> List.choose (getOneToManyVar nameMap true) |> sortByName }
      { entityInterfaces.createManyToMany with vars = e.manyToManyRelationships |> List.choose (getManyToManyVar nameMap true e.logicalName) |> sortByName }
      { entityInterfaces.createRelationships with vars = e.manyToOneRelationships |> List.choose (getManyToOneVar nameMap true) |> sortByName}
      { entityInterfaces.formattedResult with
          vars =
            e.attributes
            |> List.map (getFormattedResultVariable e.optionSets)
            |> concatDistinctSort }
      { entityInterfaces.lookupResult with vars = e.attributes |> List.choose getLookupNameVariable |> sortByName } ]

  Namespace.Create(
    WEB_NS,
    declare = true,
    interfaces = [ result ],
    namespaces =
      [ Namespace.Create(
          e.schemaName,
          interfaces = [ create; update ],
          namespaces = [ Namespace.Create(INTERNAL_NS, interfaces = internalInterfaces) ]
        ) ]
  )
  |> CreateCommon.skipNsIfEmpty
