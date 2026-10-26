#nowarn "FS3261"

module Tests._001_ValidateArguments

open System
open System.IO
open Xunit
open Sabota.Args
open TestApp

[<Fact>]
let ``01. when command line specifies test project, solution root, and file to mutate, requirements are satisfied`` () =
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
let ``02. when command line specifies test project, solution root, and directory to mutate, requirements are satisfied`` () =
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
let ``03. when both file and directory targets are specified, conflict is reported`` () =
    // Arrange
    "-t test.fsproj -s solution_root -f file.fs -d src_dir"
    |> TestApp.ParseCommandLine
    // Act
    |> parseArgs
    // Assert
    |> TestApp.Assert_ArgsError_Contains "mutually exclusive"

[<Fact>]
let ``04. when no mutation target is specified, parse succeeds with missing target`` () =
    // Arrange
    "-t test.fsproj -s solution_root"
    |> TestApp.ParseCommandLine
    // Act
    |> parseArgs
    // Assert
    |> TestApp.Assert_ArgsTarget_IsMissing()

[<Fact>]
let ``05. when test project is omitted, parse succeeds without it`` () =
    // Arrange
    "-s solution_root -f file.fs"
    |> TestApp.ParseCommandLine
    // Act
    |> parseArgs
    // Assert
    |> TestApp.Assert_ArgsTarget_Size 1
    |> TestApp.Assert_ArgsTestProject None

[<Fact>]
let ``06. when solution root is omitted, parse succeeds without it`` () =
    // Arrange
    "-t test.fsproj -f file.fs"
    |> TestApp.ParseCommandLine
    // Act
    |> parseArgs
    // Assert
    |> TestApp.Assert_ArgsTarget_Size 1
    |> TestApp.Assert_ArgsSolutionRoot None

[<Fact>]
let ``07. when no arguments are provided, parse succeeds with all values missing`` () =
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
let ``08. when unknown arguments are present, they are ignored`` () =
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
let ``09. when arguments are in different order, all are recognized`` () =
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
let ``10. when specified files do not exist, validation fails`` () =
    // Arrange & Act
    validate "C:\\nonexistent\\test.fsproj" "C:\\nonexistent\\solution" (File ["C:\\nonexistent\\file.fs"])
    // Assert
    |> TestApp.Assert_ValidateError_Contains "not found"

[<Fact>]
let ``11. when specified files exist, validation passes`` () =
    // Arrange
    let baseDir = TestApp.CreateSimulatedBaseDir()
    try
        // Act
        let env = TestApp.CreateSimulatedEnvironment(Stub.TestProjectName, Stub.SolutionRootName, [Stub.FileFsName], [], baseDir)
        validate env.TestProject env.SolutionRoot (File [env.FileFs])
        // Assert
        |> TestApp.Assert_MinimalRequirementsMet()
    finally
        TestApp.CleanSimulatedEnvironment baseDir

[<Fact>]
let ``12. when a file has wrong extension, validation requires .fs`` () =
    // Arrange
    let baseDir = TestApp.CreateSimulatedBaseDir()
    try
        let env = TestApp.CreateSimulatedEnvironment(Stub.TestProjectName, Stub.SolutionRootName, [Stub.FileCsName], [], baseDir)
        // Act
        validate env.TestProject env.SolutionRoot (File env.Files)
        // Assert
        |> TestApp.Assert_ValidateError_Contains ".fs"
    finally
        TestApp.CleanSimulatedEnvironment baseDir

[<Fact>]
let ``13. when test project has wrong extension, validation requires .fsproj`` () =
    // Arrange
    let baseDir = TestApp.CreateSimulatedBaseDir()
    try
        let env = TestApp.CreateSimulatedEnvironment("test.csproj", Stub.SolutionRootName, [Stub.FileFsName], [], baseDir)
        // Act
        validate env.TestProject env.SolutionRoot (File env.Files)
        // Assert
        |> TestApp.Assert_ValidateError_Contains ".fsproj"
    finally
        TestApp.CleanSimulatedEnvironment baseDir

[<Fact>]
let ``14. when specified directory exists, validation passes`` () =
    // Arrange
    let baseDir = TestApp.CreateSimulatedBaseDir()
    try
        let env = TestApp.CreateSimulatedEnvironment(Stub.TestProjectName, Stub.SolutionRootName, [], [Stub.DirSrcName], baseDir)
        // Act
        validate env.TestProject env.SolutionRoot (Directory env.Directories)
        // Assert
        |> TestApp.Assert_MinimalRequirementsMet()
    finally
        TestApp.CleanSimulatedEnvironment baseDir

[<Fact>]
let ``15. when specified directory does not exist, validation fails`` () =
    // Arrange
    let baseDir = TestApp.CreateSimulatedBaseDir()
    try
        let env = TestApp.CreateSimulatedEnvironment(Stub.TestProjectName, Stub.SolutionRootName, [], [], baseDir)
        let nonExistentDir = Path.Combine(baseDir, Stub.NonExistentDirName)
        // Act
        validate env.TestProject env.SolutionRoot (Directory [nonExistentDir])
        // Assert
        |> TestApp.Assert_ValidateError_Contains "Directory not found"
    finally
        TestApp.CleanSimulatedEnvironment baseDir

[<Fact>]
let ``16. when file target is a relative path, it is recognized`` () =
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
let ``17. when directory target is a relative path, it is recognized`` () =
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
let ``18. when only test project is provided, solution and target are missing`` () =
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
let ``19. when solution root is missing but files are valid, parse succeeds`` () =
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
let ``20. when paths do not exist on disk, validation reports them as not found`` () =
    // Arrange & Act
    validate "C:\\no\\test.fsproj" "C:\\no\\solution" (File ["C:\\no\\file.fs"])
    // Assert
    |> TestApp.Assert_ValidateError_Contains "not found"

[<Fact>]
let ``21. when multiple files are comma-separated, all are recognized`` () =
    // Arrange
    "-t test.fsproj -s solution_root -f file1.fs,file2.fs,file3.fs"
    |> TestApp.ParseCommandLine
    // Act
    |> parseArgs
    // Assert
    |> TestApp.Assert_ArgsTarget_Size 3
    |> TestApp.Assert_ArgsTarget_Contains ["file1.fs"; "file2.fs"; "file3.fs"]

[<Fact>]
let ``22. when comma-separated files contain spaces, they are trimmed`` () =
    // Arrange
    "-t test.fsproj -s solution_root -f \"file1.fs , file2.fs , file3.fs\""
    |> TestApp.ParseCommandLine
    // Act
    |> parseArgs
    // Assert
    |> TestApp.Assert_ArgsTarget_Size 3
    |> TestApp.Assert_ArgsTarget_Contains ["file1.fs"; "file2.fs"; "file3.fs"]

[<Fact>]
let ``23. when multiple files are specified, all must exist`` () =
    // Arrange
    let baseDir = TestApp.CreateSimulatedBaseDir()
    try
        let env = TestApp.CreateSimulatedEnvironment(Stub.TestProjectName, Stub.SolutionRootName, [Stub.File1FsName; Stub.File2FsName], [], baseDir)
        // Act
        validate env.TestProject env.SolutionRoot (File env.Files)
        // Assert
        |> TestApp.Assert_MinimalRequirementsMet()
    finally
        TestApp.CleanSimulatedEnvironment baseDir

[<Fact>]
let ``24. when one file in a list does not exist, validation fails`` () =
    // Arrange
    let baseDir = TestApp.CreateSimulatedBaseDir()
    try
        let env = TestApp.CreateSimulatedEnvironment(Stub.TestProjectName, Stub.SolutionRootName, [Stub.File1FsName], [], baseDir)
        let nonExistentFile = Path.Combine(baseDir, Stub.NonExistentFsName)
        // Act
        validate env.TestProject env.SolutionRoot (File (env.Files @ [nonExistentFile]))
        // Assert
        |> TestApp.Assert_ValidateError_Contains "File not found"
    finally
        TestApp.CleanSimulatedEnvironment baseDir

[<Fact>]
let ``25. when one file in a list has wrong extension, validation fails`` () =
    // Arrange
    let baseDir = TestApp.CreateSimulatedBaseDir()
    try
        let env = TestApp.CreateSimulatedEnvironment(Stub.TestProjectName, Stub.SolutionRootName, [Stub.File1FsName; Stub.FileCsName], [], baseDir)
        // Act
        validate env.TestProject env.SolutionRoot (File env.Files)
        // Assert
        |> TestApp.Assert_ValidateError_Contains ".fs"
    finally
        TestApp.CleanSimulatedEnvironment baseDir

[<Fact>]
let ``26. when only test project is given, solution and target are absent`` () =
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
let ``27. when only solution root is given, test project and target are absent`` () =
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
let ``28. when command line is empty, all configuration values are absent`` () =
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
let ``29. when multiple directories are comma-separated, all are recognized`` () =
    // Arrange
    "-t test.fsproj -s solution_root -d dir1,dir2,dir3"
    |> TestApp.ParseCommandLine
    // Act
    |> parseArgs
    // Assert
    |> TestApp.Assert_ArgsTarget_Size 3
    |> TestApp.Assert_ArgsDirectory_Contains ["dir1"; "dir2"; "dir3"]

[<Fact>]
let ``30. when comma-separated directories contain spaces, they are trimmed`` () =
    // Arrange
    "-t test.fsproj -s solution_root -d \"dir1 , dir2 , dir3\""
    |> TestApp.ParseCommandLine
    // Act
    |> parseArgs
    // Assert
    |> TestApp.Assert_ArgsTarget_Size 3
    |> TestApp.Assert_ArgsDirectory_Contains ["dir1"; "dir2"; "dir3"]

[<Fact>]
let ``31. when multiple directories exist, validation passes`` () =
    // Arrange
    let baseDir = TestApp.CreateSimulatedBaseDir()
    try
        let env = TestApp.CreateSimulatedEnvironment(Stub.TestProjectName, Stub.SolutionRootName, [], [Stub.Dir1Name; Stub.Dir2Name], baseDir)
        // Act
        validate env.TestProject env.SolutionRoot (Directory env.Directories)
        // Assert
        |> TestApp.Assert_MinimalRequirementsMet()
    finally
        TestApp.CleanSimulatedEnvironment baseDir

[<Fact>]
let ``32. when one directory in a list does not exist, validation fails`` () =
    // Arrange
    let baseDir = TestApp.CreateSimulatedBaseDir()
    try
        let env = TestApp.CreateSimulatedEnvironment(Stub.TestProjectName, Stub.SolutionRootName, [], [Stub.Dir1Name], baseDir)
        let nonExistentDir = Path.Combine(baseDir, Stub.NonExistentDirName)
        // Act
        validate env.TestProject env.SolutionRoot (Directory (env.Directories @ [nonExistentDir]))
        // Assert
        |> TestApp.Assert_ValidateError_Contains "Directory not found"
    finally
        TestApp.CleanSimulatedEnvironment baseDir

[<Fact>]
let ``33. when searching a directory tree, .fs files in nested folders are found`` () =
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
let ``34. when searching multiple directory trees, all .fs files are collected`` () =
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
let ``35. when a single directory is specified, it forms a list of one`` () =
    // Arrange
    "-t test.fsproj -s solution_root -d single_dir"
    |> TestApp.ParseCommandLine
    // Act
    |> parseArgs
    // Assert
    |> TestApp.Assert_ArgsTarget_Size 1
    |> TestApp.Assert_ArgsDirectory_Contains ["single_dir"]
