
using System.Security.Claims;
using LibraryManagement.Repositories.Interfaces;
using LibraryManagement.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LibraryManagement.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class BorrowTransactionsController : ControllerBase
{
    private readonly IBorrowService _borrowService;
    private readonly IBorrowTransactionRepository _borrowRepository;

    public BorrowTransactionsController(
        IBorrowService borrowService,
        IBorrowTransactionRepository borrowRepository)
    {
        _borrowService = borrowService;
        _borrowRepository = borrowRepository;
    }

    private int? CurrentUserId =>
        int.TryParse(
            User.FindFirstValue(ClaimTypes.NameIdentifier),
            out var id) ? id : null;

    // POST: api/borrowtransactions/borrow
    [HttpPost("borrow")]
    [Authorize(Roles = "Admin,Librarian,Member")]
    public async Task<IActionResult> BorrowBook(
        int bookId,
        int memberId)
    {
        if (User.IsInRole("Member") && memberId != CurrentUserId)
        {
            return Forbid();
        }

        var transaction =
            await _borrowService.BorrowAsync(
                bookId,
                memberId);

        return Ok(new
        {
            message = "Book borrowed successfully.",
            transaction
        });
    }

    // PUT: api/borrowtransactions/return/1
    [HttpPut("return/{transactionId:int}")]
    [Authorize(Roles = "Admin,Librarian,Member")]
    public async Task<IActionResult> ReturnBook(
        int transactionId)
    {
        if (User.IsInRole("Member"))
        {
            var existing =
                await _borrowRepository.GetByIdAsync(transactionId);

            if (existing != null && existing.MemberId != CurrentUserId)
            {
                return Forbid();
            }
        }

        var transaction =
            await _borrowService.ReturnAsync(
                transactionId);

        return Ok(new
        {
            message = "Book returned successfully.",
            transaction
        });
    }
}