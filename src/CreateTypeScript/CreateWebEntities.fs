module internal DG.XrmTypeScript.CreateWebEntities

open Microsoft.Xrm.Sdk.Metadata
open Constants
open InterpretCommon
open IntermediateRepresentation


let INTERNAL_NS = "_"
let SCALAR_NS = "Scalars"
let READ_NS = "Read"
let WRITE_NS = "Write"
let BINDS_NS = "Binds"
let LOOKUP_NS = "Lookup"

(** Interface name helper functions *)
let entityTag = 
  Variable.Create("\"@odata.etag\"", TsType.String)

let entityId (entity: XrmEntity, optional) =
  Variable.Create(
    entity.idAttribute.logicalName,
    TsType.String,
    comment = Comment.Attribute(entity.idAttribute.displayName, colType = entity.idAttribute.colType, isPrimaryId = true),
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

  let logicalName =
    match attr.specialType with
    | SpecialType.EntityReference -> attr.logicalName
    | _ -> ""

  Comment.Attribute(
    attr.displayName,
    colType = attr.colType,
    tes = attr.targetEntitySets,
    link = link,
    logicalName = logicalName
  )

let getScalarType (attr: XrmAttribute) =
  match attr.specialType with
  | SpecialType.MultiSelectOptionSet -> TsType.String
  | _ -> attr.varType

(** Variable functions *)

/// True when an attribute on the entity claims the same OData property name as the given
/// navigation property name. Lookup-style attributes (specialType = EntityReference) are
/// renamed to `_<name>_value` in the OData CSDL so they don't shadow; everything else
/// (Uniqueidentifier, File, Image, plain scalars) keeps its natural name and forces
/// Dataverse to drop the navigation property from the wire.
let private isShadowedByScalar (attrMap: Map<string, XrmAttribute>) navPropName =
  Map.tryFind navPropName attrMap
  |> Option.exists (fun a -> a.specialType <> SpecialType.EntityReference)

let getBindVars (nameMap: Map<string, EntityInfo>) (filter: XrmAttribute -> bool) (entity: XrmEntity) =
  let attrMap = entity.attributes |> List.map (fun a -> a.logicalName, a) |> Map.ofList

  entity.manyToOneRelationships
  |> List.filter (fun rel ->
    not (isNull rel.ReferencingEntityNavigationPropertyName)
    && Map.tryFind rel.ReferencingAttribute attrMap |> Option.exists filter
    && not (isShadowedByScalar attrMap rel.ReferencingEntityNavigationPropertyName))
  |> List.map (fun rel ->
    let eInfo = Map.find rel.ReferencedEntity nameMap

    let bindType =
      if eInfo.EntitySetName = "owners" then
        TsType.Union [ TsType.Custom "`/teams(${string})`"; TsType.Custom "`/systemusers(${string})`" ]
      else
        TsType.Custom $"`/{eInfo.EntitySetName}(${{string}})`"

    Variable.Create(
      $"\"{rel.ReferencingEntityNavigationPropertyName}@odata.bind\"",
      bindType,
      Comment.Basic eInfo.DisplayName,
      optional = true
    ))
  |> sortByName

let getLookupValueVars (attrs: XrmAttribute list) =
  attrs
  |> List.filter (fun a -> a.specialType = SpecialType.EntityReference)
  |> List.map (fun a ->
    Variable.Create(
      valueInfix a.logicalName,
      TsType.String,
      getAttributeComment a None,
      optional = true
    ))
  |> sortByName

let private toInterfaceName forWrite schemaName =
  if forWrite then $"{schemaName}.{CREATE_INTERFACE_NAME}" else schemaName

let getManyToOneVars nameMap (schemaNames: Set<string>) forWrite (entity: XrmEntity) =
  let attrMap = entity.attributes |> List.map (fun a -> a.logicalName, a) |> Map.ofList

  entity.manyToOneRelationships
  |> List.filter (fun rel ->
    not (isNull rel.ReferencingEntityNavigationPropertyName)
    && not (isShadowedByScalar attrMap rel.ReferencingEntityNavigationPropertyName))
  |> List.map (fun rel ->
    let eInfo = Map.find rel.ReferencedEntity nameMap

    let relatedInfo =
      if eInfo.EntitySetName = "owners" then
        [ Map.find "team" nameMap; Map.find "systemuser" nameMap ]
      else
        [ eInfo ]

    let unresolved =
      relatedInfo
      |> List.filter (fun e -> not (schemaNames.Contains e.SchemaName))

    let varType =
      if unresolved.IsEmpty then
        relatedInfo
        |> List.map (fun e -> TsType.Custom(toInterfaceName forWrite e.SchemaName))
        |> fun tys -> TsType.Union(tys @ [ TsType.Null ])
      else
        TsType.Any

    Variable.Create(
      rel.ReferencingEntityNavigationPropertyName,
      varType,
      Comment.Relationship(
        eInfo.DisplayName,
        relType = RelType.ManyToOne,
        partner = rel.ReferencedEntityNavigationPropertyName,
        relatedEntity = rel.ReferencedEntity
      ),
      optional = true
    ))
  |> sortByName

let getOneToManyVars nameMap (schemaNames: Set<string>) forWrite (rels: OneToManyRelationshipMetadata list) =
  rels
  |> List.filter (fun rel -> not (isNull rel.ReferencedEntityNavigationPropertyName))
  |> List.map (fun rel ->
    let eInfo = Map.find rel.ReferencingEntity nameMap
    let inGeneration = schemaNames.Contains eInfo.SchemaName

    let varType =
      if inGeneration then
        TsType.Union [ TsType.Array(TsType.Custom(toInterfaceName forWrite eInfo.SchemaName)); TsType.Null ]
      else
        TsType.Any

    Variable.Create(
      rel.ReferencedEntityNavigationPropertyName,
      varType,
      Comment.Relationship(
        eInfo.DisplayName,
        relType = RelType.OneToMany,
        partner = rel.ReferencingEntityNavigationPropertyName,
        relatedEntity = rel.ReferencingEntity
      ),
      optional = true
    ))
  |> sortByName

let getManyToManyVars nameMap (schemaNames: Set<string>) forWrite (entity: XrmEntity) =
  entity.manyToManyRelationships
  |> List.filter (fun rel ->
    (entity.logicalName = rel.Entity1LogicalName || entity.logicalName = rel.Entity2LogicalName) // Filter intersect tables
    && not (isNull rel.Entity1NavigationPropertyName)
    && not (isNull rel.Entity2NavigationPropertyName))
  |> List.map (fun rel ->
    let navProp, partnerNavProp, otherLogical =
      if entity.logicalName = rel.Entity2LogicalName then
        rel.Entity2NavigationPropertyName, rel.Entity1NavigationPropertyName, rel.Entity1LogicalName
      else
        rel.Entity1NavigationPropertyName, rel.Entity2NavigationPropertyName, rel.Entity2LogicalName

    let eInfo = Map.find otherLogical nameMap
    let inGeneration = schemaNames.Contains eInfo.SchemaName

    let varType =
      if inGeneration then
        TsType.Union [ TsType.Array(TsType.Custom(toInterfaceName forWrite eInfo.SchemaName)); TsType.Null ]
      else
        TsType.Any

    Variable.Create(
      navProp,
      varType,
      Comment.Relationship(
        eInfo.DisplayName,
        relType = RelType.ManyToMany,
        partner = partnerNavProp,
        relatedEntity = otherLogical,
        intersectTable = rel.IntersectEntityName
      ),
      optional = true
    ))
  |> sortByName

let getFormattedVars (entity: XrmEntity) =
  entity.attributes
  |> List.filter hasFormattedValue
  |> List.map (fun attr ->
    Variable.Create(
      formattedName attr,
      TsType.String,
      getAttributeComment attr (Some entity.optionSets),
      optional = true
    ))
  |> sortByName

let getLookupNameVars (attrs: XrmAttribute list) =
  attrs
  |> List.filter (fun a -> a.specialType = SpecialType.EntityReference)
  |> List.map (fun a ->
    let vType =
      match a.targetEntitySets with
      | [||] -> TsType.String
      | tes ->
        tes
        |> Array.map (fun e -> TsType.Custom $"\"{e.LogicalName}\"")
        |> Array.toList
        |> TsType.Union

    Variable.Create(
      $"\"{valueInfix a.logicalName}@Microsoft.Dynamics.CRM.lookuplogicalname\"",
      vType,
      Comment.Attribute(a.displayName, tes = a.targetEntitySets),
      optional = true
    ))
  |> sortByName

let getScalarVars (filter: XrmAttribute -> bool) (entity: XrmEntity) =
  entity.attributes
  |> List.filter (fun attr ->
    attr.specialType <> SpecialType.EntityReference
    && attr.logicalName <> entity.idAttribute.logicalName
    && filter attr)
  |> List.map (fun attr ->
    Variable.Create(
      attr.logicalName,
      TsType.Union [ getScalarType attr; TsType.Null ],
      getAttributeComment attr (Some entity.optionSets),
      optional = true
    ))
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

let getIntersectEntities (nameMap: Map<string, EntityInfo>) (entity: XrmEntity) =
  if not entity.isIntersect then []
  else
    match entity.manyToManyRelationships with
    | [] -> []
    | rel :: _ ->
      [ rel.Entity1LogicalName; rel.Entity2LogicalName ]
      |> List.map (fun ln -> Map.find ln nameMap)

let getBlankEntityInterfaces (nameMap: Map<string, EntityInfo>) (entity: XrmEntity) =
  let comment =
    Comment.Entity(
      entity.displayName,
      setName = entity.setName,
      isIntersect = entity.isIntersect,
      intersectEntities = getIntersectEntities nameMap entity,
      logicalName = entity.logicalName
    )

  let rScalars = "Readable"
  let cScalars = "Creatable"
  let uScalars = "Updatable"

  let cBinds = "Creatable"
  let uBinds = "Updatable"

  let relations = "Relationships"
  let m2o = "ManyToOne"
  let o2m = "OneToMany"
  let m2m = "ManyToMany"

  let frmt = "Formatted"
  let lookupN = "LogicalNames"
  let lookupV = "Values"

  {
    readableScalars = Interface.Create(rScalars, extends = [ cScalars ])
    creatableScalars = Interface.Create(cScalars, extends = [ uScalars ])
    updatableScalars = Interface.Create uScalars

    readRelationships = Interface.Create(relations, extends = [ m2o; o2m; m2m ])
    readManyToOne = Interface.Create m2o
    readOneToMany = Interface.Create o2m
    readManyToMany = Interface.Create m2m

    writeRelationships = Interface.Create(relations, extends = [ m2o; o2m; m2m ])
    writeManyToOne = Interface.Create m2o
    writeOneToMany = Interface.Create o2m
    writeManyToMany = Interface.Create m2m

    creatableBinds = Interface.Create(cBinds, extends = [ uBinds ])
    updatableBinds = Interface.Create uBinds

    formatted = Interface.Create frmt
    lookupNames = Interface.Create lookupN
    lookupValues = Interface.Create lookupV

    update =
      Interface.Create(
        UPDATE_INTERFACE_NAME,
        comment,
        [
          $"{INTERNAL_NS}.{SCALAR_NS}.{uScalars}"
          $"{INTERNAL_NS}.{WRITE_NS}.{relations}"
          $"{INTERNAL_NS}.{BINDS_NS}.{uBinds}" ]
      )
    create =
      Interface.Create(
        CREATE_INTERFACE_NAME,
        comment,
        [
          $"{INTERNAL_NS}.{SCALAR_NS}.{cScalars}"
          $"{INTERNAL_NS}.{WRITE_NS}.{relations}"
          $"{INTERNAL_NS}.{BINDS_NS}.{cBinds}" ]
      )
    read =
      Interface.Create(
        entity.schemaName,
        comment,
        [ 
          $"{entity.schemaName}.{INTERNAL_NS}.{SCALAR_NS}.{rScalars}"
          $"{entity.schemaName}.{INTERNAL_NS}.{READ_NS}.{relations}"
          $"{entity.schemaName}.{INTERNAL_NS}.{frmt}"
          $"{entity.schemaName}.{INTERNAL_NS}.{LOOKUP_NS}.{lookupN}"
          $"{entity.schemaName}.{INTERNAL_NS}.{LOOKUP_NS}.{lookupV}" ]
      ) 
  }
        
/// Create entity interfaces
let getEntityInterfaceLines nameMap (schemaNames: Set<string>) (entity: XrmEntity) =
  let entityInterfaces = getBlankEntityInterfaces nameMap entity

  let scalarInterfaces =
    [
      { entityInterfaces.readableScalars with vars = getScalarVars (fun a -> not a.createable) entity }
      { entityInterfaces.creatableScalars with vars = getScalarVars (fun a -> a.createable && not a.updateable) entity }
      { entityInterfaces.updatableScalars with vars = getScalarVars (fun a -> a.updateable) entity }
    ]

  let readInterfaces =
    [
      entityInterfaces.readRelationships
      { entityInterfaces.readManyToOne with vars = getManyToOneVars nameMap schemaNames false entity }
      { entityInterfaces.readOneToMany with vars = getOneToManyVars nameMap schemaNames false entity.oneToManyRelationships }
      { entityInterfaces.readManyToMany with vars = getManyToManyVars nameMap schemaNames false entity }
    ]

  let writeInterfaces =
    [
      entityInterfaces.writeRelationships
      { entityInterfaces.writeManyToOne with vars = getManyToOneVars nameMap schemaNames true entity }
      { entityInterfaces.writeOneToMany with vars = getOneToManyVars nameMap schemaNames true entity.oneToManyRelationships }
      { entityInterfaces.writeManyToMany with vars = getManyToManyVars nameMap schemaNames true entity }
    ]

  let bindInterfaces =
    [
      { entityInterfaces.creatableBinds with vars = getBindVars nameMap (fun a -> a.createable && not a.updateable) entity }
      { entityInterfaces.updatableBinds with vars = getBindVars nameMap (fun a -> a.updateable) entity }
    ]

  let lookupInterfaces =
    [
      { entityInterfaces.lookupValues with vars = getLookupValueVars entity.attributes }
      { entityInterfaces.lookupNames with vars = getLookupNameVars entity.attributes }
    ]

  let internalInterfaces =
    [
      { entityInterfaces.formatted with vars = getFormattedVars entity }
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
          namespaces =
            [ Namespace.Create(
                INTERNAL_NS,
                interfaces = internalInterfaces,
                namespaces =
                  [ Namespace.Create(SCALAR_NS, interfaces = scalarInterfaces)
                    Namespace.Create(READ_NS, interfaces = readInterfaces)
                    Namespace.Create(WRITE_NS, interfaces = writeInterfaces)
                    Namespace.Create(BINDS_NS, interfaces = bindInterfaces)
                    Namespace.Create(LOOKUP_NS, interfaces = lookupInterfaces) ]
              ) ]
        ) ]
  )
  |> CreateCommon.skipNsIfEmpty
