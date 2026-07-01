namespace ai_speis_be.Repositories.CVRepo
{
    public class JDQueryParameters
    {
        public int PageNumber { get; set; } = 1;
        public int PageSize { get; set; } = 10;
        public string? Status { get; set; } // Map từ CVFileStatus
        public string SortBy { get; set; } = "UploadedAt";
        public bool IsAscending { get; set; } = false;
    }
}
