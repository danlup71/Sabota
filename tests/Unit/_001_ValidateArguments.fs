#nowarn "FS3261"

module Tests._001_ValidateArguments

open System
open System.IO
open Xunit
open Sabota.Args
open TestApp

[<Fact>]
let ``parseArgs with -t -s -f returns Ok with File target`` () =
    // Arrange
    "-t test.fsproj -s solution_root -f file.fs"
    |> TestApp.ParseCommandLine
    // Act
    |> parseArgs
    // Assert
    |> TestApp.Assert_ArgsTarget_Size 1
    |> TestApp.Assert_ArgsTarget_Contains ["file.fs"]
    |> TestApp.Assert_ArgsTestProject (Some "test.fsproj")
    |> TestApp.Assert_ArgsSolutionRoot (Some "solution_root")

[<Fact>]
let ``parseArgs with -t -s -d returns Ok with Directory target`` () =
    // Arrange
    "-t test.fsproj -s solution_root -d src_dir"
    |> TestApp.ParseCommandLine
    // Act
    |> parseArgs
    // Assert
    |> TestApp.Assert_ArgsTarget_Size 1
    |> TestApp.Assert_ArgsDirectory_Contains ["src_dir"]
    |> TestApp.Assert_ArgsTestProject (Some "test.fsproj")
    |> TestApp.Assert_ArgsSolutionRoot (Some "solution_root")

[<Fact>]
let ``parseArgs with both -f and -d returns Error`` () =
    // Arrange
    "-t test.fsproj -s solution_root -f file.fs -d src_dir"
    |> TestApp.ParseCommandLine
    // Act
    |> parseArgs
    // Assert
    |> TestApp.Assert_ArgsError_Contains "mutually exclusive"

[<Fact>]
let ``parseArgs with neither -f nor -d returns Ok with None target`` () =
    // Arrange
    "-t test.fsproj -s solution_root"
    |> TestApp.ParseCommandLine
    // Act
    |> parseArgs
    // Assert
    |> TestApp.Assert_ArgsTarget_IsMissing()

[<Fact>]
let ``parseArgs missing -t returns Ok with None`` () =
    // Arrange
    "-s solution_root -f file.fs"
    |> TestApp.ParseCommandLine
    // Act
    |> parseArgs
    // Assert
    |> TestApp.Assert_ArgsTarget_Size 1
    |> TestApp.Assert_ArgsTestProject None

[<Fact>]
let ``parseArgs missing -s returns Ok with None`` () =
    // Arrange
    "-t test.fsproj -f file.fs"
    |> TestApp.ParseCommandLine
    // Act
    |> parseArgs
    // Assert
    |> TestApp.Assert_ArgsTarget_Size 1
    |> TestApp.Assert_ArgsSolutionRoot None

[<Fact>]
let ``parseArgs with empty array returns Ok with all None`` () =
    // Arrange
    ""
    |> TestApp.ParseCommandLine
    // Act
    |> parseArgs
    // Assert
    |> TestApp.Assert_ArgsTarget_IsMissing()
    |> TestApp.Assert_ArgsTestProject_IsMissing()
    |> TestApp.Assert_ArgsSolutionRoot_IsMissing()

[<Fact>]
let ``parseArgs ignores unknown arguments`` () =
    // Arrange
    "-t test.fsproj -s solution_root -f file.fs -x unknown"
    |> TestApp.ParseCommandLine
    // Act
    |> parseArgs
    // Assert
    |> TestApp.Assert_ArgsTarget_Size 1
    |> TestApp.Assert_ArgsTarget_Contains ["file.fs"]
    |> TestApp.Assert_ArgsTestProject (Some "test.fsproj")
    |> TestApp.Assert_ArgsSolutionRoot (Some "solution_root")

[<Fact>]
let ``parseArgs accepts arguments in any order`` () =
    // Arrange
    "-f file.fs -t test.fsproj -s solution_root"
    |> TestApp.ParseCommandLine
    // Act
    |> parseArgs
    // Assert
    |> TestApp.Assert_ArgsTarget_Size 1
    |> TestApp.Assert_ArgsTarget_Contains ["file.fs"]
    |> TestApp.Assert_ArgsTestProject (Some "test.fsproj")
    |> TestApp.Assert_ArgsSolutionRoot (Some "solution_root")

[<Fact>]
let ``validate with non-existent files returns Error`` () =
    match validate "C:\\nonexistent\\test.fsproj" "C:\\nonexistent\\solution" (File ["C:\\nonexistent\\file.fs"]) with
    | Error msg ->
        Assert.Contains("Test project not found", msg)
        Assert.Contains("Solution root not found", msg)
        Assert.Contains("File not found", msg)
    | Ok _ ->
        Assert.Fail("Expected Error for non-existent files")

[<Fact>]
let ``validate with existing files returns Ok`` () =
    // Arrange
    let baseDir = TestApp.CreateSimulatedBaseDir()
    try
        // Act
        let env = TestApp.CreateSimulatedEnvironment(Stub.TestProjectName, Stub.SolutionRootName, Stub.FileFsName, baseDir)
        validate env.TestProject env.SolutionRoot (File [env.FileFs])
        // Assert
        |> TestApp.Assert_MinimalRequirementsMet()
    finally
        TestApp.CleanSimulatedEnvironment baseDir

[<Fact>]
let ``validate file without .fs extension returns Error`` () =
    let tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString())
    Directory.CreateDirectory(tempDir) |> ignore

    let testPrj = Path.Combine(tempDir, "test.fsproj")
    let solutionRoot = Path.Combine(tempDir, "solution")
    Directory.CreateDirectory(solutionRoot) |> ignore
    let fileCs = Path.Combine(tempDir, "file.cs")

    File.WriteAllText(testPrj, "<Project/>")
    File.WriteAllText(fileCs, "class Foo {}")

    try
        match validate testPrj solutionRoot (File [fileCs]) with
        | Error msg ->
            Assert.Contains(".fs", msg)
        | Ok _ ->
            Assert.Fail("Expected Error for non-.fs file")
    finally
        Directory.Delete(tempDir, true)

[<Fact>]
let ``validate test project without .fsproj extension returns Error`` () =
    let tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString())
    Directory.CreateDirectory(tempDir) |> ignore

    let testPrj = Path.Combine(tempDir, "test.csproj")
    let solutionRoot = Path.Combine(tempDir, "solution")
    Directory.CreateDirectory(solutionRoot) |> ignore
    let fileFs = Path.Combine(tempDir, "file.fs")

    File.WriteAllText(testPrj, "<Project/>")
    File.WriteAllText(fileFs, "let x = 1")

    try
        match validate testPrj solutionRoot (File [fileFs]) with
        | Error msg ->
            Assert.Contains(".fsproj", msg)
        | Ok _ ->
            Assert.Fail("Expected Error for non-.fsproj test project")
    finally
        Directory.Delete(tempDir, true)

[<Fact>]
let ``validate with existing directory returns Ok`` () =
    let tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString())
    Directory.CreateDirectory(tempDir) |> ignore

    let testPrj = Path.Combine(tempDir, "test.fsproj")
    let solutionRoot = Path.Combine(tempDir, "solution")
    Directory.CreateDirectory(solutionRoot) |> ignore
    let src_dir = Path.Combine(tempDir, "src")
    Directory.CreateDirectory(src_dir) |> ignore

    File.WriteAllText(testPrj, "<Project/>")

    try
        match validate testPrj solutionRoot (Directory [src_dir]) with
        | Ok _ -> ()
        | Error msg ->
            Assert.Fail($"Expected Ok, got Error: {msg}")
    finally
        Directory.Delete(tempDir, true)

[<Fact>]
let ``validate with non-existent directory returns Error`` () =
    let tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString())
    Directory.CreateDirectory(tempDir) |> ignore

    let testPrj = Path.Combine(tempDir, "test.fsproj")
    let solutionRoot = Path.Combine(tempDir, "solution")
    Directory.CreateDirectory(solutionRoot) |> ignore
    let nonExistentDir = Path.Combine(tempDir, "no_dir")

    File.WriteAllText(testPrj, "<Project/>")

    try
        match validate testPrj solutionRoot (Directory [nonExistentDir]) with
        | Error msg ->
            Assert.Contains("Directory not found", msg)
        | Ok _ ->
            Assert.Fail("Expected Error for non-existent directory")
    finally
        Directory.Delete(tempDir, true)

[<Fact>]
let ``parseAndValidate with -f resolves relative paths to absolute`` () =
    // Arrange
    let tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString())
    Directory.CreateDirectory(tempDir) |> ignore

    let testPrj = Path.Combine(tempDir, "test.fsproj")
    let solutionRoot = Path.Combine(tempDir, "solution")
    Directory.CreateDirectory(solutionRoot) |> ignore
    let fileFs = Path.Combine(tempDir, "file.fs")

    File.WriteAllText(testPrj, "<Project/>")
    File.WriteAllText(fileFs, "let x = 1")

    let originalDir = Environment.CurrentDirectory
    Environment.CurrentDirectory <- tempDir

    try
        "-t test.fsproj -s solution -f file.fs"
        |> TestApp.ParseCommandLine
        // Act
        |> parseArgs
        // Assert
        |> TestApp.Assert_ArgsTarget_Size 1
        |> TestApp.Assert_ArgsTarget_Contains ["file.fs"]
        |> TestApp.Assert_ArgsTestProject (Some "test.fsproj")
        |> TestApp.Assert_ArgsSolutionRoot (Some "solution")
    finally
        Environment.CurrentDirectory <- originalDir
        Directory.Delete(tempDir, true)

[<Fact>]
let ``parseAndValidate with -d resolves relative paths to absolute`` () =
    // Arrange
    let tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString())
    Directory.CreateDirectory(tempDir) |> ignore

    let testPrj = Path.Combine(tempDir, "test.fsproj")
    let solutionRoot = Path.Combine(tempDir, "solution")
    Directory.CreateDirectory(solutionRoot) |> ignore
    let src_dir = Path.Combine(tempDir, "src")
    Directory.CreateDirectory(src_dir) |> ignore

    File.WriteAllText(testPrj, "<Project/>")

    let originalDir = Environment.CurrentDirectory
    Environment.CurrentDirectory <- tempDir

    try
        "-t test.fsproj -s solution -d src"
        |> TestApp.ParseCommandLine
        // Act
        |> parseArgs
        // Assert
        |> TestApp.Assert_ArgsTarget_Size 1
        |> TestApp.Assert_ArgsDirectory_Contains ["src"]
        |> TestApp.Assert_ArgsTestProject (Some "test.fsproj")
        |> TestApp.Assert_ArgsSolutionRoot (Some "solution")
    finally
        Environment.CurrentDirectory <- originalDir
        Directory.Delete(tempDir, true)

[<Fact>]
let ``parseArgs with missing args returns Ok with None values`` () =
    // Arrange
    "-t test.fsproj"
    |> TestApp.ParseCommandLine
    // Act
    |> parseArgs
    // Assert
    |> TestApp.Assert_ArgsTarget_IsMissing()
    |> TestApp.Assert_ArgsTestProject (Some "test.fsproj")
    |> TestApp.Assert_ArgsSolutionRoot_IsMissing()

[<Fact>]
let ``parseArgs missing -s with valid files returns Ok with None`` () =
    // Arrange
    let tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString())
    Directory.CreateDirectory(tempDir) |> ignore

    let testPrj = Path.Combine(tempDir, "test.fsproj")
    let fileFs = Path.Combine(tempDir, "file.fs")

    File.WriteAllText(testPrj, "<Project/>")
    File.WriteAllText(fileFs, "let x = 1")

    try
        [|"-t"; testPrj; "-f"; fileFs|]
        // Act
        |> parseArgs
        // Assert
        |> TestApp.Assert_ArgsTarget_Size 1
        |> TestApp.Assert_ArgsSolutionRoot None
    finally
        Directory.Delete(tempDir, true)

[<Fact>]
let ``validate rejects non-existent paths`` () =
    match validate "C:\\no\\test.fsproj" "C:\\no\\solution" (File ["C:\\no\\file.fs"]) with
    | Error msg ->
        Assert.Contains("not found", msg)
    | Ok _ ->
        Assert.Fail("Expected Error for non-existent files")

[<Fact>]
let ``validate rejects -f with wrong extension`` () =
    let tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString())
    Directory.CreateDirectory(tempDir) |> ignore

    let testPrj = Path.Combine(tempDir, "test.fsproj")
    let solutionRoot = Path.Combine(tempDir, "solution")
    Directory.CreateDirectory(solutionRoot) |> ignore
    let fileCs = Path.Combine(tempDir, "file.cs")

    File.WriteAllText(testPrj, "<Project/>")
    File.WriteAllText(fileCs, "class Foo {}")

    try
        match validate testPrj solutionRoot (File [fileCs]) with
        | Error msg ->
            Assert.Contains(".fs", msg)
        | Ok _ ->
            Assert.Fail("Expected Error for .cs file")
    finally
        Directory.Delete(tempDir, true)

[<Fact>]
let ``parseArgs with -f CSV multiple files returns Ok with File list`` () =
    // Arrange
    "-t test.fsproj -s solution_root -f file1.fs,file2.fs,file3.fs"
    |> TestApp.ParseCommandLine
    // Act
    |> parseArgs
    // Assert
    |> TestApp.Assert_ArgsTarget_Size 3
    |> TestApp.Assert_ArgsTarget_Contains ["file1.fs"; "file2.fs"; "file3.fs"]

[<Fact>]
let ``parseArgs with -f CSV with spaces returns Ok with trimmed files`` () =
    // Arrange
    "-t test.fsproj -s solution_root -f \"file1.fs , file2.fs , file3.fs\""
    |> TestApp.ParseCommandLine
    // Act
    |> parseArgs
    // Assert
    |> TestApp.Assert_ArgsTarget_Size 3
    |> TestApp.Assert_ArgsTarget_Contains ["file1.fs"; "file2.fs"; "file3.fs"]

[<Fact>]
let ``validate with multiple files validates all`` () =
    let tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString())
    Directory.CreateDirectory(tempDir) |> ignore

    let testPrj = Path.Combine(tempDir, "test.fsproj")
    let solutionRoot = Path.Combine(tempDir, "solution")
    Directory.CreateDirectory(solutionRoot) |> ignore
    let file1 = Path.Combine(tempDir, "file1.fs")
    let file2 = Path.Combine(tempDir, "file2.fs")

    File.WriteAllText(testPrj, "<Project/>")
    File.WriteAllText(file1, "let x = 1")
    File.WriteAllText(file2, "let y = 2")

    try
        match validate testPrj solutionRoot (File [file1; file2]) with
        | Ok _ -> ()
        | Error msg ->
            Assert.Fail($"Expected Ok, got Error: {msg}")
    finally
        Directory.Delete(tempDir, true)

[<Fact>]
let ``validate with one non-existent file in list returns Error`` () =
    let tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString())
    Directory.CreateDirectory(tempDir) |> ignore

    let testPrj = Path.Combine(tempDir, "test.fsproj")
    let solutionRoot = Path.Combine(tempDir, "solution")
    Directory.CreateDirectory(solutionRoot) |> ignore
    let file1 = Path.Combine(tempDir, "file1.fs")
    let file2 = Path.Combine(tempDir, "nonexistent.fs")

    File.WriteAllText(testPrj, "<Project/>")
    File.WriteAllText(file1, "let x = 1")

    try
        match validate testPrj solutionRoot (File [file1; file2]) with
        | Error msg ->
            Assert.Contains("File not found", msg)
        | Ok _ ->
            Assert.Fail("Expected Error for non-existent file in list")
    finally
        Directory.Delete(tempDir, true)

[<Fact>]
let ``validate with one wrong extension in list returns Error`` () =
    let tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString())
    Directory.CreateDirectory(tempDir) |> ignore

    let testPrj = Path.Combine(tempDir, "test.fsproj")
    let solutionRoot = Path.Combine(tempDir, "solution")
    Directory.CreateDirectory(solutionRoot) |> ignore
    let file1 = Path.Combine(tempDir, "file1.fs")
    let file2 = Path.Combine(tempDir, "file2.cs")

    File.WriteAllText(testPrj, "<Project/>")
    File.WriteAllText(file1, "let x = 1")
    File.WriteAllText(file2, "class Foo {}")

    try
        match validate testPrj solutionRoot (File [file1; file2]) with
        | Error msg ->
            Assert.Contains(".fs", msg)
        | Ok _ ->
            Assert.Fail("Expected Error for .cs file in list")
    finally
        Directory.Delete(tempDir, true)

[<Fact>]
let ``parseArgs with only -t returns Ok with None for solution and target`` () =
    // Arrange
    "-t test.fsproj"
    |> TestApp.ParseCommandLine
    // Act
    |> parseArgs
    // Assert
    |> TestApp.Assert_ArgsTarget_IsMissing()
    |> TestApp.Assert_ArgsTestProject (Some "test.fsproj")
    |> TestApp.Assert_ArgsSolutionRoot_IsMissing()

[<Fact>]
let ``parseArgs with only -s returns Ok with None for test project and target`` () =
    // Arrange
    "-s solution_root"
    |> TestApp.ParseCommandLine
    // Act
    |> parseArgs
    // Assert
    |> TestApp.Assert_ArgsTarget_IsMissing()
    |> TestApp.Assert_ArgsTestProject_IsMissing()
    |> TestApp.Assert_ArgsSolutionRoot (Some "solution_root")

[<Fact>]
let ``parseArgs with no arguments returns Ok with all None`` () =
    // Arrange
    ""
    |> TestApp.ParseCommandLine
    // Act
    |> parseArgs
    // Assert
    |> TestApp.Assert_ArgsTarget_IsMissing()
    |> TestApp.Assert_ArgsTestProject_IsMissing()
    |> TestApp.Assert_ArgsSolutionRoot_IsMissing()

[<Fact>]
let ``parseArgs with -d CSV multiple directories returns Ok with Directory list`` () =
    // Arrange
    "-t test.fsproj -s solution_root -d dir1,dir2,dir3"
    |> TestApp.ParseCommandLine
    // Act
    |> parseArgs
    // Assert
    |> TestApp.Assert_ArgsTarget_Size 3
    |> TestApp.Assert_ArgsDirectory_Contains ["dir1"; "dir2"; "dir3"]

[<Fact>]
let ``parseArgs with -d CSV with spaces returns Ok with trimmed directories`` () =
    // Arrange
    "-t test.fsproj -s solution_root -d \"dir1 , dir2 , dir3\""
    |> TestApp.ParseCommandLine
    // Act
    |> parseArgs
    // Assert
    |> TestApp.Assert_ArgsTarget_Size 3
    |> TestApp.Assert_ArgsDirectory_Contains ["dir1"; "dir2"; "dir3"]

[<Fact>]
let ``validate with multiple existing directories returns Ok`` () =
    let tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString())
    Directory.CreateDirectory(tempDir) |> ignore

    let testPrj = Path.Combine(tempDir, "test.fsproj")
    let solutionRoot = Path.Combine(tempDir, "solution")
    Directory.CreateDirectory(solutionRoot) |> ignore
    let dir1 = Path.Combine(tempDir, "dir1")
    let dir2 = Path.Combine(tempDir, "dir2")
    Directory.CreateDirectory(dir1) |> ignore
    Directory.CreateDirectory(dir2) |> ignore

    File.WriteAllText(testPrj, "<Project/>")

    try
        match validate testPrj solutionRoot (Directory [dir1; dir2]) with
        | Ok _ -> ()
        | Error msg ->
            Assert.Fail($"Expected Ok, got Error: {msg}")
    finally
        Directory.Delete(tempDir, true)

[<Fact>]
let ``validate with one non-existent directory in list returns Error`` () =
    let tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString())
    Directory.CreateDirectory(tempDir) |> ignore

    let testPrj = Path.Combine(tempDir, "test.fsproj")
    let solutionRoot = Path.Combine(tempDir, "solution")
    Directory.CreateDirectory(solutionRoot) |> ignore
    let dir1 = Path.Combine(tempDir, "dir1")
    let dir2 = Path.Combine(tempDir, "nonexistent")
    Directory.CreateDirectory(dir1) |> ignore

    File.WriteAllText(testPrj, "<Project/>")

    try
        match validate testPrj solutionRoot (Directory [dir1; dir2]) with
        | Error msg ->
            Assert.Contains("Directory not found", msg)
        | Ok _ ->
            Assert.Fail("Expected Error for non-existent directory in list")
    finally
        Directory.Delete(tempDir, true)

[<Fact>]
let ``Directory.GetFiles recursively discovers .fs files in subdirectories`` () =
    let tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString())
    Directory.CreateDirectory(tempDir) |> ignore

    // Create nested structure: tempDir/src/sub1/sub2/
    let srcDir = Path.Combine(tempDir, "src")
    let sub1 = Path.Combine(srcDir, "sub1")
    let sub2 = Path.Combine(sub1, "sub2")
    Directory.CreateDirectory(sub2) |> ignore

    // Create .fs files at different levels
    File.WriteAllText(Path.Combine(srcDir, "top.fs"), "let x = 1")
    File.WriteAllText(Path.Combine(sub1, "mid.fs"), "let y = 2")
    File.WriteAllText(Path.Combine(sub2, "deep.fs"), "let z = 3")
    // Create a non-.fs file that should be excluded
    File.WriteAllText(Path.Combine(sub1, "readme.txt"), "ignore me")

    try
        let files = Directory.GetFiles(srcDir, "*.fs", SearchOption.AllDirectories) |> Array.toList
        Assert.Equal(3, List.length files)
        
        let fileNames = files |> List.map Path.GetFileName |> List.sort
        Assert.Contains("top.fs", fileNames)
        Assert.Contains("mid.fs", fileNames)
        Assert.Contains("deep.fs", fileNames)
        Assert.False(List.contains "readme.txt" fileNames)
    finally
        Directory.Delete(tempDir, true)

[<Fact>]
let ``Directory.GetFiles across multiple directories collects all .fs files`` () =
    let tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString())
    Directory.CreateDirectory(tempDir) |> ignore

    // Create two separate directories with subdirectories
    let dir1 = Path.Combine(tempDir, "project1")
    let dir1Sub = Path.Combine(dir1, "src")
    Directory.CreateDirectory(dir1Sub) |> ignore
    
    let dir2 = Path.Combine(tempDir, "project2")
    let dir2Sub = Path.Combine(dir2, "lib")
    Directory.CreateDirectory(dir2Sub) |> ignore

    // Create .fs files in each directory tree
    File.WriteAllText(Path.Combine(dir1, "main.fs"), "let x = 1")
    File.WriteAllText(Path.Combine(dir1Sub, "util.fs"), "let y = 2")
    File.WriteAllText(Path.Combine(dir2, "core.fs"), "let z = 3")
    File.WriteAllText(Path.Combine(dir2Sub, "helper.fs"), "let w = 4")

    try
        let dirs = [dir1; dir2]
        let files =
            dirs
            |> List.collect (fun d ->
                Directory.GetFiles(d, "*.fs", SearchOption.AllDirectories)
                |> Array.toList
            )
        
        Assert.Equal(4, List.length files)
        
        let fileNames = files |> List.map Path.GetFileName |> List.sort
        Assert.Contains("main.fs", fileNames)
        Assert.Contains("util.fs", fileNames)
        Assert.Contains("core.fs", fileNames)
        Assert.Contains("helper.fs", fileNames)
    finally
        Directory.Delete(tempDir, true)

[<Fact>]
let ``parseArgs with -d single directory wraps in list`` () =
    // Arrange
    "-t test.fsproj -s solution_root -d single_dir"
    |> TestApp.ParseCommandLine
    // Act
    |> parseArgs
    // Assert
    |> TestApp.Assert_ArgsTarget_Size 1
    |> TestApp.Assert_ArgsDirectory_Contains ["single_dir"]
