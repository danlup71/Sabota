module Sabota.Visitor

open System.Linq
open FSharp.Compiler.Syntax
open FSharp.Compiler.Text

open Sabota.MutationCandidates
open Sabota.MutationTypes

// TODO danlup after prototyping refactor out with a proper algorithm 

let findMutationCandidates mutationCategories  (file: string) (parsedInput: ParsedInput) : MutationCandidate list =
    let mutable nextId : MutationId = 1
    let mutable acc : MutationCandidate list = []
    let enabledCategories : MutationCategory list = mutationCategories

    let addCandidate (kind: MutationKind) (r: range) =
        let candidate = { Id = nextId; Kind = kind; File = file; Range = r; Original = ""; Replacement = ""; SurvivorInterpretation = "" }
        nextId <- nextId + 1
        acc <- candidate :: acc

    let isEnabled (kind: MutationKind) =
        getCategory kind
        |> enabledCategories.Contains 
        
    let tryAddMutationCandidate kind range =
        if isEnabled kind then
            addCandidate kind range    
    
    let checkIdent (names: string list) (r: range) =
        match names with
        | [ "op_Equality" ] | [ "=" ] -> tryAddMutationCandidate EqualityToInequality r
        | [ "op_Inequality" ] | [ "<>" ] -> tryAddMutationCandidate InequalityToEquality r
        | [ "op_GreaterThan" ] | [ ">" ] -> 
            tryAddMutationCandidate GreaterToGreaterOrEqual r
            tryAddMutationCandidate GreaterToLess r
            tryAddMutationCandidate GreaterToLessOrEqual r
        | [ "op_GreaterThanOrEqual" ] | [ ">=" ] -> tryAddMutationCandidate GreaterOrEqualToGreater r
        | [ "op_LessThan" ] | [ "<" ] -> 
            tryAddMutationCandidate LessToLessOrEqual r
            tryAddMutationCandidate LessToGreater r
            tryAddMutationCandidate LessToGreaterOrEqual r
        | [ "op_LessThanOrEqual" ] | [ "<=" ] -> tryAddMutationCandidate LessOrEqualToLess r
        | [ "op_Addition" ] | [ "+" ] -> tryAddMutationCandidate PlusToMinus r
        | [ "op_Subtraction" ] | [ "-" ] -> tryAddMutationCandidate MinusToPlus r
        | [ "op_Multiply" ] | [ "*" ] -> tryAddMutationCandidate TimesToDivide r
        | [ "op_Divide" ] | [ "op_Division" ] | [ "/" ] -> tryAddMutationCandidate DivideToTimes r
        | [ "op_BooleanAnd" ] | [ "&&" ] -> tryAddMutationCandidate AndToOr r
        | [ "op_BooleanOr" ] | [ "||" ] -> tryAddMutationCandidate OrToAnd r
        | [ "Some" ] | _ :: [ "Some" ] -> tryAddMutationCandidate SomeToNone r
        | [ "true" ] -> tryAddMutationCandidate TrueToFalse r
        | [ "false" ] -> tryAddMutationCandidate FalseToTrue r
        | _ -> ()

    
    let discoverMutations (expr: SynExpr) =
        match expr with
        | SynExpr.LongIdent(longDotId = synLongIdent; range = m) ->
            let names = synLongIdent.LongIdent |> List.map _.idText
            checkIdent names m
        | SynExpr.Ident(ident) -> checkIdent [ ident.idText ] ident.idRange
        | SynExpr.Const(constant = SynConst.Bool(true); range = r) -> tryAddMutationCandidate TrueToFalse r
        | SynExpr.Const(constant = SynConst.Bool(false); range = r) -> tryAddMutationCandidate FalseToTrue r
        | _ -> ()

    let rec analyzeExpr (expr: SynExpr) =
        discoverMutations expr
        visitChildren expr

    and visitChildren expr =                
        match expr with
        | SynExpr.AnonRecd(copyInfo = copyInfo; recordFields = fields) ->
                                                                copyInfo |> Option.iter (fun (e, _) -> analyzeExpr e)
                                                                fields |> List.iter (fun (_, _, e) -> analyzeExpr e)

                    
        | SynExpr.Paren(expr = e) -> analyzeExpr e
        | SynExpr.Quote(quotedExpr = e) -> analyzeExpr e
        | SynExpr.Typed(expr = e) -> analyzeExpr e
        | SynExpr.Tuple(exprs = exprs) -> exprs |> List.iter analyzeExpr
        | SynExpr.ArrayOrList(exprs = exprs) -> exprs |> List.iter analyzeExpr
        | SynExpr.Record(copyInfo = copyInfo; recordFields = fields) ->
                                                                copyInfo |> Option.iter (fun (e, _) -> analyzeExpr e)
                                                                fields |> List.iter (fun synRecordField ->
                                                                    match synRecordField with
                                                                    | SynExprRecordField(expr = Some e) -> analyzeExpr e
                                                                    | _ -> ())
        | SynExpr.New(expr = e) -> analyzeExpr e
        | SynExpr.While(whileExpr = e1; doExpr = e2) ->
                                                        analyzeExpr e1
                                                        analyzeExpr e2
        | SynExpr.WhileBang(whileExpr = e1; doExpr = e2) ->
                                                            analyzeExpr e1
                                                            analyzeExpr e2
        | SynExpr.For(identBody = e1; toBody = e2; doBody = e3) ->
                                                                    analyzeExpr e1
                                                                    analyzeExpr e2
                                                                    analyzeExpr e3
        | SynExpr.ForEach(enumExpr = e1; bodyExpr = e2) ->
                                                            analyzeExpr e1
                                                            analyzeExpr e2
        | SynExpr.ArrayOrListComputed(expr = e) -> analyzeExpr e
        | SynExpr.ComputationExpr(expr = e) -> analyzeExpr e
        | SynExpr.Lambda(body = e) -> analyzeExpr e
        | SynExpr.MatchLambda(matchClauses = clauses) -> clauses |> List.iter visitMatchClause
        | SynExpr.Match(expr = e; clauses = clauses) ->
                                                        analyzeExpr e
                                                        clauses |> List.iter visitMatchClause
        | SynExpr.MatchBang(expr = e; clauses = clauses) ->
                                                        analyzeExpr e
                                                        clauses |> List.iter visitMatchClause
        | SynExpr.Do(expr = e) -> analyzeExpr e
        | SynExpr.DoBang(expr = e) -> analyzeExpr e
        | SynExpr.LetOrUse(synLetOrUse) ->
                                            synLetOrUse.Bindings |> List.iter visitBinding
                                            analyzeExpr synLetOrUse.Body
        | SynExpr.TryWith(tryExpr = e1; withCases = clauses) ->
                                            analyzeExpr e1
                                            clauses |> List.iter visitMatchClause
        | SynExpr.TryFinally(tryExpr = e1; finallyExpr = e2) ->
                                        analyzeExpr e1
                                        analyzeExpr e2
        | SynExpr.Lazy(expr = e) -> analyzeExpr e
        | SynExpr.Sequential(expr1 = e1; expr2 = e2) ->
                                                    analyzeExpr e1
                                                    analyzeExpr e2
        | SynExpr.IfThenElse(ifExpr = e1; thenExpr = e2; elseExpr = e3Opt) ->
                                                    analyzeExpr e1
                                                    analyzeExpr e2
                                                    e3Opt |> Option.iter analyzeExpr
        | SynExpr.LongIdentSet(expr = e) -> analyzeExpr e
        | SynExpr.DotGet(expr = e) -> analyzeExpr e
        | SynExpr.DotSet(targetExpr = e1; rhsExpr = e2) ->
                                                    analyzeExpr e1
                                                    analyzeExpr e2
        | SynExpr.Set(targetExpr = e1; rhsExpr = e2) ->
                                                    analyzeExpr e1
                                                    analyzeExpr e2
        | SynExpr.DotIndexedGet(objectExpr = e; indexArgs = idx) ->
                                                    analyzeExpr e
                                                    analyzeExpr idx
        | SynExpr.DotIndexedSet(objectExpr = e1; indexArgs = idx; valueExpr = e2) ->
                                                    analyzeExpr e1
                                                    analyzeExpr idx
                                                    analyzeExpr e2
        | SynExpr.IndexRange(expr1 = e1; expr2 = e2) ->
                                                    e1 |> Option.iter analyzeExpr
                                                    e2 |> Option.iter analyzeExpr
        | SynExpr.IndexFromEnd(expr = e) -> analyzeExpr e
        | SynExpr.DotNamedIndexedPropertySet(targetExpr = e1; argExpr = e2; rhsExpr = e3) ->
                                                    analyzeExpr e1
                                                    analyzeExpr e2
                                                    analyzeExpr e3
        | SynExpr.TypeTest(expr = e) -> analyzeExpr e
        | SynExpr.Upcast(expr = e) -> analyzeExpr e
        | SynExpr.Downcast(expr = e) -> analyzeExpr e
        | SynExpr.InferredUpcast(expr = e) -> analyzeExpr e
        | SynExpr.InferredDowncast(expr = e) -> analyzeExpr e
        | SynExpr.AddressOf(expr = e) -> analyzeExpr e
        | SynExpr.TraitCall(argExpr = e) -> analyzeExpr e
        | SynExpr.JoinIn(lhsExpr = e1; rhsExpr = e2) ->
                                                    analyzeExpr e1
                                                    analyzeExpr e2
        | SynExpr.YieldOrReturn(expr = e) -> analyzeExpr e
        | SynExpr.App(funcExpr = e1; argExpr = e2) ->
                                                analyzeExpr e1
                                                analyzeExpr e2
        | SynExpr.Fixed(expr = e) -> analyzeExpr e
        | SynExpr.InterpolatedString(contents = contents) ->
                                                contents |> List.iter (function
                                                    | SynInterpolatedStringPart.FillExpr(e, _) -> analyzeExpr e
                                                    | _ -> ())
        | SynExpr.DebugPoint(innerExpr = e) -> analyzeExpr e
        | SynExpr.Dynamic(funcExpr = e1; argExpr = e2) ->
                                                analyzeExpr e1
                                                analyzeExpr e2
        | SynExpr.Const(constant = c; range = r) -> ()
        | _ -> ()

    and visitMatchClause (SynMatchClause(whenExpr = whenOpt; resultExpr = e)) =
                                                whenOpt |> Option.iter analyzeExpr
                                                analyzeExpr e

    and visitBinding (SynBinding(expr = e)) = analyzeExpr e

    and visitMemberDefn (defn: SynMemberDefn) =
        match defn with
        | SynMemberDefn.Member(memberDefn = b) -> visitBinding b
        | SynMemberDefn.LetBindings(bindings = bs) -> bs |> List.iter visitBinding
        | SynMemberDefn.AutoProperty(synExpr = e) -> analyzeExpr e
        | SynMemberDefn.Interface(members = Some ms) -> ms |> List.iter visitMemberDefn
        | SynMemberDefn.NestedType(typeDefn = typeDefn) -> visitTypeDefn typeDefn
        | _ -> ()

    and visitTypeDefn (SynTypeDefn(typeRepr = repr; members = members)) =
        members |> List.iter visitMemberDefn
        match repr with
        | SynTypeDefnRepr.ObjectModel(members = ms) -> ms |> List.iter visitMemberDefn
        | _ -> ()

    and visitDecl (decl: SynModuleDecl) =
        match decl with
        | SynModuleDecl.Let(bindings = bs) -> bs |> List.iter visitBinding
        | SynModuleDecl.Expr(expr = e) -> analyzeExpr e
        | SynModuleDecl.NestedModule(decls = decls) -> decls |> List.iter visitDecl
        | SynModuleDecl.Types(typeDefns = typeDefns) -> typeDefns |> List.iter visitTypeDefn
        | _ -> ()
        
    let visitModule (SynModuleOrNamespace(decls = decls)) =
        decls |> List.iter visitDecl

    match parsedInput with
    | ParsedInput.ImplFile(ParsedImplFileInput(contents = modules)) -> modules |> List.iter visitModule
    | ParsedInput.SigFile _ -> ()

    printfn $"Candidates Found: %A{acc.Length}"
    
    List.rev acc