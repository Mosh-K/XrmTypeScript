module internal DG.XrmTypeScript.InterpretCommon

open Constants
open IntermediateRepresentation


let getEnumLink (options: OptionSet list) (a: XrmAttribute) =
  match a.colType with
  | XrmAttributeType.Picklist
  | XrmAttributeType.MultiSelectPicklist
  | XrmAttributeType.State
  | XrmAttributeType.Status ->
    let enumName = match a.varType with TsType.EnumRef name -> name | _ -> ""
    let enum = options |> List.tryFind (fun o -> o.name = enumName)

    match enum with
    | Some e -> $"{ENUM_NS}.{e.name} {e.displayName}"
    | None -> ""
  | _ -> ""
