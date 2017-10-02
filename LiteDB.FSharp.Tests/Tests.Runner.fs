module Runner

open Expecto
open Expecto.Logging
open Tests.Bson

let testConfig =  
    { Expecto.Tests.defaultConfig with 
         parallelWorkers = 1
         verbosity = LogLevel.Debug }

[<EntryPoint>]
let main argv = runTests testConfig bsonConversions