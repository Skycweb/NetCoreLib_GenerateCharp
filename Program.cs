using System;
using System.IO;
using Microsoft.Extensions.Configuration;

namespace GenerateCharp
{
    class Program
    {
        static void Main(string[] args)
        {
            var builder = new ConfigurationBuilder()
                            .SetBasePath(Directory.GetCurrentDirectory())
                            .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true);

            IConfigurationRoot configuration = builder.Build();

            // 读取 appsettings.json 文件中的属性
            string connectionString = configuration.GetConnectionString("DefaultConnection");
            string path = configuration[@"SavePath"];
            GenerateCode t = new GenerateCode(connectionString);
            t.Save(path);
        }
    }
}

