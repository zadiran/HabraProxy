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
            // fix url's in styles properties
            tree.DocumentNode.SelectSingleNode("//head").InnerHtml = Regex.Replace(tree.DocumentNode.SelectSingleNode("//head").InnerHtml,
                                                                                   @"(?<=url\()(?=/)",
                                                                                   proxiedSite);

            var head = tree.DocumentNode.SelectSingleNode("//head").InnerHtml;
            //replace all references from habrahabr site to localhost
            foreach (var a in tree.DocumentNode.SelectSingleNode("//body").SelectNodes("//a").Concat(tree.DocumentNode.SelectSingleNode("//body").SelectNodes("//link")))
	        {
                if (a.Attributes["href"] != null)
                {
                    a.Attributes["href"].Value = Regex.Replace(a.Attributes["href"].Value,
                                                               getUrlPattern(proxiedSite), 
                                                               getRootPath());
                }
            }

            // Add tm's
            // tokenize and protect values, that shouldn't be processed - tags and content inside utility tags
            //content of script and style tags
            int index = 0;
            var styleScriptDict = new Dictionary<int, string>();
            foreach (var item in tree.DocumentNode.SelectSingleNode("//body").SelectNodes("//script").Concat(tree.DocumentNode.SelectSingleNode("//body").SelectNodes("//style")))
            {
                if (string.IsNullOrWhiteSpace(item.InnerHtml))
                    continue;

                styleScriptDict[index] = item.InnerHtml;
                item.InnerHtml = protectInt(index);
                index++;
            }
            var innerBody = tree.DocumentNode.SelectSingleNode("//body").InnerHtml;

            var matches = Regex.Matches(innerBody, "<.*?>").Cast<Match>();
            
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
            foreach(var item in dict)
            {
                innerBody = innerBody.Replace(protectInt(item.Key), item.Value);
            }
            tree.DocumentNode.SelectSingleNode("//body").InnerHtml = innerBody;
            foreach (var item in tree.DocumentNode.SelectSingleNode("//body").SelectNodes("//script").Concat(tree.DocumentNode.SelectSingleNode("//body").SelectNodes("//style")))
            {
                var i = unprotectInt(item.InnerHtml);
                if (i != null)
                {
                    item.InnerHtml = styleScriptDict[i.Value];
                }
            }

            tree.DocumentNode.SelectSingleNode("//head").InnerHtml = head;

            return Content(tree.DocumentNode.InnerHtml);
        }

        private string addTMs(string input)
        {
            var _input = input;
            var sixLettersWords = Regex.Matches(_input, @"(?<=[^А-Яа-яA-Za-z<>-])[А-Яа-яA-Za-z-]{6}(?=[^А-Яа-яA-Za-z<>-])").Cast<Match>().Select(x => x.Value).Distinct();
            foreach (var word in sixLettersWords)
            {
                _input = Regex.Replace(_input, @"(?<=[^А-Яа-яA-Za-z<>-])" + word + @"(?=[^А-Яа-яA-Za-z<>-])", word + "™");
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
        
        private int? unprotectInt(string value)
        {
            if (!value.StartsWith("%$#") && !value.EndsWith("#$%"))
            {
                return null;
            }
            int result;
            if (!int.TryParse(value.Trim('#','$','%'), out result))
            {
                return null;
            }
            return result;
        }

        private string getUrlPattern(string url)
        {
            return Regex.Replace(url, "https?://", "(https?://)?");
        }
    }
}
