module Sabota.Files

open System
open System.IO
open System.Xml.Linq
open Sabota.Args
open Sabota.Config
open Sabota.MutationCandidates
open Sabota.MutationTypes

let printfnColor (color: ConsoleColor) text : 'a =
    Console.ForegroundColor <- color
    let result = printfn text
    Console.ResetColor()
    result

let timestamp () : string =
    let now = DateTime.Now
    sprintf "[%02d:%02d:%02d.%03d]" now.Hour now.Minute now.Second (now.Millisecond / 10)


let rec copyDirectoryRecursive (sourceDir: string) (targetDir: string) : unit =
    Directory.CreateDirectory(targetDir) |> ignore

    // Copy all files from source directory to target
    Directory.GetFiles(sourceDir)
    |> Array.iter (fun file ->
        let fileName = Path.GetFileName file
        let targetFile = Path.Combine(targetDir, fileName)
        File.Copy(file, targetFile, overwrite = true)
    )

    // Recursively copy subdirectories, excluding build artifacts and IDE folders
    let excludedDirs = Set.ofList ["bin"; "obj"; ".git"; ".vs"; "sabota"; "CoverageResults"; "docs"; "legacy"; "debug"; "release"]
    Directory.GetDirectories(sourceDir)
    |> Array.filter (fun dir ->
        let dirName = Path.GetFileName dir
        not (excludedDirs.Contains dirName || dirName.StartsWith('.'))
    )
    |> Array.iter (fun dir ->
        let dirName = Path.GetFileName dir
        let targetSubDir = Path.Combine(targetDir, dirName)
        copyDirectoryRecursive dir targetSubDir
    )


/// Copy a single file to the work folder, preserving its directory structure relative to a base directory.
/// If the file is outside the base directory, it is copied using its full path structure.
let copyFileToWorkFolder (originalFile: string) (baseDir: string) (workDir: string) : string =
    let relativePath = Path.GetRelativePath(baseDir, originalFile)
    let workFile = Path.Combine(workDir, relativePath)

    // Ensure the target directory exists
    let workFileDir =
        match Path.GetDirectoryName workFile with
        | null -> Environment.CurrentDirectory
        | dir -> dir
    if not (Directory.Exists workFileDir) then
        Directory.CreateDirectory(workFileDir) |> ignore

    // Copy the file
    File.Copy(originalFile, workFile, overwrite = true)
    workFile

let mapFileToWorkFolder (originalFile: string) (sourceDir: string) (workDir: string) : string =
    let relativePath = Path.GetRelativePath(sourceDir, originalFile)
    Path.Combine(workDir, relativePath)

/// Check if a file path contains an excluded build directory name (debug, release, bin, obj).
let isExcludedFile (file: string) : bool =
    let excludedBuildDirs = Set.ofList ["debug"; "release"; "bin"; "obj"]
    let parts = file.Split([|'\\'; '/'|], StringSplitOptions.RemoveEmptyEntries)
    parts |> Array.exists (fun part -> excludedBuildDirs.Contains(part.ToLower()))

/// Derive a FileTarget from ConfigDefaults, parsing comma-separated file or directory paths.
/// Defaults to the solution root directory if neither file nor directory is specified.
let deriveFileTarget (config: ConfigDefaults) : FileTarget =
    match config.File, config.Directory with
    | Some files, _ ->
        let file_list = files.Split(',') |> Array.map _.Trim() |> Array.toList
        Files file_list
    | _, Some dirs ->
        let dir_list = dirs.Split(',') |> Array.map _.Trim() |> Array.toList
        Directories dir_list
    | _, _ ->
        Directories [config.SolutionRoot]

/// Discover all .fs files from a FileTarget, excluding build artifact directories.
/// For File targets, returns the files as-is.
/// For Directory targets, recursively scans for *.fs files, filtering out excluded directories.
let discoverFiles (target: FileTarget) : string list =
    match target with
    | Files fs -> fs
    | Directories dirs ->
        dirs
        |> List.collect (fun d ->
            Directory.GetFiles(d, "*.fs", SearchOption.AllDirectories)
            |> Array.filter (fun f -> not (isExcludedFile f))
            |> Array.toList
        )

/// Clean up the work folder after all files are processed
let deleteWorkFolder (workFolder: string) : unit =
    if Directory.Exists workFolder then
        Directory.Delete(workFolder, recursive = true)
        printfnColor ConsoleColor.Cyan $"Work folder deleted: {workFolder}"

/// Restore the original file in the work folder for the next mutation
let restoreFile (originalFile: string) (workFile: string) : unit =
    if File.Exists originalFile && File.Exists workFile then
        File.Copy(originalFile, workFile, overwrite = true)

/// Extract the inner expression from a "not (X)" text
let stripNot (text: string) : string =
    let trimmed = text.Trim()
    if trimmed.StartsWith("not (") && trimmed.EndsWith(")") then
        trimmed.Substring(5, trimmed.Length - 6)
    else
        trimmed

let applyMutation (mutation: MutationCandidate) : unit =
    let filePath = mutation.File
    let r = mutation.Range
    let mutable replacement = mutation.Replacement

    printf $"apply mutation {mutation.Kind,-20} {timestamp ()}"

    if not (File.Exists filePath) then
        invalidArg (nameof filePath) $"Source file not found: {filePath}"

    let text = File.ReadAllText(filePath)
    let lines = text.Split('\n')

    // FSharp.Compiler.Text range: StartLine/StartColumn are 1-based inclusive,
    // EndLine/EndColumn are 1-based exclusive.
    // For 0-based indices: start = value - 1, end (exclusive) = value (no subtraction)
    let startLine = r.StartLine - 1
    let startCol = r.StartColumn - 1
    let endLine = r.EndLine - 1
    let endCol = r.EndColumn

    if startLine < 0 || startLine >= lines.Length || endLine < 0 || endLine >= lines.Length then
        invalidArg (nameof r) "Range is out of file bounds."

    let sb = System.Text.StringBuilder()

    // lines before the range
    for i in 0 .. startLine - 1 do
        sb.Append(lines[i]).Append('\n') |> ignore

    // start line: keep text before startCol, then append replacement
    if startLine = endLine then
        // single-line replacement
        let line = lines[startLine]
        // Extract the text covered by the range
        let rangeLen = min (endCol - startCol) (line.Length - startCol)
        let rangeText =
            if startCol < line.Length && rangeLen > 0 then
                line.Substring(startCol, rangeLen)
            else
                ""

        // The F# compiler range may include surrounding whitespace.
        // Find the actual non-whitespace content within the range and replace only that.
        let trimmedStart = rangeText.Length - rangeText.TrimStart().Length // chars trimmed from start
        let trimmedEnd = rangeText.Length - rangeText.TrimEnd().Length     // chars trimmed from end
        let innerText = rangeText.Trim()

        let leadingWhitespace =
            if trimmedStart > 0 then rangeText.Substring(0, trimmedStart)
            else ""
        let trailingWhitespace =
            if trimmedEnd > 0 then rangeText.Substring(rangeText.Length - trimmedEnd, trimmedEnd)
            else ""

        // For ReturnListValueToEmpty, the F# compiler range may include a leading character
        // before the '[' (e.g., in "returnCollection[0]", the range may start at 'n' instead of '[').
        // Find the actual '[' within the innerText and adjust the leading text accordingly.
        let leadingExtra, actualInner =
            if mutation.Kind = MutationTypes.MutationKind.ReturnListValueToEmpty && innerText <> "" then
                let bracketIndex = innerText.IndexOf('[')
                if bracketIndex > 0 then
                    let extra = innerText.Substring(0, bracketIndex)
                    let actual = innerText.Substring(bracketIndex)
                    (extra, actual)
                else
                    ("", innerText)
            else
                ("", innerText)

        // For ControlFlow mutations, construct the replacement from the actual source text
        if actualInner <> "" then
            match mutation.Kind with
            | MutationTypes.MutationKind.ConditionToNotCondition ->
                replacement <- $"not ({actualInner})"
            | MutationTypes.MutationKind.NotConditionToCondition ->
                replacement <- stripNot actualInner
            | _ -> ()

        // Keep text before the range, then leading whitespace, leading extra, replacement, trailing whitespace, then after
        let before = if startCol > 0 then line.Substring(0, startCol) else ""
        let after = if endCol < line.Length then line.Substring(endCol) else ""

        if actualInner <> "" then
            sb.Append(before).Append(leadingWhitespace).Append(leadingExtra).Append(replacement).Append(trailingWhitespace).Append(after).Append('\n') |> ignore
        else
            // Range is all whitespace, just keep it as-is
            sb.Append(line).Append('\n') |> ignore
    else
        // multi-line replacement
        let startLineText = lines[startLine]
        let before = if startCol > 0 then startLineText.Substring(0, startCol) else ""
        
        // For ReturnListValueToEmpty, preserve leading characters before '[' in multi-line case too
        let multiLineLeadingExtra =
            if mutation.Kind = MutationTypes.MutationKind.ReturnListValueToEmpty then
                let rangeText =
                    let rangeLen = min (endCol - startCol) (startLineText.Length - startCol)
                    if startCol < startLineText.Length && rangeLen > 0 then
                        startLineText.Substring(startCol, rangeLen)
                    else
                        ""
                let trimmed = rangeText.TrimStart()
                let leadingLen = rangeText.Length - trimmed.Length
                let bracketIndex = trimmed.IndexOf('[')
                if bracketIndex > 0 then
                    rangeText.Substring(0, leadingLen + bracketIndex)
                elif leadingLen > 0 then
                    rangeText.Substring(0, leadingLen)
                else
                    ""
            else
                ""
        
        sb.Append(before).Append(multiLineLeadingExtra).Append(replacement).Append('\n') |> ignore

    // lines after the range
    for i in endLine + 1 .. lines.Length - 1 do
        sb.Append(lines[i]).Append('\n') |> ignore

    // Remove trailing newline if original didn't have one
    let result = sb.ToString()
    let finalText =
        if text.EndsWith('\n') && result.EndsWith('\n') then
            result
        elif not (text.EndsWith('\n')) && result.EndsWith('\n') then
            result.Substring(0, result.Length - 1)
        else
            result

    //printfn $"\n{finalText}\n"
    File.WriteAllText(filePath, finalText)


type TestRunnerOutcome =
    | TestsPassed
    | TestsFailed
    | BuildFailed

/// Build the test project only once after copying the working tree.
/// This allows using --no-restore during mutation iterations for better performance
let build (filePath: string) : unit =
    printfn $"Building test project: {filePath} {timestamp ()}"
    if not (File.Exists filePath) then
        invalidArg (nameof filePath) $"File not found: {filePath}"

    let startInfo = System.Diagnostics.ProcessStartInfo()
    startInfo.FileName <- "dotnet"
    startInfo.Arguments <- $"build {filePath}"
    startInfo.UseShellExecute <- false
    startInfo.RedirectStandardOutput <- true
    startInfo.RedirectStandardError <- true

    use proc = System.Diagnostics.Process.Start(startInfo)
    proc.WaitForExit()

    let output = proc.StandardOutput.ReadToEnd()
    let error = proc.StandardError.ReadToEnd()

    match proc.ExitCode with
    | 0 ->
        printfnColor ConsoleColor.DarkGreen $"  -> Build succeeded {timestamp ()}"
    | _ ->
        printfnColor ConsoleColor.Red $"  -> Build failed {timestamp ()}"
        printfn $"{output}"
        printfn $"{error}"
        invalidArg (nameof filePath) "Initial build of test project failed"

let test (filePath: string) (solutionRoot: string) : TestRunnerOutcome =
    printf $"  -> testing {timestamp ()}"
    if not (File.Exists filePath) then
        invalidArg (nameof filePath) $"File not found: {filePath}"

    let startInfo = System.Diagnostics.ProcessStartInfo()
    startInfo.FileName <- "dotnet"
    startInfo.Arguments <- $"test {filePath} --no-restore"
    startInfo.WorkingDirectory <- solutionRoot
    startInfo.UseShellExecute <- false
    startInfo.RedirectStandardOutput <- true
    startInfo.RedirectStandardError <- true

    use proc = System.Diagnostics.Process.Start(startInfo)

    let outputTask  = proc.StandardOutput.ReadToEndAsync() 
    let errorTask  = proc.StandardError.ReadToEndAsync() 
    proc.WaitForExit()
    let output = outputTask.Result
    let error = errorTask.Result
    
    match proc.ExitCode with
    | 0 ->
        printfnColor ConsoleColor.Red $"  -> SURVIVED {timestamp ()}"
        TestsPassed

    | _ when (output.Contains("Build FAILED") || output.Contains("error")) ->
        printfnColor ConsoleColor.Black $"  -> BUILD FAILED {timestamp ()}"
        printfn $"{output}"
        TestRunnerOutcome.BuildFailed

    | _ ->
        printfnColor ConsoleColor.DarkGreen $"  -> KILLED {timestamp ()}"
        TestsFailed
        
