open System
open System.IO
open FSharp.Compiler.CodeAnalysis
open Sabota.Args
open Sabota.Config
open Sabota.Files
open Sabota.MuationDefinitions
open Sabota.MutationCandidates
open Sabota.MutationTypes
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

// Load config defaults
let config = loadConfig defaultConfigPath

// Parse CLI args
let cliArgs = parseArgs (System.Environment.GetCommandLineArgs())

match cliArgs with
| Error msg ->
    eprintfn "Error: %s" msg
    eprintfn "Usage: Sabota [-t <testProject>] [-s <solutionRoot>] [(-f <file1,file2,...> | -d <directory>)] [-c <categories>]"
    eprintfn "  If neither -f nor -d is provided, -s (solution root) will be used as the directory target."
    eprintfn "  Arguments can be set in sabota_config.json or via CLI (CLI takes precedence)."
    exit 1
| Ok cli ->

    // Merge CLI with config defaults: CLI takes precedence
    let testProject =
        match cli.TestProject with
        | Some v -> v
        | None ->
            match config.TestProject with
            | Some v -> v
            | None ->
                eprintfn "Error: Missing required argument: -t (test project)"
                eprintfn "  Set it via CLI or in sabota_config.json."
                exit 1

    let solutionRoot =
        match cli.SolutionRoot with
        | Some v -> v
        | None ->
            match config.SolutionRoot with
            | Some v -> v
            | None ->
                eprintfn "Error: Missing required argument: -s (solution root)"
                eprintfn "  Set it via CLI or in sabota_config.json."
                exit 1

    let target =
        match cli.Target with
        | Some t -> t
        | None ->
            match config.File, config.Directory with
            | Some f, _ ->
                let files = f.Split(',') |> Array.map (fun s -> s.Trim()) |> Array.toList
                File files
            | _, Some d -> Directory [d]
            | None, None ->
                // Fallback: use solution root as the directory target
                printfn "Neither -f nor -d provided; using solution root (-s) as directory target."
                Directory [solutionRoot]

    let mutationCategories =
        match cli.Categories with
        | Some cats ->
            printfn "Using categories from CLI argument: %s" cats
            parseCategoriesString cats
        | None -> config.Categories

    // Resolve to absolute paths
    let testProject = Path.GetFullPath testProject
    let solutionRoot = Path.GetFullPath solutionRoot
    let target =
        match target with
        | File files -> File (List.map Path.GetFullPath files)
        | Directory dirs -> Directory (List.map Path.GetFullPath dirs)

    // Validate
    match validate testProject solutionRoot target with
    | Error msg ->
        eprintfn "Error: %s" msg
        exit 1
    | Ok _ -> ()

    let files =
        match target with
        | File fs -> fs
        | Directory dirs ->
            dirs
            |> List.collect (fun d ->
                Directory.GetFiles(d, "*.fs", SearchOption.AllDirectories)
                |> Array.toList
            )

    let processFile (file : string) (workFolder : string) (workTestPrj : string) : list<MutationResult> =
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

        let candidates = findMutationCandidates mutationCategories file parseResult.ParseTree |> List.map enrich

        // Copy the source file to the work folder (it may be outside the solution root)
        let workFile = copyFileToWorkFolder file solutionRoot workFolder

        let results =
            candidates
            |> List.map (fun candidate ->
                // Apply mutation to the COPY in the work folder
                let workCandidate = { candidate with File = workFile }
                applyMutation workCandidate

                let testOutcome = test workTestPrj solutionRoot
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

    // Copy the test project to the work folder (it may be outside the solution root)
    let workTestPrj = copyProjectToWorkFolder testProject workFolder

    // Fix ProjectReference paths in the copied test project so they point to the correct
    // locations inside the work folder's solution directory
    fixProjectReferences testProject workTestPrj solutionRoot workFolder

    // Copy global.json to the work folder so dotnet test uses the correct test runner
    // (required for .NET 10 SDK Microsoft.Testing.Platform opt-in)
    copyGlobalJson solutionRoot workFolder

    // Build the test project once after copying the working tree
    // This allows using --no-restore during mutation iterations for better performance
    build workTestPrj

    let startTime = DateTime.Now

    let result =
        files
        |> List.collect (fun file -> processFile file workFolder workTestPrj)

    let executionTime = DateTime.Now - startTime

    // Clean up the work folder after all files are processed
    deleteWorkFolder workFolder

    let killed = result |> List.filter _.Outcome.Status.IsKilled |> List.length
    let survived = result |> List.filter _.Outcome.Status.IsSurvived |> List.length
    let buildFailed = result |> List.filter _.Outcome.Status.IsBuildFailed |> List.length
    printfnColor ConsoleColor.DarkGreen $"\nKILLED {killed} !"
    printfnColor ConsoleColor.Red $"SURVIVED {survived} !"
    printfnColor ConsoleColor.Black $"BUILD FAILED {buildFailed} !"

    let reportPath = generateHtmlReport result executionTime
    let forwardPath = reportPath.Replace("\\", "/")
    let linkTarget = sprintf "file:///%s" forwardPath
    let clickable = sprintf "\x1b]8;;%s\x1b\\%s\x1b]8;;\x1b\\" linkTarget reportPath
    printfnColor ConsoleColor.Cyan $"\nHTML report generated: {clickable}"

    0 |> ignore