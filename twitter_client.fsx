#time "on"
#r "nuget: Akka"
#r "nuget: Akka.FSharp"
#r "nuget: Akka.Remote"
#r "nuget: Akka.TestKit"

open System
open Akka.Actor
open Akka.FSharp
open Akka.Configuration

// Input from Command Line
let numOfUsers = fsi.CommandLineArgs.[1] |> int
let serverIP = fsi.CommandLineArgs.[2] |> string
let serverPort = fsi.CommandLineArgs.[3] |> string
let clientID = fsi.CommandLineArgs.[4] |> string //Client1

// Configuration
let configuration = 
    ConfigurationFactory.ParseString(
        sprintf @"akka {            
            stdout-loglevel : DEBUG
            loglevel : ERROR
            actor {
                provider = ""Akka.Remote.RemoteActorRefProvider, Akka.Remote""
            }
            remote.helios.tcp {
                port = 8777
                hostname = 192.168.0.213
            }
    }")

type UserMessage = 
    | Signup of int * int
    | Signin
    | Signout
    | DoTweet
    | DoQuery
    | DoFollow
    | TweetNotification of string * string
    | QueryResults of string
    | ShowFeed of string


type PrintMessage = 
    | Print of string

let system = ActorSystem.Create("Client", configuration)
let addr = "akka.tcp://Server@" + serverIP + ":" + serverPort + "/user/ServerActor"
let serverRef =  system.ActorSelection(addr)
let random = Random()
                                            
//Actors
let PrintActor (mailbox:Actor<_>) = 
    let rec loop () = actor {
        let! message = mailbox.Receive()
        match message with
        | Print(msg) -> printfn "%s" msg
        return! loop()
    }
    loop()
let printRef = spawn system "Printer" PrintActor

let mutable hashtags = []
let mutable resgisteredUserIds = []
let UserActor (mailbox:Actor<_>) = 
    let userId = mailbox.Self.Path.Name |> string
    let mutable userRank = 0
    let mutable tweetInterval = 0
    let mutable isActive = false
    let mutable currentTweetCount = 0
    let tweetOptions = ["Tweet"; "ReTweet"; "HashTagTweet"; "MentionTweet"; "HashTagMentionTweet"]
    let queryOptions = ["Hashtag"; "Mention"]
    
    let rec loop() = actor {
        let! message = mailbox.Receive()
        
        match message with
        | Signup(rank, interval     )               ->  //printfn "In signup"
                                                        tweetInterval <- interval
                                                        userRank <- rank
                                                        serverRef <! ("Signup", userId, rank, mailbox.Sender())
        
        | Signin                                    ->  //printfn "In signin"
                                                        isActive <- true
                                                        serverRef <! ("Signin", userId, 0, mailbox.Sender())

        | Signout                                   ->  isActive <- false
                                                        serverRef <! ("Signout", userId, 0, mailbox.Sender())

        | DoTweet                                   ->  if isActive then
                                                            let tweetType = tweetOptions.[random.Next(tweetOptions.Length)]
                                                            
                                                            match tweetType with
                                                            | "Tweet"               ->  let tweet = userId + " has tweeted tweet: " + (currentTweetCount |> string)
                                                                                        serverRef <! ("Tweet", tweet, 0, mailbox.Sender())
                                                            
                                                            | "ReTweet"             ->  let tweet = userId + " has retweeted. Tweet:" + (currentTweetCount |> string)
                                                                                        serverRef <! ("Tweet", tweet, 0, mailbox.Sender())
                                                            
                                                            | "HashTagTweet"        ->  let hashtag = hashtags.[random.Next(hashtags.Length)]
                                                                                        let tweet = userId + " has tweeted Tweet:" + (currentTweetCount |> string) + " with hashtag:" +  hashtag
                                                                                        serverRef <! ("Tweet", tweet, 0, mailbox.Sender())

                                                            | "MentionTweet"        ->  let mutable randomUserId = resgisteredUserIds.[random.Next(resgisteredUserIds.Length)]
                                                                                        while randomUserId = userId do 
                                                                                            randomUserId <- resgisteredUserIds.[random.Next(resgisteredUserIds.Length)]
                                                                                        let tweet = userId + " has tweeted Tweet:" + (currentTweetCount |> string) + "and mentioned @" + randomUserId
                                                                                        serverRef <! ("Tweet", tweet, 0, mailbox.Sender()) 
                                                            
                                                            | "HashTagMentionTweet" ->  let mutable randomUserId = resgisteredUserIds.[random.Next(resgisteredUserIds.Length)]
                                                                                        let hashtag = hashtags.[random.Next(hashtags.Length)]
                                                                                        while randomUserId = userId do 
                                                                                            randomUserId <- resgisteredUserIds.[random.Next(resgisteredUserIds.Length)]
                                                                                        let tweet = userId + " has tweeted Tweet:" + (currentTweetCount |> string) + "with hashtag:" + hashtag + " and mentioned @" + randomUserId
                                                                                        serverRef <! ("Tweet", tweet, 0, mailbox.Sender())

                                                            | _                     ->  printfn "Undefined type"

                                                            currentTweetCount <- currentTweetCount + 1
                                                            system.Scheduler.ScheduleTellOnce(TimeSpan.FromMilliseconds((tweetInterval+500 |> float)), mailbox.Self, DoTweet) 

        | DoQuery                                   ->  if isActive then
                                                            let queryType = queryOptions.[random.Next(queryOptions.Length)]
                                                            
                                                            match queryType with
                                                            | "Hashtag"             ->  let hashtag = hashtags.[random.Next(hashtags.Length)]
                                                                                        serverRef <! ("QueryHashtag", hashtag, 0, mailbox.Sender())


                                                            | "Mention"             ->  let randomUserId = resgisteredUserIds.[random.Next(resgisteredUserIds.Length)]
                                                                                        serverRef <! ("QueryMention", ("@"+randomUserId), 0, mailbox.Sender())

                                                            | _                     ->  printfn "Undefined type"

                                                            system.Scheduler.ScheduleTellOnce(TimeSpan.FromMilliseconds(500.0), mailbox.Self, DoQuery)

        | DoFollow                                  ->  if isActive then
                                                            let mutable followUser = resgisteredUserIds.[random.Next(resgisteredUserIds.Length)]
                                                            while followUser = userId do
                                                                followUser <- resgisteredUserIds.[random.Next(resgisteredUserIds.Length)]
                                                            serverRef <! ("Follow", followUser, 0, mailbox.Sender())

        | TweetNotification(userId, tweet)          ->  printRef <! Print("\nNEW_FEED: " + userId + " has posted:" + tweet)

        | ShowFeed(feed)                            ->  printRef <! Print(userId + " FEED: " + feed)

        | QueryResults(result)                      ->  printRef <! Print(userId + " QUERY_RESULTS: " + result)

        return! loop()
    }
    loop()

let InitiatorActor (mailbox:Actor<_>)= 
    let mutable tweetCounter = 0
    let mutable userRefMap = Map.empty
    let mutable userRankList = []
    let mutable tweetIntervalList = []
    let mutable cid = ""
    let mutable userCount = 0
    let mutable offlineUsers = Set.empty
    let mutable followerLimitReachedUsers = 0
    let hashTagsList = ["#TheBeatlesGetBack"; "#BlackFriday"; "#COVID19"; "#BiggBoss"; "#SpiderManNoWayHome"; "#Football"; "#Cricket"; "#INDvsNZ"; "#Dhoni"; 
                        "#gogators"; "#cybermonday"; "#distributedsystems"; "#testmatch"; "#omicronvariant"; "#Beast"; "#valimaiupdate"]

    let rec loop() = actor {
        let! (msg:obj) = mailbox.Receive()
        //command, clientId, number of users
        let (command,_,_,_) : Tuple<string, string, string, int> = downcast msg
        
        match command with
            | "Init"                        ->  let (_,clientId,_,users) : Tuple<string, string, string, int> = downcast msg
                                                cid <- clientId
                                                userCount <- users
                                                hashtags <- hashTagsList
                                                printRef <! Print(cid + " Started")

                                                //Computing rank for zipf distribution
                                                let rankArr = [|1..userCount|]
                                                let intervalArr = [|1..userCount|]
                                                for i in [1..userCount] do
                                                    rankArr.[i-1] <- (userCount-1)/i

                                                let swap (rankArr: int[]) (intervalArr: int[]) i j =
                                                    let temp = rankArr.[i]
                                                    rankArr.[i] <- rankArr.[j]
                                                    rankArr.[j] <- temp

                                                    let temp = intervalArr.[i]
                                                    intervalArr.[i] <- intervalArr.[j]
                                                    intervalArr.[j] <- temp

                                                let randomize (rankArr:int[]) (intervalArr:int[]) =
                                                    let rand = Random()
                                                    Array.iteri (fun index _ -> swap rankArr intervalArr index (rand.Next(index, Array.length rankArr))) rankArr

                                                randomize rankArr intervalArr
                                                userRankList <- rankArr |> Array.toList
                                                tweetIntervalList <- intervalArr |> Array.toList

                                                //Signup
                                                mailbox.Self <! ("Signup","1","",0)

                                                //Change activity status of users periodically
                                                system.Scheduler.ScheduleTellOnce(TimeSpan.FromSeconds(5.0), mailbox.Self, ("UpdateUsersActivityStatus", "", "", 0))

            | "Signup"                      ->  let (_,id,_,_) : Tuple<string, string, string, int> = downcast msg
                                                let mutable numId = id |> int
                                                let userId = cid+"_"+id
                                                let rank = userRankList.[(id|>int)-1]
                                                let tweetInterval = tweetIntervalList.[(id|>int)-1]
                                                let actorRef = spawn system userId UserActor
                                                userRefMap <- Map.add userId actorRef userRefMap

                                                actorRef <! Signup(rank, tweetInterval)

                                                if numId < userCount then
                                                    numId <- numId + 1
                                                    mailbox.Self <! ("Signup", (numId|>string), "", 0)

            | "SignupSuccess"               ->  //printfn "Signup success"
                                                let (_,userId,_,_) : Tuple<string, string, string, int> = downcast msg
                                                // printRef <! Print(userId + " Registered Sucessfully")
                                                resgisteredUserIds <- userId :: resgisteredUserIds
                                                if resgisteredUserIds.Length = userCount then
                                                    printRef <! Print("All users registered!")
                                                mailbox.Self <! ("Signin",userId,"",0)
                                                mailbox.Self <! ("StartTweeting",userId,"",0)
                                                mailbox.Self <! ("StartQuerying",userId,"",0)
                                                mailbox.Self <! ("StartFollowing",userId,"",0)

            | "Signin"                      ->  //printfn "In sign in initiator"
                                                let (_,userId,_,_) : Tuple<string, string, string, int> = downcast msg
                                                userRefMap.[userId] <! Signin

            | "Signout"                     ->  let (_,userId,_,_) : Tuple<string, string, string, int> = downcast msg
                                                userRefMap.[userId] <! Signout

            | "SigninSuccess"               ->  //printfn "Signin success"
                                                //let (_,userId,_,_) : Tuple<string, string, string, int> = downcast msg
                                                //printRef <! Print(userId + " Signed In Successfully!")
                                                printf ""

            | "SignoutSuccess"              ->  //let (_,userId,_,_) : Tuple<string, string, string, int> = downcast msg
                                                //printRef <! Print(userId + " Signed Out Successfully!")
                                                printf ""

            | "TweetNotification"           ->  let (_,ids,tweet,_) : Tuple<string, string, string, int> = downcast msg
                                                let idList = ids.Split '|'
                                                let followingUser = idList.[0]
                                                let userId = idList.[1]
                                                userRefMap.[userId] <! TweetNotification(followingUser, tweet)

            | "ShowUserFeed"                ->  let (_,userId,feed,_) : Tuple<string, string, string, int> = downcast msg
                                                userRefMap.[userId] <! ShowFeed(feed)
                                                

            | "TweetPosted"                 ->  tweetCounter <- tweetCounter + 1
                                                // printRef <! Print(tweetCounter + " tweets posted")

            | "QueryResult"                 ->  let (_,userId,result,_) : Tuple<string, string, string, int> = downcast msg
                                                userRefMap.[userId] <! QueryResults(result)

            | "FollowSuccess"               ->  let (_,userId,followUserId,_) : Tuple<string, string, string, int> = downcast msg
                                                printRef <! Print (userId + " started following "+ followUserId+"\n")

            | "FollowerLimitReached"        ->  let (_,id,_,_) : Tuple<string, string, string, int> = downcast msg
                                                printRef <! Print("\n Followers limit reached for "+ id)
                                                followerLimitReachedUsers <- followerLimitReachedUsers + 1
                                                if followerLimitReachedUsers < userCount then
                                                    userRefMap.[id] <! DoFollow
                                                
            | "StartTweeting"               ->  let (_,userid,_,_) : Tuple<string, string, string, int> = downcast msg
                                                userRefMap.[userid] <! DoTweet

            | "StartQuerying"               ->  let (_,userid,_,_) : Tuple<string, string, string, int> = downcast msg
                                                userRefMap.[userid] <! DoQuery

            | "StartFollowing"              ->  let (_,userid,_,_) : Tuple<string, string, string, int> = downcast msg
                                                userRefMap.[userid] <! DoFollow

            | "UpdateUsersActivityStatus"   ->  //printfn "Updating status"
                                                let mutable offlineCount = (resgisteredUserIds.Length * 20)/100
                                                let mutable newOfflineUsers = Set.empty
                                                //printfn "%d" resgisteredUserIds.Length
                                                //printfn "%d" offlineCount
                                                for i in [1 .. offlineCount] do
                                                    let mutable offlineUser = resgisteredUserIds.[random.Next(resgisteredUserIds.Length)]
                                                    while offlineUsers.Contains(offlineUser) || newOfflineUsers.Contains(offlineUser) do
                                                        offlineUser <- resgisteredUserIds.[random.Next(resgisteredUserIds.Length)]
                                                    mailbox.Self <! ("Signout", offlineUser, "", 0)
                                                    newOfflineUsers <- Set.add offlineUser newOfflineUsers

                                                for user in offlineUsers do
                                                    mailbox.Self <! ("Signin", user, "", 0)
                                                    
                                                offlineUsers <- Set.empty
                                                offlineUsers <- newOfflineUsers

                                                system.Scheduler.ScheduleTellOnce(TimeSpan.FromSeconds(5.0), mailbox.Self, ("UpdateUsersActivityStatus", "", "", 0)) 
            
            | _                             -> printfn "Master actor received a wrong message"
        return! loop() 
    }
    loop() 

// Start of the algorithm 
let initiatorRef = spawn system "Initiator" InitiatorActor 
initiatorRef <! ("Init", clientID, "", numOfUsers)
system.WhenTerminated.Wait()