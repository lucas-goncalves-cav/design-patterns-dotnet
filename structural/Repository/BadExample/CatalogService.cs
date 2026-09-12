using System.Data;

namespace DesignPatterns.Structural.Repository.BadExample;

/// <summary>
/// Business logic with SQL embedded in it.
///
/// This is not a strawman. Plenty of production code looks exactly like this,
/// and for a small application it ships and works.
///
/// The costs arrive later:
///
///   - The business rule and the storage mechanism are in the same method, so
///     neither can change without touching the other
///   - Testing "a discount cannot make the price zero" requires a database
///   - Column names are string literals, so renaming a column compiles fine
///     and fails at runtime
///   - Swapping SQL Server for anything else means rewriting business logic
///   - The same query, slightly different, appears in four other services
/// </summary>
public sealed class CatalogService
{
    private readonly IDbConnection _connection;

    public CatalogService(IDbConnection connection)
    {
        _connection = connection;
    }

    public void ApplyDiscount(Guid productId, decimal percentage)
    {
        if (percentage is <= 0 or >= 100)
        {
            throw new ArgumentOutOfRangeException(nameof(percentage), "Discount must be between 0 and 100.");
        }

        using var read = _connection.CreateCommand();
        read.CommandText = "SELECT Price, Active FROM Products WHERE Id = @Id";
        AddParameter(read, "@Id", productId);

        decimal currentPrice;
        bool active;

        using (var reader = read.ExecuteReader())
        {
            if (!reader.Read())
            {
                throw new InvalidOperationException($"Product {productId} was not found.");
            }

            currentPrice = reader.GetDecimal(0);
            active = reader.GetBoolean(1);
        }

        // The business rules, buried between two commands.
        if (!active)
        {
            throw new InvalidOperationException("An inactive product cannot be discounted.");
        }

        var newPrice = decimal.Round(currentPrice * (1 - percentage / 100m), 2);

        if (newPrice <= 0)
        {
            throw new InvalidOperationException("The discount would make the price zero or negative.");
        }

        using var update = _connection.CreateCommand();
        update.CommandText = "UPDATE Products SET Price = @Price WHERE Id = @Id";
        AddParameter(update, "@Price", newPrice);
        AddParameter(update, "@Id", productId);
        update.ExecuteNonQuery();
    }

    private static void AddParameter(IDbCommand command, string name, object value)
    {
        var parameter = command.CreateParameter();
        parameter.ParameterName = name;
        parameter.Value = value;
        command.Parameters.Add(parameter);
    }
}
