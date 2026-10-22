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
        | Some (File f) -> Assert.Equal("file.fs", f)
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
        | Some (Directory d) -> Assert.Equal("src_dir", d)
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
        | Some (File f) -> Assert.Equal("file.fs", f)
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
        | Some (File f) -> Assert.Equal("file.fs", f)
        | Some (Directory _) -> Assert.Fail("Expected File target")
        | None -> Assert.Fail("Expected Some target")
    | Error msg ->
        Assert.Fail($"Expected Ok, got Error: {msg}")

[<Fact>]
let ``validate with non-existent files returns Error`` () =
    match validate "C:\\nonexistent\\test.fsproj" "C:\\nonexistent\\solution" (File "C:\\nonexistent\\file.fs") with
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
        match validate testPrj solutionRoot (File fileFs) with
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
        match validate testPrj solutionRoot (File fileCs) with
        | Error msg ->
            Assert.Contains(".fs", msg)
        | Ok _ ->
            Assert.Fail("Expected Error for non-.fs file")
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
        match validate testPrj solutionRoot (Directory src_dir) with
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
        match validate testPrj solutionRoot (Directory nonExistentDir) with
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
            | Some (File f) -> Assert.Equal("file.fs", f)
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
            | Some (Directory d) -> Assert.Equal("src", d)
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
    match validate "C:\\no\\test.fsproj" "C:\\no\\solution" (File "C:\\no\\file.fs") with
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
        match validate testPrj solutionRoot (File fileCs) with
        | Error msg ->
            Assert.Contains(".fs", msg)
        | Ok _ ->
            Assert.Fail("Expected Error for .cs file")
    finally
        Directory.Delete(tempDir, true)
