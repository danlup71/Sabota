open System
open System.IO
open FSharp.Compiler.CodeAnalysis
open Mutant1.Args
open Mutant1.Files
open Mutant1.MuationDefinitions
open Mutant1.MutationCandidates
open Mutant1.Visitor
open Mutant1.Report

let createMutationResult (candidate: MutationCandidate) (source: string) (outcome: MutationOutcome) =
    {
        MutationId = candidate.Id
        MutationType = candidate.Kind
        File = candidate.File
        Range = candidate.Range
        Original = candidate.Original
        Mutated = candidate.Replacement
        Outcome = outcome
        PreviewOriginal = generatePreviewOriginal source candidate.Range
        PreviewMutated = generatePreviewMutated source candidate.Range candidate.Original candidate.Replacement
        Link = generateLink candidate.File candidate.Range
    }

// src/Saboteur/bin/Debug/net10.0/Saboteur.exe -t resources/Test1/Test1.fsproj -p resources/Sample1/Sample1.fsproj -f resources/Sample1/Sample3.fs

let checker = FSharpChecker.Create()

let argsResult = parseAndValidate (System.Environment.GetCommandLineArgs())

let parsedArgs =
    match argsResult with
    | Error msg ->
        eprintfn "Error: %s" msg
        eprintfn "Usage: Saboteur -t <testProject> -p <sourceProject> (-f <filePath> | -d <directory>)"
        exit 1
    | Ok args -> args

let testPrj = parsedArgs.TestProject
let sourcePrj = parsedArgs.SourceProject

let files =
    match parsedArgs.Target with
    | File f -> [f]
    | Directory d ->
        Directory.GetFiles(d, "*.fs")
        |> Array.toList

let processFile (file : string) : list<MutationResult> =
    let source = File.ReadAllText(file)

    printfn "parse file: %s" file

    let parsingOptions =
        { FSharpParsingOptions.Default with
            SourceFiles = [| file |] }

    let parseResult =
        checker.ParseFile(
            file,
            FSharp.Compiler.Text.SourceText.ofString source,
            parsingOptions)
        |> Async.RunSynchronously

    printfn "detect mutation candidates"

    findMutationCandidates file parseResult.ParseTree
    |> List.map enrich
    |> List.map ( fun candidate ->
            
            copyFileAsOriginal candidate.File |> ignore

            applyMutation candidate

            let exitCode, stdout, stderr = build sourcePrj 
            if (exitCode = 0) then 
                let tr = test testPrj

                let outcome =
                    if tr.Failed > 0 then
                        {
                            Status = Killed
                            Interpretation = "Tests detected the behavioural change."
                        }
                    else
                        {
                            Status = Survived
                            Interpretation = ""
                        }                    

                restoreFromOriginal file |> ignore
                
                createMutationResult candidate source outcome
            else
                let outcome =
                        {
                            Status = Killed
                            Interpretation = "Build failed."
                        }
                        
                restoreFromOriginal file |> ignore
                
                createMutationResult candidate source outcome
            )

let result =
    files
    |> List.collect processFile

let killed = result |> List.filter _.Outcome.Status.IsKilled |> List.length
let survived = result |> List.filter _.Outcome.Status.IsSurvived |> List.length
printfnColor ConsoleColor.DarkGreen $"\nKILLED {killed} !"
printfnColor ConsoleColor.Red $"SURVIVED {survived} !"
           
0 |> ignore