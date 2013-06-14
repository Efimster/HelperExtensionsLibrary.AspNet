using System;
using HelperExtensionsLibrary.AspNet;
using HelperExtensionsLibrary.AspNet.Fixture.TestModels;
using HelperExtensionsLibrary.AspNet.Testing;
using HelperExtensionsLibrary.Testing.Fixture.TestModels;
using Should.Fluent;
using Xunit;

namespace HelperExtensionsLibrary.Testing.Fixture
{
    public class AspNetSimulatorFixture
    {
        [Fact]
        public void LoopFixture()
        {
            var asp = new AspNetSimulator();

           
            asp.AspContextBase.AppendCookie("testcookie1", "testvalue1");
            asp.AspContextBase.AppendCookie("testcookie2", "testvalue2", TimeSpan.FromHours(1));
            DateTime now = DateTime.Now;

            asp.Loop(reOpenBrowser: false, now: now + TimeSpan.FromMinutes(59));

            asp.AspRequestBase.Cookies["testcookie1"].Should().Not.Be.Null();
            asp.AspRequestBase.Cookies["testcookie2"].Should().Not.Be.Null();

            asp.Loop(reOpenBrowser: true, now: now + TimeSpan.FromMinutes(59));

            asp.AspRequestBase.Cookies["testcookie1"].Should().Be.Null();
            asp.AspRequestBase.Cookies["testcookie2"].Should().Not.Be.Null();

            asp.AspContextBase.AppendCookie("testcookie1", "testvalue1");

            asp.Loop(reOpenBrowser: false, now: now + TimeSpan.FromMinutes(61));

            asp.AspRequestBase.Cookies["testcookie1"].Should().Not.Be.Null();
            asp.AspRequestBase.Cookies["testcookie2"].Should().Not.Be.Null();

            asp.Loop(reOpenBrowser: true, now: now + TimeSpan.FromMinutes(61));

            asp.AspRequestBase.Cookies["testcookie1"].Should().Be.Null();
            asp.AspRequestBase.Cookies["testcookie2"].Should().Be.Null();

        }

        [Fact]
        public void GetMvcController()
        {
            var asp = new AspNetSimulator();
            asp.DefineDependencies(new NinjectBindingMainModule());
            var controller = asp.GetMvcController<TestController>();

            controller.HttpContext.Should().Be.SameAs(asp.AspContextBase);
        }
    }
}
