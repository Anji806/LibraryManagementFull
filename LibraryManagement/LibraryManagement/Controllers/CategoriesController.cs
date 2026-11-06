using LibraryManagement.Models;
using LibraryManagement.Repositories.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LibraryManagement.Controllers;

[ApiController]
[Route("api/[controller]")]
public class CategoriesController : ControllerBase
{
    private readonly ICategoryRepository _categoryRepository;

    public CategoriesController(
        ICategoryRepository categoryRepository)
    {
        _categoryRepository = categoryRepository;
    }

    // GET: api/categories
    [HttpGet]
    [AllowAnonymous]
    public async Task<IActionResult> GetCategories()
    {
        var categories =
            await _categoryRepository.GetAllAsync();

        return Ok(categories);
    }

    // GET: api/categories/1
    [HttpGet("{id:int}")]
    [AllowAnonymous]
    public async Task<IActionResult> GetCategory(int id)
    {
        var category = 
            await _categoryRepository.GetByIdAsync(id);

        if (category == null)
        {
            return NotFound(new
            {
                message = "Category not found."
            });
        }

        return Ok(category);
    }

    // POST: api/categories
    [HttpPost]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> CreateCategory(
        Category category)
    {
        await _categoryRepository.AddAsync(category);

        await _categoryRepository.SaveChangesAsync();

        return CreatedAtAction(
            nameof(GetCategory),
            new { id = category.CategoryId },
            category
        );
    }

    // PUT: api/categories/1
    [HttpPut("{id:int}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> UpdateCategory(
        int id,
        Category category)
    {
        var existingCategory =
            await _categoryRepository.GetByIdAsync(id);

        if (existingCategory == null)
        {
            return NotFound(new
            {
                message = "Category not found."
            });
        }

        existingCategory.CategoryName =
            category.CategoryName;

        _categoryRepository.Update(existingCategory);

        await _categoryRepository.SaveChangesAsync();

        return Ok(existingCategory);
    }

    // DELETE: api/categories/1
    [HttpDelete("{id:int}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> DeleteCategory(int id)
    {
        var category =
            await _categoryRepository.GetByIdAsync(id);

        if (category == null)
        {
            return NotFound(new
            {
                message = "Category not found."
            });
        }

        _categoryRepository.Delete(category);

        await _categoryRepository.SaveChangesAsync();

        return NoContent();
    }
}