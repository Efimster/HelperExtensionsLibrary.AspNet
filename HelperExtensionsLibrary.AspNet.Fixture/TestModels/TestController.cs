using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Web.Mvc;

namespace HelperExtensionsLibrary.Testing.Fixture.TestModels
{
    public class TestController : Controller
    {
        public ActionResult Index()
        {
            return new EmptyResult();
        }
    }
}
