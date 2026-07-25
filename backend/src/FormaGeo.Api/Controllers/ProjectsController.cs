using FormaGeo.Application.Projects;
using FormaGeo.Application.Projects.CreateProject;
using FormaGeo.Application.Projects.GetProject;
using FormaGeo.Application.Projects.GetProjects;
using Microsoft.AspNetCore.Mvc;

namespace FormaGeo.Api.Controllers;

[ApiController]
[Route("api/projects")]
public sealed class ProjectsController : ControllerBase
{
    private readonly CreateProjectHandler _createProjectHandler;
    private readonly GetProjectHandler _getProjectHandler;
    private readonly GetProjectsHandler _getProjectsHandler;

    public ProjectsController(
        CreateProjectHandler createProjectHandler,
        GetProjectHandler getProjectHandler,
        GetProjectsHandler getProjectsHandler)
    {
        _createProjectHandler = createProjectHandler;
        _getProjectHandler = getProjectHandler;
        _getProjectsHandler = getProjectsHandler;
    }

    [HttpPost(Name = "CreateProject")]
    [EndpointSummary("Create a project")]
    [Consumes("application/json")]
    [ProducesResponseType(
        typeof(ProjectResponse),
        StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ProjectResponse>> CreateProjectAsync(
        CreateProjectRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var project = await _createProjectHandler.HandleAsync(
                request,
                cancellationToken);

            return Created(
                $"/api/projects/{project.Id}",
                project);
        }
        catch (ArgumentException exception)
        {
            return BadRequest(new
            {
                error = exception.Message
            });
        }
    }

    [HttpGet(Name = "GetProjects")]
    [EndpointSummary("List all projects")]
    [ProducesResponseType(
        typeof(IReadOnlyList<ProjectResponse>),
        StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<ProjectResponse>>>
        GetProjectsAsync(
            CancellationToken cancellationToken)
    {
        var projects = await _getProjectsHandler.HandleAsync(
            cancellationToken);

        return Ok(projects);
    }

    [HttpGet("{projectId:guid}", Name = "GetProject")]
    [EndpointSummary("Get project details")]
    [ProducesResponseType(
        typeof(ProjectResponse),
        StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ProjectResponse>> GetProjectAsync(
        Guid projectId,
        CancellationToken cancellationToken)
    {
        var project = await _getProjectHandler.HandleAsync(
            projectId,
            cancellationToken);

        if (project is null)
        {
            return NotFound(new
            {
                error = $"Project '{projectId}' was not found."
            });
        }

        return Ok(project);
    }
}
