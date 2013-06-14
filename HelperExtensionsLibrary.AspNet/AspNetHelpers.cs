using System;
using System.Web;
using System.Web.Security;
using HelperExtensionsLibrary.Strings;

namespace HelperExtensionsLibrary.AspNet
{
    public static class AspNetHelpers
    {
        /// <summary>
        /// Remove cookie
        /// </summary>
        /// <param name="context">http context</param>
        /// <param name="name">cookie name</param>
        public static void RemoveCookie(this HttpContextBase context, string name)
        {
            //if (context.Request.Cookies[name] != null)
            {
                HttpCookie myCookie = new HttpCookie(name) { Expires = DateTime.Now.AddDays(-1d), Value = null };
                context.AppendCookie(myCookie);
            }
        }
        /// <summary>
        /// Append cookie
        /// </summary>
        /// <param name="context">http context</param>
        /// <param name="cookie">cookie</param>
        public static void AppendCookie(this HttpContextBase context, HttpCookie cookie)
        {
            context.Response.Cookies.Add(cookie);
        }

        /// <summary>
        /// Append cookie
        /// </summary>
        /// <param name="context">http context</param>
        /// <param name="cookieName">cookie name</param>
        /// <param name="cookieValue">cookie value</param>
        /// <param name="cookieExpiration">cookie expiration. Default: non-persistant cookie</param>
        public static void AppendCookie(this HttpContextBase context, string cookieName, string cookieValue, DateTime cookieExpiration = default(DateTime))
        {
            HttpCookie myCookie = new HttpCookie(cookieName) { Expires = cookieExpiration, Value = cookieValue };
            context.AppendCookie(myCookie);
        }
        /// <summary>
        /// Append cookie
        /// </summary>
        /// <param name="context">http context</param>
        /// <param name="cookieName">cookie name</param>
        /// <param name="cookieValue">cookie value</param>
        /// <param name="cookieExpirationPeriod">cookie expiration period.</param>
        public static void AppendCookie(this HttpContextBase context, string cookieName, string cookieValue, TimeSpan cookieExpirationPeriod)
        {
            DateTime expiration = cookieExpirationPeriod != default(TimeSpan) ? DateTime.Now + cookieExpirationPeriod : default(DateTime);
            AppendCookie(context, cookieName, cookieValue, expiration);
        }
        /// <summary>
        /// Replace cookie
        /// </summary>
        /// <param name="context">http context</param>
        /// <param name="cookie">cookie</param>
        public static void ReplaceCookie(this HttpContextBase context, HttpCookie cookie)
        {
            context.AppendCookie(cookie);
        }
        /// <summary>
        /// Check whethercookie exists
        /// </summary>
        /// <param name="requestBase">http response</param>
        /// <param name="cookieName">cookie name</param>
        /// <returns>true: if cookie exists</returns>
        public static bool CookieExists(this HttpRequestBase requestBase, string cookieName)
        {
            return requestBase.Cookies[cookieName] != null;
        }

        /// <summary>
        /// Check whethercookie exists
        /// </summary>
        /// <param name="requestBase">http response</param>
        /// <param name="cookieName">cookie name</param>
        /// <returns>http cookie</returns>
        public static HttpCookie GetCookie(this HttpRequestBase requestBase, string cookieName)
        {
            if (!CookieExists(requestBase, cookieName))
                return null;

            return requestBase.Cookies[cookieName];
        }

        /// <summary>
        /// Set athentication cookie
        /// </summary>
        /// <typeparam name="T">User specific data type</typeparam>
        /// <param name="context">http context</param>
        /// <param name="tiketname">auth ticket name</param>
        /// <param name="rememberMe">true: persistant cookie : cookie.Expiration = ticket.Expiration</param>
        /// <param name="userData">User specific data</param>
        /// <param name="cookieName">auth cookie name.  FormsAuthentication.FormsCookieName by default</param>
        /// <param name="term">auth ticket expiration period. FormsAuthentication.Timeout by default (30min by default)</param>
        /// <param name="issueDate">date of auth ticket issue. Current DateTime by default</param>
        /// <returns>length of serialized ticket</returns>
        public static int SetAuthCookie<T>(this HttpContextBase context, string tiketname, bool rememberMe, T userData,
            string cookieName = null, TimeSpan term = default(TimeSpan), DateTime issueDate = default(DateTime))
        {
            issueDate = issueDate == default(DateTime) ? DateTime.Now : issueDate;
            term = term == default(TimeSpan) ? FormsAuthentication.Timeout : term;
            var newTicket = new FormsAuthenticationTicket(1, tiketname,
                issueDate,
                issueDate + term,
                rememberMe, Newtonsoft.Json.JsonConvert.SerializeObject(userData), FormsAuthentication.FormsCookiePath);

            cookieName = cookieName ?? FormsAuthentication.FormsCookieName;

            var encTicket = FormsAuthentication.Encrypt(newTicket);
            var identityCookie = new HttpCookie(cookieName, encTicket);

            if (newTicket.IsPersistent)
                identityCookie.Expires = newTicket.Expiration;

            context.ReplaceCookie(identityCookie);

            return encTicket.Length;

        }
        /// <summary>
        /// Get authenticated cookie
        /// </summary>
        /// <typeparam name="T">User specific data type</typeparam>
        /// <param name="context">http context</param>
        /// <param name="ticketName">auth ticket name</param>
        /// <param name="isPersistant">true: persistant cookie : cookie.Expiration = ticket.Expiration</param>
        /// <param name="issueDate">date of auth ticket issue.</param>
        /// <param name="expiration">auth ticket expiration date.</param>
        /// <param name="cookieName">auth cookie name.  FormsAuthentication.FormsCookieName by default</param>
        /// <param name="checkTiketExpiration">true: if cookie expired or has empty value, than removes the letter</param>
        /// <returns>User specific data</returns>
        public static T GetAuthCookie<T>(this HttpContextBase context,
            out string ticketName,
            out bool isPersistant,
            out DateTime issueDate,
            out DateTime expiration,
            string cookieName = null,
            bool checkTiketExpiration = true)
        {
            HttpRequestBase requestBase = context.Request;

            cookieName = cookieName ?? FormsAuthentication.FormsCookieName;
            HttpCookie authCookie = requestBase.Cookies[cookieName];
            isPersistant = false;
            issueDate = DateTime.MinValue;
            expiration = DateTime.MinValue;
            ticketName = string.Empty;

            if (authCookie == null)
                return default(T);

            if (authCookie.Value.IsEmpty() && authCookie.Expires == DateTime.MinValue)
            {
                context.RemoveCookie(cookieName);
                return default(T);
            }

            try
            {
                var authTicket = FormsAuthentication.Decrypt(authCookie.Value);

                if (checkTiketExpiration && authTicket.Expired)
                {
                    context.RemoveCookie(cookieName);
                    return default(T);
                }

                isPersistant = authTicket.IsPersistent;
                issueDate = authTicket.IssueDate;
                expiration = authTicket.Expiration;
                ticketName = authTicket.Name;
                return Newtonsoft.Json.JsonConvert.DeserializeObject<T>(authTicket.UserData);
            }
            catch
            {
                context.RemoveCookie(cookieName);
                return default(T);
            }

        }
        /// <summary>
        /// Get authenticated cookie
        /// </summary>
        /// <typeparam name="T">User specific data type</typeparam>
        /// <param name="context">http context</param>
        /// <param name="cookieName">auth cookie name.  FormsAuthentication.FormsCookieName by default</param>
        /// <returns>User specific data</returns>
        public static T GetAuthCookie<T>(this HttpContextBase context, string cookieName = null)
        {
            bool isPersistant;
            DateTime expiration;
            DateTime issueDate;
            string ticketName;

            return context.GetAuthCookie<T>(out ticketName, out isPersistant, out expiration, out issueDate, cookieName);
        }
    }
}
