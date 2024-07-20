namespace FaceSimilarityService.Models
{
    public class RegisterFaceRequest
    {
        public required IFormFile File { get; set; }
        public required string UserKey { get; set; }
    }
}
