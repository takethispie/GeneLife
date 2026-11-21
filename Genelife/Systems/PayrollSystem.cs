using Arch.Core;
using Arch.Core.Extensions;
using Genelife.Components.Employment;
using Genelife.Components.Gen;

namespace Genelife.Systems;

/// <summary>
/// System that pays employees their salary every X ticks
/// </summary>
public class PayrollSystem : ISimulationSystem
{
    private readonly World world;
    private readonly QueryDescription queryDescription;
    private readonly int payrollInterval;

    public PayrollSystem(World world, int payrollInterval = 300)
    {
        this.world = world;
        this.payrollInterval = payrollInterval;
        queryDescription = new QueryDescription().WithAll<Employee, Wallet>();
    }

    public string Name => "Payroll";
    public int Order => 500;
    public Cadence Cadence => new(payrollInterval <= 0 ? 1 : payrollInterval);

    void ISimulationSystem.Update(in SimulationContext context) => Update();

    public void Update()
    {
        ProcessPayroll();
    }

    private void ProcessPayroll()
    {
        world.Query(in queryDescription, (ref Employee employee, ref Wallet wallet) =>
        {
            wallet.Balance += employee.Salary;
        });
    }
}
