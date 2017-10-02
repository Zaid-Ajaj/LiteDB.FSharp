namespace LiteDB.FSharp

open LiteDB
open System.Globalization
open Newtonsoft.Json
open Newtonsoft.Json.Linq

[<AutoOpen>]
module ReflectionAdapters =
    open System.Reflection

    type System.Type with
        member this.IsValueType = this.GetTypeInfo().IsValueType
        member this.IsGenericType = this.GetTypeInfo().IsGenericType
        member this.GetMethod(name) = this.GetTypeInfo().GetMethod(name)
        member this.GetGenericArguments() = this.GetTypeInfo().GetGenericArguments()
        member this.MakeGenericType(args) = this.GetTypeInfo().MakeGenericType(args)
        member this.GetCustomAttributes(inherits : bool) : obj[] =
            downcast box(CustomAttributeExtensions.GetCustomAttributes(this.GetTypeInfo(), inherits) |> Seq.toArray)

open System
open FSharp.Reflection
open Newtonsoft.Json
open Newtonsoft.Json.Converters
open System.Reflection
open System.Collections.Generic
open System.Collections.Concurrent
open System.Text.RegularExpressions

type Kind =
    | Other = 0
    | Option = 1
    | Tuple = 2
    | Union = 3
    | DateTime = 6
    | MapOrDictWithNonStringKey = 7
    | Long = 8
    | BigInt = 9
    | Guid = 10
    | Decimal = 11
    | Binary = 12

/// Helper for serializing map/dict with non-primitive, non-string keys such as unions and records.
/// Performs additional serialization/deserialization of the key object and uses the resulting JSON
/// representation of the key object as the string key in the serialized map/dict.
type MapSerializer<'k,'v when 'k : comparison>() =
    static member Deserialize(t:Type, reader:JsonReader, serializer:JsonSerializer) =
        let dictionary =
            serializer.Deserialize<Dictionary<string,'v>>(reader)
                |> Seq.fold (fun (dict:Dictionary<'k,'v>) kvp ->
                    use tempReader = new System.IO.StringReader(kvp.Key)
                    let key = serializer.Deserialize(tempReader, typeof<'k>) :?> 'k
                    dict.Add(key, kvp.Value)
                    dict
                    ) (Dictionary<'k,'v>())
        if t.IsGenericType && t.GetGenericTypeDefinition() = typedefof<Map<_,_>>
        then dictionary |> Seq.map (|KeyValue|) |> Map.ofSeq :> obj
        elif t.IsGenericType && t.GetGenericTypeDefinition() = typedefof<Dictionary<_,_>>
        then dictionary :> obj
        else failwith "MapSerializer input type wasn't a Map or a Dictionary"
    static member Serialize(value: obj, writer:JsonWriter, serializer:JsonSerializer) =
        let kvpSeq =
            match value with
            | :? Map<'k,'v> as mapObj -> mapObj |> Map.toSeq
            | :? Dictionary<'k,'v> as dictObj -> dictObj |> Seq.map (|KeyValue|)
            | _ -> failwith "MapSerializer input value wasn't a Map or a Dictionary"
        writer.WriteStartObject()
        use tempWriter = new System.IO.StringWriter()
        kvpSeq
            |> Seq.iter (fun (k,v) ->
                let key =
                    tempWriter.GetStringBuilder().Clear() |> ignore
                    serializer.Serialize(tempWriter, k)
                    tempWriter.ToString()
                writer.WritePropertyName(key)
                serializer.Serialize(writer, v) )
        writer.WriteEndObject()

module private Cache =
    let jsonConverterTypes = ConcurrentDictionary<Type,Kind>()
    let serializationBinderTypes = ConcurrentDictionary<string,Type>()

open Cache

/// Converts F# options, tuples and unions to a format understandable
/// A derivative of Fable's JsonConverter. Code adapted from Lev Gorodinski's original.
/// See https://goo.gl/F6YiQk
type FSharpJsonConverter() =
    inherit Newtonsoft.Json.JsonConverter()
    let advance(reader: JsonReader) =
        reader.Read() |> ignore

    let readElements(reader: JsonReader, itemTypes: Type[], serializer: JsonSerializer) =
        let rec read index acc =
            match reader.TokenType with
            | JsonToken.EndArray -> acc
            | _ ->
                let value = serializer.Deserialize(reader, itemTypes.[index])
                advance reader
                read (index + 1) (acc @ [value])
        advance reader
        read 0 List.empty

    let getUci t name =
        FSharpType.GetUnionCases(t)
        |> Array.find (fun uci -> uci.Name = name)

    override x.CanConvert(t) =
        let kind =
            jsonConverterTypes.GetOrAdd(t, fun t ->
                if t.FullName = "System.DateTime"
                then Kind.DateTime
                elif t.FullName = "System.Guid"
                then Kind.Guid
                elif t.Name = "FSharpOption`1"
                then Kind.Option
                elif t.FullName = "System.Int64"
                then Kind.Long
                elif t.FullName = "System.Numerics.BigInteger"
                then Kind.BigInt
                elif t = typeof<byte[]> 
                then Kind.Binary
                elif FSharpType.IsTuple t
                then Kind.Tuple
                elif (FSharpType.IsUnion t && t.Name <> "FSharpList`1")
                then Kind.Union
                elif t.IsGenericType
                    && (t.GetGenericTypeDefinition() = typedefof<Map<_,_>> || t.GetGenericTypeDefinition() = typedefof<Dictionary<_,_>>)
                    && t.GetGenericArguments().[0] <> typeof<string>
                then
                    Kind.MapOrDictWithNonStringKey
                else Kind.Other)
        kind <> Kind.Other

    override x.WriteJson(writer, value, serializer) =
        if isNull value
        then serializer.Serialize(writer, value)
        else
            let t = value.GetType()
            match jsonConverterTypes.TryGetValue(t) with
            | false, _ ->
                serializer.Serialize(writer, value)
            | true, Kind.Long ->
                let numberLong = JObject()
                let value = sprintf "%+i" (value :?> int64)
                numberLong.Add(JProperty("$numberLong", value))
                numberLong.WriteTo(writer)
            | true, Kind.Guid ->
                let guidObject = JObject()
                let guidValue = (value :?> Guid).ToString()
                guidObject.Add(JProperty("$guid", guidValue))
                guidObject.WriteTo(writer)
            | true, Kind.DateTime ->
                let dt = value :?> DateTime
                let universalTime = if dt.Kind = DateTimeKind.Local then dt.ToUniversalTime() else dt
                let dateTime = JObject()
                dateTime.Add(JProperty("$date", universalTime.ToString("O", CultureInfo.InvariantCulture)))
                dateTime.WriteTo(writer)
            | true, Kind.Decimal ->
                let value = (value :?> decimal).ToString()
                let numberDecimal = JObject()
                numberDecimal.Add(JProperty("$numberDecimal", value))
                numberDecimal.WriteTo(writer)
            | true, Kind.Option ->
                let _,fields = FSharpValue.GetUnionFields(value, t)
                serializer.Serialize(writer, fields.[0])
            | true, Kind.Tuple ->
                let values = FSharpValue.GetTupleFields(value)
                serializer.Serialize(writer, values)
            | true, Kind.Union ->
                let uci, fields = FSharpValue.GetUnionFields(value, t)
                if fields.Length = 0
                then serializer.Serialize(writer, uci.Name)
                else
                    writer.WriteStartObject()
                    writer.WritePropertyName(uci.Name)
                    if fields.Length = 1
                    then serializer.Serialize(writer, fields.[0])
                    else serializer.Serialize(writer, fields)
                    writer.WriteEndObject()
            | true, Kind.MapOrDictWithNonStringKey ->
                let mapTypes = t.GetGenericArguments()
                let mapSerializer = typedefof<MapSerializer<_,_>>.MakeGenericType mapTypes
                let mapSerializeMethod = mapSerializer.GetMethod("Serialize")
                mapSerializeMethod.Invoke(null, [| value; writer; serializer |]) |> ignore
            | true, _ ->
                serializer.Serialize(writer, value)

    override x.ReadJson(reader, t, existingValue, serializer) =
        match jsonConverterTypes.TryGetValue(t) with
        | false, _ ->
            serializer.Deserialize(reader, t)
        | true, Kind.Guid ->
            let jsonObject = JObject.Load(reader)
            let value = jsonObject.["$guid"].Value<string>()
            upcast Guid.Parse(value)
        | true, Kind.Decimal ->
            let jsonObject = JObject.Load(reader)
            let value = jsonObject.["$numberDecimal"].Value<string>()
            upcast Decimal.Parse(value)
        | true, Kind.Long ->
            let jsonObject = JObject.Load(reader)
            let value = jsonObject.["$numberLong"].Value<string>()
            upcast Int64.Parse(value)
        | true, Kind.DateTime ->
            let jsonObject = JObject.Load(reader)
            let dateValue = jsonObject.["$date"].Value<string>()
            upcast DateTime.Parse(dateValue, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind)
        | true, Kind.Option ->
            let innerType = t.GetGenericArguments().[0]
            let innerType =
                if innerType.IsValueType
                then (typedefof<Nullable<_>>).MakeGenericType([|innerType|])
                else innerType
            let value = serializer.Deserialize(reader, innerType)
            let cases = FSharpType.GetUnionCases(t)
            if isNull value
            then FSharpValue.MakeUnion(cases.[0], [||])
            else FSharpValue.MakeUnion(cases.[1], [|value|])
        | true, Kind.Tuple ->
            match reader.TokenType with
            | JsonToken.StartArray ->
                let values = readElements(reader, FSharpType.GetTupleElements(t), serializer)
                FSharpValue.MakeTuple(values |> List.toArray, t)
            | _ -> failwith "invalid token"
        | true, Kind.Union ->
            match reader.TokenType with
            | JsonToken.String ->
                let name = serializer.Deserialize(reader, typeof<string>) :?> string
                FSharpValue.MakeUnion(getUci t name, [||])
            | JsonToken.StartObject ->
                advance reader
                let name = reader.Value :?> string
                let uci = getUci t name
                advance reader
                let itemTypes = uci.GetFields() |> Array.map (fun pi -> pi.PropertyType)
                if itemTypes.Length > 1
                then
                    let values = readElements(reader, itemTypes, serializer)
                    advance reader
                    FSharpValue.MakeUnion(uci, List.toArray values)
                else
                    let value = serializer.Deserialize(reader, itemTypes.[0])
                    advance reader
                    FSharpValue.MakeUnion(uci, [|value|])
            | JsonToken.Null -> null // for { "union": null }
            | _ -> failwith "invalid token"
        | true, Kind.MapOrDictWithNonStringKey ->
            let mapTypes = t.GetGenericArguments()
            let mapSerializer = typedefof<MapSerializer<_,_>>.MakeGenericType mapTypes
            let mapDeserializeMethod = mapSerializer.GetMethod("Deserialize")
            mapDeserializeMethod.Invoke(null, [| t; reader; serializer |])
        | true, _ ->
            serializer.Deserialize(reader, t)