using Blade;
using Blade.Messages;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using SignalWire.Relay;
using SignalWire.Relay.Calling;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace JWT
{
    internal class Program
    {
        private static ILogger Logger { get; set; }

        private static ManualResetEventSlim sCompleted = new ManualResetEventSlim();
        private static bool sSuccessful = false;

        private static Client sClient = null;

        private static string sSessionHost = null;
        private static string sSessionProject = null;
        private static string sJWTTokenServer = null;
        private static string sJWTURL = null;
        private static string sJWTTokenClient = null;
        private static string sJWTRefreshToken = null;

        public static int Main(string[] args)
        {
            // Setup logging to console for Blade and SignalWire
            BladeLogging.LoggerFactory.AddSimpleConsole(LogLevel.Trace);
            SignalWireLogging.LoggerFactory.AddSimpleConsole(LogLevel.Trace);

            // Create a logger for this entry point class type
            Logger = SignalWireLogging.CreateLogger<Program>();

            Logger.LogInformation("Started");

            Stopwatch timer = Stopwatch.StartNew();

            // Use environment variables
            sSessionHost = Environment.GetEnvironmentVariable("SWCLIENT_TEST_SESSION_HOST");
            sSessionProject = Environment.GetEnvironmentVariable("SWCLIENT_TEST_SESSION_PROJECT");
            sJWTTokenServer = Environment.GetEnvironmentVariable("SWCLIENT_TEST_JWT_TOKEN");
            sJWTURL = Environment.GetEnvironmentVariable("SWCLIENT_TEST_JWT_URL");

            // Make sure we have mandatory options filled in
            if (string.IsNullOrWhiteSpace(sSessionHost))
            {
                Logger.LogError("Missing 'SWCLIENT_TEST_SESSION_HOST' environment variable");
                return -1;
            }
            if (string.IsNullOrWhiteSpace(sSessionProject))
            {
                Logger.LogError("Missing 'SWCLIENT_TEST_SESSION_PROJECT' environment variable");
                return -1;
            }
            if (string.IsNullOrWhiteSpace(sJWTTokenServer))
            {
                Logger.LogError("Missing 'SWCLIENT_TEST_JWT_TOKEN' environment variable");
                return -1;
            }
            if (string.IsNullOrWhiteSpace(sJWTURL))
            {
                Logger.LogError("Missing 'SWCLIENT_TEST_JWT_URL' environment variable");
                return -1;
            }


            if (!JWTPost(sJWTURL, sSessionProject, sJWTTokenServer, out sJWTTokenClient, out sJWTRefreshToken))
            {
                sCompleted.Set();
            }
            else
            {
                Logger.LogInformation("Successfully obtained JWT token: {0}", sJWTTokenClient);

                try
                {
                    // Create the client
                    using (sClient = new Client(sSessionProject, sJWTTokenClient, host: sSessionHost, jwt: true))
                    {
                        // Setup callbacks before the client is started
                        sClient.OnReady += Client_OnReady;

                        // Start the client
                        sClient.Connect();

                        // Wait more than long enough for the test to be completed
                        //if (!sCompleted.Wait(TimeSpan.FromMinutes(2))) Logger.LogError("Test timed out");
                        sCompleted.Wait();
                    }
                }
                catch (Exception exc)
                {
                    Logger.LogError(exc, "Client startup failed");
                }
            }

            timer.Stop();

            // Report test outcome
            if (!sSuccessful) Logger.LogError("Completed unsuccessfully: {0} elapsed", timer.Elapsed);
            else Logger.LogInformation("Completed successfully: {0} elapsed", timer.Elapsed);

#if DEBUG
            Console.WriteLine("Press any key to exit...");
            Console.ReadKey(true);
#endif
            return sSuccessful ? 0 : -1;
        }

        private static void Client_OnReady(Client client)
        {
            // This is called when the client has established a new session, this is NOT called when a session is restored
            Logger.LogInformation("OnReady");

            if (!JWTPut(sJWTURL, sSessionProject, sJWTTokenServer, ref sJWTTokenClient, ref sJWTRefreshToken))
            {
                sCompleted.Set();
            }
            else
            {
                Logger.LogInformation("Successfully refreshed JWT token: {0}", sJWTTokenClient);

                string authentication = Client.CreateJWTAuthentication(sSessionProject, sJWTTokenClient);
                client.Session.ReauthenticateAsync(JObject.Parse(authentication)).ContinueWith(r =>
                {
                    if (r.IsFaulted || r.IsCanceled)
                    {
                        Logger.LogError(r.Exception, "Reauthentication failed");
                        sCompleted.Set();
                        return;
                    }

                    CallingAPI api = new CallingAPI(client);
                    api.Setup().ContinueWith(t =>
                    {
                        if (t.IsCompletedSuccessfully)
                        {
                            sSuccessful = true;
                        }
                        sCompleted.Set();
                    });

                });

            }
        }

        private static bool JWTPost(string uri, string project, string token, out string jwt_token, out string refresh_token)
        {
            jwt_token = null;
            refresh_token = null;

            WebRequest webRequest = WebRequest.Create(uri);
            webRequest.Timeout = 5000;

            webRequest.Method = "POST";
            webRequest.Headers.Add(HttpRequestHeader.ContentType, "application/json");
            webRequest.Headers.Add(HttpRequestHeader.Accept, "application/json");
            webRequest.Headers.Add(HttpRequestHeader.AcceptCharset, "utf-8");
            webRequest.Headers.Add(HttpRequestHeader.Authorization, "Basic " + Convert.ToBase64String(Encoding.UTF8.GetBytes(project + ":" + token)));
            webRequest.Headers.Add(HttpRequestHeader.UserAgent, "Blade.Relay/1");

            JObject result = null;
            try
            {
                using (StreamWriter writer = new StreamWriter(webRequest.GetRequestStream(), new UTF8Encoding(false)))
                {
                    writer.Write(new JObject { ["expires_in"] = 1 }.ToString(Formatting.None));
                }
                using (StreamReader webResponseReader = new StreamReader(webRequest.GetResponse().GetResponseStream(), Encoding.UTF8))
                {
                    result = JObject.Parse(webResponseReader.ReadToEnd());
                }

                jwt_token = result.Value<string>("jwt_token");
                refresh_token = result.Value<string>("refresh_token");
            }
            catch (Exception exc)
            {
                Logger.LogError(exc, "Failed JWTPost");
                return false;
            }
            Logger.LogDebug("Received GET result: {0}", result.ToString(Newtonsoft.Json.Formatting.Indented));

            return true;
        }

        private static bool JWTPut(string uri, string project, string token, ref string jwt_token, ref string refresh_token)
        {
            WebRequest webRequest = WebRequest.Create(uri);
            webRequest.Timeout = 5000;

            webRequest.Method = "PUT";
            webRequest.Headers.Add(HttpRequestHeader.ContentType, "application/json");
            webRequest.Headers.Add(HttpRequestHeader.Accept, "application/json");
            webRequest.Headers.Add(HttpRequestHeader.AcceptCharset, "utf-8");
            webRequest.Headers.Add(HttpRequestHeader.Authorization, "Basic " + Convert.ToBase64String(Encoding.UTF8.GetBytes(project + ":" + token)));
            webRequest.Headers.Add(HttpRequestHeader.UserAgent, "Blade.Relay/1");

            JObject result = null;
            try
            {
                using (StreamWriter writer = new StreamWriter(webRequest.GetRequestStream(), new UTF8Encoding(false)))
                {
                    writer.Write(new JObject { ["refresh_token"] = refresh_token }.ToString(Formatting.None));
                }
                using (StreamReader webResponseReader = new StreamReader(webRequest.GetResponse().GetResponseStream(), Encoding.UTF8))
                {
                    result = JObject.Parse(webResponseReader.ReadToEnd());
                }

                jwt_token = result.Value<string>("jwt_token");
                refresh_token = result.Value<string>("refresh_token");
            }
            catch (Exception exc)
            {
                Logger.LogError(exc, "Failed JWTPut");
                return false;
            }
            Logger.LogDebug("Received GET result: {0}", result.ToString(Newtonsoft.Json.Formatting.Indented));

            return true;
        }
    }
}
