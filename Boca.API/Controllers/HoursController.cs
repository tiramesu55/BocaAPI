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

        [HttpGet("GetCodes")]
        public async Task<ActionResult> GetCodes()
        {
            _test.LogInfo(1, "Test message");

            var ololo = await _service.Cache.GetPoliceCodes();   

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
            return Ok();
        }
        /// <summary>
        /// this action returns OK if all records are loaded.  We can change to return the number of loaded records or the number of exceptions
        /// </summary>
        /// <returns></returns>
        [HttpGet("ExportFile")]
        public async Task<ActionResult> ExportFile()
        {
            //var finalResults = inserted.Select(r => (FinalResult)r).ToList();
            //below is a separate call in a different controller
            // File.WriteAllBytes(Path.Combine(_settings.OutputFilePath, $"{fileName}_processed"), CsvExtensions.SaveToCSV(finalResults));

            // File.Move(file, $@"{ArchiveFolder}\{fileName}", true);  //move with overwrite
            List<FinalResult> x = new List<FinalResult>();
            var recs = await _service.Repository.GetForOutput();
            var finalResults = recs.OrderBy(p => p.id).Select(r => (FinalResult)r).ToList();
            //todo make a method in Boca Service
            // File.WriteAllBytes(Path.Combine(_settings.OutputFilePath, $"{fileName}_processed"), CsvExtensions.SaveToCSV(finalResults));
            return Ok();
        }
    }
}
