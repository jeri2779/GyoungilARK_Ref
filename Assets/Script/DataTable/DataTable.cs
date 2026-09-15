using UnityEngine;
using CsvHelper;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
public abstract class DataTable
{
    public abstract void Load(string filename);

    public static List<T> LoadCsv<T>(string csvText)
    {
        using (var reader = new StringReader(csvText))
        using (var csvReader = new CsvReader(reader, CultureInfo.InvariantCulture))
        {
            var records = csvReader.GetRecords<T>();
            return records.ToList();
        }
    }
}
