using System.Security.Claims;
using LibraryManagement.Repositories.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LibraryManagement.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class MembersController : ControllerBase
{
    private readonly IBorrowTransactionRepository
        _borrowRepository;

    public MembersController(
        IBorrowTransactionRepository borrowRepository)
    {
        _borrowRepository = borrowRepository;
    }

    private bool IsOtherMember(int memberId) =>
        User.IsInRole("Member") &&
        User.FindFirstValue(ClaimTypes.NameIdentifier) !=
            memberId.ToString();

    // GET: api/members/1/borrowed-books
    [HttpGet("{memberId:int}/borrowed-books")]
    [Authorize(Roles = "Admin,Librarian,Member")]
    public async Task<IActionResult> GetBorrowedBooks(
        int memberId)
    {
        if (IsOtherMember(memberId))
        {
            return Forbid();
        }

        var transactions =
            await _borrowRepository
                .GetActiveBorrowingsByMemberAsync(memberId);

        return Ok(transactions);
    }

    // GET: api/members/1/borrow-history
    [HttpGet("{memberId:int}/borrow-history")]
    [Authorize(Roles = "Admin,Librarian,Member")]
    public async Task<IActionResult> GetBorrowHistory(
        int memberId)
    {
        if (IsOtherMember(memberId))
        {
            return Forbid();
        }

        var transactions =
            await _borrowRepository
                .GetBorrowHistoryByMemberAsync(memberId);

        return Ok(transactions);
    }
}