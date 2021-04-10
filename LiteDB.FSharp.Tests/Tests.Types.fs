module Tests.Types

open System
open LiteDB.FSharp

type Person = { Id: int; Name: string }
type LowerCaseId = { id: int; age:int }
type SimpleUnion = One | Two

type PhoneNumber = private PhoneNumber of int64
with 
    member x.Value =
        let (PhoneNumber v) = x
        v

    static member Create(phoneNumber: int64) = 
        match phoneNumber.ToString().Length with 
        | 11 -> PhoneNumber phoneNumber
        | _ -> failwithf "phone number %d 's length should be 11" phoneNumber

    interface ISingleCaseInfo<int64> with 
        member x.CaseInfo(_) =
            let (PhoneNumber v) = x
            v

type Size =
    private 
        | US of float
        | EUR of float
        | UK of float
with 
    static member CreateEUR(eur: float) = 
        if eur >=  19. && eur <= 46. && eur % 0.5 = 0.
        then Size.EUR eur
        else failwithf "%f is not a valid eur value" eur


type RecordWithSimpleUnion = { Id: int; Union: SimpleUnion }
type RecordWithSinglePrivateUnion = { Id: int; PhoneNumber: PhoneNumber }
type RecordWithMultiplePrivateUnions = { Id: int; Size: Size }
type RecordWithList = { Id: int; List: int list }
type Maybe<'a> = Just of 'a | Nothing
type RecordWithGenericUnion<'t> = { Id: int; GenericUnion: Maybe<'t> }
type RecordWithDateTime = { id: int; created: DateTime }
type RecordWithMap = { id : int; map: Map<string, string> }
type RecordWithArray = { id: int; arr: int[] }
type RecordWithOptionalArray = { id: int; arr: int[] option }
type RecordWithResizeArray = { id: int; resizeArray: ResizeArray<int> }
type RecordWithDecimal = { id: int; number: decimal }
type RecordWithLong = { id: int; long: int64 }
type RecordWithFloat = { id: int; float: float }
type RecordWithGuid = { id: int; guid: Guid }
type RecordWithBytes = { id: int; data:byte[] }
type RecordWithObjectId = { id: LiteDB.ObjectId }
type RecordWithOptionOfValueType = { id:int; optionOfValueType: Option<int>  }
type RecordWithOptionOfReferenceType = { id:int; optionOfReferenceType : Option<Person>  }

type Shape =
    | Circle of float
    | Rect of float * float
    | Composite of Shape list


type Value = Num of int | String of string
type RecordWithMapDU = { Id: int; Properties: Map<string, Value> }

type RecordWithShape = { Id: int; Shape: Shape }

type ComplexUnion<'t> =
    | Any of 't
    | Int of int
    | String of string
    | Generic of Maybe<'t>


type SingleCaseDU = SingleCaseDU of int

type RecordWithSingleCaseId = { Id : SingleCaseDU; Value : string }

type IColor =
    abstract member Color : string

type IBarcode =
    abstract member Barcode : string

type ISize =
    abstract member Size : int

type IItem =
    abstract member Id : int
    abstract member Art : string
    abstract member Name : string
    abstract member Number : int


type RecWithMember = {
    Id: int
    Name: string
}
with member this.Ignored() = sprintf "%d %s" this.Id this.Name
     member this.IgnoredToo = sprintf "%d %s" this.Id this.Name


[<CLIMutable>]
type Company=
  { Id: int
    Name: string}

[<CLIMutable>]
type EOrder=
  { Id: int
    Items : IItem list
    OrderNumRange: string }

[<CLIMutable>]
type Order=
  { Id : int
    Company : Company
    EOrders : EOrder list}
