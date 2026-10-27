using Microsoft.EntityFrameworkCore;
using SGrowing.Api.Data;
using SGrowing.Api.Modules.CRM.Import;
using SGrowing.Core.Entities;
using Xunit;

namespace SGrowing.Tests.CRM;

public class CsvImportServiceTests
{
    private (SGrowingDbContext db, CsvImportService service) CreateContext()
    {
        var options = new DbContextOptionsBuilder<SGrowingDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        var db = new SGrowingDbContext(options);
        var service = new CsvImportService(db);
        return (db, service);
    }

    [Fact]
    public void PreviewCsv_ParsesQuotedMultilineAndHeaders()
    {
        // Arrange
        var (_, service) = CreateContext();
        var csv = """
            "First Name","Last Name","Work Email","Phone","Organization","Siret"
            "Alice","Smith","alice@acme.com","+15551234567","Acme Corp","9988112233"
            "Bob","Jones","bob@globex.org","+15559876543","Globex International","4455667788"
            """;

        // Act
        var preview = service.PreviewCsv(csv);

        // Assert
        Assert.Equal(2, preview.TotalRows);
        Assert.Equal(6, preview.DetectedHeaders.Count);
        Assert.Equal("Work Email", preview.DetectedHeaders[2]);
        Assert.Equal(2, preview.SampleRows.Count);
        Assert.Equal("alice@acme.com", preview.SampleRows[0]["Work Email"]);
        Assert.Empty(preview.PreviewErrors);
    }

    [Fact]
    public async Task ImportContacts_InsertsNewContacts_WithDynamicMapping()
    {
        // Arrange
        var (db, service) = CreateContext();
        var tenantId = Guid.NewGuid();
        var csv = """
            Email,First,Last,Mobile,Company,TaxNumber
            clara@saasventures.io,Clara,Oswald,+447700900123,SaaS Ventures,GB123456789
            david@stellarscale.net,David,Tennant,+447700900456,Stellar Scale,GB987654321
            """;

        var mapping = new CsvImportMapping(
            EmailColumn: "Email",
            FirstNameColumn: "First",
            LastNameColumn: "Last",
            PhoneColumn: "Mobile",
            CompanyColumn: "Company",
            TaxIdColumn: "TaxNumber"
        );

        // Act
        var result = await service.ImportContactsAsync(tenantId, csv, mapping, new CsvImportOptions());

        // Assert
        Assert.False(result.WasRolledBack);
        Assert.Equal(2, result.InsertedCount);
        Assert.Equal(0, result.ErrorCount);

        var saved = await db.Contacts.Where(c => c.TenantId == tenantId).ToListAsync();
        Assert.Equal(2, saved.Count);
        Assert.Contains(saved, c => c.Email == "clara@saasventures.io" && c.FirstName == "Clara" && c.Phone == "+447700900123");
    }

    [Fact]
    public async Task ImportContacts_DuplicateDetection_MergesBlankFieldsOrSkips()
    {
        // Arrange
        var (db, service) = CreateContext();
        var tenantId = Guid.NewGuid();

        // Existing contact in CRM with blank phone
        var existing = Contact.Create(tenantId, "Jean", "Luc", "jean.luc@starfleet.org");
        db.Contacts.Add(existing);
        await db.SaveChangesAsync();

        var csv = """
            Email,First,Last,Phone,Company
            jean.luc@starfleet.org,Jean-Luc,Picard,+33612345678,Starfleet Federation
            """;

        var mapping = new CsvImportMapping(
            EmailColumn: "Email",
            FirstNameColumn: "First",
            LastNameColumn: "Last",
            PhoneColumn: "Phone",
            CompanyColumn: "Company"
        );

        var options = new CsvImportOptions(MergeStrategy: DuplicateMergeStrategy.UpdateBlankFieldsOnly);

        // Act
        var result = await service.ImportContactsAsync(tenantId, csv, mapping, options);

        // Assert
        Assert.Equal(1, result.UpdatedCount);
        Assert.Equal(0, result.InsertedCount);

        var updated = await db.Contacts.FirstAsync(c => c.Id == existing.Id);
        Assert.Equal("+33612345678", updated.Phone);
        Assert.Equal("Jean", updated.FirstName); // Blank fields only: does not overwrite existing first name
    }

    [Fact]
    public async Task ImportContacts_BatchValidationRollback_TriggeredWhenErrorThresholdExceeded()
    {
        // Arrange
        var (db, service) = CreateContext();
        var tenantId = Guid.NewGuid();

        // 3 invalid emails out of 4 rows = 75% error rate
        var csv = """
            Email,First
            valid.lead@sgrowing.io,Marc
            not-an-email-1,Bad
            not-an-email-2,Bad
            not-an-email-3,Bad
            """;

        var mapping = new CsvImportMapping(EmailColumn: "Email", FirstNameColumn: "First");
        var options = new CsvImportOptions(
            RollbackOnError: true,
            MaxErrorPercentageTolerance: 20.0 // Allow at most 20% errors
        );

        // Act
        var result = await service.ImportContactsAsync(tenantId, csv, mapping, options);

        // Assert
        Assert.True(result.WasRolledBack);
        Assert.Equal(3, result.ErrorCount);
        Assert.Equal(0, result.InsertedCount);

        // Verify DB was not modified
        var contacts = await db.Contacts.Where(c => c.TenantId == tenantId).ToListAsync();
        Assert.Empty(contacts);
    }

    [Fact]
    public async Task ImportContacts_DuplicateDetectionByTaxId_MergesExistingContact()
    {
        // Arrange
        var (db, service) = CreateContext();
        var tenantId = Guid.NewGuid();

        var existing = Contact.Create(
            tenantId,
            firstName: "Enterprise",
            lastName: "Holdings",
            email: "old.inbox@enterprise.com",
            phone: "+33100000000",
            customFields: new Dictionary<string, object> { ["taxId"] = "FR99887766554" }
        );
        db.Contacts.Add(existing);
        await db.SaveChangesAsync();

        // CSV has NEW email, but IDENTICAL TaxId
        var csv = """
            Email,First,Last,Phone,TaxNumber
            new.inbox@enterprise.com,Enterprise,Holdings Corp,+33199999999,FR99887766554
            """;

        var mapping = new CsvImportMapping(
            EmailColumn: "Email",
            FirstNameColumn: "First",
            LastNameColumn: "Last",
            PhoneColumn: "Phone",
            TaxIdColumn: "TaxNumber"
        );

        var options = new CsvImportOptions(MergeStrategy: DuplicateMergeStrategy.OverwriteExisting);

        // Act
        var result = await service.ImportContactsAsync(tenantId, csv, mapping, options);

        // Assert
        Assert.Equal(1, result.UpdatedCount);
        Assert.Equal(0, result.InsertedCount);

        var totalContacts = await db.Contacts.Where(c => c.TenantId == tenantId).ToListAsync();
        Assert.Single(totalContacts); // Merged into existing, no duplicate created
        Assert.Equal("+33199999999", totalContacts[0].Phone);
    }

    [Fact]
    public async Task ImportContacts_Rollback_ClearsChangeTrackerDirtyState()
    {
        // Arrange
        var (db, service) = CreateContext();
        var tenantId = Guid.NewGuid();

        var existing = Contact.Create(tenantId, "OriginalName", "User", "original@sgrowing.io");
        db.Contacts.Add(existing);
        await db.SaveChangesAsync();

        // CSV tries to update existing contact, but has 80% error rate
        var csv = """
            Email,First
            original@sgrowing.io,HackedName
            bad-email-1,Bad
            bad-email-2,Bad
            bad-email-3,Bad
            bad-email-4,Bad
            """;

        var mapping = new CsvImportMapping(EmailColumn: "Email", FirstNameColumn: "First");
        var options = new CsvImportOptions(RollbackOnError: true, MaxErrorPercentageTolerance: 10.0);

        // Act
        var result = await service.ImportContactsAsync(tenantId, csv, mapping, options);

        // Assert
        Assert.True(result.WasRolledBack);
        Assert.False(db.ChangeTracker.HasChanges()); // ChangeTracker clean
        Assert.Equal("OriginalName", existing.FirstName);
    }

    [Fact]
    public void ParseCsvLines_HandlesTrailingNewlinesAndEscapes()
    {
        // Arrange
        var csv = "\"Col1\",\"Col2\"\r\n\"Val1\",\"Val2\"\r\n\r\n";

        // Act
        var rows = CsvImportService.ParseCsvLines(csv, ',');

        // Assert
        Assert.True(rows.Count >= 2);
        Assert.Equal("Col1", rows[0][0]);
        Assert.Equal("Val1", rows[1][0]);
    }
}
