using System;
using System.Text;
using System.Linq;
using System.Collections.Generic;
using System.Net;
using System.IO;
using System.Globalization;
using Newtonsoft.Json.Linq;

namespace GrabAirQualityData
{
    class Program
    {

        static void Main(string[] args)
        {
            string urltpl = "http://air.moepp.gov.mk/graphs/site/pages/MakeGraph.php?station={station}&parameter={param}&beginDate={monthstart}&beginTime=00:00&endDate={monthend}&endTime=23:00&lang=mk";
            var stations = new[] { new { station = "Rektorat", param = "PM10" }, new { station = "Karpos", param = "PM25" }, new { station = "Centar", param = "PM25" } };
            foreach (var station in stations)
            {
                Console.WriteLine("Processing " + station.param + " data for station " + station.station + Environment.NewLine);

                List <Tuple<DateTime, decimal>> data = new List<Tuple<DateTime, decimal>>();
                DateTime start = new DateTime(2014, 8, 1);
                while (start < DateTime.Today)
                {
                    DateTime monthstart = start.Date;
                    DateTime monthend = start.AddMonths(1).AddDays(-1).Date;

                    string url = urltpl.Replace("{station}", station.station).Replace("{param}", station.param)
                        .Replace("{monthstart}", monthstart.ToString("yyyy-MM-dd")).Replace("{monthend}", monthend.ToString("yyyy-MM-dd"));

                    Console.WriteLine("Fetching: " + url);
                    if (HttpGet(url, "text/html,application/xhtml+xml,application/xml;q=0.9,*/*;q=0.8", null, null, out HttpStatusCode status, out string response) && status == HttpStatusCode.OK)
                    {
                        try
                        {
                            var jres = JObject.Parse(response);
                            var measures = jres["measurements"][0]["data"] as JArray;
                            var times = (JArray)jres["times"];
                            if (measures.Count == times.Count)
                            {
                                List<Tuple<DateTime, decimal>> monthdata = new List<Tuple<DateTime, decimal>>();
                                for (int i = 0; i < measures.Count; i++)
                                    if (measures[i].Type.ToString() != "Null")
                                    {
                                        DateTime date = DateTime.ParseExact((string)times[i], "yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture, DateTimeStyles.None);
                                        monthdata.Add(new Tuple<DateTime, decimal>(date, (decimal)measures[i]));
                                    }
                                Console.WriteLine("Successfully fetched " + monthdata.Count + " measures.");
                                data.AddRange(monthdata);
                            }
                            else
                            {
                                Console.WriteLine("Error data.Count != times.Count: " + Environment.NewLine + response + Environment.NewLine);
                            }
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine("Exception: " + ex.ToString() + Environment.NewLine + response + Environment.NewLine);
                            break;
                        }
                    }
                    else
                    {
                        Console.WriteLine("Error HTTP " + status + ": " + Environment.NewLine + response + Environment.NewLine);
                    }

                    start = start.AddMonths(1);
                }

                string csvdata = String.Join(Environment.NewLine, data.Select(l => l.Item1.ToString("yyyy-MM-dd HH:mm:ss") + ";" + l.Item2.ToString()).ToArray());
                try
                {
                    File.WriteAllText(station + ".csv", "date;pm25" + Environment.NewLine + csvdata);
                    Console.WriteLine("Successfully wrote " + data.Count + " measures.");
                }
                catch (Exception ex)
                {
                    Console.WriteLine("Exception: " + ex.ToString() + Environment.NewLine + csvdata);
                }
            }

            Console.ReadLine();
        }




        public static bool HttpGet(string requestUrl, string accept, string username, string password, out HttpStatusCode responseStatus, out string responseBody)
        {
            byte[] response;
            bool result = HttpGet(requestUrl, accept, username, password, out responseStatus, out response);
            responseBody = Encoding.UTF8.GetString(response);
            return result;
        }

        public static bool HttpGet(string requestUrl, string accept, string username, string password, out HttpStatusCode responseStatus, out byte[] responseBody)
        {
            responseStatus = HttpStatusCode.Unused;

            HttpWebRequest req = (HttpWebRequest)HttpWebRequest.Create(requestUrl);
            if (!String.IsNullOrEmpty(accept))
                req.Accept = accept;
            if (username != null && password != null)
                req.Credentials = new NetworkCredential(username, password);

            try
            {
                HttpWebResponse resp = (HttpWebResponse)req.GetResponse();
                responseStatus = resp.StatusCode;
                responseBody = ReadStream(resp.GetResponseStream()).ToArray();
                resp.Close();
                return true;
            }
            catch (WebException ex)
            {
                if (ex.Status == WebExceptionStatus.ProtocolError)
                {
                    HttpWebResponse resp = (HttpWebResponse)ex.Response;
                    responseStatus = resp.StatusCode;
                    responseBody = ReadStream(resp.GetResponseStream()).ToArray();
                    resp.Close();
                    return true;
                }
                else
                {
                    responseBody = Encoding.UTF8.GetBytes(ex.ToString());
                    return false;
                }
            }
            catch (Exception ex)
            {
                responseBody = Encoding.UTF8.GetBytes(ex.ToString());
                return false;
            }
        }

        public static MemoryStream ReadStream(Stream stream)
        {
            MemoryStream memStream = new MemoryStream();
            byte[] readBuffer = new byte[4096];
            int bytesRead;
            while ((bytesRead = stream.Read(readBuffer, 0, readBuffer.Length)) > 0)
                memStream.Write(readBuffer, 0, bytesRead);
            return memStream;
        }


    }
}
