# CompanySaga Implementation

## Overview

The CompanySaga is a MassTransit state machine that manages companies with employees, handling automated hiring, payroll processing every 30 days, and work progress tracking that affects company revenue.

## Architecture

### Domain Models

#### Company
- **Id**: Unique identifier
- **Name**: Company name
- **Revenue**: Current company revenue
- **TaxRate**: Tax rate for payroll calculations (15-30%)
- **EmployeeIds**: List of employed Human IDs
- **Type**: CompanyType (Technology, Manufacturing, Services, Retail, Healthcare)
- **MinEmployees/MaxEmployees**: Employee capacity constraints

#### Employment
- **HumanId**: Reference to Human entity
- **CompanyId**: Reference to Company
- **Salary**: Employee salary
- **HireDate**: When the employee was hired
- **Status**: Employment status (Active, OnLeave, Terminated, Retired)
- **ProductivityScore**: Employee productivity (0.1 - 2.0)

### State Machine

#### States
- **Active**: Normal company operations
- **Payroll**: Processing payroll (every 30 days)
- **Hiring**: Actively seeking employees
- **WorkProgress**: Evaluating and updating productivity

#### Key Events
- **CreateCompany**: Initializes a new company saga
- **DayElapsed**: Triggers daily operations and payroll cycles
- **HireEmployee**: Adds an employee to the company
- **SalaryPaid**: Published when payroll is processed
- **EmployeeProductivityUpdated**: Updates individual employee productivity

## Features

### 1. Automated Hiring
Companies automatically evaluate hiring needs based on:
- Employee count below minimum threshold
- Low average productivity (< 0.7)
- High revenue allowing for expansion
- Maximum employee capacity constraints

### 2. Payroll Processing (Every 30 Days)
- Triggered automatically after 30 DayElapsed events
- Calculates gross salary, tax deductions, and net pay
- Updates company revenue (subtracts total payroll costs)
- Publishes SalaryPaid events for each employee
- Integrates with HumanSaga to update Human.Money

### 3. Work Progress Tracking
- Daily productivity updates for all employees
- Revenue calculation based on:
  - Company type (different base revenue rates)
  - Employee count and productivity
  - Operational costs (salaries + overhead)
- Productivity affects hiring decisions

### 4. Integration with Human Entities
- Employees are existing Human entities
- SalaryPaid events update Human.Money in HumanSaga
- Employment status can affect Human activities

## Usage Examples

### Creating a Company
```csharp
var company = CompanyGenerator.Generate(CompanyType.Technology);
var createEvent = new CreateCompany(company.Id, company);
await bus.Publish(createEvent);
```

### Hiring an Employee
```csharp
var hireEvent = new HireEmployee(companyId, humanId, 5000m);
await bus.Publish(hireEvent);
```

### Updating Productivity
```csharp
var productivityEvent = new EmployeeProductivityUpdated(companyId, humanId, 1.2m);
await bus.Publish(productivityEvent);
```

## Revenue Calculation

### Base Revenue per Employee per Day
- **Technology**: 500
- **Manufacturing**: 300
- **Services**: 400
- **Retail**: 250
- **Healthcare**: 600

### Formula
```
Daily Revenue = Employee Count × Base Revenue × Average Productivity
Daily Costs = (Total Salaries / 30) + (Employee Count × 50 overhead)
Revenue Change = Daily Revenue - Daily Costs
```

## Payroll Tax Calculation

```
Gross Salary = Employee.Salary
Tax Deducted = Gross Salary × Company.TaxRate
Net Salary = Gross Salary - Tax Deducted
```

## Configuration

### MassTransit Registration
```csharp
services.AddScoped<CalculatePayroll>();
services.AddScoped<EvaluateHiring>();
services.AddScoped<UpdateProductivity>();

x.AddSagaStateMachine<CompanySaga, CompanySagaState>(so => so.UseConcurrentMessageLimit(1))
    .MongoDbRepository(r =>
    {
        r.Connection = "mongodb://root:example@mongo:27017/";
        r.DatabaseName = "maindb";
    });
```

## Key Business Rules

1. **Payroll Cycle**: Exactly every 30 days
2. **Hiring Triggers**: 
   - Below minimum employees
   - Low productivity (< 0.7)
   - High revenue (> 50k with capacity)
3. **Productivity Range**: 0.1 to 2.0
4. **Tax Rate Range**: 15% to 30%
5. **Maximum Hiring**: 3 employees per hiring cycle

## Monitoring and Logging

The saga provides comprehensive logging for:
- Company creation and initialization
- Daily operations and revenue changes
- Hiring decisions and new employee onboarding
- Payroll processing and salary distributions
- Productivity updates and business metrics

## Integration Points

### With HumanSaga
- Consumes SalaryPaid events to update Human.Money
- Can correlate with Human activities for productivity calculation

### With Clock Service
- Listens to DayElapsed events for all time-based operations
- Maintains 30-day payroll cycles
- Triggers daily work progress updates

## File Structure

```
Genelife.Domain/
├── Company.cs
├── Employment.cs
├── Events/Company/
│   ├── CreateCompany.cs
│   ├── HireEmployee.cs
│   ├── EmployeeProductivityUpdated.cs
│   ├── PayrollCompleted.cs
│   └── SalaryPaid.cs
├── Commands/Company/
│   ├── StartHiring.cs
│   ├── ProcessPayroll.cs
│   └── UpdateWorkProgress.cs
└── Generators/
    └── CompanyGenerator.cs

Genelife.Main/
├── Sagas/
│   ├── CompanySaga.cs
│   └── CompanySagaState.cs
├── Usecases/
│   ├── CalculatePayroll.cs
│   ├── EvaluateHiring.cs
│   └── UpdateProductivity.cs
└── Examples/
    └── CompanyExample.cs
```

This implementation provides a comprehensive company management system that integrates seamlessly with the existing GeneLife simulation framework.