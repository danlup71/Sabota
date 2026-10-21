module Mutant1.Report

type MutationStatus =
    | Killed
    | Survived
    | BuildFailed
    
type MutationOutcome =
    {
        Status: MutationStatus
        Interpretation: string
    }


type MutationResult =
    {
        MutationId: int
        MutationType: MutationKind
        File: string
        Range: FSharp.Compiler.Text.Range
        Original: string
        Mutated: string
        Outcome: MutationOutcome
        PreviewOriginal: string
        PreviewMutated: string
        Link: string
    }

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

/// Generate a code snippet with the mutation highlighted in red bold using ANSI escape codes
/// The mutated token is wrapped in \x1b[1;31m (bold red) and \x1b[0m (reset)
let generatePreviewMutated (source: string) (range: FSharp.Compiler.Text.Range) (original: string) (mutated: string) : string =
    let currentLine = range.StartLine
    let prevLine = getLine source (currentLine - 1)
    let current = getLine source currentLine
    let nextLine = getLine source (currentLine + 1)

    let redBold = "\x1b[1;31m"
    let reset = "\x1b[0m"

    // Replace the original token with the highlighted mutated token on the current line
    let highlightedCurrent = current.Replace(original, $"{redBold}{mutated}{reset}")

    let parts =
        [
            if prevLine <> "" then $"    {prevLine}"
            $"  > {highlightedCurrent}"
            if nextLine <> "" then $"    {nextLine}"
        ]
    String.concat "\n" parts
