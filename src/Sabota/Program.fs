
open System
open Sabota.Args
open Sabota.Config
open Sabota.Files
open Sabota.MultiError
open Sabota.MutationTypes
open Sabota.Mutator
open Sabota.Report
open Sabota.Workspace

// src/Sabota/bin/Debug/net10.0/Sabota.exe -t resources/Test1/Test1.fsproj -p resources/Sample1/Sample1.fsproj -f resources/Sample1/Sample3.fs

    
loadSettings ()
|> Result.bind (fun config ->

    let target = deriveFileTarget config

    validatePaths config.TestProject config.SolutionRoot target
    |> Result.bind (fun _ ->

        prepareWorkspace config
        
        |> Result.bind (fun Workspace ->
        
            let startTime = DateTime.Now

            let result =
                discoverFiles target
                |> List.collect (fun file -> processFile file Workspace.Folder Workspace.TestProject config.Categories Workspace.SolutionDir)

            let executionTime = DateTime.Now - startTime

            deleteWorkFolder Workspace.Folder

            reportResults result executionTime

            exit 0
    )
))
|> Result.mapError (fun err ->
                printError err
                exit 1
)
|> ignore