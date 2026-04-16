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


let resolveRelatedEntities (nameMap: Map<string, EntityInfo>) (logicalName: string) : EntityInfo list =
  match Map.tryFind logicalName nameMap with
  | Some eInfo when eInfo.EntitySetName = "owners" ->
      let displayName k fallback = nameMap |> Map.tryFind k |> Option.map (fun e -> e.DisplayName) |> Option.defaultValue fallback
      [ { SchemaName = "Team";       EntitySetName = "teams";       DisplayName = displayName "team"       "Team" }
        { SchemaName = "SystemUser"; EntitySetName = "systemusers"; DisplayName = displayName "systemuser" "User" } ]
  | Some eInfo -> [ eInfo ]
  | None       -> []

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
    oneToManyRelationships = metadata.OneToManyRelationships |> List.ofArray
    manyToOneRelationships = metadata.ManyToOneRelationships |> List.ofArray
    manyToManyRelationships = metadata.ManyToManyRelationships |> List.ofArray
    displayName = getLabel metadata.DisplayName
  }
