using System;
using System.Collections.Generic;
using System.Security;
using System.Threading.Tasks;
using Helpers;
using Microsoft.Extensions.Configuration;
using Microsoft.Graph;
using Microsoft.Identity.Client;

namespace PollingConsoleApp
{
    class Program
    {
        private static object _deltaLink = null;
        private static IUserDeltaCollectionPage _previousPage = null;
        private static GraphServiceClient _graphClient;


        static async Task Main(string[] args)
        {
            Console.WriteLine("Hello World!");
            var config = LoadAppSettings();
            if (config == null)
            {
                Console.WriteLine("Invalid appsettings.json file.");
                return;
            }

            var userName = /*"jacob.e.ottesen@jeodev.onmicrosoft.com";//*/ReadUsername();
            var userPassword = ReadPassword();

            Console.WriteLine("All users in tenant:");
            await CheckForUpdates(config, userName, userPassword);
            Console.WriteLine();
            while (true)
            {
                Console.WriteLine("... sleeping for 10s - press CTRL+C to terminate");
                System.Threading.Thread.Sleep(10 * 1000);
                Console.WriteLine("> Checking for new/updated users since last query...");
                await CheckForUpdates(config, userName, userPassword);
            }

        }



        private static SecureString ReadPassword()
        {
            Console.WriteLine("Enter your password");
            SecureString password = new SecureString();


            //foreach (var v in pw)
            //{
            //    password.AppendChar(v);
            //}
            while (true)
            {
                ConsoleKeyInfo c = Console.ReadKey(true);
                if (c.Key == ConsoleKey.Enter)
                {
                    break;
                }
                password.AppendChar(c.KeyChar);
                Console.Write("*");
            }
            Console.WriteLine();
            return password;
        }

        private static string ReadUsername()
        {
            string username;
            Console.WriteLine("Enter your username");
            username = Console.ReadLine();
            return username;
        }

        private static IConfigurationRoot LoadAppSettings()
        {
            try
            {
                var config = new ConfigurationBuilder()
                    .SetBasePath(System.IO.Directory.GetCurrentDirectory())
                    .AddJsonFile("appsettings.json", false, true)
                    .Build();

                if (string.IsNullOrEmpty(config["applicationId"]) ||
                    string.IsNullOrEmpty(config["tenantId"]))
                {
                    return null;
                }

                return config;
            }
            catch (System.IO.FileNotFoundException)
            {
                return null;
            }
        }

        private static void OutputUsers(IUserDeltaCollectionPage users)
        {
            foreach (var user in users)
            {
                Console.WriteLine($"User: {user.Id}, {user.GivenName} {user.Surname}");
            }
        }

        private static async Task<IUserDeltaCollectionPage> GetUsers(GraphServiceClient graphClient, object deltaLink)
        {
            IUserDeltaCollectionPage page;

            // IF this is the first request (previous=null), then request all users
            //    and include Delta() to request a delta link to be included in the
            //    last page of data
            if (_previousPage == null)
            {
                page = await graphClient.Users
                    .Delta()
                    .Request()
                    .Top(5)
                    .Select("Id,GivenName,Surname")
                    .GetAsync();
            }
            // ELSE, not the first page so get the next page of users
            else
            {
                _previousPage.InitializeNextPageRequest(graphClient, deltaLink.ToString());
                page = await _previousPage.NextPageRequest.GetAsync();
            }

            _previousPage = page;
            return page;
        }

        private static async Task CheckForUpdates(IConfigurationRoot config, string userName, SecureString userPassword)
        {
            var graphClient = GetAuthenticatedGraphClient(config, userName, userPassword);

            // get a page of users
            var users = await GetUsers(graphClient, _deltaLink);

            OutputUsers(users);

            // go through all of the pages so that we can get the delta link on the last page.
            while (users.NextPageRequest != null)
            {
                users = users.NextPageRequest.GetAsync().Result;
                OutputUsers(users);
            }

        //    object nextLink;

        //    if (users.AdditionalData.TryGetValue("@odata.nextLink", out nextLink))
        //    {

        //    //    if (nextLink.ToString().Contains("delta"))
        //    //    {
        //    //        _deltaLink = nextLink.ToString();
        //    //    }

        //    var test = nextLink.ToString().Substring(nextLink.ToString().IndexOf("$skiptoken") + 1);
        //    var queryOptions = new List<QueryOption>()
        //        {
        //            new QueryOption("$skiptoken", test)
        //        };

        //    var delta = await graphClient.Users
        //    .Delta()
        //    .Request()
        //    .GetAsync();

        //}



        object deltaLink;

            if (users.AdditionalData.TryGetValue("@odata.deltaLink", out deltaLink))
            {
                _deltaLink = deltaLink;
            }
        }

        private static GraphServiceClient GetAuthenticatedGraphClient(IConfigurationRoot config, string userName, SecureString userPassword)
        {
            var authenticationProvider = CreateAuthorizationProvider(config, userName, userPassword);
            var graphClient = new GraphServiceClient(authenticationProvider);
            return graphClient;
        }

        private static IAuthenticationProvider CreateAuthorizationProvider(IConfigurationRoot config, string userName, SecureString userPassword)
        {
            var clientId = config["applicationId"];
            var authority = $"https://login.microsoftonline.com/{config["tenantId"]}/v2.0";

            List<string> scopes = new List<string>();
            scopes.Add("User.Read");
            scopes.Add("Mail.Read");

            var cca = PublicClientApplicationBuilder.Create(clientId)
                .WithAuthority(authority)
                .Build();
            return MsalAuthenticationProvider.GetInstance(cca, scopes.ToArray(), userName, userPassword);
        }
    }

}
