module Runner

open Expecto
open Expecto.Logging
open Tests.Bson
open Tests.LiteDatabase

let testConfig =  
    { Expecto.Tests.defaultConfig with 
         parallelWorkers = 1
         verbosity = LogLevel.Debug }

let liteDbTests = 
    testList "All tests" [  
        bsonConversions
        liteDatabaseUsage
    ]


[<EntryPoint>]
let main argv = runTests testConfig liteDbTests