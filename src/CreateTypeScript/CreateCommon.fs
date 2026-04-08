module internal DG.XrmTypeScript.CreateCommon

open TsStringUtil

let skipNsIfEmpty (ns: Namespace) =
  match System.String.IsNullOrWhiteSpace ns.name with
  | true ->
    (ns.interfaces |> List.map interfaceToString |> List.concat)
    @ (ns.typeDecs |> List.map makeTypeDeclaration)
  | false -> nsToString ns
