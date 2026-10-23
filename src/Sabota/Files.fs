module Sabota.Files

open System
open System.IO
open Sabota.MutationCandidates

let printfnColor (color: ConsoleColor) text : 'a =
    Console.ForegroundColor <- color
    let result = printfn text
    Console.ResetColor()
    result

let timestamp () : string =
    let now = DateTime.Now
    sprintf "[%02d:%02d:%02d.%03d]" now.Hour now.Minute now.Second (now.Millisecond / 10)

let createWorkFolder () : string =
    let tempDir = Path.GetTempPath()
    let sabotaDir = Path.Combine(tempDir, "sabota")
    Directory.CreateDirectory(sabotaDir) |> ignore

    let timestamp = DateTime.Now.ToString("yyyyMMdd-HHmmss-ff")
    let workFolder = Path.Combine(sabotaDir, $"run-{timestamp}")
    Directory.CreateDirectory(workFolder) |> ignore

    printfnColor ConsoleColor.Cyan $"Work folder created: {workFolder}"
    workFolder

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
    let excludedDirs = Set.ofList ["bin"; "obj"; ".git"; ".vs"]
    Directory.GetDirectories(sourceDir)
    |> Array.filter (fun dir ->
        let dirName = Path.GetFileName dir
        not (excludedDirs.Contains dirName)
    )
    |> Array.iter (fun dir ->
        let dirName = Path.GetFileName dir
        let targetSubDir = Path.Combine(targetDir, dirName)
        copyDirectoryRecursive dir targetSubDir
    )

let copySolutionToWorkFolder (solutionRoot: string) (workFolder: string) : string =
    let solutionDirName = Path.GetFileName solutionRoot

    let targetSolutionDir = Path.Combine(workFolder, solutionDirName)
    copyDirectoryRecursive solutionRoot targetSolutionDir

    printfnColor ConsoleColor.Cyan $"Solution copied to work folder: {targetSolutionDir}"
    targetSolutionDir

let copyProjectToWorkFolder (sourceProjectPath: string) (workFolder: string) : string =
    let sourceDir = Path.GetDirectoryName sourceProjectPath
    let sourceDirName = Path.GetFileName sourceDir

    let targetDir = Path.Combine(workFolder, sourceDirName)
    Directory.CreateDirectory(targetDir) |> ignore

    // Copy all files from source directory to work folder
    Directory.GetFiles(sourceDir)
    |> Array.iter (fun file ->
        let fileName = Path.GetFileName file
        let targetFile = Path.Combine(targetDir, fileName)
        File.Copy(file, targetFile, overwrite = true)
    )

    // Copy subdirectories recursively
    Directory.GetDirectories(sourceDir)
    |> Array.iter (fun dir ->
        let dirName = Path.GetFileName dir
        let targetSubDir = Path.Combine(targetDir, dirName)
        Directory.CreateDirectory(targetSubDir) |> ignore
        Directory.GetFiles(dir)
        |> Array.iter (fun file ->
            let fileName = Path.GetFileName file
            let targetFile = Path.Combine(targetSubDir, fileName)
            File.Copy(file, targetFile, overwrite = true)
        )
    )

    let targetProjectPath = Path.Combine(targetDir, Path.GetFileName sourceProjectPath)
    printfnColor ConsoleColor.Cyan $"Project copied to work folder: {targetProjectPath}"
    targetProjectPath

/// Copy a single file to the work folder, preserving its directory structure relative to a base directory.
/// If the file is outside the base directory, it is copied using its full path structure.
let copyFileToWorkFolder (originalFile: string) (baseDir: string) (workDir: string) : string =
    let relativePath = Path.GetRelativePath(baseDir, originalFile)
    let workFile = Path.Combine(workDir, relativePath)

    // Ensure the target directory exists
    let workFileDir = Path.GetDirectoryName workFile
    if not (Directory.Exists workFileDir) then
        Directory.CreateDirectory(workFileDir) |> ignore

    // Copy the file
    File.Copy(originalFile, workFile, overwrite = true)
    workFile

let mapFileToWorkFolder (originalFile: string) (sourceDir: string) (workDir: string) : string =
    let relativePath = Path.GetRelativePath(sourceDir, originalFile)
    Path.Combine(workDir, relativePath)

let deleteWorkFolder (workFolder: string) : unit =
    if Directory.Exists workFolder then
        Directory.Delete(workFolder, recursive = true)
        printfnColor ConsoleColor.Cyan $"Work folder deleted: {workFolder}"

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

        // For ControlFlow mutations, construct the replacement from the actual source text
        if innerText <> "" then
            match mutation.Kind with
            | MutationTypes.MutationKind.ConditionToNotCondition ->
                replacement <- $"not ({innerText})"
            | MutationTypes.MutationKind.NotConditionToCondition ->
                replacement <- stripNot innerText
            | _ -> ()

        // Keep text before the range, then leading whitespace, replacement, trailing whitespace, then after
        let before = if startCol > 0 then line.Substring(0, startCol) else ""
        let after = if endCol < line.Length then line.Substring(endCol) else ""

        if innerText <> "" then
            sb.Append(before).Append(leadingWhitespace).Append(replacement).Append(trailingWhitespace).Append(after).Append('\n') |> ignore
        else
            // Range is all whitespace, just keep it as-is
            sb.Append(line).Append('\n') |> ignore
    else
        // multi-line replacement
        let startLineText = lines[startLine]
        let before = if startCol > 0 then startLineText.Substring(0, startCol) else ""
        sb.Append(before).Append(replacement).Append('\n') |> ignore

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

let test (filePath: string) : TestRunnerOutcome =
    printf $"  -> testing {timestamp ()}"
    if not (File.Exists filePath) then
        invalidArg (nameof filePath) $"File not found: {filePath}"

    let startInfo = System.Diagnostics.ProcessStartInfo()
    startInfo.FileName <- "dotnet"
    startInfo.Arguments <- $"test {filePath}"
    startInfo.UseShellExecute <- false
    startInfo.RedirectStandardOutput <- true
    startInfo.RedirectStandardError <- true

    use proc = System.Diagnostics.Process.Start(startInfo)
    proc.WaitForExit()

    let output = proc.StandardOutput.ReadToEnd() 
    let error = proc.StandardError.ReadToEnd() 
    
    match proc.ExitCode with
    | 0 ->
        printfnColor ConsoleColor.Red $"  -> SURVIVED {timestamp ()}"
        TestsPassed

    | _ when (output.Contains("Build FAILED") || output.Contains("error")) ->
        printfnColor ConsoleColor.Black $"  -> BUILD FAILED {timestamp ()}"
        TestRunnerOutcome.BuildFailed

    | _ ->
        printfnColor ConsoleColor.DarkGreen $"  -> KILLED {timestamp ()}"
        TestsFailed
        
