﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using HtmlAgilityPack;
using Microsoft.Extensions.Configuration;

namespace ComunioBonus
{
    internal class Program
    {
        private static HttpClient _client;

        private static int _multiplier;

        private static string _username;

        private static string _password;

        private static IConfiguration _configuration;

        private static async Task Main()
        {
            Console.WriteLine("Comunio Bot");

            InitializeConfig();

            SetProperties();

            InitializeHttpClient();

            await LogIn();

            var playerPoints = (await FetchPoints()).ToList();

            var lines = playerPoints.Select(
                p => $"{p.Name},{p.Points},{p.Points * _multiplier},{p.Points} Punkte,{p.Id}");

            await File.WriteAllLinesAsync("comuniobot_points.csv", lines);

            await AddBonus(playerPoints);

            Console.WriteLine("All finished!");
        }

        private static async Task AddBonus(IEnumerable<User> users)
        {
            try
            {
                await _client.GetStringAsync("https://classic.comunio.de/administration.phtml?penalty_x=34");
            }
            catch (HttpRequestException e)
            {
                Console.WriteLine($"\nBonus site visit failed");
                Console.WriteLine("Message :{0} ", e.Message);
                Environment.Exit(1);
            }

            foreach (var user in users)
            {
                try
                {
                    var postParams = new List<KeyValuePair<string, string>>
                    {
                        new KeyValuePair<string, string>("newsDis", "messageDis"),
                        new KeyValuePair<string, string>("pid_to", $"{user.Id}"),
                        new KeyValuePair<string, string>("amount", $"{user.Points * _multiplier}"),
                        new KeyValuePair<string, string>("content", $"{user.Points} Punkte"),
                        new KeyValuePair<string, string>("cancel", "-1"),
                        new KeyValuePair<string, string>("send_x", "33"),
                    };

                    var response = await _client.PostAsync("https://classic.comunio.de/administration.phtml?penalty_x=34",
                        new FormUrlEncodedContent(postParams));

                    response.EnsureSuccessStatusCode();
                    
                    Console.WriteLine($"{user.Name} - {user.Points} points - {user.Points * _multiplier} bonus - SUCCESSFUL");
                }
                catch (HttpRequestException e)
                {
                    Console.WriteLine($"\n{user.Name} - FAILED");
                    Console.WriteLine("Message :{0} ", e.Message);
                    Environment.Exit(1);
                }
            }
        }

        private static async Task<IEnumerable<User>> FetchPoints()
        {
            try
            {
                var responseBody =
                    await _client.GetStringAsync("https://classic.comunio.de/standings.phtml?currentweekonly_x=34");

                var doc = new HtmlDocument();
                doc.LoadHtml(responseBody);

                var tableNode = doc.DocumentNode.SelectSingleNode("//*[@id=\"tablestandings\"]");

                var playerNodes = tableNode.ChildNodes.Where(cn => cn.NodeType == HtmlNodeType.Element).ToList();

                playerNodes.RemoveAt(0);

                var playerPoints = playerNodes.Select(tr =>
                {
                    var name = tr.FirstChild.NextSibling.InnerText.Trim();
                    var points = int.Parse(tr.LastChild.InnerText.Trim());
                    var idRaw = tr.FirstChild.NextSibling.FirstChild.Attributes.First(a => a.Name == "href").Value;
                    var id = int.Parse(Regex.Replace(idRaw, @".*pid=", "").Trim());

                    return new User
                    {
                        Name = name,
                        Points = points,
                        Id = id,
                    };
                });

                return playerPoints;
            }
            catch (Exception e)
            {
                Console.WriteLine("\nFetching points failed!");
                Console.WriteLine("Message :{0} ", e.Message);
                Environment.Exit(1);
                throw;
            }
        }

        private static async Task LogIn()
        {
            try
            {
                var postParams = new List<KeyValuePair<string, string>>
                {
                    new KeyValuePair<string, string>("login", _username),
                    new KeyValuePair<string, string>("pass", _password),
                    new KeyValuePair<string, string>("action", "login"),
                    new KeyValuePair<string, string>(">> Login", "-1"),
                };

                var response = await _client.PostAsync("https://classic.comunio.de/login.phtml",
                    new FormUrlEncodedContent(postParams));

                response.EnsureSuccessStatusCode();
            }
            catch (HttpRequestException e)
            {
                Console.WriteLine("\nLogin failed!");
                Console.WriteLine("Message :{0} ", e.Message);
                Environment.Exit(1);
            }
        }

        private static void InitializeHttpClient()
        {
            var cookieContainer = new CookieContainer();
            var handler = new HttpClientHandler { CookieContainer = cookieContainer };
            _client = new HttpClient(handler);
        }

        private static void InitializeConfig()
        {
            var builder = new ConfigurationBuilder().SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("appsettings.json", false, true);

            _configuration = builder.Build();
        }

        private static void SetProperties()
        {
            _multiplier = _configuration.GetSection("Multiplier").Get<int>();
            _username = _configuration.GetSection("Username").Get<string>();
            _password = _configuration.GetSection("Password").Get<string>();
        }
    }
}