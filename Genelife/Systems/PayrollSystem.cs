using Arch.Core;
using Arch.Core.Extensions;
using Genelife.Components.Employment;
using Genelife.Components.Gen;

namespace Genelife.Systems;

/// <summary>
/// System that pays employees their salary every X ticks
/// </summary>
public class PayrollSystem
{
    private readonly World world;
    private readonly QueryDescription queryDescription;
    private readonly int payrollInterval;
    private int tickCounter;

    public PayrollSystem(World world, int payrollInterval = 300)
    {
        this.world = world;
        this.payrollInterval = payrollInterval;
        tickCounter = 0;
        queryDescription = new QueryDescription().WithAll<Employee, Wallet>();
    }

    public void Update()
    {
        tickCounter++;

        if (tickCounter >= payrollInterval)
        {
            tickCounter = 0;
            ProcessPayroll();
        }
    }

    private void ProcessPayroll()
    {
        world.Query(in queryDescription, (ref Employee employee, ref Wallet wallet) =>
        {
            wallet.Balance += employee.Salary;
        });
    }
}
