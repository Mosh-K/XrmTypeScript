module internal DG.XrmTypeScript.CreateOptionSetDts

open TsStringUtil
open Constants
open IntermediateRepresentation

 
let getOptionSetEnum (os:OptionSet) =
  Namespace.Create(ENUM_NS, enums = [
  TsEnum.Create(
    os.name,
    os.options 
      |> Array.Parallel.map (fun o -> o.label, Some o.value) 
      |> List.ofArray,
    Comment.Basic os.displayName)],
    declare = true)
  |> nsToString


let getUniquePicklists (es:XrmEntity[]) =
  es
  |> Array.Parallel.map (fun e -> e.optionSets) |> List.concat
  |> Seq.distinctBy (fun os -> os.name) |> Array.ofSeq
