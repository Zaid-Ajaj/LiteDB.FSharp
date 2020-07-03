namespace LiteDB.FSharp

open System
open Microsoft.FSharp.Quotations
open Microsoft.FSharp.Quotations.Patterns
open LiteDB
open Microsoft.FSharp.Reflection

// TODO: Does not compile. The query API has apparently changed completely.
module Query =
    let internal mapper = FSharpBsonMapper()

    // We have to create NOT by ourselfes...
    let notExpr (expr: BsonExpression): BsonExpression =
        // Taken from: https://github.com/mbdavid/LiteDB/issues/1659
        sprintf "(%s) = false" expr.Source
        |> BsonExpression.Create

    let rec createQueryFromExpr<'t> (expr: Expr) : BsonExpression =
        match expr with
        | Patterns.PropertyEqual (("Id" | "id" | "ID"), value) when FSharpType.IsUnion (value.GetType()) ->
            Query.EQ("_id", Bson.serializeField value)

        | Patterns.PropertyEqual (("Id" | "id" | "ID"), value) ->
            Query.EQ("_id", Bson.serializeField  value)

        | Patterns.PropertyNotEqual (("Id" | "id" | "ID"), value) ->
            Query.Not("_id", Bson.serializeField value)

        | Patterns.ProperyGreaterThan (("Id" | "id" | "ID"), value) ->
            Query.GT("_id", Bson.serializeField value)

        | Patterns.ProperyGreaterThanOrEqual (("Id" | "id" | "ID"), value) ->
            Query.GTE("_id", Bson.serializeField value)

        | Patterns.PropertyLessThan (("Id" | "id" | "ID"), value) ->
            Query.LT("_id", Bson.serializeField value)

        | Patterns.PropertyLessThanOrEqual (("Id" | "id" | "ID"), value) ->
             Query.LTE("_id", Bson.serializeField value)

        | Patterns.StringContains (propName, value) ->
            Query.Contains(propName, unbox<string> value)

        | Patterns.StringNullOrWhiteSpace propName ->
            sprintf "TRIM(%s) = '' OR %s = null" propName propName
            |> BsonExpression.Create

        | Patterns.StringIsNullOrEmpty propName ->
            sprintf "%s = '' OR %s = null" propName propName
            |> BsonExpression.Create

        | Patterns.PropertyEqual (propName, value) when FSharpType.IsUnion (value.GetType()) ->
            Query.EQ(propName, Bson.serializeField value)

         | Patterns.PropertyEqual (propName, value) when FSharpType.IsRecord (value.GetType()) ->
            Query.EQ(propName, Bson.serializeField value)

        | Patterns.PropertyEqual (propName, value) ->
            Query.EQ(propName, Bson.serializeField(value))

        | Patterns.PropertyNotEqual (propName, value) ->
            Query.Not(propName, Bson.serializeField value)

        | Patterns.LiteralBooleanValue value ->
            sprintf "%b = true" value
            |> BsonExpression.Create

        | Patterns.ProperyGreaterThan (propName, value) ->
            Query.GT(propName, Bson.serializeField value)

        | Patterns.ProperyGreaterThanOrEqual (propName, value) ->
            Query.GTE(propName, Bson.serializeField value)

        | Patterns.PropertyLessThan (propName, value) ->
            Query.LT(propName, Bson.serializeField value)

        | Patterns.PropertyLessThanOrEqual (propName, value) ->
             Query.LTE(propName, Bson.serializeField value)

        | Patterns.BooleanGet (propName) ->
            Query.EQ(propName, Bson.serializeField true)

        | Patterns.And (left, right) ->
            let queryLeft = createQueryFromExpr left
            let queryRight = createQueryFromExpr right
            Query.And(queryLeft, queryRight)

        | Patterns.Or (left, right) ->
            let queryLeft = createQueryFromExpr left
            let queryRight = createQueryFromExpr right
            Query.Or(queryLeft, queryRight)

        | Patterns.NotProperty (innerExpr) ->
            createQueryFromExpr innerExpr
            |> notExpr

        | Lambda (_, expr) -> createQueryFromExpr expr

        | otherwise ->
            let serialziedExpr = sprintf "%A" otherwise
            failwithf "Failed to construct a query from the expression: \n%s\n" serialziedExpr
