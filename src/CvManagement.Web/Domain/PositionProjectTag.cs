namespace CvManagement.Web.Domain;

/// <summary>Tags marking which of a candidate's projects are relevant for a given position's CV.</summary>
public class PositionProjectTag
{
    public int PositionId { get; set; }
    public Position Position { get; set; } = null!;

    public int TagId { get; set; }
    public Tag Tag { get; set; } = null!;
}
