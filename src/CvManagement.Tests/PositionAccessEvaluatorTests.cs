using CvManagement.Web.Data;
using CvManagement.Web.Models;
using CvManagement.Web.Models.Enums;
using CvManagement.Web.Services.Implementations;
using Microsoft.EntityFrameworkCore;

namespace CvManagement.Tests;

/// <summary>Bulk eligibility (the path behind CV Browse, search and draft reminders) against EF InMemory.</summary>
public class PositionAccessEvaluatorTests
{
    private const string Alice = "alice";
    private const string Bob = "bob";

    private static async Task<(ApplicationDbContext Db, int PublicId, int IeltsId, int NoRulesId)> SeedAsync()
    {
        var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

        var category = new AttributeCategory { Name = "Certification" };
        var ielts = new AttributeDefinition { Name = "IELTS Score", DataType = AttributeDataType.Numeric, Category = category };

        var publicPosition = new Position { Title = "Open", AccessMode = PositionAccessMode.Public };
        var ieltsPosition = new Position
        {
            Title = "Needs IELTS > 7",
            AccessMode = PositionAccessMode.Restricted,
            AccessRules = [new PositionAccessRule { Attribute = ielts, Operator = ComparisonOperator.GreaterThan, ComparisonValueNumeric = 7m }]
        };
        var noRulesPosition = new Position { Title = "Restricted, no rules", AccessMode = PositionAccessMode.Restricted };

        db.Users.AddRange(new ApplicationUser { Id = Alice, UserName = Alice }, new ApplicationUser { Id = Bob, UserName = Bob });
        db.Positions.AddRange(publicPosition, ieltsPosition, noRulesPosition);
        db.UserAttributeValues.AddRange(
            new UserAttributeValue { UserId = Alice, Attribute = ielts, ValueNumeric = 7.5m },
            new UserAttributeValue { UserId = Bob, Attribute = ielts, ValueNumeric = 6.0m });
        await db.SaveChangesAsync();

        return (db, publicPosition.Id, ieltsPosition.Id, noRulesPosition.Id);
    }

    [Fact]
    public async Task Public_position_is_eligible_for_everyone()
    {
        var (db, publicId, _, _) = await SeedAsync();
        var eligible = await new PositionAccessEvaluator(db).FilterEligibleAsync([(Alice, publicId), (Bob, publicId)]);
        Assert.Equal(2, eligible.Count);
    }

    [Fact]
    public async Task Restricted_rule_is_evaluated_per_candidate_in_one_bulk_call()
    {
        var (db, _, ieltsId, _) = await SeedAsync();
        var eligible = await new PositionAccessEvaluator(db).FilterEligibleAsync([(Alice, ieltsId), (Bob, ieltsId)]);

        Assert.Contains((Alice, ieltsId), eligible);
        Assert.DoesNotContain((Bob, ieltsId), eligible);
    }

    [Fact]
    public async Task Restricted_position_without_rules_is_never_eligible()
    {
        var (db, _, _, noRulesId) = await SeedAsync();
        var eligible = await new PositionAccessEvaluator(db).FilterEligibleAsync([(Alice, noRulesId)]);
        Assert.Empty(eligible);
    }

    [Fact]
    public async Task Missing_position_is_not_eligible_and_does_not_throw()
    {
        var (db, _, _, _) = await SeedAsync();
        var result = await new PositionAccessEvaluator(db).IsEligibleForManyAsync(Alice, [424242]);
        Assert.False(result[424242]);
    }
}
