module Tests._003_IndependentMutation

open System.IO
open Sabota.MutationTypes
open Sabota.Workspace
open Xunit
open Sabota.MutationCandidates
open Sabota.MuationDefinitions
open Sabota.Visitor
open Sabota.Files
open TestApp

// --- createWorkFolder tests ---

[<Fact>]
let ``01. when work folder is created, directory exists with run and sabota in path`` () =
    // Act
    let workFolder = createWorkFolder ()
    try
        // Assert
        workFolder
        |> TestApp.Assert_Directory_Exists
        |> TestApp.Assert_String_Contains "run-"
        |> TestApp.Assert_String_Contains "sabota"
    finally
        deleteWorkFolder workFolder

[<Fact>]
let ``02. when work folder is created, parent sabota directory is created`` () =
    // Arrange
    let sabotaDir = TestApp.GetSabotaDir()

    // Act
    let workFolder = createWorkFolder ()
    try
        // Assert
        sabotaDir |> TestApp.Assert_Directory_Exists |> ignore
        Assert.StartsWith(sabotaDir, workFolder)
    finally
        deleteWorkFolder workFolder

// --- copyProjectToWorkFolder tests ---

[<Fact>]
let ``03. when project is copied to work folder, source files are present`` () =
    // Arrange
    let baseDir = TestApp.CreateSimulatedBaseDir()
    let env = TestApp.CreateSimulatedEnvironment(Stub.TestProjectName, "", [Stub.SourceFile1Name], [], baseDir)
    // Override the source file content
    File.WriteAllText(env.FileFs, "let x = 1")

    // Act
    let workFolder = createWorkFolder ()
    try
        let workProject = copyProjectToWorkFolder env.TestProject workFolder
        let workDir = defaultArg (Option.ofObj (Path.GetDirectoryName workProject)) ""
        let workSourceFile = Path.Combine(workDir, Stub.SourceFile1Name)

        // Assert
        workProject |> TestApp.Assert_File_Exists |> ignore
        workSourceFile |> TestApp.Assert_File_Exists |> ignore
        workSourceFile |> TestApp.Assert_FileContent_Equal "let x = 1" |> ignore
    finally
        deleteWorkFolder workFolder
        TestApp.CleanSimulatedEnvironment baseDir

[<Fact>]
let ``04. when project is copied to work folder, file content is preserved`` () =
    // Arrange
    let baseDir = TestApp.CreateSimulatedBaseDir()
    let env = TestApp.CreateSimulatedEnvironment(Stub.TestProjectName, "", [Stub.SourceFile1Name], [], baseDir)
    let originalContent = "let a = true\nlet b = false"
    File.WriteAllText(env.FileFs, originalContent)

    // Act
    let workFolder = createWorkFolder ()
    try
        let workProject = copyProjectToWorkFolder env.TestProject workFolder
        let workDir = defaultArg (Option.ofObj (Path.GetDirectoryName workProject)) ""
        let workSourceFile = Path.Combine(workDir, Stub.SourceFile1Name)

        // Assert
        workSourceFile |> TestApp.Assert_FileContent_Equal originalContent |> ignore
    finally
        deleteWorkFolder workFolder
        TestApp.CleanSimulatedEnvironment baseDir

// --- mapFileToWorkFolder tests ---

[<Fact>]
let ``05. when source file path is mapped to work folder, filename is preserved`` () =
    // Arrange
    let sourceDir = @"C:\Projects\Sample1"
    let workDir = @"C:\Temp\sabota\run-123\Sample1"
    let originalFile = @"C:\Projects\Sample1\Source.fs"

    // Act
    mapFileToWorkFolder originalFile sourceDir workDir
    // Assert
    |> TestApp.Assert_String_Contains "Source.fs"

// --- deleteWorkFolder tests ---

[<Fact>]
let ``06. when work folder is deleted, directory is removed`` () =
    // Arrange
    let workFolder = createWorkFolder ()

    // Act
    deleteWorkFolder workFolder

    // Assert
    TestApp.Assert_Directory_NotExists workFolder

[<Fact>]
let ``07. when non-existent path is deleted, no error occurs`` () =
    // Act & Assert - should not throw
    deleteWorkFolder @"C:\NonExistent\Path"



// --- applyMutation tests ---

[<Fact>]
let ``08. when true to false mutation is applied, file contains false`` () =
    // Arrange
    let baseDir, filePath, enriched = TestApp.CreateMutationTestEnv("let flag = true", MutationKind.TrueToFalse, Stub.AllMutationCategories)

    try
        // Act
        applyMutation enriched

        // Assert
        filePath
        |> TestApp.Assert_FileContent_Contains "false"
        |> TestApp.Assert_FileContent_NotContains "true"
    finally
        TestApp.CleanSimulatedEnvironment baseDir

[<Fact>]
let ``09. when false to true mutation is applied, file contains true`` () =
    // Arrange
    let baseDir, filePath, enriched = TestApp.CreateMutationTestEnv("let flag = false", MutationKind.FalseToTrue, Stub.AllMutationCategories)

    try
        // Act
        applyMutation enriched

        // Assert
        filePath
        |> TestApp.Assert_FileContent_Contains "true"
        |> TestApp.Assert_FileContent_NotContains "false"
    finally
        TestApp.CleanSimulatedEnvironment baseDir

[<Fact>]
let ``10. when equality to inequality mutation is applied, operator changes`` () =
    // Arrange
    let baseDir, filePath, enriched = TestApp.CreateMutationTestEnv("let isEqual x y = x = y", MutationKind.EqualityToInequality, Stub.AllMutationCategories)

    try
        // Act
        applyMutation enriched

        // Assert
        filePath |> TestApp.Assert_FileContent_Contains "<>"
    finally
        TestApp.CleanSimulatedEnvironment baseDir

[<Fact>]
let ``11. when mutation is applied, surrounding code is preserved`` () =
    // Arrange
    let source = """
    module TestModule
    let a = 1
    let flag = true
    let b = 2
    """
    let baseDir, filePath, enriched = TestApp.CreateMutationTestEnv(source, MutationKind.TrueToFalse, Stub.AllMutationCategories)

    try
        // Act
        applyMutation enriched

        // Assert
        filePath
        |> TestApp.Assert_FileContent_Contains "let a = 1"
        |> TestApp.Assert_FileContent_Contains "let b = 2"
        |> TestApp.Assert_FileContent_Contains "module TestModule"
    finally
        TestApp.CleanSimulatedEnvironment baseDir

// --- Independent mutation execution tests ---

[<Fact>]
let ``12. when multiple mutations are applied with restore, each changes the file independently`` () =
    // Arrange
    let baseDir = TestApp.CreateSimulatedBaseDir()
    let env = TestApp.CreateSimulatedEnvironment(Stub.TestProjectName, "", [Stub.TestFsName], [], baseDir)
    let originalContent = "let a = true\nlet b = false"
    File.WriteAllText(env.FileFs, originalContent)

    let parsed = TestApp.parseSource originalContent
    let candidates = findMutationCandidates Stub.AllMutationCategories env.FileFs parsed |> List.map enrich

    // Act
    let workFolder = createWorkFolder ()
    try
        let workProject = copyProjectToWorkFolder env.TestProject workFolder
        let workDir = defaultArg (Option.ofObj (Path.GetDirectoryName workProject)) ""
        let workFile = mapFileToWorkFolder env.FileFs baseDir workDir

        for candidate in candidates do
            let workCandidate = { candidate with File = workFile }
            applyMutation workCandidate

            let mutatedContent = File.ReadAllText workFile
            Assert.NotEqual<string>(originalContent, mutatedContent)

            restoreFile env.FileFs workFile

        // Assert - work file restored to original
        workFile |> TestApp.Assert_FileContent_Equal originalContent |> ignore

        // Assert - original file untouched
        env.FileFs |> TestApp.Assert_FileContent_Equal originalContent |> ignore
    finally
        deleteWorkFolder workFolder
        TestApp.CleanSimulatedEnvironment baseDir

[<Fact>]
let ``13. when two mutations run sequentially with restore, they do not interfere`` () =
    // Arrange
    let baseDir = TestApp.CreateSimulatedBaseDir()
    let env = TestApp.CreateSimulatedEnvironment(Stub.TestProjectName, "", [Stub.TestFsName], [], baseDir)
    let originalContent = "let a = true\nlet b = false"
    File.WriteAllText(env.FileFs, originalContent)

    let parsed = TestApp.parseSource originalContent
    let candidates = findMutationCandidates Stub.AllMutationCategories env.FileFs parsed |> List.map enrich

    let trueToFalse = candidates |> List.find (fun c -> c.Kind = MutationKind.TrueToFalse)
    let falseToTrue = candidates |> List.find (fun c -> c.Kind = MutationKind.FalseToTrue)

    // Act
    let workFolder = createWorkFolder ()
    try
        let workProject = copyProjectToWorkFolder env.TestProject workFolder
        let workDir = defaultArg (Option.ofObj (Path.GetDirectoryName workProject)) ""
        let workFile = mapFileToWorkFolder env.FileFs baseDir workDir

        let workCandidate1 = { trueToFalse with File = workFile }
        applyMutation workCandidate1
        workFile |> TestApp.Assert_FileContent_Contains "false" |> ignore

        restoreFile env.FileFs workFile

        let workCandidate2 = { falseToTrue with File = workFile }
        applyMutation workCandidate2
        workFile |> TestApp.Assert_FileContent_Contains "true" |> ignore

        // Assert - original file untouched
        env.FileFs |> TestApp.Assert_FileContent_Equal originalContent |> ignore
    finally
        deleteWorkFolder workFolder
        TestApp.CleanSimulatedEnvironment baseDir

[<Fact>]
let ``14. when full mutation cycle completes, original file is preserved`` () =
    // Arrange
    let baseDir = TestApp.CreateSimulatedBaseDir()
    let env = TestApp.CreateSimulatedEnvironment(Stub.TestProjectName, "", [Stub.TestFsName], [], baseDir)
    let originalContent = """
    module TestModule
    let isEqual x y = x = y
    let flag = true
    let check x =
        if x <> 0 then
            true
        else
            false
    """
    File.WriteAllText(env.FileFs, originalContent)

    let parsed = TestApp.parseSource originalContent
    let candidates = findMutationCandidates Stub.AllMutationCategories env.FileFs parsed |> List.map enrich

    // Act
    let workFolder = createWorkFolder ()
    try
        let workProject = copyProjectToWorkFolder env.TestProject workFolder
        let workDir = defaultArg (Option.ofObj (Path.GetDirectoryName workProject)) ""
        let workFile = mapFileToWorkFolder env.FileFs baseDir workDir

        let mutationResults =
            candidates |> List.map (fun candidate ->
                let workCandidate = { candidate with File = workFile }
                applyMutation workCandidate
                let mutatedContent = File.ReadAllText workFile
                restoreFile env.FileFs workFile
                (candidate.Kind, mutatedContent)
            )

        // Assert - work file restored
        workFile |> TestApp.Assert_FileContent_Equal originalContent |> ignore

        // Assert - original file untouched
        env.FileFs |> TestApp.Assert_FileContent_Equal originalContent |> ignore

        Assert.Equal(candidates.Length, mutationResults.Length)
        for _kind, mutatedContent in mutationResults do
            Assert.NotEqual<string>(originalContent, mutatedContent)
    finally
        deleteWorkFolder workFolder
        TestApp.CleanSimulatedEnvironment baseDir

[<Fact>]
let ``15. when each mutation is applied, only the target line changes`` () =
    // Arrange
    let baseDir = TestApp.CreateSimulatedBaseDir()
    let env = TestApp.CreateSimulatedEnvironment(Stub.TestProjectName, "", [Stub.TestFsName], [], baseDir)
    let originalContent = "let a = true\nlet b = 1 = 2"
    File.WriteAllText(env.FileFs, originalContent)

    let parsed = TestApp.parseSource originalContent
    let candidates = findMutationCandidates Stub.AllMutationCategories env.FileFs parsed |> List.map enrich

    // Act
    let workFolder = createWorkFolder ()
    try
        let workProject = copyProjectToWorkFolder env.TestProject workFolder
        let workDir = defaultArg (Option.ofObj (Path.GetDirectoryName workProject)) ""
        let workFile = mapFileToWorkFolder env.FileFs baseDir workDir

        for candidate in candidates do
            let workCandidate = { candidate with File = workFile }
            applyMutation workCandidate
            let mutatedContent = File.ReadAllText workFile

            let originalLines = originalContent.Split('\n')
            let mutatedLines = mutatedContent.Split('\n')
            let changedLines =
                originalLines
                |> List.ofArray
                |> List.mapi (fun i line -> (i, line))
                |> List.filter (fun (i, line) ->
                    i < mutatedLines.Length && mutatedLines[i] <> line)

            Assert.Single changedLines |> ignore

            restoreFile env.FileFs workFile

        // Assert - work file restored
        workFile |> TestApp.Assert_FileContent_Equal originalContent |> ignore

        // Assert - original file untouched
        env.FileFs |> TestApp.Assert_FileContent_Equal originalContent |> ignore
    finally
        deleteWorkFolder workFolder
        TestApp.CleanSimulatedEnvironment baseDir

[<Fact>]
let ``16. when multiple files are copied to work folder, each maintains distinct content`` () =
    // Arrange
    let baseDir = TestApp.CreateSimulatedBaseDir()
    let env = TestApp.CreateSimulatedEnvironment(Stub.TestProjectName, "", [Stub.SourceFile1Name; Stub.SourceFile2Name], [], baseDir)
    let content1 = "let a = true"
    let content2 = "let b = false"
    File.WriteAllText(env.Files[0], content1)
    File.WriteAllText(env.Files[1], content2)

    // Act
    let workFolder = createWorkFolder ()
    try
        let workProject = copyProjectToWorkFolder env.TestProject workFolder
        let workDir = defaultArg (Option.ofObj (Path.GetDirectoryName workProject)) ""

        let workFile1 = mapFileToWorkFolder env.Files[0] baseDir workDir
        let workFile2 = mapFileToWorkFolder env.Files[1] baseDir workDir

        // Assert - each file has correct content
        workFile1 |> TestApp.Assert_FileContent_Equal content1 |> ignore
        workFile2 |> TestApp.Assert_FileContent_Equal content2 |> ignore

        // Assert - work files are distinct paths
        Assert.True(workFile1 <> workFile2)

        // Assert - original files untouched
        env.Files[0] |> TestApp.Assert_FileContent_Equal content1 |> ignore
        env.Files[1] |> TestApp.Assert_FileContent_Equal content2 |> ignore
    finally
        deleteWorkFolder workFolder
        TestApp.CleanSimulatedEnvironment baseDir

[<Fact>]
let ``17. when candidate is enriched, original and replacement are set from mutation kind`` () =
    // Arrange
    let baseDir, _filePath, enriched = TestApp.CreateMutationTestEnv("let flag = true", MutationKind.TrueToFalse, Stub.AllMutationCategories)

    try
        // Assert
        enriched
        |> TestApp.Assert_Enriched_Original "true"
        |> TestApp.Assert_Enriched_Replacement "false"
    finally
        TestApp.CleanSimulatedEnvironment baseDir

[<Fact>]
let ``18. when candidate is enriched, metadata fields are preserved`` () =
    // Arrange
    let baseDir = TestApp.CreateSimulatedBaseDir()
    let filePath = Path.Combine(baseDir, Stub.CustomFsName)
    let source = "let isEqual x y = x = y"
    File.WriteAllText(filePath, source)

    try
        let parsed = TestApp.parseSource source
        let candidates = findMutationCandidates Stub.AllMutationCategories filePath parsed
        let candidate = candidates |> List.find (fun c -> c.Kind = MutationKind.EqualityToInequality)

        // Act
        let enriched = enrich candidate

        // Assert
        enriched |> TestApp.Assert_Enriched_Metadata_Preserved candidate
    finally
        TestApp.CleanSimulatedEnvironment baseDir

// --- ReturnListValueToEmpty mutation tests ---

[<Fact>]
let ``19. when ReturnListValueToEmpty mutation is applied, leading identifier is preserved`` () =
    // Regression test: the F# compiler range for ArrayOrListComputed in indexed access
    // may include a leading character before '[', causing the mutation to trim part of
    // the preceding identifier (e.g., "returnCollection[0]" -> "returnCollectio[]").
    // Arrange
    let baseDir = TestApp.CreateSimulatedBaseDir()
    let filePath = Path.Combine(baseDir, Stub.TestFsName)
    let source = """
    module TestModule
    let returnCollection = ["item1"; "item2"]
    let part = Some returnCollection[0]
    """
    File.WriteAllText(filePath, source)

    let categories = [ MutationCategory.ReturnListValue ]

    try
        let parsed = TestApp.parseSource source
        let candidates = findMutationCandidates categories filePath parsed
        let listToEmpty = candidates |> List.filter (fun c -> c.Kind = MutationKind.ReturnListValueToEmpty)

        // Assert - at least one candidate exists
        Assert.True(listToEmpty.Length >= 1, "Expected at least one ReturnListValueToEmpty candidate")

        // Act & Assert - apply each and verify identifier is preserved
        for candidate in listToEmpty do
            let enriched = enrich candidate
            applyMutation enriched
            filePath |> TestApp.Assert_FileContent_NotContains "returnCollectio[]" |> ignore
            File.WriteAllText(filePath, source)
    finally
        TestApp.CleanSimulatedEnvironment baseDir

[<Fact>]
let ``20. when ReturnListValueToEmpty mutation is applied on multiline source, leading identifier is preserved`` () =
    // Regression test: when the F# compiler range for ArrayOrListComputed spans
    // multiple lines, the mutation should still preserve the leading identifier
    // before the '[' bracket.
    // Arrange
    let baseDir = TestApp.CreateSimulatedBaseDir()
    let filePath = Path.Combine(baseDir, Stub.TestFsName)
    let source = """
    module TestModule
    let returnCollection = [
        "item1"
        "item2"
    ]
    let part = Some returnCollection[0]
    """
    File.WriteAllText(filePath, source)

    let categories = [ MutationCategory.ReturnListValue ]

    try
        let parsed = TestApp.parseSource source
        let candidates = findMutationCandidates categories filePath parsed
        let listToEmpty = candidates |> List.filter (fun c -> c.Kind = MutationKind.ReturnListValueToEmpty)

        // Assert - at least one candidate exists
        Assert.True(listToEmpty.Length >= 1, "Expected at least one ReturnListValueToEmpty candidate")

        // Act & Assert - apply each and verify identifier is preserved
        for candidate in listToEmpty do
            let enriched = enrich candidate
            applyMutation enriched
            filePath |> TestApp.Assert_FileContent_NotContains "returnCollectio[]" |> ignore
            File.WriteAllText(filePath, source)
    finally
        TestApp.CleanSimulatedEnvironment baseDir
