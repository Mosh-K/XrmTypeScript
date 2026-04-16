module DG.XrmTypeScript.FileGeneration

open System.IO
open System.Reflection

open Utility
open IntermediateRepresentation

open CreateOptionSetDts
open CreateFormDts
open CreateWebEntities
open CreateXrmApi

(** Resource helpers *)
let resourcePrefix = "XrmTypeScript.Types."
let getResourceLines (resName: string) =
  let assembly = Assembly.GetExecutingAssembly()
  let prefixedString = if resName.StartsWith(resourcePrefix) then resName else resourcePrefix + resName
  use res = assembly.GetManifestResourceStream(prefixedString)
  use sr = new StreamReader(res)
  seq {
    while not sr.EndOfStream do yield sr.ReadLine ()
  } |> List.ofSeq

let copyResourceDirectly outDir resName filename =
  File.WriteAllLines(
    sprintf "%s/%s" outDir filename, 
    getResourceLines resName)

(** Generation functionality *)

/// Clear any previously output files
let clearOldOutputFiles out =
  printf "Clearing old files..."
  let rec emptyDir path =
    Directory.EnumerateFiles(path, "*.d.ts") 
    |> Seq.iter File.Delete

    Directory.EnumerateDirectories(path, "*")  
    |> Seq.iter (fun dir ->
      emptyDir dir
      try Directory.Delete dir
      with _ -> ()
    )

  Directory.CreateDirectory out |> ignore
  emptyDir out
  printfn "Done!"

/// Generate the Enum definitions
let generateEnumDefs state =
  printf "Generating Enum definitions..."
  let defs = 
    state.entities
    |> getUniquePicklists
    |> Array.Parallel.map (fun os ->
      sprintf "%s/_internal/Enum/%s.d.ts" state.outputDir os.name,
      getOptionSetEnum os)

  printfn "Done!"
  defs

/// Generate the web entity definitions
let generateWebEntityDefs state =
  printf "Generating Web entity definitions..."
  let defs = 
    state.entities
    |> Array.Parallel.map (fun (e) ->
      let name = e.logicalName
      let lines = getEntityInterfaceLines state.nameMap e

      sprintf "%s/Web/%s.d.ts" state.outputDir name, 
      lines)

  printfn "Done!"
  defs

let generateXrmApiDefs state =
  printf "Generating XrmAPI definitions..."
  printfn "Done!"

  $"{state.outputDir}/XrmApi.d.ts",
  CreateCommon.skipNsIfEmpty (
    Namespace.Create(
      "Xrm",
      interfaces =
        [ Interface.Create(
            "WebApiOffline",
            funcs = getRetrieveFuncs state.rawEntities @ getRetrieveMultipleFuncs state.rawEntities
          ) ],
      declare = true
    )
  )

/// Generate the Form definitions
let generateFormDefs state crmVersion = 
  printf "Generation Form definitions..."
  let getFormType xrmForm = xrmForm.formType ?|> sprintf "/%s" ?| ""

  let formMap =
    state.forms
    |> Array.choose (fun (form: XrmForm) ->
      match form.guid with 
      | Some formId -> Some (formId, form)
      | None -> None
    )
    |> Map.ofArray
  
  let defs = 
    state.forms
    |> Array.groupBy (fun (form : XrmForm) -> (form.entityName, getFormType form), form.name)
    |> Array.map (fun (_, forms) -> 
         forms 
         |> Array.sortBy (fun form -> form.guid)
         |> Array.mapi (fun i form -> 
                    if i = 0 then form
                    else { form with name = sprintf "%s%i" form.name i }))
    |> Array.concat
    |> Array.filter (fun (form: XrmForm) -> form.formType.IsNone || (form.formType.IsSome && form.formType.Value <> "Card" && form.formType.Value <> "InteractionCentricDashboard" && form.formType.Value <> "TaskFlowForm"))
    |> Array.Parallel.map (fun xrmForm -> 
         let path = sprintf "%s/Form/%s%s" state.outputDir xrmForm.entityName (getFormType xrmForm)
         let lines = getFormDts xrmForm formMap crmVersion
         sprintf "%s/%s.d.ts" path xrmForm.name, lines)

  printfn "Done!"
  defs
