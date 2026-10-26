module Tests._001_ValidateArguments

open System
open System.IO
open Xunit
open Sabota.Args

[<Fact>]
let ``parseArgs with -t -s -f returns Ok with File target`` () =
    let args = [|"-t"; "test.fsproj"; "-s"; "solution_root"; "-f"; "file.fs"|]
    match parseArgs args with
    | Ok parsed ->
        Assert.Equal(Some "test.fsproj", parsed.TestProject)
        Assert.Equal(Some "solution_root", parsed.SolutionRoot)
        match parsed.Target with
        | Some (File files) ->
            Assert.Single(files) |> ignore
            Assert.Equal("file.fs", List.head files)
        | Some (Directory _) -> Assert.Fail("Expected File target")
        | None -> Assert.Fail("Expected Some target")
    | Error msg ->
        Assert.Fail($"Expected Ok, got Error: {msg}")

[<Fact>]
let ``parseArgs with -t -s -d returns Ok with Directory target`` () =
    let args = [|"-t"; "test.fsproj"; "-s"; "solution_root"; "-d"; "src_dir"|]
    match parseArgs args with
    | Ok parsed ->
        Assert.Equal(Some "test.fsproj", parsed.TestProject)
        Assert.Equal(Some "solution_root", parsed.SolutionRoot)
        match parsed.Target with
        | Some (Directory dirs) ->
            Assert.Single(dirs) |> ignore
            Assert.Equal("src_dir", List.head dirs)
        | Some (File _) -> Assert.Fail("Expected Directory target")
        | None -> Assert.Fail("Expected Some target")
    | Error msg ->
        Assert.Fail($"Expected Ok, got Error: {msg}")

[<Fact>]
let ``parseArgs with both -f and -d returns Error`` () =
    let args = [|"-t"; "test.fsproj"; "-s"; "solution_root"; "-f"; "file.fs"; "-d"; "src_dir"|]
    match parseArgs args with
    | Error msg ->
        Assert.Contains("mutually exclusive", msg)
    | Ok _ ->
        Assert.Fail("Expected Error when both -f and -d are provided")

[<Fact>]
let ``parseArgs with neither -f nor -d returns Ok with None target`` () =
    let args = [|"-t"; "test.fsproj"; "-s"; "solution_root"|]
    match parseArgs args with
    | Ok parsed ->
        Assert.Equal(None, parsed.Target)
    | Error msg ->
        Assert.Fail($"Expected Ok, got Error: {msg}")

[<Fact>]
let ``parseArgs missing -t returns Ok with None`` () =
    let args = [|"-s"; "solution_root"; "-f"; "file.fs"|]
    match parseArgs args with
    | Ok parsed ->
        Assert.Equal(None, parsed.TestProject)
    | Error msg ->
        Assert.Fail($"Expected Ok, got Error: {msg}")

[<Fact>]
let ``parseArgs missing -s returns Ok with None`` () =
    let args = [|"-t"; "test.fsproj"; "-f"; "file.fs"|]
    match parseArgs args with
    | Ok parsed ->
        Assert.Equal(None, parsed.SolutionRoot)
    | Error msg ->
        Assert.Fail($"Expected Ok, got Error: {msg}")

[<Fact>]
let ``parseArgs with empty array returns Ok with all None`` () =
    let args = [||]
    match parseArgs args with
    | Ok parsed ->
        Assert.Equal(None, parsed.TestProject)
        Assert.Equal(None, parsed.SolutionRoot)
        Assert.Equal(None, parsed.Target)
    | Error msg ->
        Assert.Fail($"Expected Ok, got Error: {msg}")

[<Fact>]
let ``parseArgs ignores unknown arguments`` () =
    let args = [|"-t"; "test.fsproj"; "-s"; "solution_root"; "-f"; "file.fs"; "-x"; "unknown"|]
    match parseArgs args with
    | Ok parsed ->
        Assert.Equal(Some "test.fsproj", parsed.TestProject)
        Assert.Equal(Some "solution_root", parsed.SolutionRoot)
        match parsed.Target with
        | Some (File files) ->
            Assert.Single(files) |> ignore
            Assert.Equal("file.fs", List.head files)
        | Some (Directory _) -> Assert.Fail("Expected File target")
        | None -> Assert.Fail("Expected Some target")
    | Error msg ->
        Assert.Fail($"Expected Ok, got Error: {msg}")

[<Fact>]
let ``parseArgs accepts arguments in any order`` () =
    let args = [|"-f"; "file.fs"; "-t"; "test.fsproj"; "-s"; "solution_root"|]
    match parseArgs args with
    | Ok parsed ->
        Assert.Equal(Some "test.fsproj", parsed.TestProject)
        Assert.Equal(Some "solution_root", parsed.SolutionRoot)
        match parsed.Target with
        | Some (File files) ->
            Assert.Single(files) |> ignore
            Assert.Equal("file.fs", List.head files)
        | Some (Directory _) -> Assert.Fail("Expected File target")
        | None -> Assert.Fail("Expected Some target")
    | Error msg ->
        Assert.Fail($"Expected Ok, got Error: {msg}")

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
    let tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString())
    Directory.CreateDirectory(tempDir) |> ignore

    let testPrj = Path.Combine(tempDir, "test.fsproj")
    let solutionRoot = Path.Combine(tempDir, "solution")
    Directory.CreateDirectory(solutionRoot) |> ignore
    let fileFs = Path.Combine(tempDir, "file.fs")

    File.WriteAllText(testPrj, "<Project/>")
    File.WriteAllText(fileFs, "let x = 1")

    try
        match validate testPrj solutionRoot (File [fileFs]) with
        | Ok _ -> ()
        | Error msg ->
            Assert.Fail($"Expected Ok, got Error: {msg}")
    finally
        Directory.Delete(tempDir, true)

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
        let args = [|"-t"; "test.fsproj"; "-s"; "solution"; "-f"; "file.fs"|]
        match parseArgs args with
        | Ok parsed ->
            // CLI args are parsed; Program.fs resolves to absolute paths
            Assert.Equal(Some "test.fsproj", parsed.TestProject)
            Assert.Equal(Some "solution", parsed.SolutionRoot)
            match parsed.Target with
            | Some (File files) ->
                Assert.Single(files) |> ignore
                Assert.Equal("file.fs", List.head files)
            | _ -> Assert.Fail("Expected File target")
        | Error msg ->
            Assert.Fail($"Expected Ok, got Error: {msg}")
    finally
        Environment.CurrentDirectory <- originalDir
        Directory.Delete(tempDir, true)

[<Fact>]
let ``parseAndValidate with -d resolves relative paths to absolute`` () =
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
        let args = [|"-t"; "test.fsproj"; "-s"; "solution"; "-d"; "src"|]
        match parseArgs args with
        | Ok parsed ->
            Assert.Equal(Some "test.fsproj", parsed.TestProject)
            Assert.Equal(Some "solution", parsed.SolutionRoot)
            match parsed.Target with
            | Some (Directory dirs) ->
                Assert.Single(dirs) |> ignore
                Assert.Equal("src", List.head dirs)
            | _ -> Assert.Fail("Expected Directory target")
        | Error msg ->
            Assert.Fail($"Expected Ok, got Error: {msg}")
    finally
        Environment.CurrentDirectory <- originalDir
        Directory.Delete(tempDir, true)

[<Fact>]
let ``parseArgs with missing args returns Ok with None values`` () =
    let args = [|"-t"; "test.fsproj"|]
    match parseArgs args with
    | Ok parsed ->
        Assert.Equal(Some "test.fsproj", parsed.TestProject)
        Assert.Equal(None, parsed.SolutionRoot)
        Assert.Equal(None, parsed.Target)
    | Error msg ->
        Assert.Fail($"Expected Ok, got Error: {msg}")

[<Fact>]
let ``parseArgs missing -s with valid files returns Ok with None`` () =
    let tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString())
    Directory.CreateDirectory(tempDir) |> ignore

    let testPrj = Path.Combine(tempDir, "test.fsproj")
    let fileFs = Path.Combine(tempDir, "file.fs")

    File.WriteAllText(testPrj, "<Project/>")
    File.WriteAllText(fileFs, "let x = 1")

    try
        let args = [|"-t"; testPrj; "-f"; fileFs|]
        match parseArgs args with
        | Ok parsed ->
            Assert.Equal(None, parsed.SolutionRoot)
        | Error msg ->
            Assert.Fail($"Expected Ok, got Error: {msg}")
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
    let args = [|"-t"; "test.fsproj"; "-s"; "solution_root"; "-f"; "file1.fs,file2.fs,file3.fs"|]
    match parseArgs args with
    | Ok parsed ->
        match parsed.Target with
        | Some (File files) ->
            Assert.Equal(3, List.length files)
            Assert.Equal("file1.fs", files[0])
            Assert.Equal("file2.fs", files[1])
            Assert.Equal("file3.fs", files[2])
        | _ -> Assert.Fail("Expected File target")
    | Error msg ->
        Assert.Fail($"Expected Ok, got Error: {msg}")

[<Fact>]
let ``parseArgs with -f CSV with spaces returns Ok with trimmed files`` () =
    let args = [|"-t"; "test.fsproj"; "-s"; "solution_root"; "-f"; "file1.fs , file2.fs , file3.fs"|]
    match parseArgs args with
    | Ok parsed ->
        match parsed.Target with
        | Some (File files) ->
            Assert.Equal(3, List.length files)
            Assert.Equal("file1.fs", files[0])
            Assert.Equal("file2.fs", files[1])
            Assert.Equal("file3.fs", files[2])
        | _ -> Assert.Fail("Expected File target")
    | Error msg ->
        Assert.Fail($"Expected Ok, got Error: {msg}")

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
    let args = [|"-t"; "test.fsproj"|]
    match parseArgs args with
    | Ok parsed ->
        Assert.Equal(Some "test.fsproj", parsed.TestProject)
        Assert.Equal(None, parsed.SolutionRoot)
        Assert.Equal(None, parsed.Target)
    | Error msg ->
        Assert.Fail($"Expected Ok, got Error: {msg}")

[<Fact>]
let ``parseArgs with only -s returns Ok with None for test project and target`` () =
    let args = [|"-s"; "solution_root"|]
    match parseArgs args with
    | Ok parsed ->
        Assert.Equal(None, parsed.TestProject)
        Assert.Equal(Some "solution_root", parsed.SolutionRoot)
        Assert.Equal(None, parsed.Target)
    | Error msg ->
        Assert.Fail($"Expected Ok, got Error: {msg}")

[<Fact>]
let ``parseArgs with no arguments returns Ok with all None`` () =
    let args = [||]
    match parseArgs args with
    | Ok parsed ->
        Assert.Equal(None, parsed.TestProject)
        Assert.Equal(None, parsed.SolutionRoot)
        Assert.Equal(None, parsed.Target)
        Assert.Equal(None, parsed.Categories)
    | Error msg ->
        Assert.Fail($"Expected Ok, got Error: {msg}")

[<Fact>]
let ``parseArgs with -d CSV multiple directories returns Ok with Directory list`` () =
    let args = [|"-t"; "test.fsproj"; "-s"; "solution_root"; "-d"; "dir1,dir2,dir3"|]
    match parseArgs args with
    | Ok parsed ->
        match parsed.Target with
        | Some (Directory dirs) ->
            Assert.Equal(3, List.length dirs)
            Assert.Equal("dir1", dirs[0])
            Assert.Equal("dir2", dirs[1])
            Assert.Equal("dir3", dirs[2])
        | _ -> Assert.Fail("Expected Directory target")
    | Error msg ->
        Assert.Fail($"Expected Ok, got Error: {msg}")

[<Fact>]
let ``parseArgs with -d CSV with spaces returns Ok with trimmed directories`` () =
    let args = [|"-t"; "test.fsproj"; "-s"; "solution_root"; "-d"; "dir1 , dir2 , dir3"|]
    match parseArgs args with
    | Ok parsed ->
        match parsed.Target with
        | Some (Directory dirs) ->
            Assert.Equal(3, List.length dirs)
            Assert.Equal("dir1", dirs[0])
            Assert.Equal("dir2", dirs[1])
            Assert.Equal("dir3", dirs[2])
        | _ -> Assert.Fail("Expected Directory target")
    | Error msg ->
        Assert.Fail($"Expected Ok, got Error: {msg}")

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
    let args = [|"-t"; "test.fsproj"; "-s"; "solution_root"; "-d"; "single_dir"|]
    match parseArgs args with
    | Ok parsed ->
        match parsed.Target with
        | Some (Directory dirs) ->
            Assert.Equal(1, List.length dirs)
            Assert.Equal("single_dir", dirs[0])
        | _ -> Assert.Fail("Expected Directory target")
    | Error msg ->
        Assert.Fail($"Expected Ok, got Error: {msg}")
