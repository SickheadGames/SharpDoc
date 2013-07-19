﻿using System;
using System.Net;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

using HtmlAgilityPack;

namespace SharpDoc
{
    public class AuthentifiedWebClient : WebClient
    {
        private byte[] loginBytes;
        private Uri siteHome;
        private bool siteNeedAuthentification;

        public HtmlDocument currentDocument { get; set; }
        public int numberCss { get; private set; }

        public AuthentifiedWebClient(string home, NetworkCredential loginData = null)
        {
            siteHome = new Uri(home);
            currentDocument = new HtmlDocument();
            numberCss = -1;

            if (loginData == null)
                siteNeedAuthentification = false;
            else
            {
                siteNeedAuthentification = true;
                Authentify(loginData);
            }

            // load the home page
            Load("");
        }



        public void Authentify(NetworkCredential loginData)
        {
            // Format the loginData to match the site log formular
            string login = String.Format("os_username={0}&os_password={1}", loginData.UserName, loginData.Password);
            loginBytes = Encoding.ASCII.GetBytes(login);
        }

        public void Load(string page)
        {
            currentDocument.Load(WebDocPage(page), Encoding.UTF8);
        }

        public Stream WebDocPage(string page, bool authentification = true)
        {
            Uri pageUri = new Uri(siteHome, page);
            if (pageUri.IsWellFormedOriginalString())
            {
                HttpWebRequest request = (HttpWebRequest)WebRequest.Create(pageUri);
                if (authentification && siteNeedAuthentification)
                {
                    request.Method = WebRequestMethods.Http.Post;
                    request.AllowWriteStreamBuffering = true;
                    request.ContentType = "application/x-www-form-urlencoded";
                    request.ContentLength = loginBytes.Length;

                    Stream newStream = request.GetRequestStream();
                    newStream.Write(loginBytes, 0, loginBytes.Length);
                    newStream.Close();
                }

                try
                {
                    return request.GetResponse().GetResponseStream();
                }
                catch (WebException e)
                {
                    // if the authentification failed, try without it
                    if (authentification == true)
                        return WebDocPage(page, false);
                    else
                        throw e;
                }
            }
            else
                throw new UriFormatException();
        }


        public string GetContentById(string id)
        {
            HtmlNode node = currentDocument.GetElementbyId(id);
            if (node != null)
                return node.InnerHtml;
            else
                return string.Empty;
        }

        public string GetContentByClass(string id, int instanceNumber = 0, string tagType = "div")
        {
            string classRegEx = string.Format("//{1}[@class='{0}']", id, tagType);
            var nodes = currentDocument.DocumentNode.SelectNodes(classRegEx);
            var node = nodes.Count > instanceNumber ? nodes[instanceNumber] : null;

            if (node != null)
                return node.InnerHtml;
            else
                return string.Empty;
        }


        public List<string> GetCssFiles()
        {
            List<string> webDocCss = new List<string>();

            // select all css defined by link tags with relation = 'stylesheet'
            var nodes = currentDocument.DocumentNode.SelectNodes("//link[@rel='stylesheet']");
            foreach (var node in nodes)
            {
                string cssUrl =  node.GetAttributeValue("href", string.Empty);
                StreamReader cssDoc = new StreamReader(WebDocPage(cssUrl));
                webDocCss.Add(cssDoc.ReadToEnd());
            }

            numberCss = nodes.Count;

            return webDocCss;
        }

        public void InternalizeImages(string outDir, string linkToImgDir)
        {
            string imageDir = Path.Combine(Path.Combine(outDir, "html"), linkToImgDir);

            // select all img tags
            var nodes = currentDocument.DocumentNode.SelectNodes("//img");
            foreach(var node in nodes)
            {
                string imageUrl = node.GetAttributeValue("src", string.Empty);
                string imageName = Path.GetFileName(imageUrl);
                Uri absoluteImageUrl = new Uri(siteHome, imageUrl);

                string newImageName = string.Format("webDocImage_{0}", imageName);
                string newImageAboslutePath = Path.Combine(imageDir, newImageName);
                string newImageRelativePath = Path.Combine(linkToImgDir, newImageName);

                if (!File.Exists(newImageAboslutePath))
                {
                    try
                    {
                        DownloadFile(absoluteImageUrl, newImageAboslutePath);
                    }
                    catch (WebException)
                    {

                    }
                }

                node.SetAttributeValue("src", newImageRelativePath);
            }
        }

    }
}