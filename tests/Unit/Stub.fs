module Stub

open Sabota.MutationTypes

/// All MutationCategory values for test consistency checks.
let AllMutationCategories : MutationCategory list =
    [ Equality
      RelationalBoundary
      RelationalReversal
      Boolean
      Arithmetic
      Conditional
      Option
      ReturnListValue
      ControlFlow ]

/// All MutationKind values for test consistency checks.
let AllMutationKinds : MutationKind list =
    [ EqualityToInequality
      InequalityToEquality
      TrueToFalse
      FalseToTrue
      GreaterToGreaterOrEqual
      GreaterOrEqualToGreater
      LessToLessOrEqual
      LessOrEqualToLess
      GreaterToLess
      GreaterToLessOrEqual
      LessToGreater
      LessToGreaterOrEqual
      PlusToMinus
      MinusToPlus
      TimesToDivide
      DivideToTimes
      AndToOr
      OrToAnd
      SomeToNone
      ReturnListValueToEmpty
      ConditionToNotCondition
      NotConditionToCondition ]

/// Default test project file name used in validate tests.
let TestProjectName = "test.fsproj"

/// Default solution root directory name used in validate tests.
let SolutionRootName = "solution"

/// Default F# source file name used in validate tests.
let FileFsName = "file.fs"

/// C# source file name used in validate tests for wrong-extension scenarios.
let FileCsName = "file.cs"

/// First F# source file name used in multi-file validate tests.
let File1FsName = "file1.fs"

/// Second F# source file name used in multi-file validate tests.
let File2FsName = "file2.fs"

/// Non-existent file name used in validate tests.
let NonExistentFsName = "nonexistent.fs"

/// Source directory name used in validate tests.
let DirSrcName = "src"

/// First directory name used in multi-directory validate tests.
let Dir1Name = "dir1"

/// Second directory name used in multi-directory validate tests.
let Dir2Name = "dir2"

/// Non-existent directory name used in validate tests.
let NonExistentDirName = "no_dir"

/// Test file name used in independent mutation tests.
let TestFsName = "test.fs"

/// First source file name used in multi-file mutation tests.
let SourceFile1Name = "file1.fs"

/// Second source file name used in multi-file mutation tests.
let SourceFile2Name = "file2.fs"

/// Custom file name used in enrich metadata tests.
let CustomFsName = "custom.fs"

/// Set of excluded build directory names used in excluded directory tests.
let ExcludedBuildDirs = Set.ofList ["debug"; "release"; "bin"; "obj"]

/// Config file name used in config loading tests.
let ConfigFileName = "sabota_config.json"

/// Config subdirectory name used in config path resolution tests.
let ConfigSubDirName = "config"

/// Resources directory name used in config path resolution tests (relative path target).
let ResourcesDirName = "resources"

/// UnitTest subdirectory name used in config path resolution tests.
let UnitTestDirName = "UnitTest"



