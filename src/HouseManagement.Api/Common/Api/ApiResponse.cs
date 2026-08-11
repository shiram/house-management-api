namespace HouseManagement.Api.Common.Api
{
    public class ApiResponse<T>
    {
        public int StatusCode { get; set; }
        public string Message { get; set; } = string.Empty;
        public T? Data { get; set; }
        public string? RequestId { get; set; }
    }
}