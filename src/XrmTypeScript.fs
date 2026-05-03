namespace DG.XrmTypeScript

open System
open System.IO
open System.Runtime.Serialization.Json

open Utility
open GenerationMain

type XrmTypeScript private () =

  static member GenerateFromCrm
    (
      url,
      method,
      ?clientId,
      ?clientSecret,
      ?connectionString,
      ?outDir,
      ?entities,
      ?solutions,
      ?crmVersion,
      ?useDeprecated,
      ?skipForms,
      ?oneFile,
      ?web,
      ?formIntersects,
      ?labelMapping,
      ?skipXrmApi,
      ?skipInactiveForms
    ) =
    let xrmAuth =
      { XrmAuthSettings.url = Uri(url)
        method = method
        clientId = clientId
        clientSecret = clientSecret
        connectionString = connectionString 
      }

    let rSettings =
      { XdtRetrievalSettings.entities = entities
        solutions = solutions
        skipInactiveForms = skipInactiveForms ?| true }

    let gSettings =
      { XdtGenerationSettings.out = outDir ?| "."
        crmVersion = crmVersion
        useDeprecated = useDeprecated ?| false
        skipForms = skipForms ?| false
        oneFile = oneFile ?| false
        web = web ?| false
        formIntersects = formIntersects ?| [||]
        labelMapping = labelMapping ?| [||]
        skipXrmApi = skipXrmApi ?| false }

    XrmTypeScript.GenerateFromCrm(xrmAuth, rSettings, gSettings)


  static member GenerateFromCrm(xrmAuth, rSettings, gSettings) =
    #if !DEBUG 
    try
    #endif 
      
      let rawState = retrieveRawState xrmAuth rSettings gSettings.skipForms
      generateFromRaw gSettings rawState
      printfn "\nSuccessfully generated all TypeScript declaration files."

    #if !DEBUG
    with ex -> getFirstExceptionMessage ex |> failwithf "\nUnable to generate TypeScript files: %s"
    #endif

  static member SaveMetadataToFile(xrmAuth, rSettings, ?filePath) =
    #if !DEBUG 
    try
    #endif 
      
      let filePath = 
        filePath 
        ?>>? (String.IsNullOrWhiteSpace >> not)
        ?| "XdtData.json"

      let serializer = DataContractJsonSerializer(typeof<RawState>, null, System.Int32.MaxValue, true, null, false)
      use stream = new FileStream(filePath, FileMode.Create)

      retrieveRawState xrmAuth rSettings false
      |> fun state -> serializer.WriteObject(stream, state)
      printfn "\nSuccessfully saved retrieved data to file %s." (Path.GetFullPath filePath)

    #if !DEBUG
    with ex -> getFirstExceptionMessage ex |> failwithf "\nUnable to generate data file: %s"
    #endif

  static member SaveMetadataFromFile(loadPath, savePath) =
    #if !DEBUG
    try
    #endif

      let rawState =
        try
          let serializer = DataContractJsonSerializer(typeof<RawState>)
          use stream = new FileStream(loadPath, FileMode.Open)
          serializer.ReadObject(stream) :?> RawState
        with ex -> failwithf "\nUnable to parse data file"

      let serializer = DataContractJsonSerializer(typeof<RawState>, null, System.Int32.MaxValue, true, null, false)
      use stream = new FileStream(savePath, FileMode.Create)
      serializer.WriteObject(stream, rawState)
      printfn "\nSuccessfully saved data from %s to %s." (Path.GetFullPath loadPath) (Path.GetFullPath savePath)

    #if !DEBUG
    with ex -> getFirstExceptionMessage ex |> failwithf "\nUnable to save data file: %s"
    #endif

  static member GenerateFromFile(gSettings, ?filePath) =
    #if !DEBUG 
    try
    #endif 
      let filePath = 
        filePath 
        ?>>? (String.IsNullOrWhiteSpace >> not)
        ?| "XdtData.json"

      let rawState =
        try
          let serializer = DataContractJsonSerializer(typeof<RawState>)
          use stream = new FileStream(filePath, FileMode.Open)
          serializer.ReadObject(stream) :?> RawState
        with ex -> failwithf "\nUnable to parse data file"
    
      generateFromRaw gSettings rawState
      printfn "\nSuccessfully generated all TypeScript declaration files."

    #if !DEBUG
    with ex -> getFirstExceptionMessage ex |> failwithf "\nUnable to generate TypeScript files: %s"
    #endif
