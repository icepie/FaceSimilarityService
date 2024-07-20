using Microsoft.AspNetCore.Mvc;
using System.Net;
using SkiaSharp;
using ViewFaceCore.Core;
using ViewFaceCore.Model;
using FaceSimilarityService.Services;
using ViewFaceCore;
using System.Diagnostics;
using FaceSimilarityService.Models;

namespace FaceSimilarityService.Controllers
{
    [ApiController]
    [Route("face")]
    public class FaceController : ControllerBase
    {
        private readonly FaceRecognizer _faceRecognizer;
        private readonly FaceDetector _faceDetector;
        private readonly FaceLandmarker _faceLandmarker;
        private readonly FeatureStorageService _featureStorageService;

        public FaceController(FaceRecognizer faceRecognizer, FaceDetector faceDetector, FaceLandmarker faceLandmarker, FeatureStorageService featureStorageService)
        {
            _faceRecognizer = faceRecognizer;
            _faceDetector = faceDetector;
            _faceLandmarker = faceLandmarker;
            _featureStorageService = featureStorageService;
        }

        private string GetIpAddress()
        {
            IPAddress? ipAddress = HttpContext?.Connection?.RemoteIpAddress;
            return ipAddress == null ? "127.0.0.1" : ipAddress.ToString();
        }

        [HttpPost("register")]
        public IActionResult RegisterFeature([FromForm] RegisterFaceRequest request)
        {
            try
            {
                if (request.File == null)
                {
                    return BadRequest(new { code = 1010, message = "请求参数错误" });
                }

                var bitmap = DecodeAndResizeImage(request.File, out var width, out var height);
                Console.WriteLine($"Image resolution: {width}x{height}");

                var faceInfo = _faceDetector.Detect(bitmap);
                if (faceInfo.Length == 0)
                {
                    return BadRequest(new { code = 1030, message = "未识别到人脸" });
                }

                if (faceInfo.Length > 1)
                {
                    return BadRequest(new { code = 1030, message = "检测到多个人脸" });
                }

                var landmarks = _faceLandmarker.Mark(bitmap, faceInfo[0]);
                var feature = _faceRecognizer.Extract(bitmap, landmarks);

                var ipAddress = GetIpAddress();
                var existingFeature = _featureStorageService.GetFeature(ipAddress, request.UserKey);

                if (existingFeature != null)
                {
                    return BadRequest(new { code = 1030, message = "已注册" });
                }

                // Concurrent feature comparison
                var allFeatures = _featureStorageService.GetAllFeatures(ipAddress);
                bool isDuplicate = false;

                Parallel.ForEach(allFeatures, (registeredFeature, state) =>
                {
                    var similarity = _faceRecognizer.Compare(registeredFeature.Value, feature);
                    if (similarity >= 0.7)
                    {
                        isDuplicate = true;
                        state.Break();
                    }
                });

                if (isDuplicate)
                {
                    return BadRequest(new { code = 1030, message = "已注册" });
                }

                _featureStorageService.RegisterFeature(ipAddress, request.UserKey, feature);

                return Ok(new { code = 0, message = "注册成功" });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { code = 1, message = $"内部服务器错误: {ex.Message}" });
            }
        }

        [HttpPost("unregister")]
        public IActionResult UnregisterFeature([FromForm] UnVerifyRequest request)
        {
            try
            {
                var ipAddress = GetIpAddress();
                _featureStorageService.UnregisterFeature(ipAddress, request.UserKey);
                return Ok(new { code = 0, message = "注销成功" });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { code = 1, message = $"内部服务器错误: {ex.Message}" });
            }
        }

        [HttpPost("verify")]
        public IActionResult VerifyFeature([FromForm] VerifyRequest request)
        {
            try
            {
                if (request.File == null)
                {
                    return BadRequest(new { code = 1010, message = "请求参数错误" });
                }

                var bitmap = DecodeAndResizeImage(request.File, out var width, out var height);
                Console.WriteLine($"Image resolution: {width}x{height}");

                var faceInfo = _faceDetector.Detect(bitmap);

                if (faceInfo.Length == 0)
                {
                    return BadRequest(new { code = 1030, message = "未识别到人脸" });
                }
                if (faceInfo.Length > 1)
                {
                    return BadRequest(new { code = 1030, message = "检测到多个人脸" });
                }

                var landmarks = _faceLandmarker.Mark(bitmap, faceInfo[0]);
                var feature = _faceRecognizer.Extract(bitmap, landmarks);

                using FaceAntiSpoofing faceAntiSpoofing = new();

                Stopwatch sw = Stopwatch.StartNew();
                sw.Start();

                var faceAntiResult = faceAntiSpoofing.AntiSpoofing(bitmap, faceInfo[0], landmarks);

                sw.Stop();

                Console.WriteLine($"活体检测，结果：{faceAntiResult.Status}，清晰度:{faceAntiResult.Clarity}，真实度：{faceAntiResult.Reality}，耗时：{sw.ElapsedMilliseconds}ms");


                // Error（错误或没有找到指定的人脸索引处的人脸）、Real（真实人脸）、Spoof（攻击人脸（假人脸））、Fuzzy（无法判断（人脸成像质量不好））、Detecting（正在检测）

                if (faceAntiResult.Status != AntiSpoofingStatus.Real)
                {
                    switch (faceAntiResult.Status)
                    {
                        case AntiSpoofingStatus.Error:
                            return BadRequest(new { code = 1170, message = "活体检测失败" });
                        case AntiSpoofingStatus.Spoof:
                            return BadRequest(new { code = 1171, message = "攻击人脸 (人脸疑视伪造)" });
                        case AntiSpoofingStatus.Fuzzy:
                            return BadRequest(new { code = 1172, message = "无法判断（人脸成像质量不好）" });
                        case AntiSpoofingStatus.Detecting:
                            return BadRequest(new { code = 1173, message = "系统繁忙" });
                    }
                }


                var ipAddress = GetIpAddress();
                var allFeatures = _featureStorageService.GetAllFeatures(ipAddress);

                var matchingUserKey = string.Empty;
                bool isMatched = false;

                Stopwatch sw2 = Stopwatch.StartNew();
                sw2.Start();

                Parallel.ForEach(allFeatures, (registeredFeature, state) =>
                {
                    // 打印
                    Console.WriteLine($"正在比对：{registeredFeature.Key}");

                    var similarity = _faceRecognizer.Compare(registeredFeature.Value, feature);
                    if (similarity >= 0.7)
                    {
                        matchingUserKey = registeredFeature.Key;
                        isMatched = true;
                        state.Break();
                    }
                });

                sw2.Stop();

                Console.WriteLine($"人脸识别，结果：{isMatched}，耗时：{sw2.ElapsedMilliseconds}ms");

                if (isMatched)
                {
                    return Ok(new { code = 0, data = new { userKey = matchingUserKey }, message = "验证成功" });
                }

                return Ok(new { code = 1030, message = "未找到匹配项" });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { code = 1, message = $"内部服务器错误: {ex.Message}" });
            }
        }

        [HttpGet("list")]
        public IActionResult ListRegisteredFeatures()
        {
            try
            {
                var ipAddress = GetIpAddress();
                var allFeatureKeys = _featureStorageService.GetAllFeatureKeys(ipAddress);
                return Ok(new { code = 0, data = allFeatureKeys, message = "获取成功" });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { code = 1, message = $"内部服务器错误: {ex.Message}" });
            }
        }

        private static SKBitmap DecodeAndResizeImage(IFormFile file, out int width, out int height)
        {
            using var ms = new MemoryStream();
            file.CopyTo(ms);
            var bitmap = SKBitmap.Decode(ms.ToArray());
            width = bitmap.Width;
            height = bitmap.Height;
            return bitmap;
        }

        [HttpPost("compare")]
        public async Task<IActionResult> CompareFaces([FromForm] CompareFacesRequest request)
        {
            try
            {
                if (request.File1 == null || request.File2 == null)
                {
                    return BadRequest(new { code = 1010, message = "请求参数错误" });
                }

                using (var ms1 = new MemoryStream())
                using (var ms2 = new MemoryStream())
                {
                    await request.File1.CopyToAsync(ms1);
                    await request.File2.CopyToAsync(ms2);

                    var imageBytes1 = ms1.ToArray();
                    var imageBytes2 = ms2.ToArray();

                    if (imageBytes1 == null || imageBytes1.Length == 0 || imageBytes2 == null || imageBytes2.Length == 0)
                    {
                        return BadRequest(new { code = 1020, message = "存在格式不正确的文件" });
                    }

                    var bitmap1 = SKBitmap.Decode(imageBytes1);
                    var bitmap2 = SKBitmap.Decode(imageBytes2);

                    if (bitmap1 == null || bitmap2 == null)
                    {
                        return BadRequest(new { code = 1020, message = "文件解码失败" });
                    }

                    var (similarity, recognitionResult) = await GetFaceSimilarityAsync(bitmap1, bitmap2);
                    return Ok(new { code = 0, data = new { recognition_result = recognitionResult }, message = "" });
                }
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { code = 1, message = $"内部服务器错误: {ex.Message}" });
            }
        }

        private async Task<(double faceDistances, int recognitionResult)> GetFaceSimilarityAsync(SKBitmap bitmap1, SKBitmap bitmap2)
        {
            try
            {
                var faceInfos1 = await Task.Run(() => _faceDetector.Detect(bitmap1));
                var faceInfos2 = await Task.Run(() => _faceDetector.Detect(bitmap2));

                if (faceInfos1.Length == 0 || faceInfos2.Length == 0)
                {
                    throw new Exception("存在未识别到人脸的图像");
                }

                if (faceInfos1.Length > 1 || faceInfos2.Length > 1)
                {
                    throw new Exception("存在检测到多个人脸的图像");
                }

                var landmarks1 = await Task.Run(() => _faceLandmarker.Mark(bitmap1, faceInfos1[0]));
                var landmarks2 = await Task.Run(() => _faceLandmarker.Mark(bitmap2, faceInfos2[0]));

                var feature1 = await Task.Run(() => _faceRecognizer.Extract(bitmap1, landmarks1));
                var feature2 = await Task.Run(() => _faceRecognizer.Extract(bitmap2, landmarks2));

                var similarity = await Task.Run(() => _faceRecognizer.Compare(feature1, feature2));

                double threshold = 0.7; // 调整阈值
                int recognitionResult = similarity >= threshold ? 1 : 0;

                // 日志记录相似度和识别结果
                Console.WriteLine($"Similarity: {similarity}");
                Console.WriteLine($"Recognition Result: {recognitionResult}");

                return (1 - similarity, recognitionResult);
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Error in GetFaceSimilarity: {ex.Message}");
                throw; // 再次抛出异常，以便在控制器中处理
            }
        }
    }
}
