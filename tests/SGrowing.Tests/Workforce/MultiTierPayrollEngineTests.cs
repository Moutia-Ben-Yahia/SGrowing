using FluentAssertions;
using SGrowing.Api.Modules.Workforce.MultiTierPayrollEngine;
using Xunit;

namespace SGrowing.Tests.Workforce;

public class MultiTierPayrollEngineTests
{
    private readonly PayrollEngine _engine = new();

    [Fact]
    public void ProcessPayrollRun_ForStandardFullTimeEmployee_CalculatesCorrectNetAndDeductions()
    {
        var request = new PayrollRunRequest(
            TenantId: "tenant-erp-01",
            PeriodStartDate: new DateTime(2026, 11, 1),
            PeriodEndDate: new DateTime(2026, 11, 30),
            Employees: new List<EmployeePayrollProfile>
            {
                new("EMP-001", "Amine Ben Salem", "Engineering", MonthlyBaseSalary: 3000.00m, HourlyRate: 18.75m)
            },
            Timecards: new List<TimecardRecord>() // Standard salary, 0 overtime
        );

        var result = _engine.ProcessPayrollRun(request);

        result.Should().NotBeNull();
        result.TotalEmployeesProcessed.Should().Be(1);
        result.TotalGrossPay.Should().Be(3000.00m);

        var slip = result.PaySlips[0];
        // 5% employee pension = 150.00
        slip.EmployeePensionDeduction.Should().Be(150.00m);
        // 3% employee health = 90.00
        slip.EmployeeHealthInsuranceDeduction.Should().Be(90.00m);
        // Taxable income = 3000 - 150 = 2850 (Tier 2: 15% on 2850 - 1000 = 1850 * 0.15 = 277.50)
        slip.IncomeTaxWithheld.Should().Be(277.50m);
        // Total deductions = 150 + 90 + 277.50 = 517.50
        slip.TotalDeductions.Should().Be(517.50m);
        // Net disbursement = 3000 - 517.50 = 2482.50
        slip.NetDisbursement.Should().Be(2482.50m);
    }

    [Fact]
    public void ProcessPayrollRun_WithOvertimeHours_AppliesTimeAndAHalfAndDoubleTime()
    {
        var request = new PayrollRunRequest(
            TenantId: "tenant-erp-01",
            PeriodStartDate: new DateTime(2026, 11, 1),
            PeriodEndDate: new DateTime(2026, 11, 30),
            Employees: new List<EmployeePayrollProfile>
            {
                new("EMP-002", "Sarra Karray", "Operations", MonthlyBaseSalary: 2000.00m, HourlyRate: 12.50m)
            },
            Timecards: new List<TimecardRecord>
            {
                // 10 hours at 1.5x (10 * 12.50 * 1.5 = 187.50)
                // 5 hours at 2.0x (5 * 12.50 * 2.0 = 125.00)
                new("EMP-002", RegularHoursWorked: 160, Overtime15xHours: 10, Overtime20xHours: 5)
            }
        );

        var result = _engine.ProcessPayrollRun(request);

        var slip = result.PaySlips[0];
        slip.BasePayComponent.Should().Be(2000.00m);
        slip.OvertimePayComponent.Should().Be(312.50m); // 187.50 + 125.00
        slip.GrossPay.Should().Be(2312.50m);
    }

    [Fact]
    public void ProcessPayrollRun_ForPartTimeHourlyEmployee_CalculatesHourlyWages()
    {
        var request = new PayrollRunRequest(
            TenantId: "tenant-erp-01",
            PeriodStartDate: new DateTime(2026, 11, 1),
            PeriodEndDate: new DateTime(2026, 11, 30),
            Employees: new List<EmployeePayrollProfile>
            {
                new("EMP-003", "Yassine Dridi", "Customer Support", MonthlyBaseSalary: 0m, HourlyRate: 15.00m, EmploymentType: "PartTime")
            },
            Timecards: new List<TimecardRecord>
            {
                new("EMP-003", RegularHoursWorked: 80, Overtime15xHours: 0, Overtime20xHours: 0)
            }
        );

        var result = _engine.ProcessPayrollRun(request);

        var slip = result.PaySlips[0];
        // 80 hours * $15 = $1200
        slip.BasePayComponent.Should().Be(1200.00m);
        slip.GrossPay.Should().Be(1200.00m);
    }

    [Fact]
    public void ProcessPayrollRun_ComputesEmployerPensionAndHealthContributions()
    {
        var request = new PayrollRunRequest(
            TenantId: "tenant-erp-01",
            PeriodStartDate: new DateTime(2026, 11, 1),
            PeriodEndDate: new DateTime(2026, 11, 30),
            Employees: new List<EmployeePayrollProfile>
            {
                new("EMP-004", "Farah Trabelsi", "Finance", MonthlyBaseSalary: 4000.00m, HourlyRate: 25.00m)
            },
            Timecards: new List<TimecardRecord>(),
            EmployerPensionRatePercent: 10.0m,
            EmployerHealthRatePercent: 6.0m
        );

        var result = _engine.ProcessPayrollRun(request);

        var slip = result.PaySlips[0];
        // 10% employer pension = 400.00
        slip.EmployerPensionContribution.Should().Be(400.00m);
        // 6% employer health = 240.00
        slip.EmployerHealthInsuranceContribution.Should().Be(240.00m);
        // Total employer cost = 4000 + 400 + 240 = 4640.00
        slip.TotalEmployerCost.Should().Be(4640.00m);
    }

    [Fact]
    public void ProcessPayrollRun_AppliesTaxExemptionAllowancesForDependents()
    {
        var request = new PayrollRunRequest(
            TenantId: "tenant-erp-01",
            PeriodStartDate: new DateTime(2026, 11, 1),
            PeriodEndDate: new DateTime(2026, 11, 30),
            Employees: new List<EmployeePayrollProfile>
            {
                new("EMP-005A", "No Dependents", "Sales", MonthlyBaseSalary: 2500.00m, HourlyRate: 15.00m, TaxExemptionsCount: 0),
                new("EMP-005B", "3 Dependents", "Sales", MonthlyBaseSalary: 2500.00m, HourlyRate: 15.00m, TaxExemptionsCount: 3)
            },
            Timecards: new List<TimecardRecord>()
        );

        var result = _engine.ProcessPayrollRun(request);

        var slipA = result.PaySlips.First(s => s.EmployeeId == "EMP-005A");
        var slipB = result.PaySlips.First(s => s.EmployeeId == "EMP-005B");

        // Employee B with 3 dependents gets $300 tax exemption deduction, hence lower tax withheld
        slipB.IncomeTaxWithheld.Should().BeLessThan(slipA.IncomeTaxWithheld);
        slipB.NetDisbursement.Should().BeGreaterThan(slipA.NetDisbursement);
    }
}
