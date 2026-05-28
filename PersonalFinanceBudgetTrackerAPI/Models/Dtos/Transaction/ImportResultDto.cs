namespace PersonalFinanceBudgetTrackerAPI.Models.Dtos.Transaction
{
    public class ImportResultDto
    {
        public int TotalAccounts { get; set; }
        public int TotalImported { get; set; }
        public int TotalSkipped { get; set; }
        public List<string> Errors { get; set; } = new();

    }
}
