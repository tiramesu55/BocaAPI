using BocaAPI.Interfaces;
using BocaAPI.Models.DTO;
using Microsoft.AspNetCore.Mvc;

namespace BocaAPI.Controllers
{
    [Route("api/hours")]
    [ApiController]
    public class HoursController : ControllerBase
    {
        private ILoggerService _test;
        private IBocaService _service;


        public HoursController(ILoggerService test, IBocaService bService)
        {
            _test = test;
            _service = bService;
        }

        [HttpGet("GetCodes")]  // this method we use to test connection
        public async Task<ActionResult> GetCodes()
        {
            _test.LogInfo(1, "Test message");

            var codes = await _service.Cache.GetPoliceCodes();   

            return Ok(codes);
        }
        /// <summary>
        /// this action returns OK if all records are loaded.  We can change to return the number of loaded records or the number of exceptions
        /// </summary>
        /// <returns></returns>
        [HttpGet("LoadFiles")]
        public async Task<ActionResult> LoadFiles()
        {
            var result = await _service.UploadInputFileToDatabase();
            return Ok();
        }
        /// <summary>
        /// this action returns OK if all records are loaded.  We can change to return the number of loaded records or the number of exceptions
        /// </summary>
        /// <returns></returns>
        [HttpGet("ExportFile")]
        public async Task<ActionResult> ExportFile()
        {

            var recs = await _service.ExportLatest();

            return Ok(recs);
        }
    }
}
