using System.Security.Claims;
using LibraryManagement.DTOs.Users;
using LibraryManagement.Models;
using LibraryManagement.Repositories.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LibraryManagement.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = "Admin")]
public class UsersController : ControllerBase
{
    private readonly IUserRepository _userRepository;

    public UsersController(IUserRepository userRepository)
    {
        _userRepository = userRepository;
    }

    private bool IsSelf(int id) =>
        User.FindFirstValue(ClaimTypes.NameIdentifier) == id.ToString();

    private static UserResponseDto ToDto(User user) => new()
    {
        UserId = user.UserId,
        FullName = user.FullName,
        Email = user.Email,
        Role = user.Role,
        IsActive = user.IsActive,
        Address = user.Address
    };

    // GET: api/users
    [HttpGet]
    public async Task<IActionResult> GetUsers()
    {
        var users = await _userRepository.GetAllAsync();

        return Ok(users.Select(ToDto));
    }

    // GET: api/users/1
    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetUser(int id)
    {
        var user = await _userRepository.GetByIdAsync(id);

        if (user == null)
        {
            return NotFound(new { message = "User not found." });
        }

        return Ok(ToDto(user));
    }

    // PUT: api/users/1
    [HttpPut("{id:int}")]
    public async Task<IActionResult> UpdateUser(
        int id,
        UpdateUserDto dto)
    {
        var user = await _userRepository.GetByIdAsync(id);

        if (user == null)
        {
            return NotFound(new { message = "User not found." });
        }

        if (IsSelf(id) && (dto.Role != "Admin" || !dto.IsActive))
        {
            return BadRequest(new
            {
                message = "You cannot demote or deactivate your own account."
            });
        }

        user.FullName = dto.FullName;
        user.Role = dto.Role;
        user.IsActive = dto.IsActive;
        user.Address = dto.Address;

        _userRepository.Update(user);
        await _userRepository.SaveChangesAsync();

        return Ok(ToDto(user));
    }

    // DELETE: api/users/1
[HttpDelete("{id:int}")]
public async Task<IActionResult> DeleteUser(int id)
{
    // 1. Prevent an Admin from deleting their own account.
    if (IsSelf(id))
    {
        return BadRequest(new
        {
            message = "You cannot delete your own account."
        });
    }

    // 2. Check whether the user exists.
    var user = await _userRepository.GetByIdAsync(id);

    if (user == null)
    {
        return NotFound(new
        {
            message = "User not found."
        });
    }

    // 3. Get the user's transaction counts.
    // These counts must come from the actual database records.
    var (borrowingCount, _) =
        await _userRepository.GetTransactionCountsAsync(id);

    // 4. Block removal while books are still borrowed.
    if (borrowingCount > 0)
    {
        return Conflict(new
        {
            message =
                $"Cannot remove '{user.FullName}' because the user has " +
                $"{borrowingCount} book(s) that have not been returned yet."
        });
    }

    // 5. Every book was returned: remove the user but keep the history.
    // The user's name/email are copied onto their transactions and the
    // transactions' MemberId is set to null (FK is ON DELETE SET NULL).
    try
    {
        await _userRepository.DeleteKeepingHistoryAsync(user);

        return NoContent();
    }
    catch (DbUpdateException)
    {
        return Conflict(new
        {
            message =
                "The user could not be deleted because related records " +
                "exist in the database. No transaction history should be deleted."
        });
    }
}

}
