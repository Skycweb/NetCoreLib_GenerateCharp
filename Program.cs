using System;
using System.IO;
using Microsoft.Extensions.Configuration;

namespace GenerateCharp
{
    class Program
    {
        static void Main(string[] args)
        {
            CreateFile("Database");
            CreateFile("UJDb");
        }

        static void CreateFile(string name) {
            var builder = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true);

            IConfigurationRoot configuration = builder.Build();

            // 读取 appsettings.json 文件中的属性
            string connectionString = configuration[$"generate:{name}:db"];
            string path = configuration[$"generate:{name}:path"];
            string className = configuration[$"generate:{name}:className"];
            GenerateCode t = new GenerateCode(connectionString, className);
            t.Save(path);
        }
    }
}

