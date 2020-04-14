namespace LiteDB.FSharp

open LiteDB
open System
open System.Collections.Generic
open System.Linq.Expressions
open Newtonsoft.Json
open LiteDB

type FSharpBsonMapper() = 
    inherit BsonMapper()
    let entityMappers = Dictionary<Type,EntityMapper>() 
    member this.DbRef<'T1,'T2> (exp: Expression<Func<'T1,'T2>>) =
        this.Entity<'T1>().DbRef(exp) |> ignore
    
    static member RegisterInheritedConverterType<'T1,'T2>() =
        let t1 = typeof<'T1>
        let t2 = typeof<'T2>
        Cache.inheritedConverterTypes.AddOrUpdate(
            t1.FullName,
            HashSet [t2],
            ( fun _ types -> types.Add(t2) |> ignore; types )
        ) |> ignore

    static member UseCustomJsonConverters(converters: JsonConverter[]) = 
        Bson.converters <- converters   

    override self.ToObject(entityType: System.Type, entity: BsonDocument) = Bson.deserializeByType entity entityType 
    override self.ToObject<'t>(entity: BsonDocument) = Bson.deserialize<'t> entity
    override self.ToDocument<'t>(entity: 't) = 
        //Add DBRef Feature :set field value with $ref  
        if typeof<'t>.FullName = typeof<BsonDocument>.FullName 
        then entity |> unbox<BsonDocument>
        else
        let withEntityMap (doc:BsonDocument)=
            let mapper = entityMappers.Item (entity.GetType())
            for memberMapper in mapper.Members do
                if not (isNull memberMapper.Serialize) then  
                    let value = memberMapper.Getter.Invoke(entity)
                    let serialized = memberMapper.Serialize.Invoke(value, self)
                    doc.[memberMapper.FieldName] <- serialized
            doc
        Bson.serialize<'t> entity
        |> withEntityMap 
        
    override self.BuildEntityMapper(entityType)=
        let mapper = base.BuildEntityMapper(entityType)
        entityMappers.Add(entityType, mapper)
        mapper
