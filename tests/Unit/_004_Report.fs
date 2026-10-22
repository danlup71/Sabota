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

let parseSource (source: string) : ParsedInput =
    let checker = FSharpChecker.Create()
    let tempFile = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString() + ".fs")
    try
        File.WriteAllText(tempFile, source)
        let parsingOptions = { FSharpParsingOptions.Default with SourceFiles = [| tempFile |] }
        let parseResult =
            checker.ParseFile(
                tempFile,
                SourceText.ofString source,
                parsingOptions)
            |> Async.RunSynchronously
        parseResult.ParseTree
    finally
        if File.Exists tempFile then
            File.Delete(tempFile)

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
    Assert.Contains("\x1b[1;31m", preview) // red bold ANSI code
    Assert.Contains("\x1b[0m", preview)    // reset ANSI code
    Assert.Contains("false", preview)

[<Fact>]
let ``generatePreviewMutated replaces original with mutated token`` () =
    let source = "let isEqual x y = x = y"
    let range = Range.mkRange "test.fs" (Position.mkPos 1 20) (Position.mkPos 1 21) // "=" operator
    let preview = generatePreviewMutated source range "=" "<>"
    Assert.Contains("\x1b[1;31m<>\x1b[0m", preview)

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
        let parsed = parseSource source
        let candidates = findMutationCandidates fileFs parsed |> List.map enrich
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
        Assert.Contains("\x1b[1;31m", result.PreviewMutated)
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
