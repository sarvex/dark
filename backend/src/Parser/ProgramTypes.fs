module Parser.ProgramTypes

open FSharp.Compiler
open FSharp.Compiler.CodeAnalysis
open FSharp.Compiler.Syntax

open Prelude
open Tablecloth

module PT = LibExecution.ProgramTypes

open Utils

// A placeholder is used to indicate what still needs to be filled
let placeholder = PT.EString(12345678UL, [ PT.StringText "PLACEHOLDER VALUE" ])

// This is a "Partial active pattern" that you can use as a Pattern to match a Placeholder value
let (|Placeholder|_|) (input : PT.Expr) =
  if input = placeholder then Some() else None

module TypeReference =

  let private parseTypeRef (name : string) : string * int =
    match name with
    | Regex "^([A-Z][a-z0-9A-Z]*)_v(\d+)$" [ name; version ] -> name, (int version)
    | Regex "^([A-Z][a-z0-9A-Z]*)$" [ name ] -> name, 0
    | _ -> Exception.raiseInternal "Bad format in typeRef" [ "name", name ]


  let rec fromNameAndTypeArgs
    (name : string)
    (typeArgs : List<SynType>)
    : PT.TypeReference =
    match parseTypeRef name, typeArgs with
    // no type args
    | ("Bool", 0), [] -> PT.TBool
    | ("Bytes", 0), [] -> PT.TBytes
    | ("Int", 0), [] -> PT.TInt
    | ("String", 0), [] -> PT.TString
    | ("Char", 0), [] -> PT.TChar
    | ("Float", 0), [] -> PT.TFloat
    | ("DateTime", 0), [] -> PT.TDateTime
    | ("Uuid", 0), [] -> PT.TUuid
    | ("Unit", 0), [] -> PT.TUnit
    | ("Password", 0), [] -> PT.TPassword

    // with type args
    | ("List", 0), [ arg ] -> PT.TList(fromSynType arg)
    | ("Option", 0), [ arg ] -> PT.TOption(fromSynType arg)
    | ("Result", 0), [ okArg; errorArg ] ->
      PT.TResult(fromSynType okArg, fromSynType errorArg)
    | ("Dict", 0), [ valArg ] -> PT.TDict(fromSynType valArg)
    // TYPESCLEANUP - don't use word Tuple here
    | ("Tuple", 0), first :: second :: theRest ->
      PT.TTuple(fromSynType first, fromSynType second, List.map fromSynType theRest)
    | (name, version), args ->
      // TYPESCLEANUP - Not sure what we're doing for stdlib types here
      let tn = PT.FQTypeName.User { modules = []; typ = name; version = version }
      PT.TCustomType(tn, List.map fromSynType typeArgs)

  and fromSynType (typ : SynType) : PT.TypeReference =
    let c = fromSynType

    match typ with
    | SynType.Paren (t, _) -> c t

    // Variable types (i.e. "generic types")
    // e.g. `'a` in `'a -> bool`
    | SynType.Var (SynTypar (id, _, _), _) -> PT.TVariable(id.idText)


    // Function types
    // e.g. `'a -> bool` in `let friends (lambda: ('a -> bool)) = ...`
    | SynType.Fun (arg, ret, _, _) -> PT.TFn([ c arg ], c ret)


    // Named types. covers:
    // - built-in F# types like `bool`
    // - Stdlib-defined types
    // - User-defined types
    | SynType.App (SynType.LongIdent (SynLongIdent ([ ident ], _, _)),
                   _,
                   typeArgs,
                   _,
                   _,
                   _,
                   range) -> fromNameAndTypeArgs ident.idText typeArgs

    | SynType.LongIdent (SynLongIdent ([ ident ], _, _)) ->
      let typeArgs = []
      fromNameAndTypeArgs ident.idText typeArgs

    | _ -> Exception.raiseInternal $"Unsupported type" [ "type", typ ]


module LetPattern =
  let rec fromSynPat ast (pat : SynPat) : PT.LetPattern =
    let mapPat = fromSynPat ast

    match pat with
    | SynPat.Paren (subPat, _) -> mapPat subPat
    | SynPat.Wild (_) -> PT.LPVariable(gid (), "_")
    | SynPat.Named (SynIdent (name, _), _, _, _) ->
      PT.LPVariable(gid (), name.idText)

    | SynPat.Tuple (_, (first :: second :: theRest), _) ->
      PT.LetPattern.LPTuple(
        gid (),
        mapPat first,
        mapPat second,
        List.map mapPat theRest
      )

    | _ ->
      Exception.raiseInternal
        "Unsupported let or use expr pat type"
        [ "ast", ast; "pat", pat ]


module MatchPattern =
  let rec fromSynPat (pat : SynPat) : PT.MatchPattern =
    let id = gid ()
    let r = fromSynPat

    match pat with
    | SynPat.Named (SynIdent (name, _), _, _, _) -> PT.MPVariable(id, name.idText)
    | SynPat.Wild _ -> PT.MPVariable(gid (), "_") // wildcard, not blank
    | SynPat.Const (SynConst.Int32 n, _) -> PT.MPInt(id, n)
    | SynPat.Const (SynConst.Int64 n, _) -> PT.MPInt(id, int64 n)
    | SynPat.Const (SynConst.UInt64 n, _) -> PT.MPInt(id, int64 n)
    | SynPat.Const (SynConst.UserNum (n, "I"), _) -> PT.MPInt(id, parseInt64 n)
    | SynPat.Const (SynConst.Char c, _) -> PT.MPChar(id, string c)
    | SynPat.Const (SynConst.Bool b, _) -> PT.MPBool(id, b)
    | SynPat.Const (SynConst.Unit, _) -> PT.MPUnit(id)
    | SynPat.Null _ ->
      Exception.raiseInternal "null pattern not supported, use `()`" [ "pat", pat ]
    | SynPat.Paren (pat, _) -> r pat
    | SynPat.Const (SynConst.Double d, _) ->
      let sign, whole, fraction = readFloat d
      PT.MPFloat(id, sign, whole, fraction)
    | SynPat.Const (SynConst.String (s, _, _), _) -> PT.MPString(id, s)
    | SynPat.LongIdent (SynLongIdent (names, _, _), _, _, SynArgPats.Pats args, _, _) ->
      let args = List.map r args
      let constructorName =
        List.last names
        |> Exception.unwrapOptionInternal "missing constructor name" []
      let modules =
        List.initial names |> Option.unwrap [] |> List.map (fun i -> i.idText)
      // CLEANUPTYPES use modules
      PT.MPConstructor(id, constructorName.idText, args)
    | SynPat.Tuple (_isStruct, (first :: second :: theRest), _range) ->
      PT.MPTuple(id, r first, r second, List.map r theRest)
    | SynPat.ArrayOrList (_, pats, _) -> PT.MPList(id, List.map r pats)
    | _ -> Exception.raiseInternal "unhandled pattern" [ "pattern", pat ]


module Expr =
  // CLEANUP - blanks here aren't allowed
  let private nameOrBlank (v : string) : string = if v = "___" then "" else v

  let parseFn (fnName : string) : Option<string * int> =
    match fnName with
    | Regex "^([a-z][a-z0-9A-Z]*)_v(\d+)$" [ name; version ] ->
      Some(name, (int version))
    | Regex "^([a-z][a-z0-9A-Z]*)$" [ name ] -> Some(name, 0)
    | _ -> None

  let parseEnum (enumName : string) : string =
    // No version on the Enum case, that's on the type
    match enumName with
    | Regex "^([A-Z][a-z0-9A-Z]*)$" [ name ] -> name
    | _ ->
      Exception.raiseInternal "Bad format in enum names" [ "enumName", enumName ]

  let parseTypeName (typeName : string) : string * int =
    match typeName with
    | Regex "^([A-Z][a-z0-9A-Z]*)_v(\d+)$" [ name; version ] -> name, (int version)
    | Regex "^([A-Z][a-z0-9A-Z]*)$" [ name ] -> name, 0
    | _ -> Exception.raiseInternal "Bad format in type name" [ "typeName", typeName ]


  let private ops =
    Map.ofList [ ("op_Addition", PT.ArithmeticPlus)
                 ("op_Subtraction", PT.ArithmeticMinus)
                 ("op_Multiply", PT.ArithmeticMultiply)
                 ("op_Division", PT.ArithmeticDivide)
                 ("op_Modulus", PT.ArithmeticModulo)
                 ("op_Concatenate", PT.ArithmeticPower)
                 ("op_GreaterThan", PT.ComparisonGreaterThan)
                 ("op_GreaterThanOrEqual", PT.ComparisonGreaterThanOrEqual)
                 ("op_LessThan", PT.ComparisonLessThan)
                 ("op_LessThanOrEqual", PT.ComparisonLessThanOrEqual)
                 ("op_EqualsEquals", PT.ComparisonEquals)
                 ("op_BangEquals", PT.ComparisonNotEquals)
                 ("op_PlusPlus", PT.StringConcat) ]

  let rec fromSynExpr (ast : SynExpr) : PT.Expr =
    let c = fromSynExpr

    let convertLambdaVar (var : SynSimplePat) : string =
      match var with
      | SynSimplePat.Id (name, _, _, _, _, _) -> nameOrBlank name.idText
      | _ -> Exception.raiseInternal "unsupported lambdaVar" [ "var", var ]

    // Add a pipetarget after creating it
    let cPlusPipeTarget (e : SynExpr) : PT.Expr =
      match c e with
      | PT.EFnCall (id, name, typeArgs, args) ->
        PT.EFnCall(id, name, typeArgs, PT.EPipeTarget(gid ()) :: args)
      | PT.EInfix (id, op, Placeholder, arg2) ->
        PT.EInfix(id, op, PT.EPipeTarget(gid ()), arg2)
      | PT.EInfix (id, op, arg1, Placeholder) ->
        PT.EInfix(id, op, PT.EPipeTarget(gid ()), arg1)
      | other -> other

    let id = gid ()

    match ast with

    // Literals (ints, chars, bools, etc)
    | SynExpr.Null _ ->
      Exception.raiseInternal "null not supported, use `()`" [ "ast", ast ]
    | SynExpr.Const (SynConst.Unit _, _) -> PT.EUnit id
    | SynExpr.Const (SynConst.Int32 n, _) -> PT.EInt(id, n)
    | SynExpr.Const (SynConst.Int64 n, _) -> PT.EInt(id, int64 n)
    | SynExpr.Const (SynConst.UInt64 n, _) -> PT.EInt(id, int64 n)
    | SynExpr.Const (SynConst.UserNum (n, "I"), _) -> PT.EInt(id, parseInt64 n)
    | SynExpr.Const (SynConst.Char c, _) -> PT.EChar(id, string c)
    | SynExpr.Const (SynConst.Bool b, _) -> PT.EBool(id, b)
    | SynExpr.Const (SynConst.Double d, _) ->
      let sign, whole, fraction = readFloat d
      PT.EFloat(id, sign, whole, fraction)


    // Strings
    | SynExpr.Const (SynConst.String (s, _, _), _) ->
      PT.EString(id, [ PT.StringText s ])
    | SynExpr.InterpolatedString (parts, _, _) ->
      let parts =
        parts
        |> List.map (function
          | SynInterpolatedStringPart.String (s, _) -> PT.StringText s
          | SynInterpolatedStringPart.FillExpr (e, _) -> PT.StringInterpolation(c e))
      PT.EString(id, parts)


    // Simple identifiers/operators like `==`
    | SynExpr.LongIdent (_, SynLongIdent ([ ident ], _, _), _, _) when
      Map.containsKey ident.idText ops
      ->
      let op =
        Map.get ident.idText ops
        |> Exception.unwrapOptionInternal
             "can't find operation"
             [ "name", ident.idText ]
      PT.EInfix(id, PT.InfixFnCall op, placeholder, placeholder)


    // Binary Ops: && / ||
    | SynExpr.LongIdent (_, SynLongIdent ([ ident ], _, _), _, _) when
      List.contains ident.idText [ "op_BooleanAnd"; "op_BooleanOr" ]
      ->
      let op =
        match ident.idText with
        | "op_BooleanAnd" -> PT.BinOpAnd
        | "op_BooleanOr" -> PT.BinOpOr
        | _ -> Exception.raiseInternal "unhandled operation" [ "name", ident.idText ]

      PT.EInfix(id, PT.BinOp op, placeholder, placeholder)


    // Negation
    | SynExpr.LongIdent (_, SynLongIdent ([ ident ], _, _), _, _) when
      ident.idText = "op_UnaryNegation"
      ->
      let name = PT.FQFnName.stdlibFqName [ "Int" ] "negate" 0
      PT.EFnCall(id, name, [], [])


    // One word functions like `equals`
    | SynExpr.Ident ident when Set.contains ident.idText PT.FQFnName.oneWordFunctions ->
      match parseFn ident.idText with
      | Some (name, version) ->
        PT.EFnCall(id, PT.FQFnName.stdlibFqName [] name version, [], [])
      | None -> PT.EVariable(id, ident.idText)

    // List literals
    | SynExpr.ArrayOrList (_, exprs, _) -> PT.EList(id, exprs |> List.map c)

    // a literal list is sometimes made up of nested Sequentials
    | SynExpr.ArrayOrListComputed (_, (SynExpr.Sequential _ as seq), _) ->
      let rec seqAsList expr : List<SynExpr> =
        match expr with
        | SynExpr.Sequential (_, _, expr1, expr2, _) -> expr1 :: seqAsList expr2
        | _ -> [ expr ]
      PT.EList(id, seq |> seqAsList |> List.map c)

    | SynExpr.ArrayOrListComputed (_, SynExpr.Tuple (_, exprs, _, _), _) ->
      PT.EList(id, exprs |> List.map c)

    | SynExpr.ArrayOrListComputed (_, expr, _) -> PT.EList(id, [ c expr ])


    // Tuples
    | SynExpr.Tuple (_, first :: second :: rest, _, _) ->
      PT.ETuple(id, c first, c second, List.map c rest)

    // Enum values (EConstructors)
    // TODO: remove this explicit handling
    // when the Option and Result types are defined in StdLib
    | SynExpr.App (_, _, SynExpr.Ident name, arg, _) when
      List.contains name.idText [ "Ok"; "Nothing"; "Just"; "Error" ]
      ->
      PT.EConstructor(id, None, name.idText, [ c arg ])

    // Enum values (EConstructors)
    | SynExpr.Ident name when name.idText = "Nothing" ->
      PT.EConstructor(id, None, name.idText, [])


    // Package manager function calls
    // (preliminary support)
    | SynExpr.LongIdent (_,
                         SynLongIdent ([ owner; package; modName; fnName ], _, _),
                         _,
                         _) when
      owner.idText = "Test" && package.idText = "Test" && modName.idText = "Test"
      ->
      PT.EFnCall(
        gid (),
        PT.FQFnName.packageFqName
          "test"
          "test"
          (NonEmptyList.singleton "Test")
          fnName.idText
          0,
        [],
        []
      )


    // Constructors/FnCalls - e.g. `Result.Ok` or `Result.mapSecond`
    | SynExpr.LongIdent (_, SynLongIdent (names, _, _), _, _) when
      (names
       |> List.initial
       |> Option.unwrap []
       |> List.all (fun n -> n.idText <> "" && System.Char.IsUpper(n.idText[0])))
      ->
      let modules =
        List.initial names |> Option.unwrap [] |> List.map (fun i -> i.idText)
      let name =
        List.last names
        |> Exception.unwrapOptionInternal "empty list" []
        |> fun i -> i.idText

      match parseFn name with
      | Some (name, version) ->
        PT.EFnCall(gid (), PT.FQFnName.stdlibFqName modules name version, [], [])
      | None ->
        let enumName = parseEnum name
        let typename =
          List.last modules |> Exception.unwrapOptionInternal "empty list" []
        let (typ, version) = parseTypeName typename
        let modules = List.initial modules |> Option.unwrap []
        // TYPESCLEANUP might not be a usertype
        PT.EConstructor(
          gid (),
          Some(
            PT.FQTypeName.User
              { PT.FQTypeName.UserTypeName.modules = modules
                typ = typ
                version = version } : PT.FQTypeName.T
          ),
          enumName,
          []
        )

    // Variable constructors - Ok, Error, Nothing, and Just are handled elsewhere,
    // and Enums are expected to be fully qualified
    | SynExpr.Ident name -> PT.EVariable(id, name.idText)


    // e.g. `Json.serialize<T>`
    | SynExpr.TypeApp (SynExpr.Ident name, _, typeArgs, _, _, _, _) ->
      let typeArgs =
        typeArgs |> List.map (fun synType -> TypeReference.fromSynType synType)


      let name, version =
        parseFn name.idText |> Exception.unwrapOptionInternal "invalid fn name" []
      PT.EFnCall(gid (), PT.FQFnName.userFqName [] name version, typeArgs, [])

    | SynExpr.TypeApp (SynExpr.LongIdent (_,
                                          SynLongIdent ([ modName; fnName ], _, _),
                                          _,
                                          _),
                       _,
                       typeArgs,
                       _,
                       _,
                       _,
                       _) ->
      let modules = [ modName.idText ]
      let name, version =
        parseFn fnName.idText |> Exception.unwrapOptionInternal "invalid fn" []
      let typeArgs =
        typeArgs |> List.map (fun synType -> TypeReference.fromSynType synType)

      PT.EFnCall(gid (), PT.FQFnName.stdlibFqName modules name version, typeArgs, [])

    // Field access: a.b.c.d
    | SynExpr.LongIdent (_, SynLongIdent ([ var; f1; f2; f3 ], _, _), _, _) ->
      let obj1 =
        PT.EFieldAccess(id, PT.EVariable(gid (), var.idText), nameOrBlank f1.idText)
      let obj2 = PT.EFieldAccess(id, obj1, nameOrBlank f2.idText)
      PT.EFieldAccess(id, obj2, nameOrBlank f3.idText)

    // a.b.c
    | SynExpr.LongIdent (_, SynLongIdent ([ var; field1; field2 ], _, _), _, _) ->
      let obj1 =
        PT.EFieldAccess(
          id,
          PT.EVariable(gid (), var.idText),
          nameOrBlank field1.idText
        )
      PT.EFieldAccess(id, obj1, nameOrBlank field2.idText)

    // a.b
    | SynExpr.LongIdent (_, SynLongIdent ([ var; field ], _, _), _, _) ->
      PT.EFieldAccess(id, PT.EVariable(gid (), var.idText), nameOrBlank field.idText)

    // a.b
    | SynExpr.DotGet (expr, _, SynLongIdent ([ field ], _, _), _) ->
      PT.EFieldAccess(id, c expr, nameOrBlank field.idText)


    // Lambdas
    | SynExpr.Lambda (_,
                      false,
                      SynSimplePats.SimplePats (outerVars, _),
                      body,
                      _,
                      _,
                      _) ->
      let rec extractVarsAndBody expr =
        match expr with
        // The 2nd param indicates this was part of a lambda
        | SynExpr.Lambda (_, true, SynSimplePats.SimplePats (vars, _), body, _, _, _) ->
          let nestedVars, body = extractVarsAndBody body
          vars @ nestedVars, body
        // The 2nd param indicates this was not nested
        | SynExpr.Lambda (_, false, SynSimplePats.SimplePats (vars, _), body, _, _, _) ->
          vars, body
        | SynExpr.Lambda _ ->
          Exception.raiseInternal "TODO: other types of lambda" [ "expr", expr ]
        | _ -> [], expr

      let nestedVars, body = extractVarsAndBody body
      let vars =
        (outerVars @ nestedVars)
        |> List.map convertLambdaVar
        |> (List.map (fun name -> (gid (), name)))
      PT.ELambda(id, vars, c body)


    // if/else expressions
    | SynExpr.IfThenElse (cond, thenExpr, Some elseExpr, _, _, _, _) ->
      PT.EIf(id, c cond, c thenExpr, c elseExpr)

    // if (no else) expression
    | SynExpr.IfThenElse (cond, thenExpr, None, _, _, _, _) ->
      PT.EIf(id, c cond, c thenExpr, PT.EUnit(gid ()))


    // `let` bindings
    | SynExpr.LetOrUse (_,
                        _,
                        [ SynBinding (_, _, _, _, _, _, _, pat, _, rhs, _, _, _) ],
                        body,
                        _,
                        _) ->

      PT.ELet(id, LetPattern.fromSynPat ast pat, c rhs, c body)


    // `match` exprs:
    //
    // ```fsharp
    // match Some 1 with // 'cond'
    // | None -> ... // cases
    // | Some 1 -> ...
    // | ...
    | SynExpr.Match (_, cond, cases, _, _) ->
      let convertCase
        (SynMatchClause (pat, _, expr, _, _, _) : SynMatchClause)
        : PT.MatchPattern * PT.Expr =
        (MatchPattern.fromSynPat pat, c expr)
      PT.EMatch(id, c cond, List.map convertCase cases)


    // Dicts: `{ "A" = 2; "B"" = "yellow" }`
    | SynExpr.Record (_, _, fields, _) ->
      let fields =
        fields
        |> List.map (fun field ->
          match field with
          | SynExprRecordField ((SynLongIdent ([ name ], _, _), _), _, Some expr, _) ->
            (nameOrBlank name.idText, c expr)
          | f -> Exception.raiseInternal "Not an expected field" [ "field", f ])

      PT.EDict(id, fields)

    // Parens (eg `(5)`)
    | SynExpr.Paren (expr, _, _, _) -> c expr // just unwrap

    // "Typed" (we don't use this)
    | SynExpr.Typed (expr, _, _) -> c expr // just unwrap

    // Do (eg do ())
    | SynExpr.Do (expr, _) -> c expr // just unwrap


    // Sequential code: (a; b) -> let _ = a in b
    | SynExpr.Sequential (_, _, a, b, _) ->
      PT.ELet(id, PT.LPVariable(gid (), "_"), c a, c b)


    // Pipes (|>)
    // nested pipes - F# uses 2 Apps to represent a pipe. The outer app has an
    // op_PipeRight, and the inner app has two arguments. Those arguments might
    // also be pipes
    | SynExpr.App (_,
                   _,
                   SynExpr.Ident pipe,
                   SynExpr.App (_, _, nestedPipes, arg, _),
                   _)
    | SynExpr.App (_,
                   _,
                   SynExpr.LongIdent (_, SynLongIdent ([ pipe ], _, _), _, _),
                   SynExpr.App (_, _, nestedPipes, arg, _),
                   _) when pipe.idText = "op_PipeRight" ->
      match c nestedPipes with
      | PT.EPipe (id, arg1, Placeholder, []) ->
        // when we just built the lowest, the second one goes here
        PT.EPipe(id, arg1, cPlusPipeTarget arg, [])
      | PT.EPipe (id, arg1, arg2, rest) ->
        PT.EPipe(id, arg1, arg2, rest @ [ cPlusPipeTarget arg ])
      // Exception.raiseInternal $"Pipe: {nestedPipes},\n\n{arg},\n\n{pipe}\n\n, {c arg})"
      | other ->
        // Exception.raiseInternal $"Pipe: {nestedPipes},\n\n{arg},\n\n{pipe}\n\n, {c arg})"
        // the very bottom on the pipe chain, this is the first and second expressions
        PT.EPipe(id, other, cPlusPipeTarget arg, [])

    | SynExpr.App (_, _, SynExpr.Ident pipe, expr, _)
    | SynExpr.App (_,
                   _,
                   SynExpr.LongIdent (_, SynLongIdent ([ pipe ], _, _), _, _),
                   expr,
                   _) when pipe.idText = "op_PipeRight" ->
      // the very bottom on the pipe chain, this is just the first expression
      PT.EPipe(id, c expr, placeholder, [])

    // Records
    | SynExpr.App (_,
                   _,
                   SynExpr.LongIdent (_, SynLongIdent (names, _, _), _, _),
                   SynExpr.Record (_, _, fields, _),
                   _) when List.all (fun n -> String.isCapitalized (string n)) names ->
      let fields =
        fields
        |> List.map (fun field ->
          match field with
          | SynExprRecordField ((SynLongIdent ([ name ], _, _), _), _, Some expr, _) ->
            (nameOrBlank name.idText, c expr)
          | f -> Exception.raiseInternal "Not an expected field" [ "field", f ])

      // TYPESCLEANUP: use typename
      PT.ERecord(id, None, fields)

    // More records: MyRecord { x = 5 } or Dict { x = 5 }
    | SynExpr.App (_, _, SynExpr.Ident name, SynExpr.Record (_, _, fields, _), _) when
      String.isCapitalized (string name)
      ->
      let fields =
        fields
        |> List.map (fun field ->
          match field with
          | SynExprRecordField ((SynLongIdent ([ name ], _, _), _), _, Some expr, _) ->
            (nameOrBlank name.idText, c expr)
          | f -> Exception.raiseInternal "Not an expected field" [ "field", f ])

      if string name = "Dict" then
        PT.EDict(id, fields)
      else
        // TYPESCLEANUP: use typename
        PT.ERecord(id, None, fields)


    // Callers with multiple args are encoded as apps wrapping other apps.
    | SynExpr.App (_, _, funcExpr, arg, _) -> // function application (binops and fncalls)
      match c funcExpr with
      | PT.EFnCall (id, name, typeArgs, args) ->
        PT.EFnCall(id, name, typeArgs, args @ [ c arg ])
      | PT.EInfix (id, op, Placeholder, arg2) -> PT.EInfix(id, op, c arg, arg2)
      | PT.EInfix (id, op, arg1, Placeholder) -> PT.EInfix(id, op, arg1, c arg)
      // A pipe with one entry
      | PT.EPipe (id, arg1, Placeholder, []) ->
        PT.EPipe(id, arg1, cPlusPipeTarget arg, [])
      // A pipe with more than one entry
      | PT.EPipe (id, arg1, arg2, rest) ->
        PT.EPipe(id, arg1, arg2, rest @ [ cPlusPipeTarget arg ])
      | PT.EVariable (id, name) ->
        let (name, version) =
          parseFn name |> Exception.unwrapOptionInternal "invalid fn name" []
        if Set.contains name PT.FQFnName.oneWordFunctions then
          PT.EFnCall(id, PT.FQFnName.stdlibFqName [] name version, [], [ c arg ])
        else
          PT.EFnCall(id, PT.FQFnName.userFqName [] name version, [], [ c arg ])


      // Enums
      | PT.EConstructor (id, typeName, caseName, _fields) ->
        let fields =
          match c arg with
          | PT.ETuple (_, first, second, theRest) -> first :: second :: theRest
          | other -> [ other ]

        PT.EConstructor(id, typeName, caseName, fields)

      | e ->
        Exception.raiseInternal
          "Unsupported expression in app"
          [ "fnCall expr", funcExpr
            "converted specific fncall exp", e
            "argument", arg ]


    // Error handling
    | SynExpr.FromParseError _ as expr ->
      Exception.raiseInternal "There was a parser error parsing" [ "expr", expr ]
    | expr ->
      Exception.raiseInternal "Unsupported expression" [ "ast", ast; "expr", expr ]

  // Second pass of parsing, fixing the thing it's impossible to get right on the
  // first pass, such as function names. Parse the whole program once, and then run
  // this on any expressions.
  let fixupPass (functions : Set<PT.FQFnName.UserFnName>) (e : PT.Expr) : PT.Expr =
    e
    |> LibExecution.ProgramTypesAst.preTraversal (fun e ->
      match e with
      | PT.EVariable (id, name) ->
        match parseFn name with
        | Some (name, version) ->
          let name : PT.FQFnName.UserFnName =
            { modules = []; function_ = name; version = version }
          if Set.contains name functions then
            PT.EFnCall(
              id,
              PT.FQFnName.User(name),
              [],
              [ PT.Expr.EPipeTarget(gid ()) ]
            )
          else
            e
        // couldn't be parsed as a function, so leave it alone
        | None -> e

      | _ -> e)

module UserFunction =
  let rec parseArgPat (pat : SynPat) : PT.UserFunction.Parameter =
    let r = parseArgPat

    match pat with
    | SynPat.Paren (pat, _) -> r pat

    | SynPat.Const (SynConst.Unit, _) ->
      { id = gid (); name = "unit"; typ = PT.TUnit; description = "" }

    | SynPat.Typed (SynPat.Named (SynIdent (id, _), _, _, _), typ, _) ->
      { id = gid ()
        name = id.idText
        typ = TypeReference.fromSynType typ
        description = "" }

    | _ -> Exception.raiseInternal "Unsupported argPat" [ "pat", pat ]

  let private parseSignature
    (pat : SynPat)
    : string * List<string> * List<PT.UserFunction.Parameter> =
    match pat with
    | SynPat.LongIdent (SynLongIdent ([ name ], _, _), _, typeArgPats, argPats, _, _) ->
      let typeParams =
        match typeArgPats with
        | None -> []
        | Some (SynValTyparDecls (pats, _)) ->
          match pats with
          | None -> []
          | Some typeParams ->
            match typeParams with
            | SynTyparDecls.PostfixList (decls, constraints, _) ->
              match constraints with
              | [] ->
                decls
                |> List.map (fun decl ->
                  let SynTyparDecl (_, decl) = decl

                  match decl with
                  | SynTyparDecl (_, SynTypar (name, TyparStaticReq.None, _)) ->
                    name.idText
                  | _ ->
                    Exception.raiseInternal
                      "Unsupported type parameter"
                      [ "decl", decl ])
              | _ ->
                Exception.raiseInternal
                  "Unsupported constraints in function type arg declaration"
                  [ "pat", pat; "constraints", constraints ]

            | SynTyparDecls.PrefixList _
            | SynTyparDecls.SinglePrefix _ ->
              Exception.raiseInternal
                "Unsupported type params of function declaration"
                [ "pat", pat; "typeParams", typeParams ]

      let parameters =
        match argPats with
        | SynArgPats.Pats pats -> List.map parseArgPat pats

        | SynArgPats.NamePatPairs _ ->
          Exception.raiseInternal "Unsupported pattern" [ "pat", pat ]

      (name.idText, typeParams, parameters)

    | _ -> Exception.raiseInternal "Unsupported pattern" [ "pat", pat ]

  let parseReturnInfo
    (returnInfo : Option<SynBindingReturnInfo>)
    : PT.TypeReference =
    match returnInfo with
    | None -> Exception.raiseInternal "Functions must have return types specified" []
    | Some (SynBindingReturnInfo (typeName, _, _, _)) ->
      TypeReference.fromSynType typeName


  let fromSynBinding (binding : SynBinding) : PT.UserFunction.T =
    match binding with
    | SynBinding (_, _, _, _, _, _, _, pat, returnInfo, expr, _, _, _) ->
      let (name, typeParams, parameters) = parseSignature pat
      let returnType = parseReturnInfo returnInfo
      let (name, version) =
        Expr.parseFn name |> Exception.unwrapOptionInternal "invalid fn name" []
      { tlid = gid ()
        name = PT.FQFnName.userFnName [] name version
        typeParams = typeParams
        parameters = parameters
        returnType = returnType
        description = ""
        infix = false
        body = Expr.fromSynExpr expr }


module CustomType =
  module Enum =
    let private parseField (typ : SynField) : PT.CustomType.EnumField =
      match typ with
      | SynField (_, _, fieldName, typ, _, _, _, _, _) ->
        { typ = TypeReference.fromSynType typ
          label = fieldName |> Option.map (fun id -> id.idText) }

    let private parseCase (case : SynUnionCase) : PT.CustomType.EnumCase =
      match case with
      | SynUnionCase (_, SynIdent (id, _), typ, _, _, _, _) ->
        match typ with
        | SynUnionCaseKind.Fields fields ->
          { name = id.idText; fields = List.map parseField fields }
        | _ -> Exception.raiseInternal $"Unsupported enum case" [ "case", case ]

    let fromCases typeDef (cases : List<SynUnionCase>) =
      let firstCase, additionalCases =
        match cases with
        | [] ->
          Exception.raiseInternal
            $"Can't parse enum without any cases"
            [ "typeDef", typeDef ]
        | firstCase :: additionalCases -> firstCase, additionalCases

      PT.CustomType.Enum(parseCase firstCase, List.map parseCase additionalCases)

  module Record =
    let private parseField (field : SynField) : PT.CustomType.RecordField =
      match field with
      | SynField (_, _, Some id, typ, _, _, _, _, _) ->
        { name = id.idText; typ = TypeReference.fromSynType typ }
      | _ -> Exception.raiseInternal $"Unsupported field" [ "field", field ]

    let fromFields typeDef (fields : List<SynField>) =
      match fields with
      | [] ->
        Exception.raiseInternal
          $"Unsupported record type with no fields"
          [ "typeDef", typeDef ]
      | firstField :: additionalFields ->

        PT.CustomType.Record(
          parseField firstField,
          List.map parseField additionalFields
        )

module UserType =
  let fromSynTypeDefn (typeDef : SynTypeDefn) : PT.UserType.T =
    match typeDef with
    | SynTypeDefn (SynComponentInfo (_, _params, _, [ id ], _, _, _, _),
                   SynTypeDefnRepr.Simple (SynTypeDefnSimpleRepr.Record (_, fields, _),
                                           _),
                   _,
                   _,
                   _,
                   _) ->
      { tlid = gid ()
        name = { modules = []; typ = id.idText; version = 0 }
        definition = CustomType.Record.fromFields typeDef fields }

    | SynTypeDefn (SynComponentInfo (_, _params, _, [ id ], _, _, _, _),
                   SynTypeDefnRepr.Simple (SynTypeDefnSimpleRepr.Union (_, cases, _),
                                           _),
                   _,
                   _,
                   _,
                   _) ->
      { tlid = gid ()
        name = { modules = []; typ = id.idText; version = 0 }
        definition = CustomType.Enum.fromCases typeDef cases }

    | _ ->
      Exception.raiseInternal $"Unsupported type definition" [ "typeDef", typeDef ]


let parseExprWithTypes (code : string) : PT.Expr =
  code
  |> Utils.parseAsFSharpSourceFile
  |> Utils.singleExprFromImplFile
  |> Expr.fromSynExpr

let parseExpr = parseExprWithTypes
