using CsvHelper;
using CsvHelper.Configuration;
using System.Globalization;
using System.Net;
using BocaAPI.Interfaces;
using BocaAPI.Models.DTO;
using BocaAPI.Validators;


namespace BocaAPI.Extensions
{
    public static class CsvExtensions
    {
        public static byte[] SaveToCSV<T>(this List<T> input, string delimiter = ",")
        {
            using var memoryStream = new MemoryStream();
            using var streamWriter = new StreamWriter(memoryStream);
            using var csvWriter = new CsvWriter(streamWriter, new CsvConfiguration(CultureInfo.InvariantCulture)
            {
                Delimiter = delimiter,
                HasHeaderRecord = true
            });

            csvWriter.WriteRecords(input);
            streamWriter.Flush();
            return memoryStream.ToArray();
        }
        
        public static List<CsvReadResult<T>> ReadFromCsv<T>(this Stream input)
        {
            using var stream = new StreamReader(input);
            var csvReader = new CsvReader(stream, CultureInfo.InvariantCulture);
            var records = new List<CsvReadResult<T>>();

            var count = 0;
            while(csvReader.Read())
            {
                try
                {
                    records.Add(new CsvReadResult<T>()
                    {
                        Record = csvReader.GetRecord<T>(),
                        IsValid = true
                    });
                }
                catch(Exception e)
                {
                    records.Add(new CsvReadResult<T>()
                    {
                        RowNumber = count,
                        IsValid = false,
                        Errors = e.Message
                    });
                }

                count++;
            }

            return records.Where(record => record.Record is not null).ToList();
        }
        
        public class CsvReadResult<T>
        {
            public int? RowNumber { get; set; }
            public T? Record { get; set; }
            public bool IsValid { get; set; }
            public string? Errors { get; set; }
        }
    }
}
