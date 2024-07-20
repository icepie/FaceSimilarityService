using FaceSimilarityService.Services;
using SeetaFace6Sharp;
using System.Runtime.Intrinsics.X86;

var builder = WebApplication.CreateSlimBuilder(args);


builder.Services.AddControllers();

// 判断是否为Nvidia显卡 支持CUDA 使用系统环境变量
if (Environment.GetEnvironmentVariable("FACE_CUDA") == "1")
{

    //打印内部日志
    GlobalConfig.DefaultDeviceType = DeviceType.GPU;

}
else
{
    // 使用System.Runtime进行判断cpu是否支持axv2指令集 
    if (Avx2.IsSupported)
    {
        GlobalConfig.DefaultDeviceType = DeviceType.CPU;
    }
    else
    {
        /// 打印
        Console.WriteLine("CPU不支持AVX2指令集，使用SSE2指令集");
        GlobalConfig.X86Instruction = X86Instruction.SSE2;
    }
}

builder.Services.AddSingleton<FaceDetector>();
builder.Services.AddSingleton<FaceLandmarker>();
builder.Services.AddSingleton<FaceRecognizer>();

builder.Services.AddSingleton(new FeatureStorageService("./storage.json"));

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// builder.WebHost.ConfigureKestrel(options =>
// {
//     options.ListenAnyIP(5000); // HTTP 端口
//     options.ListenAnyIP(5001, listenOptions =>
//     {
//         listenOptions.UseHttps(); // HTTPS 端口
//     });
// });

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// app.UseHttpsRedirection();

app.UseRouting(); // 添加路由中间件

app.UseAuthorization();

app.MapControllers(); // 映射控制器路由

// var summaries = new[]
// {
//     "Freezing", "Bracing", "Chilly", "Cool", "Mild", "Warm", "Balmy", "Hot", "Sweltering", "Scorching"
// };

// app.MapGet("/weatherforecast", () =>
// {
//     var forecast = Enumerable.Range(1, 5).Select(index =>
//         new WeatherForecast
//         (
//             DateOnly.FromDateTime(DateTime.Now.AddDays(index)),
//             Random.Shared.Next(-20, 55),
//             summaries[Random.Shared.Next(summaries.Length)]
//         ))
//         .ToArray();
//     return forecast;
// })
// .WithName("GetWeatherForecast")

app.Run("http://0.0.0.0:5000");  // Set the application to listen on port 5000
