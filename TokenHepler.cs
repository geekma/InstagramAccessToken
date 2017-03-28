using System;
using System.Collections.Generic;
using System.Configuration;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.RegularExpressions;
using System.Web;
using System.Web.Mvc;
using Newtonsoft.Json;

public class TokenHelper
{

    #region HttpClient Helper

    private static readonly object LockObj = new object();
    private static HttpClient _client;

    private static string ClientId
    {
        get { return ConfigurationManager.AppSettings["client_id"]; }
    }

    private static string ClientSecret
    {
        get { return ConfigurationManager.AppSettings["client_secret"]; }
    }

    private static string RedirectUri
    {
        get { return ConfigurationManager.AppSettings["redirect_uri"]; }
    }

    private static string UseName
    {
        get { return ConfigurationManager.AppSettings["usename"]; }
    }

    private static string Password
    {
        get { return ConfigurationManager.AppSettings["password"]; }
    }

    public static HttpClient HttpClient
    {
        get
        {
            if (_client == null)
            {
                lock (LockObj)
                {
                    if (_client == null)
                    {
                        _client = new HttpClient();
                    }
                }
            }

            return _client;
        }
    }

    #endregion

    #region Get Instagram API DATA

    private readonly CookieContainer _cookieContainer = new CookieContainer();

    private string GetCSRFToken(string url)
    {
        const string csrfControl = "<input type=\"hidden\" name=\"csrfmiddlewaretoken\" value=\"(.*)\"/>";
        var req = (HttpWebRequest)WebRequest.Create(url);
        req.Method = "GET";
        req.CookieContainer = _cookieContainer;
        string src = string.Empty;
        using (var resp = (HttpWebResponse)req.GetResponse())
        {
            using (var data = resp.GetResponseStream())
            {
                if (data != null)
                    using (var sr = new StreamReader(data))
                    {
                        src = sr.ReadToEnd();
                    }
            }
        }
        var m = Regex.Match(src, csrfControl);
        return m.Success ? m.Groups[1].Value : string.Empty;
    }

    private void GetAccessToken()
    {
        var loginUrl = "https://www.instagram.com/accounts/login/?next=/oauth/authorize/";
        var loginUrlParams = string.Format("?client_id={0}&redirect_uri={1}&response_type=code", ClientId,
            RedirectUri);
        loginUrl = loginUrl + HttpUtility.UrlEncode(loginUrlParams) + "&force_classic_login=";

        const string useAgent =
            "Mozilla/4.0 (compatible; MSIE 8.0; Windows NT 6.1; Trident/4.0; QQWubi 133; SLCC2; .NET CLR 2.0.50727; .NET CLR 3.5.30729; .NET CLR 3.0.30729; Media Center PC 6.0; CIBA; InfoPath.2)";
        var postData = string.Format("csrfmiddlewaretoken={0}&username={1}&password={2}", GetCSRFToken(loginUrl),
            UseName, Password);
        var code = string.Empty;
        var req = (HttpWebRequest)WebRequest.Create(loginUrl);
        req.Method = "POST";
        req.UserAgent = useAgent;
        req.ContentType = "application/x-www-form-urlencoded";
        req.KeepAlive = true;
        req.Referer = loginUrl;
        req.AllowAutoRedirect = true;
        req.CookieContainer = _cookieContainer;
        byte[] byteArray = Encoding.ASCII.GetBytes(postData);
        req.ContentLength = byteArray.Length;
        using (var dataStream = req.GetRequestStream())
        {
            dataStream.Write(byteArray, 0, byteArray.Length);
            dataStream.Flush();
            dataStream.Close();
        }

        try
        {
            using (var webResp = (HttpWebResponse)req.GetResponse())
            {
                code = HttpUtility.ParseQueryString(webResp.ResponseUri.Query).Get("code");
            }
        }
        catch (WebException e)
        {
            code = HttpUtility.ParseQueryString(e.Response.ResponseUri.Query).Get("code");
        }
        catch (Exception)
        {
            // ignored
        }
        if (string.IsNullOrEmpty(code)) return;
        var paramList = new List<KeyValuePair<string, string>>
            {
                new KeyValuePair<string, string>("client_id", ClientId),
                new KeyValuePair<string, string>("client_secret", ClientSecret),
                new KeyValuePair<string, string>("redirect_uri", RedirectUri),
                new KeyValuePair<string, string>("grant_type", "authorization_code"),
                new KeyValuePair<string, string>("code", code)
            };

        try
        {
            var response =
                HttpClient.PostAsync(new Uri("https://api.instagram.com/oauth/access_token"),
                    new FormUrlEncodedContent(paramList)).Result;
            var tokenResult = response.Content.ReadAsStringAsync().Result;
            var jsonTokenResult = JsonConvert.DeserializeObject<dynamic>(tokenResult);
            if (jsonTokenResult == null) return;
            Session.Add("access_token", jsonTokenResult.access_token);
            Session.Add("userId", jsonTokenResult.user.id);
        }
        catch (Exception)
        {
            // ignored
        }
    }

    private string GetMediaByUserId()
    {
        var instagramResult = string.Empty;
        if (Session["userId"] == null || Session["access_token"] == null)
        {
            GetAccessToken();
        }
        try
        {
            instagramResult =
                HttpClient.GetStringAsync(
                    string.Format("https://api.instagram.com/v1/users/{0}/media/recent/?access_token={1}",
                        Session["userId"], Session["access_token"])).Result;

        }
        catch (Exception)
        {
            // ignored
        }
        return instagramResult;
    }

    #endregion

}
