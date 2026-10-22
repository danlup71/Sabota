module Sabota.Report

open System
open System.IO
open Sabota.MutationTypes

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

/// Get the display name for a mutation kind
let mutationKindLabel (kind: MutationKind) : string =
    match kind with
    | EqualityToInequality -> "= -> <>"
    | InequalityToEquality -> "<> -> ="
    | TrueToFalse -> "true -> false"
    | FalseToTrue -> "false -> true"
    | GreaterToGreaterOrEqual -> "> -> >="
    | GreaterOrEqualToGreater -> ">= -> >"
    | LessToLessOrEqual -> "< -> <="
    | LessOrEqualToLess -> "<= -> <"
    | GreaterToLess -> "> -> <"
    | GreaterToLessOrEqual -> "> -> <="
    | LessToGreater -> "< -> >"
    | LessToGreaterOrEqual -> "< -> >="
    | PlusToMinus -> "+ -> -"
    | MinusToPlus -> "- -> +"
    | TimesToDivide -> "* -> /"
    | DivideToTimes -> "/ -> *"
    | AndToOr -> "&& -> ||"
    | OrToAnd -> "|| -> &&"
    | SomeToNone -> "Some -> None"
    | ReturnListValueToEmpty -> "[...] -> []"
    | ConditionToNotCondition -> "if c -> if not c"
    | NotConditionToCondition -> "if not c -> if c"

/// Get the CSS class for a mutation status
let statusClass (status: MutationStatus) : string =
    match status with
    | Killed -> "killed"
    | Survived -> "survived"
    | BuildFailed -> "build-failed"

/// HTML-escape a string
let htmlEscape (s: string) : string =
    s.Replace("&", "&amp;")
     .Replace("<", "&lt;")
     .Replace(">", "&gt;")
     .Replace("\"", "&quot;")
     .Replace("'", "&#39;")

/// Strip ANSI escape codes from a string
let stripAnsi (s: string) =
    System.Text.RegularExpressions.Regex.Replace(s, @"\x1b\[[0-9;]*m", "")

/// Generate the full HTML report and write it to a file in the sabota_result folder
let generateHtmlReport (results: MutationResult list) (executionTime: TimeSpan) : string =
    let resultDir = "sabota_result"
    Directory.CreateDirectory(resultDir) |> ignore

    let timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss")
    let fileName = sprintf "%s.htm" timestamp
    let filePath = Path.Combine(resultDir, fileName)

    let total = results.Length
    let killedCount = results |> List.filter (fun r -> r.Outcome.Status = Killed) |> List.length
    let survivedCount = results |> List.filter (fun r -> r.Outcome.Status = Survived) |> List.length
    let buildFailedCount = results |> List.filter (fun r -> r.Outcome.Status = BuildFailed) |> List.length

    let mutationScore =
        if total = 0 then 0.0
        else float killedCount / float total * 100.0

    let generatedDate = DateTime.Now.ToString("G")
    let executionTimeStr = 
        let hours = executionTime.Hours
        let minutes = executionTime.Minutes
        let seconds = executionTime.Seconds
        if hours > 0 then
            sprintf "%dh %dm %ds" hours minutes seconds
        elif minutes > 0 then
            sprintf "%dm %ds" minutes seconds
        else
            sprintf "%ds" seconds

    let rows =
        results
        |> List.mapi (fun i r ->
            let rowId = sprintf "row-%d" i
            let line = r.Range.StartLine
            let mutLabel = mutationKindLabel r.MutationType
            let statusText = htmlEscape (string r.Outcome.Status)
            let statusCls = statusClass r.Outcome.Status
            let fileEscaped = htmlEscape r.File
            let lineText = string line

            let cleanOriginalPreview = r.PreviewOriginal |> stripAnsi |> htmlEscape
            let cleanMutatedPreview =
                let mutated = r.PreviewMutated |> stripAnsi
                // If the preview already contains HTML span tags (ControlFlow mutations),
                // escape only the text content, preserving the span tags
                if mutated.Contains("<span class=\"mutation-highlight\">") then
                    let openPlaceholder = "\u0000HIGHLIGHT_OPEN\u0000"
                    let closePlaceholder = "\u0000HIGHLIGHT_CLOSE\u0000"
                    let withPlaceholders = mutated.Replace("<span class=\"mutation-highlight\">", openPlaceholder).Replace("</span>", closePlaceholder)
                    let escaped = htmlEscape withPlaceholders
                    let escapedOpen = htmlEscape openPlaceholder
                    let escapedClose = htmlEscape closePlaceholder
                    escaped.Replace(escapedOpen, "<span class=\"mutation-highlight\">").Replace(escapedClose, "</span>")
                else
                    // No span tags - escape everything and add highlight around mutated text
                    let escaped = htmlEscape mutated
                    let escapedMutated = htmlEscape r.Mutated
                    if r.Mutated <> "" && escaped.Contains(escapedMutated) then
                        let highlight = sprintf "<span class=\"mutation-highlight\">%s</span>" escapedMutated
                        escaped.Replace(escapedMutated, highlight)
                    else escaped

            let interpretationHtml =
                if r.Outcome.Interpretation <> "" then
                    sprintf "<p class=\"interpretation\">%s</p>" (htmlEscape r.Outcome.Interpretation)
                else ""

            sprintf """
            <tr class="result-row" data-row="%s" data-status="%s">
                <td class="status-cell"><span class="status-badge %s">%s</span></td>
                <td class="file-cell">%s</td>
                <td class="line-cell">%s</td>
                <td class="mutation-cell">%s</td>
            </tr>
            <tr class="detail-row" id="%s">
                <td colspan="4">
                    <div class="detail-content">
                        <div class="code-comparison">
                            <div class="code-block original">
                                <div class="code-header">Original</div>
                                <pre><code>%s</code></pre>
                            </div>
                            <div class="code-block mutated">
                                <div class="code-header">Mutated</div>
                                <pre><code>%s</code></pre>
                            </div>
                            %s
                        </div>
                    </div>
                </td>
            </tr>""" rowId statusCls statusCls statusText fileEscaped lineText mutLabel rowId cleanOriginalPreview cleanMutatedPreview interpretationHtml
        )
        |> String.concat "\n        "

    let buildFailedCard =
        sprintf """
            <div class="summary-card build-failed" data-filter="build-failed">
                <div class="label">Build Failed</div>
                <div class="count">%d</div>
            </div>""" buildFailedCount

    let css = """
        * { margin: 0; padding: 0; box-sizing: border-box; }
        body {
            font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, Oxygen, Ubuntu, sans-serif;
            background: #1e1e2e;
            color: #cdd6f4;
            padding: 2rem;
        }
        .container {
            max-width: 1200px;
            margin: 0 auto;
        }
        h1 {
            font-size: 1.8rem;
            margin-bottom: 0.5rem;
            color: #cba6f7;
        }
        .timestamp {
            color: #6c7086;
            margin-bottom: 1.5rem;
            font-size: 0.9rem;
        }
        .summary {
            display: flex;
            gap: 1rem;
            margin-bottom: 2rem;
        }
        .summary-card {
            padding: 1rem 1.5rem;
            border-radius: 8px;
            background: #313244;
            min-width: 150px;
        }
        .summary-card.total { border-left: 4px solid #89b4fa; }
        .summary-card.killed { border-left: 4px solid #a6e3a1; }
        .summary-card.survived { border-left: 4px solid #f38ba8; }
        .summary-card.build-failed { border-left: 4px solid #fab387; }
        .summary-card.score { border-left: 4px solid #94e2d5; }
        .summary-card .label { font-size: 0.8rem; color: #6c7086; text-transform: uppercase; }
        .summary-card .count { font-size: 1.5rem; font-weight: bold; }
        .summary-card { cursor: pointer; transition: opacity 0.2s; }
        .summary-card:hover { opacity: 0.8; }
        .summary-card.active { background: #45475a; box-shadow: inset 0 0 0 2px #cdd6f4; }
        table {
            width: 100%%;
            border-collapse: collapse;
            background: #313244;
            border-radius: 8px;
            overflow: hidden;
        }
        th {
            background: #45475a;
            padding: 0.75rem 1rem;
            text-align: left;
            font-weight: 600;
            color: #cdd6f4;
        }
        td {
            padding: 0.6rem 1rem;
            border-top: 1px solid #45475a;
        }
        .result-row { cursor: pointer; }
        .result-row:hover td { background: #45475a; }
        .detail-row { display: none; }
        .detail-row.expanded { display: table-row; }
        .detail-content {
            padding: 1rem;
            background: #1e1e2e;
        }
        .code-comparison {
            display: grid;
            grid-template-columns: 1fr 1fr;
            gap: 1rem;
        }
        .code-block {
            background: #313244;
            border-radius: 6px;
            overflow: hidden;
        }
        .code-header {
            padding: 0.5rem 1rem;
            font-weight: 600;
            font-size: 0.85rem;
            text-transform: uppercase;
        }
        .code-block.original .code-header { background: #45475a; color: #a6e3a1; }
        .code-block.mutated .code-header { background: #45475a; color: #f38ba8; }
        pre {
            margin: 0;
            padding: 1rem;
            overflow-x: auto;
        }
        code {
            font-family: 'Cascadia Code', 'Fira Code', 'Consolas', monospace;
            font-size: 0.9rem;
            line-height: 1.5;
        }
        .mutation-highlight {
            color: #f38ba8;
            font-weight: bold;
            background: rgba(243, 139, 168, 0.15);
            padding: 0.1em 0.3em;
            border-radius: 3px;
        }
        .status-badge {
            display: inline-block;
            padding: 0.2rem 0.6rem;
            border-radius: 4px;
            font-size: 0.8rem;
            font-weight: 600;
            text-transform: uppercase;
        }
        .status-badge.killed { background: rgba(166, 227, 161, 0.2); color: #a6e3a1; }
        .status-badge.survived { background: rgba(243, 139, 168, 0.2); color: #f38ba8; }
        .status-badge.build-failed { background: rgba(250, 179, 135, 0.2); color: #fab387; }
        .file-cell { color: #89b4fa; }
        .line-cell { color: #fab387; text-align: center; }
        .code-comparison {
            display: grid;
            grid-template-columns: 1fr 1fr;
            gap: 1rem;
        }
        .interpretation {
            margin-top: 0.75rem;
            padding: 0.5rem 0.75rem;
            background: rgba(137, 180, 250, 0.1);
            border-left: 3px solid #89b4fa;
            border-radius: 0 4px 4px 0;
            font-size: 0.9rem;
            color: #cdd6f4;
        }
        .result-row.expanded .toggle-icon {
            transform: rotate(90deg);
        }
    """

    let part1 = """<!DOCTYPE html>
<html lang="en">
<head>
    <meta charset="UTF-8">
    <meta name="viewport" content="width=device-width, initial-scale=1.0">
    <title>Sabota Mutation Report - """

    let part2 = """</title>
    <style>
"""

    let part3 = """
    </style>
</head>
<body>
    <div class="container">
        <h1>Sabota Mutation Report</h1>
        <div class="timestamp">Generated: """

    let part3b = """ | Execution time: """

    let part4 = """</div>
        <div class="summary">
            <div class="summary-card total" data-filter="all">
                <div class="label">Total</div>
                <div class="count">"""

    let part5 = """</div>
            </div>
            <div class="summary-card killed" data-filter="killed">
                <div class="label">Killed</div>
                <div class="count">"""

    let part6 = """</div>
            </div>
            <div class="summary-card survived" data-filter="survived">
                <div class="label">Survived</div>
                <div class="count">"""

    let part7 = """</div>
            </div>"""

    let part7b = """
            <div class="summary-card score" data-filter="score">
                <div class="label">Mutation Score</div>
                <div class="count">"""

    let part8 = """%</div>
            </div>"""

    let part9 = """
        </div>
        <table>
            <thead>
                <tr>
                    <th>Status</th>
                    <th>File</th>
                    <th>Line</th>
                    <th>Mutation</th>
                </tr>
            </thead>
            <tbody>
        """

    let footer = """
            </tbody>
        </table>
    </div>
    <script>
        document.querySelectorAll('.result-row').forEach(function(row) {
            row.addEventListener('click', function() {
                var rowId = this.getAttribute('data-row');
                var detailRow = document.getElementById(rowId);
                var isExpanded = detailRow.classList.contains('expanded');
                if (isExpanded) {
                    detailRow.classList.remove('expanded');
                    this.classList.remove('expanded');
                } else {
                    detailRow.classList.add('expanded');
                    this.classList.add('expanded');
                }
            });
        });

        document.querySelectorAll('.summary-card[data-filter]').forEach(function(card) {
            card.addEventListener('click', function() {
                var filter = this.getAttribute('data-filter');
                document.querySelectorAll('.summary-card').forEach(function(c) { c.classList.remove('active'); });
                this.classList.add('active');

                document.querySelectorAll('.result-row').forEach(function(row) {
                    var status = row.getAttribute('data-status');
                    var detailRow = document.getElementById(row.getAttribute('data-row'));
                    if (filter === 'all') {
                        row.style.display = '';
                        if (detailRow) detailRow.style.display = '';
                    } else if (filter === 'score') {
                        row.style.display = '';
                        if (detailRow) detailRow.style.display = '';
                    } else {
                        if (status === filter) {
                            row.style.display = '';
                            if (detailRow) detailRow.style.display = '';
                        } else {
                            row.style.display = 'none';
                            if (detailRow) detailRow.style.display = 'none';
                        }
                    }
                });
            });
        });
    </script>
</body>
</html>"""

    let header =
        part1 + timestamp + part2 + css + part3 + generatedDate + part3b + executionTimeStr + part4
        + string total + part5 + string killedCount + part6
        + string survivedCount + part7 + buildFailedCard + part7b + string mutationScore + part8
        + part9

    let html = header + rows + footer

    File.WriteAllText(filePath, html)
    Path.GetFullPath(filePath)


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
        let before = if startCol > 0 then line.Substring(0, startCol) else ""
        let after = if endCol < line.Length then line.Substring(endCol) else ""
        sb.Append(before).Append(highlightedMutated).Append(after).Append('\n') |> ignore
    else
        // multi-line replacement
        let startLineText = lines[startLine]
        let before = if startCol > 0 then startLineText.Substring(0, startCol) else ""
        sb.Append(before).Append(highlightedMutated).Append('\n') |> ignore

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
