namespace LiteDB.FSharp

open LiteDB
open System
open System.Collections.Generic


type FSharpBsonMapper() = 
    inherit BsonMapper()
    let entityMappers=Dictionary<Type,EntityMapper>() 
    override self.ToObject(entityType: System.Type, entity: BsonDocument) = Bson.deserializeByType entity entityType 
    override self.ToObject<'t>(entity: BsonDocument) = Bson.deserialize<'t> entity
    override self.ToDocument<'t>(entity: 't) = 
        let withEntityMap (doc:BsonDocument)=
            let t=entityMappers.Item (entity.GetType())
            t.Members
            |>Seq.iter(fun m->
                if not(isNull m.Serialize) then
                    let value=m.Getter.Invoke(entity)
                    doc.RawValue.[m.FieldName]<-m.Serialize.Invoke(value,self)
            )
            doc
        Bson.serialize<'t> entity
        |>withEntityMap 
        
    override self.BuildEntityMapper(entityType)=
        let v=base.BuildEntityMapper(entityType)
        entityMappers.Add(entityType,v)
        v
