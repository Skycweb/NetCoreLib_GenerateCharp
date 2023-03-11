# NetCoreLib_GenerateCharp
## 用于生成ORM代码

### 当前版本只支持MSSQL数据库


配置数据连接和生成文件保存路径<br />

打开'appsettings.json' <br />
```javascript
{
  "ConnectionStrings": {
    "DefaultConnection": "这里填写数据库连接"
  },
  "SavePath": "这里填写生成的文件保存路径"
}
```
### 例:
```javascript
{
  "ConnectionStrings": {
    "DefaultConnection": "Password=123456;Persist Security Info=True;User ID=sa;Initial Catalog=chatGPT;Data Source=.;TrustServerCertificate=true"
  },
  "SavePath": "/Project/adb/Database.cs"
}
```