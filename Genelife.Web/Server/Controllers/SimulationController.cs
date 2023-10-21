using Genelife.Web.Server.Hubs;
using Genelife.Web.Shared;
using GeneLife;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;

namespace Genelife.Web.Server.Controllers;

[Route("[controller]")]
[ApiController]
public class SimulationController : ControllerBase
{
    private readonly IHubContext<DataHub> hubContext;
    private readonly GeneLifeSimulation simulation;
    public SimulationController(GeneLifeSimulation simulation, IHubContext<DataHub> hubContext)
    {
        this.simulation = simulation;
        this.hubContext = hubContext;
    }

    [HttpGet("init")]
    public async Task<ActionResult> Initialize() { 
        simulation.Initialize();
        await hubContext.Clients.All.SendAsync(MessageType.SimulationLog, "Simulation Initialized");
        return Ok();
    }
}
