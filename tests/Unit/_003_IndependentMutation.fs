module Tests._003_IndependentMutation

open System
open System.IO
open Sabota.MutationTypes
open Xunit
open FSharp.Compiler.CodeAnalysis
open FSharp.Compiler.Text
open Sabota.MutationCandidates
open FSharp.Compiler.Syntax
open Sabota.MuationDefinitions
open Sabota.Visitor
open Sabota.Files

let mutationCategories = [ MutationCategory.Equality
                           MutationCategory.RelationalBoundary
                           MutationCategory.RelationalReversal
                           MutationCategory.Boolean
                           MutationCategory.Arithmetic
                           ]

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

// --- createWorkFolder tests ---

[<Fact>]
let ``createWorkFolder creates unique directory`` () =
    let workFolder = createWorkFolder ()
    try
        Assert.True(Directory.Exists workFolder)
        Assert.Contains("run-", workFolder)
        Assert.Contains("sabota", workFolder)
    finally
        if Directory.Exists workFolder then
            Directory.Delete(workFolder, true)

[<Fact>]
let ``createWorkFolder creates sabota parent directory`` () =
    let tempDir = Environment.GetEnvironmentVariable("TEMP")
    let sabotaDir = Path.Combine(tempDir, "sabota")
    
    let workFolder = createWorkFolder ()
    try
        Assert.True(Directory.Exists sabotaDir)
        Assert.StartsWith(sabotaDir, workFolder)
    finally
        // Only delete our own work folder, not the shared sabota directory
        if Directory.Exists workFolder then
            Directory.Delete(workFolder, true)

// --- copyProjectToWorkFolder tests ---

[<Fact>]
let ``copyProjectToWorkFolder copies project files`` () =
    let tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString())
    Directory.CreateDirectory(tempDir) |> ignore
    
    let projectFile = Path.Combine(tempDir, "Test.fsproj")
    File.WriteAllText(projectFile, "<Project></Project>")
    let sourceFile = Path.Combine(tempDir, "Source.fs")
    File.WriteAllText(sourceFile, "let x = 1")

    try
        let workFolder = createWorkFolder ()
        try
            let workProject = copyProjectToWorkFolder projectFile workFolder
            
            Assert.True(File.Exists workProject)
            Assert.True(File.Exists (Path.Combine(Path.GetDirectoryName workProject, "Source.fs")))
            
            let content = File.ReadAllText (Path.Combine(Path.GetDirectoryName workProject, "Source.fs"))
            Assert.Equal("let x = 1", content)
        finally
            deleteWorkFolder workFolder
    finally
        Directory.Delete(tempDir, true)

[<Fact>]
let ``copyProjectToWorkFolder preserves file content`` () =
    let tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString())
    Directory.CreateDirectory(tempDir) |> ignore
    
    let projectFile = Path.Combine(tempDir, "Test.fsproj")
    File.WriteAllText(projectFile, "<Project></Project>")
    let sourceFile = Path.Combine(tempDir, "Source.fs")
    let originalContent = "let a = true\nlet b = false"
    File.WriteAllText(sourceFile, originalContent)

    try
        let workFolder = createWorkFolder ()
        try
            let workProject = copyProjectToWorkFolder projectFile workFolder
            
            let workSourceDir = Path.GetDirectoryName workProject
            let workSourceFile = Path.Combine(workSourceDir, "Source.fs")
            let copiedContent = File.ReadAllText workSourceFile
            
            Assert.Equal(originalContent, copiedContent)
        finally
            deleteWorkFolder workFolder
    finally
        Directory.Delete(tempDir, true)

// --- mapFileToWorkFolder tests ---

[<Fact>]
let ``mapFileToWorkFolder maps file correctly`` () =
    let sourceDir = @"C:\Projects\Sample1"
    let workDir = @"C:\Temp\sabota\run-123\Sample1"
    let originalFile = @"C:\Projects\Sample1\Source.fs"
    
    let mappedFile = mapFileToWorkFolder originalFile sourceDir workDir
    
    Assert.Contains("Source.fs", mappedFile)

// --- deleteWorkFolder tests ---

[<Fact>]
let ``deleteWorkFolder removes directory`` () =
    let workFolder = createWorkFolder ()
    Assert.True(Directory.Exists workFolder)
    
    deleteWorkFolder workFolder
    Assert.False(Directory.Exists workFolder)

[<Fact>]
let ``deleteWorkFolder handles non-existent directory`` () =
    deleteWorkFolder @"C:\NonExistent\Path"

// --- applyMutation tests ---

[<Fact>]
let ``applyMutation replaces true with false`` () =
    let tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString())
    Directory.CreateDirectory(tempDir) |> ignore
    let fileFs = Path.Combine(tempDir, "test.fs")
    File.WriteAllText(fileFs, "let flag = true")

    try
        let parsed = parseSource "let flag = true"
        let candidates = findMutationCandidates mutationCategories fileFs parsed
        let candidate = candidates |> List.find (fun c -> c.Kind = MutationKind.TrueToFalse)
        let enriched = enrich candidate

        applyMutation enriched
        let mutatedContent = File.ReadAllText(fileFs)
        Assert.Contains("false", mutatedContent)
        Assert.DoesNotContain("true", mutatedContent)
    finally
        Directory.Delete(tempDir, true)

[<Fact>]
let ``applyMutation replaces false with true`` () =
    let tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString())
    Directory.CreateDirectory(tempDir) |> ignore
    let fileFs = Path.Combine(tempDir, "test.fs")
    File.WriteAllText(fileFs, "let flag = false")

    try
        let parsed = parseSource "let flag = false"
        let candidates = findMutationCandidates mutationCategories fileFs parsed
        let candidate = candidates |> List.find (fun c -> c.Kind = MutationKind.FalseToTrue)
        let enriched = enrich candidate

        applyMutation enriched
        let mutatedContent = File.ReadAllText(fileFs)
        Assert.Contains("true", mutatedContent)
        Assert.DoesNotContain("false", mutatedContent)
    finally
        Directory.Delete(tempDir, true)

[<Fact>]
let ``applyMutation replaces = with <>`` () =
    let tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString())
    Directory.CreateDirectory(tempDir) |> ignore
    let fileFs = Path.Combine(tempDir, "test.fs")
    File.WriteAllText(fileFs, "let isEqual x y = x = y")

    try
        let parsed = parseSource "let isEqual x y = x = y"
        let candidates = findMutationCandidates mutationCategories fileFs parsed
        let candidate = candidates |> List.find (fun c -> c.Kind = MutationKind.EqualityToInequality)
        let enriched = enrich candidate

        applyMutation enriched
        let mutatedContent = File.ReadAllText(fileFs)
        Assert.Contains("<>", mutatedContent)
    finally
        Directory.Delete(tempDir, true)

[<Fact>]
let ``applyMutation preserves surrounding code`` () =
    let tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString())
    Directory.CreateDirectory(tempDir) |> ignore
    let fileFs = Path.Combine(tempDir, "test.fs")
    let content = """
    module TestModule
    let a = 1
    let flag = true
    let b = 2
    """
    File.WriteAllText(fileFs, content)

    try
        let parsed = parseSource content
        let candidates = findMutationCandidates mutationCategories fileFs parsed
        let candidate = candidates |> List.find (fun c -> c.Kind = MutationKind.TrueToFalse)
        let enriched = enrich candidate

        applyMutation enriched
        let mutatedContent = File.ReadAllText(fileFs)
        Assert.Contains("let a = 1", mutatedContent)
        Assert.Contains("let b = 2", mutatedContent)
        Assert.Contains("module TestModule", mutatedContent)
    finally
        Directory.Delete(tempDir, true)

// --- Independent mutation execution tests ---

[<Fact>]
let ``each mutation is applied independently with file restore`` () =
    let tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString())
    Directory.CreateDirectory(tempDir) |> ignore
    let projectFile = Path.Combine(tempDir, "Test.fsproj")
    File.WriteAllText(projectFile, "<Project></Project>")
    let fileFs = Path.Combine(tempDir, "test.fs")
    let originalContent = "let a = true\nlet b = false"
    File.WriteAllText(fileFs, originalContent)

    try
        let parsed = parseSource originalContent
        let candidates = findMutationCandidates mutationCategories fileFs parsed |> List.map enrich

        // Single work folder for all mutations
        let workFolder = createWorkFolder ()
        try
            let workProject = copyProjectToWorkFolder projectFile workFolder
            let workDir = Path.GetDirectoryName workProject
            let workFile = mapFileToWorkFolder fileFs tempDir workDir

            for candidate in candidates do
                let workCandidate = { candidate with File = workFile }
                applyMutation workCandidate
                
                let mutatedContent = File.ReadAllText workFile
                Assert.NotEqual<string>(originalContent, mutatedContent)

                // Restore original for next mutation
                restoreFile fileFs workFile

            // After all mutations and restores, work file should match original
            let restoredContent = File.ReadAllText workFile
            Assert.Equal(originalContent, restoredContent)
        finally
            deleteWorkFolder workFolder

        let finalContent = File.ReadAllText(fileFs)
        Assert.Equal(originalContent, finalContent)
    finally
        Directory.Delete(tempDir, true)

[<Fact>]
let ``mutations do not interfere with each other using file restore`` () =
    let tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString())
    Directory.CreateDirectory(tempDir) |> ignore
    let projectFile = Path.Combine(tempDir, "Test.fsproj")
    File.WriteAllText(projectFile, "<Project></Project>")
    let fileFs = Path.Combine(tempDir, "test.fs")
    let originalContent = "let a = true\nlet b = false"
    File.WriteAllText(fileFs, originalContent)

    try
        let parsed = parseSource originalContent
        let candidates = findMutationCandidates mutationCategories fileFs parsed |> List.map enrich

        let trueToFalse = candidates |> List.find (fun c -> c.Kind = MutationKind.TrueToFalse)
        let falseToTrue = candidates |> List.find (fun c -> c.Kind = MutationKind.FalseToTrue)

        // Single work folder shared by both mutations
        let workFolder = createWorkFolder ()
        try
            let workProject = copyProjectToWorkFolder projectFile workFolder
            let workDir = Path.GetDirectoryName workProject
            let workFile = mapFileToWorkFolder fileFs tempDir workDir

            // First mutation: true -> false
            let workCandidate1 = { trueToFalse with File = workFile }
            applyMutation workCandidate1
            let content1 = File.ReadAllText workFile
            Assert.Contains("false", content1)
            
            // Restore original
            restoreFile fileFs workFile

            // Second mutation: false -> true (on restored original)
            let workCandidate2 = { falseToTrue with File = workFile }
            applyMutation workCandidate2
            let content2 = File.ReadAllText workFile
            Assert.Contains("true", content2)
        finally
            deleteWorkFolder workFolder

        let finalContent = File.ReadAllText(fileFs)
        Assert.Equal(originalContent, finalContent)
    finally
        Directory.Delete(tempDir, true)

[<Fact>]
let ``full cycle preserves original file across multiple mutations`` () =
    let tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString())
    Directory.CreateDirectory(tempDir) |> ignore
    let projectFile = Path.Combine(tempDir, "Test.fsproj")
    File.WriteAllText(projectFile, "<Project></Project>")
    let fileFs = Path.Combine(tempDir, "test.fs")
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
    File.WriteAllText(fileFs, originalContent)

    try
        let parsed = parseSource originalContent
        let candidates = findMutationCandidates mutationCategories fileFs parsed |> List.map enrich

        // Single work folder for all mutations
        let workFolder = createWorkFolder ()
        try
            let workProject = copyProjectToWorkFolder projectFile workFolder
            let workDir = Path.GetDirectoryName workProject
            let workFile = mapFileToWorkFolder fileFs tempDir workDir

            let mutationResults =
                candidates |> List.map (fun candidate ->
                    let workCandidate = { candidate with File = workFile }
                    applyMutation workCandidate
                    let mutatedContent = File.ReadAllText workFile
                    restoreFile fileFs workFile
                    (candidate.Kind, mutatedContent)
                )

            // After all mutations and restores, work file should match original
            let restoredContent = File.ReadAllText workFile
            Assert.Equal(originalContent, restoredContent)

            let finalContent = File.ReadAllText(fileFs)
            Assert.Equal(originalContent, finalContent)

            Assert.Equal(candidates.Length, mutationResults.Length)
            for (kind, mutatedContent) in mutationResults do
                Assert.NotEqual<string>(originalContent, mutatedContent)
        finally
            deleteWorkFolder workFolder
    finally
        Directory.Delete(tempDir, true)

[<Fact>]
let ``each mutation only changes its target range with file restore`` () =
    let tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString())
    Directory.CreateDirectory(tempDir) |> ignore
    let projectFile = Path.Combine(tempDir, "Test.fsproj")
    File.WriteAllText(projectFile, "<Project></Project>")
    let fileFs = Path.Combine(tempDir, "test.fs")
    let originalContent = "let a = true\nlet b = 1 = 2"
    File.WriteAllText(fileFs, originalContent)

    try
        let parsed = parseSource originalContent
        let candidates = findMutationCandidates mutationCategories fileFs parsed |> List.map enrich

        // Single work folder for all mutations
        let workFolder = createWorkFolder ()
        try
            let workProject = copyProjectToWorkFolder projectFile workFolder
            let workDir = Path.GetDirectoryName workProject
            let workFile = mapFileToWorkFolder fileFs tempDir workDir

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
                        i < mutatedLines.Length && mutatedLines.[i] <> line)

                Assert.Single changedLines

                // Restore original for next mutation
                restoreFile fileFs workFile

            // After all mutations and restores, work file should match original
            let restoredContent = File.ReadAllText workFile
            Assert.Equal(originalContent, restoredContent)
        finally
            deleteWorkFolder workFolder

        let finalContent = File.ReadAllText(fileFs)
        Assert.Equal(originalContent, finalContent)
    finally
        Directory.Delete(tempDir, true)

[<Fact>]
let ``multiple files share single work folder without path confusion`` () =
    // Regression test: List.collect (processFile workSolutionDir workTestPrj) was incorrectly
    // curried, passing workSolutionDir as the first arg (file) instead of the actual file path.
    let tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString())
    Directory.CreateDirectory(tempDir) |> ignore
    let projectFile = Path.Combine(tempDir, "Test.fsproj")
    File.WriteAllText(projectFile, "<Project></Project>")
    
    // Create two different source files
    let file1 = Path.Combine(tempDir, "file1.fs")
    let content1 = "let a = true"
    File.WriteAllText(file1, content1)
    
    let file2 = Path.Combine(tempDir, "file2.fs")
    let content2 = "let b = false"
    File.WriteAllText(file2, content2)

    try
        let workFolder = createWorkFolder ()
        try
            let workProject = copyProjectToWorkFolder projectFile workFolder
            let workDir = Path.GetDirectoryName workProject
            
            // Map both files to the work folder
            let workFile1 = mapFileToWorkFolder file1 tempDir workDir
            let workFile2 = mapFileToWorkFolder file2 tempDir workDir
            
            // Verify each mapped file can be read with the correct content
            let readContent1 = File.ReadAllText workFile1
            Assert.Equal(content1, readContent1)
            
            let readContent2 = File.ReadAllText workFile2
            Assert.Equal(content2, readContent2)
            
            // Verify the work files are distinct paths
            Assert.False((workFile1 = workFile2))
            
            // Verify original files are untouched
            Assert.Equal(content1, File.ReadAllText file1)
            Assert.Equal(content2, File.ReadAllText file2)
        finally
            deleteWorkFolder workFolder
    finally
        Directory.Delete(tempDir, true)

[<Fact>]
let ``enrich fills Original and Replacement from MutationKind`` () =
    let source = "let flag = true"
    let tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString())
    Directory.CreateDirectory(tempDir) |> ignore
    let fileFs = Path.Combine(tempDir, "test.fs")
    File.WriteAllText(fileFs, source)

    try
        let parsed = parseSource source
        let candidates = findMutationCandidates mutationCategories fileFs parsed
        let candidate = candidates |> List.find (fun c -> c.Kind = MutationKind.TrueToFalse)
        let enriched = enrich candidate
        Assert.Equal("true", enriched.Original)
        Assert.Equal("false", enriched.Replacement)
    finally
        Directory.Delete(tempDir, true)

[<Fact>]
let ``enrich preserves candidate metadata`` () =
    let source = "let isEqual x y = x = y"
    let tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString())
    Directory.CreateDirectory(tempDir) |> ignore
    let fileFs = Path.Combine(tempDir, "custom.fs")
    File.WriteAllText(fileFs, source)

    try
        let parsed = parseSource source
        let candidates = findMutationCandidates mutationCategories fileFs parsed
        let candidate = candidates |> List.find (fun c -> c.Kind = MutationKind.EqualityToInequality)
        let enriched = enrich candidate
        Assert.Equal(fileFs, enriched.File)
        Assert.Equal(MutationKind.EqualityToInequality, enriched.Kind)
        Assert.Equal(candidate.Id, enriched.Id)
        Assert.Equal(candidate.Range, enriched.Range)
    finally
        Directory.Delete(tempDir, true)
