open System
open System.IO
open FSharp.Compiler.CodeAnalysis
open Sabota.Args
open Sabota.Config
open Sabota.Files
open Sabota.MultiError
open Sabota.MuationDefinitions
open Sabota.MutationCandidates
open Sabota.MutationTypes
open Sabota.Visitor
open Sabota.Mutator
open Sabota.Report


// src/Sabota/bin/Debug/net10.0/Sabota.exe -t resources/Test1/Test1.fsproj -p resources/Sample1/Sample1.fsproj -f resources/Sample1/Sample3.fs

let loadSettings () : Result<ConfigDefaults, MultiError> =
    let config = loadConfig defaultConfigPath

    let cliResult : Result<ParsedArgs, MultiError> =
        match parseArgs (Environment.GetCommandLineArgs()) with
        | Ok args -> Result.Ok args
        | Error msg -> Result.Error (ParseError msg)

    cliResult
    |> Result.bind (fun cli ->
        let testProjectOpt =
            match cli.TestProject with
            | Some v -> Some v
            | None -> config.TestProject
        match testProjectOpt with
        | None -> Result.Error (MissingTestProject "Missing required argument: -t (test project). Set it via CLI or in sabota_config.json.")
        | Some _ -> Result.Ok (cli, testProjectOpt)
    )
    |> Result.bind (fun (cli, testProjectOpt) ->
        let solutionRootOpt =
            match cli.SolutionRoot with
            | Some v -> Some v
            | None -> config.SolutionRoot
        match solutionRootOpt with
        | None -> Result.Error (MissingSolutionRoot "Missing required argument: -s (solution root). Set it via CLI or in sabota_config.json.")
        | Some _ -> Result.Ok (cli, testProjectOpt, solutionRootOpt)
    )
    |> Result.bind (fun (cli, testProjectOpt, solutionRootOpt) ->
        let testProject = defaultArg testProjectOpt "impossible"
        let solutionRoot = defaultArg solutionRootOpt "impossible"

        // Resolve target: CLI takes precedence, then config, then fallback to solution root
        let fileOpt, directoryOpt =
            match cli.Target with
            | Some (File files) ->
                Some (String.concat "," files), None
            | Some (Directory dirs) ->
                None, Some (String.concat "," dirs)
            | None ->
                match config.File, config.Directory with
                | Some f, _ -> Some f, None
                | _, Some d -> None, Some d
                | None, None ->
                    printfn "Neither -f nor -d provided; using solution root (-s) as directory target."
                    None, Some solutionRoot

        let mutationCategories =
            match cli.Categories with
            | Some cats ->
                printfn $"Using categories from CLI argument: %s{cats}"
                parseCategoriesString cats
            | None -> config.Categories

        // Build merged ConfigDefaults with all resolved values
        let mergedConfig =
            {
                TestProject = Some testProject
                SolutionRoot = Some solutionRoot
                File = fileOpt
                Directory = directoryOpt
                Categories = mutationCategories
            }

        Result.Ok mergedConfig
    )

let processFile (file : string) (workFolder : string) (workTestPrj : string) mutationCategories solutionRoot  : list<MutationResult> =
    let source = File.ReadAllText(file)

    printfn $"parse file: {file} {timestamp ()}"

    let parsingOptions =
        { FSharpParsingOptions.Default with
            SourceFiles = [| file |] }

    let checker = FSharpChecker.Create()
    
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

            restoreFile file workFile

            createMutationResult candidate source outcome
        )

    results



loadSettings ()
|> Result.bind (fun defaults ->

    let mutationCategories = defaults.Categories

    // Extract testProject and solutionRoot from defaults
    let testProject = defaultArg defaults.TestProject "Missing testProject in defaults"
    let solutionRoot = defaultArg defaults.SolutionRoot "Missing solutionRoot in defaults"

    // Resolve to absolute paths
    let testProject = Path.GetFullPath testProject
    let solutionRoot = Path.GetFullPath solutionRoot

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

        let files = discoverFiles target

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