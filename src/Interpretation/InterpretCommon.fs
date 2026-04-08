module internal DG.XrmTypeScript.InterpretCommon

open Constants
open IntermediateRepresentation
open Microsoft.Xrm.Sdk.Metadata


let getLink (e: XrmEntity) (a: XrmAttribute) =
  if
    [ AttributeTypeDisplayName.PicklistType.Value
      AttributeTypeDisplayName.MultiSelectPicklistType.Value
      AttributeTypeDisplayName.StateType.Value
      AttributeTypeDisplayName.StatusType.Value ]
    |> List.contains a.typeName
  then
    let enumName = TsStringUtil.typeToString(a.varType).Split '.' |> Array.last
    let enum = e.optionSets |> List.tryFind (fun o -> o.name = enumName)
    match enum with
    | Some e -> $"{ENUM_NS}.{e.name} {e.displayName}"
    | None -> ""
  else
    ""
