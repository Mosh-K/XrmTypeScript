module internal DG.XrmTypeScript.InterpretOptionSetMetadata

open Microsoft.Xrm.Sdk
open Microsoft.Xrm.Sdk.Metadata

open Utility
open IntermediateRepresentation
open System.Text.RegularExpressions


let sanitizeLabel label = 
  Regex.Replace(label, @"[\""]", "''")
  |> fun label ->
    match label with
    | IsNumericLiteral true -> $"\"_{label}\"" 
    | "" -> emptyLabel
    | _ -> $"\"{label}\""

let getLabelString (label:Label) labelMapping =
  try
    label.UserLocalizedLabel.Label
    |> Utility.applyLabelMappings labelMapping
    |> sanitizeLabel
  with _ -> emptyLabel

let getMetadataString (metadata:OptionSetMetadataBase) labelMapping =
  getLabelString metadata.DisplayName labelMapping
  |> fun name -> 
    if name <> emptyLabel then name
    else metadata.Name


/// Interprets CRM OptionSetMetadata into intermediate type
let interpretOptionSet (metadata:OptionSetMetadataBase) labelMapping =
  match metadata with
  | :? OptionSetMetadata as osm ->

    let options =
      osm.Options
      |> Seq.map (fun opt ->
        { label = getLabelString opt.Label labelMapping
          value = opt.Value.GetValueOrDefault() }) 

    let fixedOptionLabels =
      options
      |> Seq.fold (fun (countMap:Map<string,Option list>) op ->
        if countMap.ContainsKey op.label then
          countMap.Add(
            op.label, 
            { op with label = sprintf "\"%s_%d\"" (op.label.Trim '"') (countMap.[op.label].Length+1) } 
              :: countMap.[op.label]
          )
        else countMap.Add(op.label, [op])
      ) Map.empty
      |> Map.toArray |> Array.map snd |> List.concat 
      |> List.sortBy (fun op -> op.value) |> List.toArray

    { name = metadata.Name
      options = fixedOptionLabels 
      displayName = getLabel osm.DisplayName }
    |> Some

  | :? BooleanOptionSetMetadata as bosm ->
    let options =
      [|  { label = getLabelString bosm.TrueOption.Label labelMapping
            value = 1 }
          { label = getLabelString bosm.FalseOption.Label labelMapping
            value = 0 } |]

    { name = metadata.Name
      options = options 
      displayName = getLabel bosm.DisplayName }
    |> Some

  | _ -> None