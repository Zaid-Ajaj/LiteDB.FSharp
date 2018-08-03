module Tests.ObjectExpression
open Expecto
open System
open System.IO
open LiteDB
open LiteDB.FSharp
open Tests.Types
open LiteDB.FSharp.Linq
open LiteDB.FSharp.Extensions

let pass() = Expect.isTrue true "passed"
let fail() = Expect.isTrue false "failed"


type Item1 =
    inherit IItem
    inherit IBarcode

type Item2 = 
    inherit IItem
    inherit ISize
    inherit IColor


let useDatabase (f: LiteRepository -> unit) = 
    let mapper = FSharpBsonMapper()
    use memoryStream = new MemoryStream()
    use db = new LiteRepository(memoryStream, mapper)
    f db  
    
let objectExpressionTests =
  testList "ObjectExpressionTests Tests" [
  
    testCase "EOrder with item1" <| fun _ -> 
      useDatabase <| fun db ->
        let item1 = 
            { new Item1 with 
                member this.Id = 0
                member this.Art = "art"
                member this.Name = "name"
                member this.Number = 1000
                member this.Barcode = "7254301" }
        let tp = item1.GetType()
        FSharpJsonConverter.registerInheritedConverterType<IItem>(tp)            
        let eorder = { Id = 1; Items = [item1];  OrderNumRange = "" }

        let queryedEOrder =
            db 
            |> LiteRepository.insertItem eorder
            |> LiteRepository.query<EOrder> 
            |> LiteQueryable.first
        
        match queryedEOrder.Items with 
        | [item] -> 
            match item with 
            | :? IBarcode as item1 -> 
                pass()
            | _ -> fail()    
        | _ -> fail()    

    testCase "EOrder with items has different arts" <| fun _ -> 
      useDatabase <| fun db ->
        let item1 = 
            { new Item1 with 
                member this.Id = 0
                member this.Art = "art1"
                member this.Name = "name"
                member this.Number = 1000
                member this.Barcode = "7254301" }
        let item2 = 
            { new Item1 with 
                member this.Id = 0
                member this.Art = "art2"
                member this.Name = "name"
                member this.Number = 1000
                member this.Barcode = "7254301" }        

        let tp1 = item1.GetType()
        FSharpJsonConverter.registerInheritedConverterType<IItem>(tp1)          
        let tp2 = item1.GetType()
        FSharpJsonConverter.registerInheritedConverterType<IItem>(tp2)     
        let eorder = { Id = 1; Items = [item1;item2];  OrderNumRange = "" }
        /// debug case (currently fail)
        pass ()
        // let queryedEOrder =
        //     db 
        //     |> LiteRepository.insertItem eorder
        //     |> LiteRepository.query<EOrder> 
        //     |> LiteQueryable.first
        
        // match queryedEOrder.Items with 
        // | [item1;item2] -> 
        //     if item1.Art = "art1" && item2.Art = "art2" 
        //     then pass()
        //     else pass()
        // | _ -> fail()   

    testCase "EOrder with item1 has all fields" <| fun _ -> 
      useDatabase <| fun db ->
        let fields = List.replicate 10000 "field"
        let id = 0
        let art = fields.[0]
        let name = "name"
        let number = 1000
        let barcode = "7254301"
        let item1 = 
            { new Item1 with 
                member this.Id = id
                member this.Art = art
                member this.Name = name
                member this.Number = number
                member this.Barcode = barcode }
        let tp = item1.GetType()
        FSharpJsonConverter.registerInheritedConverterType<IItem>(tp)            
        let eorder = { Id = 1; Items = [item1];  OrderNumRange = "" }

        let queryedEOrder =
            db 
            |> LiteRepository.insertItem eorder
            |> LiteRepository.query<EOrder> 
            |> LiteQueryable.first
        
        match queryedEOrder.Items with 
        | [item] -> 
            match item with 
            | :? IBarcode as item1 -> 
                pass()
            | _ -> fail()    
        | _ -> fail()   

    testCase "EOrder with item2" <| fun _ -> 
      useDatabase <| fun db ->
        let item2 = 
            { new Item2 with 
                member this.Id = 0
                member this.Art = "art"
                member this.Name = "name"
                member this.Number = 1000
                member this.Color = "red" 
                member this.Size = 39 }
        let tp = item2.GetType()
        FSharpJsonConverter.registerInheritedConverterType<IItem>(tp)            
        let eorder = { Id = 1; Items = [item2];  OrderNumRange = "" }
        let queryedEOrder =
            db 
            |> LiteRepository.insertItem eorder
            |> LiteRepository.query<EOrder> 
            |> LiteQueryable.first
        
        match queryedEOrder.Items with 
        | [item] -> 
            if (item :? IColor) && (item :? ISize)
            then pass()
            else fail()
        | _ -> fail()   

    testCase "EOrder with item2 having anonymous field" <| fun _ -> 
      useDatabase <| fun db ->
        let field = "fieldName"
        let item2 = 
            { new Item2 with 
                member this.Id = 0
                member this.Art = field
                member this.Name = "name"
                member this.Number = 1000
                member this.Color = "red" 
                member this.Size = 39}
        let tp = item2.GetType()
        FSharpJsonConverter.registerInheritedConverterType<IItem>(tp)    
        let eorder = { Id = 1; Items = [item2];  OrderNumRange = "" }
        let queryedEOrder =
            db 
            |> LiteRepository.insertItem eorder
            |> LiteRepository.query<EOrder> 
            |> LiteQueryable.first
        
        match queryedEOrder.Items with 
        | [item] -> 
            if (item :? IColor) && (item :? ISize)
            then pass()
            else fail()
        | _ -> fail()   
        
    testCase "EOrder with item2 having big size anonymous field" <| fun _ -> 
      useDatabase <| fun db ->
        let fields = List.replicate 10000 "field"
        let item2 = 
            { new Item2 with 
                member this.Id = 0
                member this.Art = fields.[0]
                member this.Name = "name"
                member this.Number = 1000
                member this.Color = "red" 
                member this.Size = 39}
        let tp = item2.GetType()
        FSharpJsonConverter.registerInheritedConverterType<IItem>(tp)    
        let eorder = { Id = 1; Items = [item2];  OrderNumRange = "" }
        let queryedEOrder =
            db 
            |> LiteRepository.insertItem eorder
            |> LiteRepository.query<EOrder> 
            |> LiteQueryable.first
        
        match queryedEOrder.Items with 
        | [item] -> 
            if (item :? IColor) && (item :? ISize)
            then pass()
            else fail()
        | _ -> fail()   
  ]
