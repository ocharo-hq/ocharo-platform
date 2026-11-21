using System.Xml.Linq;
using Microsoft.AspNetCore.DataProtection.Repositories;
using Npgsql;

namespace OcharoTrends.Api.Persistence;

internal sealed class TrendsKeyRepository(NpgsqlDataSource dataSource) : IXmlRepository
{
    public IReadOnlyCollection<XElement> GetAllElements()
    {
        using var command = dataSource.CreateCommand("SELECT xml FROM trends.data_protection_keys ORDER BY friendly_name");
        using var reader = command.ExecuteReader();
        var elements = new List<XElement>();
        while (reader.Read())
            elements.Add(XElement.Parse(reader.GetString(0)));
        return elements;
    }

    public void StoreElement(XElement element, string friendlyName)
    {
        using var command = dataSource.CreateCommand("""
            INSERT INTO trends.data_protection_keys (friendly_name, xml) VALUES ($1, $2)
            ON CONFLICT (friendly_name) DO NOTHING
            """);
        command.Parameters.AddWithValue(friendlyName);
        command.Parameters.AddWithValue(element.ToString(SaveOptions.DisableFormatting));
        command.ExecuteNonQuery();
    }
}
