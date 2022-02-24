using Boca.API.Entities;
using BocaAPI.Repository;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace BocaAPI.Controllers
{
    [Route("api/hours")]
    [ApiController]
    public class HoursController : ControllerBase
    {
        private IHoursRepository hr;
        public HoursController(IHoursRepository r)
        {
            hr = r;
        }
        [HttpGet]
        public ActionResult<SourceTime> GetTimes()
        {
            var x = new SourceTime("xxx") { Description = "yyyyyy", Id=3 };
            return Ok();
        }
    }
}
