namespace LiteDB.FSharp

open LiteDB
open System
open System.Collections.Generic


type FSharpBsonMapper() = 
    inherit BsonMapper()
    let entityMappers = Dictionary<Type,EntityMapper>() 
    override self.ToObject(entityType: System.Type, entity: BsonDocument) = Bson.deserializeByType entity entityType 
    override self.ToObject<'t>(entity: BsonDocument) = Bson.deserialize<'t> entity
    override self.ToDocument<'t>(entity: 't) = 
        //Add DBRef Feature :set field value with $ref  
        let withEntityMap (doc:BsonDocument)=
            let mapper = entityMappers.Item (entity.GetType())
            for memberMapper in mapper.Members do
                if not (isNull memberMapper.Serialize) then  
                    let value = memberMapper.Getter.Invoke(entity)
                    let serialized = memberMapper.Serialize.Invoke(value, self)
                    doc.RawValue.[memberMapper.FieldName] <- serialized
            doc
        Bson.serialize<'t> entity
        |> withEntityMap 
        
    override self.BuildEntityMapper(entityType)=
        let mapper = base.BuildEntityMapper(entityType)
        entityMappers.Add(entityType, mapper)
        mapper
