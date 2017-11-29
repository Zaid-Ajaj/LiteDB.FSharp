namespace LiteDB.FSharp
open System.Linq.Expressions
open System
open Microsoft.FSharp.Linq.RuntimeHelpers
open Microsoft.FSharp.Quotations

module Linq =
    let convertExpr (expr : Expr<'a -> 'b>) =
      let linq = LeafExpressionConverter.QuotationToExpression expr
      let call = linq :?> MethodCallExpression
      let lambda = call.Arguments.[0] :?> LambdaExpression
      Expression.Lambda<Func<'a, 'b>>(lambda.Body, lambda.Parameters) 