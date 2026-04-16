module internal DG.XrmTypeScript.InterpretEntityMetadata

open Utility
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
    
  | XrmAttributeType.MultiSelectPicklist  -> TsType.EnumRef options.Value.name, SpecialType.MultiSelectOptionSet
  | XrmAttributeType.Picklist
  | XrmAttributeType.State
  | XrmAttributeType.Status               -> TsType.EnumRef options.Value.name, SpecialType.OptionSet

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

let interpretRelationship (nameMap: Map<string, EntityInfo>) referencing (rel: OneToManyRelationshipMetadata) =
  let rLogical =
    if referencing then rel.ReferencedEntity
    else rel.ReferencingEntity
    
  Map.tryFind rLogical nameMap
  ?|> fun eInfo ->
    let relatedInfo =
      match eInfo.EntitySetName with
      | "owners" ->
        let displayName k fallback = nameMap |> Map.tryFind k |> Option.map (fun e -> e.DisplayName) |> Option.defaultValue fallback
        [ { SchemaName = "Team";       EntitySetName = "teams";       DisplayName = displayName "team"       "Team" }
          { SchemaName = "SystemUser"; EntitySetName = "systemusers"; DisplayName = displayName "systemuser" "User" } ]
      | _        -> [ eInfo ]

    { XrmRelationship.relatedInfo = relatedInfo
      attributeName =
        if referencing then
          rel.ReferencingAttribute
        else
          rel.ReferencedAttribute
      relType = if referencing then RelType.ManyToOne else RelType.OneToMany
      navProp =
        (if referencing then
           rel.ReferencingEntityNavigationPropertyName
         else
           rel.ReferencedEntityNavigationPropertyName)
        |> sanitizeNavigationProptertyName }

let interpretM2MRelationship (nameMap: Map<string, EntityInfo>) logicalName (rel: ManyToManyRelationshipMetadata) =
  let rLogical =
    match logicalName = rel.Entity2LogicalName with
    | true  -> rel.Entity1LogicalName
    | false -> rel.Entity2LogicalName
    
  Map.tryFind rLogical nameMap
  ?|> fun eInfo ->
      
    { XrmRelationship.relatedInfo = [ eInfo ]
      attributeName = ""
      relType = RelType.ManyToMany
      navProp =
        (if logicalName = rel.Entity2LogicalName then
           rel.Entity1NavigationPropertyName
         else
           rel.Entity2NavigationPropertyName)
        |> sanitizeNavigationProptertyName }

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

  { XrmEntity.schemaName = metadata.SchemaName
    logicalName = metadata.LogicalName
    idAttribute = metadata.PrimaryIdAttribute
    attributes = attributes
    optionSets = optionSets
    oneToManyRelationships = metadata.OneToManyRelationships |> Array.choose (interpretRelationship nameMap false) |> List.ofArray
    manyToOneRelationships  = metadata.ManyToOneRelationships  |> Array.choose (interpretRelationship nameMap true) |> List.ofArray
    manyToManyRelationships = metadata.ManyToManyRelationships |> Array.choose (interpretM2MRelationship nameMap metadata.LogicalName) |> List.ofArray
    displayName = getLabel metadata.DisplayName
  }
