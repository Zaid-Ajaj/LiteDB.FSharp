module Tests.InheritedType
open Expecto
open System
open System.IO
open LiteDB
open LiteDB.FSharp
open Tests.Types
open LiteDB.FSharp.Extensions
open LiteDB.FSharp.Experimental

let pass() = Expect.isTrue true "passed"
let fail() = Expect.isTrue false "failed"


type Item1 =
    val mutable Id : int
    val mutable Art : string
    val mutable Name : string
    val mutable Number : int

    
    interface IItem with 
        member this.Art = this.Art
        member this.Id = this.Id
        member this.Name = this.Name
        member this.Number = this.Number
    val mutable Barcode : string

    interface IBarcode with 
        member this.Barcode = this.Barcode    

    new (id, art, name, number, barcode) =
        { Id = id; Art = art; Name = name; Number = number; Barcode = barcode }

type Item2 = 
    val mutable Id : int
    val mutable Art : string
    val mutable Name : string
    val mutable Number : int

    interface IItem with 
        member this.Art = this.Art
        member this.Id = this.Id
        member this.Name = this.Name
        member this.Number = this.Number

    val mutable Size : int
    interface ISize with 
        member this.Size = this.Size 
    val mutable Color : string

    interface IColor with 
        member this.Color = this.Color 

    new (id, art, name, number, size, color) =
        { Id = id; Art = art; Name = name; Number = number; Size = size; Color = color }

[<CLIMutable>]
type Item1OfRecord =
    {
        Id : int
        Art : string
        Name : string
        Number : int
        Barcode: string
    }

    
    interface IItem with 
        member this.Art = this.Art
        member this.Id = this.Id
        member this.Name = this.Name
        member this.Number = this.Number

    interface IBarcode with 
        member this.Barcode = this.Barcode    

[<CLIMutable>]
type Item2OfRecord = 
    {
        Id : int
        Art : string
        Name : string
        Number : int
        Size : int
        Color : string
    }

    interface IItem with 
        member this.Art = this.Art
        member this.Id = this.Id
        member this.Name = this.Name
        member this.Number = this.Number

    interface ISize with 
        member this.Size = this.Size 

    interface IColor with 
        member this.Color = this.Color 


let useDatabase mapper (f: LiteRepository -> unit) = 
    use memoryStream = new MemoryStream()
    FSharpBsonMapper.RegisterInheritedConverterType<IItem,Item1>()
    FSharpBsonMapper.RegisterInheritedConverterType<IItem,Item2>()
    FSharpBsonMapper.RegisterInheritedConverterType<IItem,Item1OfRecord>()
    FSharpBsonMapper.RegisterInheritedConverterType<IItem,Item2OfRecord>()
    use db = new LiteRepository(memoryStream, mapper)
    f db
    
    
let inheritedTypeTests mapper=
  testList "InheritedTypeTests Tests" [
  
    testCase "EOrder with items that has different types" <| fun _ -> 
      useDatabase mapper <| fun db ->
        let item1 = 
            Item1 ( 
                id = 0,
                art = "art",
                name = "name",
                number = 1000,
                barcode = "7254301" 
            )

        let item2 = 
            Item2 ( 
                id = 0,
                art = "art",
                name = "name",
                number = 1000,
                color = "red" ,
                size = 39 
            )

        let eorder = { Id = 1; Items = [item1;item2];  OrderNumRange = "" }

        let queryedEOrder =
            db 
            |> LiteRepository.insertItem eorder
            |> LiteRepository.query<EOrder> 
            |> LiteQueryable.first
        
        match queryedEOrder.Items with 
        | [item1;item2] -> 
            match item1,item2 with 
            | :? IBarcode,:? IColor -> 
                pass()
            | _ -> fail()    
        | _ -> fail()     

    testCase "EOrder with record items that has different types" <| fun _ -> 
      useDatabase mapper <| fun db ->
        let item1 = 
                {
                    Id = 0
                    Art = "art"
                    Name = "name"
                    Number = 1000
                    Barcode = "7254301" 
                }

        let item2 = 
            {
                Id = 0
                Art = "art"
                Name = "name"
                Number = 1000
                Color = "red" 
                Size = 39 
            }

        let eorder = { Id = 1; Items = [item1;item2];  OrderNumRange = "" }

        let queryedEOrder =
            db 
            |> LiteRepository.insertItem eorder
            |> LiteRepository.query<EOrder> 
            |> LiteQueryable.first
        
        match queryedEOrder.Items with 
        | [item1;item2] -> 
            match item1,item2 with 
            | :? IBarcode,:? IColor ->
                pass()
            | _ -> fail()    
        | _ -> fail()     
  ]
