namespace SGrowing.Api.Modules.Workforce.MultiTierPayrollEngine;

public record EmployeePayrollProfile(
    string EmployeeId,
    string FullName,
    string Department,
    decimal MonthlyBaseSalary,
    decimal HourlyRate,
    string EmploymentType = "FullTime", // "FullTime", "PartTime", "Contractor"
    int TaxExemptionsCount = 0,
    bool EnrolledInPension = true,
    bool EnrolledInHealthInsurance = true
);

public record TimecardRecord(
    string EmployeeId,
    double RegularHoursWorked,
    double Overtime15xHours,  // Time-and-a-half
    double Overtime20xHours,  // Double-time (weekend/holiday)
    double UnpaidLeaveHours = 0.0,
    decimal BonusAmount = 0.0m
);

public record TaxBracket(
    decimal MinTaxableIncome,
    decimal MaxTaxableIncome,
    decimal MarginalTaxRate,
    decimal BaseTaxAmount
);

public record EmployeePaySlip(
    string PaySlipId,
    string EmployeeId,
    string FullName,
    decimal GrossPay,
    decimal BasePayComponent,
    decimal OvertimePayComponent,
    decimal BonusComponent,
    decimal EmployeePensionDeduction,
    decimal EmployeeHealthInsuranceDeduction,
    decimal IncomeTaxWithheld,
    decimal TotalDeductions,
    decimal NetDisbursement,
    decimal EmployerPensionContribution,
    decimal EmployerHealthInsuranceContribution,
    decimal TotalEmployerCost
);

public record PayrollRunRequest(
    string TenantId,
    DateTime PeriodStartDate,
    DateTime PeriodEndDate,
    List<EmployeePayrollProfile> Employees,
    List<TimecardRecord> Timecards,
    decimal EmployeePensionRatePercent = 5.0m,
    decimal EmployerPensionRatePercent = 10.0m,
    decimal EmployeeHealthRatePercent = 3.0m,
    decimal EmployerHealthRatePercent = 6.0m
);

public record PayrollRunResult(
    string PayrollBatchId,
    string TenantId,
    DateTime ProcessedAtUtc,
    int TotalEmployeesProcessed,
    decimal TotalGrossPay,
    decimal TotalNetDisbursements,
    decimal TotalIncomeTaxWithheld,
    decimal TotalEmployerContributions,
    decimal TotalCompanyPayrollCost,
    List<EmployeePaySlip> PaySlips
);
