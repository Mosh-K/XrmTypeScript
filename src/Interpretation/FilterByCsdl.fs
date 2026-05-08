module internal DG.XrmTypeScript.FilterByCsdl

open IntermediateRepresentation


let private toCsdlPropName (attr: XrmAttribute) =
  match attr.specialType with
  | SpecialType.EntityReference -> $"_{attr.logicalName}_value"
  | _ -> attr.logicalName

let filterEntity (entityInfo: CsdlEntityInfo) (entity: XrmEntity) : XrmEntity =
  let csdlProps = Set.ofArray entityInfo.StructuralProperties
  let csdlNavProps = Set.ofArray entityInfo.NavigationProperties

  { entity with
      attributes =
        entity.attributes
        |> List.filter (fun a -> Set.contains (toCsdlPropName a) csdlProps)

      manyToOneRelationships =
        entity.manyToOneRelationships
        |> List.filter (fun r ->
          Set.contains r.ReferencingEntityNavigationPropertyName csdlNavProps)
        |> List.distinctBy (fun r -> r.ReferencingEntityNavigationPropertyName)

      oneToManyRelationships =
        entity.oneToManyRelationships
        |> List.filter (fun r ->
          Set.contains r.ReferencedEntityNavigationPropertyName csdlNavProps)

      manyToManyRelationships =
        entity.manyToManyRelationships
        |> List.filter (fun r ->
          let navProp =
            if entity.logicalName = r.Entity2LogicalName
            then r.Entity2NavigationPropertyName
            else r.Entity1NavigationPropertyName
          Set.contains navProp csdlNavProps) }

/// True when an attribute on the entity claims the same OData property name as the given
/// navigation property name. Lookup-style attributes (specialType = EntityReference) are
/// renamed to `_<name>_value` in the OData CSDL so they don't shadow; everything else
/// (Uniqueidentifier, File, Image, plain scalars) keeps its natural name and forces
/// Dataverse to drop the navigation property from the wire.
let private isShadowedByScalar (attrMap: Map<string, XrmAttribute>) navPropName =
  Map.tryFind navPropName attrMap
  |> Option.exists (fun a -> a.specialType <> SpecialType.EntityReference)

let filterEntityFallback (entity: XrmEntity) : XrmEntity =
  let filteredAttrs =
    entity.attributes |> List.filter (fun a -> a.colType <> XrmAttributeType.Virtual)
  let attrMap =
    filteredAttrs |> List.map (fun a -> a.logicalName, a) |> Map.ofList

  { entity with
      attributes = filteredAttrs

      manyToOneRelationships =
        entity.manyToOneRelationships
        |> List.filter (fun rel ->
          not (isNull rel.ReferencingEntityNavigationPropertyName)
          && not (isShadowedByScalar attrMap rel.ReferencingEntityNavigationPropertyName))
        |> List.distinctBy (fun r -> r.ReferencingEntityNavigationPropertyName)

      oneToManyRelationships =
        entity.oneToManyRelationships
        |> List.filter (fun rel -> not (isNull rel.ReferencedEntityNavigationPropertyName))

      manyToManyRelationships =
        entity.manyToManyRelationships
        |> List.filter (fun rel ->
          (entity.logicalName = rel.Entity1LogicalName || entity.logicalName = rel.Entity2LogicalName) // Filter intersect tables
          && not (isNull rel.Entity1NavigationPropertyName)
          && not (isNull rel.Entity2NavigationPropertyName)) }
