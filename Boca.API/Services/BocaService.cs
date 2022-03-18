using BocaAPI.Interfaces;
using BocaAPI.Models;
using BocaAPI.Models.DTO;
using Microsoft.Extensions.Caching.Memory;
using System.Globalization;

namespace BocaAPI.Services
{
    public class BocaService: IBocaService
    {
        private readonly ICacheService _cacheService;
        private readonly IBocaRepository _repository;

        public BocaService(ICacheService cacheService, IBocaRepository repository)
        {
            _cacheService = cacheService;
            _repository = repository;
        }

        public async Task UploadInputFileToDatabase()
        {
            string fileName = "";
            using var stream = new StreamReader(fileName);
            var records = new CsvHelper.CsvReader(stream, CultureInfo.InvariantCulture).GetRecords<VCSExport>().ToList();

            var policeCodes = await _cacheService.GetPoliceCodes();

            //_repository.UploadToDatabase(records.Where(it => policeCodes.Any(pc => pc.Infinium_Codes == it.WCPID)));


        }

    }
}
