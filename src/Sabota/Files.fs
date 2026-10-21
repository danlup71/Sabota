module Mutant1.Files

open System
open System.IO
open Mutant1.MutationCandidates

let printfnColor (color: ConsoleColor) text : 'a =
    Console.ForegroundColor <- color
    let result = printfn text
    Console.ResetColor()
    result

let copyFileAsOriginal (filePath: string) : string =
    printfn "" //""Create original file copy"
    
    if not (File.Exists filePath) then
        invalidArg (nameof filePath) $"Source file not found: {filePath}"

    let directory = Path.GetDirectoryName filePath
    let fileNameWithoutExt = Path.GetFileNameWithoutExtension filePath
    let extension = Path.GetExtension filePath

    let newFileName = $"{fileNameWithoutExt}_original{extension}"
    let newFilePath = Path.Combine(directory, newFileName)

    File.Copy(filePath, newFilePath, overwrite = true)
    newFilePath
    
    
let restoreFromOriginal (filePath: string) : string =
    //printfn $"restore original files"

    let directory = Path.GetDirectoryName filePath
    let fileNameWithoutExt = Path.GetFileNameWithoutExtension filePath
    let extension = Path.GetExtension filePath

    // Build the _original backup path from the given original path
    let originalFilePath = Path.Combine(directory, $"{fileNameWithoutExt}_original{extension}")

    if not (File.Exists originalFilePath) then
        invalidArg (nameof filePath) $"Backup file not found: {originalFilePath}"

    // Read the backup content before copying, so we can verify after restore
    let backupContent = File.ReadAllText(originalFilePath)

    // Copy the backup back over the mutated file
    File.Copy(originalFilePath, filePath, overwrite = true)

    // Verify the restored file matches the backup
    let restoredContent = File.ReadAllText(filePath)
    if restoredContent <> backupContent then
        invalidArg (nameof filePath) $"Restore verification failed: restored file does not match backup at {originalFilePath}"

    // Safe to delete the temporary backup now that restore is verified
    File.Delete originalFilePath

    filePath


let applyMutation (mutation:MutationCandidate): unit =
    
    let filePath = mutation.File
    let r = mutation.Range
    let replacement = mutation.Replacement
    
    printf $"apply mutation {mutation.Kind}"

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

    File.WriteAllText(filePath, finalText)


let build (filePath: string) : int * string * string =
    printf "      -> building"

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
   
    let stdout = proc.StandardOutput.ReadToEnd()
    let stderr = proc.StandardError.ReadToEnd()
    let exitCode = proc.ExitCode

    (exitCode, stdout, stderr)


type TestResult = {
    Result: string
    Failed: int
    Passed: int
    Total: int
    Skipped: int
    Duration: string
    Project: string
}

let test (filePath: string) : TestResult =
    printf "      -> testing"
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
   
    let stdout = proc.StandardOutput.ReadToEnd()
    let exitCode = proc.ExitCode

    let result = if exitCode = 0 then "Passed" else "Failed"

    let opts = System.Text.RegularExpressions.RegexOptions.IgnoreCase

    let tryInt (pattern: string) =
        match System.Text.RegularExpressions.Regex.Match(stdout, pattern, opts) with
        | m when m.Success -> int m.Groups[1].Value
        | _ -> 0

    let tryStr (pattern: string) =
        match System.Text.RegularExpressions.Regex.Match(stdout, pattern, opts) with
        | m when m.Success -> m.Groups[1].Value.Trim()
        | _ -> ""

    let failed = tryInt @"Failed:\s*(\d+)"
    let passed = tryInt @"Passed:\s*(\d+)"
    let total = tryInt @"Total:\s*(\d+)"
    let skipped = tryInt @"Skipped:\s*(\d+)"
    let duration = tryStr @"Duration:\s*(\d+\s*ms)"
    let project = tryStr @"(\S+\.dll)\s*\(net"

    if failed > 0
        then
            printfnColor ConsoleColor.DarkGreen "      -> KILLED"
        else
            printfnColor ConsoleColor.Red "      -> SURVIVED"
    
    {
        Result = result
        Failed = failed
        Passed = passed
        Total = total
        Skipped = skipped
        Duration = duration
        Project = project
    }
    
    
