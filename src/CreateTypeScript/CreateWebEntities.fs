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

let entityTag = 
  Variable.Create("\"@odata.etag\"", TsType.String)

let entityId (entity: XrmEntity, optional ) =
  Variable.Create(entity.idAttribute, TsType.String, optional = optional)

let valueInfix (s: string) = $"_{s}_value"

let formattedName (a: XrmAttribute) =
  let format = "@OData.Community.Display.V1.FormattedValue"

  match a.specialType with
  | SpecialType.EntityReference -> $"\"{valueInfix a.logicalName}{format}\""
  | _ -> $"\"{a.logicalName}{format}\""

(** Various type helper functions *)
let sortByName = List.sortBy (fun (x: Variable) -> x.name)

let hasFormattedValue a = 
  match a.specialType, a.varType with
  | SpecialType.EntityReference, _
  | SpecialType.Money, _
  | SpecialType.OptionSet, _
  | SpecialType.MultiSelectOptionSet, _
  | _, TsType.Date -> true
  | _ -> false

let getAttributeComment (attr: XrmAttribute) (options: OptionSet list option) =
  let link =
    match options with
    | Some opts -> getEnumLink opts attr
    | None -> ""
  Comment.Create(attr.displayName, colType = attr.colType, ?tes = attr.targetEntitySets, link = link)

let getScalarType (attr: XrmAttribute) =
  match attr.specialType with
  | SpecialType.MultiSelectOptionSet -> TsType.String
  | _ -> attr.varType

(** Variable functions *)
let getBindVariables (nameMap: Map<string, EntityInfo>) isUpdate (entity: XrmEntity) =
  let attrMap = entity.attributes |> List.map (fun a -> a.logicalName, a) |> Map.ofList
  entity.manyToOneRelationships
  |> List.choose (fun rel ->
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
          )))
  |> sortByName

let getLookupValueVariables (attrs: XrmAttribute list) =
  attrs
  |> List.choose (fun a ->
    match a.specialType with
    | SpecialType.EntityReference ->
      Some(Variable.Create(
        valueInfix a.logicalName,
        TsType.String,
        getAttributeComment a None,
        optional = true
      ))
    | _ -> None)
  |> sortByName

let private toInterfaceName forCreate schemaName =
  if forCreate then $"{schemaName}.{CREATE_INTERFACE}" else schemaName

let getManyToOneVars (nameMap: Map<string, EntityInfo>) (forCreate: bool) (rels: OneToManyRelationshipMetadata list) =
  rels
  |> List.choose (fun rel ->
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
      )))
  |> sortByName

let getOneToManyVars (nameMap: Map<string, EntityInfo>) (forCreate: bool) (rels: OneToManyRelationshipMetadata list) =
  rels
  |> List.choose (fun rel ->
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
      )))
  |> sortByName

let getManyToManyVars (nameMap: Map<string, EntityInfo>) (forCreate: bool) (entity: XrmEntity) =
  entity.manyToManyRelationships
  |> List.choose (fun rel ->
    let navProp =
      if entity.logicalName = rel.Entity2LogicalName then rel.Entity1NavigationPropertyName
      else rel.Entity2NavigationPropertyName
    let otherLogical =
      if entity.logicalName = rel.Entity2LogicalName then rel.Entity1LogicalName
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
      )))
  |> sortByName

let getFormattedResultVariables (entity: XrmEntity) =
  entity.attributes
  |> List.choose (fun attr ->
    match hasFormattedValue attr with
    | true ->
      Some(Variable.Create(
        formattedName attr,
        TsType.String,
        getAttributeComment attr (Some entity.optionSets),
        optional = true
      ))
    | false -> None)
  |> sortByName

let getLookupNameVariables (attrs: XrmAttribute list) =
  attrs
  |> List.choose (fun a ->
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
          getAttributeComment a None,
          optional = true
        )
      ))
  |> sortByName

let getScalarVariables (entity: XrmEntity) =
  entity.attributes
  |> List.choose (fun attr ->
    match attr.specialType with
    | SpecialType.EntityReference -> None
    | _ ->
      Some(Variable.Create(
        attr.logicalName,
        TsType.Union [ getScalarType attr; TsType.Null ],
        getAttributeComment attr (Some entity.optionSets),
        optional = true
      )))
  |> sortByName

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

let getBlankEntityInterfaces (entity: XrmEntity) =
  let comment = Comment.Create entity.displayName

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
        entity.schemaName,
        comment,
        [ bn; rrn; frn; lrn ] |> List.map (withNamespace $"{entity.schemaName}.{INTERNAL_NS}")
      ) }
        
/// Create entity interfaces
let getEntityInterfaceLines (nameMap: Map<string, EntityInfo>) (entity: XrmEntity) =
  let entityInterfaces = getBlankEntityInterfaces entity

  let update =
    { entityInterfaces.update with vars = entityId (entity, true) :: getBindVariables nameMap true entity }
  let create =
    { entityInterfaces.create with vars = getBindVariables nameMap false entity }
  let result =
    { entityInterfaces.result with vars = entityTag :: entityId (entity, false) :: getLookupValueVariables entity.attributes }

  let internalInterfaces =
    [ { entityInterfaces._base with vars = getScalarVariables entity }
      { entityInterfaces.resultOneToMany with vars = getOneToManyVars nameMap false entity.oneToManyRelationships }
      { entityInterfaces.resultManyToMany with vars = getManyToManyVars nameMap false entity }
      { entityInterfaces.resultRelationships with vars = getManyToOneVars nameMap false entity.manyToOneRelationships }
      { entityInterfaces.createOneToMany with vars =  getOneToManyVars nameMap true entity.oneToManyRelationships }
      { entityInterfaces.createManyToMany with vars = getManyToManyVars nameMap true entity }
      { entityInterfaces.createRelationships with vars = getManyToOneVars nameMap true entity.manyToOneRelationships }
      { entityInterfaces.formattedResult with vars = getFormattedResultVariables entity }
      { entityInterfaces.lookupResult with vars = getLookupNameVariables entity.attributes } ]

  Namespace.Create(
    WEB_NS,
    declare = true,
    interfaces = [ result ],
    namespaces =
      [ Namespace.Create(
          entity.schemaName,
          interfaces = [ create; update ],
          namespaces = [ Namespace.Create(INTERNAL_NS, interfaces = internalInterfaces) ]
        ) ]
  )
  |> CreateCommon.skipNsIfEmpty
