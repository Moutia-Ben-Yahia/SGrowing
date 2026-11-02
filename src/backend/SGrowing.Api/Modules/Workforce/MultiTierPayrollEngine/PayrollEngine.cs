namespace SGrowing.Api.Modules.Workforce.MultiTierPayrollEngine;

public interface IPayrollEngine
{
    PayrollRunResult ProcessPayrollRun(PayrollRunRequest request);
}

public class PayrollEngine : IPayrollEngine
{
    // Progressive graduated monthly income tax brackets
    private static readonly List<TaxBracket> DefaultMonthlyTaxBrackets = new()
    {
        new(0m, 1000m, 0.0m, 0m),              // Tier 1: 0 - 1,000 -> 0%
        new(1000m, 3000m, 0.15m, 0m),          // Tier 2: 1,000 - 3,000 -> 15%
        new(3000m, 6000m, 0.25m, 300m),        // Tier 3: 3,000 - 6,000 -> 25% + 300
        new(6000m, decimal.MaxValue, 0.35m, 1050m) // Tier 4: > 6,000 -> 35% + 1,050
    };

    public PayrollRunResult ProcessPayrollRun(PayrollRunRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var timecardMap = request.Timecards.ToDictionary(t => t.EmployeeId, StringComparer.OrdinalIgnoreCase);
        var paySlips = new List<EmployeePaySlip>();

        decimal totalGross = 0.0m;
        decimal totalNet = 0.0m;
        decimal totalTax = 0.0m;
        decimal totalEmployer = 0.0m;

        foreach (var emp in request.Employees)
        {
            timecardMap.TryGetValue(emp.EmployeeId, out var tc);

            decimal basePay;
            decimal overtimePay = 0.0m;
            decimal bonus = tc?.BonusAmount ?? 0.0m;

            if (string.Equals(emp.EmploymentType, "FullTime", StringComparison.OrdinalIgnoreCase))
            {
                basePay = emp.MonthlyBaseSalary;
                if (tc != null)
                {
                    var hourlyEquivalent = emp.HourlyRate > 0 ? emp.HourlyRate : emp.MonthlyBaseSalary / 160.0m;
                    overtimePay += (decimal)tc.Overtime15xHours * hourlyEquivalent * 1.5m;
                    overtimePay += (decimal)tc.Overtime20xHours * hourlyEquivalent * 2.0m;
                }
            }
            else // PartTime or Contractor
            {
                var regularHours = tc?.RegularHoursWorked ?? 0.0;
                basePay = (decimal)regularHours * emp.HourlyRate;
                if (tc != null)
                {
                    overtimePay += (decimal)tc.Overtime15xHours * emp.HourlyRate * 1.5m;
                    overtimePay += (decimal)tc.Overtime20xHours * emp.HourlyRate * 2.0m;
                }
            }

            decimal grossPay = basePay + overtimePay + bonus;

            // Pre-tax deductions (Social Security Pension + Health)
            decimal empPension = 0.0m;
            decimal employerPension = 0.0m;
            if (emp.EnrolledInPension)
            {
                empPension = Math.Round(grossPay * (request.EmployeePensionRatePercent / 100.0m), 2);
                employerPension = Math.Round(grossPay * (request.EmployerPensionRatePercent / 100.0m), 2);
            }

            decimal empHealth = 0.0m;
            decimal employerHealth = 0.0m;
            if (emp.EnrolledInHealthInsurance)
            {
                empHealth = Math.Round(grossPay * (request.EmployeeHealthRatePercent / 100.0m), 2);
                employerHealth = Math.Round(grossPay * (request.EmployerHealthRatePercent / 100.0m), 2);
            }

            // Taxable income after statutory pension deduction and dependent allowance
            decimal personalExemption = emp.TaxExemptionsCount * 100.0m;
            decimal taxableIncome = Math.Max(0.0m, grossPay - empPension - personalExemption);

            // Progressive Tax Bracket Calculation
            decimal incomeTax = CalculateProgressiveTax(taxableIncome);

            decimal totalDeductions = empPension + empHealth + incomeTax;
            decimal netDisbursement = Math.Max(0.0m, grossPay - totalDeductions);
            decimal totalEmployerCost = grossPay + employerPension + employerHealth;

            totalGross += grossPay;
            totalNet += netDisbursement;
            totalTax += incomeTax;
            totalEmployer += (employerPension + employerHealth);

            paySlips.Add(new EmployeePaySlip(
                PaySlipId: $"SLIP-{Guid.NewGuid():N}",
                EmployeeId: emp.EmployeeId,
                FullName: emp.FullName,
                GrossPay: grossPay,
                BasePayComponent: basePay,
                OvertimePayComponent: overtimePay,
                BonusComponent: bonus,
                EmployeePensionDeduction: empPension,
                EmployeeHealthInsuranceDeduction: empHealth,
                IncomeTaxWithheld: incomeTax,
                TotalDeductions: totalDeductions,
                NetDisbursement: netDisbursement,
                EmployerPensionContribution: employerPension,
                EmployerHealthInsuranceContribution: employerHealth,
                TotalEmployerCost: totalEmployerCost
            ));
        }

        return new PayrollRunResult(
            PayrollBatchId: $"PAYROLL-{Guid.NewGuid():N}",
            TenantId: request.TenantId,
            ProcessedAtUtc: DateTime.UtcNow,
            TotalEmployeesProcessed: paySlips.Count,
            TotalGrossPay: totalGross,
            TotalNetDisbursements: totalNet,
            TotalIncomeTaxWithheld: totalTax,
            TotalEmployerContributions: totalEmployer,
            TotalCompanyPayrollCost: totalGross + totalEmployer,
            PaySlips: paySlips
        );
    }

    private static decimal CalculateProgressiveTax(decimal taxableIncome)
    {
        if (taxableIncome <= 0.0m) return 0.0m;

        foreach (var bracket in DefaultMonthlyTaxBrackets)
        {
            if (taxableIncome > bracket.MinTaxableIncome && taxableIncome <= bracket.MaxTaxableIncome)
            {
                var taxableInBracket = taxableIncome - bracket.MinTaxableIncome;
                return Math.Round(bracket.BaseTaxAmount + (taxableInBracket * bracket.MarginalTaxRate), 2);
            }
        }

        // Top tier
        var topBracket = DefaultMonthlyTaxBrackets[^1];
        var excess = taxableIncome - topBracket.MinTaxableIncome;
        return Math.Round(topBracket.BaseTaxAmount + (excess * topBracket.MarginalTaxRate), 2);
    }
}
