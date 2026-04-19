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

let entityId (entity: XrmEntity, optional) =
  Variable.Create(
    entity.idAttribute.logicalName,
    TsType.String,
    comment = Comment.Create(entity.idAttribute.displayName, colType = entity.idAttribute.colType, isPrimaryId = true),
    optional = optional
  )

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
let getBindVars (nameMap: Map<string, EntityInfo>) (filter: XrmAttribute -> bool) (entity: XrmEntity) =
  let attrMap = entity.attributes |> List.map (fun a -> a.logicalName, a) |> Map.ofList
  entity.manyToOneRelationships
  |> List.choose (fun rel ->
    Map.tryFind rel.ReferencingAttribute attrMap
    ?>> fun attr ->
      (match filter attr with
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

let getLookupValueVars (attrs: XrmAttribute list) =
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

let private toInterfaceName forWrite schemaName =
  if forWrite then $"{schemaName}.{CREATE_INTERFACE_NAME}" else schemaName

let getManyToOneVars (nameMap: Map<string, EntityInfo>) (forWrite: bool) (rels: OneToManyRelationshipMetadata list) =
  rels
  |> List.choose (fun rel ->
    let relatedInfo = resolveRelatedEntities nameMap rel.ReferencedEntity
    match relatedInfo with
    | [] -> None
    | _  ->
      let varType =
        relatedInfo
        |> List.map (fun e -> TsType.Custom(toInterfaceName forWrite e.SchemaName))
        |> fun tys -> TsType.Union(tys @ [ TsType.Null ])
      Some(Variable.Create(
        rel.ReferencingEntityNavigationPropertyName |> sanitizeNavProp,
        varType,
        Comment.Create(relatedInfo |> List.map (fun e -> e.DisplayName) |> String.concat " | ", relType = RelType.ManyToOne),
        optional = true
      )))
  |> sortByName

let getOneToManyVars (nameMap: Map<string, EntityInfo>) (forWrite: bool) (rels: OneToManyRelationshipMetadata list) =
  rels
  |> List.choose (fun rel ->
    Map.tryFind rel.ReferencingEntity nameMap
    |> Option.map (fun eInfo ->
      let varType =
        TsType.Union
          [ TsType.Array(TsType.Custom(toInterfaceName forWrite eInfo.SchemaName))
            TsType.Null ]
      Variable.Create(
        rel.ReferencedEntityNavigationPropertyName |> sanitizeNavProp,
        varType,
        Comment.Create(eInfo.DisplayName, relType = RelType.OneToMany),
        optional = true
      )))
  |> sortByName

let getManyToManyVars (nameMap: Map<string, EntityInfo>) (forWrite: bool) (entity: XrmEntity) =
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
          [ TsType.Array(TsType.Custom(toInterfaceName forWrite eInfo.SchemaName))
            TsType.Null ]
      Variable.Create(
        navProp |> sanitizeNavProp,
        varType,
        Comment.Create(eInfo.DisplayName, relType = RelType.ManyToMany),
        optional = true
      )))
  |> sortByName

let getFormattedVars (entity: XrmEntity) =
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

let getLookupNameVars (attrs: XrmAttribute list) =
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
          Comment.Create(a.displayName, ?tes = a.targetEntitySets),
          optional = true
        )
      ))
  |> sortByName

let getScalarVars (filter: XrmAttribute -> bool) (entity: XrmEntity) =
  entity.attributes
  |> List.choose (fun attr ->
    match attr.specialType with
    | SpecialType.EntityReference -> None
    | _ when attr.logicalName = entity.idAttribute.logicalName -> None
    | _ when not (filter attr) -> None
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
  readableScalars: Interface
  creatableScalars: Interface
  updatableScalars: Interface

  creatableBinds: Interface
  updatableBinds: Interface

  writeOneToMany: Interface
  writeManyToMany: Interface
  writeManyToOne: Interface
  writeRelationships: Interface

  readOneToMany: Interface
  readManyToMany: Interface
  readManyToOne: Interface
  readRelationships: Interface

  formatted: Interface
  lookupNames: Interface
  lookupValues: Interface

  create: Interface
  update: Interface
  read: Interface
}

let getBlankEntityInterfaces (entity: XrmEntity) =
  let comment = Comment.Create(entity.displayName, setName = entity.setName)

  let rScalars = "ReadableScalars"
  let cScalars = "CreatableScalars"
  let uScalars = "UpdatableScalars"

  let cBinds = "CreatableBinds"
  let uBinds = "UpdatableBinds"

  let wRelations = "WriteRelationships"
  let wM2O = "WriteManyToOne"
  let wO2M = "WriteOneToMany"
  let wM2M = "WriteManyToMany"

  let rRelations = "ReadRelationships"
  let rM2O = "ReadManyToOne"
  let rO2M = "ReadOneToMany"
  let rM2M = "ReadManyToMany"

  let frmt = "Formatted"
  let lookupN = "LookupNames"
  let lookupV = "LookupValues"

  {
    readableScalars = Interface.Create(rScalars, extends = [ cScalars ])
    creatableScalars = Interface.Create(cScalars, extends = [ uScalars ])
    updatableScalars = Interface.Create uScalars

    readRelationships = Interface.Create(rRelations, extends = [ rM2O; rO2M; rM2M ])
    readManyToOne = Interface.Create rM2O
    readOneToMany = Interface.Create rO2M
    readManyToMany = Interface.Create rM2M

    writeRelationships = Interface.Create(wRelations, extends = [ wM2O; wO2M; wM2M ])
    writeManyToOne = Interface.Create wM2O
    writeOneToMany = Interface.Create wO2M
    writeManyToMany = Interface.Create wM2M

    creatableBinds = Interface.Create(cBinds, extends = [ uBinds ])
    updatableBinds = Interface.Create uBinds

    formatted = Interface.Create frmt
    lookupNames = Interface.Create lookupN
    lookupValues = Interface.Create lookupV

    update =
      Interface.Create(
        UPDATE_INTERFACE_NAME,
        comment,
        [ uScalars; wRelations; uBinds ] |> List.map (withNamespace INTERNAL_NS)
      )
    create =
      Interface.Create(
        CREATE_INTERFACE_NAME,
        comment,
        [ cScalars; wRelations; cBinds ] |> List.map (withNamespace INTERNAL_NS)
      )
    read =
      Interface.Create(
        entity.schemaName,
        comment,
        [ rScalars; rRelations; frmt; lookupN; lookupV ] |> List.map (withNamespace $"{entity.schemaName}.{INTERNAL_NS}")
      ) }
        
/// Create entity interfaces
let getEntityInterfaceLines (nameMap: Map<string, EntityInfo>) (entity: XrmEntity) =
  let entityInterfaces = getBlankEntityInterfaces entity

  let internalInterfaces =
    [
      { entityInterfaces.readableScalars with vars = getScalarVars (fun a -> not a.createable) entity }
      { entityInterfaces.creatableScalars with vars = getScalarVars (fun a -> a.createable && not a.updateable) entity }
      { entityInterfaces.updatableScalars with vars = getScalarVars (fun a -> a.updateable) entity }

      entityInterfaces.readRelationships
      { entityInterfaces.readManyToOne with vars = getManyToOneVars nameMap false entity.manyToOneRelationships }
      { entityInterfaces.readOneToMany with vars = getOneToManyVars nameMap false entity.oneToManyRelationships }
      { entityInterfaces.readManyToMany with vars = getManyToManyVars nameMap false entity }

      entityInterfaces.writeRelationships
      { entityInterfaces.writeManyToOne with vars = getManyToOneVars nameMap true entity.manyToOneRelationships }
      { entityInterfaces.writeOneToMany with vars =  getOneToManyVars nameMap true entity.oneToManyRelationships }
      { entityInterfaces.writeManyToMany with vars = getManyToManyVars nameMap true entity }

      { entityInterfaces.creatableBinds with vars = getBindVars nameMap (fun a -> a.createable && not a.updateable) entity }
      { entityInterfaces.updatableBinds with vars = getBindVars nameMap (fun a -> a.updateable) entity }

      { entityInterfaces.formatted with vars = getFormattedVars entity }
      { entityInterfaces.lookupNames with vars = getLookupNameVars entity.attributes } 
      { entityInterfaces.lookupValues with vars = getLookupValueVars entity.attributes } 
    ]

  let update = { entityInterfaces.update with vars = [ entityId (entity, true) ] }
  let create = { entityInterfaces.create with vars = [ entityId (entity, true) ] }
  let read = { entityInterfaces.read with vars = [ entityTag; entityId (entity, false) ] }

  Namespace.Create(
    WEB_NS,
    declare = true,
    interfaces = [ read ],
    namespaces =
      [ Namespace.Create(
          entity.schemaName,
          interfaces = [ create; update ],
          namespaces = [ Namespace.Create(INTERNAL_NS, interfaces = internalInterfaces) ]
        ) ]
  )
  |> CreateCommon.skipNsIfEmpty
