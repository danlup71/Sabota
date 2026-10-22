module Sabota.Args

open System
open System.IO

type FileTarget =
    | File of string
    | Directory of string

type ParsedArgs = {
    TestProject : string option
    SolutionRoot : string option
    Target : FileTarget option
    Categories : string option
}

type ParseResult =
    | Ok of ParsedArgs
    | Error of string

let parseArgs (args : string[]) : ParseResult =
    let mutable testProject = None
    let mutable solutionRoot = None
    let mutable filePath = None
    let mutable directoryPath = None
    let mutable categories = None

    let rec loop i =
        if i >= args.Length then ()
        else
            match args.[i] with
            | "-t" when i + 1 < args.Length ->
                testProject <- Some args.[i + 1]
                loop (i + 2)
            | "-s" when i + 1 < args.Length ->
                solutionRoot <- Some args.[i + 1]
                loop (i + 2)
            | "-f" when i + 1 < args.Length ->
                filePath <- Some args.[i + 1]
                loop (i + 2)
            | "-d" when i + 1 < args.Length ->
                directoryPath <- Some args.[i + 1]
                loop (i + 2)
            | "-c" when i + 1 < args.Length ->
                categories <- Some args.[i + 1]
                loop (i + 2)
            | _ ->
                loop (i + 1)

    loop 0

    // Check mutual exclusivity
    if filePath.IsSome && directoryPath.IsSome then
        Error "-f and -d are mutually exclusive; specify only one"
    else
        let target =
            match filePath, directoryPath with
            | Some f, None -> Some (File f)
            | None, Some d -> Some (Directory d)
            | None, None -> None
            | _, _ -> None

        Ok {
            TestProject = testProject
            SolutionRoot = solutionRoot
            Target = target
            Categories = categories
        }

let validate (testProject : string) (solutionRoot : string) (target : FileTarget) : ParseResult =
    let errors =
        [
            if not (File.Exists testProject) then
                $"Test project not found: {testProject}"
            if not (Directory.Exists solutionRoot) then
                $"Solution root not found: {solutionRoot}"
            match target with
            | File f ->
                if not (File.Exists f) then
                    $"File not found: {f}"
                elif not (f.EndsWith(".fs", StringComparison.OrdinalIgnoreCase)) then
                    $"File must be an F# source file (.fs): {f}"
            | Directory d ->
                if not (Directory.Exists d) then
                    $"Directory not found: {d}"
        ]

    if List.isEmpty errors then
        Ok {
            TestProject = Some testProject
            SolutionRoot = Some solutionRoot
            Target = Some target
            Categories = None
        }
    else
        Error (String.Join("\n", errors))
