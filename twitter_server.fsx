#time "on"
#r "nuget: Akka.FSharp" 
#r "nuget: Akka.Remote"

open System
open System.Data
open Akka.Actor
open Akka.Configuration
open Akka.FSharp

//Configuration
let configuration = 
    ConfigurationFactory.ParseString(
        @"akka {
            actor {
                provider = ""Akka.Remote.RemoteActorRefProvider, Akka.Remote""
            }
            remote {
                helios.tcp {
                    port = 1801
                    hostname = 192.168.0.229
                }
            }
        }")

let system = ActorSystem.Create("Server", configuration)

let dataSet = new DataSet()

//Tables
let userDT = new DataTable("User_Data_Table")
userDT.Columns.Add("user_id") |> ignore
userDT.Columns.Add("user_rank") |> ignore
userDT.Columns.Add("online") |> ignore
userDT.PrimaryKey = [|userDT.Columns.["user_id"]|] |> ignore
dataSet.Tables.Add(userDT)

let feedsDT = new DataTable("Feeds_Data_Table")
feedsDT.Columns.Add("user_id") |> ignore
feedsDT.Columns.Add("owner_id") |> ignore 
feedsDT.Columns.Add("tweet_id") |> ignore
dataSet.Tables.Add(feedsDT)

let tweetDT = new DataTable("Tweet_Data_Table")
tweetDT.Columns.Add("tweet_id") |> ignore
tweetDT.Columns.Add("tweet") |> ignore
tweetDT.Columns.Add("tweet_time") |> ignore
tweetDT.PrimaryKey = [|tweetDT.Columns.["tweet_id"]|] |> ignore
dataSet.Tables.Add(tweetDT)

let mentionDT = new DataTable("Mention_Data_Table")
mentionDT.Columns.Add("user_id") |> ignore
mentionDT.Columns.Add("mentioned_by") |> ignore
mentionDT.Columns.Add("tweet_id") |> ignore
dataSet.Tables.Add(mentionDT)

let hashtagDT = new DataTable("Hashtag_Data_Table")
hashtagDT.Columns.Add("hashtag") |> ignore
hashtagDT.Columns.Add("tweet_id") |> ignore
dataSet.Tables.Add(hashtagDT)

let followersDT = new DataTable("Followers_Data_Table")
followersDT.Columns.Add("user_id") |> ignore
followersDT.Columns.Add("follower_id") |> ignore
dataSet.Tables.Add(followersDT)

dataSet.AcceptChanges()

//Messages
type DBMessages = 
    | RegisterUser of string * int * IActorRef
    | SignInUser of string
    | SignOutUser of string
    | AddTweetToDB of string * string
    | AddHashtagToDB of string * string 
    | AddMentionUserToDB of string * string * string
    | AddFeedToDB of string * string * string * IActorRef
    | SendUserFeedFromDB of string * IActorRef
    | AddFollower of string * string * IActorRef
    | SendQueryHashtagResult of string * string * IActorRef
    | SendQueryMentionResult of string * string * IActorRef

type FeedMessages = 
    | UpdateFollowersFeeds of string * string * string * IActorRef
    | SendUserFeed of string * IActorRef

type FollowMessages = 
    | FollowUser of string * string * IActorRef

type TweetMessages = 
    | AddTweet of string * string * IActorRef
    | ParseTweet of string * string
    | AddHashtag of string
    | AddMentionUser of string * string

type QueryMessages = 
    | QueryHashtag of string * string * IActorRef
    | QueryMention of string * string * IActorRef

type UserMessages = 
    | Signup of string * int * IActorRef
    | Signin of string * IActorRef
    | Signout of string * IActorRef

//Actors
let DatabaseActor (mailbox:Actor<_>) =
    
    let rec loop() = actor{
        let! msg = mailbox.Receive()
        // printf "%A" msg
        // printfn " %s" (mailbox.Sender().Path.Name)
        match msg with
        | RegisterUser(userId, rank, senderRef)                             ->  userDT.Rows.Add(userId, rank, "false") |> ignore
                                                                                userDT.AcceptChanges() 
                                                                                let responseMsg = ("SignupSuccess",userId,"",0)
                                                                                senderRef <! responseMsg

        | SignInUser(userId)                                                ->  let row = userDT.Select("user_id='"+userId+"'")
                                                                                if row.Length > 0 then
                                                                                    row.[0].["online"] <- "true"

        | SignOutUser(userId)                                               ->  let row = userDT.Select("user_id='"+userId+"'")
                                                                                if row.Length > 0 then
                                                                                    row.[0].["online"] <- "false"

        | AddTweetToDB(tweetId, tweet)                                      ->  tweetDT.Rows.Add(tweetId, tweet, DateTime.Now) |> ignore
                                                                                tweetDT.AcceptChanges()

                                                                    
        | AddHashtagToDB(hashtag, tweetId)                                  ->  hashtagDT.Rows.Add(hashtag, tweetId) |> ignore
                                                                                hashtagDT.AcceptChanges()

        | AddMentionUserToDB (mentionedUser, mentionedBy, tweetId)          ->  let mentionedusername = (mentionedUser).Split '@'
                                                                                mentionDT.Rows.Add(mentionedusername.[1], mentionedBy, tweetId) |> ignore
                                                                                mentionDT.AcceptChanges()

        | AddFeedToDB(userId, tweetId, tweet, senderRef)                    ->  feedsDT.Rows.Add(userId, userId, tweetId) |> ignore
        
                                                                                //Getting all followers
                                                                                let queryResult = query{
                                                                                    for follower in followersDT.AsEnumerable() do
                                                                                        where (follower.["user_id"] |> string = userId)
                                                                                        select (follower.["follower_id"] |> string)
                                                                                }
                                                                                let followersList = Seq.toList queryResult
                                                                                for followerId in followersList do
                                                                                    feedsDT.Rows.Add(followerId, userId, tweetId) |> ignore
                                                                                    let mutable isUserOnline = false 
                                                                                    let row = userDT.Select("user_id='"+followerId+"'")
                                                                                    if row.Length > 0 then
                                                                                        let status = (row.[0].["online"]).ToString()
                                                                                        isUserOnline <- status="true"

                                                                                    if isUserOnline then
                                                                                        let response = ("TweetNotification", (userId+"|"+followerId), tweet, 0)
                                                                                        senderRef <! response

                                                                                feedsDT.AcceptChanges()

                                                                                //for followerId in followersList do
                                                                                //    mailbox.Self <! SendTweetNotification(userId, followerId, tweet, senderRef)
                                                                                    

        | SendUserFeedFromDB(userId, senderRef)                             ->  let queryResult = query{
                                                                                    for tweet in tweetDT.AsEnumerable() do
                                                                                    join feed in feedsDT.AsEnumerable() on ((tweet.["tweet_id"] |> string) = (feed.["tweet_id"] |> string))
                                                                                    where (feed.["user_id"] |> string=userId)
                                                                                    select((feed.["owner_id"]|>string) + " has Tweeted:{"+(tweet.["tweet"] |> string)+"} AT {"+(tweet.["tweet_time"] |> string)+"}\n")
                                                                                }

                                                                                let feedList = Seq.toList queryResult
                                                                                if feedList.Length > 0 then
                                                                                    let feed = List.fold (+) "" feedList
                                                                                    let response = ("ShowUserFeed", userId, feed, 0)
                                                                                    senderRef <! response

        | SendQueryHashtagResult(userId, ht, senderRef)                    ->   let queryResult = query{
                                                                                    for tweet in tweetDT.AsEnumerable() do
                                                                                    join hashtag in hashtagDT.AsEnumerable() on ((tweet.["tweet_id"] |> string) = (hashtag.["tweet_id"] |> string))
                                                                                    where (hashtag.["hastag"] |> string=ht)
                                                                                    select(ht+": Tweet:{"+(tweet.["tweet"] |> string)+"} posted AT {"+(tweet.["tweet_time"] |> string)+"}\n")
                                                                                }

                                                                                let tweetList = Seq.toList queryResult
                                                                                let mutable result = ""
                                                                                if tweetList.Length > 0 then
                                                                                    result <- List.fold (+) "" tweetList
                                                                                else
                                                                                    result <- "No tweets with "+ht
                                                                                let response = ("QueryResult", userId, result, 0)
                                                                                senderRef <! response

        | SendQueryMentionResult(userId, mentionUser, senderRef)           ->   let queryResult = query{
                                                                                    for tweet in tweetDT.AsEnumerable() do
                                                                                    join mention in mentionDT.AsEnumerable() on ((tweet.["tweet_id"] |> string) = (mention.["tweet_id"] |> string))
                                                                                    where (mention.["user_id"] |> string=mentionUser)
                                                                                    select(mentionUser + ": Tweet:{"+(tweet.["tweet"] |> string)+"} posted AT {"+(tweet.["tweet_time"] |> string)+"}\n")
                                                                                }

                                                                                let tweetList = Seq.toList queryResult
                                                                                let mutable result = ""
                                                                                if tweetList.Length > 0 then
                                                                                    result <- List.fold (+) "" tweetList
                                                                                else
                                                                                    result <- "No tweets with "+mentionUser
                                                                                let response = ("QueryResult", userId, result, 0)
                                                                                senderRef <! response

        | AddFollower(userId, followerId, senderRef)                        ->   //Getting all followers
                                                                                let queryResult = query{
                                                                                    for follower in followersDT.AsEnumerable() do
                                                                                        where (follower.["user_id"] |> string = userId)
                                                                                        select (follower.["follower_id"] |> string)
                                                                                }
                                                                                let followersList = Seq.toList queryResult
                                                                                
                                                                                //Checking if follwers limit is reached for the follow user
                                                                                let row = userDT.Select("user_id='"+followerId+"'")
                                                                                let followerRank = row.[0].["user_rank"]
                                                                                let followersCount = followersList.Length
                                                                                
                                                                                if followersCount >= (followerRank.ToString() |> int) then
                                                                                    let responseMsg = ("FollowerLimitReached", userId, "" ,0)
                                                                                    senderRef <! responseMsg
                                                                                else
                                                                                    //Checking if the user id is already in the followers list; otherwise adding the userid to followers
                                                                                    if not (List.contains userId followersList) then
                                                                                        followersDT.Rows.Add(followerId, userId) |> ignore
                                                                                        followersDT.AcceptChanges()  
                                                                                        let responseMsg = ("FollowSuccess", userId, followerId ,0)
                                                                                        senderRef <! responseMsg  
                                                                                    
                                                                                    
        
        return! loop()
    }
    loop()
let dbRef = spawn system "Database" DatabaseActor

let FeedActor (mailbox:Actor<_>) =
    
    let rec loop() = actor{
        let! msg = mailbox.Receive()
        match msg with
        | UpdateFollowersFeeds(userId, tweetId, tweet, senderRef)  ->   dbRef <! AddFeedToDB(userId, tweetId, tweet, senderRef)
                                                                        
        | SendUserFeed(userId, senderRef)                          ->   dbRef <! SendUserFeedFromDB(userId, senderRef)
                                                                        
        return! loop()
    }
    loop()
let feedRef = spawn system "Feed" FeedActor


let FollowActor (mailbox:Actor<_>) =
    
    let rec loop() = actor{
        let! msg = mailbox.Receive()
        match msg with
        | FollowUser(userId, followUserId, senderRef)  ->   dbRef <! AddFollower(userId, followUserId, senderRef)
                                                            
        return! loop()
    }
    loop()
let followRef = spawn system "Follow" FollowActor

let TweetActor (mailbox:Actor<_>) =
    let mutable tweetCount = 0

    let rec loop() = actor{
        let! msg = mailbox.Receive()
        match msg with
        | AddTweet(userId, tweet, senderRef)           ->   tweetCount <- tweetCount + 1
                                                            dbRef <! AddTweetToDB((tweetCount|>string), tweet)
                                                            mailbox.Self <! ParseTweet(userId, tweet)
                                                            feedRef <! UpdateFollowersFeeds (userId, (tweetCount|>string), tweet, senderRef)
                                                            let responseMsg = ("TweetPosted",userId,"",0)
                                                            senderRef <! responseMsg
                                                            

        | ParseTweet(userId, tweet)                     ->  let wordList = tweet.Split ' '
                                                            for word in wordList do
                                                                if word.StartsWith '#' then
                                                                    mailbox.Self <! AddHashtag(word)
                                                                elif word.StartsWith '@' then
                                                                    mailbox.Self <! AddMentionUser(word, userId)

        | AddHashtag(hashtag)                           ->  dbRef <! AddHashtagToDB(hashtag, (tweetCount|>string))

        | AddMentionUser(mentionedUser, mentionedBy)    ->  dbRef <! AddMentionUserToDB(mentionedUser,mentionedBy, (tweetCount|>string))
                                                                            
        return! loop()
    }
    loop()
let tweetRef = spawn system "Tweet" TweetActor

let QueryActor (mailbox:Actor<_>) =
    let mutable tweetCount = 0

    let rec loop() = actor{
        let! msg = mailbox.Receive()
        match msg with
        | QueryHashtag(userId, hashtag, senderRef)  ->  dbRef <! SendQueryHashtagResult(userId, hashtag, senderRef)

        | QueryMention(userId, mention, senderRef)  -> dbRef <! SendQueryMentionResult(userId, mention, senderRef)
                                                                               
        return! loop()
    }
    loop()
let queryRef = spawn system "Query" QueryActor



let UserActor (mailbox:Actor<_>) = 
    
    let rec loop () = actor {
        let! message = mailbox.Receive()
        
        match message with
        | Signup (userId, rank, senderRef)      ->  dbRef <! RegisterUser (userId, rank, senderRef)
                                                    
        | Signin (userId, senderRef)            ->  dbRef <! SignInUser (userId)
                                                    feedRef <! SendUserFeed(userId, senderRef)
                                                    let responseMsg = ("SigninSuccess",userId,"",0)
                                                    senderRef <! responseMsg

        | Signout(userId, senderRef)            ->  dbRef <! SignOutUser(userId)
                                                    let responseMsg = ("SignoutSuccess",userId,"",0)
                                                    senderRef <! responseMsg

        return! loop()
    } 
    loop()
let userActorRef = spawn system "User" UserActor


let ServerActor (mailbox:Actor<_>) = 
    let mutable requestCount = 0UL
    let startTime = DateTime.Now
    let rec loop () = actor {
        let! (msg: obj) = mailbox.Receive()
        let (command, _, _, _) : Tuple<string, string, int, IActorRef> = downcast msg
        //printf "%A" msg
        //printfn " %s" (mailbox.Sender().Path.Name)
        requestCount <- requestCount + 1UL
        match command with
        | "Init"                            ->  printfn "SERVER_STARTED"
                                                mailbox.Self <! ("Print","",0,mailbox.Self)

        | "Signup"                          ->  let (_, userId, rank, senderRef) : Tuple<string, string, int, IActorRef> = downcast msg
                                                userActorRef <! Signup(userId, rank, senderRef)

        | "Signin"                          ->  //printfn "Singin request"
                                                let (_, userId, _, senderRef) : Tuple<string, string, int, IActorRef> = downcast msg
                                                userActorRef <! Signin(userId, senderRef)
                                                
        | "Signout"                         ->  let (_, userId, _, senderRef) : Tuple<string, string, int, IActorRef> = downcast msg
                                                userActorRef <! Signout(userId, senderRef)

        | "Tweet"                           ->  let (_, tweet, _, senderRef) : Tuple<string, string, int, IActorRef> = downcast msg
                                                let userId = mailbox.Sender().Path.Name
                                                tweetRef <! AddTweet(userId, tweet, senderRef)

        | "QueryHashtag"                    ->  let (_, hashtag, _, senderRef) : Tuple<string, string, int, IActorRef> = downcast msg
                                                let userId = mailbox.Sender().Path.Name
                                                queryRef <! QueryHashtag(userId, hashtag, senderRef) 

        | "QueryMention"                    ->  let (_, mention, _, senderRef) : Tuple<string, string, int, IActorRef> = downcast msg
                                                let userId = mailbox.Sender().Path.Name
                                                queryRef <! QueryMention(userId, mention, senderRef)

        | "Follow"                          ->  let (_, followUserId, _, senderRef) : Tuple<string, string, int, IActorRef> = downcast msg
                                                let userId = mailbox.Sender().Path.Name
                                                followRef <! FollowUser(userId, followUserId, senderRef)

        | "Print"                           ->  let mutable average = 0UL
                                                requestCount <- requestCount - 1UL
                                                let timediff = (DateTime.Now-startTime).TotalSeconds |> uint64
                                                if requestCount > 0UL && timediff > 0UL then
                                                    average <- requestCount/timediff
                                                    printfn "Total requests server %u. Average requests served per second %u" requestCount average
                                                system.Scheduler.ScheduleTellOnce(TimeSpan.FromMilliseconds(1000.0), mailbox.Self, ("Print","",0,mailbox.Self))
        
        | _                                 ->  printfn "Case not covered"
        return! loop()
    }
    loop()

let server = spawn system "ServerActor" ServerActor
server <! ("Init","",0,server)
system.WhenTerminated.Wait()
