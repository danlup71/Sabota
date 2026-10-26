module Tests._004_Report

open System
open System.IO
open Xunit
open FSharp.Compiler.CodeAnalysis
open FSharp.Compiler.Text
open FSharp.Compiler.Syntax
open Sabota.MutationTypes
open Sabota.MutationCandidates
open Sabota.Visitor
open Sabota.MuationDefinitions
open Sabota.Report
open TestApp

let mutationCategories = [ MutationCategory.Equality
                           MutationCategory.RelationalBoundary
                           MutationCategory.RelationalReversal
                           MutationCategory.Boolean
                           MutationCategory.Arithmetic
                           MutationCategory.ControlFlow
                           ]


// --- US-005: Mutation status (Killed, Survived, BuildFailed) ---

[<Fact>]
let ``MutationStatus has Killed case`` () =
    let outcome = { Status = Killed; Interpretation = "Tests detected the change." }
    Assert.Equal(Killed, outcome.Status)

[<Fact>]
let ``MutationStatus has Survived case`` () =
    let outcome = { Status = Survived; Interpretation = "Tests did not detect the change." }
    Assert.Equal(Survived, outcome.Status)

[<Fact>]
let ``MutationStatus has BuildFailed case`` () =
    let outcome = { Status = BuildFailed; Interpretation = "Build failed." }
    Assert.Equal(BuildFailed, outcome.Status)

// --- US-006: Link to surviving mutation location ---

[<Fact>]
let ``generateLink produces file URL with line number`` () =
    let file = "SampleProject/Calculator.fs"
    let range = Range.mkRange file (Position.mkPos 42 13) (Position.mkPos 42 14)
    let link = generateLink file range
    Assert.Contains("Calculator.fs", link)
    Assert.Contains("L42", link)

[<Fact>]
let ``generateLink uses StartLine from range`` () =
    let file = "test.fs"
    let range = Range.mkRange file (Position.mkPos 5 1) (Position.mkPos 5 10)
    let link = generateLink file range
    Assert.Contains("L5", link)

// --- US-007: Original and mutated code previews ---

[<Fact>]
let ``generatePreviewOriginal shows current line with > prefix`` () =
    let source = "let x = 1\nlet flag = true\nlet y = 2"
    let range = Range.mkRange "test.fs" (Position.mkPos 2 11) (Position.mkPos 2 15) // "true" on line 2
    let preview = generatePreviewOriginal source range
    Assert.Contains("> let flag = true", preview)

[<Fact>]
let ``generatePreviewOriginal includes previous line`` () =
    let source = "let x = 1\nlet flag = true\nlet y = 2"
    let range = Range.mkRange "test.fs" (Position.mkPos 2 11) (Position.mkPos 2 15)
    let preview = generatePreviewOriginal source range
    Assert.Contains("let x = 1", preview)

[<Fact>]
let ``generatePreviewOriginal includes next line`` () =
    let source = "let x = 1\nlet flag = true\nlet y = 2"
    let range = Range.mkRange "test.fs" (Position.mkPos 2 11) (Position.mkPos 2 15)
    let preview = generatePreviewOriginal source range
    Assert.Contains("let y = 2", preview)

[<Fact>]
let ``generatePreviewOriginal omits previous line when on first line`` () =
    let source = "let flag = true\nlet y = 2"
    let range = Range.mkRange "test.fs" (Position.mkPos 1 11) (Position.mkPos 1 15)
    let preview = generatePreviewOriginal source range
    // On first line, no previous line should appear, but next line should be present
    Assert.Contains("> let flag = true", preview)
    Assert.Contains("let y = 2", preview) // next line is present
    // Preview should have 2 lines: the ">" current line and the next line (no previous line)
    let lines = preview.Split('\n')
    Assert.Equal(2, lines.Length)

[<Fact>]
let ``generatePreviewOriginal omits next line when on last line`` () =
    let source = "let x = 1\nlet flag = true"
    let range = Range.mkRange "test.fs" (Position.mkPos 2 11) (Position.mkPos 2 15)
    let preview = generatePreviewOriginal source range
    Assert.Contains("let x = 1", preview)
    Assert.Contains("> let flag = true", preview)

[<Fact>]
let ``generatePreviewMutated highlights mutation in red bold`` () =
    let source = "let flag = true"
    let range = Range.mkRange "test.fs" (Position.mkPos 1 11) (Position.mkPos 1 15)
    let preview = generatePreviewMutated source range "true" "false"
    Assert.Contains("<span class=\"mutation-highlight\">", preview)
    Assert.Contains("</span>", preview)
    Assert.Contains("false", preview)

[<Fact>]
let ``generatePreviewMutated replaces original with mutated token`` () =
    let source = "let isEqual x y = x = y"
    let range = Range.mkRange "test.fs" (Position.mkPos 1 20) (Position.mkPos 1 21) // "=" operator
    let preview = generatePreviewMutated source range "=" "<>"
    Assert.Contains("<span class=\"mutation-highlight\"><>", preview)
    Assert.Contains("</span>", preview)

[<Fact>]
let ``generatePreviewMutated includes surrounding lines`` () =
    let source = "let x = 1\nlet flag = true\nlet y = 2"
    let range = Range.mkRange "test.fs" (Position.mkPos 2 11) (Position.mkPos 2 15)
    let preview = generatePreviewMutated source range "true" "false"
    Assert.Contains("let x = 1", preview)
    Assert.Contains("let y = 2", preview)

// --- US-008: Summary of mutation results ---

[<Fact>]
let ``MutationResult contains all required fields`` () =
    let source = "let flag = true"
    let range = Range.mkRange "test.fs" (Position.mkPos 1 11) (Position.mkPos 1 15)
    let outcome = { Status = Killed; Interpretation = "Test detected change." }
    
    let result =
        {
            MutationId = 1
            MutationType = TrueToFalse
            File = "test.fs"
            Range = range
            Original = "true"
            Mutated = "false"
            Outcome = outcome
            PreviewOriginal = generatePreviewOriginal source range
            PreviewMutated = generatePreviewMutated source range "true" "false"
            Link = generateLink "test.fs" range
        }

    Assert.Equal(1, result.MutationId)
    Assert.Equal(TrueToFalse, result.MutationType)
    Assert.Equal("test.fs", result.File)
    Assert.Equal(Killed, result.Outcome.Status)
    Assert.NotEmpty(result.PreviewOriginal)
    Assert.NotEmpty(result.PreviewMutated)
    Assert.NotEmpty(result.Link)

[<Fact>]
let ``full pipeline generates MutationResult with previews`` () =
    let source = """
module TestModule
let x = 1
let flag = true
let y = 2
"""
    let tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString())
    Directory.CreateDirectory(tempDir) |> ignore
    let fileFs = Path.Combine(tempDir, "test.fs")
    File.WriteAllText(fileFs, source)

    try
        let parsed = TestApp.parseSource source
        let candidates = findMutationCandidates mutationCategories fileFs parsed |> List.map enrich
        let candidate = candidates |> List.find (fun c -> c.Kind = MutationKind.TrueToFalse)

        let outcome = { Status = Survived; Interpretation = "Tests did not detect the change." }
        let result =
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

        Assert.NotEmpty(result.PreviewOriginal)
        Assert.NotEmpty(result.PreviewMutated)
        Assert.NotEmpty(result.Link)
        Assert.Contains("file://", result.Link)
        Assert.Contains("true", result.PreviewOriginal)
        Assert.Contains("<span class=\"mutation-highlight\">", result.PreviewMutated)
    finally
        Directory.Delete(tempDir, true)

[<Fact>]
let ``getLine returns empty string for out of bounds`` () =
    let source = "line1\nline2"
    Assert.Equal("", getLine source 0)
    Assert.Equal("", getLine source 3)
    Assert.Equal("", getLine source -1)

[<Fact>]
let ``getLine returns correct line for valid index`` () =
    let source = "line1\nline2\nline3"
    Assert.Equal("line1", getLine source 1)
    Assert.Equal("line2", getLine source 2)
    Assert.Equal("line3", getLine source 3)

// --- ControlFlow preview tests ---

[<Fact>]
let ``generatePreviewMutated wraps ControlFlow condition with not`` () =
    // The F# parser includes the leading space in the condition's range
    // "if x > 0 then"
    //    12345678901234567890
    // Range covers " x > 0" (columns 4-9, 1-based: start=4, end=9 exclusive)
    let source = "if x > 0 then 1 else 0"
    let range = Range.mkRange "test.fs" (Position.mkPos 1 4) (Position.mkPos 1 9)
    let preview = generatePreviewMutated source range "if condition then" "if not condition then"
    // Should contain " not (x > 0)" with only "not" highlighted in red span
    let redBold = "<span class=\"mutation-highlight\">"
    let reset = "</span>"
    Assert.Contains($"{redBold}not{reset}", preview)
    // The condition should NOT be highlighted
    Assert.DoesNotContain($"{redBold}not ({redBold}", preview)
    Assert.DoesNotContain("ifnot", preview)
    Assert.DoesNotContain("if not condition then", preview)

[<Fact>]
let ``generatePreviewMutated removes not from negated ControlFlow condition`` () =
    // "if not (x > 0) then 1 else 0"
    //    1234567890123456789
    // The F# parser includes the leading space in the range
    // Range covers " not (x > 0)" (columns 4-15, 1-based)
    let source = "if not (x > 0) then 1 else 0"
    let range = Range.mkRange "test.fs" (Position.mkPos 1 4) (Position.mkPos 1 15)
    let preview = generatePreviewMutated source range "if not condition then" "if condition then"
    // Should contain " x > 0" highlighted in red span (the condition where not was removed)
    let redBold = "<span class=\"mutation-highlight\">"
    let reset = "</span>"
    Assert.Contains($"{redBold}x > 0{reset}", preview)
    Assert.DoesNotContain("if condition then", preview)

[<Fact>]
let ``ControlFlow mutation range includes leading space`` () =
    // Test with actual parsed source to get the real range
    let source = """
module TestModule
let check x =
    if x > 0 then
        1
    else
        0
"""
    let parsed = TestApp.parseSource source
    let candidates = findMutationCandidates mutationCategories "test.fs" parsed |> List.map enrich

    let conditionToNot = candidates |> List.filter (fun c -> c.Kind = MutationKind.ConditionToNotCondition)
    Assert.Single conditionToNot |> ignore

    let candidate = conditionToNot.[0]
    let range = candidate.Range
    
    // The line is "    if x > 0 then" (line 4, 1-based)
    Assert.Equal(4, range.StartLine)
    
    // Extract the line text
    let lines = source.Split('\n')
    let line = lines.[range.StartLine - 1]
    Assert.Equal("    if x > 0 then", line)
    
    // The F# parser includes the leading space in the condition's range
    let startCol0 = range.StartColumn - 1  // convert to 0-based
    let endCol = range.EndColumn            // already exclusive
    let rangeText = line.Substring(startCol0, endCol - startCol0)
    
    // The range includes the leading space: " x > 0"
    Assert.Equal(" x > 0", rangeText)
    
    // The enriched candidate should have the placeholder strings
    Assert.Equal("if condition then", candidate.Original)
    Assert.Equal("if not condition then", candidate.Replacement)
    
    // The preview should preserve the space and show "if not (x > 0)" with only "not" highlighted
    let preview = generatePreviewMutated source range candidate.Original candidate.Replacement
    let redBold = "<span class=\"mutation-highlight\">"
    let reset = "</span>"
    Assert.DoesNotContain("ifnot", preview)
    Assert.Contains($"{redBold}not{reset}", preview)
