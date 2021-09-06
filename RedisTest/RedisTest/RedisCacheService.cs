namespace RedisTest
{
    using System;
    using StackExchange.Redis;
    using System.Net.Sockets;
    using System.Threading;
    using Microsoft.Extensions.Configuration;

    /// <summary>
    /// Helper class for redis cache service code.
    /// </summary>
    /// <remarks>
    /// Official guide uses lot of static fields to make ConnectionMultiplexer a singleton. Here I presume that this class will be deistributed through app instead. (Using DI for example.)
    /// </remarks>
    public class RedisCacheService
    {
        private readonly IConfigurationRoot configurationRoot;
        private const string secretName = "CacheConnection";

        public RedisCacheService(IConfigurationRoot configurationRoot)
        {
            this.configurationRoot = configurationRoot;
        }

        public void Initialize()
        {
            lazyConnection = CreateConnection();
        }

        public void Uninitialize()
        {
            // Clears the cache.
            // Uncomment to clear data for next run of test application.
            // (This will get funny if more people are going to try this demo at once.)
            //GetDatabase()?.Execute("FLUSHALL");

            // Closes the connection.
            CloseConnection(lazyConnection);
        }

        public IDatabase GetDatabase()
        {
            return BasicRetry(() => Connection.GetDatabase());
        }

        public System.Net.EndPoint[] GetEndPoints()
        {
            return BasicRetry(() => Connection.GetEndPoints());
        }

        public IServer GetServer(string host, int port)
        {
            return BasicRetry(() => Connection.GetServer(host, port));
        }

        /// <summary>
        /// Get the client list, useful to see if connection list is growing.
        /// </summary>
        /// <returns>Client list.</returns>
        /// <remarks>
        /// Note that this requires allowAdmin=true in the connection string.
        /// Same as command "CLIENT LIST"
        /// </remarks>
        public ClientInfo[] GetClients()
        {
            var endpoint = (System.Net.DnsEndPoint)GetEndPoints()[0];
            IServer server = GetServer(endpoint.Host, endpoint.Port);
            return server.ClientList();
        }

        /// <summary>
        /// Force a new ConnectionMultiplexer to be created.
        /// NOTES:
        ///     1. Users of the ConnectionMultiplexer MUST handle ObjectDisposedExceptions, which can now happen as a result of calling ForceReconnect().
        ///     2. Don't call ForceReconnect for Timeouts, just for RedisConnectionExceptions or SocketExceptions.
        ///     3. Call this method every time you see a connection exception. The code will:
        ///         a. wait to reconnect for at least the "ReconnectErrorThreshold" time of repeated errors before actually reconnecting
        ///         b. not reconnect more frequently than configured in "ReconnectMinFrequency"
        /// </summary>
        public void ForceReconnect()
        {
            var utcNow = DateTimeOffset.UtcNow;
            var previousTicks = Interlocked.Read(ref lastReconnectTicks);
            var previousReconnectTime = new DateTimeOffset(previousTicks, TimeSpan.Zero);
            var elapsedSinceLastReconnect = utcNow - previousReconnectTime;

            // If multiple threads call ForceReconnect at the same time, we only want to honor one of them.
            if (elapsedSinceLastReconnect < ReconnectMinFrequency)
            {
                return;
            }

            lock (reconnectLock)
            {
                utcNow = DateTimeOffset.UtcNow;
                elapsedSinceLastReconnect = utcNow - previousReconnectTime;

                if (firstErrorTime == DateTimeOffset.MinValue)
                {
                    // We haven't seen an error since last reconnect, so set initial values.
                    firstErrorTime = utcNow;
                    previousErrorTime = utcNow;
                    return;
                }

                if (elapsedSinceLastReconnect < ReconnectMinFrequency)
                {
                    return; // Some other thread made it through the check and the lock, so nothing to do.
                }

                var elapsedSinceFirstError = utcNow - firstErrorTime;
                var elapsedSinceMostRecentError = utcNow - previousErrorTime;

                bool shouldReconnect =
                    elapsedSinceFirstError >= ReconnectErrorThreshold // Make sure we gave the multiplexer enough time to reconnect on its own if it could.
                    && elapsedSinceMostRecentError <= ReconnectErrorThreshold; // Make sure we aren't working on stale data (e.g. if there was a gap in errors, don't reconnect yet).

                // Update the previousErrorTime timestamp to be now (e.g. this reconnect request).
                previousErrorTime = utcNow;

                if (!shouldReconnect)
                {
                    return;
                }

                firstErrorTime = DateTimeOffset.MinValue;
                previousErrorTime = DateTimeOffset.MinValue;

                Lazy<ConnectionMultiplexer> oldConnection = lazyConnection;
                CloseConnection(oldConnection);
                lazyConnection = CreateConnection();
                Interlocked.Exchange(ref lastReconnectTicks, utcNow.UtcTicks);
            }
        }

        #region Inner Microsoft Docs code https://docs.microsoft.com/en-us/azure/azure-cache-for-redis/cache-dotnet-core-quickstart
        private Lazy<ConnectionMultiplexer> lazyConnection;
        /// <summary>
        /// This should always behave as a singleton. Reuse for the whole application!
        /// </summary>
        private ConnectionMultiplexer Connection { get => lazyConnection.Value; }

        #region Reconnection fields
        private long lastReconnectTicks = DateTimeOffset.MinValue.UtcTicks;
        private DateTimeOffset firstErrorTime = DateTimeOffset.MinValue;
        private DateTimeOffset previousErrorTime = DateTimeOffset.MinValue;

        private readonly object reconnectLock = new object();

        // In general, let StackExchange.Redis handle most reconnects,
        // so limit the frequency of how often ForceReconnect() will
        // actually reconnect.
        private TimeSpan ReconnectMinFrequency => TimeSpan.FromSeconds(60);

        // If errors continue for longer than the below threshold, then the
        // multiplexer seems to not be reconnecting, so ForceReconnect() will
        // re-create the multiplexer.
        private TimeSpan ReconnectErrorThreshold => TimeSpan.FromSeconds(30);

        private int RetryMaxAttempts => 5;
        #endregion

        /// <summary>
        /// Creates connection to cache using HOST name.
        /// </summary>
        /// <returns></returns>
        private Lazy<ConnectionMultiplexer> CreateConnection()
        {
            return new Lazy<ConnectionMultiplexer>(() =>
            {
                var cacheConnection = configurationRoot[secretName];
                return ConnectionMultiplexer.Connect(cacheConnection);
            });
        }

        private void CloseConnection(Lazy<ConnectionMultiplexer> oldConnection)
        {
            if (oldConnection == null)
            {
                return;
            }

            try
            {
                oldConnection.Value.Close();
            }
            catch (Exception)
            {
                // Example error condition: if accessing oldConnection.Value causes a connection attempt and that fails.
            }
        }

        // In real applications, consider using a framework such as
        // Polly to make it easier to customize the retry approach.
        /// <summary>
        /// Tries to return func, until <see cref="RetryMaxAttempts"/> is reached.
        /// </summary>
        private T BasicRetry<T>(Func<T> func)
        {
            int reconnectRetry = 0;
            int disposedRetry = 0;

            while (true)
            {
                try
                {
                    return func();
                }
                catch (Exception ex) when (ex is RedisConnectionException || ex is SocketException)
                {
                    reconnectRetry++;

                    if (reconnectRetry > RetryMaxAttempts)
                    {
                        throw;
                    }

                    ForceReconnect();
                }
                catch (ObjectDisposedException)
                {
                    disposedRetry++;
                    if (disposedRetry > RetryMaxAttempts)
                    {
                        throw;
                    }
                }
            }
        }


        #endregion
    }
}
