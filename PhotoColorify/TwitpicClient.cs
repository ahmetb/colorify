using System;
using System.IO;
using System.Net;
using System.Text;
using System.Threading;

namespace Colorify
{
    public class TwitpicClient
    {
        private const string EMPTY_TWEET_MESSAGE = "Check out picture I have done with Colorify.";

        /// <summary>
        /// URL for the TwitPic API's upload method
        /// </summary>
        private const string TWITPIC_UPLOAD_API_URL = "http://twitpic.com/api/upload";

        /// <summary>
        /// URL for the TwitPic API's upload and post method
        /// </summary>
        private const string TWITPIC_UPLOAD_AND_POST_API_URL = "http://twitpic.com/api/uploadAndPost";

        private AutoResetEvent allDone = new AutoResetEvent(false);
        /// <summary>
        /// Uploads the photo and sends a new Tweet
        /// </summary>
        /// <param name="binaryImageData">The binary image data.</param>
        /// <param name="tweetMessage">The tweet message.</param>
        /// <param name="filename">The filename.</param>
        /// <returns>Return true, if the operation was succeded.</returns>
        public bool UploadPhoto(byte[] binaryImageData, string tweetMessage, string twitterUsername, string twitterPassword)
        {
            // Documentation: http://www.twitpic.com/api.do
            string boundary = Guid.NewGuid().ToString();
            string requestUrl = TWITPIC_UPLOAD_AND_POST_API_URL; // String.IsNullOrEmpty(tweetMessage) ? TWITPIC_UPLOAD_API_URL : TWITPIC_UPLOAD_AND_POST_API_URL
            HttpWebRequest request = (HttpWebRequest)WebRequest.Create(requestUrl);
            request.AllowReadStreamBuffering = true;
            request.AllowAutoRedirect = true;
            string encoding = "iso-8859-1";

            if (string.IsNullOrEmpty(tweetMessage)) tweetMessage = EMPTY_TWEET_MESSAGE; 

            request.ContentType = string.Format("multipart/form-data; boundary={0}", boundary);
            request.Method = "POST";

            string header = string.Format("--{0}", boundary);
            string footer = string.Format("--{0}--", boundary);

            StringBuilder contents = new StringBuilder();
            contents.AppendLine(header);

            string fileContentType = "image/jpeg";
            string fileHeader = String.Format("Content-Disposition: file; name=\"{0}\"; filename=\"{1}\"", "media", "image.jpg");
            string fileData = Encoding.GetEncoding(encoding).GetString(binaryImageData, 0, binaryImageData.Length);

            contents.AppendLine(fileHeader);
            contents.Append("Content-Length: " + binaryImageData.Length);
            contents.AppendLine(String.Format("Content-Type: {0}", fileContentType));
            contents.AppendLine();
            contents.AppendLine(fileData);

            contents.AppendLine(header);
            contents.AppendLine(String.Format("Content-Disposition: form-data; name=\"{0}\"", "username"));
            contents.AppendLine();
            contents.AppendLine(twitterUsername);

            contents.AppendLine(header);
            contents.AppendLine(String.Format("Content-Disposition: form-data; name=\"{0}\"", "password"));
            contents.AppendLine();
            contents.AppendLine(twitterPassword);
            
            if (!String.IsNullOrEmpty(tweetMessage))
            {
                contents.AppendLine(header);
                contents.AppendLine(String.Format("Content-Disposition: form-data; name=\"{0}\"", "message"));
                contents.AppendLine();
                contents.AppendLine(tweetMessage);
            }

            contents.AppendLine(footer);

            byte[] bytes = Encoding.GetEncoding(encoding).GetBytes(contents.ToString());

            var asyncresult = request.BeginGetRequestStream((IAsyncResult result) => { }, null);
            
            using(Stream requestStream = request.EndGetRequestStream(asyncresult))
            {
                requestStream.Write(bytes, 0, bytes.Length);
                requestStream.Close();
            }

            var respresult = request.BeginGetResponse(ResponseCallBack, null);
            allDone.WaitOne();

            
            using (WebResponse response = request.EndGetResponse(respresult))
            {
                using (StreamReader reader = new StreamReader(response.GetResponseStream()))
                {
                    string result = reader.ReadToEnd();
                    reader.Close();
                    response.Close();

                    return result.Contains(@"<mediaurl>");
                }
            }

            return false;
        }

        private void ResponseCallBack(IAsyncResult ar)
        {
            allDone.Set();
        }
    }
}
