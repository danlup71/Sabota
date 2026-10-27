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
    TestApp.Validate "C:\\nonexistent\\test.fsproj" "C:\\nonexistent\\solution" (File ["C:\\nonexistent\\file.fs"])
    // Assert
    |> TestApp.Assert_ValidateError_Contains "not found"

[<Fact>]
let ``11. when specified files exist, validation passes`` () =
    // Arrange
    let baseDir = TestApp.CreateSimulatedBaseDir()
    try
        // Act
        let env = TestApp.CreateSimulatedEnvironment(Stub.TestProjectName, Stub.SolutionRootName, [Stub.FileFsName], [], baseDir)
        TestApp.Validate env.TestProject env.SolutionRoot (File [env.FileFs])
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
        TestApp.Validate env.TestProject env.SolutionRoot (File env.Files)
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
        TestApp.Validate env.TestProject env.SolutionRoot (File env.Files)
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
        TestApp.Validate env.TestProject env.SolutionRoot (Directory env.Directories)
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
        TestApp.Validate env.TestProject env.SolutionRoot (Directory [nonExistentDir])
        // Assert
        |> TestApp.Assert_ValidateError_Contains "Directory not found"
    finally
        TestApp.CleanSimulatedEnvironment baseDir

[<Fact>]
let ``16. when file target is a relative path, it is recognized`` () =
    // Arrange
    let baseDir = TestApp.CreateSimulatedBaseDir()
    TestApp.CreateSimulatedEnvironment(Stub.TestProjectName, Stub.SolutionRootName, [Stub.FileFsName], [], baseDir) |> ignore
    let restore = TestApp.SetCurrentDirectory baseDir
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
        restore()
        TestApp.CleanSimulatedEnvironment baseDir

[<Fact>]
let ``17. when directory target is a relative path, it is recognized`` () =
    // Arrange
    let baseDir = TestApp.CreateSimulatedBaseDir()
    TestApp.CreateSimulatedEnvironment(Stub.TestProjectName, Stub.SolutionRootName, [], [Stub.DirSrcName], baseDir) |> ignore
    let restore = TestApp.SetCurrentDirectory baseDir
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
        restore()
        TestApp.CleanSimulatedEnvironment baseDir

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
    let baseDir = TestApp.CreateSimulatedBaseDir()
    let env = TestApp.CreateSimulatedEnvironment(Stub.TestProjectName, "", [Stub.FileFsName], [], baseDir)
    try
        $"-t %s{env.TestProject} -f %s{env.FileFs}"
        |> TestApp.ParseCommandLine
        // Act
        |> parseArgs
        // Assert
        |> TestApp.Assert_ArgsTarget_Size 1
        |> TestApp.Assert_ArgsSolutionRoot None
    finally
        TestApp.CleanSimulatedEnvironment baseDir

[<Fact>]
let ``20. when paths do not exist on disk, validation reports them as not found`` () =
    // Arrange & Act
    TestApp.Validate "C:\\no\\test.fsproj" "C:\\no\\solution" (File ["C:\\no\\file.fs"])
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
        TestApp.Validate env.TestProject env.SolutionRoot (File env.Files)
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
        TestApp.Validate env.TestProject env.SolutionRoot (File (env.Files @ [nonExistentFile]))
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
        TestApp.Validate env.TestProject env.SolutionRoot (File env.Files)
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
        TestApp.Validate env.TestProject env.SolutionRoot (Directory env.Directories)
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
        TestApp.Validate env.TestProject env.SolutionRoot (Directory (env.Directories @ [nonExistentDir]))
        // Assert
        |> TestApp.Assert_ValidateError_Contains "Directory not found"
    finally
        TestApp.CleanSimulatedEnvironment baseDir

[<Fact>]
let ``33. when searching a directory tree, .fs files in nested folders are found`` () =
    // Arrange
    let baseDir = TestApp.CreateSimulatedBaseDir()
    let srcDir = Path.Combine(baseDir, "src")
    TestApp.CreateNestedFsFiles(srcDir, [("", "top.fs"); ("sub1", "mid.fs"); ("sub1/sub2", "deep.fs")]) |> ignore
    TestApp.CreateNestedOtherFiles(srcDir, [("sub1", "readme.txt")]) |> ignore
    try
        // Act
        TestApp.DiscoverFsFiles(srcDir)
        // Assert
        |> TestApp.Assert_FileCount 3
        |> TestApp.Assert_FileNamesContain ["top.fs"; "mid.fs"; "deep.fs"]
        |> TestApp.Assert_FileNamesNotContain "readme.txt"
    finally
        TestApp.CleanSimulatedEnvironment baseDir

[<Fact>]
let ``34. when searching multiple directory trees, all .fs files are collected`` () =
    // Arrange
    let baseDir = TestApp.CreateSimulatedBaseDir()
    let dir1 = Path.Combine(baseDir, "project1")
    let dir2 = Path.Combine(baseDir, "project2")
    TestApp.CreateNestedFsFiles(dir1, [("", "main.fs"); ("src", "util.fs")]) |> ignore
    TestApp.CreateNestedFsFiles(dir2, [("", "core.fs"); ("lib", "helper.fs")]) |> ignore
    try
        // Act
        TestApp.DiscoverFsFilesInDirs [dir1; dir2]
        // Assert
        |> TestApp.Assert_FileCount 4
        |> TestApp.Assert_FileNamesContain ["main.fs"; "util.fs"; "core.fs"; "helper.fs"]
    finally
        TestApp.CleanSimulatedEnvironment baseDir

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
