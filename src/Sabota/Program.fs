open System
open System.IO
open Sabota.Args
open Sabota.Config
open Sabota.Files
open Sabota.MultiError
open Sabota.Mutator
open Sabota.Report

// src/Sabota/bin/Debug/net10.0/Sabota.exe -t resources/Test1/Test1.fsproj -p resources/Sample1/Sample1.fsproj -f resources/Sample1/Sample3.fs

loadSettings ()
|> Result.bind (fun defaults ->

    let mutationCategories = defaults.Categories

    // Extract testProject and solutionRoot from defaults (guaranteed to be present)
    let testProject = Path.GetFullPath defaults.TestProject
    let solutionRoot = Path.GetFullPath defaults.SolutionRoot

    // Derive FileTarget from defaults
    let target =
        match defaults.File, defaults.Directory with
        | Some files, _ ->
            let file_list = files.Split(',') |> Array.map (fun s -> s.Trim()) |> Array.toList
            File (List.map Path.GetFullPath file_list)
        | _, Some dirs ->
            let dir_list = dirs.Split(',') |> Array.map (fun s -> s.Trim()) |> Array.toList
            Directory (List.map Path.GetFullPath dir_list)
        | _, _ ->
            Directory [Path.GetFullPath solutionRoot]

    validatePaths testProject solutionRoot target
    |> Result.bind (fun _ ->

        // Create a single work folder and copy the solution once for all files
        let workFolder = createWorkFolder ()
        let workSolutionDir = copySolutionToWorkFolder solutionRoot workFolder

        // Copy the test project to the work folder (it may be outside the solution root)
        let workTestPrj = copyProjectToWorkFolder testProject workFolder

        // Fix ProjectReference paths in the copied test project so they point to the correct
        // locations inside the work folder's solution directory
        fixProjectReferences testProject workTestPrj solutionRoot workFolder

        // Copy global.json to the work folder so dotnet test uses the correct test runner
        // (required for .NET 10 SDK Microsoft.Testing.Platform opt-in)
        copyGlobalJson solutionRoot workFolder

        // Build the test project only once after copying the working tree.
        // This allows using --no-restore during mutation iterations for better performance
        build workTestPrj

        let startTime = DateTime.Now

        let files = discoverFiles target

        let result =
            files
            |> List.collect (fun file -> processFile file workFolder workTestPrj mutationCategories solutionRoot)

        let executionTime = DateTime.Now - startTime

        deleteWorkFolder workFolder

        reportResults result executionTime

        exit 0
    )
)
|> Result.mapError (fun err ->
    match err with
    | ParseError msg ->
        eprintfn $"Error: %s{msg}"
        eprintfn "Usage: Sabota [-t <testProject>] [-s <solutionRoot>] [(-f <file1,file2,...> | -d <directory>)] [-c <categories>]"
        eprintfn "  If neither -f nor -d is provided, -s (solution root) will be used as the directory target."
        eprintfn "  Arguments can be set in sabota_config.json or via CLI (CLI takes precedence)."
    | MissingTestProject msg ->
        eprintfn $"Error: %s{msg}"
        eprintfn "Usage: Sabota [-t <testProject>] [-s <solutionRoot>] [(-f <file1,file2,...> | -d <directory>)] [-c <categories>]"
        eprintfn "  Arguments can be set in sabota_config.json or via CLI (CLI takes precedence)."
    | MissingSolutionRoot msg ->
        eprintfn $"Error: %s{msg}"
        eprintfn "Usage: Sabota [-t <testProject>] [-s <solutionRoot>] [(-f <file1,file2,...> | -d <directory>)] [-c <categories>]"
        eprintfn "  Arguments can be set in sabota_config.json or via CLI (CLI takes precedence)."
    | ValidationError msg ->
        eprintfn $"Validation error: %s{msg}"
    | GenericError msg ->
        eprintfn $"Error: %s{msg}"
    exit 1
)
|> ignore