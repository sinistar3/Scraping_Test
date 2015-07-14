using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Net;
using System.IO;
using System.Web;

namespace Scraping_Test_v._01
{
    class ArrestRecord
    {
        public string BookingNumber { get; set; }
        public string Name          { get; set; }
        public string Gender        { get; set; }
        public string DoB           { get; set; }
        public string ArrestedBy    { get; set; }
        public string ArrestDate    { get; set; }
        public string ArrestLine    { get { return string.Format("{0}|{1}|{2}|{3}|{4}|{5}", BookingNumber, Name, Gender, DoB, ArrestedBy, ArrestDate); } }
    }

    class Scraper
    {
        public static Uri webUri = new Uri("http://ws.ocsd.org/ArrestLog/ArrestLogMain.aspx");
        public static int fileIndex = 0;
        public static string viewState = "";
        public static string viewStateGenerator = "";
        public static string eventValidation = "";
        public static string nameFilter = "LMN";
        public static string FileHeader = "BookingNumber|Name|Sex|DOB|ArrestedBy|ArrestDate";
        public static List<FileInfo> fileList;
        public static List<ArrestRecord> Records;
        public static List<string> dates = new List<string> { "", "062215", "062315", "062415", "062515", "062615" };


        /// <summary>
        /// Obligatory Main Method.
        /// </summary>
        
        static void Main(string[] args)
        {
            Console.WriteLine("Retrieving HTML from " + webUri.AbsoluteUri + "\n\n");
            GetInfo();
            Console.WriteLine("\n\nParsing data from HTML.\n\n");
            ParseInfo();
            Console.WriteLine("\n\nWriting data to records text file.\n\n");
            WriteInfo();
            Console.WriteLine("\n\nFinished. Press enter or close window to exit.");
            Console.Read();
        }
        
        private static void GetInfo()
        {
            fileList = new List<FileInfo>();
            foreach (var date in dates)
            {
                if (!date.Equals(""))
                    Console.WriteLine(string.Format("Retrieving data from date {0}-{1}-20{2}...\n"
                    , date.Substring(0, 2)
                    , date.Substring(2, 2)
                    , date.Substring(4, 2)));
                else
                    Console.WriteLine("Retrieving HTML from initial page...");

                RetrieveHtml(date);

                Console.WriteLine("Complete. HTML recorded to " + fileList[dates.IndexOf(date)].FullName + "\n\n");
            }
        }

        /// <summary>
        /// Makes POST requests and reads the resultant HTML to a file. If the date string is not blank, this method will format 
        /// </summary>
        /// <param name="date"> string that contains the ddl value for the POST, and also acts as a key for the stored HTML files </param>
        
        private static void RetrieveHtml(string date)
        {
            HttpWebRequest request = (HttpWebRequest)WebRequest.Create(webUri);

            if (!date.Equals(""))
            {
                string postStatement = FormatRequest(date);

                //Need this stuff to do a POST.
                request.ContentType = "application/x-www-form-urlencoded";
                request.Method = "POST";

                //Encode POST statement.
                var data = Encoding.ASCII.GetBytes(postStatement);
                request.ContentLength = data.Length;

                //Send POST statement as a request.
                using (var stream = request.GetRequestStream())
                {
                    stream.Write(data, 0, data.Length);
                }
            }

            //Sleep from 1-5 seconds after request.
            Random rng = new Random();
            int waitTime = rng.Next(1000, 5000);
            Console.WriteLine("\n\nWaiting " + waitTime.ToString() + "ms\n\n");
            System.Threading.Thread.Sleep(waitTime);

            using (var response = (HttpWebResponse)request.GetResponse())
            {
                if (response.StatusCode == HttpStatusCode.OK)
                {
                    Stream receiveStream = response.GetResponseStream();
                    StreamReader readStream = null;

                    if (response.CharacterSet == null)
                    {
                        readStream = new StreamReader(receiveStream);
                    }
                    else
                    {
                        readStream = new StreamReader(receiveStream, Encoding.GetEncoding(response.CharacterSet));
                    }

                    string data = readStream.ReadToEnd();

                    string pathToFile = "ScrapedFiles\\HTML\\ArrestLog" + date + ".html";
                    fileList.Add(new FileInfo(pathToFile));

                    if (!fileList.Last().Directory.Exists)
                        Directory.CreateDirectory(fileList.Last().DirectoryName);

                    using (StreamWriter writer = new StreamWriter(pathToFile))
                    {
                        writer.Write(data);
                    }
                    viewState = ExtractViewState(data, "__VIEWSTATE");
                    viewStateGenerator = ExtractViewState(data, "__VIEWSTATEGENERATOR");
                    eventValidation = ExtractViewState(data, "__EVENTVALIDATION");

                    readStream.Close();
                    request.Abort();
                }
            }
        }

        /// <summary>
        /// Formats a statement for a POST to the website
        /// </summary>
        /// <param name="date"> Sets a specific value for the dropdown list on the page for the POST statement. </param>
        /// <returns> Returns the formatted POST statement. </returns>
        /// 
        private static string FormatRequest(string date)
        {
            Random rng = new Random();
            //Set up stringbuilder for POST request
            StringBuilder sb = new StringBuilder();
            //Add __VIEWSTATE value to POST statement.
            sb.Append("__VIEWSTATE=");
            sb.Append(viewState);
            //Add __VIEWSTATEGENERATOR value to POST statement.
            sb.Append("&__VIEWSTATEGENERATOR=");
            sb.Append(viewStateGenerator);
            //Add __EVENTVALIDATION value to POST statement.
            sb.Append("&__EVENTVALIDATION=");
            sb.Append(eventValidation);
            //Add dropdownlist value to POST statement.
            sb.Append("&ddlArrestDates=");
            sb.Append(date);
            //Randomize an x value for click location in POST statement.
            sb.Append("&btnSearch.x=");
            sb.Append(rng.Next(1, 75).ToString());
            //Randomize a y value for click location in POST statement.
            sb.Append("&btnSearch.y=");
            sb.Append(rng.Next(1, 24).ToString());
            return sb.ToString();
        }
        
        private static string ExtractViewState(string htmlFromFile, string elementToFind)
        {
            string viewStateName = elementToFind;
            string elementKey = "value=\"";

            int elementNamePosition = htmlFromFile.IndexOf(viewStateName);
            int elementValuePosition = htmlFromFile.IndexOf(elementKey, elementNamePosition);

            int elementValueStartPosition = elementValuePosition + elementKey.Length;
            int elementValueEndPosition = htmlFromFile.IndexOf("\"", elementValueStartPosition);

            return HttpUtility.UrlEncodeUnicode(htmlFromFile.Substring(elementValueStartPosition, elementValueEndPosition - elementValueStartPosition));
        }

        private static void ParseInfo()
        {
            Records = new List<ArrestRecord>();
            foreach (var file in fileList)
            {
                if (!file.Name.Equals("ArrestLog.html"))
                {
                    ParseHtml(file.FullName);
                }
            }
        }

        /// <summary>
        /// Reads the HTML files requested earlier in the program.  Depends on the specific structure of the HTML.
        /// </summary>
        /// <param name="fileToParse"> File path string to html file to parse. </param> 

        private static void ParseHtml(string fileToParse)
        {
            //Uses the indexed dates used in the POST to come up with the arrest date.
            string arrestDate = string.Format("{0}-{1}-20{2}"
                , dates[++fileIndex].Substring(0, 2)
                , dates[fileIndex].Substring(2, 2)
                , dates[fileIndex].Substring(4, 2));
            using (StreamReader reader = File.OpenText(fileToParse))
            {
                string line;
                while ((line = reader.ReadLine()) != null)
                {
                    //The first line of the data block always contains a class that contains "ArrestRow".
                    if (line.Contains("ArrestRow"))
                    {
                        int lineCount = 0;
                        line = reader.ReadLine(); lineCount++;
                        string bookingNumber = line.Substring(line.IndexOf("BookingLink") + 13, 7);
                        line = reader.ReadLine(); lineCount++;
                        string name = line.Substring(line.IndexOf('>') + 1, line.LastIndexOf('<') - line.IndexOf('>') - 1);
                        line = reader.ReadLine(); lineCount++;
                        string dateOfBirth = line.Substring(line.IndexOf('>') + 1, line.LastIndexOf('<') - line.IndexOf('>') - 1);
                        line = reader.ReadLine(); lineCount++;
                        string gender = line.Substring(line.IndexOf('>') + 1, line.LastIndexOf('<') - line.IndexOf('>') - 1);
                        while (lineCount < 11)
                        {
                            line = reader.ReadLine(); lineCount++;
                        }
                        string arrestedBy = line.Substring(line.IndexOf("</b> ") + 5, line.LastIndexOf("</td>") - line.IndexOf("</b> ") - 5);

                        //Only get data if the name of the suspect meets the name filter criteria.
                        if (nameFilter.Contains(name[0]))
                            Records.Add(new ArrestRecord
                            {
                                BookingNumber = bookingNumber,
                                Name = name,
                                Gender = gender,
                                DoB = dateOfBirth,
                                ArrestedBy = arrestedBy,
                                ArrestDate = arrestDate
                            });
                    }
                }
            }
        }

        private static void WriteInfo()
        {
            string pathToFile = "ScrapedFiles\\Records.txt";
            fileList.Add(new FileInfo(pathToFile));
            if (!fileList.Last().Directory.Exists)
            {
                Directory.CreateDirectory(fileList.Last().DirectoryName);
            }
            using (StreamWriter writer = new StreamWriter(pathToFile))
            {
                writer.WriteLine(FileHeader);
                Records.Sort((a, b) => a.Name.CompareTo(b.Name));
                foreach (var record in Records)
                {
                    Console.WriteLine(record.ArrestLine + "\n");
                    writer.WriteLine(record.ArrestLine);
                }
            }
        }
    }
}
