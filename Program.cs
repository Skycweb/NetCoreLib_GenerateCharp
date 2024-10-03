using System;
using System.IO;
using Microsoft.Extensions.Configuration;

namespace GenerateCharp
{
    class Program
    {
        static void Main(string[] _)
        {
            CreateFiles();
            //CreateFile("UJDb");
        }

        static void CreateFiles()
        {
            var builder = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true);

            IConfigurationRoot configuration = builder.Build();

            // 获取 "generate" 节点下的所有子节点
            var generateSection = configuration.GetSection("generate").GetChildren();

            // 遍历每个子节点
            foreach (var section in generateSection)
            {
                string name = section.Key; // 获取当前节点的名称
                string connectionString = section["db"];
                string path = section["path"];
                string className = section["className"];

                // 生成代码并保存
                GenerateCode t = new(connectionString, className);
                t.Save(path);

                Console.WriteLine($"Generated code for: {name}, Path: {path}");
            }
        }
    }
}

