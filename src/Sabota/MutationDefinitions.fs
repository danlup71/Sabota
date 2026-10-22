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

let enrich (candidate:MutationCandidate) =
    let definition =
        getDefinition candidate.Kind

    { candidate with Replacement = definition.Replacement; Original = definition.Original; SurvivorInterpretation = definition.SurvivorInterpretation }
