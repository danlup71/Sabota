module Tests._008_MutationDiscovery

open System.IO
open Sabota.MutationTypes
open Sabota.Mutator
open Xunit
open Sabota.MutationCandidates
open TestApp

// --- discoverMutationsForFile tests ---

[<Fact>]
let ``01. when file contains boolean literals, discovery returns matching candidates`` () =
    // Arrange
    let baseDir = TestApp.CreateSimulatedBaseDir()
    let source = "let flag = true\nlet other = false"
    let filePath = TestApp.CreateFileWithContent(baseDir, Stub.TestFsName, source)

    try
        // Act
        let candidates = discoverMutationsForFile filePath Stub.AllMutationCategories

        // Assert
        candidates
        |> TestApp.Assert_Candidates_Count 2
        |> TestApp.Assert_MutationKind_Has MutationKind.TrueToFalse
        |> TestApp.Assert_MutationKind_Has MutationKind.FalseToTrue
        |> ignore
    finally
        TestApp.CleanSimulatedEnvironment baseDir

[<Fact>]
let ``02. when file has no mutatable code, discovery returns empty list`` () =
    // Arrange
    let baseDir = TestApp.CreateSimulatedBaseDir()
    let source = "let x = 42\nlet y = \"hello\""
    let filePath = TestApp.CreateFileWithContent(baseDir, Stub.TestFsName, source)

    try
        // Act
        let candidates = discoverMutationsForFile filePath Stub.AllMutationCategories

        // Assert
        candidates
        |> TestApp.Assert_NoCandidates
        |> ignore
    finally
        TestApp.CleanSimulatedEnvironment baseDir

[<Fact>]
let ``03. when file contains multiple mutation types, all are discovered`` () =
    // Arrange
    let baseDir = TestApp.CreateSimulatedBaseDir()
    let source = """
    let isEqual x y = x = y
    let flag = true
    let add a b = a + b
    """
    let filePath = TestApp.CreateFileWithContent(baseDir, Stub.TestFsName, source)

    try
        // Act
        let candidates = discoverMutationsForFile filePath Stub.AllMutationCategories

        // Assert
        candidates
        |> TestApp.Assert_MutationKind_Has MutationKind.EqualityToInequality
        |> TestApp.Assert_MutationKind_Has MutationKind.TrueToFalse
        |> TestApp.Assert_MutationKind_Has MutationKind.PlusToMinus
        |> ignore
    finally
        TestApp.CleanSimulatedEnvironment baseDir

[<Fact>]
let ``04. when discovery is limited to one category, only that category is returned`` () =
    // Arrange
    let baseDir = TestApp.CreateSimulatedBaseDir()
    let source = """
    let isEqual x y = x = y
    let flag = true
    """
    let filePath = TestApp.CreateFileWithContent(baseDir, Stub.TestFsName, source)
    let categories = [ MutationCategory.Equality ]

    try
        // Act
        let candidates = discoverMutationsForFile filePath categories

        // Assert
        candidates
        |> TestApp.Assert_MutationKind_Has MutationKind.EqualityToInequality
        |> fun c ->
            let hasNonEquality = c |> List.exists (fun cand -> cand.Kind <> MutationKind.EqualityToInequality)
            Assert.False(hasNonEquality, "Expected only Equality category mutations")
            c
        |> ignore
    finally
        TestApp.CleanSimulatedEnvironment baseDir

[<Fact>]
let ``05. when discovered candidates are enriched, original and replacement are set`` () =
    // Arrange
    let baseDir = TestApp.CreateSimulatedBaseDir()
    let source = "let flag = true"
    let filePath = TestApp.CreateFileWithContent(baseDir, Stub.TestFsName, source)

    try
        // Act
        let candidates = discoverMutationsForFile filePath Stub.AllMutationCategories

        // Assert
        let candidate = candidates |> List.find (fun c -> c.Kind = MutationKind.TrueToFalse)
        candidate
        |> TestApp.Assert_Enriched_Original "true"
        |> TestApp.Assert_Enriched_Replacement "false"
        |> ignore
    finally
        TestApp.CleanSimulatedEnvironment baseDir

[<Fact>]
let ``06. when discovered candidates include file path, it matches the input file`` () =
    // Arrange
    let baseDir = TestApp.CreateSimulatedBaseDir()
    let source = "let flag = true"
    let filePath = TestApp.CreateFileWithContent(baseDir, Stub.CustomFsName, source)

    try
        // Act
        let candidates = discoverMutationsForFile filePath Stub.AllMutationCategories

        // Assert
        candidates
        |> TestApp.Assert_AllCandidatesFile filePath
        |> ignore
    finally
        TestApp.CleanSimulatedEnvironment baseDir

// --- discoverAllMutations tests ---

[<Fact>]
let ``07. when multiple files are scanned, summary reports correct file count`` () =
    // Arrange
    let baseDir = TestApp.CreateSimulatedBaseDir()
    let file1 = TestApp.CreateFileWithContent(baseDir, Stub.SourceFile1Name, "let a = true")
    let file2 = TestApp.CreateFileWithContent(baseDir, Stub.SourceFile2Name, "let b = false")
    let files = [ file1; file2 ]

    try
        // Act
        let summary = discoverAllMutations files Stub.AllMutationCategories

        // Assert
        summary
        |> TestApp.Assert_Discovery_TotalFiles 2
        |> ignore
    finally
        TestApp.CleanSimulatedEnvironment baseDir

[<Fact>]
let ``08. when multiple files are scanned, summary reports correct total mutations`` () =
    // Arrange
    let baseDir = TestApp.CreateSimulatedBaseDir()
    let file1 = TestApp.CreateFileWithContent(baseDir, Stub.SourceFile1Name, "let a = true")
    let file2 = TestApp.CreateFileWithContent(baseDir, Stub.SourceFile2Name, "let b = false")
    let files = [ file1; file2 ]

    try
        // Act
        let summary = discoverAllMutations files Stub.AllMutationCategories

        // Assert
        summary
        |> TestApp.Assert_Discovery_TotalMutations 2
        |> ignore
    finally
        TestApp.CleanSimulatedEnvironment baseDir

[<Fact>]
let ``09. when one file has no mutations, summary reflects only the mutations that exist`` () =
    // Arrange
    let baseDir = TestApp.CreateSimulatedBaseDir()
    let file1 = TestApp.CreateFileWithContent(baseDir, Stub.SourceFile1Name, "let a = true")
    let file2 = TestApp.CreateFileWithContent(baseDir, Stub.SourceFile2Name, "let x = 42")
    let files = [ file1; file2 ]

    try
        // Act
        let summary = discoverAllMutations files Stub.AllMutationCategories

        // Assert
        summary
        |> TestApp.Assert_Discovery_TotalFiles 2
        |> TestApp.Assert_Discovery_TotalMutations 1
        |> ignore
    finally
        TestApp.CleanSimulatedEnvironment baseDir

[<Fact>]
let ``10. when all files are empty of mutations, summary reports zero mutations`` () =
    // Arrange
    let baseDir = TestApp.CreateSimulatedBaseDir()
    let file1 = TestApp.CreateFileWithContent(baseDir, Stub.SourceFile1Name, "let x = 42")
    let file2 = TestApp.CreateFileWithContent(baseDir, Stub.SourceFile2Name, "let y = \"hello\"")
    let files = [ file1; file2 ]

    try
        // Act
        let summary = discoverAllMutations files Stub.AllMutationCategories

        // Assert
        summary
        |> TestApp.Assert_Discovery_TotalFiles 2
        |> TestApp.Assert_Discovery_TotalMutations 0
        |> ignore
    finally
        TestApp.CleanSimulatedEnvironment baseDir

[<Fact>]
let ``11. when files are scanned, per-file entries list each file with its mutation count`` () =
    // Arrange
    let baseDir = TestApp.CreateSimulatedBaseDir()
    let file1 = TestApp.CreateFileWithContent(baseDir, Stub.SourceFile1Name, "let a = true\nlet b = false")
    let file2 = TestApp.CreateFileWithContent(baseDir, Stub.SourceFile2Name, "let x = 42")
    let files = [ file1; file2 ]

    try
        // Act
        let summary = discoverAllMutations files Stub.AllMutationCategories

        // Assert
        summary
        |> TestApp.Assert_Discovery_FileExists file1
        |> TestApp.Assert_Discovery_FileExists file2
        |> fun s ->
            let entry1 = s.Files |> List.find (fun fs -> fs.File = file1)
            entry1 |> TestApp.Assert_FileMutation_Count 2 |> ignore
            let entry2 = s.Files |> List.find (fun fs -> fs.File = file2)
            entry2 |> TestApp.Assert_FileMutation_Count 0 |> ignore
            s
        |> ignore
    finally
        TestApp.CleanSimulatedEnvironment baseDir

[<Fact>]
let ``12. when empty file list is scanned, summary reports zero files and mutations`` () =
    // Act
    let summary = discoverAllMutations [] Stub.AllMutationCategories

    // Assert
    summary
    |> TestApp.Assert_Discovery_TotalFiles 0
    |> TestApp.Assert_Discovery_TotalMutations 0
    |> ignore

[<Fact>]
let ``13. when category filter is applied to multiple files, only matching mutations are counted`` () =
    // Arrange
    let baseDir = TestApp.CreateSimulatedBaseDir()
    let file1 = TestApp.CreateFileWithContent(baseDir, Stub.SourceFile1Name, "let a = true\nlet isEqual x y = x = y")
    let files = [ file1 ]
    let categories = [ MutationCategory.Boolean ]

    try
        // Act
        let summary = discoverAllMutations files categories

        // Assert
        summary
        |> TestApp.Assert_Discovery_TotalFiles 1
        |> TestApp.Assert_Discovery_TotalMutations 1
        |> ignore
    finally
        TestApp.CleanSimulatedEnvironment baseDir

[<Fact>]
let ``14. when file contains complex expressions, all mutation candidates are discovered`` () =
    // Arrange
    let baseDir = TestApp.CreateSimulatedBaseDir()
    let source = """
    module TestModule
    let check x y =
        if x > 0 && y <> 0 then
            Some (x + y)
        else
            None
    """
    let filePath = TestApp.CreateFileWithContent(baseDir, Stub.TestFsName, source)

    try
        // Act
        let candidates = discoverMutationsForFile filePath Stub.AllMutationCategories

        // Assert
        candidates
        |> TestApp.Assert_MutationKind_Has MutationKind.GreaterToGreaterOrEqual
        |> TestApp.Assert_MutationKind_Has MutationKind.InequalityToEquality
        |> TestApp.Assert_MutationKind_Has MutationKind.AndToOr
        |> TestApp.Assert_MutationKind_Has MutationKind.SomeToNone
        |> TestApp.Assert_MutationKind_Has MutationKind.PlusToMinus
        |> ignore
    finally
        TestApp.CleanSimulatedEnvironment baseDir
