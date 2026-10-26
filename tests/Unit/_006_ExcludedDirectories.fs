module Tests._006_ExcludedDirectories

open System
open System.IO
open Xunit
open Sabota.Files

// --- Tests for isExcludedFile logic (mirrors Program.fs behavior) ---

let excludedBuildDirs = Set.ofList ["debug"; "release"; "bin"; "obj"]

let isExcludedFile (file: string) : bool =
    let parts = file.Split([|'\\'; '/'|], StringSplitOptions.RemoveEmptyEntries)
    parts |> Array.exists (fun part -> excludedBuildDirs.Contains(part.ToLower()))

[<Fact>]
let ``isExcludedFile returns true for file in debug directory`` () =
    Assert.True(isExcludedFile "src/debug/Module.fs")
    Assert.True(isExcludedFile "src\\debug\\Module.fs")

[<Fact>]
let ``isExcludedFile returns true for file in release directory`` () =
    Assert.True(isExcludedFile "src/release/Module.fs")
    Assert.True(isExcludedFile "src\\release\\Module.fs")

[<Fact>]
let ``isExcludedFile returns true for file in bin directory`` () =
    Assert.True(isExcludedFile "src/bin/Module.fs")
    Assert.True(isExcludedFile "src\\bin\\Module.fs")

[<Fact>]
let ``isExcludedFile returns true for file in obj directory`` () =
    Assert.True(isExcludedFile "src/obj/Module.fs")
    Assert.True(isExcludedFile "src\\obj\\Module.fs")

[<Fact>]
let ``isExcludedFile is case-insensitive`` () =
    Assert.True(isExcludedFile "src/Debug/Module.fs")
    Assert.True(isExcludedFile "src/DEBUG/Module.fs")
    Assert.True(isExcludedFile "src/Release/Module.fs")
    Assert.True(isExcludedFile "src/RELEASE/Module.fs")
    Assert.True(isExcludedFile "src/Bin/Module.fs")
    Assert.True(isExcludedFile "src/Obj/Module.fs")

[<Fact>]
let ``isExcludedFile returns true for deeply nested excluded directories`` () =
    Assert.True(isExcludedFile "src/project/debug/Module.fs")
    Assert.True(isExcludedFile "src/project/release/sub/Module.fs")
    Assert.True(isExcludedFile "a/b/c/d/bin/Module.fs")

[<Fact>]
let ``isExcludedFile returns false for normal files`` () =
    Assert.False(isExcludedFile "src/Module.fs")
    Assert.False(isExcludedFile "src/project/Module.fs")
    Assert.False(isExcludedFile "src/project/sub/Module.fs")

[<Fact>]
let ``isExcludedFile returns false for directories with similar names`` () =
    Assert.False(isExcludedFile "src/debugger/Module.fs")
    Assert.False(isExcludedFile "src/releases/Module.fs")
    Assert.False(isExcludedFile "src/binary/Module.fs")
    Assert.False(isExcludedFile "src/object/Module.fs")

[<Fact>]
let ``isExcludedFile returns false for file names that match excluded dirs`` () =
    // A file named "debug.fs" in a normal directory should not be excluded
    Assert.False(isExcludedFile "src/debug.fs")
    Assert.False(isExcludedFile "src/module/debug.fs")

// --- Tests for copyDirectoryRecursive excluded directories ---

[<Fact>]
let ``copyDirectoryRecursive skips bin and obj directories`` () =
    let tempRoot = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString())
    try
        // Create source structure
        let srcDir = Path.Combine(tempRoot, "source")
        let srcBin = Path.Combine(srcDir, "bin")
        let srcObj = Path.Combine(srcDir, "obj")
        let srcSrc = Path.Combine(srcDir, "src")
        Directory.CreateDirectory(srcBin) |> ignore
        Directory.CreateDirectory(srcObj) |> ignore
        Directory.CreateDirectory(srcSrc) |> ignore

        File.WriteAllText(Path.Combine(srcBin, "file.dll"), "dll")
        File.WriteAllText(Path.Combine(srcObj, "file.o"), "o")
        File.WriteAllText(Path.Combine(srcSrc, "Module.fs"), "module")

        // Copy to target
        let tgtDir = Path.Combine(tempRoot, "target")
        copyDirectoryRecursive srcDir tgtDir

        // Verify bin and obj are not copied
        Assert.False(Directory.Exists(Path.Combine(tgtDir, "bin")))
        Assert.False(Directory.Exists(Path.Combine(tgtDir, "obj")))

        // Verify src is copied
        Assert.True(Directory.Exists(Path.Combine(tgtDir, "src")))
        Assert.True(File.Exists(Path.Combine(tgtDir, "src", "Module.fs")))
    finally
        if Directory.Exists tempRoot then
            Directory.Delete(tempRoot, recursive = true)

[<Fact>]
let ``copyDirectoryRecursive skips debug and release directories`` () =
    let tempRoot = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString())
    try
        let srcDir = Path.Combine(tempRoot, "source")
        let srcDebug = Path.Combine(srcDir, "debug")
        let srcRelease = Path.Combine(srcDir, "release")
        let srcSrc = Path.Combine(srcDir, "src")
        Directory.CreateDirectory(srcDebug) |> ignore
        Directory.CreateDirectory(srcRelease) |> ignore
        Directory.CreateDirectory(srcSrc) |> ignore

        File.WriteAllText(Path.Combine(srcDebug, "Module.fs"), "debug")
        File.WriteAllText(Path.Combine(srcRelease, "Module.fs"), "release")
        File.WriteAllText(Path.Combine(srcSrc, "Module.fs"), "module")

        let tgtDir = Path.Combine(tempRoot, "target")
        copyDirectoryRecursive srcDir tgtDir

        Assert.False(Directory.Exists(Path.Combine(tgtDir, "debug")))
        Assert.False(Directory.Exists(Path.Combine(tgtDir, "release")))
        Assert.True(Directory.Exists(Path.Combine(tgtDir, "src")))
    finally
        if Directory.Exists tempRoot then
            Directory.Delete(tempRoot, recursive = true)

[<Fact>]
let ``copyDirectoryRecursive skips dot-prefixed directories`` () =
    let tempRoot = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString())
    try
        let srcDir = Path.Combine(tempRoot, "source")
        let srcGit = Path.Combine(srcDir, ".git")
        let srcVs = Path.Combine(srcDir, ".vs")
        let srcSrc = Path.Combine(srcDir, "src")
        Directory.CreateDirectory(srcGit) |> ignore
        Directory.CreateDirectory(srcVs) |> ignore
        Directory.CreateDirectory(srcSrc) |> ignore

        File.WriteAllText(Path.Combine(srcGit, "config"), "git")
        File.WriteAllText(Path.Combine(srcVs, "config"), "vs")
        File.WriteAllText(Path.Combine(srcSrc, "Module.fs"), "module")

        let tgtDir = Path.Combine(tempRoot, "target")
        copyDirectoryRecursive srcDir tgtDir

        Assert.False(Directory.Exists(Path.Combine(tgtDir, ".git")))
        Assert.False(Directory.Exists(Path.Combine(tgtDir, ".vs")))
        Assert.True(Directory.Exists(Path.Combine(tgtDir, "src")))
    finally
        if Directory.Exists tempRoot then
            Directory.Delete(tempRoot, recursive = true)

[<Fact>]
let ``copyDirectoryRecursive copies files in root directory`` () =
    let tempRoot = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString())
    try
        let srcDir = Path.Combine(tempRoot, "source")
        Directory.CreateDirectory(srcDir) |> ignore
        File.WriteAllText(Path.Combine(srcDir, "Module.fs"), "module")
        File.WriteAllText(Path.Combine(srcDir, "Module2.fs"), "module2")

        let tgtDir = Path.Combine(tempRoot, "target")
        copyDirectoryRecursive srcDir tgtDir

        Assert.True(File.Exists(Path.Combine(tgtDir, "Module.fs")))
        Assert.True(File.Exists(Path.Combine(tgtDir, "Module2.fs")))
    finally
        if Directory.Exists tempRoot then
            Directory.Delete(tempRoot, recursive = true)

[<Fact>]
let ``copyDirectoryRecursive handles nested non-excluded directories`` () =
    let tempRoot = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString())
    try
        let srcDir = Path.Combine(tempRoot, "source")
        let nested = Path.Combine(srcDir, "src", "sub", "deep")
        Directory.CreateDirectory(nested) |> ignore
        File.WriteAllText(Path.Combine(nested, "Module.fs"), "deep")

        let tgtDir = Path.Combine(tempRoot, "target")
        copyDirectoryRecursive srcDir tgtDir

        Assert.True(File.Exists(Path.Combine(tgtDir, "src", "sub", "deep", "Module.fs")))
    finally
        if Directory.Exists tempRoot then
            Directory.Delete(tempRoot, recursive = true)

[<Fact>]
let ``copyDirectoryRecursive skips sabota and CoverageResults directories`` () =
    let tempRoot = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString())
    try
        let srcDir = Path.Combine(tempRoot, "source")
        let srcSabota = Path.Combine(srcDir, "sabota")
        let srcCoverage = Path.Combine(srcDir, "CoverageResults")
        Directory.CreateDirectory(srcSabota) |> ignore
        Directory.CreateDirectory(srcCoverage) |> ignore

        File.WriteAllText(Path.Combine(srcSabota, "result.json"), "result")
        File.WriteAllText(Path.Combine(srcCoverage, "coverage.xml"), "coverage")

        let tgtDir = Path.Combine(tempRoot, "target")
        copyDirectoryRecursive srcDir tgtDir

        Assert.False(Directory.Exists(Path.Combine(tgtDir, "sabota")))
        Assert.False(Directory.Exists(Path.Combine(tgtDir, "CoverageResults")))
    finally
        if Directory.Exists tempRoot then
            Directory.Delete(tempRoot, recursive = true)
