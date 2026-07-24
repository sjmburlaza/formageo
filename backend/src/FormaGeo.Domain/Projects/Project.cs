namespace FormaGeo.Domain.Projects;

public sealed class Project
{
    private Project()
    {
        // Required by Entity Framework Core.
    }

    private Project(Guid id, string name, DateTimeOffset createdAtUtc)
    {
        Id = id;
        Name = name;
        CreatedAtUtc = createdAtUtc;
    }

    public Guid Id { get; private set; }

    public string Name { get; private set; } = string.Empty;

    public DateTimeOffset CreatedAtUtc { get; private set; }

    public static Project Create(string name)
    {
        var normalizedName = name.Trim();

        if (string.IsNullOrWhiteSpace(normalizedName))
        {
            throw new ArgumentException(
                "Project name is required.",
                nameof(name));
        }

        if (normalizedName.Length > 200)
        {
            throw new ArgumentException(
                "Project name cannot exceed 200 characters.",
                nameof(name));
        }

        return new Project(
            Guid.NewGuid(),
            normalizedName,
            DateTimeOffset.UtcNow);
    }
}