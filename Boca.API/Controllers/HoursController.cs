using BocaAPI.Interfaces;
using BocaAPI.Models.DTO;
using Microsoft.AspNetCore.Mvc;

namespace BocaAPI.Controllers
{
    [Route("api/hours")]
    [ApiController]
    public class HoursController : ControllerBase
    {
        private IBocaService _service;


        public HoursController(IBocaService bService)
        {

            _service = bService;
        }

        [HttpGet("GetCodes")]  // this method we use to test connection
        public async Task<ActionResult> GetCodes()
        {

            var codes = await _service.Repository.GetPoliceCodes();   

            return Ok(codes);
        }
        [HttpGet("GetErrors")]  // this method we use to test connection
        public async Task<ActionResult> GetErrors()
        {
          
            var err = await _service.Repository.GetErrors();

            return Ok(err);
        }
        /// <summary>
        /// delete errors
        /// </summary>
        /// <returns></returns>
        [HttpDelete("DeleteErrors")]  // this method we use to test connection
        public async Task<ActionResult> DeleteErrors()
        {
             await _service.Repository.DeleteErrors();

            return Ok();
        }
        /// <summary>
        /// this action returns OK if all records are loaded.  We can change to return the number of loaded records or the number of exceptions
        /// </summary>
        /// <returns></returns>
        [HttpGet("LoadFiles")]
        public async Task<ActionResult> LoadFiles()
        {
            await _service.UploadInputFileToDatabase();
            //export now. The latest data are in the NewlyInsertedtable
           // await _service.ExportLatest();
            return Ok();
        }
        /// <summary>
        /// this action returns OK if all records are loaded.  We can change to return the number of loaded records or the number of exceptions
        /// </summary>
        /// <returns></returns>
        [HttpGet("ExportFile/{Name?}")]
        public async Task<ActionResult> ExportFile( string Name= "VCSTime")
        {

            var recs = await _service.ExportLatest(Name);

            return Ok(recs);
        }
    }
}
