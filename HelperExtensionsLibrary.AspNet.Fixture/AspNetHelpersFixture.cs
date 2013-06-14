using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Xunit;
using Should.Fluent;
using System.Web;
using FakeItEasy;
using HelperExtensionsLibrary.AspNet;
using System.Web.Security;


namespace HelperExtensionsLibrary.AspNet.Fixture
{
    public class AspNetHelpersFixture
    {
        HttpCookieCollection Cookies { get; set; }
        /// <summary>
        /// Asp.Net http context
        /// </summary>
        public HttpContextBase AspContextBase { get; private set; }
        /// <summary>
        /// Asp.Net http request
        /// </summary>
        public HttpRequestBase AspRequestBase { get; private set; }
        /// <summary>
        /// Aasp.Net http response
        /// </summary>
        public HttpResponseBase AspResponseBase { get; private set; }


        public AspNetHelpersFixture()
        {
            Cookies = new HttpCookieCollection();
            
            AspContextBase = A.Fake<HttpContextBase>();
            AspRequestBase = A.Fake<HttpRequestBase>();
            AspResponseBase = A.Fake<HttpResponseBase>();
            A.CallTo(() => AspContextBase.Request).Returns(AspRequestBase);
            A.CallTo(() => AspContextBase.Response).Returns(AspResponseBase);
            A.CallTo(() => AspRequestBase.Cookies).Returns(Cookies);
            A.CallTo(() => AspResponseBase.Cookies).Returns(Cookies);
        }
        [Fact]
        public void CRUDCookieFixture()
        {
            Cookies.Clear();
            var now = DateTime.Now;
            
            AspContextBase.AppendCookie("testcookie1", "testcookievalue1");
            Cookies["testcookie1"].Should().Not.Be.Null();
            Cookies["testcookie1"].Value.Should().Equal("testcookievalue1");
            Cookies["testcookie1"].Expires.Should().Be.Equals(DateTime.MinValue);
            AspContextBase.AppendCookie("testcookie2", "testcookievalue2", now + TimeSpan.FromDays(1));
            Cookies["testcookie2"].Expires.Should().Be.Equals(now+ TimeSpan.FromDays(1));

            AspContextBase.AppendCookie("testcookie3", "testcookievalue3", TimeSpan.FromHours(24));
            Cookies["testcookie3"].Expires.Should().Be.LessThan(now + TimeSpan.FromMinutes(24 * 60 + 1));
            Cookies["testcookie3"].Expires.Should().Be.GreaterThanOrEqualTo(now + TimeSpan.FromHours(24));

            AspContextBase.ReplaceCookie(new HttpCookie("testcookie1", "testcookievalue11") { Expires = now + TimeSpan.FromDays(1) });
            var cookie = GetLatestCookie("testcookie1");
            cookie.Should().Not.Be.Null();
            cookie.Value.Should().Equal("testcookievalue11");
            cookie.Expires.Should().Be.Equals(now + TimeSpan.FromDays(1));

            AspContextBase.RemoveCookie("testcookie2");
            cookie = GetLatestCookie("testcookie2");
            cookie.Expires.Should().Be.LessThan(DateTime.Now);
            
            AspRequestBase.CookieExists("testcookie4").Should().Be.False();
            AspRequestBase.GetCookie("testcookie4").Should().Be.Null();

        }

        private HttpCookie GetLatestCookie(string cookieName)
        {
           
            for (int i=Cookies.Count -1;i>=0 ; i--)
            {
                var cookie = Cookies[i];
                if (cookie.Name == cookieName)
                    return cookie;
            }

            return null;
        }



        [Fact]
        public void AuthCookieFixture()
        {
            Cookies.Clear();
            var mydata = Tuple.Create(1, 2, (DateTime?)null, TimeSpan.FromDays(1), "teststring");

            var now = DateTime.Now;

            AspContextBase.SetAuthCookie("ticket", false, mydata);
            string ticketName;
            bool isPersistant;
            DateTime issueDate;
            DateTime expirationDate;

            var checkData = AspContextBase.GetAuthCookie<Tuple<int, int, DateTime?, TimeSpan, string>>(
                out ticketName, 
                out isPersistant,
                out issueDate,
                out expirationDate);

            ticketName.Should().Equal("ticket");
            isPersistant.Should().Be.False();
            issueDate.Should().Be.GreaterThanOrEqualTo(now).Should().Be.LessThanOrEqualTo(DateTime.Now);
            expirationDate.Should().Equal(issueDate + FormsAuthentication.Timeout);
            checkData.Should().Equal(mydata);
            Cookies[FormsAuthentication.FormsCookieName].Should().Not.Be.Null();
            Cookies[FormsAuthentication.FormsCookieName].Expires.Should().Equal(DateTime.MinValue);

            Cookies.Clear();

            AspContextBase.SetAuthCookie("ticket", true, mydata);
            checkData = AspContextBase.GetAuthCookie<Tuple<int, int, DateTime?, TimeSpan, string>>(
                out ticketName,
                out isPersistant,
                out issueDate,
                out expirationDate);
            Cookies[FormsAuthentication.FormsCookieName].Expires.Should().Equal(issueDate + FormsAuthentication.Timeout);


            Cookies.Clear();

            AspContextBase.SetAuthCookie("ticket", true, mydata, cookieName: "EFSM", term: TimeSpan.FromMinutes(1), issueDate: DateTime.Today);
            checkData = AspContextBase.GetAuthCookie<Tuple<int, int, DateTime?, TimeSpan, string>>(
                out ticketName,
                out isPersistant,
                out issueDate,
                out expirationDate,
                cookieName: "EFSM",
                checkTiketExpiration: false);

            issueDate.Should().Equal(DateTime.Today);
            expirationDate.Should().Equal(issueDate + TimeSpan.FromMinutes(1));
            Cookies["EFSM"].Should().Not.Be.Null();

            checkData = AspContextBase.GetAuthCookie<Tuple<int, int, DateTime?, TimeSpan, string>>(
                out ticketName,
                out isPersistant,
                out issueDate,
                out expirationDate,
                cookieName: "EFSM",
                checkTiketExpiration: true);

            checkData.Should().Be.Null();
            
        }
    }
}
