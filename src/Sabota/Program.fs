open System
open System.IO
open FSharp.Compiler.CodeAnalysis
open Sabota.Args
open Sabota.Files
open Sabota.MuationDefinitions
open Sabota.MutationCandidates
open Sabota.Visitor
open Sabota.Report

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

// src/Sabota/bin/Debug/net10.0/Sabota.exe -t resources/Test1/Test1.fsproj -p resources/Sample1/Sample1.fsproj -f resources/Sample1/Sample3.fs

let checker = FSharpChecker.Create()

let argsResult = parseAndValidate (System.Environment.GetCommandLineArgs())

let parsedArgs =
    match argsResult with
    | Error msg ->
        eprintfn "Error: %s" msg
        eprintfn "Usage: Saboteur -t <testProject> -s <solutionRoot> (-f <filePath> | -d <directory>)"
        exit 1
    | Ok args -> args

let testPrj = parsedArgs.TestProject
let solutionRoot = parsedArgs.SolutionRoot

let files =
    match parsedArgs.Target with
    | File f -> [f]
    | Directory d ->
        Directory.GetFiles(d, "*.fs")
        |> Array.toList


// Get the solution directory name for mapping in work folder
let solutionDirName = Path.GetFileName solutionRoot

let processFile (file : string) (workSolutionDir : string) (workTestPrj : string) : list<MutationResult> =
    let source = File.ReadAllText(file)

    printfn $"parse file: {file} {timestamp ()}"

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

    let candidates = findMutationCandidates file parseResult.ParseTree |> List.map enrich

    // Map the candidate file to the work folder (relative to solution root)
    let workFile = mapFileToWorkFolder file solutionRoot workSolutionDir

    let results =
        candidates |> List.map (fun candidate ->
            // Apply mutation to the COPY in the work folder
            let workCandidate = { candidate with File = workFile }
            applyMutation workCandidate

            let testOutcome = test workTestPrj
            let outcome =
                match testOutcome with
                | TestRunnerOutcome.BuildFailed ->
                    {
                        Status = MutationStatus.BuildFailed
                        Interpretation = "Build failed."
                    }
                | TestRunnerOutcome.TestsFailed ->
                    {
                        Status = MutationStatus.Killed
                        Interpretation = "Tests detected the behavioural change."
                    }
                | TestRunnerOutcome.TestsPassed ->
                    {
                        Status = MutationStatus.Survived
                        Interpretation = candidate.SurvivorInterpretation
                    }
            
            // Restore the original file in the work folder for the next mutation
            restoreFile file workFile

            createMutationResult candidate source outcome
        )

    results

// Create a single work folder and copy the solution once for all files
let workFolder = createWorkFolder ()
let workSolutionDir = copySolutionToWorkFolder solutionRoot workFolder
let workTestPrj = mapFileToWorkFolder testPrj solutionRoot workSolutionDir

let result =
    files
    |> List.collect (fun file -> processFile file workSolutionDir workTestPrj)

// Clean up the work folder after all files are processed
deleteWorkFolder workFolder

let killed = result |> List.filter _.Outcome.Status.IsKilled |> List.length
let survived = result |> List.filter _.Outcome.Status.IsSurvived |> List.length
printfnColor ConsoleColor.DarkGreen $"\nKILLED {killed} !"
printfnColor ConsoleColor.Red $"SURVIVED {survived} !"

let reportPath = generateHtmlReport result
let forwardPath = reportPath.Replace("\\", "/")
let linkTarget = sprintf "file:///%s" forwardPath
let clickable = sprintf "\x1b]8;;%s\x1b\\%s\x1b]8;;\x1b\\" linkTarget reportPath
printfnColor ConsoleColor.Cyan $"\nHTML report generated: {clickable}"

0 |> ignore