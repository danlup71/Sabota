module Sabota.Args

open System
open System.IO
open Sabota.MultiError

type FileTarget =
    | File of string list
    | Directory of string list

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
                let files = args.[i + 1].Split(',') |> Array.map (fun s -> s.Trim()) |> Array.toList
                filePath <- Some files
                loop (i + 2)
            | "-d" when i + 1 < args.Length ->
                let dirs = args.[i + 1].Split(',') |> Array.map (fun s -> s.Trim()) |> Array.toList
                directoryPath <- Some dirs
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
            | Some fs, None -> Some (File fs)
            | None, Some ds -> Some (Directory ds)
            | None, None -> None
            | _, _ -> None

        Ok {
            TestProject = testProject
            SolutionRoot = solutionRoot
            Target = target
            Categories = categories
        }

let validate (testProject : string) (solutionRoot : string) (target : FileTarget) : Result<ParsedArgs, MultiError> =
    let errors =
        [
            if not (testProject.EndsWith(".fsproj", StringComparison.OrdinalIgnoreCase)) then
                $"Test project must be an F# project file (.fsproj): {testProject}"
            if not (File.Exists testProject) then
                $"Test project not found: {testProject}"
            if not (Directory.Exists solutionRoot) then
                $"Solution root not found: {solutionRoot}"
            match target with
            | File files ->
                for f in files do
                    if not (File.Exists f) then
                        $"File not found: {f}"
                    elif not (f.EndsWith(".fs", StringComparison.OrdinalIgnoreCase)) then
                        $"File must be an F# source file (.fs): {f}"
            | Directory dirs ->
                for d in dirs do
                    if not (Directory.Exists d) then
                        $"Directory not found: {d}"
        ]

    if List.isEmpty errors then
        Result.Ok {
            TestProject = Some testProject
            SolutionRoot = Some solutionRoot
            Target = Some target
            Categories = None
        }
    else
        Result.Error (ValidationError (String.Join("\n", errors)))
