# Development mock data

Starting the API in the `Development` environment applies pending database
migrations and idempotently creates **Metro Manila Site Feasibility Study
(Demo)**. The project contains five analysis-ready candidate sites with valid
WGS 84 polygon boundaries and saved preferences for every active contextual
layer.

Set `DatabaseInitialization:SeedMockData` to `false` in development
configuration to disable the sample project. Existing projects and changes to
the sample project's saved layer preferences are preserved on later starts.
