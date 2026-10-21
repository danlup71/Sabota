module Tests._003_IndependentMutation

open System
open System.IO
open Xunit
open FSharp.Compiler.CodeAnalysis
open FSharp.Compiler.Text
open Mutant1
open Mutant1.MutationCandidates
open FSharp.Compiler.Syntax
open Mutant1.MuationDefinitions
open Mutant1.Visitor
open Mutant1.Files

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

// --- copyFileAsOriginal tests ---

[<Fact>]
let ``copyFileAsOriginal creates backup file`` () =
    let tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString())
    Directory.CreateDirectory(tempDir) |> ignore
    let fileFs = Path.Combine(tempDir, "test.fs")
    File.WriteAllText(fileFs, "let x = true")

    try
        let backupPath = copyFileAsOriginal fileFs
        Assert.True(File.Exists backupPath)
        Assert.Equal("test_original.fs", Path.GetFileName backupPath)
    finally
        Directory.Delete(tempDir, true)

[<Fact>]
let ``copyFileAsOriginal backup content matches original`` () =
    let tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString())
    Directory.CreateDirectory(tempDir) |> ignore
    let fileFs = Path.Combine(tempDir, "test.fs")
    let content = "let x = true\nlet y = false"
    File.WriteAllText(fileFs, content)

    try
        let backupPath = copyFileAsOriginal fileFs
        let backupContent = File.ReadAllText(backupPath)
        Assert.Equal(content, backupContent)
    finally
        Directory.Delete(tempDir, true)

[<Fact>]
let ``copyFileAsOriginal overwrites existing backup`` () =
    let tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString())
    Directory.CreateDirectory(tempDir) |> ignore
    let fileFs = Path.Combine(tempDir, "test.fs")
    File.WriteAllText(fileFs, "let x = 1")

    let backupPath1 = copyFileAsOriginal fileFs
    File.WriteAllText(fileFs, "let x = 2")
    let backupPath2 = copyFileAsOriginal fileFs

    try
        Assert.Equal(backupPath1, backupPath2)
        let backupContent = File.ReadAllText(backupPath2)
        Assert.Equal("let x = 2", backupContent)
    finally
        Directory.Delete(tempDir, true)

// --- restoreFromOriginal tests ---

[<Fact>]
let ``restoreFromOriginal restores mutated file to original content`` () =
    let tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString())
    Directory.CreateDirectory(tempDir) |> ignore
    let fileFs = Path.Combine(tempDir, "test.fs")
    let originalContent = "let x = true"
    File.WriteAllText(fileFs, originalContent)

    try
        copyFileAsOriginal fileFs |> ignore
        File.WriteAllText(fileFs, "let x = false")
        restoreFromOriginal fileFs |> ignore

        let restoredContent = File.ReadAllText(fileFs)
        Assert.Equal(originalContent, restoredContent)
    finally
        Directory.Delete(tempDir, true)

[<Fact>]
let ``restoreFromOriginal deletes backup file`` () =
    let tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString())
    Directory.CreateDirectory(tempDir) |> ignore
    let fileFs = Path.Combine(tempDir, "test.fs")
    File.WriteAllText(fileFs, "let x = true")

    try
        let backupPath = copyFileAsOriginal fileFs
        restoreFromOriginal fileFs |> ignore
        Assert.False(File.Exists backupPath)
    finally
        Directory.Delete(tempDir, true)

[<Fact>]
let ``restoreFromOriginal throws when backup missing`` () =
    let tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString())
    Directory.CreateDirectory(tempDir) |> ignore
    let fileFs = Path.Combine(tempDir, "test.fs")
    File.WriteAllText(fileFs, "let x = true")

    try
        Assert.Throws<System.ArgumentException>(fun () ->
            restoreFromOriginal fileFs |> ignore)
    finally
        Directory.Delete(tempDir, true)

// --- applyMutation tests ---

[<Fact>]
let ``applyMutation replaces true with false`` () =
    let tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString())
    Directory.CreateDirectory(tempDir) |> ignore
    let fileFs = Path.Combine(tempDir, "test.fs")
    File.WriteAllText(fileFs, "let flag = true")

    try
        let parsed = parseSource "let flag = true"
        let candidates = findMutationCandidates fileFs parsed
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
        let candidates = findMutationCandidates fileFs parsed
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
        let candidates = findMutationCandidates fileFs parsed
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
        let candidates = findMutationCandidates fileFs parsed
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
let ``each mutation is applied and restored independently`` () =
    let tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString())
    Directory.CreateDirectory(tempDir) |> ignore
    let fileFs = Path.Combine(tempDir, "test.fs")
    let originalContent = "let a = true\nlet b = false"
    File.WriteAllText(fileFs, originalContent)

    try
        let parsed = parseSource originalContent
        let candidates = findMutationCandidates fileFs parsed |> List.map enrich

        for candidate in candidates do
            copyFileAsOriginal fileFs |> ignore
            applyMutation candidate
            let mutatedContent = File.ReadAllText(fileFs)
            Assert.NotEqual<string>(originalContent, mutatedContent)
            restoreFromOriginal fileFs |> ignore

        let finalContent = File.ReadAllText(fileFs)
        Assert.Equal(originalContent, finalContent)
    finally
        Directory.Delete(tempDir, true)

[<Fact>]
let ``mutations do not interfere with each other`` () =
    let tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString())
    Directory.CreateDirectory(tempDir) |> ignore
    let fileFs = Path.Combine(tempDir, "test.fs")
    let originalContent = "let a = true\nlet b = false"
    File.WriteAllText(fileFs, originalContent)

    try
        let parsed = parseSource originalContent
        let candidates = findMutationCandidates fileFs parsed |> List.map enrich

        let trueToFalse = candidates |> List.find (fun c -> c.Kind = MutationKind.TrueToFalse)
        let falseToTrue = candidates |> List.find (fun c -> c.Kind = MutationKind.FalseToTrue)

        // Apply first mutation
        copyFileAsOriginal fileFs |> ignore
        applyMutation trueToFalse
        let content1 = File.ReadAllText(fileFs)
        Assert.Contains("false", content1)

        // Restore and apply second mutation
        restoreFromOriginal fileFs |> ignore
        copyFileAsOriginal fileFs |> ignore
        applyMutation falseToTrue
        let content2 = File.ReadAllText(fileFs)
        Assert.Contains("true", content2)

        // Verify first mutation is not present
        restoreFromOriginal fileFs |> ignore
        let finalContent = File.ReadAllText(fileFs)
        Assert.Equal(originalContent, finalContent)
    finally
        Directory.Delete(tempDir, true)

[<Fact>]
let ``full cycle preserves file across multiple mutations`` () =
    let tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString())
    Directory.CreateDirectory(tempDir) |> ignore
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
        let candidates = findMutationCandidates fileFs parsed |> List.map enrich

        let mutationResults =
            candidates |> List.map (fun candidate ->
                copyFileAsOriginal fileFs |> ignore
                applyMutation candidate
                let mutatedContent = File.ReadAllText(fileFs)
                restoreFromOriginal fileFs |> ignore
                (candidate.Kind, mutatedContent)
            )

        // Verify file is restored to original after all mutations
        let finalContent = File.ReadAllText(fileFs)
        Assert.Equal(originalContent, finalContent)

        // Verify each mutation produced a different result
        Assert.Equal(candidates.Length, mutationResults.Length)
        for (kind, mutatedContent) in mutationResults do
            Assert.NotEqual<string>(originalContent, mutatedContent)
    finally
        Directory.Delete(tempDir, true)

[<Fact>]
let ``each mutation only changes its target range`` () =
    let tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString())
    Directory.CreateDirectory(tempDir) |> ignore
    let fileFs = Path.Combine(tempDir, "test.fs")
    let originalContent = "let a = true\nlet b = 1 = 2"
    File.WriteAllText(fileFs, originalContent)

    try
        let parsed = parseSource originalContent
        let candidates = findMutationCandidates fileFs parsed |> List.map enrich

        for candidate in candidates do
            copyFileAsOriginal fileFs |> ignore
            applyMutation candidate
            let mutatedContent = File.ReadAllText(fileFs)

            // Only one token should be changed
            let originalLines = originalContent.Split('\n')
            let mutatedLines = mutatedContent.Split('\n')
            let changedLines =
                originalLines
                |> List.ofArray
                |> List.mapi (fun i line -> (i, line))
                |> List.filter (fun (i, line) ->
                    i < mutatedLines.Length && mutatedLines.[i] <> line)

            Assert.Single changedLines
            restoreFromOriginal fileFs |> ignore
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
        let candidates = findMutationCandidates fileFs parsed
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
        let candidates = findMutationCandidates fileFs parsed
        let candidate = candidates |> List.find (fun c -> c.Kind = MutationKind.EqualityToInequality)
        let enriched = enrich candidate
        Assert.Equal(fileFs, enriched.File)
        Assert.Equal(MutationKind.EqualityToInequality, enriched.Kind)
        Assert.Equal(candidate.Id, enriched.Id)
        Assert.Equal(candidate.Range, enriched.Range)
    finally
        Directory.Delete(tempDir, true)
