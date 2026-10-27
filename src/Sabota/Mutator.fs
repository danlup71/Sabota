module Sabota.Mutator
open Sabota.MutationCandidates
open Sabota.MutationTypes


/// Generate a link to the file at the given line number
let generateLink (file: string) (range: FSharp.Compiler.Text.Range) : string =
    let line = range.StartLine
    $"file://{file}#L{line}"

/// Extract a line from source text by 1-based line number; returns empty string if out of bounds
let getLine (source: string) (lineNumber: int) : string =
    let lines = source.Split('\n')
    if lineNumber >= 1 && lineNumber <= lines.Length then
        lines.[lineNumber - 1]
    else
        ""

/// Generate a code snippet showing the previous line, current line, and next line
/// Format: "prev_line\n> current_line\nnext_line"
let generatePreviewOriginal (source: string) (range: FSharp.Compiler.Text.Range) : string =
    let currentLine = range.StartLine
    let prevLine = getLine source (currentLine - 1)
    let current = getLine source currentLine
    let nextLine = getLine source (currentLine + 1)

    let parts =
        [
            if prevLine <> "" then $"    {prevLine}"
            $"  > {current}"
            if nextLine <> "" then $"    {nextLine}"
        ]
    String.concat "\n" parts

/// Extract the inner expression from a "not (X)" text
let stripNotForPreview (text: string) : string =
    let trimmed = text.Trim()
    if trimmed.StartsWith("not (") && trimmed.EndsWith(")") then
        trimmed.Substring(5, trimmed.Length - 6)
    else
        trimmed

/// Generate a code snippet with the mutation highlighted using HTML span tags
/// The mutated token is wrapped in <span class="mutation-highlight"> for red bold styling in the HTML report
/// Uses the Range to extract the actual original text from the source and replace it with the mutation.
let generatePreviewMutated (source: string) (range: FSharp.Compiler.Text.Range) (original: string) (mutated: string) : string =
    let lines = source.Split('\n')
    let redBold = "<span class=\"mutation-highlight\">"
    let reset = "</span>"

    // FSharp.Compiler.Text range: StartLine/StartColumn are 1-based inclusive,
    // EndLine/EndColumn are 1-based exclusive.
    let startLine = range.StartLine - 1
    let startCol = range.StartColumn - 1
    let endLine = range.EndLine - 1
    let endCol = range.EndColumn

    // Extract the actual source text at the range for ControlFlow mutations
    let rangeText =
        if startLine = endLine && startCol < lines[startLine].Length then
            let len = min (endCol - startCol) (lines[startLine].Length - startCol)
            if len > 0 then lines[startLine].Substring(startCol, len).Trim() else ""
        else
            ""

    let highlightedMutated =
        // For ControlFlow mutations, construct the replacement from the actual source text
        // The F# parser includes the leading space after "if" in the condition's range,
        // so we need to preserve that space in the replacement
        // Only highlight the added/removed "not" keyword in red
        if rangeText <> "" && original = "if condition then" then
            // ConditionToNotCondition: highlight only "not" in red
            $" {redBold}not{reset} ({rangeText})"
        elif rangeText <> "" && original = "if not condition then" then
            // NotConditionToCondition: highlight the stripped condition (where "not" was removed) in red
            $" {redBold}{stripNotForPreview rangeText}{reset}"
        else
            $"{redBold}{mutated}{reset}"

    // Build the mutated source by replacing the range with the highlighted mutation
    let sb = System.Text.StringBuilder()

    // lines before the range
    for i in 0 .. startLine - 1 do
        sb.Append(lines[i]).Append('\n') |> ignore

    if startLine = endLine then
        // single-line replacement
        let line = lines[startLine]
        // For ReturnListValueToEmpty, the F# compiler range may include a leading character
        // before the '[' (e.g., in "returnCollection[0]", the range may start at 'n' instead of '[').
        // Find the actual '[' within the range and preserve any leading characters.
        let leadingExtra =
            if original = "[...]" then
                let rangeLen = min (endCol - startCol) (line.Length - startCol)
                let rangeText =
                    if startCol < line.Length && rangeLen > 0 then
                        line.Substring(startCol, rangeLen)
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
        let before = if startCol > 0 then line.Substring(0, startCol) else ""
        let after = if endCol < line.Length then line.Substring(endCol) else ""
        sb.Append(before).Append(leadingExtra).Append(highlightedMutated).Append(after).Append('\n') |> ignore
    else
        // multi-line replacement
        let startLineText = lines[startLine]
        // For ReturnListValueToEmpty, preserve leading characters before '[' in multi-line case too
        let multiLineLeadingExtra =
            if original = "[...]" then
                let rangeLen = min (endCol - startCol) (startLineText.Length - startCol)
                let rangeText =
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
        let before = if startCol > 0 then startLineText.Substring(0, startCol) else ""
        sb.Append(before).Append(multiLineLeadingExtra).Append(highlightedMutated).Append('\n') |> ignore

    // lines after the range
    for i in endLine + 1 .. lines.Length - 1 do
        sb.Append(lines[i]).Append('\n') |> ignore

    let mutatedSource = sb.ToString()

    // Now generate the preview snippet from the mutated source, centered on the start line
    let currentLine = range.StartLine
    let prevLine = getLine mutatedSource (currentLine - 1)
    let current = getLine mutatedSource currentLine
    let nextLine = getLine mutatedSource (currentLine + 1)

    let parts =
        [
            if prevLine <> "" then $"    {prevLine}"
            $"  > {current}"
            if nextLine <> "" then $"    {nextLine}"
        ]
    String.concat "\n" parts
    
    
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