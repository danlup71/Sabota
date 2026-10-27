open Sabota.Args
open Sabota.Report
open Sabota.Workspace

// src/Sabota/bin/Debug/net10.0/Sabota.exe -t resources/Test1/Test1.fsproj -p resources/Sample1/Sample1.fsproj -f resources/Sample1/Sample3.fs  
    
let run() =
    loadSettings ()
    |> Result.bind prepareWorkspace
    |> Result.bind runMutationAnalysis    
    
match run() with
| Result.Ok runResult ->
    reportResults runResult
    exit 0

| Result.Error err ->
    printError err
    exit 1
    
