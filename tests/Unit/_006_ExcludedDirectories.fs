module Tests._006_ExcludedDirectories

open System
open System.IO
open Xunit
open Sabota.Files
open TestApp

// --- Tests for isExcludedFile logic (mirrors Program.fs behavior) ---

[<Theory>]
[<InlineData("src/debug/Module.fs")>]
[<InlineData("src\\debug\\Module.fs")>]
let ``01. when file is in debug directory, it is excluded`` (file : string) =
    file
    |> TestApp.Assert_IsExcludedFile
    |> ignore

[<Theory>]
[<InlineData("src/release/Module.fs")>]
[<InlineData("src\\release\\Module.fs")>]
let ``02. when file is in release directory, it is excluded`` (file : string) =
    file
    |> TestApp.Assert_IsExcludedFile
    |> ignore

[<Theory>]
[<InlineData("src/bin/Module.fs")>]
[<InlineData("src\\bin\\Module.fs")>]
let ``03. when file is in bin directory, it is excluded`` (file : string) =
    file
    |> TestApp.Assert_IsExcludedFile
    |> ignore

[<Theory>]
[<InlineData("src/obj/Module.fs")>]
[<InlineData("src\\obj\\Module.fs")>]
let ``04. when file is in obj directory, it is excluded`` (file : string) =
    file
    |> TestApp.Assert_IsExcludedFile
    |> ignore

[<Theory>]
[<InlineData("src/Debug/Module.fs")>]
[<InlineData("src/DEBUG/Module.fs")>]
[<InlineData("src/Release/Module.fs")>]
[<InlineData("src/RELEASE/Module.fs")>]
[<InlineData("src/Bin/Module.fs")>]
[<InlineData("src/Obj/Module.fs")>]
let ``05. when directory name has mixed case, it is still excluded`` (file : string) =
    file
    |> TestApp.Assert_IsExcludedFile
    |> ignore

[<Theory>]
[<InlineData("src/project/debug/Module.fs")>]
[<InlineData("src/project/release/sub/Module.fs")>]
[<InlineData("a/b/c/d/bin/Module.fs")>]
let ``06. when excluded directory is deeply nested, it is excluded`` (file : string) =
    file
    |> TestApp.Assert_IsExcludedFile
    |> ignore

[<Theory>]
[<InlineData("src/Module.fs")>]
[<InlineData("src/project/Module.fs")>]
[<InlineData("src/project/sub/Module.fs")>]
let ``07. when file is in a normal directory, it is not excluded`` (file : string) =
    file
    |> TestApp.Assert_IsIncluded
    |> ignore

[<Theory>]
[<InlineData("src/debugger/Module.fs")>]
[<InlineData("src/releases/Module.fs")>]
[<InlineData("src/binary/Module.fs")>]
[<InlineData("src/object/Module.fs")>]
let ``08. when directory name is similar to excluded, it is not excluded`` (file : string) =
    file
    |> TestApp.Assert_IsIncluded
    |> ignore

[<Theory>]
[<InlineData("src/debug.fs")>]
[<InlineData("src/module/debug.fs")>]
let ``09. when file name matches excluded directory name, it is not excluded`` (file : string) =
    file
    |> TestApp.Assert_IsIncluded
    |> ignore

// --- Tests for copyDirectoryRecursive excluded directories ---

[<Fact>]
let ``10. when copying directory tree, bin and obj are skipped`` () =
    let tempRoot = TestApp.CreateSimulatedBaseDir()
    try
        // Arrange
        let srcDir, tgtDir = TestApp.CreateCopyDirectoryStructure(
            tempRoot, "source",
            [("bin", "file.dll", "dll"); ("obj", "file.o", "o"); ("src", "Module.fs", "module")],
            []
        )

        // Act
        copyDirectoryRecursive srcDir tgtDir

        // Assert
        TestApp.Assert_Directory_NotExists(Path.Combine(tgtDir, "bin")) |> ignore
        TestApp.Assert_Directory_NotExists(Path.Combine(tgtDir, "obj"))  |> ignore
        TestApp.Assert_Directory_Exists(Path.Combine(tgtDir, "src")) |> ignore
        TestApp.Assert_File_Exists(Path.Combine(tgtDir, "src", "Module.fs")) |> ignore
    finally
        TestApp.CleanSimulatedEnvironment tempRoot

[<Fact>]
let ``11. when copying directory tree, debug and release are skipped`` () =
    let tempRoot = TestApp.CreateSimulatedBaseDir()
    try
        // Arrange
        let srcDir, tgtDir = TestApp.CreateCopyDirectoryStructure(
            tempRoot, "source",
            [("debug", "Module.fs", "debug"); ("release", "Module.fs", "release"); ("src", "Module.fs", "module")],
            []
        )

        // Act
        copyDirectoryRecursive srcDir tgtDir

        // Assert
        TestApp.Assert_Directory_NotExists(Path.Combine(tgtDir, "debug")) |> ignore
        TestApp.Assert_Directory_NotExists(Path.Combine(tgtDir, "release")) |> ignore
        TestApp.Assert_Directory_Exists(Path.Combine(tgtDir, "src")) |> ignore
    finally
        TestApp.CleanSimulatedEnvironment tempRoot

[<Fact>]
let ``12. when copying directory tree, dot-prefixed directories are skipped`` () =
    let tempRoot = TestApp.CreateSimulatedBaseDir()
    try
        // Arrange
        let srcDir, tgtDir = TestApp.CreateCopyDirectoryStructure(
            tempRoot, "source",
            [(".git", "config", "git"); (".vs", "config", "vs"); ("src", "Module.fs", "module")],
            []
        )

        // Act
        copyDirectoryRecursive srcDir tgtDir

        // Assert
        TestApp.Assert_Directory_NotExists(Path.Combine(tgtDir, ".git")) |> ignore
        TestApp.Assert_Directory_NotExists(Path.Combine(tgtDir, ".vs")) |> ignore
        TestApp.Assert_Directory_Exists(Path.Combine(tgtDir, "src")) |> ignore
    finally
        TestApp.CleanSimulatedEnvironment tempRoot

[<Fact>]
let ``13. when copying directory tree, root files are included`` () =
    let tempRoot = TestApp.CreateSimulatedBaseDir()
    try
        // Arrange
        let srcDir, tgtDir = TestApp.CreateCopyDirectoryStructure(
            tempRoot, "source",
            [("", "Module.fs", "module"); ("", "Module2.fs", "module2")],
            []
        )

        // Act
        copyDirectoryRecursive srcDir tgtDir

        // Assert
        TestApp.Assert_File_Exists(Path.Combine(tgtDir, "Module.fs")) |> ignore
        TestApp.Assert_File_Exists(Path.Combine(tgtDir, "Module2.fs")) |> ignore
    finally
        TestApp.CleanSimulatedEnvironment tempRoot

[<Fact>]
let ``14. when copying directory tree, nested non-excluded directories are included`` () =
    let tempRoot = TestApp.CreateSimulatedBaseDir()
    try
        // Arrange
        let srcDir, tgtDir = TestApp.CreateCopyDirectoryStructure(
            tempRoot, "source",
            [("src/sub/deep", "Module.fs", "deep")],
            []
        )

        // Act
        copyDirectoryRecursive srcDir tgtDir

        // Assert
        TestApp.Assert_File_Exists(Path.Combine(tgtDir, "src", "sub", "deep", "Module.fs")) |> ignore
    finally
        TestApp.CleanSimulatedEnvironment tempRoot

[<Fact>]
let ``15. when copying directory tree, sabota and CoverageResults are skipped`` () =
    let tempRoot = TestApp.CreateSimulatedBaseDir()
    try
        // Arrange
        let srcDir, tgtDir = TestApp.CreateCopyDirectoryStructure(
            tempRoot, "source",
            [("sabota", "result.json", "result"); ("CoverageResults", "coverage.xml", "coverage")],
            []
        )

        // Act
        copyDirectoryRecursive srcDir tgtDir

        // Assert
        TestApp.Assert_Directory_NotExists(Path.Combine(tgtDir, "sabota")) |> ignore
        TestApp.Assert_Directory_NotExists(Path.Combine(tgtDir, "CoverageResults")) |> ignore
    finally
        TestApp.CleanSimulatedEnvironment tempRoot
