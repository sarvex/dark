module LibExecution.ProgramTypesAst

open System.Threading.Tasks
open FSharp.Control.Tasks

open Prelude
open ProgramTypes

// Traverse is really only meant to be used by preTraversal and postTraversal
let traverse (f : Expr -> Expr) (expr : Expr) : Expr =
  match expr with
  | EInt _
  | EBool _
  | EString _
  | EChar _
  | EUnit _
  | EVariable _
  | EPipeTarget _
  | EFloat _ -> expr
  | ELet (id, pat, rhs, next) -> ELet(id, pat, f rhs, f next)
  | EIf (id, cond, ifexpr, elseexpr) -> EIf(id, f cond, f ifexpr, f elseexpr)
  | EFieldAccess (id, expr, fieldname) -> EFieldAccess(id, f expr, fieldname)
  | EInfix (id, op, left, right) -> EInfix(id, op, f left, f right)
  | EPipe (id, expr1, expr2, exprs) -> EPipe(id, f expr1, f expr2, List.map f exprs)
  | EFnCall (id, name, typeArgs, exprs) ->
    EFnCall(id, name, typeArgs, List.map f exprs)
  | ELambda (id, names, expr) -> ELambda(id, names, f expr)
  | EList (id, exprs) -> EList(id, List.map f exprs)
  | EDict (id, pairs) -> EDict(id, List.map (fun (k, v) -> (k, f v)) pairs)
  | ETuple (id, first, second, theRest) ->
    ETuple(id, f first, f second, List.map f theRest)
  | EMatch (id, mexpr, pairs) ->
    EMatch(id, f mexpr, List.map (fun (name, expr) -> (name, f expr)) pairs)
  | ERecord (id, typeName, fields) ->
    ERecord(id, typeName, List.map (fun (name, expr) -> (name, f expr)) fields)
  | EConstructor (id, typeName, caseName, fields) ->
    EConstructor(id, typeName, caseName, List.map f fields)



let rec preTraversal (f : Expr -> Expr) (expr : Expr) : Expr =
  let r = preTraversal f in
  let expr = f expr in
  traverse r expr


let rec postTraversal (f : Expr -> Expr) (expr : Expr) : Expr =
  let r = postTraversal f in
  let result = traverse r expr
  f result

let rec matchPatternPreTraversal
  (f : MatchPattern -> MatchPattern)
  (pattern : MatchPattern)
  : MatchPattern =
  let r = matchPatternPreTraversal f in
  let pattern = f pattern in
  match pattern with
  | MPVariable _
  | MPChar _
  | MPInt _
  | MPBool _
  | MPString _
  | MPUnit _
  | MPFloat _ -> pattern
  | MPConstructor (patternID, caseName, fieldPats) ->
    MPConstructor(patternID, caseName, List.map (fun p -> r p) fieldPats)
  | MPTuple (patternID, first, second, theRest) ->
    MPTuple(patternID, r first, r second, List.map r theRest)
  | MPList (patternID, pats) -> MPList(patternID, List.map r pats)


let rec matchPatternPostTraversal
  (f : MatchPattern -> MatchPattern)
  (pattern : MatchPattern)
  : MatchPattern =
  let r = matchPatternPostTraversal f in
  let result =
    match pattern with
    | MPVariable _
    | MPChar _
    | MPInt _
    | MPBool _
    | MPString _
    | MPUnit _
    | MPFloat _ -> pattern
    | MPConstructor (patternID, caseName, fieldPats) ->
      MPConstructor(patternID, caseName, List.map r fieldPats)
    | MPTuple (patternID, first, second, theRest) ->
      MPTuple(patternID, r first, r second, List.map r theRest)
    | MPList (patternID, pats) -> MPList(patternID, List.map r pats)
  f result
