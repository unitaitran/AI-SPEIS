namespace ai_speis_be.Services.CodingService.Harness
{
    public interface ICodingHarnessEngine
    {
        /// <summary>
        /// Tự động bọc source code của ứng viên bằng Test Harness Driver tương ứng với ngôn ngữ.
        /// </summary>
        string WrapCode(string sourceCode, int languageId, string functionName);
    }
}
