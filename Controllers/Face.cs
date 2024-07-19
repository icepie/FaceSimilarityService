using Microsoft.AspNetCore.Mvc;
using Face.Models;
using System;
using System.IO;
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
        public IActionResult CompareFaces([FromForm] CompareFacesRequest request)
        {
            try
            {
                if (request.File1 == null || request.File2 == null)
                {
                    return BadRequest(new { code = 1, message = "Invalid image data" });
                }

                SKBitmap bitmap1, bitmap2;

                using (var ms1 = new MemoryStream())
                {
                    request.File1.CopyTo(ms1);
                    var imageBytes1 = ms1.ToArray();
                    if (imageBytes1 == null || imageBytes1.Length == 0)
                    {
                        return BadRequest(new { code = 1, message = "Invalid image data for file1" });
                    }
                    bitmap1 = SKBitmap.Decode(imageBytes1);
                }

                using (var ms2 = new MemoryStream())
                {
                    request.File2.CopyTo(ms2);
                    var imageBytes2 = ms2.ToArray();
                    if (imageBytes2 == null || imageBytes2.Length == 0)
                    {
                        return BadRequest(new { code = 1, message = "Invalid image data for file2" });
                    }
                    bitmap2 = SKBitmap.Decode(imageBytes2);
                }

                if (bitmap1 == null || bitmap2 == null)
                {
                    return BadRequest(new { code = 1, message = "Failed to decode images" });
                }

                var (similarity, recognitionResult) = GetFaceSimilarity(bitmap1, bitmap2);
                return Ok(new { code = 0, data = new { face_distances = similarity, recognition_result = recognitionResult }, message = "" });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { code = 1, message = $"Internal server error: {ex.Message}" });
            }
        }

        private (double faceDistances, int recognitionResult) GetFaceSimilarity(SKBitmap bitmap1, SKBitmap bitmap2)
        {
            try
            {
                var faceInfos1 = _faceDetector.Detect(bitmap1);
                var faceInfos2 = _faceDetector.Detect(bitmap2);

                if (faceInfos1.Length == 0 || faceInfos2.Length == 0)
                {
                    throw new Exception("No faces detected in one or both images.");
                }

                var landmarks1 = _faceLandmarker.Mark(bitmap1, faceInfos1[0]);
                var landmarks2 = _faceLandmarker.Mark(bitmap2, faceInfos2[0]);

                var feature1 = _faceRecognizer.Extract(bitmap1, landmarks1);
                var feature2 = _faceRecognizer.Extract(bitmap2, landmarks2);

                var similarity = _faceRecognizer.Compare(feature1, feature2);

                // Example logic to determine recognition result based on similarity threshold
                double threshold = 0.7; // Adjusted threshold
                int recognitionResult = similarity >= threshold ? 1 : 0;

                // Log values for debugging
                Console.WriteLine($"Similarity: {similarity}");
                Console.WriteLine($"Recognition Result: {recognitionResult}");

                return (1 - similarity, recognitionResult);
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Error in GetFaceSimilarity: {ex.Message}");
                throw; // Re-throwing the exception to be handled in the controller
            }
        }
    }
}
