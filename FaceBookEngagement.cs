using System;
using System.Linq;
using System.Security.Policy;
using System.Xml;
using Azure.Core;
using FaceBookEngagement.Context;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace FaceBookEngagement
{
    public class FaceBookEngagement
    {
        private readonly ILogger _logger;
        private readonly HttpClient _httpClient;
        private readonly IConfiguration _configuration;
        private readonly ApplicationDbContext _context;

        public FaceBookEngagement(ILoggerFactory loggerFactory, HttpClient httpClient, IConfiguration configuration, ApplicationDbContext context)
        {
            _logger = loggerFactory.CreateLogger<FaceBookEngagement>();
            _httpClient = httpClient;
            _configuration = configuration;
            _context = context;
            _configuration = new ConfigurationBuilder()
            .SetBasePath(Environment.CurrentDirectory)
            .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
            .Build();
        }

        [Function("FaceBookEngagement")]
        public async Task Run([TimerTrigger("0 */1 * * * *")] TimerInfo myTimer)
        {
            _logger.LogInformation($"C# Timer trigger function executed at: {DateTime.Now}");
            await GetFacebookPostEngagements();
            if (myTimer.ScheduleStatus is not null)
            {
                _logger.LogInformation($"Next timer schedule at: {myTimer.ScheduleStatus.Next}");
            }
        }

        public async Task GetFacebookPostEngagements()
        {
            var accesstoken = _configuration["Facebook:accesstoken"];
            _logger.LogWarning("Accesstoken: {accesstoken}", accesstoken);
            var today = DateOnly.FromDateTime(DateTime.Today);
            var clientData = await _context.ClientDetails
                .Where(c => c.FacebookId != null && (c.FacebookRunDate == null || c.FacebookRunDate < today))
                .OrderBy(c => c.FacebookRunDate)
                .Select(c => c.FacebookId)
                .ToHashSetAsync();
            _logger.LogWarning("Client: {clientid}", clientData);
            using (HttpClient client = new HttpClient())
            {
                string url = "https://graph.facebook.com/v22.0/me/accounts?access_token="+accesstoken;

                HttpResponseMessage response = await client.GetAsync(url);

                if (response.IsSuccessStatusCode)
                {
                    string result = await response.Content.ReadAsStringAsync();
                    JObject jsonResponse = JObject.Parse(result);
                    //_logger.LogWarning("Client: {clientid}", result);
                    JArray elements = jsonResponse["data"] as JArray;
                    if (elements != null)
                    {
                        foreach (var element in elements)
                        {
                            var accId = element["id"]?.ToString();
                            var acc_access_token = element["access_token"]?.ToString();
                            bool exist = clientData.Contains(long.Parse(accId));
                            if (exist)
                            {
                                string postURL = "https://graph.facebook.com/v19.0/" + accId + "/posts?limit=100&access_token=" + acc_access_token;

                                HttpResponseMessage postResponse = await client.GetAsync(url);

                                if (postResponse.IsSuccessStatusCode)
                                {
                                    string post_result = await postResponse.Content.ReadAsStringAsync();
                                    JObject jsonPostResponse = JObject.Parse(post_result);
                                }
                                else
                                {
                                    _logger.LogWarning("Acc: {accid} {exist}", accId, exist);
                                }
                            }
                        }
                    }
                }
                else
                {
                    Console.WriteLine($"Error: {response.StatusCode}");
                    string error = await response.Content.ReadAsStringAsync();
                    Console.WriteLine(error);
                }
            }
            //if (clientData != null)
            //{
            //    var PostsData = await _context.LinkedInPosts
            //    .Where(c => c.OrganizationId == clientData.LinkedinId)
            //    .Select(c => c.PostId)
            //    .ToHashSetAsync();
            //    string jsonResult = JsonConvert.SerializeObject(PostsData, Formatting.Indented);
            //    _logger.LogWarning("Client: {clientid}", jsonResult);
            //    _logger.LogWarning("Client: {clientid} {@FirstRecord}", clientData.LinkedinId, clientData.Name);
            //    var accessToken = await GetValidLinkedInTokenAsync();
            //    ////foreach (var clientData in clients)
            //    ////{
            //    string baseUrl = "https://api.linkedin.com/v2/shares";
            //    int count = 20; // Number of records per request
            //    int start = 0;  // Start from 0
            //    int totalRecords = int.MaxValue;

            //    using HttpClient client = new HttpClient();
            //    client.DefaultRequestHeaders.Add("Authorization", "Bearer " + accessToken);

            //    while (start < totalRecords)
            //    {
            //        string requestUrl = $"{baseUrl}?q=owners&owners=urn:li:organization:{clientData.LinkedinId}&start={start}&count={count}";
            //        Console.WriteLine("requesturl :" + requestUrl);
            //        var request = new HttpRequestMessage(HttpMethod.Get, requestUrl);

            //        try
            //        {
            //            var response = await client.SendAsync(request);
            //            if (response.IsSuccessStatusCode)
            //            {
            //                var json = await response.Content.ReadAsStringAsync();
            //                JObject jsonResponse = JObject.Parse(json);

            //                if (start == 0)
            //                {
            //                    totalRecords = jsonResponse["paging"]?["total"]?.Value<int>() ?? 0;
            //                    _logger.LogWarning("Total: {totalRecords}", totalRecords);
            //                }

            //                JArray elements = jsonResponse["elements"] as JArray;
            //                if (elements != null)
            //                {
            //                    foreach (var element in elements)
            //                    {
            //                        var postId = element["id"]?.ToString();
            //                        bool postExists = PostsData.Contains(postId);
            //                        _logger.LogWarning($"Post : {postId}., status: {postExists}");
            //                        int ExistingPostId = 0;
            //                        string activityUrn = element["activity"]?.ToString();
            //                        var socialActions = await FetchSocialActions(client, accessToken, activityUrn);
            //                        if (postExists)
            //                        {
            //                            var existingPostData = await _context.LinkedInPosts
            //                                .Where(p => p.PostId == postId)
            //                                .OrderByDescending(e => e.CreatedOn)
            //                                .FirstOrDefaultAsync();
            //                            if (existingPostData != null)
            //                            {
            //                                ExistingPostId = existingPostData.Id; // Converts int to string safely
            //                                _logger.LogWarning("Existing Post ID: {ExistingPostId}", ExistingPostId);
            //                            }
            //                            else
            //                            {
            //                                _logger.LogWarning("No post found with ID: {PostId}", postId);
            //                            }
            //                        }
            //                        else
            //                        {
            //                            JArray contentEntities = element["content"]?["contentEntities"] as JArray;
            //                            var resolvedURL = "";
            //                            if (contentEntities != null && contentEntities.Count > 0)
            //                            {
            //                                var firstEntity = contentEntities[0];
            //                                _logger.LogWarning("First Entity: {firstEntity}", firstEntity);
            //                                // Check if thumbnails exist and are empty
            //                                JArray thumbnails = firstEntity["thumbnails"] as JArray;
            //                                _logger.LogWarning("thumbnails count: {thumbnails}", thumbnails != null ? thumbnails.Count : 0);
            //                                if (thumbnails == null || thumbnails.Count == 0)
            //                                {
            //                                    _logger.LogWarning("if thumbnails count: {thumbnails}", thumbnails != null ? thumbnails.Count : 0);
            //                                    resolvedURL = firstEntity["entityLocation"]?.ToString();
            //                                }
            //                                else
            //                                {
            //                                    _logger.LogWarning("else thumbnails count: {thumbnails}", thumbnails.Count);
            //                                    Console.WriteLine("Thumbnails are available.");
            //                                    resolvedURL = firstEntity["thumbnails"]?[0]?["resolvedUrl"]?.ToString();
            //                                }
            //                            }
            //                            else
            //                            {
            //                                Console.WriteLine("No content entities found.");
            //                            }
            //                            LinkedInPost post = new LinkedInPost();
            //                            post.OrganizationId = clientData.LinkedinId;
            //                            post.PostId = element["id"]?.ToString();
            //                            post.PostContent = element["text"]?["text"]?.ToString();
            //                            post.ResolvedURL = resolvedURL;
            //                            post.CreatedOn = DateTimeOffset.FromUnixTimeMilliseconds(element["created"]?["time"]?.Value<long>() ?? 0).UtcDateTime;
            //                            post.PostsCount = totalRecords;
            //                            await _context.LinkedInPosts.AddAsync(post);
            //                            await _context.SaveChangesAsync();
            //                            ExistingPostId = post.Id;
            //                            _logger.LogWarning("New Post ID: {ExistingPostId}", ExistingPostId);
            //                        }
            //                        var postObjCheck = await _context.LinkedInPostsEngagements
            //                            .Where(c => c.OrganizationId == clientData.LinkedinId && c.LinkedInPostId == ExistingPostId && c.DateOfRun <= today)
            //                            .FirstOrDefaultAsync();
            //                        if (postObjCheck == null)
            //                        {
            //                            LinkedInPostsEngagement postObj = new LinkedInPostsEngagement
            //                            {
            //                                OrganizationId = clientData.LinkedinId,
            //                                LinkedInPostId = ExistingPostId,
            //                                TotalLikes = socialActions["likes"],
            //                                Impressions = 0,
            //                                TotalComments = socialActions["comments"],
            //                                TotalShares = socialActions["shares"],
            //                                DateOfRun = today
            //                            };
            //                            _logger.LogWarning($"{clientData.LinkedinId}------- {ExistingPostId} ---- {today}");
            //                            await _context.LinkedInPostsEngagements.AddAsync(postObj);
            //                            await _context.SaveChangesAsync();
            //                            _logger.LogWarning("Post Data inserted");
            //                        }
            //                        else
            //                        {
            //                            _logger.LogWarning("Post Data Exist");
            //                        }
            //                        //Console.WriteLine(new string('-', 50));
            //                    }
            //                }
            //                else
            //                {
            //                    _logger.LogWarning("elements is not getting");
            //                }
            //                start += count;
            //            }
            //            else
            //            {
            //                Console.WriteLine($"API request failed with status: {response.StatusCode}");
            //                break;
            //            }
            //        }
            //        catch (HttpRequestException ex)
            //        {
            //            Console.WriteLine($"Request failed: {ex.Message}");
            //            break;
            //        }
            //    }
            //    clientData.LinkedInRunDate = today;
            //    await _context.SaveChangesAsync();
            //}
            //else
            //{
            //    _logger.LogInformation("No matching record found.");
            //}
            //return System.Text.Json.JsonSerializer.Serialize(clientData);
        }
    }
}

