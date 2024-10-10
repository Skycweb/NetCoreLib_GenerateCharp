using System;
using Microsoft.Data.SqlClient;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;

namespace GenerateCharp
{
	public class GenerateCode
	{
		public GenerateCode(string connectString,string className)
		{
            this._connStr = connectString;
            StringBuilder.Append(@$"
using System;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
namespace adb
{{
    /// <summary>
    /// 
    /// </summary>
    public class {className} : DbContext
    {{
        /// <summary>
        /// 
        /// </summary>
        public {className}(DbContextOptions<{className}> options) : base(options){{}}

{this.CreateDbContext()}
        /// <summary>
        /// 
        /// </summary>
        protected override void OnModelCreating(ModelBuilder modelBuilder){{
            base.OnModelCreating(modelBuilder);
{this.CreateDbContext_Configuration()}
        }}
    }}
{this.CreateTableClass()}
{this.CreateTableConfiguration()}
}}"
);

		}
        public void Save(string path) {
            var file = File.Create(path);
            var data = Encoding.UTF8.GetBytes(this.StringBuilder.ToString());
            file.Write(data,0,data.Length);
            file.Dispose();
        }
        private readonly string _connStr = "";
        private readonly Dictionary<string, TableFieldClass> tableAllFileds = new();
        private readonly StringBuilder StringBuilder = new();
        public StringBuilder CreateTableClass()
        {
            var cls = GetClass();
            StringBuilder text = new();
            foreach (var item in cls)
            {
                var fileds = GetFields(item.Key);
                text.AppendLine(@"    /// <summary>");
                text.AppendLine($@"    /// {item.Value}");
                text.AppendLine(@"    /// </summary>");
                var clsName = $"    public class {item.Key}";
                if (fileds.Any(x => x.FieldName == "enable") && ChangeToCSharpType(fileds.First(x=>x.FieldName == "enable").FieldType, fileds.First(x => x.FieldName == "enable").isNull).Contains("Int32"))
                    clsName += $" : tTable ";
                if (fileds.Any(x => x.FieldName == "addTime") && fileds.First(x => x.FieldName == "addTime").isNull == false)
                    clsName += $" {(!clsName.Contains(':', StringComparison.CurrentCulture) ? ":" : ",")} IAddTime ";
                clsName += " {";
                text.AppendLine(clsName);

                foreach (var field in fileds)
                {
                    if (item.Key.StartsWith("t"))
                    {
                        tableAllFileds[field.FieldName] = field;
                    }
                    text.AppendLine(@"        /// <summary>");
                    if (item.Key.StartsWith("v"))
                    {
                        if (tableAllFileds.ContainsKey(field.FieldName))
                        {
                            text.AppendLine($@"       /// {tableAllFileds[field.FieldName].Describe}");
                        }
                        else
                        {
                            text.AppendLine($@"       /// 未知");
                        }
                    }
                    else
                    {
                        text.AppendLine($@"       /// {field.Describe}");
                    }
                    text.AppendLine(@"        /// </summary>");
                    if (field.FieldName == "enable" && ChangeToCSharpType(field.FieldType,field.isNull).Contains("Int32"))
                        text.AppendLine($@"       public override {ChangeToCSharpType(field.FieldType, field.isNull)} {field.FieldName} {{get;set;}} = {getDefaultValue(field.FieldType, field.DefaultValue, field.isNull)};");
                    else
                    {
                        text.AppendLine($@"       public {ChangeToCSharpType(field.FieldType, field.isNull)} {field.FieldName} {{get;set;}} = {getDefaultValue(field.FieldType, field.DefaultValue, field.isNull)};");
                    }
                }
                text.AppendLine($"    }}");
            }
            return text;
        }

        public StringBuilder CreateDbContext()
        {
            StringBuilder text = new();
            var cls = GetClass();
            foreach (var item in cls)
            {
                text.AppendLine(@"        /// <summary>");
                text.AppendLine($@"       /// {item.Value}");
                text.AppendLine(@"        /// </summary>");
                text.AppendLine($"        public DbSet<{item.Key}> {item.Key} {{ get; set; }}");
            }
            return text;
        }

        public StringBuilder CreateTableConfiguration()
        {
            StringBuilder text = new();
            var cls = GetClass();
            foreach (var item in cls)
            {
                text.AppendLine(@"    /// <summary>");
                text.AppendLine($@"    /// 配置文件,{item.Value}");
                text.AppendLine(@"    /// </summary>");
                text.AppendLine($"    public class {item.Key}Configuration : IEntityTypeConfiguration<{item.Key}>");
                text.AppendLine($"    {{");
                text.AppendLine(@"          /// <summary>");
                text.AppendLine($@"         /// 配置,{item.Value}");
                text.AppendLine(@"          /// </summary>");
                text.AppendLine($@"        public void Configure(EntityTypeBuilder<{item.Key}> builder)");
                text.AppendLine($@"        {{");


                var fileds = GetFields(item.Key);

                if (item.Key.StartsWith("v"))
                {
                    text.AppendLine($@"            builder.HasNoKey();");
                    text.AppendLine($@"            builder.ToView(""{item.Key}"");");
                }
                else
                {
                    text.AppendLine($@"            builder.ToTable(""{item.Key}"", ""dbo"");");
                    if (fileds.Where(x => x.isKey == true).Any())
                        text.AppendLine($@"            builder.HasKey(x => new {{{string.Join(",", fileds.Where(x => x.isKey == true).Select(x => $"x.{x.FieldName}"))}}});");

                }


                foreach (var field in fileds)
                {
                    text.AppendLine($@"            builder.Property(x => x.{field.FieldName}).HasColumnName(""{field.FieldName}"").HasColumnType(""{field.FieldType}""){(field.isNull ? ".IsRequired(false)" : ".IsRequired(true)")}{(field.isKey && field.AoutKey? ".ValueGeneratedOnAdd().UseIdentityColumn()" : "")}{(!string.IsNullOrWhiteSpace(field.DefaultValue) ? $".HasDefaultValue({getDefaultValue(field.FieldType, field.DefaultValue, field.isNull)})" : "")};");
                }
                text.AppendLine($@"        }}");
                text.AppendLine($"    }}");
            }
            return text;
        }


        public StringBuilder CreateDbContext_Configuration()
        {
            StringBuilder text = new();
            var cls = GetClass();
            foreach (var item in cls)
            {
                text.AppendLine($"            modelBuilder.ApplyConfiguration(new {item.Key}Configuration());");
            }
            return text;
        }
        private Dictionary<string, string> classList = null;
        public Dictionary<string, string> GetClass()
        {
            if (classList != null) return classList;
            //var list = new List<string>();
            classList  = new Dictionary<string, string>();
            using (var conn = new SqlConnection(this._connStr))
            {
                conn.Open();
                var cmd = new SqlCommand(@"select obj.id,obj.name,ext.[value] as [description] from [sysobjects]  obj
                                            left join sys.extended_properties as ext on ext.major_id=obj.id and ext.minor_id=0
                                            where (obj.[type] = 'u' OR obj.[type]='v')
                                            order by obj.[name] ",conn);
                // 移除获取视图
                
                var rs = cmd.ExecuteReader(System.Data.CommandBehavior.CloseConnection);
                while (rs.Read())
                {
                    //list.Add(rs.GetString(1));
                    if (!classList.ContainsKey(rs.GetString(1)))
                    {
                        if (rs.GetString(1).StartsWith("v"))
                        {
                            classList.Add(rs.GetString(1), "视图类");
                        }
                        else
                        {
                            var files = this.GetFields(rs.GetString(1));
                            if (files.Any(x => x.isKey == true))
                            {
                                classList.Add(rs.GetString(1), rs[2].ToString());
                            }
                        }
                    }

                }
                rs.Close();
            }
            
            return classList;
        }

        public List<string> GetPClass()
        {
            var list = new List<string>();
            using (var conn = new SqlConnection(_connStr))
            {
                conn.Open();
                var cmd = new SqlCommand(@"SELECT name FROM sys.all_objects
WHERE ([type] = 'P' OR [type] = 'X' OR [type] = 'PC') AND [is_ms_shipped] = 0 ORDER BY [name]", conn);
                
                
                var rs = cmd.ExecuteReader(System.Data.CommandBehavior.CloseConnection);
                while (rs.Read())
                {
                    list.Add(rs.GetString(0));
                }
                rs.Close();
            }
            return list;
        }

        public List<TableFieldClass> GetFields(string tableName)
        {
            var list = new List<TableFieldClass>();
            using (var conn = new SqlConnection(_connStr))
            {
                var cmd = new SqlCommand(string.Format(@"SELECT  
                                                                 C.name as [字段名],T.name as [字段类型]  
                                                                 ,convert(bit,C.IsNullable)  as [可否为空]  
                                                                 ,convert(bit,case when exists(SELECT 1 FROM sysobjects where xtype='PK' and parent_obj=c.id and name in (  
                                                                     SELECT name FROM sysindexes WHERE indid in(  
                                                                         SELECT indid FROM sysindexkeys WHERE id = c.id AND colid=c.colid))) then 1 else 0 end)   
                                                                             as [是否主键]  
                                                                 ,convert(bit,COLUMNPROPERTY(c.id,c.name,'IsIdentity')) as [自动增长]  
                                                                 ,C.Length as [占用字节]   
                                                                 ,COLUMNPROPERTY(C.id,C.name,'PRECISION') as [长度]  
                                                                 ,isnull(COLUMNPROPERTY(c.id,c.name,'Scale'),0) as [小数位数]  
                                                                 ,ISNULL(CM.text,'') as [默认值]  
                                                                 ,isnull(ETP.value,'') AS [字段描述]  
                                                                 --,ROW_NUMBER() OVER (ORDER BY C.name) AS [Row]  
                                                            FROM syscolumns C  
                                                            INNER JOIN systypes T ON C.xusertype = T.xusertype   
                                                            left JOIN sys.extended_properties ETP   ON  ETP.major_id = c.id AND ETP.minor_id = C.colid AND ETP.name ='MS_Description'   
                                                            left join syscomments CM on C.cdefault=CM.id  
                                                            WHERE C.id = object_id('{0}') 
                                                            Order By [是否主键] DESC,T.name DESC", tableName))
                {
                    Connection = conn
                };
                conn.Open();
                var rs = cmd.ExecuteReader(System.Data.CommandBehavior.CloseConnection);
                while (rs.Read())
                {
                    var newm = new TableFieldClass
                    {
                        AoutKey = (bool)rs["自动增长"]
                     ,
                        DefaultValue = rs["默认值"].ToString()
                     ,
                        Describe = rs["字段描述"].ToString()
                      ,
                        Digits = rs["小数位数"].ToString()
                      ,
                        FieldName = rs["字段名"].ToString()
                      ,
                        FieldType = rs["字段类型"].ToString()
                      ,
                        isKey = (bool)rs["是否主键"]
                      ,
                        isNull = (bool)rs["可否为空"]
                      ,
                        Size = (int)rs["长度"]
                    };
                    //if (newm.isKey && (newm.FieldType.ToLower() == "bigint" || newm.FieldType.ToLower() == "int"))
                    //    newm.isNull = true;
                    list.Add(newm);
                }
                rs.Close();
            }
            return list;
        }

        public List<ProcedureClass> GetPFields(string tableName)
        {
            var list = new List<ProcedureClass>();
            using (var conn = new SqlConnection(_connStr))
            {
                var cmd = new SqlCommand(string.Format(@"select '参数名称' = name, 
                                                                 '类型' = type_name(xusertype), 
                                                                 '长度' = length,    
                                                                 '参数顺序' = colid, 
                                                                 '排序方式' = collation,
                                                                 '参数传输类型' = isoutparam 
                                                           from    syscolumns 
                                                           where   id=object_id('{0}')", tableName))
                {
                    Connection = conn
                };
                conn.Open();
                var rs = cmd.ExecuteReader(System.Data.CommandBehavior.CloseConnection);
                while (rs.Read())
                {
                    list.Add(new ProcedureClass
                    {
                        InputOrOutputS = rs["参数传输类型"].ToString()
                                                     ,
                        FieldName = rs["参数名称"].ToString()[1..]
                                                     ,
                        FieldType = rs["类型"].ToString(),
                        Size = Convert.ToInt32(rs["长度"])

                    });
                }
                rs.Close();
            }
            return list;
        }

        public static string ChangeToCSharpType(string type, bool t)
        {
            string isNull = t ? "?" : "";
            string reval = type.ToLower() switch
            {
                "int" => "Int32" + isNull,
                "text" => "string",
                "bigint" => "Int64" + isNull,
                "binary" => "byte[]",
                "bit" => "bool" + isNull,
                "char" => "string",
                "datetime" => "DateTime" + isNull,
                "date" => "DateTime" + isNull,
                "time" => "TimeSpan" + isNull,
                "decimal" => "decimal" + isNull,
                "float" => "double" + isNull,
                "image" => "byte[]",
                "money" => "decimal" + isNull,
                "nchar" => "string",
                "ntext" => "string",
                "numeric" => "decimal" + isNull,
                "nvarchar" => "string",
                "real" => "Single" + isNull,
                "smalldatetime" => "DateTime" + isNull,
                "smallint" => "Int16" + isNull,
                "smallmoney" => "decimal" + isNull,
                "timestamp" => "DateTime" + isNull,
                "tinyint" => "byte",
                "uniqueidentifier" => "Guid" + isNull,
                "varbinary" => "byte[]",
                "varchar" => "string",
                "Variant" => "Object",
                _ => "string",
            };
            return reval;
        }

        public static string _repalce(string s)
        {
            return s.Replace("(", "").Replace(")", "");
            //switch (s.Substring(0, 2))
            //{
            //    case "(":
            //        return s.Substring(2, s.Length - 4);
            //    case "('":
            //        return s.Substring(2, s.Length - 4);
            //    default:
            //        return s.Substring(1, s.Length - 2);
            //}
        }

        public static string getDefaultValue(string type, string s, bool isNull)
        {

            string reval;
            switch (type.ToLower())
            {
                case "int":
                    if (string.IsNullOrWhiteSpace(s))
                    {
                        reval = isNull ? "null" : "0";
                    }
                    else
                    {
                        try
                        {
                            int a = Convert.ToInt32(_repalce(s));
                            s = a.ToString(System.Globalization.CultureInfo.InvariantCulture);
                        }
                        catch
                        {
                            s = "0";
                        }
                        reval = s;
                    }
                    break;
                case "text":
                    reval = (string.IsNullOrWhiteSpace(s) ? "string.Empty" : "\"" + _repalce(s) + "\"");
                    break;
                case "bigint":
                    if (string.IsNullOrWhiteSpace(s))
                    {
                        reval = isNull ? "null" : "0";
                    }
                    else
                    {
                        try
                        {
                            var a = Convert.ToInt64(_repalce(s));
                        }
                        catch
                        {
                            s = "0";
                        }
                        reval = s;
                    }
                    break;
                case "binary":
                    reval = "null";
                    break;
                case "bit":
                    if (string.IsNullOrWhiteSpace(s))
                    {
                        reval = isNull ? "null" : "false";
                    }
                    else
                    {
                        var bstr = _repalce(s);
                        bstr = bstr switch
                        {
                            "0" => "false",
                            "1" => "true",
                            _ => s,
                        };
                        reval = "Convert.ToBoolean(\"" + bstr + "\")";
                    }
                    break;
                case "char":
                    reval = string.IsNullOrWhiteSpace(s) ? "string.Empty" : "\"" + _repalce(s) + "\"";
                    break;
                case "date":
                case "datetime":
                    if (string.IsNullOrWhiteSpace(s))
                    {
                        reval = isNull ? "null" : "DateTime.Now";
                    }
                    else
                    {
                        s = _repalce(s);
                        reval = (s == "getdate" ? "DateTime.Now" : "DateTime.Parse(\"" + s + "\")");
                    }
                    break;
                case "time":
                    if (string.IsNullOrWhiteSpace(s))
                    {
                        reval = isNull ? "null" : "new TimeSpan(DateTime.Now.Ticks)";
                    }
                    else
                    {
                        s = _repalce(s);
                        reval = (s == "getdate" ? "new TimeSpan(DateTime.Now.Ticks)" : "new TimeSpan(\"" + s + "\")");
                    }
                    break;
                case "decimal":
                    if (string.IsNullOrWhiteSpace(s))
                    {
                        reval = isNull ? "null" : "(decimal)0";
                    }
                    else
                    {
                        try
                        {
                            var a = Convert.ToDecimal(_repalce(s));
                            s = "(decimal)" + a.ToString(System.Globalization.CultureInfo.InvariantCulture);
                        }
                        catch
                        {
                            s = "(decimal)0";
                        }
                        reval = s;
                    }
                    break;
                case "float":
                    if (string.IsNullOrWhiteSpace(s))
                    {
                        reval = isNull ? "null" : "0";
                    }
                    else
                    {
                        try
                        {
                            var a = Convert.ToDouble(_repalce(s));
                            s = a.ToString(System.Globalization.CultureInfo.InvariantCulture);
                        }
                        catch
                        {
                            s = "0";
                        }
                        reval = s;
                    }
                    break;
                case "image":
                    reval = "null";
                    break;
                case "money":
                    if (string.IsNullOrWhiteSpace(s))
                    {
                        reval = isNull ? "null" : "0";
                    }
                    else
                    {
                        try
                        {
                            double a = Convert.ToDouble(_repalce(s));
                            s = a.ToString(System.Globalization.CultureInfo.InvariantCulture);
                        }
                        catch
                        {
                            s = "0";
                        }
                        reval = s;
                    }
                    break;
                case "nchar":
                    reval = string.IsNullOrWhiteSpace(s) ? "string.Empty" : "\"" + _repalce(s) + "\"";
                    break;
                case "ntext":
                    reval = string.IsNullOrWhiteSpace(s) ? "string.Empty" : "\"" + _repalce(s) + "\"";
                    break;
                case "numeric":
                    if (string.IsNullOrWhiteSpace(s))
                    {
                        reval = isNull ? "null" : "0";
                    }
                    else
                    {
                        try
                        {
                            var a = Convert.ToDouble(_repalce(s));
                            s = a.ToString(System.Globalization.CultureInfo.InvariantCulture);
                        }
                        catch
                        {
                            s = "0";
                        }
                        reval = s;
                    }
                    break;
                case "nvarchar":
                    reval = string.IsNullOrWhiteSpace(s) ? "string.Empty" : "\"" + _repalce(s) + "\"";
                    break;
                case "real":
                    if (string.IsNullOrWhiteSpace(s))
                    {
                        reval = isNull ? "null" : "0";
                    }
                    else
                    {
                        try
                        {
                            var a = Convert.ToDouble(_repalce(s));
                            s = a.ToString(System.Globalization.CultureInfo.InvariantCulture);
                        }
                        catch
                        {
                            s = "0";
                        }
                        reval = s;
                    }
                    break;
                case "smalldatetime":
                    if (string.IsNullOrWhiteSpace(s))
                    {
                        reval = isNull ? "null" : "DateTime.Now";
                    }
                    else
                    {

                        s = _repalce(s);
                        reval = (s == "getdate" ? "DateTime.Now" : "DateTime.Parse(\"" + s + "\")");
                    }
                    break;
                case "smallint":
                    if (string.IsNullOrWhiteSpace(s))
                    {
                        reval = isNull ? "null" : "0";
                    }
                    else
                    {
                        try
                        {
                            var a = Convert.ToDouble(_repalce(s));
                            s = a.ToString(System.Globalization.CultureInfo.InvariantCulture);
                        }
                        catch
                        {
                            s = "0";
                        }
                        reval = s;
                    }
                    break;
                case "smallmoney":
                    if (string.IsNullOrWhiteSpace(s))
                    {
                        reval = isNull ? "null" : "0";
                    }
                    else
                    {
                        try
                        {
                            double a = Convert.ToDouble(_repalce(s));
                            s = a.ToString(System.Globalization.CultureInfo.InvariantCulture);
                        }
                        catch
                        {
                            s = "0";
                        }
                        reval = s;
                    }
                    break;
                case "timestamp":
                    if (string.IsNullOrWhiteSpace(s))
                    {
                        reval = isNull ? "null" : "DateTime.Now";
                    }
                    else
                    {
                        s = _repalce(s);
                        reval = (s == "getdate" ? "DateTime.Now" : "DateTime.Parse(\"" + s + "\")");
                    }
                    break;
                case "tinyint":
                    if (string.IsNullOrWhiteSpace(s))
                    {
                        reval = isNull ? "null" : "0";
                    }
                    else
                    {
                        try
                        {
                            double a = Convert.ToDouble(s);
                        }
                        catch
                        {
                            s = "0";
                        }
                        reval = s;
                    }
                    break;
                case "uniqueidentifier":
                    if (string.IsNullOrWhiteSpace(s))
                    {
                        reval = isNull ? "null" : "new Guid()";
                    }
                    else
                    {
                        reval = (s == "(newid())" ? "Guid.NewGuid()" : "new Guid(\"" + s + "\")");

                    }
                    break;
                case "varbinary":
                    reval = "null";
                    break;
                case "varchar":
                    reval = string.IsNullOrWhiteSpace(s) ? "string.Empty" : "\"" + s + "\"";
                    break;
                case "variant":
                    reval = "null";
                    break;
                default:
                    reval = string.IsNullOrWhiteSpace(s) ? "string.Empty" : "\"" + s + "\"";
                    break;
            }
            return reval;
        }

        public static string ChangeToCSharpType(string type)
        {
            string reval = type.ToLower() switch
            {
                "int" => "Int32",
                "text" => "String",
                "bigint" => "Int64",
                "binary" => "System.Byte[]",
                "bit" => "Boolean",
                "char" => "String",
                "datetime" => "System.DateTime",
                "time" => "System.TimeSpan",
                "decimal" => "System.Decimal",
                "float" => "System.Double",
                "image" => "System.Byte[]",
                "money" => "System.Decimal",
                "nchar" => "String",
                "ntext" => "String",
                "numeric" => "System.Decimal",
                "nvarchar" => "String",
                "real" => "System.Single",
                "smalldatetime" => "System.DateTime",
                "smallint" => "Int16",
                "smallmoney" => "System.Decimal",
                "timestamp" => "System.DateTime",
                "tinyint" => "System.Byte",
                "uniqueidentifier" => "System.Guid",
                "varbinary" => "System.Byte[]",
                "varchar" => "String",
                "Variant" => "Object",
                _ => "String",
            };
            return reval;
        }
    }

    public class TableFieldClass
    {
        /// <summary>
        /// 字段名称
        /// </summary>
        public string FieldName { get; set; }
        /// <summary>
        /// 字段类型
        /// </summary>
        public string FieldType { get; set; }
        /// <summary>
        /// 是否可以为空
        /// </summary>
        public bool isNull { get; set; }
        /// <summary>
        /// 是否为主键
        /// </summary>
        public bool isKey { get; set; }
        /// <summary>
        /// 是否为自增列
        /// </summary>
        public bool AoutKey { get; set; }
        /// <summary>
        /// 字段的大小
        /// </summary>
        public int Size { get; set; }
        /// <summary>
        /// 字段小数位数
        /// </summary>
        public string Digits { get; set; }
        /// <summary>
        /// 默认值
        /// </summary>
        public string DefaultValue { get; set; }
        /// <summary>
        /// 字段描述
        /// </summary>
        public string Describe { get; set; }
    }

    public class ProcedureClass
    {
        /// <summary>
        /// 字段名称
        /// </summary>
        public string FieldName { get; set; }

        /// <summary>
        /// 字段类型
        /// </summary>
        public string FieldType { get; set; }

        /// <summary>
        /// 字段的大小
        /// </summary>
        public int Size { get; set; }

        /// <summary>
        /// 字段小数位数
        /// </summary>
        public string Digits { get; set; }

        /// <summary>
        /// 传输类型
        /// </summary>
        public string InputOrOutputS { get; set; }
    }


}


