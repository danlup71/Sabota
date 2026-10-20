module Mutant1.MuationDefinitions

open Mutant1.MutationCandidates


type MutationDefinition =
    {
        Original : string
        Replacement : string
    }

let getDefinition (kind:Mutant1.MutationKind) =
    match kind with
    | EqualityToInequality ->
        { Original = "="
          Replacement = "<>" }

    | InequalityToEquality ->
        { Original = "<>"
          Replacement = "=" }

    | TrueToFalse ->
        { Original = "true"
          Replacement = "false" }

    | FalseToTrue ->
        { Original = "false"
          Replacement = "true" }
        
    | GreaterToGreaterOrEqual -> 
        { Original = ">"
          Replacement = ">=" }
        
    | GreaterOrEqualToGreater -> 
        { Original = ">="
          Replacement = ">" }
        
    | LessToLessOrEqual -> 
        { Original = "<"
          Replacement = "<=" }
        
    | LessOrEqualToLess -> 
        { Original = "<="
          Replacement = "<" }

let enrich (candidate:MutationCandidate) =
    let definition =
        getDefinition candidate.Kind

    { candidate with Replacement = definition.Replacement; Original = definition.Original }
