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
