module Tests.Types

open System

type Person = { Id: int; Name: string }
type LowerCaseId = { id: int; age:int }
type SimpleUnion = One | Two
type RecordWithSimpleUnion = { Id: int; Union: SimpleUnion }
type RecordWithList = { Id: int; List: int list }
type Maybe<'a> = Just of 'a | Nothing
type RecordWithGenericUnion<'t> = { Id: int; GenericUnion: Maybe<'t> }
type RecordWithDateTime = { id: int; created: DateTime }
type RecordWithMap = { id : int; map: Map<string, string> }
type RecordWithArray = { id: int; arr: int[] }
type RecordWithDecimal = { id: int; number: decimal }
type RecordWithLong = { id: int; long: int64 }
type RecordWithGuid = { id: int; guid: Guid }
type RecordWithBytes = { id: int; data:byte[] }
type RecordWithObjectId = { id: LiteDB.ObjectId }
type Shape = 
    | Circle of float
    | Rect of float * float
    | Composite of Shape list

type RecordWithShape = { Id: int; Shape: Shape }