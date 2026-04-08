
// --------------------------------------------------------------------------------------
// Build script — plain F# / dotnet fsi (no FAKE dependency)
// Run with: dotnet fsi build.fsx [target]
// Targets: Clean, BuildSetup, Build, RunXTS, Zip  (default: Zip)
// --------------------------------------------------------------------------------------

open System
open System.IO
open System.IO.Compression
open System.Diagnostics

Environment.CurrentDirectory <- __SOURCE_DIRECTORY__

let getGitTag () =
    let psi = ProcessStartInfo(fileName = "git", arguments = "describe --tags --abbrev=0")
    psi.RedirectStandardOutput <- true
    psi.UseShellExecute <- false
    let p: Process = Process.Start(psi)
    let output = p.StandardOutput.ReadToEnd().Trim()
    p.WaitForExit()
    if p.ExitCode <> 0 || output = "" then failwith "Could not read git tag. Make sure a tag exists."
    output


// --------------------------------------------------------------------------------------
// Helpers
// --------------------------------------------------------------------------------------

let run exe args workDir =
    let psi = ProcessStartInfo(fileName = exe, arguments = (args: string))
    psi.WorkingDirectory <- workDir
    psi.UseShellExecute  <- false
    let p: Process = Process.Start(psi)
    p.WaitForExit()
    if p.ExitCode <> 0 then
        failwithf $"'{exe} {args}' exited with code {p.ExitCode}" 

let cleanDir dir =
    if Directory.Exists dir then
        Directory.Delete(dir, recursive = true)
    Directory.CreateDirectory dir |> ignore

let copyFiles destDir (files: string seq) =
    for f in files do
        File.Copy(f, $"{destDir}/{Path.GetFileName f}", overwrite = true)

let zipDirectory (sourceDir: string) (zipFilePath: string) =
    let sourceDirFull = Path.GetFullPath sourceDir
    let zipFileFull   = Path.GetFullPath zipFilePath
    if File.Exists zipFileFull then File.Delete zipFileFull
    let zipParent = Path.GetDirectoryName zipFileFull
    if not (String.IsNullOrWhiteSpace zipParent) then
        Directory.CreateDirectory zipParent |> ignore
    ZipFile.CreateFromDirectory(sourceDirFull, zipFileFull, CompressionLevel.Optimal, includeBaseDirectory = false)

// --------------------------------------------------------------------------------------
// Targets
// --------------------------------------------------------------------------------------

let clean () =
    for dir in [ "bin"; "temp"; "src/bin"; "test/typings/XRM" ] do
        printfn $"Cleaning {dir}"
        cleanDir dir

let buildSetup () =
    let envInfoPath = Path.GetFullPath "src/EnvInfo.config"
    if not (File.Exists envInfoPath) then
        File.WriteAllLines(envInfoPath, [|
            "<?xml version=\"1.0\" encoding=\"utf-8\"?>"
            "<appSettings>"
            "</appSettings>"
        |])

let build ver =
    let args =
        $"build XrmTypeScript.sln -c Release " +
        $"-p:Version={ver} " +
        "-p:NoWarn=NU5100"
    run "dotnet" args Environment.CurrentDirectory

let runXts () =
    let pathToRelease = Path.GetFullPath "src/bin/Release/net462"
    let args =
        [ "load", "../../../XdtData.json"
          // "save", "../../../XdtData.json"
          "out",  "../../../../test/typings/XRM"
          "web",  "true"
          "entities", "account,contact" ]
        |> List.map (fun (k, v) -> $"-{k}:{v}")
        |> String.concat " "
    run $"{pathToRelease}/XrmTypeScript.exe" args pathToRelease

let zip tag =
    let buildOutDir = "src/bin/Release/net462"
    let stageDir    = "temp/zipstage"
    let zipPath     = $"bin/XrmTypeScript-{tag}-bin.zip"

    cleanDir stageDir

    for f in [ "files/Run.ps1"; "files/Run.fsx"; "files/XrmTypeScript.exe.config" ] do
        File.Copy(f, $"{stageDir}/{Path.GetFileName f}", overwrite = true)

    copyFiles stageDir (Directory.GetFiles(buildOutDir, "*.dll"))
    copyFiles stageDir (Directory.GetFiles(buildOutDir, "*.exe"))
    File.Copy($"{buildOutDir}/XrmTypeScript.xml", $"{stageDir}/XrmTypeScript.xml", overwrite = true)

    zipDirectory stageDir zipPath
    printfn "Created %s" (Path.GetFullPath zipPath)
    Directory.Delete(stageDir, recursive = true)

// --------------------------------------------------------------------------------------
// Target dispatch
// --------------------------------------------------------------------------------------

let runTarget name =
    printfn ""
    printfn "=== %s ===" name
    match name with
    | "Clean"      -> clean ()
    | "BuildSetup" -> clean (); buildSetup ()
    | "Build"      -> clean (); buildSetup (); build "0.0.0"
    | "RunXTS"     -> clean (); buildSetup (); build "0.0.0"; runXts ()
    | "Zip"        ->
        let tag = getGitTag ()
        clean (); buildSetup (); build (tag.TrimStart 'v'); runXts (); zip tag
    | other -> failwithf $"Unknown target: {other}"

let target =
    match fsi.CommandLineArgs |> Array.toList with
    | _ :: t :: _ -> t
    | _            -> "Zip"

runTarget target
