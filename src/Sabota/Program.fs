
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
    | ParseError msg 
    | ValidationError msg 
    | GenericError msg ->
        printError msg
    | MissingTestProject msg 
    | MissingSolutionRoot msg ->
        printError msg
        printUsage()
    exit 1
)
|> ignore