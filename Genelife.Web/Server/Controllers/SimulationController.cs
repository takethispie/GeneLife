using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Genelife.Web.Server.Controllers;

[Route("[controller]")]
[ApiController]
public class SimulationController : ControllerBase
{
    [HttpGet("test")]
    public string Get()
    {
        return "test";
    }
}
