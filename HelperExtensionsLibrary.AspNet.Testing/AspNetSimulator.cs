using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.IO;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Security.Principal;
using System.Web;
using System.Web.Mvc;
using System.Web.Routing;
using System.Web.SessionState;
using System.Web.UI;
using FakeItEasy;
using HelperExtensionsLibrary.IEnumerable;
using HelperExtensionsLibrary.Reflection;
using Ninject;
using Ninject.Modules;

namespace HelperExtensionsLibrary.AspNet.Testing
{
    public class AspNetSimulator
    {
        /// <summary>
        /// Represents cookie on browser side
        /// </summary>
        public class BrowserCookie
        {
            /// <summary>
            /// Http cookie on browser side. Expiration == DateTime.MinValue
            /// </summary>
            public HttpCookie RequestCookie { get; private set; }
            /// <summary>
            /// Expiration of persistance
            /// </summary>
            public DateTime Expires { get; private set; }
            /// <summary>
            /// true: cookie saved on client side permanently
            /// </summary>
            public bool IsPersistant { get; private set; }

            public BrowserCookie(HttpCookie cookie)
            {
                RequestCookie = cookie;
                Expires = cookie.Expires;
                IsPersistant = cookie.Expires != DateTime.MinValue ? true : false;
                RequestCookie.Expires = DateTime.MinValue;
            }

        }
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
        public HttpApplication AspApplication { get; private set; }
        /// <summary>
        /// Browser cookies
        /// </summary>
        private IDictionary<string, BrowserCookie> BrowserAppCookies { get; set; }
        /// <summary>
        /// Function for creating new http session
        /// </summary>
        private Func<HttpSessionState> CreateNewSessionFunc;
        /// <summary>
        /// Asp.Net http session
        /// </summary>
        public HttpSessionStateBase AspSession { get { return AspContextBase.Session; } }
        /// <summary>
        /// Context of MVC controller
        /// </summary>
        public ControllerContext MvcControllerContext { get; private set; }
        //private HttpRequest AspRequest { get; set; }
        /// <summary>
        /// Asp.Net underlain http response
        /// </summary>
        private HttpResponse AspResponse { get; set; }
        /// <summary>
        /// Asp.Net underlain http context
        /// </summary>
        public HttpContext AspContext
        {
            get
            {
                UpdateUnderlaingContext();
                return AspApplication.Context;
            }
        }
        private bool requestChanged;
        private Action<HttpContext, HttpRequest> RequestSetter { get; set; }
        private bool responseChanged;
        private Action<HttpContext, HttpResponse> ResponseSetter { get; set; }
        private bool contextChanged;
        /// <summary>
        /// Ninject Injection kernal
        /// </summary>
        public IKernel Injection { get; private set; }
        private Stream ResponseStream { get; set; }
        private NameValueCollection ResponseHeaders { get; set; }


        public AspNetSimulator()
        {
            AspApplication = A.Fake<HttpApplication>();

            var request = new HttpRequest("request.txt", "http://localhost/", string.Empty);

            var ResponseStream = new MemoryStream();
            AspResponse = new HttpResponse(new HtmlTextWriter(new StreamWriter(ResponseStream)));

            CreateNewSessionFunc = GetCreateSessionFunc();
            SetContext(request, AspResponse); 
            RequestSetter = ReflectionExtensions.ConstructFieldOrPropertySetter<HttpContext, HttpRequest>("_request");
            ResponseSetter = ReflectionExtensions.ConstructFieldOrPropertySetter<HttpContext, HttpResponse>("_response");


            requestChanged = false;
            responseChanged = false;
            contextChanged = false;


            AspContextBase = A.Fake<HttpContextBase>(fake => fake.Wrapping(new HttpContextWrapper(AspContext)));
            AspResponseBase = A.Fake<HttpResponseBase>(fake => fake.Wrapping(new HttpResponseWrapper(AspResponse)));
            AspRequestBase = A.Fake<HttpRequestBase>(fake => fake.Wrapping(new HttpRequestWrapper(request)));

            //could be removed
            A.CallTo(() => AspContextBase.Request).Returns(AspRequestBase);
            A.CallTo(() => AspContextBase.Response).Returns(AspResponseBase);
            A.CallTo(() => AspContextBase.ApplicationInstance).Returns(null);
            A.CallTo(() => AspRequestBase.QueryString).ReturnsLazily(
                () =>
                {
                    return HttpUtility.ParseQueryString(AspRequestBase.Url.Query);
                });
            A.CallTo(() => AspResponseBase.OutputStream).ReturnsLazily(() => ResponseStream);
            A.CallTo(() => AspResponseBase.Headers).ReturnsLazily(() => ResponseHeaders);
            Action<string, string> act = (name, val) => { AspResponseBase.Headers[name] = val; };
            A.CallTo(() => AspResponseBase.AddHeader(A<string>.Ignored, A<string>.Ignored)).Invokes(act);
            A.CallTo(() => AspResponseBase.AppendHeader(A<string>.Ignored, A<string>.Ignored)).Invokes(act);
            MvcControllerContext = A.Fake<ControllerContext>(fake => fake.Wrapping(new ControllerContext(AspContextBase, new RouteData(), A.Fake<ControllerBase>())));


            BrowserCookieClear();
            BrowserAppCookies = null;

            ClearResponse();

        }
        /// <summary>
        /// Function to create new Session
        /// </summary>
        /// <returns></returns>
        private Func<HttpSessionState> GetCreateSessionFunc()
        {

            ConstructorInfo ci = typeof(HttpSessionState).
                GetConstructor(BindingFlags.Instance | BindingFlags.NonPublic, null, new[] { typeof(IHttpSessionState) }, new ParameterModifier[] { });

            var param = Expression.Parameter(typeof(IHttpSessionState), "TIHttpSessionState");
            var newSessionFunc = Expression.Lambda<Func<IHttpSessionState, HttpSessionState>>(Expression.New(ci, param), param).Compile();
            return () => newSessionFunc(new HttpSessionStateContainer(
                        Guid.NewGuid().ToString(),
                        new SessionStateItemCollection(),
                        new HttpStaticObjectsCollection(),
                        30,
                        true,
                        HttpCookieMode.UseCookies,
                        SessionStateMode.InProc,
                        false));
        }
        /// <summary>
        /// Creates underlain http context
        /// </summary>
        /// <param name="request">underlain http request</param>
        /// <param name="response">underlain http response</param>
        private void SetContext(HttpRequest request, HttpResponse response)
        {
            var context = new HttpContext(request, response);
            context.Items.Add("AspSession", CreateNewSessionFunc());

            var setter = ReflectionExtensions.ConstructFieldOrPropertySetter<HttpApplication, HttpContext>("_context");
            setter(AspApplication, context);
        }

        /// <summary>
        /// Emulate browser request
        /// </summary>
        /// <param name="reOpenBrowser">true: simulates new browser window</param>
        /// <param name="now">Date and Time of loop. Default: DateTime.Now</param>
        public void Loop(bool reOpenBrowser = false, DateTime now = default(DateTime))
        {
            if (BrowserAppCookies == null)
                BrowserAppCookies = new Dictionary<string, BrowserCookie>();

            if (reOpenBrowser)
            {

                AspContextBase.Session.Abandon();
                AspContextBase.Session.Clear();
            }

            if (reOpenBrowser)
                BrowserAppCookies.Where(cookie =>
                    !cookie.Value.IsPersistant || cookie.Value.Expires < now)
                    .Select(x => x.Key)
                    .ToArray()
                    .ForEach(x => BrowserAppCookies.Remove(x));

            if (!reOpenBrowser)
            {
                for (int i = 0; i < AspResponse.Cookies.Count; i++)
                {
                    var cookie = AspResponse.Cookies[i];
                    BrowserCookie existingCookie;
                    if (BrowserAppCookies.TryGetValue(cookie.Name, out existingCookie))
                    {
                        if (cookie.Expires < now && cookie.Expires != DateTime.MinValue)
                            BrowserAppCookies.Remove(cookie.Name);
                        else
                            BrowserAppCookies[cookie.Name] = new BrowserCookie(cookie);
                    }
                    else if (cookie.Expires > now || cookie.Expires == DateTime.MinValue)
                        BrowserAppCookies.Add(cookie.Name, new BrowserCookie(cookie));
                }
            }

            ClearResponse();
            AspContextBase.Request.Cookies.Clear();

            BrowserAppCookies.ForEach(cookie => AspContextBase.Request.Cookies.Add(cookie.Value.RequestCookie));
            responseChanged = true;
            requestChanged = true;
        }
        /// <summary>
        /// Clears browser cookies
        /// </summary>
        public void BrowserCookieClear()
        {
            AspContextBase.Request.Cookies.Clear();
            AspContextBase.Response.Cookies.Clear();
            if (BrowserAppCookies != null)
                BrowserAppCookies.Clear();

            responseChanged = true;
            requestChanged = true;
        }
        /// <summary>
        /// Simulates autentication. Set principal
        /// </summary>
        /// <param name="principal">user principal</param>
        public void AuthenticateBy(IPrincipal principal)
        {
            A.CallTo(() => AspContextBase.User).Returns(principal);
            contextChanged = true;
        }
        /// <summary>
        /// Set reuest url
        /// </summary>
        /// <param name="uri">url</param>
        public void SetRequestUri(Uri uri)
        {
            A.CallTo(() => AspRequestBase.Url).Returns(uri);
            A.CallTo(() => AspRequestBase.RawUrl).Returns(uri.ToString());
            requestChanged = true;
        }
        /// <summary>
        /// Set request cookies
        /// </summary>
        /// <param name="cookies"></param>
        public void SetRequestCookie(IDictionary<string, string> cookies)
        {
            cookies.ForEach(cookie => AspRequestBase.Cookies.Add(new HttpCookie(cookie.Key, cookie.Value)));
        }

        #region Update underlain context
        /// <summary>
        /// If request has been changed, populates underliain one
        /// </summary>
        private void UpdateUnderlainRequest()
        {
            if (!requestChanged)
                return;

            var request = new HttpRequest("request.txt", AspRequestBase.Url.AbsoluteUri, AspRequestBase.Url.Query);
            AspRequestBase = A.Fake<HttpRequestBase>(x => x.Wrapping(new HttpRequestWrapper(request)));
            A.CallTo(() => AspContextBase.Request).Returns(AspRequestBase);
            RequestSetter(AspContext, request);
            requestChanged = false;
        }
        /// <summary>
        /// If response has been changed, populates underliain one
        /// </summary>
        /// <param name="force">force update</param>
        private void UpdateUnderlainResponse(bool force = false)
        {
            if (!(force || responseChanged))
                return;

            var response = new HttpResponse(TextWriter.Null);
            AspResponseBase = A.Fake<HttpResponseBase>(x => x.Wrapping(new HttpResponseWrapper(response)));
            A.CallTo(() => AspContextBase.Response).Returns(AspResponseBase);
            ResponseSetter(AspContext, response);
            responseChanged = false;
        }
        /// <summary>
        /// If context has been changed, populates underliain one (request, response, identity,..)
        /// </summary>
        private void UpdateUnderlaingContext()
        {
            UpdateUnderlainRequest();
            UpdateUnderlainResponse();


            if (!contextChanged)
                return;

            AspContext.User = AspContextBase.User;
            contextChanged = false;
        }

        #endregion
        /// <summary>
        /// SetUp simulator dependencies
        /// </summary>
        /// <param name="module"></param>
        public void DefineDependencies(NinjectModule module)
        {
            Injection = new StandardKernel(module);
        }
        /// <summary>
        /// Creates MVC controller using injection
        /// </summary>
        /// <typeparam name="TController">Controller type</typeparam>
        /// <returns>MVC controller</returns>
        public TController GetMvcController<TController>() where TController : Controller
        {
            if (Injection == null)
                throw new Exception("Dependencies should be defined. Call DefineDependencies method.");

            var controller = Injection.Get<TController>();
            controller.ControllerContext = MvcControllerContext;
            return controller;
        }
        /// <summary>
        /// Clears Asp.Net http response
        /// </summary>
        public void ClearResponse()
        {
            if (AspResponseBase.Headers != null)
                AspResponseBase.Headers.Clear();
            if (AspResponseBase.OutputStream != null)
                AspResponseBase.OutputStream.Dispose();
            if (AspResponseBase.Output != null)
                AspResponseBase.Output.Dispose();

            AspResponse.Cookies.Clear();

            ResponseStream = new MemoryStream();
            A.CallTo(() => AspResponseBase.OutputStream).ReturnsLazily(() => ResponseStream);
            ResponseHeaders = new NameValueCollection();
            AspResponse.Output = new HtmlTextWriter(new StreamWriter(ResponseStream));
        }


    }
}
