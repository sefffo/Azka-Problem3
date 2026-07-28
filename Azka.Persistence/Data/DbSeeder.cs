using Azka.Domain.Entities;
using Azka.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace Azka.Persistence.Data;

public static class DbSeeder
{
    public static async Task SeedAsync(AppDbContext context)
    {
        if (await context.Assets.AnyAsync()) return;

        var assets = new List<Asset>
        {
            new()
            {
                AssetNumber = "MTR-001",
                AssetType = AssetType.SmartElectricityMeter,
                Address = "15 El Tahrir St, Cairo",
                Latitude = 30.0444,
                Longitude = 31.2357,
                CustomerName = "Omar Hassan",
                Status = AssetStatus.Active,
                InstallationDate = new DateTime(2025, 1, 15)
            },
            new()
            {
                AssetNumber = "MTR-002",
                AssetType = AssetType.SmartWaterMeter,
                Address = "42 Nile Corniche, Giza",
                Latitude = 30.0131,
                Longitude = 31.2089,
                CustomerName = "Nadia Ali",
                Status = AssetStatus.Active,
                InstallationDate = new DateTime(2025, 3, 10)
            },
            new()
            {
                AssetNumber = "GTW-001",
                AssetType = AssetType.Gateway,
                Address = "7 El Maadi St, Cairo",
                Latitude = 29.9600,
                Longitude = 31.2500,
                CustomerName = "Ahmed Farouk",
                Status = AssetStatus.Active,
                InstallationDate = new DateTime(2024, 11, 20)
            }
        };

        context.Assets.AddRange(assets);

        var engineers = new List<Engineer>
        {
            new()
            {
                EmployeeNumber = "ENG-001",
                FullName = "Ahmed Hassan",
                Team = "Alpha",
                Region = "Cairo",
                Skills = "Electrical,Smart Meters",
                WorkingHours = "08:00-16:00",
                DailyCapacityHours = 8.0,
                IsActive = true
            },
            new()
            {
                EmployeeNumber = "ENG-002",
                FullName = "Sara Mahmoud",
                Team = "Beta",
                Region = "Giza",
                Skills = "Mechanical,Water Systems",
                WorkingHours = "09:00-17:00",
                DailyCapacityHours = 7.0,
                IsActive = true
            }
        };

        context.Engineers.AddRange(engineers);
        await context.SaveChangesAsync();
    }
}