using MedicalData.Api.Data;
using MedicalData.Api.Dto.Requests;
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

    [HttpGet("labtypes")]
    public async Task<ActionResult<IEnumerable<string>>> GetAllTypes()
    {
        return await context.LabResults.GroupBy(x => x.TestName).Select(x => x.Key).ToListAsync();
    }
    
    [HttpGet("labresults")]
    public async Task<ActionResult<IEnumerable<LabResult>>> GetAllResultsByTestName(string testName)
    {
        return await context.LabResults.Where(x => x.TestName == testName).OrderBy(x => x.TestDate).ToListAsync();
    }
    
    [HttpPost("by-date")]
    public async Task<ActionResult<LabResult>> UploadByDate([FromBody] UploadByDateRequest request)
    {
        foreach (var result in request.Results)
        {
            var dbResult = await context.LabResults.Where(x => x.TestName == result.Key && !string.IsNullOrWhiteSpace(x.Unit)).FirstOrDefaultAsync();
            LabResult LabResult = new LabResult();
            LabResult.TestName = result.Key;
            LabResult.Value = result.Value;
            LabResult.TestDate = request.Date;
            if (dbResult != null)
            {
                LabResult.ReferenceRange = dbResult.ReferenceRange;
                LabResult.Unit = dbResult.Unit;
            }

            context.LabResults.Add(LabResult);
        }
        await context.SaveChangesAsync();
        return Ok();
    }
    
    [HttpPost("update_text")]
    public async Task<ActionResult<LabResult>> UpdateText()
    {
        var labResults = await context.LabResults.ToListAsync();
        foreach (var result in labResults)
        {
            if (result.TestName.Contains("\n"))
            {
                result.TestName = result.TestName.Replace("\n", " ");
            }
        }
        await context.SaveChangesAsync();
        return Ok();
    }
}


// {
// "date": "2025-01-25T06:22:07.473Z",
// "results": {
//     "Глюкоза (Glucose, GLU)": 5.4,
//     "Холестерин (Total Blood Cholesterol, ХС, CHOL)": 4.2,
//     "Тригліцериди (Triglyceride, ТГ, TG)": 1.0,
//     "Холестерин ліпопротеїдів високої щільності (High-density lipoprotein\ncholesterol, Хс.ЛПВЩ, HDL)": 0.73,
//     "Холестерин ліпопротеїдів низької щільності (Low-density lipoprotein\ncholesterol, Хс.ЛПНЩ, LDL)": 3.02,
//     "Холестерин не-ліпопротеїдів високої щільності (Non-high-density lipoprotein\ncholesterol, Хс.не-ЛПВЩ, Non–HDL-C)": 3.5,
//     "Холестерин ліпопротеїдів дуже низької щільності (Very Low Density Lipoprotein,Хс.ЛПДНЩ, VLDL)": 0.45,
//     "Індекс атерогенності (ІА, АІР)": 4.8,
//     "Інсулін (Insulin)": 16.19
// }
// }