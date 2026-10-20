module Mutant1.MutationTypes

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

type MutationKind =
    | EqualityToInequality
    | InequalityToEquality
    | TrueToFalse
    | FalseToTrue
    | GreaterToGreaterOrEqual
    | GreaterOrEqualToGreater
    | LessToLessOrEqual
    | LessOrEqualToLess    
    
    