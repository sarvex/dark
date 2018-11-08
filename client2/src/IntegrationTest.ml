open Tea
open! Porting
module B = Blank
module P = Pointer
open Prelude
module TL = Toplevel
open Types

let pass : testResult = Ok ()

let fail ?(f : 'a -> string = toString) (v: 'a)  : testResult =
  Error (f v)

let onlyTL (m : model) : toplevel =
  let len = List.length m.toplevels in
  let _ =
    if len = 0
    then Debug.crash "no toplevels"
    else if len > 1 then Debug.crash ("too many toplevels: " ^ (show_list show_toplevel m.toplevels))
    else "nothing to see here"
  in
  m.toplevels |> List.head |> deOption "onlytl1"

let onlyHandler (m : model) : handler =
  m |> onlyTL |> TL.asHandler |> deOption "onlyhandler"

let onlyDB (m : model) : dB = m |> onlyTL |> TL.asDB |> deOption "onlyDB"

let onlyExpr (m : model) : nExpr =
  m |> onlyHandler |> (fun x -> x.ast) |> B.asF |> deOption "onlyast4"

let enter_changes_state (m : model) : testResult =
  match m.cursorState with
  | Entering (Creating _) -> pass
  | _ -> fail ~f:show_cursorState m.cursorState

let field_access (m : model) : testResult =
  match onlyExpr m with
  | FieldAccess (F (_, Variable "request"), F (_, "body")) -> pass
  | expr -> fail ~f:show_nExpr expr

let field_access_closes (m : model) : testResult =
  match m.cursorState with
  | Entering (Filling (_, id)) ->
      let ast =
        onlyTL m |> TL.asHandler |> deOption "test" |> fun x -> x.ast
      in
      if AST.allData ast |> List.filter P.isBlank = [] then pass
      else
        fail ~f:(show_list show_pointerData) (TL.allBlanks (onlyTL m))
  | _ -> fail ~f:show_cursorState m.cursorState

let field_access_pipes (m : model) : testResult =
  match onlyExpr m with
  | Thread
      [F (_, FieldAccess (F (_, Variable "request"), F (_, "body"))); Blank _]
    ->
      pass
  | expr -> fail ~f:show_nExpr expr

let field_access_nested (m : model) : testResult =
  match onlyExpr m with
  | FieldAccess
      ( F
          ( _
          , FieldAccess
              ( F (_, FieldAccess (F (_, Variable "request"), F (_, "body")))
              , F (_, "field") ) )
      , F (_, "field2") ) ->
      pass
  | expr -> fail ~f:show_nExpr expr

let pipeline_let_equals (m : model) : testResult =
  let astR =
    match onlyExpr m with
    | Let (F (_, "value"), F (_, Value "3"), Blank _) -> pass
    | e -> fail ~f:show_nExpr e
  in
  let stateR =
    match m.cursorState with
    | Entering _ -> pass
    | _ -> fail ~f:show_cursorState m.cursorState
  in
  Result.map2 (fun () () -> ()) astR stateR

let pipe_within_let (m : model) : testResult =
  match onlyExpr m with
  | Let
      ( F (_, "value")
      , F (_, Value "3")
      , F
          ( _
          , Thread
              [ F (_, Variable "value")
              ; F (_, FnCall ("assoc", [Blank _; Blank _], _)) ] ) ) ->
      pass
  | e -> fail ~f:show_nExpr e

let tabbing_works (m : model) : testResult =
  match onlyExpr m with
  | If (Blank _, F (_, Value "5"), Blank _) -> pass
  | e -> fail ~f:show_nExpr e

let left_right_works (m : model) : testResult =
  let h = onlyHandler m in
  match m.cursorState with
  | Selecting (tlid, Some id) -> (
      let pd = TL.getTL m tlid |> fun tl -> TL.find tl id in
      match pd with
      | Some (PEventSpace _) -> pass
      | other -> fail (m.cursorState, other))
  | s -> fail ~f:show_cursorState m.cursorState

let varbinds_are_editable (m : model) : testResult =
  match onlyExpr m with
  | Let (F (id1, "var"), Blank _, Blank _) as l -> (
    match m.cursorState with
    | Entering (Filling (_, id2)) ->
        if id1 = id2
        then pass
        else fail (l, m.cursorState)
    | s -> fail (l, m.cursorState))
  | e -> fail ~f: show_nExpr e

let editing_request_edits_request (m : model) : testResult =
  match onlyExpr m with
  | FieldAccess (F (id1, Variable "request"), Blank _) -> (
    match m.complete.completions with
    | [_; cs; _; _; _; _] -> (
      match cs with
      | [ACVariable "request"] -> pass
      | _ -> fail cs
    )
    | allcs -> fail ~f:(show_list (show_list show_autocompleteItem)) allcs
  )
  | e -> fail ~f:show_nExpr e

let autocomplete_highlights_on_partial_match (m : model) : testResult =
  match onlyExpr m with
  | FnCall ("Int::add", _, _) -> pass
  | e -> fail ~f:show_nExpr e

let no_request_global_in_non_http_space (m : model) : testResult =
  match onlyExpr m with
  | FnCall ("Http::badRequest", _, _) -> pass
  | e -> fail ~f:show_nExpr e

let hover_values_for_varnames (m : model) : testResult =
  let tlid = m.toplevels |> List.head |> deOption "test" |> fun x -> x.id in
  pass

let pressing_up_doesnt_return_to_start (m : model) : testResult =
  match onlyExpr m with
  | FnCall ("Char::toASCIIChar", _, _) -> pass
  | e -> fail ~f:show_nExpr e

let deleting_selects_the_blank (m : model) : testResult =
  match onlyExpr m with
  | Value "6" -> pass
  | e -> fail ~f:show_nExpr e

let right_number_of_blanks (m : model) : testResult =
  match onlyExpr m with
  | FnCall ("assoc", [Blank _; Blank _; Blank _], _) -> pass
  | e -> fail ~f:show_nExpr e

let ellen_hello_world_demo (m : model) : testResult =
  let spec = onlyTL m |> TL.asHandler |> deOption "hw2" |> fun x -> x.spec in
  match ((spec.module_, spec.name), (spec.modifier, onlyExpr m)) with
  | (F (_, "HTTP"), F (_, "/hello")), (F (_, "GET"), Value "\"Hello world!\"")
    ->
      pass
  | other -> fail other

let editing_does_not_deselect (m : model) : testResult =
  match m.cursorState with
  | Entering (Filling (tlid, id)) -> (
      let pd = TL.getTL m tlid |> fun tl -> TL.find tl id in
      match pd with
      | Some (PExpr (F (_, Value "\"hello zane\""))) -> pass
      | other -> fail other )
  | other -> fail ~f:show_cursorState other

let editing_headers (m : model) : testResult =
  let spec = onlyTL m |> TL.asHandler |> deOption "hw2" |> fun x -> x.spec in
  match (spec.module_, spec.name, spec.modifier) with
  | F (_, "HTTP"), F (_, "/myroute"), F (_, "GET") -> pass
  | other -> fail other

let tabbing_through_let (m : model) : testResult =
  match onlyExpr m with
  | Let (F (_, "myvar"), F (_, Value "5"), F (_, Value "5")) -> pass
  | e -> fail ~f:show_nExpr e

let case_sensitivity (m : model) : testResult =
  if List.length m.toplevels <> 3 then fail m.toplevels
  else
    m.toplevels
    |> List.map (fun tl ->
           match tl.data with
           | TLDB {dbName; cols} -> (
             match (dbName, cols) with
             | ( "TestUnicode"
               , [ (F (_, "cOlUmNnAmE"), F (_, "Str"))
                 ; ( Blank _, Blank _)
                 ] ) ->
                 pass
             | other -> fail other )
           | TLHandler h -> (
             match h.ast with
             | F
                 ( _
                 , Thread
                     [ F (_, Value "{}")
                     ; F ( _ , FnCall
                             ( "assoc"
                             , [ F (_, Value "\"cOlUmNnAmE\"")
                               ; F (_, Value "\"some value\"")
                               ]
                             , _ ) )
                     ; F
                         ( _
                         , FnCall
                             ("DB::insert", [F (_, Variable "TestUnicode")], _)
                         )
                     ] ) ->
                 pass
             | F
                 ( id
                 , Thread
                     [ F (_, Variable "TestUnicode")
                     ; F (_, FnCall ("DB::fetchAll", [], _))
                     ; F (_, FnCall ("List::head", [], _))
                     ; F
                         ( _
                         , Lambda
                             ( [F (_, "var")]
                             , F
                                 ( _
                                 , FieldAccess
                                     ( F (_, Variable "var")
                                     , F (_, "cOlUmNnAmE") ) ) ) )
                     ] ) ->
                 Analysis.getCurrentLiveValue m tl.id id
                 |> Option.map (fun lv ->
                        if lv = DStr "some value"
                        then pass
                        else fail ~f:show_dval lv)
                 |> Option.withDefault (fail ~f:show_expr h.ast)
             | _ -> fail ~f:show_expr h.ast )
           | other -> fail other )
    |> Result.combine
    |> Result.map (fun _ -> ())

let focus_on_ast_in_new_empty_tl (m : model) : testResult =
  match (onlyHandler m).ast with
  | Blank id ->
      if idOf m.cursorState = Some id then pass else fail (id, m.cursorState)
  | e -> fail ~f:show_expr e

let focus_on_path_in_new_filled_tl (m : model) : testResult =
  match (onlyHandler m).spec.name with
  | Blank id ->
      if idOf m.cursorState = Some id then pass else fail (id, m.cursorState)
  | e -> fail e

let focus_on_cond_in_new_tl_with_if (m : model) : testResult =
  match onlyExpr m with
  | If (cond, _, _) ->
      if idOf m.cursorState = Some (B.toID cond) then pass
      else fail ~f:show_cursorState m.cursorState
  | e -> fail ~f:show_nExpr e

let dont_shift_focus_after_filling_last_blank (m : model) : testResult =
  match m.cursorState with
  | Selecting (_, mId) ->
      if
        mId
        = ( m |> onlyHandler
          |> (fun x -> x.spec)
          |> (fun x -> x.modifier)
          |> B.toID
          |> fun x -> Some x )
      then pass
      else fail (m.toplevels, m.cursorState)
  | s -> fail (m.toplevels, m.cursorState)

let rename_db_fields (m : model) : testResult =
  m.toplevels
  |> List.map (fun tl ->
         match tl.data with
         | TLDB {dbName; cols} -> (
           match cols with
           | [ (Blank _, Blank _)
             ; (F (_, "field2"), F (_, "String"))
             ; (F (id, "field6"), F (_, "String")) ] -> (
             match m.cursorState with
             | Selecting (_, None) -> pass
             | _ -> fail ~f:show_cursorState m.cursorState )
           | _ -> fail cols )
         | _ -> pass )
  |> Result.combine
  |> Result.map (fun _ -> ())

let rename_db_type (m : model) : testResult =
  m.toplevels
  |> List.map (fun tl ->
         match tl.data with
         | TLDB {dbName; cols} -> (
           match cols with
           | [ (Blank _, Blank _)
             ; (F (_, "field2"), F (_, "Int"))
             ; (F (_, "field1"), F (id, "String")) ] -> (
             match m.cursorState with
             | Selecting (_, Some sid) ->
                 if sid = id then pass else fail (cols, m.cursorState)
             | _ -> fail ~f:show_cursorState m.cursorState )
           | _ -> fail cols )
         | _ -> pass )
  |> Result.combine
  |> Result.map (fun _ -> ())

let paste_right_number_of_blanks (m : model) : testResult =
  m.toplevels
  |> List.map (fun tl ->
         match tl.data with
         | TLHandler {ast} -> (
           match ast with
             | F (_, Thread [_; F (_, FnCall ("-", [Blank _], _))]) -> pass
           | F (_, FnCall ("-", [Blank _; Blank _], _)) -> pass
           | _ -> fail ~f:show_expr ast )
         | _ -> fail ("Shouldn't be other handlers here", tl.data) )
  |> Result.combine
  |> Result.map (fun _ -> ())

let paste_keeps_focus (m : model) : testResult =
  match onlyExpr m with
  | FnCall ("+", [F (id, Value "3"); F (_, Value "3")], _) as fn -> (
    match m.cursorState with
    | Selecting (_, sid) ->
        if Some id = sid then pass else fail (fn, m.cursorState)
    | _ -> fail (fn, m.cursorState) )
  | other -> fail ~f:show_nExpr other

let nochange_for_failed_paste (m : model) : testResult =
  match onlyExpr m with
  | Let (F (id, "x"), F (_, Value "2"), _) -> (
    match m.cursorState with
    | Selecting (_, sid) ->
      if Some id = sid
      then pass
      else fail ~f:show_cursorState m.cursorState
    | _ -> fail ~f:show_cursorState m.cursorState )
  | other -> fail ~f:show_nExpr other

let feature_flag_works (m : model) : testResult =
  let h = onlyHandler m in
  let ast = h.ast in
  match ast with
  | F
      ( _
      , Let
          ( F (_, "a")
          , F (_, Value "13")
          , F
              ( id
              , FeatureFlag
                  ( F (_, "myflag")
                  , F
                      ( _
                      , FnCall
                          ( "Int::greaterThan"
                          , [F (_, Value "10"); F (_, Variable "a")]
                          , _ ) )
                  , F (_, Value "\"A\"")
                  , F (_, Value "\"B\"") ) ) ) ) -> (
      let res = Analysis.getCurrentLiveValue m h.tlid id in
      match res with
      | Some val_ -> if val_ = DStr "B" then pass else fail (ast, val_)
      | _ -> fail (ast, res) )
  | _ -> fail (ast, m.cursorState)

let feature_flag_in_function (m : model) : testResult =
  let fun_ = List.head m.userFunctions in
  match fun_ with
  | Some f -> (
    match f.ufAST with
    | F
        ( id
        , FnCall
            ( "+"
            , [ F ( _
                  , FeatureFlag
                      ( F (_, "myflag")
                      , F (_, Value "true")
                      , F (_, Value "5")
                      , F (_, Value "3") ) )
              ; F (_, Value "5")
            ]
            , NoRail ) ) ->
        pass
    | _ -> fail (f.ufAST, None) )
  | None -> fail ("Cant find function", None)

let simple_tab_ordering (m : model) : testResult =
  let ast = onlyHandler m |> fun x -> x.ast in
  match ast with
  | F (_, Let (Blank _, F (_, Value "4"), Blank id)) -> (
    match m.cursorState with
    | Entering (Filling (_, sid)) ->
        if id = sid then pass else fail (ast, m.cursorState, id)
    | _ -> fail (ast, m.cursorState, id) )
  | _ -> fail (ast, m.cursorState)

let variable_extraction (m : model) : testResult =
  let ast = onlyHandler m |> fun x -> x.ast in
  match ast with
  | F
      ( _
      , Let
          ( F (_, "foo")
          , F (_, Value "1")
          , F
              ( _
              , Let
                  ( F (_, "bar")
                  , F (_, Value "2")
                  , F
                      ( _
                      , Let
                          ( F (_, "new_variable")
                          , F
                              ( _
                              , FnCall
                                  ( "+"
                                  , [ F (_, Variable "foo")
                                    ; F (_, Variable "bar") ]
                                  , _ ) )
                          , F
                              ( _
                              , Let
                                  ( F (_, "baz")
                                  , F (_, Value "5")
                                  , F (_, Variable "new_variable") ) ) ) ) ) )
          ) ) ->
      pass
  | _ -> fail (ast, m.cursorState)

let invalid_syntax (m : model) : testResult =
  match onlyHandler m |> fun x -> x.ast with
  | Blank id -> (
    match m.cursorState with
    | Entering (Filling (_, sid)) ->
        if id = sid then pass else fail m.cursorState
    | _ -> fail m.cursorState )
  | other -> fail ~f:show_expr other

let editing_stays_in_same_place_with_enter (m : model) : testResult =
  match (m.cursorState, onlyExpr m) with
  | Selecting (_, id1), Let (F (id2, "v2"), _, _) ->
      if id1 = Some id2 then pass else fail (m.cursorState, onlyExpr m)
  | other -> fail other

let editing_goes_to_next_with_tab (m : model) : testResult =
  match (m.cursorState, onlyExpr m) with
  | Entering (Filling (_, id1)), Let (F (_, "v2"), Blank id2, _) ->
      if id1 = id2 then pass else fail (m.cursorState, onlyExpr m)
  | other -> fail other

let editing_starts_a_thread_with_shift_enter (m : model) : testResult =
  match (m.cursorState, onlyExpr m) with
  | ( Entering (Filling (_, id1))
    , Let (_, F (_, Thread [F (_, Value "52"); Blank id2]), _) ) ->
      if id1 = id2 then pass else fail (m.cursorState, onlyExpr m)
  | other -> fail other

let object_literals_work (m : model) : testResult =
  match (m.cursorState, onlyExpr m) with
  | ( Entering (Filling (tlid, id))
    , ObjectLiteral
        [ (F (_, "k1"), Blank _)
        ; (F (_, "k2"), F (_, Value "2"))
        ; (F (_, "k3"), F (_, Value "3"))
        ; (F (_, "k4"), Blank _)
        ; (Blank _, Blank _)
        ] ) -> (
      let tl = TL.getTL m tlid in
      let target = TL.findExn tl id in
      match target with
      | PEventName _ -> pass
      | other -> fail (m.cursorState, other) )
  | other -> fail other

let rename_function (m : model) : testResult =
  match onlyExpr m with
  | FnCall ("hello", _, _) -> pass
  | other -> fail ~f:show_nExpr other

let sending_to_rail_works (m : model) : testResult =
  let ast = onlyHandler m |> fun x -> x.ast in
  match ast with
  | F (_, FnCall ("List::head_v1", _, NoRail)) -> pass
  | _ -> fail ~f:show_expr ast

let execute_function_works (m : model) : testResult = pass

let function_version_renders (m : model) : testResult = pass

let only_backspace_out_of_strings_on_last_char (m : model) : testResult =
  let ast = onlyHandler m |> fun x -> x.ast in
  if m.complete.value = "" then pass else fail ast

let trigger (test_name : string) : integrationTestState =
  let name = String.dropLeft 5 test_name in
  IntegrationTestExpectation
    ( match name with
    | "enter_changes_state" -> enter_changes_state
    | "field_access" -> field_access
    | "field_access_closes" -> field_access_closes
    | "field_access_pipes" -> field_access_pipes
    | "field_access_nested" -> field_access_nested
    | "pipeline_let_equals" -> pipeline_let_equals
    | "pipe_within_let" -> pipe_within_let
    | "tabbing_works" -> tabbing_works
    | "left_right_works" -> left_right_works
    | "varbinds_are_editable" -> varbinds_are_editable
    | "editing_request_edits_request" -> editing_request_edits_request
    | "autocomplete_highlights_on_partial_match" ->
        autocomplete_highlights_on_partial_match
    | "no_request_global_in_non_http_space" ->
        no_request_global_in_non_http_space
    | "hover_values_for_varnames" -> hover_values_for_varnames
    | "pressing_up_doesnt_return_to_start" ->
        pressing_up_doesnt_return_to_start
    | "deleting_selects_the_blank" -> deleting_selects_the_blank
    | "right_number_of_blanks" -> right_number_of_blanks
    | "ellen_hello_world_demo" -> ellen_hello_world_demo
    | "editing_does_not_deselect" -> editing_does_not_deselect
    | "editing_headers" -> editing_headers
    | "tabbing_through_let" -> tabbing_through_let
    | "case_sensitivity" -> case_sensitivity
    | "focus_on_ast_in_new_empty_tl" -> focus_on_ast_in_new_empty_tl
    | "focus_on_path_in_new_filled_tl" -> focus_on_path_in_new_filled_tl
    | "focus_on_cond_in_new_tl_with_if" -> focus_on_cond_in_new_tl_with_if
    | "dont_shift_focus_after_filling_last_blank" ->
        dont_shift_focus_after_filling_last_blank
    | "rename_db_fields" -> rename_db_fields
    | "rename_db_type" -> rename_db_type
    | "paste_right_number_of_blanks" -> paste_right_number_of_blanks
    | "paste_keeps_focus" -> paste_keeps_focus
    | "nochange_for_failed_paste" -> nochange_for_failed_paste
    | "feature_flag_works" -> feature_flag_works
    | "simple_tab_ordering" -> simple_tab_ordering
    | "variable_extraction" -> variable_extraction
    | "invalid_syntax" -> invalid_syntax
    | "editing_stays_in_same_place_with_enter" ->
        editing_stays_in_same_place_with_enter
    | "editing_goes_to_next_with_tab" -> editing_goes_to_next_with_tab
    | "editing_starts_a_thread_with_shift_enter" ->
        editing_starts_a_thread_with_shift_enter
    | "object_literals_work" -> object_literals_work
    | "rename_function" -> rename_function
    | "sending_to_rail_works" -> sending_to_rail_works
    | "feature_flag_in_function" -> feature_flag_in_function
    | "execute_function_works" -> execute_function_works
    | "function_version_renders" -> function_version_renders
    | "only_backspace_out_of_strings_on_last_char" ->
        only_backspace_out_of_strings_on_last_char
    | n -> Debug.crash ("Test " ^ n ^ " not added to IntegrationTest.trigger")
    )
