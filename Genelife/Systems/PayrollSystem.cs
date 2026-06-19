using Arch.Core;
using Arch.Core.Extensions;
using Genelife.Components;

namespace Genelife.Systems;

/// <summary>
/// System that pays employees their salary every X ticks
/// </summary>
public class PayrollSystem
{
    private readonly World _world;
    private readonly QueryDescription _queryDescription;
    private readonly int _payrollInterval;
    private int _tickCounter;

    public PayrollSystem(World world, int payrollInterval = 300)
    {
        _world = world;
        _payrollInterval = payrollInterval;
        _tickCounter = 0;
        _queryDescription = new QueryDescription().WithAll<Employee, Wallet>();
    }

    public void Update()
    {
        _tickCounter++;

        if (_tickCounter >= _payrollInterval)
        {
            _tickCounter = 0;
            ProcessPayroll();
        }
    }

    private void ProcessPayroll()
    {
        _world.Query(in _queryDescription, (ref Employee employee, ref Wallet wallet) =>
        {
            wallet.Balance += employee.Salary;
        });
    }
}
