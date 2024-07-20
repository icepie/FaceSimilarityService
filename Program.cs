using FaceSimilarityService.Services;

var builder = WebApplication.CreateSlimBuilder(args);


builder.Services.AddControllers();
builder.Services.AddSingleton<ViewFaceCore.Core.FaceRecognizer>();
builder.Services.AddSingleton<ViewFaceCore.Core.FaceDetector>();
builder.Services.AddSingleton<ViewFaceCore.Core.FaceLandmarker>();

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
