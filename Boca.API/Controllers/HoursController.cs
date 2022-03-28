﻿using BocaAPI.Interfaces;
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

        [HttpGet]
        public async Task<ActionResult> TestLog()
        {
            _test.LogInfo(1, "Test message");

            var ololo = await _service.Cache.GetPoliceCodes();   

            return Ok();
        }
        /// <summary>
        /// this action returns OK if all records are loaded.  We can change to return the number of loaded records or the number of exceptions
        /// </summary>
        /// <returns></returns>
        [HttpGet]
        public async Task<ActionResult> LoadCsv()
        {
            await _service.UploadInputFileToDatabase();
            return Ok();
        }
    }
}
