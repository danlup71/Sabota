module Sabota.Args

open System
open System.IO

type FileTarget =
    | File of string
    | Directory of string

type ParsedArgs = {
    TestProject : string
    SolutionRoot : string
    Target : FileTarget
}

type ParseResult =
    | Ok of ParsedArgs
    | Error of string

let parseArgs (args : string[]) : ParseResult =
    let mutable testProject = None
    let mutable solutionRoot = None
    let mutable filePath = None
    let mutable directoryPath = None

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
            | _ ->
                loop (i + 1)

    loop 0

    let target =
        match filePath, directoryPath with
        | Some f, None -> Some (File f)
        | None, Some d -> Some (Directory d)
        | Some _, Some _ ->
            Error "-f and -d are mutually exclusive; specify only one" |> fun _ -> None
        | None, None -> None

    let missing =
        [
            if testProject.IsNone then "-t (test project)"
            if solutionRoot.IsNone then "-s (solution root)"
            if target.IsNone then "-f (file) or -d (directory)"
        ]

    if target.IsNone && (filePath.IsSome && directoryPath.IsSome) then
        Error "-f and -d are mutually exclusive; specify only one"
    elif List.isEmpty missing then
        Ok {
            TestProject = testProject.Value
            SolutionRoot = solutionRoot.Value
            Target = target.Value
        }
    else
        let joined = String.Join(", ", missing)
        Error $"Missing required arguments: {joined}"

let validate (parsed : ParsedArgs) : ParseResult =
    let errors =
        [
            if not (File.Exists parsed.TestProject) then
                $"Test project not found: {parsed.TestProject}"
            if not (Directory.Exists parsed.SolutionRoot) then
                $"Solution root not found: {parsed.SolutionRoot}"
            match parsed.Target with
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
        Ok parsed
    else
        Error (String.Join("\n", errors))

let parseAndValidate (args : string[]) : ParseResult =
    match parseArgs args with
    | Error msg -> Error msg
    | Ok parsed ->
        let absolute =
            {
                TestProject = Path.GetFullPath parsed.TestProject
                SolutionRoot = Path.GetFullPath parsed.SolutionRoot
                Target =
                    match parsed.Target with
                    | File f -> File (Path.GetFullPath f)
                    | Directory d -> Directory (Path.GetFullPath d)
            }
        validate absolute
