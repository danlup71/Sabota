module Mutant1.Visitor

open FSharp.Compiler.Syntax
open FSharp.Compiler.Text

open Mutant1.MutationCandidates

// TODO danlup after prototyping refactor out with a proper algorithm 

let findMutationCandidates (file: string) (parsedInput: ParsedInput) : MutationCandidate list =
    let mutable nextId : MutationId = 1
    let mutable acc : MutationCandidate list = []

    let addCandidate (kind: MutationKind) (r: range) =
        let candidate = { Id = nextId; Kind = kind; File = file; Range = r; Original = ""; Replacement = ""; SurvivorInterpretation = "" }
        nextId <- nextId + 1
        acc <- candidate :: acc

    let checkIdent (names: string list) (r: range) =
        match names with
        | [ "op_Equality" ] | [ "=" ] ->
            addCandidate EqualityToInequality r
        | [ "op_Inequality" ] | [ "<>" ] ->
            addCandidate InequalityToEquality r
        | [ "op_GreaterThan" ] | [ ">" ] ->
            addCandidate GreaterToGreaterOrEqual r
        | [ "op_GreaterThanOrEqual" ] | [ ">=" ] ->
            addCandidate GreaterOrEqualToGreater r
        | [ "op_LessThan" ] | [ "<" ] ->
            addCandidate LessToLessOrEqual r
        | [ "op_LessThanOrEqual" ] | [ "<=" ] ->
            addCandidate LessOrEqualToLess r
        | [ "true" ] ->
            addCandidate TrueToFalse r
        | [ "false" ] ->
            addCandidate FalseToTrue r
        | _ -> ()

    let rec visitExpr (expr: SynExpr) =
        match expr with
        | SynExpr.LongIdent(longDotId = synLongIdent; range = m) ->
            let names = synLongIdent.LongIdent |> List.map (fun id -> id.idText)
            checkIdent names m
        | SynExpr.Ident(ident) ->
            checkIdent [ ident.idText ] ident.idRange
        | SynExpr.Paren(expr = e) ->
            visitExpr e
        | SynExpr.Quote(quotedExpr = e) ->
            visitExpr e
        | SynExpr.Typed(expr = e) ->
            visitExpr e
        | SynExpr.Tuple(exprs = exprs) ->
            exprs |> List.iter visitExpr
        | SynExpr.AnonRecd(copyInfo = copyInfo; recordFields = fields) ->
            copyInfo |> Option.iter (fun (e, _) -> visitExpr e)
            fields |> List.iter (fun (_, _, e) -> visitExpr e)
        | SynExpr.ArrayOrList(exprs = exprs) ->
            exprs |> List.iter visitExpr
        | SynExpr.Record(copyInfo = copyInfo; recordFields = fields) ->
            copyInfo |> Option.iter (fun (e, _) -> visitExpr e)
            fields |> List.iter (fun synRecordField ->
                match synRecordField with
                | SynExprRecordField(expr = Some e) -> visitExpr e
                | _ -> ())
        | SynExpr.New(expr = e) ->
            visitExpr e
        | SynExpr.While(whileExpr = e1; doExpr = e2) ->
            visitExpr e1
            visitExpr e2
        | SynExpr.WhileBang(whileExpr = e1; doExpr = e2) ->
            visitExpr e1
            visitExpr e2
        | SynExpr.For(identBody = e1; toBody = e2; doBody = e3) ->
            visitExpr e1
            visitExpr e2
            visitExpr e3
        | SynExpr.ForEach(enumExpr = e1; bodyExpr = e2) ->
            visitExpr e1
            visitExpr e2
        | SynExpr.ArrayOrListComputed(expr = e) ->
            visitExpr e
        | SynExpr.ComputationExpr(expr = e) ->
            visitExpr e
        | SynExpr.Lambda(body = e) ->
            visitExpr e
        | SynExpr.MatchLambda(matchClauses = clauses) ->
            clauses |> List.iter visitMatchClause
        | SynExpr.Match(expr = e; clauses = clauses) ->
            visitExpr e
            clauses |> List.iter visitMatchClause
        | SynExpr.MatchBang(expr = e; clauses = clauses) ->
            visitExpr e
            clauses |> List.iter visitMatchClause
        | SynExpr.Do(expr = e) ->
            visitExpr e
        | SynExpr.DoBang(expr = e) ->
            visitExpr e
        | SynExpr.LetOrUse(synLetOrUse) ->
            synLetOrUse.Bindings |> List.iter visitBinding
            visitExpr synLetOrUse.Body
        | SynExpr.TryWith(tryExpr = e1; withCases = clauses) ->
            visitExpr e1
            clauses |> List.iter visitMatchClause
        | SynExpr.TryFinally(tryExpr = e1; finallyExpr = e2) ->
            visitExpr e1
            visitExpr e2
        | SynExpr.Lazy(expr = e) ->
            visitExpr e
        | SynExpr.Sequential(expr1 = e1; expr2 = e2) ->
            visitExpr e1
            visitExpr e2
        | SynExpr.IfThenElse(ifExpr = e1; thenExpr = e2; elseExpr = e3Opt) ->
            visitExpr e1
            visitExpr e2
            e3Opt |> Option.iter visitExpr
        | SynExpr.LongIdentSet(expr = e) ->
            visitExpr e
        | SynExpr.DotGet(expr = e) ->
            visitExpr e
        | SynExpr.DotSet(targetExpr = e1; rhsExpr = e2) ->
            visitExpr e1
            visitExpr e2
        | SynExpr.Set(targetExpr = e1; rhsExpr = e2) ->
            visitExpr e1
            visitExpr e2
        | SynExpr.DotIndexedGet(objectExpr = e; indexArgs = idx) ->
            visitExpr e
            visitExpr idx
        | SynExpr.DotIndexedSet(objectExpr = e1; indexArgs = idx; valueExpr = e2) ->
            visitExpr e1
            visitExpr idx
            visitExpr e2
        | SynExpr.IndexRange(expr1 = e1; expr2 = e2) ->
            e1 |> Option.iter visitExpr
            e2 |> Option.iter visitExpr
        | SynExpr.IndexFromEnd(expr = e) ->
            visitExpr e
        | SynExpr.DotNamedIndexedPropertySet(targetExpr = e1; argExpr = e2; rhsExpr = e3) ->
            visitExpr e1
            visitExpr e2
            visitExpr e3
        | SynExpr.TypeTest(expr = e) ->
            visitExpr e
        | SynExpr.Upcast(expr = e) ->
            visitExpr e
        | SynExpr.Downcast(expr = e) ->
            visitExpr e
        | SynExpr.InferredUpcast(expr = e) ->
            visitExpr e
        | SynExpr.InferredDowncast(expr = e) ->
            visitExpr e
        | SynExpr.AddressOf(expr = e) ->
            visitExpr e
        | SynExpr.TraitCall(argExpr = e) ->
            visitExpr e
        | SynExpr.JoinIn(lhsExpr = e1; rhsExpr = e2) ->
            visitExpr e1
            visitExpr e2
        | SynExpr.YieldOrReturn(expr = e) ->
            visitExpr e
        | SynExpr.App(funcExpr = e1; argExpr = e2) ->
            visitExpr e1
            visitExpr e2
        | SynExpr.Fixed(expr = e) ->
            visitExpr e
        | SynExpr.InterpolatedString(contents = contents) ->
            contents |> List.iter (function
                | SynInterpolatedStringPart.FillExpr(e, _) -> visitExpr e
                | _ -> ())
        | SynExpr.DebugPoint(innerExpr = e) ->
            visitExpr e
        | SynExpr.Dynamic(funcExpr = e1; argExpr = e2) ->
            visitExpr e1
            visitExpr e2
        | SynExpr.Const(constant = SynConst.Bool(true); range = r) ->
            addCandidate TrueToFalse r
        | SynExpr.Const(constant = SynConst.Bool(false); range = r) ->
            addCandidate FalseToTrue r
        | SynExpr.Const(constant = c; range = r) ->
            ()
        | _ -> ()

    and visitMatchClause (SynMatchClause(whenExpr = whenOpt; resultExpr = e)) =
        whenOpt |> Option.iter visitExpr
        visitExpr e

    and visitBinding (SynBinding(expr = e)) =
        visitExpr e

    and visitMemberDefn (defn: SynMemberDefn) =
        match defn with
        | SynMemberDefn.Member(memberDefn = b) -> visitBinding b
        | SynMemberDefn.LetBindings(bindings = bs) -> bs |> List.iter visitBinding
        | SynMemberDefn.AutoProperty(synExpr = e) -> visitExpr e
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
        | SynModuleDecl.Let(bindings = bs) ->
            bs |> List.iter visitBinding
        | SynModuleDecl.Expr(expr = e) ->
            visitExpr e
        | SynModuleDecl.NestedModule(decls = decls) ->
            decls |> List.iter visitDecl
        | SynModuleDecl.Types(typeDefns = typeDefns) ->
            typeDefns |> List.iter visitTypeDefn
        | _ -> ()

    let visitModule (SynModuleOrNamespace(decls = decls)) =
        decls |> List.iter visitDecl

    match parsedInput with
    | ParsedInput.ImplFile(ParsedImplFileInput(contents = modules)) ->
        modules |> List.iter visitModule
    | ParsedInput.SigFile _ -> ()

    printfn $"Candidates Found: %A{acc.Length}"
    
    List.rev acc