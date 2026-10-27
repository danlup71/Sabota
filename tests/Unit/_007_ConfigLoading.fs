module _007_ConfigLoading

open System
open System.IO
open Xunit
open Sabota.MutationTypes
open TestApp


[<Fact>]
let ``01. when config file does not exist, returns empty defaults`` () =
    // Arrange
    let nonExistentPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString(), "nonexistent.json")

    // Act & Assert
    nonExistentPath
    |> Sabota.Config.loadConfig
    |> TestApp.Assert_Config_TestProject ""
    |> TestApp.Assert_Config_SolutionRoot ""
    |> TestApp.Assert_Config_File None
    |> TestApp.Assert_Config_Directory None
    |> TestApp.Assert_Config_Categories_Count 9
    |> ignore


[<Fact>]
let ``02. when config has relative test project path, it is resolved to absolute`` () =
    // Arrange
    let baseDir = TestApp.CreateSimulatedBaseDir()
    let configDir = Path.Combine(baseDir, Stub.ConfigSubDirName)
    Directory.CreateDirectory(configDir) |> ignore
    let resourcesDir = Path.Combine(baseDir, Stub.ResourcesDirName)
    Directory.CreateDirectory(resourcesDir) |> ignore
    let testProjectFile = Path.Combine(resourcesDir, Stub.TestProjectName)
    File.WriteAllText(testProjectFile, "<Project/>") |> ignore
    try
        let configPath = TestApp.CreateConfigFile(
            configDir, Stub.ConfigFileName,
            "../" + Stub.ResourcesDirName + "/" + Stub.TestProjectName,
            "../" + Stub.ResourcesDirName,
            None, None,
            [("Equality", true)]
        )
        let expectedTestProject = Path.GetFullPath(testProjectFile)

        // Act
        let config = Sabota.Config.loadConfig configPath

        // Assert
        config
        |> TestApp.Assert_Config_TestProject expectedTestProject
        |> ignore
    finally
        TestApp.CleanSimulatedEnvironment baseDir


[<Fact>]
let ``03. when config has relative solution root path, it is resolved to absolute`` () =
    // Arrange
    let baseDir = TestApp.CreateSimulatedBaseDir()
    let configDir = Path.Combine(baseDir, Stub.ConfigSubDirName)
    Directory.CreateDirectory(configDir) |> ignore
    let resourcesDir = Path.Combine(baseDir, Stub.ResourcesDirName)
    Directory.CreateDirectory(resourcesDir) |> ignore
    let testProjectFile = Path.Combine(resourcesDir, Stub.TestProjectName)
    File.WriteAllText(testProjectFile, "<Project/>") |> ignore
    try
        let configPath = TestApp.CreateConfigFile(
            configDir, Stub.ConfigFileName,
            "../" + Stub.ResourcesDirName + "/" + Stub.TestProjectName,
            "../" + Stub.ResourcesDirName,
            None, None,
            [("Equality", true)]
        )
        let expectedSolutionRoot = Path.GetFullPath(resourcesDir)

        // Act
        let config = Sabota.Config.loadConfig configPath

        // Assert
        config
        |> TestApp.Assert_Config_SolutionRoot expectedSolutionRoot
        |> ignore
    finally
        TestApp.CleanSimulatedEnvironment baseDir


[<Fact>]
let ``04. when config has parent directory traversal paths, they are normalized`` () =
    // Arrange
    let baseDir = TestApp.CreateSimulatedBaseDir()
    let configDir = Path.Combine(baseDir, Stub.ConfigSubDirName)
    Directory.CreateDirectory(configDir) |> ignore
    let resourcesDir = Path.Combine(baseDir, Stub.ResourcesDirName)
    Directory.CreateDirectory(resourcesDir) |> ignore
    let unitTestDir = Path.Combine(resourcesDir, Stub.UnitTestDirName)
    Directory.CreateDirectory(unitTestDir) |> ignore
    let testProjectFile = Path.Combine(unitTestDir, Stub.TestProjectName)
    File.WriteAllText(testProjectFile, "<Project/>") |> ignore
    try
        let configPath = TestApp.CreateConfigFile(
            configDir, Stub.ConfigFileName,
            "../../" + Stub.ResourcesDirName + "/" + Stub.UnitTestDirName + "/" + Stub.TestProjectName,
            "../../" + Stub.ResourcesDirName,
            None, None,
            [("Equality", true)]
        )

        // Act
        let config = Sabota.Config.loadConfig configPath

        // Assert - paths should not contain '..'
        config.TestProject
        |> TestApp.Assert_Path_NoDotDot
        |> TestApp.Assert_Path_IsAbsolute
        |> ignore

        config.SolutionRoot
        |> TestApp.Assert_Path_NoDotDot
        |> TestApp.Assert_Path_IsAbsolute
        |> ignore
    finally
        TestApp.CleanSimulatedEnvironment baseDir


[<Fact>]
let ``05. when config has absolute paths, they are preserved and normalized`` () =
    // Arrange
    let baseDir = TestApp.CreateSimulatedBaseDir()
    let resourcesDir = Path.Combine(baseDir, Stub.ResourcesDirName)
    Directory.CreateDirectory(resourcesDir) |> ignore
    let testProjectFile = Path.Combine(resourcesDir, Stub.TestProjectName)
    File.WriteAllText(testProjectFile, "<Project/>") |> ignore
    try
        let configPath = TestApp.CreateConfigFile(
            baseDir, Stub.ConfigFileName,
            testProjectFile,
            resourcesDir,
            None, None,
            [("Equality", true)]
        )

        // Act
        let config = Sabota.Config.loadConfig configPath

        // Assert
        config
        |> TestApp.Assert_Config_TestProject testProjectFile
        |> TestApp.Assert_Config_SolutionRoot resourcesDir
        |> ignore
    finally
        TestApp.CleanSimulatedEnvironment baseDir


[<Fact>]
let ``06. when config has empty file and directory fields, they are None`` () =
    // Arrange
    let baseDir = TestApp.CreateSimulatedBaseDir()
    let resourcesDir = Path.Combine(baseDir, Stub.ResourcesDirName)
    Directory.CreateDirectory(resourcesDir) |> ignore
    let testProjectFile = Path.Combine(resourcesDir, Stub.TestProjectName)
    File.WriteAllText(testProjectFile, "<Project/>") |> ignore
    try
        let configPath = TestApp.CreateConfigFile(
            baseDir, Stub.ConfigFileName,
            Stub.TestProjectName,
            "",
            None, None,
            [("Equality", true)]
        )

        // Act
        let config = Sabota.Config.loadConfig configPath

        // Assert
        config
        |> TestApp.Assert_Config_File None
        |> TestApp.Assert_Config_Directory None
        |> ignore
    finally
        TestApp.CleanSimulatedEnvironment baseDir


[<Fact>]
let ``07. when config has enabled categories, they are loaded`` () =
    // Arrange
    let baseDir = TestApp.CreateSimulatedBaseDir()
    let testProjectFile = Path.Combine(baseDir, Stub.TestProjectName)
    File.WriteAllText(testProjectFile, "<Project/>") |> ignore
    try
        let configPath = TestApp.CreateConfigFile(
            baseDir, Stub.ConfigFileName,
            Stub.TestProjectName,
            "",
            None, None,
            [("Equality", true); ("Boolean", true); ("Arithmetic", false)]
        )

        // Act
        let config = Sabota.Config.loadConfig configPath

        // Assert
        config
        |> TestApp.Assert_Config_Categories_Contains MutationCategory.Equality
        |> TestApp.Assert_Config_Categories_Contains MutationCategory.Boolean
        |> (fun c ->
            Assert.False(List.contains MutationCategory.Arithmetic c.Categories, "Arithmetic should not be in categories")
            c)
        |> ignore
    finally
        TestApp.CleanSimulatedEnvironment baseDir


[<Fact>]
let ``08. when config has all categories enabled, all are loaded`` () =
    // Arrange
    let baseDir = TestApp.CreateSimulatedBaseDir()
    let testProjectFile = Path.Combine(baseDir, Stub.TestProjectName)
    File.WriteAllText(testProjectFile, "<Project/>") |> ignore
    try
        let configPath = TestApp.CreateConfigFile(
            baseDir, Stub.ConfigFileName,
            Stub.TestProjectName,
            "",
            None, None,
            [("Equality", true); ("RelationalBoundary", true); ("RelationalReversal", true);
             ("Boolean", true); ("Arithmetic", true); ("Conditional", true);
             ("Option", true); ("ReturnListValue", true); ("ControlFlow", true)]
        )

        // Act
        let config = Sabota.Config.loadConfig configPath

        // Assert
        config
        |> TestApp.Assert_Config_Categories_Count 9
        |> ignore
    finally
        TestApp.CleanSimulatedEnvironment baseDir


[<Fact>]
let ``09. when resolvePathRelativeToConfig receives relative path, it returns absolute path`` () =
    // Arrange
    let baseDir = TestApp.CreateSimulatedBaseDir()
    let configPath = Path.Combine(baseDir, Stub.ConfigFileName)
    File.WriteAllText(configPath, "{}") |> ignore
    let relativePath = "../" + Stub.ResourcesDirName + "/" + Stub.TestProjectName
    let expectedPath = Path.GetFullPath(Path.Combine(baseDir, relativePath))
    try
        // Act
        let resolvedPath = Sabota.Config.resolvePathRelativeToConfig configPath relativePath

        // Assert
        resolvedPath
        |> TestApp.Assert_Path_IsAbsolute
        |> TestApp.Assert_Path_NoDotDot
        |> TestApp.Assert_Path_Equal expectedPath
        |> ignore
    finally
        TestApp.CleanSimulatedEnvironment baseDir


[<Fact>]
let ``10. when resolvePathRelativeToConfig receives absolute path, it returns normalized path`` () =
    // Arrange
    let baseDir = TestApp.CreateSimulatedBaseDir()
    let configPath = Path.Combine(baseDir, Stub.ConfigFileName)
    File.WriteAllText(configPath, "{}") |> ignore
    let absolutePath = Path.Combine(baseDir, "..", Stub.ResourcesDirName, ".", Stub.TestProjectName)
    let expectedPath = Path.GetFullPath(absolutePath)
    try
        // Act
        let resolvedPath = Sabota.Config.resolvePathRelativeToConfig configPath absolutePath

        // Assert
        resolvedPath
        |> TestApp.Assert_Path_IsAbsolute
        |> TestApp.Assert_Path_NoDotDot
        |> TestApp.Assert_Path_Equal expectedPath
        |> ignore
    finally
        TestApp.CleanSimulatedEnvironment baseDir


[<Fact>]
let ``11. when config file has relative file field, it is resolved to absolute`` () =
    // Arrange
    let baseDir = TestApp.CreateSimulatedBaseDir()
    let configDir = Path.Combine(baseDir, Stub.ConfigSubDirName)
    Directory.CreateDirectory(configDir) |> ignore
    let srcDir = Path.Combine(baseDir, Stub.DirSrcName)
    Directory.CreateDirectory(srcDir) |> ignore
    let fileFs = Path.Combine(srcDir, Stub.FileFsName)
    File.WriteAllText(fileFs, "let x = 1") |> ignore
    let testProjectFile = Path.Combine(baseDir, Stub.TestProjectName)
    File.WriteAllText(testProjectFile, "<Project/>") |> ignore
    try
        let configPath = TestApp.CreateConfigFile(
            configDir, Stub.ConfigFileName,
            "../" + Stub.TestProjectName,
            "..",
            Some("../" + Stub.DirSrcName + "/" + Stub.FileFsName),
            None,
            [("Equality", true)]
        )
        let expectedFile = Path.GetFullPath(fileFs)

        // Act
        let config = Sabota.Config.loadConfig configPath

        // Assert
        config
        |> TestApp.Assert_Config_File (Some expectedFile)
        |> ignore
    finally
        TestApp.CleanSimulatedEnvironment baseDir


[<Fact>]
let ``12. when config file has relative directory field, it is resolved to absolute`` () =
    // Arrange
    let baseDir = TestApp.CreateSimulatedBaseDir()
    let configDir = Path.Combine(baseDir, Stub.ConfigSubDirName)
    Directory.CreateDirectory(configDir) |> ignore
    let srcDir = Path.Combine(baseDir, Stub.DirSrcName)
    Directory.CreateDirectory(srcDir) |> ignore
    let testProjectFile = Path.Combine(baseDir, Stub.TestProjectName)
    File.WriteAllText(testProjectFile, "<Project/>") |> ignore
    try
        let configPath = TestApp.CreateConfigFile(
            configDir, Stub.ConfigFileName,
            "../" + Stub.TestProjectName,
            "..",
            None,
            Some("../" + Stub.DirSrcName),
            [("Equality", true)]
        )
        let expectedDir = Path.GetFullPath(srcDir)

        // Act
        let config = Sabota.Config.loadConfig configPath

        // Assert
        config
        |> TestApp.Assert_Config_Directory (Some expectedDir)
        |> ignore
    finally
        TestApp.CleanSimulatedEnvironment baseDir
