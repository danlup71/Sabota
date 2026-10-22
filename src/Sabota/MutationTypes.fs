module Sabota.MutationTypes

type MutationCategory =
    | Equality
    | RelationalBoundary
    | RelationalReversal
    | Boolean
    | Arithmetic

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
// | PlusToMinus
// | MinusToPlus
// | TimesToDivide
// | DivideToTimes ->

    // -------------------- Conditional
// | AndToOr
// | OrToAnd
// | SomeToNone

// && -> ||
// || -> &&
// Some x -> None

    
    // -------------------- ReturnValue
// [] -> [dummy]
// [x] -> []

    // -------------------- ControlFlow
// if (condition) -> if (!condition)

    
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

    
    