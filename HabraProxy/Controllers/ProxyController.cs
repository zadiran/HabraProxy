using HtmlAgilityPack;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Web.Mvc;

namespace HabraProxy.Controllers
{
    public class ProxyController : Controller
    {
        // Path to site can be changed in web.config
        private string proxiedSite = ConfigurationManager.AppSettings["ProxiedSite"].ToString();

        /// <summary>
        /// Proxy
        /// </summary>
        /// <param name="path">Url of proxy</param>
        /// <returns>Content of proxied site</returns>
        public ActionResult Index(string path = null)
        {
            // Get path to habrahabr
            var _path = path;
            if (_path == null)
                _path = proxiedSite;
            else
                _path = _path.Replace(getRootPath(), proxiedSite);
            
            // Download page from habrahabr
            var wc = new WebClient();
            var content = wc.DownloadData(_path);
            var encoded = Encoding.UTF8.GetString(content);
            if (string.IsNullOrEmpty(encoded))
                return Content(string.Empty);


            var tree = new HtmlDocument();
            tree.LoadHtml(encoded);
            
            //replace all references from habrahabr site to localhost
            foreach (var a in tree.DocumentNode.SelectNodes("//a"))
	        {
                if (a.Attributes["href"] != null)
                {
                    a.Attributes["href"].Value = Regex.Replace(a.Attributes["href"].Value,
                                                               getUrlPattern(proxiedSite), 
                                                               getRootPath());
                }
	        }  

            // Add tm's
            var innerBody = tree.DocumentNode.SelectSingleNode("//body").InnerHtml;

            // tokenize and protect values, that shouldn't be processed - tags and content inside utility tags
            var matches = Regex.Matches(innerBody, "(<style.*?>.*?</style>|<script.*?>.*?</script>|<.*?>)").Cast<Match>();
            int index = 0;
            var dict = new Dictionary<int, string>();
            foreach (var match in matches)
            {
                dict[index] = match.Value;
                innerBody = innerBody.Replace(match.Value, protectInt(index));
                index++;
            }

            // add tm
            innerBody = addTMs(innerBody);

            //restore tokenized values
            foreach (var item in dict)
            {
                innerBody = innerBody.Replace(protectInt(item.Key), item.Value);
            }


            tree.DocumentNode.SelectSingleNode("//body").InnerHtml = innerBody;

            return Content(tree.DocumentNode.InnerHtml);
        }

        private string addTMs(string input)
        {
            var _input = input;
            var sixLettersWords = Regex.Matches(_input, @"\b[A-Za-zА-Яа-я]{6}\b").Cast<Match>().Select(x => x.Value).Distinct();
            foreach (var word in sixLettersWords)
            {
                _input = Regex.Replace(_input, @"(?<=\b)" + word + @"(?=\b)", word + "™");
            }
            return _input;
        }

        private string getRootPath()
        {
            return Request.Url.Scheme + "://" + Request.Url.Authority + Request.ApplicationPath.TrimEnd('/');
        }

        private string protectInt(int value)
        {
            return string.Format("%$#{0}#$%", value);
        }
        
        private string getUrlPattern(string url)
        {
            return Regex.Replace(url, "https?://", "(https?://)?");
        }
    }
}
