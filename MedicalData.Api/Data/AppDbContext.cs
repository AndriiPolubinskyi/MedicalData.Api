using MedicalData.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace MedicalData.Api.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<LabResult> LabResults { get; set; }
}