module LibExecutionStdLib.LibMath

open System.Threading.Tasks
open System.Numerics
open FSharp.Control.Tasks
open FSharpPlus

open LibExecution.RuntimeTypes
open Prelude

module Errors = LibExecution.Errors

let fn = FQFnName.stdlibFnName

let err (str : string) = Ply(Dval.errStr str)

let incorrectArgs = LibExecution.Errors.incorrectArgs

let varA = TVariable "a"
let varB = TVariable "b"

let fns : List<BuiltInFn> = []
// [ { name = fn "Math" "pi" 0
//     parameters = []
//     returnType = TFloat
//     description =
//       "Returns an approximation for the mathematical constant π, the ratio of a circle's circumference to its diameter."
//     fn =
//       (function
//       | _, [] -> Value(DFloat System.Math.PI)
//       | _ -> incorrectArgs ())
//     sqlSpec = NotYetImplementedTODO
//     previewable = Pure
//     deprecated = NotDeprecated }
//   { name = fn "Math" "tau" 0
//     parameters = []
//     returnType = TFloat
//     description =
//       "Returns an approximation for the mathematical constant τ, the number of radians in one turn. Equivalent to `Float::multiply Math::pi 2`."
//     fn =
//       (function
//       | _, [] -> Value(DFloat System.Math.Tau)
//       | _ -> incorrectArgs ())
//     sqlSpec = NotYetImplementedTODO
//     previewable = Pure
//     deprecated = NotDeprecated }
//   { name = fn "Math" "degrees" 0
//     parameters = [ Param.make "angleInDegrees" TFloat "" ]
//     returnType = TFloat
//     description =
//       "Returns the equivalent of `angleInDegrees` in radians, the unit used by all of Dark's trigonometry functions.
//        There are 360 degrees in a circle."
//     fn =
//       (function
//       | _, [ DFloat degrees ] -> Value(DFloat(degrees * System.Math.PI / 180.0))
//       | _ -> incorrectArgs ())
//     sqlSpec = NotYetImplementedTODO
//     previewable = Pure
//     deprecated = NotDeprecated }
//   { name = fn "Math" "turns" 0
//     parameters = [ Param.make "angleInTurns" TFloat "" ]
//     returnType = TFloat
//     description =
//       "Returns the equivalent of `angleInTurns` in radians, the unit used by all of Dark's trigonometry functions.
//        There is 1 turn in a circle."
//     fn =
//       (function
//       | _, [ DFloat turns ] -> Value(DFloat(System.Math.Tau * turns))
//       | _ -> incorrectArgs ())
//     sqlSpec = NotYetImplementedTODO
//     previewable = Pure
//     deprecated = NotDeprecated }
//   { name = fn "Math" "radians" 0
//     parameters = [ Param.make "angleInRadians" TFloat "" ]
//     returnType = TFloat
//     description =
//       "Returns `angleInRadians` in radians, the unit used by all of Dark's trigonometry functions.
//       There are `Float::multiply 2 Math::pi` radians in a circle."
//     fn =
//       (function
//       | _, [ DFloat rads ] -> Value(DFloat rads)
//       | _ -> incorrectArgs ())
//     sqlSpec = NotYetImplementedTODO
//     previewable = Pure
//     deprecated = NotDeprecated }
//   { name = fn "Math" "cos" 0
//     parameters = [ Param.make "angleInRadians" TFloat "" ]
//     returnType = TFloat
//     description =
//       "Returns the cosine of the given `angleInRadians`.
//        One interpretation of the result relates to a right triangle: the cosine is the ratio of the lengths of the side adjacent to the angle and the hypotenuse."
//     fn =
//       (function
//       | _, [ DFloat a ] -> Value(DFloat(System.Math.Cos a))
//       | _ -> incorrectArgs ())
//     sqlSpec = NotYetImplementedTODO
//     previewable = Pure
//     deprecated = NotDeprecated }
//   { name = fn "Math" "sin" 0
//     parameters = [ Param.make "angleInRadians" TFloat "" ]
//     returnType = TFloat
//     description =
//       "Returns the sine of the given `angleInRadians`.
//        One interpretation of the result relates to a right triangle: the sine is the ratio of the lengths of the side opposite the angle and the hypotenuse."
//     fn =
//       (function
//       | _, [ DFloat a ] -> Value(DFloat(System.Math.Sin a))
//       | _ -> incorrectArgs ())
//     sqlSpec = NotYetImplementedTODO
//     previewable = Pure
//     deprecated = NotDeprecated }
//   { name = fn "Math" "tan" 0
//     parameters = [ Param.make "angleInRadians" TFloat "" ]
//     returnType = TFloat
//     description =
//       "Returns the tangent of the given `angleInRadians`.
//        One interpretation of the result relates to a right triangle: the tangent is the ratio of the lengths of the side opposite the angle and the side adjacent to the angle."
//     fn =
//       (function
//       | _, [ DFloat a ] -> Value(DFloat(System.Math.Tan a))
//       | _ -> incorrectArgs ())
//     sqlSpec = NotYetImplementedTODO
//     previewable = Pure
//     deprecated = NotDeprecated }
//   { name = fn "Math" "acos" 0
//     parameters = [ Param.make "ratio" TFloat "" ]
//     returnType = TOption varA
//     description =
//       "Returns the arc cosine of `ratio`, as an Option.
//        If `ratio` is in the inclusive range `[-1.0, 1.0]`, returns
//        `Just result` where `result` is in radians and is between `0.0` and `Math::pi`. Otherwise, returns `Nothing`.
//        This function is the inverse of `Math::cos`."
//     fn =
//       (function
//       | _, [ DFloat r ] ->
//         let res = System.Math.Acos r in

//         if System.Double.IsNaN res then
//           Value(DOption None)
//         else
//           Value(DOption(Some(DFloat res)))
//       | _ -> incorrectArgs ())
//     sqlSpec = NotYetImplementedTODO
//     previewable = Pure
//     deprecated = NotDeprecated }
//   { name = fn "Math" "asin" 0
//     parameters = [ Param.make "ratio" TFloat "" ]
//     returnType = TOption varA
//     description =
//       "Returns the arc sine of `ratio`, as an Option.
//        If `ratio` is in the inclusive range `[-1.0, 1.0]`, returns
//        `Just result` where `result` is in radians and is between `-Math::pi/2` and `Math::pi/2`. Otherwise, returns `Nothing`.
//        This function is the inverse of `Math::sin`."
//     fn =
//       (function
//       | _, [ DFloat r ] ->
//         let res = System.Math.Asin r in

//         if System.Double.IsNaN res then
//           Value(DOption None)
//         else
//           Value(DOption(Some(DFloat res)))
//       | _ -> incorrectArgs ())
//     sqlSpec = NotYetImplementedTODO
//     previewable = Pure
//     deprecated = NotDeprecated }
//   { name = fn "Math" "atan" 0
//     parameters = [ Param.make "ratio" TFloat "" ]
//     returnType = TFloat
//     description =
//       "Returns the arc tangent of `ratio`. The result is in radians and is between `-Math::pi/2` and `Math::pi/2`.
//        This function is the inverse of `Math::tan`. Use `Math::atan2` to expand the output range, if you know the numerator and denominator of `ratio`."
//     fn =
//       (function
//       | _, [ DFloat a ] -> Value(DFloat(System.Math.Atan a))
//       | _ -> incorrectArgs ())
//     sqlSpec = NotYetImplementedTODO
//     previewable = Pure
//     deprecated = NotDeprecated }
//   { name = fn "Math" "atan2" 0
//     parameters = [ Param.make "y" TFloat ""; Param.make "x" TFloat "" ]
//     returnType = TFloat
//     description =
//       "Returns the arc tangent of `y / x`, using the signs of `y` and `x` to determine the quadrant of the result.
//        The result is in radians and is between `-Math::pi` and `Math::pi`. Consider `Math::atan` if you know the value of `y / x` but not the individual values `x` and `y`."
//     fn =
//       (function
//       | _, [ DFloat y; DFloat x ] -> Value(DFloat(System.Math.Atan2(y, x)))
//       | _ -> incorrectArgs ())
//     sqlSpec = NotYetImplementedTODO
//     previewable = Pure
//     deprecated = NotDeprecated }
//   { name = fn "Math" "cosh" 0
//     parameters = [ Param.make "angleInRadians" TFloat "" ]
//     returnType = TFloat
//     description = "Returns the hyperbolic cosine of `angleInRadians`."
//     fn =
//       (function
//       | _, [ DFloat a ] -> Value(DFloat(System.Math.Cosh a))
//       | _ -> incorrectArgs ())
//     sqlSpec = NotYetImplementedTODO
//     previewable = Pure
//     deprecated = NotDeprecated }
//   { name = fn "Math" "sinh" 0
//     parameters = [ Param.make "angleInRadians" TFloat "" ]
//     returnType = TFloat
//     description = "Returns the hyperbolic sine of `angleInRadians`."
//     fn =
//       (function
//       | _, [ DFloat a ] -> Value(DFloat(System.Math.Sinh a))
//       | _ -> incorrectArgs ())
//     sqlSpec = NotYetImplementedTODO
//     previewable = Pure
//     deprecated = NotDeprecated }
//   { name = fn "Math" "tanh" 0
//     parameters = [ Param.make "angleInRadians" TFloat "" ]
//     returnType = TFloat
//     description = "Returns the hyperbolic tangent of `angleInRadians`."
//     fn =
//       (function
//       | _, [ DFloat a ] -> Value(DFloat(System.Math.Sinh a))
//       | _ -> incorrectArgs ())
//     sqlSpec = NotYetImplementedTODO
//     previewable = Pure
//     deprecated = NotDeprecated } ]
