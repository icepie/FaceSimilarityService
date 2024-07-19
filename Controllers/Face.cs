using Microsoft.AspNetCore.Mvc;
using Face.Models;
using System;
using System.IO;
using System.Threading.Tasks;
using SkiaSharp;
using ViewFaceCore.Core;
using ViewFaceCore.Model;
using ViewFaceCore;

namespace Face.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class FaceController : ControllerBase
    {
        private readonly FaceRecognizer _faceRecognizer;
        private readonly FaceDetector _faceDetector;
        private readonly FaceLandmarker _faceLandmarker;

        public FaceController()
        {
            _faceRecognizer = new FaceRecognizer();
            _faceDetector = new FaceDetector();
            _faceLandmarker = new FaceLandmarker();
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

                    // 打印调整后的图片分辨率
                    Console.WriteLine($"Resized Image1 Resolution: {bitmap1.Width}x{bitmap1.Height}");
                    Console.WriteLine($"Resized Image2 Resolution: {bitmap2.Width}x{bitmap2.Height}");

                    var (similarity, recognitionResult) = await GetFaceSimilarityAsync(bitmap1, bitmap2);
                    return Ok(new { code = 0, data = new { face_distances = similarity, recognition_result = recognitionResult }, message = "" });
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

        // private SKBitmap ResizeBitmap(SKBitmap bitmap, int targetHeight)
        // {
        //     float aspectRatio = (float)bitmap.Width / bitmap.Height;
        //     int targetWidth = (int)(targetHeight * aspectRatio);

        //     SKBitmap resizedBitmap = new SKBitmap(targetWidth, targetHeight);
        //     using (var canvas = new SKCanvas(resizedBitmap))
        //     {
        //         canvas.DrawBitmap(bitmap, new SKRect(0, 0, targetWidth, targetHeight));
        //     }
        //     return resizedBitmap;
        // }
    }
}
