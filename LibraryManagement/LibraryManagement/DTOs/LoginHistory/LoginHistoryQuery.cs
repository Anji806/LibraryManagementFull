namespace LibraryManagement.DTOs.LoginHistory;

public class LoginHistoryQuery
{
    public string? Username { get; set; }
    public string? Status { get; set; }
    public DateTime? FromUtc { get; set; }
    public DateTime? ToUtc { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 25;
    public string SortDirection { get; set; } = "desc";
}

public class LoginHistoryPage
{
    public IReadOnlyList<Models.LoginHistory> Items { get; set; } = [];
    public int TotalCount { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
}
