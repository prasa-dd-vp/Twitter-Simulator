module Server
#time "on"
#r "nuget: Akka.FSharp" 
#r "nuget: Akka.Remote"
#load "DataBase.fs"
#load "Utils.fs"

open System
open Akka.Actor
open Akka.Configuration
open Akka.FSharp
open Utils

//Configuration
let configuration = 
    ConfigurationFactory.ParseString(
        @"akka {
            actor {
                provider = ""Akka.Remote.RemoteActorRefProvider, Akka.Remote""
            }
            remote {
                helios.tcp {
                    port = 1800
                    hostname = 192.168.0.229
                }
            }
        }")

let system = ActorSystem.Create("Server", configuration)

type SignUpMessages = 
    | Signup of string * IActorRef



//Actors
let SignupActor (mailbox:Actor<_>) = 

    let rec loop () = actor {
        let! message = mailbox.Receive()
        
        match message with
        | Signup (userId, senderRef)             -> registerUser (userId |> string)
                                                    let responseMsg = "SignupSuccess"
                                                    
                                                    senderRef <! responseMsg



    } 
    loop()
let signupActorRef = spawn system "Signup" SignupActor


let ServerActor (mailbox:Actor<_>) = 
    let rec loop () = actor {
        let! (msg: obj) = mailbox.Receive()
        let (command, _, _) : Tuple<string, string, IActorRef> = downcast msg
        
        match command with
        | "Signup"                          ->  let (_, userId, senderRef) : Tuple<string, string, IActorRef> = downcast msg
                                                signupActorRef <! Signup(userId, senderRef)

        | _                                 ->  printfn "Case not covered"
        return! loop()
    }
    loop()

// Start of the algorithm - spawn Boss, the delgator
let boss = spawn system "ServerActor" ServerActor
boss <! ("Start","","","",DateTime.Now)
system.WhenTerminated.Wait()