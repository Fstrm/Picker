using System.Web.Http;

namespace WebApi.Controllers
{
    public class ValuesController : ApiController
    {
        // GET api/values
        public string Get()
        {
            return "test";
        }

        // POST api/values
        public void Post([FromBody]object report)
        {

        }
    }
}
