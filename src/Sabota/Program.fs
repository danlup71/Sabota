
open System
open Sabota.Args
open Sabota.Config
open Sabota.Files
open Sabota.MultiError
open Sabota.Mutator
open Sabota.Report

// src/Sabota/bin/Debug/net10.0/Sabota.exe -t resources/Test1/Test1.fsproj -p resources/Sample1/Sample1.fsproj -f resources/Sample1/Sample3.fs

loadSettings ()
|> Result.bind (fun config ->

    let target = deriveFileTarget config

    validatePaths config.TestProject config.SolutionRoot target
    |> Result.bind (fun _ ->

        // Create a single work folder and copy the solution once for all files
        let workFolder = createWorkFolder ()
        let workSolutionDir = copySolutionToWorkFolder config.SolutionRoot workFolder

        // Copy the test project to the work folder (it may be outside the solution root)
        let workTestPrj = copyProjectToWorkFolder config.TestProject workFolder

        fixProjectReferences config.TestProject workTestPrj config.SolutionRoot workFolder

        copyGlobalJson config.SolutionRoot workFolder

        build workTestPrj

        let startTime = DateTime.Now

        let result =
            discoverFiles target
            |> List.collect (fun file -> processFile file workFolder workTestPrj config.Categories config.SolutionRoot)

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