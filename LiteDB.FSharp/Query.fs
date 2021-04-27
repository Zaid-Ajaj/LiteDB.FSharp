namespace LiteDB.FSharp

open System
open Microsoft.FSharp.Quotations
open Microsoft.FSharp.Quotations.Patterns
open LiteDB
open Microsoft.FSharp.Reflection
open Cache
open Patterns

module Query =

    let rec createQueryFromExpr<'t> (expr: Expr) : BsonExpression =

        let createBsonValue(value: obj) =
            match getOrAddTypeKind(value.GetType()) with 
            | Kind.Union 
            | Kind.Record 
            | Kind.MapOrDictWithNonStringKey
            | Kind.Tuple
            | Kind.Other -> Bson.serializeField value
            | Kind.Enum ->
                match Type.GetTypeCode(value.GetType().GetEnumUnderlyingType()) with 
                | TypeCode.Byte    ->  BsonValue(value :?> Byte   )
                | TypeCode.Decimal ->  BsonValue(value :?> Decimal)
                | TypeCode.Double  ->  BsonValue(value :?> Double )
                | TypeCode.Single  ->  BsonValue(value :?> Single )
                | TypeCode.Int16   ->  BsonValue(value :?> Int16  )
                | TypeCode.Int32   ->  BsonValue(value :?> Int32  )
                | TypeCode.Int64   ->  BsonValue(value :?> Int64  )
                | TypeCode.UInt16  ->  BsonValue(value :?> UInt16 )
                | TypeCode.UInt64  ->  BsonValue(value :?> UInt64 )
                | TypeCode.UInt32  ->  BsonValue(value :?> UInt32 )
                | TypeCode.SByte   ->  BsonValue(value :?> SByte  )
                | tpCode -> failwithf "tpCode %A is not an enum underlying type" tpCode 
            | _ -> BsonValue(value)

        match expr with
        | Patterns.PropertyEqual (("Id" | "id" | "ID"), value) when FSharpType.IsUnion (value.GetType()) ->
            Query.EQ("_id", Bson.serializeField value)

        | Patterns.PropertyEqual (("Id" | "id" | "ID"), value) ->
            Query.EQ("_id", BsonValue value)

        | Patterns.PropertyNotEqual (("Id" | "id" | "ID"), value) ->
            Query.Not("_id", BsonValue(value))

        | Patterns.ProperyGreaterThan (("Id" | "id" | "ID"), value) ->
            Query.GT("_id", BsonValue(value))

        | Patterns.ProperyGreaterThanOrEqual (("Id" | "id" | "ID"), value) ->
            Query.GTE("_id", BsonValue(value))

        | Patterns.PropertyLessThan (("Id" | "id" | "ID"), value) ->
            Query.LT("_id", BsonValue(value))

        | Patterns.PropertyLessThanOrEqual (("Id" | "id" | "ID"), value) ->
            Query.LTE("_id", BsonValue(value))

        | Patterns.StringContains (propName, value) ->
            Query.Contains(propName, unbox<string> value)
          
        | Patterns.StringNullOrWhiteSpace propName ->
            sprintf "TRIM(%s) = '' OR %s = null" propName propName
            |> BsonExpression.Create


        | Patterns.StringIsNullOrEmpty propName ->
            sprintf "%s = '' OR %s = null" propName propName
            |> BsonExpression.Create

        | Patterns.LiteralBooleanValue value ->
            sprintf "%b = true" value
            |> BsonExpression.Create

        | Patterns.PropertyEqual (propName, value) when (value.GetType().IsEnum) ->
           let bson =
               match Type.GetTypeCode(value.GetType().GetEnumUnderlyingType()) with
               | TypeCode.Byte    ->  BsonValue(value :?> Byte   )
               | TypeCode.Decimal ->  BsonValue(value :?> Decimal)
               | TypeCode.Double  ->  BsonValue(value :?> Double )
               | TypeCode.Single  ->  BsonValue(value :?> Single )
               | TypeCode.Int16   ->  BsonValue(value :?> Int16  )
               | TypeCode.Int32   ->  BsonValue(value :?> Int32  )
               | TypeCode.Int64   ->  BsonValue(value :?> Int64  )
               | TypeCode.UInt16  ->  BsonValue(value :?> UInt16 )
               | TypeCode.UInt64  ->  BsonValue(value :?> UInt64 )
               | TypeCode.UInt32  ->  BsonValue(value :?> UInt32 )
               | TypeCode.SByte   ->  BsonValue(value :?> SByte  )
               | tpCode -> failwithf "tpCode %A is not an enum underlying type" tpCode

           Query.EQ(propName, bson)

        | Patterns.PropertyEqual (propName, value) ->
            Query.EQ(propName, createBsonValue value)

        | Patterns.PropertyNotEqual (propName, value) ->
            Query.Not(propName, createBsonValue value)

        | Patterns.ProperyGreaterThan (propName, value) ->
            Query.GT(propName, createBsonValue value)

        | Patterns.ProperyGreaterThanOrEqual (propName, value) ->
            Query.GTE(propName, createBsonValue value)

        | Patterns.PropertyLessThan (propName, value) ->
            Query.LT(propName, createBsonValue value)

        | Patterns.PropertyLessThanOrEqual (propName, value) ->
             Query.LTE(propName, createBsonValue value)

        | Patterns.BooleanGet (propName) ->
            Query.EQ(propName, BsonValue(true))

        | Patterns.And (left, right) ->
            let queryLeft = createQueryFromExpr left
            let queryRight = createQueryFromExpr right
            Query.And(queryLeft, queryRight)

        | Patterns.Or (left, right) ->
            let queryLeft = createQueryFromExpr left
            let queryRight = createQueryFromExpr right
            Query.Or(queryLeft, queryRight)

        | Patterns.NotProperty (innerExpr) ->
            // We have to create NOT by ourselfes...
            let notExpr (expr: BsonExpression): BsonExpression =
                // Taken from: https://github.com/mbdavid/LiteDB/issues/1659
                sprintf "(%s) = false" expr.Source
                |> BsonExpression.Create

            createQueryFromExpr innerExpr
            |> notExpr
           

        | Lambda (_, expr) -> createQueryFromExpr expr

        | otherwise ->
            let serialziedExpr = sprintf "%A" otherwise
            failwithf "Failed to construct a query from the expression: \n%s\n" serialziedExpr


