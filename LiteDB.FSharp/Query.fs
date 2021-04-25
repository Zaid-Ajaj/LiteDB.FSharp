namespace LiteDB.FSharp

open System
open Microsoft.FSharp.Quotations
open Microsoft.FSharp.Quotations.Patterns
open LiteDB
open Microsoft.FSharp.Reflection
open Cache

module Query =
    let internal mapper = FSharpBsonMapper()
    let rec createQueryFromExpr<'t> (expr: Expr) : Query =
        match expr with
        | Patterns.PropertyEqual (("Id" | "id" | "ID"), value) when FSharpType.IsUnion (value.GetType()) ->
            Query.EQ("_id", Bson.serializeField value)

        | Patterns.PropertyEqual (("Id" | "id" | "ID"), value) ->
            Query.EQ("_id", BsonValue value)

        | Patterns.PropertyNotEqual (("Id" | "id" | "ID"), value) ->
            Query.Not(Query.EQ("_id", BsonValue(value)))

        | Patterns.ProperyGreaterThan (("Id" | "id" | "ID"), value) ->
            Query.GT("_id", BsonValue(value))

        | Patterns.ProperyGreaterThanOrEqual (("Id" | "id" | "ID"), value) ->
            Query.GTE("_id", BsonValue(value))

        | Patterns.PropertyLessThan (("Id" | "id" | "ID"), value) ->
            Query.LT("_id", BsonValue(value))

        | Patterns.PropertyLessThanOrEqual (("Id" | "id" | "ID"), value) ->
             Query.LTE("_id", BsonValue(value))

        | Patterns.StringContains (propName, value) ->
            Query.Where(propName, fun bsonValue ->
                bsonValue
                |> Bson.deserializeField<string>
                |> fun strValue -> strValue.Contains(unbox<string> value))

        | Patterns.StringNullOrWhiteSpace propName ->
            Query.Where(propName, fun bsonValue ->
                bsonValue
                |> Bson.deserializeField<string>
                |> String.IsNullOrWhiteSpace)

        | Patterns.StringIsNullOrEmpty propName ->
            Query.Where(propName, fun bsonValue ->
                bsonValue
                |> Bson.deserializeField<string>
                |> String.IsNullOrEmpty)

        | Patterns.PropertyEqual (propName, value) when isConvertableUnionType (value.GetType()) ->
            Query.EQ(propName, Bson.serializeField value)

         | Patterns.PropertyEqual (propName, value) when FSharpType.IsRecord (value.GetType()) ->
            Query.EQ(propName, Bson.serializeField value)

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
            Query.EQ(propName, BsonValue(value))

        | Patterns.PropertyNotEqual (propName, value) ->
            Query.Not(Query.EQ(propName, BsonValue(value)))

        | Patterns.LiteralBooleanValue value ->
            Query.Where("_id", fun id -> value)

        | Patterns.ProperyGreaterThan (propName, value) ->
            Query.GT(propName, BsonValue(value))

        | Patterns.ProperyGreaterThanOrEqual (propName, value) ->
            Query.GTE(propName, BsonValue(value))

        | Patterns.PropertyLessThan (propName, value) ->
            Query.LT(propName, BsonValue(value))

        | Patterns.PropertyLessThanOrEqual (propName, value) ->
             Query.LTE(propName, BsonValue(value))

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
            let innerQuery = createQueryFromExpr innerExpr
            Query.Not(innerQuery)

        | Lambda (_, expr) -> createQueryFromExpr expr

        | otherwise ->
            let serialziedExpr = sprintf "%A" otherwise
            failwithf "Failed to construct a query from the expression: \n%s\n" serialziedExpr
