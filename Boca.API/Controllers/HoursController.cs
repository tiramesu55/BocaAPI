using BocaAPI.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace BocaAPI.Controllers
{
    [Route("api/hours")]
    [ApiController]
    public class HoursController : ControllerBase
    {
        private ILoggerService _test;
        private ICacheService _cache;


        public HoursController(ILoggerService test, ICacheService cache)
        {
            _test = test;
            _cache = cache;
        }

        [HttpGet]
        public async Task<ActionResult> TestLog()
        {
            _test.LogInfo(1, "Test message");

            var ololo = await _cache.GetPoliceCodes();   

            return Ok();
        }

        //[HttpGet]
        //public ActionResult<SourceTime> GetTimes()
        //{
        //    var x = new SourceTime("xxx") { Description = "yyyyyy", Id = 3 };
        //    return Ok();
        //}
    }
}
