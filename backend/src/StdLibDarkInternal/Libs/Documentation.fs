/// StdLib functions for documentation
module StdLibDarkInternal.Libs.Documentation

open System.Threading.Tasks

open Prelude

open LibExecution.RuntimeTypes
open LibExecution.StdLib.Shortcuts

let modul = [ "DarkInternal"; "Documentation" ]

let typ (name : string) (version : int) : FQTypeName.StdlibTypeName =
  FQTypeName.stdlibTypeName' modul name version

let fn (name : string) (version : int) : FQFnName.StdlibFnName =
  FQFnName.stdlibFnName' modul name version





let types : List<BuiltInType> =
  [ { name = typ "Function" 0
      typeParams = []
      definition =
        CustomType.Record(
          { name = "name"; typ = TString },
          [ { name = "description"; typ = TString }
            { name = "parameters"; typ = TList(TString) }
            { name = "returnType"; typ = TString } ]
        )
      deprecated = NotDeprecated
      description = "A Darklang stdlib function" }
    { name = typ "Parameter" 0
      typeParams = []
      definition =
        CustomType.Record(
          { name = "name"; typ = TString },
          [ { name = "type"; typ = TString } ]
        )
      deprecated = NotDeprecated
      description = "A function parameter" } ]


let fns : List<BuiltInFn> =
  [ { name = fn "list" 0
      typeParams = []
      parameters = []
      returnType = TList(TCustomType(FQTypeName.Stdlib(typ "Function" 0), []))
      description =
        "Returns a list of Function records, representing the functions available in the standard library. Does not return DarkInternal functions"
      fn =
        (function
        | state, _, [] ->
          let typeName = LibExecution.DvalReprDeveloper.typeName
          state.libraries.stdlibFns
          |> Map.toList
          |> List.filter (fun (key, data) ->
            (not (FQFnName.isInternalFn key)) && data.deprecated = NotDeprecated)
          |> List.map (fun (key, data) ->
            let alist =
              let returnType = typeName data.returnType
              let parameters =
                data.parameters
                |> List.map (fun p ->
                  Dval.record [ ("name", DString p.name)
                                ("type", DString(typeName p.typ)) ])
              [ ("name", DString(FQFnName.toString key))
                ("documentation", DString data.description)
                ("parameters", DList parameters)
                ("returnType", DString returnType) ]
            Dval.record alist)
          |> DList
          |> Ply
        | _ -> incorrectArgs ())
      sqlSpec = NotQueryable
      previewable = Impure
      deprecated = NotDeprecated } ]

let contents = (fns, types)
