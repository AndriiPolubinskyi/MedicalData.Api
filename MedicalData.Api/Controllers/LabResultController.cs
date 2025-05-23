using MedicalData.Api.Data;
using MedicalData.Api.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace MedicalData.Api.Controllers;

[Route("api/[controller]")]
[ApiController]
public class LabResultController(AppDbContext context) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IEnumerable<LabResult>>> GetAll()
    {
        return await context.LabResults.ToListAsync();
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<LabResult>> GetItem(int id)
    {
        var labResult = await context.LabResults.FindAsync(id);
        if (labResult == null) return NotFound();
        return labResult;
    }

    [HttpPost]
    public async Task<ActionResult<LabResult>> Post(LabResult  LabResult)
    {
        context.LabResults.Add(LabResult);
        await context.SaveChangesAsync();
        return CreatedAtAction(nameof(GetItem), new { id = LabResult.Id }, LabResult);
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Put(int id, LabResult LabResult)
    {
        if (id != LabResult.Id) return BadRequest();

        context.Entry(LabResult).State = EntityState.Modified;
        await context.SaveChangesAsync();

        return NoContent();
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(int id)
    {
        var labResult = await context.LabResults.FindAsync(id);
        if (labResult == null) return NotFound();

        context.LabResults.Remove(labResult);
        await context.SaveChangesAsync();

        return NoContent();
    }

}