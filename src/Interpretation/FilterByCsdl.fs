module internal DG.XrmTypeScript.FilterByCsdl

open Microsoft.OData.Edm
open IntermediateRepresentation


let private toCsdlPropName (attr: XrmAttribute) =
  match attr.specialType with
  | SpecialType.EntityReference -> $"_{attr.logicalName}_value"
  | _ -> attr.logicalName

let filterEntity (edmType: IEdmEntityType) (entity: XrmEntity) : XrmEntity =
  let csdlProps =
    edmType.StructuralProperties()
    |> Seq.map (fun p -> p.Name)
    |> Set.ofSeq

  let csdlNavProps =
    edmType.NavigationProperties()
    |> Seq.map (fun p -> p.Name)
    |> Set.ofSeq

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
