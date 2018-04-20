using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace Bulbasaur.DataFetcher
{
    class Program
    {
        // **********************************************
        // *** Update or verify the following values. ***
        // **********************************************

        // Replace the accessKey string value with your valid access key.
        const string accessKey = "b3ba9dfa919f4c2d814b2d570fc0636d";

        // Verify the endpoint URI.  At this writing, only one endpoint is used for Bing
        // search APIs.  In the future, regional endpoints may be available.  If you
        // encounter unexpected authorization errors, double-check this value against
        // the endpoint for your Bing Web search instance in your Azure dashboard.
        const string uriBase = "https://api.cognitive.microsoft.com/bing/v7.0/images/search";


        // Used to return search results including relevant headers
        struct SearchResult
        {
            public String jsonResult;
            public Dictionary<String, String> relevantHeaders;
        }

        static void Main(string[] args)
        {
            DownloadImage();

            Console.Write("\nPress Enter to exit ");
            Console.ReadLine();

        }

        /// <summary>
        /// Performs a Bing Web search and return the results as a SearchResult.
        /// </summary>
        static SearchResult BingWebSearch(string searchQuery)
        {
            // Construct the URI of the search request
            var uriQuery = uriBase + "?q=" + searchQuery;

            // Perform the Web request and get the response
            WebRequest request = HttpWebRequest.Create(uriQuery);
            request.Headers["Ocp-Apim-Subscription-Key"] = accessKey;
            HttpWebResponse response = (HttpWebResponse)request.GetResponseAsync().Result;
            string json = new StreamReader(response.GetResponseStream()).ReadToEnd();

            // Create result object for return
            var searchResult = new SearchResult()
            {
                jsonResult = json,
                relevantHeaders = new Dictionary<String, String>()
            };

            // Extract Bing HTTP headers
            foreach (String header in response.Headers)
            {
                if (header.StartsWith("BingAPIs-") || header.StartsWith("X-MSEdge-"))
                    searchResult.relevantHeaders[header] = response.Headers[header];
            }

            return searchResult;
        }

        /// <summary>
        /// Formats the given JSON string by adding line breaks and indents.
        /// </summary>
        /// <param name="json">The raw JSON string to format.</param>
        /// <returns>The formatted JSON string.</returns>
        static string JsonPrettyPrint(string json)
        {
            if (string.IsNullOrEmpty(json))
                return string.Empty;

            json = json.Replace(Environment.NewLine, "").Replace("\t", "");

            StringBuilder sb = new StringBuilder();
            bool quote = false;
            bool ignore = false;
            char last = ' ';
            int offset = 0;
            int indentLength = 2;

            foreach (char ch in json)
            {
                switch (ch)
                {
                    case '"':
                        if (!ignore) quote = !quote;
                        break;
                    case '\\':
                        if (quote && last != '\\') ignore = true;
                        break;
                }

                if (quote)
                {
                    sb.Append(ch);
                    if (last == '\\' && ignore) ignore = false;
                }
                else
                {
                    switch (ch)
                    {
                        case '{':
                        case '[':
                            sb.Append(ch);
                            sb.Append(Environment.NewLine);
                            sb.Append(new string(' ', ++offset * indentLength));
                            break;
                        case '}':
                        case ']':
                            sb.Append(Environment.NewLine);
                            sb.Append(new string(' ', --offset * indentLength));
                            sb.Append(ch);
                            break;
                        case ',':
                            sb.Append(ch);
                            sb.Append(Environment.NewLine);
                            sb.Append(new string(' ', offset * indentLength));
                            break;
                        case ':':
                            sb.Append(ch);
                            sb.Append(' ');
                            break;
                        default:
                            if (quote || ch != ' ') sb.Append(ch);
                            break;
                    }
                }
                last = ch;
            }

            return sb.ToString().Trim();
        }

        static void SearchImage()
        {
            Console.OutputEncoding = Encoding.UTF8;
            String[] searchTerms = File.ReadAllLines("..\\..\\..\\..\\..\\Names.txt");

            if (accessKey.Length == 32)
            {
                for (int i = 784; i < searchTerms.Length; i++)
                {
                    Console.WriteLine("Searching the Web for: " + searchTerms[i]);
                    List<String> results = new List<String>();
                    Int32 offset = 0;
                    Int32 totalEstimatedMatches = 1;
                    Int32 count = 0;
                    while (!(results.Count >= 500 || offset >= totalEstimatedMatches))
                    {
                        SearchResult result = BingWebSearch(searchTerms[i] + $"&offset={offset}");
                        count++;
                        Match match = Regex.Match(result.jsonResult, "nextOffset\": (\\d+)");
                        if (match.Success)
                        {
                            offset = Int32.Parse(Regex.Match(result.jsonResult, "nextOffset\": (\\d+)").Groups[1].Value);
                            totalEstimatedMatches = Int32.Parse(Regex.Match(result.jsonResult, "totalEstimatedMatches\": (\\d+)").Groups[1].Value);
                        }
                        else
                        {
                            break;
                        }

                        MatchCollection matches = Regex.Matches(result.jsonResult, "\"contentUrl\": \".+? \"");
                        for (int j = 0; j < matches.Count; j++)
                        {
                            results.Add(Regex.Matches(result.jsonResult, "\"contentUrl\": \"(.+?) \"")[j].Groups[1].Value.Replace("\",", "").Replace("\\", ""));
                        }

                        Thread.Sleep(TimeSpan.FromSeconds(0.3));
                    }
                    String folder = searchTerms[i].Replace(":", "");
                    Directory.CreateDirectory($"..\\..\\..\\..\\..\\Data\\{folder}");
                    File.WriteAllLines($"..\\..\\..\\..\\..\\Data\\{folder}\\result.txt", results);
                    Console.WriteLine("\n\n");
                }
            }
            else
            {
                Console.WriteLine("Invalid Bing Search API subscription key!");
                Console.WriteLine("Please paste yours into the source code.");
            }
        }

        static void DownloadImage()
        {
            Boolean rerun = true;
            while (rerun)
            {
                rerun = false;
                String[] directories = Directory.GetDirectories("Data");
                for (int i = 0; i < directories.Length; i++)
                {
                    Console.WriteLine("Downloading\t" + i.ToString() + "\t" + directories[i].Replace("Data\\", " ") + "...");
                    String path = Path.Combine(Directory.GetCurrentDirectory(), directories[i]);
                    DirectoryInfo directoryInfo = new DirectoryInfo(path);

                    String[] urls = File.ReadAllLines(Path.Combine(Directory.GetCurrentDirectory(), directories[i], "result.txt"));

                    List<String> failed = new List<string>();
                    for (int j = 0; j < urls.Length; j++)
                    {
                        try
                        {
                            WebRequest request = HttpWebRequest.Create(urls[j]);
                            var task = Task.Factory.FromAsync(request.BeginGetResponse, request.EndGetResponse, null);
                            if (!task.Wait(TimeSpan.FromMinutes(2)))
                            {
                                throw new TimeoutException();
                            }
                            WebResponse response = task.Result;
                            Stream stream = response.GetResponseStream();
                            MemoryStream memoryStream = new MemoryStream();
                            stream.CopyTo(memoryStream);
                            Byte[] buffer = memoryStream.ToArray();
                            String extension = "." + response.ContentType.Split('/')[1];
                            Int32 count = Directory.GetFiles(Path.Combine(Directory.GetCurrentDirectory(), directories[i])).Length;
                            File.WriteAllBytes(Path.Combine(path, count.ToString() + extension), buffer);


                            response.Dispose();
                            stream.Dispose();
                            memoryStream.Dispose();


                            Console.WriteLine("Succeed!");
                        }
                        catch (Exception exception)
                        {
                            failed.Add(urls[j]);
                            Console.WriteLine("Error!");
                        }
                    }

                    File.Delete(Path.Combine(Directory.GetCurrentDirectory(), directories[i], "result.txt"));
                    if (failed.Count != 0)
                    {
                        rerun = true;
                        File.WriteAllLines(Path.Combine(Directory.GetCurrentDirectory(), directories[i], "result.txt"), failed);
                    }
                    else
                    {
                        File.WriteAllText(Path.Combine(Directory.GetCurrentDirectory(), directories[i], "result.txt"), String.Empty);
                    }

                    Console.WriteLine("\n\n");
                }
            }
        }

    }
}