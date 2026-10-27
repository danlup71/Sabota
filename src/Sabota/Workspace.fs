module Sabota.Workspace

open System
open System.IO
open System.Xml.Linq
open Sabota.Files
open Sabota.Config
open Sabota.MultiError
open Sabota.MutationTypes

/// Create a single work folder and copy the solution once for all files
let createWorkFolder () : string =
    let tempDir = Path.GetTempPath()
    let sabotaDir = Path.Combine(tempDir, "sabota")
    Directory.CreateDirectory(sabotaDir) |> ignore

    let timestamp = DateTime.Now.ToString("yyyyMMdd-HHmmss-ff")
    let workFolder = Path.Combine(sabotaDir, $"run-{timestamp}")
    Directory.CreateDirectory(workFolder) |> ignore

    printfnColor ConsoleColor.Cyan $"Work folder created: {workFolder}"
    workFolder


let copySolutionToWorkFolder (solutionRoot: string) (workFolder: string) : string =
    let solutionDirName = Path.GetFileName solutionRoot

    let targetSolutionDir = Path.Combine(workFolder, solutionDirName)
    copyDirectoryRecursive solutionRoot targetSolutionDir

    printfnColor ConsoleColor.Cyan $"Solution copied to work folder: {targetSolutionDir}"
    targetSolutionDir

/// Copy the test project to the work folder (it may be outside the solution root)
let copyProjectToWorkFolder (sourceProjectPath: string) (workFolder: string) : string =
    let sourceDir =
        match Path.GetDirectoryName sourceProjectPath with
        | null -> Environment.CurrentDirectory
        | dir -> dir
    let sourceDirName = Path.GetFileName sourceDir

    let targetDir = Path.Combine(workFolder, sourceDirName)
    Directory.CreateDirectory(targetDir) |> ignore

    // Copy all files from source directory to work folder
    Directory.GetFiles(sourceDir)
    |> Array.iter (fun file ->
        let fileName = Path.GetFileName file
        let targetFile = Path.Combine(targetDir, fileName)
        File.Copy(file, targetFile, overwrite = true)
    )

    // Copy subdirectories recursively
    Directory.GetDirectories(sourceDir)
    |> Array.iter (fun dir ->
        let dirName = Path.GetFileName dir
        let targetSubDir = Path.Combine(targetDir, dirName)
        Directory.CreateDirectory(targetSubDir) |> ignore
        Directory.GetFiles(dir)
        |> Array.iter (fun file ->
            let fileName = Path.GetFileName file
            let targetFile = Path.Combine(targetSubDir, fileName)
            File.Copy(file, targetFile, overwrite = true)
        )
    )

    let targetProjectPath = Path.Combine(targetDir, Path.GetFileName sourceProjectPath)
    printfnColor ConsoleColor.Cyan $"Project copied to work folder: {targetProjectPath}"
    targetProjectPath

/// Fix ProjectReference paths in a copied .fsproj file so they point to the correct
/// locations inside the work folder's solution directory.
/// The test project is copied to workFolder/<testDirName>/ but the solution is at
/// workFolder/<solutionDirName>/. We need to rewrite relative ProjectReference paths
/// so they resolve correctly from the test project's new location.
let fixProjectReferences (originalTestPrj: string) (workTestPrj: string) (solutionRoot: string) (workFolder: string) : unit =
    let solutionDirName = Path.GetFileName solutionRoot
    let workSolutionDir = Path.Combine(workFolder, solutionDirName)
    
    if not (File.Exists workTestPrj) then
        invalidArg (nameof workTestPrj) $"Project file not found: {workTestPrj}"

    let originalTestDir =
        match Path.GetDirectoryName originalTestPrj with
        | null -> Environment.CurrentDirectory
        | dir -> dir
    let doc = XDocument.Load(workTestPrj)
    
    // SDK-style .fsproj files don't use the MSBuild XML namespace
    let references = doc.Descendants(XName.Get("ProjectReference"))
    
    references
    |> Seq.iter (fun refElem ->
        let includeAttr = refElem.Attribute "Include"
        match includeAttr with
        | null -> ()
        | attr ->
            let originalRelativePath = attr.Value
            // The original path is relative to the original test project's directory.
            // Resolve it to an absolute path in the original solution.
            let originalProjectPath = Path.GetFullPath(Path.Combine(originalTestDir, originalRelativePath))
            
            // Now find where this project should be in the work folder.
            // The project is inside the solution, so we compute its path relative to the solution root
            // and then place it under workSolutionDir.
            let relativeToSolution = Path.GetRelativePath(solutionRoot, originalProjectPath)
            let workProjectPath = Path.Combine(workSolutionDir, relativeToSolution)
            
            // Finally, compute the new relative path from the copied test project to the work project.
            let copiedTestDir =
                match Path.GetDirectoryName workTestPrj with
                | null -> Environment.CurrentDirectory
                | dir -> dir
            let newRelativePath = Path.GetRelativePath(copiedTestDir, workProjectPath)
            
            attr.Value <- newRelativePath
    )

    doc.Save(workTestPrj)

/// Create a global.json in the work folder with the Microsoft.Testing.Platform test runner opt-in.
/// This is required for .NET 10 SDK where VSTest target is no longer supported by MTP.
/// We also merge any SDK settings from the solution's global.json if it exists.
let copyGlobalJson (solutionRoot: string) (workFolder: string) : unit =
    let targetGlobalJson = Path.Combine(workFolder, "global.json")
    let sourceGlobalJson = Path.Combine(solutionRoot, "global.json")
    
    if not (File.Exists targetGlobalJson) then
    
        let sdkConfig =
            if File.Exists sourceGlobalJson then
                let sourceContent = File.ReadAllText(sourceGlobalJson)
                // Try to parse and extract SDK settings from the source global.json
                try
                    let parsed = System.Text.Json.JsonDocument.Parse(sourceContent)
                    let sdkElement = parsed.RootElement.GetProperty("sdk")
                    Some (System.Text.Json.JsonSerializer.Serialize(sdkElement))
                with
                | :? System.Text.Json.JsonException -> None
                | _ -> None
            else
                None
        
        let json : string =
            match sdkConfig with
            | Some sdk ->
                sprintf "{\"sdk\":%s,\"test\":{\"runner\":\"Microsoft.Testing.Platform\"}}" sdk
            | None ->
                "{\"test\":{\"runner\":\"Microsoft.Testing.Platform\"}}"
        
        File.WriteAllText(targetGlobalJson, json)
        printfnColor ConsoleColor.Cyan $"global.json created in work folder: {targetGlobalJson}"


let prepareWorkspace config : Result<Workspace,MultiError> =
    
    let workFolder = createWorkFolder ()
            
    let workSolutionDir = copySolutionToWorkFolder config.SolutionRoot workFolder

    let workTestPrj = copyProjectToWorkFolder config.TestProject workFolder

    fixProjectReferences config.TestProject workTestPrj config.SolutionRoot workFolder

    copyGlobalJson config.SolutionRoot workFolder

    build workTestPrj
   
    let ws : Workspace = 
        {
         Folder = workFolder
         SolutionDir = config.SolutionRoot
         TestProject = workTestPrj
        }
        
    Result.Ok ws