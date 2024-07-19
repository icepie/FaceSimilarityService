using Microsoft.AspNetCore.Http;

namespace Face.Models
{
    public class CompareFacesRequest
    {
        public required IFormFile File1 { get; set; }
        public required IFormFile File2 { get; set; }
    }
}