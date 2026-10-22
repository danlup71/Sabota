module Sabota.MutationTypes

type MutationCategory =
    | Equality
    | RelationalBoundary
    | RelationalReversal
    | Boolean
    | Arithmetic

type MutationKind =
    | EqualityToInequality
    | InequalityToEquality
    | TrueToFalse
    | FalseToTrue
    | GreaterToGreaterOrEqual
    | GreaterOrEqualToGreater
    | LessToLessOrEqual
    | LessOrEqualToLess    
    
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

    // | GreaterToLess
    // | GreaterToLessOrEqual
    // | LessToGreater
    // | LessToGreaterOrEqual ->
    //     RelationalReversal
    
    (*

-------------------- EASY
=    -> <>
<>   -> =

>    -> >=
>=   -> >
<    -> <=
<=   -> <

true  -> false
false -> true


-------------------- ARITHMETIC
+ -> -
- -> +
* -> /
/ -> *


-------------------- CONDITIONAL LOGIC
&& -> ||
|| -> &&

Some x -> None

-------------------- RETURN VALUE
[] -> [dummy]
[x] -> []


-------------------- CONTROL FLOW
if (condition) -> if (!condition)

*)
    