module Sabota.MutationTypes

type MutationCategory =
    | Equality
    | RelationalBoundary
    | RelationalReversal
    | Boolean
    | Arithmetic
    | Conditional
    | Option
    | ReturnListValue

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
// if (condition) -> if (not condition)
// if (not condition) -> if (condition)

    
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

    
    