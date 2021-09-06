################# WELCOME #################
This is a test project for Redis cache hosted on Azure Server (free trial).
Redis cache will be online approximately to 16.9.2021.
However, system running as a server could host Redis inside a localhost docker and would be without any extra costs.



################# SETUP #################
1.	You need following packages: StackExchange.Redis, Newtonsoft.Json
	If you don't have them avaliable, you can install them using command console executing scripts inside the project root:
	dotnet add package Microsoft.Extensions.Configuration.UserSecrets
	dotnet add package StackExchange.Redis
	dotnet add package Newtonsoft.json
	dotnet restore
	
2.	Setup projects secret.
	Execute the following line in command manager in project root.
	dotnet user-secrets set CacheConnection "redistest-licek.redis.cache.windows.net,abortConnect=false,ssl=true,allowAdmin=true,password=wdaJzNvMbf+D5wk2hjDdkWROkZMk53YKc84yKXxQUm8="

3.	If you want to use this project but the test Azure database is gone, you can create your own Azure Redis Cache.
	Follow steps of the main tutorial in SOURCES. Don't forget to delete the testing cache and deactive free trial after 30 days!



################# SOURCES #################
This project follows steps described here: https://docs.microsoft.com/en-us/azure/azure-cache-for-redis/cache-dotnet-how-to-use-azure-redis-cache
Other sources:
https://docs.redis.com/latest/rs/references/client_references/client_csharp/
https://www.c-sharpcorner.com/UploadFile/2cc834/using-redis-cache-with-C-Sharp/
https://www.youtube.com/watch?v=UrQWii_kfIE
