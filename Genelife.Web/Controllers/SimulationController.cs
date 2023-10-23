using GeneLife;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Arch.Core;
using Arch.Core.Extensions;
using GeneLife.Core.Components.Characters;
using Genelife.Web.DTOs;
using System;
using System.Linq;

namespace Genelife.Web.Controllers;
[Route("api/[controller]")]
[ApiController]
public class SimulationController : ControllerBase
{
    private readonly GeneLifeSimulation simulation;

    public SimulationController(GeneLifeSimulation  simulation)
    {
        this.simulation = simulation;
    }

    [HttpGet("init")]
    public ActionResult Initialize()
    {
        try {
            simulation.Initialize();
            return Ok();
        } catch (Exception ex) {
            return StatusCode(500, ex.Message);
        }
    }

    [HttpGet("generate/smallcity")]
    public ActionResult GenerateSmallCity() {

        return StatusCode(500);
    }

    [HttpGet("state")]
    public ActionResult State()
    {
        var entities = simulation.GetAllLivingNPC();
        return Ok(entities.Select(entity => {
            var identity = entity.Get<Identity>();
            var living = entity.Get<Living>();
            var wallet = entity.Get<Wallet>();
            return new Human {  Wallet = wallet, Identity = identity, Living = living };
        }));
    }
}
