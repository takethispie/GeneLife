using GeneLife;
using Microsoft.AspNetCore.Mvc;
using Arch.Core;
using Arch.Core.Extensions;
using Genelife.Web.DTOs;
using System;
using System.Linq;
using GeneLife.Core.Commands;
using Genelife.Web.Services;
using GeneLife.Core.Components.Buildings;
using GeneLife.Core.Components;
using GeneLife.Survival.Components;
using GeneLife.Knowledge.Components;

namespace Genelife.Web.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class SimulationController : ControllerBase
    {
        private readonly GeneLifeSimulation simulation;
        private readonly ClockService clockService;

        public SimulationController(GeneLifeSimulation simulation, ClockService clockService)
        {
            this.simulation = simulation;
            this.clockService = clockService;
        }


        [HttpGet("init")]
        public ActionResult Initialize()
        {
            try
            {
                simulation.Initialize();
                clockService.Start();
                return Ok();
            }
            catch (Exception ex)
            {
                return StatusCode(500, ex.Message);
            }
        }


        [HttpGet("generate/smallcity")]
        public ActionResult GenerateSmallCity()
        {
            try
            {
                simulation.SendCommand(new CreateCityCommand { Size = TemplateCitySize.Small });
                return Ok();
            }
            catch (Exception ex)
            {
                return StatusCode(500, ex.Message);
            }
        }


        [HttpGet("state")]
        public ActionResult State()
        {
            var entities = simulation.GetAllLivingNPC();
            var buildingEntities = simulation.GetAllBuildings();
            var npcs = entities.Select(entity =>
            {
                var identity = entity.Get<Human>().FullName();
                var living = entity.Get<Living>();
                var stats = new HumanStats
                {
                    Hunger = living.Hunger.ToString(),
                    Stamina = living.Stamina.ToString(),
                    Thirst = living.Thirst.ToString(),
                    Damage = living.Damage.ToString(),
                };
                var position = entity.Get<Position>();
                var wallet = entity.Get<Human>().Money.ToString();
                return new HumanNPC
                {
                    Wallet = wallet,
                    Identity = identity,
                    Stats = stats,
                    Position = position.Coordinates.ToString(),
                };
            });
            var buildings = buildingEntities.Select(building =>
            {
                if (building.Has<HouseHold>())
                    return new Building(building.Id, building.Get<Adress>(), building.Get<Position>(), building.Get<HouseHold>());

                else if (building.Has<School>())
                    return new Building(building.Id, building.Get<Adress>(), building.Get<Position>(), building.Get<School>());

                else if (building.Has<Shop>())
                    return new Building(building.Id, building.Get<Adress>(), building.Get<Position>(), building.Get<Shop>());

                else throw new Exception("could not find matching type");
            });
            return Ok(new SimulationData { Npcs = npcs.ToArray(), Buildings = buildings.ToArray(), Logs = simulation.LogSystem.Logs.ToArray() });
        }


        [HttpGet("set/ticks/day/{ticks}")]
        public ActionResult SetTicksPerDay(int ticks) => Ok(simulation.SendCommand(new SetTicksPerDayCommand(ticks)));
    }
}