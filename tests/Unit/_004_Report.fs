module Tests._004_Report

open System
open System.IO
open Xunit
open FSharp.Compiler.Text
open Sabota.MutationTypes
open Sabota.MutationCandidates
open Sabota.Visitor
open Sabota.MuationDefinitions
open Sabota.Report
open TestApp

// --- US-006: Link to surviving mutation location ---

[<Fact>]
let ``01. generateLink produces file URL with line number`` () =
    // Arrange
    let file = "SampleProject/Calculator.fs"
    let range = Range.mkRange file (Position.mkPos 42 13) (Position.mkPos 42 14)
    // Act
    generateLink file range
    // Assert
    |> TestApp.Assert_Contains "Calculator.fs"
    |> TestApp.Assert_Contains "L42"
    |> ignore

[<Fact>]
let ``02. generateLink uses StartLine from range`` () =
    // Arrange
    let file = "test.fs"
    let range = Range.mkRange file (Position.mkPos 5 1) (Position.mkPos 5 10)
    // Act
    generateLink file range
    // Assert
    |> TestApp.Assert_Contains "L5"
    |> ignore

// --- US-007: Original and mutated code previews ---

[<Fact>]
let ``03. generatePreviewOriginal shows current line with > prefix`` () =
    // Arrange
    let source = """
let x = 1
let flag = true
let y = 2
"""
    let range = Range.mkRange "test.fs" (Position.mkPos 3 11) (Position.mkPos 3 15) // "true" on line 3
    // Act
    generatePreviewOriginal source range
    // Assert
    |> TestApp.Assert_Contains "> let flag = true"
    |> ignore

[<Fact>]
let ``04. generatePreviewOriginal includes previous line`` () =
    // Arrange
    let source = """
let x = 1
let flag = true
let y = 2
"""
    let range = Range.mkRange "test.fs" (Position.mkPos 3 11) (Position.mkPos 3 15)
    // Act
    generatePreviewOriginal source range
    // Assert
    |> TestApp.Assert_Contains "let x = 1"
    |> ignore

[<Fact>]
let ``05. generatePreviewOriginal includes next line`` () =
    // Arrange
    let source = """
let x = 1
let flag = true
let y = 2
"""
    let range = Range.mkRange "test.fs" (Position.mkPos 3 11) (Position.mkPos 3 15)
    // Act
    generatePreviewOriginal source range
    // Assert
    |> TestApp.Assert_Contains "let y = 2"
    |> ignore

[<Fact>]
let ``06. generatePreviewOriginal omits previous line when on first line`` () =
    // Arrange
    let source = """
let flag = true
let y = 2
"""
    let range = Range.mkRange "test.fs" (Position.mkPos 2 11) (Position.mkPos 2 15)
    // Act
    generatePreviewOriginal source range
    // Assert
    |> TestApp.Assert_Contains "> let flag = true"
    |> TestApp.Assert_Contains "let y = 2"
    |> fun p ->
        let lines = p.Split('\n')
        Assert.Equal(2, lines.Length)
        p
    |> ignore

[<Fact>]
let ``07. generatePreviewOriginal omits next line when on last line`` () =
    // Arrange
    let source = """
let x = 1
let flag = true
"""
    let range = Range.mkRange "test.fs" (Position.mkPos 3 11) (Position.mkPos 3 15)
    // Act
    generatePreviewOriginal source range
    // Assert
    |> TestApp.Assert_Contains "let x = 1"
    |> TestApp.Assert_Contains "> let flag = true"
    |> ignore

[<Fact>]
let ``08. generatePreviewMutated highlights mutation in red bold`` () =
    // Arrange
    let source = "let flag = true"
    let range = Range.mkRange "test.fs" (Position.mkPos 1 11) (Position.mkPos 1 15)
    // Act
    generatePreviewMutated source range "true" "false"
    // Assert
    |> TestApp.Assert_Contains "<span class=\"mutation-highlight\">"
    |> TestApp.Assert_Contains "</span>"
    |> TestApp.Assert_Contains "false"
    |> ignore

[<Fact>]
let ``09. generatePreviewMutated replaces original with mutated token`` () =
    // Arrange
    let source = "let isEqual x y = x = y"
    let range = Range.mkRange "test.fs" (Position.mkPos 1 20) (Position.mkPos 1 21) // "=" operator
    // Act
    generatePreviewMutated source range "=" "<>"
    // Assert
    |> TestApp.Assert_Contains "<span class=\"mutation-highlight\"><>"
    |> TestApp.Assert_Contains "</span>"
    |> ignore

[<Fact>]
let ``10. generatePreviewMutated includes surrounding lines`` () =
    // Arrange
    let source = """
let x = 1
let flag = true
let y = 2
"""
    let range = Range.mkRange "test.fs" (Position.mkPos 3 11) (Position.mkPos 3 15)
    // Act
    generatePreviewMutated source range "true" "false"
    // Assert
    |> TestApp.Assert_Contains "let x = 1"
    |> TestApp.Assert_Contains "let y = 2"
    |> ignore

// --- US-008: Summary of mutation results ---

[<Fact>]
let ``11. MutationResult contains all required fields`` () =
    // Arrange
    let source = "let flag = true"
    let range = Range.mkRange "test.fs" (Position.mkPos 1 11) (Position.mkPos 1 15)
    let outcome = { Status = Killed; Interpretation = "Test detected change." }
    // Act
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
    // Assert
    |> TestApp.Assert_Result_MutationId 1
    |> TestApp.Assert_Result_MutationType TrueToFalse
    |> TestApp.Assert_Result_File "test.fs"
    |> TestApp.Assert_Result_OutcomeStatus Killed
    |> TestApp.Assert_Result_PreviewOriginal_NonEmpty
    |> TestApp.Assert_Result_PreviewMutated_NonEmpty
    |> TestApp.Assert_Result_Link_NonEmpty
    |> ignore

[<Fact>]
let ``12. full pipeline generates MutationResult with previews`` () =
    // Arrange
    let source = """
    module TestModule
    let x = 1
    let flag = true
    let y = 2
    """
    let tempDir = TestApp.CreateSimulatedBaseDir()
    let fileFs = TestApp.CreateFileWithContent(tempDir, "test.fs", source)

    try
        // Act
        let parsed = TestApp.parseSource source
        let candidates = findMutationCandidates Stub.AllMutationCategories fileFs parsed |> List.map enrich
        let candidate = candidates |> List.find (fun c -> c.Kind = MutationKind.TrueToFalse)
        let outcome = { Status = Survived; Interpretation = "Tests did not detect the change." }
        
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
        // Assert
        |> TestApp.Assert_Result_PreviewOriginal_NonEmpty
        |> TestApp.Assert_Result_PreviewMutated_NonEmpty
        |> TestApp.Assert_Result_Link_NonEmpty
        |> TestApp.Assert_Result_Link_Contains "file://"
        |> TestApp.Assert_Result_PreviewOriginal_Contains "true"
        |> TestApp.Assert_Result_PreviewMutated_Contains "<span class=\"mutation-highlight\">"
        |> ignore
    finally
        TestApp.CleanSimulatedEnvironment tempDir

[<Fact>]
let ``13. getLine returns empty string for out of bounds`` () =
    // Arrange
    let source = """
line1
line2
"""
    // Act & Assert
    Assert.Equal("", getLine source 0)
    Assert.Equal("", getLine source 4)
    Assert.Equal("", getLine source -1)

[<Fact>]
let ``14. getLine returns correct line for valid index`` () =
    // Arrange
    let source = """
line1
line2
line3
"""
    // Act & Assert
    Assert.Equal("line1", getLine source 2)
    Assert.Equal("line2", getLine source 3)
    Assert.Equal("line3", getLine source 4)

// --- ControlFlow preview tests ---

[<Fact>]
let ``15. generatePreviewMutated wraps ControlFlow condition with not`` () =
    // Arrange
    // The F# parser includes the leading space in the condition's range
    // "if x > 0 then"
    //    12345678901234567890
    // Range covers " x > 0" (columns 4-9, 1-based: start=4, end=9 exclusive)
    let source = "if x > 0 then 1 else 0"
    let range = Range.mkRange "test.fs" (Position.mkPos 1 4) (Position.mkPos 1 9)
    let redBold = "<span class=\"mutation-highlight\">"
    let reset = "</span>"
    // Act
    generatePreviewMutated source range "if condition then" "if not condition then"
    // Assert
    // Should contain " not (x > 0)" with only "not" highlighted in red span
    |> TestApp.Assert_Contains $"{redBold}not{reset}"
    // The condition should NOT be highlighted
    |> TestApp.Assert_NotContains $"{redBold}not ({redBold}"
    |> TestApp.Assert_NotContains "ifnot"
    |> TestApp.Assert_NotContains "if not condition then"
    |> ignore

[<Fact>]
let ``16. generatePreviewMutated removes not from negated ControlFlow condition`` () =
    // Arrange
    // "if not (x > 0) then 1 else 0"
    //    1234567890123456789
    // The F# parser includes the leading space in the range
    // Range covers " not (x > 0)" (columns 4-15, 1-based)
    let source = "if not (x > 0) then 1 else 0"
    let range = Range.mkRange "test.fs" (Position.mkPos 1 4) (Position.mkPos 1 15)
    let redBold = "<span class=\"mutation-highlight\">"
    let reset = "</span>"
    // Act
    generatePreviewMutated source range "if not condition then" "if condition then"
    // Assert
    // Should contain " x > 0" highlighted in red span (the condition where not was removed)
    |> TestApp.Assert_Contains $"{redBold}x > 0{reset}"
    |> TestApp.Assert_NotContains "if condition then"
    |> ignore

[<Fact>]
let ``17. ControlFlow mutation range includes leading space`` () =
    // Arrange
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
    let candidates = findMutationCandidates Stub.AllMutationCategories "test.fs" parsed |> List.map enrich

    // Act
    let conditionToNot = candidates |> List.filter (fun c -> c.Kind = MutationKind.ConditionToNotCondition)
    Assert.Single conditionToNot |> ignore

    let candidate = conditionToNot.[0]
    let range = candidate.Range
    
    // Assert
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
    let redBold = "<span class=\"mutation-highlight\">"
    let reset = "</span>"
    generatePreviewMutated source range candidate.Original candidate.Replacement
    |> TestApp.Assert_NotContains "ifnot"
    |> TestApp.Assert_Contains $"{redBold}not{reset}"
    |> ignore
