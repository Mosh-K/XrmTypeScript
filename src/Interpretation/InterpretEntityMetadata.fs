module internal DG.XrmTypeScript.InterpretEntityMetadata

open Utility
open Constants

open IntermediateRepresentation
open InterpretOptionSetMetadata
open Microsoft.Xrm.Sdk.Metadata


let typeConv = function   
  | XrmAttributeType.ManagedProperty
  | XrmAttributeType.Boolean   -> TsType.Boolean
  | XrmAttributeType.DateTime  -> TsType.Date
    
  | XrmAttributeType.Memo
  | XrmAttributeType.EntityName
  | XrmAttributeType.String     -> TsType.String

  | XrmAttributeType.Integer
  | XrmAttributeType.Double
  | XrmAttributeType.BigInt
  | XrmAttributeType.Money
  | XrmAttributeType.Picklist
  | XrmAttributeType.State
  | XrmAttributeType.Status     -> TsType.Number
  | _                           -> TsType.Any

let interpretNormalAttribute aType (options:OptionSet option)  =
  match aType with
  | XrmAttributeType.Money -> TsType.Number, SpecialType.Money
    
  | XrmAttributeType.MultiSelectPicklist  -> TsType.Custom $"{ENUM_NS}.{options.Value.name}", SpecialType.MultiSelectOptionSet
  | XrmAttributeType.Picklist
  | XrmAttributeType.State
  | XrmAttributeType.Status               -> TsType.Custom $"{ENUM_NS}.{options.Value.name}", SpecialType.OptionSet

  | XrmAttributeType.Lookup
  | XrmAttributeType.PartyList
  | XrmAttributeType.Customer
  | XrmAttributeType.Owner                -> TsType.String, SpecialType.EntityReference
        
  | XrmAttributeType.Uniqueidentifier     -> TsType.String, SpecialType.Guid

  | XrmAttributeType.Decimal              -> typeConv aType, SpecialType.Decimal
  | _                                     -> typeConv aType, SpecialType.Default


let interpretAttribute (nameMap: Map<string, EntityInfo>) labelMapping (a: AttributeMetadata) =
  let aType = XrmAttributeType.fromDisplayName a.AttributeTypeName
  if a.AttributeOf <> null ||
      aType = XrmAttributeType.Virtual ||
      a.LogicalName.StartsWith("yomi") then None, None
  else

  let options =
    match a with
    | :? EnumAttributeMetadata as eam -> interpretOptionSet eam.OptionSet labelMapping
    | _ -> None

  let targetEntitySets =
    match a with
    | :? LookupAttributeMetadata as lam -> 
      lam.Targets
      |> Array.choose 
        (fun k -> 
          match Map.tryFind k nameMap with
          | None -> None
          | Some tes -> Some (k, tes.EntitySetName, tes.DisplayName)
        )
      |> Some
    | _ -> None

  let vType, sType = interpretNormalAttribute aType options
    
  options, Some {
    XrmAttribute.schemaName = a.SchemaName
    logicalName = a.LogicalName
    varType = vType
    specialType = sType
    colType = aType
    targetEntitySets = targetEntitySets
    readable = a.IsValidForRead.GetValueOrDefault(false)
    createable = a.IsValidForCreate.GetValueOrDefault(false)
    updateable = a.IsValidForUpdate.GetValueOrDefault(false)
    displayName = getLabel a.DisplayName
  }

let sanitizeNavigationProptertyName string =
    if string = null then "navigationPropertyNameNotDefined"
    else string

let interpretRelationship (nameMap: Map<string, EntityInfo>) referencing (attributes: XrmAttribute list) (rel: OneToManyRelationshipMetadata) =
  let rLogical =
    if referencing then rel.ReferencedEntity
    else rel.ReferencingEntity
    
  Map.tryFind rLogical nameMap
  ?|> fun eInfo ->
    let setNames =
      match eInfo.EntitySetName = "owners" with
      | false -> [|eInfo.SchemaName,eInfo.EntitySetName|]
      | true -> [|"Team","teams";"SystemUser","systemusers"|]
    
    let name =
      match rel.ReferencedEntity = rel.ReferencingEntity with
      | false -> rel.SchemaName
      | true  ->
        match referencing with
        | true  -> sprintf "Referencing%s" rel.SchemaName
        | false -> sprintf "Referenced%s" rel.SchemaName

    setNames
    |> Array.map (fun (schema,setName) ->
      let xRel = 
        { XrmRelationship.schemaName = name
          attributeName = 
            if referencing then rel.ReferencingAttribute 
            else rel.ReferencedAttribute
          displayName = eInfo.DisplayName
          relType = if referencing then RelType.ManyToOne else RelType.OneToMany
          navProp = 
            if referencing then rel.ReferencingEntityNavigationPropertyName
            else rel.ReferencedEntityNavigationPropertyName
            |> sanitizeNavigationProptertyName
          referencing = referencing
          relatedSetName = setName
          relatedSchemaName = schema 
        }

      eInfo.SchemaName, xRel)


let interpretM2MRelationship (nameMap: Map<string, EntityInfo>) logicalName (rel: ManyToManyRelationshipMetadata) =
  let rLogical =
    match logicalName = rel.Entity2LogicalName with
    | true  -> rel.Entity1LogicalName
    | false -> rel.Entity2LogicalName
    
  Map.tryFind rLogical nameMap
  ?|> fun eInfo ->
      
    let xRel = 
      { XrmRelationship.schemaName = rel.SchemaName 
        attributeName = rel.SchemaName
        displayName = eInfo.DisplayName
        relType = RelType.ManyToMany
        navProp = 
          if logicalName = rel.Entity2LogicalName then rel.Entity1NavigationPropertyName
          else rel.Entity2NavigationPropertyName
          |> sanitizeNavigationProptertyName
        referencing = false
        relatedSetName = eInfo.EntitySetName
        relatedSchemaName = eInfo.SchemaName
      }
    
    eInfo.SchemaName, xRel


let interpretEntity (nameMap: Map<string, EntityInfo>) labelMapping (metadata:EntityMetadata) =
  if isNull metadata.Attributes then failwith "No attributes found!"

  let optionSets, attributes = 
    metadata.Attributes 
    |> Array.map (interpretAttribute nameMap labelMapping)
    |> Array.unzip

  let attributes = 
    attributes 
    |> Array.choose id 
    |> Array.toList
   
  let optionSets = 
    optionSets 
    |> Seq.choose id 
    |> Seq.distinctBy (fun x -> x.name) 
    |> Seq.toList
    

  let handleOneToMany referencing = function
    | null  -> Array.empty
    | x     -> 
      x 
      |> Array.choose (interpretRelationship nameMap referencing attributes)
      |> Array.concat
    
  let handleManyToMany logicalName = function
    | null  -> Array.empty
    | x     -> x |> Array.choose (interpretM2MRelationship nameMap logicalName)


  let relatedEntities, relationships = 
    [ metadata.OneToManyRelationships  |> handleOneToMany false 
      metadata.ManyToOneRelationships  |> handleOneToMany true 
      metadata.ManyToManyRelationships |> handleManyToMany metadata.LogicalName 
    ] |> Array.concat
      |> List.ofArray
      |> List.unzip

  let relatedEntities = 
    relatedEntities 
    |> Set.ofList 
    |> Set.remove metadata.SchemaName 
    |> Set.toList

  { XrmEntity.typecode = metadata.ObjectTypeCode.GetValueOrDefault()
    schemaName = metadata.SchemaName
    logicalName = metadata.LogicalName
    entitySetName = metadata.EntitySetName |> Utility.stringToOption
    idAttribute = metadata.PrimaryIdAttribute
    isIntersect = metadata.IsIntersect.GetValueOrDefault(true)
    attributes = attributes
    optionSets = optionSets
    relatedEntities = relatedEntities
    allRelationships = relationships
    displayName = getLabel metadata.DisplayName
    availableRelationships = List.empty // old
  }