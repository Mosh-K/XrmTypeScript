module DG.XrmTypeScript.Setup

open System
open System.Collections.Generic

open IntermediateRepresentation
open InterpretEntityMetadata
open InterpretBpfJson
open InterpretFormXml


let intersectMappedSets a b = Map.ofSeq (seq {
  for KeyValue(k, va) in a do
    match Map.tryFind k b with
    | Some vb -> yield k, Set.intersect va vb
    | None    -> () })

// Reduces a list of quadruple sets to a single quadruple set
let intersectFormQuads =
  Seq.reduce (fun (d1, a1, c1, q1, t1) (d2, a2, c2, q2, t2) ->
    Set.union d1 d2, Set.intersect a1 a2, Set.intersect c1 c2, Set.intersect q1 q2, intersectMappedSets t1 t2)

let intersectContentByGuid typ (dict: IDictionary<Guid, 'a>) ((name, guids): Intersect) contentMap reduce =
  guids 
  |> Seq.choose (fun g ->
    match dict.ContainsKey g with
    | true  -> Some dict.[g]
    | false -> printfn "%s with GUID %A was not found" typ g; None)

  |> Seq.map contentMap
  |> reduce
  |> fun q -> name, q

// Makes intersection of forms by guid
let intersectFormContentByGuid (formDict: IDictionary<Guid, XrmForm>) intersect =
  let contentMap =
    (fun (f: XrmForm) -> 
      f.entityDependencies |> Set.ofSeq,  
      f.attributes |> Set.ofList, 
      f.controls |> Set.ofList, 
      f.quickViewForms |> Set.ofList,
      f.tabs |> Seq.map (fun t -> (t.iname, t.name), t.sections |> Set.ofList) |> Map.ofSeq)

  intersectContentByGuid "Form" formDict intersect contentMap intersectFormQuads

let intersect (dict: IDictionary<Guid, 'a>) instersectContentByGuids mapContent =
  Array.distinctBy fst
  >> Array.Parallel.map (instersectContentByGuids dict)
  >> Seq.map mapContent

// Intersect forms based on argument
let intersectForms formDict formsToIntersect =
  let contentMap =
    (fun (name, (deps, a, c, q, t)) -> 
    { XrmForm.name = name
      entityName = "_special"
      guid = None
      entityDependencies = deps |> Set.toSeq
      formType = None
      attributes = a |> Set.toList
      controls = c |> Set.toList
      quickViewForms = q |> Seq.toList
      tabs = t |> Map.toList |> List.map (fun ((k1, k2), v) -> { iname = k1; name = k2; displayName = ""; sections = v |> Set.toList })
    })

  intersect formDict intersectFormContentByGuid contentMap formsToIntersect
  |> Seq.append formDict.Values
  |> Seq.toArray

/// Interprets the raw CRM data into an intermediate state used for further generation
let interpretCrmData out formsToIntersect (rawState: RawState) labelMapping =
  printf "Interpreting data..."

  let entityMetadata =
    rawState.metadata |> Array.Parallel.map (interpretEntity rawState.nameMap labelMapping)

  let bpfControls = interpretBpfs rawState.bpfData

  let formDict = interpretFormXmls entityMetadata rawState.formData bpfControls
  let forms = intersectForms formDict formsToIntersect
  printfn "Done!"

  { InterpretedState.entities = entityMetadata
    bpfControls = bpfControls
    forms = forms
    outputDir = out 
  }
