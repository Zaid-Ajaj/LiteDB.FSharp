namespace LiteDB.FSharp
 module Experimental=
   open LiteDB
   open System
   open TypeShape.Core
   open TypeShape.Core.Utils
   open LiteDB.FSharp
   type Convert<'t> = { To : 't -> BsonValue; From : BsonValue -> 't }

   [<AutoOpen>]
   module Impl =
     let inline delay (f : unit -> 'T) : BsonValue -> 'T = fun _ -> f()

     let toKey (x : string) =
       if (x.ToLower() = "id") then "_id"
       else x.Trim('@')
   let private locker = new obj()
   let private ctx = new TypeGenerationContext()
   let rec private  genPickler<'T> : unit -> Convert<'T> =

     fun () ->
     lock locker (fun () -> genPicklerCached<'T> ctx)

   and private genPicklerCached<'T> (ctx : TypeGenerationContext) : Convert<'T> =
    let delay (c : Cell<Convert<'T>>) : Convert<'T> =
      { To = fun sb -> c.Value.To sb
        From = fun x -> c.Value.From x }

    match ctx.InitOrGetCachedValue<Convert<'T>> delay with
    | Cached(value = f) -> f
    | NotCached t ->
      let p = genPicklerAux<'T> ctx
      ctx.Commit t p

   and private genPicklerAux<'T> (ctx : TypeGenerationContext) : Convert<'T> =

    let mkParser (parser : 't -> BsonValue) (writer : BsonValue -> 't) : Convert<'T> =
      {
      To = fun x -> (unbox parser) x
      From = fun x -> (unbox writer) x
      }

    let  mkMemberPickler (shape : IShapeMember<'Class>) =
      shape.Accept { new IMemberVisitor<'Class, ('Class -> BsonValue) * (BsonValue -> 'Class -> 'Class)> with
        member __.Visit(shape : ShapeMember<'Class, 'Field>) =
          let fP = genPicklerCached<'Field> ctx

          let printer = fun x ->
            shape.Get x |> fP.To

          let parser =
            fun (bson : BsonValue) ->
              if (bson.IsDocument) then
                let doc = bson.AsDocument
                fun x ->
                 let res = shape.Set x (fP.From doc.[toKey shape.Label])
                 res
              else
                fun x -> x

          printer, parser
      }

    let combineMemberPicklers (v : BsonValue -> 'Class) (members : IShapeMember<'Class> []) =
      let (printers, parsers) = members |> Array.map mkMemberPickler |> Array.unzip
      let names = members |> Array.map (fun x -> x.Label) |> Array.map toKey
      let printer =
         fun x ->
        let doc = new BsonDocument()
        let arr = printers |> Array.zip names
        for i in 0..printers.Length - 1 do
          doc.[names.[i]] <- printers.[i] x
        arr |> Array.iter (fun (name, printer) -> doc.[name] <- printer x)
        doc :> BsonValue

      let parser =
         fun bson ->
           let mutable res = v bson
           for p in parsers do
            res <- p bson res
           res

      mkParser printer parser
    if (typeof<'T>.Name = typeof<BsonDocument>.Name)
      then mkParser (fun x -> x :> BsonValue) (fun x -> x.AsDocument)
    else
      match shapeof<'T> with
      | Shape.Unit -> mkParser (fun _ -> BsonValue.Null) (fun _ -> ())
      | Shape.Bool -> mkParser (fun x -> unbox<bool> x |> BsonValue) (fun v ->
                                       if (v.IsNull) then false
                                       else v.AsBoolean)
      // TODO: Does not compile. The BsonValue constructor taking an obj is gone, and there is none
      // for byte. Also BsonValue.RawValue is gone, and there is no .AsByte property.
      | Shape.Byte -> mkParser (fun (x : byte) -> x |> BsonValue) (fun v -> unbox<byte> v.RawValue)
      | Shape.Int32 -> mkParser (fun (x : int) -> x |> BsonValue) (fun v -> v.AsInt32)
      | Shape.Int64 -> mkParser (fun x -> unbox<int64> x |> BsonValue) (fun v -> v.AsInt64)
      | Shape.String -> mkParser (fun x -> unbox<string> x |> BsonValue) (fun v -> v.AsString)
      | Shape.Guid -> mkParser (fun x -> unbox<Guid> x |> BsonValue) (fun v -> v.AsGuid)
      | Shape.Decimal -> mkParser (fun x -> unbox<Decimal> x |> BsonValue) (fun v -> v.AsDecimal)
      | Shape.Double -> mkParser (fun x -> unbox<Double> x |> BsonValue) (fun v -> v.AsDouble)
      | Shape.DateTime -> mkParser (fun x -> unbox<DateTime> x |> BsonValue) (fun v -> v.AsDateTime)
      | Shape.FSharpOption s ->
             s.Element.Accept {
               new ITypeVisitor<Convert<'T>>
                 with
                  member __.Visit<'t>() =
                   let tP = genPicklerCached<'t> ctx
                   let printer = function
                     | None -> BsonValue.Null
                     | Some t -> tP.To t

                   let parser =
                     fun (v : BsonValue) ->
                       let vv =
                          if (not v.IsNull) then tP.From v |> Some
                          else None
                       vv
                   mkParser printer parser
             }

      | Shape.FSharpList s ->
        s.Element.Accept {
          new ITypeVisitor<Convert<'T>> with
            member __.Visit<'t>() =
              let eP = genPicklerCached<'t> ctx
              let printer (x : 't list) =
                let ts = x
                let res = ResizeArray<BsonValue>(ts.Length)
                for t in ts do
                  res.Add(eP.To t)
                res |> BsonArray :> BsonValue

              let parser = fun (v : BsonValue) ->
                if (v.IsArray) then v.AsArray |> Seq.map eP.From |> List.ofSeq
                else []

              mkParser printer parser
        }

      | Shape.Enum s ->
         s.Accept {
            new IEnumVisitor<Convert<'T>> with
            member __.Visit<'t, 'u when 't : enum<'u>
                        and 't : struct
                        and 't :> ValueType
                        and 't : (new : unit -> 't)>() =
              let eP = genPicklerCached<'t> ctx
              let printer =
                fun x ->
                let ts = unbox<'t> x |> LanguagePrimitives.EnumToValue
                // TODO: Does not compile. The BsonValue constructor taking an obj is gone.
                // Potentially replaceable with BsonDocument?
                ts |> BsonValue

              let parser = fun (v : BsonValue) ->
                 // TODO: Does not compile. BsonValue.RawValue is gone. Potentially replaceable with
                 // BsonDocument via .AsDocument?
                 let res : 't = LanguagePrimitives.EnumOfValue(unbox<'u> v.RawValue)
                 res

              mkParser printer parser
              }

      | Shape.ByteArray as s ->
        s.Accept {
             new ITypeVisitor<Convert<'T>> with
            member __.Visit<'t>() =
              let eP = genPicklerCached<'t> ctx
              let printer =
                fun x ->
                let ts = unbox<byte array> x
                ts |> BsonValue

              let parser = fun (v : BsonValue) ->
                if (v.IsBinary) then v.AsBinary
                else [||]

              mkParser printer parser
        }

      | Shape.Array s when s.Rank = 1 ->
        s.Element.Accept {
             new ITypeVisitor<Convert<'T>> with
            member __.Visit<'t>() =
              let eP = genPicklerCached<'t> ctx
              let printer =
                fun x ->
                let ts = unbox<'t array> x
                ts |> Array.map eP.To |> BsonArray :> BsonValue

              let parser = fun (v : BsonValue) ->
                if (v.IsArray) then v.AsArray |> Seq.map eP.From |> Array.ofSeq
                else [||]

              mkParser printer parser
        }

      | Shape.FSharpMap s ->
        s.Accept {
             new IFSharpMapVisitor<Convert<'T>> with
                 member __.Visit<'k, 'v when 'k : comparison>() =
                   let kp = genPicklerCached<'k> ctx
                   let vp = genPicklerCached<'v> ctx
                   let printer =
                    fun x ->
                     let m = unbox<Map<'k, 'v>> x
                     let mutable doc = new BsonDocument()
                     let res = ResizeArray<BsonValue>(m.Count)
                     for (kv) in m do
                       let doc = new BsonDocument()
                       doc.["key"] <- kp.To kv.Key
                       doc.["value"] <- vp.To kv.Value
                       res.Add doc
                       doc.["values"] <- BsonArray res
                     doc :> BsonValue

                   let parser =
                     fun (v : BsonValue) ->
                      if (v.IsDocument) then
                        let d = v.AsDocument
                        if (d.ContainsKey "values") then
                          let arr = v.AsDocument.["values"].AsArray
                          let mutable map = Map.empty
                          for v in arr do
                            let d = v.AsDocument
                            map <- map |> Map.add (kp.From d.["key"]) (vp.From d.["value"])
                          map
                        else Map.empty
                      else Map.empty
                   mkParser printer parser
               }

      | Shape.Tuple(:? (ShapeTuple<'T>) as shape) ->
         combineMemberPicklers (delay shape.CreateUninitialized) shape.Elements

      | Shape.FSharpRecord(:? (ShapeFSharpRecord<'T>) as shape) ->
         combineMemberPicklers (delay shape.CreateUninitialized) shape.Fields

      | Shape.FSharpUnion(:? (ShapeFSharpUnion<'T>) as shape) ->

        let mkUnionCaseInfo (case : ShapeFSharpUnionCase<'T>) =
          let hasFields = case.Fields.Length > 0
          let init = delay case.CreateUninitialized
          let pickler = combineMemberPicklers (init) case.Fields
          let printer =
             fun x ->
              if (hasFields) then
                let doc = new BsonDocument()
                doc.["__case"] <- case.CaseInfo.Name |> BsonValue
                doc.["Items"] <- pickler.To x
                // TODO: Does not compile. The BsonValue constructor taking a BsonValue is gone.
                // However, BsonDocument implements BsonValue directly. Unclear if that is an option.
                doc |> BsonValue
              else (case.CaseInfo.Name |> BsonValue)
          let parser =
             fun v ->
              if (hasFields) then
                 pickler.From v
              else
               init v

          mkParser printer parser

        let caseInfo = shape.UnionCases |> Array.map mkUnionCaseInfo

        {
            To =
              fun x ->
                let tag = shape.GetTag x
                let printer = caseInfo.[tag]
                printer.To x

            From =
              fun v ->
                 if (v.IsDocument) then
                   let doc = v.AsDocument
                   let case = doc.["__case"].AsString
                   let index = shape.UnionCases |> Array.findIndex (fun x -> x.CaseInfo.Name = case)
                   let v = doc.[case]
                   let printer = caseInfo.[index]
                   printer.From doc.["Items"]

                 else if (v.IsString) then
                  let str = v.AsString
                  let index = shape.UnionCases |> Array.findIndex (fun x -> x.CaseInfo.Name = str)
                  let printer = caseInfo.[index]
                  printer.From v

                 else raise (ArgumentException("Invalid type!!!"))
        }

      | Shape.Poco((:? (ShapePoco<'T>) as shape)) ->
        combineMemberPicklers (delay shape.CreateUninitialized) (shape.Fields |> Array.filter (fun s -> s.IsPublic))
      | _ -> failwithf "unsupported type '%O'" typeof<'T>


   type TypeShapeMapper() =
    inherit FSharpBsonMapper()

    override self.ToObject(entityType : System.Type, entity : BsonDocument) = Bson.deserializeByType entity entityType
    override self.ToObject<'t>(entity : BsonDocument) =
      try
        let pickler = genPickler<'t>()
        let res = pickler.From(entity :> BsonValue)
        res
      with exn ->
       Bson.deserialize<'t> entity
    override self.ToDocument<'t>(entity : 't) =
      try
        let pickler = genPickler<'t>()
        let res = (pickler.To entity) :?> BsonDocument
        res
      with exn ->

        if typeof<'t>.FullName = typeof<BsonDocument>.FullName
        then entity |> unbox<BsonDocument>
        else
        base.ToDocument entity


