using CsvHelper;
using CsvHelper.Configuration;
using System.Globalization;

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


        public static List<T> ReadFromCsv<T>(this Stream input)
        {
            using var stream = new StreamReader(input);
            return new CsvReader(stream, CultureInfo.InvariantCulture).GetRecords<T>().ToList();
        }


    }
}
