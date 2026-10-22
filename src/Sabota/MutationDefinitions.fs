module Sabota.MuationDefinitions

open Sabota.MutationCandidates


type MutationDefinition =
    {
        Original : string
        Replacement : string
        Description : string
        SurvivorInterpretation : string
    }

let getDefinition = function 
    | MutationTypes.MutationKind.EqualityToInequality ->
        { Original = "="
          Replacement = "<>"
          Description = "Equality replaced with inequality."
          SurvivorInterpretation = "The tests did not detect that an equality check was inverted." }

    | MutationTypes.MutationKind.InequalityToEquality ->
        { Original = "<>"
          Replacement = "="
          Description = "Inequality replaced with equality."
          SurvivorInterpretation = "The tests did not detect that an inequality check was inverted." }

    | MutationTypes.MutationKind.TrueToFalse ->
        { Original = "true"
          Replacement = "false"
          Description = "Boolean true replaced with false."
          SurvivorInterpretation = "The tests did not detect that a true literal was changed to false." }

    | MutationTypes.MutationKind.FalseToTrue ->
        { Original = "false"
          Replacement = "true"
          Description = "Boolean false replaced with true."
          SurvivorInterpretation = "The tests did not detect that a false literal was changed to true." }
        
    | MutationTypes.MutationKind.GreaterToGreaterOrEqual -> 
        { Original = ">"
          Replacement = ">="
          Description = "Greater-than replaced with greater-than-or-equal."
          SurvivorInterpretation = "The tests did not detect that a strict greater-than comparison was relaxed to include equality." }
        
    | MutationTypes.MutationKind.GreaterOrEqualToGreater -> 
        { Original = ">="
          Replacement = ">"
          Description = "Greater-than-or-equal replaced with greater-than."
          SurvivorInterpretation = "The tests did not detect that an inclusive greater-than-or-equal comparison was made strict." }
        
    | MutationTypes.MutationKind.LessToLessOrEqual -> 
        { Original = "<"
          Replacement = "<="
          Description = "Less-than replaced with less-than-or-equal."
          SurvivorInterpretation = "The tests did not detect that a strict less-than comparison was relaxed to include equality." }
        
    | MutationTypes.MutationKind.LessOrEqualToLess -> 
        { Original = "<="
          Replacement = "<"
          Description = "Less-than-or-equal replaced with less-than."
          SurvivorInterpretation = "The tests did not detect that an inclusive less-than-or-equal comparison was made strict." }

    | MutationTypes.MutationKind.GreaterToLess -> 
        { Original = ">"
          Replacement = "<"
          Description = "Greater-than replaced with less-than."
          SurvivorInterpretation = "The tests did not detect that a greater-than comparison was reversed to less-than." }

    | MutationTypes.MutationKind.GreaterToLessOrEqual -> 
        { Original = ">"
          Replacement = "<="
          Description = "Greater-than replaced with less-than-or-equal."
          SurvivorInterpretation = "The tests did not detect that a greater-than comparison was reversed to less-than-or-equal." }

    | MutationTypes.MutationKind.LessToGreater -> 
        { Original = "<"
          Replacement = ">"
          Description = "Less-than replaced with greater-than."
          SurvivorInterpretation = "The tests did not detect that a less-than comparison was reversed to greater-than." }

    | MutationTypes.MutationKind.LessToGreaterOrEqual -> 
        { Original = "<"
          Replacement = ">="
          Description = "Less-than replaced with greater-than-or-equal."
          SurvivorInterpretation = "The tests did not detect that a less-than comparison was reversed to greater-than-or-equal." }

    | MutationTypes.MutationKind.PlusToMinus -> 
        { Original = "+"
          Replacement = "-"
          Description = "Addition replaced with subtraction."
          SurvivorInterpretation = "The tests did not detect that an addition operation was changed to subtraction." }

    | MutationTypes.MutationKind.MinusToPlus -> 
        { Original = "-"
          Replacement = "+"
          Description = "Subtraction replaced with addition."
          SurvivorInterpretation = "The tests did not detect that a subtraction operation was changed to addition." }

    | MutationTypes.MutationKind.TimesToDivide -> 
        { Original = "*"
          Replacement = "/"
          Description = "Multiplication replaced with division."
          SurvivorInterpretation = "The tests did not detect that a multiplication operation was changed to division." }

    | MutationTypes.MutationKind.DivideToTimes -> 
        { Original = "/"
          Replacement = "*"
          Description = "Division replaced with multiplication."
          SurvivorInterpretation = "The tests did not detect that a division operation was changed to multiplication." }

    | MutationTypes.MutationKind.AndToOr -> 
        { Original = "&&"
          Replacement = "||"
          Description = "Logical AND replaced with logical OR."
          SurvivorInterpretation = "The tests did not detect that a logical AND was changed to logical OR." }

    | MutationTypes.MutationKind.OrToAnd -> 
        { Original = "||"
          Replacement = "&&"
          Description = "Logical OR replaced with logical AND."
          SurvivorInterpretation = "The tests did not detect that a logical OR was changed to logical AND." }

    | MutationTypes.MutationKind.SomeToNone -> 
        { Original = "Some"
          Replacement = "None"
          Description = "Some value replaced with None."
          SurvivorInterpretation = "The tests did not detect that a Some value was changed to None." }

    | MutationTypes.MutationKind.ReturnListValueToEmpty -> 
        { Original = "[...]"
          Replacement = "[]"
          Description = "List expression replaced with empty list."
          SurvivorInterpretation = "The tests did not detect that a list return value was changed to an empty list." }

let enrich (candidate:MutationCandidate) =
    let definition =
        getDefinition candidate.Kind

    { candidate with Replacement = definition.Replacement; Original = definition.Original; SurvivorInterpretation = definition.SurvivorInterpretation }
