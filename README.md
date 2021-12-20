# Twitter Simulation

<ins>**STEPS TO EXECUTE THE PROJECT**</ins>

1. Unzip the zip file project4.zip and go to folder project4.
2. Start the server program using the command - dotnet fsi twitter_server.fsx and it runs
    on the port 1800.
3. Start the client program using the command - dotnet fsi twitter_client.fsx
    <num_of_users> <server_IP_addr> <server_port_num> <client_id>
4. Sample command to start a client tester -
    dotnet fsi twitter-simulation-client.fsx 1000 192.168.0.151 1800 client
5. To run multiple clients in this system, each client should give the same command
    mentioned in step 4 from different terminals or from different machines with different
    client ids.

<ins>**IMPLEMENTATION**</ins>

Twitter simulation implementation consists of two parts, a client process to simulate actions
for the number of users given in the input(twitter_client) and a server engine for performing
all services. These two will run on separate machines or on different terminals in a machine.

A Twitter like Engine( **Server** ) has been implementedusing actor model in the AKKA
framework which performs the following functionalities:
* Registration of the user account
* Sign in for each registered user
* Posting tweets by active users in the system
* Re-tweet by users
* Follow/Subscribe to other user’s tweets
* Query of tweets subscribed to, query of tweets with specific hashtags and tweets with
the user’s mention
* Showing feed to active users

This Engine uses Data tables in F# to store, update and query data of each user, their tweets,
their feed and their followers. Whenever a client request for any operation comes, the engine
will perform the service using the appropriate datatables and return the response. In the
simulator, the data required for each action are stored in their respective actors and are not
shared to make them work independently.

**Datatables** used in this implementation - User table,Tweets table, Feeds table, Mentions
table, Hashtags table and a Followers data table.

A Twitter simulator/tester( **Client** ) has been implementedusing a simulation actor and actors
for users in the system, which use the AKKA actor model and perform simulation of all the
functionalities specified for the server engine. The client machine hits requests to the server
for various actions like Follow, Adding Tweets, Sign in etc., and displays the response to the
end user.
* Theclientmachinehandlesaspecifiednumberofusersandsimulatesfunctionalities
for each user individually.
* TheclientfirstspawnstheUserActorforeachuserandeachoneofthemisregistered
with the server (user data stored in table).
* After auser’s registrationisover,theUserActorstarts performingsimulationsfor
tweeting, following/subscribing, querying hashtags etc., randomly after getting
instruction from the simulator.
* All theactions simulatedintheclientarebased onthe **Zipfdistribution** ranking
given to the users(explained in later sections).
* Inorderto **simulateliveconnectionanddisconnectionofusers** ,20%ofusersfrom
theregistereduserslistarerandomlychosenandmadetogoofflineandanother20%
of users who are already offline are made active. This is done every 5 seconds.

<ins>**Zipf Distribution**</ins>

Zipf's law is an empirical law formulated using mathematical statistics that refers to the fact
that for many types of data studied in the physical and social sciences, the rank-frequency
distribution is an inverse relation.
Simulation of Zipf distribution on the number of followers has been implemented using the
following specifications and constraints:

* Each user is assigned a rank randomly. The user with the highest rank will have the
most number of followers(num_of_users - 1) and the user with second rank will have
(num_of_users - 1)/2 subscribers and so on. This forms a zipf distribution on the
number of subscribers
* To ensure that tweets are distributed properly, the frequency of each user tweeting is
done based on the user’s rank, i.e The user with first rank will tweet more frequently
than all others, say once in 50ms(hence posting more number of tweets), user with
second rank will post tweets once in 55ms and so on for lower ranked ones.
* When multiple clients are included in this simulation, each client will follow the same
Zipf distribution for all users in that client.
* Thus the number of users with higher number of subscribers/followers will be less
than the number of users with minimum - 1 subscriber(s) and hence Zipf distribution
is followed correctly.

In this simulation, the **tweet** , **follow** , **queryingofhashtags&mentions** etckeepshappening
**oncein50msto100ms** approximately.Thususerfeeds,queryresultswillbedisplayedinthe
clientmachine.Fortheprogram/simulationto **terminate** ,ctrl+Corcmd+C shouldbegiven.
Sinceinthissimulationcertainusersgoonline/offlineata 5 secondinterval,theprogram
keeps running simulating the live connections.

<ins>**LARGEST NETWORK HANDLED**</ins>

Thelargestnetworkhandled inour projectisupto 100,000userswith 2 clientmachines
sendingrequeststoaserverengine.Serverhandledaround 5000 requestspersecondforupto
10000 users and from there on, it remained the same.

<ins>**PERFORMANCE EVALUATION**</ins>

In this simulation, the tweet, follow, querying of hashtags & mentions etc keeps happening
once in 50ms to 100ms approximately. Thus user feeds, query results will be displayed in the
client machine. For the program/simulation to terminate, ctrl+C or cmd+C should be given.
Since in this simulation certain users go online/offline at a 5 second interval, the program
keeps running simulating the live connections.

**Graph showing Zipf distribution** on number of subscribersand number of tweets in both
single client as well as multi client scenarios:
**X-axis:** User ID values; **Y-axis:** Number of subscribers or Number of tweets


<ins>**Server Performance Statistics and observations**</ins>

In order to monitor the performance of the server engine, we analysed the number of requests
the server engine could handle in 1 second. We monitored this for various input
values(ranging from 500 to 10000) with both single client and multiple clients.

For a lower number of users, the number of requests handled per second is less since the
requests sent to the server will be less as well. But with an increase in the number of users,
the requests sent to the server is more and thus the number of requests handled per second by
the server increases. But after a certain point, the requests handled per second stays the same.
The maximum requests the server could handle in 1 second was around 5000 for 10000 users
whereas for 100 users the maximum requests the server handled in 1 second was around 100
only. Another interesting observation was that the average time taken for each utility(like
tweeting, following etc) increases when the number of users and client machines increases
due to a high number of requests generated.

