module Tests._006_ExcludedDirectories

open System
open System.IO
open Xunit
open Sabota.Files
open TestApp

// --- Tests for isExcludedFile logic (mirrors Program.fs behavior) ---

[<Fact>]
let ``01. when file is in debug directory, it is excluded`` () =
    // Arrange
    ["src/debug/Module.fs"; "src\\debug\\Module.fs"]
    // Act & Assert
    |> List.iter (fun f -> f |> TestApp.Assert_IsExcludedFile |> ignore)

[<Fact>]
let ``02. when file is in release directory, it is excluded`` () =
    // Arrange
    ["src/release/Module.fs"; "src\\release\\Module.fs"]
    // Act & Assert
    |> List.iter (fun f -> f |> TestApp.Assert_IsExcludedFile |> ignore)

[<Fact>]
let ``03. when file is in bin directory, it is excluded`` () =
    // Arrange
    ["src/bin/Module.fs"; "src\\bin\\Module.fs"]
    // Act & Assert
    |> List.iter (fun f -> f |> TestApp.Assert_IsExcludedFile |> ignore)

[<Fact>]
let ``04. when file is in obj directory, it is excluded`` () =
    // Arrange
    ["src/obj/Module.fs"; "src\\obj\\Module.fs"]
    // Act & Assert
    |> List.iter (fun f -> f |> TestApp.Assert_IsExcludedFile |> ignore)

[<Fact>]
let ``05. when directory name has mixed case, it is still excluded`` () =
    // Arrange
    ["src/Debug/Module.fs"; "src/DEBUG/Module.fs"; "src/Release/Module.fs"; "src/RELEASE/Module.fs"; "src/Bin/Module.fs"; "src/Obj/Module.fs"]
    // Act & Assert
    |> List.iter (fun f -> f |> TestApp.Assert_IsExcludedFile |> ignore)

[<Fact>]
let ``06. when excluded directory is deeply nested, it is excluded`` () =
    // Arrange
    ["src/project/debug/Module.fs"; "src/project/release/sub/Module.fs"; "a/b/c/d/bin/Module.fs"]
    // Act & Assert
    |> List.iter (fun f -> f |> TestApp.Assert_IsExcludedFile |> ignore)

[<Fact>]
let ``07. when file is in a normal directory, it is not excluded`` () =
    // Arrange
    ["src/Module.fs"; "src/project/Module.fs"; "src/project/sub/Module.fs"]
    // Act & Assert
    |> List.iter (fun f -> f |> TestApp.Assert_IsIncluded |> ignore)

[<Fact>]
let ``08. when directory name is similar to excluded, it is not excluded`` () =
    // Arrange
    ["src/debugger/Module.fs"; "src/releases/Module.fs"; "src/binary/Module.fs"; "src/object/Module.fs"]
    // Act & Assert
    |> List.iter (fun f -> f |> TestApp.Assert_IsIncluded |> ignore)

[<Fact>]
let ``09. when file name matches excluded directory name, it is not excluded`` () =
    // Arrange
    ["src/debug.fs"; "src/module/debug.fs"]
    // Act & Assert
    |> List.iter (fun f -> f |> TestApp.Assert_IsIncluded |> ignore)

// --- Tests for copyDirectoryRecursive excluded directories ---

[<Fact>]
let ``10. when copying directory tree, bin and obj are skipped`` () =
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
let ``11. when copying directory tree, debug and release are skipped`` () =
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
let ``12. when copying directory tree, dot-prefixed directories are skipped`` () =
    let tempRoot = TestApp.CreateSimulatedBaseDir()
    try
        // Arrange
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

        // Act
        let tgtDir = Path.Combine(tempRoot, "target")
        copyDirectoryRecursive srcDir tgtDir

        // Assert
        Assert.False(Directory.Exists(Path.Combine(tgtDir, ".git")))
        Assert.False(Directory.Exists(Path.Combine(tgtDir, ".vs")))
        Assert.True(Directory.Exists(Path.Combine(tgtDir, "src")))
    finally
        if Directory.Exists tempRoot then
            Directory.Delete(tempRoot, recursive = true)

[<Fact>]
let ``13. when copying directory tree, root files are included`` () =
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
let ``14. when copying directory tree, nested non-excluded directories are included`` () =
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
let ``15. when copying directory tree, sabota and CoverageResults are skipped`` () =
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
