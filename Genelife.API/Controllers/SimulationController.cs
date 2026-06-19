using Microsoft.AspNetCore.Mvc;
using Genelife.Api.Services;

namespace Genelife.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class SimulationController : ControllerBase
{
    private readonly SimulationManager _simulationManager;

    public SimulationController(SimulationManager simulationManager)
    {
        _simulationManager = simulationManager;
    }

    [HttpPost("start")]
    public IActionResult StartSimulation()
    {
        if (_simulationManager.IsRunning)
            return BadRequest(new { message = "Simulation is already running" });

        _simulationManager.StartSimulation();
        return Ok(new { message = "Simulation started" });
    }

    [HttpPost("stop")]
    public IActionResult StopSimulation()
    {
        if (!_simulationManager.IsRunning)
            return BadRequest(new { message = "Simulation is not running" });

        _simulationManager.StopSimulation();
        return Ok(new { message = "Simulation stopped" });
    }

    [HttpGet("status")]
    public IActionResult GetStatus()
    {
        return Ok(new
        {
            isRunning = _simulationManager.IsRunning,
            sims = _simulationManager.GetAllSims()
        });
    }

    [HttpPost("sims")]
    public IActionResult AddSim([FromBody] AddSimRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
            return BadRequest(new { message = "Name is required" });

        try
        {
            _simulationManager.AddSim(request.Name);
            return Ok(new { message = $"Sim '{request.Name}' added successfully" });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpGet("sims")]
    public IActionResult GetAllSims()
    {
        var sims = _simulationManager.GetAllSims();
        return Ok(sims);
    }
}

public class AddSimRequest
{
    public string Name { get; set; } = string.Empty;
}
