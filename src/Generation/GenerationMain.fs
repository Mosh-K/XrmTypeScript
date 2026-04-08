module DG.XrmTypeScript.GenerationMain

open System.IO
open Utility

open DataRetrieval
open Setup
open FileGeneration


/// Retrieve data from CRM and setup raw state
let retrieveRawState xrmAuth rSettings =
  let mainProxy = connectToCrm xrmAuth

  let crmVersion = retrieveCrmVersion mainProxy
  let entities = 
    getFullEntityList rSettings.entities rSettings.solutions mainProxy
      
  // Retrieve data from CRM
  retrieveCrmData crmVersion entities mainProxy rSettings.skipInactiveForms

/// Main generator function
let generateFromRaw gSettings rawState =
  let out = gSettings.out ?| "."
  let formIntersects = gSettings.formIntersects ?| [||]
  let crmVersion = gSettings.crmVersion ?| rawState.crmVersion

  // Pre-generation tasks 
  clearOldOutputFiles out

  // Interpret data and generate resource files
  let data =
    interpretCrmData out formIntersects rawState gSettings.labelMapping

  let defs = 
    seq {
      yield! generateEnumDefs data
      if not gSettings.skipForms then yield! generateFormDefs data crmVersion

      match gSettings.web with
      | true -> 
        if not gSettings.skipXrmApi then yield generateXrmApiDefs data
        yield! generateWebEntityDefs  data
      | false -> ()

    }
    |> Array.ofSeq

  printf "Writing to files..."
  copyResourceDirectly out "xrm.d.ts" "xrm.d.ts"

  match gSettings.oneFile with
  | false -> 
    defs 
    |> Array.Parallel.iter (fun (path, lines) -> 
      Directory.CreateDirectory (Path.GetDirectoryName(path)) |> ignore
      File.WriteAllLines(path, lines)
    )
  | true  -> 
    let singleFilePath = Path.Combine(out, "context.d.ts")
    defs |> Array.Parallel.map snd |> List.concat |> fun lines -> File.WriteAllLines(singleFilePath, lines)
  printfn "Done!"
