using GeneLife;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Arch.Core;
using Arch.Core.Extensions;
using GeneLife.Core.Components.Characters;
using Genelife.Web.DTOs;
using System;
using System.Linq;
using GeneLife.Core.Commands;
using GeneLife.Core.Extensions;

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
        try {
            var res = simulation.SendCommand(new CreateCityCommand {  Size = TemplateCitySize.Small});
            return Ok(res);
        } catch (Exception ex) {
            return StatusCode(500, ex.Message);
        }
    }

    [HttpGet("state")]
    public ActionResult State()
    {
        var entities = simulation.GetAllLivingNPC();
        var npcs = entities.Select(entity => {
            var identity = entity.Get<Identity>().FullName();
            var living = entity.Get<Living>();
            var stats = new HumanStats {
                Hunger = living.Hunger.ToString(),
                Stamina = living.Stamina.ToString(),
                Thirst = living.Thirst.ToString(),
                Damage = living.Damage.ToString(),
            };
            var wallet = entity.Get<Wallet>().Money.ToString();
            return new Human { Wallet = wallet, Identity = identity, Stats = stats };
        });
        return Ok(new SimulationData {  Npcs = npcs.ToArray() });
    }
}
