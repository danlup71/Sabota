module Sabota.MutationTypes

open System
open System.IO

type MutationCategory =
    | Equality
    | RelationalBoundary
    | RelationalReversal
    | Boolean
    | Arithmetic
    | Conditional
    | Option
    | ReturnListValue
    | ControlFlow

type MutationKind =

    // -------------------- Equality
    | EqualityToInequality
    | InequalityToEquality
    
    // -------------------- Boolean
    | TrueToFalse
    | FalseToTrue
    
    // -------------------- RelationalBoundary
    | GreaterToGreaterOrEqual
    | GreaterOrEqualToGreater
    | LessToLessOrEqual
    | LessOrEqualToLess    

    // -------------------- RelationalReversal
    | GreaterToLess
    | GreaterToLessOrEqual
    | LessToGreater
    | LessToGreaterOrEqual


    // -------------------- Arithmetic
    | PlusToMinus
    | MinusToPlus
    | TimesToDivide
    | DivideToTimes

    // -------------------- Conditional
    | AndToOr
    | OrToAnd
    
    // -------------------- Option
    | SomeToNone
   
    // -------------------- ReturnValue
    | ReturnListValueToEmpty

    // -------------------- ControlFlow
    | ConditionToNotCondition
    | NotConditionToCondition

    
let getCategory = function
    | EqualityToInequality
    | InequalityToEquality ->
        Equality

    | TrueToFalse
    | FalseToTrue ->
        Boolean

    | GreaterToGreaterOrEqual
    | GreaterOrEqualToGreater
    | LessToLessOrEqual
    | LessOrEqualToLess ->
        RelationalBoundary

    | GreaterToLess
    | GreaterToLessOrEqual
    | LessToGreater
    | LessToGreaterOrEqual ->
        RelationalReversal

    | PlusToMinus
    | MinusToPlus
    | TimesToDivide
    | DivideToTimes ->
        Arithmetic

    | AndToOr
    | OrToAnd ->
        Conditional

    | SomeToNone ->
        Option

    | ReturnListValueToEmpty ->
        ReturnListValue

    | ConditionToNotCondition
    | NotConditionToCondition ->
        ControlFlow

    
type MutationStatus =
| Killed
| Survived
| BuildFailed
    
type MutationOutcome =
    {
        Status: MutationStatus
        Interpretation: string
    }

type MutationResult =
    {
        MutationId: int
        MutationType: MutationKind
        File: string
        Range: FSharp.Compiler.Text.Range
        Original: string
        Mutated: string
        Outcome: MutationOutcome
        PreviewOriginal: string
        PreviewMutated: string
        Link: string
    }

type FileTarget =
    | Files of string list
    | Directories of string list
    static member Empty() : FileTarget = Files []

type ConfigDefaults = {
    TestProject : string
    SolutionRoot : string
    File : string option
    Directory : string option
    Categories : MutationCategory list
}

    
type Workspace =
    { Folder : string
      SolutionDir : string
      TestProject : string
      Config: ConfigDefaults
      Target: FileTarget
    }
    
type RunResult =
    {
        runResult: MutationResult list
        executionTime: TimeSpan
    }